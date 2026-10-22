using Expedition.Simulation.Characters;

namespace Expedition.Simulation.Tasks;

/// <summary>
/// Immutable description of a kind of work, loaded from <c>data/tasks</c>.
/// Runtime progress is held by <see cref="TaskInstance"/>.
/// </summary>
public sealed record TaskDefinition
{
    /// <summary>Unique definition ID, for example <c>task.pump_bilge</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Work-minutes needed at normal speed (skill level 2, not tired).</summary>
    public required int DurationMinutes { get; init; }

    /// <summary>True for rest. Rest can be started however tired the character is and is not interrupted by fatigue.</summary>
    public bool IsRest { get; init; }

    /// <summary>
    /// True if the task must be done at a work spot that lists it (the pump, the galley). The worker
    /// walks there first. False means the task is done wherever the character stands.
    /// </summary>
    public bool NeedsWorkSpot { get; init; }

    /// <summary>Skill that governs work speed, or null if none applies.</summary>
    public string? Skill { get; init; }

    /// <summary>
    /// Physical attribute that also governs work speed (for example <c>attribute.strength</c> for
    /// pumping), or null. Combines with <see cref="Skill"/>: a strong novice and a weak expert differ.
    /// </summary>
    public string? Attribute { get; init; }

    /// <summary>Minimum level of <see cref="Skill"/> needed to attempt the task.</summary>
    public int MinSkill { get; init; }

    /// <summary>Items that must be on hand but are not used up, keyed by item ID.</summary>
    public IReadOnlyList<string> RequiredTools { get; init; } = [];

    /// <summary>Items used up when the task starts, keyed by item ID. Not refunded if the task is interrupted.</summary>
    public IReadOnlyDictionary<string, int> Consumes { get; init; } = new Dictionary<string, int>();

    /// <summary>Need changes per hour while doing this task, added on top of the baseline.</summary>
    public NeedRates NeedsPerHour { get; init; } = NeedRates.None;

    /// <summary>Risk of injury while working, or null for safe tasks.</summary>
    public TaskHazard? Hazard { get; init; }

    /// <summary>Change to the ship per hour of work at normal speed, for example pumping the bilge.</summary>
    public Voyage.ShipEffects? ShipPerHour { get; init; }

    /// <summary>Change to the ship when the work is finished, for example a hull patch.</summary>
    public Voyage.ShipEffects? ShipOnComplete { get; init; }

    /// <summary>Need changes for everyone aboard when the work is finished, for example a hot meal easing stress.</summary>
    public NeedRates? CrewOnComplete { get; init; }

    /// <summary>True if doing this keeps the ship's watch (helm and lookout), which she needs to make full speed.</summary>
    public bool KeepsWatch { get; init; }

    /// <summary>
    /// Skills the work shows, besides <see cref="Skill"/>, without them setting its pace: a watch kept in
    /// a seaway shows who is a seaman. The captain learns them when he sees it done.
    /// </summary>
    public IReadOnlyList<string> Shows { get; init; } = [];

    /// <summary>
    /// The task taken up as soon as this one is finished, making a loop of work (PORT_EXPERIENCE §7): a
    /// docker shoulders a crate, then carries it to the lighter. Null for none.
    /// </summary>
    public string? Then { get; init; }

    /// <summary>
    /// What someone does instead while it rains in port (R2d, PORT_EXPERIENCE §14): gossip at the well,
    /// play in the square or mend nets on the quay become sheltering under the eaves. Null to carry on.
    /// </summary>
    public string? InRain { get; init; }

    /// <summary>What is carried while walking to and doing this task, for drawing: crate, basket, plank, yarn.</summary>
    public string? Carries { get; init; }

    /// <summary>
    /// Tending the sick and hurt: while someone is at it, everyone sick or hurt aboard is cared for, as well
    /// as the tender's skill allows.
    /// </summary>
    public bool TendsSick { get; init; }

