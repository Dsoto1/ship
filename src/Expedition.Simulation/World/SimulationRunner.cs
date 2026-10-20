using System.Globalization;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Items;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// Applies commands to a campaign and advances time. This is the only place where tasks start,
/// progress and end, where needs change over time, and where the crew eats and drinks.
/// </summary>
/// <remarks>
/// Each tick, characters are processed in ascending ID order so results depend only on the
/// starting state and the seed. For each living character:
/// needs change (baseline plus current activity); they eat or drink if they have reached the
/// threshold and something is available; starvation or dehydration costs health; then, if they are
/// working, the task is interrupted if they are exhausted, otherwise work progresses, hazards are
/// rolled, and the task completes once its work is done.
/// Characters take supplies from their own inventory first, then from the shared store.
/// </remarks>
public sealed partial class SimulationRunner(CampaignState campaign, GameContent content)
{
    private const double HoursPerTick = SimulationClock.MinutesPerTick / 60.0;

    /// <summary>The campaign being simulated.</summary>
    public CampaignState Campaign { get; } = campaign;

    /// <summary>Definitions and rules in use.</summary>
    public GameContent Content { get; } = content;

    /// <summary>What changed each character's needs recently. Diagnostic only; not saved.</summary>
    public NeedLedger NeedLedger { get; } = new();

    /// <summary>
    /// What changed <paramref name="need"/> for a character over the last day, largest effect first.
    /// Answers questions such as "why is Diego exhausted?".
    /// </summary>
    public IReadOnlyList<NeedChange> ExplainNeed(CharacterId characterId, TrackedNeed need) =>
        NeedLedger.Explain(characterId, need, Campaign.Clock.Tick);

    /// <summary>
    /// Checks whether <paramref name="characterId"/> could start <paramref name="taskId"/> now,
    /// without changing anything.
    /// </summary>
    public CommandResult CheckEligibility(CharacterId characterId, string taskId)
    {
        if (!Campaign.Characters.TryGetValue(characterId, out var character))
        {
            return CommandResult.Rejected($"No character with ID {characterId} in the campaign.");
        }

        if (!Content.Tasks.TryGetValue(taskId, out var task))
        {
            return CommandResult.Rejected($"Unknown task '{taskId}'.");
        }

        if (Campaign.ActiveTasks.TryGetValue(characterId, out var current))
        {
            return CommandResult.Rejected($"{character.Name} is already doing {current.DefinitionId}.");
        }

        return Check(character, task, materialsAlreadyUsed: false);
    }

    /// <summary>Starts a task now if the character is idle and eligible, using up its materials.</summary>
    public CommandResult AssignTask(CharacterId characterId, string taskId)
    {
        var eligibility = CheckEligibility(characterId, taskId);
        if (!eligibility.Succeeded)
        {
            return eligibility;
        }

        Start(Campaign.Characters[characterId], new TaskInstance(Campaign.Ids.NextTaskId(), characterId, taskId, TaskPriority.Normal));
        return CommandResult.Ok();
    }

    /// <summary>
    /// Gives an order. An idle character starts at once. Otherwise:
    /// a <see cref="TaskPriority.Normal"/> order waits at the end of the queue;
    /// a <see cref="TaskPriority.Urgent"/> order pauses normal work (which resumes later with its
    /// progress kept) and starts now, or waits behind other urgent work.
    /// Orders that could never be carried out (dead, unknown task, missing skill) are refused at once;
    /// other conditions, such as tools or a free work spot, are checked when the order starts.
    /// </summary>
    public CommandResult QueueTask(CharacterId characterId, string taskId, TaskPriority priority = TaskPriority.Normal)
    {
        var (result, startsNow) = EvaluateOrder(characterId, taskId, priority);
        if (!result.Succeeded)
        {
            return result;
        }

        var order = new TaskInstance(Campaign.Ids.NextTaskId(), characterId, taskId, priority);
        if (!startsNow)
        {
            Campaign.Enqueue(order, aheadOfSamePriority: false);
            return result;
        }

        if (Campaign.ActiveTasks.ContainsKey(characterId))
        {
            Campaign.PauseTask(characterId, taskId);
        }

        Start(Campaign.Characters[characterId], order);
        return result;
    }

    /// <summary>
    /// Whether <see cref="QueueTask"/> would accept this order, without changing anything. The interface
    /// uses it to show which orders are available and why others are not.
    /// </summary>
    public CommandResult CheckOrder(CharacterId characterId, string taskId, TaskPriority priority = TaskPriority.Normal) =>
        EvaluateOrder(characterId, taskId, priority).Result;

    private (CommandResult Result, bool StartsNow) EvaluateOrder(CharacterId characterId, string taskId, TaskPriority priority)
    {
        if (!Campaign.Characters.TryGetValue(characterId, out var character))
        {
            return (CommandResult.Rejected($"No character with ID {characterId} in the campaign."), false);
        }

        if (!Content.Tasks.TryGetValue(taskId, out var task))
        {
            return (CommandResult.Rejected($"Unknown task '{taskId}'."), false);
        }

        if (character.SurvivalState is SurvivalState.Dead or SurvivalState.Missing)
        {
            return (CommandResult.Rejected($"{character.Name} is {character.SurvivalState} and cannot take orders."), false);
        }

        if (task.Skill is not null && character.Skills.Get(task.Skill) < task.MinSkill)
        {
            return (CommandResult.Rejected($"{character.Name} lacks the skill: {task.Name} needs {SkillName(task.Skill)} {task.MinSkill}."), false);
        }

        Campaign.ActiveTasks.TryGetValue(characterId, out var current);
        var idle = current is null && !Campaign.QueueOf(characterId).Any(waiting => waiting.Origin == TaskOrigin.Order);
        var preempts = current is not null && priority > current.Priority;

        // Work the character took up themselves (a duty, or resting) gives way to any order of theirs.
        var overridesOwnWork = current is not null && current.Origin != TaskOrigin.Order && priority >= current.Priority;
        if (!character.CanWork || !(idle || preempts || overridesOwnWork))
        {
            return (CommandResult.Ok(), false);
        }

        // Starting now: every rule applies, as for AssignTask.
        var eligibility = Check(character, task, materialsAlreadyUsed: false);

        // If it cannot start yet over their own work, it waits instead of being refused.
        return !eligibility.Succeeded && !preempts && overridesOwnWork
            ? (CommandResult.Ok(), false)
            : (eligibility, eligibility.Succeeded);
    }

