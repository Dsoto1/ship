using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.Knowledge;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class KnowledgeTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static (CampaignState Campaign, CharacterId Subject, CharacterId Informant) Setup()
    {
        var campaign = TestDeckScenario.Create(Shipped).Campaign;
        var crew = campaign.Characters.Keys.Order().ToList();
        return (campaign, crew[1], crew[2]);
    }

    [Fact]
    public void NothingIsKnownAtFirst()
    {
        var (campaign, subject, _) = Setup();

        Assert.Empty(campaign.Knowledge.Entries);
        Assert.Null(campaign.Knowledge.Assess(subject, "skill.rigging"));
    }

    [Fact]
    public void AClaimIsBelievedUntilBetterEvidenceArrives()
    {
        var (campaign, subject, _) = Setup();
        campaign.Learn(subject, "skill.navigation", 4, KnowledgeSource.Claim);

        Assert.Equal(new Assessment(4, KnowledgeSource.Claim, false), campaign.Knowledge.Assess(subject, "skill.navigation"));

        campaign.Learn(subject, "skill.navigation", 1, KnowledgeSource.Test);

        Assert.Equal(new Assessment(1, KnowledgeSource.Test, true), campaign.Knowledge.Assess(subject, "skill.navigation"));
    }

    [Fact]
    public void AWeakerSourceNeverOverridesAStrongerOne()
    {
        var (campaign, subject, informant) = Setup();
        campaign.Learn(subject, "skill.rigging", 3, KnowledgeSource.Observation);
        new SimulationRunner(campaign, Shipped).Advance(60);
        campaign.Learn(subject, "skill.rigging", 5, KnowledgeSource.Reference, informant);

        var assessment = campaign.Knowledge.Assess(subject, "skill.rigging")!;

        Assert.Equal(3, assessment.Level);
        Assert.True(assessment.Conflicting);
    }

    [Fact]
    public void AmongEqualSourcesTheNewestWins()
    {
        var (campaign, subject, informant) = Setup();
        campaign.Learn(subject, "skill.rigging", 2, KnowledgeSource.Reference, informant);
        new SimulationRunner(campaign, Shipped).Advance(60);
        campaign.Learn(subject, "skill.rigging", 4, KnowledgeSource.Reference, subject);

        Assert.Equal(4, campaign.Knowledge.Assess(subject, "skill.rigging")!.Level);
    }

    [Fact]
    public void RepeatedFactsAreNotRecordedTwice()
    {
        var (campaign, subject, _) = Setup();

        Assert.True(campaign.Learn(subject, "skill.rigging", 3, KnowledgeSource.Observation));
        new SimulationRunner(campaign, Shipped).Advance(600);
        Assert.False(campaign.Learn(subject, "skill.rigging", 3, KnowledgeSource.Observation));
        Assert.Single(campaign.Knowledge.Entries);
    }

    [Fact]
    public void KnowledgeNeverChangesTheTruth()
    {
        var (campaign, subject, _) = Setup();
        var truth = campaign.Characters[subject].Skills.Get("skill.rigging");

        campaign.Learn(subject, "skill.rigging", 0, KnowledgeSource.Rumour);

        Assert.Equal(truth, campaign.Characters[subject].Skills.Get("skill.rigging"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void LevelsOutsideTheScaleAreRejected(int level)
    {
        var (campaign, subject, _) = Setup();

        Assert.Throws<ArgumentOutOfRangeException>(() => campaign.Learn(subject, "skill.rigging", level, KnowledgeSource.Claim));
    }

    [Fact]
    public void KnowledgeAboutStrangersIsRejected()
    {
        var (campaign, _, _) = Setup();

        Assert.Throws<KeyNotFoundException>(() => campaign.Learn(new CharacterId(999), "skill.rigging", 1, KnowledgeSource.Claim));
    }

    [Fact]
    public void FinishedWorkRevealsTheRealSkillAndAttribute()
    {
        var runner = TestDeckScenario.Create(Shipped);
        var rigger = runner.Campaign.Characters.Values.OrderBy(c => c.Id).ElementAt(1);
        Assert.True(runner.AssignTask(rigger.Id, "task.repair_rigging").Succeeded);

        runner.Advance(600);

        var skill = runner.Campaign.Knowledge.Assess(rigger.Id, "skill.rigging");
        var attribute = runner.Campaign.Knowledge.Assess(rigger.Id, "attribute.coordination");
        Assert.Equal(new Assessment(rigger.Skills.Get("skill.rigging"), KnowledgeSource.Observation, false), skill);
        Assert.Equal(rigger.Attributes.Get("attribute.coordination"), attribute!.Level);
    }

    [Fact]
    public void KnowledgeSurvivesSaveAndLoad()
    {
        var (campaign, subject, informant) = Setup();
        campaign.Learn(subject, "skill.rigging", 5, KnowledgeSource.Reference, informant);
        new SimulationRunner(campaign, Shipped).Advance(30);
        campaign.Learn(subject, "trait.loyal", 1, KnowledgeSource.Rumour);
        var json = SaveSerializer.Serialize(campaign);

        var loaded = SaveSerializer.Deserialize(json, Shipped);

        Assert.Equal(campaign.Knowledge.Entries, loaded.Knowledge.Entries);
        Assert.Equal(json, SaveSerializer.Serialize(loaded));
    }

    [Fact]
    public void FormatEightSaveUpgradesKnowingNothing()
    {
        var root = JsonNode.Parse(SaveSerializer.Serialize(Setup().Campaign))!.AsObject();
        root["schemaVersion"] = 8;
        root.Remove("knowledge");

        Assert.Empty(SaveSerializer.Deserialize(root.ToJsonString(), Shipped).Knowledge.Entries);
    }

    [Theory]
    [InlineData("unknown subject")]
    [InlineData("unknown informant")]
    [InlineData("bad level")]
    [InlineData("future tick")]
    [InlineData("bad source")]
    public void TamperedKnowledgeIsRejected(string tampering)
    {
        var (campaign, subject, informant) = Setup();
        campaign.Learn(subject, "skill.rigging", 2, KnowledgeSource.Reference, informant);
        var root = JsonNode.Parse(SaveSerializer.Serialize(campaign))!.AsObject();
        var entry = root["knowledge"]!.AsArray()[0]!;
        switch (tampering)
        {
            case "unknown subject": entry["subject"] = 999; break;
            case "unknown informant": entry["informant"] = 999; break;
            case "bad level": entry["level"] = 9; break;
            case "future tick": entry["tick"] = 1_000_000; break;
            case "bad source": entry["source"] = "Prophecy"; break;
        }

        Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(root.ToJsonString(), Shipped));
    }
}

public class KnowledgeTextTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    [Fact]
    public void DescribesBeliefsNotTruth()
    {
        var campaign = TestDeckScenario.Create(Shipped).Campaign;
        var subject = campaign.Characters.Keys.Order().ElementAt(1);
        campaign.Learn(subject, "skill.rigging", 0, KnowledgeSource.Claim);
        campaign.Learn(subject, "skill.rigging", 1, KnowledgeSource.Rumour);
        campaign.Learn(subject, "trait.loyal", 1, KnowledgeSource.Rumour);

        var lines = History.KnowledgeText.Describe(campaign.Knowledge, subject, Shipped);

        Assert.Equal(["Rigging 0 (says so; disputed)", "Loyal (rumour)"], lines);
    }
}
