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

    /// <summary>Preparations in port, or null. Added in format 15 (format 14's hiring is dropped).</summary>
    public PortData? Port { get; init; }

    /// <summary>An event waiting for the captain, or null. Added in format 12.</summary>
    public PendingEventData? PendingEvent { get; init; }

    /// <summary>How often each event has fired. Added in format 12.</summary>
    public required IReadOnlyList<EventRecordData> EventHistory { get; init; }

    /// <summary>When the last event fired. Added in format 12.</summary>
    public required long LastEventTick { get; init; }

    /// <summary>Whether the crew are on short rations. Added in format 12.</summary>
    public required bool Rationing { get; init; }

    /// <summary>Items whose spoiling the crew has found. Added in format 20.</summary>
    public IReadOnlyList<string> SpoilageNoted { get; init; } = [];

    /// <summary>The voyage, or null when not at sea. Added in format 11.</summary>
    public VoyageData? Voyage { get; init; }

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

    /// <summary>Crew or Local. Added in format 15.</summary>
    public required string Membership { get; init; }

    /// <summary>Person data ID, or null. Added in format 15.</summary>
    public string? PersonId { get; init; }

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

    /// <summary>Task definition IDs, most important first.</summary>
    public required IReadOnlyList<string> Tasks { get; init; }
}

/// <summary>A saved voyage. Added in format 11.</summary>
public sealed record VoyageData
{
    /// <summary>Ship name.</summary>
    public required string ShipName { get; init; }

    /// <summary>Destination.</summary>
    public required string Destination { get; init; }

    /// <summary>Length of the passage in leagues.</summary>
    public required double TotalLeagues { get; init; }

    /// <summary>Leagues sailed.</summary>
    public required double LeaguesSailed { get; init; }

    /// <summary>Hull condition.</summary>
    public required double Hull { get; init; }

    /// <summary>Bilge level.</summary>
    public required double Bilge { get; init; }

    /// <summary>Rigging condition.</summary>
    public required double Rigging { get; init; }

    /// <summary>Weather ID.</summary>
    public required string Weather { get; init; }

    /// <summary>Tick the weather next changes.</summary>
    public required long WeatherUntilTick { get; init; }

    /// <summary>Last speed, leagues per hour.</summary>
    public required double Speed { get; init; }

    /// <summary>Whether a watch was kept in the last minute.</summary>
    public required bool WatchKept { get; init; }

    /// <summary>Outcome name.</summary>
    public required string Outcome { get; init; }

    /// <summary>Sail plan name. Added in format 13.</summary>
    public required string SailPlan { get; init; }
}

/// <summary>A saved waiting event.</summary>
public sealed record PendingEventData
{
    /// <summary>Event definition ID.</summary>
    public required string Event { get; init; }

    /// <summary>Subject character ID number, or null.</summary>
    public int? Subject { get; init; }

    /// <summary>Other character ID number, or null.</summary>
    public int? Other { get; init; }

    /// <summary>Tick it happened.</summary>
    public required long Tick { get; init; }
}

/// <summary>How often a saved event has fired.</summary>
public sealed record EventRecordData
{
    /// <summary>Event definition ID.</summary>
    public required string Event { get; init; }

    /// <summary>Times fired.</summary>
    public required int Count { get; init; }

    /// <summary>When it last fired.</summary>
    public required long LastTick { get; init; }
}

/// <summary>Saved preparations in port. Added in format 15.</summary>
public sealed record PortData
{
    /// <summary>Leads heard, oldest first. Added in format 16.</summary>
    public IReadOnlyList<HeardLeadData> Leads { get; init; } = [];

    /// <summary>Who has sought the captain out. Added in format 19.</summary>
    public IReadOnlyList<int> Approached { get; init; } = [];

    /// <summary>Who is walking over to the captain now, and since when. Added in format 19.</summary>
    public int? Approaching { get; init; }

