namespace Expedition.Simulation.Voyage;

/// <summary>
/// A mark on the far coast that a ship's people might know it by, from the sailing directions
/// (<c>data/voyage.json</c> "route" → "coast"). The coast runs north and south through the destination;
/// a landmark lies <see cref="North"/> leagues north of it (negative south).
/// </summary>
public sealed record Landmark
{
    /// <summary>Stable ID, for example "coast.punta_ceniza".</summary>
    public required string Id { get; init; }

    /// <summary>Its name on the chart.</summary>
    public required string Name { get; init; }

    /// <summary>Leagues north of the destination along the coast (negative: south).</summary>
    public required double North { get; init; }

    /// <summary>What it looks like from offshore: what the sailing directions say to know it by.</summary>
    public required string Looks { get; init; }

    /// <summary>What the lead brings up off it, for example "fine grey sand".</summary>
    public required string Bottom { get; init; }

    /// <summary>A landmark this one is easily taken for, or null.</summary>
    public string? Like { get; init; }
}

/// <summary>
/// The approach to the far coast (NAVIGATION §14, §15, §30, §31): the bank off it that the lead finds,
/// the signs of land, how far off the masthead raises it, and the marks along it. Landfall is not
/// arriving: the coast must be raised, known for what it is, and run along to the destination.
/// </summary>
public sealed record CoastRules
{
    /// <summary>The marks along the coast, from the sailing directions.</summary>
    public IReadOnlyList<Landmark> Landmarks { get; init; } = [];

    /// <summary>The landmark that is the destination itself.</summary>
    public required string Destination { get; init; }

    /// <summary>A landmark can be seen and known within this many leagues of it along the coast.</summary>
    public double LandmarkLeagues { get; init; } = 7;

    /// <summary>How far off the masthead raises the coast by day, in clear weather, leagues.</summary>
    public double SightingLeagues { get; init; } = 6;

    /// <summary>How far off it is seen at night: only the surf, close in.</summary>
    public double NightSightingLeagues { get; init; } = 1.2;

    /// <summary>The part of the sighting range left with no lookout kept.</summary>
    public double NoLookout { get; init; } = 0.15;

    /// <summary>How close she stands in to the coast once it is raised, leagues.</summary>
    public double StandOffLeagues { get; init; } = 2;

    /// <summary>Within this many leagues of the coast without seeing it, she strikes.</summary>
    public double StrikeLeagues { get; init; } = 0.3;

    /// <summary>What striking the coast costs the hull.</summary>
    public double StrikeDamage { get; init; } = 25;

    /// <summary>The bank runs out this far, leagues; beyond it the lead finds no bottom.</summary>
    public double ShelfLeagues { get; init; } = 25;

    /// <summary>The depth at the edge of the bank, and the length of the deep-sea line, fathoms.</summary>
    public double ShelfFathoms { get; init; } = 100;

    /// <summary>The depth close in under the coast, fathoms.</summary>
    public double ShoreFathoms { get; init; } = 4;

    /// <summary>Within this many leagues, the signs of land start to show.</summary>
    public double LandSignLeagues { get; init; } = 40;

    /// <summary>The chance each hour, with a watch kept, of a sign of land.</summary>
    public double LandSignChancePerHour { get; init; } = 0.06;

    /// <summary>By the reckoning, the coast is near within this many leagues: the captain is told once.</summary>
    public double NearLeagues { get; init; } = 30;

    /// <summary>The navigator sounds every so many hours once the reckoning puts her on the bank.</summary>
    public double SoundEveryHours { get; init; } = 2;

    /// <summary>Hours the ship waits for the captain's word on a coast before the best hand aboard decides.</summary>
    public double DecideAfterHours { get; init; } = 3;

    /// <summary>The landmark with this ID, or null.</summary>
    public Landmark? Find(string? id) => Landmarks.FirstOrDefault(l => l.Id == id);

    /// <summary>The landmark in view from <paramref name="north"/> leagues north of the destination, or null on a plain stretch.</summary>
    public Landmark? InView(double north) =>
        Landmarks.Where(l => Math.Abs(l.North - north) <= LandmarkLeagues).OrderBy(l => Math.Abs(l.North - north)).FirstOrDefault();

    /// <summary>What the lead brings up off a stretch of coast: the nearest landmark's bottom.</summary>
    public string BottomAt(double north) => Landmarks.OrderBy(l => Math.Abs(l.North - north)).Select(l => l.Bottom).FirstOrDefault() ?? "sand";

    /// <summary>The depth <paramref name="offshore"/> leagues off the coast, fathoms, or null if beyond the bank.</summary>
    public double? DepthAt(double offshore) =>
        offshore > ShelfLeagues ? null : ShoreFathoms + (ShelfFathoms - ShoreFathoms) * Math.Pow(Math.Max(offshore, 0) / ShelfLeagues, 1.3);

    /// <summary>Problems with these rules, empty if valid.</summary>
    public IEnumerable<string> Validate()
    {
        if (Landmarks.Select(l => l.Id).Distinct(StringComparer.Ordinal).Count() != Landmarks.Count || Find(Destination) is not { North: 0 }
            || Landmarks.Any(l => l.Like is { } like && (like == l.Id || Find(like) is null)))
        {
            yield return "Voyage coast: landmark IDs must be unique, each one's look-alike another landmark, and the destination a landmark at north 0.";
        }

        if (SightingLeagues <= 0 || NightSightingLeagues <= 0 || StandOffLeagues <= 0 || ShelfLeagues <= 0 || ShelfFathoms <= ShoreFathoms
            || NoLookout is < 0 or > 1 || StrikeLeagues < 0 || LandmarkLeagues <= 0 || SoundEveryHours <= 0 || DecideAfterHours <= 0)
        {
            yield return "Voyage coast: ranges, depths and hours must be positive, the shelf deeper than the shore.";
        }
    }
}

/// <summary>A cast of the lead: when, by whom, where by the reckoning, and what it found (no bottom: null depth).</summary>
public sealed record Sounding(long Tick, Core.CharacterId Who, ChartPoint At, double? Fathoms, string? Bottom);
