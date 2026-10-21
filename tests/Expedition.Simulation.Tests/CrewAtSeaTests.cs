using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Knowledge;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>The crew at sea: claims tested by work, and events written for the people who sailed.</summary>
public sealed class CrewAtSeaTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static CharacterId Id(SimulationRunner runner, string person) => runner.CharacterFor(person)!.Id;

    // Hires these people (hearing their claims first) and sails.
    private static SimulationRunner AtSea(params string[] people)
    {
        var runner = ExpeditionScenario.Create(Shipped);
        foreach (var person in people)
        {
            var id = Id(runner, person);
            foreach (var (topic, level) in Shipped.Port!.People.Single(p => p.Id == person).Seeking!.Claims)
            {
                runner.Campaign.Learn(id, topic, level, KnowledgeSource.Claim);
            }

            Assert.True(runner.DeveloperHire(id).Succeeded, person);
        }

        Assert.True(ExpeditionScenario.SetSail(runner).Succeeded);
        return runner;
    }

    [Fact]
    public void AWatchKeptAtSeaShowsWhoIsASeamanAndTheCrewNotice()
    {
        var runner = AtSea("person.tome", "person.rodrigo", "person.gil", "person.wat");
        var tome = Id(runner, "person.tome");
        var rodrigo = Id(runner, "person.rodrigo");
        var wat = Id(runner, "person.wat");
        foreach (var id in new[] { tome, rodrigo })
        {
            var assigned = runner.AssignTask(id, "task.stand_watch");
            Assert.True(assigned.Succeeded, assigned.ToString());
        }

        runner.Advance(5 * 60);

        var tested = runner.Campaign.Chronicle.Entries.Where(e => e.Kind == ChronicleEntryKind.ClaimTested).ToList();
        // Tomé swore he had never sailed; Rodrigo claimed more than he has. Wat claimed what he has.
        Assert.Contains(tested, e => e.Characters[0] == tome && e.Get(ChronicleParameters.Need) == "skill.seamanship" && e.Get(ChronicleParameters.Level) == "3");
        Assert.Contains(tested, e => e.Characters[0] == rodrigo && e.Get(ChronicleParameters.Need) == "skill.seamanship" && e.Get(ChronicleParameters.Level) == "1");
        Assert.DoesNotContain(tested, e => e.Characters[0] == wat);
        Assert.Equal(KnowledgeSource.Observation, runner.Campaign.Knowledge.Assess(tome, "skill.seamanship")!.Source);
        Assert.True(runner.Campaign.Relationships[(wat, tome)].Respect > 0);
        Assert.True(runner.Campaign.Relationships[(wat, rodrigo)].Respect < 0);

        var formatter = new ChronicleFormatter(runner.Campaign, Shipped);
        var lines = tested.Select(formatter.Format).ToList();
        Assert.Contains(lines, l => l.Contains("better at seamanship than you were told", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("shows for what it is", StringComparison.Ordinal));

        // Only the first sight is remarked on, however many watches follow.
        runner.Advance(2 * 24 * 60);
        Assert.Single(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.ClaimTested && e.Characters[0] == tome && e.Get(ChronicleParameters.Need) == "skill.seamanship");
    }

    [Fact]
    public void PeopleEventsFireOnlyForThoseWhoSailed()
    {
        var runner = AtSea("person.tome", "person.gil", "person.pascual", "person.wat");
        Assert.False(runner.DeveloperRaiseEvent("event.velho_letters").Succeeded);
        Assert.True(runner.DeveloperRaiseEvent("event.pascual_aloft").Succeeded);
        Assert.Equal(Id(runner, "person.pascual"), runner.Campaign.PendingEvent!.Subject);
    }

    [Fact]
    public void WhatTheCaptainWasToldInPortOpensChoicesAtSea()
    {
        var runner = AtSea("person.tome", "person.gil", "person.pascual", "person.wat");
        Assert.True(runner.DeveloperRaiseEvent("event.tome_seaman").Succeeded);
        Assert.DoesNotContain(runner.OfferedChoices(), c => c.Choice.Id == "caridad");
        runner.ResolveEvent("where");

        var told = AtSea("person.tome", "person.gil", "person.pascual", "person.wat");
        told.Campaign.Learn(Id(told, "person.tome"), "fact.tome_sailor", 1, KnowledgeSource.Reference);
        Assert.True(told.DeveloperRaiseEvent("event.tome_seaman").Succeeded);
        Assert.Contains(told.OfferedChoices(), c => c.Choice.Id == "caridad");
        Assert.True(told.ResolveEvent("caridad").Result.Succeeded);
        Assert.NotNull(told.Campaign.Knowledge.Assess(Id(told, "person.tome"), "fact.tome_master"));
    }

    [Fact]
    public void AnEventCanShowTheTruthOfAClaim()
    {
        var runner = AtSea("person.tome", "person.gil", "person.pascual", "person.wat");
        var pascual = Id(runner, "person.pascual");
        Assert.True(runner.DeveloperRaiseEvent("event.pascual_aloft").Succeeded);
        Assert.True(runner.ResolveEvent("order").Result.Succeeded);

        Assert.Equal(new Assessment(0, KnowledgeSource.Observation, true), runner.Campaign.Knowledge.Assess(pascual, "skill.rigging"));
        Assert.Equal(1, runner.Campaign.Knowledge.Assess(pascual, "fact.pascual_never_sailed")!.Level);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.ClaimTested && e.Characters[0] == pascual);
    }

    [Fact]
    public void TheCoastEventsWaitForTheCoast()
    {
        var runner = AtSea("person.velho", "person.gil", "person.tome", "person.wat");
        runner.Advance(3 * 24 * 60);
        Assert.DoesNotContain(runner.Campaign.EventHistory.Keys, k => k == "event.velho_bar");
        Assert.Null(Shipped.Events["event.velho_letters"].Trigger.LeaguesBelow);
        Assert.Equal(80, Shipped.Events["event.velho_bar"].Trigger.LeaguesBelow);
    }
}
