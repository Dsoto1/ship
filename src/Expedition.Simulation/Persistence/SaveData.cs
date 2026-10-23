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

    /// <summary>Whether the short rations are emergency rations. Added in format 43.</summary>
    public bool EmergencyRations { get; init; }

    /// <summary>The households. Added in format 44.</summary>
    public IReadOnlyList<HouseholdData> Households { get; init; } = [];

    /// <summary>The next household's number. Added in format 44.</summary>
    public int NextHousehold { get; init; } = 1;

    /// <summary>Items whose spoiling the crew has found. Added in format 20.</summary>
    public IReadOnlyList<string> SpoilageNoted { get; init; } = [];

    /// <summary>The voyage, or null when not at sea. Added in format 11.</summary>
    public VoyageData? Voyage { get; init; }

    /// <summary>The ship lost and how the survivors came ashore, or null. Added in format 33.</summary>
    public WreckData? Wreck { get; init; }

    /// <summary>The crew's life aboard: practice, mentors, grievances, fresh food, promises. Added in format 34.</summary>
    public CrewLifeData? Life { get; init; }

    /// <summary>The camp ashore, or null. Added in format 37.</summary>
    public CampData? Camp { get; init; }

    /// <summary>Camps moved on from, with what still lies in them (step 2).</summary>
    public IReadOnlyList<CampData> LeftCamps { get; init; } = [];

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

    /// <summary>When they were born, if at the camp. Added in format 44.</summary>
    public long? Born { get; init; }

    /// <summary>How they came to the company (born, relief, brennish or a people's ID), or null. Added in format 44.</summary>
    public string? Came { get; init; }

    /// <summary>When they came to the company, or null. Added in format 44.</summary>
    public long? Joined { get; init; }
}

/// <summary>A saved household. Added in format 44.</summary>
public sealed record HouseholdData
{
    /// <summary>Its number.</summary>
    public required int Id { get; init; }

    /// <summary>The couple's character IDs.</summary>
    public required IReadOnlyList<int> Adults { get; init; }

    /// <summary>Their children's character IDs.</summary>
    public IReadOnlyList<int> Children { get; init; } = [];

    /// <summary>When they made a home together.</summary>
    public required long Formed { get; init; }

    /// <summary>The people whose customs they keep, or null.</summary>
    public string? Customs { get; init; }

    /// <summary>When a child is due, or null.</summary>
    public long? Due { get; init; }
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

/// <summary>A saved voyage's navigation. Added in format 26.</summary>
public sealed record NavigationData
{
    /// <summary>Where she truly is: [east, north] leagues from the port.</summary>
    public required IReadOnlyList<double> True { get; init; }

    /// <summary>Where the captain reckons she is.</summary>
    public required IReadOnlyList<double> Reckoned { get; init; }

    /// <summary>The set of the sea this spell, leagues an hour [east, north].</summary>
    public required IReadOnlyList<double> Drift { get; init; }

    /// <summary>The log's error today, as a factor.</summary>
    public required double LogError { get; init; }

    /// <summary>The course steered, degrees.</summary>
    public required double Heading { get; init; }

    /// <summary>Format 26 and 27: true once she had fetched the coast (and knew it). Read only; not written.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Landed { get; init; }

    /// <summary>True once she is running down the destination's latitude.</summary>
    public bool RunningDown { get; init; }

    /// <summary>The day of the last noon sight, or -1.</summary>
    public long LastSightDay { get; init; } = -1;

    /// <summary>Who takes the noon sight if the captain does not.</summary>
    public int? Navigator { get; init; }

    /// <summary>The reckoned track, as [east, north] pairs.</summary>
    public IReadOnlyList<IReadOnlyList<double>> Track { get; init; } = [];

    /// <summary>Noon sights taken.</summary>
    public IReadOnlyList<NoonSightData> Sights { get; init; } = [];

    /// <summary>Where the wind blows from, degrees; null in a format 26 save (the prevailing wind). Added in format 27.</summary>
    public double? WindFrom { get; init; }

    /// <summary>Her tack when beating: +1 or -1. Added in format 27.</summary>
    public int Tack { get; init; } = 1;

    /// <summary>Hours on that tack. Added in format 27.</summary>
    public double BoardHours { get; init; }

    /// <summary>The approach to the coast. Added in format 28; null before the coast is raised or near.</summary>
    public ApproachData? Approach { get; init; }

