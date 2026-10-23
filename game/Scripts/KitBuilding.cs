using Godot;

namespace Expedition.Game;

/// <summary>
/// A house assembled from the Medieval Village MegaKit's modular pieces (Quaternius, CC0; in
/// Assets/Buildings/MegaKit), in place of one built from boxes. The kit is laid out on a 2 m grid: each
/// wall piece is 2 m wide and a storey (3 m) high, its outer face toward +Z, with the window, shutters or
/// door set into its opening as separate pieces. A house's front and sides are divided into whole pieces,
/// stretched a little to fit its footprint exactly. A trial (--kit): see docs/BUILDINGS_KIT.md.
/// </summary>
public static class KitBuilding
{
    private const string Kit = "res://Assets/Buildings/MegaKit/glTF/";
    private const float Piece = 2f, Storey = 3f;
    private static readonly Dictionary<string, PackedScene?> Scenes = [];

    // The roofs the kit has, by footprint (across, deep).
    private static readonly (int Across, int Deep)[] Roofs = [(4, 4), (4, 6), (4, 8), (6, 6), (6, 8), (6, 10), (8, 8), (8, 10)];

    /// <summary>True when the trial is on: ordinary houses are built from the kit.</summary>
    public static bool Enabled { get; set; }

    /// <summary>With the trial on, only every other house is built from the kit, to compare them side by side.</summary>
    public static bool Alternate { get; set; }

    private static int _asked;

    /// <summary>Whether this house is to be built from the kit.</summary>
    public static bool Take() => Enabled && Available && (!Alternate || _asked++ % 2 == 0);

    /// <summary>Whether the kit's files are in the game.</summary>
    public static bool Available => ResourceLoader.Exists(Kit + "Wall_Plaster_Straight.gltf");

