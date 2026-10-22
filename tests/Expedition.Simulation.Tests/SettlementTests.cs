using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Shore;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>R7: making the camp last through the first season (docs/R7_ESTABLISHMENT.md).</summary>
public sealed class SettlementTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Landed(string site = "coast.punta_ceniza", ulong seed = CampaignState.DefaultSeed)
    {
        var runner = TestDeckScenario.Create(Shipped, seed);
        runner.Advance(60);
        Assert.True(runner.DeveloperLand(site).Succeeded);
        return runner;
    }

    private static int Count(SimulationRunner runner, ChronicleEntryKind kind) => runner.Campaign.Chronicle.Entries.Count(e => e.Kind == kind);

    [Fact]
    public void TheShipsChestComesAshoreWithTheStores()
    {
        var runner = Landed();
        runner.Advance(24 * 60);
        Assert.Equal(3, runner.Campaign.SharedStore.Count("item.axe") + runner.Campaign.Camp!.Broken.GetValueOrDefault("item.axe")
                        - (runner.Campaign.Chronicle.Entries.Count(e => e.Kind == ChronicleEntryKind.ToolLost && e.Get(ChronicleParameters.To) == "item.axe")));
        Assert.True(runner.Campaign.SharedStore.Count("item.seed_wheat") > 0);
    }

    [Fact]
    public void ABuildingIsRaisedPhaseByPhaseOnItsPlot()
    {
        var runner = Landed();
        runner.Advance(24 * 60);
        Assert.True(runner.OrderBuilding("building.hut").Succeeded);
        var camp = runner.Campaign.Camp!;
        var project = Assert.Single(camp.Projects);
        Assert.Equal("hut_1", project.Plot);
        var phases = new List<int>();
        for (var hour = 0; hour < 30 * 24 && camp.Projects.Count > 0; hour++)
        {
            runner.Advance(60);
            if (camp.Projects.Count > 0 && (phases.Count == 0 || phases[^1] != camp.Projects[0].Phase))
            {
                phases.Add(camp.Projects[0].Phase);
            }
        }

        // Every phase in turn, never skipping one.
        Assert.Equal(Enumerable.Range(0, phases.Count), phases);
        var hut = Assert.Single(camp.Buildings);
        Assert.InRange(hut.Quality, 0.1, 1);
        Assert.Equal(1, Count(runner, ChronicleEntryKind.BuildingFinished));
        Assert.True(runner.HutBerths >= 4);
    }

    [Fact]
    public void ThereIsGroundForSoManyOfEach()
    {
        var runner = Landed();
        for (var i = 0; i < 4; i++)
        {
            Assert.True(runner.OrderBuilding("building.hut").Succeeded);
        }

        Assert.False(runner.OrderBuilding("building.hut").Succeeded);
        Assert.True(runner.OrderBuilding("building.storehouse").Succeeded);
        Assert.False(runner.OrderBuilding("building.palace").Succeeded);
        Assert.True(runner.CancelProject(runner.Campaign.Camp!.Projects[0].Id).Succeeded);
        Assert.True(runner.OrderBuilding("building.hut").Succeeded);
    }

    [Fact]
    public void TwoHandsCannotUseOneAxe()
    {
        var runner = Landed();
        runner.Advance(24 * 60);
        // Leave her one axe, and give them work that wants it.
        runner.Campaign.SharedStore.TryRemove("item.axe", runner.Campaign.SharedStore.Count("item.axe") - 1);
        runner.OrderBuilding("building.hut");
        runner.OrderBuilding("building.hut");
        for (var minute = 0; minute < 3 * 24 * 60; minute += 10)
        {
            runner.Advance(10);
            var axes = runner.Campaign.ActiveTasks.Values.Count(t => Shipped.Tasks[t.DefinitionId].RequiredTools.Contains("item.axe"));
            Assert.True(axes <= runner.Campaign.SharedStore.Count("item.axe"), $"{axes} hands on {runner.Campaign.SharedStore.Count("item.axe")} axes");
        }
    }

    [Fact]
    public void StoresKeepBetterUnderCoverAndBestInAStorehouse()
    {
        var runner = Landed();
        var open = runner.StoresPlaceFactor();
        runner.Advance(24 * 60);
        Assert.Equal(1, runner.Campaign.Camp!.Work(CampWorks.Cover));
        var covered = runner.StoresPlaceFactor();
        Assert.True(runner.DeveloperRaise("building.storehouse", 0.4).Succeeded);
        var rough = runner.StoresPlaceFactor();
        var good = Landed();
        good.DeveloperRaise("building.storehouse", 0.9);
        Assert.True(open > covered && covered > rough && rough > good.StoresPlaceFactor(), $"{open} {covered} {rough} {good.StoresPlaceFactor()}");
    }

    [Fact]
    public void TheWetComesThenTheStormAndItFindsOutTheBadlyBuilt()
    {
        var runner = Landed();
        runner.DeveloperRaise("building.hut", 0.2);
        runner.DeveloperRaise("building.hut", 0.95);
        var rules = Shipped.Shore!.Settlement;
        runner.Advance((int)((rules.WetFromDay + 0.5) * 24 * 60));
        Assert.True(runner.WetSeason);
        Assert.Equal(1, Count(runner, ChronicleEntryKind.WetSeason));
        runner.Advance((int)((rules.StormDay - rules.WetFromDay) * 24 * 60));
        Assert.True(runner.StormAshore);
        Assert.True(runner.Raining);
        runner.Advance(rules.StormHours * 60);
        Assert.False(runner.StormAshore);
        Assert.Equal(1, Count(runner, ChronicleEntryKind.StormAshore));
        var camp = runner.Campaign.Camp!;
        var good = camp.Buildings.Single(b => b.Quality > 0.9);
        var bad = camp.Buildings.FirstOrDefault(b => b.Quality < 0.3);
        Assert.True(bad is null || bad.Condition < good.Condition, $"rough {bad?.Condition}, sound {good.Condition}");
    }

    [Fact]
    public void TheGardenIsAnExperiment()
    {
        var runner = Landed();
        runner.DeveloperRaise("building.garden");
        runner.Advance(4 * 24 * 60);
        var camp = runner.Campaign.Camp!;
        Assert.True(Count(runner, ChronicleEntryKind.Sown) >= 3);
        Assert.Contains(camp.Beds, b => b.Crop == "crop.wheat");
        runner.Advance((Shipped.Shore!.Settlement.JudgeDay - 4) * 24 * 60);
        // Wheat does not take on this shore: it never comes to harvest.
        Assert.DoesNotContain(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.Harvested && e.Get(ChronicleParameters.To) == "crop.wheat");
        Assert.True(Count(runner, ChronicleEntryKind.CropFailed) + Count(runner, ChronicleEntryKind.Harvested) >= 2);
    }

    [Fact]
    public void AfterNinetyDaysTheFirstSeasonIsJudged()
    {
        var runner = Landed();
        // No huts: still in sail and brush shelters, they want dry roofs.
        runner.SetCampPriorities([.. CampPriorities.Default.Where(p => p != CampPriorities.Building), CampPriorities.Building]);
        runner.Advance(Shipped.Shore!.Settlement.JudgeDay * 24 * 60 + 60);
        var camp = runner.Campaign.Camp!;
        Assert.False(camp.Survived);
        Assert.Contains("dry roofs for all", runner.SeasonVerdict().Wanting);
        var entry = Assert.Single(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.SeasonJudged);
        Assert.Equal("failed", entry.Get(ChronicleParameters.Outcome));
    }

    [Fact]
    public void WithRoofsForAllTheSeasonIsSurvived()
    {
        var runner = Landed();
        for (var i = 0; i < 2; i++)
        {
            runner.DeveloperRaise("building.hut", 0.8);
        }

        runner.Advance(Shipped.Shore!.Settlement.JudgeDay * 24 * 60 + 60);
        Assert.Equal(runner.SeasonVerdict().Survived, runner.Campaign.Camp!.Survived);
        Assert.NotNull(runner.Campaign.Camp.Survived);
        Assert.NotEqual("a landfall camp", runner.CampStage());
    }

    [Fact]
    public void TheSettlementSurvivesSaveAndContinuesIdentically()
    {
        var runner = Landed();
        runner.Advance(24 * 60);
        runner.OrderBuilding("building.hut");
        runner.OrderBuilding("building.storehouse");
        runner.DeveloperRaise("building.garden");
        runner.Advance(6 * 24 * 60 + 13);
        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.NotEmpty(loaded.Campaign.Camp!.Beds);

        runner.Advance(5 * 24 * 60);
        loaded.Advance(5 * 24 * 60);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }

    [Fact]
    public void ACampSavedBeforeTheSettlementGainsItsPriorities()
    {
        var runner = Landed();
        var root = JsonNode.Parse(SaveSerializer.Serialize(runner.Campaign))!.AsObject();
        root["schemaVersion"] = 37;
        var camp = root["camp"]!.AsObject();
        camp["priorities"] = new JsonArray(CampPriorities.Default.Where(p => p is not CampPriorities.Building and not CampPriorities.Garden)
            .Select(p => (JsonNode)JsonValue.Create(p)!).ToArray());
        var loaded = SaveSerializer.Deserialize(root.ToJsonString(), Shipped);
        Assert.Equal(CampPriorities.Default.Count, loaded.Camp!.Priorities.Count);
        Assert.Equal(CampPriorities.Scouting, loaded.Camp.Priorities[^1]);
    }
}