    /// <summary>The legs laid off, each [east, north]. Added in format 36.</summary>
    public IReadOnlyList<IReadOnlyList<double>> Legs { get; init; } = [];

    /// <summary>What is written on the chart. Added in format 36.</summary>
    public IReadOnlyList<ChartNoteData> Notes { get; init; } = [];

    /// <summary>The island she has made for, or null on the passage. Added in format 29.</summary>
    public IslandCallData? Call { get; init; }
}

/// <summary>A saved call at an island. Added in format 29.</summary>
public sealed record IslandCallData
{
    /// <summary>The island's ID.</summary>
    public required string Island { get; init; }

    /// <summary>True once raised.</summary>
    public bool Sighted { get; init; }

    /// <summary>Where she reckons it lies, [east, north], once seen.</summary>
    public IReadOnlyList<double>? SeenAt { get; init; }

    /// <summary>True at anchor off it.</summary>
    public bool Anchored { get; init; }

    /// <summary>Hours searched for it.</summary>
    public double Searched { get; init; }
}

/// <summary>A saved landfall approach (Voyage.Navigation's coast state). Added in format 28.</summary>
public sealed record ApproachData
{
    /// <summary>True once the coast is raised.</summary>
    public bool Sighted { get; init; }

    /// <summary>+1 north along the coast, -1 south, 0 standing off and on.</summary>
    public int Direction { get; init; }

    /// <summary>The landmark the captain takes the coast for.</summary>
    public string? Identified { get; init; }

    /// <summary>The last landmark in view.</summary>
    public string? Seen { get; init; }

    /// <summary>True while a landmark waits for the captain's word.</summary>
    public bool Pending { get; init; }

    /// <summary>Hours it has waited.</summary>
    public double PendingHours { get; init; }

    /// <summary>What the best hand aboard takes it for.</summary>
    public string? Opinion { get; init; }

    /// <summary>Who that is.</summary>
    public int? OpinionBy { get; init; }

    /// <summary>True once told the coast is near by the reckoning.</summary>
    public bool NearWarned { get; init; }

    /// <summary>Casts of the lead.</summary>
    public IReadOnlyList<SoundingData> Soundings { get; init; } = [];

    /// <summary>The landmark in view shown not to be the destination.</summary>
    public string? RuledOut { get; init; }
}

/// <summary>One saved cast of the lead.</summary>
public sealed record SoundingData
{
    /// <summary>When.</summary>
    public required long Tick { get; init; }

    /// <summary>Who cast it.</summary>
    public required int Who { get; init; }

    /// <summary>Where by the reckoning, [east, north].</summary>
    public required IReadOnlyList<double> At { get; init; }

    /// <summary>Fathoms, or null for no bottom.</summary>
    public double? Fathoms { get; init; }

    /// <summary>What came up, or null.</summary>
    public string? Bottom { get; init; }
}

/// <summary>One saved noon sight.</summary>
public sealed record NoonSightData
{
    /// <summary>Day taken.</summary>
    public required long Day { get; init; }

    /// <summary>Who took it.</summary>
    public required int Taker { get; init; }

    /// <summary>The latitude it gave, leagues north of the port.</summary>
    public required double Y { get; init; }

    /// <summary>How far it was out (hidden truth).</summary>
    public required double Error { get; init; }
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

    /// <summary>Lines carried away, by number (format 25).</summary>
    public IReadOnlyList<int> PartedLines { get; init; } = [];

    /// <summary>Whether the main topsail yard is sprung (format 25).</summary>
    public bool YardSprung { get; init; }

    /// <summary>Rigging work done towards mending the next thing carried away (format 25).</summary>
    public double Mending { get; init; }

    /// <summary>The watch bill: character ID to watch name (format 30).</summary>
    public IReadOnlyDictionary<int, string> Watches { get; init; } = new SortedDictionary<int, string>();

    /// <summary>True while all hands are called (format 30).</summary>
    public bool AllHands { get; init; }

    /// <summary>True while the watch below is turned out (format 30).</summary>
    public bool TurnedOut { get; init; }

    /// <summary>The purse carried to sea, sueldos (format 32).</summary>
    public int Funds { get; init; }

