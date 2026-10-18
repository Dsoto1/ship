namespace Expedition.Simulation.World;

/// <summary>A cell on a location's walkable grid. One cell is roughly one metre.</summary>
/// <param name="X">Column, 0 at the left of the layout.</param>
/// <param name="Y">Row, 0 at the top of the layout.</param>
public readonly record struct GridPosition(int X, int Y)
{
    /// <summary>Steps needed to walk between two cells on an open grid (no diagonals).</summary>
    public int ManhattanDistance(GridPosition other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    /// <summary>Readable form such as <c>(3, 7)</c>.</summary>
    public override string ToString() => $"({X}, {Y})";
}
