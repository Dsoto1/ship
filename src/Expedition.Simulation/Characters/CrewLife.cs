using Expedition.Simulation.Core;

namespace Expedition.Simulation.Characters;

/// <summary>
/// How far a grievance has gone (VOYAGE_EXPERIENCE §19): muttered, said to the captain, shared with others,
/// and at last work refused. Each step is reached in turn, so the captain always hears of it before the worst.
/// </summary>
public enum GrievanceStage
{
    /// <summary>Nothing worth saying.</summary>
    None,

    /// <summary>Muttered about, forward and in the watch below.</summary>
    Concern,

    /// <summary>Brought to the captain.</summary>
    Complaint,

    /// <summary>Others with the same complaint have gathered round them.</summary>
    Faction,

    /// <summary>They refuse all but the work that keeps them alive.</summary>
    Refusal,
}

/// <summary>What one person holds against the voyage: a score for each cause, and how far it has gone.</summary>
public sealed class Grievance
{
    private readonly SortedDictionary<string, double> _causes = new(StringComparer.Ordinal);

    /// <summary>Each cause and how much it weighs, 0..100. Causes: <see cref="GrievanceCauses"/>.</summary>
    public IReadOnlyDictionary<string, double> Causes => _causes;

    /// <summary>All causes together, 0..100.</summary>
    public double Total => Math.Min(100, _causes.Values.Sum());

    /// <summary>The cause that weighs most, or null.</summary>
    public string? Main => _causes.Where(c => c.Value >= 1).OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal).Select(c => c.Key).FirstOrDefault();

    /// <summary>How far it has gone.</summary>
    public GrievanceStage Stage { get; internal set; }

    /// <summary>When it last changed stage.</summary>
    public long StageTick { get; internal set; }

    /// <summary>The furthest stage the captain has been brought to face (the complaint, the refusal).</summary>
    public GrievanceStage Raised { get; internal set; }

    internal void Add(string cause, double amount)
    {
        var value = Math.Clamp(_causes.GetValueOrDefault(cause) + amount, 0, 100);
        if (value <= 0)
        {
            _causes.Remove(cause);
        }
        else
        {
            _causes[cause] = value;
        }
    }
}

/// <summary>The causes of grievances.</summary>
public static class GrievanceCauses
{
    /// <summary>Short rations.</summary>
    public const string Rations = "rations";

    /// <summary>Going hungry or thirsty.</summary>
    public const string Hunger = "hunger";

    /// <summary>Worked to exhaustion.</summary>
    public const string Overwork = "overwork";

    /// <summary>Pressing on in heavy weather.</summary>
    public const string Danger = "danger";

    /// <summary>The sick left untended.</summary>
    public const string Sick = "sick";

    /// <summary>The captain's hard hand: they think badly of the captain.</summary>
    public const string Harsh = "harsh";

    /// <summary>A promise the captain did not keep.</summary>
    public const string Promise = "promise";

    /// <summary>Every cause.</summary>
    public static readonly IReadOnlyList<string> All = [Rations, Hunger, Overwork, Danger, Sick, Harsh, Promise];
}

/// <summary>Someone learning a skill from a more experienced shipmate (VOYAGE_EXPERIENCE §26).</summary>
/// <param name="Mentor">Who teaches.</param>
/// <param name="Skill">The skill they teach.</param>
/// <param name="Since">When it began.</param>
public sealed record Mentorship(CharacterId Mentor, string Skill, long Since);

/// <summary>A promise the captain made to the crew, to be kept by a time.</summary>
/// <param name="Cause">What it answers, a <see cref="GrievanceCauses"/> value.</param>
/// <param name="Due">When it must be kept by.</param>
public sealed record Promise(string Cause, long Due);

/// <summary>
/// The life of the crew aboard (VOYAGE_EXPERIENCE §15–19, §25–26): what each has practised, who learns
/// from whom, what they hold against the voyage, when they last ate anything fresh, the promises the
/// captain made, and how clean the ship is kept. Part of the campaign state.
/// </summary>
public sealed class CrewLife
{
    private readonly Dictionary<CharacterId, SortedDictionary<string, double>> _practice = [];
    private readonly Dictionary<CharacterId, Mentorship> _mentors = [];
    private readonly Dictionary<CharacterId, Grievance> _grievances = [];
    private readonly Dictionary<CharacterId, long> _lastFresh = [];
    private readonly List<Promise> _promises = [];

    /// <summary>How clean the ship is kept below, 0..100: low, and sickness spreads.</summary>
    public double Cleanliness { get; internal set; } = 90;

    /// <summary>How many were sick when it was last recorded, so a spreading sickness is told as it grows.</summary>
    public int SickNoted { get; internal set; }

    /// <summary>Hours of practice in each skill, towards the next level, by person.</summary>
    public IReadOnlyDictionary<CharacterId, SortedDictionary<string, double>> Practice => _practice;

    /// <summary>Who learns from whom: learner to mentorship.</summary>
    public IReadOnlyDictionary<CharacterId, Mentorship> Mentors => _mentors;

    /// <summary>What each person holds against the voyage.</summary>
    public IReadOnlyDictionary<CharacterId, Grievance> Grievances => _grievances;

    /// <summary>When each person last ate anything fresh.</summary>
    public IReadOnlyDictionary<CharacterId, long> LastFresh => _lastFresh;

    /// <summary>Promises not yet kept or broken.</summary>
    public IReadOnlyList<Promise> Promises => _promises;

    /// <summary>Hours practised towards the next level of a skill.</summary>
    public double PracticeOf(CharacterId id, string skill) =>
        _practice.TryGetValue(id, out var skills) ? skills.GetValueOrDefault(skill) : 0;

    /// <summary>Someone's grievance (an empty one if they hold none).</summary>
    public Grievance GrievanceOf(CharacterId id) => _grievances.TryGetValue(id, out var grievance) ? grievance : new Grievance();

    internal Grievance Hold(CharacterId id)
    {
        if (!_grievances.TryGetValue(id, out var grievance))
        {
            _grievances[id] = grievance = new Grievance();
        }

        return grievance;
    }

    internal void AddPractice(CharacterId id, string skill, double hours)
    {
        if (!_practice.TryGetValue(id, out var skills))
        {
            _practice[id] = skills = new SortedDictionary<string, double>(StringComparer.Ordinal);
        }

        skills[skill] = Math.Max(0, skills.GetValueOrDefault(skill) + hours);
    }

    internal void ResetPractice(CharacterId id, string skill)
    {
        if (_practice.TryGetValue(id, out var skills))
        {
            skills.Remove(skill);
        }
    }

    internal void SetMentor(CharacterId learner, Mentorship? mentorship)
    {
        if (mentorship is null)
        {
            _mentors.Remove(learner);
        }
        else
        {
            _mentors[learner] = mentorship;
        }
    }

    internal void NoteFresh(CharacterId id, long tick) => _lastFresh[id] = tick;

    internal void Promise(Promise promise)
    {
        _promises.RemoveAll(p => p.Cause == promise.Cause);
        _promises.Add(promise);
    }

    internal void EndPromise(Promise promise) => _promises.Remove(promise);

    internal void Restore(double cleanliness, int sickNoted)
    {
        Cleanliness = Math.Clamp(cleanliness, 0, 100);
        SickNoted = Math.Max(0, sickNoted);
    }
}
