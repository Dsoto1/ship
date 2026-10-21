namespace Expedition.Simulation.Voyage;

/// <summary>How a voyage stands.</summary>
public enum VoyageOutcome
{
    /// <summary>At sea.</summary>
    UnderWay,

    /// <summary>The destination was sighted.</summary>
    Arrived,

    /// <summary>The ship filled and sank.</summary>
    Foundered,

    /// <summary>Nobody is left alive aboard.</summary>
    AllHandsLost,
}

/// <summary>How much sail the captain orders carried (D-018).</summary>
public enum SailPlan
{
    /// <summary>Every sail she can carry: faster, but the ship is driven hard and wears and leaks more.</summary>
    PressOn,

    /// <summary>Sail shortened as the weather requires.</summary>
    Normal,

    /// <summary>Hove to: almost no headway, but she rides out heavy weather with far less strain.</summary>
    HoveTo,
}

/// <summary>A part of the ship whose condition the crew look after.</summary>
public enum ShipPart
{
    /// <summary>The hull: 100 is sound. A damaged hull leaks faster.</summary>
    Hull,

    /// <summary>Water in the bilge: 0 is dry; at 100 she founders.</summary>
    Bilge,

    /// <summary>Masts, yards, sails and ropes: 100 is sound. Worn rigging slows her.</summary>
    Rigging,
}

/// <summary>Changes to the ship's condition, for task effects in data.</summary>
public sealed record ShipEffects
{
    /// <summary>Hull change.</summary>
    public double Hull { get; init; }

    /// <summary>Bilge change (negative pumps water out).</summary>
    public double Bilge { get; init; }

    /// <summary>Rigging change.</summary>
    public double Rigging { get; init; }
}

/// <summary>
/// The ship at sea: her condition, the weather, and how far there is to go. Part of persistent campaign
/// state while the expedition is on a voyage (<see cref="Campaign.CampaignState.Voyage"/>).
/// </summary>
public sealed class VoyageState
{
    /// <summary>Creates a voyage at its start.</summary>
    public VoyageState(string shipName, string destination, double totalLeagues, string weatherId, long weatherUntilTick)
    {
        ShipName = shipName;
        Destination = destination;
        TotalLeagues = totalLeagues;
        WeatherId = weatherId;
        WeatherUntilTick = weatherUntilTick;
    }

    /// <summary>The ship's name, for example "the Garza".</summary>
    public string ShipName { get; }

    /// <summary>Where she is bound, for example "the Ember Coast".</summary>
    public string Destination { get; }

    /// <summary>Length of the passage.</summary>
    public double TotalLeagues { get; }

    /// <summary>Distance made good so far.</summary>
    public double LeaguesSailed { get; internal set; }

    /// <summary>Distance still to go.</summary>
    public double LeaguesRemaining => Math.Max(0, TotalLeagues - LeaguesSailed);

    /// <summary>Hull condition, 0..100.</summary>
    public double Hull { get; internal set; } = 100;

    /// <summary>Water in the bilge, 0..100.</summary>
    public double Bilge { get; internal set; }

    /// <summary>Rigging condition, 0..100.</summary>
    public double Rigging { get; internal set; } = 100;

    /// <summary>Current weather ID.</summary>
    public string WeatherId { get; internal set; }

    /// <summary>Tick at which the weather next changes.</summary>
    public long WeatherUntilTick { get; internal set; }

    /// <summary>Speed in the last minute, leagues per hour.</summary>
    public double SpeedLeaguesPerHour { get; internal set; }

    /// <summary>Whether anyone was standing watch in the last minute.</summary>
    public bool WatchKept { get; internal set; }

    /// <summary>At sea, arrived or foundered.</summary>
    public VoyageOutcome Outcome { get; internal set; }

    /// <summary>How much sail is carried.</summary>
    public SailPlan SailPlan { get; internal set; } = SailPlan.Normal;

    /// <summary>Reads one part's condition.</summary>
    public double Get(ShipPart part) => part switch
    {
        ShipPart.Hull => Hull,
        ShipPart.Bilge => Bilge,
        _ => Rigging,
    };

