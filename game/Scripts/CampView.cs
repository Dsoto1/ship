using Expedition.Simulation.Shore;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// The beach of the first camp (R6, docs/R6_LANDFALL_CAMP.md): sand running up from the surf to the forest,
/// driftwood and rocks, and the camp's works as they are made: sail or brush shelters in a row, the fire
/// and its light, the stores stacked and covered, the latrine, the sick shelter, the water casks, the boat
/// drawn up at the landing. Built from <c>location.shore_camp</c>; placed in the world with row 0 at the
/// water's edge facing the ship's anchorage.
/// </summary>
public partial class CampView : Node3D
{
    private static readonly Color Sand = new(0.84f, 0.76f, 0.58f);
    private static readonly Color WetSand = new(0.62f, 0.55f, 0.42f);
    private static readonly Color Earth = new(0.36f, 0.33f, 0.2f);
    private static readonly Color Canvas = new(0.86f, 0.82f, 0.7f);
    private static readonly Color Brush = new(0.42f, 0.4f, 0.22f);
    private static readonly Color Pole = new(0.45f, 0.33f, 0.2f);
    private static readonly Color Stone = new(0.5f, 0.48f, 0.44f);
    private static readonly Color Leaf = new(0.2f, 0.36f, 0.16f);
    private static readonly Color DarkLeaf = new(0.13f, 0.26f, 0.12f);
    private static readonly Color Bark = new(0.33f, 0.25f, 0.17f);
    private static readonly Color Cask = new(0.55f, 0.4f, 0.24f);
    private static readonly Color Sack = new(0.74f, 0.66f, 0.5f);
    private static readonly Color Hull = new(0.4f, 0.27f, 0.16f);

    private readonly Dictionary<Color, StandardMaterial3D> _plain = [];
    private LocationDefinition _location = null!;
    private Node3D _works = null!;
    private OmniLight3D _fireLight = null!;
    private Node3D _flames = null!;
    private Node3D _boat = null!;
    private string _shown = "";
    private readonly List<EdgeTree> _edgeTrees = [];

    // A tree of the forest's edge, and what stands in its place as it is felled and grows back.
    private sealed record EdgeTree(Node3D Tree, MeshInstance3D Stump, MeshInstance3D Bush, Node3D Sapling, int X, int Y);
    private Node3D _ghost = null!;
    private string _ghostShown = "";
    private Node3D _settlement = null!;
    private Node3D _wet = null!;
    private StandardMaterial3D _groundMaterial = null!;
    private string _settled = "";
    private long _settledAt = -1;

    /// <summary>The ground's height at a cell position, metres (the beach rises inland).</summary>
    public float HeightAt(float x, float z)
    {
        foreach (var area in _location.Ground)
        {
            if (area.HeightAt(x, z) is { } height)
            {
                return (float)height + Lift;
            }
        }

        return z < 0 ? Lift - 0.25f * Mathf.Min(-z, 6f) : Lift;
    }

    // The whole camp stands this far above the sea's surface, so the surf breaks on the sand's foot.
    private const float Lift = 0.3f;

    /// <summary>The camp's centre, in its own frame, for the camera.</summary>
    public Vector3 Centre => new(_location.Width / 2f, Lift, 9f);

