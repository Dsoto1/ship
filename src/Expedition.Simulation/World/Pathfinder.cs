namespace Expedition.Simulation.World;

/// <summary>
/// Shortest walking routes on a location grid. Breadth-first search over the four neighbours,
/// always explored in the same order, so the same request always returns the same path.
/// </summary>
public static class Pathfinder
{
    private static readonly GridPosition[] Steps = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    /// <summary>
    /// Cells from <paramref name="from"/> to <paramref name="to"/>, both included, or null if
    /// <paramref name="to"/> cannot be reached. A path to the same cell has one entry.
    /// </summary>
    public static IReadOnlyList<GridPosition>? FindPath(LocationDefinition location, GridPosition from, GridPosition to) =>
        FindPath(location, from, to, null);

    /// <summary>
    /// The same, with cells <paramref name="blocked"/> says no one may cross for now (buildings standing
    /// ashore) kept out of the way. The start is always allowed.
    /// </summary>
    public static IReadOnlyList<GridPosition>? FindPath(LocationDefinition location, GridPosition from, GridPosition to, Func<GridPosition, bool>? blocked)
    {
        bool Open(GridPosition cell) => location.IsWalkable(cell) && blocked?.Invoke(cell) != true;
        if (!location.IsWalkable(from) || !Open(to))
        {
            return null;
        }

        var cameFrom = new Dictionary<GridPosition, GridPosition> { [from] = from };
        var frontier = new Queue<GridPosition>();
        frontier.Enqueue(from);
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current == to)
            {
                return Reconstruct(cameFrom, from, to);
            }

            foreach (var step in Steps)
            {
                var next = new GridPosition(current.X + step.X, current.Y + step.Y);
                if (Open(next) && cameFrom.TryAdd(next, current))
                {
                    frontier.Enqueue(next);
                }
            }

            // Ladders and doors to another level.
            foreach (var next in location.LinkedTo(current))
            {
                if (Open(next) && cameFrom.TryAdd(next, current))
                {
                    frontier.Enqueue(next);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// A shortest route as people walk it in the open: of all the shortest ways round what is in the way,
    /// the one that keeps nearest the straight line, so it cuts across on the slant rather than going along
    /// and then up. Null if <paramref name="to"/> cannot be reached.
    /// </summary>
    public static IReadOnlyList<GridPosition>? FindNaturalPath(LocationDefinition location, GridPosition from, GridPosition to, Func<GridPosition, bool>? blocked)
    {
        bool Open(GridPosition cell) => location.IsWalkable(cell) && blocked?.Invoke(cell) != true;
        if (!location.IsWalkable(from) || !Open(to))
        {
            return null;
        }

        // How far each cell is from the end, walking.
        var distance = new Dictionary<GridPosition, int> { [to] = 0 };
        var frontier = new Queue<GridPosition>();
        frontier.Enqueue(to);
        while (frontier.Count > 0 && !distance.ContainsKey(from))
        {
            var current = frontier.Dequeue();
            foreach (var next in Neighbours(location, current))
            {
                if ((next == from || Open(next)) && distance.TryAdd(next, distance[current] + 1))
                {
                    frontier.Enqueue(next);
                }
            }
        }

        if (!distance.TryGetValue(from, out var left))
        {
            return null;
        }

        // Then downhill from the start, each step the one nearest the straight way.
        double dx = to.X - from.X, dy = to.Y - from.Y;
        var length = Math.Max(1e-9, Math.Sqrt(dx * dx + dy * dy));
        var path = new List<GridPosition> { from };
        var here = from;
        while (left > 0)
        {
            var best = here;
            var bestOff = double.MaxValue;
            foreach (var next in Neighbours(location, here))
            {
                if (distance.TryGetValue(next, out var d) && d == left - 1)
                {
                    var off = Math.Abs((next.X - from.X) * dy - (next.Y - from.Y) * dx) / length;
                    if (off < bestOff - 1e-9)
                    {
                        best = next;
                        bestOff = off;
                    }
                }
            }

            path.Add(best);
            here = best;
            left--;
        }

        return path;
    }

    private static IEnumerable<GridPosition> Neighbours(LocationDefinition location, GridPosition cell)
    {
        foreach (var step in Steps)
        {
            yield return new GridPosition(cell.X + step.X, cell.Y + step.Y);
        }

        foreach (var next in location.LinkedTo(cell))
        {
            yield return next;
        }
    }

    private static List<GridPosition> Reconstruct(Dictionary<GridPosition, GridPosition> cameFrom, GridPosition from, GridPosition to)
    {
        var path = new List<GridPosition> { to };
        while (path[^1] != from)
        {
            path.Add(cameFrom[path[^1]]);
        }

        path.Reverse();
        return path;
    }
}
