using Expedition.Simulation.Core;

namespace Expedition.Simulation.Characters;

/// <summary>One dimension of how a character regards another (TECHNICAL_SPEC.md §6).</summary>
public enum RelationshipDimension
{
    /// <summary>Belief that the other will be honest and keep their word.</summary>
    Trust,

    /// <summary>Regard for the other's competence and standing.</summary>
    Respect,

    /// <summary>Liking (positive) or hostility (negative).</summary>
    Affection,
}

/// <summary>One recorded reason a relationship changed.</summary>
/// <param name="Tick">When it happened.</param>
/// <param name="Dimension">Which dimension changed.</param>
/// <param name="Delta">Requested change; the value itself stays within -100..100.</param>
/// <param name="Cause">Why: a content or event ID, or free text from a scenario.</param>
public sealed record RelationshipChange(long Tick, RelationshipDimension Dimension, int Delta, string Cause);

/// <summary>
/// How <see cref="From"/> regards <see cref="To"/>. Directed: Diego can trust the captain while the
/// captain distrusts Diego, and those are two separate records. Each value runs from -100 to 100, 0 is
/// neutral, and <see cref="History"/> keeps every reason so the game can explain why a relationship
/// is what it is. Relationships with dead characters remain valid history.
/// </summary>
public sealed class Relationship
{
    /// <summary>Lowest value of any dimension.</summary>
    public const int Min = -100;

    /// <summary>Highest value of any dimension.</summary>
    public const int Max = 100;

    private readonly List<RelationshipChange> _history = [];
    private readonly int[] _values = new int[Enum.GetValues<RelationshipDimension>().Length];

    /// <summary>Creates a neutral relationship.</summary>
    /// <exception cref="ArgumentException">Both IDs are the same.</exception>
    public Relationship(CharacterId from, CharacterId to)
    {
        if (from == to)
        {
            throw new ArgumentException($"{from} cannot have a relationship with themselves.");
        }

        From = from;
        To = to;
    }

    /// <summary>The character who holds the view.</summary>
    public CharacterId From { get; }

    /// <summary>The character the view is about.</summary>
    public CharacterId To { get; }

    /// <summary>Trust, -100..100.</summary>
    public int Trust => Get(RelationshipDimension.Trust);

    /// <summary>Respect, -100..100.</summary>
    public int Respect => Get(RelationshipDimension.Respect);

    /// <summary>Affection (negative is hostility), -100..100.</summary>
    public int Affection => Get(RelationshipDimension.Affection);

    /// <summary>Every recorded change, oldest first.</summary>
    public IReadOnlyList<RelationshipChange> History => _history;

    /// <summary>The value of one dimension.</summary>
    public int Get(RelationshipDimension dimension) => _values[(int)dimension];

    /// <summary>Applies a change, clamping to the valid range, and remembers why.</summary>
    internal void Apply(RelationshipChange change)
    {
        var index = (int)change.Dimension;
        _values[index] = Math.Clamp(_values[index] + change.Delta, Min, Max);
        _history.Add(change);
    }

    /// <summary>Sets values and history directly while loading a save.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A value is outside -100..100.</exception>
    internal void Restore(int trust, int respect, int affection, IEnumerable<RelationshipChange> history)
    {
        foreach (var value in new[] { trust, respect, affection })
        {
            if (value is < Min or > Max)
            {
                throw new ArgumentOutOfRangeException(nameof(trust), value, $"Relationship values must be between {Min} and {Max}.");
            }
        }

        _values[(int)RelationshipDimension.Trust] = trust;
        _values[(int)RelationshipDimension.Respect] = respect;
        _values[(int)RelationshipDimension.Affection] = affection;
        _history.AddRange(history);
    }
}
