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
    private sealed record EdgeTree(Node3D Tree, Node3D Stump, MeshInstance3D Bush, Node3D Sapling, int X, int Y);
    private Node3D _ghost = null!;
    private string _ghostShown = "";
    private Node3D _settlement = null!;
    private Node3D _wet = null!;
    private StandardMaterial3D _groundMaterial = null!;
    private string _settled = "";
    private string _ground = "forest";
    private int _seed = 1519;
    private long _settledAt = -1;

    /// <summary>The ground's height at a cell position, metres (the beach rises inland).</summary>
    public float HeightAt(float x, float z)
    {
        foreach (var area in _location.Ground)
        {
            if (area.HeightAt(x, z) is { } height)
            {
                return (float)height + Lift + Dune(x, z);
            }
        }

        return z < 0 ? Lift - 0.25f * Mathf.Min(-z, 6f) : Lift;
    }

    // The sand's rise and fall: low over the camp's middle, where it is built on, more on the dunes.
    private float Dune(float x, float z)
    {
        var amplitude = _ground switch { "dunes" => 0.4f, "headland" => 0.16f, "mangrove" => 0.05f, _ => 0.12f };
        var middle = Mathf.Clamp(Mathf.Abs(z - 9) / 6f, 0.35f, 1f);
        var rise = Mathf.Clamp((z - 1) / 3f, 0, 1);
        return amplitude * middle * rise * (Fbm(x * 0.16f + 3, z * 0.16f) - 0.45f);
    }

    // The whole camp stands this far above the sea's surface, so the surf breaks on the sand's foot.
    private const float Lift = 0.3f;

    /// <summary>The camp's centre, in its own frame, for the camera.</summary>
    public Vector3 Centre => new(_location.Width / 2f, Lift, 9f);

    /// <summary>Builds the beach, the forest's edge and the camp's places.</summary>
    public void Build(LocationDefinition location, string ground = "forest", ulong seed = 1519)
    {
        _ground = ground;
        _seed = (int)(seed % 1000003);
        // Everything but the people, who are kept under the camp by Main.
        foreach (var child in GetChildren().Where(c => c.Name != "CampFigures"))
        {
            child.QueueFree();
        }

        _location = location;
        _canoes = null;
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

    // Sand from under the surf up to the forest, reaching well past the grid on every side so the camp never
    // floats: wet at the water, dry and pale on the ridges, darker in the hollows, creeper and grass coming
    // out onto it below the trees, and leaf litter and earth among them. Finer over the camp, where it is
    // looked at, coarser beyond.
    private void BuildGround()
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        var (width, height) = (_location.Width, _location.Height);
        void Quad(float x, float z, float step)
        {
            var corners = new[] { new Vector2(x, z), new Vector2(x + step, z), new Vector2(x + step, z + step), new Vector2(x, z + step) };
            var points = corners.Select(c => new Vector3(c.X, HeightAt(Mathf.Clamp(c.X, 1, width - 2), c.Y), c.Y)).ToArray();
            // Coloured at each corner, so the colours blend across the ground and no square shows.
            var colours = corners.Select(c => Colour(c.X, c.Y)).ToArray();
            tool.SetNormal(Vector3.Up);
            foreach (var i in new[] { 0, 1, 2, 0, 2, 3 })
            {
                tool.SetColor(colours[i]);
                tool.AddVertex(points[i]);
            }
        }

        // Each corner's colour is worked out once, as neighbouring squares share it.
        var known = new Dictionary<(int, int), Color>();
        Color Colour(float x, float z)
        {
            var key = (Mathf.RoundToInt(x * 2), Mathf.RoundToInt(z * 2));
            if (!known.TryGetValue(key, out var colour))
            {
                colour = GroundColour(x, z);
                known[key] = colour;
            }

            return colour;
        }

        for (var z = -8f; z < height + 40; z += 1f)
        {
            for (var x = -60f; x < width + 60; x += 1f)
            {
                var inside = x >= -4 && x < width + 4 && z >= -4 && z < height + 2;
                if (!inside)
                {
                    Quad(x, z, 1f);
                    continue;
                }

                for (var fz = 0f; fz < 1f; fz += 0.5f)
                {
                    for (var fx = 0f; fx < 1f; fx += 0.5f)
                    {
                        Quad(x + fx, z + fz, 0.5f);
                    }
                }
            }
        }

        tool.GenerateNormals();
        _groundMaterial = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.97f };
        AddChild(new MeshInstance3D { Name = "Ground", Mesh = tool.Commit(), MaterialOverride = _groundMaterial });

        // Driftwood and rocks where the map has them; on a headland the rock breaks through in outcrops.
        var rng = new RandomNumberGenerator { Seed = (ulong)_seed };
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (_location.Rows[y][x] == LocationDefinition.Fitting && y < 18)
                {
                    var at = new Vector3(x, HeightAt(x, y), y);
                    if ((x + y) % 2 == 0 && _ground != "headland")
                    {
                        Add(this, new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.16f, Height = 1.6f + rng.Randf() * 1.4f, RadialSegments = 7 }, Pole.Lightened(0.25f + rng.Randf() * 0.1f),
                            at + new Vector3(0, 0.12f, 0), new Vector3(rng.RandfRange(-6, 6), rng.RandfRange(0, 180), 90));
                    }
                    else
                    {
                        var cluster = new Node3D { Position = at, RotationDegrees = new Vector3(0, rng.RandfRange(0, 360), 0) };
                        AddChild(cluster);
                        if (Place(Pick(rng, "Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3"), rng.RandfRange(0.5f, 0.9f), rng, new IntoNode(cluster, Plain)))
                        {
                            for (var k = 0; k < rng.RandiRange(1, 3); k++)
                            {
                                var small = new Transform3D(new Basis(Vector3.Up, rng.RandfRange(0, Mathf.Tau)), new Vector3(rng.RandfRange(-0.9f, 0.9f), 0, rng.RandfRange(-0.9f, 0.9f)));
                                var pebble = new Node3D { Transform = small };
                                cluster.AddChild(pebble);
                                Place(Pick(rng, "Pebble_Round_1", "Pebble_Round_2", "Pebble_Round_3", "Pebble_Round_4", "Pebble_Round_5"), rng.RandfRange(0.12f, 0.3f), rng, new IntoNode(pebble, Plain));
                            }

                            continue;
                        }

                        for (var k = 0; k < 2 + rng.RandiRange(0, 2); k++)
                        {
                            var r = 0.25f + rng.Randf() * 0.35f;
                            Add(this, new SphereMesh { Radius = r, Height = r * (1.1f + rng.Randf() * 0.5f), RadialSegments = 7, Rings = 4 }, Stone.Darkened(rng.Randf() * 0.15f),
                                at + new Vector3(rng.RandfRange(-0.4f, 0.4f), r * 0.3f, rng.RandfRange(-0.4f, 0.4f)), new Vector3(0, rng.RandfRange(0, 180), rng.RandfRange(-12, 12)));
                        }
                    }
                }
            }
        }
    }

    // Where the forest's edge runs, column by column: the first row of trees from the sea.
    private int EdgeRow(int x)
    {
        x = Math.Clamp(x, 1, _location.Width - 2);
        for (var y = 10; y < _location.Height - 1; y++)
        {
            if (_location.Rows[y][x] == LocationDefinition.Blocked)
            {
                return y;
            }
        }

        return _location.Height - 1;
    }

    private static readonly Color DrySand = new(0.9f, 0.83f, 0.66f);
    private static readonly Color Creeper = new(0.36f, 0.47f, 0.2f);
    private static readonly Color Litter = new(0.34f, 0.29f, 0.18f);
    private static readonly Color Mud = new(0.33f, 0.3f, 0.22f);
    private static readonly Color Rock = new(0.46f, 0.45f, 0.42f);

    // The ground's colour at a point.
    private Color GroundColour(float x, float z)
    {
        var edge = Mathf.Lerp(EdgeRow(Mathf.FloorToInt(x)), EdgeRow(Mathf.CeilToInt(x)), x - Mathf.Floor(x)) - 1.5f + 1.5f * (Fbm(x * 0.7f, z * 0.7f) - 0.5f);
        var n = Fbm(x * 0.35f, z * 0.35f);
        var fine = Noise(x * 2.3f, z * 2.3f);
        Color colour;
        if (z < 1.2f + 0.6f * Noise(x * 0.4f, 3f))
        {
            colour = _ground == "mangrove" ? Mud : WetSand;
        }
        else
        {
            // Ridges dry and pale, hollows darker; wind ripples on the dunes.
            colour = Sand.Lerp(DrySand, Mathf.Clamp((n - 0.45f) * 2.5f, 0, 1)).Darkened(Mathf.Clamp((0.4f - n) * 0.4f, 0, 0.12f));
            if (_ground == "dunes")
            {
                colour = colour.Darkened(0.05f * Mathf.Sin(x * 2.1f + z * 0.7f + 3 * n));
            }

            // Creeper and grass come out onto the sand below the trees, in tongues.
            var creep = Mathf.Clamp(1 - (edge - z) / (_ground == "dunes" ? 6f : 3.5f), 0, 1) * Mathf.Clamp((Fbm(x * 0.5f + 7, z * 0.5f) - 0.3f) * 2.2f, 0, 1);
            colour = colour.Lerp(Creeper.Darkened(0.12f * fine), creep * 0.85f);
            // Under the trees, litter and earth.
            colour = colour.Lerp(_ground == "mangrove" ? Mud : Litter.Lightened(0.15f * n), Mathf.Clamp((z - edge) / 2.5f, 0, 1));
            if (_ground == "headland" && Fbm(x * 0.3f + 31, z * 0.3f) > 0.68f && z > 2)
            {
                colour = colour.Lerp(Rock.Darkened(0.1f * fine), 0.8f);
            }
        }

        return colour.Darkened(0.05f * (fine - 0.5f));
    }

    // Value noise, smooth, in 0..1, the same for the same seed.
    private float Noise(float x, float z)
    {
        var (ix, iz) = (Mathf.FloorToInt(x), Mathf.FloorToInt(z));
        var (fx, fz) = (x - ix, z - iz);
        (fx, fz) = (fx * fx * (3 - 2 * fx), fz * fz * (3 - 2 * fz));
        float H(int a, int b)
        {
            var h = unchecked((uint)(a * 374761393 + b * 668265263 + _seed * 1442695041));
            h = unchecked((h ^ (h >> 13)) * 1274126177u);
            return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
        }

        return Mathf.Lerp(Mathf.Lerp(H(ix, iz), H(ix + 1, iz), fx), Mathf.Lerp(H(ix, iz + 1), H(ix + 1, iz + 1), fx), fz);
    }

    private float Fbm(float x, float z) => 0.55f * Noise(x, z) + 0.3f * Noise(x * 2.1f + 17, z * 2.1f) + 0.15f * Noise(x * 4.3f + 41, z * 4.3f);

    // ================================================================ the Nature Kit (Kenney, CC0)

    // One part of a model: a single surface, where it sits in the model, its own colour, and whether it is
    // leaves (green), to be tinted from the coast's palette.
    private sealed record NaturePart(Mesh Mesh, Transform3D At, Color Colour, bool Leafy, Material? Textured);

    // The kit's own colours are its bright mint palette; each part is given the coast's instead, by what its
    // material is: leaves and grass green, bark and wood brown, stone grey, dirt earth. Anything else (the
    // flowers, the fungi) keeps its own colour.
    private static (Color Colour, bool Leafy) Recoloured(string material, Color own)
    {
        var name = material.ToLowerInvariant();
        return name switch
        {
            _ when name.Contains("leaf") || name.Contains("grass") || name.Contains("plant") || name.Contains("green") => (Leaves[0], true),
            _ when name.Contains("wood") || name.Contains("bark") || name.Contains("log") => (Bark.Lightened(0.05f), false),
            _ when name.Contains("stone") || name.Contains("rock") => (Stone, false),
            _ when name.Contains("dirt") || name.Contains("ground") => (Earth.Lightened(0.15f), false),
            _ => (own.LinearToSrgb(), false),
        };
    }

    // A model as parts, with its height, for scaling it to the size wanted.
    private sealed record NatureModel(IReadOnlyList<NaturePart> Parts, float Height);

    private static readonly Dictionary<string, NatureModel?> Models = [];

    // The model from Assets/Nature (the Nature Kit's .glb) or Assets/Nature/MegaKit (the MegaKit's textured
    // .gltf), or null if it is not there (the shapes made here stand in for it).
    private static NatureModel? Model(string name)
    {
        if (Models.TryGetValue(name, out var known))
        {
            return known;
        }

        var path = ResourceLoader.Exists($"res://Assets/Nature/MegaKit/{name}.gltf") ? $"res://Assets/Nature/MegaKit/{name}.gltf" : $"res://Assets/Nature/{name}.glb";
        NatureModel? model = null;
        if (ResourceLoader.Exists(path) && GD.Load<PackedScene>(path)?.Instantiate<Node3D>() is { } root)
        {
            var parts = new List<NaturePart>();
            var bounds = new Aabb();
            var first = true;
            void Walk(Node node, Transform3D at)
            {
                var here = node is Node3D n ? at * n.Transform : at;
                if (node is MeshInstance3D { Mesh: { } mesh })
                {
                    for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
                    {
                        var single = new ArrayMesh();
                        single.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, mesh.SurfaceGetArrays(surface));
                        var material = mesh.SurfaceGetMaterial(surface);
                        var (colour, leafy) = Recoloured(material?.ResourceName ?? "", material is BaseMaterial3D own ? own.AlbedoColor : new Color(0.5f, 0.5f, 0.5f));
                        // A textured part (the MegaKit's) keeps its own material: its leaves are cut out of
                        // their texture, which a flat colour would lose.
                        var textured = material is BaseMaterial3D { AlbedoTexture: not null } ? material : null;
                        parts.Add(new NaturePart(single, here, colour, leafy || (material?.ResourceName ?? "").Contains("Leaves", StringComparison.Ordinal), textured));
                        var box = here * single.GetAabb();
                        bounds = first ? box : bounds.Merge(box);
                        first = false;
                    }
                }

                foreach (var child in node.GetChildren())
                {
                    Walk(child, here);
                }
            }

            Walk(root, Transform3D.Identity);
            root.Free();
            model = parts.Count == 0 ? null : new NatureModel(parts, Mathf.Max(0.01f, bounds.End.Y));
        }

        Models[name] = model;
        return model;
    }

    // A textured material tinted: its leaves a little warmer, cooler, darker or lighter, one of a few
    // copies kept per material, so a stand of one model is not all one green.
    private static readonly Dictionary<(Material, int), Material> Tinted = [];
    private static readonly Color[] Tints = [Colors.White, new(0.86f, 0.95f, 0.8f), new(1.05f, 1f, 0.82f), new(0.8f, 0.88f, 0.86f)];

    private static Material Tint(Material material, bool leafy, int tint)
    {
        if (!leafy || tint == 0 || material is not BaseMaterial3D own)
        {
            return material;
        }

        if (!Tinted.TryGetValue((material, tint), out var copy))
        {
            var tinted = (BaseMaterial3D)own.Duplicate();
            tinted.AlbedoColor = own.AlbedoColor * Tints[tint];
            copy = tinted;
            Tinted[(material, tint)] = copy;
        }

        return copy;
    }

    // Where a part is put: in a node of its own (a tree that may be felled) or into the batched forest.
    private interface INatureSink
    {
        void Coloured(Mesh mesh, Transform3D at, Color colour);

        void Textured(Mesh mesh, Transform3D at, Material material);
    }

    private sealed class IntoNode(Node3D node, Func<Color, Material> plain) : INatureSink
    {
        public void Coloured(Mesh mesh, Transform3D at, Color colour) => node.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = plain(colour), Transform = at });

        public void Textured(Mesh mesh, Transform3D at, Material material) => node.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material, Transform = at });
    }

    private sealed class IntoBatch(MeshBatcher batch, Transform3D origin) : INatureSink
    {
        public void Coloured(Mesh mesh, Transform3D at, Color colour) => batch.AddColoured(mesh, origin * at, colour);

        public void Textured(Mesh mesh, Transform3D at, Material material) => batch.Add(mesh, origin * at, material);
    }

    private static bool Place(string name, float height, RandomNumberGenerator rng, INatureSink sink)
    {
        if (Model(name) is not { } model)
        {
            return false;
        }

        var tint = rng.RandiRange(0, Tints.Length - 1);
        var scale = height / model.Height;
        var squash = Basis.Identity.Scaled(new Vector3(scale * rng.RandfRange(0.9f, 1.1f), scale, scale * rng.RandfRange(0.9f, 1.1f)));
        Action<Mesh, Transform3D, Color> emit = sink.Coloured;
        foreach (var part in model.Parts.Where(p => p.Textured is not null))
        {
            sink.Textured(part.Mesh, new Transform3D(squash, Vector3.Zero) * part.At, Tint(part.Textured!, part.Leafy, tint));
        }

        if (model.Parts.All(p => p.Textured is not null))
        {
            return true;
        }

        return Place(model with { Parts = model.Parts.Where(p => p.Textured is null).ToList() }, squash, rng, emit);
    }

    // The model's parts, scaled to the height wanted, the leaves tinted toward one of the coast's greens and
    // all a shade lighter or darker, so no two trees of a model are alike. False if there is no model.
    private static bool Place(string name, float height, RandomNumberGenerator rng, Action<Mesh, Transform3D, Color> emit) =>
        Model(name) is { } model && Place(model, Basis.Identity.Scaled(new Vector3(height / model.Height * rng.RandfRange(0.9f, 1.1f), height / model.Height, height / model.Height * rng.RandfRange(0.9f, 1.1f))), rng, emit);

    private static bool Place(NatureModel model, Basis squash, RandomNumberGenerator rng, Action<Mesh, Transform3D, Color> emit)
    {
        var tint = Leaves[rng.RandiRange(0, Leaves.Length - 1)];
        var mix = rng.RandfRange(0.25f, 0.5f);
        var shade = rng.RandfRange(-0.08f, 0.08f);
        foreach (var part in model.Parts)
        {
            var colour = part.Leafy ? tint.Lerp(Leaves[(int)(mix * 10) % Leaves.Length], mix * 0.5f) : part.Colour;
            colour = shade > 0 ? colour.Lightened(shade) : colour.Darkened(-shade);
            emit(part.Mesh, new Transform3D(squash, Vector3.Zero) * part.At, colour);
        }

        return true;
    }

    private static string Pick(RandomNumberGenerator rng, params string[] names) => names[rng.RandiRange(0, names.Length - 1)];

    // A model for a kind of tree, chosen at random among those that suit it: near the camp the MegaKit's
    // textured trees (gnarled ones for the tall), further back the Nature Kit's lighter ones; palms are
    // the Nature Kit's everywhere.
    private static string? ModelFor(TreeKind kind, bool near, RandomNumberGenerator rng) => (kind, near) switch
    {
        (TreeKind.Broadleaf, true) when rng.Randf() < 0.85f => Pick(rng, "CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4", "CommonTree_5"),
        (TreeKind.Emergent, true) => Pick(rng, "TwistedTree_1", "TwistedTree_2", "TwistedTree_3", "TwistedTree_4", "TwistedTree_5"),
        _ => ModelFor(kind, rng),
    };

    private static string? ModelFor(TreeKind kind, RandomNumberGenerator rng) => kind switch
    {
        TreeKind.Palm => Pick(rng, "tree_palm", "tree_palmBend", "tree_palmShort", "tree_palmTall", "tree_palmDetailedShort", "tree_palmDetailedTall"),
        TreeKind.Broadleaf => Pick(rng, "tree_default", "tree_default_dark", "tree_detailed", "tree_detailed_dark", "tree_fat", "tree_fat_darkh",
            "tree_oak", "tree_oak_dark", "tree_simple", "tree_simple_dark", "tree_small", "tree_small_dark", "tree_thin", "tree_thin_dark"),
        TreeKind.Emergent => Pick(rng, "tree_plateau", "tree_plateau_dark", "tree_tall", "tree_tall_dark"),
        _ => null,
    };

    // How tall each kind stands, metres.
    private static float HeightOf(TreeKind kind, RandomNumberGenerator rng) => kind switch
    {
        TreeKind.Palm => rng.RandfRange(4.5f, 7.5f),
        TreeKind.Emergent => rng.RandfRange(8f, 11f),
        _ => rng.RandfRange(4f, 6.5f),
    };

    // The kinds of tree on this coast, and the undergrowth.
    private enum TreeKind { Broadleaf, Palm, Emergent, Mangrove }

    private static readonly Color[] Leaves =
    [
        new(0.2f, 0.36f, 0.16f), new(0.13f, 0.26f, 0.12f), new(0.3f, 0.41f, 0.15f), new(0.14f, 0.3f, 0.2f), new(0.27f, 0.32f, 0.13f),
    ];

    private static readonly Color PalmLeaf = new(0.33f, 0.45f, 0.17f);
    private static readonly Color PalmBark = new(0.48f, 0.4f, 0.3f);

    // Which kind grows where: palms along the sand's edge, broadleaf behind, tall trees standing over them
    // further in; mangroves in the wet. By the site's lie of the land.
    private TreeKind KindAt(float depth, RandomNumberGenerator rng)
    {
        var roll = rng.Randf();
        var front = depth < 2.5f;
        return _ground switch
        {
            "mangrove" => front || roll < 0.6f ? TreeKind.Mangrove : roll < 0.9f ? TreeKind.Broadleaf : TreeKind.Palm,
            "dunes" => roll < (front ? 0.75f : 0.45f) ? TreeKind.Palm : roll < 0.95f ? TreeKind.Broadleaf : TreeKind.Emergent,
            "river" => roll < (front ? 0.15f : 0.05f) ? TreeKind.Palm : roll < (depth > 4 ? 0.7f : 0.85f) ? TreeKind.Broadleaf : TreeKind.Emergent,
            "headland" => roll < (front ? 0.5f : 0.25f) ? TreeKind.Palm : roll < 0.92f ? TreeKind.Broadleaf : TreeKind.Emergent,
            _ => roll < (front ? 0.4f : 0.15f) ? TreeKind.Palm : roll < (depth > 4 ? 0.8f : 0.9f) ? TreeKind.Broadleaf : TreeKind.Emergent,
        };
    }

    // A tree of a kind, its parts handed to emit (mesh, where, colour), so the same tree can go into a node
    // of its own or into the batched forest. Returns its height, for the stump and sapling left after it.
    private static float Grow(TreeKind kind, float scale, bool near, RandomNumberGenerator rng, INatureSink sink)
    {
        // A model from the kits, if they are there.
        if (ModelFor(kind, near, rng) is { } name)
        {
            var tall = HeightOf(kind, rng) * scale;
            if (Place(name, tall, rng, sink))
            {
                return tall;
            }
        }

        Action<Mesh, Transform3D, Color> emit = sink.Coloured;

        var leaves = Leaves[rng.RandiRange(0, Leaves.Length - 1)].Lightened(rng.RandfRange(-0.06f, 0.08f));
        switch (kind)
        {
            case TreeKind.Palm:
            {
                // A trunk that leans and curves, in segments, and a head of drooping fronds.
                var height = (4f + rng.Randf() * 3f) * scale;
                var lean = rng.RandfRange(8f, 24f);
                var turn = rng.RandfRange(0, 360);
                var basis = new Basis(Vector3.Up, Mathf.DegToRad(turn));
                var at = Vector3.Zero;
                const int segments = 5;
                for (var i = 0; i < segments; i++)
                {
                    var tilt = Mathf.DegToRad(lean * (i + 1) / segments);
                    var direction = basis * new Vector3(0, Mathf.Cos(tilt), Mathf.Sin(tilt));
                    var length = height / segments;
                    var segment = new Basis(Vector3.Right, tilt);
                    emit(new CylinderMesh { TopRadius = 0.11f * scale, BottomRadius = (0.17f - 0.012f * i) * scale, Height = length * 1.05f, RadialSegments = 6 },
                        new Transform3D(basis * segment, at + direction * length / 2), PalmBark.Darkened(0.04f * (i % 2)));
                    at += direction * length;
                }

                var fronds = 7 + rng.RandiRange(0, 2);
                for (var i = 0; i < fronds; i++)
                {
                    var around = new Basis(Vector3.Up, Mathf.DegToRad(turn + i * 360f / fronds + rng.RandfRange(-12, 12)));
                    var droop = new Basis(Vector3.Right, Mathf.DegToRad(rng.RandfRange(18, 42)));
                    var frond = around * droop;
                    var length = (1.9f + rng.Randf() * 0.8f) * scale;
                    emit(new BoxMesh { Size = new Vector3(0.5f * scale, 0.03f, length) }, new Transform3D(frond, at + frond * new Vector3(0, 0, length / 2)),
                        PalmLeaf.Darkened(rng.Randf() * 0.15f));
                }

                emit(new SphereMesh { Radius = 0.22f * scale, Height = 0.36f * scale, RadialSegments = 6, Rings = 3 }, new Transform3D(Basis.Identity, at - new Vector3(0, 0.1f, 0)), Bark);
                return height;
            }

            case TreeKind.Emergent:
            {
                // Tall and straight, a broad flat crown standing over the rest.
                var height = (7f + rng.Randf() * 3f) * scale;
                emit(new CylinderMesh { TopRadius = 0.16f * scale, BottomRadius = 0.34f * scale, Height = height, RadialSegments = 7 }, new Transform3D(Basis.Identity, new Vector3(0, height / 2, 0)), Bark.Lightened(0.08f));
                for (var i = 0; i < 3; i++)
                {
                    var r = (1.6f + rng.Randf() * 0.8f) * scale;
                    var off = new Vector3(rng.RandfRange(-1.2f, 1.2f), rng.RandfRange(-0.3f, 0.4f), rng.RandfRange(-1.2f, 1.2f)) * scale;
                    emit(new SphereMesh { Radius = r, Height = r * 0.8f, RadialSegments = 9, Rings = 4 }, new Transform3D(Basis.Identity, new Vector3(0, height, 0) + off), leaves.Darkened(0.05f * i));
                }

                return height;
            }

            case TreeKind.Mangrove:
            {
                // Low, on arching prop roots, dense and dark.
                var height = (2.2f + rng.Randf() * 1.2f) * scale;
                var roots = 4 + rng.RandiRange(0, 2);
                for (var i = 0; i < roots; i++)
                {
                    var around = new Basis(Vector3.Up, Mathf.DegToRad(i * 360f / roots + rng.RandfRange(-15, 15)));
                    var splay = new Basis(Vector3.Right, Mathf.DegToRad(32));
                    var root = around * splay;
                    emit(new CylinderMesh { TopRadius = 0.04f * scale, BottomRadius = 0.05f * scale, Height = 1.2f * scale, RadialSegments = 5 },
                        new Transform3D(root, new Vector3(0, 0.5f * scale, 0) + around * new Vector3(0, 0, 0.32f * scale)), Bark.Darkened(0.1f));
                }

                emit(new CylinderMesh { TopRadius = 0.09f * scale, BottomRadius = 0.13f * scale, Height = height - 0.8f * scale, RadialSegments = 6 },
                    new Transform3D(Basis.Identity, new Vector3(0, 0.8f * scale + (height - 0.8f * scale) / 2, 0)), Bark.Darkened(0.05f));
                for (var i = 0; i < 2; i++)
                {
                    var r = (1.1f + rng.Randf() * 0.5f) * scale;
                    emit(new SphereMesh { Radius = r, Height = r * 1.1f, RadialSegments = 8, Rings = 4 },
                        new Transform3D(Basis.Identity, new Vector3(rng.RandfRange(-0.4f, 0.4f) * scale, height + 0.2f * i, rng.RandfRange(-0.4f, 0.4f) * scale)), Leaves[1].Darkened(0.05f * i));
                }

                return height;
            }

            default:
            {
                // A broadleaf: a trunk leaning a little, a crown of several masses, not one ball.
                var height = (3.2f + rng.Randf() * 2.5f) * scale;
                var way = rng.RandfRange(0, Mathf.Tau);
                var lean = new Basis(new Vector3(Mathf.Cos(way), 0, Mathf.Sin(way)), Mathf.DegToRad(rng.RandfRange(0, 7)));
                emit(new CylinderMesh { TopRadius = 0.12f * scale, BottomRadius = 0.23f * scale, Height = height, RadialSegments = 6 }, new Transform3D(lean, lean * new Vector3(0, height / 2, 0)), Bark);
                var top = lean * new Vector3(0, height, 0);
                var masses = 2 + rng.RandiRange(0, 2);
                for (var i = 0; i < masses; i++)
                {
                    var r = (1.0f + rng.Randf() * 0.8f) * scale * (i == 0 ? 1.15f : 0.85f);
                    var off = i == 0 ? Vector3.Zero : new Vector3(rng.RandfRange(-0.9f, 0.9f), rng.RandfRange(-0.5f, 0.4f), rng.RandfRange(-0.9f, 0.9f)) * scale;
                    emit(new SphereMesh { Radius = r, Height = r * 1.4f, RadialSegments = 8, Rings = 4 }, new Transform3D(Basis.Identity, top + off + new Vector3(0, r * 0.3f, 0)),
                        leaves.Darkened(0.06f * i).Lightened(i == masses - 1 ? 0.05f : 0));
                }

                return height;
            }
        }
    }

    // Undergrowth: a shrub, or a fern's fronds, low under the trees and at their edge.
    private static void Undergrowth(Vector3 at, RandomNumberGenerator rng, MeshBatcher batch)
    {
        // Bushes, ferns, broad-leaved plants and grass from the MegaKit (the Nature Kit's if it is not there);
        // now and then flowers or fungi.
        var origin = new Transform3D(new Basis(Vector3.Up, rng.RandfRange(0, Mathf.Tau)), at);
        var roll = rng.Randf();
        var (name, size) = roll switch
        {
            < 0.03f => (Pick(rng, "Mushroom_Common", "Mushroom_Laetiporus", "mushroom_tanGroup"), rng.RandfRange(0.2f, 0.35f)),
            < 0.05f => (Pick(rng, "Flower_3_Group", "Flower_4_Group", "Bush_Common_Flowers"), rng.RandfRange(0.25f, 0.45f)),
            < 0.35f => (Pick(rng, "Bush_Common", "plant_bushLarge", "plant_bushDetailed"), rng.RandfRange(0.8f, 1.6f)),
            < 0.55f => (Pick(rng, "Fern_1", "Plant_1_Big", "Plant_7_Big"), rng.RandfRange(0.7f, 1.3f)),
            < 0.75f => (Pick(rng, "Plant_1", "Plant_7", "plant_flatTall"), rng.RandfRange(0.5f, 0.9f)),
            _ => (Pick(rng, "Grass_Wispy_Tall", "Grass_Wispy_Short", "Grass_Common_Tall", "Clover_2"), rng.RandfRange(0.3f, 0.7f)),
        };
        if (Place(name, size, rng, new IntoBatch(batch, origin)))
        {
            return;
        }

        var leaves = Leaves[rng.RandiRange(0, Leaves.Length - 1)].Lightened(rng.RandfRange(0, 0.1f));
        if (rng.Randf() < 0.5f)
        {
            var r = 0.35f + rng.Randf() * 0.5f;
            batch.AddColoured(new SphereMesh { Radius = r, Height = r * 1.1f, RadialSegments = 7, Rings = 3 }, new Transform3D(Basis.Identity, at + new Vector3(0, r * 0.35f, 0)), leaves);
            return;
        }

        var turn = rng.RandfRange(0, 180);
        for (var i = 0; i < 5; i++)
        {
            var frond = new Basis(Vector3.Up, Mathf.DegToRad(turn + i * 72)) * new Basis(Vector3.Right, Mathf.DegToRad(-35));
            batch.AddColoured(new BoxMesh { Size = new Vector3(0.22f, 0.02f, 0.9f) }, new Transform3D(frond, at + frond * new Vector3(0, 0, 0.42f) + new Vector3(0, 0.05f, 0)), leaves.Lightened(0.06f));
        }
    }

    // The forest: the grid's trees, and more behind them and along both flanks, of the site's kinds, with
    // undergrowth under them and fallen trunks here and there.
    private void BuildForest()
    {
        var batch = new MeshBatcher();
        var rng = new RandomNumberGenerator { Seed = (ulong)_seed + 1519 };
        void Batched(TreeKind kind, Vector3 at, float scale, bool near)
        {
            var origin = new Transform3D(new Basis(Vector3.Up, rng.RandfRange(0, Mathf.Tau)), at);
            Grow(kind, scale, near, rng, new IntoBatch(batch, origin));
        }

        float Ground(float x, float z) => HeightAt(Mathf.Clamp(x, 1, _location.Width - 2), z);

        // The forest's edge, tree by tree, so the ones felled for poles can be taken away (R7): a stump
        // left in each one's place, then scrub, brush and a sapling as it grows back.
        _edgeTrees.Clear();
        var middle = new Vector3(_location.Width / 2f, 0, 17);
        var edge = new List<(Vector3 At, int X, int Y)>();
        for (var y = 10; y < _location.Height - 1; y++)
        {
            for (var x = 1; x < _location.Width - 1; x++)
            {
                if (_location.Rows[y][x] == LocationDefinition.Blocked)
                {
                    var at = new Vector3(x + rng.RandfRange(-0.3f, 0.3f), 0, y + rng.RandfRange(-0.3f, 0.3f));
                    at.Y = Ground(at.X, at.Z);
                    edge.Add((at, x, y));
                }
            }
        }

        foreach (var (at, cx, cy) in edge.OrderBy(e => (e.At - middle).LengthSquared()))
        {
            var kind = KindAt(cy - EdgeRow(cx), rng);
            var tree = new Node3D { Position = at, RotationDegrees = new Vector3(0, rng.RandfRange(0, 360), 0) };
            AddChild(tree);
            var scale = 0.85f + rng.Randf() * 0.35f;
            var height = Grow(kind, scale, true, rng, new IntoNode(tree, Plain));
            var crown = 1.2f * scale;
            var leaves = Leaves[rng.RandiRange(0, Leaves.Length - 1)];
            var stump = new Node3D { Position = at, RotationDegrees = new Vector3(0, rng.RandfRange(0, 360), 0), Visible = false };
            AddChild(stump);
            if (!Place(Pick(rng, "stump_old", "stump_round", "stump_roundDetailed"), 0.4f, rng, (mesh, where, colour) => stump.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = Plain(colour), Transform = where })))
            {
                Add(stump, new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.25f, Height = 0.35f, RadialSegments = 6 }, Bark.Lightened(0.15f), new Vector3(0, 0.17f, 0));
            }
            var bush = Add(this, new SphereMesh { Radius = 0.55f, Height = 0.6f, RadialSegments = 8, Rings = 4 }, leaves.Lightened(0.12f), at + new Vector3(0.25f, 0.2f, 0.1f));
            bush.Visible = false;
            var sapling = new Node3D { Position = at, Visible = false };
            AddChild(sapling);
            var young = Math.Min(height, 5f) * 0.4f;
            Add(sapling, new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.07f, Height = young, RadialSegments = 5 }, Bark, new Vector3(0, young / 2, 0));
            Add(sapling, new SphereMesh { Radius = crown * 0.45f, Height = crown * 0.7f, RadialSegments = 8, Rings = 4 }, leaves.Lightened(0.08f), new Vector3(0, young + crown * 0.15f, 0));
            _edgeTrees.Add(new EdgeTree(tree, stump, bush, sapling, cx, cy));

            // Undergrowth at its foot, inside the forest only, so nothing grows where the camp may build.
            if (cy > EdgeRow(cx) && rng.Randf() < 0.6f)
            {
                Undergrowth(at + new Vector3(rng.RandfRange(-0.5f, 0.5f), 0, rng.RandfRange(-0.5f, 0.5f)), rng, batch);
            }
        }

        // Behind and on the flanks, the forest goes on, thicker and thinner, taller in places.
        var thickness = _ground switch { "dunes" => 0.55f, "headland" => 0.7f, _ => 0.9f };
        void Stand(float x, float z, float depth)
        {
            if (Fbm(x * 0.12f + 5, z * 0.12f) < 1.05f - thickness - 0.25f)
            {
                return;
            }

            var at = new Vector3(x + rng.RandfRange(-0.7f, 0.7f), 0, z + rng.RandfRange(-0.6f, 0.6f));
            at.Y = Ground(at.X, at.Z);
            var scale = 0.95f + 0.45f * Fbm(x * 0.08f, z * 0.08f) + rng.Randf() * 0.15f;
            Batched(KindAt(depth, rng), at, scale, depth < 9 && at.X > -8 && at.X < _location.Width + 8);
            if (rng.Randf() < 0.45f)
            {
                Undergrowth(at + new Vector3(rng.RandfRange(-0.8f, 0.8f), 0, rng.RandfRange(-0.8f, 0.8f)), rng, batch);
            }
        }

        for (var z = _location.Height - 1f; z < _location.Height + 22; z += 1.6f)
        {
            for (var x = -40f; x < _location.Width + 40; x += 1.7f)
            {
                Stand(x, z, 6 + z - _location.Height);
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
                        Stand(x, z, Mathf.Max(0, z - 16) + k * 0.3f);
                    }
                }
            }
        }

        // A fallen trunk or two, mossed, inside the trees.
        for (var i = 0; i < 4; i++)
        {
            var at = new Vector3(rng.RandfRange(2, _location.Width - 3), 0, _location.Height + rng.RandfRange(0, 6));
            at.Y = Ground(at.X, at.Z);
            var lying = new Transform3D(new Basis(Vector3.Up, rng.RandfRange(0, Mathf.Tau)), at);
            if (Place("log_large", 0.5f, rng, (mesh, where, colour) => batch.AddColoured(mesh, lying * where, colour.Lerp(Leaves[0], 0.15f))))
            {
                continue;
            }

            at.Y += 0.2f;
            batch.AddColoured(new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.3f, Height = 3.5f + rng.Randf() * 2, RadialSegments = 7 },
                new Transform3D(new Basis(Vector3.Up, rng.RandfRange(0, Mathf.Tau)) * new Basis(Vector3.Forward, Mathf.Pi / 2), at), Bark.Lerp(Leaves[0], 0.3f));
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

        // The palisade (R8): stakes round the camp, set from the gate by the landing round both ways, as far as
        // it has got; the gate left open on the sea side.
        var palisade = (float)camp.Work(CampWorks.Palisade);
        if (palisade > 0)
        {
            var batch = new MeshBatcher();
            const float x0 = 3f, x1 = 33f, y0 = 4f, y1 = 17.5f, gate = 2f, gateX = 18f;
            var ring = new List<Vector2>();
            // From the gate's west post west along the front, up the west side, along the back, down the east side,
            // and back along the front to the gate's east post.
            void Run(Vector2 from, Vector2 to)
            {
                var n = Math.Max(1, (int)(from.DistanceTo(to) / 0.45f));
                for (var i = 0; i < n; i++)
                {
                    ring.Add(from.Lerp(to, i / (float)n));
                }
            }

            Run(new Vector2(gateX - gate, y0), new Vector2(x0, y0));
            Run(new Vector2(x0, y0), new Vector2(x0, y1));
            Run(new Vector2(x0, y1), new Vector2(x1, y1));
            Run(new Vector2(x1, y1), new Vector2(x1, y0));
            Run(new Vector2(x1, y0), new Vector2(gateX + gate, y0));
            var shown = (int)(ring.Count * Math.Min(1, palisade));
            var stake = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.09f, Height = 2.2f, RadialSegments = 5 };
            for (var i = 0; i < shown; i++)
            {
                // Laid round from both ends at once, so the two halves meet at the back.
                var k = i % 2 == 0 ? i / 2 : ring.Count - 1 - i / 2;
                var p = ring[k];
                var height = 2f + 0.25f * Mathf.Sin(k * 2.3f);
                batch.AddColoured(stake, new Transform3D(Basis.Identity.Scaled(new Vector3(1, height / 2.2f, 1)), new Vector3(p.X, HeightAt(p.X, p.Y) + height / 2, p.Y)), Pole.Darkened(0.1f + 0.1f * ((k * 7) % 3) / 2f));
            }

            batch.Commit(_works);
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

    private Node3D? _canoes;

    /// <summary>A people's canoes drawn up on the beach while they are at the camp (R8); null for none.</summary>
    public void ShowVisitors(string? community)
    {
        if (_canoes is null)
        {
            _canoes = new Node3D { Name = "Canoes" };
            AddChild(_canoes);
            // Two dugouts, long and narrow, drawn up on the sand beside the landing, a paddle across each.
            foreach (var (x, angle) in new[] { (12.5f, 14f), (14.2f, 6f) })
            {
                var canoe = new Node3D { Position = new Vector3(x, HeightAt(x, 1.2f), 1.2f), RotationDegrees = new Vector3(0, angle, 0) };
                Add(canoe, new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.32f, Height = 5.2f, RadialSegments = 8 }, Hull.Darkened(0.25f), new Vector3(0, 0.12f, 0), new Vector3(90, 0, 0));
                Add(canoe, new BoxMesh { Size = new Vector3(0.5f, 0.08f, 4.6f) }, Hull.Darkened(0.5f), new Vector3(0, 0.4f, 0));
                Add(canoe, new BoxMesh { Size = new Vector3(1.3f, 0.04f, 0.1f) }, Pole.Lightened(0.1f), new Vector3(0, 0.46f, 0.8f), new Vector3(0, 25, 0));
                _canoes.AddChild(canoe);
            }
        }

        _canoes.Visible = community is not null;
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

    // Paths only where feet have gone, and ground cleared or greening over: painted into one soft layer on
    // the ground, a few texels to the metre, so a path wanders as a track does and fades at its edges,
    // instead of being laid in squares. Each worn cell is a soft splash, a little off its cell's centre, and
    // each worn cell is joined to its worn neighbours by a band of splashes, the narrower where less worn.
    private const int PaintPerCell = 8;

    // A box blur across and then down, radius r texels.
    private static float[] Blur(float[] from, int w, int h, int r)
    {
        var across = new float[from.Length];
        for (var y = 0; y < h; y++)
        {
            var sum = 0f;
            for (var x = -r; x < w + r; x++)
            {
                sum += x + r < w && x + r >= 0 ? from[y * w + x + r] : 0;
                sum -= x - r - 1 >= 0 && x - r - 1 < w ? from[y * w + x - r - 1] : 0;
                if (x >= 0 && x < w)
                {
                    across[y * w + x] = sum / (2 * r + 1);
                }
            }
        }

        var down = new float[from.Length];
        for (var x = 0; x < w; x++)
        {
            var sum = 0f;
            for (var y = -r; y < h + r; y++)
            {
                sum += y + r < h && y + r >= 0 ? across[(y + r) * w + x] : 0;
                sum -= y - r - 1 >= 0 && y - r - 1 < h ? across[(y - r - 1) * w + x] : 0;
                if (y >= 0 && y < h)
                {
                    down[y * w + x] = sum / (2 * r + 1);
                }
            }
        }

        return down;
    }

    private void PaintGround(CampState camp, SimulationRunner runner, bool wet, List<Footprint> builtOn)
    {
        var (width, height) = (_location.Width, _location.Height);
        var (pw, ph) = (width * PaintPerCell, height * PaintPerCell);
        var trod = new float[pw * ph];
        var bare = new float[pw * ph];
        var green = new float[pw * ph];
        void Splash(float[] into, float cx, float cz, float radius, float strength)
        {
            var (px, pz) = ((cx + 0.5f) * PaintPerCell, (cz + 0.5f) * PaintPerCell);
            var r = radius * PaintPerCell;
            for (var y = Math.Max(0, (int)(pz - r - 1)); y < Math.Min(ph, (int)(pz + r + 2)); y++)
            {
                for (var x = Math.Max(0, (int)(px - r - 1)); x < Math.Min(pw, (int)(px + r + 2)); x++)
                {
                    var d = new Vector2(x + 0.5f - px, y + 0.5f - pz).Length() / r;
                    if (d < 1)
                    {
                        var v = strength * (1 - d * d) * (1 - d * d);
                        into[y * pw + x] = Mathf.Max(into[y * pw + x], v);
                    }
                }
            }
        }

        // Where a splash falls in its cell: off the centre by a little, the same each time.
        Vector2 Off(int x, int y) => new(Noise(x * 3.1f + 0.5f, y * 3.1f) - 0.5f, Noise(x * 3.1f + 9.5f, y * 3.1f) - 0.5f);
        var worn = camp.Trodden.Select(t => (Cell: CampState.Cell(t.Key), Wear: (float)runner.PathWear(t.Value)))
            .Where(t => t.Wear > 0).ToDictionary(t => t.Cell, t => t.Wear);
        foreach (var ((x, y), wear) in worn)
        {
            var at = new Vector2(x, y) + Off(x, y) * 0.5f;
            Splash(trod, at.X, at.Y, 0.45f + 0.35f * wear, 0.55f + 0.45f * wear);
            foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (1, 1), (-1, 1) })
            {
                if (!worn.TryGetValue((x + dx, y + dy), out var next)
                    || (dx != 0 && dy != 0 && (worn.ContainsKey((x + dx, y)) || worn.ContainsKey((x, y + dy)))))
                {
                    continue;
                }

                var to = new Vector2(x + dx, y + dy) + Off(x + dx, y + dy) * 0.5f;
                var both = Mathf.Min(wear, next);
                for (var t = 0.2f; t < 1f; t += 0.2f)
                {
                    var p = at.Lerp(to, t);
                    Splash(trod, p.X, p.Y, 0.4f + 0.3f * both, 0.5f + 0.45f * both);
                }
            }
        }

        // Ground given up greening over: bare earth, then scrub and grass coming through it.
        foreach (var (cellKey, growing) in camp.Regrowing.Where(r => !r.Value.Tree))
        {
            var (gx, gy) = CampState.Cell(cellKey);
            if (builtOn.Any(g => g.Contains(gx, gy)))
            {
                continue;
            }

            var stage = runner.Growth(growing);
            Splash(bare, gx, gy, 0.8f, 1f);
            if (stage != GrowthStage.Bare)
            {
                Splash(green, gx, gy, 0.75f, stage == GrowthStage.Scrub ? 0.4f : 0.7f);
            }
        }

        // Blurred, so the grid's steps round off into curves, then brought back up to strength.
        trod = Blur(Blur(trod, pw, ph, 4), pw, ph, 4);
        for (var i = 0; i < trod.Length; i++)
        {
            var v = Mathf.Clamp((trod[i] - 0.06f) / 0.34f, 0, 1);
            trod[i] = v * v * (3 - 2 * v);
        }

        var image = Image.CreateEmpty(pw, ph, false, Image.Format.Rgba8);
        var trodden = (_ground == "mangrove" ? Mud : new Color(0.62f, 0.5f, 0.34f)) * (wet ? new Color(0.78f, 0.76f, 0.72f) : Colors.White);
        for (var y = 0; y < ph; y++)
        {
            for (var x = 0; x < pw; x++)
            {
                var i = y * pw + x;
                // The edges broken a little, as feet and weather leave them.
                var rough = 0.75f + 0.5f * Noise(x * 0.45f, y * 0.45f);
                var (t, b, g) = (Mathf.Clamp(trod[i] * rough, 0, 1), bare[i], green[i]);
                var alpha = Mathf.Clamp(Mathf.Max(t * 0.95f, b * 0.95f), 0, 1);
                if (alpha <= 0.01f)
                {
                    image.SetPixel(x, y, new Color(0, 0, 0, 0));
                    continue;
                }

                var colour = Cleared.Lerp(Green.Darkened(0.15f), g * rough * 0.8f);
                colour = b > 0 ? colour.Lerp(trodden, t / (t + b)) : trodden.Darkened(0.06f * (rough - 1));
                image.SetPixel(x, y, new Color(colour, alpha));
            }
        }

        image.GenerateMipmaps();
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        const float step = 0.5f;
        for (var z = -0.5f; z < height - 0.5f; z += step)
        {
            for (var x = -0.5f; x < width - 0.5f; x += step)
            {
                var corners = new[] { new Vector2(x, z), new Vector2(x + step, z), new Vector2(x + step, z + step), new Vector2(x, z + step) };
                foreach (var i in new[] { 0, 1, 2, 0, 2, 3 })
                {
                    var c = corners[i];
                    tool.SetUV(new Vector2((c.X + 0.5f) / width, (c.Y + 0.5f) / height));
                    tool.SetNormal(Vector3.Up);
                    tool.AddVertex(new Vector3(c.X, HeightAt(Mathf.Clamp(c.X, 1, width - 2), c.Y) + 0.012f, c.Y));
                }
            }
        }

        _settlement.AddChild(new MeshInstance3D
        {
            Name = "Paint",
            Mesh = tool.Commit(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = ImageTexture.CreateFromImage(image),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 1f,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
                RenderPriority = -1,
            },
        });
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
        // Ground given up (a building abandoned or blown down) greening over: the earth and grass painted
        // (PaintGround), scrub and then brush coming up through it.
        foreach (var (cellKey, growing) in camp.Regrowing.Where(r => !r.Value.Tree))
        {
            var (gx, gy) = CampState.Cell(cellKey);
            if (grounds.Any(g => g.Ground.Contains(gx, gy)))
            {
                continue;
            }

            var at = new Vector3(gx, HeightAt(gx, gy), gy);
            var stage = runner.Growth(growing);
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

        PaintGround(camp, runner, wet, grounds.Select(g => g.Ground).ToList());
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
            "building.landing" => (3.8f, 2.8f, 1f),
            "building.well" => (1.6f, 1.6f, 1.5f),
            "building.house" => (4.2f, 3.2f, 2.5f),
            "building.smokehouse" => (2.4f, 2.4f, 1.8f),
            "building.chapel" => (3.2f, 4.2f, 3.0f),
            "building.trading_house" => (4.2f, 3.2f, 2.2f),
            "building.salt_pans" => (4.4f, 2.4f, 0.9f),
            "building.forge" => (3.2f, 3.2f, 2.0f),
            "building.lookout" => (1.6f, 1.6f, 4.0f),
            "building.boat_shed" => (4.4f, 3.4f, 2.4f),
            "building.magazine" => (2.4f, 2.4f, 1.6f),
            "building.battery" => (3.6f, 2.6f, 0.9f),
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
        if (!building.Landing && (clear > 0 || phase > 0))
        {
            Add(parent, new BoxMesh { Size = new Vector3(w + 0.8f, 0.02f, d + 0.8f) }, Cleared, at + new Vector3(0, 0.01f, 0));
            foreach (var (cx, cz) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                Add(parent, new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.5f }, Pole, at + new Vector3(cx * (w / 2 + 0.3f), 0.25f, cz * (d / 2 + 0.3f)));
            }
        }

        if (building.Water)
        {
            // A well: the pit going down, its spoil heaped beside it; then lined with wattle to a curb, and
            // a pair of posts and a beam to draw from.
            var dug = Reached("task.dig_well");
            if (dug > 0 || phase > phases.IndexOf("task.dig_well"))
            {
                Add(parent, new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.55f, Height = 0.02f, RadialSegments = 14 }, Earth.Darkened(0.55f), at + new Vector3(0, 0.025f, 0));
                Add(parent, new SphereMesh { Radius = 0.3f + 0.35f * dug, Height = 0.25f + 0.4f * dug, RadialSegments = 8, Rings = 4 }, Sand.Darkened(0.15f), at + new Vector3(0.95f, 0.05f, 0.4f));
            }

            var lined = Reached("task.line_well");
            if (lined > 0)
            {
                var curb = 0.15f + 0.45f * lined;
                Add(parent, new CylinderMesh { TopRadius = 0.62f, BottomRadius = 0.66f, Height = curb, RadialSegments = 14 }, Brush, at + new Vector3(0, curb / 2, 0));
                Add(parent, new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.02f, RadialSegments = 14 }, new Color(0.12f, 0.16f, 0.18f), at + new Vector3(0, curb + 0.01f, 0));
            }

            if (lined >= 1)
            {
                foreach (var side in new[] { -1, 1 })
                {
                    Add(parent, new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.06f, Height = 1.5f }, Pole, at + new Vector3(side * 0.75f, 0.75f, 0));
                }

                Add(parent, new BoxMesh { Size = new Vector3(1.7f, 0.1f, 0.1f) }, Pole, at + new Vector3(0, 1.45f, 0));
                Add(parent, new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.11f, Height = 0.22f, RadialSegments = 8 }, Cask, at + new Vector3(0.2f, 0.95f, 0));
            }

            return;
        }

        if (building.Landing)
        {
            // A landing: stones bedded in rows from the water's edge up, then a planked staging on posts over
            // the seaward end, where the boat comes alongside.
            var laid = Reached("task.lay_stones");
            var rows = (int)Math.Ceiling(6 * laid);
            for (var row = 0; row < rows; row++)
            {
                for (var k = 0; k < 7; k++)
                {
                    var jitter = ((row * 7 + k) * 37 % 11) / 11f - 0.5f;
                    Add(parent, new BoxMesh { Size = new Vector3(0.48f, 0.16f, 0.42f) }, Stone.Lightened(0.08f * jitter),
                        at + new Vector3(-w / 2 + 0.3f + k * (w - 0.6f) / 6f + 0.06f * jitter, 0.06f, -d / 2 + 0.25f + row * (d - 0.5f) / 5f), new Vector3(0, 20 * jitter, 0));
                }
            }

            var staged = Reached("task.build_staging");
            var planks = (int)Math.Ceiling(8 * staged);
            // The staging runs out from the hard over the water, narrower than it, on posts driven in the sand.
            const float span = 1.2f;
            var out0 = -d / 2 - 1.6f;
            if (staged > 0)
            {
                foreach (var pz in new[] { out0 + 0.1f, -d / 2, -d / 2 + 0.9f })
                {
                    foreach (var px in new[] { -span / 2, span / 2 })
                    {
                        Add(parent, new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.08f, Height = 1.4f }, Pole.Darkened(0.2f), at + new Vector3(px, -0.15f, pz));
                    }
                }
            }

            for (var i = 0; i < planks; i++)
            {
                Add(parent, new BoxMesh { Size = new Vector3(span + 0.3f, 0.06f, 0.3f) }, Pole.Lightened(0.12f), at + new Vector3(0, 0.5f, out0 + 0.15f + i * 0.32f));
            }

            return;
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
