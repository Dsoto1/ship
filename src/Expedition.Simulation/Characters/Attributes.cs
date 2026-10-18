namespace Expedition.Simulation.Characters;

/// <summary>
/// A character's physical capabilities (strength, endurance, coordination, perception), separate from
/// trained skills (TECHNICAL_SPEC.md §5.4). Same 0–5 scale as skills, but an unrecorded attribute is
/// <see cref="Ordinary"/> rather than zero: everyone has some strength.
/// </summary>
/// <remarks>
/// Scale: 0 very weak or impaired, 1 below average, 2 ordinary, 3 strong or capable, 4 notable,
/// 5 exceptional. Keyed by attribute definition ID, for example <c>attribute.strength</c>.
/// </remarks>
public sealed class Attributes
{
    /// <summary>Lowest valid level.</summary>
    public const int MinLevel = 0;

    /// <summary>Level of anyone not described otherwise.</summary>
    public const int Ordinary = 2;

    /// <summary>Highest valid level.</summary>
    public const int MaxLevel = 5;

    private readonly Dictionary<string, int> _levels = new(StringComparer.Ordinal);

    /// <summary>Attributes that differ from <see cref="Ordinary"/>.</summary>
    public IReadOnlyDictionary<string, int> Levels => _levels;

    /// <summary>The level of <paramref name="attributeId"/>; <see cref="Ordinary"/> if not recorded.</summary>
    public int Get(string attributeId) => _levels.GetValueOrDefault(attributeId, Ordinary);

    /// <summary>Sets a level. Setting <see cref="Ordinary"/> removes the entry.</summary>
    /// <exception cref="ArgumentException">The attribute ID is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The level is outside 0..5.</exception>
    public void Set(string attributeId, int level)
    {
        if (string.IsNullOrWhiteSpace(attributeId))
        {
            throw new ArgumentException("Attribute ID must not be empty.", nameof(attributeId));
        }

        if (level is < MinLevel or > MaxLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, $"Attribute levels must be between {MinLevel} and {MaxLevel}.");
        }

        if (level == Ordinary)
        {
            _levels.Remove(attributeId);
        }
        else
        {
            _levels[attributeId] = level;
        }
    }
}
