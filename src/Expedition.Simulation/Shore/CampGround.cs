using Expedition.Simulation.Core;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Shore;

/// <summary>
/// The ground of a camp, made from the beach's plan (data/locations/shore_camp.json), the site's lie of the
/// land (<see cref="ShoreSite.Ground"/>) and the voyage's seed: where the forest's edge runs, in bays and
/// points; groves standing out on the sand and clearings inside the trees; rocks on the beach. The same
/// site and seed always give the same ground, so it is made again on loading and never saved. Every work
/// place stays open and can be walked to from the hearth.
/// </summary>
public static class CampGround
{
    // How each lie of the land shapes the ground: where the edge runs on average and how far it wanders,
    // how thick the trees are behind it, how many groves stand out on the sand, and rocks on the beach.
    private sealed record Profile(double Edge, double Wander, double Thickness, double Groves, int Rocks);

    private static Profile ProfileOf(string ground) => ground switch
    {
        "headland" => new(18.5, 2.2, 0.16, 0.10, 7),
        "river" => new(17, 1.6, 0.34, 0.22, 2),
        "dunes" => new(19.5, 1.4, 0.10, 0.05, 1),
        "mangrove" => new(16.5, 1.2, 0.40, 0.25, 0),
        _ => new(18, 1.8, 0.22, 0.12, 3),
    };

    // Columns kept open into the forest: the scout's way in on the left, the water path on the right.
    private static bool OpenWay(int x) => x is >= 2 and <= 5 or >= 30 and <= 33;

    /// <summary>The camp's ground for <paramref name="site"/> on this voyage.</summary>
    public static LocationDefinition Make(LocationDefinition plan, string site, string ground, ulong seed)
    {
        var profile = ProfileOf(ground);
        var random = new SimulationRandom(Hash(site, seed));
        var (width, height) = (plan.Width, plan.Height);
        var rows = plan.Rows.Select(r => r.ToCharArray()).ToArray();
        var keep = new HashSet<(int, int)>();
        foreach (var spot in plan.Spots)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    keep.Add((spot.X + dx, spot.Y + dy));
                }
            }
        }

        // The edge: two waves of different length laid over each other, so it runs in bays and points.
        var (phase1, phase2) = (random.NextDouble() * 10, random.NextDouble() * 10);
        int EdgeAt(int x) => (int)Math.Round(Math.Clamp(
            profile.Edge + profile.Wander * Math.Sin(x * 0.21 + phase1) + 0.6 * profile.Wander * Math.Sin(x * 0.53 + phase2), 15, 21));

        // A clearing or two inside the trees, where an old fall let the light in.
        var clearings = Enumerable.Range(0, 1 + random.NextInt(2)).Select(_ => (X: 6 + random.NextInt(width - 12), Y: 20 + random.NextInt(3))).ToList();
        for (var y = 14; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                if (rows[y][x] == LocationDefinition.Fitting)
                {
                    continue;
                }

                var into = y - EdgeAt(x);
                var tree = into >= 0
                    ? random.NextDouble() < profile.Thickness + 0.12 * into && !clearings.Any(c => Math.Abs(c.X - x) + Math.Abs(c.Y - y) <= 1)
                    : into >= -3 && random.NextDouble() < profile.Groves * (4 + into) / 4;
                rows[y][x] = tree && !keep.Contains((x, y)) && !OpenWay(x) ? LocationDefinition.Blocked : LocationDefinition.Walkable;
            }
        }

        // The ways in are lanes cut through the trees, lined by them, from the edge on.
        for (var y = 18; y < height - 1; y++)
        {
            foreach (var x in new[] { 1, 6, 29, 34 })
            {
                if (y >= EdgeAt(x) && !keep.Contains((x, y)) && rows[y][x] == LocationDefinition.Walkable)
                {
                    rows[y][x] = LocationDefinition.Blocked;
                }
            }
        }

        // Rocks on the beach, clear of the work places and the camp's middle.
        for (var placed = 0; placed < profile.Rocks; placed++)
        {
            var (x, y) = (1 + random.NextInt(width - 2), 2 + random.NextInt(4));
            if (!keep.Contains((x, y)) && rows[y][x] == LocationDefinition.Walkable && Math.Abs(x - width / 2) > 4)
            {
                rows[y][x] = LocationDefinition.Fitting;
            }
        }

        OpenTheWays(plan, rows);
        return plan with { Rows = rows.Select(r => new string(r)).ToList() };
    }

    // Every work place reachable from the hearth: where one is shut in, the trees between are cleared.
    private static void OpenTheWays(LocationDefinition plan, char[][] rows)
    {
        if (plan.FindSpot("spot.hearth") is not { } hearth)
        {
            return;
        }

        // A few passes at most: each opens one shut place.
        for (var pass = 0; pass < plan.Spots.Count + 1; pass++)
        {
            var reached = new HashSet<(int X, int Y)>();
            var queue = new Queue<(int X, int Y)>([(hearth.X, hearth.Y)]);
            reached.Add((hearth.X, hearth.Y));
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var next in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (next.Item2 >= 0 && next.Item2 < rows.Length && next.Item1 >= 0 && next.Item1 < rows[next.Item2].Length
                        && rows[next.Item2][next.Item1] == LocationDefinition.Walkable && reached.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            if (plan.Spots.FirstOrDefault(s => !reached.Contains((s.X, s.Y))) is not { } shut)
            {
                return;
            }

            // Clear toward the sea from it until the open ground is met.
            for (var y = shut.Y; y > 0 && !reached.Contains((shut.X, y)); y--)
            {
                rows[y][shut.X] = LocationDefinition.Walkable;
            }
        }
    }

    // A stable hash of the site and the seed (FNV-1a, then mixed), the same on every machine.
    private static ulong Hash(string name, ulong seed)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in name)
        {
            hash = (hash ^ c) * 1099511628211UL;
        }

        hash ^= seed + 0x9E3779B97F4A7C15UL;
        hash = (hash ^ (hash >> 30)) * 0xBF58476D1CE4E5B9UL;
        hash = (hash ^ (hash >> 27)) * 0x94D049BB133111EBUL;
        return hash ^ (hash >> 31);
    }
}
