namespace Expedition.Simulation.Shore;

/// <summary>
/// The shore where the expedition lands (<c>data/shore.json</c>; docs/R6_LANDFALL_CAMP.md): how the camp
/// goes, and what each landing site has to be found.
/// </summary>
public sealed record ShoreRules
{
    /// <summary>How the camp goes.</summary>
    public CampRules Rules { get; init; } = new();

    /// <summary>Making the camp last: buildings, crops, the season (R7).</summary>
    public SettlementRules Settlement { get; init; } = new();

    /// <summary>The landing sites, with <c>default</c> for any landing not listed.</summary>
    public IReadOnlyList<ShoreSite> Sites { get; init; } = [];

    /// <summary>The site for a landing (a coast landmark, an island, <c>castrena</c>), or the default.</summary>
    public ShoreSite Site(string id) =>
        Sites.FirstOrDefault(s => s.Id == id) ?? Sites.FirstOrDefault(s => s.Id == "default") ?? new ShoreSite { Id = "default", Name = "the shore" };
}

/// <summary>The numbers of the camp's first days.</summary>
public sealed record CampRules
{
    /// <summary>Berths a finished sail shelter gives.</summary>
    public int SailBerths { get; init; } = 4;

    /// <summary>Berths a finished brush shelter gives.</summary>
    public int BrushBerths { get; init; } = 3;

    /// <summary>Berths the sick shelter gives.</summary>
    public int SickBerths { get; init; } = 3;

    /// <summary>Fatigue an hour for someone out in the open on a wet night.</summary>
    public double ExposedFatiguePerHour { get; init; } = 3;

    /// <summary>Stress an hour for someone out in the open on a wet night.</summary>
    public double ExposedStressPerHour { get; init; } = 2;

    /// <summary>Chance an hour of a chill (a fever) for someone out in the rain.</summary>
    public double ChillPerWetHour { get; init; } = 0.012;

    /// <summary>Hours a fire burns once made up.</summary>
    public double FireHours { get; init; } = 6;

    /// <summary>Share of each food lost each hour of rain while the stores are uncovered.</summary>
    public double SpoilPerRainHour { get; init; } = 0.01;

    /// <summary>Kg the boat brings ashore each trip.</summary>
    public double BoatTripKg { get; init; } = 160;

    /// <summary>Kg brought ashore each trip without a boat, swimming and wading.</summary>
    public double SwimTripKg { get; init; } = 30;

    /// <summary>How much fouler the camp grows each day without a latrine.</summary>
    public double FoulPerDay { get; init; } = 4;

    /// <summary>How much cleaner it comes back each day with one.</summary>
    public double CleanPerDay { get; init; } = 2;

    /// <summary>The chance a scouting trip finds the next thing.</summary>
    public double ScoutChance { get; init; } = 0.45;

    /// <summary>Added for each level of the scout's tracking or foraging.</summary>
    public double ScoutChancePerSkill { get; init; } = 0.1;

    /// <summary>Hours after landing that the first days' report is made.</summary>
    public int ReportHours { get; init; } = 72;
}

/// <summary>A landing site and what it has.</summary>
public sealed record ShoreSite
{
    /// <summary>The landing's ID: a coast landmark, an island, <c>castrena</c> or <c>default</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Its name.</summary>
    public required string Name { get; init; }

    /// <summary>The chance of rain each hour.</summary>
    public double Rain { get; init; } = 0.08;

    /// <summary>The chance of a fever each night hour, for each person, from the swamp air.</summary>
    public double Fever { get; init; }

    /// <summary>Whether there is brush and poles to build with.</summary>
    public bool Brush { get; init; } = true;

    /// <summary>What is seen from the beach on landing.</summary>
    public IReadOnlyList<string> Seen { get; init; } = [];

    /// <summary>Its water.</summary>
    public IReadOnlyList<WaterSource> Water { get; init; } = [];

    /// <summary>Its food.</summary>
    public IReadOnlyList<FoodSource> Food { get; init; } = [];

    /// <summary>Everything else there is to find: ruins, signs of people.</summary>
    public IReadOnlyList<ShoreFind> Finds { get; init; } = [];
}

/// <summary>A source of water.</summary>
public sealed record WaterSource
{
    /// <summary>ID, for example <c>water.factory_spring</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Its name.</summary>
    public required string Name { get; init; }

    /// <summary>spring, stream or brackish. Brackish water may bring on the flux.</summary>
    public required string Kind { get; init; }

    /// <summary>Measures an hour a hand can fill and carry, at normal pace.</summary>
    public double PerHour { get; init; } = 30;

    /// <summary>How far from the camp, in cells of the camp's map (a longer walk, fewer trips).</summary>
    public int Walk { get; init; } = 12;

    /// <summary>Known on landing.</summary>
    public bool Known { get; init; }

    /// <summary>What is seen or guessed of it before it is found; null for nothing.</summary>
    public string? Hint { get; init; }
}

/// <summary>A source of food.</summary>
public sealed record FoodSource
{
    /// <summary>ID.</summary>
    public required string Id { get; init; }

    /// <summary>Its name.</summary>
    public required string Name { get; init; }

    /// <summary>The item taken.</summary>
    public required string Item { get; init; }

    /// <summary>Units an hour at normal pace.</summary>
    public double PerHour { get; init; } = 4;

    /// <summary>Strange food: without someone who knows it, it can make them ill.</summary>
    public bool Unfamiliar { get; init; }

    /// <summary>Known on landing.</summary>
    public bool Known { get; init; }

    /// <summary>What is seen or guessed of it before it is found; null for nothing.</summary>
    public string? Hint { get; init; }
}

/// <summary>Something else found ashore: a ruin, signs of people.</summary>
public sealed record ShoreFind
{
    /// <summary>ID.</summary>
    public required string Id { get; init; }

    /// <summary><c>ruin</c>, <c>people</c> or <c>land</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Its name.</summary>
    public required string Name { get; init; }

    /// <summary>What is found, told.</summary>
    public required string Text { get; init; }

    /// <summary>Planks that can be got from it (a ruin's timbers).</summary>
    public int Planks { get; init; }

    /// <summary>Known on landing.</summary>
    public bool Known { get; init; }

    /// <summary>What is seen or guessed of it before it is found; null for nothing.</summary>
    public string? Hint { get; init; }
}
