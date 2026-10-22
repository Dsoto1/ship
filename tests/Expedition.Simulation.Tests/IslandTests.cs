using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Voyage;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Calling at an island for water: making for it, raising it, anchoring, watering, and the island only reported.</summary>
public sealed class IslandTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
    private static readonly IReadOnlyList<Island> Islands = Shipped.Voyage!.Route!.Islands;

    // A ship with a fair wind from the east that does not change, put near an island (truly at the given
    // point, the reckoning out by the given amount).
    private static (SimulationRunner Runner, VoyageState Ship, Navigation Navigation) Near(ChartPoint at, ChartPoint reckoningOut, ulong seed = CampaignState.DefaultSeed)
    {
        var runner = TestDeckScenario.Create(Shipped, seed, withDuties: true);
        var ship = runner.Campaign.Voyage!;
        ship.WeatherId = "weather.fair";
        ship.WeatherUntilTick = long.MaxValue;
        var navigation = ship.Navigation!;
        navigation.WindFrom = 45;
        navigation.True = at;
        navigation.Reckoned = new ChartPoint(at.X + reckoningOut.X, at.Y + reckoningOut.Y);
        return (runner, ship, navigation);
    }

    private static void AdvanceUntil(SimulationRunner runner, Func<bool> done, int hours)
    {
        for (var i = 0; i < hours * 4 && !done(); i++)
        {
            runner.Advance(15);
        }
    }

    private static IEnumerable<ChronicleEntry> Entries(SimulationRunner runner, ChronicleEntryKind kind) =>
        runner.Campaign.Chronicle.Entries.Where(e => e.Kind == kind);

    [Fact]
    public void MakingForTheAguadaSheRaisesItAnchorsAndKnowsWhereSheIs()
    {
        var aguada = Islands.Single(i => i.Id == "island.aguada");
        var (runner, _, navigation) = Near(new ChartPoint(aguada.X + 25, aguada.Y + 20), new ChartPoint(4, -3));

        Assert.True(runner.MakeForIsland("island.aguada").Succeeded);
        AdvanceUntil(runner, () => runner.AnchoredAt is not null, hours: 96);

        Assert.Equal("island.aguada", runner.AnchoredAt?.Id);
        Assert.Single(Entries(runner, ChronicleEntryKind.IslandSighted));
        Assert.Equal("fix", Entries(runner, ChronicleEntryKind.Anchored).Single().Get(ChronicleParameters.Reason));
        Assert.Equal(navigation.True, navigation.Reckoned);

        // At anchor she makes no way.
        var here = navigation.True;
        runner.Advance(120);
        Assert.Equal(here, navigation.True);
    }

    [Fact]
    public void AtAnchorTheBoatWatersTheShipFastAndNotAtSea()
    {
        var aguada = Islands.Single(i => i.Id == "island.aguada");
        var (runner, ship, _) = Near(new ChartPoint(aguada.X + 3, aguada.Y), new ChartPoint(0, 0));
        var crew = runner.Campaign.Characters.Keys.Order().ToList();
        Assert.False(runner.AssignTask(crew[2], "task.water_ashore").Succeeded);

        runner.MakeForIsland("island.aguada");
        AdvanceUntil(runner, () => runner.AnchoredAt is not null, hours: 24);
        Assert.NotNull(runner.AnchoredAt);
        // Make room in the casks, then send the boat.
        runner.Campaign.SharedStore.TryRemove("item.water", Math.Min(200, runner.Campaign.SharedStore.Count("item.water")));
        var before = runner.Campaign.SharedStore.Count("item.water");
        Assert.True(runner.AssignTask(crew[2], "task.water_ashore").Succeeded);
        runner.Advance(240);

        Assert.True(runner.Campaign.SharedStore.Count("item.water") > before + 60, $"{before} -> {runner.Campaign.SharedStore.Count("item.water")}");

        // Weighing, she goes back to her passage, and the boat's work is done with.
        Assert.True(runner.ResumePassage().Succeeded);
        Assert.Null(runner.AnchoredAt);
        runner.Advance(60);
        Assert.Contains(Entries(runner, ChronicleEntryKind.WeighedAnchor), e => e.Get(ChronicleParameters.Reason) == "weighed");
        Assert.InRange(ship.Navigation!.Heading, 180, 270);
    }

    [Fact]
    public void EachVoyageMeetsTheAguadaTheEsquivaAndOneIslandOfEachBand()
    {
        var oceans = Enumerable.Range(1, 30)
            .Select(seed => TestDeckScenario.Create(Shipped, (ulong)seed, withDuties: false).Islands.Select(i => i.Id).ToList())
            .ToList();
        foreach (var islands in oceans)
        {
            Assert.Contains("island.aguada", islands);
            Assert.Contains("island.esquiva", islands);
            foreach (var band in new[] { "near", "middle", "far" })
            {
                Assert.Single(islands, id => Shipped.Voyage!.Route!.Islands.First(i => i.Id == id).Band == band);
            }
        }

        // Not the same ocean every time, but the same for the same seed.
        Assert.True(oceans.Select(o => string.Join(",", o)).Distinct().Count() > 3);
        Assert.Equal(oceans[4], TestDeckScenario.Create(Shipped, 5, withDuties: false).Islands.Select(i => i.Id).ToList());
    }

    [Fact]
    public void AReportedIslandIsThereForSomeVoyagesAndNotForOthers()
    {
        var esquiva = Islands.Single(i => i.Id == "island.esquiva");
        var truths = Enumerable.Range(1, 30)
            .Select(seed => (Seed: (ulong)seed, Truth: TestDeckScenario.Create(Shipped, (ulong)seed, withDuties: false).IslandTruth(esquiva)))
            .ToList();
        Assert.Contains(truths, t => t.Truth.Exists);
        Assert.Contains(truths, t => !t.Truth.Exists);
        Assert.All(truths, t => Assert.True(t.Truth.At.DistanceTo(esquiva.Charted) <= esquiva.PositionError + 1e-9));
        // The same voyage always finds the same.
        Assert.Equal(truths[0].Truth, TestDeckScenario.Create(Shipped, truths[0].Seed, withDuties: false).IslandTruth(esquiva));

        // Not there: she reaches its place on the chart, sees nothing, and gives it up.
        var missing = truths.First(t => !t.Truth.Exists).Seed;
        var (lost, _, lostNavigation) = Near(new ChartPoint(esquiva.X + 20, esquiva.Y + 15), new ChartPoint(0, 0), missing);
        lost.MakeForIsland("island.esquiva");
        AdvanceUntil(lost, () => lostNavigation.Call is null, hours: 72);
        Assert.Single(Entries(lost, ChronicleEntryKind.IslandNotFound));
        Assert.Null(lost.AnchoredAt);

        // There, and within sight of where it is drawn: she finds it and anchors. (Further off, she would
        // reach its place on the chart and see nothing, as for one not there.)
        var there = truths.First(t => t.Truth.Exists && t.Truth.At.DistanceTo(esquiva.Charted) < 3);
        var (found, _, _) = Near(new ChartPoint(there.Truth.At.X + 15, there.Truth.At.Y + 10), new ChartPoint(0, 0), there.Seed);
        found.MakeForIsland("island.esquiva");
        AdvanceUntil(found, () => found.AnchoredAt is not null || found.Campaign.Voyage!.Navigation!.Call is null, hours: 72);
        Assert.Equal("island.esquiva", found.AnchoredAt?.Id);
        Assert.Equal("found", Entries(found, ChronicleEntryKind.Anchored).Single().Get(ChronicleParameters.Reason));
    }

    [Fact]
    public void ThePurseGoesToSeaAndBuysFromTheIslandersAtTheAguada()
    {
        // Sailing from port, the purse goes aboard.
        var sailing = ExpeditionScenario.Create(Shipped);
        foreach (var seeker in sailing.Content.Port!.People.Where(p => p.Seeking is not null).Take(5))
        {
            sailing.DeveloperHire(sailing.CharacterFor(seeker.Id)!.Id);
        }

        var purse = sailing.Campaign.Port!.Funds;
        Assert.True(ExpeditionScenario.SetSail(sailing).Succeeded);
        Assert.Equal(purse, sailing.Campaign.Voyage!.Funds);

        var aguada = Islands.Single(i => i.Id == "island.aguada");
        var (runner, ship, _) = Near(new ChartPoint(aguada.X + 3, aguada.Y), new ChartPoint(0, 0));
        ship.Funds = 200;
        var water = aguada.Sells.Single(g => g.Item == "item.water");
        Assert.False(runner.BuyFromIslanders("item.water").Succeeded);

        runner.MakeForIsland("island.aguada");
        AdvanceUntil(runner, () => runner.AnchoredAt is not null, hours: 24);
        runner.Campaign.SharedStore.TryRemove("item.water", Math.Min(300, runner.Campaign.SharedStore.Count("item.water")));
        var before = runner.Campaign.SharedStore.Count("item.water");
        Assert.True(runner.BuyFromIslanders("item.water").Succeeded);
        Assert.Equal(before + water.Quantity, runner.Campaign.SharedStore.Count("item.water"));
        Assert.Equal(200 - water.Price, ship.Funds);
        Assert.Single(Entries(runner, ChronicleEntryKind.IslandTrade));

        // The purse runs out before their stock does.
        Assert.True(runner.BuyFromIslanders("item.water").Succeeded);
        var broke = runner.BuyFromIslanders("item.water");
        Assert.False(broke.Succeeded);
        Assert.Contains("purse", broke.Reason);
    }

    [Fact]
    public void ACallAtAnIslandSurvivesSaveAndContinuesIdentically()
    {
        var aguada = Islands.Single(i => i.Id == "island.aguada");
        var (runner, _, _) = Near(new ChartPoint(aguada.X + 12, aguada.Y + 8), new ChartPoint(2, 2));
        runner.MakeForIsland("island.aguada");
        runner.Advance(4 * 60);

        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));

        runner.Advance(12 * 60);
        loaded.Advance(12 * 60);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
