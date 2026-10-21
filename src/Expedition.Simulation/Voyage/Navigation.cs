namespace Expedition.Simulation.Voyage;

/// <summary>
/// Where to steer for, and how a position is reckoned (<c>data/voyage.json</c> "route"). Positions are in
/// leagues east (x) and north (y) of the port. The destination lies at <see cref="Bearing"/> from it; its
/// coast runs north and south through the destination, so a ship that reaches the coast's longitude has
/// made her landfall, wherever along it she fetches up.
/// </summary>
public sealed record RouteRules
{
    /// <summary>Compass bearing from the port to the destination, degrees (0 north, 90 east).</summary>
    public required double Bearing { get; init; }

    /// <summary>The port's latitude, degrees north.</summary>
    public required double StartLatitude { get; init; }

    /// <summary>The port's longitude, degrees east (negative west).</summary>
    public required double StartLongitude { get; init; }

    /// <summary>Leagues to a degree of latitude (17.5 for a Castrenan league).</summary>
    public double LeaguesPerDegree { get; init; } = 17.5;

    /// <summary>The set of the sea, leagues an hour east and north: the steady current of these waters.</summary>
    public IReadOnlyList<double> Current { get; init; } = [0, 0];

    /// <summary>How far each spell of weather adds its own set, leagues an hour at most (more in heavy seas).</summary>
    public double CurrentVariation { get; init; } = 0.04;

    /// <summary>How far wrong the log's reckoning of speed may be on a day (standard deviation, as a fraction).</summary>
    public double SpeedErrorSd { get; init; } = 0.06;

    /// <summary>Error of a careful noon sight in a calm sea, leagues of latitude (standard deviation).</summary>
    public double SightError { get; init; } = 3;

    /// <summary>
    /// How far out from the coast she makes the destination's latitude, leagues, before running it down
    /// due west: the way to make a landfall without a longitude.
    /// </summary>
    public double Offing { get; init; } = 60;

    /// <summary>Fetching the coast within this many leagues of the destination counts as arriving.</summary>
    public double LandfallTolerance { get; init; } = 4;

    /// <summary>Noon sights can be taken from this hour (inclusive) until <see cref="SightUntil"/> (exclusive).</summary>
    public double SightFrom { get; init; } = 11.5;

    /// <summary>The last moment for the noon sight; at this time an appointed navigator takes it.</summary>
    public double SightUntil { get; init; } = 12.5;

    /// <summary>Problems with these rules, empty if valid.</summary>
    public IEnumerable<string> Validate()
    {
        if (Bearing is < 0 or >= 360 || Math.Sin(Bearing * Math.PI / 180) >= -0.2)
        {
            yield return "Voyage route: bearing must be 0..360 and run west (the coast lies across the westward run).";
        }

        if (Current.Count != 2 || LeaguesPerDegree <= 0 || SightError < 0 || SpeedErrorSd < 0 || LandfallTolerance <= 0 || SightFrom >= SightUntil)
        {
            yield return "Voyage route: current must be [east, north]; leagues per degree, tolerance positive; sight window from < until.";
        }
    }
}

/// <summary>A position on the chart, leagues east and north of the port.</summary>
public readonly record struct ChartPoint(double X, double Y)
{
    /// <summary>The distance to another point, leagues.</summary>
    public double DistanceTo(ChartPoint other) => Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));
}

/// <summary>A noon sight: when, who took it, and the latitude it gave, as leagues north of the port.</summary>
public sealed record NoonSight(long Day, Core.CharacterId Taker, double Y, double Error);

/// <summary>
/// Where the ship is and where her captain thinks she is (TECHNICAL_SPEC: truth apart from belief). She
/// steers for the destination from the reckoned position, by dead reckoning: the course steered and the
/// speed the log gives. The truth moves with the current too, and the log is never quite right, so the
/// two drift apart. A noon sight puts the reckoned latitude right (within the sight's error); nothing
/// at sea gives the longitude, which is only known at landfall.
/// </summary>
public sealed class Navigation
{
    private readonly List<ChartPoint> _track = [];
    private readonly List<NoonSight> _sights = [];

    /// <summary>A new passage from the port, bound for <paramref name="destination"/>, making its latitude <paramref name="offing"/> leagues out.</summary>
    public Navigation(ChartPoint destination, double offing = 60)
    {
        Destination = destination;
        Offing = offing;
    }

    /// <summary>Leagues out from the coast where she makes the destination's latitude.</summary>
    public double Offing { get; }

    /// <summary>True once she has made the destination's latitude at the offing and is running it down westward.</summary>
    public bool RunningDown { get; internal set; }

    /// <summary>Where she is bound.</summary>
    public ChartPoint Destination { get; }

    /// <summary>Where she truly is. The captain does not know this.</summary>
    public ChartPoint True { get; internal set; }

    /// <summary>Where the captain reckons she is.</summary>
    public ChartPoint Reckoned { get; internal set; }

    /// <summary>The set of the sea this spell, leagues an hour east and north. Unknown to the captain.</summary>
    public ChartPoint Drift { get; internal set; }

