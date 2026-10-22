using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Stores where they lie inside the camp (docs/THE_MOVE.md; ECONOMY_LOOP §11-12): carried, not teleported.</summary>
public sealed class CarryingTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
    private static readonly string[] Materials = ["item.poles", "item.thatch", "item.stone", "item.plank"];

    private static SimulationRunner Building()
    {
        var runner = TestDeckScenario.Create(Shipped, CampaignState.DefaultSeed);
        runner.Advance(60);
        Assert.True(runner.DeveloperLand("coast.punta_ceniza").Succeeded);
        runner.Advance(24 * 60);
        Assert.True(runner.OrderBuilding("building.hut").Succeeded);
        return runner;
    }

    private static void Minute(SimulationRunner runner)
    {
        runner.Advance(1);
        runner.DeveloperDismissMeeting();
    }

    [Fact]
    public void WhatIsCutIsInTheWorkersHandsUntilCarriedToTheStores()
    {
        var runner = Building();
        var camp = runner.Campaign.Camp!;
        int holder = 0;
        string item = "";
        for (var minute = 0; minute < 4 * 24 * 60 && holder == 0; minute++)
        {
            Minute(runner);
            foreach (var (who, held) in camp.InHand)
            {
                Assert.True(held.Sum(h => h.Value * Shipped.Items[h.Key].WeightKg) <= 25);
                if (held.Keys.FirstOrDefault(Materials.Contains) is { } material)
                {
                    (holder, item) = (who, material);
                }
            }
        }

        Assert.NotEqual(0, holder);
        // In hand, not yet in the stores; then carried, and in the stores.
        var inStores = runner.Campaign.SharedStore.Count(item);
        var carried = 0;
        for (var minute = 0; minute < 24 * 60 && camp.InHand.ContainsKey(holder); minute++)
        {
            carried = camp.InHand[holder].GetValueOrDefault(item);
            Minute(runner);
        }

        Assert.False(camp.InHand.ContainsKey(holder));
        Assert.True(runner.Campaign.SharedStore.Count(item) >= inStores + carried - 6);
        var placement = runner.Campaign.Placements[new CharacterId(holder)];
        Assert.True(placement.CellAt(runner.Campaign.Clock.Tick).ManhattanDistance(runner.StoreCell()!.Value) <= 2);
    }

    [Fact]
    public void TheMaterialsForAPieceOfWorkAreTakenUpAtTheStoresOnTheWay()
    {
        var runner = Building();
        var store = runner.StoreCell()!.Value;
        var seen = false;
        for (var minute = 0; minute < 6 * 24 * 60 && !seen; minute++)
        {
            Minute(runner);
            foreach (var (id, task) in runner.Campaign.ActiveTasks)
            {
                // A phase begun afresh (not resumed on materials already laid on the plot) walks by the stores.
                if (Shipped.Tasks[task.DefinitionId] is { BuildPhase: true, Consumes.Count: > 0 } && runner.Campaign.Placements[id].Movement is { } walk
                    && walk.StartTick == runner.Campaign.Clock.Tick && walk.Path[0].ManhattanDistance(store) > 1
                    && walk.Path.Any(cell => cell.ManhattanDistance(store) <= 1))
                {
                    seen = true;
                }
            }
        }

        Assert.True(seen);
    }

    [Fact]
    public void TheStoresLieAtTheStorehouseOnceItStands()
    {
        var runner = Building();
        var beach = runner.StoreCell();
        var stack = runner.Location(SimulationRunner.CampLocation).FindSpot("spot.stores")!;
        Assert.Equal(new GridPosition(stack.X, stack.Y), beach);
        Assert.True(runner.DeveloperRaise("building.storehouse", 0.7).Succeeded);
        var door = runner.StoreCell();
        Assert.NotEqual(beach, door);
        Assert.Contains(runner.CampSpots(), s => new GridPosition(s.X, s.Y) == door && s.Id.EndsWith(".a", StringComparison.Ordinal));
    }

    [Fact]
    public void ALoadInTheHandsOfSomeoneWhoFallsIsTakenUpByOthers()
    {
        var runner = Building();
        var camp = runner.Campaign.Camp!;
        for (var minute = 0; minute < 4 * 24 * 60 && camp.InHand.Count == 0; minute++)
        {
            Minute(runner);
        }

        var (who, held) = camp.InHand.First();
        var before = held.ToDictionary(h => h.Key, h => runner.Campaign.SharedStore.Count(h.Key) + h.Value);
        var character = runner.Campaign.Characters[new CharacterId(who)];
        character.Health.AddCondition("condition.fever");
        Assert.True(character.TrySetSurvivalState(SurvivalState.Incapacitated).Succeeded);
        for (var minute = 0; minute < 61 && camp.InHand.ContainsKey(who); minute++)
        {
            runner.Advance(1);
        }

        Assert.False(camp.InHand.ContainsKey(who));
        Assert.All(before, b => Assert.True(runner.Campaign.SharedStore.Count(b.Key) >= b.Value - 8));
    }

    [Fact]
    public void ALoadInHandIsSaved()
    {
        var runner = Building();
        var camp = runner.Campaign.Camp!;
        for (var minute = 0; minute < 4 * 24 * 60 && camp.InHand.Count == 0; minute++)
        {
            Minute(runner);
        }

        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(camp.InHand.Keys, loaded.Campaign.Camp!.InHand.Keys);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        runner.Advance(6 * 60);
        loaded.Advance(6 * 60);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
