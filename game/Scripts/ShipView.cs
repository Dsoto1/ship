using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// Draws the ship around a location grid: a curved hull and bulwarks that follow the walkable deck,
/// planked deck, masts with yards and sails, standing rigging, a bowsprit, a raised stern with its
/// lantern, and fittings at the work spots (pump, galley fire, rope coils, the hull-repair stack).
/// One cell is one metre; grid X is world X and grid Y is world Z, with the bow at row 0 (D-006).
/// Purely visual: it holds no game state. Ship type and rig are placeholders until the hero ship is
/// approved (ART_BIBLE.md 6.1); the *Garza* is a caravel (SETTING.md 4).
/// </summary>
public partial class ShipView : Node3D
{
    private const float WallThickness = 0.14f;
    private const float KeelDepth = 2.35f;

    private static readonly Color HullWood = new(0.26f, 0.18f, 0.12f);
    private static readonly Color Wale = new(0.17f, 0.12f, 0.08f);
    private static readonly Color Bottom = new(0.72f, 0.68f, 0.58f);
    private static readonly Color RailCap = new(0.42f, 0.31f, 0.2f);
    private static readonly Color Spar = new(0.36f, 0.26f, 0.17f);
    private static readonly Color Rope = new(0.33f, 0.27f, 0.19f);
    private static readonly Color Iron = new(0.2f, 0.2f, 0.21f);

    private readonly List<ShaderMaterial> _sails = [];
    private readonly List<(Node3D Sail, int Tier)> _squareSails = [];
    private readonly List<(OmniLight3D Light, StandardMaterial3D Glass)> _lanterns = [];
    private ShaderMaterial _deckMaterial = null!;
    private OmniLight3D? _galleyFire;
    private float[] _halfWidth = [];
    private float _sternZ;

    /// <summary>The hull's centre line (world X) and its length from stem to stern.</summary>
    public float CentreX { get; private set; }

    /// <summary>Bow (smallest Z) and stern (largest Z) of the hull.</summary>
    public (float Bow, float Stern) Length { get; private set; }

    /// <summary>Widest half beam, for the wake.</summary>
    public float HalfBeam { get; private set; }

