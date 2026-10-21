namespace Expedition.Simulation.Voyage;

/// <summary>
/// An island off the route where a ship might water (<c>data/voyage.json</c> "route" → "islands";
/// VOYAGE_EXPERIENCE §7, §27; NAVIGATION §21). A charted island is where the chart says. A reported one
/// may not be: whether it is there at all, and how far from where it is drawn, the voyage's seed decides.
/// </summary>
public sealed record Island
{
    /// <summary>Stable ID, for example "island.aguada".</summary>
    public required string Id { get; init; }

    /// <summary>Its name on the chart.</summary>
    public required string Name { get; init; }

    /// <summary>Where the chart puts it: leagues east of the port.</summary>
    public required double X { get; init; }

    /// <summary>Where the chart puts it: leagues north of the port.</summary>
    public required double Y { get; init; }

    /// <summary>True for an island only reported: drawn dashed, perhaps not there.</summary>
    public bool Reported { get; init; }

    /// <summary>For a reported island, the chance it is there at all.</summary>
    public double ExistsChance { get; init; } = 1;

    /// <summary>For a reported island, how far from where it is drawn it may truly lie, leagues.</summary>
    public double PositionError { get; init; }

    /// <summary>How far off its peak is raised by day, leagues.</summary>
    public double SightingLeagues { get; init; } = 9;

    /// <summary>How good its water is to get, 0 none .. 1 a spring by the beach.</summary>
    public double Spring { get; init; } = 1;

    /// <summary>How much there is to forage, 0 bare rock .. 1 fruit and birds for the taking.</summary>
    public double Forage { get; init; } = 0.5;

    /// <summary>True if people live there: a few houses at the watering place.</summary>
    public bool Settled { get; init; }

    /// <summary>What it looks like when raised.</summary>
    public required string Looks { get; init; }

    /// <summary>What its people will sell a ship at anchor, for coin: lots of so many, at so much, so many lots in all.</summary>
    public IReadOnlyList<IslandGoods> Sells { get; init; } = [];

    /// <summary>Where the chart puts it.</summary>
    public ChartPoint Charted => new(X, Y);

    /// <summary>Problems with this island, empty if valid.</summary>
    public IEnumerable<string> Validate()
    {
        if (ExistsChance is < 0 or > 1 || PositionError < 0 || SightingLeagues <= 0 || Spring is < 0 or > 1 || Forage is < 0 or > 1 || (!Reported && (ExistsChance < 1 || PositionError > 0)))
        {
            yield return $"Island '{Id}': chances and spring/forage 0..1, sighting positive; only a reported island may be missing or misplaced.";
        }
    }
}

/// <summary>A lot the islanders sell: so many of an item for so many sueldos, and how many such lots they have.</summary>
public sealed record IslandGoods
{
    /// <summary>The item.</summary>
    public required string Item { get; init; }

    /// <summary>Units in a lot.</summary>
    public required int Quantity { get; init; }

    /// <summary>Sueldos for a lot.</summary>
    public required int Price { get; init; }

    /// <summary>Lots they have to sell this call.</summary>
    public int Stock { get; init; } = 3;

    /// <summary>What it is like, said when it is bought.</summary>
    public string Looks { get; init; } = "";
}

/// <summary>Where a ship is with an island she has made for.</summary>
public sealed record IslandCall
{
    /// <summary>The island she is bound for.</summary>
    public required string Island { get; init; }

    /// <summary>True once it is raised.</summary>
    public bool Sighted { get; init; }

    /// <summary>Where she reckons it lies, once seen; she steers for it by eye.</summary>
    public ChartPoint? SeenAt { get; init; }

    /// <summary>True once she lies at anchor off it.</summary>
    public bool Anchored { get; init; }

    /// <summary>Hours of daylight spent searching where the chart puts it, with nothing in sight.</summary>
    public double Searched { get; init; }
}
