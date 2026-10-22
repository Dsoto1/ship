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
        AddChild(new MeshInstance3D
        {
            Name = "Ground", Mesh = tool.Commit(),
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.97f },
        });

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

        for (var y = 0; y < _location.Height; y++)
        {
            for (var x = 0; x < _location.Width; x++)
            {
                if (y >= 18 && _location.Rows[y][x] == LocationDefinition.Blocked && x > 0 && x < _location.Width - 1 && y < _location.Height - 1)
                {
                    Tree(x + rng.RandfRange(-0.3f, 0.3f), y + rng.RandfRange(-0.3f, 0.3f), 1f);
                }
            }
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
}
