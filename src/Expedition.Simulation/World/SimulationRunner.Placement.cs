using Expedition.Simulation.Campaign;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Shore;
using Expedition.Simulation.Tasks;

namespace Expedition.Simulation.World;

/// <summary>Whether a building may be set down where asked, what is wrong there, and what its ground will mean.</summary>
/// <param name="Allowed">True when nothing forbids it.</param>
/// <param name="Problems">Why it may not go there.</param>
/// <param name="Notes">What the place will mean for it: drier stores, a long walk, trees to fell.</param>
/// <param name="Trees">Trees standing on the ground, felled in the clearing.</param>
public sealed record PlacementCheck(bool Allowed, IReadOnlyList<string> Problems, IReadOnlyList<string> Notes, int Trees);

/// <summary>A building's ground on the camp's map, inclusive of both corners.</summary>
public readonly record struct Footprint(int X0, int Y0, int X1, int Y1)
{
    /// <summary>True when <paramref name="x"/>, <paramref name="y"/> is on it.</summary>
    public bool Contains(int x, int y) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1;

    /// <summary>True when the two share a cell.</summary>
    public bool Overlaps(Footprint other) => X0 <= other.X1 && other.X0 <= X1 && Y0 <= other.Y1 && other.Y0 <= Y1;
}

/// <summary>
/// The captain sets each building down where they choose (R7): its ground must be dry sand or the forest's
/// edge, clear of the rocks, the driftwood, the camp's own places and the other buildings, and must not shut
/// the way from the hearth to the water, the landing or anywhere else people go: they walk round buildings.
/// Trees on the ground are felled in the clearing, more work and poles
/// for it. Where it stands matters: stores keep drier up the beach, the sick do better away from the huts, a
/// garden at the forest's edge does better than one on the open sand. Each building's two work places are
/// at its front, the sea side.
/// </summary>
public sealed partial class SimulationRunner
{
    // The old fixed plots (format 38 and before), by name: the middle of the ground each building stood on.
    // Used for old saves, and as the first places tried when a building is ordered without a place.
    private static readonly IReadOnlyDictionary<string, (int X, int Y)> LegacyPlots = new SortedDictionary<string, (int X, int Y)>(StringComparer.Ordinal)
    {
        ["hut_1"] = (11, 17), ["hut_2"] = (15, 17), ["hut_3"] = (19, 17), ["hut_4"] = (23, 17),
        ["store_1"] = (28, 14), ["cook_1"] = (15, 8), ["infirmary_1"] = (5, 18), ["workshop_1"] = (30, 10), ["garden_1"] = (27, 18),
    };

    // The work done at a building's front: its phases, the thatch's repair if it is roofed, and what it does
    // once made: the workshop's mending and sawing, the garden's work, the well's water.
    private static IReadOnlyList<string> WorkAt(BuildingDefinition building)
    {
        var tasks = building.Phases.Select(p => p.Task).Distinct().ToList();
        if (building.Roofed)
        {
            tasks.Add("task.repair_building");
        }

        if (building.Mends)
        {
            tasks.AddRange(["task.mend_tools", "task.pit_saw_planks"]);
        }

        if (building.Beds > 0)
        {
            tasks.AddRange(["task.sow_garden", "task.tend_garden", "task.harvest"]);
        }

        if (building.Water)
        {
            tasks.Add("task.fetch_water");
        }

        return tasks;
    }

    // Wet sand: nothing is built at or below this row.
    private const int WetSandRow = 2;

    // Clearing work added for each three trees on the ground, and poles each tree gives.
    private const int TreesPerClearing = 3;
    private const int PolesPerTree = 2;

    /// <summary>Where an old save's fixed plot stood, or null for a placed building's.</summary>
    public static (int X, int Y)? LegacyPlot(string plot) => LegacyPlots.TryGetValue(plot, out var at) ? at : null;

