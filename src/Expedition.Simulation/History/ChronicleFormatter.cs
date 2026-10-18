using System.Globalization;
using System.Text.RegularExpressions;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;

namespace Expedition.Simulation.History;

/// <summary>
/// Turns chronicle entries into English text, resolving IDs to current names. This is the single
/// place chronicle wording lives, so a later localisation layer replaces this class and nothing else.
/// </summary>
public sealed partial class ChronicleFormatter(CampaignState campaign, GameContent content)
{
    /// <summary>Readable text for <paramref name="entry"/>.</summary>
    public string Format(ChronicleEntry entry)
    {
        if (entry.Get(ChronicleParameters.LegacyText) is { } legacy)
        {
            return legacy;
        }

        var who = entry.Characters.Count > 0 ? Name(entry.Characters[0]) : "Someone";
        var cause = Cause(entry.Get(ChronicleParameters.Cause));
        return entry.Kind switch
        {
            ChronicleEntryKind.PhaseChanged =>
                $"The expedition moved from {Phase(entry.Get(ChronicleParameters.From))} to {Phase(entry.Get(ChronicleParameters.To))}: {cause}.",
            ChronicleEntryKind.TransitionRejected =>
                $"Could not move from {Phase(entry.Get(ChronicleParameters.From))} to {Phase(entry.Get(ChronicleParameters.To))} ({cause}).",
            ChronicleEntryKind.SurvivalStateChanged => entry.Get(ChronicleParameters.To) switch
            {
                nameof(SurvivalState.Dead) => $"{who} died ({cause}).",
                nameof(SurvivalState.Alive) => $"{who} is able to act again ({cause}).",
                var state => $"{who} is now {Words(state)} ({cause}).",
            },
            ChronicleEntryKind.Injury =>
                $"{who} suffered {Prettify(entry.Get(ChronicleParameters.Condition))} while doing {Task(entry.Get(ChronicleParameters.Task))} " +
                $"(-{entry.Get(ChronicleParameters.Damage)} health, now {entry.Get(ChronicleParameters.Health)}).",
            ChronicleEntryKind.TaskInterrupted =>
                $"{who} stopped {Task(entry.Get(ChronicleParameters.Task))}: {InterruptReason(entry, cause)}.",
            ChronicleEntryKind.SupplyShortage => entry.Get(ChronicleParameters.Need) == "water"
                ? $"{who} went thirsty: there is nothing left to drink."
                : $"{who} went hungry: there is no food left.",
            _ => $"{entry.Kind}: {string.Join(", ", entry.Parameters.Select(p => $"{p.Key}={p.Value}"))}",
        };
    }

    /// <summary>Readable name for a <see cref="World.NeedChange"/> source, for "why" explanations.</summary>
    public string DescribeNeedSource(string source) => source switch
    {
        World.NeedLedger.Baseline => "daily life",
        ChronicleCauses.Starvation => "starvation",
        ChronicleCauses.Dehydration => "dehydration",
        _ when content.Tasks.TryGetValue(source, out var task) => task.Name.ToLowerInvariant(),
        _ when content.Items.TryGetValue(source, out var item) => item.Name,
        _ => Prettify(source),
    };

    private string InterruptReason(ChronicleEntry entry, string cause) => entry.Get(ChronicleParameters.Reason) switch
    {
        InterruptReasons.Exhausted => "exhausted",
        InterruptReasons.Cancelled => "ordered to stop",
        InterruptReasons.Moved => "ordered elsewhere",
        InterruptReasons.Incapacitated => $"{Words(entry.Get(ChronicleParameters.To))} ({cause})",
        var other => Words(other),
    };

    private string Name(CharacterId id) => campaign.Characters.TryGetValue(id, out var character) ? character.Name : id.ToString();

    private string Task(string? taskId) =>
        taskId is not null && content.Tasks.TryGetValue(taskId, out var task) ? task.Name.ToLowerInvariant() : Prettify(taskId);

    private string Cause(string? cause) => cause switch
    {
        null => "no reason recorded",
        _ when cause.StartsWith("task.", StringComparison.Ordinal) => Task(cause),
        _ when cause.Contains('.') && !cause.Contains(' ') => Prettify(cause),
        _ => cause,
    };

    /// <summary><c>condition.rope_burn</c> becomes <c>rope burn</c>, until conditions get display names (Ticket 3d).</summary>
    private static string Prettify(string? id) =>
        id is null ? "something" : id[(id.IndexOf('.') + 1)..].Replace('_', ' ');

    private static string Phase(string? phase) => Words(phase);

    /// <summary><c>PortPreparation</c> becomes <c>port preparation</c>.</summary>
    private static string Words(string? identifier) =>
        identifier is null ? "unknown" : CapitalSplit().Replace(identifier, " $1").Trim().ToLower(CultureInfo.InvariantCulture);

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex CapitalSplit();
}
