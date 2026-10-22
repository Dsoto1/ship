using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Voyage;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>R4c: the captain's standing orders, and calling the captain.</summary>
public sealed class StandingOrdersTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static (SimulationRunner Runner, VoyageState Ship) Deck(string weather = "weather.fair", ulong seed = CampaignState.DefaultSeed)
    {
        var runner = TestDeckScenario.Create(Shipped, seed);
        var ship = runner.Campaign.Voyage!;
        ship.WeatherId = weather;
        ship.WeatherUntilTick = long.MaxValue;
        return (runner, ship);
    }

    private static int Count(SimulationRunner runner, ChronicleEntryKind kind, string reason) =>
        runner.Campaign.Chronicle.Entries.Count(e => e.Kind == kind && e.Get(ChronicleParameters.Reason) == reason);

    [Fact]
    public void SheIsHoveToInAStormAndMakesSailWhenItEases()
    {
        var (runner, ship) = Deck("weather.storm");
        Assert.True(runner.SetStandingOrders(new StandingOrders { HeaveToInStorm = true }).Succeeded);
        runner.Advance(20);
        Assert.Equal(SailPlan.HoveTo, ship.SailPlan);
        Assert.Equal(1, Count(runner, ChronicleEntryKind.StandingOrderActed, StandingOrderReasons.HoveTo));

        ship.WeatherId = "weather.fair";
        runner.Advance(20);
        Assert.Equal(SailPlan.Normal, ship.SailPlan);
        Assert.Equal(1, Count(runner, ChronicleEntryKind.StandingOrderActed, StandingOrderReasons.MadeSail));
    }

    [Fact]
    public void SheIsNotLeftPressingOnInHeavyWeather()
    {
        var (runner, ship) = Deck("weather.gale");
        runner.SetSailPlan(SailPlan.PressOn);
        runner.SetStandingOrders(new StandingOrders { EaseInHeavyWeather = true });
        runner.Advance(20);
        Assert.Equal(SailPlan.Normal, ship.SailPlan);
    }

    [Fact]
    public void HandsAreSetToMakeReadyWhenTheSignsAreSeen()
    {
        var (runner, ship) = Deck();
        ship.NextWeatherId = "weather.storm";
        runner.SetStandingOrders(new StandingOrders { PrepareForWeather = true });
        runner.Advance(20);
        Assert.Equal(1, Count(runner, ChronicleEntryKind.StandingOrderActed, StandingOrderReasons.Preparing));
        var ordered = runner.Campaign.ActiveTasks.Values.Concat(runner.Campaign.Crew.SelectMany(c => runner.Campaign.QueueOf(c.Id)))
            .Count(t => Shipped.Tasks[t.DefinitionId].Prepares is not null);
        Assert.True(ordered >= 1);
    }

    [Fact]
    public void AllHandsAreCalledForTheBilgeAndDismissedWhenItIsPumped()
    {
        var (runner, ship) = Deck();
        runner.SetStandingOrders(new StandingOrders { AllHandsAtBilge = 40, WakeBilgeOver = null });
        ship.Apply(new ShipEffects { Bilge = 50 }, 1);
        runner.Advance(10);
        Assert.True(ship.AllHands);
        for (var i = 0; i < 48 && ship.AllHands; i++)
        {
            runner.Advance(30);
        }

        Assert.False(ship.AllHands);
        Assert.Equal(1, Count(runner, ChronicleEntryKind.StandingOrderActed, StandingOrderReasons.Dismissed));
    }

    [Fact]
    public void ShortRationsAreOrderedWhenTheStoresRunLow()
    {
        var (runner, _) = Deck();
        var (food, water) = runner.SupplyForecast();
        runner.SetStandingOrders(new StandingOrders { ShortRationsUnderDays = Math.Min(food, water) + 5 });
        runner.Advance(10);
        Assert.True(runner.Campaign.Rationing);
        Assert.Equal(1, Count(runner, ChronicleEntryKind.StandingOrderActed, StandingOrderReasons.Rations));
    }

    [Fact]
    public void TheCaptainIsCalledOnceAsAThingComesAndAgainOnlyAfterItHasPassed()
    {
        var (runner, ship) = Deck();
        runner.SetStandingOrders(new StandingOrders { WakeBilgeOver = 30, WakeStoresUnderDays = null });
        ship.Apply(new ShipEffects { Bilge = 60 }, 1);
        runner.Advance(10);
        ship.Apply(new ShipEffects { Bilge = 5 }, 1);
        runner.Advance(30);
        Assert.Equal(1, Count(runner, ChronicleEntryKind.CaptainCalled, StandingOrderReasons.WakeBilge));

        // Pumped down well below, then up again: a second call.
        ship.Apply(new ShipEffects { Bilge = -100 }, 1);
        runner.Advance(10);
        ship.Apply(new ShipEffects { Bilge = 60 }, 1);
        runner.Advance(10);
        Assert.Equal(2, Count(runner, ChronicleEntryKind.CaptainCalled, StandingOrderReasons.WakeBilge));
    }

    [Fact]
    public void TheCaptainIsCalledAtNoonAndWhenTheReckoningGrowsDoubtful()
    {
        var (runner, _) = Deck();
        runner.SetStandingOrders(new StandingOrders { WakeAtNoon = true, WakeDoubtOver = 5, WakeStoresUnderDays = null });
        runner.Advance(3 * 24 * 60);
        Assert.InRange(Count(runner, ChronicleEntryKind.CaptainCalled, StandingOrderReasons.WakeNoon), 2, 4);
        Assert.Equal(1, Count(runner, ChronicleEntryKind.CaptainCalled, StandingOrderReasons.WakeDoubt));
        var formatter = new ChronicleFormatter(runner.Campaign, runner.Content);
        Assert.Contains("may be out by", formatter.Format(runner.Campaign.Chronicle.Entries.First(e => e.Get(ChronicleParameters.Reason) == StandingOrderReasons.WakeDoubt)));
    }

    [Fact]
    public void OrdersOutOfReasonAreRefused()
    {
        var (runner, _) = Deck();
        Assert.False(runner.SetStandingOrders(new StandingOrders { AllHandsAtBilge = 120 }).Succeeded);
        Assert.False(runner.SetStandingOrders(new StandingOrders { WakeWindShift = 2 }).Succeeded);
    }

    [Fact]
    public void StandingOrdersSurviveSaveAndContinueIdentically()
    {
        var (runner, ship) = Deck("weather.storm");
        runner.SetStandingOrders(new StandingOrders { HeaveToInStorm = true, AllHandsAtBilge = 30, WakeAtNoon = true, WakeWindShift = 30 });
        runner.Advance(7 * 60 + 13);
        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.Equal(ship.Orders, loaded.Campaign.Voyage!.Orders);
        Assert.Equal(ship.OrderFlags.Order(), loaded.Campaign.Voyage.OrderFlags.Order());

        runner.Advance(10 * 60);
        loaded.Advance(10 * 60);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
