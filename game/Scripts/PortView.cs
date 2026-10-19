using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// Draws the Bocana waterfront (R2a) from its location data: the ground and the raised customs terrace
/// with its steps, every feature the map lists (buildings by style, stalls, cargo, piers, boats, the
/// shipwright's hull on the stocks), and the town climbing the hill behind. One cell is one metre: grid
/// X is world X, grid Y is world Z, the water to the south (PORT_EXPERIENCE.md §3–4). Purely visual and
/// built from code until the asset pipeline replaces it (D-021). The Garza is drawn by
/// <see cref="ShipView"/> at <see cref="Berth"/>.
/// </summary>
public partial class PortView : Node3D
{
    private static readonly Color Plaster = new(0.86f, 0.81f, 0.7f);
    private static readonly Color Ochre = new(0.82f, 0.66f, 0.42f);
    private static readonly Color Lime = new(0.93f, 0.91f, 0.86f);
    private static readonly Color Stone = new(0.62f, 0.58f, 0.5f);
    private static readonly Color DarkStone = new(0.45f, 0.42f, 0.37f);
    private static readonly Color Timber = new(0.25f, 0.17f, 0.11f);
    private static readonly Color Wood = new(0.48f, 0.35f, 0.22f);
    private static readonly Color WetWood = new(0.3f, 0.23f, 0.16f);
    private static readonly Color Shadow = new(0.08f, 0.07f, 0.06f);
    private static readonly Color Shutter = new(0.27f, 0.36f, 0.3f);
    private static readonly Color Rope = new(0.55f, 0.47f, 0.33f);
    private static readonly Color Canvas = new(0.88f, 0.84f, 0.74f);

    private readonly Dictionary<Color, StandardMaterial3D> _materials = [];
    private readonly List<(OmniLight3D Light, StandardMaterial3D Glass)> _lanterns = [];
    private readonly List<OmniLight3D> _fires = [];
    private ShaderMaterial _roof = null!;
    private ShaderMaterial _ground = null!;
    private LocationDefinition _location = null!;

    /// <summary>Centre of the Garza's hull where she lies alongside, at the waterline-relative deck height.</summary>
    public Vector3 Berth { get; private set; }

    /// <summary>Ground height in metres at a point, for standing figures on the terrace and the steps.</summary>
    public float HeightAt(float x, float z)
    {
        foreach (var area in _location.Ground)
        {
            if (area.HeightAt(x, z) is { } height)
            {
                return (float)height;
            }
        }

        return 0f;
    }

    /// <summary>Builds the waterfront for <paramref name="location"/>.</summary>
    public void Build(LocationDefinition location)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _location = location;
        _lanterns.Clear();
        _fires.Clear();
        _roof = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/roof.gdshader") };
        _ground = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/ground.gdshader") };
        _ground.SetShaderParameter("quay_from", 29.5f);

        var ship = location.Features.FirstOrDefault(f => f.Kind == "ship");
        Berth = ship is null ? new Vector3(31, 0, 39.3f) : new Vector3((ship.X0 + ship.X1) / 2f, 0, 39.3f);

        BuildGround();
        foreach (var feature in location.Features)
        {
            BuildFeature(feature);
        }

