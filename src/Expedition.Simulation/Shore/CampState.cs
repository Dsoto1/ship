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

    /// <summary>The palisade round the camp (R8): 0..1, as far as it has got.</summary>
    public const string Palisade = "palisade";

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

/// <summary>How long the camp's working day is (step C).</summary>
public enum WorkHours
{
    /// <summary>The day's work ends three hours sooner, and hands rest sooner: less done, less strain.</summary>
    Short,

    /// <summary>As the work has always been.</summary>
    Normal,

    /// <summary>Three hours more of it, and hands work on more tired: more done, and it tells on them.</summary>
    Long,
}

/// <summary>How much a project matters against the others (ECONOMY_LOOP §29): the hands take the highest first.</summary>
public enum ProjectPriority
{
    /// <summary>Before any other work of the camp.</summary>
    Critical,

    /// <summary>Before the ordinary projects.</summary>
    High,

    /// <summary>As ordered.</summary>
    Normal,

    /// <summary>When nothing else is to be had.</summary>
    Low,

    /// <summary>Not worked at all until taken up again.</summary>
    Paused,
}

/// <summary>Something that happened to a building: a storm weathered, a repair (ECONOMY_LOOP §45).</summary>
/// <param name="Tick">When.</param>
/// <param name="Kind"><c>storm</c> or <c>repaired</c>.</param>
/// <param name="Amount">The condition lost to the storm, or gained by the repair.</param>
public sealed record BuildingEvent(long Tick, string Kind, double Amount);

/// <summary>A building being raised on its plot, phase by phase (R7).</summary>
public sealed class CampProject
{
    /// <summary>A number for it, in the order ordered.</summary>
    public required int Id { get; init; }

    /// <summary>The building (<see cref="BuildingDefinition.Id"/>).</summary>
    public required string Building { get; init; }

    /// <summary>The plot, its name for the work places about it: <c>b3</c> for the third ordered, or an old fixed plot's name.</summary>
    public required string Plot { get; init; }

    /// <summary>Where it stands: the cell at the middle of its footprint.</summary>
    public required int X { get; init; }

    /// <summary>See <see cref="X"/>.</summary>
    public required int Y { get; init; }

    /// <summary>Turned a quarter, its width running inland.</summary>
    public bool Turned { get; init; }

    /// <summary>Trees standing on its ground, to be felled in the clearing.</summary>
    public int Trees { get; init; }

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

    /// <summary>How it stands against the other projects.</summary>
    public ProjectPriority Priority { get; internal set; } = ProjectPriority.Normal;

    /// <summary>Who leads the work, if anyone (ECONOMY_LOOP §31).</summary>
    public CharacterId? Lead { get; internal set; }

    /// <summary>When it was ordered.</summary>
    public long Begun { get; init; }

    private readonly SortedDictionary<int, int> _builders = [];
    private readonly SortedDictionary<string, int> _used = new(StringComparer.Ordinal);

    /// <summary>Who has worked on it: each hand's tasks done.</summary>
    public IReadOnlyDictionary<int, int> Builders => _builders;

    /// <summary>The materials gone into it.</summary>
    public IReadOnlyDictionary<string, int> Used => _used;

    internal void Built(CharacterId by, IReadOnlyDictionary<string, int> used)
    {
        _builders[by.Value] = _builders.GetValueOrDefault(by.Value) + 1;
        foreach (var (item, count) in used)
        {
            _used[item] = _used.GetValueOrDefault(item) + count;
        }
    }

    internal void RestoreRecord(ProjectPriority priority, CharacterId? lead, IReadOnlyDictionary<int, int> builders, IReadOnlyDictionary<string, int> used)
    {
        Priority = priority;
        Lead = lead;
        foreach (var (who, count) in builders)
        {
            _builders[who] = count;
        }

        foreach (var (item, count) in used)
        {
            _used[item] = count;
        }
    }

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

    /// <summary>Where it stands: the cell at the middle of its footprint.</summary>
    public required int X { get; init; }