    private readonly SortedSet<int> _parted = [];

    /// <summary>
    /// Lines carried away in heavy weather, by number (0 up to <see cref="VoyageRules.PartingLines"/>):
    /// which shroud or stay is the view's to say. They stay parted until the rigging is worked on.
    /// </summary>
    public IReadOnlySet<int> PartedLines => _parted;

    /// <summary>True once the main topsail yard has been sprung in a blow; the topsail cannot be set.</summary>
    public bool YardSprung { get; internal set; }

    /// <summary>Rigging work done since the last thing was mended, towards the next (see <see cref="Apply"/>).</summary>
    public double Mending { get; internal set; }

    /// <summary>Points of rigging work it takes to fish a sprung yard.</summary>
    public const double YardMendWork = 12;

    /// <summary>Points of rigging work it takes to splice and set up a parted line.</summary>
    public const double LineMendWork = 5;

    /// <summary>
    /// Applies a change, keeping each value within 0..100. Work on the rigging also mends what heavy
    /// weather carried away: the yard first, then the lines, one at a time as the work adds up.
    /// </summary>
    internal void Apply(ShipEffects effects, double scale)
    {
        Hull = Math.Clamp(Hull + effects.Hull * scale, 0, 100);
        Bilge = Math.Clamp(Bilge + effects.Bilge * scale, 0, 100);
        Rigging = Math.Clamp(Rigging + effects.Rigging * scale, 0, 100);
        if (effects.Rigging * scale <= 0 || (!YardSprung && _parted.Count == 0))
        {
            return;
        }

        Mending += effects.Rigging * scale;
        while (true)
        {
            if (YardSprung && Mending >= YardMendWork)
            {
                Mending -= YardMendWork;
                YardSprung = false;
            }
            else if (!YardSprung && _parted.Count > 0 && Mending >= LineMendWork)
            {
                Mending -= LineMendWork;
                _parted.Remove(_parted.Min);
            }
            else
            {
                break;
            }
        }

        if (!YardSprung && _parted.Count == 0)
        {
            Mending = 0;
        }
    }

    /// <summary>A line carries away.</summary>
    internal void Part(int line) => _parted.Add(line);

    /// <summary>Developer only: carries away these lines and, if asked, springs the yard.</summary>
    public void DeveloperCarryAway(IEnumerable<int> lines, bool yard)
    {
        foreach (var line in lines)
        {
            _parted.Add(line);
        }

        YardSprung |= yard;
    }

    /// <summary>Sets what is carried away while loading a save. The loader validates it.</summary>
    internal void RestoreCarriedAway(IEnumerable<int> parted, bool yardSprung, double mending)
    {
        _parted.Clear();
        _parted.UnionWith(parted);
        YardSprung = yardSprung;
        Mending = mending;
    }

    /// <summary>Developer tool for screenshots and testing: holds this weather until changed.</summary>
    public void DeveloperSetWeather(string weatherId)
    {
        WeatherId = weatherId;
        WeatherUntilTick = long.MaxValue;
    }

    /// <summary>Developer tool for screenshots and testing: sets the bilge level.</summary>
    public void DeveloperSetBilge(double bilge) => Bilge = Math.Clamp(bilge, 0, 99);

    /// <summary>Developer only: sets the hull and rigging, to look at her damaged.</summary>
    public void DeveloperSetCondition(double? hull, double? rigging)
    {
        Hull = Math.Clamp(hull ?? Hull, 1, 100);
        Rigging = Math.Clamp(rigging ?? Rigging, 0, 100);
    }

    /// <summary>Sets every value while loading a save. The loader validates them.</summary>
    internal void Restore(double sailed, double hull, double bilge, double rigging, double speed, bool watchKept, VoyageOutcome outcome)
    {
        LeaguesSailed = sailed;
        Hull = hull;
        Bilge = bilge;
        Rigging = rigging;
        SpeedLeaguesPerHour = speed;
        WatchKept = watchKept;
        Outcome = outcome;
    }
}
