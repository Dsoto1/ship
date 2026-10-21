using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Voyage;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>The watches: the round, the watch below asleep, the deck kept, all hands, tired hands' mistakes.</summary>
public sealed class WatchTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static (SimulationRunner Runner, VoyageState Ship) WithBill(ulong seed = CampaignState.DefaultSeed)
    {
        var runner = TestDeckScenario.Create(Shipped, seed, withDuties: true);
        runner.DrawUpWatchBill();
        var ship = runner.Campaign.Voyage!;
        ship.WeatherId = "weather.fair";
        ship.WeatherUntilTick = long.MaxValue;
        return (runner, ship);
    }

    // On to the next evening's first watch, when one watch is below.
    private static void ToTheFirstWatch(SimulationRunner runner)
    {
        var now = runner.Campaign.Clock.Tick % SimulationClock.TicksPerDay;
        runner.Advance((int)((21 * SimulationClock.TicksPerHour - now + SimulationClock.TicksPerDay) % SimulationClock.TicksPerDay));
    }

    [Fact]
    public void TheRoundShiftsEachDaySoNoWatchKeepsTheSameHours()
    {
        const long day = SimulationClock.TicksPerDay;
        var middle = SimulationClock.TicksPerHour * 1;
        Assert.NotEqual(WatchBill.OnDeck(middle), WatchBill.OnDeck(day + middle));
        // Within a day the two watches take turns, watch by watch.
        var turns = WatchBill.Changes.Select(h => WatchBill.OnDeck(h * SimulationClock.TicksPerHour)).ToList();
        for (var i = 1; i < turns.Count; i++)
        {
            Assert.NotEqual(turns[i - 1], turns[i]);
        }

        Assert.Equal("the forenoon watch", WatchBill.NameAt(9 * SimulationClock.TicksPerHour));
        Assert.True(WatchBill.IsOnDeck(Watch.Day, 10 * SimulationClock.TicksPerHour, allHands: false));
        // By day both watches turn to for the day's work.
        Assert.True(WatchBill.IsOnDeck(Watch.Starboard, 10 * SimulationClock.TicksPerHour, allHands: false));
        Assert.True(WatchBill.IsOnDeck(Watch.Larboard, 10 * SimulationClock.TicksPerHour, allHands: false));
        Assert.False(WatchBill.IsOnDeck(Watch.Day, 22 * SimulationClock.TicksPerHour, allHands: false));
    }

    [Fact]
    public void TheMateSplitsTheCrewInTwoWatchesWithTheCookADayWorker()
    {
        var (runner, ship) = WithBill();
        var hands = runner.Campaign.Crew.Where(c => c.Id != runner.Campaign.CaptainId).ToList();
        Assert.All(hands, h => Assert.NotEqual(Watch.None, ship.WatchOf(h.Id)));
        var starboard = hands.Count(h => ship.WatchOf(h.Id) == Watch.Starboard);
        var larboard = hands.Count(h => ship.WatchOf(h.Id) == Watch.Larboard);
        Assert.InRange(Math.Abs(starboard - larboard), 0, 1);
        Assert.Contains(hands, h => ship.WatchOf(h.Id) == Watch.Day && runner.Campaign.DutiesOf(h.Id) is ["task.cook", ..]);
    }

    [Fact]
    public void TheWatchBelowSleepsAndTheDeckIsAlwaysKept()
    {
        var (runner, ship) = WithBill();
        var kept = 0;
        var minutes = 0;
        var tiredAwakeBelow = 0;
        var helmBelow = 0;
        for (var minute = 0; minute < 3 * 24 * 60; minute += 5)
        {
            runner.Advance(5);
            minutes++;
            kept += ship.WatchKept ? 1 : 0;
            foreach (var (id, _) in ship.Watches.Where(pair => !runner.OnDeck(pair.Key)))
            {
                var task = runner.Campaign.ActiveTasks.GetValueOrDefault(id);
                var definition = task is null ? null : Shipped.Tasks[task.DefinitionId];
                helmBelow += definition?.KeepsWatch == true ? 1 : 0;
                tiredAwakeBelow += runner.Campaign.Characters[id].Needs.Fatigue >= 30 && definition is { IsRest: false } && task!.Origin != Tasks.TaskOrigin.Order ? 1 : 0;
            }
        }

        Assert.True(kept > minutes * 0.95, $"kept {kept} of {minutes}");
        // The helm is the watch on deck's; below, the tired sleep (the rested may turn to a needed duty).
        Assert.Equal(0, helmBelow);
        Assert.True(tiredAwakeBelow < minutes * 0.05, $"{tiredAwakeBelow} tired hands at work below");
        Assert.Equal(VoyageOutcome.UnderWay, ship.Outcome);
    }

    [Fact]
    public void AllHandsBringsTheWatchBelowOnDeckUntilDismissed()
    {
        var (runner, ship) = WithBill();
        ToTheFirstWatch(runner);
        var below = ship.Watches.Keys.Where(id => !runner.OnDeck(id)).ToList();
        Assert.NotEmpty(below);

        Assert.True(runner.CallAllHands(true).Succeeded);
        runner.Advance(5);
        Assert.All(below, id => Assert.True(runner.OnDeck(id)));
        Assert.DoesNotContain(below, id => runner.Campaign.ActiveTasks.TryGetValue(id, out var task) && Shipped.Tasks[task.DefinitionId].IsRest
                                           && runner.Campaign.Characters[id].Needs.Fatigue < Shipped.Rules.MaxFatigueToStartWork);

        runner.CallAllHands(false);
        Assert.Equal(2, runner.Campaign.Chronicle.Entries.Count(e => e.Kind == ChronicleEntryKind.AllHands));
        Assert.False(ship.AllHands);
    }

    [Fact]
    public void AnOrderGoesFirstWhateverTheWatch()
    {
        var (runner, ship) = WithBill();
        ToTheFirstWatch(runner);
        var sleeper = ship.Watches.First(pair => pair.Value != Watch.Day && !runner.OnDeck(pair.Key)).Key;
        Assert.True(runner.QueueTask(sleeper, "task.pump_bilge", Tasks.TaskPriority.Urgent).Succeeded);
        runner.Advance(3);
        Assert.Equal("task.pump_bilge", runner.Campaign.ActiveTasks[sleeper].DefinitionId);
    }

    [Fact]
    public void ACrewKeepingWatchesStillMakesTheCrossing()
    {
        var arrivals = 0;
        foreach (var seed in Enumerable.Range(1, 10))
        {
            var runner = TestDeckScenario.Create(Shipped, (ulong)seed, withDuties: true);
            runner.DrawUpWatchBill();
            var ship = runner.Campaign.Voyage!;
            while (ship.Outcome == VoyageOutcome.UnderWay && runner.Campaign.Clock.Day < 45)
            {
                runner.Advance(60);
            }

            arrivals += ship.Outcome == VoyageOutcome.Arrived ? 1 : 0;
        }

        // About as many as a crew driven round the clock (four of ten against five): fewer hands on deck,
        // but fresh ones, and the helm always kept.
        Assert.InRange(arrivals, 3, 10);
    }

    [Fact]
    public void TheWatchBillSurvivesSaveAndContinuesIdentically()
    {
        var (runner, ship) = WithBill();
        runner.CallAllHands(true);
        runner.Advance(7 * 60 + 13);
        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.True(loaded.Campaign.Voyage!.AllHands);
        Assert.Equal(ship.Watches.Count, loaded.Campaign.Voyage.Watches.Count);

        runner.Advance(10 * 60);
        loaded.Advance(10 * 60);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
