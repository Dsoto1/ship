using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
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
