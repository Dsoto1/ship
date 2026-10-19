using Godot;

namespace Expedition.Game;

/// <summary>
/// The harbour beyond the quay (PORT_EXPERIENCE §6 Tier C, §19 "several background vessels"): ships lying
/// at anchor with hands working on their decks, and boats rowing between the piers and the ships by day.
/// Presentation only, like <see cref="PortCrowd"/>: nothing here is simulated or saved. Ships swing and
/// roll a little at their anchors; the boats keep game time, so they stop when the game is paused.
/// Built from code until the asset pipeline replaces it (D-021).
/// </summary>
public partial class HarbourView : Node3D
{
    private static readonly Color Pitch = new(0.2f, 0.15f, 0.11f);
    private static readonly Color Strake = new(0.32f, 0.24f, 0.16f);
    private static readonly Color Weathered = new(0.42f, 0.36f, 0.28f);
    private static readonly Color Deck = new(0.55f, 0.45f, 0.32f);
    private static readonly Color Spar = new(0.3f, 0.22f, 0.15f);
    private static readonly Color Line = new(0.3f, 0.25f, 0.18f);
    private static readonly Color Furled = new(0.7f, 0.66f, 0.56f);

    private readonly List<Anchored> _ships = [];
    private readonly List<Boat> _boats = [];
    private StandardMaterial3D _hull = null!;
    private StandardMaterial3D _lamp = null!;
    private float _time;

    private sealed class Anchored
    {
        public Node3D Node = null!;
        public Vector3 At;
        public float Yaw;
        public float Seed;
        public MultiMesh Hands = null!;
        public List<(Vector3 At, float Yaw, int Pose)> Places = [];
        public float Phase;
    }

    private sealed class Boat
    {
        public Node3D Node = null!;
        public Vector3[] Route = [];
        public float Along;
        public float Wait;
        public bool Out = true;
        public MultiMesh Rowers = null!;
        public Node3D[] Oars = [];
        public float Stroke;
    }

