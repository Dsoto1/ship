using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Houses and the smokehouse (docs/STRUCTURES.md).</summary>
public sealed class StructuresTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Landed()
    {
        var runner = TestDeckScenario.Create(Shipped, CampaignState.DefaultSeed);
        runner.Advance(60);
        Assert.True(runner.DeveloperLand("coast.punta_ceniza").Succeeded);
        for (var hour = 0; hour < 24; hour++)
        {
            runner.Advance(60);
            runner.DeveloperDismissMeeting();
        }

        return runner;
    }

    private static SimulationRunner Settled()
    {
        var runner = Landed();
        runner.Campaign.World!.Charter = "kept";
        runner.ReliefArrivals(runner.Campaign.World, Shipped.Shore!.Region!);
        Assert.Equal(2, runner.Campaign.Households.Count);
        return runner;
    }

    [Fact]
    public void AHouseGoesToTheHouseholdLongestWithoutOne()
    {
        var runner = Settled();
        var (first, second) = (runner.Campaign.Households[0], runner.Campaign.Households[1]);
        Assert.Contains(runner.Community().Causes, c => !c.Good && c.Text == "2 households with no house of their own");

        Assert.True(runner.DeveloperRaise("building.house").Succeeded);
        runner.StepPeopleDay(runner.Campaign.Camp!);
        Assert.NotNull(runner.HomeOf(first));
        Assert.Null(runner.HomeOf(second));
        Assert.Equal(1, runner.HouseholdsWithoutHomes);
        // A house's berths are dry berths like a hut's.
        Assert.Equal(4, runner.HutBerths);

        Assert.True(runner.DeveloperRaise("building.house").Succeeded);
        runner.StepPeopleDay(runner.Campaign.Camp!);
        Assert.NotNull(runner.HomeOf(second));
        Assert.Contains(runner.Community().Causes, c => c.Good && c.Text == "every household in a house of its own");

        // Living in it eases them; making do without tells on them.
        var adult = runner.Campaign.Characters[first.Adults[0]];
        adult.Needs.Stress = 30;
        runner.StepPeopleDay(runner.Campaign.Camp!);
        Assert.True(adult.Needs.Stress < 30);

        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.Equal(runner.HomeOf(first)!.Plot, loaded.HomeOf(loaded.Campaign.Households[0])!.Plot);
    }

    [Fact]
    public void FishIsSmokedAtTheSmokehouseToKeep()
    {
        var runner = Landed();
        var smoking = Shipped.Tasks["task.smoke_fish"];
        Assert.True(runner.Campaign.SharedStore.TryAdd("item.fish", 30, Shipped.Items).Succeeded);
        var before = runner.Campaign.SharedStore.Count("item.smoked_fish");
        Assert.True(Shipped.Items["item.smoked_fish"].SpoilsPerDay < Shipped.Items["item.fish"].SpoilsPerDay / 10);

        Assert.True(runner.DeveloperRaise("building.smokehouse").Succeeded);
        Assert.True(runner.DutyNeeded(smoking));
        for (var hour = 0; hour < 36 && runner.Campaign.SharedStore.Count("item.smoked_fish") <= before; hour++)
        {
            runner.Advance(60);
            runner.DeveloperDismissMeeting();
        }

        Assert.True(runner.Campaign.SharedStore.Count("item.smoked_fish") >= before + 10);
    }
}
