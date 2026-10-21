using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Knowledge;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Conversations in port: topics that open, guarded answers, trust, and what an offer needs first.</summary>
public sealed class ConversationTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Port() => ExpeditionScenario.Create(Shipped, CampaignState.DefaultSeed);

    private static CharacterId Id(SimulationRunner runner, string person) => PortTests.Id(runner, person);

    private static int Trust(SimulationRunner runner, CharacterId id) =>
        runner.Campaign.Relationships.TryGetValue((id, runner.Campaign.CaptainId!.Value), out var view) ? view.Trust : 0;

    [Fact]
    public void TheFirstMeetingIsWrittenForThePersonAndTheNextIsDifferent()
    {
        var runner = Port();
        var velho = Id(runner, "person.velho");
        PortTests.ApproachPerson(runner, "person.velho");

        var (first, said) = runner.BeginConversation(velho);
        var (_, again) = runner.BeginConversation(velho);

        Assert.True(first.Succeeded);
        Assert.Contains("I wondered when you would come", said);
        Assert.NotEqual(said, again);
        Assert.Equal("topic.velho.charts", runner.Topics(velho)[0].Id);
    }

    [Fact]
    public void TopicsOpenOneFromAnother()
    {
        var runner = Port();
        var velho = Id(runner, "person.velho");
        PortTests.ApproachPerson(runner, "person.velho");
        Assert.DoesNotContain(runner.Topics(velho), t => t.Id == "topic.velho.coast");

        runner.Discuss(velho, "topic.velho.charts");

        Assert.Contains(runner.Topics(velho), t => t.Id == "topic.velho.coast");
        Assert.True(runner.Topics(velho).Single(t => t.Id == "topic.velho.charts").Discussed);
        Assert.Equal(5, runner.Campaign.Knowledge.Assess(velho, "skill.navigation")!.Level);
    }

    [Fact]
    public void AGuardedAnswerCostsTrustUntilItIsEarned()
    {
        var runner = Port();
        var velho = Id(runner, "person.velho");
        PortTests.ApproachPerson(runner, "person.velho");
        runner.Discuss(velho, "topic.velho.charts");
        runner.Discuss(velho, "topic.velho.coast");
        var before = Trust(runner, velho);

        var (_, pressed, _) = runner.Discuss(velho, "topic.velho.why");

        Assert.Contains("That is my business", pressed);
        Assert.Equal(before - 4, Trust(runner, velho));
        Assert.False(runner.Campaign.Port!.Discussed.Contains((velho, "topic.velho.why")));

        runner.Campaign.ChangeRelationship(velho, runner.Campaign.CaptainId!.Value, RelationshipDimension.Trust, 20, "test");
        var (_, answer, _) = runner.Discuss(velho, "topic.velho.why");
        Assert.Contains("I know the way", answer);
        Assert.True(runner.Campaign.Port!.Discussed.Contains((velho, "topic.velho.why")));
    }

    [Fact]
    public void WhatOthersTellOpensWhatYouCanAsk()
    {
        var runner = Port();
        var ximena = Id(runner, "person.ximena");
        PortTests.ApproachPerson(runner, "person.ximena");
        runner.Discuss(ximena, "topic.ximena.mahenu");
        Assert.DoesNotContain(runner.Topics(ximena), t => t.Id == "topic.ximena.season");

        var hernando = Id(runner, "person.hernando");
        PortTests.ApproachPerson(runner, "person.hernando");
        runner.Discuss(hernando, "topic.hernando.coast");
        runner.Discuss(hernando, "topic.hernando.ximena");

        var heard = runner.Campaign.Knowledge.Assess(ximena, "fact.ximena_season")!;
        Assert.Equal(KnowledgeSource.Reference, heard.Source);
        Assert.Contains(runner.Topics(ximena), t => t.Id == "topic.ximena.season");
        Assert.Contains("lead.hernando_ximena", runner.Campaign.Port!.Leads.Select(l => l.LeadId));
    }

    [Fact]
    public void NoOfferBeforeTheyHaveSaidWhatTheyWant()
    {
        var runner = Port();
        var gil = Id(runner, "person.gil");
        PortTests.ApproachPerson(runner, "person.gil");

        Assert.Contains("Talk with them first", runner.Offer(gil, 900).Reason);
        PortTests.HearTerms(runner, "person.gil");
        Assert.True(runner.Offer(gil, 900).Succeeded);
    }

    [Fact]
    public void SomeWillNotSignWithACaptainTheyDoNotKnow()
    {
        var runner = Port();
        var velho = Id(runner, "person.velho");
        PortTests.HearTerms(runner, "person.velho");
        Assert.True(Trust(runner, velho) < 10);

        Assert.Contains("I do not know you yet", runner.Offer(velho, 1600).Reason);

        runner.Campaign.ChangeRelationship(velho, runner.Campaign.CaptainId!.Value, RelationshipDimension.Trust, 10, "test");
        Assert.True(runner.Offer(velho, 1600).Succeeded);
    }

    [Fact]
    public void TrustBringsThePriceDown()
    {
        var runner = Port();
        var gil = Id(runner, "person.gil");
        PortTests.HearTerms(runner, "person.gil");
        Assert.False(runner.Offer(gil, 750).Succeeded);

        runner.Campaign.ChangeRelationship(gil, runner.Campaign.CaptainId!.Value, RelationshipDimension.Trust, 20, "test");
        Assert.True(runner.Offer(gil, 700).Succeeded);
    }

    [Fact]
    public void WhatWasSaidSurvivesSaveAndLoad()
    {
        var runner = Port();
        var bartolo = Id(runner, "person.bartolo");
        PortTests.HearTerms(runner, "person.bartolo");

        var loaded = new SimulationRunner(SaveSerializer.Deserialize(SaveSerializer.Serialize(runner.Campaign), Shipped), Shipped);

        Assert.True(loaded.TermsHeard(bartolo));
        Assert.Equal(runner.Campaign.Port!.Discussed.Count, loaded.Campaign.Port!.Discussed.Count);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }

    [Fact]
    public void EveryoneLookingForABerthCanBeTalkedToTheirTerms()
    {
        var seeking = Shipped.Port!.People.Where(p => p.Seeking is not null).ToList();
        Assert.All(seeking, p => Assert.Contains(p.Conversation!.Topics, t => t.Terms));
    }
}

