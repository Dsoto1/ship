using Expedition.Simulation.Core;

namespace Expedition.Simulation.Shore;

/// <summary>How well a place is known (SETTLEMENT §26: fog of knowledge, not of existence).</summary>
public enum PlaceKnowledge
{
    /// <summary>Not known at all.</summary>
    Unknown,

    /// <summary>Told of, perhaps wrongly.</summary>
    Reported,

    /// <summary>Seen, from a distance or the sea.</summary>
    Seen,

    /// <summary>Been to.</summary>
    Visited,
}

/// <summary>What the expedition knows of a place, and who told it.</summary>
public sealed class KnownPlace
{
    /// <summary>How well.</summary>
    public PlaceKnowledge Level { get; internal set; }

    /// <summary>Who or what told it: the sailing directions, a people's ID, a party's journey.</summary>
    public string Source { get; internal set; } = "";
}

/// <summary>A people's standing toward the newcomers (R8).</summary>
public sealed class CommunityState
{
    /// <summary>Trust, -100..100: fair dealing, gifts answered, promises kept, help given.</summary>
    public double Trust { get; internal set; }

    /// <summary>Grievance, 0..100: the opposite, and their ground and wood used without leave.</summary>
    public double Grievance { get; internal set; }

    /// <summary>True once they have met the newcomers face to face.</summary>
    public bool Met { get; internal set; }

    /// <summary>Days they have watched the camp from a distance before coming.</summary>
    public int Watched { get; internal set; }

    /// <summary>When they next mean to come to the camp.</summary>
    public long NextVisit { get; internal set; }

    /// <summary>A gift they gave not yet answered, since this tick; 0 for none.</summary>
    public long GiftOwedSince { get; internal set; }

    /// <summary>Things they have taken in trade, by item: each more of a thing is worth less to them.</summary>
    public SortedDictionary<string, int> Received { get; } = new(StringComparer.Ordinal);

    /// <summary>Leave they have given: <c>wood</c> to cut their dye-wood, <c>ground</c> to stay on their ground.</summary>
    public SortedSet<string> Leave { get; } = new(StringComparer.Ordinal);

    /// <summary>A theft to be taken up with them at their next visit: the item.</summary>
    public string? Grievous { get; internal set; }
}

/// <summary>A party away from the camp on the coast (SETTLEMENT §26).</summary>
public sealed class Journey
{
    /// <summary>A number for it.</summary>
    public required int Id { get; init; }

    /// <summary>Who went.</summary>
    public required IReadOnlyList<CharacterId> Members { get; init; }

    /// <summary>Where to.</summary>
    public required string Destination { get; init; }

    /// <summary>explore, visit, or wood (to cut embrawood).</summary>
    public required string Purpose { get; init; }

    /// <summary>The places on the way, the camp's first.</summary>
    public required IReadOnlyList<string> Path { get; init; }

    /// <summary>When they set out.</summary>
    public required long Left { get; init; }

    /// <summary>When they reach it.</summary>
    public required long Arrives { get; init; }

    /// <summary>When they are back.</summary>
    public required long Returns { get; init; }

    /// <summary>True once they have reached it.</summary>
    public bool Arrived { get; internal set; }

    /// <summary>Logs they bring back.</summary>
    public int Logs { get; internal set; }

    /// <summary>What they found and did, for their report.</summary>
    public List<string> Report { get; } = [];
}

/// <summary>A meeting at the camp waiting for the captain's word (R8): a people's delegation, a theft, a ship.</summary>
public sealed class Meeting
{
    /// <summary>delegation, theft, brennish, or request.</summary>
    public required string Kind { get; init; }

    /// <summary>The people who came, or null for the Brennish.</summary>
    public string? Community { get; init; }

    /// <summary>When they came.</summary>
    public required long Tick { get; init; }

    /// <summary>How far it has got: 0 the greeting, then each thing said.</summary>
    public int Round { get; internal set; }

    /// <summary>A gift they brought (item, count), if any.</summary>
    public (string Item, int Count)? Gift { get; internal set; }

    /// <summary>The item taken, for a theft.</summary>
    public string? Item { get; init; }

    /// <summary>What was put to them and refused at this meeting: not to be pressed again until they next come.</summary>
    public HashSet<string> Refused { get; init; } = [];
}

/// <summary>
/// The world around the camp (R8): what is known of the coast, each people's standing, parties away, the
/// meeting in hand, what has been learned, the interlopers, and the charter. Saved in format 41.
/// </summary>
public sealed class WorldState
{
    private readonly SortedDictionary<string, KnownPlace> _places = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _routes = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, CommunityState> _communities = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, string> _learned = new(StringComparer.Ordinal);
    private readonly List<Journey> _journeys = [];

    /// <summary>Places known, by ID.</summary>
    public IReadOnlyDictionary<string, KnownPlace> Places => _places;

    /// <summary>Routes known, by <see cref="RegionRoute.Key"/>.</summary>
    public IReadOnlySet<string> Routes => _routes;

    /// <summary>Each people's standing.</summary>
    public IReadOnlyDictionary<string, CommunityState> Communities => _communities;

    /// <summary>What has been learned, by topic, with the people who told it.</summary>
    public IReadOnlyDictionary<string, string> Learned => _learned;

    /// <summary>Parties away.</summary>
    public IReadOnlyList<Journey> Journeys => _journeys;

    /// <summary>The meeting waiting for the captain's word, or null.</summary>
    public Meeting? Meeting { get; internal set; }

    /// <summary>The next journey's number.</summary>
    public int NextJourney { get; internal set; } = 1;

    /// <summary>True once the trade peace is broken: someone seized, or a village raided.</summary>
    public bool PeaceBroken { get; internal set; }

    /// <summary>True once the captain has told Mahenu what the palisade is against.</summary>
    public bool PalisadeExplained { get; internal set; }

    /// <summary>True once the palisade's going up has been seen and taken amiss.</summary>
    public bool PalisadeNoticed { get; internal set; }

    /// <summary>When a letter home went with a passing ship; 0 for none.</summary>
    public long LetterSent { get; internal set; }

    /// <summary>The Brennish ship's visit: 0 not yet; 1 at the cove; 2 her boat has come; 3 gone.</summary>
    public int Brennish { get; internal set; }

    /// <summary>True if the captain warned her off in the Company's name.</summary>
    public bool BrennishWarned { get; internal set; }

    /// <summary>Logs sold to the Brennish, against the charter.</summary>
    public int LogsSold { get; internal set; }

    /// <summary>Thefts from the camp.</summary>
    public int Thefts { get; internal set; }

    /// <summary>Gaspar Lezo, the garrison's survivor at Mahenu: 0 not met; 1 met by a party; 2 come to the camp to talk; 3 joined; 4 stays at Mahenu, until a party sees him there again.</summary>
    public int Survivor { get; internal set; }

    /// <summary>The charter's judgement when the relief ship came: kept, in part, or revoked; null before.</summary>
    public string? Charter { get; internal set; }

    internal KnownPlace Know(string place)
    {
        if (!_places.TryGetValue(place, out var known))
        {
            known = new KnownPlace();
            _places[place] = known;
        }

        return known;
    }

    internal bool KnowRoute(string key) => _routes.Add(key);

    internal CommunityState Community(string id)
    {
        if (!_communities.TryGetValue(id, out var state))
        {
            state = new CommunityState();
            _communities[id] = state;
        }

        return state;
    }

    internal bool Learn(string topic, string from) => _learned.TryAdd(topic, from);

    internal void AddJourney(Journey journey) => _journeys.Add(journey);

    internal void EndJourney(Journey journey) => _journeys.Remove(journey);
}