    /// <summary>
    /// Sets a character's standing duties: tasks they take up in turn whenever they are idle with no
    /// orders. An empty list clears them. Refused for unknown or repeated tasks, rest (which characters
    /// take of their own accord), tasks the character lacks the skill for, and the dead or missing.
    /// </summary>
    public CommandResult SetDuties(CharacterId characterId, IEnumerable<string> taskIds)
    {
        if (!Campaign.Characters.TryGetValue(characterId, out var character))
        {
            return CommandResult.Rejected($"No character with ID {characterId} in the campaign.");
        }

        if (character.SurvivalState is SurvivalState.Dead or SurvivalState.Missing)
        {
            return CommandResult.Rejected($"{character.Name} is {character.SurvivalState} and cannot take duties.");
        }

        var duties = taskIds.ToList();
        foreach (var taskId in duties)
        {
            if (!Content.Tasks.TryGetValue(taskId, out var task))
            {
                return CommandResult.Rejected($"Unknown task '{taskId}'.");
            }

            if (task.IsRest)
            {
                return CommandResult.Rejected($"{task.Name} is not a duty: everyone rests when they are tired.");
            }

            if (task.Skill is not null && character.Skills.Get(task.Skill) < task.MinSkill)
            {
                return CommandResult.Rejected($"{character.Name} lacks the skill: {task.Name} needs {SkillName(task.Skill)} {task.MinSkill}.");
            }
        }

        if (duties.Distinct().Count() != duties.Count)
        {
            return CommandResult.Rejected("A duty is listed twice.");
        }

        Campaign.SetDuties(characterId, duties);
        return CommandResult.Ok();
    }

    /// <summary>
    /// Why an idle character is not taking up any of their duties, or null if they would (or have none).
    /// For the interface, so an idle crew member never stands about without explanation.
    /// </summary>
    public string? WhyNoDuty(CharacterId characterId)
    {
        if (!Campaign.Characters.TryGetValue(characterId, out var character) || Campaign.DutiesOf(characterId) is not { Count: > 0 } duties)
        {
            return null;
        }

        var reasons = duties.Select(id => Check(character, Content.Tasks[id], materialsAlreadyUsed: false)).ToList();
        return reasons.Any(r => r.Succeeded) ? null : string.Join(" ", reasons.Select(r => r.Reason).Distinct());
    }

    /// <summary>Removes one waiting order. Used materials of a paused task are not refunded.</summary>
    public CommandResult RemoveQueuedTask(CharacterId characterId, TaskId taskId) =>
        Campaign.RemoveQueued(characterId, taskId)
            ? CommandResult.Ok()
            : CommandResult.Rejected($"{taskId} is not waiting in {characterId}'s queue.");

    /// <summary>Removes every waiting order for a character. The current task carries on.</summary>
    public CommandResult ClearQueue(CharacterId characterId)
    {
        foreach (var task in Campaign.QueueOf(characterId).ToList())
        {
            Campaign.RemoveQueued(characterId, task.Id);
        }

        return CommandResult.Ok();
    }

    /// <summary>On short rations, the hour water is served.</summary>
    public const int RationWaterHour = 7;

    /// <summary>On short rations, the hour food is served.</summary>
    public const int RationFoodHour = 12;

    /// <summary>Stress at or above which people work more slowly.</summary>
    public const double DisheartenedStress = 70;

    /// <summary>Need-ledger source for the strain of short rations.</summary>
    public const string RationingSource = "short rations";

    private static readonly NeedRates RationingStress = new() { Stress = 0.6 };

    private string ItemName(string id) => Content.Items.TryGetValue(id, out var item) ? item.Name : id;

    private string SkillName(string id) => Content.Skills.TryGetValue(id, out var skill) ? skill.Name : id;

    /// <summary>The eligibility rules shared by every way of starting work.</summary>
    private CommandResult Check(Character character, TaskDefinition task, bool materialsAlreadyUsed)
    {
        if (!character.CanWork)
        {
            return CommandResult.Rejected($"{character.Name} is {character.SurvivalState} and cannot work.");
        }

        if (!task.IsRest && character.Needs.Fatigue >= Content.Rules.MaxFatigueToStartWork)
        {
            return CommandResult.Rejected($"{character.Name} is too exhausted to work and needs rest.");
        }

        if (task.Skill is not null && character.Skills.Get(task.Skill) < task.MinSkill)
        {
            return CommandResult.Rejected(
                $"{character.Name} lacks the skill: {task.Name} needs {SkillName(task.Skill)} {task.MinSkill}.");
        }

        var missingTools = task.RequiredTools.Where(tool => Available(character, tool) == 0).ToList();
        if (missingTools.Count > 0)
        {
            return CommandResult.Rejected($"{task.Name} needs tools that are not on hand: {string.Join(", ", missingTools.Select(ItemName))}.");
        }

        if (!materialsAlreadyUsed)
        {
            var missing = task.Consumes.Where(pair => Available(character, pair.Key) < pair.Value)
                .Select(pair => $"{ItemName(pair.Key)} ×{pair.Value}")
                .ToList();
            if (missing.Count > 0)
            {
                return CommandResult.Rejected($"{task.Name} needs materials that are not on hand: {string.Join(", ", missing)}.");
            }
        }

        if (task.NeedsWorkSpot)
        {
            var (_, _, reason) = FindWorkSpot(character, task);
            if (reason is not null)
            {
                return CommandResult.Rejected(reason);
            }
        }

        return CommandResult.Ok();
    }

