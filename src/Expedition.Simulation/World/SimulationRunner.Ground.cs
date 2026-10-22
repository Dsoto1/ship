using Expedition.Simulation.Core;
using Expedition.Simulation.Shore;

namespace Expedition.Simulation.World;

/// <summary>
/// The camp's ground over the season (R7): paths are worn only where feet go, a little each time someone
/// walks over a cell, and fade when they stop; what was felled or cleared grows back while no one touches
/// it, scrub, brush, a sapling, a tree again. People walk round buildings, not through them, once the
/// posts are in.
/// </summary>
public sealed partial class SimulationRunner
{
    private GroundRules? GroundRules => Settling?.Ground;

    // ================================================================ walking round buildings

    private string? _blockingKey;
    private IReadOnlyList<Footprint> _blocking = [];

    // The ground no one walks across: buildings standing, and those being raised once the posts go in.
    private IReadOnlyList<Footprint> Blocking()
    {
        if (Campaign.Camp is not { } camp || Settling is not { } rules)
        {
            return [];
        }

        var key = string.Join(",", camp.Projects.Select(p => $"{p.Id}:{p.Phase > 0}")) + "|" + string.Join(",", camp.Buildings.Select(b => b.Plot));
        if (key != _blockingKey)
        {
            _blockingKey = key;
            _blocking = camp.Projects.Where(p => p.Phase > 0 && rules.Building(p.Building) is not null).Select(FootprintOf)
                .Concat(camp.Buildings.Where(b => rules.Building(b.Building) is not null).Select(FootprintOf)).ToList();
        }

        return _blocking;
    }

    // A walking route here: ashore, round the buildings on the slant; whoever stands inside one's ground
    // walks out of it.
    internal IReadOnlyList<GridPosition>? PathIn(LocationDefinition location, GridPosition from, GridPosition to)
    {
        if (location.Id != CampLocation)
        {
            return Pathfinder.FindPath(location, from, to);
        }

        // Across the open beach they cut the corners, as people do.
        var blocking = Blocking();
        return Pathfinder.FindNaturalPath(location, from, to,
            blocking.Count == 0 ? null : cell => blocking.Any(b => b.Contains(cell.X, cell.Y) && !b.Contains(from.X, from.Y)));
    }

    // ================================================================ paths

    // Someone sets off across the camp: every cell on the way is trodden once more, and trampled ground
    // stops growing back.
    private void Tread(IReadOnlyList<GridPosition> path)
    {
        if (Campaign.Camp is not { } camp)
        {
            return;
        }

        var now = Campaign.Clock.Tick;
        for (var i = 1; i < path.Count; i++)
        {
            var key = CampState.Key(path[i].X, path[i].Y);
            var cell = camp.Tread(key);
            cell.Steps++;
            cell.Last = now;
            if (camp.Regrowing.TryGetValue(key, out var growing))
            {
                growing.Since = now;
            }
        }
    }

    /// <summary>How worn a cell is, 0 (not to be seen) to 1 (trodden bare), from how many feet have gone over it.</summary>
    public double PathWear(TroddenCell cell) =>
        GroundRules is { } rules ? Math.Clamp((cell.Steps - rules.PathFaint) / (double)(rules.PathFull - rules.PathFaint), 0, 1) : 0;

    // ================================================================ growing back

    /// <summary>How far a cell has grown back.</summary>
    public GrowthStage Growth(RegrowingCell cell)
    {
        var rules = GroundRules ?? new GroundRules();
        var days = (Campaign.Clock.Tick - cell.Since) / (double)SimulationClock.TicksPerDay;
        return days >= rules.SaplingDays && cell.Tree ? GrowthStage.Sapling
            : days >= rules.BrushDays ? GrowthStage.Brush
            : days >= rules.ScrubDays ? GrowthStage.Scrub
            : GrowthStage.Bare;
    }

    // A tree felled for poles: the standing one nearest where they cut. Cutting there keeps what was
    // felled before from growing back.
    internal void Fell(GridPosition near)
    {
        if (Campaign.Camp is not { } camp || !Content.Locations.TryGetValue(CampLocation, out var location))
        {
            return;
        }

        var covered = AllGround(camp);
        (int X, int Y)? best = null;
        var bestDistance = int.MaxValue;
        for (var y = 18; y < location.Height - 1; y++)
        {
            for (var x = 1; x < location.Width - 1; x++)
            {
                var distance = (x - near.X) * (x - near.X) + (y - near.Y) * (y - near.Y);
                if (distance < bestDistance && StandingTree(location, camp, x, y) && !covered.Any(g => g.Contains(x, y)))
                {
                    best = (x, y);
                    bestDistance = distance;
                }
            }
        }

        if (best is not { } felled)
        {
            return;
        }

        var now = Campaign.Clock.Tick;
        camp.Regrow(CampState.Key(felled.X, felled.Y), new RegrowingCell { Tree = true, Since = now });
        foreach (var (key, growing) in camp.Regrowing)
        {
            var (x, y) = CampState.Cell(key);
            if (Math.Abs(x - felled.X) <= 2 && Math.Abs(y - felled.Y) <= 2)
            {
                growing.Since = now;
            }
        }
    }

