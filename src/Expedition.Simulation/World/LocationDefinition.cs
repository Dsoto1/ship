namespace Expedition.Simulation.World;

/// <summary>
/// A place characters can stand and walk, such as a ship's deck, loaded from <c>data/locations</c>.
/// The layout is a grid of rows: <c>.</c> is walkable and <c>#</c> is blocked (rails, masts, hatches).
/// Work spots mark where particular tasks are done.
/// </summary>
public sealed record LocationDefinition
{
    /// <summary>Walkable cell character in <see cref="Rows"/>.</summary>
    public const char Walkable = '.';

    /// <summary>Blocked cell character in <see cref="Rows"/>.</summary>
    public const char Blocked = '#';

    /// <summary>Unique ID, for example <c>location.test_deck</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Layout rows, top to bottom. All rows have the same length.</summary>
    public required IReadOnlyList<string> Rows { get; init; }

    /// <summary>Places where specific tasks are done. One worker per spot.</summary>
    public IReadOnlyList<WorkSpotDefinition> Spots { get; init; } = [];

    /// <summary>
    /// How far, in cells, the captain can see what people are doing here; null means everything (a ship's
    /// deck, where the captain hears of all work). Used for observing work and secrets (R1).
    /// </summary>
    public int? SightRange { get; init; }

    /// <summary>Number of columns.</summary>
    public int Width => Rows.Count == 0 ? 0 : Rows[0].Length;

    /// <summary>Number of rows.</summary>
    public int Height => Rows.Count;

    /// <summary>True if <paramref name="cell"/> is inside the grid and walkable.</summary>
    public bool IsWalkable(GridPosition cell) =>
        cell.Y >= 0 && cell.Y < Height && cell.X >= 0 && cell.X < Rows[cell.Y].Length && Rows[cell.Y][cell.X] == Walkable;

    /// <summary>The spot with <paramref name="spotId"/>, or null.</summary>
    public WorkSpotDefinition? FindSpot(string spotId) => Spots.FirstOrDefault(s => s.Id == spotId);
}

/// <summary>A cell where one or more kinds of task are done, such as the pump or the galley fire.</summary>
public sealed record WorkSpotDefinition
{
    /// <summary>ID unique within its location, for example <c>spot.pump</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Column.</summary>
    public required int X { get; init; }

    /// <summary>Row.</summary>
    public required int Y { get; init; }

    /// <summary>Task definition IDs that are done here.</summary>
    public required IReadOnlyList<string> Tasks { get; init; }

    /// <summary>True if the spot is inside a building: people there cannot be seen from outside.</summary>
    public bool Indoors { get; init; }

    /// <summary>The spot's cell.</summary>
    public GridPosition Cell => new(X, Y);
}
