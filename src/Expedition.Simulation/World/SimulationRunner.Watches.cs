using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// The watches at sea (VOYAGE_EXPERIENCE §9, §17). Hands in the watch bill keep their watch: on deck they
/// work their standing duties, and keep the helm and lookout when nothing else wants doing, resting only
/// when worn right out; below, they sleep. At the change of watch the one going below drops its routine
/// work and the one coming up is turned out. All hands brings both watches on deck, at the cost of sleep.
/// The captain's orders always go first. Hands left out of the bill do as they always did.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>True if this hand should be on deck now, by the watch bill (always, for hands not in it).</summary>
    public bool OnDeck(CharacterId id) =>
        Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship || WatchBill.IsOnDeck(ship.WatchOf(id), Campaign.Clock.Tick, ship.AllHands || ship.TurnedOut);

    // The bilge at which the officer of the watch turns the watch below out to the pumps, and the level
    // it must be pumped down to before they go below again.
    private const double TurnOutBilge = 35;
    private const double StandDownBilge = 12;

    // Once a minute: the watch below turned out when the pumps are losing, and sent below again once she
    // is pumped out. (A storm alone does not: watch and watch keeps the crew fit to ride it out; calling all
    // hands for it is the captain's choice.) Recorded each way.
    private void OfficerOfTheWatch(VoyageState ship, WeatherDefinition weather)
    {
        if (ship.Watches.Count == 0)
        {
            return;
        }

        if (!ship.TurnedOut && ship.Bilge >= TurnOutBilge)
        {
            ship.TurnedOut = true;
            Campaign.Record(ChronicleEntryKind.AllHands, [new(ChronicleParameters.To, "turnedOut"), new(ChronicleParameters.Reason, "bilge")]);
        }
        else if (ship.TurnedOut && ship.Bilge <= StandDownBilge)
        {
            ship.TurnedOut = false;
            Campaign.Record(ChronicleEntryKind.AllHands, [new(ChronicleParameters.To, "stoodDown")]);
        }
    }

    // The change of watch for one hand: below, routine work is dropped; on deck, a sleeper not worn out is turned out.
    private void KeepWatch(Character character)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship || ship.WatchOf(character.Id) == Watch.None
            || !Campaign.ActiveTasks.TryGetValue(character.Id, out var task))
        {
            return;
        }

        var definition = Content.Tasks[task.DefinitionId];
        var resting = definition.IsRest;
        if (OnDeck(character.Id))
        {
            if (resting && task.Origin == TaskOrigin.SelfCare && character.Needs.Fatigue < RestThreshold(character, Content.Rules.RestAtFatigue ?? 70) - 10)
            {
                Campaign.AbandonTask(character.Id);
            }
            else if (!resting && task.Origin == TaskOrigin.Duty && !definition.KeepsWatch && !HelmManned()
                     && HelmsmanFor(ship) == character.Id)
            {
                // The helm first: the hand of the watch at routine work is sent to it.
                Campaign.AbandonTask(character.Id);
            }
        }
        else if (!resting && task.Origin == TaskOrigin.Duty && (definition.KeepsWatch || character.Needs.Fatigue >= BelowRestAt))
        {
            // Below: the helm is left to the watch on deck, and the tired turn in.
            Campaign.AbandonTask(character.Id);
        }
    }

    // Below, a hand turns in once at all tired; rested, they may still turn to work their duties need.
    private const double BelowRestAt = 25;

    // For a hand of the watch below with nothing to do: sleep, if at all tired (true: done); rested, false,
    // to take up a needed duty (never the helm, which is the watch on deck's).
    private bool GoBelow(Character character)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship || ship.WatchOf(character.Id) == Watch.None || OnDeck(character.Id))
        {
            return false;
        }

        if (character.Needs.Fatigue < BelowRestAt)
        {
            return false;
        }

        if (Campaign.QueueOf(character.Id).Count == 0
            && Content.Tasks.Values.Where(t => t.IsRest).OrderBy(t => t.Id, StringComparer.Ordinal).FirstOrDefault() is { } rest
            && Check(character, rest, materialsAlreadyUsed: false).Succeeded)
        {
            Start(character, new TaskInstance(Campaign.Ids.NextTaskId(), character.Id, rest.Id, TaskPriority.Normal) { Origin = TaskOrigin.SelfCare });
        }

        return true;
    }

    // True if someone is at the helm and lookout now.
    private bool HelmManned() => Campaign.ActiveTasks.Values.Any(t => Content.Tasks[t.DefinitionId].KeepsWatch && Campaign.Characters[t.CharacterId].CanWork);

    // Who of the watch on deck goes to an empty helm: the first, by the bill, who is at routine work and fit for it.
    private CharacterId? HelmsmanFor(VoyageState ship) => ship.Watches
        .Where(pair => pair.Value is Watch.Starboard or Watch.Larboard && OnDeck(pair.Key) && Campaign.Characters[pair.Key].CanWork && !Refusing(pair.Key)
                       && Campaign.Characters[pair.Key].Needs.Fatigue < Content.Rules.MaxFatigueToStartWork
                       && (!Campaign.ActiveTasks.TryGetValue(pair.Key, out var task) || task.Origin != TaskOrigin.Order))
        .Select(pair => (CharacterId?)pair.Key).OrderBy(id => id!.Value.Value).FirstOrDefault();

    // A hand of the watch on deck keeps the helm and lookout when no one is at it, before any other duty.
    private bool TakeTheHelm(Character character)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship || ship.WatchOf(character.Id) is Watch.None or Watch.Day
            || HelmManned() || HelmsmanFor(ship) != character.Id)
        {
            return false;
        }

        StandTheWatch(character);
        return Campaign.ActiveTasks.ContainsKey(character.Id);
    }

    // A hand of the watch on deck with no duty to do keeps the helm and lookout.
    private void StandTheWatch(Character character)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship || ship.WatchOf(character.Id) is Watch.None or Watch.Day
            || !OnDeck(character.Id) || Campaign.ActiveTasks.ContainsKey(character.Id) || Campaign.QueueOf(character.Id).Count > 0 || Refusing(character.Id)
            || Content.Tasks.Values.FirstOrDefault(t => t.KeepsWatch) is not { } watch
            || !Check(character, watch, materialsAlreadyUsed: false).Succeeded)
        {
            return;
        }

        Start(character, new TaskInstance(Campaign.Ids.NextTaskId(), character.Id, watch.Id, TaskPriority.Normal) { Origin = TaskOrigin.Duty });
    }

    // The fatigue an on-deck hand of the watch works up to before resting: longer than off watch, their
    // sleep being below, but not so long that the whole watch is worn out together.
    private double RestThreshold(Character character, double usual) =>
        Campaign.Voyage is { Outcome: VoyageOutcome.UnderWay } ship && ship.WatchOf(character.Id) != Watch.None
            ? Math.Min(usual + 10, Content.Rules.MaxFatigueToStartWork)
            : usual;

    /// <summary>
    /// The mate's watch bill: the crew split into two watches in turn, the cook a day-worker. Drawn up when
    /// she sails from port; the captain can change it.
    /// </summary>
    public void DrawUpWatchBill()
    {
        if (Campaign.Voyage is not { } ship)
        {
            return;
        }

        var next = Watch.Starboard;
        foreach (var hand in Campaign.Crew.Where(c => c.Id != Campaign.CaptainId && c.Membership == Membership.Crew).OrderBy(c => c.Id.Value))
        {
            if (Campaign.DutiesOf(hand.Id) is [var first, ..] && first == "task.cook")
            {
                ship.SetWatch(hand.Id, Watch.Day);
                continue;
            }

            ship.SetWatch(hand.Id, next);
            next = next == Watch.Starboard ? Watch.Larboard : Watch.Starboard;
        }
    }

    /// <summary>Puts a hand in a watch (or out of the bill, with <see cref="Watch.None"/>).</summary>
    public CommandResult SetWatch(CharacterId id, Watch watch)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship)
        {
            return CommandResult.Rejected("The watches are kept at sea.");
        }

        if (!Campaign.Crew.Any(c => c.Id == id) || id == Campaign.CaptainId)
        {
            return CommandResult.Rejected("Only the crew stand watches.");
        }

        ship.SetWatch(id, watch);
        return CommandResult.Ok();
    }

    /// <summary>
    /// Calls all hands on deck, or sends the watch below again. All hands spends the crew's sleep for the
    /// work in hand: a storm, a leak gaining, the anchor. Recorded in the chronicle.
    /// </summary>
    public CommandResult CallAllHands(bool call)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship)
        {
            return CommandResult.Rejected("All hands are called at sea.");
        }

        if (ship.AllHands != call)
        {
            ship.AllHands = call;
            Campaign.Record(ChronicleEntryKind.AllHands, [new(ChronicleParameters.To, call ? "called" : "dismissed")]);
        }

        return CommandResult.Ok();
    }

    // Tired hands make mistakes: the chance of an accident climbs as fatigue passes the tired mark.
    private double MistakeFactor(Character character)
    {
        var rules = Content.Rules;
        var over = Math.Clamp((character.Needs.Fatigue - rules.TiredFatigue * 0.7) / (rules.ExhaustionFatigue - rules.TiredFatigue * 0.7), 0, 1);
        return 1 + 3 * over * over;
    }
}
