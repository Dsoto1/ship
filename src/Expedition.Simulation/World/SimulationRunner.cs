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

        if (!character.CanWork)
        {
            return CommandResult.Rejected($"{character.Name} is {character.SurvivalState} and cannot work.");
        }

        if (Campaign.ActiveTasks.TryGetValue(characterId, out var current))
        {
            return CommandResult.Rejected($"{character.Name} is already doing {current.DefinitionId}.");
        }

        if (!task.IsRest && character.Needs.Fatigue >= Content.Rules.MaxFatigueToStartWork)
        {
            return CommandResult.Rejected($"{character.Name} is too exhausted to work and needs rest.");
        }

        if (task.Skill is not null && character.Skills.Get(task.Skill) < task.MinSkill)
        {
            return CommandResult.Rejected(
                $"{character.Name} lacks the skill: {task.Name} needs {task.Skill} {task.MinSkill}.");
        }

        var missingTools = task.RequiredTools.Where(tool => Available(character, tool) == 0).ToList();
        if (missingTools.Count > 0)
        {
            return CommandResult.Rejected($"{task.Name} needs tools that are not on hand: {string.Join(", ", missingTools)}.");
        }

        var missing = task.Consumes.Where(pair => Available(character, pair.Key) < pair.Value)
            .Select(pair => $"{pair.Key} x{pair.Value}")
            .ToList();
        if (missing.Count > 0)
        {
            return CommandResult.Rejected($"{task.Name} needs materials that are not on hand: {string.Join(", ", missing)}.");
        }

        return CommandResult.Ok();
    }

    /// <summary>Starts a task if the character is eligible, using up its materials.</summary>
    public CommandResult AssignTask(CharacterId characterId, string taskId)
    {
        var eligibility = CheckEligibility(characterId, taskId);
        if (!eligibility.Succeeded)
        {
            return eligibility;
        }

        var character = Campaign.Characters[characterId];
        foreach (var (itemId, quantity) in Content.Tasks[taskId].Consumes)
        {
            Take(character, itemId, quantity);
        }

        Campaign.StartTask(new TaskInstance(characterId, taskId, Campaign.Clock.Tick));
        return CommandResult.Ok();
    }

    /// <summary>Stops a character's task on the player's order. Materials already used are not refunded.</summary>
    public CommandResult CancelTask(CharacterId characterId, string reason = "ordered to stop")
    {
        return Campaign.InterruptTask(characterId, reason) is null
            ? CommandResult.Rejected($"{characterId} has no task to cancel.")
            : CommandResult.Ok();
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

        Campaign.ActiveTasks.TryGetValue(character.Id, out var task);
        var definition = task is null ? null : Content.Tasks[task.DefinitionId];
        character.Needs.Apply(Content.Rules.BaselineNeedsPerHour + (definition?.NeedsPerHour ?? NeedRates.None), HoursPerTick);

        EatAndDrink(character);
        SufferDeprivation(character, outcomes);

        if (task is null || definition is null || !Campaign.ActiveTasks.ContainsKey(character.Id))
        {
            return;
        }

        if (!definition.IsRest && character.Needs.Fatigue >= Content.Rules.ExhaustionFatigue)
        {
            AddIfPresent(outcomes, Campaign.InterruptTask(character.Id, $"{character.Name} is exhausted"));
            return;
        }

        task.Progress += WorkRate(character, definition) * SimulationClock.MinutesPerTick;

        if (definition.Hazard is { } hazard && Campaign.Random.Chance(hazard.ChancePerHour * HoursPerTick))
        {
            Injure(character, definition, hazard, outcomes);
            if (!Campaign.ActiveTasks.ContainsKey(character.Id))
            {
                return;
            }
        }

        if (task.Progress >= definition.DurationMinutes)
        {
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
                character.Needs.Hunger = Needs.Clamp(character.Needs.Hunger - food.HungerRelief);
                Campaign.FoodShortageRecorded = false;
            }
            else if (!Campaign.FoodShortageRecorded)
            {
                Campaign.FoodShortageRecorded = true;
                Campaign.Record(ChronicleEntryKind.SupplyShortage, $"{character.Name} went hungry: there is no food left.", character.Id);
            }
        }

        if (character.Needs.Thirst >= rules.DrinkAtThirst)
        {
            var drink = TakeFirstAvailable(character, Content.Drinks);
            if (drink is not null)
            {
                character.Needs.Thirst = Needs.Clamp(character.Needs.Thirst - drink.ThirstRelief);
                Campaign.WaterShortageRecorded = false;
            }
            else if (!Campaign.WaterShortageRecorded)
            {
                Campaign.WaterShortageRecorded = true;
                Campaign.Record(ChronicleEntryKind.SupplyShortage, $"{character.Name} went thirsty: there is nothing left to drink.", character.Id);
            }
        }
    }

    private void SufferDeprivation(Character character, List<TaskOutcome> outcomes)
    {
        var rules = Content.Rules;
        if (character.Needs.Thirst >= Needs.Max)
        {
            character.Health.Lose(rules.DehydrationHealthLossPerHour * HoursPerTick);
            CheckHealthThresholds(character, "dehydration", outcomes);
        }

        if (character.Needs.Hunger >= Needs.Max && character.SurvivalState != SurvivalState.Dead)
        {
            character.Health.Lose(rules.StarvationHealthLossPerHour * HoursPerTick);
            CheckHealthThresholds(character, "starvation", outcomes);
        }
    }

    /// <summary>Work-minutes produced per in-game minute: skill multiplier, slowed when tired.</summary>
    private double WorkRate(Character character, TaskDefinition task)
    {
        if (task.IsRest)
        {
            return 1.0;
        }

        var rules = Content.Rules;
        var level = task.Skill is null ? Skills.RoutineLevel : character.Skills.Get(task.Skill);
        var rate = rules.WorkRateBySkillLevel[level];
        return character.Needs.Fatigue >= rules.TiredFatigue ? rate * rules.TiredWorkRateMultiplier : rate;
    }

    private void Injure(Character character, TaskDefinition task, TaskHazard hazard, List<TaskOutcome> outcomes)
    {
        character.Health.Lose(hazard.HealthDamage);
        character.Health.AddCondition(hazard.Condition);
        Campaign.Record(
            ChronicleEntryKind.Injury,
            $"{character.Name} suffered {hazard.Condition} while doing {task.Name} (-{hazard.HealthDamage} health, now {character.Health.Value:0.#}).",
            character.Id);
        CheckHealthThresholds(character, $"{hazard.Condition} during {task.Name}", outcomes);
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

        AddIfPresent(outcomes, Campaign.InterruptTask(character.Id, $"{character.Name} was struck down by {cause}"));
        Campaign.ChangeSurvivalState(character.Id, newState.Value, cause);
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