    /// <summary>Lays out the ships at anchor and the boats.</summary>
    public void Build()
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _ships.Clear();
        _boats.Clear();
        _hull = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.85f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        _lamp = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.8f, 0.45f), EmissionEnabled = true, Emission = new Color(1f, 0.7f, 0.35f), EmissionEnergyMultiplier = 0f };

        // A nao further out, a caravel off the west pier, a fishing caravel off the yard.
        Ship("nao", new Vector3(30, SeaAndSky.WaterLevel, 66), 0.35f, 24f, 7f);
        Ship("caravel", new Vector3(-6, SeaAndSky.WaterLevel, 56), -0.5f, 17f, 5f);
        Ship("fishing", new Vector3(86, SeaAndSky.WaterLevel, 52), 1.2f, 10f, 3.4f);

        RowingBoat([new Vector3(11.4f, 0, 44.5f), new Vector3(6, 0, 50), new Vector3(-2.5f, 0, 53.4f)]);
        RowingBoat([new Vector3(48.2f, 0, 43f), new Vector3(44, 0, 52), new Vector3(34.5f, 0, 62.4f)]);
    }

    /// <summary>Moves the boats on by <paramref name="minutes"/> of game time and rocks the ships.</summary>
    public void Update(float hour, float minutes, float delta, float darkness)
    {
        _time += delta;
        _lamp.EmissionEnergyMultiplier = darkness * 3f;
        foreach (var ship in _ships)
        {
            ship.Node.Position = ship.At + new Vector3(0, Mathf.Sin(_time * 0.5f + ship.Seed) * 0.07f, 0);
            ship.Node.Rotation = new Vector3(Mathf.Sin(_time * 0.37f + ship.Seed) * 0.012f, ship.Yaw + Mathf.Sin(_time * 0.03f + ship.Seed) * 0.06f,
                Mathf.Sin(_time * 0.53f + ship.Seed) * 0.02f);
            ship.Phase += minutes * 3f;
            for (var i = 0; i < ship.Places.Count; i++)
            {
                var (at, yaw, pose) = ship.Places[i];
                CrowdMesh.Set(ship.Hands, i, at, yaw, 0.95f, ship.Phase + i * 1.7f, 0f, pose, 300 + i * 13 + (int)ship.Seed);
            }

            // Fewer hands on deck at night.
            ship.Hands.VisibleInstanceCount = hour is > 6 and < 20 ? ship.Places.Count : 1;
        }

        var working = hour is > 6 and < 20;
        foreach (var boat in _boats)
        {
            Row(boat, working ? minutes : 0f);
        }
    }

    private void Row(Boat boat, float minutes)
    {
        const float speed = 3.5f;
        var lengths = Enumerable.Range(0, boat.Route.Length - 1).Select(i => boat.Route[i].DistanceTo(boat.Route[i + 1])).ToArray();
        var total = lengths.Sum();
        if (boat.Wait > 0)
        {
            boat.Wait -= minutes;
        }
        else if (minutes > 0)
        {
            boat.Along += speed * minutes;
            boat.Stroke += minutes * 26f;
            if (boat.Along >= total)
            {
                boat.Along = 0;
                boat.Out = !boat.Out;
                boat.Wait = boat.Out ? 40f : 20f;
            }
        }

        // Where along the route, from the pier out or from the ship back.
        var distance = boat.Out ? boat.Along : total - boat.Along;
        if (boat.Wait > 0)
        {
            distance = boat.Out ? 0 : total;
        }

        var segment = 0;
        while (segment < lengths.Length - 1 && distance > lengths[segment])
        {
            distance -= lengths[segment++];
        }

        var a = boat.Route[segment];
        var b = boat.Route[segment + 1];
        var at = a.Lerp(b, Mathf.Clamp(distance / lengths[segment], 0, 1));
        var heading = boat.Out ? b - a : a - b;
        boat.Node.Position = at + new Vector3(0, SeaAndSky.WaterLevel + Mathf.Sin(_time * 0.9f + segment) * 0.04f, 0);
        boat.Node.Rotation = new Vector3(0, Mathf.Atan2(heading.X, heading.Z) - Mathf.Pi / 2f, Mathf.Sin(_time * 0.8f) * 0.02f);

        var rowing = boat.Wait <= 0 && minutes > 0;
        var sweep = rowing ? Mathf.Sin(boat.Stroke) : 0f;
        for (var i = 0; i < boat.Oars.Length; i++)
        {
            var side = i % 2 == 0 ? 1f : -1f;
            boat.Oars[i].Rotation = new Vector3(0, sweep * 0.5f * side, side * (rowing ? 0.25f + Mathf.Cos(boat.Stroke) * 0.12f : 0.4f));
        }

        for (var i = 0; i < 2; i++)
        {
            CrowdMesh.Set(boat.Rowers, i, new Vector3(-0.9f + i * 1.5f, -0.55f, 0), -Mathf.Pi / 2f, 0.9f, boat.Stroke * 2f, 0f, CrowdMesh.Hauling, 410 + i * 7);
        }

        boat.Rowers.VisibleInstanceCount = 2;
    }

    // ---- building

    private void Ship(string kind, Vector3 at, float yaw, float length, float beam)
    {
        var node = new Node3D { Name = "Anchored " + kind, Position = at, Rotation = new Vector3(0, yaw, 0) };
        AddChild(node);
        var batch = new MeshBatcher();
        var deck = kind == "fishing" ? 0.9f : 1.6f;
        Hull(batch, length, beam, deck, kind == "fishing" ? 0.9f : 1.7f);
        var half = length / 2f;
        var places = new List<(Vector3, float, int)>();

        if (kind == "nao")
        {
            // Castles fore and aft, three masts, the courses furled on their yards.
            Castle(batch, -half * 0.62f, half * 0.38f, beam * 0.78f, deck, 1.9f);
            Castle(batch, half * 0.62f, half * 0.24f, beam * 0.6f, deck, 1.4f);
            Mast(batch, new Vector3(half * 0.42f, deck, 0), 12f, beam * 1.25f, square: true);
            Mast(batch, new Vector3(0, deck, 0), 16f, beam * 1.6f, square: true);
            Mast(batch, new Vector3(-half * 0.52f, deck + 1.9f, 0), 8f, beam, square: false);
            Line3(batch, new Vector3(0, deck + 16f, 0), new Vector3(half * 0.42f, deck + 12f, 0));
            Line3(batch, new Vector3(half * 0.42f, deck + 12f, 0), new Vector3(half + 1.5f, deck + 1f, 0));
            batch.AddColoured(new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.16f, Height = 5.5f, RadialSegments = 6, Rings = 1 },
                new Transform3D(Basis.FromEuler(new Vector3(0, 0, -1.2f)), new Vector3(half + 1.6f, deck + 0.9f, 0)), Spar);
            places.Add((new Vector3(1.5f, deck, 1.2f), 0.8f, CrowdMesh.Hauling));
            places.Add((new Vector3(2.4f, deck, 1.0f), 0.8f, CrowdMesh.Hauling));
            places.Add((new Vector3(-4f, deck, -1.4f), 2.4f, CrowdMesh.Walking));
            places.Add((new Vector3(-half * 0.62f, deck + 1.9f, 0.6f), 1.6f, CrowdMesh.Walking));
        }
        else if (kind == "caravel")
        {
            // A raised poop and two lateen masts, the long yards lowered and lashed along the deck.
            Castle(batch, -half * 0.66f, half * 0.34f, beam * 0.8f, deck, 1.3f);
            Mast(batch, new Vector3(half * 0.3f, deck, 0), 11f, half * 1.6f, square: false);
            Mast(batch, new Vector3(-half * 0.25f, deck, 0), 9f, half * 1.3f, square: false);
            places.Add((new Vector3(2f, deck, 0.8f), -1.2f, CrowdMesh.Hauling));
            places.Add((new Vector3(-1f, deck, -0.9f), 0.4f, CrowdMesh.Walking));
        }
        else
        {
            // A small caravel with nets over the rail: hands sorting the catch.
            Mast(batch, new Vector3(half * 0.25f, deck, 0), 7f, half * 1.4f, square: false);
            for (var i = 0; i < 3; i++)
            {
                batch.AddColoured(new BoxMesh { Size = new Vector3(1.2f, 0.25f, 0.5f) }, new Transform3D(Basis.Identity, new Vector3(-1.5f + i * 0.9f, deck + 0.12f, beam / 2f - 0.2f)), new Color(0.35f, 0.33f, 0.28f));
            }

            places.Add((new Vector3(-1f, deck, 0.5f), 1.5f, CrowdMesh.Hauling));
            places.Add((new Vector3(0.4f, deck, -0.3f), 1.9f, CrowdMesh.Hauling));
        }

        // Ensign at the stern, and a lantern lit at night.
        batch.AddColoured(new BoxMesh { Size = new Vector3(0.06f, 4f, 0.06f) }, new Transform3D(Basis.Identity, new Vector3(-half + 0.4f, deck + 2.6f, 0)), Spar);
        batch.AddColoured(new BoxMesh { Size = new Vector3(1.1f, 0.7f, 0.03f) }, new Transform3D(Basis.Identity, new Vector3(-half - 0.15f, deck + 4.2f, 0)),
            kind == "nao" ? new Color(0.55f, 0.15f, 0.13f) : new Color(0.75f, 0.7f, 0.55f));
        node.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.22f, 0.3f, 0.22f) },
            MaterialOverride = _lamp,
            Position = new Vector3(-half + 0.3f, deck + 1.6f, 0),
        });

        batch.Commit(node);
        var hands = CrowdMesh.Create(places.Count);
        node.AddChild(hands);
        _ships.Add(new Anchored { Node = node, At = at, Yaw = yaw, Seed = at.X * 0.37f, Hands = hands.Multimesh, Places = places });
    }

    // A lofted hull, bow to +X: full amidships, a sharp stem, a narrower square transom aft.
    private void Hull(MeshBatcher batch, float length, float beam, float deck, float depth)
    {
        const int stations = 12;
        Vector3[] Ring(int i)
        {
            var t = -1f + 2f * i / stations;
            var half = beam / 2f * (t >= 0 ? Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow(t, 2.4f))) : 1 - 0.42f * Mathf.Pow(-t, 2.5f));
            var x = t * length / 2f;
            var top = deck + 0.35f + 0.55f * t * t;
            return [new Vector3(x, top, half), new Vector3(x, deck * 0.35f, half * 0.92f), new Vector3(x, -depth, half * 0.3f)];
        }

        for (var i = 0; i < stations; i++)
        {
            var a = Ring(i);
            var b = Ring(i + 1);
            foreach (var side in new[] { 1f, -1f })
            {
                Vector3 S(Vector3 v) => new(v.X, v.Y, v.Z * side);
                Quad(batch, S(a[0]), S(b[0]), S(b[1]), S(a[1]), Strake);
                Quad(batch, S(a[1]), S(b[1]), S(b[2]), S(a[2]), Pitch);
            }

            // The deck, a little below the rail.
            Quad(batch, a[0] with { Y = deck }, b[0] with { Y = deck }, b[0] with { Y = deck, Z = -b[0].Z }, a[0] with { Y = deck, Z = -a[0].Z }, Deck);
        }

        var stern = Ring(0);
        Quad(batch, stern[0], stern[0] with { Z = -stern[0].Z }, stern[1] with { Z = -stern[1].Z }, stern[1], Strake.Darkened(0.15f));
        Quad(batch, stern[1], stern[1] with { Z = -stern[1].Z }, stern[2] with { Z = -stern[2].Z }, stern[2], Pitch);

        // A wale along each side.
        for (var i = 0; i < stations; i++)
        {
            var a = Ring(i)[0];
            var b = Ring(i + 1)[0];
            foreach (var side in new[] { 1f, -1f })
            {
                var from = new Vector3(a.X, a.Y - 0.45f, a.Z * side * 1.01f);
                var to = new Vector3(b.X, b.Y - 0.45f, b.Z * side * 1.01f);
                Quad(batch, from, to, to - new Vector3(0, 0.14f, 0), from - new Vector3(0, 0.14f, 0), Weathered);
            }
        }
    }

    private void Quad(MeshBatcher batch, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour) =>
        batch.AddTriangles(_hull, [a, b, c, a, c, d], null, colour);

    private static void Castle(MeshBatcher batch, float x, float length, float width, float deck, float height)
    {
        batch.AddColoured(new BoxMesh { Size = new Vector3(length, height, width) }, new Transform3D(Basis.Identity, new Vector3(x, deck + height / 2f, 0)), Strake);
        batch.AddColoured(new BoxMesh { Size = new Vector3(length + 0.1f, 0.1f, width + 0.1f) }, new Transform3D(Basis.Identity, new Vector3(x, deck + height, 0)), Weathered);
    }

    private static void Mast(MeshBatcher batch, Vector3 foot, float height, float yard, bool square)
    {
        Line3(batch, foot, foot + new Vector3(0, height, 0), 0.16f, Spar);
        if (square)
        {
            var at = foot + new Vector3(0, height * 0.72f, 0);
            Line3(batch, at - new Vector3(0, 0, yard / 2f), at + new Vector3(0, 0, yard / 2f), 0.08f, Spar);
            Line3(batch, at - new Vector3(0, 0.16f, yard * 0.45f), at + new Vector3(0, -0.16f, yard * 0.45f), 0.14f, Furled);
            batch.AddColoured(new CylinderMesh { TopRadius = 0.45f, BottomRadius = 0.35f, Height = 0.35f, RadialSegments = 8, Rings = 1 },
                new Transform3D(Basis.Identity, foot + new Vector3(0, height * 0.8f, 0)), Spar);
        }
        else
        {
            // A lateen yard lowered and slanting fore and aft, its sail bundled along it.
            var low = foot + new Vector3(yard * 0.45f, 1.2f, 0.3f);
            var high = foot + new Vector3(-yard * 0.45f, height * 0.75f, 0.3f);
            Line3(batch, low, high, 0.07f, Spar);
            Line3(batch, low.Lerp(high, 0.1f) + new Vector3(0, -0.15f, 0), low.Lerp(high, 0.85f) + new Vector3(0, -0.15f, 0), 0.13f, Furled);
        }

        // Shrouds to each side.
        foreach (var side in new[] { 1f, -1f })
        {
            Line3(batch, foot + new Vector3(0, height * 0.8f, 0), foot + new Vector3(-0.6f, 0.4f, side * 2.4f));
            Line3(batch, foot + new Vector3(0, height * 0.8f, 0), foot + new Vector3(0.6f, 0.4f, side * 2.4f));
        }
    }

    private static void Line3(MeshBatcher batch, Vector3 from, Vector3 to, float radius = 0.02f, Color? colour = null)
    {
        var direction = to - from;
        var length = direction.Length();
        var mesh = new CylinderMesh { TopRadius = radius * 0.85f, BottomRadius = radius, Height = length, RadialSegments = radius > 0.05f ? 6 : 3, Rings = 1 };
        batch.AddColoured(mesh, new Transform3D(new Basis(new Quaternion(Vector3.Up, direction / length)), (from + to) / 2f), colour ?? Line);
    }

    private void RowingBoat(Vector3[] route)
    {
        var node = new Node3D { Name = "Rowing boat" };
        AddChild(node);
        var batch = new MeshBatcher();
        Hull(batch, 5.2f, 1.7f, 0.25f, 0.4f);
        batch.AddColoured(new BoxMesh { Size = new Vector3(0.9f, 0.4f, 0.7f) }, new Transform3D(Basis.Identity, new Vector3(1.6f, 0.45f, 0)), new Color(0.6f, 0.52f, 0.38f));
        batch.Commit(node);

        var oars = new List<Node3D>();
        foreach (var x in new[] { -0.9f, 0.6f })
        {
            foreach (var side in new[] { 1f, -1f })
            {
                var pivot = new Node3D { Position = new Vector3(x, 0.55f, side * 0.75f) };
                node.AddChild(pivot);
                pivot.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.06f, 0.06f, 2.6f) },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = Spar.Lightened(0.15f), Roughness = 0.9f },
                    Position = new Vector3(0, 0, side * 1.1f),
                });
                oars.Add(pivot);
            }
        }

        var rowers = CrowdMesh.Create(2);
        node.AddChild(rowers);
        _boats.Add(new Boat { Node = node, Route = route, Rowers = rowers.Multimesh, Oars = oars.ToArray(), Wait = route[0].X > 20 ? 25f : 5f });
    }
}
