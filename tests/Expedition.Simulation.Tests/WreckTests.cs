using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Voyage;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>The shipwreck as a transition: who is lost, the boat, what is saved by reach, where ashore, survivors as they were.</summary>
public sealed class WreckTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    // Fills her at sea, where she is truly (east, north) of the port.
    private static (SimulationRunner Runner, VoyageState Ship) Founder(ulong seed, ChartPoint at)
    {
        var runner = TestDeckScenario.Create(Shipped, seed, withDuties: true);
        var ship = runner.Campaign.Voyage!;
        ship.WeatherId = "weather.fair";
        ship.WeatherUntilTick = long.MaxValue;
        ship.Navigation!.True = ship.Navigation.Reckoned = at;
        ship.Apply(new ShipEffects { Bilge = 99.99 }, 1);
        runner.Advance(5);
        return (runner, ship);
    }

    [Fact]
    public void FounderingAtSeaTheBoatMakesForTheNearestLandWithWhatItCouldCarry()
    {
        var (runner, ship) = Founder(CampaignState.DefaultSeed, new ChartPoint(-40, -45));
        var wreck = runner.Campaign.Wreck;

        Assert.NotNull(wreck);
        Assert.Equal(VoyageOutcome.Foundered, ship.Outcome);
        Assert.Equal(CampaignPhase.ShipwreckSurvival, runner.Campaign.Phase);
        Assert.Equal("foundered", wreck.Cause);
        // Sixty leagues out from La Bocana: home is nearest.
        Assert.Equal("castrena", wreck.Ashore);
        Assert.True(wreck.DaysInBoat > 1);
        Assert.Empty(wreck.Remains);
        Assert.True(wreck.Survivors.Count > 0);
        Assert.Single(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.Wrecked);

        // What came ashore is what the boat carried, within its burden with the people in it.
        var rules = Shipped.Voyage!.Wreck;
        var kg = Shipped.Items.Values.Sum(i => runner.Campaign.SharedStore.Count(i.Id) * i.WeightKg);
        Assert.True(kg <= rules.BoatCapacityKg - rules.PersonKg * wreck.Survivors.Count + 1e-6, $"{kg} kg ashore");
        Assert.True(runner.Campaign.SharedStore.Count("item.water") + runner.Campaign.SharedStore.Count("item.biscuit") > 0);
    }

    [Fact]
    public void SurvivorsComeAshoreAsTheyWereMarkedByIt()
    {
        var before = TestDeckScenario.Create(Shipped, withDuties: true);
        var ids = before.Campaign.Crew.Select(c => c.Id).Order().ToList();
        var (runner, _) = Founder(CampaignState.DefaultSeed, new ChartPoint(-40, -45));
        foreach (var id in runner.Campaign.Wreck!.Survivors)
        {
            var survivor = runner.Campaign.Characters[id];
            Assert.Equal(SurvivalState.Alive, survivor.SurvivalState);
            Assert.True(survivor.Health.Conditions.Contains("condition.bruised") || survivor.Health.Conditions.Contains("condition.near_drowned"));
            // Their relationships come ashore with them.
            foreach (var other in ids.Where(o => o != id))
            {
                Assert.Equal(before.Campaign.Relationships.GetValueOrDefault((id, other))?.Trust, runner.Campaign.Relationships.GetValueOrDefault((id, other))?.Trust);
            }
        }

        Assert.All(runner.Campaign.Wreck.Lost, id => Assert.Contains(runner.Campaign.Characters[id].SurvivalState, new[] { SurvivalState.Dead, SurvivalState.Missing }));
    }

    [Fact]
    public void BreakingUpOnTheCoastCostsMoreLivesButLeavesTheWreckToSalvage()
    {
        var founderLost = 0;
        var struckLost = 0;
        var remains = 0;
        foreach (var seed in Enumerable.Range(1, 12))
        {
            var (foundered, _) = Founder((ulong)seed, new ChartPoint(-200, -200));
            founderLost += foundered.Campaign.Wreck!.Lost.Count;

            // Struck at night, unseen (no one on watch), with the hull at its last.
            var runner = TestDeckScenario.Create(Shipped, (ulong)seed, withDuties: false);
            var ship = runner.Campaign.Voyage!;
            ship.WeatherId = "weather.fair";
            ship.WeatherUntilTick = long.MaxValue;
            runner.Advance((int)((SimulationClock.TicksPerDay - runner.Campaign.Clock.Tick % SimulationClock.TicksPerDay) % SimulationClock.TicksPerDay));
            var navigation = ship.Navigation!;
            navigation.True = navigation.Reckoned = new ChartPoint(navigation.Destination.X + 0.5, navigation.Destination.Y - 13);
            ship.Apply(new ShipEffects { Hull = -(ship.Hull - 20) }, 1);
            for (var i = 0; i < 240 && runner.Campaign.Wreck is null; i++)
            {
                runner.Advance(1);
            }

            var wreck = runner.Campaign.Wreck;
            Assert.NotNull(wreck);
            Assert.Equal("struck", wreck.Cause);
            Assert.Equal(0, wreck.DaysInBoat);
            Assert.Equal("coast.punta_falsa", wreck.Ashore);
            struckLost += wreck.Lost.Count;
            remains += wreck.Remains.Values.Sum();
        }

        Assert.True(struckLost > founderLost, $"lost: {struckLost} breaking up, {founderLost} foundering");
        Assert.True(remains > 0);
    }

    [Fact]
    public void ALongPassageInTheBoatWithNoWaterKills()
    {
        var lostWithWater = 0;
        var lostWithout = 0;
        // Far from any land: a long run in the boat.
        var far = new ChartPoint(-120, -330);
        foreach (var seed in Enumerable.Range(1, 12))
        {
            var (watered, _) = Founder((ulong)seed, far);
            lostWithWater += watered.Campaign.Wreck!.Lost.Count;

            var runner = TestDeckScenario.Create(Shipped, (ulong)seed, withDuties: true);
            foreach (var drink in Shipped.Drinks)
            {
                runner.Campaign.SharedStore.TryRemove(drink.Id, runner.Campaign.SharedStore.Count(drink.Id));
            }

            var ship = runner.Campaign.Voyage!;
            ship.WeatherId = "weather.fair";
            ship.WeatherUntilTick = long.MaxValue;
            ship.Navigation!.True = ship.Navigation.Reckoned = far;
            ship.Apply(new ShipEffects { Bilge = 99.99 }, 1);
            runner.Advance(5);
            lostWithout += runner.Campaign.Wreck!.Lost.Count;
            Assert.True(runner.Campaign.Wreck.DaysInBoat > 2);
        }

        Assert.True(lostWithout > lostWithWater, $"lost: {lostWithout} with no water, {lostWithWater} with");
    }

    [Fact]
    public void TheWreckSurvivesSaveAndLoad()
    {
        var (runner, _) = Founder(CampaignState.DefaultSeed, new ChartPoint(-150, -160));
        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = SaveSerializer.Deserialize(json, Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded));
        Assert.Equal(runner.Campaign.Wreck!.AshoreName, loaded.Wreck!.AshoreName);
    }
}
