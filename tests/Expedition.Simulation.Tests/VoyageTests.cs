using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Voyage;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class VoyageTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static (SimulationRunner Runner, VoyageState Ship, List<CharacterId> Crew) Setup(bool withDuties = false, ulong seed = CampaignState.DefaultSeed)
    {
        var runner = TestDeckScenario.Create(Shipped, seed, withDuties);
        return (runner, runner.Campaign.Voyage!, runner.Campaign.Characters.Keys.Order().ToList());
    }

    private static void HoldWeather(VoyageState ship, string weather)
    {
        ship.WeatherId = weather;
        ship.WeatherUntilTick = long.MaxValue;
    }

    [Fact]
    public void TheScenarioSailsForTheEmberCoast()
    {
        var (_, ship, _) = Setup();

        Assert.Equal("the Ember Coast", ship.Destination);
        Assert.Equal(TestDeckScenario.VoyageLeagues, ship.LeaguesRemaining);
        Assert.Equal((100.0, 0.0, 100.0), (ship.Hull, ship.Bilge, ship.Rigging));
        Assert.Equal(VoyageOutcome.UnderWay, ship.Outcome);
    }

    [Fact]
    public void AVoyageStartsOnlyOnceAndOnlyAtSea()
    {
        var (runner, _, _) = Setup();

        Assert.False(runner.StartVoyage("the Garza", "anywhere", 10).Succeeded);
    }

    [Fact]
    public void LeftUntendedTheBilgeFillsAndSheFounders()
    {
        var (runner, ship, _) = Setup();

        runner.Advance(5 * 24 * 60);

        Assert.Equal(VoyageOutcome.Foundered, ship.Outcome);
        Assert.Equal(CampaignPhase.ShipwreckSurvival, runner.Campaign.Phase);
        var kinds = runner.Campaign.Chronicle.Entries.Select(e => e.Kind).ToList();
        Assert.Equal(2, kinds.Count(k => k == ChronicleEntryKind.BilgeAlarm));
        Assert.Contains(ChronicleEntryKind.Foundered, kinds);
    }

    [Fact]
    public void PumpingEmptiesTheBilge()
    {
        var (runner, ship, crew) = Setup();
        ship.Apply(new ShipEffects { Bilge = 60 }, 1);

        Assert.True(runner.AssignTask(crew[3], "task.pump_bilge").Succeeded);
        runner.Advance(61);

        Assert.InRange(ship.Bilge, 30, 50);
    }

    [Fact]
    public void WithoutAWatchSheBarelyMakesWay()
    {
        var (runner, ship, crew) = Setup();
        HoldWeather(ship, "weather.fair");
        runner.Advance(10);
        var drifting = ship.SpeedLeaguesPerHour;
        Assert.False(ship.WatchKept);

        runner.AssignTask(crew[1], "task.stand_watch");
        runner.Advance(10);

        Assert.True(ship.WatchKept);
        Assert.Equal(drifting / Shipped.Voyage!.NoWatchSpeedMultiplier, ship.SpeedLeaguesPerHour, 3);
    }

    [Fact]
    public void WornRiggingSlowsHerAndRepairsRestoreIt()
    {
        var (runner, ship, crew) = Setup();
        HoldWeather(ship, "weather.calm");
        runner.AssignTask(crew[0], "task.stand_watch");
        runner.Advance(5);
        var sound = ship.SpeedLeaguesPerHour;

        ship.Apply(new ShipEffects { Rigging = -50 }, 1);
        runner.Advance(1);
        Assert.True(ship.SpeedLeaguesPerHour < sound * 0.8);

        var before = ship.Rigging;
        Assert.True(runner.AssignTask(crew[1], "task.repair_rigging").Succeeded);
        runner.Advance(3 * 60);
        Assert.InRange(ship.Rigging, before + 10, before + 12);
    }

    [Fact]
    public void ADamagedHullLeaksFasterUntilPatched()
    {
        var (runner, ship, crew) = Setup();
        HoldWeather(ship, "weather.fair");
        runner.Advance(60);
        var soundLeak = ship.Bilge;

        ship.Apply(new ShipEffects { Hull = -50, Bilge = -100 }, 1);
        runner.Advance(60);
        Assert.True(ship.Bilge > soundLeak * 2);

        var hull = ship.Hull;
        Assert.True(runner.AssignTask(crew[4], "task.patch_hull").Succeeded);
        runner.Advance(4 * 60);
        Assert.True(ship.Hull > hull + 13);
    }

    [Fact]
    public void AStormBattersTheShip()
    {
        var (runner, ship, crew) = Setup();
        HoldWeather(ship, "weather.storm");
        runner.AssignTask(crew[3], "task.pump_bilge");

        runner.Advance(6 * 60);

        Assert.True(ship.Hull < 93, $"hull {ship.Hull}");
        Assert.True(ship.Rigging < 87, $"rigging {ship.Rigging}");
        Assert.True(ship.Bilge > 20, $"bilge {ship.Bilge}");
    }

    [Fact]
    public void SheArrivesWhenTheDistanceIsRun()
    {
        var runner = TestDeckScenario.Create(Shipped, withDuties: true, withVoyage: false);
        Assert.True(runner.StartVoyage("the Garza", "the Ember Coast", 3).Succeeded);

        runner.Advance(12 * 60);

        Assert.Equal(VoyageOutcome.Arrived, runner.Campaign.Voyage!.Outcome);
        Assert.Equal(CampaignPhase.Landfall, runner.Campaign.Phase);
        var landfall = runner.Campaign.Chronicle.Entries.Single(e => e.Kind == ChronicleEntryKind.Landfall);
        Assert.Equal("Land! The Ember Coast sighted from the masthead.", new ChronicleFormatter(runner.Campaign, Shipped).Format(landfall));
    }

    [Fact]
    public void NothingWearsOnceTheVoyageIsOver()
    {
        var runner = TestDeckScenario.Create(Shipped, withDuties: true, withVoyage: false);
        runner.StartVoyage("the Garza", "the Ember Coast", 1);
        runner.Advance(6 * 60);
        var ship = runner.Campaign.Voyage!;
        var (hull, bilge) = (ship.Hull, ship.Bilge);

        runner.Advance(24 * 60);

        Assert.Equal((hull, bilge), (ship.Hull, ship.Bilge));
    }

    [Fact]
    public void TheWeatherFollowsTheSeed()
    {
        string Weathers(ulong seed)
        {
            var (runner, ship, _) = Setup(seed: seed);
            var seen = new List<string>();
            for (var hour = 0; hour < 10 * 24; hour++)
            {
                runner.Advance(60);
                if (seen.Count == 0 || seen[^1] != ship.WeatherId)
                {
                    seen.Add(ship.WeatherId);
                }
            }

            return string.Join(",", seen);
        }

        Assert.Equal(Weathers(3), Weathers(3));
        Assert.NotEqual(Weathers(3), Weathers(4));
        Assert.True(Weathers(3).Split(',').Length > 4);
    }

    [Fact]
    public void ACrewOnDutiesUsuallyMakesTheCrossingInAboutThreeWeeks()
    {
        var arrivals = new List<double>();
        foreach (var seed in Enumerable.Range(1, 10))
        {
            var (runner, ship, _) = Setup(withDuties: true, seed: (ulong)seed);
            while (ship.Outcome == VoyageOutcome.UnderWay && runner.Campaign.Clock.Day < 45)
            {
                runner.Advance(60);
            }

            if (ship.Outcome == VoyageOutcome.Arrived)
            {
                arrivals.Add(runner.Campaign.Clock.Tick / (double)SimulationClock.TicksPerDay);
            }
        }

        // Most crews make it unaided, but not all: the captain's choices are meant to matter.
        Assert.InRange(arrivals.Count, 6, 9);
        Assert.All(arrivals, day => Assert.InRange(day, 17, 32));
    }

    [Fact]
    public void TheForecastFallsAsSuppliesAreUsed()
    {
        var (runner, _, _) = Setup(withDuties: true);
        var start = runner.SupplyForecast();
        Assert.InRange(start.WaterDays, 20, 30);
        Assert.InRange(start.FoodDays, 20, 35);

        // Nobody is hungry or thirsty on the first day, so the stores start going down on the second.
        runner.Advance(5 * 24 * 60);

        var later = runner.SupplyForecast();
        Assert.True(later.WaterDays < start.WaterDays - 2.5, $"water {start.WaterDays} -> {later.WaterDays}");
        Assert.True(later.FoodDays < start.FoodDays - 2, $"food {start.FoodDays} -> {later.FoodDays}");
    }

    [Fact]
    public void TheVoyageSurvivesSaveAndContinuesIdentically()
    {
        var (original, _, _) = Setup(withDuties: true, seed: 11);
        original.Advance(2 * 24 * 60 + 17);
        var json = SaveSerializer.Serialize(original.Campaign);

        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);

        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        original.Advance(3 * 24 * 60);
        loaded.Advance(3 * 24 * 60);
        Assert.Equal(SaveSerializer.Serialize(original.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }

    [Fact]
    public void FormatTenSaveLoadsWithoutAVoyage()
    {
        var root = JsonNode.Parse(SaveSerializer.Serialize(Setup(withDuties: true).Runner.Campaign))!.AsObject();
        root["schemaVersion"] = 10;
        root.Remove("voyage");
        foreach (var duty in root["duties"]!.AsArray())
        {
            duty!["next"] = 0;
        }

        var loaded = SaveSerializer.Deserialize(root.ToJsonString(), Shipped);

        Assert.Null(loaded.Voyage);
        Assert.NotEmpty(loaded.Duties);
    }

    [Theory]
    [InlineData("bilge")]
    [InlineData("weather")]
    [InlineData("outcome")]
    [InlineData("distance")]
    public void TamperedVoyagesAreRejected(string tampering)
    {
        var root = JsonNode.Parse(SaveSerializer.Serialize(Setup().Runner.Campaign))!.AsObject();
        var voyage = root["voyage"]!;
        switch (tampering)
        {
            case "bilge": voyage["bilge"] = 150; break;
            case "weather": voyage["weather"] = "weather.frogs"; break;
            case "outcome": voyage["outcome"] = "Raptured"; break;
            case "distance": voyage["totalLeagues"] = -5; break;
        }

        Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(root.ToJsonString(), Shipped));
    }

    [Fact]
    public void VoyageEventsReadAsEnglish()
    {
        var (runner, ship, _) = Setup();
        runner.Advance(5 * 24 * 60);
        var formatter = new ChronicleFormatter(runner.Campaign, Shipped);
        var text = runner.Campaign.Chronicle.Entries.Select(formatter.Format).ToList();

        Assert.Contains("The bilge is 50% full and rising. Man the pumps!", text);
        Assert.Contains("The Garza filled and foundered.", text);
        Assert.Contains(text, line => line.StartsWith("The wind", StringComparison.Ordinal) || line.StartsWith("The weather worsened", StringComparison.Ordinal));
        Assert.DoesNotContain(text, line => line.Contains("weather.", StringComparison.Ordinal));
    }

    [Fact]
    public void HeavingToRidesOutAStormAtTheCostOfDistance()
    {
        // Rigging wear has no dice in it, unlike seas striking the hull, so it compares cleanly.
        (double Rigging, double Sailed) RideOut(SailPlan plan)
        {
            var (runner, ship, crew) = Setup(seed: 21);
            HoldWeather(ship, "weather.storm");
            runner.AssignTask(crew[0], "task.stand_watch");
            Assert.True(runner.SetSailPlan(plan).Succeeded);
            for (var hour = 0; hour < 8; hour++)
            {
                // Nobody is pumping in this test; keep her afloat so the whole storm is ridden out.
                ship.DeveloperSetBilge(0);
                runner.Advance(60);
            }

            Assert.Equal(VoyageOutcome.UnderWay, ship.Outcome);
            return (ship.Rigging, ship.LeaguesSailed);
        }

        var hoveTo = RideOut(SailPlan.HoveTo);
        var normal = RideOut(SailPlan.Normal);
        var pressOn = RideOut(SailPlan.PressOn);

        Assert.True(hoveTo.Rigging > normal.Rigging && normal.Rigging > pressOn.Rigging, $"{hoveTo.Rigging} {normal.Rigging} {pressOn.Rigging}");
        Assert.True(hoveTo.Sailed < normal.Sailed * 0.2 && pressOn.Sailed > normal.Sailed);
    }

    [Fact]
    public void TheSailPlanIsSavedAndRecorded()
    {
        var (runner, _, _) = Setup();
        runner.SetSailPlan(SailPlan.PressOn);

        var loaded = SaveSerializer.Deserialize(SaveSerializer.Serialize(runner.Campaign), Shipped);

        Assert.Equal(SailPlan.PressOn, loaded.Voyage!.SailPlan);
        var entry = runner.Campaign.Chronicle.Entries.Last();
        Assert.Equal("The captain ordered every sail set: press on.", new ChronicleFormatter(runner.Campaign, Shipped).Format(entry));
    }

    [Fact]
    public void RepairsLeftCountWhatTheHoldCanStillMend()
    {
        var (runner, _, _) = Setup();

        // 10 oakum and 6 planks at 2 + 1 a patch; 8 coils of rope at 1 a repair.
        Assert.Equal(5, runner.RepairsLeft()[ShipPart.Hull]);
        Assert.Equal(8, runner.RepairsLeft()[ShipPart.Rigging]);

        runner.Campaign.SharedStore.TryRemove("item.oakum", 9);
        Assert.Equal(0, runner.RepairsLeft()[ShipPart.Hull]);
    }

    [Fact]
    public void BadVoyageRulesAreRejected()
    {
        var rules = Shipped.Voyage! with { StartingWeather = "weather.sunshine" };

        Assert.NotEmpty(rules.Validate());
    }
}
