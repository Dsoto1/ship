namespace Expedition.Simulation.World;

/// <summary>
/// Campaign time, counted in fixed ticks. One tick is one in-game minute (see docs/DECISIONS.md D-004).
/// The clock only moves forward. How fast ticks pass in real time is decided by the presentation layer.
/// </summary>
public sealed class SimulationClock
{
    /// <summary>In-game minutes per tick.</summary>
    public const int MinutesPerTick = 1;

    /// <summary>Ticks in one in-game hour.</summary>
    public const int TicksPerHour = 60 / MinutesPerTick;

    /// <summary>Ticks in one in-game day.</summary>
    public const int TicksPerDay = 24 * TicksPerHour;

    /// <summary>Creates a clock at the given tick (0 = midnight starting day 0).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The tick is negative.</exception>
    public SimulationClock(long tick = 0)
    {
        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), tick, "The clock cannot start before tick 0.");
        }

        Tick = tick;
    }

    /// <summary>Ticks elapsed since the campaign began.</summary>
    public long Tick { get; private set; }

    /// <summary>Whole days elapsed since the campaign began.</summary>
    public long Day => Tick / TicksPerDay;

    /// <summary>Time of day as elapsed time since midnight.</summary>
    public TimeSpan TimeOfDay => TimeSpan.FromMinutes(Tick % TicksPerDay * MinutesPerTick);

    /// <summary>Advances the clock by one tick. Called only by the simulation runner.</summary>
    internal void Step() => Tick++;

    /// <summary>Sets the tick while loading a save.</summary>
    internal void Restore(long tick) => Tick = tick;

    /// <summary>Readable time such as <c>Day 3, 14:05</c>.</summary>
    public override string ToString() => $"Day {Day}, {TimeOfDay:hh\\:mm}";
}