        BuildTownBehind();
        BuildLights();
    }

    /// <summary>Lanterns, forge and cook fire by the light of day; wet stone in rain.</summary>
    public void UpdateLook(float darkness, float time, float wetness = 0f)
    {
        var flicker = 0.9f + 0.1f * Mathf.Sin(time * 11f) * Mathf.Sin(time * 4.3f);
        foreach (var (light, glass) in _lanterns)
        {
            light.LightEnergy = darkness * 2.2f * flicker;
            glass.EmissionEnergyMultiplier = 0.2f + darkness * 3f;
        }

        foreach (var fire in _fires)
        {
            fire.LightEnergy = (0.7f + darkness * 1.6f) * flicker;
        }

        _ground.SetShaderParameter("wetness", wetness);
    }

    // ---- ground, terrace, steps, quay edge

    private void BuildGround()
    {
        // The town's ground, from well behind the back lane down to the quay edge, and the terrace top.
        GroundQuad(-40, -40, 100, 34.5f, 0f);
        GroundQuad(-40, -40, 18.5f, 24.5f, 2.6f);

        // The terrace: a stone mass with its retaining walls, broken by two flights of steps.
        AddBox(Box(-40, 18.5f, -40, 24.5f, 0, 2.58f), DarkStone);
        AddBox(Box(18.5f, 19.5f, -40, 14.5f, 0, 2.6f), Stone);
        AddBox(Box(18.5f, 19.5f, 18.5f, 25.5f, 0, 2.6f), Stone);
        AddBox(Box(-40, 5.5f, 24.5f, 25.5f, 0, 2.6f), Stone);
        AddBox(Box(9.5f, 18.5f, 24.5f, 25.5f, 0, 2.6f), Stone);
        Steps(18.5f, 22.5f, 14.5f, 18.5f, alongX: true);
        Steps(5.5f, 9.5f, 24.5f, 29.5f, alongX: false);

        // A balustrade round the terrace edge, looking out over the harbour (§3.5, §4).
        Balustrade(new Vector3(-40, 2.6f, 24.6f), new Vector3(5.5f, 2.6f, 24.6f));
        Balustrade(new Vector3(9.5f, 2.6f, 24.6f), new Vector3(19.4f, 2.6f, 24.6f));
        Balustrade(new Vector3(19.4f, 2.6f, 18.5f), new Vector3(19.4f, 2.6f, 24.6f));
        Balustrade(new Vector3(19.4f, 2.6f, -1f), new Vector3(19.4f, 2.6f, 14.5f));

        // The quay wall dropping to the water, with its coping, and a stretch of shingle beyond the yard.
        AddBox(Box(-40, 100, 34.5f, 34.9f, -2.8f, 0f), DarkStone);
        AddBox(Box(-40, 100, 34.45f, 35f, -0.02f, 0.12f), Stone);
        AddBox(Box(-40, 100, 34.9f, 35.2f, -1.3f, -0.9f), new Color(0.2f, 0.3f, 0.22f));
    }

    private void GroundQuad(float x0, float z0, float x1, float z1, float y)
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var v in new[] { new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1) })
        {
            tool.SetUV(new Vector2(v.X, v.Z));
            tool.AddVertex(v);
        }

        tool.GenerateNormals();
        AddChild(new MeshInstance3D { Mesh = tool.Commit(), MaterialOverride = _ground });
    }

    private void Steps(float x0, float x1, float z0, float z1, bool alongX)
    {
        const int count = 9;
        for (var i = 0; i < count; i++)
        {
            var top = 2.6f * (1f - (i + 0.5f) / count);
            var box = alongX
                ? Box(x0 + (x1 - x0) * i / count, x0 + (x1 - x0) * (i + 1) / count, z0, z1, 0, top)
                : Box(x0, x1, z0 + (z1 - z0) * i / count, z0 + (z1 - z0) * (i + 1) / count, 0, top);
            AddBox(box, i % 2 == 0 ? Stone : Stone.Darkened(0.06f));
        }
    }

    private void Balustrade(Vector3 from, Vector3 to)
    {
        var along = to - from;
        var length = along.Length();
        var mid = (from + to) / 2f;
        var rail = AddBox(mid + new Vector3(0, 0.95f, 0), new Vector3(along.X == 0 ? 0.25f : length, 0.12f, along.Z == 0 ? 0.25f : length), Lime.Darkened(0.12f));
        for (var d = 0.4f; d < length; d += 0.55f)
        {
            AddCylinder(from + along / length * d + new Vector3(0, 0.45f, 0), 0.07f, 0.9f, Lime.Darkened(0.18f));
        }
    }

    // ---- features

    private void BuildFeature(LocationFeature f)
    {
        var left = f.X0 - 0.5f;
        var right = f.X1 + 0.5f;
        var back = f.Y0 - 0.5f;
        var front = f.Y1 + 0.5f;
        var middle = new Vector3((left + right) / 2f, HeightAt((left + right) / 2f, (back + front) / 2f), (back + front) / 2f);
        switch (f.Kind)
        {
            case "building":
                Building(f.Style ?? "house", left, right, back, front, HeightAt(middle.X, back + 0.2f));
                break;
            case "vault":
                // Warehouse vaults under the terrace, opening onto the quay.
                AddBox(Box(f.X0 == 0 ? -40 : left, right, back - 1f, front, 0, 2.6f), Stone.Darkened(0.04f));
                for (var x = left + 1.6f; x < right - 0.8f; x += 3f)
                {
                    Arch(new Vector3(x, 0, front + 0.02f), 1.8f, 2.0f);
                }

                break;
            case "terrace":
            case "steps":
            case "balustrade":
            case "ship":
                break;
            case "pier":
                Pier(left, right, back, front);
                break;
            case "boat":
                Boat(new Vector3(middle.X, SeaAndSky.WaterLevel + 0.3f, middle.Z), right - left, f.Style == "mast");
                break;
            case "vessel":
                Merchantman(new Vector3(middle.X, SeaAndSky.WaterLevel, middle.Z), right - left);
                break;
            case "notice_board":
                Board(new Vector3(middle.X, middle.Y + 1.4f, back));
                break;
            case "chart_table":
                AddBox(middle + new Vector3(0, 0.75f, 0), new Vector3(1.3f, 0.08f, 0.9f), Wood);
                AddBox(middle + new Vector3(0, 0.8f, 0), new Vector3(1.0f, 0.01f, 0.7f), new Color(0.86f, 0.8f, 0.64f));
                AddBox(middle + new Vector3(0, 0.37f, 0), new Vector3(0.12f, 0.74f, 0.12f), Timber);
                break;
            case "table":
                AddBox(middle + new Vector3(0, 0.72f, 0), new Vector3(right - left, 0.08f, 0.85f), Wood);
                AddBox(middle + new Vector3(0, 0.36f, 0), new Vector3(0.12f, 0.72f, 0.5f), Wood.Darkened(0.2f));
                AddBox(middle + new Vector3(0, 0.6f, 0.15f), new Vector3(0.12f, 0.1f, 0.12f), new Color(0.6f, 0.45f, 0.25f));
                break;
            case "flagpole":
                AddCylinder(middle + new Vector3(0, 4f, 0), 0.06f, 8f, Timber);
                Banner(middle + new Vector3(0.05f, 7.6f, 0), new Color(0.62f, 0.13f, 0.12f), new Color(0.85f, 0.7f, 0.25f));
                break;
            case "fire":
                CookFire(middle);
                break;
            case "stall":
                Stall(middle, right - left, f.Style ?? "produce");
                break;
            case "well":
                Well(middle);
                break;
            case "cargo":
                Cargo(middle, right - left, front - back, f.Style ?? "crates");
                break;
            case "bollard":
                AddCylinder(middle + new Vector3(0, 0.35f, 0), 0.18f, 0.7f, DarkStone);
                AddCylinder(middle + new Vector3(0, 0.72f, 0), 0.22f, 0.08f, DarkStone);
                break;
            case "ropewalk":
                Ropewalk(left, right, back, front);
                break;
            case "anvil":
                AddBox(middle + new Vector3(0, 0.3f, 0), new Vector3(0.35f, 0.6f, 0.35f), Wood.Darkened(0.3f));
                AddBox(middle + new Vector3(0, 0.68f, 0), new Vector3(0.6f, 0.16f, 0.25f), new Color(0.2f, 0.2f, 0.22f));
                break;
            case "canvas":
                // Sailcloth spread on the ground for mending.
                AddBox(middle + new Vector3(0, 0.02f, 0), new Vector3(right - left, 0.03f, front - back), Canvas);
                AddBox(middle + new Vector3(0.6f, 0.04f, 0.2f), new Vector3(0.9f, 0.02f, 0.5f), Canvas.Darkened(0.12f));
                break;
            case "fence":
                Fence(left, right, back, front);
                break;
            case "hull_on_stocks":
                HullOnStocks(left, right, back, front);
                break;
            case "timber":
                for (var i = 0; i < 5; i++)
                {
                    AddBox(new Vector3(middle.X, 0.1f + i * 0.2f, middle.Z), new Vector3(right - left - (i % 2) * 0.3f, 0.18f, front - back - 0.4f), Wood.Lightened(0.05f * (i % 3)));
                }

                break;
            case "sawpit":
                AddBox(middle + new Vector3(0, 0.02f, 0), new Vector3(right - left, 0.04f, front - back), Shadow);
                AddBox(middle + new Vector3(0, 0.3f, 0), new Vector3(right - left + 0.4f, 0.25f, 0.3f), Wood);
                AddBox(middle + new Vector3(0.6f, 0.9f, 0), new Vector3(0.05f, 1.2f, 0.25f), new Color(0.5f, 0.5f, 0.52f));
                break;
            case "net_frame":
                NetFrame(middle, right - left);
                break;
            case "gaol_cart":
                GaolCart(middle);
                break;
        }
    }

    private void Building(string style, float left, float right, float back, float front, float baseHeight)
    {
        var at = new Vector3(0, baseHeight, 0);
        var middleX = (left + right) / 2f;
        switch (style)
        {
            case "chamber":
                // The Chamber of the Ocean Sea: dressed stone, an arched door, the Crown's banner.
                House(left, right, back, front, Stone.Lightened(0.08f), Stone, 2, 3.8f, frame: false, baseHeight);
                Arch(new Vector3(middleX, baseHeight, front + 0.02f), 1.9f, 3.0f);
                for (var x = left + 0.7f; x < right; x += (right - left - 1.4f) / 4f)
                {
                    AddBox(new Vector3(x, baseHeight + 1.9f, front + 0.15f), new Vector3(0.35f, 3.8f, 0.3f), Stone.Lightened(0.12f));
                }

                Banner(new Vector3(right - 0.6f, baseHeight + 8f, front + 0.2f), new Color(0.62f, 0.13f, 0.12f), new Color(0.85f, 0.7f, 0.25f));
                Lantern(new Vector3(middleX - 1.6f, baseHeight + 2.6f, front + 0.3f));
                break;
            case "counting":
                House(left, right, back, front, Ochre, Stone, 3, 3f, frame: true, baseHeight);
                AddBox(new Vector3(middleX, baseHeight + 3.4f, front + 0.6f), new Vector3(right - left, 0.15f, 1.2f), Timber);
                for (var x = left + 0.2f; x <= right; x += 1.7f)
                {
                    AddBox(new Vector3(x, baseHeight + 3.9f, front + 1.15f), new Vector3(0.08f, 0.9f, 0.08f), Timber);
                }

                AddBox(new Vector3(middleX, baseHeight + 4.35f, front + 1.15f), new Vector3(right - left, 0.08f, 0.1f), Timber);
                AddBox(new Vector3(middleX, baseHeight + 1.5f, front + 0.06f), new Vector3(1.4f, 2.6f, 0.08f), Timber);
                break;
            case "tavern":
                var split = (left + right) / 2f;
                House(left, split, back, front, Plaster, Stone, 2, 3.2f, frame: true, baseHeight);
                House(split, right, back, front, new Color(0.8f, 0.74f, 0.62f), Stone, 3, 3f, frame: true, baseHeight);
                AddBox(new Vector3(split - 0.4f, baseHeight + 1.4f, front + 0.06f), new Vector3(1.6f, 2.5f, 0.08f), Timber);
                HangingSign(new Vector3(split + 0.6f, baseHeight + 3.4f, front + 0.7f), new Color(0.2f, 0.25f, 0.32f));
                Lantern(new Vector3(split - 1.6f, baseHeight + 2.6f, front + 0.3f));
                break;
            case "chapel":
                House(left, right, back, front, Lime, Lime, 1, 7f, frame: false, baseHeight);
                AddBox(new Vector3(middleX, baseHeight + 10.2f, front - 0.3f), new Vector3(2.4f, 3.2f, 0.6f), Lime);
                AddBox(new Vector3(middleX, baseHeight + 10.1f, front + 0.02f), new Vector3(0.9f, 1.4f, 0.05f), Shadow);
                AddCylinder(new Vector3(middleX, baseHeight + 10f, front - 0.2f), 0.32f, 0.5f, new Color(0.45f, 0.35f, 0.2f));
                Arch(new Vector3(middleX, baseHeight, front + 0.02f), 1.6f, 3.2f);
                AddCylinder(new Vector3(middleX, baseHeight + 5.2f, front + 0.04f), 0.65f, 0.08f, Shadow, rotateX: true);
                AddBox(new Vector3(middleX, baseHeight + 0.1f, front + 0.6f), new Vector3(5f, 0.2f, 1.0f), Stone);
                Lantern(new Vector3(middleX - 1.6f, baseHeight + 2.6f, front + 0.3f));
                break;
            case "warehouse":
                House(left, right, back, front, new Color(0.7f, 0.62f, 0.5f), Stone, 2, 3.6f, frame: true, baseHeight);
                AddBox(new Vector3(middleX, baseHeight + 1.6f, front + 0.06f), new Vector3(2.6f, 3.0f, 0.08f), Timber);
                AddBox(new Vector3(middleX, baseHeight + 5.5f, front + 0.06f), new Vector3(1.4f, 1.6f, 0.08f), Timber);
                AddBox(new Vector3(middleX, baseHeight + 6.8f, front + 0.9f), new Vector3(0.25f, 0.25f, 1.8f), Timber);
                AddCylinder(new Vector3(middleX, baseHeight + 5.2f, front + 1.6f), 0.02f, 3.2f, Rope);
                HangingSign(new Vector3(right - 1f, baseHeight + 3.2f, front + 0.7f), new Color(0.3f, 0.2f, 0.12f));
                break;
            case "cooperage":
                House(left, right, back, front, new Color(0.76f, 0.68f, 0.56f), Stone, 1, 3.6f, frame: true, baseHeight);
                AddBox(new Vector3(middleX, baseHeight + 1.4f, front + 0.04f), new Vector3(3.2f, 2.6f, 0.06f), Shadow);
                for (var i = 0; i < 3; i++)
                {
                    Cask(new Vector3(right - 0.8f - i * 0.75f, baseHeight + 0.42f, front + 0.6f), false);
                }

                break;
            case "smithy":
                House(left, right, back, front, DarkStone.Lightened(0.15f), DarkStone, 1, 3.4f, frame: false, baseHeight);
                AddBox(new Vector3(middleX, baseHeight + 1.3f, front + 0.04f), new Vector3(3f, 2.4f, 0.06f), Shadow);
                AddBox(new Vector3(right - 1.2f, baseHeight + 6.4f, (back + front) / 2f), new Vector3(1.1f, 3.2f, 1.1f), DarkStone);
                AddChild(Smoke(new Vector3(right - 1.2f, baseHeight + 8.1f, (back + front) / 2f), dark: true));
                AddBox(new Vector3(middleX - 0.8f, baseHeight + 0.6f, front - 0.4f), new Vector3(1.2f, 0.4f, 0.6f), new Color(1f, 0.45f, 0.12f), emissive: true);
                Fire(new Vector3(middleX - 0.8f, baseHeight + 1.2f, front + 0.4f), 4f);
                break;
            case "sailloft":
                House(left, right, back, front, new Color(0.8f, 0.76f, 0.66f), Stone, 2, 3.2f, frame: true, baseHeight);
                AddBox(new Vector3(middleX, baseHeight + 4.8f, front + 0.06f), new Vector3(2.2f, 2f, 0.08f), Timber);
                AddBox(new Vector3(middleX, baseHeight + 1.4f, front + 0.06f), new Vector3(1.6f, 2.6f, 0.08f), Timber);
                break;
            default:
                House(left, right, back, front, Plaster.Darkened(0.04f), Stone, 3, 3f, frame: true, baseHeight);
                break;
        }
    }

    // A house over a rectangle, facing south: stone ground floor, plastered storeys, a pitched tiled roof.
    private void House(float left, float right, float back, float front, Color wall, Color plinth, int storeys, float storeyHeight, bool frame, float baseHeight, bool detail = true)
    {
        var width = right - left;
        var depth = front - back;
        var height = storeys * storeyHeight;
        var centre = new Vector3((left + right) / 2f, baseHeight, (back + front) / 2f);

        AddBox(centre + new Vector3(0, storeyHeight / 2f, 0), new Vector3(width, storeyHeight, depth), plinth);
        if (storeys > 1)
        {
            AddBox(centre + new Vector3(0, storeyHeight + (height - storeyHeight) / 2f, 0), new Vector3(width + 0.1f, height - storeyHeight, depth + 0.1f), wall);
        }

        var eave = baseHeight + height;
        var ridge = eave + Mathf.Min(depth * 0.42f, 3.6f);
        Roof(new Vector3(left - 0.4f, eave, front + 0.5f), new Vector3(right + 0.4f, eave, front + 0.5f), ridge, centre.Z);
        Roof(new Vector3(right + 0.4f, eave, back - 0.5f), new Vector3(left - 0.4f, eave, back - 0.5f), ridge, centre.Z);
        Gable(left, eave, ridge, back, front, centre.Z, wall);
        Gable(right, eave, ridge, back, front, centre.Z, wall);

        if (frame && storeys > 1 && detail)
        {
            for (var s = 1; s <= storeys; s++)
            {
                AddBox(new Vector3(centre.X, baseHeight + s * storeyHeight, front + 0.06f), new Vector3(width + 0.1f, 0.18f, 0.08f), Timber);
            }

            for (var x = left; x <= right + 0.01f; x += 1.5f)
            {
                AddBox(new Vector3(x, baseHeight + storeyHeight + (height - storeyHeight) / 2f, front + 0.06f), new Vector3(0.14f, height - storeyHeight, 0.08f), Timber);
            }
        }

        for (var s = 1; s < storeys; s++)
        {
            var y = baseHeight + s * storeyHeight + storeyHeight * 0.5f;
            for (var x = left + 1.2f; x < right - 0.8f; x += detail ? 2f : 2.6f)
            {
                AddBox(new Vector3(x, y, front + 0.06f), new Vector3(0.7f, 1.0f, 0.06f), Shadow);
                if (detail)
                {
                    AddBox(new Vector3(x - 0.55f, y, front + 0.09f), new Vector3(0.35f, 1.0f, 0.04f), Shutter);
                    AddBox(new Vector3(x + 0.55f, y, front + 0.09f), new Vector3(0.35f, 1.0f, 0.04f), Shutter);
                }
            }
        }

        // The east side faces the camera too: a window or two.
        if (detail && depth > 4 && storeys > 1)
        {
            AddBox(new Vector3(right + 0.06f, baseHeight + storeyHeight * 1.5f, centre.Z), new Vector3(0.06f, 1.0f, 0.7f), Shadow);
        }
    }

    private void Roof(Vector3 a, Vector3 b, float ridgeHeight, float ridgeZ)
    {
        var c = new Vector3(b.X, ridgeHeight, ridgeZ);
        var d = new Vector3(a.X, ridgeHeight, ridgeZ);
        var slope = (d - a).Length();
        var length = (b - a).Length();
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        void V(Vector3 v, float u, float w) { tool.SetUV(new Vector2(u, w)); tool.AddVertex(v); }
        V(a, 0, slope); V(d, 0, 0); V(c, length, 0);
        V(a, 0, slope); V(c, length, 0); V(b, length, slope);
        tool.GenerateNormals();
        AddChild(new MeshInstance3D { Mesh = tool.Commit(), MaterialOverride = _roof });
        AddBox((a + b) / 2f + new Vector3(0, -0.06f, 0), new Vector3(Mathf.Abs(b.X - a.X), 0.14f, 0.18f), new Color(0.42f, 0.22f, 0.14f));
    }

    private void Gable(float x, float eave, float ridge, float back, float front, float ridgeZ, Color wall)
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        tool.SetColor(wall);
        tool.AddVertex(new Vector3(x, eave, back));
        tool.AddVertex(new Vector3(x, ridge, ridgeZ));
        tool.AddVertex(new Vector3(x, eave, front));
        tool.GenerateNormals();
        var material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.95f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        AddChild(new MeshInstance3D { Mesh = tool.Commit(), MaterialOverride = material });
    }

    // A dark arched opening on a south face: a door, a vault mouth.
    private void Arch(Vector3 foot, float width, float height)
    {
        AddBox(foot + new Vector3(0, (height - width / 2f) / 2f, 0), new Vector3(width, height - width / 2f, 0.06f), Shadow);
        AddCylinder(foot + new Vector3(0, height - width / 2f, 0), width / 2f, 0.06f, Shadow, rotateX: true);
    }

    private void Pier(float left, float right, float back, float front)
    {
        AddBox(Box(left, right, back, front, -0.18f, -0.02f), WetWood.Lightened(0.15f));
        for (var x = left + 0.3f; x < right; x += 0.5f)
        {
            AddBox(new Vector3(x, -0.01f, (back + front) / 2f), new Vector3(0.03f, 0.02f, front - back), WetWood);
        }

        for (var z = back + 0.5f; z <= front; z += 2f)
        {
            foreach (var x in new[] { left + 0.15f, right - 0.15f })
            {
                AddCylinder(new Vector3(x, -1.2f, z), 0.16f, 2.6f, WetWood);
            }
        }
    }

    private void Boat(Vector3 at, float length, bool mast)
    {
        var hull = new Color(0.32f, 0.26f, 0.2f);
        AddBox(at, new Vector3(length, 0.7f, 1.6f), hull);
        AddBox(at + new Vector3(0, 0.37f, 0), new Vector3(length - 0.3f, 0.06f, 1.3f), Wood);
        var bow = AddBox(at + new Vector3(length / 2f + 0.2f, 0.1f, 0), new Vector3(0.9f, 0.6f, 1.1f), hull);
        bow.RotationDegrees = new Vector3(0, 45, 0);
        AddBox(at + new Vector3(0, 0.35f, 0.81f), new Vector3(length, 0.12f, 0.04f), new Color(0.55f, 0.22f, 0.16f));
        if (mast)
        {
            AddBox(at + new Vector3(-0.3f, 2.7f, 0), new Vector3(0.12f, 5f, 0.12f), Timber);
            AddBox(at + new Vector3(-0.3f, 4.1f, 0), new Vector3(0.08f, 0.08f, 3.2f), Timber);
            var sail = AddBox(at + new Vector3(-0.3f, 3.3f, 0), new Vector3(0.04f, 1.4f, 2.6f), Canvas.Darkened(0.15f));
            sail.RotationDegrees = new Vector3(0, 0, 4);
        }
    }

    // A merchantman lying at anchor out in the harbour: a hull, castles and three masts (Tier C, §6).
    private void Merchantman(Vector3 at, float length)
    {
        var hull = new Color(0.25f, 0.19f, 0.14f);
        AddBox(at + new Vector3(0, 0.6f, 0), new Vector3(length * 0.8f, 2.4f, 5.5f), hull);
        AddBox(at + new Vector3(-length * 0.32f, 2.4f, 0), new Vector3(length * 0.22f, 1.8f, 5.3f), hull.Lightened(0.08f));
        AddBox(at + new Vector3(length * 0.33f, 2.1f, 0), new Vector3(length * 0.16f, 1.2f, 4.6f), hull.Lightened(0.08f));
        AddBox(at + new Vector3(0, 1.85f, 2.76f), new Vector3(length * 0.8f, 0.18f, 0.05f), new Color(0.75f, 0.6f, 0.3f));
        foreach (var (x, height) in new[] { (-length * 0.2f, 11f), (length * 0.08f, 14f), (length * 0.3f, 9f) })
        {
            AddCylinder(at + new Vector3(x, 1.8f + height / 2f, 0), 0.18f, height, Timber);
            AddBox(at + new Vector3(x, 1.8f + height * 0.75f, 0), new Vector3(0.14f, 0.14f, height * 0.6f), Timber);
            var furled = AddBox(at + new Vector3(x, 1.8f + height * 0.75f + 0.15f, 0), new Vector3(0.3f, 0.25f, height * 0.55f), Canvas.Darkened(0.2f));
        }
    }

    private void Ropewalk(float left, float right, float back, float front)
    {
        // A long low roof on posts over the walk, open along the street side.
        for (var x = left + 0.2f; x <= right; x += 3f)
        {
            AddBox(new Vector3(x, 1.25f, back + 0.2f), new Vector3(0.14f, 2.5f, 0.14f), Timber);
            AddBox(new Vector3(x, 1.25f, front - 0.2f), new Vector3(0.14f, 2.5f, 0.14f), Timber);
        }

        AddBox(Box(left, right, back, back + 0.4f, 0, 1.0f), Wood.Darkened(0.15f));
        Roof(new Vector3(left - 0.3f, 2.5f, front + 0.3f), new Vector3(right + 0.3f, 2.5f, front + 0.3f), 3.4f, (back + front) / 2f);
        Roof(new Vector3(right + 0.3f, 2.5f, back - 0.3f), new Vector3(left - 0.3f, 2.5f, back - 0.3f), 3.4f, (back + front) / 2f);

        // Yarn stretched the length of the walk, and the spinning wheel at the head.
        for (var i = 0; i < 3; i++)
        {
            AddBox(new Vector3((left + right) / 2f, 1.0f + i * 0.12f, (back + front) / 2f + 0.25f), new Vector3(right - left - 2f, 0.025f, 0.025f), Rope.Lightened(0.15f));
        }

        AddCylinder(new Vector3(left + 0.8f, 0.9f, (back + front) / 2f - 0.4f), 0.6f, 0.08f, Wood, rotateX: true);
        AddBox(new Vector3(right - 0.6f, 0.6f, (back + front) / 2f - 0.4f), new Vector3(0.5f, 1.2f, 0.6f), Wood);
    }

    private void Fence(float left, float right, float back, float front)
    {
        var vertical = right - left < front - back;
        var length = vertical ? front - back : right - left;
        for (var d = 0.5f; d < length; d += 1f)
        {
            var at = vertical ? new Vector3((left + right) / 2f, 0.6f, back + d) : new Vector3(left + d, 0.6f, (back + front) / 2f);
            if (_location.IsWalkable(new GridPosition(Mathf.FloorToInt(at.X + 0.5f), Mathf.FloorToInt(at.Z + 0.5f))))
            {
                continue;
            }

            AddBox(at, vertical ? new Vector3(0.1f, 1.2f, 0.95f) : new Vector3(0.95f, 1.2f, 0.1f), Wood.Darkened(0.1f));
        }
    }

    // A small hull on the stocks: keel, ribs, part-planked, pointing down the slip to the water.
    private void HullOnStocks(float left, float right, float back, float front)
    {
        var x = (left + right) / 2f;
        var length = front - back;
        AddBox(new Vector3(x, 0.15f, (back + front) / 2f + 1.5f), new Vector3(1.2f, 0.3f, length + 3f), Wood.Darkened(0.25f));
        AddBox(new Vector3(x, 0.55f, (back + front) / 2f), new Vector3(0.25f, 0.4f, length), Timber);
        for (var i = 0; i < 9; i++)
        {
            var z = back + 0.8f + i * (length - 1.6f) / 8f;
            var t = Mathf.Sin((i + 0.5f) / 9f * Mathf.Pi);
            var half = 0.9f + t * 1.6f;
            foreach (var side in new[] { -1f, 1f })
            {
                var rib = AddBox(new Vector3(x + side * half * 0.6f, 1.6f, z), new Vector3(0.12f, 2.4f, 0.14f), Wood);
                rib.RotationDegrees = new Vector3(0, 0, -side * 22f);
            }
        }

        // Planking up the lower half on the camera's side.
        for (var row = 0; row < 3; row++)
        {
            var plank = AddBox(new Vector3(x + 1.1f + row * 0.2f, 0.95f + row * 0.32f, (back + front) / 2f + 0.3f), new Vector3(0.06f, 0.3f, length - 2.2f - row * 0.8f), Wood.Lightened(0.08f));
            plank.RotationDegrees = new Vector3(0, 0, -22f);
        }

        // Shores holding her upright.
        foreach (var z in new[] { back + 1.5f, front - 1.5f })
        {
            foreach (var side in new[] { -1f, 1f })
            {
                var shore = AddBox(new Vector3(x + side * 2.4f, 1.0f, z), new Vector3(0.14f, 2.3f, 0.14f), Wood.Darkened(0.15f));
                shore.RotationDegrees = new Vector3(0, 0, side * 30f);
            }
        }
    }

    private void Stall(Vector3 at, float width, string style)
    {
        var awning = style switch
        {
            "fish" => new Color(0.2f, 0.36f, 0.55f),
            "cloth" => new Color(0.62f, 0.2f, 0.32f),
            "tools" => new Color(0.4f, 0.42f, 0.25f),
            _ => new Color(0.72f, 0.2f, 0.16f),
        };
        width = Mathf.Max(width, 2f);
        AddBox(at + new Vector3(0, 0.45f, 0), new Vector3(width, 0.9f, 0.9f), Wood);
        foreach (var (x, z) in new[] { (-width / 2f, -0.5f), (width / 2f, -0.5f), (-width / 2f, 0.7f), (width / 2f, 0.7f) })
        {
            AddBox(at + new Vector3(x, 1.1f, z), new Vector3(0.08f, 2.2f, 0.08f), Timber);
        }

        var stripes = Mathf.RoundToInt(width / 0.35f);
        for (var i = 0; i < stripes; i++)
        {
            var stripe = i % 2 == 0 ? awning : new Color(0.9f, 0.86f, 0.76f);
            var box = AddBox(at + new Vector3(-width / 2f + (i + 0.5f) * width / stripes, 2.15f, 0.1f), new Vector3(width / stripes, 0.04f, 1.7f), stripe);
            box.RotationDegrees = new Vector3(14, 0, 0);
        }

        Color[] goods = style switch
        {
            "fish" => [new(0.6f, 0.66f, 0.68f), new(0.7f, 0.55f, 0.5f), new(0.5f, 0.56f, 0.6f)],
            "cloth" => [new(0.7f, 0.2f, 0.2f), new(0.25f, 0.3f, 0.5f), new(0.85f, 0.78f, 0.6f)],
            "tools" => [new(0.35f, 0.35f, 0.38f), new(0.5f, 0.4f, 0.28f), new(0.3f, 0.3f, 0.32f)],
            _ => [new(0.7f, 0.3f, 0.18f), new(0.55f, 0.6f, 0.25f), new(0.85f, 0.7f, 0.35f), new(0.5f, 0.3f, 0.45f)],
        };
        var count = Mathf.RoundToInt(width / 0.45f);
        for (var i = 0; i < count; i++)
        {
            var colour = goods[i % goods.Length];
            if (style is "cloth" or "tools")
            {
                AddBox(at + new Vector3(-width / 2f + 0.3f + i * 0.45f, 0.96f, 0), new Vector3(0.36f, 0.1f, 0.5f), colour);
            }
            else
            {
                AddCylinder(at + new Vector3(-width / 2f + 0.3f + i * 0.45f, 0.97f, 0), 0.17f, 0.14f, colour);
            }
        }
    }

    private void Well(Vector3 at)
    {
        AddCylinder(at + new Vector3(0, 0.45f, 0), 0.95f, 0.9f, Stone);
        AddCylinder(at + new Vector3(0, 0.92f, 0), 0.75f, 0.06f, Shadow);
        AddBox(at + new Vector3(-0.9f, 1.5f, 0), new Vector3(0.12f, 2.2f, 0.12f), Timber);
        AddBox(at + new Vector3(0.9f, 1.5f, 0), new Vector3(0.12f, 2.2f, 0.12f), Timber);
        AddBox(at + new Vector3(0, 2.55f, 0), new Vector3(2.2f, 0.12f, 0.12f), Timber);
        AddCylinder(at + new Vector3(0.3f, 1.6f, 0), 0.015f, 1.8f, Rope);
        AddCylinder(at + new Vector3(0.3f, 0.65f, 0), 0.14f, 0.22f, Wood);
    }

    private void Cargo(Vector3 at, float width, float depth, string style)
    {
        switch (style)
        {
            case "casks":
            case "barrels":
                for (var x = -width / 2f + 0.35f; x < width / 2f; x += 0.7f)
                {
                    for (var z = -depth / 2f + 0.35f; z < depth / 2f; z += 0.7f)
                    {
                        Cask(at + new Vector3(x, 0.42f, z), false);
                    }
                }

                Cask(at + new Vector3(0, 1.12f, 0), true);
                break;
            case "bales":
                AddBox(at + new Vector3(0, 0.35f, 0), new Vector3(width, 0.7f, Mathf.Max(depth, 0.9f)), new Color(0.72f, 0.66f, 0.5f));
                AddBox(at + new Vector3(0.1f, 0.95f, 0), new Vector3(width * 0.7f, 0.5f, 0.8f), new Color(0.66f, 0.6f, 0.46f));
                AddBox(at + new Vector3(0, 0.36f, 0), new Vector3(width + 0.02f, 0.06f, 0.92f), Rope);
                break;
            default:
                AddBox(at + new Vector3(-0.4f, 0.4f, -0.3f), new Vector3(0.9f, 0.8f, 0.9f), Wood);
                AddBox(at + new Vector3(0.5f, 0.35f, 0.3f), new Vector3(0.8f, 0.7f, 0.8f), Wood.Darkened(0.1f));
                AddBox(at + new Vector3(-0.3f, 1.15f, -0.2f), new Vector3(0.7f, 0.7f, 0.7f), Wood.Lightened(0.1f));
                AddBox(at + new Vector3(0.45f, 0.95f, 0.3f), new Vector3(0.5f, 0.5f, 0.5f), Wood.Lightened(0.05f));
                break;
        }
    }

    private void Cask(Vector3 at, bool lying)
    {
        var cask = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.28f, BottomRadius = 0.28f, Height = 0.8f, RadialSegments = 12 },
            MaterialOverride = Material(new Color(0.46f, 0.32f, 0.18f)),
            Position = at,
        };
        if (lying) cask.RotationDegrees = new Vector3(0, 0, 90);
        AddChild(cask);
        foreach (var y in new[] { -0.25f, 0.25f })
        {
            var hoop = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.3f, Height = 0.05f, RadialSegments = 12 },
                MaterialOverride = Material(new Color(0.2f, 0.18f, 0.16f)),
                Position = lying ? new Vector3(y, 0, 0) : new Vector3(0, y, 0),
            };
            if (lying) hoop.RotationDegrees = new Vector3(0, 0, 90);
            cask.AddChild(hoop);
        }
    }

    private void NetFrame(Vector3 at, float width)
    {
        width = Mathf.Max(width, 1.6f);
        AddBox(at + new Vector3(-width / 2f, 0.9f, 0), new Vector3(0.08f, 1.8f, 0.08f), Timber);
        AddBox(at + new Vector3(width / 2f, 0.9f, 0), new Vector3(0.08f, 1.8f, 0.08f), Timber);
        AddBox(at + new Vector3(0, 1.8f, 0), new Vector3(width + 0.2f, 0.06f, 0.06f), Timber);
        var net = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.42f, 0.36f, 0.26f, 0.75f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(width, 1.4f) }, MaterialOverride = net, Position = at + new Vector3(0, 1.05f, 0) });
    }

    private void GaolCart(Vector3 at)
    {
        AddBox(at + new Vector3(0, 0.55f, 0), new Vector3(2.2f, 0.12f, 1.4f), Timber);
        for (var i = 0; i <= 6; i++)
        {
            AddBox(at + new Vector3(-1f + i * 0.33f, 1.3f, -0.65f), new Vector3(0.04f, 1.5f, 0.04f), Shadow);
            AddBox(at + new Vector3(-1f + i * 0.33f, 1.3f, 0.65f), new Vector3(0.04f, 1.5f, 0.04f), Shadow);
        }

        AddBox(at + new Vector3(0, 2.05f, 0), new Vector3(2.2f, 0.06f, 1.4f), Shadow);
        foreach (var (x, z) in new[] { (-0.9f, -0.75f), (0.9f, -0.75f), (-0.9f, 0.75f), (0.9f, 0.75f) })
        {
            AddCylinder(at + new Vector3(x, 0.45f, z), 0.45f, 0.08f, Wood, rotateX: true);
        }
    }

    private void CookFire(Vector3 at)
    {
        AddCylinder(at + new Vector3(0, 0.15f, 0), 0.55f, 0.3f, DarkStone);
        AddBox(at + new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.15f), emissive: true);
        AddCylinder(at + new Vector3(0, 0.55f, 0), 0.25f, 0.35f, Shadow);
        Fire(at + new Vector3(0, 1f, 0), 5f);
        AddChild(Smoke(at + new Vector3(0, 0.9f, 0), dark: false));
    }

    private void Fire(Vector3 at, float range)
    {
        var light = new OmniLight3D { Position = at, LightColor = new Color(1f, 0.6f, 0.3f), OmniRange = range };
        AddChild(light);
        _fires.Add(light);
    }

    // ---- the town climbing the hill behind, and the shore beyond both ends

    private void BuildTownBehind()
    {
        var rng = new RandomNumberGenerator { Seed = 1517 };
        Color[] walls = [Plaster, Ochre, Lime, new Color(0.78f, 0.7f, 0.6f), new Color(0.84f, 0.74f, 0.66f)];
        for (var row = 0; row < 4; row++)
        {
            var z = -1f - row * 7.5f;
            var rise = row * 2.2f;
            for (var x = -34f; x < 100f; x += 5.5f + rng.Randf() * 1.5f)
            {
                var onTerrace = x < 18f;
                var width = 4.5f + rng.Randf() * 1.5f;
                var storeys = 2 + rng.RandiRange(0, 1);
                var baseHeight = (onTerrace ? 2.6f : 0f) + rise;
                if (baseHeight > 0.01f)
                {
                    AddBox(Box(x - 0.6f, x + width + 0.6f, z - 6.5f, z + 0.2f, baseHeight - 2.6f - (onTerrace ? 0 : 0), baseHeight), Stone.Darkened(0.1f));
                }

                House(x, x + width, z - 6f, z, walls[rng.RandiRange(0, walls.Length - 1)], Stone, storeys, 3f, frame: rng.Randf() > 0.5f, baseHeight, detail: row == 0);
            }
        }

        // The town church's tower on the hill: the skyline's landmark.
        AddBox(new Vector3(44, 9.5f, -24), new Vector3(4.5f, 19f, 4.5f), Lime.Darkened(0.05f));
        Roof(new Vector3(41.5f, 19f, -21.5f), new Vector3(46.5f, 19f, -21.5f), 22.5f, -24f);
        Roof(new Vector3(46.5f, 19f, -26.5f), new Vector3(41.5f, 19f, -26.5f), 22.5f, -24f);

        // Beyond the west end, the terrace runs on into the old town; beyond the east, a shingle beach.
        AddBox(Box(80f, 140f, 34.5f, 46f, -2.2f, -0.9f), new Color(0.62f, 0.56f, 0.45f));
    }

    private void BuildLights()
    {
        foreach (var x in new[] { 12f, 29f, 47f, 60f })
        {
            Lantern(new Vector3(x, 2.4f, 34f), onPost: true);
        }

        Lantern(new Vector3(9.6f, 5f, 24f), onPost: true);
    }

    // ---- small parts

    private void Banner(Vector3 at, Color field, Color charge)
    {
        AddBox(at + new Vector3(0.55f, -0.7f, 0), new Vector3(1.0f, 1.3f, 0.03f), field);
        AddBox(at + new Vector3(0.55f, -0.7f, 0.03f), new Vector3(0.35f, 0.35f, 0.02f), charge);
    }

    private void Board(Vector3 at)
    {
        AddBox(at, new Vector3(1.6f, 1.1f, 0.08f), Wood);
        for (var i = 0; i < 5; i++)
        {
            AddBox(at + new Vector3(-0.5f + i * 0.25f, 0.1f * ((i % 3) - 1), 0.05f), new Vector3(0.22f, 0.3f, 0.01f), new Color(0.9f, 0.86f, 0.74f));
        }

        AddBox(at + new Vector3(-0.7f, -0.9f, 0), new Vector3(0.08f, 1.0f, 0.08f), Timber);
        AddBox(at + new Vector3(0.7f, -0.9f, 0), new Vector3(0.08f, 1.0f, 0.08f), Timber);
    }

    private void HangingSign(Vector3 at, Color face)
    {
        AddBox(at + new Vector3(0, 0.35f, -0.35f), new Vector3(0.08f, 0.08f, 0.9f), Timber);
        AddBox(at, new Vector3(0.06f, 0.6f, 0.8f), face);
        AddBox(at + new Vector3(0.04f, 0, 0), new Vector3(0.02f, 0.35f, 0.08f), new Color(0.82f, 0.7f, 0.35f));
        AddBox(at + new Vector3(0.04f, -0.12f, 0), new Vector3(0.02f, 0.06f, 0.35f), new Color(0.82f, 0.7f, 0.35f));
    }

    private void Lantern(Vector3 at, bool onPost = false)
    {
        if (onPost)
        {
            AddBox(at + new Vector3(0, -1.2f, 0), new Vector3(0.12f, 2.4f, 0.12f), Timber);
        }

        var glass = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.8f, 0.45f), EmissionEnabled = true, Emission = new Color(1f, 0.7f, 0.35f), EmissionEnergyMultiplier = 0.2f };
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.22f, 0.3f, 0.22f) }, MaterialOverride = glass, Position = at });
        var light = new OmniLight3D { Position = at, LightColor = new Color(1f, 0.72f, 0.4f), OmniRange = 8f, OmniAttenuation = 1.4f };
        AddChild(light);
        _lanterns.Add((light, glass));
    }

    private static CpuParticles3D Smoke(Vector3 at, bool dark) => new()
    {
        Position = at,
        Amount = 24,
        Lifetime = 5f,
        Direction = new Vector3(0.2f, 1, 0),
        Spread = 12f,
        Gravity = new Vector3(0.2f, 0.25f, 0),
        InitialVelocityMin = 0.4f,
        InitialVelocityMax = 0.7f,
        ScaleAmountMin = 0.6f,
        ScaleAmountMax = 1.6f,
        Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f, RadialSegments = 6, Rings = 3 },
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = dark ? new Color(0.3f, 0.29f, 0.28f, 0.3f) : new Color(0.6f, 0.58f, 0.55f, 0.22f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        },
    };

    private static Aabb Box(float x0, float x1, float z0, float z1, float y0, float y1) =>
        new(new Vector3(x0, y0, z0), new Vector3(x1 - x0, y1 - y0, z1 - z0));

    private MeshInstance3D AddBox(Aabb box, Color colour) => AddBox(box.GetCenter(), box.Size, colour);

    private MeshInstance3D AddBox(Vector3 position, Vector3 size, Color colour, bool emissive = false)
    {
        var material = emissive
            ? new StandardMaterial3D { AlbedoColor = colour, EmissionEnabled = true, Emission = colour, EmissionEnergyMultiplier = 1.5f }
            : Material(colour);
        var box = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = material, Position = position };
        AddChild(box);
        return box;
    }

    private void AddCylinder(Vector3 position, float radius, float height, Color colour, bool rotateX = false)
    {
        var cylinder = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height, RadialSegments = 12 },
            MaterialOverride = Material(colour),
            Position = position,
        };
        if (rotateX) cylinder.RotationDegrees = new Vector3(90, 0, 0);
        AddChild(cylinder);
    }

    private StandardMaterial3D Material(Color colour)
    {
        if (!_materials.TryGetValue(colour, out var material))
        {
            material = new StandardMaterial3D { AlbedoColor = colour, Roughness = 0.9f };
            _materials[colour] = material;
        }

        return material;
    }
}