/// <summary>The Three Anchors: a room with walls, and a round stood in it.</summary>
public sealed class TavernTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Port() => ExpeditionScenario.Create(Shipped, CampaignState.DefaultSeed);

    private static CharacterId Captain(SimulationRunner runner) => runner.Campaign.CaptainId!.Value;

    private static void WalkTo(SimulationRunner runner, int x, int y)
    {
        Assert.True(runner.MoveCharacter(Captain(runner), new World.GridPosition(x, y)).Succeeded);
        for (var minute = 0; minute < 120 && runner.Campaign.Placements[Captain(runner)].IsMoving; minute++)
        {
            runner.Advance(1);
        }
    }

    [Fact]
    public void TheCommonRoomIsARoomYouCanWalkInto()
    {
        var bocana = Shipped.Locations[Shipped.Port!.Location];
        var room = bocana.Rooms.Single(r => r.Id == "room.three_anchors");
        Assert.True(bocana.IsWalkable(new World.GridPosition(28, 13)));
        Assert.All(bocana.Spots.Where(s => s.Id.StartsWith("spot.tavern_inside", StringComparison.Ordinal)), s => Assert.Equal(room, bocana.RoomAt(s.Cell)));

        var runner = Port();
        WalkTo(runner, 28, 9);
        Assert.Equal(room, runner.CaptainRoom);
    }

    [Fact]
    public void WallsHideWhoIsInsideAndWhoIsOutside()
    {
        var runner = Port();
        WalkTo(runner, 28, 9);
        var outside = runner.Campaign.Characters.Values.Where(c => c.Membership == Membership.Local && runner.RoomOf(c.Id) is null).ToList();
        Assert.NotEmpty(outside);
        Assert.All(outside, c => Assert.False(runner.CaptainSees(c.Id)));
        Assert.All(outside, c => Assert.NotNull(runner.WhyCannotSpeak(c.Id)));
    }

    [Fact]
    public void ARoundWarmsTheRoomOnceADay()
    {
        var runner = Port();
        Assert.False(runner.BuyRound().Result.Succeeded);
        runner.Advance(11 * 60);
        WalkTo(runner, 28, 9);
        var drinking = runner.Campaign.Characters.Values.Where(c => c.PersonId is not null && runner.RoomOf(c.Id) == runner.CaptainRoom).Select(c => c.Id).ToList();
        var funds = runner.Campaign.Port!.Funds;

        var (result, said) = runner.BuyRound();

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(funds - 30, runner.Campaign.Port.Funds);
        Assert.All(drinking, id => Assert.Equal(4, runner.Campaign.Relationships[(id, Captain(runner))].Affection));
        Assert.Contains("round", said);
        Assert.False(runner.BuyRound().Result.Succeeded);

        var loaded = SaveSerializer.Deserialize(SaveSerializer.Serialize(runner.Campaign), Shipped);
        Assert.Equal(runner.Campaign.Port.LastRound, loaded.Port!.LastRound);
    }
}

