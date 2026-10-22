using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Voyage;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>R4a and R4b's remainder: fog and the hidden sun, reading the signs, why she is slow, legs and notes on the chart.</summary>
public sealed class ChartAndWeatherTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static (SimulationRunner Runner, VoyageState Ship, Navigation Navigation) Deck(string weather = "weather.fair", ulong seed = CampaignState.DefaultSeed)
    {
        var runner = TestDeckScenario.Create(Shipped, seed);
        var ship = runner.Campaign.Voyage!;
        ship.WeatherId = weather;
        ship.WeatherUntilTick = long.MaxValue;
        return (runner, ship, ship.Navigation!);
    }

    private static void ToNoon(SimulationRunner runner)
    {
        var now = runner.Campaign.Clock.Tick % SimulationClock.TicksPerDay;
        runner.Advance((int)((12 * SimulationClock.TicksPerHour - now + SimulationClock.TicksPerDay) % SimulationClock.TicksPerDay));
    }

    [Fact]
    public void FogHidesTheSunAndTheLand()
    {
        var (runner, _, _) = Deck("weather.fog");
        runner.SetNavigator(null);
        ToNoon(runner);
        Assert.True(runner.NoonNow);
        Assert.False(runner.CanTakeNoonSight);
        var (result, _) = runner.TakeNoonSight(runner.Campaign.CaptainId!.Value, 1);
        Assert.Contains("hidden", result.Reason);

        var fog = Shipped.Voyage!.Weather("weather.fog");
        var fair = Shipped.Voyage.Weather("weather.fair");
        Assert.True(fog.Seeing < fair.Seeing * 0.2, $"fog sees {fog.Seeing:0.00} of a clear day");
    }

    [Fact]
    public void ASeasonedSeamanReadsTheSignsSooner()
    {
        var storms = Shipped.Voyage!.Storms;
        var (runner, _, _) = Deck();
        var landsman = runner.Campaign.Crew.First(c => c.Skills.Get("skill.seamanship") == 0);
        var seaman = runner.Campaign.Crew.First(c => c.Skills.Get("skill.seamanship") >= 3);
        Assert.True(SimulationRunner.WarningHours(storms, seaman) > SimulationRunner.WarningHours(storms, landsman) * 1.5);
        Assert.True(SimulationRunner.WarningHours(storms, null) >= 2);
    }

    [Fact]
    public void SheSaysWhySheIsSlow()
    {
        var (runner, ship, _) = Deck("weather.gale");
        runner.SetSailPlan(SailPlan.HoveTo);
        ship.YardSprung = true;
        runner.Advance(5);
        var reasons = runner.SpeedReasons().Select(r => r.Reason).ToList();
        Assert.Contains("hove to", reasons);
        Assert.Contains("the topsail yard sprung", reasons);
        Assert.Equal("hove to", reasons[0]);
    }

    [Fact]
    public void SheSteersForEachLegInTurnAndThenThePassage()
    {
        var (runner, _, navigation) = Deck();
        var here = navigation.Reckoned;
        // A leg well to the south of her course.
        var leg = new ChartPoint(here.X - 20, here.Y - 30);
        Assert.True(runner.PlotLeg(leg).Succeeded);
        runner.Advance(30);
        var bearing = (Math.Atan2(leg.X - here.X, leg.Y - here.Y) * 180 / Math.PI + 360) % 360;
        Assert.True(Math.Abs(Navigation.Wrap(navigation.Heading - bearing)) < 70, $"heading {navigation.Heading:0}, leg bears {bearing:0}");

        var plan = runner.LegPlan();
        Assert.Equal(2, plan.Count);
        Assert.True(plan[0].LowDays < plan[0].HighDays);

        for (var hour = 0; hour < 6 * 24 && navigation.Legs.Count > 0; hour++)
        {
            runner.Advance(60);
        }

        Assert.Empty(navigation.Legs);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.LegReached);
    }

    [Fact]
    public void ALegOnTheLandIsRefused()
    {
        var (runner, _, navigation) = Deck();
        Assert.False(runner.PlotLeg(new ChartPoint(navigation.Destination.X - 5, navigation.Destination.Y)).Succeeded);
        Assert.False(runner.PlotLeg(new ChartPoint(20, 0)).Succeeded);
        Assert.False(runner.ClearLegs().Succeeded);
    }

    [Fact]
    public void TheChartKeepsTheCaptainsNotesAndWhatTheVoyageMet()
    {
        var (runner, ship, navigation) = Deck();
        Assert.True(runner.WriteOnChart(navigation.Reckoned, "Weed and birds here").Succeeded);
        Assert.False(runner.WriteOnChart(navigation.Reckoned, new string('x', 80)).Succeeded);

        ship.WeatherId = "weather.fresh";
        ship.WeatherUntilTick = runner.Campaign.Clock.Tick + 1;
        ship.NextWeatherId = "weather.gale";
        runner.Advance(3);
        Assert.Contains(navigation.Notes, n => !n.ByCaptain && n.Text.StartsWith("A gale", StringComparison.Ordinal));

        // The voyage's own marks cannot be rubbed out; the captain's can.
        Assert.False(runner.RubOut(navigation.Notes.ToList().FindIndex(n => !n.ByCaptain)).Succeeded);
        Assert.True(runner.RubOut(0).Succeeded);
        Assert.DoesNotContain(navigation.Notes, n => n.ByCaptain);
    }

    [Fact]
    public void LegsAndNotesSurviveSaveAndContinueIdentically()
    {
        var (runner, _, navigation) = Deck();
        runner.PlotLeg(new ChartPoint(navigation.Reckoned.X - 30, navigation.Reckoned.Y - 10));
        runner.PlotLeg(new ChartPoint(navigation.Reckoned.X - 60, navigation.Reckoned.Y - 25));
        runner.WriteOnChart(navigation.Reckoned, "Sargasso weed");
        runner.Advance(5 * 60 + 7);
        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.Equal(2, loaded.Campaign.Voyage!.Navigation!.Legs.Count);

        runner.Advance(10 * 60);
        loaded.Advance(10 * 60);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
