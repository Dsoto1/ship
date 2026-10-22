using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Shore;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>R8: the world around the camp (docs/R8_THE_WORLD_AROUND.md).</summary>
public sealed class WorldTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Landed(string site = "coast.punta_ceniza", int ahuneri = 0)
    {
        var runner = TestDeckScenario.Create(Shipped, CampaignState.DefaultSeed);
        runner.Advance(60);
        Assert.True(runner.DeveloperLand(site).Succeeded);
        if (ahuneri > 0)
        {
            runner.Campaign.Crew.First(c => c.Id != runner.Campaign.CaptainId).Languages.Set("language.ahuneri", ahuneri);
        }

        return runner;
    }

    private static WorldState World(SimulationRunner runner) => runner.Campaign.World!;

    [Fact]
    public void TheCoastIsKnownOnlyAsTheSailingDirectionsGiveIt()
    {
        var runner = Landed();
        Assert.Equal(PlaceKnowledge.Visited, runner.PlaceKnown("place.punta_ceniza"));
        Assert.Equal(PlaceKnowledge.Reported, runner.PlaceKnown("place.mahenu"));
        Assert.Equal(PlaceKnowledge.Unknown, runner.PlaceKnown("place.kessat"));
        Assert.Equal(PlaceKnowledge.Unknown, runner.PlaceKnown("place.brennish_cove"));
        // Kessat is there all the same: it just is not known.
        Assert.NotNull(Shipped.Shore!.Region!.Place("place.kessat"));
        Assert.Equal("watching", runner.Stance("people.oruwai"));
    }

    [Fact]
    public void MahenuComesFirstWithAGiftThatWantsAnswering()
    {
        var runner = Landed(ahuneri: 3);
        var world = World(runner);
        for (var day = 0; day < 8 && world.Meeting is null; day++)
        {
            runner.Advance(24 * 60);
        }

        Assert.Equal("people.mahenu", world.Meeting?.Community);
        Assert.NotNull(world.Meeting!.Gift);
        Assert.True(world.Communities["people.mahenu"].Met);
        var choices = runner.MeetingChoices();
        Assert.Contains(choices, c => c.Id.StartsWith("answer:", StringComparison.Ordinal));
        Assert.Contains(choices, c => c.Id == "accept");

        // Accept and give nothing: something is owed, and if it stays owed it is an insult.
        var trust = world.Communities["people.mahenu"].Trust;
        Assert.True(runner.ResolveMeeting("accept").Result.Succeeded);
        Assert.True(world.Communities["people.mahenu"].GiftOwedSince > 0);
        Assert.True(runner.ResolveMeeting("farewell").Result.Succeeded);
        Assert.Null(world.Meeting);
        runner.Advance(10 * 24 * 60);
        Assert.True(world.Communities["people.mahenu"].Trust < trust);
        Assert.True(world.Communities["people.mahenu"].Grievance > 0);
    }

    [Fact]
    public void TradeIsAtTheirValuationAndAnsweredGiftsBuildTrust()
    {
        var runner = Landed(ahuneri: 4);
        runner.Advance(24 * 60);
        var world = World(runner);
        Assert.True(runner.DeveloperMeeting("people.mahenu").Succeeded);
        var answer = runner.MeetingChoices().First(c => c.Id.StartsWith("answer:", StringComparison.Ordinal));
        var trust = world.Communities["people.mahenu"].Trust;
        Assert.True(runner.ResolveMeeting(answer.Id).Result.Succeeded);
        Assert.True(world.Communities["people.mahenu"].Trust > trust);
        Assert.Equal(0, world.Communities["people.mahenu"].GiftOwedSince);

        var trade = runner.MeetingChoices().First(c => c.Id.StartsWith("trade:", StringComparison.Ordinal));
        var parts = trade.Id.Split(':');
        var before = runner.Campaign.SharedStore.Count(parts[3]);
        Assert.True(runner.ResolveMeeting(trade.Id).Result.Succeeded);
        Assert.True(runner.Campaign.SharedStore.Count(parts[3]) > before);
    }

    [Fact]
    public void WhatTheyTellChangesWhatTheCampCanDo()
    {
        var runner = Landed(ahuneri: 4);
        runner.Advance(24 * 60);
        var world = World(runner);
        runner.DeveloperTrust("people.mahenu", 30);
        Assert.True(runner.DeveloperMeeting("people.mahenu").Succeeded);
        runner.ResolveMeeting(runner.MeetingChoices().First(c => c.Id.StartsWith("answer:", StringComparison.Ordinal)).Id);
        for (var round = 0; round < 6 && !runner.Learned("know.roots"); round++)
        {
            if (runner.MeetingChoices().FirstOrDefault(c => c.Id == "ask:know.roots") is not { } ask)
            {
                break;
            }

            runner.ResolveMeeting(ask.Id);
        }

        Assert.True(runner.Learned("know.roots"));
        Assert.Equal("people.mahenu", world.Learned["know.roots"]);
        Assert.True(runner.Campaign.SharedStore.Count("item.cassava_cuttings") >= 4);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.Learned);
    }

    [Fact]
    public void ABadInterpreterMakesMoreGoAstray()
    {
        var none = Landed();
        var fluent = Landed(ahuneri: 4);
        Assert.True(none.MisunderstandingChance("people.mahenu") > 0.3);
        Assert.True(fluent.MisunderstandingChance("people.mahenu") < 0.05);
        // The river people are spoken to in the lagoon's trade tongue, a level worse.
        Assert.True(fluent.MisunderstandingChance("people.tesiwa") > fluent.MisunderstandingChance("people.mahenu"));
    }

    [Fact]
    public void APartyGoesOutPackedAndComesBackWithTheCoastOnTheMap()
    {
        var runner = Landed(ahuneri: 3);
        runner.Advance(24 * 60);
        var world = World(runner);
        var party = runner.Campaign.Crew.Where(c => c.Id != runner.Campaign.CaptainId && c.CanWork).Take(2).Select(c => c.Id).ToList();
        var way = runner.Way("place.mahenu");
        Assert.NotNull(way);
        Assert.True(way!.Value.Guessed);
        Assert.True(runner.SendParty(party, "place.mahenu", "visit").Succeeded);
        Assert.All(party, id => Assert.True(runner.Away(id)));
        Assert.All(party, id => Assert.False(runner.Campaign.Placements.ContainsKey(id)));
        Assert.All(party, id => Assert.True(Shipped.Foods.Sum(f => runner.Campaign.Inventories[runner.Campaign.Characters[id].InventoryId].Count(f.Id)) > 0));

        for (var hour = 0; hour < 10 * 24 && world.Journeys.Count > 0; hour++)
        {
            runner.Advance(60);
        }

        Assert.Empty(world.Journeys);
        Assert.All(party, id => Assert.True(runner.Campaign.Placements.ContainsKey(id)));
        Assert.Equal(PlaceKnowledge.Visited, runner.PlaceKnown("place.mahenu"));
        Assert.Equal(PlaceKnowledge.Visited, runner.PlaceKnown("place.lagoon_shore"));
        // From Mahenu the way on to the dye-wood is seen, and the route known now.
        Assert.True(runner.PlaceKnown("place.dyewood") >= PlaceKnowledge.Seen);
        Assert.False(runner.Way("place.mahenu")!.Value.Guessed);
        Assert.True(world.Communities["people.mahenu"].Met);
    }

    [Fact]
    public void APartyCannotGoWhereNoOneHasHeardOf()
    {
        var runner = Landed();
        var someone = runner.Campaign.Crew.First(c => c.Id != runner.Campaign.CaptainId).Id;
        Assert.False(runner.SendParty([someone], "place.kessat", "explore").Succeeded);
        Assert.False(runner.SendParty([someone], "place.mahenu", "wood").Succeeded);
    }

    [Fact]
    public void CuttingTheirWoodWithoutLeaveIsAGrievance()
    {
        var runner = Landed();
        runner.Advance(24 * 60);
        var world = World(runner);
        world.Know("place.dyewood").Level = PlaceKnowledge.Reported;
        var party = runner.Campaign.Crew.Where(c => c.Id != runner.Campaign.CaptainId && c.CanWork).Take(3).Select(c => c.Id).ToList();
        Assert.True(runner.SendParty(party, "place.dyewood", "wood").Succeeded);
        for (var hour = 0; hour < 14 * 24 && world.Journeys.Count > 0; hour++)
        {
            runner.Advance(60);
        }

        Assert.True(runner.Campaign.SharedStore.Count("item.embrawood") >= 6);
        Assert.True(world.Communities["people.oruwai"].Grievance >= 10);
    }

    [Fact]
    public void HoldingTheirPeopleBreaksTheTradePeace()
    {
        var runner = Landed();
        var world = World(runner);
        world.Meeting = new Meeting { Kind = "theft", Community = "people.oruwai", Tick = runner.Campaign.Clock.Tick, Item = "item.axe" };
        var axes = runner.Campaign.SharedStore.Count("item.axe");
        Assert.True(runner.ResolveMeeting("seize").Result.Succeeded);
        Assert.True(world.PeaceBroken);
        Assert.Equal(axes + 1, runner.Campaign.SharedStore.Count("item.axe"));
        Assert.True(world.Communities["people.oruwai"].Grievance >= 40);
        Assert.Contains(runner.CharterTerms(), t => t.Term == "the trade peace kept" && !t.Met);
    }

    [Fact]
    public void SellingLogsToTheBrennishIsAgainstTheCharter()
    {
        var runner = Landed();
        var world = World(runner);
        runner.Campaign.SharedStore.TryAdd("item.embrawood", 5, Shipped.Items);
        world.Meeting = new Meeting { Kind = "brennish", Tick = runner.Campaign.Clock.Tick };
        var funds = runner.Campaign.Voyage!.Funds;
        Assert.True(runner.ResolveMeeting("sell_logs").Result.Succeeded);
        Assert.Equal(5, world.LogsSold);
        Assert.Equal(funds + 5 * Shipped.Shore!.Region!.Brennish!.BuysLog, runner.Campaign.Voyage.Funds);
        Assert.Contains(runner.CharterTerms(), t => t.Term == "the trade peace kept" && !t.Met);
    }

    [Fact]
    public void ThePalisadeIsRaisedAndMahenuNoticesIt()
    {
        var runner = Landed();
        runner.Advance(24 * 60);
        var camp = runner.Campaign.Camp!;
        Assert.True(runner.OrderPalisade(true).Succeeded);
        runner.Campaign.SharedStore.TryAdd("item.poles", 60, Shipped.Items);
        for (var day = 0; day < 30 && camp.Work(CampWorks.Palisade) < 1; day++)
        {
            runner.Advance(24 * 60);
        }

        Assert.True(camp.Work(CampWorks.Palisade) >= 1);
        Assert.True(World(runner).PalisadeNoticed);
        Assert.Contains(runner.CharterTerms(), t => t.Term == "a palisade" && t.Met);
    }

    [Fact]
    public void TheWorldIsSavedWithAPartyAway()
    {
        var runner = Landed(ahuneri: 3);
        runner.Advance(24 * 60);
        var party = runner.Campaign.Crew.Where(c => c.Id != runner.Campaign.CaptainId && c.CanWork).Take(2).Select(c => c.Id).ToList();
        Assert.True(runner.SendParty(party, "place.mahenu", "visit").Succeeded);
        runner.DeveloperTrust("people.oruwai", 12);
        runner.Advance(5 * 60);
        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        runner.Advance(8 * 24 * 60);
        loaded.Advance(8 * 24 * 60);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
