using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Tasks;

namespace Expedition.Simulation.World;

/// <summary>
/// Applies commands to a campaign and advances time. This is the only place where tasks start,
/// progress and end, and where needs change over time.
/// </summary>
/// <remarks>
/// Each tick, characters are processed in ascending ID order so results depend only on the
/// starting state and the seed. For each living character: needs change (baseline plus current
/// activity), the task is interrupted if the character is exhausted, otherwise work progresses,
/// hazards are rolled, and the task completes once its work is done.
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

        var missingTools = task.RequiredTools.Where(tool => Campaign.Supplies.Count(tool) == 0).ToList();
        if (missingTools.Count > 0)
        {
            return CommandResult.Rejected($"{task.Name} needs tools that are not on hand: {string.Join(", ", missingTools)}.");
        }

        if (!Campaign.Supplies.HasAll(task.Consumes))
        {
            var missing = task.Consumes.Where(pair => Campaign.Supplies.Count(pair.Key) < pair.Value)
                .Select(pair => $"{pair.Key} x{pair.Value}");
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

        Campaign.Supplies.TryRemoveAll(Content.Tasks[taskId].Consumes);
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

        if (task is null || definition is null)
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
        character.Health.Value = Math.Max(Health.Min, character.Health.Value - hazard.HealthDamage);
        character.Health.AddCondition(hazard.Condition);
        Campaign.Record(
            ChronicleEntryKind.Injury,
            $"{character.Name} suffered {hazard.Condition} while doing {task.Name} (-{hazard.HealthDamage} health, now {character.Health.Value}).",
            character.Id);

        if (character.Health.Value <= Content.Rules.IncapacitatedAtHealth)
        {
            var newState = character.Health.Value == Health.Min ? SurvivalState.Dead : SurvivalState.Incapacitated;
            var reason = $"{hazard.Condition} during {task.Name}";
            AddIfPresent(outcomes, Campaign.InterruptTask(character.Id, $"{character.Name} was injured: {reason}"));
            Campaign.ChangeSurvivalState(character.Id, newState, reason);
        }
    }

    private static void AddIfPresent(List<TaskOutcome> outcomes, TaskOutcome? outcome)
    {
        if (outcome is not null)
        {
            outcomes.Add(outcome);
        }
    }
}