    /// <summary>How much cleaner the ship is when the work is done (scrubbing, airing the bedding).</summary>
    public double Cleans { get; init; }

    /// <summary>When this task is worth taking up as a standing duty; null means always.</summary>
    public DutyCondition? DutyWhen { get; init; }

    /// <summary>The weather the work needs at sea (rain to catch, a sea quiet enough to fish), or null for any.</summary>
    public WeatherNeed? NeedsWeather { get; init; }

    /// <summary>What the work brings into the ship's stores as it goes (water caught, fish), or null.</summary>
    public TaskYield? Yield { get; init; }

    /// <summary>The preparation for heavy weather this work puts in place when done (Voyage.Preparations), or null.</summary>
    public string? Prepares { get; init; }

    /// <summary>"plug" or "patch": the work deals with a leak when done (the worst open one; for a patch, the worst not made good).</summary>
    public string? RepairsLeak { get; init; }
}

/// <summary>
/// The weather a task needs. It can only be started, and only goes on, at sea and while this holds: the
/// rain stops, and the hands stop catching it.
/// </summary>
public sealed record WeatherNeed
{
    /// <summary>Only while it is raining.</summary>
    public bool Rain { get; init; }

    /// <summary>Only with the sea state below this (0 calm .. 3 storm).</summary>
    public double? SeaStateBelow { get; init; }

    /// <summary>Only at anchor off an island: work for the boat and the shore.</summary>
    public bool AtAnchor { get; init; }
}

/// <summary>
/// What a task brings in: on average <see cref="PerHour"/> units an hour at normal pace, more for a
/// skilled hand. Chance decides each unit, minute by minute.
/// </summary>
public sealed record TaskYield
{
    /// <summary>The item brought in, for example <c>item.water</c>.</summary>
    public required string Item { get; init; }

    /// <summary>Units an hour at normal pace.</summary>
    public required double PerHour { get; init; }

    /// <summary>True if the yield falls off as the sea gets up: lines and nets do best in a calm.</summary>
    public bool BestInCalm { get; init; }

    /// <summary>Multiplies the yield over the bank off a coast, where the fish are.</summary>
    public double OnTheBank { get; init; } = 1;

    /// <summary>Ashore, what the yield depends on: "spring" (the island's water) or "forage" (what grows there); null for neither.</summary>
    public string? Ashore { get; init; }
}

/// <summary>
/// When a standing duty needs doing. All conditions given must hold. Ship conditions only apply at sea.
/// </summary>
public sealed record DutyCondition
{
    /// <summary>Only when the bilge is above this level.</summary>
    public double? BilgeAbove { get; init; }

    /// <summary>Only when the hull is below this condition.</summary>
    public double? HullBelow { get; init; }

    /// <summary>Only when the rigging is below this condition.</summary>
    public double? RiggingBelow { get; init; }

    /// <summary>Only starting within one of these hour windows, each [from, to), for example meal times.</summary>
    public IReadOnlyList<double[]>? StartBetween { get; init; }

    /// <summary>Only while a leak is open.</summary>
    public bool? OpenLeak { get; init; }

    /// <summary>Only while heavy weather is foreseen or blowing.</summary>
    public bool? StormComing { get; init; }

    /// <summary>Only while someone aboard is sick or hurt.</summary>
    public bool? SickAboard { get; init; }

    /// <summary>Only when the ship is kept less clean than this (0..100).</summary>
    public double? CleanlinessBelow { get; init; }
}

/// <summary>Chance of injury while doing a task.</summary>
public sealed record TaskHazard
{
    /// <summary>Probability of an injury in each hour of work (0..1).</summary>
    public required double ChancePerHour { get; init; }

    /// <summary>Health lost when the injury happens.</summary>
    public required int HealthDamage { get; init; }

    /// <summary>Condition ID added to the character, for example <c>condition.rope_burn</c>.</summary>
    public required string Condition { get; init; }
}
