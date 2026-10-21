namespace Expedition.Simulation.Voyage;

/// <summary>
/// Which watch a hand stands (VOYAGE_EXPERIENCE §9, §17). The ship's company is split in two watches that
/// take the deck in turn, four hours on and four below, the two short dog watches in the evening shifting
/// the round so no one keeps the same hours every day. By day both turn to for the day's work; it is the
/// evening and night watches the watch below sleeps. Day-workers (the cook, the carpenter) work by day
/// and sleep at night.
/// </summary>
public enum Watch
{
    /// <summary>Not in the watch bill: does as before (the captain, passengers).</summary>
    None,

    /// <summary>The starboard watch.</summary>
    Starboard,

    /// <summary>The larboard watch.</summary>
    Larboard,

    /// <summary>A day-worker, idlers in the old word: on deck by day, below at night.</summary>
    Day,
}

/// <summary>The round of the watches, from the clock.</summary>
public static class WatchBill
{
    /// <summary>The hours each watch begins: the first watch at 20:00 runs to midnight, the dog watches split 16:00–20:00.</summary>
    public static readonly IReadOnlyList<int> Changes = [0, 4, 8, 12, 16, 18, 20];

    /// <summary>The names of the watches, by the hour they begin.</summary>
    public static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
    {
        [0] = "the middle watch", [4] = "the morning watch", [8] = "the forenoon watch", [12] = "the afternoon watch",
        [16] = "the first dog watch", [18] = "the last dog watch", [20] = "the first watch",
    };

    /// <summary>Day-workers are on deck from this hour until <see cref="DayEnds"/>.</summary>
    public const int DayBegins = 6;

    /// <summary>When the day-workers go below.</summary>
    public const int DayEnds = 18;

    /// <summary>
    /// The day's work: from this hour until <see cref="DaysWorkEnds"/> both watches turn to the ship's
    /// work, as was the custom; the watch below sleeps the evening and night watches.
    /// </summary>
    public const int DaysWorkBegins = 6;

    /// <summary>When the day's work ends.</summary>
    public const int DaysWorkEnds = 18;

    /// <summary>The watch, of the seven in a day, at this minute (0 the middle watch .. 6 the first watch).</summary>
    public static int Slot(long tick)
    {
        var hour = (int)(tick % World.SimulationClock.TicksPerDay / World.SimulationClock.TicksPerHour);
        var slot = 0;
        for (var i = 0; i < Changes.Count; i++)
        {
            if (hour >= Changes[i])
            {
                slot = i;
            }
        }

        return slot;
    }

    /// <summary>
    /// Which of the two watches has the deck at this minute. Seven watches a day, an odd number, so the
    /// round shifts by one each day: whoever had the middle watch last night has the morning watch tonight.
    /// </summary>
    public static Watch OnDeck(long tick)
    {
        var count = tick / World.SimulationClock.TicksPerDay * Changes.Count + Slot(tick);
        return count % 2 == 0 ? Watch.Starboard : Watch.Larboard;
    }

    /// <summary>True if a hand of this watch is on deck at this minute (or all hands are called).</summary>
    public static bool IsOnDeck(Watch watch, long tick, bool allHands)
    {
        if (allHands || watch == Watch.None)
        {
            return true;
        }

        var hour = tick % World.SimulationClock.TicksPerDay / World.SimulationClock.TicksPerHour;
        if (watch == Watch.Day)
        {
            return hour is >= DayBegins and < DayEnds;
        }

        return hour is >= DaysWorkBegins and < DaysWorkEnds || OnDeck(tick) == watch;
    }

    /// <summary>The name of the watch in progress, for example "the forenoon watch".</summary>
    public static string NameAt(long tick) => Names[Changes[Slot(tick)]];
}
