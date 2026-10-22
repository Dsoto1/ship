using Expedition.Simulation.Voyage;
using Godot;

namespace Expedition.Game;

/// <summary>
/// The island she has made for, once the masthead has raised it: a hill of green standing out of the sea
/// in the direction it truly bears, small and grey at first and nearer as she runs in, close aboard at
/// anchor. A settled island has its few houses by the watering place; a wild one is low and wooded. Like
/// the far coast the distances are the view's; the simulation keeps where it is. Visual only.
/// </summary>
public partial class IslandView : Node3D
{
    private ShaderMaterial _land = null!;
    private string? _built;
    private float _fade;

    public override void _Ready()
    {
        _land = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/coast.gdshader") };
        Visible = false;
    }

    /// <summary>Places the island for this frame, or hides it.</summary>
    /// <param name="island">The island raised, or null.</param>
    /// <param name="at">Where it truly is.</param>
    /// <param name="navigation">Where she truly is.</param>
    /// <param name="anchored">True at anchor off it.</param>
    /// <param name="bearing">The route's starting bearing: the world's -z is that course.</param>
    public void Show(Island? island, ChartPoint at, Navigation? navigation, bool anchored, double bearing, Vector3 centre, float view, float darkness, float gloom, float delta)
    {
        if (island is null || navigation is null)
        {
            _fade = 0;
            Visible = false;
            return;
        }

        if (_built != island.Id)
        {
            Build(island);
        }

        _fade = Mathf.MoveToward(_fade, 1, delta * 0.25f);
        var off = (float)navigation.True.DistanceTo(at);
        var compass = Mathf.RadToDeg(Mathf.Atan2((float)(at.X - navigation.True.X), (float)(at.Y - navigation.True.Y)));
        var a = Mathf.DegToRad((float)(compass - bearing));
        var towards = new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a));
        var closeness = anchored ? 1f : Mathf.Clamp(((float)island.SightingLeagues - off) / ((float)island.SightingLeagues - 0.8f), 0, 1);
        var distance = Mathf.Max(20f, view * Mathf.Lerp(1.0f, 0.62f, closeness));
        Position = centre + towards * distance;
        // Its houses face the ship.
        Basis = Basis.LookingAt(towards, Vector3.Up);
        _land.SetShaderParameter("haze", Mathf.Lerp(1f, Mathf.Clamp(off / 10f, 0.1f, 0.75f) + gloom * 0.2f, _fade));
        _land.SetShaderParameter("darkness", darkness);
        Visible = true;
    }

    private void Build(Island island)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _built = island.Id;
        // A high island for a long sighting range, a low one for a short.
        var peak = island.SightingLeagues >= 11 ? 14f : island.SightingLeagues >= 9 ? 9f : 2.5f;
        // Bare rock where there is no water to grow anything; green where there is.
        var barren = island.Spring <= 0;
        var low = barren ? new Color(0.45f, 0.44f, 0.41f) : new Color(0.2f, 0.32f, 0.18f);
        var high = barren ? new Color(0.62f, 0.61f, 0.58f) : new Color(0.32f, 0.38f, 0.27f);
        var radius = island.SightingLeagues >= 9 ? 14f : 11f;
        const int rings = 10;
        const int segments = 28;
        var water = SeaAndSky.WaterLevel;
        (Vector3 At, Color Colour) Point(int ring, int segment)
        {
            var theta = segment * Mathf.Tau / segments;
            var wobble = 1f + 0.12f * Mathf.Sin(theta * 3f + 0.7f) + 0.06f * Mathf.Sin(theta * 7f);
            var r = ring / (float)rings * radius * wobble;
            var t = ring / (float)rings;
            var height = ring == rings ? -0.5f : peak * Mathf.Pow(Mathf.Max(0, 1 - t * t), 1.4f) * (1f + 0.15f * Mathf.Sin(theta * 2f)) + 0.15f;
            var colour = t > 0.93f ? new Color(0.86f, 0.87f, 0.83f)
                : t > 0.82f ? new Color(0.76f, 0.7f, 0.52f)
                : low.Lerp(high, Mathf.Clamp(height / peak, 0, 1));
            return (new Vector3(Mathf.Sin(theta) * r, water + height, Mathf.Cos(theta) * r), colour);
        }

        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        for (var ring = 0; ring < rings; ring++)
        {
            for (var segment = 0; segment < segments; segment++)
            {
                var next = (segment + 1) % segments;
                foreach (var (rr, ss) in new[] { (ring, segment), (ring + 1, segment), (ring + 1, next), (ring, segment), (ring + 1, next), (ring, next) })
                {
                    var (at, colour) = Point(rr, ss);
                    tool.SetColor(colour);
                    tool.AddVertex(at);
                }
            }
        }

        tool.GenerateNormals();
        AddChild(new MeshInstance3D { Mesh = tool.Commit(), MaterialOverride = _land, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        // Its few houses by the stream, on the side towards the ship (+z: the node looks away from her).
        if (island.Settled)
        {
            var lime = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.88f, 0.82f), Roughness = 0.9f };
            var roof = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.3f, 0.2f), Roughness = 0.9f };
            foreach (var (x, z) in new[] { (-2.5f, 9.6f), (0.4f, 10.2f), (2.8f, 9.4f), (-0.9f, 8.4f) })
            {
                var y = water + 0.9f;
                AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.4f, 0.9f, 1.1f) }, MaterialOverride = lime, Position = new Vector3(x, y + 0.45f, z) });
                AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(1.6f, 0.5f, 1.2f) }, MaterialOverride = roof, Position = new Vector3(x, y + 1.15f, z) });
            }
        }
    }
}
