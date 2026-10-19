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

    /// <summary>
    /// Walking speed here, in cells per game minute, or null for the rule-wide speed. Slower in port so
    /// people can be watched going about their day (D-023).
    /// </summary>
    public double? WalkCellsPerMinute { get; init; }

    /// <summary>Raised ground and steps, for drawing (the customs terrace). The simulation's grid is flat.</summary>
    public IReadOnlyList<GroundArea> Ground { get; init; } = [];

    /// <summary>What stands where (buildings, stalls, cargo), for drawing. The simulation ignores these.</summary>
    public IReadOnlyList<LocationFeature> Features { get; init; } = [];

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

    /// <summary>
    /// What kind of place this is, for how people are drawn there (PORT_EXPERIENCE §7), for example
    /// <c>SitPoint</c>, <c>CarryPickup</c> or <c>HammerStation</c>. Null for none in particular.
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>The spot's cell.</summary>
    public GridPosition Cell => new(X, Y);
}

/// <summary>
/// A rectangle of raised ground, inclusive of both corners. With <see cref="ToHeight"/> it is a ramp or
/// flight of steps, falling from <see cref="Height"/> to <see cref="ToHeight"/> along <see cref="Along"/>.
/// </summary>
public sealed record GroundArea
{
    /// <summary>First column.</summary>
    public required int X0 { get; init; }
    /// <summary>First row.</summary>
    public required int Y0 { get; init; }
    /// <summary>Last column.</summary>
    public required int X1 { get; init; }
    /// <summary>Last row.</summary>
    public required int Y1 { get; init; }

    /// <summary>Height in metres (at the low-coordinate edge, for a ramp).</summary>
    public required double Height { get; init; }

    /// <summary>Height at the high-coordinate edge, for a ramp; null for flat ground.</summary>
    public double? ToHeight { get; init; }

    /// <summary>"x" or "y": the direction a ramp falls along.</summary>
    public string? Along { get; init; }

    /// <summary>Ground height in metres at a point in cell coordinates, or null if outside the area.</summary>
    public double? HeightAt(double x, double y)
    {
        if (x < X0 - 0.5 || x > X1 + 0.5 || y < Y0 - 0.5 || y > Y1 + 0.5)
        {
            return null;
        }

        if (ToHeight is not { } to)
        {
            return Height;
        }

        var t = Along == "x" ? (x - (X0 - 0.5)) / (X1 - X0 + 1) : (y - (Y0 - 0.5)) / (Y1 - Y0 + 1);
        return Height + (to - Height) * Math.Clamp(t, 0, 1);
    }
}

/// <summary>Something drawn in a place, over cells X0..X1, Y0..Y1 inclusive: a building, a stall, a cask stack.</summary>
public sealed record LocationFeature
{
    /// <summary>What it is, for example <c>building</c>, <c>stall</c>, <c>cargo</c>, <c>ship</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>First column.</summary>
    public required int X0 { get; init; }
    /// <summary>First row.</summary>
    public required int Y0 { get; init; }
    /// <summary>Last column.</summary>
    public required int X1 { get; init; }
    /// <summary>Last row.</summary>
    public required int Y1 { get; init; }

    /// <summary>Which one, for example <c>chamber</c> or <c>tavern</c> for a building.</summary>
    public string? Style { get; init; }
}