    /// <summary>See <see cref="X"/>.</summary>
    public required int Y { get; init; }

    /// <summary>Turned a quarter.</summary>
    public bool Turned { get; init; }

    /// <summary>How well it was made, 0..1.</summary>
    public required double Quality { get; init; }

    /// <summary>Its condition, 0..100: weather wears it, the storm most of all; at 0 it is down.</summary>
    public double Condition { get; internal set; } = 100;

    /// <summary>Who leads the work there now (ECONOMY_LOOP §31).</summary>
    public CharacterId? Lead { get; internal set; }

    /// <summary>Who led its building, if anyone did.</summary>
    public CharacterId? BuiltUnder { get; init; }

    /// <summary>A house's household: the number of the household that lives there, or null (step P).</summary>
    public int? Household { get; internal set; }

    /// <summary>When it was ordered, and when finished; null for one from before buildings kept their history.</summary>
    public long? Begun { get; init; }

    /// <summary>See <see cref="Begun"/>.</summary>
    public long? Built { get; init; }

    /// <summary>Who built it: each hand's tasks done.</summary>
    public IReadOnlyDictionary<int, int> Builders { get; init; } = new SortedDictionary<int, int>();

    /// <summary>The materials that went into it.</summary>
    public IReadOnlyDictionary<string, int> Used { get; init; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

    private readonly List<BuildingEvent> _history = [];

    /// <summary>What it has been through since, oldest first.</summary>
    public IReadOnlyList<BuildingEvent> History => _history;

    internal void Happened(BuildingEvent happened) => _history.Add(happened);

    // A storm lasting hours is one storm: its later hours add to the same entry.
    internal void Weathered(long tick, double lost, long sameStormWithin)
    {
        if (_history.Count > 0 && _history[^1] is { Kind: "storm" } last && tick - last.Tick <= sameStormWithin)
        {
            _history[^1] = last with { Amount = last.Amount + lost };
            return;
        }

        _history.Add(new BuildingEvent(tick, "storm", lost));
    }
}

/// <summary>A cell of the camp walked over (R7): how worn it is, and when last trodden.</summary>
public sealed class TroddenCell
{
    /// <summary>Times walked over, less what the ground has taken back.</summary>
    public int Steps { get; internal set; }

    /// <summary>When last walked over.</summary>
    public long Last { get; internal set; }
}

/// <summary>A cell where a tree was felled or ground cleared, growing back while it is left alone (R7).</summary>
public sealed class RegrowingCell
{
    /// <summary>True where a tree stood: it comes back as one. Otherwise ground that greens over.</summary>
    public required bool Tree { get; init; }

    /// <summary>Since when it has been left alone.</summary>
    public long Since { get; internal set; }
}

/// <summary>How far a cell has grown back.</summary>
public enum GrowthStage
{
    /// <summary>Just cut or cleared: a stump, or bare earth.</summary>
    Bare,

    /// <summary>Scrub and grass coming in.</summary>
    Scrub,

    /// <summary>Brush.</summary>
    Brush,

    /// <summary>A young tree.</summary>
    Sapling,
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
    public bool ShipAtAnchor { get; internal set; }

    /// <summary>True if there is a boat for landing stores.</summary>
    public bool Boat { get; internal set; } = true;

    private readonly List<CharacterId> _keepers = [];

    /// <summary>The ship's company: who stays aboard her at anchor to keep her (step S).</summary>
    public IReadOnlyList<CharacterId> Keepers => _keepers;

    internal void SetKeepers(IEnumerable<CharacterId> keepers)
    {
        _keepers.Clear();
        _keepers.AddRange(keepers.Distinct().OrderBy(k => k.Value));
    }

    /// <summary>Guns aboard the ship (step S).</summary>
    public int Guns { get; internal set; }

    /// <summary>Guns landed and mounted on the gun platform.</summary>
    public int GunsAshore { get; internal set; }

