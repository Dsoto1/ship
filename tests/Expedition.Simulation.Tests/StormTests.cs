using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Voyage;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Heavy weather: the warning, the preparations and what they save, leaks plugged and patched.</summary>
public sealed class StormTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static IEnumerable<ChronicleEntry> Entries(SimulationRunner runner, ChronicleEntryKind kind) =>
        runner.Campaign.Chronicle.Entries.Where(e => e.Kind == kind);

    [Fact]
    public void AGaleIsForeseenFromItsSignsHoursBeforeItBlows()
    {
        foreach (var seed in Enumerable.Range(1, 20))
        {
            var runner = TestDeckScenario.Create(Shipped, (ulong)seed, withDuties: true);
            var ship = runner.Campaign.Voyage!;
            ship.WeatherId = "weather.fresh";
            ship.WeatherUntilTick = runner.Campaign.Clock.Tick + 8 * 60;
            runner.Advance(3 * 60);
            if (ship.NextWeatherId is not ("weather.gale" or "weather.storm"))
            {
                continue;
            }

            var warning = Assert.Single(Entries(runner, ChronicleEntryKind.StormWarning));
            Assert.Single(warning.Characters);
            Assert.True(runner.StormComing);
            runner.Advance(6 * 60);
            Assert.Equal(warning.Get(ChronicleParameters.To), ship.WeatherId);
            return;
        }

        Assert.Fail("No voyage of twenty saw a gale coming after a fresh breeze.");
    }

    // A ship held in a storm for a day, with every preparation made or none, and her crew left to it.
    private static (SimulationRunner Runner, VoyageState Ship) Storm(bool prepared, ulong seed)
    {
        var runner = TestDeckScenario.Create(Shipped, seed, withDuties: true);
        var ship = runner.Campaign.Voyage!;
        ship.WeatherId = "weather.storm";
        ship.WeatherUntilTick = long.MaxValue;
        if (prepared)
        {
            foreach (var preparation in Preparations.All)
            {
                ship.Prepare(preparation);
            }
        }
        else
        {
            // Left unprepared: nobody has the preparations among their duties.
            foreach (var (id, roster) in runner.Campaign.Duties.ToList())
            {
                runner.Campaign.SetDuties(id, roster.Tasks.Where(t => Shipped.Tasks[t].Prepares is null).ToList());
            }
        }

        runner.Advance(24 * 60);
        return (runner, ship);
    }

    [Fact]
    public void PreparingForAStormSavesTheRiggingTheStoresAndTheHull()
    {
        double stores = 0, storesPrepared = 0;
        int shifts = 0, carried = 0, carriedPrepared = 0, rope = 0, ropePrepared = 0;
        foreach (var seed in Enumerable.Range(1, 6))
        {
            var (open, openShip) = Storm(false, (ulong)seed);
            var (ready, readyShip) = Storm(true, (ulong)seed);
            // The rigger mends either way, so the rigging itself reads alike: count what was carried away, and the rope spent.
            carried += Entries(open, ChronicleEntryKind.RiggingCarriedAway).Count();
            carriedPrepared += Entries(ready, ChronicleEntryKind.RiggingCarriedAway).Count();
            rope += open.Campaign.SharedStore.Count("item.rope");
            ropePrepared += ready.Campaign.SharedStore.Count("item.rope");
            stores += open.Campaign.SharedStore.Count("item.water") + open.Campaign.SharedStore.Count("item.biscuit");
            storesPrepared += ready.Campaign.SharedStore.Count("item.water") + ready.Campaign.SharedStore.Count("item.biscuit");
            shifts += Entries(open, ChronicleEntryKind.CargoShifted).Count();
            Assert.Empty(Entries(ready, ChronicleEntryKind.CargoShifted));
            Assert.Empty(Entries(ready, ChronicleEntryKind.StoresWetted));
        }

        Assert.True(carriedPrepared < carried, $"carried away {carried} open, {carriedPrepared} prepared");
        Assert.True(ropePrepared >= rope, $"rope left {rope} open, {ropePrepared} prepared");
        Assert.True(storesPrepared > stores, $"stores {stores} open, {storesPrepared} prepared");
        Assert.True(shifts > 0);
    }

    [Fact]
    public void TheCrewPrepareWhenTheyHaveTheDutyAndUndoItInFairWeather()
    {
        var runner = TestDeckScenario.Create(Shipped, withDuties: true);
        var ship = runner.Campaign.Voyage!;
        ship.WeatherId = "weather.gale";
        ship.WeatherUntilTick = long.MaxValue;
        runner.Advance(6 * 60);
        Assert.Equal(Preparations.All.Order(), ship.Prepared.Order());
        Assert.True(Entries(runner, ChronicleEntryKind.Prepared).Count() >= 3);

        ship.WeatherId = "weather.fair";
        ship.WeatherUntilTick = long.MaxValue;
        runner.Advance(5);
        Assert.Empty(ship.Prepared);
        Assert.Single(Entries(runner, ChronicleEntryKind.PreparationsUndone));
    }

    [Fact]
    public void ALeakIsPluggedQuicklyButCanBurstAndIsMadeGoodWithPlank()
    {
        var runner = TestDeckScenario.Create(Shipped, withDuties: false);
        var ship = runner.Campaign.Voyage!;
        ship.WeatherId = "weather.fair";
        ship.WeatherUntilTick = long.MaxValue;
        var leak = new Leak { Id = 1, Where = "the starboard bow", Severity = 3, Tick = 0 };
        ship.Spring(leak);
        var crew = runner.Campaign.Characters.Keys.Order().ToList();

        Assert.True(runner.AssignTask(crew[4], "task.plug_leak").Succeeded);
        runner.Advance(60);
        Assert.Equal(LeakRepair.Plugged, ship.Leaks.Single().Repair);
        Assert.Equal(0.75, ship.Leaks.Single().Inflow, 6);
        Assert.False(runner.AssignTask(crew[4], "task.plug_leak").Succeeded);

        // In heavy weather the plug may not hold.
        ship.WeatherId = "weather.storm";
        for (var hour = 0; hour < 200 && ship.Leaks.Single().Repair == LeakRepair.Plugged; hour++)
        {
            runner.Advance(60);
            if (ship.Outcome != VoyageOutcome.UnderWay)
            {
                return;
            }
        }

        Assert.Contains(Entries(runner, ChronicleEntryKind.LeakReopened), e => e.Get(ChronicleParameters.To) == "the starboard bow");

        // A proper patch makes it good, and the history is kept.
        ship.WeatherId = "weather.fair";
        Assert.True(runner.AssignTask(crew[4], "task.patch_hull").Succeeded);
        runner.Advance(5 * 60);
        Assert.Equal(LeakRepair.Patched, ship.Leaks.Single().Repair);
        Assert.Equal(0, ship.Leaks.Single().Inflow);
        Assert.Single(Entries(runner, ChronicleEntryKind.LeakPatched));
    }

    [Fact]
    public void HeavyWeatherSurvivesSaveAndContinuesIdentically()
    {
        var (runner, _) = Storm(prepared: false, seed: 3);
        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        runner.Advance(6 * 60);
        loaded.Advance(6 * 60);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
