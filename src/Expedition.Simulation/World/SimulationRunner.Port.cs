using System.Globalization;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Knowledge;
using Expedition.Simulation.Port;
using Expedition.Simulation.Tasks;

namespace Expedition.Simulation.World;

/// <summary>
/// The living port (R1, D-022): people going about their day on schedules, what the captain sees and
/// hears, people who give up and leave, the chandler's dwindling stock, the convoy's deadline, and the
/// captain's dealings with the people of the waterfront.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>How close, in cells, the captain must be to speak with someone.</summary>
    public const int SpeakingDistance = 2;

    private Dictionary<string, PersonDefinition>? _people;

    private IReadOnlyDictionary<string, PersonDefinition> People =>
        _people ??= Content.Port?.People.ToDictionary(p => p.Id, StringComparer.Ordinal) ?? [];

    /// <summary>The person a character was created from, or null for the expedition's own people.</summary>
    public PersonDefinition? PersonOf(CharacterId id) =>
        Campaign.Characters.TryGetValue(id, out var character) && character.PersonId is { } person && People.TryGetValue(person, out var definition)
            ? definition
            : null;

    /// <summary>The character created from a person, if any.</summary>
    public Character? CharacterFor(string personId) =>
        Campaign.Characters.Values.FirstOrDefault(c => c.PersonId == personId);

    /// <summary>Opens the port: the purse and the chandler's stock. Locals must already be placed.</summary>
    public CommandResult BeginPort()
    {
        if (Content.Port is not { } rules || Campaign.Phase != CampaignPhase.PortPreparation || Campaign.Port is not null)
        {
            return CommandResult.Rejected("Port preparations happen once, in port, with port rules (data/port).");
        }

        var port = new PortState { Funds = rules.Funds };
        for (var lot = 0; lot < rules.Goods.Count; lot++)
        {
            port.SetStock(lot, rules.Goods[lot].Stock);
        }

        Campaign.SetPort(port);
        return CommandResult.Ok();
    }

    /// <summary>
    /// Whether the captain can see what a character is doing: the same place, not indoors, and within the
    /// place's sight range (everything is seen on a ship's deck). Without placements, everything is seen.
    /// </summary>
    public bool CaptainSees(CharacterId id)
    {
        if (Campaign.CaptainId is not { } captain || captain == id)
        {
            return true;
        }

        if (!Campaign.Placements.TryGetValue(captain, out var watcher) || !Campaign.Placements.TryGetValue(id, out var watched))
        {
            return true;
        }

        if (watcher.LocationId != watched.LocationId || IsIndoors(id))
        {
            return false;
        }

        var location = Content.Locations[watched.LocationId];
        return location.SightRange is not { } range || Distance(watcher.CellAt(Campaign.Clock.Tick), watched.CellAt(Campaign.Clock.Tick)) <= range;
    }

    /// <summary>True if the character is at an indoor spot (in a house, the chapel, an office): out of sight and reach.</summary>
    public bool IsIndoors(CharacterId id)
    {
        if (!Campaign.ActiveTasks.TryGetValue(id, out var task) || task.SpotId is null || !Campaign.Placements.TryGetValue(id, out var placement)
            || placement.IsMoving)
        {
            return false;
        }

        return Content.Locations[placement.LocationId].FindSpot(task.SpotId)?.Indoors == true;
    }

    private static int Distance(GridPosition a, GridPosition b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    // A local's day: the schedule block for this hour, at its spot. Demonstrations the captain asked for
    // come first. Checked when idle, and on the hour when the block may change.
    private void FollowSchedule(Character character)
    {
        if (!character.CanWork || character.PersonId is null || !People.TryGetValue(character.PersonId, out var person)
            || !Campaign.Placements.ContainsKey(character.Id))
        {
            return;
        }

        Campaign.ActiveTasks.TryGetValue(character.Id, out var current);
        if (current?.Origin == TaskOrigin.Order)
        {
            return;
        }

        var minute = Campaign.Clock.Tick % SimulationClock.TicksPerHour;
        if (current is not null && minute != 0)
        {
            return;
        }

        if (current is null && minute % 10 != 0 && Campaign.Clock.Tick > 0 && !_scheduleStarting)
        {
            return;
        }

        var hour = (int)(Campaign.Clock.Tick % SimulationClock.TicksPerDay / SimulationClock.TicksPerHour);
        var block = person.Schedule.FirstOrDefault(b => b.Contains(hour));
        if (block is null || (current is not null && IsStepOf(block.Task, current.DefinitionId)))
        {
            return;
        }

        if (current is not null)
        {
            Campaign.AbandonTask(character.Id);
        }

        var task = Content.Tasks[block.Task];
        var spot = block.Spot ?? (task.Id == "task.sleep_at_home" ? person.Home : null);
        if (FindWorkSpot(character, task, spot).Spot is not null)
        {
            Start(character, new TaskInstance(Campaign.Ids.NextTaskId(), character.Id, task.Id, TaskPriority.Normal) { Origin = TaskOrigin.Schedule }, spot);
        }
    }

    // True if a task is the start of a loop of work, or one of its later steps.
    private bool IsStepOf(string first, string task)
    {
        var step = first;
        for (var i = 0; i < 8 && step is not null; i++)
        {
            if (step == task)
            {
                return true;
            }

            step = Content.Tasks.TryGetValue(step, out var definition) ? definition.Then : null;
        }

        return false;
    }

    // True on the first tick of a port, so everyone takes up their place at once.
    private bool _scheduleStarting;

    /// <summary>Puts every local where their schedule says, at once (used when a port scene starts).</summary>
    public void SettleLocals()
    {
        _scheduleStarting = true;
        foreach (var local in Campaign.Characters.Values.Where(c => c.Membership == Membership.Local).OrderBy(c => c.Id))
        {
            FollowSchedule(local);
            if (Campaign.Placements.TryGetValue(local.Id, out var placement) && placement.Movement is { } walk)
            {
                // Arrive at once rather than everyone walking out of their front doors.
                var location = Content.Locations[placement.LocationId];
                SetCell(local.Id, location.Id, walk.Path[^1]);
            }
        }

        _scheduleStarting = false;
    }

    private void SetCell(CharacterId id, string locationId, GridPosition cell) =>
        Campaign.SetPlacement(id, new CharacterPlacement(locationId, cell));

    // Once a minute in port: secrets seen, people leaving, the chandler's trade, the convoy's deadline.
    private void StepPort()
    {
        if (Campaign.Port is not { } port || Content.Port is not { } rules)
        {
            return;
        }

        var tick = Campaign.Clock.Tick;
        if (tick % 5 == 0)
        {
            NoticeSecrets();
        }

        foreach (var local in Campaign.Characters.Values.Where(c => c.Membership == Membership.Local && c.PersonId is not null).OrderBy(c => c.Id))
        {
            if (People.TryGetValue(local.PersonId!, out var person) && person.Seeking?.Leaves is { } leaves
                && tick >= leaves.Tick && !port.Gone.Contains(local.Id) && !port.Hired.Contains(local.Id))
            {
                port.MarkGone(local.Id);
                Campaign.AbandonTask(local.Id);
                Campaign.RemovePlacement(local.Id);
                Campaign.Record(ChronicleEntryKind.LeftPort, [new(ChronicleParameters.Reason, leaves.Reason)], local.Id);
            }
        }

        if (tick > 0 && tick % SimulationClock.TicksPerDay == 0)
        {
            for (var lot = 0; lot < rules.Goods.Count; lot++)
            {
                port.SetStock(lot, port.Stock.GetValueOrDefault(lot) - rules.Goods[lot].SoldPerDay);
            }
        }

        var departure = rules.Departure.Tick;
        if (!port.Warned && tick >= departure - 12 * SimulationClock.TicksPerHour)
        {
            port.Warned = true;
            Campaign.Record(ChronicleEntryKind.ConvoyWarning, [new(ChronicleParameters.Level, Hours((departure - tick) / (double)SimulationClock.TicksPerHour))]);
        }

        var halfDaysLate = tick < departure ? 0 : (int)((tick - departure) / (12 * SimulationClock.TicksPerHour)) + 1;
        while (port.LateFeesCharged < halfDaysLate)
        {
            port.LateFeesCharged++;
            port.Funds -= rules.LateFeePerHalfDay;
            Campaign.Record(ChronicleEntryKind.LateFee, [new(ChronicleParameters.Level, rules.LateFeePerHalfDay.ToString(CultureInfo.InvariantCulture))]);
        }
    }

    private static string Hours(double hours) => Math.Round(hours).ToString(CultureInfo.InvariantCulture);

    private void NoticeSecrets()
    {
        foreach (var local in Campaign.Characters.Values.Where(c => c.Membership == Membership.Local && c.PersonId is not null).OrderBy(c => c.Id))
        {
            if (!People.TryGetValue(local.PersonId!, out var person) || person.Secrets.Count == 0
                || !Campaign.ActiveTasks.TryGetValue(local.Id, out var task) || !Arrived(local.Id) || !CaptainSees(local.Id))
            {
                continue;
            }

            foreach (var secret in person.Secrets.Where(s => s.During == task.DefinitionId))
            {
                if (secret.WithPerson is { } with && !(CharacterFor(with) is { } other && Campaign.ActiveTasks.ContainsKey(other.Id) && Arrived(other.Id)
                        && Distance(Campaign.Placements[other.Id].Cell, Campaign.Placements[local.Id].Cell) <= 3))
                {
                    continue;
                }

                if (Campaign.Learn(local.Id, secret.Id, 1, KnowledgeSource.Observation))
                {
                    Campaign.Record(ChronicleEntryKind.Noticed, [new(ChronicleParameters.To, secret.Id)], local.Id);
                }
            }
        }
    }

    private bool Arrived(CharacterId id) => Campaign.Placements.TryGetValue(id, out var placement) && !placement.IsMoving;

    /// <summary>Why the captain cannot speak with someone now, or null if they can.</summary>
    public string? WhyCannotSpeak(CharacterId id)
    {
        if (Campaign.CaptainId is not { } captain || !Campaign.Placements.TryGetValue(captain, out var here))
        {
            return "The captain is not here.";
        }

        if (!Campaign.Characters.TryGetValue(id, out var person) || !Campaign.Placements.TryGetValue(id, out var there) || there.LocationId != here.LocationId)
        {
            return "They are not here.";
        }

        if (IsIndoors(id))
        {
            return $"{person.Name} is indoors.";
        }

        return Distance(here.CellAt(Campaign.Clock.Tick), there.CellAt(Campaign.Clock.Tick)) > SpeakingDistance
            ? $"Walk over to {person.Name} first."
            : null;
    }

    /// <summary>
    /// Talks with someone nearby. Anyone looking for a berth says what they can do (their claims become
    /// knowledge); others pass the time of day. Returns what was said.
    /// </summary>
    public (CommandResult Result, string Said) TalkTo(CharacterId id)
    {
        if (WhyCannotSpeak(id) is { } why)
        {
            return (CommandResult.Rejected(why), "");
        }

        var character = Campaign.Characters[id];
        Campaign.Port?.MarkTalked(id);
        if (PersonOf(id) is not { } person)
        {
            return (CommandResult.Ok(), $"{character.Name} has work to do.");
        }

        // Whom they point you to, if they have not already (PORT_EXPERIENCE §9).
        // The full word and where to look go in the journal; here they are only named.
        var pointers = person.Leads.Where(l => Hear(l)).Select(l => CharacterFor(l.About)?.Name ?? l.About).ToList();
        var tips = pointers.Count == 0 ? "" : $"\n\n{person.Name} tells you of {Join(pointers)}, and where to find them. (Noted in your journal.)";

        if (person.Seeking is not { } seeking || Campaign.Port?.Hired.Contains(id) == true)
        {
            return (CommandResult.Ok(), (pointers.Count == 0 ? $"{person.Name}, {person.Occupation}, passes the time of day with you." : $"{person.Name}, {person.Occupation}, stops to talk.") + tips);
        }

        foreach (var (topic, level) in seeking.Claims.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            Campaign.Learn(id, topic, level, KnowledgeSource.Claim);
        }

        return (CommandResult.Ok(), seeking.Pitch + tips);
    }

    private static string Join(IReadOnlyList<string> names) =>
        names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];

    // Notes a lead in the journal, once. True if it was new.
    private bool Hear(LeadDefinition lead)
    {
        if (Campaign.Port is not { } port || !port.AddLead(lead.Id, Campaign.Clock.Tick))
        {
            return false;
        }

        var about = CharacterFor(lead.About);
        Campaign.Record(ChronicleEntryKind.LeadHeard, [new(ChronicleParameters.To, lead.Id)], about is null ? [] : [about.Id]);
        return true;
    }

    /// <summary>
    /// Reads the Chamber's notice board, which the captain must be standing by: seamen seeking berths,
    /// passages wanted, pardons offered. New notices go in the journal. Returns what the board says.
    /// </summary>
    public (CommandResult Result, string Said) ReadNotices()
    {
        if (Content.Port is not { NoticeBoard: { } boardId } rules || Campaign.Port is null || Campaign.CaptainId is not { } captain
            || !Campaign.Placements.TryGetValue(captain, out var here) || Content.Locations[here.LocationId].FindSpot(boardId) is not { } board)
        {
            return (CommandResult.Rejected("There is no notice board here."), "");
        }

        if (Distance(here.CellAt(Campaign.Clock.Tick), board.Cell) > SpeakingDistance)
        {
            return (CommandResult.Rejected("Walk over to the Chamber's notice board first."), "");
        }

        var fresh = rules.Notices.Where(Hear).ToList();
        var all = rules.Notices.Select(n => $"• {n.Text}");
        return (CommandResult.Ok(), (fresh.Count == 0 ? "Nothing new on the Chamber's board.\n" : "The Chamber's board:\n") + string.Join("\n", all));
    }

    /// <summary>The leads in the captain's journal about someone.</summary>
    public IEnumerable<LeadDefinition> LeadsAbout(CharacterId id)
    {
        if (Campaign.Port is not { } port || Content.Port is not { } rules || Campaign.Characters.GetValueOrDefault(id)?.PersonId is not { } person)
        {
            return [];
        }

        var heard = port.Leads.Select(l => l.LeadId).ToHashSet(StringComparer.Ordinal);
        return rules.AllLeads().Select(l => l.Lead).Where(l => l.About == person && heard.Contains(l.Id));
    }

    /// <summary>Who gave a lead, or null for the notice board.</summary>
    public CharacterId? LeadGiver(string leadId) =>
        Content.Port?.AllLeads().FirstOrDefault(l => l.Lead.Id == leadId).From is { } from ? CharacterFor(from)?.Id : null;

    /// <summary>
    /// Asks someone nearby what they know of another person. What they say depends on who they are: the
    /// people who work with someone know them; the landlady knows gossip. It becomes knowledge with them
    /// named as the informant.
    /// </summary>
    public (CommandResult Result, string Said) AskAbout(CharacterId informant, CharacterId about)
    {
        if (WhyCannotSpeak(informant) is { } why)
        {
            return (CommandResult.Rejected(why), "");
        }

        if (!Campaign.Characters.TryGetValue(about, out var subject))
        {
            return (CommandResult.Rejected("No such person."), "");
        }

        var speaker = Campaign.Characters[informant];
        Campaign.Port?.MarkAsked(informant, about);
        var reference = PersonOf(informant)?.Knows.FirstOrDefault(k => k.About == subject.PersonId);
        if (reference is null)
        {
            return (CommandResult.Ok(), $"{speaker.Name} shrugs: \"{subject.Name}? I couldn't say.\"");
        }

        foreach (var (topic, level) in reference.Levels.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            Campaign.Learn(about, topic, level, reference.Rumour ? KnowledgeSource.Rumour : KnowledgeSource.Reference, informant);
        }

        return (CommandResult.Ok(), $"{speaker.Name}: \"{reference.Text}\"");
    }

    /// <summary>
    /// Asks someone looking for a berth to show what they can do: they leave what they are doing and do a
    /// piece of work that uses the skill. If the captain watches it finished, it counts as a test. The
    /// proud take offence and refuse.
    /// </summary>
    public CommandResult RequestDemonstration(CharacterId id, string skill)
    {
        if (WhyCannotSpeak(id) is { } why)
        {
            return CommandResult.Rejected(why);
        }

        var character = Campaign.Characters[id];
        if (PersonOf(id)?.Seeking is null || Campaign.Port is not { } port || port.Hired.Contains(id))
        {
            return CommandResult.Rejected($"{character.Name} is not looking for a berth.");
        }

        if (character.Traits.Contains("trait.proud"))
        {
            if (Campaign.CaptainId is { } captain)
            {
                Campaign.ChangeRelationship(id, captain, RelationshipDimension.Trust, -10, "asked to prove himself like an apprentice");
            }

            return CommandResult.Rejected($"{character.Name} will not be tested like an apprentice, and is offended you asked.");
        }

        var task = Content.Tasks.Values.Where(t => t.Skill == skill && t.NeedsWorkSpot).OrderBy(t => t.Id, StringComparer.Ordinal)
            .FirstOrDefault(t => FindWorkSpot(character, t).Spot is not null);
        if (task is null)
        {
            return CommandResult.Rejected($"There is no way to show {SkillName(skill)} here just now.");
        }

        Campaign.AbandonTask(id);
        Start(character, new TaskInstance(Campaign.Ids.NextTaskId(), id, task.Id, TaskPriority.Normal));
        return CommandResult.Ok();
    }

    /// <summary>
    /// Offers someone a berth for an advance. They weigh it: the money (and their pride), the captain, and
    /// who else is aboard. A lowball offer insults them. A passenger's offer is negative: what they pay.
    /// </summary>
    public CommandResult Offer(CharacterId id, int advance)
    {
        if (WhyCannotSpeak(id) is { } why)
        {
            return CommandResult.Rejected(why);
        }

        var character = Campaign.Characters[id];
        if (PersonOf(id)?.Seeking is not { } seeking || Campaign.Port is not { } port || port.Hired.Contains(id) || port.Gone.Contains(id))
        {
            return CommandResult.Rejected($"{character.Name} is not looking for a berth.");
        }

        var rules = Content.Port!;
        if (port.Hired.Count >= rules.Berths)
        {
            return CommandResult.Rejected($"Every berth is taken ({rules.Berths}).");
        }

        if (advance > port.Funds)
        {
            return CommandResult.Rejected($"Only {port.Funds} sueldos are left in the purse.");
        }

        foreach (var aversion in seeking.WontSailWith)
        {
            if (CharacterFor(aversion.Person) is { } other && port.Hired.Contains(other.Id))
            {
                return CommandResult.Rejected($"{character.Name}: \"{aversion.Reason}\"");
            }
        }

        if (Campaign.CaptainId is { } captain && Campaign.Relationships.TryGetValue((id, captain), out var view) && view.Trust <= -25)
        {
            return CommandResult.Rejected($"{character.Name}: \"I'll not sail under you, Captain.\"");
        }

        if (advance < seeking.Lowest)
        {
            var insulting = seeking.Lowest > 0 && advance < seeking.Lowest * 0.75;
            if (insulting && Campaign.CaptainId is { } offended)
            {
                Campaign.ChangeRelationship(id, offended, RelationshipDimension.Respect, -10, "offered an insulting advance");
            }

            return CommandResult.Rejected(seeking.Asks < 0
                ? $"{character.Name}: \"I will pay my way, Captain, but not that much.\""
                : insulting
                    ? $"{character.Name}: \"Is that a joke? I asked {seeking.Asks}.\""
                    : $"{character.Name}: \"Not for that. I might come for {(seeking.Asks + seeking.Lowest) / 2}.\"");
        }

        port.Funds -= advance;
        port.MarkHired(id);
        character.Membership = Membership.Crew;
        Campaign.AbandonTask(id);
        var deck = Content.Locations[rules.ShipLocation];
        SetCell(id, deck.Id, FreeCell(deck));
        SetDuties(id, DutiesFor(character));
        Campaign.Record(ChronicleEntryKind.CrewHired, [new(ChronicleParameters.Level, advance.ToString(CultureInfo.InvariantCulture))], id);
        return CommandResult.Ok();
    }

    /// <summary>Buys a lot from the chandler, if there is stock and money.</summary>
    public CommandResult Buy(int lot)
    {
        if (Content.Port is not { } rules || Campaign.Port is not { } port)
        {
            return CommandResult.Rejected("The chandler is in port; the ship has sailed.");
        }

        if (lot < 0 || lot >= rules.Goods.Count)
        {
            return CommandResult.Rejected("The chandler does not sell that.");
        }

        var goods = rules.Goods[lot];
        if (port.Stock.GetValueOrDefault(lot) <= 0)
        {
            return CommandResult.Rejected($"The chandler has sold the last of the {ItemName(goods.Item).ToLowerInvariant()}.");
        }

        if (port.Funds < goods.Price)
        {
            return CommandResult.Rejected($"That costs {goods.Price} sueldos and only {port.Funds} are left.");
        }

        var stowed = Campaign.SharedStore.TryAdd(goods.Item, goods.Quantity, Content.Items);
        if (!stowed.Succeeded)
        {
            return stowed;
        }

        port.Funds -= goods.Price;
        port.SetStock(lot, port.Stock[lot] - 1);
        return CommandResult.Ok();
    }

    /// <summary>
    /// Sails: needs the minimum crew. The captain goes aboard, the port is left behind (its people stay,
    /// unsimulated), and the voyage begins.
    /// </summary>
    public CommandResult SetSail(string shipName, string destination, double leagues)
    {
        if (Content.Port is not { } rules || Campaign.Port is not { } port)
        {
            return CommandResult.Rejected("The ship is not in port.");
        }

        if (port.Hired.Count < rules.MinimumCrew)
        {
            return CommandResult.Rejected($"{Capital(shipName)} needs at least {rules.MinimumCrew} hands to sail; {port.Hired.Count} are signed on.");
        }

        var moved = Campaign.TryTransitionTo(CampaignPhase.Voyage, Campaign.Clock.Tick <= rules.Departure.Tick
            ? "sailed with the convoy from La Bocana"
            : "sailed from La Bocana, after the convoy");
        if (!moved.Succeeded)
        {
            return moved;
        }

        foreach (var local in Campaign.Characters.Values.Where(c => c.Membership == Membership.Local).ToList())
        {
            Campaign.AbandonTask(local.Id);
            Campaign.RemovePlacement(local.Id);
        }

        if (Campaign.CaptainId is { } captain)
        {
            Campaign.AbandonTask(captain);
            var deck = Content.Locations[rules.ShipLocation];
            SetCell(captain, deck.Id, FreeCell(deck));
        }

        Campaign.SetPort(null);
        return StartVoyage(shipName, destination, leagues);
    }

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    // The first free walkable cell, searching outward from the middle of a location.
    private GridPosition FreeCell(LocationDefinition location)
    {
        var taken = Campaign.Placements.Values.Where(p => p.LocationId == location.Id).Select(p => p.Cell).ToHashSet();
        var spots = location.Spots.Select(s => s.Cell).ToHashSet();
        var middle = new GridPosition(location.Width / 2, location.Height / 2);
        return Enumerable.Range(0, location.Height)
            .SelectMany(y => Enumerable.Range(0, location.Width).Select(x => new GridPosition(x, y)))
            .Where(cell => location.IsWalkable(cell) && !taken.Contains(cell) && !spots.Contains(cell))
            .OrderBy(cell => Math.Abs(cell.X - middle.X) + Math.Abs(cell.Y - middle.Y))
            .ThenBy(cell => cell.Y).ThenBy(cell => cell.X)
            .First();
    }

    /// <summary>
    /// Standing duties to suit someone's skills: their trade first, then the pumps and the watch that
    /// everyone shares. Tasks they cannot do are left out.
    /// </summary>
    public IReadOnlyList<string> DutiesFor(Character character)
    {
        var duties = new List<string>();
        if (character.Skills.Get("skill.ship_repair") >= 1) duties.Add("task.patch_hull");
        if (character.Skills.Get("skill.rigging") >= 1) duties.Add("task.repair_rigging");
        if (character.Skills.Get("skill.cooking") >= 2) duties.Add("task.cook");
        duties.AddRange(["task.pump_bilge", "task.stand_watch"]);
        return duties.Where(id => Content.Tasks.TryGetValue(id, out var task)
                && (task.Skill is null || character.Skills.Get(task.Skill) >= task.MinSkill))
            .ToList();
    }
}
