using Expedition.Simulation.Voyage;
using Godot;

namespace Expedition.Game;

/// <summary>
/// The far coast seen from the ship once the masthead has raised it (NAVIGATION §25, §30): a strip of land
/// off to the west, grey in the haze when first seen and clearer as she stands in. It is drawn from the
/// sailing directions, so each mark looks as they say: the Río Bermejo a gap in the land with the sea red
/// off it; the Bajos Largos low sand with surf on the bars; Punta Ceniza a rocky headland with a blackened
/// ruin and a lagoon behind; Punta Falsa a headland like it with reefs breaking white; the Manglares a low
/// dark wall. The distances are the view's, not the chart's: a league is a few metres along the shore, and
/// the land stands nearer as she closes it. Visual only; the simulation keeps where she is.
/// </summary>
public partial class CoastView : Node3D
{
    // Metres of shore in the view to a league along the coast.
    private const float PerLeague = 6f;
    private const float Inland = 40f;
    private ShaderMaterial _land = null!;
    private CoastRules? _built;
    private float _fade;

    public override void _Ready()
    {
        _land = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/coast.gdshader") };
        Visible = false;
    }

    /// <summary>Places the coast for this frame, or hides it when no land is in sight.</summary>
    /// <param name="navigation">The voyage's navigation, or null.</param>
    /// <param name="coast">The coast's rules.</param>
    /// <param name="bearing">The route's starting bearing: the world's -z is that course (Main's ship swing is measured from it).</param>
    /// <param name="centre">The ship's centre in the world.</param>
    /// <param name="view">The camera's orthographic size: the land stands at the edge of the view when first raised.</param>
    public void Show(Navigation? navigation, CoastRules? coast, double bearing, Vector3 centre, float view, float darkness, float gloom, float delta)
    {
        if (navigation is not { Sighted: true } || coast is null)
        {
            _fade = 0;
            Visible = false;
            return;
        }

        if (_built != coast)
        {
            Build(coast);
        }

        _fade = Mathf.MoveToward(_fade, 1, delta * 0.25f);
        var north = World(0, bearing);
        var west = World(270, bearing);
        var toSea = -west;
        var up = toSea.Cross(north);
        Basis = up.Y >= 0 ? new Basis(north, up, toSea) : new Basis(-north, -up, toSea);
        var flip = up.Y >= 0 ? 1f : -1f;
        var offshore = (float)navigation.Offshore;
        // At the edge of the view when first raised, nearer as she stands in; never on top of her.
        var distance = Mathf.Max(20f, view * Mathf.Lerp(0.82f, 0.56f, Mathf.Clamp((6f - offshore) / 4f, 0, 1)));
        var along = (float)(navigation.True.Y - navigation.Destination.Y) * PerLeague * flip;
        Position = centre + west * distance - Basis.X * along + new Vector3(0, 0, 0);
        _land.SetShaderParameter("haze", Mathf.Lerp(1f, Mathf.Clamp((offshore - 1.5f) / 5f, 0.12f, 0.75f) + gloom * 0.2f, _fade));
        _land.SetShaderParameter("darkness", darkness);
        Visible = true;
    }

