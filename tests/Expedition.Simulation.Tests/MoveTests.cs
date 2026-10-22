using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Shore;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Step 2, the move (docs/THE_MOVE.md): the camp moved by its people, what is left fetched.</summary>
public sealed class MoveTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Landed()
    {
        var runner = TestDeckScenario.Create(Shipped, CampaignState.DefaultSeed);
        runner.Advance(60);
        Assert.True(runner.DeveloperLand("coast.punta_ceniza").Succeeded);
        runner.Advance(24 * 60);
        return runner;
    }

    // Hours on, the meetings that come waved off, so nothing is traded away.
    private static void Play(SimulationRunner runner, int hours, Func<bool>? until = null)
    {
        for (var hour = 0; hour < hours && until?.Invoke() != true; hour++)
        {
            runner.Advance(60);
            runner.DeveloperDismissMeeting();
        }
    }

    // Every one of an item the expedition has, wherever it lies: in the camp's stores, the old camps', a
    // party's loads, or broken waiting to be mended.
    private static int Everywhere(SimulationRunner runner, string item) =>
        runner.Campaign.SharedStore.Count(item)
        + runner.Campaign.LeftCamps.Values.Sum(c => runner.StoresOf(c).GetValueOrDefault(item))
        + (runner.Campaign.World?.Journeys.Sum(j => j.Loads.GetValueOrDefault(item)) ?? 0)
        + (runner.Campaign.Camp?.Broken.GetValueOrDefault(item) ?? 0) + runner.Campaign.LeftCamps.Values.Sum(c => c.Broken.GetValueOrDefault(item))
        + (runner.Campaign.Camp?.Afloat.GetValueOrDefault(item) ?? 0) + runner.Campaign.LeftCamps.Values.Sum(c => c.Afloat.GetValueOrDefault(item))
        + runner.Campaign.Characters.Values.Sum(c => runner.Campaign.Inventories[c.InventoryId].Count(item));

    private static readonly string[] Kept = ["item.trade_cloth", "item.trade_knives", "item.spade", "item.axe", "item.embrawood"];

    [Fact]
    public void APartyLooksASiteOverAsAPlaceToSettle()
    {
        var runner = Landed();
        Assert.Empty(runner.Survey("place.factory"));
        var party = runner.Campaign.Crew.Where(c => c.Id != runner.Campaign.CaptainId && c.CanWork).Take(1).Select(c => c.Id).ToList();
        Assert.True(runner.SendParty(party, "place.factory", "explore").Succeeded);
        Play(runner, 24, () => runner.Campaign.World!.Journeys.Count == 0);
        var survey = runner.Survey("place.factory");
        Assert.Contains(survey, l => l.Topic == "The charter" && l.Text.Contains("own ground", StringComparison.Ordinal));
        Assert.Contains(survey, l => l.Topic == "Water" && l.Text.Contains("4 minutes", StringComparison.Ordinal));
        Assert.Contains(survey, l => l.Topic == "Holding it" && l.Text.Contains("neck", StringComparison.Ordinal));
        Assert.Contains(survey, l => l.Topic == "Whose ground" && l.Text.Contains("Oruwai", StringComparison.Ordinal));
        // The beach the camp stands on is not the charter's.
        Assert.Contains(runner.Survey("place.punta_ceniza"), l => l.Topic == "The charter" && l.Text.StartsWith("Not", StringComparison.Ordinal));
    }

    [Fact]
    public void TheCampIsMovedByItsPeopleAndWhatTheyCannotCarryStays()
    {
        var runner = Landed();
        // Logs cut and stacked: more than one move can carry.
        runner.Campaign.SharedStore.TryAdd("item.embrawood", 40, Shipped.Items);
        Play(runner, 24);
        var before = Kept.ToDictionary(i => i, i => Everywhere(runner, i));
        var stores = runner.Campaign.SharedStore.Quantities.Sum(q => q.Value * Shipped.Items[q.Key].WeightKg);
        var people = runner.Campaign.Crew.Count(c => runner.Campaign.Placements.ContainsKey(c.Id));
        Assert.True(runner.MoveCamp("place.factory").Succeeded);
        Assert.False(runner.MoveCamp("place.factory").Succeeded);
        var journey = Assert.Single(runner.Campaign.World!.Journeys);
        Assert.Equal(people, journey.Members.Count);
        Assert.All(journey.Members, m => Assert.False(runner.Campaign.Placements.ContainsKey(m)));
        // Packs and the boat, not everything.
        var carried = journey.Loads.Sum(l => l.Value * Shipped.Items[l.Key].WeightKg);
        Assert.InRange(carried, 1, people * 25 + 500 + 1);
        Assert.True(carried < stores);
        // Tools are taken first: whatever of them is not in a worker's hands.
        Assert.Contains(journey.Loads.Keys, i => Shipped.Items[i].Category == "tool");

        Play(runner, 12, () => runner.Campaign.Camp!.Site == "coast.ceniza_headland");
        var camp = runner.Campaign.Camp!;
        Assert.Equal("coast.ceniza_headland", camp.Site);
        Assert.True(camp.Reported);
        Assert.Contains("water.factory_spring", camp.Found);
        Assert.Equal(people, runner.Campaign.Crew.Count(c => runner.Campaign.Placements.ContainsKey(c.Id)));
        // The old camp stays, with what was not carried; nothing was made or lost on the way.
        var old = Assert.Single(runner.Campaign.LeftCamps.Values);
        Assert.Equal("coast.punta_ceniza", old.Site);
        Assert.True(runner.StoresOf(old).Count > 0 || old.Afloat.Count > 0 || runner.Campaign.Camp!.Afloat.Count > 0);
        Assert.All(Kept, i => Assert.Equal(before[i], Everywhere(runner, i)));
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.CampMoved);
        // Its own ground.
        Assert.NotEqual(CampGround.Make(Shipped.Locations[SimulationRunner.CampLocation], "coast.punta_ceniza", "headland", runner.Campaign.Seed).Rows,
            runner.Location(SimulationRunner.CampLocation).Rows);
    }

    [Fact]
    public void WhatIsLeftIsFetchedByPartiesAndTheBoat()
    {
        var runner = Landed();
        // Logs cut and stacked: more than one move can carry.
        runner.Campaign.SharedStore.TryAdd("item.embrawood", 40, Shipped.Items);
        Play(runner, 24);
        var before = Kept.ToDictionary(i => i, i => Everywhere(runner, i));
        Assert.True(runner.MoveCamp("place.factory").Succeeded);
        Play(runner, 12, () => runner.Campaign.Camp!.Site == "coast.ceniza_headland");
        var old = runner.Campaign.LeftCamps["coast.punta_ceniza"];
        var left = runner.StoresOf(old).Sum(q => q.Value * Shipped.Items[q.Key].WeightKg);
        var hands = runner.Campaign.Crew.Where(c => c.Id != runner.Campaign.CaptainId && c.CanWork).Take(2).Select(c => c.Id).ToList();
        var sent = runner.SendParty(hands, "place.punta_ceniza", "fetch");
        Assert.True(sent.Succeeded, sent.Reason);
        var trip = Assert.Single(runner.Campaign.World!.Journeys);
        Assert.True(trip.Boat);
        Assert.False(runner.Campaign.Camp!.Boat);
        Play(runner, 48, () => runner.Campaign.World!.Journeys.Count == 0);
        Assert.True(runner.Campaign.Camp!.Boat);
        Assert.True(runner.StoresOf(old).Sum(q => q.Value * Shipped.Items[q.Key].WeightKg) < left);
        Assert.All(Kept, i => Assert.Equal(before[i], Everywhere(runner, i)));
    }

    [Fact]
    public void MovingBackTakesUpTheOldCampAsItWasLeft()
    {
        var runner = Landed();
        Assert.True(runner.DeveloperRaise("building.hut", 0.6).Succeeded);
        Assert.True(runner.MoveCamp("place.factory").Succeeded);
        Play(runner, 12, () => runner.Campaign.Camp!.Site == "coast.ceniza_headland");
        Assert.Empty(runner.Campaign.Camp!.Buildings);
        Assert.True(runner.MoveCamp("place.punta_ceniza").Succeeded);
        Play(runner, 12, () => runner.Campaign.Camp!.Site == "coast.punta_ceniza");
        Assert.Single(runner.Campaign.Camp!.Buildings);
        Assert.Contains("coast.ceniza_headland", runner.Campaign.LeftCamps.Keys);
    }

    [Fact]
    public void TheCharterWantsTheFactoryOnTheHeadland()
    {
        var runner = Landed();
        Play(runner, 96);
        Assert.Contains(runner.CharterTerms(), t => t.Term.Contains("headland", StringComparison.Ordinal) && !t.Met);
        Assert.True(runner.MoveCamp("place.factory").Succeeded);
        Play(runner, 12, () => runner.Campaign.Camp!.Site == "coast.ceniza_headland");
        Play(runner, 96);
        Assert.Contains(runner.CharterTerms(), t => t.Term.Contains("headland", StringComparison.Ordinal) && t.Met);
    }

    [Fact]
    public void AMoveAndTheCampsLeftAreSaved()
    {
        var runner = Landed();
        Assert.True(runner.MoveCamp("place.factory").Succeeded);
        runner.Advance(30);
        // Mid-move: the loads on the road.
        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Play(runner, 12);
        Play(loaded, 12);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
        // Settled, with a camp left behind.
        json = SaveSerializer.Serialize(loaded.Campaign);
        var again = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal("coast.ceniza_headland", again.Campaign.Camp!.Site);
        Assert.Equal(runner.StoresOf(runner.Campaign.LeftCamps["coast.punta_ceniza"]), again.StoresOf(again.Campaign.LeftCamps["coast.punta_ceniza"]));
        Assert.Equal(json, SaveSerializer.Serialize(again.Campaign));
    }
}
