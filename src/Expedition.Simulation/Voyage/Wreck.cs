namespace Expedition.Simulation.Voyage;

/// <summary>
/// The ship lost, as it happened (VOYAGE_EXPERIENCE §39, §40): how, where the survivors came ashore, who
/// was lost, what the boat saved, and what is left in the wreck to be got at later. The survivors keep
/// their needs, injuries and relationships; this records the passage from ship to shore.
/// </summary>
public sealed record WreckState
{
    /// <summary>"foundered" (filled at sea) or "struck" (broke up on the coast).</summary>
    public required string Cause { get; init; }

    /// <summary>When she went.</summary>
    public required long Tick { get; init; }

    /// <summary>Where the survivors came ashore: a coast landmark ID, an island ID, or "castrena" (the home coast).</summary>
    public required string Ashore { get; init; }

    /// <summary>Its name, for the record.</summary>
    public required string AshoreName { get; init; }

    /// <summary>Leagues the boat ran to get there (0 when she broke up on that very coast).</summary>
    public double Leagues { get; init; }

    /// <summary>Days in the boat.</summary>
    public double DaysInBoat { get; init; }

    /// <summary>Whether the boat got away whole; if not, they reached shore as they could.</summary>
    public bool BoatLaunched { get; init; } = true;

    /// <summary>Who came ashore alive.</summary>
    public IReadOnlyList<Core.CharacterId> Survivors { get; init; } = [];

    /// <summary>Who was lost at the wreck or in the boat.</summary>
    public IReadOnlyList<Core.CharacterId> Lost { get; init; } = [];

    /// <summary>What was got into the boat, by item.</summary>
    public IReadOnlyDictionary<string, int> Saved { get; init; } = new Dictionary<string, int>();

    /// <summary>Of it, the water and drink used up in the boat, units.</summary>
    public int Drunk { get; init; }

    /// <summary>What is still in the wreck, to be got at later (nothing when she went down at sea).</summary>
    public IReadOnlyDictionary<string, int> Remains { get; init; } = new Dictionary<string, int>();
}

/// <summary>How a wreck goes (<c>data/voyage.json</c> "wreck").</summary>
public sealed record WreckRules
{
    /// <summary>What the boat carries, kg, people included.</summary>
    public double BoatCapacityKg { get; init; } = 1100;

    /// <summary>Kg a person takes up in the boat.</summary>
    public double PersonKg { get; init; } = 70;

    /// <summary>The boat's way, leagues a day.</summary>
    public double BoatLeaguesPerDay { get; init; } = 22;

    /// <summary>What can be got at in the time there is, by item category (food, water, tool, material, drink...): the share of what is aboard.</summary>
    public IReadOnlyDictionary<string, double> Reach { get; init; } = new Dictionary<string, double>
    {
        ["water"] = 0.5, ["food"] = 0.5, ["tool"] = 0.9, ["material"] = 0.3, ["drink"] = 0.3,
    };

    /// <summary>Breaking up on a coast at night or in heavy weather, what can be got at is this share of the usual.</summary>
    public double SuddenReach { get; init; } = 0.4;

    /// <summary>The chance of being lost at the wreck: foundering slowly at sea.</summary>
    public double FounderLoss { get; init; } = 0.04;

    /// <summary>The chance of being lost at the wreck: breaking up on a coast.</summary>
    public double StruckLoss { get; init; } = 0.15;

    /// <summary>Added for someone asleep below when she struck, for the incapacitated, and in heavy weather.</summary>
    public double BelowLoss { get; init; } = 0.12;

    /// <summary>See <see cref="BelowLoss"/>.</summary>
    public double HelplessLoss { get; init; } = 0.4;

    /// <summary>See <see cref="BelowLoss"/>.</summary>
    public double HeavyWeatherLoss { get; init; } = 0.1;

    /// <summary>The chance the boat is stove in breaking up on a coast.</summary>
    public double BoatStoveChance { get; init; } = 0.35;

    /// <summary>Water each survivor needs a day in the boat, units.</summary>
    public double WaterPerDay { get; init; } = 2;

    /// <summary>The chance each day in the boat, with no water, that a survivor dies.</summary>
    public double ThirstLossPerDay { get; init; } = 0.12;
}