    /// <summary>The ground a building covers with its middle at <paramref name="x"/>, <paramref name="y"/>; turned, its width runs inland.</summary>
    public static Footprint FootprintOf(BuildingDefinition building, int x, int y, bool turned)
    {
        var (across, deep) = turned ? (building.Depth, building.Width) : (building.Width, building.Depth);
        var x0 = x - across / 2;
        var y0 = y - (deep - 1) / 2;
        return new Footprint(x0, y0, x0 + across - 1, y0 + deep - 1);
    }

    private Footprint FootprintOf(CampProject project) => FootprintOf(Settling!.Building(project.Building)!, project.X, project.Y, project.Turned);

    private Footprint FootprintOf(CampBuilding building) => FootprintOf(Settling!.Building(building.Building)!, building.X, building.Y, building.Turned);

    // A building's two work places, at its front: the sea side, or for a landing, which runs into the sea,
    // its head on the dry sand.
    private static (int X, int Y)[] FrontOf(BuildingDefinition building, int x, Footprint ground) =>
        building.Landing ? [(x - 1, ground.Y1 + 1), (x, ground.Y1 + 1)] : [(x - 1, ground.Y0 - 1), (x, ground.Y0 - 1)];

    private (int X, int Y)[] FrontOf(CampProject project) => FrontOf(Settling!.Building(project.Building)!, project.X, FootprintOf(project));

    private (int X, int Y)[] FrontOf(CampBuilding building) => FrontOf(Settling!.Building(building.Building)!, building.X, FootprintOf(building));

    private string? _campSpotsKey;
    private IReadOnlyList<WorkSpotDefinition> _campSpots = [];

    /// <summary>The work places at the buildings, raised or being raised: two at each one's front.</summary>
    public IReadOnlyList<WorkSpotDefinition> CampSpots()
    {
        if (Campaign.Camp is not { } camp || Settling is not { } rules)
        {
            return [];
        }

        var key = $"{camp.Projects.Count}:{camp.Buildings.Count}:{camp.NextProject}:{camp.Projects.FirstOrDefault()?.Plot}:{camp.Buildings.FirstOrDefault()?.Plot}";
        if (key == _campSpotsKey)
        {
            return _campSpots;
        }

        var spots = new List<WorkSpotDefinition>();
        foreach (var (plot, kind, x, y, turned) in camp.Projects.Select(p => (p.Plot, p.Building, p.X, p.Y, p.Turned))
                     .Concat(camp.Buildings.Select(b => (b.Plot, b.Building, b.X, b.Y, b.Turned))))
        {
            if (rules.Building(kind) is not { } building)
            {
                continue;
            }

            var tasks = WorkAt(building);
            var front = FrontOf(building, x, FootprintOf(building, x, y, turned));
            spots.Add(new WorkSpotDefinition { Id = $"plot.{plot}.a", X = front[0].X, Y = front[0].Y, Tasks = tasks, Kind = "HammerStation" });
            spots.Add(new WorkSpotDefinition { Id = $"plot.{plot}.b", X = front[1].X, Y = front[1].Y, Tasks = tasks, Kind = "HammerStation" });
        }

        _campSpotsKey = key;
        _campSpots = spots;
        return spots;
    }

    /// <summary>A work spot in a location by ID, the camp's buildings' work places included.</summary>
    public WorkSpotDefinition? SpotIn(string locationId, string spotId) =>
        Content.Locations.TryGetValue(locationId, out var location) && location.FindSpot(spotId) is { } spot ? spot
        : locationId == CampLocation ? CampSpots().FirstOrDefault(s => s.Id == spotId) : null;

