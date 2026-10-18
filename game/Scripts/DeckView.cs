using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// Graybox drawing of a location grid: planks for walkable cells, low blocks for rails and fittings,
/// tall poles for masts, and flat markers for work spots. One cell is one metre; grid X is world X and
/// grid Y is world Z (D-006). Purely visual: it holds no game state.
/// </summary>
public partial class DeckView : Node3D
{
    private static readonly Color Plank = new(0.55f, 0.42f, 0.29f);
    private static readonly Color PlankAlternate = new(0.51f, 0.39f, 0.27f);
    private static readonly Color Fitting = new(0.36f, 0.27f, 0.19f);
    private static readonly Color Hull = new(0.27f, 0.2f, 0.15f);
    private static readonly Color SpotMarker = new(0.83f, 0.76f, 0.58f);

    /// <summary>Builds the meshes for <paramref name="location"/>, replacing any earlier ones.</summary>
    public void Build(LocationDefinition location)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        for (var y = 0; y < location.Height; y++)
        {
            for (var x = 0; x < location.Width; x++)
            {
                var cell = new GridPosition(x, y);
                if (location.IsWalkable(cell))
                {
                    AddBox(new Vector3(x, -0.06f, y), new Vector3(0.98f, 0.12f, 0.98f), (x + y) % 2 == 0 ? Plank : PlankAlternate);
                    continue;
                }

                var walkableNeighbours = Neighbours(cell).Count(location.IsWalkable);
                if (walkableNeighbours == 4)
                {
                    AddMast(new Vector3(x, 0, y));
                }
                else if (walkableNeighbours > 0)
                {
                    AddBox(new Vector3(x, 0.35f, y), new Vector3(1f, 0.7f, 1f), Fitting);
                }
            }
        }

        // A dark hull block under the deck so the ship reads as one object on the water.
        var centre = new Vector3((location.Width - 1) / 2f, -0.9f, (location.Height - 1) / 2f);
        AddBox(centre, new Vector3(location.Width - 0.5f, 1.6f, location.Height - 0.5f), Hull);

        foreach (var spot in location.Spots)
        {
            var marker = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.32f, Height = 0.02f },
                MaterialOverride = Material(SpotMarker),
                Position = new Vector3(spot.X, 0.01f, spot.Y),
            };
            AddChild(marker);
            AddChild(new Label3D
            {
                Text = spot.Id.Replace("spot.", "").Replace('_', ' '),
                Position = new Vector3(spot.X, 0.05f, spot.Y + 0.45f),
                RotationDegrees = new Vector3(-90, 0, 0),
                FontSize = 28,
                PixelSize = 0.006f,
                Modulate = new Color(0.15f, 0.12f, 0.1f),
                OutlineSize = 0,
            });
        }
    }

    private static IEnumerable<GridPosition> Neighbours(GridPosition cell) =>
    [
        cell with { Y = cell.Y - 1 }, cell with { X = cell.X + 1 }, cell with { Y = cell.Y + 1 }, cell with { X = cell.X - 1 },
    ];

    private void AddBox(Vector3 position, Vector3 size, Color color) =>
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = Material(color), Position = position });

    private void AddMast(Vector3 foot)
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.22f, Height = 9f },
            MaterialOverride = Material(Fitting),
            Position = foot + new Vector3(0, 4.5f, 0),
        });
    }

    private static StandardMaterial3D Material(Color color) => new() { AlbedoColor = color, Roughness = 0.9f };
}