    /// <summary>
    /// Builds a house filling <paramref name="left"/>..<paramref name="right"/> by
    /// <paramref name="back"/>..<paramref name="front"/>, its front to the south (+Z), of so many storeys
    /// on ground at <paramref name="baseHeight"/>. <paramref name="seed"/> picks among the kit's variants.
    /// </summary>
    public static Node3D House(float left, float right, float back, float front, int storeys, float storeyHeight, float baseHeight, bool brick, int seed)
    {
        var house = new Node3D { Name = "KitHouse" };
        var across = Mathf.Max(1, Mathf.RoundToInt((right - left) / Piece));
        var deep = Mathf.Max(1, Mathf.RoundToInt((front - back) / Piece));
        var stretchX = (right - left) / (across * Piece);
        var stretchZ = (front - back) / (deep * Piece);
        var lift = storeyHeight / Storey;
        var rng = new RandomNumberGenerator { Seed = (ulong)seed };
        var door = across / 2;

        for (var s = 0; s < storeys; s++)
        {
            var y = baseHeight + s * storeyHeight;
            // The front, to the south: the street door in the middle of the ground floor, windows elsewhere.
            for (var i = 0; i < across; i++)
            {
                var x = left + (i + 0.5f) * Piece * stretchX;
                var at = new Vector3(x, y, front);
                if (s == 0 && i == door)
                {
                    Add(house, brick ? "Wall_UnevenBrick_Door_Round" : "Wall_Plaster_Door_Round", at, 0, stretchX, lift);
                    Add(house, "DoorFrame_Round_Brick", at, 0, stretchX, lift);
                    Add(house, "Door_1_Round", at + new Vector3(-0.53f * stretchX, 0, 0), 0, stretchX, lift);
                }
                else if (s == 0)
                {
                    Add(house, brick ? "Wall_UnevenBrick_Window_Wide_Flat" : "Wall_Plaster_Straight_Base", at, 0, stretchX, lift);
                    if (brick)
                    {
                        Add(house, "Window_Wide_Flat1", at, 0, stretchX, lift);
                    }
                }
                else
                {
                    var round = rng.Randf() < 0.3f;
                    Add(house, round ? "Wall_Plaster_Window_Thin_Round" : "Wall_Plaster_Window_Wide_Flat", at, 0, stretchX, lift);
                    Add(house, round ? "Window_Thin_Round1" : "Window_Wide_Flat1", at, 0, stretchX, lift);
                    Add(house, round ? "WindowShutters_Thin_Round_Open" : "WindowShutters_Wide_Flat_Open", at, 0, stretchX, lift);
                    if (s == 1 && i == door && across >= 3 && rng.Randf() < 0.5f)
                    {
                        Add(house, "Balcony_Simple_Straight", at, 0, stretchX, lift);
                    }
                }
            }

            // The back, to the north, plain.
            for (var i = 0; i < across; i++)
            {
                Add(house, "Wall_Plaster_Straight", new Vector3(left + (i + 0.5f) * Piece * stretchX, y, back), 180, stretchX, lift);
            }

            // The sides: the east one faces the camera, so it has windows above the ground floor.
            for (var j = 0; j < deep; j++)
            {
                var z = back + (j + 0.5f) * Piece * stretchZ;
                var window = s > 0 && j == deep / 2;
                Add(house, window ? "Wall_Plaster_Window_Wide_Flat" : (s == 0 ? "Wall_Plaster_Straight_Base" : "Wall_Plaster_Straight"), new Vector3(right, y, z), 90, stretchZ, lift);
                if (window)
                {
                    Add(house, "Window_Wide_Flat1", new Vector3(right, y, z), 90, stretchZ, lift);
                    Add(house, "WindowShutters_Wide_Flat_Open", new Vector3(right, y, z), 90, stretchZ, lift);
                }

                Add(house, s == 0 ? "Wall_Plaster_Straight_Base" : "Wall_Plaster_Straight", new Vector3(left, y, z), -90, stretchZ, lift);
            }

            // The corners, dressed.
            foreach (var (cx, cz) in new[] { (left, front), (right, front), (left, back), (right, back) })
            {
                Add(house, s == 0 ? "Corner_Exterior_Brick" : "Corner_Exterior_Wood", new Vector3(cx, y, cz), 0, 1, lift);
            }
        }

        // The roof: the kit's nearest in size, stretched to the walls.
        var (roofAcross, roofDeep) = Roofs.OrderBy(r => Mathf.Abs(r.Across - (right - left)) + Mathf.Abs(r.Deep - (front - back))).First();
        var roof = Add(house, $"Roof_RoundTiles_{roofAcross}x{roofDeep}", new Vector3((left + right) / 2f, baseHeight + storeys * storeyHeight, (back + front) / 2f), 0, 1, 1);
        if (roof is not null)
        {
            roof.Scale = new Vector3((right - left) / roofAcross, 1, (front - back) / roofDeep);
        }

        // The kit's roofs have their gables front and back: the gable ends filled to the street and behind.
        var gable = roofAcross switch { <= 4 => 4, <= 6 => 6, _ => 8 };
        var eave = baseHeight + storeys * storeyHeight;
        foreach (var (z, yaw) in new[] { (front, 0f), (back, 180f) })
        {
            if (Add(house, $"Roof_Front_Brick{gable}", new Vector3((left + right) / 2f, eave, z), yaw, 1, 1) is { } end)
            {
                end.Scale = new Vector3((right - left) / gable, 1, 1);
            }
        }

        if (rng.Randf() < 0.6f)
        {
            Add(house, "Prop_Chimney", new Vector3(right - 1.2f, baseHeight + storeys * storeyHeight + 1.2f, (back + front) / 2f - 0.6f), 0, 1, 1);
        }

        return house;
    }

    // One of the kit's pieces, set at a place along a wall facing out (yaw 0 = south, 90 = east), stretched
    // along the wall and lifted to the storey's height.
    private static Node3D? Add(Node3D house, string piece, Vector3 at, float yaw, float stretch, float lift)
    {
        if (!Scenes.TryGetValue(piece, out var scene))
        {
            scene = ResourceLoader.Exists(Kit + piece + ".gltf") ? GD.Load<PackedScene>(Kit + piece + ".gltf") : null;
            Scenes[piece] = scene;
        }

        if (scene?.Instantiate() is not Node3D node)
        {
            return null;
        }

        node.Position = at;
        node.RotationDegrees = new Vector3(0, yaw, 0);
        node.Scale = new Vector3(stretch, lift, 1);
        house.AddChild(node);
        return node;
    }
}
