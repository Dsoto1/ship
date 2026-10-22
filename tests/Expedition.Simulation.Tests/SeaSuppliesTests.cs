using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Voyage;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>What the sea gives: rain caught in the sails, fish over the side, eaten while fresh.</summary>
public sealed class SeaSuppliesTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static (SimulationRunner Runner, VoyageState Ship, List<CharacterId> Crew) InWeather(string weather, ulong seed = CampaignState.DefaultSeed, bool withDuties = false)
    {
        var runner = TestDeckScenario.Create(Shipped, seed, withDuties);
        var ship = runner.Campaign.Voyage!;
        ship.WeatherId = weather;
        ship.WeatherUntilTick = long.MaxValue;
        var crew = runner.Campaign.Characters.Keys.Order().ToList();
        return (runner, ship, crew);
    }

    [Fact]
    public void ItRainsForAboutTheWeathersShareOfTheHours()
    {
        // With the crew at their duties, so she stays afloat through it.
        var (runner, ship, _) = InWeather("weather.gale", withDuties: true);
        var wet = 0;
        for (var hour = 0; hour < 200; hour++)
        {
            wet += runner.Raining ? 1 : 0;
            runner.Advance(60);
        }

        Assert.Equal(VoyageOutcome.UnderWay, ship.Outcome);
        Assert.InRange(wet, 70, 130);

        var (calm, _, _) = InWeather("weather.calm");
        Assert.False(calm.Raining);
    }

    [Fact]
    public void RainIsCaughtOnlyWhileItRains()
    {
        var (runner, _, crew) = InWeather("weather.calm");
        var refused = runner.AssignTask(crew[1], "task.catch_rain");
        Assert.False(refused.Succeeded);
        Assert.Contains("no rain", refused.Reason);

        var (wet, _, hands) = InWeather("weather.storm");
        while (!wet.Raining)
        {
            wet.Advance(60);
        }

        var before = wet.Campaign.SharedStore.Count("item.water");
        Assert.True(wet.AssignTask(hands[1], "task.catch_rain").Succeeded);
        // Until the rain stops or the work is done.
        for (var minute = 0; minute < 3 * 60 && wet.Campaign.ActiveTasks.ContainsKey(hands[1]); minute++)
        {
            wet.Advance(1);
        }

        Assert.True(wet.Campaign.SharedStore.Count("item.water") > before + 2, $"{before} -> {wet.Campaign.SharedStore.Count("item.water")}");
    }

    [Fact]
    public void TheRainStoppingStopsTheWork()
    {
        var (runner, ship, crew) = InWeather("weather.storm");
        while (!runner.Raining)
        {
            runner.Advance(60);
        }

        Assert.True(runner.AssignTask(crew[1], "task.catch_rain").Succeeded);
        ship.WeatherId = "weather.calm";
        runner.Advance(2);

        Assert.False(runner.Campaign.ActiveTasks.TryGetValue(crew[1], out var task) && task.DefinitionId == "task.catch_rain");
    }

    [Fact]
    public void FishBiteInACalmAndBetterOverTheBankButNotInAGale()
    {
        int Catch(bool onTheBank, ulong seed)
        {
            var (runner, ship, crew) = InWeather("weather.calm", seed);
            if (onTheBank)
            {
                runner.DeveloperPlaceOffCoast(15, 40);
            }

            var before = runner.Campaign.SharedStore.Count("item.fish");
            for (var round = 0; round < 4; round++)
            {
                runner.AssignTask(crew[2], "task.fish");
                runner.Advance(180);
            }

            return runner.Campaign.SharedStore.Count("item.fish") - before;
        }

        var deep = Enumerable.Range(1, 4).Sum(s => Catch(false, (ulong)s));
        var bank = Enumerable.Range(1, 4).Sum(s => Catch(true, (ulong)s));
        Assert.True(deep > 0);
        Assert.True(bank > deep * 1.5, $"deep {deep}, bank {bank}");

        var (gale, _, hands) = InWeather("weather.gale");
        Assert.False(gale.AssignTask(hands[2], "task.fish").Succeeded);
    }

    [Fact]
    public void FreshFoodIsEatenBeforeItSpoils()
    {
        // The quickest to spoil is eaten first: fresh fish before biscuit.
        Assert.Equal(Shipped.Foods.Max(f => f.SpoilsPerDay), Shipped.Foods[0].SpoilsPerDay);
        Assert.True(Shipped.Foods.ToList().FindIndex(f => f.Id == "item.fish") < Shipped.Foods.ToList().FindIndex(f => f.Id == "item.biscuit"));
        var (runner, _, _) = InWeather("weather.fair");
        Assert.True(runner.Campaign.SharedStore.TryAdd("item.fish", 6, Shipped.Items).Succeeded);
        var biscuit = runner.Campaign.SharedStore.Count("item.biscuit");

        // Until someone eats.
        for (var hour = 0; hour < 72 && runner.Campaign.SharedStore.Count("item.fish") == 6 && runner.Campaign.SharedStore.Count("item.biscuit") == biscuit; hour++)
        {
            runner.Advance(60);
        }

        Assert.True(runner.Campaign.SharedStore.Count("item.fish") < 6);
        Assert.Equal(biscuit, runner.Campaign.SharedStore.Count("item.biscuit"));
    }
}
