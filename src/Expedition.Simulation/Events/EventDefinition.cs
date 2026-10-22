using Expedition.Simulation.Characters;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.Events;

/// <summary>
/// Something that can happen, loaded from <c>data/events</c>: when it may fire, what the player reads,
/// and the choices they have (GAME_VISION.md §8, D-017). Text may name people with {subject}, {other},
/// {captain}, {ship} and {destination}.
/// </summary>
public sealed record EventDefinition
{
    /// <summary>ID, for example <c>event.man_overboard</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Heading, for example "Man overboard!".</summary>
    public required string Title { get; init; }

    /// <summary>What happens, as the captain sees it.</summary>
    public required string Text { get; init; }

    /// <summary>When it can happen.</summary>
    public required EventTrigger Trigger { get; init; }

    /// <summary>What the captain can do. At least one.</summary>
    public required IReadOnlyList<EventChoice> Choices { get; init; }
}

/// <summary>When an event may fire. All conditions given must hold; then it fires with the chance per day.</summary>
public sealed record EventTrigger
{
    /// <summary>Chance of firing in a day when its conditions hold (checked hourly).</summary>
    public required double ChancePerDay { get; init; }

    /// <summary>Not before this day of the voyage (1 is the first).</summary>
    public int MinDay { get; init; } = 1;

    /// <summary>Only in one of these weathers.</summary>
    public IReadOnlyList<string>? Weather { get; init; }

    /// <summary>Only when the water would last fewer days than this.</summary>
    public double? WaterDaysBelow { get; init; }

    /// <summary>Only when the bilge is above this level.</summary>
    public double? BilgeAbove { get; init; }

    /// <summary>Only with fewer leagues than this left to run: the coast is near.</summary>
    public double? LeaguesBelow { get; init; }

    /// <summary>Only if the captain already knows these things (fact or skill IDs) about the subject.</summary>
    public IReadOnlyList<string> Known { get; init; } = [];

    /// <summary>Only if the captain does not yet know these things about the subject.</summary>
    public IReadOnlyList<string> NotKnown { get; init; } = [];

    /// <summary>Only while the crew are on short rations, or only while they are not.</summary>
    public bool? Rationing { get; init; }

    /// <summary>At most once per voyage.</summary>
    public bool Once { get; init; }

    /// <summary>Days before it can happen again.</summary>
    public double CooldownDays { get; init; } = 4;

    /// <summary>Who it happens to, or null if it involves the whole ship.</summary>
    public SubjectRule? Subject { get; init; }
}

/// <summary>How to choose who an event is about.</summary>
public sealed record SubjectRule
{
    /// <summary>
    /// <c>crew</c> (anyone able except the captain), <c>anyone</c> (including the captain), <c>stressed</c>
    /// (stress at least <see cref="MinStress"/>), or <c>feud</c> (two people who think badly of each other:
    /// subject and other).
    /// </summary>
    public string Pick { get; init; } = "crew";

    /// <summary>For <c>stressed</c>: the least stress that counts.</summary>
    public double MinStress { get; init; } = 60;

    /// <summary>Only people from here, for example "Lusara".</summary>
    public string? Origin { get; init; }

    /// <summary>Only people with this trait.</summary>
    public string? Trait { get; init; }

    /// <summary>Only people with this condition, for example <c>condition.fever</c>: the event follows from it.</summary>
    public string? Condition { get; init; }

    /// <summary>
    /// Only this person of the port (for example <c>person.velho</c>), if they sailed: events written for
    /// someone, about who they are and what they hid.
    /// </summary>
    public string? Person { get; init; }
}

/// <summary>One thing the captain can do.</summary>
public sealed record EventChoice
{
    /// <summary>ID, unique within the event.</summary>
    public required string Id { get; init; }

    /// <summary>What the button says.</summary>
    public required string Text { get; init; }

    /// <summary>An item the choice needs in the hold, or null.</summary>
    public string? RequiresItem { get; init; }

    /// <summary>How many of <see cref="RequiresItem"/> are needed.</summary>
    public int RequiresAmount { get; init; } = 1;

    /// <summary>
    /// Offered only if the captain knows these things about the subject: he cannot ask about the Santa
    /// Clara if no one told him of it.
    /// </summary>
    public IReadOnlyList<string> Known { get; init; } = [];

    /// <summary>A skill roll deciding between success and failure, or null for a sure outcome.</summary>
    public SkillCheck? Check { get; init; }

    /// <summary>What happens (on success, if there is a check).</summary>
    public required EventOutcome Success { get; init; }

    /// <summary>What happens when the check fails.</summary>
    public EventOutcome? Failure { get; init; }
}

/// <summary>A chance of success set by someone's skill: base + perLevel × level, kept within 5–95%.</summary>
public sealed record SkillCheck
{
    /// <summary>Skill ID, or null for plain luck (the base chance).</summary>
    public string? Skill { get; init; }

