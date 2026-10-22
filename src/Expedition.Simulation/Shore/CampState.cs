using Expedition.Simulation.Core;

namespace Expedition.Simulation.Shore;

/// <summary>The works of the camp, by ID (docs/R6_LANDFALL_CAMP.md).</summary>
public static class CampWorks
{
    /// <summary>Berths under shelter.</summary>
    public const string Shelter = "shelter";

    /// <summary>The hearth: 1 once made.</summary>
    public const string Fire = "fire";

    /// <summary>The stores' cover: 1 once made.</summary>
    public const string Cover = "cover";

    /// <summary>The latrine: 1 once dug.</summary>
    public const string Latrine = "latrine";

    /// <summary>The sick shelter: 1 once made.</summary>
    public const string SickShelter = "sick_shelter";

    /// <summary>How many sail shelters stand (of the berths in <see cref="Shelter"/>).</summary>
    public const string SailShelters = "shelters_sail";

    /// <summary>How many brush shelters stand.</summary>
    public const string BrushShelters = "shelters_brush";

    /// <summary>Every work, in the order they are shown.</summary>
    public static readonly IReadOnlyList<string> All = [Shelter, Fire, Cover, Latrine, SickShelter];
}

/// <summary>The captain's priorities ashore (SETTLEMENT_EXPERIENCE §4), and what each sets the hands to.</summary>
public static class CampPriorities
{
    /// <summary>The sick and hurt: the sick shelter, tending.</summary>
    public const string Medical = "medical";

    /// <summary>Drinking water: fetching it.</summary>
    public const string Water = "water";

    /// <summary>Shelter from the weather.</summary>
    public const string Shelter = "shelter";

    /// <summary>The stores kept dry and safe: covering them.</summary>
    public const string Food = "food";

    /// <summary>Fire and hot food.</summary>
    public const string Fire = "fire";

    /// <summary>The latrine.</summary>
    public const string Sanitation = "sanitation";

    /// <summary>The stores and tools got ashore from the ship or the wreck.</summary>
    public const string Stores = "stores";

    /// <summary>Scouting.</summary>
    public const string Scouting = "scouting";

    /// <summary>Building (R7): the ordered buildings' phases, the poles and thatch for them, repairs, mending tools.</summary>
    public const string Building = "building";

    /// <summary>The garden (R7): sowing, tending, bringing in.</summary>
    public const string Garden = "garden";

    /// <summary>The order SETTLEMENT_EXPERIENCE §4 gives, with the stores brought forward: nothing is ashore yet. Then the settlement's work.</summary>
    public static readonly IReadOnlyList<string> Default = [Medical, Water, Shelter, Stores, Fire, Food, Sanitation, Garden, Building, Scouting];
}

/// <summary>The four hard choices of the first days (SETTLEMENT_EXPERIENCE §4).</summary>
public sealed record CampChoices
{
    /// <summary>Roof the shelters with the spare sailcloth (quick and dry; the cloth is gone for mending her). Otherwise brush.</summary>
    public bool UseSailcloth { get; init; } = true;

    /// <summary>Let the worn out rest before they work, or work everyone hard.</summary>
    public bool RestTheExhausted { get; init; } = true;

    /// <summary>Send scouts out, or keep everyone together at the camp.</summary>
    public bool SendScouts { get; init; } = true;

    /// <summary>Gather the shore's food, or keep to the ship's stores.</summary>
    public bool Forage { get; init; } = true;
}

/// <summary>A building being raised on its plot, phase by phase (R7).</summary>
public sealed class CampProject
{
    /// <summary>A number for it, in the order ordered.</summary>
    public required int Id { get; init; }

    /// <summary>The building (<see cref="BuildingDefinition.Id"/>).</summary>
    public required string Building { get; init; }

    /// <summary>The plot, for example <c>hut_2</c>.</summary>
    public required string Plot { get; init; }

    /// <summary>The phase it is at.</summary>
    public int Phase { get; internal set; }

    /// <summary>Times the phase's task has been done.</summary>
    public int Done { get; internal set; }

    /// <summary>The quality of the work so far, summed over the tasks done.</summary>
    public double QualitySum { get; internal set; }

    /// <summary>The tasks done so far.</summary>
    public int Worked { get; internal set; }

    /// <summary>Loads of the phase's materials laid ready on the plot by work begun and stopped: the next start uses them.</summary>
    public int Laid { get; internal set; }

    internal void Restore(int phase, int done, double qualitySum, int worked, int laid)
    {
        Phase = phase;
        Done = done;
        QualitySum = qualitySum;
        Worked = worked;
        Laid = laid;
    }
}

/// <summary>A building standing (R7): how well it was made, and how it stands now.</summary>
public sealed class CampBuilding
{
    /// <summary>The building.</summary>
    public required string Building { get; init; }

    /// <summary>Its plot.</summary>
    public required string Plot { get; init; }

    /// <summary>How well it was made, 0..1.</summary>
    public required double Quality { get; init; }

    /// <summary>Its condition, 0..100: weather wears it, the storm most of all; at 0 it is down.</summary>
    public double Condition { get; internal set; } = 100;
}

/// <summary>A garden bed sown (R7).</summary>
public sealed class GardenBed
{
    /// <summary>The crop.</summary>
    public required string Crop { get; init; }

    /// <summary>When it was sown.</summary>
    public required long Sown { get; init; }

    /// <summary>Its health, 0..1: what share of a full crop it will give.</summary>
    public double Health { get; internal set; } = 1;

    /// <summary>When it was last weeded and tended.</summary>
    public long Tended { get; internal set; }
}

