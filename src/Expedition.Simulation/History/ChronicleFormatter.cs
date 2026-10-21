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
                $"{who} suffered {Condition(entry.Get(ChronicleParameters.Condition))} while doing {Task(entry.Get(ChronicleParameters.Task))} " +
                $"(-{entry.Get(ChronicleParameters.Damage)} health, now {entry.Get(ChronicleParameters.Health)}).",
            ChronicleEntryKind.TaskInterrupted =>
                $"{who} stopped {Task(entry.Get(ChronicleParameters.Task))}: {InterruptReason(entry, cause)}.",
            ChronicleEntryKind.TaskPaused =>
                $"{who} paused {Task(entry.Get(ChronicleParameters.Task))} to {Task(entry.Get(ChronicleParameters.PausedBy))}.",
            ChronicleEntryKind.TaskDropped =>
                $"{who} could not start {Task(entry.Get(ChronicleParameters.Task))}: {cause}",
            ChronicleEntryKind.StoresSpoiled => Spoiled(entry),
            ChronicleEntryKind.SickenedByRation =>
                $"{who} is down with {Condition(entry.Get(ChronicleParameters.Condition)).ToLowerInvariant()}, from the {ItemName(entry.Get(ChronicleParameters.To)).ToLowerInvariant()}.",
            ChronicleEntryKind.Recovered => $"{who} is over {Condition(entry.Get(ChronicleParameters.Condition)).ToLowerInvariant()}.",
            ChronicleEntryKind.SupplyShortage => entry.Get(ChronicleParameters.Need) == "water"
                ? $"{who} went thirsty: there is nothing left to drink."
                : $"{who} went hungry: there is no food left.",
            ChronicleEntryKind.RelationshipChanged => Relationship(entry, who, cause),
            ChronicleEntryKind.CaptainAppointed => $"{who} took command of the expedition ({cause}).",
            ChronicleEntryKind.WeatherChanged => WeatherChange(entry),
            ChronicleEntryKind.EventResolved => EventLine(entry),
            ChronicleEntryKind.AllHandsLost => $"No one is left alive aboard {Ship()}.",
            ChronicleEntryKind.Noticed => $"You noticed something about {who}: {Secret(entry.Get(ChronicleParameters.To))}",
            ChronicleEntryKind.LeftPort => $"{who} {entry.Get(ChronicleParameters.Reason)}.",
            ChronicleEntryKind.ConvoyWarning => $"The Chamber's notice: the convoy sails at dawn, in {entry.Get(ChronicleParameters.Level)} hours.",
            ChronicleEntryKind.LeadHeard => Lead(entry.Get(ChronicleParameters.To)) is { } lead
                ? $"Heard of someone worth finding: \"{lead.Text}\" ({lead.Where})"
                : "Heard of someone worth finding.",
            ChronicleEntryKind.DicePlayed => int.TryParse(entry.Get(ChronicleParameters.Level), out var won) && won >= 0
                ? $"Dice in the Three Anchors: you won {won} sueldos."
                : $"Dice in the Three Anchors: you lost {-int.Parse(entry.Get(ChronicleParameters.Level) ?? "0", System.Globalization.CultureInfo.InvariantCulture)} sueldos.",
            ChronicleEntryKind.SongSung => $"{who} sang \"{entry.Get(ChronicleParameters.To)}\" in the Three Anchors.",
            ChronicleEntryKind.RoundBought => $"You stood {entry.Get(ChronicleParameters.To)} a round, {entry.Get(ChronicleParameters.Level)} sueldos.",
            ChronicleEntryKind.LateFee => $"The convoy has gone without {Ship()}. Harbour fees and the Company's displeasure: {entry.Get(ChronicleParameters.Level)} sueldos.",
            ChronicleEntryKind.CrewHired => int.TryParse(entry.Get(ChronicleParameters.Level), out var paid) && paid < 0
                ? $"{who} signed on, paying {-paid} sueldos for the passage."
                : $"{who} signed on for an advance of {entry.Get(ChronicleParameters.Level)} sueldos.",
            ChronicleEntryKind.SailPlanChanged => entry.Get(ChronicleParameters.To) switch
            {
                "PressOn" => "The captain ordered every sail set: press on.",
                "HoveTo" => "The captain ordered the ship hove to, to ride it out.",
                _ => "The captain ordered sail set for the weather.",
            },
            ChronicleEntryKind.RationingChanged => entry.Get(ChronicleParameters.To) == "short"
                ? "The captain put the crew on short rations."
                : "The crew are back on full rations.",
            ChronicleEntryKind.HullStruck =>
                $"A heavy sea struck {Ship()} and opened a seam (hull down {entry.Get(ChronicleParameters.Damage)}, now {entry.Get(ChronicleParameters.Level)}%).",
            ChronicleEntryKind.RiggingCarriedAway => entry.Get(ChronicleParameters.To) == "yard"
                ? $"A squall sprang the main topsail yard on {Ship()}; the topsail cannot be set until it is fished (rigging now {entry.Get(ChronicleParameters.Level)}%)."
                : $"A line parted aloft on {Ship()} and flies loose (rigging now {entry.Get(ChronicleParameters.Level)}%).",
            ChronicleEntryKind.ClaimTested => ClaimLine(who, entry),
            ChronicleEntryKind.NoonSight => $"Noon sight by {who}: latitude {entry.Get(ChronicleParameters.Level)}.",
            ChronicleEntryKind.BilgeAlarm => $"The bilge is {entry.Get(ChronicleParameters.Level)}% full and rising. Man the pumps!",
            ChronicleEntryKind.Foundered => $"{Capitalise(entry.Get(ChronicleParameters.To) ?? "The ship")} filled and foundered.",
            ChronicleEntryKind.CoastNear => $"By the reckoning the coast is {entry.Get(ChronicleParameters.Level)} leagues off. Heave the lead, and keep a good lookout.",
            ChronicleEntryKind.LandSign => entry.Get(ChronicleParameters.To) switch
            {
                "birds" => "Land birds about the ship, flying west at dusk.",
                "weed" => "Weed in the water, and a drifting branch with its leaves still green.",
                "water" => "The sea has changed colour: greener, and cloudy.",
                _ => "A bank of cloud stands still in the west all day, as cloud does over land.",
            },
            ChronicleEntryKind.Sounding => entry.Get(ChronicleParameters.Level) == "none"
                ? $"{who} heaved the deep-sea lead: no bottom at {Coast()?.ShelfFathoms ?? 100:0} fathoms."
                : $"{who} heaved the lead: {entry.Get(ChronicleParameters.Level)} fathoms, {entry.Get(ChronicleParameters.To)}.",
            ChronicleEntryKind.LandSighted => $"Land! The coast raised from the masthead, some {entry.Get(ChronicleParameters.Level)} leagues off.",
            ChronicleEntryKind.LandmarkSighted => $"In view: {Coast()?.Find(entry.Get(ChronicleParameters.To))?.Looks ?? "a mark on the coast."}",
            ChronicleEntryKind.CoastIdentified => $"{who} took the coast for {Coast()?.Find(entry.Get(ChronicleParameters.To))?.Name ?? "a mark on the chart"}.",
            ChronicleEntryKind.NotTheDestination =>
                $"Standing in where {Coast()?.Find(entry.Get(ChronicleParameters.To))?.Name ?? "the destination"} should be: no ruin on the headland, no lagoon behind. It is not here.",
            ChronicleEntryKind.StruckTheCoast =>
                $"She struck in the dark, unseen surf and then rock, and was got off with the hull stove (down {entry.Get(ChronicleParameters.Damage)}, now {entry.Get(ChronicleParameters.Level)}%).",
            ChronicleEntryKind.Landfall => $"Landfall: {entry.Get(ChronicleParameters.To) ?? "the coast"} reached, and known for what it is.",
            _ => $"{entry.Kind}: {string.Join(", ", entry.Parameters.Select(p => $"{p.Key}={p.Value}"))}",
        };
    }

    private Voyage.CoastRules? Coast() => content.Voyage?.Route?.Coast;

    /// <summary>The name of a kind of weather, for example "a gale".</summary>
    public string WeatherName(string? id) =>
        id is not null && content.Voyage?.Weathers.FirstOrDefault(w => w.Id == id) is { } weather ? weather.Name : Prettify(id ?? "weather");

    private string WeatherChange(ChronicleEntry entry)
    {
        var to = entry.Get(ChronicleParameters.To);
        var wind = content.Voyage?.Weathers.FirstOrDefault(w => w.Id == to)?.WindLeaguesPerHour ?? 1;
        var from = content.Voyage?.Weathers.FirstOrDefault(w => w.Id == entry.Get(ChronicleParameters.From))?.WindLeaguesPerHour ?? 1;
        var verb = to is "weather.gale" or "weather.storm" ? "The weather worsened" : wind > from ? "The wind rose" : "The wind eased";
        return $"{verb}: {WeatherName(to)}.";
    }

    // "Man overboard! Heave to and lower the boat: Pedro was hauled back aboard, half drowned."
    private string EventLine(ChronicleEntry entry)
    {
        if (!content.Events.TryGetValue(entry.Get(ChronicleParameters.Event) ?? "", out var definition))
        {
            return $"An event ({entry.Get(ChronicleParameters.Event)}) was decided.";
        }

        var choice = definition.Choices.FirstOrDefault(c => c.Id == entry.Get(ChronicleParameters.Choice));
        var outcome = choice is null ? null : entry.Get(ChronicleParameters.Outcome) == "failure" && choice.Failure is not null ? choice.Failure : choice.Success;
        CharacterId? subject = entry.Characters.Count > 0 ? entry.Characters[0] : null;
        CharacterId? other = entry.Characters.Count > 1 ? entry.Characters[1] : null;
        string Fill(string text) => EventText.Fill(text, campaign, subject, other);
        return choice is null || outcome is null
            ? Fill(definition.Title)
            : $"{Fill(definition.Title)} {Fill(choice.Text)}: {Fill(outcome.Text)}";
    }

    private string Secret(string? id) =>
        id is not null && content.Port?.People.Select(p => p.FactText(id)).FirstOrDefault(t => t is not null) is { Length: > 0 } text
            ? char.ToLowerInvariant(text[0]) + text[1..]
            : "something odd.";

    private string ItemName(string? id) => id is not null && content.Items.TryGetValue(id, out var item) ? item.Name : id ?? "stores";

    // Found at sea: biscuit with weevils, wine gone sour, pork gone green, water gone foul.
    private string Spoiled(ChronicleEntry entry)
    {
        var id = entry.Get(ChronicleParameters.To);
        var name = ItemName(id).ToLowerInvariant();
        var lost = entry.Get(ChronicleParameters.Level);
        return id is not null && content.Items.TryGetValue(id, out var item) && item.ThirstRelief > 0
            ? $"The {name} has turned: {lost} poured over the side, and more will follow."
            : $"Weevils and rot in the {name}: {lost} thrown over the side, and more will follow.";
    }

    private Port.LeadDefinition? Lead(string? id) =>
        content.Port?.AllLeads().Select(l => l.Lead).FirstOrDefault(l => l.Id == id);

    private string ClaimLine(string who, ChronicleEntry entry)
    {
        var topic = entry.Get(ChronicleParameters.Need) ?? "";
        var skill = content.Skills.TryGetValue(topic, out var definition) ? definition.Name.ToLowerInvariant()
            : content.Languages.TryGetValue(topic, out var language) ? language.Name : topic;
        var told = int.Parse(entry.Get(ChronicleParameters.From) ?? "0", CultureInfo.InvariantCulture);
        var seen = int.Parse(entry.Get(ChronicleParameters.Level) ?? "0", CultureInfo.InvariantCulture);
        var (toldWord, seenWord) = (KnowledgeText.LevelWord(told).ToLowerInvariant(), KnowledgeText.LevelWord(seen).ToLowerInvariant());
        return seen < told
            ? $"At sea, {who}'s {skill} shows for what it is: {seenWord}, not the {toldWord} you were told. The crew have seen it too."
            : $"At sea, {who} turns out better at {skill} than you were told: {seenWord}, not {toldWord}. The crew have noticed.";
    }

    private string Ship() => campaign.Voyage?.ShipName ?? "the ship";

    private static string Capitalise(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Readable name for a <see cref="World.NeedChange"/> source, for "why" explanations.</summary>
    public string DescribeNeedSource(string source) => source switch
    {
        World.NeedLedger.Baseline => "daily life",
        ChronicleCauses.Starvation => "starvation",
        ChronicleCauses.Dehydration => "dehydration",
        _ when content.Tasks.TryGetValue(source, out var task) => task.Name.ToLowerInvariant(),
        _ when content.Items.TryGetValue(source, out var item) => item.Name,
        _ when content.Conditions.TryGetValue(source, out var condition) => condition.Name.ToLowerInvariant(),
        _ => Prettify(source),
    };

    private string Relationship(ChronicleEntry entry, string who, string cause)
    {
        var whom = entry.Characters.Count > 1 ? Name(entry.Characters[1]) : "someone";
        var delta = int.TryParse(entry.Get(ChronicleParameters.Delta), NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : 0;
        var change = delta >= 0 ? $"rose by {delta}" : $"fell by {-delta}";
        var dimension = Words(entry.Get(ChronicleParameters.Dimension));
        var preposition = dimension == "trust" ? "in" : "for";
        return $"{who}'s {dimension} {preposition} {whom} {change} ({cause}).";
    }

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
        _ when content.Conditions.ContainsKey(cause) => Condition(cause),
        _ when cause.Contains('.') && !cause.Contains(' ') => Prettify(cause),
        _ => cause,
    };

    private string Condition(string? conditionId) =>
        conditionId is not null && content.Conditions.TryGetValue(conditionId, out var condition)
            ? condition.Name.ToLowerInvariant()
            : Prettify(conditionId);

    /// <summary>Fallback for IDs without a definition: <c>condition.rope_burn</c> becomes <c>rope burn</c>.</summary>
    private static string Prettify(string? id) =>
        id is null ? "something" : id[(id.IndexOf('.') + 1)..].Replace('_', ' ');

    private static string Phase(string? phase) => Words(phase);

    /// <summary><c>PortPreparation</c> becomes <c>port preparation</c>.</summary>
    private static string Words(string? identifier) =>
        identifier is null ? "unknown" : CapitalSplit().Replace(identifier, " $1").Trim().ToLower(CultureInfo.InvariantCulture);

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex CapitalSplit();
}
