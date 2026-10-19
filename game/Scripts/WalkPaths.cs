using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>Walking routes for people who are not simulated (the crowd, ambient scenes): grid paths made straight.</summary>
public static class WalkPaths
{
    /// <summary>A straightened route between two cells, or null if there is none.</summary>
    public static List<Vector2>? Between(LocationDefinition location, GridPosition from, GridPosition to) =>
        Pathfinder.FindPath(location, from, to) is { } cells ? Smooth(location, cells) : null;

    /// <summary>Straightens a grid path: from each point, on to the farthest one in plain sight.</summary>
    public static List<Vector2> Smooth(LocationDefinition location, IReadOnlyList<GridPosition> cells)
    {
        var points = new List<Vector2> { new(cells[0].X, cells[0].Y) };
        var i = 0;
        while (i < cells.Count - 1)
        {
            var j = cells.Count - 1;
            while (j > i + 1 && !Clear(location, cells[i], cells[j]))
            {
                j--;
            }

            points.Add(new Vector2(cells[j].X, cells[j].Y));
            i = j;
        }

        return points;
    }

    private static bool Clear(LocationDefinition location, GridPosition from, GridPosition to)
    {
        var a = new Vector2(from.X, from.Y);
        var b = new Vector2(to.X, to.Y);
        var steps = Mathf.CeilToInt(a.DistanceTo(b) / 0.2f);
        for (var s = 1; s < steps; s++)
        {
            var p = a.Lerp(b, s / (float)steps);
            foreach (var corner in new[] { new Vector2(-0.3f, -0.3f), new Vector2(0.3f, -0.3f), new Vector2(-0.3f, 0.3f), new Vector2(0.3f, 0.3f) })
            {
                var q = p + corner;
                if (!location.IsWalkable(new GridPosition(Mathf.RoundToInt(q.X), Mathf.RoundToInt(q.Y))))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
