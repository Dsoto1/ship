using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Items;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Small, hand-checked content so test arithmetic is easy to follow.</summary>
internal static class TestContent
{
    public static SimulationRules Rules { get; } = new()
    {
        BaselineNeedsPerHour = new NeedRates { Hunger = 0.5, Thirst = 1.0, Fatigue = 1.0 },
        WorkRateBySkillLevel = [0.5, 0.75, 1.0, 1.25, 1.5, 1.75],
        WorkRateByAttributeLevel = [0.5, 0.75, 1.0, 1.25, 1.5, 1.75],
        MaxFatigueToStartWork = 90,
        ExhaustionFatigue = 100,
        TiredFatigue = 75,
        TiredWorkRateMultiplier = 0.5,
        IncapacitatedAtHealth = 25,
        EatAtHunger = 30,
        DrinkAtThirst = 30,
        StarvationHealthLossPerHour = 1.0,
        DehydrationHealthLossPerHour = 4.0,
        WalkCellsPerMinute = 4,
        PersonalCarryCapacity = new InventoryCapacity { MaxWeightKg = 20, MaxVolumeLitres = 30 },
    };

    public static IReadOnlyList<ItemDefinition> Items { get; } =
    [
        new() { Id = "item.biscuit", Name = "Biscuit", Category = "food", WeightKg = 0.25, VolumeLitres = 0.5, HungerRelief = 12 },
        new() { Id = "item.water", Name = "Water", Category = "water", WeightKg = 1, VolumeLitres = 1, ThirstRelief = 12 },
        new() { Id = "item.marlinspike", Name = "Marlinspike", Category = "tool", WeightKg = 0.3, VolumeLitres = 0.1 },
        new() { Id = "item.rope", Name = "Rope", Category = "material", WeightKg = 4, VolumeLitres = 6 },
        new() { Id = "item.plank", Name = "Plank", Category = "material", WeightKg = 9, VolumeLitres = 12 },
    ];

    public static TaskDefinition Rest { get; } = new()
    {
        Id = "task.rest", Name = "Rest", DurationMinutes = 240, IsRest = true,
        NeedsPerHour = new NeedRates { Fatigue = -9 },
    };

    public static TaskDefinition Pump { get; } = new()
    {
        Id = "task.pump", Name = "Pump", DurationMinutes = 60,
        NeedsPerHour = new NeedRates { Fatigue = 6, Thirst = 1 },
    };

    /// <summary>Long, tiring work for exhaustion tests: 10 fatigue per hour including baseline.</summary>
    public static TaskDefinition Haul { get; } = new()
    {
        Id = "task.haul", Name = "Haul cargo", DurationMinutes = 600, Attribute = "attribute.strength",
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

    /// <summary>Work at a spot: 60 minutes at the pump, with walking first.</summary>
    public static TaskDefinition SpotPump { get; } = new()
    {
        Id = "task.spot_pump", Name = "Pump at the pump", DurationMinutes = 60, NeedsWorkSpot = true,
    };

    /// <summary>
    /// 7 x 5 deck. Row 2 is a wall with one gap at column 5, so walking from the top to the bottom half
    /// must detour. Two pump spots at the bottom.
    /// </summary>
    public static LocationDefinition Deck { get; } = new()
    {
        Id = "location.deck",
        Name = "Deck",
        Rows =
        [
            ".......",
            ".......",
            "#####.#",
            ".......",
            ".......",
        ],
        Spots =
        [
            new WorkSpotDefinition { Id = "spot.pump_a", X = 0, Y = 4, Tasks = ["task.spot_pump"] },
            new WorkSpotDefinition { Id = "spot.pump_b", X = 6, Y = 4, Tasks = ["task.spot_pump"] },
        ],
    };

    public static GameContent Create() => new(
        Rules,
        [new SkillDefinition { Id = "skill.rigging", Name = "Rigging", Group = "maritime" }],
        Items,
        [Rest, Pump, Haul, RepairRigging, StormWork, SpotPump],
        [Deck],
        [new AttributeDefinition { Id = "attribute.strength", Name = "Strength" }],
        [new TraitDefinition { Id = "trait.cautious", Name = "Cautious", Description = "Avoids risk." }],
        [
            new ConditionDefinition { Id = "condition.bruised", Name = "Bruising", Kind = "injury" },
            new ConditionDefinition { Id = "condition.fever", Name = "Fever", Kind = "illness" },
        ]);
}
