using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Knowledge;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class RecruitmentTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Port(ulong seed = CampaignState.DefaultSeed) => ExpeditionScenario.Create(Shipped, seed);

    [Fact]
    public void TheCampaignStartsInPortWithTheCaptainAlone()
    {
        var runner = Port();

        Assert.Equal(CampaignPhase.PortPreparation, runner.Campaign.Phase);
        Assert.Single(runner.Campaign.Characters);
        Assert.Null(runner.Campaign.Voyage);
        Assert.Equal(7000, runner.Campaign.Recruitment!.Funds);
        Assert.Equal(12, runner.AvailableCandidates().Count);
    }

    [Fact]
    public void TalkingIsFreeAndRevealsOnlyClaims()
    {
        var runner = Port();

        Assert.True(runner.TalkTo("candidate.boy").Succeeded);

        var state = runner.Campaign.Recruitment!;
        Assert.Equal(6, state.InquiriesLeft);
        Assert.Contains(new Recruitment.CandidateFact("candidate.boy", "skill.sailing", 3, KnowledgeSource.Claim), state.Facts);
        Assert.DoesNotContain(state.Facts, f => f.Source != KnowledgeSource.Claim);
    }

    [Fact]
    public void AskingAroundCostsTimeAndGivesAReference()
    {
        var runner = Port();

        Assert.True(runner.AskAbout("candidate.boy").Succeeded);
        Assert.False(runner.AskAbout("candidate.boy").Succeeded);

        var state = runner.Campaign.Recruitment!;
        Assert.Equal(5, state.InquiriesLeft);
        Assert.Contains(new Recruitment.CandidateFact("candidate.boy", "skill.sailing", 0, KnowledgeSource.Reference), state.Facts);
    }

    [Fact]
    public void ATestShowsTheTruthButTheProudRefuse()
    {
        var runner = Port();

        Assert.True(runner.TestCandidate("candidate.boy", "skill.sailing").Succeeded);
        Assert.Contains(new Recruitment.CandidateFact("candidate.boy", "skill.sailing", 1, KnowledgeSource.Test), runner.Campaign.Recruitment!.Facts);

        var pilot = runner.TestCandidate("candidate.pilot", "skill.navigation");
        Assert.False(pilot.Succeeded);
        Assert.Contains("refuses", pilot.Reason);
        Assert.Equal(4, runner.Campaign.Recruitment.InquiriesLeft);
    }

    [Fact]
    public void InquiriesRunOut()
    {
        var runner = Port();
        var candidates = runner.AvailableCandidates().Select(c => c.Id).ToList();
        for (var i = 0; i < 6; i++)
        {
            Assert.True(runner.AskAbout(candidates[i]).Succeeded);
        }

        Assert.False(runner.AskAbout(candidates[6]).Succeeded);
        Assert.False(runner.TestCandidate(candidates[6], "skill.sailing").Succeeded);
        Assert.True(runner.TalkTo(candidates[6]).Succeeded);
    }

    [Fact]
    public void HagglingEitherLowersOrRaisesThePriceAndTheProudMayLeave()
    {
        var outcomes = new HashSet<string>();
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var runner = Port(seed);
            var result = runner.Haggle("candidate.rigger");
            var price = runner.AskingPrice(Shipped.Recruitment!.Candidates.Single(c => c.Id == "candidate.rigger"));
            Assert.Equal(result.Succeeded ? 680 : 940, price);
            outcomes.Add(result.Succeeded ? "lower" : "higher");
            Assert.False(runner.Haggle("candidate.rigger").Succeeded);

            if (!runner.Haggle("candidate.pilot").Succeeded && runner.Campaign.Recruitment!.Gone.Contains("candidate.pilot"))
            {
                outcomes.Add("gone");
            }
        }

        Assert.Equal(["gone", "higher", "lower"], outcomes.Order());
    }

    [Fact]
    public void HiringPaysTheAdvanceAndBringsThemAboardAsTheyReallyAre()
    {
        var runner = Port();
        runner.TalkTo("candidate.convict");

        Assert.True(runner.Hire("candidate.convict").Succeeded);

        var state = runner.Campaign.Recruitment!;
        Assert.Equal(7000 - 150, state.Funds);
        var tome = runner.Campaign.Characters[state.Hired["candidate.convict"]];
        Assert.Equal(3, tome.Skills.Get("skill.sailing"));
        Assert.Equal(Sex.Male, tome.Sex);
        Assert.True(runner.Campaign.Placements.ContainsKey(tome.Id));
        Assert.Contains("task.repair_rigging", runner.Campaign.DutiesOf(tome.Id));
        // What the captain believes is what he was told, not the truth.
        Assert.Equal(new Assessment(0, KnowledgeSource.Claim, false), runner.Campaign.Knowledge.Assess(tome.Id, "skill.sailing"));
        Assert.False(runner.Hire("candidate.convict").Succeeded);
        Assert.DoesNotContain(runner.AvailableCandidates(), c => c.Id == "candidate.convict");
    }

    [Fact]
    public void TheGentlemanPaysForHisPassage()
    {
        var runner = Port();

        runner.Hire("candidate.gentleman");

        Assert.Equal(7400, runner.Campaign.Recruitment!.Funds);
    }

    [Fact]
    public void BerthsAndTheBudgetAreLimited()
    {
        var runner = Port();
        foreach (var id in new[] { "candidate.pilot", "candidate.surgeon", "candidate.carpenter", "candidate.interpreter" })
        {
            Assert.True(runner.Hire(id).Succeeded);
        }

        // 7000 - 1600 - 1300 - 1100 - 1200 = 1800 left.
        Assert.True(runner.Hire("candidate.cooper").Succeeded);
        Assert.True(runner.Hire("candidate.rigger").Succeeded);
        Assert.False(runner.Hire("candidate.brennish").Succeeded); // 50 left, 700 asked
        Assert.True(runner.Hire("candidate.gentleman").Succeeded);
        Assert.False(runner.Hire("candidate.convict").Succeeded); // all seven berths taken
    }

    [Fact]
    public void TheChandlerSellsStoresForTheSamePurse()
    {
        var runner = Port();
        var water = runner.Campaign.SharedStore.Count("item.water");

        Assert.True(runner.Buy(0).Succeeded);

        Assert.Equal(water + 20, runner.Campaign.SharedStore.Count("item.water"));
        Assert.Equal(6940, runner.Campaign.Recruitment!.Funds);
        Assert.False(runner.Buy(99).Succeeded);
    }

    [Fact]
    public void TheShipSailsOnlyWithEnoughHands()
    {
        var runner = Port();
        runner.Hire("candidate.cook");
        Assert.False(ExpeditionScenario.SetSail(runner).Succeeded);

        foreach (var id in new[] { "candidate.convict", "candidate.boy", "candidate.brennish" })
        {
            runner.Hire(id);
        }

        Assert.True(ExpeditionScenario.SetSail(runner).Succeeded);
        Assert.Equal(CampaignPhase.Voyage, runner.Campaign.Phase);
        Assert.NotNull(runner.Campaign.Voyage);
        Assert.Null(runner.Campaign.Recruitment);
        Assert.False(runner.Hire("candidate.surgeon").Succeeded);
        Assert.False(runner.Buy(0).Succeeded);
    }

    [Fact]
    public void WhatTheyClaimedIsFoundOutAtWork()
    {
        var runner = Port(5);
        runner.TalkTo("candidate.rigger");
        foreach (var id in new[] { "candidate.rigger", "candidate.cook", "candidate.convict", "candidate.carpenter" })
        {
            runner.Hire(id);
        }

        var rodrigo = runner.Campaign.Recruitment!.Hired["candidate.rigger"];
        ExpeditionScenario.SetSail(runner);
        Assert.Equal(4, runner.Campaign.Knowledge.Assess(rodrigo, "skill.rigging")!.Level);

        Assert.True(runner.AssignTask(rodrigo, "task.repair_rigging").Succeeded);
        runner.Advance(4 * 60);

        Assert.Equal(new Assessment(2, KnowledgeSource.Observation, true), runner.Campaign.Knowledge.Assess(rodrigo, "skill.rigging"));
    }

    [Theory]
    [InlineData(true, 5, 8)]
    [InlineData(false, 0, 1)]
    public void AWellFoundShipUsuallyArrivesAndAnUnprovisionedOneDoesNot(bool buyStores, int least, int most)
    {
        var arrived = 0;
        foreach (var seed in Enumerable.Range(1, 8))
        {
            var runner = Port((ulong)seed);
            foreach (var id in new[] { "candidate.surgeon", "candidate.carpenter", "candidate.rigger", "candidate.convict", "candidate.cook", "candidate.brennish", "candidate.boy" })
            {
                Assert.True(runner.Hire(id).Succeeded);
            }

            // Ten casks of water, biscuit and pork, and spare oakum, planks and rope.
            foreach (var lot in buyStores ? new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 5, 5, 5, 6, 6, 6, 4, 4, 1, 1, 1, 1, 1, 2, 2, 2 } : [])
            {
                Assert.True(runner.Buy(lot).Succeeded);
            }

            Assert.True(ExpeditionScenario.SetSail(runner).Succeeded);
            var ship = runner.Campaign.Voyage!;
            var choices = new Random(seed);
            while (ship.Outcome == Voyage.VoyageOutcome.UnderWay && runner.Campaign.Clock.Day < 50)
            {
                // A sensible captain: heave to in storms, press on in fair weather.
                runner.SetSailPlan(ship.WeatherId == "weather.storm" ? Voyage.SailPlan.HoveTo
                    : ship.WeatherId is "weather.fair" or "weather.calm" ? Voyage.SailPlan.PressOn : Voyage.SailPlan.Normal);
                runner.Advance(60);
                if (runner.Campaign.PendingEvent is not null)
                {
                    var offered = runner.OfferedChoices().Where(c => c.Available).ToList();
                    runner.ResolveEvent(offered[choices.Next(offered.Count)].Choice.Id);
                }
            }

            arrived += ship.Outcome == Voyage.VoyageOutcome.Arrived ? 1 : 0;
        }

        Assert.InRange(arrived, least, most);
    }

    [Fact]
    public void HiringInProgressSurvivesSaveAndLoad()
    {
        var runner = Port();
        runner.TalkTo("candidate.pilot");
        runner.AskAbout("candidate.pilot");
        runner.TestCandidate("candidate.pilot", "skill.navigation");
        runner.Haggle("candidate.cook");
        runner.Hire("candidate.carpenter");
        runner.Buy(1);
        var json = SaveSerializer.Serialize(runner.Campaign);

        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);

        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.Equal(runner.Campaign.Recruitment!.Funds, loaded.Campaign.Recruitment!.Funds);
        Assert.True(loaded.Hire("candidate.surgeon").Succeeded);
    }

    [Fact]
    public void FormatThirteenSaveHasNoHiring()
    {
        var root = JsonNode.Parse(SaveSerializer.Serialize(TestDeckScenario.Create(Shipped).Campaign))!.AsObject();
        root["schemaVersion"] = 13;
        root.Remove("recruitment");

        Assert.Null(SaveSerializer.Deserialize(root.ToJsonString(), Shipped).Recruitment);
    }

    [Theory]
    [InlineData("unknown candidate")]
    [InlineData("unknown crew")]
    [InlineData("bad level")]
    public void TamperedHiringIsRejected(string tampering)
    {
        var runner = Port();
        runner.TalkTo("candidate.cook");
        runner.Hire("candidate.cook");
        var root = JsonNode.Parse(SaveSerializer.Serialize(runner.Campaign))!.AsObject();
        var hiring = root["recruitment"]!;
        switch (tampering)
        {
            case "unknown candidate": hiring["gone"]!.AsArray().Add("candidate.kraken"); break;
            case "unknown crew": hiring["hired"]!["candidate.cook"] = 999; break;
            case "bad level": hiring["facts"]!.AsArray()[0]!["level"] = 9; break;
        }

        Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(root.ToJsonString(), Shipped));
    }
}
