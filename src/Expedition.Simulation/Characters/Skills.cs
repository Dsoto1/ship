namespace Expedition.Simulation.Characters;

/// <summary>
/// A character's actual skill levels, keyed by skill definition ID (for example <c>skill.carpentry</c>).
/// Uses the five-level scale from the vision document: 0 = no training to 5 = exceptional specialist.
/// This is the truth; what the captain believes about a character's skills is tracked separately (later ticket).
/// </summary>
public sealed class Skills
{
    /// <summary>Lowest valid skill level: no meaningful training.</summary>
    public const int MinLevel = 0;

    /// <summary>Routine competence: the level at which work proceeds at normal speed.</summary>
    public const int RoutineLevel = 2;

    /// <summary>Highest valid skill level: exceptional specialist.</summary>
    public const int MaxLevel = 5;

    private readonly Dictionary<string, int> _levels = new(StringComparer.Ordinal);

    /// <summary>All skills with a level above zero.</summary>
    public IReadOnlyDictionary<string, int> Levels => _levels;

    /// <summary>Returns the level for <paramref name="skillId"/>, or 0 if the character has no training in it.</summary>
    public int Get(string skillId) => _levels.GetValueOrDefault(skillId, MinLevel);

    /// <summary>Sets the level for <paramref name="skillId"/>. Setting 0 removes the entry.</summary>
    /// <exception cref="ArgumentException">The skill ID is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The level is outside 0..5.</exception>
    public void Set(string skillId, int level)
    {
        if (string.IsNullOrWhiteSpace(skillId))
        {
            throw new ArgumentException("Skill ID must not be empty.", nameof(skillId));
        }

        if (level is < MinLevel or > MaxLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, $"Skill levels must be between {MinLevel} and {MaxLevel}.");
        }

        if (level == MinLevel)
        {
            _levels.Remove(skillId);
        }
        else
        {
            _levels[skillId] = level;
        }
    }
}