    /// <summary>When they set off. Added in format 19.</summary>
    public long ApproachingSince { get; init; }

    /// <summary>Creditors' claims answered: [creditor, 1 paid or 0 refused]. Added in format 19.</summary>
    public IReadOnlyList<int[]> Claims { get; init; } = [];

    /// <summary>Greetings given: [who, day]. Added in format 19.</summary>
    public IReadOnlyList<int[]> Greeted { get; init; } = [];

    /// <summary>When the captain was last greeted. Added in format 19.</summary>
    public long LastGreeting { get; init; } = long.MinValue / 2;

    /// <summary>The weather now, or null. Added in format 18.</summary>
    public string? Weather { get; init; }

    /// <summary>When the weather next changes. Added in format 18.</summary>
    public long WeatherUntil { get; init; }

    /// <summary>Lots bought, oldest first. Added in format 17.</summary>
    public IReadOnlyList<PurchaseData> Purchases { get; init; } = [];

    /// <summary>What the captain made of each lot he looked at: lot index and quality or "unsure". Added in format 20.</summary>
    public IReadOnlyList<InspectionData> Inspected { get; init; } = [];

    /// <summary>What the expedition owes sellers for stores bought on credit. Added in format 21.</summary>
    public IReadOnlyList<DebtData> Owed { get; init; } = [];

    /// <summary>What the captain has talked over with whom. Added in format 22.</summary>
    public IReadOnlyList<DiscussedData> Discussed { get; init; } = [];

    /// <summary>Sueldos left.</summary>
    public required int Funds { get; init; }

    /// <summary>Chandler's lots left, by lot index.</summary>
    public required IReadOnlyDictionary<int, int> Stock { get; init; }

    /// <summary>Character ID numbers spoken with.</summary>
    public required IReadOnlyList<int> TalkedTo { get; init; }

    /// <summary>Pairs [informant, about] asked.</summary>
    public required IReadOnlyList<int[]> Asked { get; init; }

    /// <summary>Character ID numbers hired here.</summary>
    public required IReadOnlyList<int> Hired { get; init; }

    /// <summary>Character ID numbers who left.</summary>
    public required IReadOnlyList<int> Gone { get; init; }

    /// <summary>Half days of late fees charged.</summary>
    public required int LateFeesCharged { get; init; }

    /// <summary>The day the captain last stood a room a round, or -1. Added in format 23.</summary>
    public long LastRound { get; init; } = -1;

    /// <summary>Whether the eve-of-sailing warning was given.</summary>
    public required bool Warned { get; init; }
}

/// <summary>A saved lead. Added in format 16.</summary>
public sealed record HeardLeadData
{
    /// <summary>Lead content ID.</summary>
    public required string Lead { get; init; }

    /// <summary>When it was heard.</summary>
    public required long Tick { get; init; }
}

/// <summary>A saved purchase from the chandler. Added in format 17.</summary>
public sealed record PurchaseData
{
    /// <summary>Lot index.</summary>
    public required int Lot { get; init; }

    /// <summary>When it was bought.</summary>
    public required long Tick { get; init; }
}

/// <summary>A lot the captain looked at closely. Added in format 20.</summary>
public sealed record InspectionData
{
    /// <summary>Lot index.</summary>
    public required int Lot { get; init; }

    /// <summary>What he saw: a quality, or <c>unsure</c>.</summary>
    public required string Seen { get; init; }
}

/// <summary>A topic talked over with someone. Added in format 22.</summary>
public sealed record DiscussedData
{
    /// <summary>Character ID number.</summary>
    public required int Who { get; init; }

    /// <summary>Topic ID from their conversation.</summary>
    public required string Topic { get; init; }
}

/// <summary>What the expedition owes a seller. Added in format 21.</summary>
public sealed record DebtData
{
    /// <summary>Seller ID.</summary>
    public required string Seller { get; init; }

    /// <summary>Sueldos owed.</summary>
    public required int Amount { get; init; }
}