    /// <summary>What the ship is away doing (fish, coast), or null while she lies at anchor.</summary>
    public string? Errand { get; internal set; }

    /// <summary>When she is back from her errand.</summary>
    public long ErrandBack { get; internal set; }

    /// <summary>The inventory the camp's stores lie in, where they lie: the shared store while it is the camp.</summary>
    public string? StoreId { get; internal set; }

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

    /// <summary>True while the captain wants the palisade raised (R8).</summary>
    public bool PalisadeOrdered { get; internal set; }

    /// <summary>The camp's working day (step C, ECONOMY_LOOP §34).</summary>
    public WorkHours Hours { get; internal set; } = WorkHours.Normal;

    /// <summary>Days of food the camp keeps in hand before it stops gathering more (§34).</summary>
    public int ReserveFoodDays { get; internal set; } = 4;

    /// <summary>Days of water the camp keeps in hand before it stops fetching more.</summary>
    public int ReserveWaterDays { get; internal set; } = 4;

    private readonly SortedDictionary<string, long> _milestones = new(StringComparer.Ordinal);

    /// <summary>The self-sufficiency milestones reached, and when (§44).</summary>
    public IReadOnlyDictionary<string, long> Milestones => _milestones;

    internal void Reached(string milestone, long tick) => _milestones.TryAdd(milestone, tick);

    /// <summary>The first season judged, true when it was survived; null before.</summary>
    public bool? Survived { get; internal set; }

    private readonly SortedDictionary<int, TroddenCell> _trodden = [];
    private readonly SortedDictionary<int, RegrowingCell> _regrowing = [];

    /// <summary>The cells walked over, by <see cref="Key"/>: the paths, as they are worn.</summary>
    public IReadOnlyDictionary<int, TroddenCell> Trodden => _trodden;

    /// <summary>The cells growing back, by <see cref="Key"/>.</summary>
    public IReadOnlyDictionary<int, RegrowingCell> Regrowing => _regrowing;

    /// <summary>Loads of stone gathered from the rocks.</summary>
    public int StoneGathered { get; internal set; }

    /// <summary>Planks salvaged from a ruin's timbers.</summary>
    public int TimberSalvaged { get; internal set; }

    /// <summary>When thatch was last cut.</summary>
    public long ThatchLastCut { get; internal set; }

    private readonly SortedDictionary<int, SortedDictionary<string, int>> _inHand = [];

    /// <summary>
    /// What each person has in hand, by character number: cut, gathered, drawn or caught, and not yet carried
    /// to the stores (ECONOMY_LOOP §11). It is not in the stores until it is carried there.
    /// </summary>
    public IReadOnlyDictionary<int, SortedDictionary<string, int>> InHand => _inHand;

    internal void Hold(int who, string item, int count)
    {
        if (!_inHand.TryGetValue(who, out var held))
        {
            held = new SortedDictionary<string, int>(StringComparer.Ordinal);
            _inHand[who] = held;
        }

        held[item] = held.GetValueOrDefault(item) + count;
    }

    internal SortedDictionary<string, int>? Release(int who) => _inHand.Remove(who, out var held) ? held : null;

    /// <summary>A cell's key in <see cref="Trodden"/> and <see cref="Regrowing"/>.</summary>
    public static int Key(int x, int y) => y * 1000 + x;

    /// <summary>The cell a key stands for.</summary>
    public static (int X, int Y) Cell(int key) => (key % 1000, key / 1000);

    internal TroddenCell Tread(int key)
    {
        if (!_trodden.TryGetValue(key, out var cell))
        {
            cell = new TroddenCell();
            _trodden[key] = cell;
        }

        return cell;
    }

    internal void Untread(int key) => _trodden.Remove(key);

    internal void Regrow(int key, RegrowingCell cell) => _regrowing[key] = cell;

    internal void Regrown(int key) => _regrowing.Remove(key);

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

    // Rounded, so that ten tenths make a whole.
    internal void AddWork(string work, double amount) => _works[work] = Math.Round(Work(work) + amount, 6);

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
