using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Tasks;

namespace Expedition.Simulation.Tests;

/// <summary>Small, hand-checked content so test arithmetic is easy to follow.</summary>
internal static class TestContent
{
    public static SimulationRules Rules { get; } = new()
    {
        BaselineNeedsPerHour = new NeedRates { Hunger = 0.5, Thirst = 1.0, Fatigue = 1.0 },
        WorkRateBySkillLevel = [0.5, 0.75, 1.0, 1.25, 1.5, 1.75],
        MaxFatigueToStartWork = 90,
        ExhaustionFatigue = 100,
        TiredFatigue = 75,
        TiredWorkRateMultiplier = 0.5,
        IncapacitatedAtHealth = 25,
    };

    public static TaskDefinition Rest { get; } = new()
    {
        Id = "task.rest", Name = "Rest", DurationMinutes = 240, IsRest = true,
        NeedsPerHour = new NeedRates { Fatigue = -9 },
    };

    public static TaskDefinition Pump { get; } = new()
    {
        Id = "task.pump", Name = "Pump", DurationMinutes = 60,
        NeedsPerHour = new NeedRates { Fatigue = 6 },
    };

    /// <summary>Long, tiring work for exhaustion tests: 10 fatigue per hour including baseline.</summary>
    public static TaskDefinition Haul { get; } = new()
    {
        Id = "task.haul", Name = "Haul cargo", DurationMinutes = 600,
        NeedsPerHour = new NeedRates { Fatigue = 9 },
    };

    public static TaskDefinition RepairRigging { get; } = new()
    {
        Id = "task.repair_rigging", Name = "Repair rigging", DurationMinutes = 120,
        Skill = "skill.rigging", MinSkill = 1,
        RequiredTools = ["item.marlinspike"],
        Consumes = new Dictionary<string, int> { ["item.rope"] = 2 },
    };

    /// <summary>Work in a storm: an injury is expected about once an hour.</summary>
    public static TaskDefinition StormWork { get; } = new()
    {
        Id = "task.storm_work", Name = "Secure the yard", DurationMinutes = 600,
        NeedsPerHour = new NeedRates { Fatigue = 3, Stress = 2 },
        Hazard = new TaskHazard { ChancePerHour = 1.0, HealthDamage = 20, Condition = "condition.bruised" },
    };

    public static GameContent Create() => new(
        Rules,
        [new SkillDefinition { Id = "skill.rigging", Name = "Rigging", Group = "maritime" }],
        [Rest, Pump, Haul, RepairRigging, StormWork]);
}
