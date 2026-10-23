namespace Expedition.Simulation.Shore;

/// <summary>
/// Making the camp last through the first season (R7, docs/R7_ESTABLISHMENT.md; <c>data/shore.json</c>
/// "settlement"): the buildings and their phases, the crops, what the forest's edge gives, the season.
/// </summary>
public sealed record SettlementRules
{
    /// <summary>The buildings that can be raised.</summary>
    public IReadOnlyList<BuildingDefinition> Buildings { get; init; } = [];

    /// <summary>The crops the seed chest holds.</summary>
    public IReadOnlyList<CropDefinition> Crops { get; init; } = [];

    /// <summary>Poles the forest's edge gives near the camp, where there is brush.</summary>
    public int Poles { get; init; } = 140;

    /// <summary>Bundles of thatch it gives.</summary>
    public int Thatch { get; init; } = 260;

    /// <summary>Loads of stone the point's rocks give, where the site has rock.</summary>
    public int Stone { get; init; } = 90;

    /// <summary>The share of that a site without brush has (driftwood, sedge).</summary>
    public double Scarce { get; init; } = 0.2;

    /// <summary>Days after landing that the wet season comes.</summary>
    public int WetFromDay { get; init; } = 20;

    /// <summary>How long the wet season lasts; then the dry, until it comes again a year on (step P).</summary>
    public int WetDays { get; init; } = 170;

    /// <summary>Days in a year.</summary>
    public int YearDays { get; init; } = 365;

    /// <summary>How many times as often it rains in the wet.</summary>
    public double WetRain { get; init; } = 4;

    /// <summary>The day the storm at the change of season comes.</summary>
    public int StormDay { get; init; } = 30;

    /// <summary>How many hours it blows.</summary>
    public int StormHours { get; init; } = 30;

    /// <summary>The day the first season is judged.</summary>
    public int JudgeDay { get; init; } = 90;

    /// <summary>The chance an hour of use that a tool breaks.</summary>
    public double ToolBreaksPerHour { get; init; } = 0.004;

    /// <summary>The chance mending a broken tool saves it.</summary>
    public double MendChance { get; init; } = 0.8;

    /// <summary>How the ground wears into paths and grows back.</summary>
    public GroundRules Ground { get; init; } = new();

    /// <summary>A building by ID, or null.</summary>
    public BuildingDefinition? Building(string id) => Buildings.FirstOrDefault(b => b.Id == id);
}

/// <summary>
/// Paths worn by feet, and the vegetation coming back where it was cut or cleared and is left alone (R7).
/// </summary>
public sealed record GroundRules
{
    /// <summary>Times walked over (less what has grown back) before a cell shows as trodden.</summary>
    public int PathFaint { get; init; } = 40;

    /// <summary>Times walked over for a path worn bare.</summary>
    public int PathFull { get; init; } = 400;

    /// <summary>The share of the wear the ground takes back a day while still walked.</summary>
    public double PathWear { get; init; } = 0.04;

    /// <summary>The share it takes back a day once left alone.</summary>
    public double PathUntrod { get; init; } = 0.12;

    /// <summary>Days without a foot on it before a path is left alone.</summary>
    public int PathUntrodDays { get; init; } = 5;

    /// <summary>Days left alone before cut or cleared ground greens over with scrub.</summary>
    public int ScrubDays { get; init; } = 8;

    /// <summary>Days before brush: cleared sand is then as it was.</summary>
    public int BrushDays { get; init; } = 20;

    /// <summary>Days before a felled tree's place has a sapling.</summary>
    public int SaplingDays { get; init; } = 40;

    /// <summary>Days before it is a tree again, poles for the cutting.</summary>
    public int TreeDays { get; init; } = 75;

    /// <summary>Days without cutting before the thatch grows back.</summary>
    public int ThatchRegrowAfterDays { get; init; } = 10;

    /// <summary>Bundles of thatch that grow back a day after that.</summary>
    public int ThatchRegrowPerDay { get; init; } = 3;
}

/// <summary>A building: where it goes, how it is raised, and what it does.</summary>
public sealed record BuildingDefinition
{
    /// <summary>ID, for example <c>building.hut</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Its name.</summary>
    public required string Name { get; init; }

    /// <summary>The kind of plot it is (for old saves and the fallback placing), for example <c>hut</c>.</summary>
    public required string Plot { get; init; }

    /// <summary>Its footprint across, cells, before it is turned.</summary>
    public int Width { get; init; } = 4;

    /// <summary>Its footprint deep (from the front, the sea side, back), cells.</summary>
    public int Depth { get; init; } = 3;

    /// <summary>The phases, in order.</summary>
    public IReadOnlyList<PhaseDefinition> Phases { get; init; } = [];

    /// <summary>Dry berths it gives, for good.</summary>
    public int Berths { get; init; }

    /// <summary>Berths for the sick, laid apart and tended.</summary>
    public int SickBerths { get; init; }

    /// <summary>Keeps the stores off the ground and dry.</summary>
    public bool Stores { get; init; }

    /// <summary>A roofed hearth: the fire never goes out.</summary>
    public bool Cooks { get; init; }

    /// <summary>Where broken tools are mended.</summary>
    public bool Mends { get; init; }

    /// <summary>Garden beds, once it is made.</summary>
    public int Beds { get; init; }

    /// <summary>A well: water at the camp, where the site has it to dig for.</summary>
    public bool Water { get; init; }

    /// <summary>A landing at the water's edge: the boat lands more each trip; walked over, not round.</summary>
    public bool Landing { get; init; }

    /// <summary>Thatched: worn by the weather and repaired.</summary>
    public bool Roofed => Phases.Any(p => p.Task == "task.thatch_roof");
}

/// <summary>A phase of raising a building: a task, done so many times on its plot.</summary>
public sealed record PhaseDefinition
{
    /// <summary>The task.</summary>
    public required string Task { get; init; }

    /// <summary>How many times it is done.</summary>
    public int Times { get; init; } = 1;
}

/// <summary>A crop from the seed chest.</summary>
public sealed record CropDefinition
{
    /// <summary>ID.</summary>
    public required string Id { get; init; }

    /// <summary>Its name, plural, lower case.</summary>
    public required string Name { get; init; }

    /// <summary>The seed item; a bed takes two measures.</summary>
    public required string Seed { get; init; }

    /// <summary>What it gives.</summary>
    public required string Yields { get; init; }

    /// <summary>Units a healthy bed gives.</summary>
    public int PerBed { get; init; } = 30;

    /// <summary>Days from sowing to harvest.</summary>
    public int GrowDays { get; init; } = 60;

    /// <summary>How well it takes on this shore, 0..1: a bed wastes a little each day by the rest.</summary>
    public double Suitability { get; init; } = 0.5;

    /// <summary>More it wastes each day of the wet season.</summary>
    public double WetRot { get; init; }

    /// <summary>Sown only in the dry season.</summary>
    public bool DrySowingOnly { get; init; }

    /// <summary>What must be learned of the coast's people before it can be grown (R8), or null.</summary>
    public string? Knowledge { get; init; }
}