    /// <summary>Makes <paramref name="order"/> the active task. Eligibility has been checked.</summary>
    private void Start(Character character, TaskInstance order, string? preferredSpot = null)
    {
        var task = Content.Tasks[order.DefinitionId];
        if (!order.MaterialsConsumed)
        {
            foreach (var (itemId, quantity) in task.Consumes)
            {
                Take(character, itemId, quantity);
            }

            order.MaterialsConsumed = true;
        }

        if (task.NeedsWorkSpot)
        {
            var (spot, path, _) = FindWorkSpot(character, task, preferredSpot);
            order.SpotId = spot!.Id;
            StartWalk(character.Id, path!);
        }

        Campaign.StartTask(order);
    }

    /// <summary>
    /// Starts the next waiting order for an idle character. A character who is too tired waits rather than
    /// dropping work; an order that cannot start for any other reason is dropped and recorded.
    /// A character walking somewhere on the player's order finishes the walk first.
    /// </summary>
    private void StartNextQueued(Character character)
    {
        if (!character.CanWork || Campaign.ActiveTasks.ContainsKey(character.Id))
        {
            return;
        }

        if (Campaign.Placements.TryGetValue(character.Id, out var placement) && placement.IsMoving)
        {
            return;
        }

        while (Campaign.QueueOf(character.Id) is [var next, ..])
        {
            var task = Content.Tasks[next.DefinitionId];
            if (!task.IsRest && character.Needs.Fatigue >= Content.Rules.MaxFatigueToStartWork)
            {
                return;
            }

            Campaign.Dequeue(character.Id);
            var eligibility = Check(character, task, next.MaterialsConsumed);
            if (eligibility.Succeeded)
            {
                Start(character, next);
                return;
            }

            Campaign.Record(ChronicleEntryKind.TaskDropped, [
                new(ChronicleParameters.Task, next.DefinitionId), new(ChronicleParameters.Cause, eligibility.Reason),
            ], character.Id);
        }
    }

    /// <summary>
    /// What an idle character does with no orders waiting (TECHNICAL_SPEC.md §19.1): rest if tired
    /// (self-care, when the rules turn it on), otherwise take up the next standing duty that can start,
    /// in turn. Duties that cannot start (no rope, no free spot) are skipped silently; the interface
    /// asks <see cref="WhyNoDuty"/>.
    /// </summary>
    private void StartOnOwnAccord(Character character)
    {
        if (!character.CanWork || Campaign.ActiveTasks.ContainsKey(character.Id)
            || (Campaign.Placements.TryGetValue(character.Id, out var placement) && placement.IsMoving))
        {
            return;
        }

        if (Content.Rules.RestAtFatigue is { } restAt && character.Needs.Fatigue >= restAt
            && Content.Tasks.Values.Where(t => t.IsRest).OrderBy(t => t.Id, StringComparer.Ordinal).FirstOrDefault() is { } rest
            && Check(character, rest, materialsAlreadyUsed: false).Succeeded)
        {
            Start(character, new TaskInstance(Campaign.Ids.NextTaskId(), character.Id, rest.Id, TaskPriority.Normal) { Origin = TaskOrigin.SelfCare });
            return;
        }

        if (Campaign.QueueOf(character.Id).Count > 0 || !Campaign.Duties.TryGetValue(character.Id, out var roster))
        {
            return;
        }

        foreach (var task in roster.Tasks.Select(id => Content.Tasks[id]))
        {
            if (DutyNeeded(task) && Check(character, task, materialsAlreadyUsed: false).Succeeded)
            {
                Start(character, new TaskInstance(Campaign.Ids.NextTaskId(), character.Id, task.Id, TaskPriority.Normal) { Origin = TaskOrigin.Duty });
                return;
            }
        }
    }

    /// <summary>
    /// Whether a duty needs doing now (its <see cref="TaskDefinition.DutyWhen"/>): for example pumping only
    /// when the bilge is up, cooking only at meal times. Ship conditions only count at sea.
    /// </summary>
    public bool DutyNeeded(TaskDefinition task)
    {
        if (task.DutyWhen is not { } when)
        {
            return true;
        }

        if (when.BilgeAbove is not null || when.HullBelow is not null || when.RiggingBelow is not null)
        {
            if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship)
            {
                return false;
            }

            if ((when.BilgeAbove is { } bilge && ship.Bilge <= bilge)
                || (when.HullBelow is { } hull && ship.Hull >= hull)
                || (when.RiggingBelow is { } rigging && ship.Rigging >= rigging))
            {
                return false;
            }
        }