    /// <summary>Builds the meshes for <paramref name="location"/>, replacing any earlier ones.</summary>
    public void Build(LocationDefinition location)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _sails.Clear();
        _squareSails.Clear();
        _lanterns.Clear();
        _galleyFire = null;
        MeasureHull(location);
        BuildHull();
        BuildDeckFittings(location);
        BuildStern();
        BuildMastsAndRigging(location);
        BuildSpotProps(location);
    }

    /// <summary>
    /// Sets things that change with time: lantern light at night, the galley fire, and how see-through
    /// the sails are (they fade when the camera is close, so the crew stay visible).
    /// </summary>
    /// <param name="darkness">0 in daylight, 1 at full night.</param>
    /// <param name="sailOpacity">1 for solid sails.</param>
    /// <param name="time">Seconds, for flicker.</param>
    /// <param name="galleyLit">Whether someone is cooking.</param>
    /// <param name="seaState">0 calm .. 3 storm: topsails are furled from a gale, courses reefed in a storm.</param>
    /// <param name="delta">Seconds since the last frame, so sail changes take a moment.</param>
    public void UpdateLook(float darkness, float sailOpacity, float time, bool galleyLit, float seaState = 1f, float delta = 0f)
    {
        foreach (var (sail, tier) in _squareSails)
        {
            var set = tier == 1 ? Mathf.Clamp(1.9f - seaState * 0.75f, 0.08f, 1f) : Mathf.Clamp(2.3f - seaState * 0.65f, 0.4f, 1f);
            var scale = Mathf.Lerp(sail.Scale.Y, set, Mathf.Clamp(delta * 1.5f, 0, 1));
            sail.Scale = new Vector3(1, delta == 0 ? set : scale, Mathf.Lerp(0.3f, 1f, Mathf.Clamp(seaState, 0.2f, 1f)));
        }

        var flicker = 0.9f + 0.1f * Mathf.Sin(time * 11f) * Mathf.Sin(time * 4.3f);
        foreach (var (light, glass) in _lanterns)
        {
            light.LightEnergy = darkness * 2.2f * flicker;
            glass.EmissionEnergyMultiplier = 0.2f + darkness * 3f;
        }

        if (_galleyFire is not null)
        {
            _galleyFire.LightEnergy = galleyLit ? (0.6f + darkness * 1.5f) * flicker : 0f;
        }

        foreach (var sail in _sails)
        {
            sail.SetShaderParameter("opacity", sailOpacity);
        }
    }

    // Half beam per deck row: the walkable span plus the bulwark, so the hull hugs the deck.
    private void MeasureHull(LocationDefinition location)
    {
        _halfWidth = new float[location.Height];
        var lastWalkableRow = 0;
        var left = location.Width;
        var right = 0;
        for (var y = 0; y < location.Height; y++)
        {
            var walkable = Enumerable.Range(0, location.Width).Where(x => location.IsWalkable(new GridPosition(x, y))).ToList();
            if (walkable.Count == 0)
            {
                continue;
            }

            lastWalkableRow = y;
            left = Math.Min(left, walkable.Min());
            right = Math.Max(right, walkable.Max());
            _halfWidth[y] = (walkable.Max() - walkable.Min()) / 2f + 0.5f + WallThickness;
        }

        for (var y = lastWalkableRow + 1; y < location.Height; y++)
        {
            _halfWidth[y] = _halfWidth[lastWalkableRow];
        }

        CentreX = (left + right) / 2f;
        _sternZ = lastWalkableRow + 0.5f;
        Length = (-1.9f, _sternZ + 1.9f);
        HalfBeam = _halfWidth.Max();
    }

    private float HalfWidthAt(float z)
    {
        if (z <= 0)
        {
            // The bow: from the first row's width to a sharp stem.
            var t = Mathf.Clamp((z - Length.Bow) / (0 - Length.Bow), 0, 1);
            return Mathf.Lerp(0.04f, _halfWidth[0], Mathf.Sin(t * Mathf.Pi / 2f));
        }

        var row = Mathf.Clamp(z, 0, _halfWidth.Length - 1);
        var below = (int)Mathf.Floor(row);
        var above = Math.Min(below + 1, _halfWidth.Length - 1);
        return Mathf.Lerp(_halfWidth[below], _halfWidth[above], row - below);
    }

    // Bulwark height above the deck: the sheer rises towards bow and stern.
    private float BulwarkTop(float z)
    {
        var bow = 1f - Mathf.SmoothStep(-2f, 4f, z);
        var stern = Mathf.SmoothStep(_sternZ - 6f, _sternZ, z);
        return 0.7f + bow * 0.55f + stern * 0.3f;
    }

    private void BuildHull()
    {
        // Cross-section from the gunwale (deck level) down to the keel, as fractions of half beam and depth.
        (float X, float Y, Color Colour)[] profile =
        [
            (1.00f, 0.00f, HullWood), (1.03f, -0.18f, Wale), (1.03f, -0.32f, Wale), (1.02f, -0.5f, HullWood),
            (0.97f, -0.95f, HullWood), (0.93f, -1.08f, Bottom), (0.84f, -1.45f, Bottom), (0.62f, -1.88f, Bottom),
            (0.32f, -2.2f, Bottom), (0.0f, -KeelDepth, Bottom),
        ];

        var stations = Stations(Length.Bow, _sternZ + 0.6f, 0.25f);
        var hull = new SurfaceTool();
        hull.Begin(Mesh.PrimitiveType.Triangles);
        var deck = new SurfaceTool();
        deck.Begin(Mesh.PrimitiveType.Triangles);
        var walls = new SurfaceTool();
        walls.Begin(Mesh.PrimitiveType.Triangles);

        for (var i = 0; i + 1 < stations.Count; i++)
        {
            var (z0, z1) = (stations[i], stations[i + 1]);
            var (w0, w1) = (HalfWidthAt(z0), HalfWidthAt(z1));
            var (d0, d1) = (DepthScale(z0), DepthScale(z1));
            foreach (var side in new[] { -1f, 1f })
            {
                for (var j = 0; j + 1 < profile.Length; j++)
                {
                    var a = new Vector3(CentreX + side * w0 * profile[j].X, profile[j].Y * d0, z0);
                    var b = new Vector3(CentreX + side * w1 * profile[j].X, profile[j].Y * d1, z1);
                    var c = new Vector3(CentreX + side * w1 * profile[j + 1].X, profile[j + 1].Y * d1, z1);
                    var d = new Vector3(CentreX + side * w0 * profile[j + 1].X, profile[j + 1].Y * d0, z0);
                    Quad(hull, a, b, c, d, profile[j].Colour, side < 0);
                }

                // Bulwark: outer face continues the hull up, inner face is set in by the wall thickness.
                var (t0, t1) = (BulwarkTop(z0), BulwarkTop(z1));
                var inner0 = Mathf.Max(w0 - WallThickness, 0.02f);
                var inner1 = Mathf.Max(w1 - WallThickness, 0.02f);
                Quad(walls, new(CentreX + side * w0, 0, z0), new(CentreX + side * w1, 0, z1),
                    new(CentreX + side * w1, t1, z1), new(CentreX + side * w0, t0, z0), HullWood, side > 0);
                Quad(walls, new(CentreX + side * inner0, 0, z0), new(CentreX + side * inner1, 0, z1),
                    new(CentreX + side * inner1, t1, z1), new(CentreX + side * inner0, t0, z0), HullWood.Lightened(0.15f), side < 0);
                Quad(walls, new(CentreX + side * inner0, t0, z0), new(CentreX + side * inner1, t1, z1),
                    new(CentreX + side * w1, t1, z1), new(CentreX + side * w0, t0, z0), RailCap, side < 0);
            }

            // Deck between the bulwarks, with UVs in metres for the plank shader.
            var l0 = CentreX - Mathf.Max(w0 - WallThickness, 0.02f);
            var r0 = CentreX + Mathf.Max(w0 - WallThickness, 0.02f);
            var l1 = CentreX - Mathf.Max(w1 - WallThickness, 0.02f);
            var r1 = CentreX + Mathf.Max(w1 - WallThickness, 0.02f);
            DeckQuad(deck, new(l0, 0, z0), new(r0, 0, z0), new(r1, 0, z1), new(l1, 0, z1));
        }

        // Close the stern below the transom.
        AddMesh(Finish(hull), VertexColoured(0.8f));
        AddMesh(Finish(walls), VertexColoured(0.85f));
        _deckMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/deck.gdshader") };
        AddMesh(Finish(deck), _deckMaterial);

        // Stem post and bowsprit.
        AddSpar(new Vector3(CentreX, -1.6f, Length.Bow + 0.2f), new Vector3(CentreX, BulwarkTop(Length.Bow) + 0.15f, Length.Bow - 0.15f), 0.1f, HullWood);
        AddSpar(new Vector3(CentreX, 0.9f, 0.6f), new Vector3(CentreX, 2.6f, Length.Bow - 4.5f), 0.12f, Spar);
    }

    // The keel rises a little towards bow and stern.
    private float DepthScale(float z)
    {
        var t = Mathf.Clamp((z - Length.Bow) / (_sternZ + 0.6f - Length.Bow), 0, 1);
        return 0.72f + 0.28f * Mathf.Sin(t * Mathf.Pi);
    }

    // Hatch gratings and other fittings: non-walkable cells inside the deck that are not masts.
    private void BuildDeckFittings(LocationDefinition location)
    {
        for (var y = 0; y < location.Height; y++)
        {
            var walkable = Enumerable.Range(0, location.Width).Where(x => location.IsWalkable(new GridPosition(x, y))).ToList();
            if (walkable.Count == 0)
            {
                continue;
            }

            for (var x = walkable.Min() + 1; x < walkable.Max(); x++)
            {
                var cell = new GridPosition(x, y);
                if (location.IsWalkable(cell) || IsMast(location, cell))
                {
                    continue;
                }

                var hatch = location.IsWalkable(cell with { X = x - 1 }) != location.IsWalkable(cell with { X = x + 1 });
                if (hatch)
                {
                    // Hatch coaming with a grating on top.
                    AddBox(new Vector3(x, 0.14f, y), new Vector3(1f, 0.28f, 1f), Spar);
                    AddBox(new Vector3(x, 0.285f, y), new Vector3(0.86f, 0.02f, 0.86f), new Color(0.12f, 0.09f, 0.07f));
                }
                else
                {
                    // Lashed water casks.
                    AddCask(new Vector3(x - 0.2f, 0, y - 0.2f));
                    AddCask(new Vector3(x + 0.22f, 0, y + 0.15f));
                    AddCask(new Vector3(x - 0.15f, 0, y + 0.3f), small: true);
                }
            }
        }
    }

    private static bool IsMast(LocationDefinition location, GridPosition cell) =>
        !location.IsWalkable(cell)
        && new[] { cell with { Y = cell.Y - 1 }, cell with { X = cell.X + 1 }, cell with { Y = cell.Y + 1 }, cell with { X = cell.X - 1 } }
            .All(location.IsWalkable);

    // A raised stern deck behind the walkable area, with the transom, windows and the stern lantern.
    private void BuildStern()
    {
        var width = HalfWidthAt(_sternZ) * 2f;
        var depth = Length.Stern - _sternZ;
        var centre = new Vector3(CentreX, 0.75f, _sternZ + depth / 2f);
        AddBox(centre, new Vector3(width, 1.5f, depth), HullWood);
        AddBox(centre + new Vector3(0, 0.77f, 0), new Vector3(width + 0.1f, 0.06f, depth + 0.1f), RailCap);
        AddBox(new Vector3(CentreX, -0.6f, Length.Stern - 0.05f), new Vector3(width * 0.9f, 2.6f, 0.12f), HullWood.Darkened(0.15f));

        // Cabin door and windows facing the waist.
        AddBox(new Vector3(CentreX, 0.6f, _sternZ - 0.01f), new Vector3(0.7f, 1.2f, 0.04f), Wale);
        foreach (var x in new[] { -1.4f, 1.4f })
        {
            AddBox(new Vector3(CentreX + x, 0.85f, _sternZ - 0.01f), new Vector3(0.4f, 0.32f, 0.04f), new Color(0.1f, 0.09f, 0.08f));
        }

        // Rail posts around the stern deck.
        for (var i = 0; i <= 6; i++)
        {
            var x = CentreX - width / 2f + i * width / 6f;
            AddBox(new Vector3(x, 1.75f, Length.Stern - 0.1f), new Vector3(0.08f, 0.45f, 0.08f), RailCap);
        }

        AddBox(new Vector3(CentreX, 1.98f, Length.Stern - 0.1f), new Vector3(width, 0.06f, 0.08f), RailCap);
        AddLantern(new Vector3(CentreX, 2.5f, Length.Stern + 0.1f), large: true);

        // Tiller and rudder head.
        AddSpar(new Vector3(CentreX, 1.6f, Length.Stern - 0.3f), new Vector3(CentreX, 1.65f, Length.Stern - 2.0f), 0.05f, Spar);
        AddBox(new Vector3(CentreX, -0.8f, Length.Stern + 0.15f), new Vector3(0.18f, 2.8f, 0.5f), HullWood.Darkened(0.2f));
    }

    private void BuildMastsAndRigging(LocationDefinition location)
    {
        var masts = new List<Vector3>();
        for (var y = 0; y < location.Height; y++)
        {
            for (var x = 0; x < location.Width; x++)
            {
                if (IsMast(location, new GridPosition(x, y)))
                {
                    masts.Add(new Vector3(x, 0, y));
                }
            }
        }

        masts.Sort((a, b) => a.Z.CompareTo(b.Z));
        for (var i = 0; i < masts.Count; i++)
        {
            // The mainmast (the second, or the only one) is the tallest.
            var main = i == Math.Min(1, masts.Count - 1);
            var height = main ? 12f : 9.5f;
            var beam = HalfWidthAt(masts[i].Z);
            AddMast(masts[i], height, beam, main, i == 0 && masts.Count > 1);
        }

        // Mizzen on the stern deck with a lateen sail.
        var mizzenFoot = new Vector3(CentreX, 1.5f, (_sternZ + Length.Stern) / 2f);
        AddSpar(mizzenFoot, mizzenFoot + new Vector3(0, 6.5f, 0), 0.12f, Spar);
        var yardLow = mizzenFoot + new Vector3(0, 1.4f, 2.2f);
        var yardHigh = mizzenFoot + new Vector3(0, 7.6f, -3.2f);
        AddSpar(yardLow, yardHigh, 0.06f, Spar);
        AddLateenSail(yardLow, yardHigh, mizzenFoot + new Vector3(0, 0.6f, -0.9f));

        // Forestay from the foremast head to the bowsprit, and a stay between the masts.
        if (masts.Count > 0)
        {
            AddRope(masts[0] + new Vector3(0, 9f, 0), new Vector3(CentreX, 2.5f, Length.Bow - 4.2f));
        }

        if (masts.Count > 1)
        {
            AddRope(masts[1] + new Vector3(0, 11.5f, 0), masts[0] + new Vector3(0, 7.5f, 0));
            AddRope(masts[1] + new Vector3(0, 10f, 0), mizzenFoot + new Vector3(0, 5.5f, 0));
        }
    }

    private void AddMast(Vector3 foot, float height, float beam, bool main, bool fore)
    {
        AddSpar(foot, foot + new Vector3(0, height, 0), main ? 0.2f : 0.16f, Spar);

        // Fighting top on the mainmast.
        if (main)
        {
            AddMesh(new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.4f, Height = 0.35f }, Plain(Spar), foot + new Vector3(0, height * 0.72f, 0));
        }

        // Course yard and sail, plus a small topsail on the mainmast.
        var courseYard = height * (main ? 0.66f : 0.8f);
        var yardHalf = beam * (main ? 1.45f : 1.2f);
        AddSpar(foot + new Vector3(-yardHalf, courseYard, 0), foot + new Vector3(yardHalf, courseYard, 0), 0.07f, Spar);
        AddSquareSail(foot + new Vector3(0, courseYard, 0.15f), yardHalf * 1.9f, courseYard - 1.9f, belly: 1.2f, tier: 0);
        if (main)
        {
            var topYard = height * 0.9f;
            AddSpar(foot + new Vector3(-yardHalf * 0.55f, topYard, 0), foot + new Vector3(yardHalf * 0.55f, topYard, 0), 0.05f, Spar);
            AddSquareSail(foot + new Vector3(0, topYard, 0.1f), yardHalf * 1.05f, topYard - courseYard - 0.6f, belly: 0.6f, tier: 1);
            AddPennant(foot + new Vector3(0, height + 0.05f, 0));
        }

        // Shrouds: three a side from below the top to the bulwark, slightly aft of the mast.
        foreach (var side in new[] { -1f, 1f })
        {
            for (var k = 0; k < 3; k++)
            {
                var deckPoint = new Vector3(CentreX + side * (beam - 0.05f), BulwarkTop(foot.Z) , foot.Z + 0.35f + k * 0.45f);
                AddRope(foot + new Vector3(0, height * 0.7f, 0), deckPoint);
            }
        }

        AddLantern(foot + new Vector3(0, 2.4f, -0.25f), large: false);
        if (fore)
        {
            // Belaying rack at the foot of the foremast.
            AddBox(foot + new Vector3(0, 0.55f, -0.45f), new Vector3(0.9f, 0.08f, 0.12f), Spar);
        }
    }

    // A square sail hanging from a yard at <paramref name="top"/>, bellied forward (towards -z) by the wind from astern.
    // Tier 0 is a course, 1 a topsail: in heavy weather topsails are furled first, then courses reefed.
    private void AddSquareSail(Vector3 top, float width, float height, float belly, int tier)
    {
        var anchor = new Node3D { Position = top };
        AddChild(anchor);
        _squareSails.Add((anchor, tier));
        const int columns = 10;
        const int rows = 8;
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        var points = new Vector3[columns + 1, rows + 1];
        for (var r = 0; r <= rows; r++)
        {
            for (var c = 0; c <= columns; c++)
            {
                var u = c / (float)columns;
                var v = r / (float)rows;
                var x = (u - 0.5f) * width * (1f - 0.08f * v);
                var y = -v * height;
                var z = -belly * Mathf.Sin(u * Mathf.Pi) * Mathf.Sin(Mathf.Clamp(v * 1.1f, 0, 1) * Mathf.Pi * 0.85f + 0.12f);
                points[c, r] = new Vector3(x, y, z);
            }
        }

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < columns; c++)
            {
                SailQuad(tool, points[c, r], points[c + 1, r], points[c + 1, r + 1], points[c, r + 1],
                    new Vector2(c / (float)columns, r / (float)rows), new Vector2((c + 1) / (float)columns, (r + 1) / (float)rows));
            }
        }

        AddSailMesh(tool, anchor);
    }

    private void AddLateenSail(Vector3 yardLow, Vector3 yardHigh, Vector3 tack)
    {
        const int steps = 8;
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        for (var i = 0; i < steps; i++)
        {
            var t0 = i / (float)steps;
            var t1 = (i + 1) / (float)steps;
            var a = yardLow.Lerp(yardHigh, t0);
            var b = yardLow.Lerp(yardHigh, t1);
            var bulge = new Vector3(0.9f * Mathf.Sin(t0 * Mathf.Pi), 0, 0);
            var bulge1 = new Vector3(0.9f * Mathf.Sin(t1 * Mathf.Pi), 0, 0);
            var c = tack.Lerp(b, 0.15f) + bulge1 * 0.5f;
            var d = tack.Lerp(a, 0.15f) + bulge * 0.5f;
            SailQuad(tool, a, b, c, d, new Vector2(t0, 0), new Vector2(t1, 1));
        }

        AddSailMesh(tool);
    }

    private void AddSailMesh(SurfaceTool tool, Node3D? parent = null)
    {
        tool.GenerateNormals();
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/sail.gdshader") };
        _sails.Add(material);
        var instance = new MeshInstance3D { Mesh = tool.Commit(), MaterialOverride = material };
        (parent ?? this).AddChild(instance);
    }

    private static void SailQuad(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv0, Vector2 uv1)
    {
        var uvA = uv0;
        var uvB = new Vector2(uv1.X, uv0.Y);
        var uvC = uv1;
        var uvD = new Vector2(uv0.X, uv1.Y);
        tool.SetUV(uvA); tool.AddVertex(a);
        tool.SetUV(uvB); tool.AddVertex(b);
        tool.SetUV(uvC); tool.AddVertex(c);
        tool.SetUV(uvA); tool.AddVertex(a);
        tool.SetUV(uvC); tool.AddVertex(c);
        tool.SetUV(uvD); tool.AddVertex(d);
    }

    private void AddPennant(Vector3 top)
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        tool.SetColor(new Color(0.62f, 0.12f, 0.1f));
        tool.AddVertex(top);
        tool.AddVertex(top + new Vector3(0.2f, -0.05f, 2.4f));
        tool.AddVertex(top + new Vector3(0, -0.45f, 0));
        tool.GenerateNormals();
        var material = VertexColoured(0.9f);
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        AddMesh(tool.Commit(), material);
    }

    private void BuildSpotProps(LocationDefinition location)
    {
        foreach (var spot in location.Spots)
        {
            var at = new Vector3(spot.X, 0, spot.Y);
            if (spot.Tasks.Contains("task.pump_bilge"))
            {
                AddSpar(at + new Vector3(0.35f, 0, 0), at + new Vector3(0.35f, 1.0f, 0), 0.09f, Spar);
                AddSpar(at + new Vector3(0.35f, 0.95f, 0), at + new Vector3(-0.15f, 1.15f, 0), 0.03f, Iron);
            }
            else if (spot.Tasks.Contains("task.cook"))
            {
                // Galley fire box: a sand-filled box with a hood.
                AddBox(at + new Vector3(0.45f, 0.35f, 0), new Vector3(0.6f, 0.7f, 0.8f), new Color(0.45f, 0.3f, 0.22f));
                AddBox(at + new Vector3(0.45f, 0.72f, 0), new Vector3(0.5f, 0.04f, 0.7f), new Color(0.9f, 0.45f, 0.15f), emissive: true);
                AddMesh(new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.18f, Height = 0.22f }, Plain(Iron), at + new Vector3(0.45f, 0.85f, 0));
                _galleyFire = new OmniLight3D { Position = at + new Vector3(0.45f, 1.1f, 0), LightColor = new Color(1f, 0.6f, 0.3f), OmniRange = 3.5f };
                AddChild(_galleyFire);
            }
            else if (spot.Tasks.Contains("task.repair_rigging"))
            {
                AddMesh(new TorusMesh { InnerRadius = 0.12f, OuterRadius = 0.28f }, Plain(Rope), at + new Vector3(0.4f, 0.05f, 0.25f));
                AddMesh(new TorusMesh { InnerRadius = 0.1f, OuterRadius = 0.22f }, Plain(Rope), at + new Vector3(0.4f, 0.13f, 0.25f));
            }
            else if (spot.Tasks.Contains("task.patch_hull"))
            {
                AddBox(at + new Vector3(0, 0.06f, 0.45f), new Vector3(0.3f, 0.06f, 1.6f), Spar.Lightened(0.2f));
                AddBox(at + new Vector3(0.05f, 0.12f, 0.45f), new Vector3(0.3f, 0.06f, 1.5f), Spar.Lightened(0.1f));
                AddMesh(new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.13f, Height = 0.3f }, Plain(new Color(0.08f, 0.07f, 0.06f)), at + new Vector3(0.4f, 0.15f, -0.3f));
            }
        }
    }

    private void AddLantern(Vector3 at, bool large)
    {
        var glass = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.8f, 0.45f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.7f, 0.35f),
            EmissionEnergyMultiplier = 0.2f,
        };
        var size = large ? 0.32f : 0.18f;
        AddMesh(new BoxMesh { Size = new Vector3(size, size * 1.4f, size) }, glass, at);
        AddMesh(new CylinderMesh { TopRadius = 0.01f, BottomRadius = size * 0.7f, Height = size * 0.5f }, Plain(Iron), at + new Vector3(0, size, 0));
        var light = new OmniLight3D
        {
            Position = at,
            LightColor = new Color(1f, 0.72f, 0.4f),
            OmniRange = large ? 9f : 6f,
            OmniAttenuation = 1.4f,
            ShadowEnabled = false,
        };
        AddChild(light);
        _lanterns.Add((light, glass));
    }

    private void AddCask(Vector3 foot, bool small = false)
    {
        var radius = small ? 0.18f : 0.24f;
        var height = small ? 0.45f : 0.62f;
        AddMesh(new CylinderMesh { TopRadius = radius * 0.88f, BottomRadius = radius * 0.88f, Height = height }, Plain(new Color(0.48f, 0.34f, 0.2f)), foot + new Vector3(0, height / 2f, 0));
        foreach (var h in new[] { 0.15f, 0.85f })
        {
            AddMesh(new CylinderMesh { TopRadius = radius * 0.9f, BottomRadius = radius * 0.9f, Height = 0.04f }, Plain(Iron), foot + new Vector3(0, height * h, 0));
        }
    }

    private void AddRope(Vector3 from, Vector3 to) => AddSpar(from, to, 0.018f, Rope);

    // A cylinder between two points, for masts, yards and ropes.
    private void AddSpar(Vector3 from, Vector3 to, float radius, Color colour)
    {
        var direction = to - from;
        var length = direction.Length();
        if (length < 0.001f)
        {
            return;
        }

        var instance = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius * 0.85f, BottomRadius = radius, Height = length, RadialSegments = radius > 0.05f ? 12 : 5, Rings = 1 },
            MaterialOverride = Plain(colour),
            Position = (from + to) / 2f,
        };
        instance.Quaternion = new Quaternion(Vector3.Up, direction / length);
        AddChild(instance);
    }

    private void AddBox(Vector3 position, Vector3 size, Color colour, bool emissive = false)
    {
        var material = Plain(colour);
        if (emissive)
        {
            material.EmissionEnabled = true;
            material.Emission = colour;
            material.EmissionEnergyMultiplier = 1.5f;
        }

        AddMesh(new BoxMesh { Size = size }, material, position);
    }

    private void AddMesh(Mesh mesh, Material material, Vector3? position = null) =>
        AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material, Position = position ?? Vector3.Zero });

    private static StandardMaterial3D Plain(Color colour) => new() { AlbedoColor = colour, Roughness = 0.88f };

    private static StandardMaterial3D VertexColoured(float roughness) => new() { VertexColorUseAsAlbedo = true, Roughness = roughness };

    private static List<float> Stations(float from, float to, float step)
    {
        var stations = new List<float>();
        for (var z = from; z < to; z += step)
        {
            stations.Add(z);
        }

        stations.Add(to);
        return stations;
    }

    // Two triangles; <paramref name="flip"/> reverses the winding so both sides of the hull face outwards.
    private static void Quad(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour, bool flip)
    {
        tool.SetColor(colour);
        if (flip)
        {
            tool.AddVertex(a); tool.AddVertex(c); tool.AddVertex(b);
            tool.AddVertex(a); tool.AddVertex(d); tool.AddVertex(c);
        }
        else
        {
            tool.AddVertex(a); tool.AddVertex(b); tool.AddVertex(c);
            tool.AddVertex(a); tool.AddVertex(c); tool.AddVertex(d);
        }
    }

    private static void DeckQuad(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        foreach (var vertex in new[] { a, b, c, a, c, d })
        {
            tool.SetUV(new Vector2(vertex.X, vertex.Z));
            tool.AddVertex(vertex);
        }
    }

    private static ArrayMesh Finish(SurfaceTool tool)
    {
        tool.GenerateNormals();
        return tool.Commit();
    }
}
