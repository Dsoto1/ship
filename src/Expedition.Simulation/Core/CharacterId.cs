namespace Expedition.Simulation.Core;

/// <summary>
/// Stable, campaign-scoped identifier for a persistent character.
/// The same ID refers to the same person in every campaign phase and after save/load.
/// </summary>
/// <param name="Value">Positive integer issued by <see cref="IdAllocator"/>.</param>
public readonly record struct CharacterId(int Value) : IComparable<CharacterId>
{
    /// <summary>Orders IDs by their number, which is also the order they were issued.</summary>
    public int CompareTo(CharacterId other) => Value.CompareTo(other.Value);

    /// <summary>Returns a readable form such as <c>char-0007</c> for logs and the chronicle.</summary>
    public override string ToString() => $"char-{Value:D4}";
}