/// <summary>
/// The camp ashore, the first days (docs/R6_LANDFALL_CAMP.md): where, since when, how far each work has got,
/// what has been found, what is still afloat, the captain's priorities and choices, and how each person has
/// slept. Saved in format 37.
/// </summary>
public sealed class CampState
{
    private readonly SortedDictionary<string, double> _works = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _found = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> _afloat = new(StringComparer.Ordinal);
    private readonly SortedDictionary<int, int> _sheltered = [];
    private readonly SortedDictionary<int, int> _exposed = [];

    /// <summary>The landing site's ID (a key of <c>data/shore.json</c>'s sites).</summary>
    public required string Site { get; init; }

    /// <summary>When they came ashore.</summary>
    public required long Landed { get; init; }

    /// <summary>True if the ship lies at anchor off the camp; false after a wreck.</summary>
    public bool ShipAtAnchor { get; init; }

    /// <summary>True if there is a boat for landing stores.</summary>
    public bool Boat { get; init; } = true;

    /// <summary>How many came ashore, for the season's judgement.</summary>
    public int LandedCount { get; init; }

    /// <summary>How far each work has got (<see cref="CampWorks"/>): berths for the shelter, 0..1 for the others.</summary>
    public IReadOnlyDictionary<string, double> Works => _works;

    /// <summary>What has been found: water, food and finds by ID.</summary>
    public IReadOnlySet<string> Found => _found;

    /// <summary>What is still afloat, in the ship or the wreck, to be landed.</summary>
    public IReadOnlyDictionary<string, int> Afloat => _afloat;

    /// <summary>The captain's priorities, first first.</summary>
    public IReadOnlyList<string> Priorities { get; internal set; } = CampPriorities.Default;

    /// <summary>The captain's choices.</summary>
    public CampChoices Choices { get; internal set; } = new();

    /// <summary>The fire burns until this tick (0 when never lit).</summary>
    public long FireUntil { get; internal set; }

    /// <summary>Food spoiled in the rain, units.</summary>
    public int Spoiled { get; internal set; }

    /// <summary>Boat trips made.</summary>
    public int Trips { get; internal set; }

    /// <summary>True once the first days' report is made.</summary>
    public bool Reported { get; internal set; }

    private readonly List<CampProject> _projects = [];
    private readonly List<CampBuilding> _buildings = [];
    private readonly List<GardenBed> _beds = [];
    private readonly SortedDictionary<string, int> _broken = new(StringComparer.Ordinal);

    /// <summary>Buildings being raised (R7).</summary>
    public IReadOnlyList<CampProject> Projects => _projects;

    /// <summary>Buildings standing.</summary>
    public IReadOnlyList<CampBuilding> Buildings => _buildings;

    /// <summary>Garden beds sown.</summary>
    public IReadOnlyList<GardenBed> Beds => _beds;

    /// <summary>Broken tools waiting to be mended, by item.</summary>
    public IReadOnlyDictionary<string, int> Broken => _broken;

    /// <summary>Poles cut from the forest's edge so far.</summary>
    public int PolesCut { get; internal set; }

    /// <summary>Bundles of thatch cut so far.</summary>
    public int ThatchCut { get; internal set; }

    /// <summary>The next project's number.</summary>
    public int NextProject { get; internal set; } = 1;

    /// <summary>The first season judged, true when it was survived; null before.</summary>
    public bool? Survived { get; internal set; }

    internal void AddProject(CampProject project) => _projects.Add(project);

    internal void RemoveProject(CampProject project) => _projects.Remove(project);

    internal void AddBuilding(CampBuilding building) => _buildings.Add(building);

    internal void RemoveBuilding(CampBuilding building) => _buildings.Remove(building);

    internal void AddBed(GardenBed bed) => _beds.Add(bed);

    internal void RemoveBed(GardenBed bed) => _beds.Remove(bed);

    internal void Break(string tool, int count = 1)
    {
        var left = _broken.GetValueOrDefault(tool) + count;
        if (left > 0)
        {
            _broken[tool] = left;
        }
        else
        {
            _broken.Remove(tool);
        }
    }

    /// <summary>Nights each person slept under shelter, by character ID.</summary>
    public IReadOnlyDictionary<int, int> NightsSheltered => _sheltered;

    /// <summary>Nights each person spent in the open, by character ID.</summary>
    public IReadOnlyDictionary<int, int> NightsExposed => _exposed;

    /// <summary>How far a work has got.</summary>
    public double Work(string work) => _works.GetValueOrDefault(work);

    internal void AddWork(string work, double amount) => _works[work] = Work(work) + amount;

    internal bool Find(string id) => _found.Add(id);

    internal void PutAfloat(string item, int count)
    {
        if (count > 0)
        {
            _afloat[item] = _afloat.GetValueOrDefault(item) + count;
        }
    }

    internal void TakeAfloat(string item, int count)
    {
        var left = _afloat.GetValueOrDefault(item) - count;
        if (left > 0)
        {
            _afloat[item] = left;
        }
        else
        {
            _afloat.Remove(item);
        }
    }

    internal void Night(CharacterId id, bool sheltered)
    {
        var tally = sheltered ? _sheltered : _exposed;
        tally[id.Value] = tally.GetValueOrDefault(id.Value) + 1;
    }

    /// <summary>Rebuilds a camp from a save.</summary>
    internal void Restore(IReadOnlyDictionary<string, double> works, IEnumerable<string> found, IReadOnlyDictionary<string, int> afloat,
        IReadOnlyDictionary<int, int> sheltered, IReadOnlyDictionary<int, int> exposed)
    {
        foreach (var (work, amount) in works) _works[work] = amount;
        foreach (var id in found) _found.Add(id);
        foreach (var (item, count) in afloat) _afloat[item] = count;
        foreach (var (id, nights) in sheltered) _sheltered[id] = nights;
        foreach (var (id, nights) in exposed) _exposed[id] = nights;
    }
}
