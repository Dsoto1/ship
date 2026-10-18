namespace Expedition.Simulation.Persistence;

// Plain data shapes written to and read from save files. They mirror runtime state but hold no
// behaviour, so the file format is explicit and changes to it are visible in review.
// Any change here needs a new SaveSerializer.CurrentSchemaVersion and a migration.

/// <summary>Root of a save file.</summary>
public sealed record SaveData
{
    /// <summary>Format version of this file. See <see cref="SaveSerializer.CurrentSchemaVersion"/>.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>Campaign phase name.</summary>
    public required string Phase { get; init; }

    /// <summary>Simulation tick.</summary>
    public required long Tick { get; init; }

    /// <summary>Highest character ID ever issued, so IDs are never reused.</summary>
    public required int LastCharacterId { get; init; }

    /// <summary>Random generator state.</summary>
    public required RandomData Random { get; init; }

    /// <summary>Inventory that serves as the crew's shared store.</summary>
    public required string SharedStoreId { get; init; }

    /// <summary>Whether a food shortage is currently recorded.</summary>
    public bool FoodShortageRecorded { get; init; }

    /// <summary>Whether a water shortage is currently recorded.</summary>
    public bool WaterShortageRecorded { get; init; }

    /// <summary>Every character, living or dead, in ID order.</summary>
    public required IReadOnlyList<CharacterData> Characters { get; init; }

    /// <summary>Every inventory, in ID order.</summary>
    public required IReadOnlyList<InventoryData> Inventories { get; init; }

    /// <summary>Where characters are, in character ID order. Added in format 2.</summary>
    public required IReadOnlyList<PlacementData> Placements { get; init; }

    /// <summary>Active tasks, in character ID order.</summary>
    public required IReadOnlyList<TaskData> ActiveTasks { get; init; }

    /// <summary>Full chronicle, in sequence order.</summary>
    public required IReadOnlyList<ChronicleEntryData> Chronicle { get; init; }
}

/// <summary>PCG32 state. Stored as strings because 64-bit integers do not survive every JSON reader.</summary>
public sealed record RandomData
{
    /// <summary>Internal state, decimal.</summary>
    public required string State { get; init; }

    /// <summary>Stream increment, decimal. Always odd.</summary>
    public required string Increment { get; init; }
}

/// <summary>A saved character.</summary>
public sealed record CharacterData
{
    /// <summary>Character ID number.</summary>
    public required int Id { get; init; }

    /// <summary>Full name.</summary>
    public required string Name { get; init; }

    /// <summary>Age in years.</summary>
    public required int Age { get; init; }

    /// <summary>Place of origin.</summary>
    public required string Origin { get; init; }

    /// <summary>Survival state name.</summary>
    public required string SurvivalState { get; init; }

    /// <summary>Skill levels above zero, by skill ID.</summary>
    public required IReadOnlyDictionary<string, int> Skills { get; init; }

    /// <summary>Trait IDs.</summary>
    public required IReadOnlyList<string> Traits { get; init; }

    /// <summary>Need values.</summary>
    public required NeedsData Needs { get; init; }

    /// <summary>Health value.</summary>
    public required double Health { get; init; }

    /// <summary>Condition IDs.</summary>
    public required IReadOnlyList<string> Conditions { get; init; }
}

/// <summary>Saved need values.</summary>
public sealed record NeedsData
{
    /// <summary>Hunger.</summary>
    public required double Hunger { get; init; }

    /// <summary>Thirst.</summary>
    public required double Thirst { get; init; }

    /// <summary>Fatigue.</summary>
    public required double Fatigue { get; init; }

    /// <summary>Stress.</summary>
    public required double Stress { get; init; }
}

/// <summary>A saved inventory.</summary>
public sealed record InventoryData
{
    /// <summary>Inventory ID.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Weight limit, or null.</summary>
    public double? MaxWeightKg { get; init; }

    /// <summary>Volume limit, or null.</summary>
    public double? MaxVolumeLitres { get; init; }

    /// <summary>Quantities by item ID.</summary>
    public required IReadOnlyDictionary<string, int> Items { get; init; }
}

/// <summary>A saved active task.</summary>
public sealed record TaskData
{
    /// <summary>Character doing the task.</summary>
    public required int CharacterId { get; init; }

    /// <summary>Task definition ID.</summary>
    public required string DefinitionId { get; init; }

    /// <summary>Tick it started.</summary>
    public required long StartedAtTick { get; init; }

    /// <summary>Work-minutes done.</summary>
    public required double Progress { get; init; }

    /// <summary>Reserved work spot, or null. Added in format 2.</summary>
    public string? SpotId { get; init; }
}

/// <summary>A saved character position.</summary>
public sealed record PlacementData
{
    /// <summary>Character ID number.</summary>
    public required int CharacterId { get; init; }

    /// <summary>Location definition ID.</summary>
    public required string LocationId { get; init; }

    /// <summary>Column of the current (or walk-start) cell.</summary>
    public required int X { get; init; }

    /// <summary>Row of the current (or walk-start) cell.</summary>
    public required int Y { get; init; }

    /// <summary>The walk in progress, or null.</summary>
    public MovementData? Movement { get; init; }
}

/// <summary>A saved walk in progress.</summary>
public sealed record MovementData
{
    /// <summary>Path cells as [x, y] pairs, start and destination included.</summary>
    public required IReadOnlyList<int[]> Path { get; init; }

    /// <summary>Tick the walk began.</summary>
    public required long StartTick { get; init; }

    /// <summary>Tick of arrival.</summary>
    public required long ArriveTick { get; init; }
}

/// <summary>A saved chronicle entry.</summary>
public sealed record ChronicleEntryData
{
    /// <summary>Sequence number.</summary>
    public required long Sequence { get; init; }

    /// <summary>Tick.</summary>
    public required long Tick { get; init; }

    /// <summary>Entry kind name.</summary>
    public required string Kind { get; init; }

    /// <summary>Description.</summary>
    public required string Description { get; init; }

    /// <summary>Character ID numbers involved.</summary>
    public required IReadOnlyList<int> Characters { get; init; }
}
