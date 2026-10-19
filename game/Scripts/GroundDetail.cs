using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// A per-cell map of how the ground is used, computed from a location's data (PORT_VISUAL_PASS §3.3):
/// <list type="bullet">
/// <item>red: wear, from the routes people really walk (spot to hub, and each work loop's pickup to dropoff);</item>
/// <item>green: grime, gathering against walls, stalls and stacks;</item>
/// <item>blue: damp, by the water, the well and the fish trade;</item>
/// <item>alpha: what litters the ground and how much: straw, sawdust or fish scales by thirds, the amount within each third.</item>
/// </list>
/// The ground shader reads it, so any map built the same way gets the same treatment. Visual only.
/// </summary>
public static class GroundDetail
{
    private const float Straw = 1f / 3f, Sawdust = 2f / 3f, Scales = 1f;

    /// <summary>Builds the detail texture for <paramref name="location"/>.</summary>
    public static ImageTexture Build(LocationDefinition location, IReadOnlyDictionary<string, Expedition.Simulation.Tasks.TaskDefinition> tasks)
    {
        var width = location.Width;
        var height = location.Height;
        var wear = new float[width, height];
        var grime = new float[width, height];
        var damp = new float[width, height];
        var debris = new float[width, height];
        var debrisWeight = new float[width, height];

        // Wear: routes from every distinct work place to the nearest hub, and along each work loop.
        var hubs = location.Spots.Where(s => s.Kind is "BoardShip" or "ConversationPoint" or "MarketStall").Select(s => s.Cell)
            .Concat([new GridPosition(width / 2, height / 2)]).Where(location.IsWalkable).Distinct().ToList();
        var places = location.Spots.Select(s => s.Cell).Distinct().ToList();
        foreach (var place in places)
        {
            var hub = hubs.OrderBy(h => Math.Abs(h.X - place.X) + Math.Abs(h.Y - place.Y)).FirstOrDefault();
            Walk(location, place, hub, wear, 1f);
        }

        foreach (var task in tasks.Values.Where(t => t.Then is not null))
        {
            var from = location.Spots.Where(s => s.Tasks.Contains(task.Id)).Select(s => s.Cell).Distinct().ToList();
            var to = location.Spots.Where(s => s.Tasks.Contains(task.Then!)).Select(s => s.Cell).Distinct().ToList();
            foreach (var a in from)
            {
                foreach (var b in to)
                {
                    Walk(location, a, b, wear, 3f / Math.Max(1, to.Count));
                }
            }
        }

        // Grime against anything solid; damp along the quay edge and over the piers.
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                if (!location.IsWalkable(new GridPosition(x, y)))
                {
                    continue;
                }

                var near = 0f;
                for (var dx = -2; dx <= 2; dx++)
                {
                    for (var dy = -2; dy <= 2; dy++)
                    {
                        var cell = new GridPosition(x + dx, y + dy);
                        if ((dx != 0 || dy != 0) && cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height && !location.IsWalkable(cell))
                        {
                            near = Math.Max(near, Math.Max(Math.Abs(dx), Math.Abs(dy)) == 1 ? 1f : 0.35f);
                        }
                    }
                }

                grime[x, y] = near;
                if (y >= 33)
                {
                    damp[x, y] = y >= 35 ? 0.8f : 0.45f;
                }
            }
        }

        // Litter and damp by what is done where.
        foreach (var spot in location.Spots)
        {
            var job = string.Join(" ", spot.Tasks);
            if (job.Contains("fish") || job.Contains("catch"))
            {
                Splash(debris, debrisWeight, spot.Cell, 2.2f, Scales);
                Splash(damp, null, spot.Cell, 2.2f, 0.85f);
            }
            else if (job.Contains("carpentry") || job.Contains("timber") || job.Contains("cooper"))
            {
                Splash(debris, debrisWeight, spot.Cell, 2.8f, Sawdust);
            }
            else if (job.Contains("drink") || job.Contains("wait_in_irons") || job.Contains("trade_goods"))
            {
                Splash(debris, debrisWeight, spot.Cell, 1.6f, Straw);
            }
        }

        foreach (var feature in location.Features)
        {
            var centre = new GridPosition((feature.X0 + feature.X1) / 2, (feature.Y0 + feature.Y1) / 2);
            switch (feature.Kind, feature.Style)
            {
                case ("well", _):
                    Splash(damp, null, centre, 2.5f, 0.9f);
                    break;
                case ("stall", "fish"):
                    Splash(debris, debrisWeight, centre, 2.5f, Scales);
                    Splash(damp, null, centre, 2.5f, 0.8f);
                    break;
                case ("stall", _):
                case ("cargo", "bales"):
                case ("gaol_cart", _):
                    Splash(debris, debrisWeight, centre, 2f, Straw);
                    break;
                case ("timber", _):
                case ("sawpit", _):
                case ("hull_on_stocks", _):
                    Splash(debris, debrisWeight, centre, 3.5f, Sawdust);
                    break;
            }
        }

        var max = 1f;
        foreach (var w in wear) max = Math.Max(max, w);
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                // Soften the routes a little so they read as worn bands rather than lines.
                var w = 0f;
                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        var (cx, cy) = (Math.Clamp(x + dx, 0, width - 1), Math.Clamp(y + dy, 0, height - 1));
                        w += wear[cx, cy] * (dx == 0 && dy == 0 ? 0.4f : 0.075f);
                    }
                }

                var amount = Mathf.Clamp(Mathf.Log(1 + w) / Mathf.Log(1 + max * 0.5f), 0, 1);
                // Alpha holds the litter's type in thirds and how much of it there is within the third.
                var type = debris[x, y] >= Scales ? 2 : debris[x, y] >= Sawdust ? 1 : 0;
                var alpha = debrisWeight[x, y] > 0.05f ? (type + Math.Min(0.95f, debrisWeight[x, y])) / 3f : 0f;
                image.SetPixel(x, y, new Color(amount, grime[x, y], Math.Min(1, damp[x, y]), alpha));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static void Walk(LocationDefinition location, GridPosition from, GridPosition to, float[,] wear, float weight)
    {
        if (from == to || Pathfinder.FindPath(location, from, to) is not { } path)
        {
            return;
        }

        foreach (var cell in path)
        {
            wear[cell.X, cell.Y] += weight;
        }
    }

    // Spreads a value round a cell, strongest at the centre; for debris the type with the most weight wins.
    private static void Splash(float[,] field, float[,]? weights, GridPosition at, float radius, float value)
    {
        var r = (int)Math.Ceiling(radius);
        for (var dx = -r; dx <= r; dx++)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                var (x, y) = (at.X + dx, at.Y + dy);
                if (x < 0 || y < 0 || x >= field.GetLength(0) || y >= field.GetLength(1))
                {
                    continue;
                }

                var strength = 1f - Mathf.Sqrt(dx * dx + dy * dy) / (radius + 0.5f);
                if (strength <= 0)
                {
                    continue;
                }

                if (weights is null)
                {
                    field[x, y] = Math.Max(field[x, y], value * strength);
                }
                else if (strength > weights[x, y])
                {
                    weights[x, y] = strength;
                    field[x, y] = value;
                }
            }
        }
    }
}