    /// <summary>Lots bought from islanders, by "island|item" (format 32).</summary>
    public IReadOnlyDictionary<string, int> Bought { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    /// <summary>The weather foreseen to follow this spell, or null (format 31).</summary>
    public string? NextWeather { get; init; }

    /// <summary>The preparations for heavy weather in place (format 31).</summary>
    public IReadOnlyList<string> Prepared { get; init; } = [];

    /// <summary>Every leak sprung, with its repair (format 31).</summary>
    public IReadOnlyList<LeakData> Leaks { get; init; } = [];

    /// <summary>The captain's standing orders. Added in format 35.</summary>
    public Voyage.StandingOrders? Orders { get; init; }

    /// <summary>What the standing orders have done and not undone, and calls made and not reset. Added in format 35.</summary>
    public IReadOnlyList<string> OrderFlags { get; init; } = [];

    /// <summary>The wind when last noted for the call on a shift. Added in format 35.</summary>
    public double? WindNoted { get; init; }


    /// <summary>Position, course and reckoning, if the voyage has a route (format 26).</summary>
    public NavigationData? Navigation { get; init; }

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

    /// <summary>The evening a song was last called for, or -1. Added in format 24.</summary>
    public long LastSong { get; init; } = -1;

    /// <summary>Character ID numbers who have sung in the tavern. Added in format 24.</summary>
    public IReadOnlyList<int> Sung { get; init; } = [];

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


/// <summary>One saved leak (format 31).</summary>
public sealed record LeakData
{
    /// <summary>Its number.</summary>
    public required int Id { get; init; }

    /// <summary>Where.</summary>
    public required string Where { get; init; }

    /// <summary>How bad, open.</summary>
    public required double Severity { get; init; }

    /// <summary>Open, Plugged or Patched.</summary>
    public required string Repair { get; init; }

    /// <summary>When sprung.</summary>
    public required long Tick { get; init; }

    /// <summary>When last repaired, or -1.</summary>
    public long RepairedTick { get; init; } = -1;
}

/// <summary>A saved camp ashore. Added in format 37.</summary>
public sealed record CampData
{
    /// <summary>The site's ID.</summary>
    public required string Site { get; init; }

    /// <summary>The inventory its stores lie in.</summary>
    public string? StoreId { get; init; }

    /// <summary>When they landed.</summary>
    public required long Landed { get; init; }

    /// <summary>The ship at anchor off.</summary>
    public bool ShipAtAnchor { get; init; }

    /// <summary>A boat to land stores with.</summary>
    public bool Boat { get; init; }

    /// <summary>The works, by ID.</summary>
    public IReadOnlyDictionary<string, double> Works { get; init; } = new Dictionary<string, double>();

    /// <summary>What has been found.</summary>
    public IReadOnlyList<string> Found { get; init; } = [];

    /// <summary>What is still afloat.</summary>
    public IReadOnlyDictionary<string, int> Afloat { get; init; } = new Dictionary<string, int>();

    /// <summary>The captain's priorities.</summary>
    public IReadOnlyList<string> Priorities { get; init; } = [];

    /// <summary>Roof the shelters with sailcloth.</summary>
    public bool UseSailcloth { get; init; }

    /// <summary>Rest the worn out first.</summary>
    public bool RestTheExhausted { get; init; }

    /// <summary>Send scouts out.</summary>
    public bool SendScouts { get; init; }

    /// <summary>Gather the shore's food.</summary>
    public bool Forage { get; init; }

    /// <summary>The fire burns until.</summary>
    public long FireUntil { get; init; }

    /// <summary>Food spoiled in the rain.</summary>
    public int Spoiled { get; init; }

    /// <summary>Boat trips made.</summary>
    public int Trips { get; init; }

    /// <summary>The first days' report made.</summary>
    public bool Reported { get; init; }

    /// <summary>Nights under shelter, by character ID.</summary>
    public IReadOnlyDictionary<int, int> NightsSheltered { get; init; } = new Dictionary<int, int>();

    /// <summary>What each person has in hand, not yet carried to the stores.</summary>
    public IReadOnlyDictionary<int, IReadOnlyDictionary<string, int>> InHand { get; init; } = new Dictionary<int, IReadOnlyDictionary<string, int>>();

    /// <summary>Nights in the open, by character ID.</summary>
    public IReadOnlyDictionary<int, int> NightsExposed { get; init; } = new Dictionary<int, int>();

    /// <summary>How many came ashore. Added in format 38.</summary>
    public int LandedCount { get; init; }

    /// <summary>Buildings being raised. Added in format 38.</summary>
    public IReadOnlyList<CampProjectData> Projects { get; init; } = [];

    /// <summary>Buildings standing. Added in format 38.</summary>
    public IReadOnlyList<CampBuildingData> Buildings { get; init; } = [];

    /// <summary>Garden beds. Added in format 38.</summary>
    public IReadOnlyList<GardenBedData> Beds { get; init; } = [];

    /// <summary>Broken tools. Added in format 38.</summary>
    public IReadOnlyDictionary<string, int> Broken { get; init; } = new Dictionary<string, int>();

    /// <summary>Poles cut. Added in format 38.</summary>
    public int PolesCut { get; init; }

    /// <summary>Thatch cut. Added in format 38.</summary>
    public int ThatchCut { get; init; }

    /// <summary>Stone gathered. Added in format 40.</summary>
    public int StoneGathered { get; init; }

    /// <summary>Planks salvaged. Added in format 40.</summary>
    public int TimberSalvaged { get; init; }

    /// <summary>When thatch was last cut. Added in format 40.</summary>
    public long ThatchLastCut { get; init; }

    /// <summary>The cells walked over. Added in format 40.</summary>
    public IReadOnlyList<TroddenCellData> Trodden { get; init; } = [];

    /// <summary>The cells growing back. Added in format 40.</summary>
    public IReadOnlyList<RegrowingCellData> Regrowing { get; init; } = [];

    /// <summary>The palisade ordered. Added in format 41.</summary>
    public bool PalisadeOrdered { get; init; }

    /// <summary>The working day (a <see cref="Shore.WorkHours"/> name). Added in format 43.</summary>
    public string Hours { get; init; } = nameof(Shore.WorkHours.Normal);

    /// <summary>Days of food kept in hand. Added in format 43.</summary>
    public int ReserveFoodDays { get; init; } = 4;

    /// <summary>Days of water kept in hand. Added in format 43.</summary>
    public int ReserveWaterDays { get; init; } = 4;

    /// <summary>The milestones reached, and when. Added in format 43.</summary>
    public IReadOnlyDictionary<string, long> Milestones { get; init; } = new Dictionary<string, long>();

    /// <summary>The world around the camp (R8), or null. Added in format 41.</summary>
    public WorldData? World { get; init; }

    /// <summary>The next project's number. Added in format 38.</summary>
    public int NextProject { get; init; } = 1;

    /// <summary>The first season's judgement, or null. Added in format 38.</summary>
    public bool? Survived { get; init; }
}

/// <summary>A building being raised. Added in format 38.</summary>
public sealed record CampProjectData
{
    /// <summary>Its number.</summary>
    public required int Id { get; init; }

    /// <summary>The building.</summary>
    public required string Building { get; init; }

    /// <summary>The plot.</summary>
    public required string Plot { get; init; }

    /// <summary>Its phase.</summary>
    public int Phase { get; init; }

    /// <summary>Times the phase is done.</summary>
    public int Done { get; init; }

    /// <summary>Quality summed.</summary>
    public double QualitySum { get; init; }

    /// <summary>Tasks done.</summary>
    public int Worked { get; init; }

    /// <summary>Materials laid ready.</summary>
    public int Laid { get; init; }

    /// <summary>Where it stands. Added in format 39.</summary>
    public int X { get; init; }

    /// <summary>See <see cref="X"/>.</summary>
    public int Y { get; init; }

    /// <summary>Turned a quarter. Added in format 39.</summary>
    public bool Turned { get; init; }

    /// <summary>Trees on its ground. Added in format 39.</summary>
    public int Trees { get; init; }

    /// <summary>Its priority (a <see cref="Shore.ProjectPriority"/> name). Added in format 42.</summary>
    public string Priority { get; init; } = nameof(Shore.ProjectPriority.Normal);

    /// <summary>Who leads it, or null. Added in format 42.</summary>
    public int? Lead { get; init; }

    /// <summary>When it was ordered. Added in format 42.</summary>
    public long Begun { get; init; }

    /// <summary>Who has worked on it: tasks done by each. Added in format 42.</summary>
    public IReadOnlyDictionary<int, int> Builders { get; init; } = new Dictionary<int, int>();

    /// <summary>The materials gone into it. Added in format 42.</summary>
    public IReadOnlyDictionary<string, int> Used { get; init; } = new Dictionary<string, int>();
}

/// <summary>A building standing. Added in format 38.</summary>
public sealed record CampBuildingData
{
    /// <summary>The building.</summary>
    public required string Building { get; init; }

    /// <summary>The plot.</summary>
    public required string Plot { get; init; }

    /// <summary>Its quality.</summary>
    public required double Quality { get; init; }

    /// <summary>Its condition.</summary>
    public double Condition { get; init; }

    /// <summary>Where it stands. Added in format 39.</summary>
    public int X { get; init; }

    /// <summary>See <see cref="X"/>.</summary>
    public int Y { get; init; }

    /// <summary>Turned a quarter. Added in format 39.</summary>
    public bool Turned { get; init; }

    /// <summary>Who leads the work there now, or null. Added in format 42.</summary>
    public int? Lead { get; init; }

    /// <summary>Who led its building, or null. Added in format 42.</summary>
    public int? BuiltUnder { get; init; }

    /// <summary>When it was ordered, or null for one built before buildings kept their history. Added in format 42.</summary>
    public long? Begun { get; init; }

    /// <summary>When it was finished, or null. Added in format 42.</summary>
    public long? Built { get; init; }

    /// <summary>Who built it: tasks done by each. Added in format 42.</summary>
    public IReadOnlyDictionary<int, int> Builders { get; init; } = new Dictionary<int, int>();

    /// <summary>The materials that went into it. Added in format 42.</summary>
    public IReadOnlyDictionary<string, int> Used { get; init; } = new Dictionary<string, int>();

    /// <summary>What it has been through, oldest first. Added in format 42.</summary>
    public IReadOnlyList<BuildingEventData> History { get; init; } = [];
}

/// <summary>Something that happened to a building. Added in format 42.</summary>
public sealed record BuildingEventData
{
    /// <summary>When.</summary>
    public long Tick { get; init; }

    /// <summary><c>storm</c> or <c>repaired</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Condition lost or gained.</summary>
    public double Amount { get; init; }
}

/// <summary>A cell of the camp walked over. Added in format 40.</summary>
public sealed record TroddenCellData
{
    /// <summary>Column.</summary>
    public int X { get; init; }

    /// <summary>Row.</summary>
    public int Y { get; init; }

    /// <summary>Times walked over, less what has grown back.</summary>
    public int Steps { get; init; }

    /// <summary>When last walked over.</summary>
    public long Last { get; init; }
}

/// <summary>A cell growing back. Added in format 40.</summary>
public sealed record RegrowingCellData
{
    /// <summary>Column.</summary>
    public int X { get; init; }

    /// <summary>Row.</summary>
    public int Y { get; init; }

    /// <summary>A tree's place, or cleared ground.</summary>
    public bool Tree { get; init; }

    /// <summary>Left alone since.</summary>
    public long Since { get; init; }
}

/// <summary>A garden bed. Added in format 38.</summary>
public sealed record GardenBedData
{
    /// <summary>The crop.</summary>
    public required string Crop { get; init; }

    /// <summary>Sown when.</summary>
    public required long Sown { get; init; }

    /// <summary>Its health.</summary>
    public double Health { get; init; }

    /// <summary>Tended when.</summary>
    public long Tended { get; init; }
}

/// <summary>A saved wreck. Added in format 33.</summary>
public sealed record WreckData
{
    /// <summary>foundered or struck.</summary>
    public required string Cause { get; init; }

    /// <summary>When.</summary>
    public required long Tick { get; init; }

    /// <summary>Where ashore: an ID.</summary>
    public required string Ashore { get; init; }

    /// <summary>Its name.</summary>
    public required string AshoreName { get; init; }

    /// <summary>Leagues the boat ran.</summary>
    public double Leagues { get; init; }

    /// <summary>Days in the boat.</summary>
    public double DaysInBoat { get; init; }

    /// <summary>Whether the boat got away.</summary>
    public bool BoatLaunched { get; init; }

    /// <summary>Who came ashore.</summary>
    public IReadOnlyList<int> Survivors { get; init; } = [];

    /// <summary>Who was lost.</summary>
    public IReadOnlyList<int> Lost { get; init; } = [];

    /// <summary>What was got into the boat.</summary>
    public IReadOnlyDictionary<string, int> Saved { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Water and drink used up in the boat.</summary>
    public int Drunk { get; init; }

    /// <summary>What is still in the wreck.</summary>
    public IReadOnlyDictionary<string, int> Remains { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);
}

/// <summary>The crew's life aboard. Added in format 34.</summary>
public sealed record CrewLifeData
{
    /// <summary>How clean the ship is kept, 0..100.</summary>
    public double Cleanliness { get; init; } = 90;

    /// <summary>How many were sick when last told.</summary>
    public int SickNoted { get; init; }

    /// <summary>Practice towards the next level, by person.</summary>
    public IReadOnlyList<PracticeData> Practice { get; init; } = [];

    /// <summary>Who learns from whom.</summary>
    public IReadOnlyList<MentorshipData> Mentors { get; init; } = [];

    /// <summary>Grievances, by person.</summary>
    public IReadOnlyList<GrievanceData> Grievances { get; init; } = [];

    /// <summary>When each person last ate anything fresh, by character ID.</summary>
    public IReadOnlyDictionary<int, long> LastFresh { get; init; } = new SortedDictionary<int, long>();

    /// <summary>Promises not yet kept or broken.</summary>
    public IReadOnlyList<PromiseData> Promises { get; init; } = [];
}

/// <summary>Hours practised in one skill.</summary>
public sealed record PracticeData
{
    /// <summary>Character ID.</summary>
    public required int Who { get; init; }

    /// <summary>Skill ID.</summary>
    public required string Skill { get; init; }

    /// <summary>Hours.</summary>
    public required double Hours { get; init; }
}

/// <summary>A learner and their mentor.</summary>
public sealed record MentorshipData
{
    /// <summary>Learner's character ID.</summary>
    public required int Learner { get; init; }

    /// <summary>Mentor's character ID.</summary>
    public required int Mentor { get; init; }

    /// <summary>Skill taught.</summary>
    public required string Skill { get; init; }

    /// <summary>When it began.</summary>
    public long Since { get; init; }
}

/// <summary>One person's grievance.</summary>
public sealed record GrievanceData
{
    /// <summary>Character ID.</summary>
    public required int Who { get; init; }

    /// <summary>Stage name.</summary>
    public required string Stage { get; init; }

    /// <summary>When it last changed stage.</summary>
    public long StageTick { get; init; }

    /// <summary>The furthest stage brought to the captain.</summary>
    public string Raised { get; init; } = "None";

    /// <summary>Each cause's weight.</summary>
    public IReadOnlyDictionary<string, double> Causes { get; init; } = new SortedDictionary<string, double>(StringComparer.Ordinal);
}

/// <summary>A promise to the crew.</summary>
public sealed record PromiseData
{
    /// <summary>Its cause.</summary>
    public required string Cause { get; init; }

    /// <summary>When it must be kept by.</summary>
    public required long Due { get; init; }
}

/// <summary>A note on the chart. Added in format 36.</summary>
public sealed record ChartNoteData
{
    /// <summary>Where: [east, north].</summary>
    public required IReadOnlyList<double> At { get; init; }

    /// <summary>What it says.</summary>
    public required string Text { get; init; }

    /// <summary>When written.</summary>
    public long Tick { get; init; }

    /// <summary>The captain's own, or the voyage's mark.</summary>
    public bool ByCaptain { get; init; }
}

/// <summary>The world around the camp (R8). Added in format 41.</summary>
public sealed record WorldData
{
    /// <summary>Places known.</summary>
    public IReadOnlyList<KnownPlaceData> Places { get; init; } = [];

    /// <summary>Routes known, by key.</summary>
    public IReadOnlyList<string> Routes { get; init; } = [];

    /// <summary>Each people's standing.</summary>
    public IReadOnlyList<CommunityData> Communities { get; init; } = [];

    /// <summary>What has been learned, by topic, with who told it.</summary>
    public IReadOnlyDictionary<string, string> Learned { get; init; } = new Dictionary<string, string>();

    /// <summary>Sites surveyed.</summary>
    public IReadOnlyList<string> Surveyed { get; init; } = [];

    /// <summary>Parties away.</summary>
    public IReadOnlyList<JourneyData> Journeys { get; init; } = [];

    /// <summary>The meeting waiting, or null.</summary>
    public MeetingData? Meeting { get; init; }

    /// <summary>The next journey's number.</summary>
    public int NextJourney { get; init; } = 1;

    /// <summary>The trade peace broken.</summary>
    public bool PeaceBroken { get; init; }

    /// <summary>The palisade explained to Mahenu.</summary>
    public bool PalisadeExplained { get; init; }

    /// <summary>The palisade seen going up.</summary>
    public bool PalisadeNoticed { get; init; }

    /// <summary>When a letter home went; 0 for none.</summary>
    public long LetterSent { get; init; }

    /// <summary>The Brennish ship's visit, 0..3.</summary>
    public int Brennish { get; init; }

    /// <summary>The garrison's survivor, 0..4.</summary>
    public int Survivor { get; init; }

    /// <summary>She was warned off.</summary>
    public bool BrennishWarned { get; init; }

    /// <summary>Logs sold to her.</summary>
    public int LogsSold { get; init; }

    /// <summary>Thefts.</summary>
    public int Thefts { get; init; }

    /// <summary>The charter's judgement, or null.</summary>
    public string? Charter { get; init; }
}

/// <summary>A place known.</summary>
public sealed record KnownPlaceData
{
    /// <summary>The place.</summary>
    public required string Id { get; init; }

    /// <summary>How well: reported, seen, visited.</summary>
    public required string Level { get; init; }

    /// <summary>Who told it.</summary>
    public string Source { get; init; } = "";
}

/// <summary>A people's standing.</summary>
public sealed record CommunityData
{
    /// <summary>The people.</summary>
    public required string Id { get; init; }

    /// <summary>Trust.</summary>
    public double Trust { get; init; }

    /// <summary>Grievance.</summary>
    public double Grievance { get; init; }

    /// <summary>Met.</summary>
    public bool Met { get; init; }

    /// <summary>Days watched.</summary>
    public int Watched { get; init; }

    /// <summary>Next visit.</summary>
    public long NextVisit { get; init; }

    /// <summary>A gift owed since.</summary>
    public long GiftOwedSince { get; init; }

    /// <summary>What they have had in trade.</summary>
    public IReadOnlyDictionary<string, int> Received { get; init; } = new Dictionary<string, int>();

    /// <summary>Leave given.</summary>
    public IReadOnlyList<string> Leave { get; init; } = [];

    /// <summary>A theft to take up with them.</summary>
    public string? Grievous { get; init; }
}

/// <summary>A party away.</summary>
public sealed record JourneyData
{
    /// <summary>Its number.</summary>
    public int Id { get; init; }

    /// <summary>Who went.</summary>
    public IReadOnlyList<int> Members { get; init; } = [];

    /// <summary>Where to.</summary>
    public required string Destination { get; init; }

    /// <summary>Why.</summary>
    public required string Purpose { get; init; }

    /// <summary>The way.</summary>
    public IReadOnlyList<string> Path { get; init; } = [];

    /// <summary>Set out.</summary>
    public long Left { get; init; }

    /// <summary>Arrive.</summary>
    public long Arrives { get; init; }

    /// <summary>Back.</summary>
    public long Returns { get; init; }

    /// <summary>Arrived yet.</summary>
    public bool Arrived { get; init; }

    /// <summary>Logs.</summary>
    public int Logs { get; init; }

    /// <summary>Their report so far.</summary>
    public IReadOnlyList<string> Report { get; init; } = [];

    /// <summary>What the party carries.</summary>
    public IReadOnlyDictionary<string, int> Loads { get; init; } = new Dictionary<string, int>();

    /// <summary>Whether it has the boat.</summary>
    public bool Boat { get; init; }
}

/// <summary>A meeting waiting.</summary>
public sealed record MeetingData
{
    /// <summary>Its kind.</summary>
    public required string Kind { get; init; }

    /// <summary>The people.</summary>
    public string? Community { get; init; }

    /// <summary>When.</summary>
    public long Tick { get; init; }

    /// <summary>How far it has got.</summary>
    public int Round { get; init; }

    /// <summary>Their gift's item.</summary>
    public string? GiftItem { get; init; }

    /// <summary>What was refused at this meeting.</summary>
    public IReadOnlyList<string> Refused { get; init; } = [];

    /// <summary>How many.</summary>
    public int GiftCount { get; init; }

    /// <summary>A theft's item.</summary>
    public string? Item { get; init; }
}
