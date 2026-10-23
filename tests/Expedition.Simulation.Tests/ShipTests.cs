using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Shore;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Step S (docs/THE_SHIP.md): the ship after landfall, her guns, and the privateers.</summary>
public sealed class ShipTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Landed()
    {
        var runner = TestDeckScenario.Create(Shipped, CampaignState.DefaultSeed);
        runner.Advance(60);
        Assert.True(runner.DeveloperLand("coast.punta_ceniza").Succeeded);
        runner.Advance(60);
        runner.DeveloperDismissMeeting();
        Assert.True(runner.Campaign.Camp!.ShipAtAnchor);
        return runner;
    }

    private static List<CharacterId> Hands(SimulationRunner runner, int count) =>
        runner.Campaign.Crew.Where(c => c.Id != runner.Campaign.CaptainId && c.CanWork).Take(count).Select(c => c.Id).ToList();

    private static void Days(SimulationRunner runner, int days)
    {
        for (var day = 0; day < days; day++)
        {
            runner.StepShipDay(runner.Campaign.Camp!);
        }
    }

    [Fact]
    public void TheShipsCompanyKeepsHerAndLivesOffWhatIsAboard()
    {
        var kept = Landed();
        Assert.Equal(SimulationRunner.GunsAboard, kept.Campaign.Camp!.Guns);
        var keepers = Hands(kept, 2);
        Assert.True(kept.SetKeepers(keepers).Succeeded);
        Assert.All(keepers, k => Assert.True(kept.Aboard(k) && kept.Away(k) && !kept.Campaign.Placements.ContainsKey(k)));
        var left = Landed();
        Days(kept, 20);
        Days(left, 20);
        Assert.True(kept.Campaign.Voyage!.Hull > left.Campaign.Voyage!.Hull + 4, $"kept {kept.Campaign.Voyage.Hull:0.0}, left {left.Campaign.Voyage.Hull:0.0}");
        var pack = kept.Campaign.Inventories[kept.Campaign.Characters[keepers[0]].InventoryId];
        Assert.True(Shipped.Foods.Sum(f => pack.Count(f.Id)) >= 3);

        // Ashore again: back at the hearth, at the camp's work.
        Assert.True(kept.SetKeepers([]).Succeeded);
        Assert.All(keepers, k => Assert.True(kept.Campaign.Placements.ContainsKey(k) && !kept.Away(k)));
    }

    [Fact]
    public void WithHandsEnoughSheFishesAndCoasts()
    {
        var runner = Landed();
        Assert.True(runner.SetKeepers(Hands(runner, 3)).Succeeded);
        Assert.False(runner.SendShip("fish").Succeeded);
        Assert.True(runner.SetKeepers(Hands(runner, 4)).Succeeded);
        var fish = runner.Campaign.SharedStore.Count("item.fish");
        Assert.True(runner.SendShip("fish").Succeeded);
        Assert.False(runner.SetKeepers([]).Succeeded);
        runner.Advance(24 * 60);
        Days(runner, 1);
        Assert.Null(runner.Campaign.Camp!.Errand);
        Assert.True(runner.Campaign.SharedStore.Count("item.fish") > fish);

        Assert.Equal(PlaceKnowledge.Unknown, runner.PlaceKnown("place.brennish_cove"));
        Assert.True(runner.SendShip("coast").Succeeded);
        runner.Advance(4 * 24 * 60);
        runner.DeveloperDismissMeeting();
        Days(runner, 1);
        Assert.True(runner.PlaceKnown("place.brennish_cove") >= PlaceKnowledge.Seen);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.ShipErrand && e.Get(ChronicleParameters.To) == "coast");
    }

    [Fact]
    public void SheIsCareenedAtTheSlip()
    {
        var runner = Landed();
        var ship = runner.Campaign.Voyage!;
        ship.Apply(new Voyage.ShipEffects { Hull = 70 - ship.Hull }, 1);
        Assert.True(runner.DeveloperRaise("building.boat_shed").Succeeded);
        runner.Campaign.SharedStore.TryAdd("item.oakum", 6, Shipped.Items);
        runner.Campaign.SharedStore.TryAdd("item.plank", 3, Shipped.Items);
        var hand = runner.Campaign.Crew.First(c => c.Id != runner.Campaign.CaptainId);
        hand.Skills.Set("skill.ship_repair", 2);
        for (var hour = 0; hour < 96 && ship.Hull < 79; hour++)
        {
            runner.Advance(60);
            runner.DeveloperDismissMeeting();
        }

        Assert.True(ship.Hull >= 79, $"hull {ship.Hull:0.0}");
    }

    [Fact]
    public void PowderKeepsDryInAMagazine()
    {
        int Lost(bool magazine)
        {
            var runner = Landed();
            if (magazine)
            {
                Assert.True(runner.DeveloperRaise("building.magazine").Succeeded);
            }

            runner.Campaign.SharedStore.TryAdd("item.powder", 500, Shipped.Items);
            var before = runner.Campaign.SharedStore.Count("item.powder");
            Days(runner, 10);
            return before - runner.Campaign.SharedStore.Count("item.powder");
        }

        var open = Lost(false);
        Assert.True(open >= 25 && Lost(true) < open / 4, $"open {open}");
    }

    [Fact]
    public void GunsLandedOnThePlatformAreFoughtFromTheShore()
    {
        var runner = Landed();
        Assert.False(runner.LandGuns().Succeeded);
        Assert.True(runner.DeveloperRaise("building.battery").Succeeded);
        Assert.True(runner.LandGuns().Succeeded);
        Assert.Equal((SimulationRunner.GunsAboard - 2, 2), (runner.Campaign.Camp!.Guns, runner.Campaign.Camp.GunsAshore));
        var (aboard, ashore, _) = runner.GunsManned();
        Assert.Equal(0, aboard);
        Assert.Equal(2, ashore);
        Assert.True(runner.FightChance(2, true, 2) > runner.FightChance(2, false, 2));
    }

    [Fact]
    public void APrivateerIsSeenFromTheLookoutAndMetByDecision()
    {
        var runner = Landed();
        var world = runner.Campaign.World!;
        Assert.True(runner.DeveloperRaise("building.lookout").Succeeded);
        Assert.True(runner.SetKeepers(Hands(runner, 6)).Succeeded);
        runner.Campaign.SharedStore.TryAdd("item.powder", 10, Shipped.Items);
        runner.Campaign.SharedStore.TryAdd("item.shot", 10, Shipped.Items);
        Days(runner, 1);
        world.PrivateerNext = runner.Campaign.Clock.Tick;
        Days(runner, 1);
        Assert.Equal(1, world.Privateer);
        Assert.Null(world.Meeting);
        Days(runner, 1);
        Assert.Equal("privateer", world.Meeting?.Kind);
        Assert.Contains("letter of marque", runner.DescribeMeeting()!.Value.Text);
        var choices = runner.MeetingChoices();
        Assert.Equal(new[] { "fight", "pay", "goods", "hide" }, choices.Select(c => c.Id));
        Assert.True(choices[0].Available);

        runner.ResolveMeeting("fight");
        Assert.Null(world.Meeting);
        Assert.Equal(0, world.Privateer);
        Assert.Equal(6, runner.Campaign.SharedStore.Count("item.powder"));
        Assert.True(world.PrivateerNext > runner.Campaign.Clock.Tick + 100L * SimulationClock.TicksPerDay);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.PrivateerMet);
        // Six aboard hold her, whatever the fight's outcome.
        Assert.True(runner.Campaign.Camp!.ShipAtAnchor);
    }

    [Fact]
    public void WithTooFewAboardAPrivateerCutsOutTheShip()
    {
        var runner = Landed();
        var world = runner.Campaign.World!;
        Days(runner, 1);
        world.PrivateerNext = runner.Campaign.Clock.Tick;
        Days(runner, 1);
        Assert.Equal("privateer", world.Meeting?.Kind);
        Assert.False(runner.MeetingChoices()[0].Available);
        runner.ResolveMeeting("hide");
        Assert.False(runner.Campaign.Camp!.ShipAtAnchor);
        Assert.Empty(runner.Campaign.Camp.Afloat);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.ShipLost && e.Get(ChronicleParameters.Reason) == "privateer");
    }

    [Fact]
    public void TheShipsCompanyAndHerGunsAreSaved()
    {
        var runner = Landed();
        Assert.True(runner.SetKeepers(Hands(runner, 4)).Succeeded);
        Assert.True(runner.DeveloperRaise("building.battery").Succeeded);
        Assert.True(runner.LandGuns().Succeeded);
        Assert.True(runner.SendShip("fish").Succeeded);
        runner.Campaign.World!.PrivateerNext = 12345;

        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.Equal(4, loaded.Campaign.Camp!.Keepers.Count);
        Assert.Equal(("fish", 2), (loaded.Campaign.Camp.Errand, loaded.Campaign.Camp.GunsAshore));
        for (var hour = 0; hour < 30; hour++)
        {
            runner.Advance(60);
            loaded.Advance(60);
        }

        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