        var hour = Campaign.Clock.TimeOfDay.TotalHours;
        return when.StartBetween is not { } windows || windows.Any(w => hour >= w[0] && hour < w[1]);
    }

    /// <summary>
    /// A crew member on routine duty drops it when a duty higher in their list becomes needed and can start:
    /// the bilge rising pulls the watch to the pump. Checked every few minutes.
    /// </summary>
    private void ReconsiderDuty(Character character)
    {
        if (Campaign.Clock.Tick % 5 != 0 || !character.CanWork
            || !Campaign.ActiveTasks.TryGetValue(character.Id, out var current) || current.Origin != TaskOrigin.Duty
            || !Campaign.Duties.TryGetValue(character.Id, out var roster))
        {
            return;
        }

        var rank = roster.Tasks.ToList().IndexOf(current.DefinitionId);
        foreach (var task in roster.Tasks.Take(rank < 0 ? roster.Tasks.Count : rank).Select(id => Content.Tasks[id]))
        {
            if (DutyNeeded(task) && Check(character, task, materialsAlreadyUsed: false).Succeeded)
            {
                Campaign.AbandonTask(character.Id);
                Start(character, new TaskInstance(Campaign.Ids.NextTaskId(), character.Id, task.Id, TaskPriority.Normal) { Origin = TaskOrigin.Duty });
                return;
            }
        }
    }

    /// <summary>
    /// Starts a voyage: the expedition must be at sea (phase Voyage) and not already on one. The weather
    /// starts as the voyage rules say.
    /// </summary>
    public CommandResult StartVoyage(string shipName, string destination, double leagues)
    {
        if (Content.Voyage is not { } rules)
        {
            return CommandResult.Rejected("The game content has no voyage rules (data/voyage.json).");
        }

        if (Campaign.Phase != CampaignPhase.Voyage || Campaign.Voyage is not null || leagues <= 0)
        {
            return CommandResult.Rejected("A voyage can only start once, at sea, with a positive distance.");
        }

        var weather = rules.Weather(rules.StartingWeather);
        var until = Campaign.Clock.Tick + WeatherSpell(weather);
        Campaign.SetVoyage(new VoyageState(shipName, destination, leagues, weather.Id, until));
        return CommandResult.Ok();
    }

    private long WeatherSpell(WeatherDefinition weather) =>
        (weather.MinHours + Campaign.Randomness.Weather.NextInt(weather.MaxHours - weather.MinHours + 1)) * (long)SimulationClock.TicksPerHour;

    // One minute at sea: weather, leaks, wear, speed, alarms, and the end of the voyage.
    private void StepVoyage()
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship || Content.Voyage is not { } rules)
        {
            return;
        }

        StepStores();
        var weather = rules.Weather(ship.WeatherId);
        if (Campaign.Clock.Tick >= ship.WeatherUntilTick)
        {
            var roll = Campaign.Randomness.Weather.NextDouble() * weather.Next.Values.Sum();
            var next = weather.Next.OrderBy(n => n.Key, StringComparer.Ordinal).SkipWhile(n => (roll -= n.Value) > 0).Select(n => n.Key).FirstOrDefault()
                       ?? weather.Next.Keys.Order(StringComparer.Ordinal).Last();
            if (next != weather.Id)
            {
                Campaign.Record(ChronicleEntryKind.WeatherChanged, [new(ChronicleParameters.From, weather.Id), new(ChronicleParameters.To, next)]);
            }

            weather = rules.Weather(next);
            ship.WeatherId = next;
            ship.WeatherUntilTick = Campaign.Clock.Tick + WeatherSpell(weather);
        }

        var bilgeBefore = ship.Bilge;
        var plan = rules.Plan(ship.SailPlan);
        var leak = (rules.BaseLeakPerHour + (100 - ship.Hull) * rules.LeakPerHullPointLost) * weather.LeakMultiplier * plan.Leak;
        ship.Apply(new ShipEffects { Bilge = leak, Hull = -weather.HullWearPerHour * plan.Wear, Rigging = -weather.RiggingWearPerHour * plan.Wear }, HoursPerTick);
        if (weather.HullStrikeChancePerHour > 0 && Campaign.Randomness.Weather.Chance(weather.HullStrikeChancePerHour * plan.Wear * HoursPerTick))
        {
            ship.Apply(new ShipEffects { Hull = -weather.HullStrikeDamage }, 1);
            Campaign.Record(ChronicleEntryKind.HullStruck, [
                new(ChronicleParameters.Damage, Number(weather.HullStrikeDamage)), new(ChronicleParameters.Level, Number(ship.Hull)),
            ]);
        }

        ship.WatchKept = Campaign.ActiveTasks.Values.Any(task =>
            Content.Tasks[task.DefinitionId].KeepsWatch && Campaign.Characters[task.CharacterId].CanWork
            && !(Campaign.Placements.TryGetValue(task.CharacterId, out var placement) && placement.IsMoving));
        ship.SpeedLeaguesPerHour = weather.WindLeaguesPerHour
            * (rules.RiggingSpeedFloor + (1 - rules.RiggingSpeedFloor) * ship.Rigging / 100)
            * (ship.WatchKept ? 1 : rules.NoWatchSpeedMultiplier)
            * plan.Speed;
        ship.LeaguesSailed += ship.SpeedLeaguesPerHour * HoursPerTick;

        foreach (var level in rules.BilgeAlarms.Where(level => bilgeBefore < level && ship.Bilge >= level))
        {
            Campaign.Record(ChronicleEntryKind.BilgeAlarm, [new(ChronicleParameters.Level, Number(level))]);
        }

        if (ship.Bilge >= 100)
        {
            ship.Outcome = VoyageOutcome.Foundered;
            Campaign.Record(ChronicleEntryKind.Foundered, [new(ChronicleParameters.To, ship.ShipName)]);
            Campaign.TryTransitionTo(CampaignPhase.ShipwreckSurvival, $"{ship.ShipName} filled and foundered");
        }
        else if (ship.LeaguesRemaining <= 0)
        {
            ship.Outcome = VoyageOutcome.Arrived;
            Campaign.Record(ChronicleEntryKind.Landfall, [new(ChronicleParameters.To, ship.Destination)]);
            Campaign.TryTransitionTo(CampaignPhase.Landfall, $"sighted {ship.Destination}");
        }
    }

    /// <summary>
    /// The captain orders how much sail to carry: press on for speed at the cost of wear, or heave to and
    /// ride out heavy weather (D-018). Recorded in the chronicle.
    /// </summary>
    public CommandResult SetSailPlan(SailPlan plan)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship)
        {
            return CommandResult.Rejected("The ship is not under way.");
        }

        if (ship.SailPlan != plan)
        {
            ship.SailPlan = plan;
            Campaign.Record(ChronicleEntryKind.SailPlanChanged, [new(ChronicleParameters.To, plan.ToString())]);
        }

        return CommandResult.Ok();
    }

    /// <summary>
    /// How many more times each part of the ship can be repaired with what is in the shared store, from
    /// the repair tasks' materials: for example hull patches left from the oakum and planks aboard.
    /// </summary>
    public IReadOnlyDictionary<ShipPart, int> RepairsLeft()
    {
        var result = new Dictionary<ShipPart, int>();
        foreach (var task in Content.Tasks.Values.Where(t => t.ShipOnComplete is not null).OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            var part = task.ShipOnComplete!.Hull > 0 ? ShipPart.Hull : task.ShipOnComplete.Rigging > 0 ? ShipPart.Rigging : (ShipPart?)null;
            if (part is null)
            {
                continue;
            }

            var times = task.Consumes.Count == 0 ? int.MaxValue : task.Consumes.Min(pair => Campaign.SharedStore.Count(pair.Key) / pair.Value);
            result[part.Value] = result.TryGetValue(part.Value, out var existing) ? Math.Max(existing, times) : times;
        }

        return result;
    }

    /// <summary>
    /// Keeps someone in command. If the captain is dead or missing, the able crew member the others
    /// regard most highly (trust and respect, plus leadership) takes command as acting captain. This is a
    /// placeholder until the owner approves succession rules (TECHNICAL_SPEC.md §18.1). If no one is left
    /// alive aboard, the voyage ends with all hands lost and the campaign fails.
    /// </summary>
    private void CheckCommand()
    {
        var aboard = Campaign.Crew.Where(c => c.SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated).ToList();
        if (aboard.Count == 0)
        {
            if (Campaign.Voyage is { Outcome: VoyageOutcome.UnderWay } lost)
            {
                lost.Outcome = VoyageOutcome.AllHandsLost;
                Campaign.Record(ChronicleEntryKind.AllHandsLost, []);
                Campaign.TryTransitionTo(CampaignPhase.CampaignFailed, $"no one is left alive aboard {lost.ShipName}");
            }

            return;
        }

        if (Campaign.CaptainId is not { } captain || Campaign.Characters[captain].SurvivalState is not (SurvivalState.Dead or SurvivalState.Missing))
        {
            return;
        }

        double Standing(Character candidate) =>
            Campaign.Relationships.Values.Where(r => r.To == candidate.Id).Sum(r => r.Trust + r.Respect)
            + candidate.Skills.Get("skill.leadership") * 15;
        var successor = aboard.Where(c => c.CanWork).OrderByDescending(Standing).ThenBy(c => c.Id).FirstOrDefault();
        if (successor is not null)
        {
            Campaign.AppointCaptain(successor.Id, "the crew looked to them when the captain was lost");
        }
    }

    private static string Number(double value) => Math.Round(value).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Roughly how many days the food and the drink in the shared store will last the living crew at
    /// their everyday rate (work makes them hungrier and thirstier, so this errs on the long side).
    /// </summary>
    /// <param name="feeding">How many to feed, if not the crew as it is (in port: a full crew).</param>
    public (double FoodDays, double WaterDays) SupplyForecast(int? feeding = null)
    {
        var mouths = feeding ?? Campaign.Crew.Count(c => c.SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated);
        if (mouths == 0)
        {
            return (0, 0);
        }

        var store = Campaign.SharedStore;
        var food = Content.Foods.Sum(item => store.Count(item.Id) * item.HungerRelief);
        var drink = Content.Drinks.Sum(item => store.Count(item.Id) * item.ThirstRelief);
        var hungerPerDay = Math.Max(Content.Rules.BaselineNeedsPerHour.Hunger * 24 * 1.15, 0.01) * mouths;
        var thirstPerDay = Math.Max(Content.Rules.BaselineNeedsPerHour.Thirst * 24 * 1.15, 0.01) * mouths;
        return (food / hungerPerDay, drink / thirstPerDay);
    }

    /// <summary>How many days <paramref name="quantity"/> of an item feeds or waters <paramref name="mouths"/> people (0 for neither).</summary>
    public double DaysOf(string itemId, int quantity, int mouths)
    {
        if (mouths <= 0 || !Content.Items.TryGetValue(itemId, out var item))
        {
            return 0;
        }

        var rates = Content.Rules.BaselineNeedsPerHour;
        return item.ThirstRelief > 0 ? quantity * item.ThirstRelief / (Math.Max(rates.Thirst * 24 * 1.15, 0.01) * mouths)
            : item.HungerRelief > 0 ? quantity * item.HungerRelief / (Math.Max(rates.Hunger * 24 * 1.15, 0.01) * mouths)
            : 0;
    }

    /// <summary>Puts a character on a location's grid, for example when a scenario starts. Ends any walk.</summary>
    public CommandResult PlaceCharacter(CharacterId characterId, string locationId, GridPosition cell)
    {
        if (!Campaign.Characters.ContainsKey(characterId))
        {
            return CommandResult.Rejected($"No character with ID {characterId} in the campaign.");
        }

        if (!Content.Locations.TryGetValue(locationId, out var location))
        {
            return CommandResult.Rejected($"Unknown location '{locationId}'.");
        }

        if (!location.IsWalkable(cell))
        {
            return CommandResult.Rejected($"{cell} is not a walkable cell in {location.Name}.");
        }

        Campaign.SetPlacement(characterId, new CharacterPlacement(locationId, cell));
        return CommandResult.Ok();
    }

    /// <summary>
    /// Orders a character to walk to <paramref name="destination"/> in their current location, as in
    /// direct control. Any current task stops: the player has given a new order.
    /// </summary>
    public CommandResult MoveCharacter(CharacterId characterId, GridPosition destination)
    {
        if (!Campaign.Characters.TryGetValue(characterId, out var character))
        {
            return CommandResult.Rejected($"No character with ID {characterId} in the campaign.");
        }

        if (!character.CanWork)
        {
            return CommandResult.Rejected($"{character.Name} is {character.SurvivalState} and cannot walk.");
        }

        if (!Campaign.Placements.TryGetValue(characterId, out var placement))
        {
            return CommandResult.Rejected($"{character.Name} is not at any location.");
        }

        var location = Content.Locations[placement.LocationId];
        var path = Pathfinder.FindPath(location, placement.CellAt(Campaign.Clock.Tick), destination);
        if (path is null)
        {
            return CommandResult.Rejected($"{character.Name} cannot reach {destination}.");
        }

        Campaign.InterruptTask(characterId, InterruptReasons.Moved);
        StartWalk(characterId, path);
        return CommandResult.Ok();
    }

    /// <summary>Stops a character's task on the player's order. Materials already used are not refunded.</summary>
    /// <remarks>The next waiting order, if any, starts straight away.</remarks>
    public CommandResult CancelTask(CharacterId characterId)
    {
        if (Campaign.InterruptTask(characterId, InterruptReasons.Cancelled) is null)
        {
            return CommandResult.Rejected($"{characterId} has no task to cancel.");
        }

        StartNextQueued(Campaign.Characters[characterId]);
        return CommandResult.Ok();
    }

    /// <summary>Adds items to an inventory if they exist and fit, for example when buying provisions.</summary>
    public CommandResult AddItems(InventoryId inventoryId, string itemId, int quantity)
    {
        return Campaign.Inventories.TryGetValue(inventoryId, out var inventory)
            ? inventory.TryAdd(itemId, quantity, Content.Items)
            : CommandResult.Rejected($"No inventory with ID {inventoryId}.");
    }

    /// <summary>Moves items between two inventories. Either all of them move or none do.</summary>
    public CommandResult TransferItems(InventoryId from, InventoryId to, string itemId, int quantity)
    {
        if (!Campaign.Inventories.TryGetValue(from, out var source))
        {
            return CommandResult.Rejected($"No inventory with ID {from}.");
        }

        if (!Campaign.Inventories.TryGetValue(to, out var destination))
        {
            return CommandResult.Rejected($"No inventory with ID {to}.");
        }

        return Inventory.TryTransfer(source, destination, itemId, quantity, Content.Items);
    }

    /// <summary>Advances the campaign by <paramref name="ticks"/> ticks and reports every task that ended.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ticks"/> is negative.</exception>
    public IReadOnlyList<TaskOutcome> Advance(int ticks)
    {
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks), ticks, "Time cannot run backwards.");
        }

        var outcomes = new List<TaskOutcome>();
        for (var i = 0; i < ticks; i++)
        {
            Campaign.Clock.Step();
            foreach (var character in Campaign.Characters.Values.OrderBy(c => c.Id.Value))
            {
                StepCharacter(character, outcomes);
            }

            StepVoyage();
            StepPort();
            CheckCommand();
            ConsiderEvents();
        }

        return outcomes;
    }

    private void StepCharacter(Character character, List<TaskOutcome> outcomes)
    {
        // The dead and the missing are not simulated: the missing are somewhere the crew cannot see.
        if (character.SurvivalState is SurvivalState.Dead or SurvivalState.Missing)
        {
            return;
        }

        ResolveArrival(character.Id);
        var local = character.Membership == Membership.Local;
        if (local)
        {
            // Locals live by their schedule; their needs are not simulated (R1).
            FollowSchedule(character);
        }
        else
        {
            ReconsiderDuty(character);
            StartNextQueued(character);
            StartOnOwnAccord(character);
        }

        Campaign.ActiveTasks.TryGetValue(character.Id, out var task);
        var definition = task is null ? null : Content.Tasks[task.DefinitionId];
        if (local)
        {
            if (task is not null && definition is not null && !(Campaign.Placements.TryGetValue(character.Id, out var walking) && walking.IsMoving))
            {
                Work(character, task, definition, outcomes);
            }

            return;
        }

        character.Needs.Apply(Content.Rules.BaselineNeedsPerHour + (definition?.NeedsPerHour ?? NeedRates.None), HoursPerTick);
        RecordRates(character.Id, NeedLedger.Baseline, Content.Rules.BaselineNeedsPerHour);
        if (Campaign.Rationing)
        {
            character.Needs.Apply(RationingStress, HoursPerTick);
            RecordRates(character.Id, RationingSource, RationingStress);
        }
        if (definition is not null)
        {
            RecordRates(character.Id, definition.Id, definition.NeedsPerHour);
        }

        EatAndDrink(character);
        SufferDeprivation(character, outcomes);
        StepIllness(character, outcomes);

        if (task is null || definition is null || !Campaign.ActiveTasks.ContainsKey(character.Id))
        {
            return;
        }

        if (!definition.IsRest && character.Needs.Fatigue >= Content.Rules.ExhaustionFatigue)
        {
            AddIfPresent(outcomes, Campaign.InterruptTask(character.Id, InterruptReasons.Exhausted));
            return;
        }

        // Walking to the work spot: no work is done until the character arrives.
        if (Campaign.Placements.TryGetValue(character.Id, out var placement) && placement.IsMoving)
        {
            return;
        }

        Work(character, task, definition, outcomes);
    }

    // Progress, ship effects, hazards and completion for someone at their work.
    private void Work(Character character, TaskInstance task, TaskDefinition definition, List<TaskOutcome> outcomes)
    {
        var rate = WorkRate(character, definition);
        task.Progress += rate * SimulationClock.MinutesPerTick;
        if (definition.ShipPerHour is { } effects && Campaign.Voyage is { Outcome: VoyageOutcome.UnderWay } voyage)
        {
            voyage.Apply(effects, rate * HoursPerTick);
        }

        if (definition.Hazard is { } hazard && Campaign.Randomness.Tasks.Chance(hazard.ChancePerHour * HoursPerTick))
        {
            Injure(character, definition, hazard, outcomes);
            if (!Campaign.ActiveTasks.ContainsKey(character.Id))
            {
                return;
            }
        }

        if (task.Progress >= definition.DurationMinutes)
        {
            // Finished work shows what someone can really do, if the captain saw it. A demonstration the
            // captain asked for counts as a test.
            if (CaptainSees(character.Id))
            {
                var source = task.Origin == TaskOrigin.Order && character.Membership == Membership.Local
                    ? Knowledge.KnowledgeSource.Test
                    : Knowledge.KnowledgeSource.Observation;
                if (definition.Skill is not null)
                {
                    Campaign.Learn(character.Id, definition.Skill, character.Skills.Get(definition.Skill), source);
                }

                if (definition.Attribute is not null)
                {
                    Campaign.Learn(character.Id, definition.Attribute, character.Attributes.Get(definition.Attribute), source);
                }
            }

            Campaign.EndTask(character.Id);
            outcomes.Add(new TaskOutcome(Campaign.Clock.Tick, character.Id, definition.Id, TaskEnd.Completed, string.Empty));
            if (definition.ShipOnComplete is { } repair && Campaign.Voyage is { Outcome: VoyageOutcome.UnderWay } ship)
            {
                ship.Apply(repair, 1);
            }

            if (definition.CrewOnComplete is { } relief)
            {
                foreach (var shipmate in Campaign.Crew.Where(c => c.SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated))
                {
                    shipmate.Needs.Apply(relief, 1);
                }
            }

            // A loop of work goes straight on to its next step; a demonstration does not.
            if (definition.Then is { } next && task.Origin != TaskOrigin.Order && character.CanWork
                && Content.Tasks.TryGetValue(next, out var following) && FindWorkSpot(character, following).Spot is not null)
            {
                Start(character, new TaskInstance(Campaign.Ids.NextTaskId(), character.Id, next, task.Priority) { Origin = task.Origin });
            }
        }
    }

    /// <summary>
    /// Eats or drinks one unit when a need reaches its threshold. Incapacitated characters are fed by
    /// others from the same supplies. A shortage is recorded once, and again only after it has been relieved.
    /// </summary>
    private void EatAndDrink(Character character)
    {
        var rules = Content.Rules;

        // On short rations food and water are served once a day each (water at dawn, food at noon), about
        // half the usual. The stores last longer, but hunger and thirst climb day by day.
        var minuteOfDay = Campaign.Clock.Tick % SimulationClock.TicksPerDay;
        var mayEat = !Campaign.Rationing ? character.Needs.Hunger >= rules.EatAtHunger
            : minuteOfDay == RationFoodHour * SimulationClock.TicksPerHour && character.Needs.Hunger > 0;
        var mayDrink = !Campaign.Rationing ? character.Needs.Thirst >= rules.DrinkAtThirst
            : minuteOfDay == RationWaterHour * SimulationClock.TicksPerHour && character.Needs.Thirst > 0;
        if (mayEat)
        {
            var food = TakeFirstAvailable(character, Content.Foods);
            if (food is not null)
            {
                var before = character.Needs.Hunger;
                character.Needs.Hunger = Needs.Clamp(character.Needs.Hunger - food.HungerRelief);
                Sicken(character, food);
                Ledger(character.Id, TrackedNeed.Hunger, food.Id, character.Needs.Hunger - before);
                Campaign.FoodShortageRecorded = false;
            }
            else if (!Campaign.FoodShortageRecorded)
            {
                Campaign.FoodShortageRecorded = true;
                Campaign.Record(ChronicleEntryKind.SupplyShortage, [new(ChronicleParameters.Need, "food")], character.Id);
            }
        }

        if (mayDrink)
        {
            var drink = TakeFirstAvailable(character, Content.Drinks);
            if (drink is not null)
            {
                var before = character.Needs.Thirst;
                character.Needs.Thirst = Needs.Clamp(character.Needs.Thirst - drink.ThirstRelief);
                Sicken(character, drink);
                Ledger(character.Id, TrackedNeed.Thirst, drink.Id, character.Needs.Thirst - before);
                Campaign.WaterShortageRecorded = false;
            }
            else if (!Campaign.WaterShortageRecorded)
            {
                Campaign.WaterShortageRecorded = true;
                Campaign.Record(ChronicleEntryKind.SupplyShortage, [new(ChronicleParameters.Need, "water")], character.Id);
            }
        }
    }

    private void SufferDeprivation(Character character, List<TaskOutcome> outcomes)
    {
        var rules = Content.Rules;
        if (character.Needs.Thirst >= Needs.Max)
        {
            LoseHealth(character, rules.DehydrationHealthLossPerHour * HoursPerTick, ChronicleCauses.Dehydration);
            CheckHealthThresholds(character, ChronicleCauses.Dehydration, outcomes);
        }

        if (character.Needs.Hunger >= Needs.Max && character.SurvivalState != SurvivalState.Dead)
        {
            LoseHealth(character, rules.StarvationHealthLossPerHour * HoursPerTick, ChronicleCauses.Starvation);
            CheckHealthThresholds(character, ChronicleCauses.Starvation, outcomes);
        }
    }

    private void RecordRates(CharacterId id, string source, NeedRates rates)
    {
        Ledger(id, TrackedNeed.Hunger, source, rates.Hunger * HoursPerTick);
        Ledger(id, TrackedNeed.Thirst, source, rates.Thirst * HoursPerTick);
        Ledger(id, TrackedNeed.Fatigue, source, rates.Fatigue * HoursPerTick);
        Ledger(id, TrackedNeed.Stress, source, rates.Stress * HoursPerTick);
    }

    private void LoseHealth(Character character, double amount, string source)
    {
        var before = character.Health.Value;
        character.Health.Lose(amount);
        Ledger(character.Id, TrackedNeed.Health, source, character.Health.Value - before);
    }

    private void Ledger(CharacterId id, TrackedNeed need, string source, double amount) =>
        NeedLedger.Add(Campaign.Clock.Tick, id, need, source, amount);

    /// <summary>Work-minutes produced per in-game minute: skill and attribute multipliers, slowed when tired.</summary>
    private double WorkRate(Character character, TaskDefinition task)
    {
        if (task.IsRest)
        {
            return 1.0;
        }

        var rules = Content.Rules;
        var level = task.Skill is null ? Skills.RoutineLevel : character.Skills.Get(task.Skill);
        var rate = rules.WorkRateBySkillLevel[level];
        if (task.Attribute is not null)
        {
            rate *= rules.WorkRateByAttributeLevel[character.Attributes.Get(task.Attribute)];
        }

        // Disheartened people work more slowly.
        if (character.Needs.Stress >= DisheartenedStress)
        {
            rate *= 0.85;
        }

        return character.Needs.Fatigue >= rules.TiredFatigue ? rate * rules.TiredWorkRateMultiplier : rate;
    }

    private void Injure(Character character, TaskDefinition task, TaskHazard hazard, List<TaskOutcome> outcomes)
    {
        LoseHealth(character, hazard.HealthDamage, hazard.Condition);
        character.Health.AddCondition(hazard.Condition);
        Campaign.Record(ChronicleEntryKind.Injury, [
            new(ChronicleParameters.Condition, hazard.Condition),
            new(ChronicleParameters.Task, task.Id),
            new(ChronicleParameters.Damage, hazard.HealthDamage.ToString(CultureInfo.InvariantCulture)),
            new(ChronicleParameters.Health, character.Health.Value.ToString("0.#", CultureInfo.InvariantCulture)),
        ], character.Id);
        CheckHealthThresholds(character, hazard.Condition, outcomes);
    }

    /// <summary>Incapacitates a character at low health and kills them at zero, interrupting their work.</summary>
    private void CheckHealthThresholds(Character character, string cause, List<TaskOutcome> outcomes)
    {
        SurvivalState? newState = character.Health.Value <= Health.Min ? SurvivalState.Dead
            : character.Health.Value <= Content.Rules.IncapacitatedAtHealth && character.SurvivalState == SurvivalState.Alive
                ? SurvivalState.Incapacitated
                : null;
        if (newState is null || newState == character.SurvivalState)
        {
            return;
        }

        AddIfPresent(outcomes, Campaign.InterruptTask(character.Id, InterruptReasons.Incapacitated, [
            new(ChronicleParameters.To, newState.Value.ToString()), new(ChronicleParameters.Cause, cause),
        ]));
        Campaign.ChangeSurvivalState(character.Id, newState.Value, cause);
    }

    /// <summary>
    /// The nearest free work spot for <paramref name="task"/> in the character's location, with the path
    /// to it; or a reason why there is none. Ties go to the spot listed first.
    /// </summary>
    private (WorkSpotDefinition? Spot, IReadOnlyList<GridPosition>? Path, string? Reason) FindWorkSpot(Character character, TaskDefinition task, string? preferredSpot = null)
    {
        if (!Campaign.Placements.TryGetValue(character.Id, out var placement))
        {
            return (null, null, $"{character.Name} is not at any location, and {task.Name} must be done at a work spot.");
        }

        var location = Content.Locations[placement.LocationId];
        var spots = location.Spots.Where(s => s.Tasks.Contains(task.Id)).ToList();
        if (spots.Count == 0)
        {
            return (null, null, $"There is nowhere to {task.Name.ToLowerInvariant()} in {location.Name}.");
        }

        var taken = Campaign.ActiveTasks.Values.Select(t => t.SpotId).Where(id => id is not null).ToHashSet();
        var free = spots.Where(s => !taken.Contains(s.Id)).ToList();
        if (free.Count == 0)
        {
            return (null, null, $"Every place to {task.Name.ToLowerInvariant()} is already in use.");
        }

        var start = placement.CellAt(Campaign.Clock.Tick);
        if (preferredSpot is not null && free.FirstOrDefault(s => s.Id == preferredSpot) is { } preferred
            && Pathfinder.FindPath(location, start, preferred.Cell) is { } preferredPath)
        {
            return (preferred, preferredPath, null);
        }

        var best = free
            .Select(spot => (Spot: spot, Path: Pathfinder.FindPath(location, start, spot.Cell)))
            .Where(candidate => candidate.Path is not null)
            .OrderBy(candidate => candidate.Path!.Count)
            .FirstOrDefault();
        return best.Spot is null
            ? (null, null, $"{character.Name} cannot reach anywhere to {task.Name.ToLowerInvariant()}.")
            : (best.Spot, best.Path, null);
    }

    /// <summary>Starts walking along <paramref name="path"/>; a one-cell path means already there.</summary>
    private void StartWalk(CharacterId characterId, IReadOnlyList<GridPosition> path)
    {
        var placement = Campaign.Placements[characterId];
        var now = Campaign.Clock.Tick;
        placement.Cell = placement.CellAt(now);
        if (path.Count < 2)
        {
            placement.Movement = null;
            return;
        }

        var speed = Content.Locations[placement.LocationId].WalkCellsPerMinute ?? Content.Rules.WalkCellsPerMinute;
        var minutes = Math.Max(1, (long)Math.Ceiling((path.Count - 1) / speed));
        placement.Movement = new Movement(path, now, now + minutes / SimulationClock.MinutesPerTick);
    }

    private void ResolveArrival(CharacterId characterId)
    {
        if (Campaign.Placements.TryGetValue(characterId, out var placement)
            && placement.Movement is { } movement
            && Campaign.Clock.Tick >= movement.ArriveTick)
        {
            placement.Cell = movement.Destination;
            placement.Movement = null;
        }
    }

    /// <summary>Quantity of an item the character can reach: their own plus the shared store.</summary>
    private int Available(Character character, string itemId) =>
        Campaign.Inventories[character.InventoryId].Count(itemId) + Campaign.SharedStore.Count(itemId);

    /// <summary>Removes items, taking from the character's own inventory first. Caller has checked availability.</summary>
    private void Take(Character character, string itemId, int quantity)
    {
        var personal = Campaign.Inventories[character.InventoryId];
        var fromPersonal = Math.Min(quantity, personal.Count(itemId));
        if (fromPersonal > 0)
        {
            personal.TryRemove(itemId, fromPersonal);
        }

        if (quantity > fromPersonal)
        {
            Campaign.SharedStore.TryRemove(itemId, quantity - fromPersonal);
        }
    }

    private ItemDefinition? TakeFirstAvailable(Character character, IReadOnlyList<ItemDefinition> candidates)
    {
        foreach (var item in candidates)
        {
            if (Available(character, item.Id) > 0)
            {
                Take(character, item.Id, 1);
                return item;
            }
        }

        return null;
    }

    private static void AddIfPresent(List<TaskOutcome> outcomes, TaskOutcome? outcome)
    {
        if (outcome is not null)
        {
            outcomes.Add(outcome);
        }
    }
}
