using Expedition.Simulation.Core;

namespace Expedition.Simulation.History;

/// <summary>
/// One recorded event in the campaign's history. It stores stable IDs and named values only, never
/// finished sentences: <see cref="ChronicleFormatter"/> turns an entry into player-facing text, so names,
/// wording and translations can change without touching saves (TECHNICAL_SPEC.md §23, §33).
/// </summary>
/// <param name="Sequence">Position in the chronicle, starting at 1. Strictly increasing.</param>
/// <param name="Tick">Simulation tick at which the event happened.</param>
/// <param name="Kind">Category of the event; decides which <paramref name="Parameters"/> are present.</param>
/// <param name="Characters">Characters involved, the main one first.</param>
/// <param name="Parameters">Named values such as <c>task</c>, <c>cause</c> or <c>to</c>. See <see cref="ChronicleParameters"/>.</param>
public sealed record ChronicleEntry(
    long Sequence,
    long Tick,
    ChronicleEntryKind Kind,
    IReadOnlyList<CharacterId> Characters,
    IReadOnlyDictionary<string, string> Parameters)
{
    /// <summary>The value of <paramref name="name"/>, or null if the entry does not have it.</summary>
    public string? Get(string name) => Parameters.GetValueOrDefault(name);
}

/// <summary>Parameter names used in chronicle entries. Values are IDs, enum names or numbers, never prose.</summary>
public static class ChronicleParameters
{
    /// <summary>Previous phase or survival state.</summary>
    public const string From = "from";

    /// <summary>New phase or survival state.</summary>
    public const string To = "to";

    /// <summary>
    /// What caused the event: a content ID (<c>condition.rope_burn</c>), a <see cref="ChronicleCauses"/> value,
    /// or free text given by a scenario or test, shown as-is.
    /// </summary>
    public const string Cause = "cause";

    /// <summary>Task definition ID.</summary>
    public const string Task = "task";

    /// <summary>Task definition ID of the urgent order that paused another task.</summary>
    public const string PausedBy = "pausedBy";

    /// <summary>Condition definition ID.</summary>
    public const string Condition = "condition";

    /// <summary>Health lost, as a number.</summary>
    public const string Damage = "damage";

    /// <summary>Health remaining afterwards, as a number.</summary>
    public const string Health = "health";

    /// <summary>Why a task stopped. See <see cref="InterruptReasons"/>.</summary>
    public const string Reason = "reason";

    /// <summary>Relationship dimension: <c>Trust</c>, <c>Respect</c> or <c>Affection</c>.</summary>
    public const string Dimension = "dimension";

    /// <summary>Signed change, as a number.</summary>
    public const string Delta = "delta";

    /// <summary>Which need ran short: <c>food</c> or <c>water</c>.</summary>
    public const string Need = "need";

    /// <summary>Text of an entry written before the chronicle was structured (save format 2 or older).</summary>
    public const string LegacyText = "legacyText";
}

/// <summary>Built-in causes that the simulation itself produces.</summary>
public static class ChronicleCauses
{
    /// <summary>Health lost to hunger at its maximum.</summary>
    public const string Starvation = "starvation";

    /// <summary>Health lost to thirst at its maximum.</summary>
    public const string Dehydration = "dehydration";
}

/// <summary>Values of <see cref="ChronicleParameters.Reason"/> for interrupted tasks.</summary>
public static class InterruptReasons
{
    /// <summary>Fatigue reached the exhaustion limit.</summary>
    public const string Exhausted = "exhausted";

    /// <summary>The player ordered the character to stop.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>The player ordered the character to walk somewhere else.</summary>
    public const string Moved = "moved";

    /// <summary>The character became unable to work (state in <c>to</c>, cause in <c>cause</c>).</summary>
    public const string Incapacitated = "incapacitated";
}
