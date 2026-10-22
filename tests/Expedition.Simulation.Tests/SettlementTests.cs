using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Shore;
using Expedition.Simulation.Core;
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
        Assert.Equal($"b{project.Id}", project.Plot);
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
    public void BuildingsGoWhereTheGroundAllows()
    {
        var runner = Landed();
        bool Refused(int x, int y, string why) => runner.Placement("building.hut", x, y) is { Allowed: false } check
                                                 && check.Problems.Any(p => p.Contains(why, StringComparison.Ordinal));

        Assert.True(Refused(12, 3, "wet"));
        Assert.True(Refused(28, 4, "Rocks"));
        Assert.True(Refused(25, 6, "a place the camp already uses"));
        Assert.True(runner.Placement("building.hut", 11, 13).Allowed);
        Assert.True(runner.PlaceBuilding("building.hut", 11, 13).Succeeded);
        Assert.True(Refused(12, 14, "Another building"));
        Assert.False(runner.PlaceBuilding("building.hut", 12, 14).Succeeded);
        // Not wall to wall: a cell of open ground is kept between them, to walk round each.
        Assert.True(Refused(15, 13, "Too close"));
        Assert.DoesNotContain(runner.Placement("building.hut", 16, 13).Problems, p => p.Contains("Too close", StringComparison.Ordinal));
        Assert.False(runner.Placement("building.palace", 20, 12).Allowed);

        // Turned, its width runs inland.
        var hut = Shipped.Shore!.Settlement!.Building("building.hut")!;
        var turned = SimulationRunner.FootprintOf(hut, 20, 12, turned: true);
        Assert.Equal((hut.Depth, hut.Width), (turned.X1 - turned.X0 + 1, turned.Y1 - turned.Y0 + 1));
    }

    [Fact]
    public void ABuildingMayNotShutTheWayFromTheHearth()
    {
        var runner = Landed();
        // Somewhere along the way through the trees to the water, a hut across it shuts it; where the forest
        // runs is the site's and the voyage's (CampGround), so it is looked for.
        var shut = Enumerable.Range(14, 10).SelectMany(y => Enumerable.Range(24, 11).Select(x => runner.Placement("building.hut", x, y)))
            .Any(c => c.Problems.Any(p => p.Contains("shut the way from the hearth to the water", StringComparison.Ordinal)));
        Assert.True(shut);
    }

    [Fact]
    public void PeopleWalkRoundBuildings()
    {
        var runner = Landed();
        Assert.True(runner.DeveloperRaise("building.hut", 0.6, (14, 13)).Succeeded);
        var hut = SimulationRunner.FootprintOf(Shipped.Shore!.Settlement!.Building("building.hut")!, 14, 13, false);
        var location = runner.Location(SimulationRunner.CampLocation);
        var path = runner.PathIn(location, new GridPosition(hut.X0 - 1, 13), new GridPosition(hut.X1 + 1, 13))!;
        Assert.NotNull(path);
        Assert.DoesNotContain(path, c => hut.Contains(c.X, c.Y));
        // Someone caught inside walks out.
        Assert.NotNull(runner.PathIn(location, new GridPosition(14, 13), new GridPosition(hut.X1 + 1, 13)));
    }

    [Fact]
    public void PathsAreWornOnlyWhereFeetGo()
    {
        var runner = Landed();
        runner.Advance(6 * 24 * 60);
        var camp = runner.Campaign.Camp!;
        var worn = camp.Trodden.Where(t => runner.PathWear(t.Value) > 0).Select(t => CampState.Cell(t.Key)).ToList();
        Assert.NotEmpty(worn);
        // A path, not a field: most of the beach is not worn.
        Assert.InRange(worn.Count, 3, 36 * 26 / 4);
        // Nobody goes to the far corner by the rocks.
        Assert.False(camp.Trodden.TryGetValue(CampState.Key(2, 6), out var corner) && runner.PathWear(corner) > 0);
    }

    [Fact]
    public void PathsFadeWhenLeftAlone()
    {
        var runner = Landed();
        var camp = runner.Campaign.Camp!;
        var key = CampState.Key(3, 7);
        var cell = camp.Tread(key);
        cell.Steps = 500;
        cell.Last = runner.Campaign.Clock.Tick;
        Assert.True(runner.PathWear(cell) > 0.9);
        runner.Advance(30 * 24 * 60);
        Assert.True(!camp.Trodden.TryGetValue(key, out var left) || runner.PathWear(left) == 0);
    }

    [Fact]
    public void AFelledTreeGrowsBackWhenLeftAlone()
    {
        var runner = Landed();
        var camp = runner.Campaign.Camp!;
        camp.PolesCut = 8;
        runner.Fell(new GridPosition(3, 21));
        var (key, growing) = Assert.Single(camp.Regrowing);
        Assert.True(growing.Tree);
        Assert.Equal(GrowthStage.Bare, runner.Growth(growing));
        var rules = Shipped.Shore!.Settlement!.Ground;
        var stages = new List<GrowthStage>();
        for (var day = 0; day < rules.TreeDays + 2 && camp.Regrowing.ContainsKey(key); day++)
        {
            runner.Advance(24 * 60);
            if (camp.Regrowing.ContainsKey(key) && (stages.Count == 0 || stages[^1] != runner.Growth(growing)))
            {
                stages.Add(runner.Growth(growing));
            }
        }

        Assert.Equal([GrowthStage.Bare, GrowthStage.Scrub, GrowthStage.Brush, GrowthStage.Sapling], stages);
        Assert.False(camp.Regrowing.ContainsKey(key));
        Assert.Equal(4, camp.PolesCut);
    }

    [Fact]
    public void GroundGivenUpGrowsBack()
    {
        var runner = Landed();
        Assert.True(runner.PlaceBuilding("building.hut", 11, 13).Succeeded);
        var camp = runner.Campaign.Camp!;
        var project = camp.Projects[0];
        Assert.True(runner.CancelProject(project.Id).Succeeded);
        Assert.Empty(camp.Regrowing);

        Assert.True(runner.PlaceBuilding("building.hut", 11, 13).Succeeded);
        project = camp.Projects[0];
        project.Restore(1, 0, 0.5, 1, 0);
        Assert.True(runner.CancelProject(project.Id).Succeeded);
        Assert.Equal(12, camp.Regrowing.Count);
    }

    [Fact]
    public void AWellIsDugOnlyWhereThereIsWaterToDigFor()
    {
        // At Punta Ceniza the ground is rock and the spring is near: no well.
        var ceniza = Landed();
        Assert.Contains(ceniza.Placement("building.well", 8, 9).Problems, p => p.Contains("no water to be had", StringComparison.Ordinal));

        var river = Landed("coast.rio_bermejo");
        var check = river.Placement("building.well", 8, 9);
        Assert.True(check.Allowed, string.Join("; ", check.Problems));
        Assert.Contains(check.Notes, n => n.Contains("Sweet water", StringComparison.Ordinal));
        Assert.Contains(river.Placement("building.well", 30, 14).Notes, n => n.Contains("latrine", StringComparison.Ordinal));

        Assert.True(river.DeveloperRaise("building.well", 0.6, (8, 9)).Succeeded);
        var water = river.CampWater();
        Assert.Equal("water.well", water?.Id);
        Assert.Equal("spring", water!.Kind);
    }

    [Fact]
    public void ALandingRunsDownToTheWaterAndTheBoatLandsMore()
    {
        var runner = Landed("coast.rio_bermejo");
        Assert.Contains(runner.Placement("building.landing", 24, 8).Problems, p => p.Contains("water's edge", StringComparison.Ordinal));
        var check = runner.Placement("building.landing", 24, 2);
        Assert.True(check.Allowed, string.Join("; ", check.Problems));
        Assert.Contains(Landed("coast.bajos_largos").Placement("building.landing", 24, 2).Problems, p => p.Contains("no rock", StringComparison.Ordinal));

        double Load(SimulationRunner r)
        {
            var camp = r.Campaign.Camp!;
            var before = camp.Afloat.Sum(a => a.Value * Shipped.Items[a.Key].WeightKg);
            r.Land(camp, null);
            return before - camp.Afloat.Sum(a => a.Value * Shipped.Items[a.Key].WeightKg);
        }

        var without = Load(Landed("coast.rio_bermejo"));
        Assert.True(runner.DeveloperRaise("building.landing", 0.6, (24, 2)).Succeeded);
        Assert.True(Load(runner) > without * 1.3, $"{without} kg without a landing");
    }

    [Fact]
    public void ALandingIsRaisedWithStoneGatheredAndPlanksSawn()
    {
        var runner = Landed("coast.rio_bermejo");
        runner.Advance(24 * 60);
        var camp = runner.Campaign.Camp!;
        Assert.True(runner.DeveloperRaise("building.workshop").Succeeded);
        // None of her own planks: they must be sawn.
        runner.Campaign.SharedStore.TryRemove("item.plank", runner.Campaign.SharedStore.Count("item.plank"));
        Assert.True(runner.PlaceBuilding("building.landing", 24, 2).Succeeded);
        for (var day = 0; day < 40 && !camp.Buildings.Any(b => b.Building == "building.landing"); day++)
        {
            runner.Advance(24 * 60);
        }

        Assert.Contains(camp.Buildings, b => b.Building == "building.landing");
        Assert.True(camp.StoneGathered >= 18);
        Assert.Equal(0, camp.TimberSalvaged);
    }

    [Fact]
    public void TheFactorysTimbersAreSalvagedForPlanks()
    {
        var runner = Landed();
        runner.Advance(24 * 60);
        var camp = runner.Campaign.Camp!;
        for (var day = 0; day < 10 && !camp.Found.Contains("find.factory"); day++)
        {
            runner.Advance(24 * 60);
        }

        Assert.Contains("find.factory", camp.Found);
        Assert.Equal(30, runner.StockLeft("camp_timber"));
        runner.Campaign.SharedStore.TryRemove("item.plank", runner.Campaign.SharedStore.Count("item.plank"));
        Assert.True(runner.PlaceBuilding("building.landing", 24, 2).Succeeded);
        for (var day = 0; day < 20 && camp.TimberSalvaged == 0; day++)
        {
            runner.Advance(24 * 60);
        }

        Assert.True(camp.TimberSalvaged > 0);
        Assert.True(runner.StockLeft("camp_timber") < 30);
    }

    [Fact]
    public void TheChandlerSellsToolsAndSeed()
    {
        var goods = Shipped.Port!.Goods.Select(g => g.Item).ToHashSet();
        foreach (var item in new[] { "item.axe", "item.saw", "item.adze", "item.spade", "item.mattock", "item.seed_onion", "item.seed_cabbage" })
        {
            Assert.Contains(item, goods);
        }
    }

    [Fact]
    public void TheGroundFillsUp()
    {
        var runner = Landed();
        var huts = 0;
        while (runner.OrderBuilding("building.hut").Succeeded)
        {
            huts++;
        }

        Assert.InRange(huts, 8, 60);
        Assert.False(runner.OrderBuilding("building.palace").Succeeded);
        Assert.True(runner.CancelProject(runner.Campaign.Camp!.Projects[0].Id).Succeeded);
        Assert.True(runner.OrderBuilding("building.hut").Succeeded);
    }

    [Fact]
    public void TreesOnTheGroundAreFelledForPoles()
    {
        var runner = Landed();
        runner.Advance(24 * 60);
        var (x, y) = Enumerable.Range(18, 6).SelectMany(cy => Enumerable.Range(2, 32).Select(cx => (cx, cy)))
            .First(at => runner.Placement("building.hut", at.cx, at.cy) is { Allowed: true, Trees: >= 3 });
        var check = runner.Placement("building.hut", x, y);
        Assert.Contains(check.Notes, n => n.Contains("to fell", StringComparison.Ordinal));
        Assert.True(runner.PlaceBuilding("building.hut", x, y).Succeeded);
        var camp = runner.Campaign.Camp!;
        var project = camp.Projects[0];
        Assert.Equal(check.Trees, project.Trees);
        Assert.True(runner.PhaseTimes(project) > 1);

        Assert.Equal(1 + (check.Trees + 2) / 3, runner.PhaseTimes(project));

        // The clearing done, its trees are poles in the store, before any were cut at the forest's edge for it.
        var poles = runner.Campaign.SharedStore.Count("item.poles");
        var cut = camp.PolesCut;
        var used = 0;
        for (var hour = 0; hour < 20 * 24 && project.Phase == 0; hour++)
        {
            runner.Advance(60);
        }

        Assert.True(project.Phase > 0);
        used = runner.Campaign.SharedStore.Count("item.poles") - poles - (camp.PolesCut - cut);
        Assert.True(used >= check.Trees * 2 - 8, $"{used} poles gained from {check.Trees} trees");
    }

    [Fact]
    public void WhereItStandsMatters()
    {
        var runner = Landed();
        var low = runner.Placement("building.storehouse", 10, 6);
        var high = runner.Placement("building.storehouse", 9, 14);
        Assert.True(low.Allowed && high.Allowed, string.Join("; ", low.Problems.Concat(high.Problems)));
        Assert.Contains(low.Notes, n => n.Contains("damp", StringComparison.Ordinal));
        Assert.Contains(high.Notes, n => n.Contains("drier", StringComparison.Ordinal));

        Assert.True(runner.DeveloperRaise("building.storehouse", 0.6, (10, 6)).Succeeded);
        var damp = runner.StoresPlaceFactor();
        runner.Campaign.Camp!.RemoveBuilding(runner.Campaign.Camp.Buildings[0]);
        Assert.True(runner.DeveloperRaise("building.storehouse", 0.6, (9, 14)).Succeeded);
        Assert.True(runner.StoresPlaceFactor() < damp);

        // The sick kept close by the huts pass it on more.
        Assert.True(runner.DeveloperRaise("building.hut", 0.6, (15, 13)).Succeeded);
        Assert.Contains(runner.Placement("building.infirmary", 13, 18).Notes, n => n.Contains("huts", StringComparison.Ordinal));
        Assert.DoesNotContain(runner.Placement("building.infirmary", 5, 18).Notes, n => n.Contains("huts", StringComparison.Ordinal));
    }

    [Fact]
    public void ACampSavedOnTheOldPlotsKeepsItsBuildingsWhereTheyStood()
    {
        var runner = Landed();
        Assert.True(runner.DeveloperRaise("building.hut").Succeeded);
        var root = JsonNode.Parse(SaveSerializer.Serialize(runner.Campaign))!.AsObject();
        root["schemaVersion"] = 38;
        var building = root["camp"]!["buildings"]![0]!.AsObject();
        building["plot"] = "hut_2";
        building.Remove("x");
        building.Remove("y");
        building.Remove("turned");
        var loaded = SaveSerializer.Deserialize(root.ToJsonString(), Shipped);
        var hut = Assert.Single(loaded.Camp!.Buildings);
        Assert.Equal(("hut_2", 15, 17), (hut.Plot, hut.X, hut.Y));
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
