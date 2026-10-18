namespace Expedition.Simulation.World;

/// <summary>
/// Where a character is: which location, which cell, and any walk in progress. Part of persistent
/// campaign state, so positions survive save/load. Characters without a placement are not on any
/// map (for example, candidates the player has not met yet).
/// </summary>
public sealed class CharacterPlacement
{
    /// <summary>Places a standing character.</summary>
    public CharacterPlacement(string locationId, GridPosition cell)
    {
        LocationId = locationId;
        Cell = cell;
    }

    /// <summary>The <see cref="LocationDefinition"/> the character is in.</summary>
    public string LocationId { get; }

    /// <summary>The cell the character is standing on, or where their current walk started.</summary>
    public GridPosition Cell { get; internal set; }

    /// <summary>The walk in progress, or null when standing still.</summary>
    public Movement? Movement { get; internal set; }

    /// <summary>True while walking.</summary>
    public bool IsMoving => Movement is not null;

    /// <summary>The cell the character occupies at <paramref name="tick"/>, accounting for any walk.</summary>
    public GridPosition CellAt(long tick) => Movement?.CellAt(tick) ?? Cell;
}

/// <summary>
/// A walk along <see cref="Path"/> that starts at <see cref="StartTick"/> and arrives at
/// <see cref="ArriveTick"/>. Progress along the path is proportional to time, which lets the
/// presentation draw the character smoothly between ticks.
/// </summary>
/// <param name="Path">Cells from start to destination, both included. At least two cells.</param>
/// <param name="StartTick">Tick the walk began.</param>
/// <param name="ArriveTick">Tick the character reaches the last cell. Always after <paramref name="StartTick"/>.</param>
public sealed record Movement(IReadOnlyList<GridPosition> Path, long StartTick, long ArriveTick)
{
    /// <summary>Final cell of the walk.</summary>
    public GridPosition Destination => Path[^1];

    /// <summary>
    /// Position along the path at a possibly fractional <paramref name="tick"/>, in cell units.
    /// Intended for drawing; simulation rules use <see cref="CellAt"/>.
    /// </summary>
    public (double X, double Y) PositionAt(double tick)
    {
        var fraction = Math.Clamp((tick - StartTick) / (ArriveTick - StartTick), 0.0, 1.0);
        var along = fraction * (Path.Count - 1);
        var index = Math.Min((int)along, Path.Count - 2);
        var local = along - index;
        var a = Path[index];
        var b = Path[index + 1];
        return (a.X + (b.X - a.X) * local, a.Y + (b.Y - a.Y) * local);
    }

    /// <summary>The path cell reached by <paramref name="tick"/>.</summary>
    public GridPosition CellAt(long tick)
    {
        var fraction = Math.Clamp((double)(tick - StartTick) / (ArriveTick - StartTick), 0.0, 1.0);
        return Path[(int)Math.Floor(fraction * (Path.Count - 1))];
    }
}
