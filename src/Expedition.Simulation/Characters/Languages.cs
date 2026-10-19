namespace Expedition.Simulation.Characters;

/// <summary>Sex, recorded as a fact about the character. Older saves have <see cref="Unrecorded"/>.</summary>
public enum Sex
{
    /// <summary>Not recorded (characters from saves before format 8, or created without it).</summary>
    Unrecorded,

    /// <summary>Male.</summary>
    Male,

    /// <summary>Female.</summary>
    Female,
}

/// <summary>
/// Languages a character speaks, keyed by language definition ID (for example <c>language.ahuneri</c>),
/// on the same 0–5 scale as skills: 0 none, 2 can get by, 3 fluent, 5 native and eloquent.
/// Unrecorded languages are 0.
/// </summary>
public sealed class Languages
{
    /// <summary>Lowest valid level.</summary>
    public const int MinLevel = 0;

    /// <summary>Highest valid level.</summary>
    public const int MaxLevel = 5;

    private readonly Dictionary<string, int> _levels = new(StringComparer.Ordinal);

    /// <summary>Languages spoken at level 1 or above.</summary>
    public IReadOnlyDictionary<string, int> Levels => _levels;

    /// <summary>Level in <paramref name="languageId"/>, or 0.</summary>
    public int Get(string languageId) => _levels.GetValueOrDefault(languageId);

    /// <summary>Sets a level; 0 removes the entry.</summary>
    /// <exception cref="ArgumentException">The language ID is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The level is outside 0..5.</exception>
    public void Set(string languageId, int level)
    {
        if (string.IsNullOrWhiteSpace(languageId))
        {
            throw new ArgumentException("Language ID must not be empty.", nameof(languageId));
        }

        if (level is < MinLevel or > MaxLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, $"Language levels must be between {MinLevel} and {MaxLevel}.");
        }

        if (level == 0)
        {
            _levels.Remove(languageId);
        }
        else
        {
            _levels[languageId] = level;
        }
    }
}