    // What a building set down on this ground would shut off from the hearth, people walking round it and
    // every other: the landing, the water, a place the camp uses, another building's front. Null if nothing.
    private string? WayShut(LocationDefinition location, CampState camp, BuildingDefinition building, Footprint ground, (int X, int Y)[] front)
    {
        if (location.FindSpot("spot.hearth") is not { } hearth)
        {
            return null;
        }

        // People walk round every building but a landing.
        var all = camp.Projects.Where(p => Settling?.Building(p.Building) is { Landing: false }).Select(FootprintOf)
            .Concat(camp.Buildings.Where(b => Settling?.Building(b.Building) is { Landing: false }).Select(FootprintOf)).ToList();
        if (!building.Landing)
        {
            all.Add(ground);
        }
        bool Reached(GridPosition to) => Pathfinder.FindPath(location, hearth.Cell, to, cell => all.Any(g => g.Contains(cell.X, cell.Y))) is not null;
        foreach (var (id, name) in new[] { ("spot.landing_1", "the landing"), ("spot.water_1", "the water") })
        {
            if (location.FindSpot(id) is { } end && !Reached(end.Cell))
            {
                return name;
            }
        }

        if (location.Spots.Any(s => s.Id != hearth.Id && location.IsWalkable(s.Cell) && !all.Any(g => g.Contains(s.X, s.Y)) && !Reached(s.Cell)))
        {
            return "a place the camp uses";
        }

        var fronts = camp.Projects.Select(FrontOf).Concat(camp.Buildings.Select(FrontOf)).Append(front);
        return fronts.SelectMany(f => f).Any(f => !Reached(new GridPosition(f.X, f.Y))) ? "the front of a building" : null;
    }

    /// <summary>
    /// Whether <paramref name="buildingId"/> may be set down with its middle at <paramref name="x"/>,
    /// <paramref name="y"/>, and what its ground will mean for it. Nothing is changed.
    /// </summary>
    public PlacementCheck Placement(string buildingId, int x, int y, bool turned = false)
    {
        var problems = new List<string>();
        var notes = new List<string>();
        if (Campaign.Camp is not { } camp || Settling?.Building(buildingId) is not { } building
            || !Content.Locations.TryGetValue(CampLocation, out var location))
        {
            return new PlacementCheck(false, ["There is nothing to build here."], notes, 0);
        }

        var ground = FootprintOf(building, x, y, turned);
        var front = FrontOf(building, x, ground);
        if (ground.X0 < 1 || ground.X1 > location.Width - 2 || ground.Y1 > location.Height - 2 || ground.Y0 < 1 || (!building.Landing && ground.Y0 - 1 <= 0))
        {
            return new PlacementCheck(false, ["It will not fit there."], notes, 0);
        }

        // A landing runs down into the wet sand, where the boat comes in; nothing else may.
        if (building.Landing ? ground.Y0 > WetSandRow : ground.Y0 <= WetSandRow)
        {
            problems.Add(building.Landing ? "A landing must run down to the water's edge." : "The sand there is wet: the tide comes up to it.");
        }

        var site = CampSite;
        if (building.Landing && site is { Stone: false })
        {
            problems.Add("There is no rock here to gather stone for a landing.");
        }

        if (building.Water && site?.Well is null)
        {
            problems.Add("There is no water to be had by digging here.");
        }

        var trees = 0;
        var fixedSpots = location.Spots.Select(s => (s.X, s.Y)).ToHashSet();
        bool rock = false, spot = false;
        for (var cy = ground.Y0; cy <= ground.Y1; cy++)
        {
            for (var cx = ground.X0; cx <= ground.X1; cx++)
            {
                switch (location.Rows[cy][cx])
                {
                    case LocationDefinition.Fitting:
                        rock = true;
                        break;
                }

                trees += StandingTree(location, camp, cx, cy) ? 1 : 0;
                spot |= fixedSpots.Contains((cx, cy));
            }
        }

        if (rock)
        {
            problems.Add("Rocks and driftwood are in the way.");
        }

        if (spot)
        {
            problems.Add("It would cover a place the camp already uses.");
        }

        var others = camp.Projects.Select(p => (Ground: FootprintOf(p), Front: FrontOf(p)))
            .Concat(camp.Buildings.Select(b => (Ground: FootprintOf(b), Front: FrontOf(b)))).ToList();
        if (others.Any(o => o.Ground.Overlaps(ground) || o.Front.Any(f => ground.Contains(f.X, f.Y)) || front.Any(f => o.Ground.Contains(f.X, f.Y) || o.Front.Contains(f))))
        {
            problems.Add("Another building is in the way.");
        }

        // The two places to work at it must be open ground the hands can reach from the hearth.
        var hearth = location.FindSpot("spot.hearth");
        if (front.Any(f => !location.IsWalkable(new GridPosition(f.X, f.Y)) || fixedSpots.Contains(f)
                           || hearth is not null && Pathfinder.FindPath(location, hearth.Cell, new GridPosition(f.X, f.Y)) is null))
        {
            problems.Add("There is no open ground at its front to work from.");
        }

        // People walk round buildings: it must not shut the way from the hearth to anywhere they go.
        if (problems.Count == 0 && WayShut(location, camp, building, ground, front) is { } shut)
        {
            problems.Add($"It would shut the way from the hearth to {shut}.");
        }

        if (trees > 0)
        {
            notes.Add($"{trees} {(trees == 1 ? "tree" : "trees")} to fell first: more clearing, {trees * PolesPerTree} poles from it.");
        }

        var factor = SitingFactor(building, x, y, camp);
        if (building.Stores)
        {
            notes.Add(factor < 1 ? "High on the beach: the stores keep drier." : factor > 1 ? "Low and damp: the stores will spoil faster." : "The stores keep as well here as anywhere.");
        }

        if (building.SickBerths > 0 && factor > InfirmaryApart)
        {
            notes.Add("Close by the huts: the sickness will pass to the others more.");
        }

        if (building.Beds > 0)
        {
            notes.Add(factor < 1 ? "At the forest's edge: sheltered, better soil." : factor > 1 ? "Open sand: the crops will waste more." : "Middling ground for a garden.");
        }

        if (building.Water && site?.Well is { } well)
        {
            notes.Add(WellFouled(x, y) ? "Close by the latrine: the water will foul." : well.Kind == "brackish" ? "The water will come in brackish." : "Sweet water, at the camp.");
        }

        if (building.Berths > 0 && hearth is not null && Math.Sqrt(Math.Pow(x - hearth.X, 2) + Math.Pow(y - hearth.Y, 2)) > HutFar)
        {
            notes.Add("A long walk from the fire.");
        }

        return new PlacementCheck(problems.Count == 0, problems, notes, trees);
    }

