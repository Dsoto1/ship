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
    public static IReadOnlyList<GridPosition>? FindPath(LocationDefinition location, GridPosition from, GridPosition to)
    {
        if (!location.IsWalkable(from) || !location.IsWalkable(to))
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
                if (location.IsWalkable(next) && cameFrom.TryAdd(next, current))
                {
                    frontier.Enqueue(next);
                }
            }

            // Ladders and doors to another level.
            foreach (var next in location.LinkedTo(current))
            {
                if (location.IsWalkable(next) && cameFrom.TryAdd(next, current))
                {
                    frontier.Enqueue(next);
                }
            }
        }

        return null;
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