    /// <summary>Builds the beach, the forest's edge and the camp's places.</summary>
    public void Build(LocationDefinition location)
    {
        // Everything but the people, who are kept under the camp by Main.
        foreach (var child in GetChildren().Where(c => c.Name != "CampFigures"))
        {
            child.QueueFree();
        }

        _location = location;
        BuildGround();
        BuildForest();
        _works = new Node3D { Name = "Works" };
        AddChild(_works);
        _settlement = new Node3D { Name = "Settlement" };
        AddChild(_settlement);
        _wet = new Node3D { Name = "Wet", Visible = false };
        AddChild(_wet);
        _ghost = new Node3D { Name = "Ghost", Visible = false };
        AddChild(_ghost);
        _ghostShown = "";
        BuildPuddles();
        _flames = new Node3D { Name = "Flames", Visible = false };
        AddChild(_flames);
        _fireLight = new OmniLight3D { LightColor = new Color(1f, 0.66f, 0.35f), LightEnergy = 0f, OmniRange = 9f, ShadowEnabled = false };
        AddChild(_fireLight);
        if (location.FindSpot("spot.hearth") is { } hearth)
        {
            var at = Spot(hearth);
            _flames.Position = at + new Vector3(0, 0.05f, 0.6f);
            _fireLight.Position = at + new Vector3(0, 0.8f, 0.6f);
            for (var i = 0; i < 3; i++)
            {
                _flames.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.18f - i * 0.04f, Height = 0.55f - i * 0.1f, RadialSegments = 6 },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.55f + i * 0.15f, 0.2f), EmissionEnabled = true, Emission = new Color(1f, 0.5f + i * 0.15f, 0.15f), EmissionEnergyMultiplier = 2.5f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
                    Position = new Vector3((i - 1) * 0.08f, 0.25f, (i % 2) * 0.06f),
                });
            }
        }

        _boat = BuildBoat();
        AddChild(_boat);
        _shown = "";
    }

    // The point at a spot, on the ground.
    private Vector3 Spot(WorkSpotDefinition spot) => new(spot.X, HeightAt(spot.X, spot.Y), spot.Y);

    private StandardMaterial3D Plain(Color colour)
    {
        if (!_plain.TryGetValue(colour, out var material))
        {
            material = new StandardMaterial3D { AlbedoColor = colour, Roughness = 0.92f };
            _plain[colour] = material;
        }

        return material;
    }

    private MeshInstance3D Add(Node3D parent, Mesh mesh, Color colour, Vector3 at, Vector3? rotation = null)
    {
        var instance = new MeshInstance3D { Mesh = mesh, MaterialOverride = Plain(colour), Position = at, RotationDegrees = rotation ?? Vector3.Zero };
        parent.AddChild(instance);
        return instance;
    }

    // Sand from under the surf up to the forest, wet near the water and trodden earth among the trees,
    // reaching well past the grid on every side so the camp never floats.
    private void BuildGround()
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        const float step = 1f;
        for (var z = -8f; z < _location.Height + 40; z += step)
        {
            for (var x = -60f; x < _location.Width + 60; x += step)
            {
                var corners = new[] { new Vector2(x, z), new Vector2(x + step, z), new Vector2(x + step, z + step), new Vector2(x, z + step) };
                var points = corners.Select(c => new Vector3(c.X, HeightAt(Mathf.Clamp(c.X, 1, _location.Width - 2), c.Y), c.Y)).ToArray();
                var colour = z < 1.5f ? WetSand : z < 16 ? Sand : Sand.Lerp(Earth, Mathf.Clamp((z - 16) / 5, 0, 1));
                // A little mottling, so the sand is not a flat sheet.
                var n = Mathf.Sin(x * 1.7f + z * 0.9f) * Mathf.Cos(x * 0.6f - z * 1.3f);
                colour = colour.Darkened(0.04f + 0.04f * n);
                tool.SetColor(colour);
                tool.SetNormal(Vector3.Up);
                foreach (var i in new[] { 0, 1, 2, 0, 2, 3 })
                {
                    tool.AddVertex(points[i]);
                }
            }
        }

        tool.GenerateNormals();
        _groundMaterial = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.97f };
        AddChild(new MeshInstance3D { Name = "Ground", Mesh = tool.Commit(), MaterialOverride = _groundMaterial });

        // Driftwood and rocks where the map has them.
        for (var y = 0; y < _location.Height; y++)
        {
            for (var x = 0; x < _location.Width; x++)
            {
                if (_location.Rows[y][x] == LocationDefinition.Fitting && y < 18)
                {
                    var at = new Vector3(x, HeightAt(x, y), y);
                    if ((x + y) % 2 == 0)
                    {
                        Add(this, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.16f, Height = 2.2f }, Pole.Lightened(0.25f), at + new Vector3(0, 0.12f, 0), new Vector3(0, 30 + x * 7, 90));
                    }
                    else
                    {
                        Add(this, new SphereMesh { Radius = 0.45f, Height = 0.6f }, Stone, at + new Vector3(0, 0.15f, 0));
                    }
                }
            }
        }
    }

    // The forest: the grid's trees, and more behind them and along both flanks.
    private void BuildForest()
    {
        var batch = new MeshBatcher();
        var rng = new RandomNumberGenerator { Seed = 1519 };
        void Tree(float x, float z, float scale)
        {
            var ground = HeightAt(Mathf.Clamp(x, 1, _location.Width - 2), z);
            var height = (3.2f + rng.Randf() * 2.5f) * scale;
            batch.AddColoured(new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.22f * scale, Height = height, RadialSegments = 6 },
                new Transform3D(Basis.Identity, new Vector3(x, ground + height / 2, z)), Bark);
            var crown = (1.4f + rng.Randf() * 0.9f) * scale;
            batch.AddColoured(new SphereMesh { Radius = crown, Height = crown * 1.5f, RadialSegments = 8, Rings = 4 },
                new Transform3D(Basis.Identity, new Vector3(x, ground + height + crown * 0.3f, z)), rng.Randf() < 0.5f ? Leaf : DarkLeaf);
        }

        // The forest's edge, tree by tree, so the ones felled for poles can be taken away (R7): a stump
        // left in each one's place, then scrub, brush and a sapling as it grows back.
        _edgeTrees.Clear();
        var edge = new List<(Vector3 At, float Height, float Crown, Color Leaves, int X, int Y)>();
        for (var y = 0; y < _location.Height; y++)
        {
            for (var x = 0; x < _location.Width; x++)
            {
                if (y >= 18 && _location.Rows[y][x] == LocationDefinition.Blocked && x > 0 && x < _location.Width - 1 && y < _location.Height - 1)
                {
                    var at = new Vector3(x + rng.RandfRange(-0.3f, 0.3f), 0, y + rng.RandfRange(-0.3f, 0.3f));
                    at.Y = HeightAt(Mathf.Clamp(at.X, 1, _location.Width - 2), at.Z);
                    edge.Add((at, 3.2f + rng.Randf() * 2.5f, 1.4f + rng.Randf() * 0.9f, rng.Randf() < 0.5f ? Leaf : DarkLeaf, x, y));
                }
            }
        }

        var middle = new Vector3(_location.Width / 2f, 0, 17);
        foreach (var (at, height, crown, leaves, cx, cy) in edge.OrderBy(e => (e.At - middle).LengthSquared()))
        {
            var tree = new Node3D { Position = at };
            AddChild(tree);
            Add(tree, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.22f, Height = height, RadialSegments = 6 }, Bark, new Vector3(0, height / 2, 0));
            Add(tree, new SphereMesh { Radius = crown, Height = crown * 1.5f, RadialSegments = 8, Rings = 4 }, leaves, new Vector3(0, height + crown * 0.3f, 0));
            var stump = Add(this, new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.25f, Height = 0.35f, RadialSegments = 6 }, Bark.Lightened(0.15f), at + new Vector3(0, 0.17f, 0));
            stump.Visible = false;
            var bush = Add(this, new SphereMesh { Radius = 0.55f, Height = 0.6f, RadialSegments = 8, Rings = 4 }, leaves.Lightened(0.12f), at + new Vector3(0.25f, 0.2f, 0.1f));
            bush.Visible = false;
            var sapling = new Node3D { Position = at, Visible = false };
            AddChild(sapling);
            Add(sapling, new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.07f, Height = height * 0.4f, RadialSegments = 5 }, Bark, new Vector3(0, height * 0.2f, 0));
            Add(sapling, new SphereMesh { Radius = crown * 0.45f, Height = crown * 0.7f, RadialSegments = 8, Rings = 4 }, leaves.Lightened(0.08f), new Vector3(0, height * 0.4f + crown * 0.15f, 0));
            _edgeTrees.Add(new EdgeTree(tree, stump, bush, sapling, cx, cy));
        }

        // Behind and on the flanks, the forest goes on.
        for (var z = _location.Height - 1f; z < _location.Height + 22; z += 1.6f)
        {
            for (var x = -40f; x < _location.Width + 40; x += 1.7f)
            {
                Tree(x + rng.RandfRange(-0.6f, 0.6f), z + rng.RandfRange(-0.5f, 0.5f), 1.15f);
            }
        }

        foreach (var side in new[] { -1, 1 })
        {
            for (var z = 8f; z < _location.Height; z += 1.8f)
            {
                for (var k = 0; k < 20; k++)
                {
                    var x = side < 0 ? -1.5f - k * 1.8f : _location.Width + 0.5f + k * 1.8f;
                    if (rng.Randf() < 0.75f + (z - 8) / 30)
                    {
                        Tree(x + rng.RandfRange(-0.5f, 0.5f), z + rng.RandfRange(-0.5f, 0.5f), 1f);
                    }
                }
            }
        }

        batch.Commit(this);
    }

    // The ship's boat, drawn up on the sand.
    private Node3D BuildBoat()
    {
        var boat = new Node3D { Name = "Boat" };
        Add(boat, new BoxMesh { Size = new Vector3(1.3f, 0.45f, 4.2f) }, Hull, new Vector3(0, 0.22f, 0));
        Add(boat, new PrismMesh { Size = new Vector3(1.3f, 0.9f, 0.45f) }, Hull, new Vector3(0, 0.22f, -2.3f), new Vector3(90, 0, 0));
        Add(boat, new BoxMesh { Size = new Vector3(1.1f, 0.05f, 3.8f) }, Hull.Lightened(0.2f), new Vector3(0, 0.42f, 0));
        foreach (var z in new[] { -0.8f, 0.4f, 1.4f })
        {
            Add(boat, new BoxMesh { Size = new Vector3(1.2f, 0.06f, 0.28f) }, Hull.Lightened(0.3f), new Vector3(0, 0.5f, z));
        }

        if (_location.FindSpot("spot.landing_1") is { } landing)
        {
            boat.Position = Spot(landing) + new Vector3(1f, -0.05f, 0.2f);
            boat.RotationDegrees = new Vector3(0, 8, 0);
        }

        return boat;
    }

    /// <summary>
    /// The camp as it stands: its works, the fire, the stores, and the boat (away while hands are landing
    /// stores, gone if there is none). Rebuilt only when something changes.
    /// </summary>
    public void Show(CampState camp, int storesAshore, bool fireBurning, bool landing, bool waterKnown, float darkness, float t)
    {
        var key = string.Join(",", camp.Works.Select(p => $"{p.Key}={p.Value}")) + $"|{storesAshore / 10}|{waterKnown}";
        if (key != _shown)
        {
            _shown = key;
            Rebuild(camp, storesAshore, waterKnown);
        }

        _flames.Visible = fireBurning;
        _fireLight.LightEnergy = fireBurning ? (0.8f + 1.6f * darkness) * (0.85f + 0.15f * Mathf.Sin(t * 13f) * Mathf.Sin(t * 7.3f)) : 0f;
        if (fireBurning)
        {
            _flames.Scale = new Vector3(1, 0.85f + 0.2f * Mathf.Sin(t * 11f), 1);
        }

        _boat.Visible = camp.Boat && !landing;
    }

    private void Rebuild(CampState camp, int storesAshore, bool waterKnown)
    {
        foreach (var child in _works.GetChildren())
        {
            child.QueueFree();
        }

        // The shelters in their row, sail ones first.
        var sail = (int)camp.Work(CampWorks.SailShelters);
        var brush = (int)camp.Work(CampWorks.BrushShelters);
        var places = new[] { "spot.shelter_a", "spot.shelter_b", "spot.shelter_c", "spot.shelter_d" }
            .Select(id => _location.FindSpot(id)).OfType<WorkSpotDefinition>().ToList();
        for (var i = 0; i < sail + brush; i++)
        {
            var spot = places[i % places.Count];
            var extra = i / places.Count;
            var at = Spot(spot) + new Vector3(0, 0, 2.2f + extra * 2.6f);
            if (i < sail)
            {
                SailShelter(at);
            }
            else
            {
                BrushShelter(at);
            }
        }

        if (camp.Work(CampWorks.SickShelter) >= 1 && _location.FindSpot("spot.sick") is { } sick)
        {
            var at = Spot(sick) + new Vector3(0, 0, 2.2f);
            SailShelter(at);
            // Pallets under it, and a pale cloth flown from the ridge pole for the sick.
            foreach (var dx in new[] { -0.45f, 0.45f })
            {
                Add(_works, new BoxMesh { Size = new Vector3(0.6f, 0.1f, 1.7f) }, Sack, at + new Vector3(dx, 0.05f, 0));
            }

            Add(_works, new BoxMesh { Size = new Vector3(0.02f, 0.3f, 0.45f) }, new Color(0.95f, 0.95f, 0.9f), at + new Vector3(0, 1.75f, -1.1f));
        }

        if (camp.Work(CampWorks.Fire) >= 1 && _location.FindSpot("spot.hearth") is { } hearth)
        {
            var at = Spot(hearth) + new Vector3(0, 0, 0.6f);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Mathf.Tau / 8;
                Add(_works, new SphereMesh { Radius = 0.16f, Height = 0.22f }, Stone, at + new Vector3(Mathf.Cos(angle) * 0.45f, 0.06f, Mathf.Sin(angle) * 0.45f));
            }

            Add(_works, new CylinderMesh { TopRadius = 0.4f, BottomRadius = 0.4f, Height = 0.04f }, new Color(0.2f, 0.18f, 0.16f), at + new Vector3(0, 0.02f, 0));
            // A pole and crossbar for a kettle, and the wood stacked by.
            foreach (var dx in new[] { -0.6f, 0.6f })
            {
                Add(_works, new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.04f, Height = 1.1f }, Pole, at + new Vector3(dx, 0.55f, 0));
            }

            Add(_works, new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 1.3f }, Pole, at + new Vector3(0, 1.08f, 0), new Vector3(0, 0, 90));
            for (var i = 0; i < 4; i++)
            {
                Add(_works, new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.07f, Height = 1.2f }, Pole.Lightened(0.15f), at + new Vector3(1.2f, 0.07f + i * 0.12f, -0.2f + (i % 2) * 0.15f), new Vector3(90, 0, 0));
            }
        }

        // The stores: casks and sacks on the sand, as many as there are, and a sail over them once covered.
        if (_location.FindSpot("spot.stores") is { } stores && storesAshore > 0)
        {
            var at = Spot(stores) + new Vector3(0, 0, 1.6f);
            var piles = Math.Clamp(storesAshore / 40, 1, 12);
            for (var i = 0; i < piles; i++)
            {
                var offset = new Vector3((i % 4) * 0.7f - 1f, 0, (i / 4) * 0.75f);
                if (i % 3 == 2)
                {
                    Add(_works, new BoxMesh { Size = new Vector3(0.55f, 0.4f, 0.65f) }, Sack, at + offset + new Vector3(0, 0.2f, 0));
                }
                else
                {
                    Add(_works, new CylinderMesh { TopRadius = 0.27f, BottomRadius = 0.27f, Height = 0.75f }, Cask, at + offset + new Vector3(0, 0.38f, 0));
                }
            }

            if (camp.Work(CampWorks.Cover) >= 1)
            {
                Add(_works, new PrismMesh { Size = new Vector3(3.6f, 1.2f, 2.8f) }, Canvas.Darkened(0.08f), at + new Vector3(0.05f, 0.95f, 0.75f));
            }
        }

        if (camp.Work(CampWorks.Latrine) >= 1 && _location.FindSpot("spot.latrine") is { } latrine)
        {
            var at = Spot(latrine) + new Vector3(0, 0, 1.2f);
            Add(_works, new BoxMesh { Size = new Vector3(1.4f, 0.05f, 0.5f) }, new Color(0.18f, 0.15f, 0.1f), at + new Vector3(0, 0.02f, 0));
            Add(_works, new BoxMesh { Size = new Vector3(1.6f, 0.25f, 0.4f) }, Earth, at + new Vector3(0, 0.12f, 0.55f));
            // A screen of brush on poles.
            Add(_works, new BoxMesh { Size = new Vector3(1.8f, 1.3f, 0.12f) }, Brush, at + new Vector3(0, 0.65f, -0.5f));
        }

        if (waterKnown && _location.FindSpot("spot.water_1") is { } water)
        {
            var at = Spot(water) + new Vector3(1f, 0, -1.2f);
            foreach (var dx in new[] { 0f, 0.65f })
            {
                Add(_works, new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.25f, Height = 0.65f }, Cask, at + new Vector3(dx, 0.33f, 0), new Vector3(0, 0, 90));
            }
        }
    }

    // A ridge-pole tent of two sail cloths, open at the ends.
    private void SailShelter(Vector3 at)
    {
        Add(_works, new PrismMesh { Size = new Vector3(2.6f, 1.6f, 3.2f) }, Canvas, at + new Vector3(0, 0.8f, 0));
        foreach (var dz in new[] { -1.65f, 1.65f })
        {
            Add(_works, new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.045f, Height = 1.75f }, Pole, at + new Vector3(0, 0.87f, dz));
        }
    }

    // A lean-to of poles thatched with brush and palm, its back to the weather.
    private void BrushShelter(Vector3 at)
    {
        Add(_works, new BoxMesh { Size = new Vector3(2.6f, 0.15f, 2.4f) }, Brush, at + new Vector3(0, 0.85f, 0), new Vector3(-38, 0, 0));
        Add(_works, new BoxMesh { Size = new Vector3(2.6f, 0.12f, 1.0f) }, Brush.Darkened(0.15f), at + new Vector3(0, 0.55f, 1.0f), new Vector3(-70, 0, 0));
        foreach (var dx in new[] { -1.2f, 1.2f })
        {
            Add(_works, new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.05f, Height = 1.6f }, Pole, at + new Vector3(dx, 0.8f, -0.95f));
        }
    }

    // ================================================================ the settlement (R7)

    private static readonly Color Thatch = new(0.66f, 0.56f, 0.34f);
    private static readonly Color OldThatch = new(0.42f, 0.38f, 0.28f);
    private static readonly Color Cleared = new(0.43f, 0.36f, 0.24f);
    private static readonly Color Bed = new(0.3f, 0.23f, 0.15f);
    private static readonly Color Green = new(0.32f, 0.55f, 0.2f);
    private static readonly Color Yellowed = new(0.62f, 0.56f, 0.26f);

    // Puddles in the wet, where the beach is lowest and the camp walks most.
    private void BuildPuddles()
    {
        var water = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.5f, 0.52f, 0.6f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.04f, Metallic = 0.35f };
        var rng = new RandomNumberGenerator { Seed = 38 };
        for (var i = 0; i < 26; i++)
        {
            var at = new Vector3(rng.RandfRange(4, _location.Width - 4), 0, rng.RandfRange(5, 17));
            at.Y = HeightAt(at.X, at.Z) + 0.015f;
            _wet.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = rng.RandfRange(0.25f, 0.7f), BottomRadius = 0.4f, Height = 0.01f, RadialSegments = 12 },
                MaterialOverride = water, Position = at, Scale = new Vector3(1, 1, rng.RandfRange(0.5f, 1.4f)),
            });
        }
    }

    // The middle of a building's ground.
    private Vector3 Middle(Footprint ground)
    {
        var x = (ground.X0 + ground.X1) / 2f;
        var z = (ground.Y0 + ground.Y1) / 2f;
        return new Vector3(x, HeightAt(x, z), z);
    }

    // A building's own frame: at the middle of its ground, turned a quarter if it was set down turned.
    private Node3D Holder(Footprint ground, bool turned)
    {
        var holder = new Node3D { Position = Middle(ground), RotationDegrees = new Vector3(0, turned ? 90 : 0, 0) };
        _settlement.AddChild(holder);
        return holder;
    }

    /// <summary>
    /// The outline of a building being set down, green where it may go and red where it may not, with its
    /// front (where the hands work) marked; null hides it.
    /// </summary>
    public void ShowGhost(BuildingDefinition? building, int x, int y, bool turned, bool allowed)
    {
        var key = building is null ? "" : $"{building.Id}:{x}:{y}:{turned}:{allowed}";
        if (key == _ghostShown)
        {
            return;
        }

        _ghostShown = key;
        foreach (var child in _ghost.GetChildren())
        {
            child.QueueFree();
        }

        _ghost.Visible = building is not null;
        if (building is null)
        {
            return;
        }

        var ground = SimulationRunner.FootprintOf(building, x, y, turned);
        var colour = allowed ? new Color(0.35f, 0.85f, 0.4f, 0.4f) : new Color(0.95f, 0.3f, 0.25f, 0.45f);
        var material = new StandardMaterial3D
        {
            AlbedoColor = colour, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var edge = new StandardMaterial3D { AlbedoColor = new Color(colour, 0.9f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        var middle = Middle(ground);
        var across = ground.X1 - ground.X0 + 1;
        var deep = ground.Y1 - ground.Y0 + 1;
        _ghost.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(across, 0.9f, deep) }, MaterialOverride = material, Position = middle + new Vector3(0, 0.45f, 0) });
        // Its outline on the ground, and the front, where its two work places are.
        foreach (var (size, offset) in new[]
                 {
                     (new Vector3(across, 0.06f, 0.1f), new Vector3(0, 0.03f, -deep / 2f)), (new Vector3(across, 0.06f, 0.1f), new Vector3(0, 0.03f, deep / 2f)),
                     (new Vector3(0.1f, 0.06f, deep), new Vector3(-across / 2f, 0.03f, 0)), (new Vector3(0.1f, 0.06f, deep), new Vector3(across / 2f, 0.03f, 0)),
                 })
        {
            _ghost.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = edge, Position = middle + offset });
        }

        foreach (var fx in new[] { x - 1, x })
        {
            var at = new Vector3(fx, HeightAt(fx, ground.Y0 - 1), ground.Y0 - 1);
            _ghost.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.18f, Height = 0.05f }, MaterialOverride = edge, Position = at + new Vector3(0, 0.03f, 0) });
        }
    }

    /// <summary>
    /// The settlement as it stands (R7): each building at its phase (ground cleared, posts, frame, roof going
    /// on course by course, fitted out), worn by its condition; the garden's beds by growth and health; the
    /// forest's edge felled for poles and growing back; paths worn only where feet go; the wet season's mud
    /// and puddles. Rebuilt only on change.
    /// </summary>
    public void ShowSettlement(Expedition.Simulation.World.SimulationRunner runner)
    {
        if (runner.Campaign.Camp is not { } camp || runner.Content.Shore?.Settlement is not { } rules)
        {
            return;
        }

        // Looked at once a game minute, not every frame: the ground changes slowly.
        if (runner.Campaign.Clock.Tick == _settledAt && _settled.Length > 0)
        {
            return;
        }

        _settledAt = runner.Campaign.Clock.Tick;
        var wet = runner.WetSeason;
        var key = string.Join(";", camp.Projects.Select(p => $"{p.Plot}:{p.Building}:{p.X}:{p.Y}:{p.Turned}:{p.Phase}:{p.Done}"))
                  + "#" + string.Join(";", camp.Buildings.Select(b => $"{b.Plot}:{b.X}:{b.Y}:{b.Turned}:{(int)(b.Condition / 15)}"))
                  + "#" + string.Join(";", camp.Beds.Select(b => $"{b.Crop}:{(int)(runner.Growth(b) * 6)}:{(int)(b.Health * 5)}"))
                  + "#" + string.Join(";", camp.Trodden.Select(t => $"{t.Key}:{(int)(runner.PathWear(t.Value) * 6)}").Where(t => !t.EndsWith(":0", StringComparison.Ordinal)))
                  + "#" + string.Join(";", camp.Regrowing.Select(r => $"{r.Key}:{runner.Growth(r.Value)}"))
                  + $"#{wet}";
        if (key == _settled)
        {
            return;
        }

        _settled = key;
        foreach (var child in _settlement.GetChildren())
        {
            child.QueueFree();
        }

        // The ground darkens and puddles in the wet.
        _wet.Visible = wet;
        _groundMaterial.AlbedoColor = wet ? new Color(0.78f, 0.76f, 0.72f) : Colors.White;

        // Trees on a building's ground: felled as its clearing goes on, stumps and all grubbed out once done.
        var grounds = camp.Projects.Where(p => rules.Building(p.Building) is not null)
            .Select(p => (Ground: SimulationRunner.FootprintOf(rules.Building(p.Building)!, p.X, p.Y, p.Turned), Cleared: p.Phase > 0, Clearing: p.Phase == 0 && p.Done > 0))
            .Concat(camp.Buildings.Where(b => rules.Building(b.Building) is not null)
                .Select(b => (Ground: SimulationRunner.FootprintOf(rules.Building(b.Building)!, b.X, b.Y, b.Turned), Cleared: true, Clearing: false)))
            .ToList();

        // Elsewhere the forest's edge as the cutting has left it: felled trees' stumps, greening over with
        // scrub, then brush, then a sapling, and a tree again when left alone long enough.
        foreach (var edge in _edgeTrees)
        {
            var under = grounds.FirstOrDefault(g => g.Ground.Contains(edge.X, edge.Y));
            var stage = under.Cleared || under.Clearing ? (GrowthStage?)null
                : camp.Regrowing.TryGetValue(CampState.Key(edge.X, edge.Y), out var growing) ? runner.Growth(growing) : null;
            var standing = !under.Cleared && !under.Clearing && stage is null;
            edge.Tree.Visible = standing;
            edge.Stump.Visible = under.Clearing || stage is GrowthStage.Bare or GrowthStage.Scrub;
            edge.Bush.Visible = stage is GrowthStage.Scrub or GrowthStage.Brush;
            edge.Bush.Scale = Vector3.One * (stage == GrowthStage.Brush ? 1.5f : 0.7f);
            edge.Sapling.Visible = stage == GrowthStage.Sapling;
        }

        var ground = new MeshBatcher();
        // Ground given up (a building abandoned or blown down) greening over: bare earth, then scrub and grass
        // coming through it, then brush.
        foreach (var (cellKey, growing) in camp.Regrowing.Where(r => !r.Value.Tree))
        {
            var (gx, gy) = CampState.Cell(cellKey);
            if (grounds.Any(g => g.Ground.Contains(gx, gy)))
            {
                continue;
            }

            var at = new Vector3(gx, HeightAt(gx, gy), gy);
            var stage = runner.Growth(growing);
            var earth = stage == GrowthStage.Bare ? Cleared : Cleared.Lerp(Green, stage == GrowthStage.Scrub ? 0.35f : 0.6f);
            ground.AddColoured(new BoxMesh { Size = new Vector3(1, 0.02f, 1) }, new Transform3D(Basis.Identity, at + new Vector3(0, 0.01f, 0)), earth);
            if (stage != GrowthStage.Bare)
            {
                var size = stage == GrowthStage.Scrub ? 0.22f : 0.42f;
                foreach (var (ox, oz) in new[] { (-0.25f, -0.2f), (0.2f, 0.25f), ((gx * 7 + gy * 3) % 5 * 0.1f - 0.2f, -0.05f) })
                {
                    ground.AddColoured(new SphereMesh { Radius = size, Height = size * 1.3f, RadialSegments = 6, Rings = 3 },
                        new Transform3D(Basis.Identity, at + new Vector3(ox, size * 0.4f, oz)), (gx + gy) % 2 == 0 ? Leaf : Green);
                }
            }
        }

        // Paths only where feet have gone: each cell walked over often enough shows trodden, the more
        // worn the darker and wider, so the ways people really take come out of the sand by themselves.
        // Each worn cell is joined to its worn neighbours by a strip, so a path reads as one track.
        var trodden = Sand.Darkened(0.28f) * (wet ? new Color(0.78f, 0.76f, 0.72f) : Colors.White);
        var worn = camp.Trodden.Select(t => (Cell: CampState.Cell(t.Key), Wear: (float)runner.PathWear(t.Value)))
            .Where(t => t.Wear > 0).ToDictionary(t => t.Cell, t => t.Wear);
        Vector3 On(float x, float z, float wear) => new(x, HeightAt(x, z) + 0.008f + 0.003f * wear, z);
        foreach (var ((px, py), wear) in worn)
        {
            var colour = Sand.Lerp(trodden, 0.35f + 0.65f * wear);
            var width = 0.5f + 0.5f * wear;
            ground.AddColoured(new CylinderMesh { TopRadius = width / 2, BottomRadius = width / 2, Height = 0.012f, RadialSegments = 10 },
                new Transform3D(Basis.Identity, On(px, py, wear)), colour);
            foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (1, 1), (-1, 1) })
            {
                if (!worn.TryGetValue((px + dx, py + dy), out var next))
                {
                    continue;
                }

                // Diagonal joins only where the step is not already joined round the corner.
                if (dx != 0 && dy != 0 && (worn.ContainsKey((px + dx, py)) || worn.ContainsKey((px, py + dy))))
                {
                    continue;
                }

                var both = Math.Min(wear, next);
                var length = new Vector2(dx, dy).Length();
                var basis = new Basis(Vector3.Up, Mathf.Atan2(dx, dy));
                ground.AddColoured(new BoxMesh { Size = new Vector3(0.5f + 0.5f * both, 0.012f, length) },
                    new Transform3D(basis, On(px + dx / 2f, py + dy / 2f, both)), Sand.Lerp(trodden, 0.35f + 0.65f * both));
            }
        }

        ground.Commit(_settlement);

        foreach (var project in camp.Projects)
        {
            if (rules.Building(project.Building) is { } building)
            {
                // How far along: whole phases done, and the share of the one in hand.
                var share = project.Phase < building.Phases.Count ? project.Done / (float)Math.Max(1, runner.PhaseTimes(project)) : 1;
                var holder = Holder(SimulationRunner.FootprintOf(building, project.X, project.Y, project.Turned), project.Turned);
                DrawBuilding(holder, building, Vector3.Zero, project.Phase, share, 100, 0.5f);
            }
        }

        foreach (var standing in camp.Buildings)
        {
            if (rules.Building(standing.Building) is { } building)
            {
                var holder = Holder(SimulationRunner.FootprintOf(building, standing.X, standing.Y, standing.Turned), standing.Turned);
                DrawBuilding(holder, building, Vector3.Zero, building.Phases.Count, 1, (float)standing.Condition, (float)standing.Quality);
            }
        }

        // The garden's beds: green while healthy, yellowing as they waste, taller as they grow.
        if (camp.Buildings.FirstOrDefault(b => rules.Building(b.Building)?.Beds > 0) is { } garden)
        {
            var beds = Holder(SimulationRunner.FootprintOf(rules.Building(garden.Building)!, garden.X, garden.Y, garden.Turned), garden.Turned);
            var plot = Vector3.Zero;
            for (var i = 0; i < camp.Beds.Count; i++)
            {
                var bed = camp.Beds[i];
                var at = plot + new Vector3(-2.2f + (i % 3) * 2.2f, 0.12f, -0.6f + (i / 3) * 1.6f);
                var grown = (float)runner.Growth(bed);
                var colour = Yellowed.Lerp(Green, (float)Math.Clamp(bed.Health, 0, 1));
                for (var row = 0; row < 3; row++)
                {
                    Add(beds, new BoxMesh { Size = new Vector3(1.6f, 0.06f + 0.4f * grown, 0.22f) }, colour, at + new Vector3(0, 0.03f + 0.2f * grown, -0.4f + row * 0.4f));
                }
            }
        }
    }

    // One building, as far as it has got: phase 0 the ground cleared, then posts, frame, roof, and fitting
    // out; a share of the phase in hand shows that much of it. Worn by its condition: the thatch dark and
    // patchy, sagging when bad. Rougher work sags sooner.
    private void DrawBuilding(Node3D parent, BuildingDefinition building, Vector3 at, int phase, float share, float condition, float quality)
    {
        var isGarden = building.Beds > 0;
        var (w, d, h) = building.Id switch
        {
            "building.storehouse" => (4.2f, 3.2f, 2.4f),
            "building.infirmary" => (4.4f, 3.0f, 2.2f),
            "building.workshop" => (3.6f, 3.0f, 2.2f),
            "building.cookhouse" => (3.0f, 3.0f, 2.1f),
            "building.garden" => (7.4f, 3.8f, 1.1f),
            _ => (3.2f, 2.8f, 2.1f),
        };
        var phases = building.Phases.Select(p => p.Task).ToList();
        float Reached(string task)
        {
            var index = phases.IndexOf(task);
            return index < 0 ? 0 : phase > index ? 1 : phase == index ? share : 0;
        }

        // The ground cleared: a patch of turned earth, pegged out.
        var clear = Reached("task.clear_ground");
        if (clear > 0 || phase > 0)
        {
            Add(parent, new BoxMesh { Size = new Vector3(w + 0.8f, 0.02f, d + 0.8f) }, Cleared, at + new Vector3(0, 0.01f, 0));
            foreach (var (cx, cz) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                Add(parent, new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.5f }, Pole, at + new Vector3(cx * (w / 2 + 0.3f), 0.25f, cz * (d / 2 + 0.3f)));
            }
        }

        if (isGarden)
        {
            // Beds dug in rows, then a fence of stakes round it.
            var dug = Reached("task.dig_beds");
            for (var i = 0; i < (int)(6 * dug); i++)
            {
                Add(parent, new BoxMesh { Size = new Vector3(1.8f, 0.12f, 1.2f) }, Bed, at + new Vector3(-2.2f + (i % 3) * 2.2f, 0.06f, -0.6f + (i / 3) * 1.6f));
            }

            var fence = Reached("task.fence_garden");
            var stakes = (int)(36 * fence);
            for (var i = 0; i < stakes; i++)
            {
                var t = i / 36f * 4;
                var side = (int)t;
                var f = t - side;
                var p = side switch
                {
                    0 => new Vector3(-w / 2 + f * w, 0, -d / 2),
                    1 => new Vector3(w / 2, 0, -d / 2 + f * d),
                    2 => new Vector3(w / 2 - f * w, 0, d / 2),
                    _ => new Vector3(-w / 2, 0, d / 2 - f * d),
                };
                Add(parent, new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.04f, Height = 0.9f }, Pole, at + p + new Vector3(0, 0.45f, 0));
            }

            return;
        }

        // Rough work and a poor condition sag: the roof and frame lean, the thatch goes dark and thin.
        var worn = 1 - condition / 100f;
        var sag = Mathf.Clamp(worn * (1.4f - quality), 0, 0.6f);
        var lift = building.Stores ? 0.6f : 0;
        var top = lift + h * (1 - 0.15f * sag);

        // Posts: the corners first, then along the sides.
        var posts = Reached("task.set_posts");
        var corners = new List<Vector3>();
        foreach (var fx in new[] { -0.5f, 0f, 0.5f })
        {
            foreach (var fz in new[] { -0.5f, 0.5f })
            {
                corners.Add(new Vector3(fx * w, 0, fz * d));
            }
        }

        var shown = (int)Math.Ceiling(corners.Count * posts);
        for (var i = 0; i < shown; i++)
        {
            var c = corners[(i * 5) % corners.Count];
            Add(parent, new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.08f, Height = top }, Pole, at + c + new Vector3(0, top / 2, 0), new Vector3(sag * 8, 0, sag * 6));
        }

        // The frame: plates along the top, then the rafters and the ridge.
        var frame = Reached("task.raise_frame");
        if (frame > 0)
        {
            foreach (var fz in new[] { -0.5f, 0.5f })
            {
                Add(parent, new BoxMesh { Size = new Vector3(w, 0.1f, 0.1f) }, Pole, at + new Vector3(0, top, fz * d));
            }

            var rafters = (int)Math.Ceiling(5 * frame);
            for (var i = 0; i < rafters; i++)
            {
                var x = -w / 2 + i * w / 4;
                foreach (var side in new[] { -1, 1 })
                {
                    Add(parent, new BoxMesh { Size = new Vector3(0.07f, 0.07f, d * 0.62f) }, Pole, at + new Vector3(x, top + 0.45f, side * d * 0.25f), new Vector3(side * -34, 0, 0));
                }
            }

            if (frame >= 1)
            {
                Add(parent, new BoxMesh { Size = new Vector3(w + 0.2f, 0.09f, 0.09f) }, Pole, at + new Vector3(0, top + 0.9f - sag * 0.4f, 0));
            }
        }

        // The roof, course by course from the eaves up; dark and patchy as it wears.
        var roof = Reached("task.thatch_roof");
        if (roof > 0)
        {
            var thatch = Thatch.Lerp(OldThatch, Mathf.Clamp(worn * 1.5f, 0, 1));
            var courses = Math.Max(1, (int)Math.Ceiling(4 * roof));
            var gaps = worn > 0.45f ? (int)((worn - 0.45f) * 10) : 0;
            for (var c = 0; c < courses; c++)
            {
                foreach (var side in new[] { -1, 1 })
                {
                    if (gaps > 0 && (c + side + 3) % 4 < gaps)
                    {
                        continue;
                    }

                    var z = side * d * (0.42f - c * 0.1f);
                    Add(parent, new BoxMesh { Size = new Vector3(w + 0.5f, 0.14f, d * 0.24f) }, thatch,
                        at + new Vector3(0, top + 0.12f + c * 0.22f - sag * 0.3f * c / 4, z), new Vector3(side * -34 + sag * 10, 0, sag * 4));
                }
            }
        }

        // Fitted out: the storehouse's floor raised off the ground and a door; the others' fittings when finished.
        var fitted = phases.Contains("task.fit_out") ? Reached("task.fit_out") : roof >= 1 ? 1 : 0;
        if (fitted > 0)
        {
            if (building.Stores)
            {
                Add(parent, new BoxMesh { Size = new Vector3(w, 0.1f, d) }, Pole.Lightened(0.15f), at + new Vector3(0, lift, 0));
                if (fitted >= 1)
                {
                    Add(parent, new BoxMesh { Size = new Vector3(w - 0.3f, 1.4f, d - 0.3f) }, Brush.Darkened(0.1f), at + new Vector3(0, lift + 0.75f, 0));
                    Add(parent, new BoxMesh { Size = new Vector3(0.9f, 1.3f, 0.06f) }, Pole.Darkened(0.2f), at + new Vector3(0, lift + 0.7f, -d / 2 + 0.1f));
                }
            }
            else if (building.Cooks)
            {
                Add(parent, new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.6f, Height = 0.4f }, Stone, at + new Vector3(0, 0.2f, 0));
            }
            else if (building.SickBerths > 0)
            {
                foreach (var dx in new[] { -1.2f, 0f, 1.2f })
                {
                    Add(parent, new BoxMesh { Size = new Vector3(0.7f, 0.35f, 1.8f) }, Sack, at + new Vector3(dx, 0.18f, 0));
                }

                Add(parent, new BoxMesh { Size = new Vector3(0.02f, 0.35f, 0.5f) }, new Color(0.95f, 0.95f, 0.9f), at + new Vector3(w / 2, top + 0.6f, 0));
            }
            else if (building.Mends)
            {
                Add(parent, new BoxMesh { Size = new Vector3(2.0f, 0.12f, 0.6f) }, Pole.Lightened(0.1f), at + new Vector3(0, 0.8f, 0.4f));
                Add(parent, new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.25f, Height = 1.6f }, Bark, at + new Vector3(0.5f, 0.25f, -0.6f), new Vector3(0, 0, 90));
            }
            else if (building.Berths > 0)
            {
                // Walls of woven brush on three sides, the front open.
                Add(parent, new BoxMesh { Size = new Vector3(w, top * 0.8f, 0.12f) }, Brush, at + new Vector3(0, top * 0.4f, d / 2));
                foreach (var side in new[] { -1, 1 })
                {
                    Add(parent, new BoxMesh { Size = new Vector3(0.12f, top * 0.8f, d) }, Brush, at + new Vector3(side * w / 2, top * 0.4f, 0));
                }
            }
        }
    }
}
