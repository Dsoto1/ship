using Expedition.Simulation.Shore;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>
/// A stand-in for the player, for runs that must go on past the first decision: it answers what waits for
/// the captain the way a careful, decent captain would, so a campaign can be played out to the relief ship
/// and its balance judged. Not part of the game.
/// </summary>
public sealed class TestCaptain(SimulationRunner runner)
{
    private bool _ordered;
    private bool _woodSent;
    private long _meetingTick = -1;
    private int _trades;
    private long _nextVisit;

    /// <summary>Decisions made, for the report: meetings and parties begin "day N:".</summary>
    public List<string> Log { get; } = [];

    /// <summary>Plays <paramref name="hours"/> hours, deciding each hour what waits.</summary>
    public void Play(int hours)
    {
        for (var hour = 0; hour < hours; hour++)
        {
            runner.Advance(60);
            Decide();
        }
    }

    private bool WoodLeave => runner.Campaign.World?.Communities.GetValueOrDefault("people.oruwai")?.Leave.Contains("wood") == true;

    private void Decide()
    {
        // An event at sea: the first choice that can be made.
        if (runner.Campaign.PendingEvent is not null && runner.OfferedChoices().FirstOrDefault(c => c.Available) is { } choice)
        {
            runner.ResolveEvent(choice.Choice.Id);
        }

        // Meetings, as many rounds as they run.
        for (var round = 0; round < 20 && runner.Campaign.World?.Meeting is { } meeting; round++)
        {
            if (meeting.Tick != _meetingTick)
            {
                (_meetingTick, _trades) = (meeting.Tick, 0);
            }

            var id = Choose(runner.MeetingChoices(), _trades < 3, WoodLeave);
            _trades += id.StartsWith("trade:", StringComparison.Ordinal) ? 1 : 0;
            Log.Add($"day {runner.CampDay:0}: {runner.DescribeMeeting()?.Title}: {id}");
            runner.ResolveMeeting(id);
        }

        Build();
        SendParties();
    }

    // What a careful captain says: answer gifts with trade goods; ask what they know; ask leave; explain the
    // palisade; a few fair trades, never of the camp's tools; never seize anyone; carry a letter home.
    private static string Choose(IReadOnlyList<MeetingChoice> choices, bool trade, bool woodLeave)
    {
        // Gaspar Lezo: taken in once Oruwai has given leave for the wood, as taking him in sets Oruwai back.
        if (choices.Any(c => c.Id == "join"))
        {
            return woodLeave ? "join" : "stay";
        }

        string? First(string prefix) => choices.FirstOrDefault(c => c.Available && c.Id.StartsWith(prefix, StringComparison.Ordinal))?.Id;
        bool Trade(MeetingChoice c) => c.Available && c.Id.StartsWith("trade:", StringComparison.Ordinal) && c.Note is null;
        return First("answer:") ?? First("ask_back")
            ?? choices.FirstOrDefault(c => c.Available && c.Id.StartsWith("ask:", StringComparison.Ordinal) && c.Note is null)?.Id
            ?? First("leave:") ?? First("explain")
            ?? (trade ? choices.FirstOrDefault(c => Trade(c) && c.Id.Contains(":item.embrawood:", StringComparison.Ordinal))?.Id ?? choices.FirstOrDefault(Trade)?.Id : null)
            ?? First("take_up") ?? First("letter")
            ?? First("farewell") ?? choices.First(c => c.Available).Id;
    }

    // On the third day ashore: huts for all, the storehouse, the infirmary, the workshop, the garden, a
    // landing, the palisade.
    private void Build()
    {
        if (_ordered || runner.Campaign.Camp is null || runner.CampDay < 3)
        {
            return;
        }

        _ordered = true;
        var people = runner.Campaign.Crew.Count();
        foreach (var building in Enumerable.Repeat("building.hut", (people + 3) / 4)
                     .Concat(["building.storehouse", "building.infirmary", "building.workshop", "building.garden", "building.landing"]))
        {
            var placed = runner.OrderBuilding(building);
            Log.Add($"ordered {building}: {(placed.Succeeded ? "ok" : placed.Reason)}");
        }

        runner.OrderPalisade(true);
    }

    // Once a day: a party for the red wood when leave is given and logs are short; up to Mahenu to find the
    // garrison's survivor once his story is known (again, if he stayed, once the wood is secured).
    private void SendParties()
    {
        if (runner.Campaign.World is not { } world || runner.Campaign.Camp is null || runner.Campaign.Clock.Tick % SimulationClock.TicksPerDay >= 60)
        {
            return;
        }

        if (_woodSent && world.Journeys.Count == 0 && runner.Campaign.SharedStore.Count("item.embrawood") < 40)
        {
            _woodSent = false;
        }

        if (!_woodSent && WoodLeave && runner.PlaceKnown("place.dyewood") >= PlaceKnowledge.Reported)
        {
            var party = runner.Campaign.Crew.Where(c => c.Id != runner.Campaign.CaptainId && c.CanWork && !runner.Away(c.Id)).Take(3).Select(c => c.Id).ToList();
            var sent = runner.SendParty(party, "place.dyewood", "wood");
            Log.Add($"day {runner.CampDay:0}: wood party: {(sent.Succeeded ? "sent" : sent.Reason)}");
            _woodSent = sent.Succeeded;
        }

        if (world is { Survivor: 0 or 4, Journeys.Count: 0 } && runner.Learned("know.garrison_mahenu") && (world.Survivor == 0 || WoodLeave)
            && runner.Campaign.Clock.Tick >= _nextVisit)
        {
            var party = runner.Campaign.Crew.Where(c => c.Id != runner.Campaign.CaptainId && c.CanWork)
                .OrderByDescending(c => c.Languages.Get("language.ahuneri")).Take(2).Select(c => c.Id).ToList();
            var sent = runner.SendParty(party, "place.mahenu", "visit");
            Log.Add($"day {runner.CampDay:0}: visit Mahenu: {(sent.Succeeded ? "sent" : sent.Reason)}");
            _nextVisit = runner.Campaign.Clock.Tick + 10L * SimulationClock.TicksPerDay;
        }
    }
}
