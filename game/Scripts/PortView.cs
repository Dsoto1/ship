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
    private readonly List<OmniLight3D> _lanterns = [];
    private MeshBatcher _batch = new();
    private MeshBatcher _covers = new();
    private Node3D _coverNode = null!;
    private MeshBatcher _dry = new();
    private Node3D _dryNode = null!;

    /// <summary>True while it rains: the stallholders' goods are under cloth, the sailcloth taken in.</summary>
    public bool Covered
    {
        set
        {
            if (_coverNode is not null)
            {
                _coverNode.Visible = value;
                _dryNode.Visible = !value;
            }
        }
    }
    private readonly List<(Node3D Node, StandardMaterial3D Material, Aabb Bounds)> _foreground = [];
    private StandardMaterial3D _glass = null!;
    private readonly List<OmniLight3D> _fires = [];
    private ShaderMaterial _roof = null!;
    private ShaderMaterial _ground = null!;
    private LocationDefinition _location = null!;

    /// <summary>The Garza's gangway, taken in at departure.</summary>
    public Node3D Gangway { get; private set; } = null!;

    /// <summary>Her mooring lines to the bollards, cast off at departure.</summary>
    public Node3D Lines { get; private set; } = null!;

    /// <summary>Centre of the Garza's hull where she lies alongside, at the waterline-relative deck height.</summary>
    public Vector3 Berth { get; private set; }

    /// <summary>
    /// Tall pieces near the camera (the hoist, a moored ship's masts) that fade when they cover the
    /// people the player is following (PORT_VISUAL_PASS §3.12): node, its material, and world bounds.
    /// </summary>
    public IReadOnlyList<(Node3D Node, StandardMaterial3D Material, Aabb Bounds)> Foreground => _foreground;

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

    private IReadOnlyDictionary<string, Expedition.Simulation.Tasks.TaskDefinition> _tasks = new Dictionary<string, Expedition.Simulation.Tasks.TaskDefinition>();

    /// <summary>Builds the waterfront for <paramref name="location"/>; the tasks say where work loops run.</summary>
    public void Build(LocationDefinition location, IReadOnlyDictionary<string, Expedition.Simulation.Tasks.TaskDefinition> tasks)
    {
        _tasks = tasks;
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _batch = new MeshBatcher();
        _covers = new MeshBatcher();
        _dry = new MeshBatcher();
        _location = location;
        _lanterns.Clear();
        _fires.Clear();
        _foreground.Clear();
        _shells.Clear();
        _fading.Clear();
        _roof = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/roof.gdshader") };
        _ground = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/ground.gdshader") };
        _ground.SetShaderParameter("quay_from", 29.5f);
        _ground.SetShaderParameter("detail", GroundDetail.Build(location, _tasks));
        _ground.SetShaderParameter("map_size", new Vector2(location.Width, location.Height));
        Textures(_ground, "cobble", "cobblestone_floor_08");
        _ground.SetShaderParameter("scenery_tex", GD.Load<TextureLayered>("res://Assets/Textures/scenery_array/scenery_diff.jpg"));
        _ground.SetShaderParameter("scenery_normal", GD.Load<TextureLayered>("res://Assets/Textures/scenery_array/scenery_nor_gl.jpg"));
        _ground.SetShaderParameter("scenery_rough", GD.Load<TextureLayered>("res://Assets/Textures/scenery_array/scenery_rough.jpg"));
        Textures(_roof, "tiles", "clay_roof_tiles_02");
        _glass = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.8f, 0.45f), EmissionEnabled = true, Emission = new Color(1f, 0.7f, 0.35f), EmissionEnergyMultiplier = 0.2f };

        var ship = location.Features.FirstOrDefault(f => f.Kind == "ship");
        Berth = ship is null ? new Vector3(31, 0, 39.3f) : new Vector3((ship.X0 + ship.X1) / 2f, 0, 39.3f);

        BuildGround();
        foreach (var feature in location.Features)
        {
            BuildFeature(feature);
        }

        BuildSeats();
        BuildTownBehind();
        Lanes.Clear();
        Idlers.Clear();
        BuildOutskirts();
        BuildLights();
        _batch.Commit(this);
        _coverNode = new Node3D { Name = "RainCovers", Visible = false };
        AddChild(_coverNode);
        _covers.Commit(_coverNode);
        _dryNode = new Node3D { Name = "FairWeatherOnly" };
        AddChild(_dryNode);
        _dry.Commit(_dryNode);
    }

    // Hands a shader the colour, normal and roughness maps of a CC0 texture (Assets/CREDITS.md).
    private static void Textures(ShaderMaterial material, string name, string asset)
    {
        var folder = $"res://Assets/Textures/{asset}/{asset}";
        material.SetShaderParameter(name == "cobble" ? "cobble_tex" : name, GD.Load<Texture2D>($"{folder}_diff_1k.jpg"));
        material.SetShaderParameter($"{name}_normal", GD.Load<Texture2D>($"{folder}_nor_gl_1k.jpg"));
        material.SetShaderParameter($"{name}_rough", GD.Load<Texture2D>($"{folder}_rough_1k.jpg"));
    }

    /// <summary>Lanterns, forge and cook fire by the light of day; wet stone in rain.</summary>
    public void UpdateLook(float darkness, float time, float wetness = 0f)
    {
        var flicker = 0.9f + 0.1f * Mathf.Sin(time * 11f) * Mathf.Sin(time * 4.3f);
        foreach (var light in _lanterns)
        {
            light.LightEnergy = darkness * 3.2f * flicker;
        }

        _glass.EmissionEnergyMultiplier = 0.2f + darkness * 3f;

        foreach (var fire in _fires)
        {
            fire.LightEnergy = (0.7f + darkness * 1.6f) * flicker;
        }

        _ground.SetShaderParameter("wetness", wetness);

        // Rain darkens and glosses everything that stands out in it, not only the ground.
        SceneryMaterial.Wetness = wetness;
        _roof.SetShaderParameter("wetness", wetness);
    }

    // ---- ground, terrace, steps, quay edge

    private void BuildGround()
    {
        // The town's ground, from well behind the back lane down to the quay edge, and the terrace top.
        GroundQuad(-72, -40, 100, 34.5f, 0f);
        GroundQuad(-72, -40, 18.5f, 24.5f, 2.6f);

        // The terrace: a stone mass with its retaining walls, broken by two flights of steps.
        AddBox(Box(-72, 18.5f, -40, 24.5f, 0, 2.58f), DarkStone);
        AddBox(Box(18.5f, 19.5f, -40, 14.5f, 0, 2.6f), Stone);
        AddBox(Box(18.5f, 19.5f, 18.5f, 25.5f, 0, 2.6f), Stone);
        AddBox(Box(-72, 5.5f, 24.5f, 25.5f, 0, 2.6f), Stone);
        AddBox(Box(9.5f, 18.5f, 24.5f, 25.5f, 0, 2.6f), Stone);
        Steps(18.5f, 22.5f, 14.5f, 18.5f, alongX: true);
        Steps(5.5f, 9.5f, 24.5f, 29.5f, alongX: false);

        // A balustrade round the terrace edge, looking out over the harbour (§3.5, §4).
        Balustrade(new Vector3(-72, 2.6f, 24.6f), new Vector3(5.5f, 2.6f, 24.6f));
        Balustrade(new Vector3(9.5f, 2.6f, 24.6f), new Vector3(19.4f, 2.6f, 24.6f));
        Balustrade(new Vector3(19.4f, 2.6f, 18.5f), new Vector3(19.4f, 2.6f, 24.6f));
        Balustrade(new Vector3(19.4f, 2.6f, -1f), new Vector3(19.4f, 2.6f, 14.5f));

        // The quay wall dropping to the water, with its coping, and a stretch of shingle beyond the yard.
        AddBox(Box(-72, 100, 34.5f, 34.9f, -2.8f, 0f), DarkStone);
        AddBox(Box(-72, 100, 34.45f, 35f, -0.02f, 0.12f), Stone);
        AddBox(Box(-72, 100, 34.9f, 35.2f, -1.3f, -0.9f), new Color(0.2f, 0.3f, 0.22f));
    }

    private void GroundQuad(float x0, float z0, float x1, float z1, float y)
    {
        // In 8 m tiles, so the ground batches and culls with the rest of the scenery.
        for (var tx = x0; tx < x1; tx += 8f)
        {
            for (var tz = z0; tz < z1; tz += 8f)
            {
                var ex = Mathf.Min(tx + 8f, x1);
                var ez = Mathf.Min(tz + 8f, z1);
                Vector3[] v = [new(tx, y, tz), new(ex, y, tz), new(ex, y, ez), new(tx, y, tz), new(ex, y, ez), new(tx, y, ez)];
                _batch.AddTriangles(_ground, v, v.Select(p => new Vector2(p.X, p.Z)).ToArray());
            }
        }
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
        AddBox(mid + new Vector3(0, 0.95f, 0), new Vector3(along.X == 0 ? 0.25f : length, 0.12f, along.Z == 0 ? 0.25f : length), Lime.Darkened(0.12f));
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
                var beside = RoomsBeside(f);
                if (beside.Count == 0)
                {
                    Building(f.Style ?? "house", left, right, back, front, HeightAt(middle.X, back + 0.2f));
                    break;
                }

                // A building east of a room hides that room's east end from the camera: built into a node
                // of its own, so it can fade while the captain is in there.
                var scenery = _batch;
                _batch = new MeshBatcher();
                var shellsBefore = _shells.Keys.ToHashSet();
                Building(f.Style ?? "house", left, right, back, front, HeightAt(middle.X, back + 0.2f));
                var piece = new Node3D { Name = $"{f.Style}_beside" };
                AddChild(piece);
                _batch.Commit(piece);
                _batch = scenery;
                // A room's own shell (the counting house beside the Chamber) fades with it.
                var pieces = new List<Node3D> { piece };
                pieces.AddRange(_shells.Where(p => !shellsBefore.Contains(p.Key)).Select(p => p.Value));
                foreach (var room in beside)
                {
                    if (!_fading.TryGetValue(room, out var list))
                    {
                        _fading[room] = list = [];
                    }

                    list.AddRange(pieces);
                }

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
            case "palm":
                Palm(middle, (f.X0 * 31 + f.Y0 * 17) % 5);
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
                // Sailcloth spread on the ground for mending: taken in when it rains.
                var dry = _batch;
                _batch = _dry;
                AddBox(middle + new Vector3(0, 0.02f, 0), new Vector3(right - left, 0.03f, front - back), SceneryMaterial.Cloth(Canvas));
                AddBox(middle + new Vector3(0.6f, 0.04f, 0.2f), new Vector3(0.9f, 0.02f, 0.5f), SceneryMaterial.Cloth(Canvas.Darkened(0.12f)));
                _batch = dry;
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
            case "planks":
                // Sawn planks stacked on bearers, offcuts scattered beside: the repair station's stock.
                for (var i = 0; i < 4; i++)
                {
                    AddBox(new Vector3(middle.X, 0.14f + i * 0.09f, middle.Z), new Vector3(right - left - (i % 2) * 0.2f, 0.07f, 0.34f), Wood.Lightened(0.06f * (i % 3)));
                }

                AddBox(new Vector3(middle.X, 0.05f, middle.Z), new Vector3(right - left - 0.4f, 0.1f, 0.5f), WetWood);
                AddBox(new Vector3(middle.X + 0.9f, 0.03f, middle.Z + 0.45f), new Vector3(0.3f, 0.05f, 0.12f), Wood.Lightened(0.15f), rotation: new Vector3(0, 30, 0));
                AddBox(new Vector3(middle.X - 0.6f, 0.03f, middle.Z + 0.5f), new Vector3(0.22f, 0.05f, 0.1f), Wood.Lightened(0.15f), rotation: new Vector3(0, -50, 0));
                break;
            case "sawhorse":
                foreach (var side in new[] { -0.35f, 0.35f })
                {
                    AddBox(middle + new Vector3(side, 0.35f, -0.18f), new Vector3(0.06f, 0.75f, 0.06f), Wood, rotation: new Vector3(-14, 0, 0));
                    AddBox(middle + new Vector3(side, 0.35f, 0.18f), new Vector3(0.06f, 0.75f, 0.06f), Wood, rotation: new Vector3(14, 0, 0));
                }

                AddBox(middle + new Vector3(0, 0.72f, 0), new Vector3(0.9f, 0.08f, 0.1f), Wood.Darkened(0.1f));
                AddBox(middle + new Vector3(0.1f, 0.8f, 0), new Vector3(1.4f, 0.06f, 0.24f), Wood.Lightened(0.1f));
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
                Chamber(left, right, back, front, baseHeight);
                break;
            case "counting":
                CountingHouse(left, right, back, front, baseHeight);
                break;
            case "tavern":
                Tavern(left, right, back, front, baseHeight);
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
                Chandlery(left, right, back, front, baseHeight);
                break;
            case "cooperage":
                House(left, right, back, front, new Color(0.76f, 0.68f, 0.56f), Stone, 1, 3.6f, frame: true, baseHeight);
                AddBox(new Vector3(middleX, baseHeight + 1.4f, front + 0.04f), new Vector3(3.2f, 2.6f, 0.06f), Shadow);
                for (var i = 0; i < 3; i++)
                {
                    Cask(new Vector3(right - 0.8f - i * 0.75f, baseHeight + 0.42f, front + 0.6f), false);
                }

                CooperageKit(left, front, baseHeight);
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

    // Builds a piece into its own mesh with a fading material, so it can thin out when it covers
    // someone; everything drawn inside <paramref name="draw"/> belongs to the piece.
    private void ForegroundPiece(Action draw)
    {
        var scenery = _batch;
        _batch = new MeshBatcher(ownMaterial: true);
        draw();
        var root = new Node3D();
        AddChild(root);
        var made = _batch.Commit(root);
        _batch = scenery;
        var material = (StandardMaterial3D)made[0].MaterialOverride;
        material.Transparency = BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass;
        var bounds = made.Select(m => m.GetAabb()).Aggregate((a, b) => a.Merge(b));
        _foreground.Add((root, material, bounds));
    }

    // Shear-legs over the lighter: two spars lashed at the head, a block and tackle, a sling of casks
    // halfway up; how a small quay without a crane swings loads into a boat.
    private void ShearLegs(Vector3 foot, float height)
    {
        ForegroundPiece(() =>
        {
            var head = foot + new Vector3(0, height, 1.2f);
            foreach (var side in new[] { -1.1f, 1.1f })
            {
                Spar(foot + new Vector3(side, 0, 0), head, 0.09f, WetWood);
            }

            Spar(head, foot + new Vector3(0, 0, -3.2f), 0.025f, Rope);
            Spar(head, head + new Vector3(0, -2.6f, 0), 0.02f, Rope);
            AddBox(head + new Vector3(0, -0.25f, 0), new Vector3(0.18f, 0.32f, 0.12f), Wood.Darkened(0.2f));
            AddBox(head + new Vector3(0, -2.85f, 0), new Vector3(0.7f, 0.5f, 0.5f), new Color(0.46f, 0.32f, 0.18f));
            AddBox(head + new Vector3(0, -2.85f, 0), new Vector3(0.72f, 0.05f, 0.52f), Rope);
        });
    }

    // A pole between two points.
    private void Spar(Vector3 from, Vector3 to, float radius, Color colour)
    {
        var direction = to - from;
        var length = direction.Length();
        var mesh = new CylinderMesh { TopRadius = radius * 0.85f, BottomRadius = radius, Height = length, RadialSegments = 6, Rings = 1 };
        _batch.AddColoured(mesh, new Transform3D(new Basis(new Quaternion(Vector3.Up, direction / length)), (from + to) / 2f), colour);
    }

    // ---- dressing kits: the secondary layer that says what a building is for (PORT_VISUAL_PASS §3.4)

    // The chandler's: doors open on the stores, a load on the hoist line, rope, canvas and the steelyard.
    private void ChandlerKit(float doorX, float front, float baseHeight)
    {
        var at = new Vector3(doorX, baseHeight, front);
        // Door leaves swung open against the wall.
        AddBox(at + new Vector3(-1.75f, 1.5f, 0.45f), new Vector3(0.9f, 2.9f, 0.07f), Timber.Lightened(0.08f), rotation: new Vector3(0, -70, 0));
        AddBox(at + new Vector3(1.75f, 1.5f, 0.45f), new Vector3(0.9f, 2.9f, 0.07f), Timber.Lightened(0.08f), rotation: new Vector3(0, 70, 0));
        // Rope coils in two heaps, and canvas bolts on a trestle.
        foreach (var (x, z, n) in new[] { (-2.6f, 1.0f, 3), (-1.9f, 1.7f, 2) })
        {
            for (var i = 0; i < n; i++)
            {
                AddShape(new TorusMesh { InnerRadius = 0.12f, OuterRadius = 0.34f, Rings = 12, RingSegments = 5 }, at + new Vector3(x, 0.07f + i * 0.13f, z), Rope.Lightened(0.04f * i));
            }
        }

        Trestle(at + new Vector3(2.4f, 0, 1.3f), 1.6f, 0.5f);
        for (var i = 0; i < 3; i++)
        {
            AddCylinder(at + new Vector3(2.4f, 0.86f + (i == 2 ? 0.2f : 0f), 1.16f + (i == 2 ? 0.14f : i * 0.28f)), 0.13f, 1.4f, SceneryMaterial.Cloth(Canvas.Darkened(0.04f * i)), rotation: new Vector3(0, 0, 90));
        }

        // The steelyard: a post, a beam, a pan.
        AddBox(at + new Vector3(1.1f, 1.1f, 2.1f), new Vector3(0.1f, 2.2f, 0.1f), Timber);
        AddBox(at + new Vector3(1.1f, 2.15f, 2.1f), new Vector3(1.1f, 0.05f, 0.05f), new Color(0.25f, 0.24f, 0.24f), rotation: new Vector3(0, 0, -6));
        AddCylinder(at + new Vector3(0.65f, 1.35f, 2.1f), 0.2f, 0.04f, new Color(0.3f, 0.28f, 0.26f));
        AddCylinder(at + new Vector3(0.65f, 1.75f, 2.1f), 0.008f, 0.75f, Rope);
    }

    /// <summary>The roof, upper floor and front wall of the room the captain can walk into, by room ID.</summary>
    public IReadOnlyDictionary<string, Node3D> RoomShells => _shells;

    private readonly Dictionary<string, Node3D> _shells = [];

    /// <summary>
    /// Lifts the roof off <paramref name="roomId"/> (the captain is in it), or puts every roof back on;
    /// the buildings beside it that would hide its east end fade to a ghost while he is in there.
    /// </summary>
    public void OpenRoom(string? roomId, float delta = 1f)
    {
        foreach (var (id, shell) in _shells)
        {
            shell.Visible = id != roomId;
        }

        var faded = roomId is not null && _fading.TryGetValue(roomId, out var list) ? list : [];
        foreach (var piece in _fading.Values.SelectMany(p => p).Distinct())
        {
            var target = faded.Contains(piece) ? 0.78f : 0f;
            foreach (var mesh in piece.GetChildren().OfType<GeometryInstance3D>())
            {
                mesh.Transparency = Mathf.MoveToward(mesh.Transparency, target, delta * 3f);
                mesh.CastShadow = mesh.Transparency > 0.5f ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On;
            }
        }
    }

    private readonly Dictionary<string, List<Node3D>> _fading = [];

    // The rooms whose building stands just west of <paramref name="building"/> (within four cells, the same
    // rows): from the camera, this one stands in front of their east ends.
    private List<string> RoomsBeside(LocationFeature building)
    {
        var found = new List<string>();
        foreach (var room in _location.Rooms)
        {
            var home = _location.Features.FirstOrDefault(f => f.Kind == "building" && f.X0 <= room.X0 && f.X1 >= room.X1 && f.Y0 <= room.Y0 && f.Y1 >= room.Y1);
            if (home is not null && home != building && building.X0 > home.X1 && building.X0 - home.X1 <= 4
                && building.Y0 <= home.Y1 && building.Y1 >= home.Y0)
            {
                found.Add(room.Id);
            }
        }

        return found;
    }

    // The Three Anchors: a kitchen range behind, and in front the common room on the map (rows 6-12),
    // walls, floor and furniture always built; its upper floor, roof and the front wall above the sill
    // go in a shell that lifts away while the captain is inside, so he sees the room and who is in it.
    private void Tavern(float left, float right, float back, float front, float baseHeight)
    {
        const float Storey = 3.2f;
        var split = 28.5f;
        var wall = Plaster;
        var room = 5.5f;
        // The kitchen range at the back stays whole.
        House(left, split, back, room, wall, Stone, 2, Storey, frame: true, baseHeight, detail: false);
        House(split, right, back, room, new Color(0.8f, 0.74f, 0.62f), Stone, 3, 3f, frame: true, baseHeight, detail: false);

        // The common room: plank floor, walls, the sill-high front wall with the door gap.
        RoomFloorAndWalls(left, right, room, front, 28f, baseHeight, Storey, new Color(0.42f, 0.3f, 0.19f), planks: true, wall.Darkened(0.15f));

        // The hearth on the back wall, with its fire, and Nuño's pot over it.
        AddBox(Box(23.5f, 25.5f, room, room + 1f, baseHeight, baseHeight + 1.2f), DarkStone);
        AddBox(Box(23.7f, 25.3f, room + 0.6f, room + 1.02f, baseHeight + 0.15f, baseHeight + 0.95f), Shadow);
        AddBox(new Vector3(24.5f, baseHeight + 0.3f, room + 0.7f), new Vector3(0.9f, 0.25f, 0.3f), new Color(1f, 0.45f, 0.12f), emissive: true);
        AddCylinder(new Vector3(24.5f, baseHeight + 0.75f, room + 0.7f), 0.22f, 0.35f, Iron);
        Fire(new Vector3(24.5f, baseHeight + 1f, room + 1.6f), 5f);

        // The counter, and behind it the casks on their racks and cups on a shelf.
        AddBox(Box(29.5f, 33.5f, 7.5f, 8.5f, baseHeight, baseHeight + 1.05f), Wood.Darkened(0.1f));
        AddBox(Box(29.4f, 33.6f, 7.45f, 8.55f, baseHeight + 1.05f, baseHeight + 1.12f), Wood);
        for (var x = 30.2f; x < 33.4f; x += 0.75f)
        {
            Cask(new Vector3(x, baseHeight + 0.62f, room + 0.45f), true);
        }

        AddBox(Box(29.6f, 33.4f, room, room + 0.3f, baseHeight + 1.7f, baseHeight + 1.76f), Wood);
        for (var x = 29.9f; x < 33.3f; x += 0.4f)
        {
            AddCylinder(new Vector3(x, baseHeight + 1.86f, room + 0.15f), 0.06f, 0.18f, new Color(0.62f, 0.42f, 0.28f));
        }

        // Two tables with their benches, cups and a jug, a candle on each.
        foreach (var (x0, x1, z) in new[] { (24.5f, 27.5f, 10f), (30.5f, 32.5f, 11f) })
        {
            AddBox(Box(x0, x1, z - 0.45f, z + 0.45f, baseHeight + 0.72f, baseHeight + 0.78f), Wood);
            foreach (var x in new[] { x0 + 0.2f, x1 - 0.2f })
            {
                AddBox(new Vector3(x, baseHeight + 0.36f, z), new Vector3(0.1f, 0.72f, 0.7f), Wood.Darkened(0.15f));
            }

            foreach (var bench in new[] { z - 0.85f, z + 0.85f })
            {
                AddBox(Box(x0, x1, bench - 0.18f, bench + 0.18f, baseHeight + 0.4f, baseHeight + 0.46f), Wood.Darkened(0.05f));
                AddBox(Box(x0 + 0.1f, x1 - 0.1f, bench - 0.08f, bench + 0.08f, baseHeight, baseHeight + 0.4f), Wood.Darkened(0.25f));
            }

            AddCylinder(new Vector3((x0 + x1) / 2f, baseHeight + 0.9f, z), 0.07f, 0.22f, new Color(0.62f, 0.42f, 0.28f));
            AddCylinder(new Vector3(x0 + 0.6f, baseHeight + 0.83f, z + 0.2f), 0.045f, 0.1f, new Color(0.7f, 0.55f, 0.38f));
            AddCylinder(new Vector3(x1 - 0.7f, baseHeight + 0.83f, z - 0.15f), 0.045f, 0.1f, new Color(0.7f, 0.55f, 0.38f));
            // A tallow candle, lit: a small flame and its glow, brighter as the day goes.
            AddCylinder(new Vector3(x1 - 0.4f, baseHeight + 0.86f, z), 0.03f, 0.16f, new Color(0.95f, 0.9f, 0.75f));
            AddBox(new Vector3(x1 - 0.4f, baseHeight + 0.97f, z), new Vector3(0.025f, 0.05f, 0.025f), new Color(1f, 0.75f, 0.35f), emissive: true);
            var candle = new OmniLight3D { Position = new Vector3(x1 - 0.4f, baseHeight + 1.15f, z), LightColor = new Color(1f, 0.7f, 0.4f), OmniRange = 3.5f, OmniAttenuation = 1.4f };
            AddChild(candle);
            _lanterns.Add(candle);
        }

        // Rushes and a dog by the fire.
        AddBox(new Vector3(26.4f, baseHeight + 0.12f, room + 1.7f), new Vector3(0.7f, 0.24f, 0.32f), new Color(0.45f, 0.36f, 0.25f));
        AddShape(new SphereMesh { Radius = 0.13f, Height = 0.24f, RadialSegments = 8, Rings = 4 }, new Vector3(26.85f, baseHeight + 0.2f, room + 1.75f), new Color(0.45f, 0.36f, 0.25f));

        // The shell: upper floors, roof, and the front wall above the sill (windows and the door's head).
        RoomShell("room.three_anchors", () =>
        {
            House(left, split, room, front, wall, wall, 2, Storey, frame: true, baseHeight, detail: true);
            House(split, right, room, front, new Color(0.8f, 0.74f, 0.62f), new Color(0.8f, 0.74f, 0.62f), 3, 3f, frame: true, baseHeight, detail: true);
            AddBox(new Vector3(28f, baseHeight + 1.4f, front + 0.06f), new Vector3(1.0f, 2.5f, 0.08f), Timber);
            foreach (var x in new[] { 25f, 32f })
            {
                AddBox(new Vector3(x, baseHeight + 1.7f, front + 0.06f), new Vector3(1.1f, 0.9f, 0.06f), _glass.AlbedoColor.Darkened(0.5f));
            }
        });

        HangingSign(new Vector3(29.1f, baseHeight + 3.4f, front + 0.7f), new Color(0.2f, 0.25f, 0.32f));
        Lantern(new Vector3(26.9f, baseHeight + 2.6f, front + 0.3f));
        TavernKit(28f, front, baseHeight);
    }

    // A room on the map inside a building (LocationDefinition.Rooms): its floor, the west and back walls
    // to the ceiling, and the east and front walls to the sill, the front broken by the door (a cell wide
    // at doorX). Everything above the sill is in the room's shell (RoomShell), which lifts while the
    // captain is inside, so the camera's corner of the room is always open to it.
    private void RoomFloorAndWalls(float left, float right, float roomBack, float front, float doorX, float baseHeight, float storey, Color floor, bool planks, Color inner)
    {
        AddBox(Box(left + 1f, right - 1f, roomBack, front - 1f, baseHeight, baseHeight + 0.04f), floor);
        if (planks)
        {
            for (var z = roomBack + 0.5f; z < front - 1f; z += 0.5f)
            {
                AddBox(new Vector3((left + right) / 2f, baseHeight + 0.045f, z), new Vector3(right - left - 2f, 0.01f, 0.03f), floor.Darkened(0.25f));
            }
        }
        else
        {
            // Flagstones: the joints a metre apart, staggered.
            for (var z = roomBack + 1f; z < front - 1f; z += 1f)
            {
                AddBox(new Vector3((left + right) / 2f, baseHeight + 0.045f, z), new Vector3(right - left - 2f, 0.01f, 0.04f), floor.Darkened(0.3f));
            }

            for (var x = left + 1.5f; x < right - 1f; x += 1f)
            {
                AddBox(new Vector3(x, baseHeight + 0.045f, (roomBack + front - 1f) / 2f), new Vector3(0.04f, 0.01f, front - 1f - roomBack), floor.Darkened(0.3f));
            }
        }

        AddBox(Box(left, left + 1f, roomBack, front, baseHeight, baseHeight + storey), Stone);
        AddBox(Box(right - 1f, right, roomBack, front, baseHeight, baseHeight + 0.9f), Stone);
        AddBox(Box(left, doorX - 0.5f, front - 1f, front, baseHeight, baseHeight + 0.9f), Stone);
        AddBox(Box(doorX + 0.5f, right, front - 1f, front, baseHeight, baseHeight + 0.9f), Stone);
        AddBox(Box(left + 1f, right - 1f, roomBack - 0.05f, roomBack, baseHeight, baseHeight + storey), inner);
    }

    // Builds a room's shell (what <paramref name="draw"/> adds) into a node of its own, which OpenRoom hides.
    private void RoomShell(string roomId, Action draw)
    {
        var shellBatch = new MeshBatcher();
        var scenery = _batch;
        _batch = shellBatch;
        draw();
        _batch = scenery;
        var shell = new Node3D { Name = roomId.Replace("room.", "") + "_shell" };
        AddChild(shell);
        shellBatch.Commit(shell);
        _shells[roomId] = shell;
    }

    // A candle or a hanging lamp inside: a small flame and its glow, which follows the lanterns at dusk.
    private void Candle(Vector3 at, float range = 3.5f)
    {
        AddCylinder(at, 0.03f, 0.16f, new Color(0.95f, 0.9f, 0.75f));
        AddBox(at + new Vector3(0, 0.11f, 0), new Vector3(0.025f, 0.05f, 0.025f), new Color(1f, 0.75f, 0.35f), emissive: true);
        var light = new OmniLight3D { Position = at + new Vector3(0, 0.3f, 0), LightColor = new Color(1f, 0.7f, 0.4f), OmniRange = range, OmniAttenuation = 1.4f };
        AddChild(light);
        _lanterns.Add(light);
    }

    // The Chamber of the Ocean Sea: its hall on the terrace (cells x 2-8, y 6-10), the archive behind.
    // Quintana's desk with ledgers, ink and the seal; the licence presses on the back wall under the
    // Crown's banner; a bench where captains wait their turn; a strongbox.
    private void Chamber(float left, float right, float back, float front, float baseHeight)
    {
        const float Storey = 3.8f;
        var room = 5.5f;
        var stone = Stone.Lightened(0.08f);
        House(left, right, back, room, stone, Stone, 2, Storey, frame: false, baseHeight, detail: false);
        RoomFloorAndWalls(left, right, room, front, 5f, baseHeight, Storey, new Color(0.5f, 0.47f, 0.42f), planks: false, Lime.Darkened(0.1f));
        var y = baseHeight;

        // The presses: tall cupboards of pigeonholes, rolled licences in them, against the back wall.
        foreach (var x0 in new[] { 1.6f, 4.1f, 6.6f })
        {
            AddBox(Box(x0, x0 + 2.2f, room, room + 0.45f, y, y + 2.6f), Wood.Darkened(0.2f));
            for (var r = 0; r < 4; r++)
            {
                for (var c = 0; c < 4; c++)
                {
                    var at = new Vector3(x0 + 0.3f + c * 0.53f, y + 0.5f + r * 0.55f, room + 0.46f);
                    AddBox(at, new Vector3(0.44f, 0.44f, 0.02f), Shadow);
                    if ((r + c + (int)x0) % 3 != 0)
                    {
                        AddCylinder(at + new Vector3(0, -0.12f, 0), 0.07f, 0.36f, Canvas.Darkened(0.1f + 0.05f * c), rotation: new Vector3(0, 0, 90));
                    }
                }
            }
        }

        Banner(new Vector3(5f, y + 3.1f, room + 0.15f), new Color(0.62f, 0.13f, 0.12f), new Color(0.85f, 0.7f, 0.25f));

        // The desk across the hall (cells x 3-6, row 8), and on it the ledger of licences, ink, the seal.
        AddBox(Box(2.6f, 6.4f, 7.6f, 8.4f, y + 0.74f, y + 0.82f), Wood);
        AddBox(Box(2.7f, 6.3f, 7.7f, 8.3f, y, y + 0.74f), Wood.Darkened(0.2f));
        AddBox(new Vector3(4f, y + 0.85f, 8f), new Vector3(0.7f, 0.05f, 0.5f), new Color(0.85f, 0.8f, 0.68f));
        AddBox(new Vector3(4.9f, y + 0.9f, 7.9f), new Vector3(0.45f, 0.14f, 0.32f), new Color(0.35f, 0.2f, 0.12f));
        AddBox(new Vector3(5.5f, y + 0.95f, 7.9f), new Vector3(0.4f, 0.24f, 0.3f), new Color(0.3f, 0.17f, 0.1f));
        AddCylinder(new Vector3(3.2f, y + 0.87f, 8.15f), 0.05f, 0.1f, Shadow);
        AddCylinder(new Vector3(6f, y + 0.86f, 8.15f), 0.08f, 0.06f, new Color(0.62f, 0.13f, 0.12f));
        Candle(new Vector3(2.9f, y + 0.9f, 7.85f));
        // The clerk's stool behind it, and the bench along the east wall for those who wait.
        AddCylinder(new Vector3(4f, y + 0.25f, 6.8f), 0.2f, 0.5f, Wood.Darkened(0.1f));
        Bench(8.6f, 10.4f, 7.75f, alongX: false);
        // The strongbox, iron-bound, by the west wall.
        AddBox(new Vector3(2f, y + 0.25f, 10f), new Vector3(0.7f, 0.5f, 0.45f), Wood.Darkened(0.3f));
        AddBox(new Vector3(2f, y + 0.25f, 10f), new Vector3(0.72f, 0.06f, 0.47f), Iron);

        RoomShell("room.chamber", () =>
        {
            House(left, right, room, front, stone, Stone, 2, Storey, frame: false, baseHeight, detail: true);
            Arch(new Vector3(5f, baseHeight, front + 0.02f), 1.9f, 3.0f);
            for (var x = left + 0.7f; x < right; x += (right - left - 1.4f) / 4f)
            {
                AddBox(new Vector3(x, baseHeight + 1.9f, front + 0.15f), new Vector3(0.35f, 3.8f, 0.3f), Stone.Lightened(0.12f));
            }

            Banner(new Vector3(right - 0.6f, baseHeight + 8f, front + 0.2f), new Color(0.62f, 0.13f, 0.12f), new Color(0.85f, 0.7f, 0.25f));
        });
        Lantern(new Vector3(5f - 1.6f, baseHeight + 2.6f, front + 0.3f));
    }

    // The Ledra-Varzi Company's counting room (cells x 12-16, y 6-10), the strongroom behind: the long desk
    // with the counting cloth, coins in stacks, the scales and the ledgers; shelves of letter-books; the
    // Company's iron-bound chest; a bale of sample cloth, a cone of sugar and a pepper sack from the coast.
    private void CountingHouse(float left, float right, float back, float front, float baseHeight)
    {
        var room = 5.5f;
        House(left, right, back, room, Ochre, Stone, 3, 3f, frame: true, baseHeight, detail: false);
        RoomFloorAndWalls(left, right, room, front, 14f, baseHeight, 3f, new Color(0.45f, 0.33f, 0.21f), planks: true, Ochre.Darkened(0.2f));
        var y = baseHeight;

        // The desk (cells x 13-15, row 8) under its chequered counting cloth.
        AddBox(Box(12.6f, 15.4f, 7.6f, 8.4f, y + 0.74f, y + 0.8f), Wood.Darkened(0.05f));
        AddBox(Box(12.7f, 15.3f, 7.7f, 8.3f, y, y + 0.74f), Wood.Darkened(0.2f));
        for (var i = 0; i < 6; i++)
        {
            for (var j = 0; j < 2; j++)
            {
                AddBox(new Vector3(13.05f + i * 0.4f, y + 0.805f, 7.85f + j * 0.3f), new Vector3(0.38f, 0.01f, 0.28f), (i + j) % 2 == 0 ? new Color(0.2f, 0.32f, 0.22f) : new Color(0.3f, 0.42f, 0.3f));
            }
        }

        foreach (var (x, n) in new[] { (13.3f, 5), (13.6f, 3), (14.1f, 7) })
        {
            AddCylinder(new Vector3(x, y + 0.82f + n * 0.01f, 8.05f), 0.06f, n * 0.02f, new Color(0.75f, 0.62f, 0.3f));
        }

        AddBox(new Vector3(14.9f, y + 0.86f, 7.95f), new Vector3(0.5f, 0.08f, 0.36f), new Color(0.3f, 0.17f, 0.1f));
        AddBox(new Vector3(14.6f, y + 1.1f, 8.2f), new Vector3(0.05f, 0.5f, 0.05f), new Color(0.6f, 0.5f, 0.3f));
        AddBox(new Vector3(14.6f, y + 1.33f, 8.2f), new Vector3(0.45f, 0.03f, 0.03f), new Color(0.6f, 0.5f, 0.3f));
        Candle(new Vector3(12.9f, y + 0.84f, 8.15f));
        AddCylinder(new Vector3(13f, y + 0.25f, 6.85f), 0.2f, 0.5f, Wood.Darkened(0.1f));
        AddCylinder(new Vector3(15f, y + 0.25f, 6.85f), 0.2f, 0.5f, Wood.Darkened(0.1f));

        // Letter-books and ledgers on the back wall.
        for (var shelf = 0; shelf < 3; shelf++)
        {
            var h = y + 0.9f + shelf * 0.62f;
            AddBox(Box(11.6f, 16.4f, room, room + 0.35f, h, h + 0.05f), Wood);
            for (var x = 11.75f; x < 16.3f; x += 0.17f)
            {
                var tall = 0.32f + ((int)(x * 37) % 5) * 0.03f;
                AddBox(new Vector3(x, h + 0.05f + tall / 2f, room + 0.18f), new Vector3(0.13f, tall, 0.26f),
                    ((int)(x * 13) % 3) switch { 0 => new Color(0.35f, 0.2f, 0.12f), 1 => new Color(0.45f, 0.3f, 0.16f), _ => new Color(0.5f, 0.15f, 0.12f) });
            }
        }

        // The Company's chest, and goods from the coast: sample cloth, a sugar loaf, a sack of pepper.
        AddBox(new Vector3(12.2f, y + 0.3f, 10f), new Vector3(0.8f, 0.6f, 0.5f), Wood.Darkened(0.3f));
        AddBox(new Vector3(12.2f, y + 0.3f, 10f), new Vector3(0.82f, 0.06f, 0.52f), Iron);
        AddBox(new Vector3(12.2f, y + 0.61f, 10f), new Vector3(0.82f, 0.02f, 0.52f), Iron);
        AddBox(new Vector3(16.3f, y + 0.3f, 10.2f), new Vector3(0.6f, 0.6f, 0.5f), new Color(0.72f, 0.64f, 0.48f));
        AddShape(new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.14f, Height = 0.4f, RadialSegments = 8, Rings = 1 }, new Vector3(16.3f, y + 0.8f, 10.2f), new Color(0.92f, 0.9f, 0.84f));
        AddShape(new SphereMesh { Radius = 0.25f, Height = 0.4f, RadialSegments = 8, Rings = 4 }, new Vector3(15.6f, y + 0.2f, 10.4f), new Color(0.55f, 0.42f, 0.28f));

        RoomShell("room.counting", () =>
        {
            House(left, right, room, front, Ochre, Stone, 3, 3f, frame: true, baseHeight, detail: true);
            var middleX = (left + right) / 2f;
            AddBox(new Vector3(middleX, baseHeight + 3.4f, front + 0.6f), new Vector3(right - left, 0.15f, 1.2f), Timber);
            for (var x = left + 0.2f; x <= right; x += 1.7f)
            {
                AddBox(new Vector3(x, baseHeight + 3.9f, front + 1.15f), new Vector3(0.08f, 0.9f, 0.08f), Timber);
            }

            AddBox(new Vector3(middleX, baseHeight + 4.35f, front + 1.15f), new Vector3(right - left, 0.08f, 0.1f), Timber);
            AddBox(new Vector3(14f, baseHeight + 1.5f, front + 0.06f), new Vector3(1.0f, 2.6f, 0.08f), Timber);
        });
    }

    // Corbalán's chandlery (cells x 48-53, y 7-12), the store behind: the counter with the steelyard and
    // a ledger, shelves of jars, lamps and tallow behind it, rope and canvas, casks along the west wall
    // and biscuit sacks along the east, a lamp over the counter.
    private void Chandlery(float left, float right, float back, float front, float baseHeight)
    {
        const float Storey = 3.6f;
        var room = 6.5f;
        var wall = new Color(0.7f, 0.62f, 0.5f);
        House(left, right, back, room, wall, Stone, 2, Storey, frame: true, baseHeight, detail: false);
        RoomFloorAndWalls(left, right, room, front, 50f, baseHeight, Storey, new Color(0.4f, 0.3f, 0.2f), planks: true, wall.Darkened(0.2f));
        var y = baseHeight;

        // The counter (cells x 48-52, row 9), the steelyard and the account book on it.
        AddBox(Box(47.5f, 52.5f, 8.6f, 9.4f, y, y + 0.95f), Wood.Darkened(0.12f));
        AddBox(Box(47.4f, 52.6f, 8.55f, 9.45f, y + 0.95f, y + 1.02f), Wood);
        AddBox(new Vector3(51.6f, y + 1.45f, 9f), new Vector3(0.08f, 0.9f, 0.08f), Timber);
        AddBox(new Vector3(51.6f, y + 1.9f, 9f), new Vector3(0.9f, 0.04f, 0.04f), Iron, rotation: new Vector3(0, 0, -5));
        AddCylinder(new Vector3(51.25f, y + 1.35f, 9f), 0.16f, 0.03f, Iron);
        AddBox(new Vector3(49.2f, y + 1.06f, 9f), new Vector3(0.5f, 0.05f, 0.36f), new Color(0.35f, 0.2f, 0.12f));
        Candle(new Vector3(48.2f, y + 1.1f, 9f), 4f);

        // Shelves on the back wall: jars of oil and pitch, lamps, bundles of tallow candles, nails.
        for (var shelf = 0; shelf < 3; shelf++)
        {
            var h = y + 0.8f + shelf * 0.7f;
            AddBox(Box(47.6f, 53.4f, room, room + 0.4f, h, h + 0.05f), Wood);
            for (var x = 47.9f; x < 53.2f; x += 0.42f)
            {
                var kind = ((int)(x * 10) + shelf) % 3;
                if (kind == 0)
                {
                    AddCylinder(new Vector3(x, h + 0.17f, room + 0.2f), 0.12f, 0.3f, new Color(0.55f, 0.38f, 0.24f));
                }
                else if (kind == 1)
                {
                    AddBox(new Vector3(x, h + 0.1f, room + 0.2f), new Vector3(0.3f, 0.16f, 0.22f), new Color(0.85f, 0.8f, 0.62f));
                }
                else
                {
                    AddCylinder(new Vector3(x, h + 0.13f, room + 0.2f), 0.08f, 0.22f, Iron.Lightened(0.15f));
                }
            }
        }

        // Rope coils hung on pegs on the west wall, and canvas bolts in the corner.
        for (var i = 0; i < 3; i++)
        {
            AddShape(new TorusMesh { InnerRadius = 0.15f, OuterRadius = 0.38f, Rings = 12, RingSegments = 5 }, new Vector3(47.62f, y + 1.6f, 8f + i * 0.9f), Rope.Lightened(0.05f * i), new Vector3(0, 0, 90));
        }

        for (var i = 0; i < 3; i++)
        {
            AddCylinder(new Vector3(53f, y + 0.15f + i * 0.28f, 7.3f + (i % 2) * 0.1f), 0.14f, 1.2f, SceneryMaterial.Cloth(Canvas.Darkened(0.05f * i)), rotation: new Vector3(90, 0, 0));
        }

        // Casks along the west wall (cells x 48, rows 11-12), one lying on top; biscuit sacks along the east.
        Cask(new Vector3(48f, y + 0.42f, 11f), false);
        Cask(new Vector3(48f, y + 0.42f, 11.75f), false);
        Cask(new Vector3(48f, y + 1.12f, 11.35f), true);
        foreach (var (z, h) in new[] { (10.8f, 0f), (11.6f, 0f), (11.2f, 0.5f), (12.2f, 0f) })
        {
            AddShape(new SphereMesh { Radius = 0.36f, Height = 0.55f, RadialSegments = 8, Rings = 4 }, new Vector3(53f, y + 0.27f + h, z), new Color(0.72f, 0.64f, 0.48f));
        }

        RoomShell("room.chandlery", () =>
        {
            House(left, right, room, front, wall, Stone, 2, Storey, frame: true, baseHeight, detail: true);
            var middleX = (left + right) / 2f;
            AddBox(new Vector3(50f, baseHeight + 1.6f, front + 0.06f), new Vector3(2.6f, 3.0f, 0.08f), Timber);
            AddBox(new Vector3(middleX, baseHeight + 5.5f, front + 0.06f), new Vector3(1.4f, 1.6f, 0.08f), Timber);
            AddBox(new Vector3(middleX, baseHeight + 6.8f, front + 0.9f), new Vector3(0.25f, 0.25f, 1.8f), Timber);
            AddCylinder(new Vector3(middleX, baseHeight + 5.2f, front + 1.6f), 0.02f, 3.2f, Rope);
            // A bale halfway up the hoist line.
            AddBox(new Vector3(middleX, baseHeight + 4.3f, front + 1.6f), new Vector3(0.7f, 0.55f, 0.55f), new Color(0.7f, 0.64f, 0.48f));
            AddBox(new Vector3(middleX, baseHeight + 4.3f, front + 1.6f), new Vector3(0.72f, 0.06f, 0.57f), Rope);
        });
        HangingSign(new Vector3(right - 1f, baseHeight + 3.2f, front + 0.7f), new Color(0.3f, 0.2f, 0.12f));
        ChandlerKit(50f, front, baseHeight);
    }

    // The tavern: the green branch over the door that marks wine for sale, casks, a cask-top table with jug and cups.
    private void TavernKit(float doorX, float front, float baseHeight)
    {
        var at = new Vector3(doorX, baseHeight, front);
        AddBox(at + new Vector3(0, 2.75f, 0.35f), new Vector3(0.06f, 0.06f, 0.7f), Timber);
        foreach (var (dx, dy) in new[] { (0f, 0f), (0.18f, -0.12f), (-0.16f, -0.08f), (0.05f, -0.24f) })
        {
            AddShape(new SphereMesh { Radius = 0.22f, Height = 0.36f, RadialSegments = 8, Rings = 4 }, at + new Vector3(dx, 2.55f + dy, 0.72f), new Color(0.3f, 0.42f, 0.2f));
        }

        Cask(at + new Vector3(-1.4f, 0.42f, 0.55f), false);
        Cask(at + new Vector3(-2.05f, 0.42f, 0.5f), false);
        Cask(at + new Vector3(1.4f, 0.42f, 0.7f), false);
        AddCylinder(at + new Vector3(1.4f, 0.84f, 0.7f), 0.36f, 0.05f, Wood);
        AddCylinder(at + new Vector3(1.32f, 0.97f, 0.66f), 0.07f, 0.22f, new Color(0.62f, 0.42f, 0.28f));
        AddCylinder(at + new Vector3(1.55f, 0.92f, 0.78f), 0.045f, 0.1f, new Color(0.7f, 0.55f, 0.38f));
        // A broom against the wall.
        AddBox(at + new Vector3(2.5f, 0.75f, 0.14f), new Vector3(0.04f, 1.5f, 0.04f), Wood, rotation: new Vector3(0, 0, -10));
        AddBox(at + new Vector3(2.62f, 0.12f, 0.14f), new Vector3(0.24f, 0.24f, 0.08f), new Color(0.6f, 0.5f, 0.3f), rotation: new Vector3(0, 0, -10));
    }

    // The cooperage: staves leaned against the wall, hoops on pegs, a cask half raised in its truss.
    private void CooperageKit(float left, float front, float baseHeight)
    {
        var at = new Vector3(left, baseHeight, front);
        for (var i = 0; i < 9; i++)
        {
            AddBox(at + new Vector3(0.6f + i * 0.14f, 0.6f, 0.2f), new Vector3(0.1f, 1.2f, 0.03f), Wood.Lightened(0.03f * (i % 3)), rotation: new Vector3(-12, 0, (i % 2 == 0 ? 3 : -2)));
        }

        foreach (var x in new[] { 2.2f, 2.65f })
        {
            AddShape(new TorusMesh { InnerRadius = 0.25f, OuterRadius = 0.29f, Rings = 16, RingSegments = 4 }, at + new Vector3(x, 1.7f, 0.1f), Iron, new Vector3(90, 0, 0));
        }

        // Staves standing in a ring, not yet hooped at the top.
        var centre = at + new Vector3(3.6f, 0, 1.2f);
        for (var i = 0; i < 14; i++)
        {
            var angle = i / 14f * Mathf.Tau;
            var spread = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            AddBox(centre + spread * 0.3f + new Vector3(0, 0.4f, 0), new Vector3(0.11f, 0.8f, 0.03f), Wood.Lightened(0.05f),
                rotation: new Vector3(-Mathf.Sin(angle) * 8f, -angle * 180f / Mathf.Pi + 90f, Mathf.Cos(angle) * 8f));
        }

        AddShape(new TorusMesh { InnerRadius = 0.3f, OuterRadius = 0.34f, Rings = 16, RingSegments = 4 }, centre + new Vector3(0, 0.12f, 0), Iron);
    }

    private static readonly Color Iron = new(0.22f, 0.21f, 0.2f);

    // A trestle table: boards on two A-frames.
    private void Trestle(Vector3 at, float width, float depth)
    {
        foreach (var side in new[] { -width / 2f + 0.15f, width / 2f - 0.15f })
        {
            AddBox(at + new Vector3(side, 0.36f, -depth / 3f), new Vector3(0.05f, 0.75f, 0.05f), Wood.Darkened(0.15f), rotation: new Vector3(14, 0, 0));
            AddBox(at + new Vector3(side, 0.36f, depth / 3f), new Vector3(0.05f, 0.75f, 0.05f), Wood.Darkened(0.15f), rotation: new Vector3(-14, 0, 0));
        }

        AddBox(at + new Vector3(0, 0.74f, 0), new Vector3(width, 0.05f, depth), Wood);
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
        _batch.AddTriangles(_roof, [a, d, c, a, c, b],
            [new(0, slope), new(0, 0), new(length, 0), new(0, slope), new(length, 0), new(length, slope)]);
        AddBox((a + b) / 2f + new Vector3(0, -0.06f, 0), new Vector3(Mathf.Abs(b.X - a.X), 0.14f, 0.18f), new Color(0.42f, 0.22f, 0.14f));
    }

    private void Gable(float x, float eave, float ridge, float back, float front, float ridgeZ, Color wall)
    {
        _batch.AddTriangles(_batch.Coloured, [new Vector3(x, eave, back), new Vector3(x, ridge, ridgeZ), new Vector3(x, eave, front)], colour: wall);
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
        AddBox(at + new Vector3(length / 2f + 0.2f, 0.1f, 0), new Vector3(0.9f, 0.6f, 1.1f), hull, rotation: new Vector3(0, 45, 0));
        AddBox(at + new Vector3(0, 0.35f, 0.81f), new Vector3(length, 0.12f, 0.04f), new Color(0.55f, 0.22f, 0.16f));
        if (mast)
        {
            AddBox(at + new Vector3(-0.3f, 2.7f, 0), new Vector3(0.12f, 5f, 0.12f), Timber);
            AddBox(at + new Vector3(-0.3f, 4.1f, 0), new Vector3(0.08f, 0.08f, 3.2f), Timber);
            AddBox(at + new Vector3(-0.3f, 3.3f, 0), new Vector3(0.04f, 1.4f, 2.6f), SceneryMaterial.Cloth(Canvas.Darkened(0.15f)), rotation: new Vector3(0, 0, 4));
        }
    }

    // A merchantman lying at anchor out in the harbour: a hull, castles and three masts (Tier C, §6).
    // A merchantman lying at anchor out in the harbour: a hull with castles and three masts with their
    // sails furled on the yards (Tier C, PORT_EXPERIENCE §6). The rig fades when it covers someone.
    private void Merchantman(Vector3 at, float length)
    {
        var hull = new Color(0.25f, 0.19f, 0.14f);
        AddBox(at + new Vector3(0, 0.6f, 0), new Vector3(length * 0.8f, 2.4f, 5.5f), hull);
        AddBox(at + new Vector3(-length * 0.32f, 2.4f, 0), new Vector3(length * 0.22f, 1.8f, 5.3f), hull.Lightened(0.08f));
        AddBox(at + new Vector3(length * 0.33f, 2.1f, 0), new Vector3(length * 0.16f, 1.2f, 4.6f), hull.Lightened(0.08f));
        AddBox(at + new Vector3(0, 1.85f, 2.76f), new Vector3(length * 0.8f, 0.18f, 0.05f), new Color(0.75f, 0.6f, 0.3f));
        ForegroundPiece(() =>
        {
            foreach (var (x, height) in new[] { (-length * 0.2f, 11f), (length * 0.08f, 14f), (length * 0.3f, 9f) })
            {
                Spar(at + new Vector3(x, 1.8f, 0), at + new Vector3(x, 1.8f + height, 0), 0.16f, Timber);
                var yard = at + new Vector3(x, 1.8f + height * 0.72f, 0);
                Spar(yard + new Vector3(0, 0, -height * 0.32f), yard + new Vector3(0, 0, height * 0.32f), 0.07f, Timber);
                Spar(yard + new Vector3(0, -0.12f, -height * 0.28f), yard + new Vector3(0, -0.12f, height * 0.28f), 0.11f, Canvas.Darkened(0.35f));
                Spar(at + new Vector3(x, 1.8f + height, 0), at + new Vector3(x + 2.5f, 2.2f, 2.6f), 0.015f, Rope);
                Spar(at + new Vector3(x, 1.8f + height, 0), at + new Vector3(x + 2.5f, 2.2f, -2.6f), 0.015f, Rope);
            }
        });
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

    // A post-and-rail fence with timber stacked against it in places: a yard boundary with a reason
    // (PORT_VISUAL_PASS §3.5). The gate is left open.
    private void Fence(float left, float right, float back, float front)
    {
        var vertical = right - left < front - back;
        var length = vertical ? front - back : right - left;
        for (var d = 0.5f; d < length; d += 1f)
        {
            var at = vertical ? new Vector3((left + right) / 2f, 0, back + d) : new Vector3(left + d, 0, (back + front) / 2f);
            if (_location.IsWalkable(new GridPosition(Mathf.FloorToInt(at.X + 0.5f), Mathf.FloorToInt(at.Z + 0.5f))))
            {
                continue;
            }

            AddBox(at + new Vector3(0, 0.62f, 0), new Vector3(0.11f, 1.24f, 0.11f), WetWood);
            foreach (var y in new[] { 0.45f, 1.05f })
            {
                AddBox(at + new Vector3(0, y, 0), vertical ? new Vector3(0.05f, 0.09f, 1.02f) : new Vector3(1.02f, 0.09f, 0.05f), Wood.Darkened(0.08f));
            }

            // Every few posts, planks or poles leaned against the rails, as a yard keeps its timber.
            var seed = (int)(at.X * 3 + at.Z * 5);
            if (seed % 4 == 0)
            {
                for (var i = 0; i < 3; i++)
                {
                    var lean = vertical ? new Vector3(0, 0, 0) : new Vector3(0, 0, 0);
                    var offset = vertical ? new Vector3(-0.22f - i * 0.05f, 0.7f, 0.2f * i - 0.2f) : new Vector3(0.2f * i - 0.2f, 0.7f, 0.22f + i * 0.05f);
                    AddBox(at + offset, new Vector3(vertical ? 0.06f : 0.18f, 1.45f, vertical ? 0.18f : 0.06f), Wood.Lightened(0.05f * i),
                        rotation: vertical ? new Vector3(0, 0, -14) : new Vector3(14, 0, 0));
                }
            }
        }
    }

    // A small hull on the stocks: keel, ribs, part-planked, pointing down the slip to the water.
    // A small hull on the stocks, pointing down the slip to the water: keel, stem and sternpost, a row of
    // frames (a floor timber with futtocks rising from it), and the first strakes of planking.
    private void HullOnStocks(float left, float right, float back, float front)
    {
        var x = (left + right) / 2f;
        var length = front - back;
        AddBox(new Vector3(x, 0.15f, (back + front) / 2f + 1.5f), new Vector3(1.2f, 0.3f, length + 3f), Wood.Darkened(0.25f));
        AddBox(new Vector3(x, 0.5f, (back + front) / 2f), new Vector3(0.22f, 0.35f, length), Timber);
        AddBox(new Vector3(x, 1.6f, front - 0.3f), new Vector3(0.22f, 2.4f, 0.22f), Timber, rotation: new Vector3(-18, 0, 0));
        AddBox(new Vector3(x, 1.5f, back + 0.3f), new Vector3(0.22f, 2.2f, 0.22f), Timber, rotation: new Vector3(8, 0, 0));
        const int frames = 8;
        for (var i = 0; i < frames; i++)
        {
            var z = back + 1.2f + i * (length - 2.4f) / (frames - 1);
            var fullness = Mathf.Sin((i + 0.6f) / frames * Mathf.Pi);
            var half = 0.7f + fullness * 1.3f;
            AddBox(new Vector3(x, 0.75f, z), new Vector3(half * 2f, 0.16f, 0.14f), Wood);
            foreach (var side in new[] { -1f, 1f })
            {
                AddBox(new Vector3(x + side * (half + 0.18f), 1.55f, z), new Vector3(0.14f, 1.7f, 0.14f), Wood, rotation: new Vector3(0, 0, -side * 12f));
            }
        }

        // Two strakes of planking along each side, and a ribband holding the frames' heads.
        foreach (var side in new[] { -1f, 1f })
        {
            for (var row = 0; row < 2; row++)
            {
                AddBox(new Vector3(x + side * (1.5f + row * 0.12f), 0.95f + row * 0.32f, (back + front) / 2f), new Vector3(0.06f, 0.3f, length - 2.6f - row * 0.6f), Wood.Lightened(0.1f), rotation: new Vector3(0, 0, -side * 12f));
            }

            AddBox(new Vector3(x + side * 2.1f, 2.35f, (back + front) / 2f), new Vector3(0.08f, 0.1f, length - 2.2f), Wood.Darkened(0.1f));
            foreach (var z in new[] { back + 2f, front - 2.5f })
            {
                AddBox(new Vector3(x + side * 2.7f, 1.0f, z), new Vector3(0.14f, 2.2f, 0.14f), Wood.Darkened(0.15f), rotation: new Vector3(0, 0, side * 28f));
            }
        }
    }

    // A market stall: a trestle board under an undyed canvas awning that sags and is patched, goods that
    // say what is sold, and the baskets and crates around it (PORT_VISUAL_PASS §3.10).
    // A cloth thrown over the stall's goods, lumpy over what is under it and hanging over the front:
    // only shown in rain (PORT_EXPERIENCE §14 "vendors cover goods").
    private void RainCover(Vector3 at, float width)
    {
        var batch = _batch;
        _batch = _covers;
        var cloth = SceneryMaterial.Cloth(new Color(0.4f, 0.36f, 0.3f));
        AddBox(at + new Vector3(0, 1.02f, 0), new Vector3(width + 0.15f, 0.06f, 0.95f), cloth);
        for (var i = 0; i < 3; i++)
        {
            AddShape(new SphereMesh { Radius = 0.28f, Height = 0.22f, RadialSegments = 8, Rings = 3 }, at + new Vector3(-width / 2f + 0.45f + i * width / 3f, 1.06f, 0), cloth);
        }

        AddBox(at + new Vector3(0, 0.82f, 0.5f), new Vector3(width + 0.15f, 0.42f, 0.03f), cloth.Darkened(0.08f));
        _batch = batch;
    }

    private void Stall(Vector3 at, float width, string style)
    {
        width = Mathf.Max(width, 2f);
        var seed = (int)(at.X * 7 + at.Z * 13);
        // Dyed awnings the sun has faded unevenly: madder red, saffron, woad; the fish stalls salt-greyed.
        Color[] dyed = [new Color(0.7f, 0.2f, 0.14f), new Color(0.82f, 0.6f, 0.22f), new Color(0.3f, 0.42f, 0.55f), new Color(0.62f, 0.26f, 0.18f), Canvas.Darkened(0.06f)];
        var canvas = style switch
        {
            "produce" => new Color(0.7f, 0.22f, 0.15f),
            "fish" => new Color(0.66f, 0.68f, 0.66f),
            _ => dyed[Math.Abs(seed) % dyed.Length],
        };
        Trestle(at, width, 0.9f);
        RainCover(at, width);
        foreach (var (x, z, h) in new[] { (-width / 2f, -0.5f, 2.3f), (width / 2f, -0.5f, 2.3f), (-width / 2f, 0.7f, 1.9f), (width / 2f, 0.7f, 1.9f) })
        {
            AddBox(at + new Vector3(x, h / 2f, z), new Vector3(0.07f, h, 0.07f), Timber);
        }

        // The awning in two slopes meeting low in the middle: canvas that sags between its poles.
        var back = at + new Vector3(0, 2.3f, -0.5f);
        var mid = at + new Vector3(0, 2.0f, 0.12f);
        var fore = at + new Vector3(0, 1.9f, 0.75f);
        Awning(back, mid, width, canvas);
        Awning(mid, fore, width, canvas.Darkened(0.06f));
        AddBox(at + new Vector3(width * 0.2f, 2.09f, -0.12f), new Vector3(0.4f, 0.015f, 0.32f), canvas.Darkened(0.18f), rotation: new Vector3(-22, 0, 0));

        switch (style)
        {
            case "fish":
                for (var i = 0; i < 3; i++)
                {
                    var basket = at + new Vector3(-width / 2f + 0.4f + i * 0.6f, 0.8f, 0);
                    AddCylinder(basket, 0.26f, 0.1f, new Color(0.62f, 0.52f, 0.32f));
                    for (var f = 0; f < 3; f++)
                    {
                        AddShape(new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 6, Rings = 3 }, basket + new Vector3(-0.12f + f * 0.12f, 0.07f, (f % 2) * 0.08f), new Color(0.62f, 0.66f, 0.68f), new Vector3(0, 30 * f, 0));
                    }
                }

                AddBox(at + new Vector3(width / 2f - 0.35f, 0.79f, 0.1f), new Vector3(0.45f, 0.03f, 0.3f), Wood.Lightened(0.15f));
                AddCylinder(at + new Vector3(width / 2f + 0.3f, 0.2f, 0.5f), 0.18f, 0.4f, Wood.Darkened(0.2f));
                AddCylinder(at + new Vector3(-width / 2f - 0.35f, 0.18f, 0.3f), 0.3f, 0.36f, new Color(0.62f, 0.52f, 0.32f));
                break;
            case "produce":
                for (var i = 0; i < 3; i++)
                {
                    AddShape(new SphereMesh { Radius = 0.22f, Height = 0.4f, RadialSegments = 8, Rings = 4 }, at + new Vector3(-width / 2f + 0.4f + i * 0.55f, 0.95f, 0), new Color(0.7f, 0.6f, 0.42f));
                }

                // Strings of garlic and onions hung from the awning bar.
                foreach (var x in new[] { -0.5f, 0f, 0.5f })
                {
                    AddCylinder(at + new Vector3(x, 1.72f, 0.66f), 0.05f, 0.42f, x == 0f ? new Color(0.62f, 0.4f, 0.24f) : new Color(0.88f, 0.84f, 0.74f));
                }

                AddShape(new SphereMesh { Radius = 0.26f, Height = 0.5f, RadialSegments = 8, Rings = 4 }, at + new Vector3(width / 2f + 0.35f, 0.25f, 0.4f), new Color(0.68f, 0.58f, 0.4f));
                AddCylinder(at + new Vector3(-width / 2f - 0.3f, 0.16f, 0.4f), 0.28f, 0.32f, new Color(0.6f, 0.5f, 0.3f));
                break;
            case "cloth":
                for (var i = 0; i < 4; i++)
                {
                    var bolt = new[] { new Color(0.58f, 0.24f, 0.22f), new Color(0.3f, 0.34f, 0.46f), new Color(0.82f, 0.76f, 0.6f), new Color(0.42f, 0.4f, 0.28f) }[i];
                    AddBox(at + new Vector3(-width / 2f + 0.35f + i * 0.45f, 0.82f + (i % 2) * 0.08f, 0), new Vector3(0.36f, 0.14f, 0.55f), bolt);
                }

                AddBox(at + new Vector3(0, 0.92f, 0.35f), new Vector3(width * 0.8f, 0.02f, 0.03f), Wood.Lightened(0.2f));
                break;
            default:
                AddBox(at + new Vector3(0, 0.79f, 0), new Vector3(width * 0.8f, 0.03f, 0.6f), Wood.Darkened(0.2f));
                for (var i = 0; i < 5; i++)
                {
                    AddBox(at + new Vector3(-width / 2f + 0.4f + i * 0.33f, 0.83f, (i % 2) * 0.15f), new Vector3(0.22f, 0.04f, 0.08f), Iron, rotation: new Vector3(0, i * 25, 0));
                }

                AddCylinder(at + new Vector3(width / 2f + 0.3f, 0.2f, 0.4f), 0.2f, 0.4f, Wood);
                break;
        }
    }

    private void Awning(Vector3 from, Vector3 to, float width, Color colour)
    {
        var length = (to - from).Length();
        var tilt = Mathf.Atan2(to.Y - from.Y, to.Z - from.Z) * 180f / Mathf.Pi;
        AddBox((from + to) / 2f, new Vector3(width + 0.1f, 0.03f, length), SceneryMaterial.Cloth(colour), rotation: new Vector3(-tilt, 0, 0));
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
            case "jars":
                // Oil and wine in tall jars, some lying in straw.
                foreach (var (dx, dz, lying) in new[] { (-0.3f, -0.2f, false), (0.25f, -0.25f, false), (0f, 0.25f, false), (0.4f, 0.3f, true) })
                {
                    var jar = at + new Vector3(dx, lying ? 0.2f : 0.38f, dz);
                    AddShape(new SphereMesh { Radius = 0.2f, Height = 0.6f, RadialSegments = 10, Rings = 6 }, jar, new Color(0.66f, 0.42f, 0.28f), lying ? new Vector3(0, 0, 90) : null);
                    AddCylinder(jar + (lying ? new Vector3(0.32f, 0, 0) : new Vector3(0, 0.32f, 0)), 0.07f, 0.1f, new Color(0.6f, 0.38f, 0.25f), rotation: lying ? new Vector3(0, 0, 90) : null);
                }

                AddBox(at + new Vector3(0.1f, 0.04f, 0.05f), new Vector3(1.1f, 0.08f, 0.9f), new Color(0.78f, 0.68f, 0.4f));
                break;
            case "sacks":
                foreach (var (dx, dz, dy) in new[] { (-0.3f, -0.15f, 0f), (0.3f, -0.15f, 0f), (0f, 0.3f, 0f), (0f, 0f, 0.42f) })
                {
                    AddShape(new SphereMesh { Radius = 0.34f, Height = 0.5f, RadialSegments = 8, Rings = 4 }, at + new Vector3(dx, 0.25f + dy, dz), new Color(0.74f, 0.65f, 0.48f).Darkened(dy > 0 ? 0f : 0.06f));
                }

                break;
            case "baskets":
                // The night's catch in baskets, and a net heaped beside them.
                foreach (var (dx, dz) in new[] { (-0.3f, 0f), (0.3f, 0.1f), (0f, -0.35f) })
                {
                    AddCylinder(at + new Vector3(dx, 0.17f, dz), 0.26f, 0.34f, new Color(0.62f, 0.5f, 0.3f));
                    AddCylinder(at + new Vector3(dx, 0.33f, dz), 0.22f, 0.04f, new Color(0.62f, 0.66f, 0.68f));
                }

                AddShape(new SphereMesh { Radius = 0.4f, Height = 0.3f, RadialSegments = 8, Rings = 4 }, at + new Vector3(0.1f, 0.1f, 0.45f), new Color(0.4f, 0.36f, 0.28f));
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
                // Lashed for the lighter: rope bands round the stack.
                AddBox(at + new Vector3(-0.4f, 0.55f, -0.3f), new Vector3(0.92f, 0.04f, 0.92f), Rope);
                AddBox(at + new Vector3(-0.4f, 0.7f, -0.3f), new Vector3(0.04f, 1.4f, 0.92f), Rope);
                break;
        }
    }

    private void Cask(Vector3 at, bool lying)
    {
        var turn = lying ? new Vector3(0, 0, 90) : Vector3.Zero;
        AddCylinder(at, 0.28f, 0.8f, new Color(0.46f, 0.32f, 0.18f), rotation: turn);
        foreach (var offset in new[] { -0.25f, 0.25f })
        {
            AddCylinder(at + (lying ? new Vector3(offset, 0, 0) : new Vector3(0, offset, 0)), 0.3f, 0.05f, new Color(0.2f, 0.18f, 0.16f), rotation: turn);
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
        // Shafts resting on the ground, a chock under a wheel, a chain from the wheel to a ring in the quay.
        foreach (var z in new[] { -0.45f, 0.45f })
        {
            AddBox(at + new Vector3(1.9f, 0.3f, z), new Vector3(1.8f, 0.08f, 0.08f), Wood, rotation: new Vector3(0, 0, 15));
        }

        AddBox(at + new Vector3(-0.9f, 0.08f, 1.0f), new Vector3(0.25f, 0.16f, 0.2f), Wood.Darkened(0.2f));
        AddBox(at + new Vector3(-0.9f, 0.25f, 1.1f), new Vector3(0.04f, 0.4f, 0.04f), Iron, rotation: new Vector3(40, 0, 0));
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
            for (var x = -72f; x < 150f; x += 5.5f + rng.Randf() * 1.5f)
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

    }

    // Something to sit on under every seat out of doors: benches along the tavern's tables (a run of seats
    // facing the same way becomes one bench), and a crate for the porters resting by the pier head. Seats
    // in rooms have their own furniture; the convict's is the gaol cart.
    private void BuildSeats()
    {
        var seats = _location.Spots.Where(s => s.Kind == "SitPoint" && _location.RoomAt(s.Cell) is null && !s.Id.Contains("gaol", StringComparison.Ordinal)).ToList();
        foreach (var crate in seats.Where(s => s.Id.Contains("cargo_rest", StringComparison.Ordinal)))
        {
            var at = new Vector3(crate.Cell.X, HeightAt(crate.Cell.X, crate.Cell.Y), crate.Cell.Y - 0.12f);
            AddBox(at + new Vector3(0, 0.22f, 0), new Vector3(0.62f, 0.44f, 0.5f), Wood.Darkened(0.1f));
            AddBox(at + new Vector3(0, 0.45f, 0), new Vector3(0.64f, 0.03f, 0.52f), Wood);
        }

        var benches = seats.Where(s => !s.Id.Contains("cargo_rest", StringComparison.Ordinal)).ToList();
        foreach (var row in benches.Where(s => s.Face is "N" or "S").GroupBy(s => (s.Cell.Y, s.Face)))
        {
            // Seats in a row two cells apart or closer share a bench.
            var xs = row.Select(s => s.Cell.X).Order().ToList();
            var start = xs[0];
            for (var i = 1; i <= xs.Count; i++)
            {
                if (i < xs.Count && xs[i] - xs[i - 1] <= 2)
                {
                    continue;
                }

                // The bench sits a little behind the seat, away from the table it faces.
                var behind = row.Key.Face == "S" ? -0.12f : 0.12f;
                Bench(start - 0.6f, xs[i - 1] + 0.6f, row.Key.Y + behind, alongX: true);
                if (i < xs.Count)
                {
                    start = xs[i];
                }
            }
        }

        foreach (var stool in benches.Where(s => s.Face is not ("N" or "S")))
        {
            var behind = stool.Face == "E" ? -0.12f : 0.12f;
            Bench(stool.Cell.Y - 0.35f, stool.Cell.Y + 0.35f, stool.Cell.X + behind, alongX: false);
        }
    }

    // A plank bench at seat height on two trestle legs, from <paramref name="from"/> to <paramref name="to"/>
    // along x (or z), at <paramref name="across"/> on the other axis.
    private void Bench(float from, float to, float across, bool alongX)
    {
        var middle = (from + to) / 2f;
        var length = to - from;
        Vector3 At(float along, float y) => alongX ? new Vector3(along, HeightAt(along, across) + y, across) : new Vector3(across, HeightAt(across, along) + y, along);
        var top = alongX ? new Vector3(length, 0.06f, 0.36f) : new Vector3(0.36f, 0.06f, length);
        AddBox(At(middle, 0.43f), top, Wood.Darkened(0.05f));
        foreach (var leg in new[] { from + 0.12f, to - 0.12f })
        {
            AddBox(At(leg, 0.2f), alongX ? new Vector3(0.08f, 0.4f, 0.3f) : new Vector3(0.3f, 0.4f, 0.08f), Wood.Darkened(0.25f));
        }
    }

    // ---- beyond the ends of the map: the old town to the west, the fishermen's shore to the east

    /// <summary>Walks the crowd takes beyond the ends of the map, where nobody else can go (PortCrowd).</summary>
    public List<Vector3[]> Lanes { get; } = [];

    /// <summary>People at something beyond the ends of the map: mending nets, talking at the fountain.</summary>
    public List<(Vector3 At, float Yaw, Pose Pose)> Idlers { get; } = [];

    // Not walkable and not simulated: the town goes on past where the player can walk, so the port does
    // not end in an empty plain at one end and open water at the other.
    private void BuildOutskirts()
    {
        const float Terrace = 2.6f;
        var rng = new RandomNumberGenerator { Seed = 1492 };
        Color[] walls = [Plaster, Ochre, Lime, new Color(0.8f, 0.72f, 0.62f), new Color(0.86f, 0.77f, 0.68f), new Color(0.74f, 0.66f, 0.55f)];
        Color Wall() => walls[rng.RandiRange(0, walls.Length - 1)];

        // West, on the terrace: a lane of houses running off into the old town, and a row facing the
        // harbour along the promenade, with a small plaza and its fountain where they meet the map.
        for (var x = -72f; x < -12.5f; x += 5f + rng.Randf() * 1.6f)
        {
            var width = Mathf.Min(4.4f + rng.Randf() * 1.6f, -12.5f - x);
            House(x, x + width, 0.5f, 6.8f, Wall(), Stone, 2 + rng.RandiRange(0, 1), 3f, rng.Randf() > 0.4f, Terrace);
            House(x, x + width, 11.6f, 18.6f, Wall(), Stone, 2 + rng.RandiRange(0, 1), 3f, rng.Randf() > 0.4f, Terrace);
            if (rng.Randf() > 0.6f)
            {
                AddChild(Smoke(new Vector3(x + width * 0.7f, Terrace + 9.8f, 3.5f), dark: false));
            }
        }

        // The old fish market on the plaza: an open arcade under a tiled roof.
        House(-12f, -3f, 0.5f, 6.8f, Stone.Lightened(0.1f), Stone, 2, 3.6f, frame: false, Terrace);
        for (var x = -10.5f; x < -3.5f; x += 2.4f)
        {
            Arch(new Vector3(x, Terrace, 6.82f), 1.7f, 2.9f);
        }

        House(-12f, -6f, 11.6f, 18.6f, Ochre, Stone, 3, 3f, frame: true, Terrace);
        Well(new Vector3(-3.5f, Terrace, 14.5f));
        Tree(new Vector3(-9f, Terrace, 21.5f), cypress: false);
        Tree(new Vector3(-2.5f, Terrace, 20.5f), cypress: false);
        Tree(new Vector3(-30f, Terrace, 21.8f), cypress: false);
        Tree(new Vector3(-48f, Terrace, 21.8f), cypress: false);
        AddBox(new Vector3(-6.5f, Terrace + 0.25f, 22.6f), new Vector3(3f, 0.5f, 0.6f), Stone);
        Lantern(new Vector3(-7f, Terrace + 2.4f, 19.6f), onPost: true);

        // Below the terrace: vaults in its wall, boats along the quay, and the sea tower that closes it.
        for (var x = -44f; x < -2f; x += 4.6f)
        {
            Arch(new Vector3(x, 0, 25.52f), 1.9f, 2.2f);
        }

        Cargo(new Vector3(-8f, 0, 28.5f), 2f, 1.4f, "casks");
        Cargo(new Vector3(-17f, 0, 27.5f), 2.4f, 1f, "bales");
        Cargo(new Vector3(-26f, 0, 28f), 2f, 2f, "crates");
        ShearLegs(new Vector3(-21f, 0, 33.6f), 6f);
        foreach (var (x, mast) in new[] { (-9f, false), (-15f, true), (-24f, false), (-31f, true), (-38f, false) })
        {
            Boat(new Vector3(x, SeaAndSky.WaterLevel + 0.45f, 37.2f), 4.5f + rng.Randf() * 1.5f, mast);
        }

        // Fish stores leaning on the terrace wall by the tower, nets and pots before them.
        Shed(-43.5f, -35f, 25.5f, 29.5f);
        Cargo(new Vector3(-12.5f, 0, 31.5f), 1.6f, 1.4f, "crates");
        Cargo(new Vector3(-33f, 0, 31f), 2f, 1.4f, "casks");
        NetFrame(new Vector3(-38f, 0, 31.5f), 3f);
        foreach (var x in new[] { -58f, -40f, -22f })
        {
            AddBox(new Vector3(x, Terrace + 0.25f, 23.6f), new Vector3(2.6f, 0.5f, 0.6f), Stone);
            Lantern(new Vector3(x + 3f, Terrace + 2.4f, 23.4f), onPost: true);
        }

        Tree(new Vector3(-62f, Terrace, 21.8f), cypress: false);
        Tree(new Vector3(-16f, Terrace, 22.2f), cypress: false);
        Idlers.Add((new Vector3(-40f, Terrace + 0.3f, 23.2f), 0f, Pose.Sitting));
        Idlers.Add((new Vector3(-37.6f, 0, 30.6f), Mathf.Pi, Pose.Praying));
        Idlers.Add((new Vector3(-30f, 0, 33.8f), 0f, Pose.Leaning));
        Idlers.Add((new Vector3(-25f, Terrace, 23.9f), 0f, Pose.Leaning));

        AddCylinder(new Vector3(-48f, 3.5f, 30f), 4f, 11f, DarkStone.Lightened(0.08f));
        for (var a = 0; a < 12; a++)
        {
            var angle = a * Mathf.Tau / 12f;
            AddBox(new Vector3(-48f + Mathf.Cos(angle) * 3.8f, 9.4f, 30f + Mathf.Sin(angle) * 3.8f), new Vector3(1f, 0.8f, 1f), DarkStone.Lightened(0.08f));
        }

        Arch(new Vector3(-48f, 0, 34.02f), 1.5f, 2.4f);
        Banner(new Vector3(-48f, 12f, 30f), new Color(0.62f, 0.13f, 0.12f), new Color(0.85f, 0.7f, 0.25f));
        AddBox(Box(-52f, -44f, 33f, 60f, -2.8f, 0.6f), DarkStone);
        AddBox(Box(-52f, -44f, 33f, 60f, 0.6f, 0.9f), Stone);

        // East of the yard: a warehouse and a boat shed, and a slip with a boat on it.
        House(80.5f, 91f, 19f, 25f, new Color(0.7f, 0.62f, 0.5f), Stone, 2, 3.4f, frame: true, 0f);
        AddBox(new Vector3(85.8f, 1.5f, 25.06f), new Vector3(2.6f, 3f, 0.08f), Timber);
        Shed(92f, 100f, 16f, 25f);
        Boat(new Vector3(96f, 0.45f, 20.5f), 6.5f, false);
        AddBox(Box(84f, 91f, 26f, 40f, -1.6f, -0.05f), WetWood.Lightened(0.1f));
        Boat(new Vector3(87.5f, 0.4f, 29.5f), 5.5f, false);
        Cargo(new Vector3(82.5f, 0, 28.5f), 1.6f, 1.6f, "crates");
        Cargo(new Vector3(95f, 0, 30f), 2f, 1.4f, "casks");

        // Past the end of the quay, a shingle beach going down into the water, the fishermen's huts
        // above it, their boats drawn up and their nets on frames, and a headland with a watchtower.
        float Shore(float z) => 0.15f - Mathf.Max(0f, z - 6f) * 0.06f;
        // Grey enough to take the scenery's stone grain; a darker wet band where the sea reaches.
        var shingle = new Color(0.58f, 0.55f, 0.49f);
        _batch.AddTriangles(_batch.Coloured,
            [new Vector3(100f, Shore(6f), 6f), new Vector3(175f, Shore(6f), 6f), new Vector3(175f, Shore(52f), 52f),
             new Vector3(100f, Shore(6f), 6f), new Vector3(175f, Shore(52f), 52f), new Vector3(100f, Shore(52f), 52f)], colour: shingle);
        _batch.AddTriangles(_batch.Coloured,
            [new Vector3(100f, Shore(24f) + 0.02f, 24f), new Vector3(175f, Shore(24f) + 0.02f, 24f), new Vector3(175f, Shore(32f) + 0.02f, 32f),
             new Vector3(100f, Shore(24f) + 0.02f, 24f), new Vector3(175f, Shore(32f) + 0.02f, 32f), new Vector3(100f, Shore(32f) + 0.02f, 32f)], colour: new Color(0.4f, 0.38f, 0.34f));
        for (var i = 0; i < 40; i++)
        {
            var z = 7f + rng.Randf() * 22f;
            AddBox(new Vector3(101f + rng.Randf() * 44f, Shore(z) + 0.06f, z), new Vector3(0.3f + rng.Randf() * 0.6f, 0.2f, 0.3f + rng.Randf() * 0.5f),
                DarkStone.Lightened(rng.Randf() * 0.15f), rotation: new Vector3(0, rng.Randf() * 90f, 0));
        }

        AddBox(Box(100f, 175f, -40f, 6.2f, -0.6f, 0.15f), new Color(0.5f, 0.49f, 0.4f));
        AddBox(Box(99.8f, 100.4f, 6f, 34.9f, -2.8f, 0.12f), DarkStone);
        AddBox(new Vector3(102.2f, -0.12f, 10f), new Vector3(3.6f, 0.35f, 4f), Stone, rotation: new Vector3(-8f, 0, 0));
        for (var x = 104f; x < 140f; x += 6.5f + rng.Randf() * 2f)
        {
            House(x, x + 4f, -2f, 3.5f, Wall(), Stone, 1, 2.8f, frame: false, 0.15f);
            if (rng.Randf() > 0.4f)
            {
                AddChild(Smoke(new Vector3(x + 3f, 5.2f, 0.7f), dark: false));
            }
        }

        foreach (var (x, z, turn) in new[] { (106f, 17f, 80f), (112f, 19f, 95f), (119f, 16.5f, 70f), (127f, 20f, 100f), (134f, 18f, 85f) })
        {
            BeachedBoat(new Vector3(x, Shore(z) + 0.3f, z), 4.5f, turn);
        }

        NetFrame(new Vector3(109f, Shore(10f), 10f), 3f);
        NetFrame(new Vector3(123f, Shore(10.5f), 10.5f), 3.4f);
        AddCylinder(new Vector3(116f, Shore(12f) + 0.45f, 12f), 0.35f, 0.9f, Wood);
        AddBox(new Vector3(116f, Shore(12f) + 0.75f, 12f), new Vector3(2.2f, 0.08f, 0.08f), Timber);

        for (var i = 0; i < 9; i++)
        {
            var at = new Vector3(146f + rng.Randf() * 22f, -1f, 4f + i * 4.5f + rng.Randf() * 2f);
            var height = 4f + (8 - i) * 1.1f + rng.Randf() * 2f;
            AddBox(at + new Vector3(0, height / 2f, 0), new Vector3(7f + rng.Randf() * 5f, height, 6f + rng.Randf() * 3f),
                DarkStone.Lightened(rng.Randf() * 0.1f), rotation: new Vector3(rng.Randf() * 10f - 5f, rng.Randf() * 60f, rng.Randf() * 10f - 5f));
        }

        AddCylinder(new Vector3(156f, 15.5f, 10f), 2.2f, 7f, Stone.Lightened(0.05f));
        AddCylinder(new Vector3(156f, 19.3f, 10f), 2.6f, 0.6f, Stone);
        Tree(new Vector3(142f, 0.15f, -3f), cypress: true);
        Tree(new Vector3(138f, 0.15f, 2f), cypress: false);
        Tree(new Vector3(102f, 0.15f, -6f), cypress: true);

        // Where the crowd walks out there, and who is at work.
        Lanes.Add([new Vector3(-62f, Terrace, 9.2f), new Vector3(-1f, Terrace, 9.2f)]);
        Lanes.Add([new Vector3(-66f, Terrace, 21f), new Vector3(-13f, Terrace, 21f), new Vector3(-5f, Terrace, 17.5f), new Vector3(-1f, Terrace, 17.5f)]);
        Lanes.Add([new Vector3(-43f, 0, 30.5f), new Vector3(-1f, 0, 30.5f)]);
        Lanes.Add([new Vector3(80f, 0, 31.5f), new Vector3(99f, 0, 31.5f), new Vector3(101f, 0.15f, 9.5f), new Vector3(140f, 0.15f, 5.5f)]);
        Lanes.Add([new Vector3(80.5f, 0, 14f), new Vector3(101f, 0.15f, 5f), new Vector3(141f, 0.15f, 4.5f)]);
        Idlers.Add((new Vector3(108.5f, Shore(11.2f), 11.2f), Mathf.Pi, Pose.Praying));
        Idlers.Add((new Vector3(122.5f, Shore(11.7f), 11.7f), Mathf.Pi, Pose.Praying));
        Idlers.Add((new Vector3(113f, Shore(17.5f), 17.5f), -1.2f, Pose.Working));
        Idlers.Add((new Vector3(-4.8f, Terrace, 15.6f), 0.6f, Pose.Talking));
        Idlers.Add((new Vector3(-3.2f, Terrace, 16.2f), -2.6f, Pose.Talking));
        Idlers.Add((new Vector3(-6.5f, Terrace + 0.3f, 22.2f), 0f, Pose.Sitting));
        Idlers.Add((new Vector3(-16.5f, 0, 28.6f), Mathf.Pi * 0.5f, Pose.Working));
        Idlers.Add((new Vector3(88f, 0, 27.5f), 0f, Pose.Hammering));
    }

    // An open shed for a boat: a tiled roof on posts, closed at the back.
    private void Shed(float left, float right, float back, float front)
    {
        for (var x = left + 0.2f; x <= right; x += 2.6f)
        {
            AddBox(new Vector3(x, 1.6f, front - 0.2f), new Vector3(0.18f, 3.2f, 0.18f), Timber);
        }

        AddBox(Box(left, right, back, back + 0.3f, 0, 3.2f), Wood.Darkened(0.2f));
        Gable(left, 3.2f, 5.2f, back, front, (back + front) / 2f, Wood.Darkened(0.2f));
        Gable(right, 3.2f, 5.2f, back, front, (back + front) / 2f, Wood.Darkened(0.2f));
        Roof(new Vector3(left - 0.3f, 3.2f, front + 0.4f), new Vector3(right + 0.3f, 3.2f, front + 0.4f), 5.2f, (back + front) / 2f);
        Roof(new Vector3(right + 0.3f, 3.2f, back - 0.4f), new Vector3(left - 0.3f, 3.2f, back - 0.4f), 5.2f, (back + front) / 2f);
    }

    // A boat drawn up on the shingle, heeled a little, bow up the beach: a hull with its sides and
    // thwarts, a pointed stem, and the oars laid across it.
    private void BeachedBoat(Vector3 at, float length, float turn)
    {
        var hull = new Color(0.32f, 0.26f, 0.2f);
        var angles = new Vector3(0, turn, 8f);
        var basis = Basis.FromEuler(angles * Mathf.Pi / 180f);
        void Piece(Vector3 offset, Vector3 size, Color colour, float yaw = 0f) =>
            AddBox(at + basis * offset, size, colour, rotation: angles + new Vector3(0, yaw, 0));
        Piece(new Vector3(0, -0.15f, 0), new Vector3(length * 0.8f, 0.3f, 1.1f), hull);
        Piece(new Vector3(0, 0.12f, 0.62f), new Vector3(length * 0.8f, 0.5f, 0.1f), hull);
        Piece(new Vector3(0, 0.12f, -0.62f), new Vector3(length * 0.8f, 0.5f, 0.1f), hull);
        Piece(new Vector3(0, 0.37f, 0.66f), new Vector3(length * 0.8f, 0.08f, 0.06f), new Color(0.55f, 0.22f, 0.16f));
        Piece(new Vector3(-length * 0.4f, 0.12f, 0), new Vector3(0.12f, 0.6f, 1.3f), hull);
        Piece(new Vector3(length * 0.45f, 0.1f, 0.28f), new Vector3(0.95f, 0.55f, 0.1f), hull, -30f);
        Piece(new Vector3(length * 0.45f, 0.1f, -0.28f), new Vector3(0.95f, 0.55f, 0.1f), hull, 30f);
        Piece(new Vector3(0, 0.05f, 0), new Vector3(length * 0.78f, 0.04f, 1.12f), Shadow);
        for (var x = -0.25f; x < 0.3f; x += 0.5f)
        {
            Piece(new Vector3(x * length, 0.2f, 0), new Vector3(0.22f, 0.06f, 1.2f), Wood);
        }

        Piece(new Vector3(0.1f, 0.3f, 0.2f), new Vector3(length * 0.9f, 0.05f, 0.08f), Wood.Lightened(0.1f), 4f);
    }

    // A date palm: a ringed trunk leaning a little and curving back, and a head of drooping fronds.
    private void Palm(Vector3 at, int variant)
    {
        var lean = new Vector3(0.12f + variant * 0.03f, 0, variant % 2 == 0 ? 0.08f : -0.06f);
        var top = at;
        const int Segments = 7;
        for (var i = 0; i < Segments; i++)
        {
            var t = i / (float)Segments;
            var next = at + new Vector3(0, (i + 1) * 0.9f, 0) + lean * (i + 1) * (1.2f - t);
            var middle = (top + next) / 2f;
            var up = (next - top).Normalized();
            var basis = new Basis(new Quaternion(Vector3.Up, up));
            _batch.AddColoured(new CylinderMesh { TopRadius = 0.15f - t * 0.04f, BottomRadius = 0.18f - t * 0.04f, Height = 0.92f, RadialSegments = 7, Rings = 1 },
                new Transform3D(basis, middle), new Color(0.45f, 0.36f, 0.26f).Darkened(i % 2 * 0.08f));
            top = next;
        }

        // Each frond in two lengths, the outer narrower and hanging lower, so the head arches.
        var green = new Color(0.32f, 0.42f, 0.2f);
        for (var i = 0; i < 14; i++)
        {
            var yaw = i * Mathf.Tau / 14f + variant;
            var rise = i % 2 == 0 ? -12f : 8f;
            var inner = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, Mathf.DegToRad(rise));
            var mid = top + inner * new Vector3(0, 0, 0.9f);
            var outer = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, Mathf.DegToRad(rise + 38f + (i % 3) * 10f));
            var tip = mid + outer * new Vector3(0, 0, 1.0f);
            var colour = green.Lightened((i % 4) * 0.05f).Darkened(i % 2 == 0 ? 0.08f : 0f);
            _batch.AddColoured(new BoxMesh { Size = new Vector3(0.42f, 0.025f, 0.95f) }, new Transform3D(inner, (top + mid) / 2f), colour);
            _batch.AddColoured(new BoxMesh { Size = new Vector3(0.26f, 0.025f, 1.05f) }, new Transform3D(outer, (mid + tip) / 2f), colour.Darkened(0.06f));
        }

        AddShape(new SphereMesh { Radius = 0.22f, Height = 0.4f, RadialSegments = 8, Rings = 4 }, top + new Vector3(0, -0.15f, 0), new Color(0.55f, 0.38f, 0.16f));
    }

    // An orange tree on the plaza, or a dark cypress on the hill.
    private void Tree(Vector3 at, bool cypress)
    {
        AddCylinder(at + new Vector3(0, 1f, 0), 0.16f, 2f, Timber);
        if (cypress)
        {
            AddShape(new CylinderMesh { TopRadius = 0.05f, BottomRadius = 1f, Height = 7f, RadialSegments = 8, Rings = 1 }, at + new Vector3(0, 4.6f, 0), new Color(0.16f, 0.24f, 0.15f));
            return;
        }

        AddShape(new SphereMesh { Radius = 1.6f, Height = 2.6f, RadialSegments = 10, Rings = 5 }, at + new Vector3(0, 2.9f, 0), new Color(0.22f, 0.34f, 0.18f));
        AddShape(new SphereMesh { Radius = 1.1f, Height = 1.8f, RadialSegments = 8, Rings = 4 }, at + new Vector3(0.7f, 3.5f, 0.4f), new Color(0.26f, 0.38f, 0.2f));
    }

    private void BuildLights()
    {
        foreach (var x in new[] { 12f, 29f, 47f, 60f })
        {
            Lantern(new Vector3(x, 2.4f, 34f), onPost: true);
        }

        Lantern(new Vector3(9.6f, 5f, 24f), onPost: true);

        // The shear-legs that swing loads down into the lighter at the east pier.
        ShearLegs(new Vector3(43.5f, 0, 33.6f), 6.5f);
        MooringAndGangway();
    }

    // What ties the Garza to La Bocana: bow, stern and spring lines to the bollards, a gangway with
    // man-ropes, fenders, and her ensign at the stern (PORT_VISUAL_PASS §3.11).
    private void MooringAndGangway()
    {
        // In their own nodes, so the departure can take in the gangway and cast off the lines (R2e).
        var scenery = _batch;
        _batch = new MeshBatcher();
        Lines = new Node3D { Name = "MooringLines" };
        AddChild(Lines);
        var berth = Berth;
        var side = berth.Z - 4.3f;
        foreach (var (bollard, onShip) in new[] { (19f, berth.X - 10.5f), (27f, berth.X - 2.5f), (35f, berth.X + 2.5f), (41f, berth.X + 10f) })
        {
            var post = new Vector3(bollard, 0.6f, 34f);
            var cleat = new Vector3(onShip, 0.85f, side + 0.1f);
            var sag = (post + cleat) / 2f + new Vector3(0, -0.35f, 0);
            Spar(post, sag, 0.025f, Rope);
            Spar(sag, cleat, 0.025f, Rope);
        }

        _batch.Commit(Lines);
        Gangway = new Node3D { Name = "Gangway" };
        AddChild(Gangway);

        // The gangway: a plank with cleats, rising from the quay to her rail, man-ropes either side.
        var foot = new Vector3(berth.X, 0.06f, 33.6f);
        var top = new Vector3(berth.X, 0.75f, side + 0.5f);
        var mid = (foot + top) / 2f;
        var tilt = Mathf.Atan2(top.Y - foot.Y, top.Z - foot.Z) * 180f / Mathf.Pi;
        AddBox(mid, new Vector3(0.75f, 0.07f, (top - foot).Length()), Wood, rotation: new Vector3(-tilt, 0, 0));
        for (var i = 1; i < 6; i++)
        {
            AddBox(foot.Lerp(top, i / 6f) + new Vector3(0, 0.05f, 0), new Vector3(0.7f, 0.03f, 0.05f), Wood.Darkened(0.2f));
        }

        foreach (var x in new[] { -0.4f, 0.4f })
        {
            AddBox(foot + new Vector3(x, 0.5f, 0), new Vector3(0.06f, 1f, 0.06f), Timber);
            Spar(foot + new Vector3(x, 1f, 0), top + new Vector3(x, 0.9f, 0), 0.015f, Rope);
        }

        _batch.Commit(Gangway);
        _batch = new MeshBatcher();

        // Fenders of old rope hanging between her side and the quay.
        foreach (var x in new[] { -7f, -1.5f, 4f, 8.5f })
        {
            AddCylinder(new Vector3(berth.X + x, -0.25f, side - 0.05f), 0.13f, 0.45f, Rope.Darkened(0.2f));
        }

        // Her ensign at the stern staff, lifting in the breeze: the Company's colours over the Crown's red.
        var staff = new Vector3(berth.X - 11.6f, 0.8f, berth.Z);
        Spar(staff, staff + new Vector3(0, 4.2f, 0), 0.04f, Timber);
        AddBox(staff + new Vector3(-0.75f, 3.7f, 0), new Vector3(1.4f, 0.9f, 0.03f), SceneryMaterial.Cloth(new Color(0.62f, 0.13f, 0.12f)), rotation: new Vector3(0, 12, 0));
        AddBox(staff + new Vector3(-0.75f, 3.7f, 0.02f), new Vector3(0.5f, 0.5f, 0.02f), SceneryMaterial.Cloth(new Color(0.85f, 0.7f, 0.25f)), rotation: new Vector3(0, 12, 0));
        // Fenders and ensign go with her lines: in when she casts off.
        _batch.Commit(Lines);
        _batch = scenery;
    }

    // ---- small parts

    private void Banner(Vector3 at, Color field, Color charge)
    {
        AddBox(at + new Vector3(0.55f, -0.7f, 0), new Vector3(1.0f, 1.3f, 0.03f), SceneryMaterial.Cloth(field));
        AddBox(at + new Vector3(0.55f, -0.7f, 0.03f), new Vector3(0.35f, 0.35f, 0.02f), SceneryMaterial.Cloth(charge));
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

        _batch.Add(new BoxMesh { Size = new Vector3(0.22f, 0.3f, 0.22f) }, new Transform3D(Basis.Identity, at), _glass);
        var light = new OmniLight3D { Position = at, LightColor = new Color(1f, 0.68f, 0.36f), OmniRange = 10f, OmniAttenuation = 1.6f };
        AddChild(light);
        _lanterns.Add(light);
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

    private void AddBox(Aabb box, Color colour) => AddBox(box.GetCenter(), box.Size, colour);

    // Static scenery goes into the batcher; only glowing parts (forge, embers) stay separate nodes.
    private void AddBox(Vector3 position, Vector3 size, Color colour, bool emissive = false, Vector3? rotation = null)
    {
        var transform = new Transform3D(Basis.FromEuler((rotation ?? Vector3.Zero) * Mathf.Pi / 180f), position);
        if (emissive)
        {
            var glow = new StandardMaterial3D { AlbedoColor = colour, EmissionEnabled = true, Emission = colour, EmissionEnergyMultiplier = 1.5f };
            AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = glow, Transform = transform });
            return;
        }

        _batch.AddColoured(new BoxMesh { Size = size }, transform, colour);
    }

    private void AddShape(Mesh mesh, Vector3 position, Color colour, Vector3? rotation = null) =>
        _batch.AddColoured(mesh, new Transform3D(Basis.FromEuler((rotation ?? Vector3.Zero) * Mathf.Pi / 180f), position), colour);

    private void AddCylinder(Vector3 position, float radius, float height, Color colour, bool rotateX = false, Vector3? rotation = null)
    {
        var turn = rotation ?? (rotateX ? new Vector3(90, 0, 0) : Vector3.Zero);
        var mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height, RadialSegments = radius > 0.5f ? 16 : 10, Rings = 1 };
        _batch.AddColoured(mesh, new Transform3D(Basis.FromEuler(turn * Mathf.Pi / 180f), position), colour);
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
