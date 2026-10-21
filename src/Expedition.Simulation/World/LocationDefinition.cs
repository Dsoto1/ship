namespace Expedition.Simulation.World;

/// <summary>
/// A place characters can stand and walk, such as a ship's deck, loaded from <c>data/locations</c>.
/// The layout is a grid of rows: <c>.</c> is walkable and <c>#</c> is blocked (rails, masts, hatches);
/// <c>o</c> is a fitting on the deck (a gun, the capstan): blocked to walkers, but part of the deck.
/// Work spots mark where particular tasks are done.
/// </summary>
public sealed record LocationDefinition
{
    /// <summary>Walkable cell character in <see cref="Rows"/>.</summary>
    public const char Walkable = '.';

    /// <summary>Blocked cell character in <see cref="Rows"/>.</summary>
    public const char Blocked = '#';

    /// <summary>
    /// A fitting standing on the deck, such as a gun or the capstan: no one walks there, but it is deck,
    /// not rail. Which fitting it is, the location's <see cref="Features"/> say (kind <c>fitting</c>).
    /// </summary>
    public const char Fitting = 'o';

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

    /// <summary>
    /// Rooms that can be walked into, such as a tavern's common room. Walls hide what goes on inside: only
    /// someone in the same room sees and speaks with those in it.
    /// </summary>
    public IReadOnlyList<RoomDefinition> Rooms { get; init; } = [];

    /// <summary>The room <paramref name="cell"/> is in, or null outdoors.</summary>
    public RoomDefinition? RoomAt(GridPosition cell) =>
        Rooms.FirstOrDefault(r => cell.X >= r.X0 && cell.X <= r.X1 && cell.Y >= r.Y0 && cell.Y <= r.Y1);

    /// <summary>Number of columns.</summary>
    public int Width => Rows.Count == 0 ? 0 : Rows[0].Length;

    /// <summary>Number of rows.</summary>
    public int Height => Rows.Count;

    /// <summary>True if <paramref name="cell"/> is inside the grid and walkable.</summary>
    public bool IsWalkable(GridPosition cell) =>
        cell.Y >= 0 && cell.Y < Height && cell.X >= 0 && cell.X < Rows[cell.Y].Length && Rows[cell.Y][cell.X] == Walkable;

    /// <summary>True if <paramref name="cell"/> is deck: walkable, or a fitting standing on it.</summary>
    public bool IsDeck(GridPosition cell) =>
        IsWalkable(cell) || (cell.Y >= 0 && cell.Y < Height && cell.X >= 0 && cell.X < Rows[cell.Y].Length && Rows[cell.Y][cell.X] == Fitting);

    /// <summary>The spot with <paramref name="spotId"/>, or null.</summary>
    public WorkSpotDefinition? FindSpot(string spotId) => Spots.FirstOrDefault(s => s.Id == spotId);
}

/// <summary>A room inside a building, inclusive of both corners: walls all round, and a way in.</summary>
public sealed record RoomDefinition
{
    /// <summary>ID, for example <c>room.three_anchors</c>.</summary>
    public required string Id { get; init; }

    /// <summary>What it is called in a sentence: "the Three Anchors".</summary>
    public required string Name { get; init; }

    /// <summary>West edge.</summary>
    public required int X0 { get; init; }

    /// <summary>North edge.</summary>
    public required int Y0 { get; init; }

    /// <summary>East edge.</summary>
    public required int X1 { get; init; }

    /// <summary>South edge.</summary>
    public required int Y1 { get; init; }

    /// <summary>What it costs to stand everyone here a round, or null where no drink is sold.</summary>
    public int? Round { get; init; }
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

    /// <summary>
    /// The compass direction someone here faces while at work, for clusters such as a seller and a
    /// customer across a stall (N is towards row 0); null to face whatever is beside them.
    /// </summary>
    public string? Face { get; init; }

    /// <summary>Compass directions allowed in <see cref="Face"/>.</summary>
    public static readonly IReadOnlyList<string> Compass = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];

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
