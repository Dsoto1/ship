using Expedition.Simulation.Content;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;

namespace Expedition.Simulation.Tests;

public class TestDeckScenarioTests
{
    private static GameContent Shipped() => ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    [Fact]
    public void ScenarioStartsAtSeaWithAPlacedCrewAndStockedHold()
    {
        var runner = TestDeckScenario.Create(Shipped());
        var campaign = runner.Campaign;

        Assert.Equal(Campaign.CampaignPhase.Voyage, campaign.Phase);
        Assert.Equal(6, campaign.Characters.Count);
        Assert.Equal(6, campaign.Placements.Count);
        var deck = runner.Content.Locations[TestDeckScenario.LocationId];
        Assert.All(campaign.Placements.Values, p => Assert.True(deck.IsWalkable(p.Cell)));
        Assert.Equal(TestDeckScenario.HoldId, campaign.SharedStoreId);
        Assert.Equal(170, campaign.SharedStore.Count("item.biscuit"));
    }

    [Fact]
    public void EveryShipboardTaskCanBeAssignedSomewhereOnTheDeck()
    {
        var runner = TestDeckScenario.Create(Shipped(), withDuties: false);
        var crew = runner.Campaign.Characters.Values.OrderBy(c => c.Id).ToList();

        Assert.True(runner.AssignTask(crew[1].Id, "task.repair_rigging").Succeeded);
        Assert.True(runner.AssignTask(crew[4].Id, "task.patch_hull").Succeeded);
        Assert.True(runner.AssignTask(crew[2].Id, "task.cook").Succeeded);
        Assert.True(runner.AssignTask(crew[3].Id, "task.pump_bilge").Succeeded);
        Assert.True(runner.AssignTask(crew[5].Id, "task.stand_watch").Succeeded);
        Assert.True(runner.AssignTask(crew[0].Id, "task.rest").Succeeded);

        runner.Advance(8 * 60);

        Assert.Empty(runner.Campaign.ActiveTasks);
    }

    [Fact]
    public void ScenarioRoundTripsThroughASave()
    {
        var runner = TestDeckScenario.Create(Shipped());
        runner.AssignTask(runner.Campaign.Characters.Values.First().Id, "task.stand_watch");
        runner.Advance(30);
        var json = SaveSerializer.Serialize(runner.Campaign);

        Assert.Equal(json, SaveSerializer.Serialize(SaveSerializer.Deserialize(json, Shipped())));
    }
}