    // The world direction of a compass bearing: the world's -z is the course she set out on.
    private static Vector3 World(double compass, double bearing)
    {
        var a = Mathf.DegToRad((float)(compass - bearing));
        return new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a));
    }

    private void Build(CoastRules coast)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _built = coast;
        var marks = coast.Landmarks.Select(l => (Id: l.Id, X: (float)l.North * PerLeague)).ToList();
        float Near(string id, float x, float width) =>
            marks.Where(m => m.Id == id).Select(m => Mathf.Exp(-(x - m.X) * (x - m.X) / (2 * width * width))).DefaultIfEmpty(0).Max();
        float MarkX(string id) => marks.FirstOrDefault(m => m.Id == id).X;

        // The shore line: headlands stand out towards the sea.
        float Shore(float x) => 4.5f * (Near("coast.punta_ceniza", x, 5) + Near("coast.punta_falsa", x, 5));

        (float Height, Color Colour) Ground(float x, float depth)
        {
            var hills = 3.5f + 2.5f * Mathf.Sin(x * 0.031f) + 1.6f * Mathf.Sin(x * 0.077f + 1f) + 0.6f * Mathf.Sin(x * 0.23f);
            var rise = Mathf.SmoothStep(1.5f, 7f, depth);
            var height = Mathf.Max(hills, 1.5f) * rise;
            var forest = new Color(0.2f, 0.31f, 0.18f).Lerp(new Color(0.3f, 0.36f, 0.24f), Mathf.Clamp(height / 8f, 0, 1));
            // Surf on the beach's edge, then sand, then the forest.
            var colour = depth < 0.6f ? new Color(0.86f, 0.87f, 0.83f) : depth < 1.6f ? new Color(0.72f, 0.66f, 0.5f) : forest;

            // Low sand spits and palms: the bars.
            var bars = Near("coast.bajos_largos", x, 18);
            height = Mathf.Lerp(height, 0.4f + 0.5f * Mathf.SmoothStep(3f, 14f, depth), bars);
            colour = colour.Lerp(depth < 8f ? new Color(0.8f, 0.75f, 0.58f) : new Color(0.26f, 0.36f, 0.2f), bars);

            // The mangrove wall: low, flat, dark, with no beach.
            var mangrove = Near("coast.manglares", x, 22);
            height = Mathf.Lerp(height, 2.2f * Mathf.SmoothStep(0f, 1.5f, depth), mangrove);
            colour = colour.Lerp(new Color(0.13f, 0.22f, 0.13f), mangrove);

            // Rocky headlands.
            var rock = Near("coast.punta_ceniza", x, 4.5f) + Near("coast.punta_falsa", x, 4.5f);
            var headland = 5.5f * rock * (1 - Mathf.SmoothStep(4f, 16f, depth));
            height = Mathf.Max(height, headland + Mathf.Min(depth, 2f) * rock);
            colour = colour.Lerp(new Color(0.44f, 0.41f, 0.37f), Mathf.Clamp(rock * 1.3f, 0, 1) * (1 - Mathf.SmoothStep(8f, 16f, depth)));

            // The lagoon behind Ceniza: flat water among the trees.
            var ceniza = MarkX("coast.punta_ceniza");
            if (Mathf.Abs(x - ceniza) < 13f && depth is > 13f and < 27f)
            {
                height = -0.4f;
            }

            // The river mouth: a gap right through.
            if (Mathf.Abs(x - MarkX("coast.rio_bermejo")) < 5f)
            {
                height = -0.6f;
            }

            return (height, colour);
        }

        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        const float step = 2f;
        var from = marks.Min(m => m.X) - 140f;
        var to = marks.Max(m => m.X) + 140f;
        var columns = (int)((to - from) / step);
        var depths = new[] { 0f, 0.6f, 1.6f, 3f, 4f, 6f, 9f, 13f, 17f, 22f, 27f, 33f, Inland };
        var grid = new (Vector3 At, Color Colour)[columns + 1, depths.Length];
        for (var c = 0; c <= columns; c++)
        {
            var x = from + c * step;
            for (var r = 0; r < depths.Length; r++)
            {
                var (height, colour) = Ground(x, depths[r]);
                // The seaward edge dips under the water so the land meets the sea cleanly.
                var y = SeaAndSky.WaterLevel + (r == 0 ? -0.5f : height + 0.15f);
                grid[c, r] = (new Vector3(x, y, Shore(x) - depths[r]), colour);
            }
        }

        for (var c = 0; c < columns; c++)
        {
            for (var r = 0; r < depths.Length - 1; r++)
            {
                foreach (var (cc, rr) in new[] { (c, r), (c + 1, r), (c + 1, r + 1), (c, r), (c + 1, r + 1), (c, r + 1) })
                {
                    tool.SetColor(grid[cc, rr].Colour);
                    tool.AddVertex(grid[cc, rr].At);
                }
            }
        }

        tool.GenerateNormals();
        AddChild(new MeshInstance3D { Mesh = tool.Commit(), MaterialOverride = _land, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        var water = SeaAndSky.WaterLevel;
        // The lagoon's still water, and the red-brown stain off the river.
        var ceniza = MarkX("coast.punta_ceniza");
        Flat(new Vector3(ceniza, water + 0.05f, Shore(ceniza) - 20f), new Vector2(26f, 14f), new Color(0.32f, 0.5f, 0.5f, 0.95f));
        var river = MarkX("coast.rio_bermejo");
        Flat(new Vector3(river, water + 0.04f, 9f), new Vector2(30f, 26f), new Color(0.5f, 0.26f, 0.15f, 0.5f), soft: true);
        Flat(new Vector3(river, water + 0.05f, -14f), new Vector2(10f, 30f), new Color(0.36f, 0.3f, 0.22f, 0.95f));

        // The ruin on Ceniza's shoulder: blackened walls, roofless.
        var ruin = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.1f, 0.09f), Roughness = 1f };
        var top = SeaAndSky.WaterLevel + Ground(ceniza, 6f).Height + 0.15f;
        foreach (var (dx, dz, size) in new[] { (-1.6f, 0f, new Vector3(2.6f, 1.3f, 0.3f)), (0f, -1.2f, new Vector3(0.3f, 1.1f, 2.4f)), (1.4f, 0.4f, new Vector3(0.3f, 0.7f, 1.6f)), (-0.2f, 1.1f, new Vector3(1.8f, 0.5f, 0.3f)) })
        {
            AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = ruin, Position = new Vector3(ceniza + dx, top + size.Y / 2f, Shore(ceniza) - 6f + dz) });
        }

        // Surf: on the bars off the Bajos Largos, and breaking on the reefs off Punta Falsa.
        var surf = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.96f, 0.94f), Roughness = 0.8f, EmissionEnabled = true, Emission = new Color(0.25f, 0.25f, 0.25f) };
        var bars = MarkX("coast.bajos_largos");
        foreach (var (offset, length) in new[] { (5f, 60f), (9f, 40f), (12f, 22f) })
        {
            AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(length, 0.12f, 0.5f) }, MaterialOverride = surf, Position = new Vector3(bars + offset * 0.7f, water + 0.05f, offset) });
        }

        var falsa = MarkX("coast.punta_falsa");
        foreach (var (dx, dz, length) in new[] { (-4f, 9f, 3.5f), (-1.5f, 9.6f, 2f), (3f, 11f, 4f), (5.5f, 10.4f, 2f), (8f, 8.5f, 3f), (-8f, 11.5f, 2.5f) })
        {
            Flat(new Vector3(falsa + dx, water + 0.06f, dz), new Vector2(length, 1.3f), new Color(0.94f, 0.95f, 0.93f, 0.85f), soft: true);
        }

    }

    // A flat patch on the water; soft ones fade out to their edges.
    private void Flat(Vector3 at, Vector2 size, Color colour, bool soft = false)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = colour, Roughness = 0.4f,
            Transparency = colour.A < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        };
        if (soft)
        {
            var fade = new Gradient();
            fade.SetColor(0, Colors.White);
            fade.SetColor(1, new Color(1, 1, 1, 0));
            material.AlbedoTexture = new GradientTexture2D
            {
                Gradient = fade, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f), Width = 64, Height = 64,
            };
        }
        AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = size }, MaterialOverride = material, Position = at, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }
}
