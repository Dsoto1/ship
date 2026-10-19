using Expedition.Simulation.Core;

namespace Expedition.Simulation.Knowledge;

/// <summary>
/// How a piece of knowledge was obtained, in increasing order of reliability (TECHNICAL_SPEC.md §5.2, §15.3).
/// Claims and references can be wrong, on purpose or not; observation and tests show the truth.
/// </summary>
public enum KnowledgeSource
{
    /// <summary>Overheard or passed on; may be wrong.</summary>
    Rumour,

    /// <summary>What the person says about themselves; may be honest, mistaken or a lie.</summary>
    Claim,

    /// <summary>What someone who knows them says; may be biased.</summary>
    Reference,

    /// <summary>Seen while they worked.</summary>
    Observation,

    /// <summary>A deliberate test or demonstration.</summary>
    Test,
}

/// <summary>
/// One thing the captain has learned about a character.
/// </summary>
/// <param name="Tick">When it was learned.</param>
/// <param name="Subject">Who it is about.</param>
/// <param name="Topic">What it is about: a skill, attribute, language or trait ID.</param>
/// <param name="Level">The reported or observed level (0–5); for traits, 1 = has it and 0 = does not.</param>
/// <param name="Source">How it was learned.</param>
/// <param name="Informant">Who said it, for references and rumours; null otherwise.</param>
public sealed record KnowledgeEntry(long Tick, CharacterId Subject, string Topic, int Level, KnowledgeSource Source, CharacterId? Informant = null);

/// <summary>The captain's current best understanding of one topic about one character.</summary>
/// <param name="Level">The believed level.</param>
/// <param name="Source">The most reliable kind of evidence behind it.</param>
/// <param name="Conflicting">True if other evidence disagrees with this level.</param>
public sealed record Assessment(int Level, KnowledgeSource Source, bool Conflicting);

/// <summary>
/// What the captain (the player) knows about people, kept separate from the truth stored on each
/// <see cref="Characters.Character"/>. Recruitment and other interfaces must show assessments from here,
/// never the hidden truth (TECHNICAL_SPEC.md §5.2). Every entry is kept, so the player can see why
/// they believe something and who told them.
/// </summary>
public sealed class KnowledgeBook
{
    private readonly List<KnowledgeEntry> _entries = [];

    /// <summary>Everything learned, oldest first.</summary>
    public IReadOnlyList<KnowledgeEntry> Entries => _entries;

    /// <summary>Everything learned about <paramref name="subject"/>, oldest first.</summary>
    public IEnumerable<KnowledgeEntry> About(CharacterId subject) => _entries.Where(e => e.Subject == subject);

    /// <summary>
    /// The best current belief about one topic: the most reliable source wins, and among equally
    /// reliable sources, the most recent. Null if nothing is known.
    /// </summary>
    public Assessment? Assess(CharacterId subject, string topic)
    {
        var evidence = _entries.Where(e => e.Subject == subject && e.Topic == topic).ToList();
        if (evidence.Count == 0)
        {
            return null;
        }

        var best = evidence.OrderByDescending(e => e.Source).ThenByDescending(e => e.Tick).First();
        return new Assessment(best.Level, best.Source, evidence.Any(e => e.Level != best.Level));
    }

    /// <summary>Topics anything is known about for <paramref name="subject"/>, in order.</summary>
    public IReadOnlyList<string> TopicsAbout(CharacterId subject) =>
        _entries.Where(e => e.Subject == subject).Select(e => e.Topic).Distinct().Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// Records something learned. Repeating exactly the same fact from the same source and informant is
    /// ignored, so watching someone work every day does not flood the book.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The level is outside 0..5.</exception>
    internal bool Learn(KnowledgeEntry entry)
    {
        if (entry.Level is < 0 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(entry), entry.Level, "Known levels must be between 0 and 5.");
        }

        if (_entries.Any(e => e.Subject == entry.Subject && e.Topic == entry.Topic && e.Level == entry.Level
                              && e.Source == entry.Source && e.Informant == entry.Informant))
        {
            return false;
        }

        _entries.Add(entry);
        return true;
    }

    /// <summary>Adds an entry read from a save.</summary>
    internal void Restore(KnowledgeEntry entry) => _entries.Add(entry);
}