    /// <summary>How far the log over-reads (above 1) or under-reads the speed today. Unknown to the captain.</summary>
    public double LogError { get; internal set; } = 1;

    /// <summary>The course steered, compass degrees, from the reckoned position.</summary>
    public double Heading { get; internal set; }

    /// <summary>True once she has raised the coast; she then runs along it for the destination.</summary>
    public bool Landed { get; internal set; }

    /// <summary>The day of the last noon sight, or -1.</summary>
    public long LastSightDay { get; internal set; } = -1;

    /// <summary>Who takes the noon sight if the captain does not, or null.</summary>
    public Core.CharacterId? Navigator { get; internal set; }

    /// <summary>The reckoned positions, one each noon and at the start: the line on the chart.</summary>
    public IReadOnlyList<ChartPoint> Track => _track;

    /// <summary>Every noon sight taken.</summary>
    public IReadOnlyList<NoonSight> Sights => _sights;

    /// <summary>Leagues still to run, truly: to the coast, then along it.</summary>
    public double TrueRemaining => Landed ? Math.Abs(True.Y - Destination.Y) : True.DistanceTo(Destination);

    /// <summary>Leagues still to run by the captain's reckoning.</summary>
    public double ReckonedRemaining => Landed ? Math.Abs(True.Y - Destination.Y) : Reckoned.DistanceTo(Destination);

    // The course to steer from where the captain reckons she is. First for the offing, a point on the
    // destination's latitude well out from the coast; then due west down that latitude, edging north or
    // south to hold it, until the coast is raised.
    internal double CourseFrom(ChartPoint from)
    {
        if (Landed)
        {
            return Destination.Y - from.Y >= 0 ? 0 : 180;
        }

        if (RunningDown || from.X <= Destination.X + Offing)
        {
            var off = Math.Clamp(Destination.Y - from.Y, -6, 6);
            return (Math.Atan2(-20, off) * 180 / Math.PI + 360) % 360;
        }

        var dx = Destination.X + Offing - from.X;
        var dy = Destination.Y - from.Y;
        return (Math.Atan2(dx, dy) * 180 / Math.PI + 360) % 360;
    }

    /// <summary>
    /// Sails for <paramref name="hours"/> at <paramref name="speed"/> leagues an hour: the reckoning by the
    /// course and the log, the truth with the set of the sea besides. Returns true when she arrives.
    /// </summary>
    internal bool Sail(double speed, double hours, double tolerance)
    {
        Heading = CourseFrom(Reckoned);
        var radians = Heading * Math.PI / 180;
        var (east, north) = (Math.Sin(radians), Math.Cos(radians));
        if (Landed)
        {
            // Along the coast with the land in sight: no reckoning needed, she follows it.
            var step = Math.Min(speed * hours, Math.Abs(Destination.Y - True.Y));
            True = True with { Y = True.Y + Math.Sign(Destination.Y - True.Y) * step };
            Reckoned = True;
            return Math.Abs(True.Y - Destination.Y) <= tolerance;
        }

        True = new ChartPoint(True.X + (east * speed + Drift.X) * hours, True.Y + (north * speed + Drift.Y) * hours);
        Reckoned = new ChartPoint(Reckoned.X + east * speed * LogError * hours, Reckoned.Y + north * speed * LogError * hours);
        if (!RunningDown && Reckoned.X <= Destination.X + Offing + 1)
        {
            RunningDown = true;
        }
        if (True.X > Destination.X)
        {
            return false;
        }

        // The coast: now she knows her longitude, and runs along the shore for the destination.
        Landed = true;
        True = True with { X = Destination.X };
        Reckoned = True;
        return Math.Abs(True.Y - Destination.Y) <= tolerance;
    }

    /// <summary>Moves her along her course (or back), truth and reckoning alike: time gained or lost.</summary>
    internal void Shift(double leagues)
    {
        var radians = CourseFrom(Reckoned) * Math.PI / 180;
        var (dx, dy) = (Math.Sin(radians) * leagues, Math.Cos(radians) * leagues);
        True = new ChartPoint(True.X + dx, True.Y + dy);
        Reckoned = new ChartPoint(Reckoned.X + dx, Reckoned.Y + dy);
    }

    internal void Mark() => _track.Add(Reckoned);

    internal void Sight(NoonSight sight)
    {
        _sights.Add(sight);
        LastSightDay = sight.Day;
        Reckoned = Reckoned with { Y = sight.Y };
    }

    /// <summary>Sets every value while loading a save. The loader validates them.</summary>
    internal void Restore(ChartPoint truth, ChartPoint reckoned, ChartPoint drift, double logError, double heading, bool landed, bool runningDown,
        long lastSightDay, Core.CharacterId? navigator, IEnumerable<ChartPoint> track, IEnumerable<NoonSight> sights)
    {
        (True, Reckoned, Drift, LogError, Heading, Landed, RunningDown, LastSightDay, Navigator) = (truth, reckoned, drift, logError, heading, landed, runningDown, lastSightDay, navigator);
        _track.Clear();
        _track.AddRange(track);
        _sights.Clear();
        _sights.AddRange(sights);
    }
}
