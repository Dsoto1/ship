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