/// <summary>Evenings in the Three Anchors: dice and a song, each taking its time.</summary>
public sealed class TavernEveningTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static CharacterId Captain(SimulationRunner runner) => runner.Campaign.CaptainId!.Value;

    // The captain in the common room at the given hour of the first evening, with the drinkers settled.
    private static SimulationRunner Evening(int hour)
    {
        var runner = ExpeditionScenario.Create(Shipped, CampaignState.DefaultSeed);
        var start = runner.Campaign.Clock.TimeOfDay.TotalMinutes;
        runner.Advance((int)(hour * 60 - start - 40));
        Assert.True(runner.MoveCharacter(Captain(runner), new World.GridPosition(28, 9)).Succeeded);
        runner.Advance(40);
        Assert.NotNull(runner.CaptainRoom);
        return runner;
    }

    [Fact]
    public void DiceHoldTheTableHalfAnHourAndMoveTheMoney()
    {
        var runner = Evening(20);
        var players = runner.DicePartners();
        Assert.NotEmpty(players);
        var funds = runner.Campaign.Port!.Funds;
        var (result, said) = runner.PlayDice(20);

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(players, runner.Dicing);
        Assert.Contains("still going", runner.PlayDice(20).Result.Reason);
        var change = runner.Campaign.Port.Funds - funds;
        Assert.True(change == -20 || change == 20 * players.Count, said);
        Assert.All(players, p => Assert.True(runner.Campaign.Relationships[(p, Captain(runner))].Affection > 0));
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.DicePlayed);
        runner.Advance(SimulationRunner.DiceMinutes);
        Assert.Empty(runner.Dicing);
    }

    [Fact]
    public void NoDiceInTheMorningOrForOddStakes()
    {
        var runner = Evening(10);
        Assert.Contains("evening", runner.PlayDice(20).Result.Reason);
        var evening = Evening(20);
        Assert.False(evening.PlayDice(15).Result.Succeeded);
    }

    [Fact]
    public void ASongOnceAnEveningShowsSomethingOfTheSinger()
    {
        var runner = Evening(20);
        var (result, said) = runner.CallForSong();

        Assert.True(result.Succeeded, result.Reason);
        var singer = runner.Campaign.Port!.Sung.Single();
        Assert.Equal(singer, runner.Singing);
        runner.Advance(SimulationRunner.SongMinutes);
        Assert.Null(runner.Singing);
        Assert.Contains(runner.Campaign.Characters[singer].Name.Split(' ')[0], said);
        Assert.Equal(5, runner.Campaign.Relationships[(singer, Captain(runner))].Affection);
        Assert.Contains("already", runner.CallForSong().Result.Reason);

        var loaded = SaveSerializer.Deserialize(SaveSerializer.Serialize(runner.Campaign), Shipped);
        Assert.Equal(runner.Campaign.Port.Sung, loaded.Port!.Sung);
        Assert.Equal(runner.Campaign.Port.LastSong, loaded.Port.LastSong);
    }

    [Fact]
    public void HearingXimenaSingShowsHerTongue()
    {
        var song = Shipped.Port!.People.Single(p => p.Id == "person.ximena").Song!;
        Assert.Equal(4, song.Shows["language.ahuneri"]);
    }

    [Fact]
    public void TheConvoyGivesFourEveningsInPort()
    {
        Assert.Equal(5, Shipped.Port!.Departure.Day);
    }
}

/// <summary>The chandlery and the Chamber: rooms like the tavern, with their people at work inside.</summary>
public sealed class ShopAndChamberTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static CharacterId Captain(SimulationRunner runner) => runner.Campaign.CaptainId!.Value;

    private static SimulationRunner AtTen()
    {
        var runner = ExpeditionScenario.Create(Shipped, CampaignState.DefaultSeed);
        runner.Advance((int)(10 * 60 - runner.Campaign.Clock.TimeOfDay.TotalMinutes));
        return runner;
    }

    private static void WalkTo(SimulationRunner runner, int x, int y)
    {
        Assert.True(runner.MoveCharacter(Captain(runner), new World.GridPosition(x, y)).Succeeded);
        for (var minute = 0; minute < 120 && runner.Campaign.Placements[Captain(runner)].IsMoving; minute++)
        {
            runner.Advance(1);
        }
    }

    [Theory]
    [InlineData("person.corbalan", "room.chandlery", 50, 10)]
    [InlineData("person.clerk_chamber", "room.chamber", 4, 9)]
    [InlineData("person.varzi", "room.counting", 13, 9)]
    public void TheyWorkInsideAndAreSeenOnlyFromInside(string person, string room, int x, int y)
    {
        var runner = AtTen();
        var who = runner.CharacterFor(person)!.Id;
        Assert.Equal(room, runner.RoomOf(who)?.Id);
        Assert.False(runner.IsIndoors(who));
        Assert.Contains("Go in", runner.WhyCannotSpeak(who) ?? "");

        WalkTo(runner, x, y);
        Assert.Equal(room, runner.CaptainRoom?.Id);
        Assert.True(runner.CaptainSees(who));
        Assert.Null(runner.WhyCannotSpeak(who));
    }

    [Fact]
    public void TheChandlersGoodsAreJudgedInHisShop()
    {
        var runner = AtTen();
        var corbalan = Shipped.Port!.Sellers.Single(s => s.Id == "seller.corbalan");
        WalkTo(runner, 50, 15);
        Assert.Contains("Go in", runner.WhyCannotInspect(corbalan) ?? "");
        WalkTo(runner, 51, 11);
        Assert.Null(runner.WhyCannotInspect(corbalan));
    }
}