    // A well within this many cells of the latrine is fouled by it.
    private const double WellLatrineDistance = 6;

    private bool WellFouled(int x, int y) =>
        Content.Locations.TryGetValue(CampLocation, out var location) && location.FindSpot("spot.latrine") is { } latrine
        && Math.Sqrt(Math.Pow(latrine.X - x, 2) + Math.Pow(latrine.Y - y, 2)) < WellLatrineDistance;

    // What the ground does for a building: the stores' spoiling, the infirmary's contagion, the garden's waste.
    private const double StoresHigh = 0.85;
    private const double StoresLow = 1.3;
    private const double InfirmaryApart = 0.7;
    private const double InfirmaryNear = 0.9;
    private const double InfirmaryHutDistance = 6;
    private const double GardenSheltered = 0.8;
    private const double GardenOpen = 1.25;
    private const double HutFar = 14;

    private double SitingFactor(BuildingDefinition building, int x, int y, CampState camp)
    {
        if (building.Stores)
        {
            return y >= 13 ? StoresHigh : y <= 8 ? StoresLow : 1;
        }

        if (building.SickBerths > 0)
        {
            var huts = camp.Projects.Select(p => (p.Building, p.X, p.Y)).Concat(camp.Buildings.Select(b => (b.Building, b.X, b.Y)))
                .Where(h => Settling?.Building(h.Building)?.Berths > 0);
            return huts.Any(h => Math.Sqrt(Math.Pow(h.X - x, 2) + Math.Pow(h.Y - y, 2)) < InfirmaryHutDistance) ? InfirmaryNear : InfirmaryApart;
        }

        if (building.Beds > 0)
        {
            return y >= 15 ? GardenSheltered : y <= 10 ? GardenOpen : 1;
        }

        return 1;
    }

    // The same, for a building standing.
    private double SitingFactor(CampBuilding standing) =>
        Campaign.Camp is { } camp && Settling?.Building(standing.Building) is { } building ? SitingFactor(building, standing.X, standing.Y, camp) : 1;