    /// <summary><c>best</c> (the most skilled person able), <c>subject</c> or <c>captain</c>.</summary>
    public string Who { get; init; } = "best";

    /// <summary>Chance at skill 0.</summary>
    public required double Base { get; init; }

    /// <summary>Chance added per skill level.</summary>
    public double PerLevel { get; init; }
}

/// <summary>The result of a choice: text for the player and its effects.</summary>
public sealed record EventOutcome
{
    /// <summary>What happened.</summary>
    public required string Text { get; init; }

    /// <summary>Changes, applied in order.</summary>
    public IReadOnlyList<EventEffect> Effects { get; init; } = [];
}

/// <summary>
/// One change an outcome makes. Set the fields for the kind of change; several may be combined.
/// <see cref="Who"/> is <c>subject</c>, <c>other</c>, <c>captain</c>, <c>crew</c> (everyone but the captain)
/// or <c>all</c>.
/// </summary>
public sealed record EventEffect
{
    /// <summary>Item to add to (positive) or take from (negative) the shared store.</summary>
    public string? Item { get; init; }

    /// <summary>How many of <see cref="Item"/>.</summary>
    public int Amount { get; init; }

    /// <summary>Change to the ship.</summary>
    public ShipEffects? Ship { get; init; }

    /// <summary>Leagues gained (positive) or lost (negative).</summary>
    public double Leagues { get; init; }

    /// <summary>Who the need, health, condition and survival changes apply to.</summary>
    public string? Who { get; init; }

    /// <summary>Need changes, applied once.</summary>
    public NeedRates? Needs { get; init; }

    /// <summary>Health gained (positive) or lost (negative).</summary>
    public double Health { get; init; }

    /// <summary>Condition to add.</summary>
    public string? Condition { get; init; }

    /// <summary>A change in how people regard someone.</summary>
    public RelationshipChange? Relationship { get; init; }

    /// <summary>A new survival state, for example Missing for a man lost overboard.</summary>
    public SurvivalState? Becomes { get; init; }

    /// <summary>Put the crew on short rations (true) or back on full (false).</summary>
    public bool? Rationing { get; init; }

    /// <summary>A change to what <see cref="Who"/> hold against the voyage.</summary>
    public GrievanceEffect? Grievance { get; init; }

    /// <summary>A promise the captain makes, broken if its cause is not put right in time.</summary>
    public PromiseEffect? Promise { get; init; }

    /// <summary>A sail order given.</summary>
    public Voyage.SailPlan? SailPlan { get; init; }

    /// <summary>A task ordered for <see cref="Who"/>, urgent: "task.tend_sick".</summary>
    public string? Order { get; init; }

    /// <summary>
    /// What the captain learns of <see cref="EventEffect.Who"/>: the truth about a skill, a language or
    /// a fact of their past, as seen (or, with a source of Claim, only as they now tell it).
    /// </summary>
    public LearnEffect? Learn { get; init; }
}

/// <summary>Something the captain learns from an event.</summary>
public sealed record LearnEffect
{
    /// <summary>A skill, language, trait or fact ID.</summary>
    public required string Topic { get; init; }

    /// <summary>
    /// The level learned, or null for the truth: the person's real skill or language level, or 1 for a
    /// fact (0 if it is not theirs).
    /// </summary>
    public int? Level { get; init; }

    /// <summary>How it is learned: Observation (seen), Test, or Claim (their own word now).</summary>
    public Knowledge.KnowledgeSource Source { get; init; } = Knowledge.KnowledgeSource.Observation;
}

/// <summary>How one person or group regards another changes.</summary>
public sealed record RelationshipChange
{
    /// <summary>Who changes their view: a <see cref="EventEffect.Who"/> value.</summary>
    public required string From { get; init; }

    /// <summary>Who they regard differently: <c>subject</c>, <c>other</c> or <c>captain</c>.</summary>
    public required string To { get; init; }

    /// <summary>Trust, Respect or Affection.</summary>
    public required RelationshipDimension Dimension { get; init; }

    /// <summary>Change, -100..100.</summary>
    public required int Amount { get; init; }
}

/// <summary>A grievance eased or worsened by an outcome.</summary>
public sealed record GrievanceEffect
{
    /// <summary>The cause (<see cref="Characters.GrievanceCauses"/>), or "all" for every cause.</summary>
    public required string Cause { get; init; }

    /// <summary>Added to it: negative eases it.</summary>
    public required double Amount { get; init; }
}

/// <summary>A promise: the cause put right within so many days, or the crew remember it.</summary>
public sealed record PromiseEffect
{
    /// <summary>What it answers (<see cref="Characters.GrievanceCauses"/>).</summary>
    public required string Cause { get; init; }

    /// <summary>Days to keep it in.</summary>
    public double Days { get; init; } = 3;
}
