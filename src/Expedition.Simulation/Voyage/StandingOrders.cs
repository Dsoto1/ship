namespace Expedition.Simulation.Voyage;

/// <summary>
/// The captain's standing orders (VOYAGE_EXPERIENCE §8, §35; NAVIGATION §7, §9): what the officer of the
/// watch does without asking, and when the captain is to be called. They let time run fast without the
/// captain giving up the ship: each order acted on is told in the chronicle, and each call wakes them
/// (an alert that pauses the game). Unset numbers mean the order is not given.
/// </summary>
public sealed record StandingOrders
{
    /// <summary>Heave to when it blows a storm, and make sail again when it eases.</summary>
    public bool HeaveToInStorm { get; init; }

    /// <summary>Take in sail from pressing on once it blows hard.</summary>
    public bool EaseInHeavyWeather { get; init; }

    /// <summary>When the signs of heavy weather are seen, set hands to batten down, lash the cargo and strike the topmasts.</summary>
    public bool PrepareForWeather { get; init; }

    /// <summary>Call all hands when the bilge passes this (percent), and dismiss them once it is pumped down.</summary>
    public double? AllHandsAtBilge { get; init; }

    /// <summary>Put the crew on short rations when water or food would last fewer than this many days.</summary>
    public double? ShortRationsUnderDays { get; init; }

    /// <summary>Call the captain when water or food would last fewer than this many days.</summary>
    public double? WakeStoresUnderDays { get; init; } = 7;

    /// <summary>Call the captain when the bilge passes this (percent).</summary>
    public double? WakeBilgeOver { get; init; } = 40;

    /// <summary>Call the captain when the reckoning may be out by more than this many leagues.</summary>
    public double? WakeDoubtOver { get; init; }

    /// <summary>Call the captain at noon, for the sun.</summary>
    public bool WakeAtNoon { get; init; }

    /// <summary>Call the captain when the wind shifts by more than this many degrees from where it was.</summary>
    public double? WakeWindShift { get; init; }
}

/// <summary>Why the captain was called, or which order was acted on: the chronicle's words for them.</summary>
public static class StandingOrderReasons
{
    /// <summary>Hove to for a storm.</summary>
    public const string HoveTo = "hoveTo";

    /// <summary>Made sail again after the storm.</summary>
    public const string MadeSail = "madeSail";

    /// <summary>Took in sail from pressing on.</summary>
    public const string Eased = "eased";

    /// <summary>Set hands to the preparations.</summary>
    public const string Preparing = "preparing";

    /// <summary>Called all hands for the bilge.</summary>
    public const string AllHands = "allHands";

    /// <summary>Dismissed them once it was pumped down.</summary>
    public const string Dismissed = "dismissed";

    /// <summary>Put the crew on short rations.</summary>
    public const string Rations = "rations";

    /// <summary>Call: the stores.</summary>
    public const string WakeStores = "wake.stores";

    /// <summary>Call: the bilge.</summary>
    public const string WakeBilge = "wake.bilge";

    /// <summary>Call: the reckoning in doubt.</summary>
    public const string WakeDoubt = "wake.doubt";

    /// <summary>Call: noon.</summary>
    public const string WakeNoon = "wake.noon";

    /// <summary>Call: the wind has shifted.</summary>
    public const string WakeWind = "wake.wind";
}
