namespace Expedition.Simulation.Shore;

/// <summary>
/// The Ember Coast around the camp (R8, docs/R8_THE_WORLD_AROUND.md; <c>data/region.json</c>): its places and
/// routes, which exist whether or not anyone has found them; the peoples who live there; what they know and
/// will tell; the Company's charter; and the interlopers' ship.
/// </summary>
public sealed record RegionRules
{
    /// <summary>The places.</summary>
    public IReadOnlyList<RegionPlace> Places { get; init; } = [];

    /// <summary>The ways between them, walked either way.</summary>
    public IReadOnlyList<RegionRoute> Routes { get; init; } = [];

    /// <summary>The peoples.</summary>
    public IReadOnlyList<CommunityDefinition> Communities { get; init; } = [];

    /// <summary>What they know and will tell.</summary>
    public IReadOnlyList<KnowledgeTopic> Knowledge { get; init; } = [];

    /// <summary>The Company's charter.</summary>
    public CharterRules Charter { get; init; } = new();

    /// <summary>The Brennish ship at the cove.</summary>
    public BrennishRules? Brennish { get; init; }

    /// <summary>The privateers who work the coast (step S).</summary>
    public PrivateerRules? Privateer { get; init; }

    /// <summary>A place by ID, or null.</summary>
    public RegionPlace? Place(string id) => Places.FirstOrDefault(p => p.Id == id);

    /// <summary>A people by ID, or null.</summary>
    public CommunityDefinition? Community(string id) => Communities.FirstOrDefault(c => c.Id == id);

    /// <summary>A topic by ID, or null.</summary>
    public KnowledgeTopic? Topic(string id) => Knowledge.FirstOrDefault(k => k.Id == id);
}

/// <summary>A place on the coast.</summary>
public sealed record RegionPlace
{
    /// <summary>ID, for example <c>place.mahenu</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Its name.</summary>
    public required string Name { get; init; }

    /// <summary>What kind of place: headland, shore, village, town, forest, river, landing, camp, sacred, cove, swamp.</summary>
    public required string Kind { get; init; }

    /// <summary>Leagues east of Punta Ceniza.</summary>
    public double X { get; init; }

    /// <summary>Leagues north of Punta Ceniza.</summary>
    public double Y { get; init; }

    /// <summary>The landing site that is here (a key of <c>shore.json</c>'s sites), if any.</summary>
    public string? Site { get; init; }

    /// <summary>The people who live here.</summary>
    public string? People { get; init; }

    /// <summary>Whose ground it is.</summary>
    public string? Ground { get; init; }

    /// <summary>Embrawood grows here.</summary>
    public bool Wood { get; init; }

    /// <summary>Ground not to be entered.</summary>
    public bool Sacred { get; init; }

    /// <summary>Known on landing: <c>seen</c> or <c>reported</c>; null for not at all.</summary>
    public string? Known { get; init; }

    /// <summary>What is found there.</summary>
    public required string Text { get; init; }

    /// <summary>What is said of it by those who have not been.</summary>
    public required string Report { get; init; }
}

/// <summary>A way between two places.</summary>
public sealed record RegionRoute
{
    /// <summary>One end.</summary>
    public required string From { get; init; }

    /// <summary>The other end.</summary>
    public required string To { get; init; }

    /// <summary>Days to walk or paddle it.</summary>
    public required double Days { get; init; }

    /// <summary>beach, lagoon, forest or river.</summary>
    public string Way { get; init; } = "beach";

    /// <summary>Known on landing.</summary>
    public bool Known { get; init; }

    /// <summary>Its key, the same either way.</summary>
    public string Key => string.CompareOrdinal(From, To) < 0 ? $"{From}|{To}" : $"{To}|{From}";

    /// <summary>The other end from <paramref name="place"/>, or null if it does not touch it.</summary>
    public string? Other(string place) => From == place ? To : To == place ? From : null;
}

/// <summary>A people, as a community acting on its own (SETTLEMENT §28).</summary>
public sealed record CommunityDefinition
{
    /// <summary>ID, for example <c>people.mahenu</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Its name: the village or town.</summary>
    public required string Name { get; init; }

    /// <summary>The people it is of, for example "the Ahuneri".</summary>
    public required string People { get; init; }

    /// <summary>Where it lives.</summary>
    public required string Place { get; init; }

    /// <summary>Its language.</summary>
    public required string Language { get; init; }

    /// <summary>Names given to its men, for those who come to live at the camp (step P).</summary>
    public IReadOnlyList<string> MaleNames { get; init; } = [];

    /// <summary>Names given to its women.</summary>
    public IReadOnlyList<string> FemaleNames { get; init; } = [];

    /// <summary>How it stands to the newcomers at first, -100..100.</summary>
    public double Trust { get; init; }

    /// <summary>How readily it trades, 0..1.</summary>
    public double Trade { get; init; } = 0.6;

    /// <summary>How cautious it is of strangers, 0..1.</summary>
    public double Caution { get; init; } = 0.5;

    /// <summary>A gift opens a meeting, and a gift unanswered is an insult.</summary>
    public bool GiftCulture { get; init; }

