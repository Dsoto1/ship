namespace Expedition.Simulation.Voyage;

/// <summary>How a leak has been dealt with.</summary>
public enum LeakRepair
{
    /// <summary>Open: the sea comes in at its full rate.</summary>
    Open,

    /// <summary>Plugged in a hurry with oakum and a wedge: most of it stopped, but heavy weather can burst it.</summary>
    Plugged,

    /// <summary>Made good with plank and oakum: stopped for good.</summary>
    Patched,
}

/// <summary>
/// A leak sprung in heavy weather, where it is and how it was met (VOYAGE_EXPERIENCE §22): the ship's
/// history keeps it, "the starboard bow, plugged on day 12, patched on day 14".
/// </summary>
public sealed record Leak
{
    /// <summary>Number, in the order sprung.</summary>
    public required int Id { get; init; }

    /// <summary>Where, for example "the starboard bow".</summary>
    public required string Where { get; init; }

    /// <summary>How much it lets in when open, bilge points an hour in a calm (heavy seas force more).</summary>
    public required double Severity { get; init; }

    /// <summary>How it has been dealt with.</summary>
    public LeakRepair Repair { get; init; }

    /// <summary>When it was sprung.</summary>
    public required long Tick { get; init; }

    /// <summary>When it was last plugged or patched, or -1.</summary>
    public long RepairedTick { get; init; } = -1;

    /// <summary>What it lets in now, before the sea's multiplier: all if open, a quarter plugged, none patched.</summary>
    public double Inflow => Repair switch { LeakRepair.Open => Severity, LeakRepair.Plugged => Severity * 0.25, _ => 0 };
}

/// <summary>The preparations for heavy weather (VOYAGE_EXPERIENCE §21): done by the crew, undone when it is fair again.</summary>
public static class Preparations
{
    /// <summary>Topmasts struck and the topsail yard sent down: far less wear aloft, but she sails slower.</summary>
    public const string Aloft = "aloft";

    /// <summary>Cargo lashed and chocked: nothing shifts.</summary>
    public const string Cargo = "cargo";

    /// <summary>Hatches battened and gratings covered: less water below, and the stores kept dry.</summary>
    public const string Hatches = "hatches";

    /// <summary>All of them, in the order they are best done.</summary>
    public static readonly IReadOnlyList<string> All = [Hatches, Cargo, Aloft];
}

/// <summary>How heavy weather is met (<c>data/voyage.json</c> "storms"): warning, preparation, leaks, cargo.</summary>
public sealed record StormRules
{
    /// <summary>How many hours before a gale or storm its signs are seen.</summary>
    public double WarningHours { get; init; } = 6;

    /// <summary>Heavy weather from this sea state (a gale is 2): preparations count, leaks burst, cargo shifts.</summary>
    public double HeavySeaState { get; init; } = 1.8;

    /// <summary>Below this sea state it is fair enough to sway up the topmasts and cast off the lashings.</summary>
    public double FairSeaState { get; init; } = 1.3;

    /// <summary>The chance a heavy sea striking the hull springs a leak.</summary>
    public double LeakChancePerStrike { get; init; } = 0.5;

    /// <summary>A leak sprung is this severe per point of hull damage the sea did.</summary>
    public double LeakPerStrikeDamage { get; init; } = 0.1;

    /// <summary>The chance each hour in heavy weather that a plugged leak bursts open again.</summary>
    public double PlugBurstChancePerHour { get; init; } = 0.05;

    /// <summary>With the topmasts struck: rigging wear, and the chance of carrying something away, multiplied by these.</summary>
    public double AloftWear { get; init; } = 0.4;

    /// <summary>See <see cref="AloftWear"/>.</summary>
    public double AloftCarryAway { get; init; } = 0.35;

    /// <summary>Her speed with the topmasts struck.</summary>
    public double AloftSpeed { get; init; } = 0.85;

    /// <summary>Water shipped in heavy weather with the hatches battened, as a share of open.</summary>
    public double BattenedLeak { get; init; } = 0.75;

    /// <summary>With the hatches open in heavy weather, the chance each hour of stores wetted below.</summary>
    public double WetChancePerHour { get; init; } = 0.06;

    /// <summary>With the cargo not lashed, the chance each hour of it shifting, per point of sea state above heavy.</summary>
    public double ShiftChancePerHour { get; init; } = 0.12;

    /// <summary>The part of one store lost when cargo shifts (casks stove) or is wetted.</summary>
    public double StoresLost { get; init; } = 0.08;

    /// <summary>What shifting cargo does to the hull.</summary>
    public double ShiftHullDamage { get; init; } = 2;
}
