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

    /// <summary>Stable campaign identifier. Added in format 4.</summary>
    public required string CampaignId { get; init; }

    /// <summary>The campaign's starting seed, as a decimal string. Added in format 4.</summary>
    public required string Seed { get; init; }

    /// <summary>The captain's character ID number, or null. Added in format 4.</summary>
    public int? CaptainId { get; init; }

    /// <summary>State of every random stream, by stream name. Replaced the single generator in format 4.</summary>
    public required IReadOnlyDictionary<string, RandomData> RandomStreams { get; init; }

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

    /// <summary>Directed relationships, ordered by (from, to). Added in format 5.</summary>
    public required IReadOnlyList<RelationshipData> Relationships { get; init; }

    /// <summary>Highest task ID ever issued. Added in format 6.</summary>
    public required int LastTaskId { get; init; }

    /// <summary>Waiting orders, by character ID and then in queue order. Added in format 6.</summary>
    public required IReadOnlyList<TaskData> QueuedTasks { get; init; }

    /// <summary>Active tasks, in character ID order.</summary>
    public required IReadOnlyList<TaskData> ActiveTasks { get; init; }

    /// <summary>Standing duties, by character ID. Added in format 10.</summary>
    public required IReadOnlyList<DutyData> Duties { get; init; }

    /// <summary>The captain's knowledge, oldest first. Added in format 9.</summary>
    public required IReadOnlyList<KnowledgeData> Knowledge { get; init; }

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

    /// <summary>Attribute levels that differ from ordinary, by attribute ID. Added in format 4.</summary>
    public required IReadOnlyDictionary<string, int> Attributes { get; init; }

    /// <summary>Trait IDs.</summary>
    public required IReadOnlyList<string> Traits { get; init; }

    /// <summary>Sex name. Added in format 8.</summary>
    public required string Sex { get; init; }

    /// <summary>Language levels above zero, by language ID. Added in format 8.</summary>
    public required IReadOnlyDictionary<string, int> Languages { get; init; }

    /// <summary>Social status ID, or null. Added in format 8.</summary>
    public string? StatusId { get; init; }

    /// <summary>Appearance, or null if the character has none recorded. Added in format 7.</summary>
    public AppearanceData? Appearance { get; init; }

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
    /// <summary>Task ID number. Added in format 6.</summary>
    public required int Id { get; init; }

    /// <summary>Character doing the task.</summary>
    public required int CharacterId { get; init; }

    /// <summary>Task definition ID.</summary>
    public required string DefinitionId { get; init; }

    /// <summary>Priority name. Added in format 6.</summary>
    public required string Priority { get; init; }

    /// <summary>Origin name: Order, Duty or SelfCare. Added in format 10.</summary>
    public required string Origin { get; init; }

    /// <summary>Tick work first started, or null for an order that has not started.</summary>
    public long? StartedAtTick { get; init; }

    /// <summary>Whether the task's materials have been used. Added in format 6.</summary>
    public required bool MaterialsConsumed { get; init; }

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

    /// <summary>Character ID numbers involved, the main one first.</summary>
    public required IReadOnlyList<int> Characters { get; init; }

    /// <summary>Named values (IDs, enum names, numbers). Replaced the prose description in format 3.</summary>
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }
}

/// <summary>A saved directed relationship.</summary>
public sealed record RelationshipData
{
    /// <summary>Character ID number of the holder.</summary>
    public required int From { get; init; }

    /// <summary>Character ID number of the subject.</summary>
    public required int To { get; init; }

    /// <summary>Trust.</summary>
    public required int Trust { get; init; }

    /// <summary>Respect.</summary>
    public required int Respect { get; init; }

    /// <summary>Affection.</summary>
    public required int Affection { get; init; }

    /// <summary>Every recorded change, oldest first.</summary>
    public required IReadOnlyList<RelationshipChangeData> History { get; init; }
}

/// <summary>A saved relationship change.</summary>
public sealed record RelationshipChangeData
{
    /// <summary>Tick.</summary>
    public required long Tick { get; init; }

    /// <summary>Dimension name.</summary>
    public required string Dimension { get; init; }

    /// <summary>Signed change.</summary>
    public required int Delta { get; init; }

    /// <summary>Why.</summary>
    public required string Cause { get; init; }
}

/// <summary>A saved appearance: option IDs from <c>data/appearance.json</c>.</summary>
public sealed record AppearanceData
{
    /// <summary>Height in centimetres.</summary>
    public required int HeightCm { get; init; }

    /// <summary>Build name.</summary>
    public required string Build { get; init; }

    /// <summary>Skin tone option ID.</summary>
    public required string SkinTone { get; init; }

    /// <summary>Hair colour option ID.</summary>
    public required string HairColour { get; init; }

    /// <summary>Hair style option ID.</summary>
    public required string HairStyle { get; init; }

    /// <summary>Facial hair option ID.</summary>
    public required string FacialHair { get; init; }

    /// <summary>Main clothing dye option ID.</summary>
    public required string ClothingDye { get; init; }

    /// <summary>Accent dye option ID.</summary>
    public required string AccentDye { get; init; }

    /// <summary>Distinguishing mark option ID.</summary>
    public required string Mark { get; init; }
}

/// <summary>A saved knowledge entry.</summary>
public sealed record KnowledgeData
{
    /// <summary>Tick learned.</summary>
    public required long Tick { get; init; }

    /// <summary>Subject character ID number.</summary>
    public required int Subject { get; init; }

    /// <summary>Topic ID.</summary>
    public required string Topic { get; init; }

    /// <summary>Level.</summary>
    public required int Level { get; init; }

    /// <summary>Source name.</summary>
    public required string Source { get; init; }

    /// <summary>Informant character ID number, or null.</summary>
    public int? Informant { get; init; }
}

/// <summary>A character's saved standing duties.</summary>
public sealed record DutyData
{
    /// <summary>Character ID number.</summary>
    public required int CharacterId { get; init; }

    /// <summary>Task definition IDs, in turn order.</summary>
    public required IReadOnlyList<string> Tasks { get; init; }

    /// <summary>Index of the duty to try next.</summary>
    public required int Next { get; init; }
}