    /// <summary>The day after landing it first comes to the camp, if it has not been met before.</summary>
    public int FirstVisitDay { get; init; } = 7;

    /// <summary>Days between its visits, while it is minded to come.</summary>
    public int VisitEveryDays { get; init; } = 8;

    /// <summary>Told of it.</summary>
    public string About { get; init; } = "";

    /// <summary>Its people the newcomers meet.</summary>
    public IReadOnlyList<RegionPerson> Persons { get; init; } = [];

    /// <summary>What things are worth to it, in its own reckoning.</summary>
    public IReadOnlyDictionary<string, double> Wants { get; init; } = new Dictionary<string, double>();

    /// <summary>What it will part with, and what each costs it in the same reckoning.</summary>
    public IReadOnlyDictionary<string, double> Offers { get; init; } = new Dictionary<string, double>();
}

/// <summary>Someone of a people.</summary>
public sealed record RegionPerson
{
    /// <summary>ID.</summary>
    public required string Id { get; init; }

    /// <summary>Their name.</summary>
    public required string Name { get; init; }

    /// <summary>Who they are to their people.</summary>
    public required string Role { get; init; }

    /// <summary>What the newcomers see of them.</summary>
    public string About { get; init; } = "";
}

/// <summary>Something a people knows and may tell (SETTLEMENT §31).</summary>
public sealed record KnowledgeTopic
{
    /// <summary>ID, for example <c>know.fever_lore</c>.</summary>
    public required string Id { get; init; }

    /// <summary>What it is, to ask about.</summary>
    public required string Name { get; init; }

    /// <summary>The peoples who know it.</summary>
    public IReadOnlyList<string> From { get; init; } = [];

    /// <summary>Trust it takes before they tell it.</summary>
    public double Trust { get; init; }

    /// <summary>The interpreter's level it takes to be understood.</summary>
    public int Language { get; init; }

    /// <summary>What they tell.</summary>
    public required string Text { get; init; }

    /// <summary>Places it makes known (reported).</summary>
    public IReadOnlyList<string> Reveals { get; init; } = [];

    /// <summary>What they give with it.</summary>
    public IReadOnlyDictionary<string, int> Gives { get; init; } = new Dictionary<string, int>();
}

/// <summary>The Company's charter (SETTING §3), judged when the relief ship comes.</summary>
public sealed record CharterRules
{
    /// <summary>The site where the factory is to stand again: the charter's own ground.</summary>
    public string Site { get; init; } = "coast.ceniza_headland";

    /// <summary>The day after landing that the relief ship comes.</summary>
    public int ReliefDay { get; init; } = 180;

    /// <summary>Logs of embrawood it is to take home.</summary>
    public int Logs { get; init; } = 40;

    /// <summary>The share of those who landed who must be alive.</summary>
    public double AliveShare { get; init; } = 0.5;

    /// <summary>And no fewer than this.</summary>
    public int AliveAtLeast { get; init; } = 4;

    /// <summary>Whether the factory must be fortified.</summary>
    public bool Palisade { get; init; } = true;

    /// <summary>Settlers the relief ship brings when the charter is kept (step P).</summary>
    public int SettlersKept { get; init; } = 8;

    /// <summary>And when it is kept in part.</summary>
    public int SettlersInPart { get; init; } = 4;

    /// <summary>The settlers' names, men and women, in pairs: the first of each list are husband and wife.</summary>
    public IReadOnlyList<string> SettlerMen { get; init; } = [];

    /// <summary>See <see cref="SettlerMen"/>.</summary>
    public IReadOnlyList<string> SettlerWomen { get; init; } = [];
}

/// <summary>Privateers who work the coast for what a new post has (step S).</summary>
public sealed record PrivateerRules
{
    /// <summary>Her name, with its article.</summary>
    public required string Ship { get; init; }

    /// <summary>Her master.</summary>
    public required string Master { get; init; }

    /// <summary>The camp's day she is first about.</summary>
    public int FirstDay { get; init; } = 110;

    /// <summary>Days before another comes.</summary>
    public int EveryDays { get; init; } = 150;

    /// <summary>What she asks to go away, in sueldos.</summary>
    public int Demand { get; init; } = 200;
}

/// <summary>An interloper's ship that puts in at the cove (SETTLEMENT §32).</summary>
public sealed record BrennishRules
{
    /// <summary>Her name.</summary>
    public required string Ship { get; init; }

    /// <summary>Her master.</summary>
    public required string Master { get; init; }

    /// <summary>The day after landing she comes.</summary>
    public int ArriveDay { get; init; } = 50;

    /// <summary>Days she stays.</summary>
    public int StayDays { get; init; } = 12;

    /// <summary>What she sells, and the price in sueldos.</summary>
    public IReadOnlyDictionary<string, int> Sells { get; init; } = new Dictionary<string, int>();

    /// <summary>What she pays for a log of embrawood.</summary>
    public int BuysLog { get; init; } = 25;

    /// <summary>Names of her hands, for one who leaves her for the camp (step P).</summary>
    public IReadOnlyList<string> Names { get; init; } = [];
}
