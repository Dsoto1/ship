using System.Globalization;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Items;
using Expedition.Simulation.Tasks;

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
public sealed class SimulationRunner(CampaignState campaign, GameContent content)
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
    private void Start(Character character, TaskInstance order)
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
            var (spot, path, _) = FindWorkSpot(character, task);
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

        for (var i = 0; i < roster.Tasks.Count; i++)
        {
            var index = (roster.Next + i) % roster.Tasks.Count;
            var task = Content.Tasks[roster.Tasks[index]];
            if (Check(character, task, materialsAlreadyUsed: false).Succeeded)
            {
                roster.Next = (index + 1) % roster.Tasks.Count;
                Start(character, new TaskInstance(Campaign.Ids.NextTaskId(), character.Id, task.Id, TaskPriority.Normal) { Origin = TaskOrigin.Duty });
                return;
            }
        }
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
        StartNextQueued(character);
        StartOnOwnAccord(character);
        Campaign.ActiveTasks.TryGetValue(character.Id, out var task);
        var definition = task is null ? null : Content.Tasks[task.DefinitionId];
        character.Needs.Apply(Content.Rules.BaselineNeedsPerHour + (definition?.NeedsPerHour ?? NeedRates.None), HoursPerTick);
        RecordRates(character.Id, NeedLedger.Baseline, Content.Rules.BaselineNeedsPerHour);
        if (definition is not null)
        {
            RecordRates(character.Id, definition.Id, definition.NeedsPerHour);
        }

        EatAndDrink(character);
        SufferDeprivation(character, outcomes);

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

        task.Progress += WorkRate(character, definition) * SimulationClock.MinutesPerTick;

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
            // Finished work shows what someone can really do. Anyone in the campaign is assumed to work
            // in the captain's sight for now; distance and witnesses come with the port and ship scenes.
            if (definition.Skill is not null)
            {
                Campaign.Learn(character.Id, definition.Skill, character.Skills.Get(definition.Skill), Knowledge.KnowledgeSource.Observation);
            }

            if (definition.Attribute is not null)
            {
                Campaign.Learn(character.Id, definition.Attribute, character.Attributes.Get(definition.Attribute), Knowledge.KnowledgeSource.Observation);
            }

            Campaign.EndTask(character.Id);
            outcomes.Add(new TaskOutcome(Campaign.Clock.Tick, character.Id, definition.Id, TaskEnd.Completed, string.Empty));
        }
    }

    /// <summary>
    /// Eats or drinks one unit when a need reaches its threshold. Incapacitated characters are fed by
    /// others from the same supplies. A shortage is recorded once, and again only after it has been relieved.
    /// </summary>
    private void EatAndDrink(Character character)
    {
        var rules = Content.Rules;
        if (character.Needs.Hunger >= rules.EatAtHunger)
        {
            var food = TakeFirstAvailable(character, Content.Foods);
            if (food is not null)
            {
                var before = character.Needs.Hunger;
                character.Needs.Hunger = Needs.Clamp(character.Needs.Hunger - food.HungerRelief);
                Ledger(character.Id, TrackedNeed.Hunger, food.Id, character.Needs.Hunger - before);
                Campaign.FoodShortageRecorded = false;
            }
            else if (!Campaign.FoodShortageRecorded)
            {
                Campaign.FoodShortageRecorded = true;
                Campaign.Record(ChronicleEntryKind.SupplyShortage, [new(ChronicleParameters.Need, "food")], character.Id);
            }
        }

        if (character.Needs.Thirst >= rules.DrinkAtThirst)
        {
            var drink = TakeFirstAvailable(character, Content.Drinks);
            if (drink is not null)
            {
                var before = character.Needs.Thirst;
                character.Needs.Thirst = Needs.Clamp(character.Needs.Thirst - drink.ThirstRelief);
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
    private (WorkSpotDefinition? Spot, IReadOnlyList<GridPosition>? Path, string? Reason) FindWorkSpot(Character character, TaskDefinition task)
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

        var minutes = Math.Max(1, (long)Math.Ceiling((path.Count - 1) / Content.Rules.WalkCellsPerMinute));
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