    /// <summary>
    /// Sets a building down where the captain chooses and orders it raised. The hands take its phases in
    /// turn, as the priorities, the materials and the tools allow.
    /// </summary>
    public CommandResult PlaceBuilding(string buildingId, int x, int y, bool turned = false)
    {
        var check = Placement(buildingId, x, y, turned);
        if (!check.Allowed || Campaign.Camp is not { } camp)
        {
            return CommandResult.Rejected(check.Problems.FirstOrDefault() ?? "It cannot go there.");
        }

        var id = camp.NextProject++;
        camp.AddProject(new CampProject { Id = id, Building = buildingId, Plot = $"b{id}", X = x, Y = y, Turned = turned, Trees = check.Trees });
        Campaign.Record(ChronicleEntryKind.ProjectBegun, [new(ChronicleParameters.To, buildingId), new(ChronicleParameters.From, $"b{id}")]);
        return CommandResult.Ok();
    }

    /// <summary>
    /// Orders a building raised without choosing where: on the first of the old plots for it that is free,
    /// or else the best open ground for it, nearest the hearth.
    /// </summary>
    public CommandResult OrderBuilding(string buildingId)
    {
        if (Campaign.Camp is not { } camp || Settling?.Building(buildingId) is not { } building || !Content.Locations.TryGetValue(CampLocation, out var location))
        {
            return CommandResult.Rejected("There is nothing to build here.");
        }

        // The old plots first; else the best ground for it, with the fewest trees to fell, nearest the hearth.
        var hearth = location.FindSpot("spot.hearth");
        var open = Enumerable.Range(3, location.Height - 4).SelectMany(cy => Enumerable.Range(1, location.Width - 2).Select(cx => (X: cx, Y: cy)))
            .OrderBy(at => SitingFactor(building, at.X, at.Y, camp) + 0.05 * TreesOn(location, camp, FootprintOf(building, at.X, at.Y, false)))
            .ThenBy(at => hearth is null ? 0 : Math.Abs(at.X - hearth.X) + Math.Abs(at.Y - hearth.Y))
            .ThenBy(at => at.Y).ThenBy(at => at.X);
        var tried = LegacyPlots.Where(p => p.Key.StartsWith(building.Plot + "_", StringComparison.Ordinal)).Select(p => p.Value).Concat(open);
        foreach (var (x, y) in tried)
        {
            if (Placement(buildingId, x, y).Allowed)
            {
                return PlaceBuilding(buildingId, x, y);
            }
        }

        return CommandResult.Rejected($"There is no ground left for another {building.Name.ToLowerInvariant()}.");
    }

    // Trees standing on a building's ground.
    private static int TreesOn(LocationDefinition location, CampState camp, Footprint ground)
    {
        var trees = 0;
        for (var cy = Math.Max(0, ground.Y0); cy <= Math.Min(location.Height - 2, ground.Y1); cy++)
        {
            for (var cx = Math.Max(1, ground.X0); cx <= Math.Min(location.Width - 2, ground.X1); cx++)
            {
                trees += StandingTree(location, camp, cx, cy) ? 1 : 0;
            }
        }

        return trees;
    }

    // The clearing's work, more for trees on the ground.
    private static int ExtraClearing(CampProject project) => (project.Trees + TreesPerClearing - 1) / TreesPerClearing;

    // How many times a project's phase is done in all.
    private int PhaseTimes(CampProject project, PhaseDefinition phase) =>
        phase.Times + (phase.Task == "task.clear_ground" ? ExtraClearing(project) : 0);

    /// <summary>How many times the phase a project is at is done in all, felling its trees included.</summary>
    public int PhaseTimes(CampProject project) => PhaseOf(project) is { } phase ? PhaseTimes(project, phase) : 0;

    // The clearing done: the trees felled on the ground give poles.
    private void Felled(CampProject project, TaskDefinition phase)
    {
        if (phase.Id == "task.clear_ground" && project.Trees > 0)
        {
            Campaign.SharedStore.TryAdd("item.poles", project.Trees * PolesPerTree, Content.Items);
        }
    }
}