    // A tree stands at a cell: the forest's, not felled, or grown back.
    private static bool StandingTree(LocationDefinition location, CampState camp, int x, int y) =>
        y >= 18 && location.Rows[y][x] == LocationDefinition.Blocked && !camp.Regrowing.ContainsKey(CampState.Key(x, y));

    // Every building's ground, standing or being raised.
    private List<Footprint> AllGround(CampState camp) =>
        camp.Projects.Where(p => Settling?.Building(p.Building) is not null).Select(FootprintOf)
            .Concat(camp.Buildings.Where(b => Settling?.Building(b.Building) is not null).Select(FootprintOf)).ToList();

    // A building's ground given up (the work abandoned, or it blew down): left to grow back, trees and all.
    private void GroundLeft(CampState camp, Footprint ground)
    {
        if (!Content.Locations.TryGetValue(CampLocation, out var location))
        {
            return;
        }

        for (var y = Math.Max(1, ground.Y0); y <= Math.Min(location.Height - 2, ground.Y1); y++)
        {
            for (var x = Math.Max(1, ground.X0); x <= Math.Min(location.Width - 2, ground.X1); x++)
            {
                camp.Regrow(CampState.Key(x, y), new RegrowingCell { Tree = location.Rows[y][x] == LocationDefinition.Blocked, Since = Campaign.Clock.Tick });
            }
        }
    }

    // At first light: paths fade where the ground takes them back, faster once left alone; what is left
    // alone grows on, and a tree grown again is poles for the cutting; the thatch comes back if not cut.
    private void StepGroundDay(CampState camp)
    {
        if (GroundRules is not { } rules)
        {
            return;
        }

        var now = Campaign.Clock.Tick;
        DailyRounds(camp);
        foreach (var (key, cell) in camp.Trodden.ToList())
        {
            var alone = (now - cell.Last) / (double)SimulationClock.TicksPerDay > rules.PathUntrodDays;
            cell.Steps -= alone ? Math.Max(1, (int)Math.Ceiling(cell.Steps * rules.PathUntrod)) : (int)(cell.Steps * rules.PathWear);
            if (cell.Steps <= 0)
            {
                camp.Untread(key);
            }
        }

        var covered = AllGround(camp);
        foreach (var (key, growing) in camp.Regrowing.ToList())
        {
            var (x, y) = CampState.Cell(key);
            if (covered.Any(g => g.Contains(x, y)))
            {
                // Built on again: kept clear.
                growing.Since = now;
                continue;
            }

            var days = (now - growing.Since) / (double)SimulationClock.TicksPerDay;
            if (growing.Tree && days >= rules.TreeDays)
            {
                camp.Regrown(key);
                camp.PolesCut = Math.Max(0, camp.PolesCut - PolesPerTreeFelled);
            }
            else if (!growing.Tree && days >= rules.BrushDays)
            {
                camp.Regrown(key);
            }
        }

        if ((now - camp.ThatchLastCut) / (double)SimulationClock.TicksPerDay > rules.ThatchRegrowAfterDays)
        {
            camp.ThatchCut = Math.Max(0, camp.ThatchCut - rules.ThatchRegrowPerDay);
        }
    }

    // The day's comings and goings the work does not walk out step by step: each person ashore and on
    // their feet goes from where they sleep to the fire and back, to the stores, the latrine once dug, and
    // the water once found. Their feet wear those ways, round the buildings, as much as the work does.
    private void DailyRounds(CampState camp)
    {
        if (!Content.Locations.TryGetValue(CampLocation, out var location) || location.FindSpot("spot.hearth") is not { } hearth)
        {
            return;
        }

        // Where they sleep: in the huts once they stand, under the shelters before, else by the fire.
        var huts = camp.Buildings.Where(b => Settling?.Building(b.Building)?.Berths > 0 && b.Condition > StandsAbove)
            .Select(b => SpotIn(CampLocation, $"plot.{b.Plot}.a")).OfType<WorkSpotDefinition>().ToList();
        var shelters = camp.Work(CampWorks.Shelter) > 0
            ? location.Spots.Where(s => s.Id.StartsWith("spot.shelter_", StringComparison.Ordinal)).ToList()
            : [];
        var berths = huts.Count > 0 ? huts : shelters;
        var places = new List<string> { "spot.stores" };
        if (camp.Work(CampWorks.Latrine) >= 1)
        {
            places.Add("spot.latrine");
        }

        if (CampWater() is not null)
        {
            places.Add("spot.water_1");
        }

        var people = Ashore().Where(c => c.CanWork).OrderBy(c => c.Id.Value).ToList();
        for (var i = 0; i < people.Count; i++)
        {
            var berth = berths.Count > 0 ? berths[i % berths.Count].Cell : hearth.Cell;
            var ways = new List<(GridPosition From, GridPosition To)> { (berth, hearth.Cell) };
            ways.AddRange(places.Select(id => location.FindSpot(id)).OfType<WorkSpotDefinition>().Select(s => (hearth.Cell, s.Cell)));
            foreach (var (from, to) in ways)
            {
                if (from != to && PathIn(location, from, to) is { } way)
                {
                    // There and back.
                    Tread(way);
                    Tread(way);
                }
            }
        }
    }

    // Poles cut for each tree felled at the forest's edge.
    private const int PolesPerTreeFelled = 4;
}
