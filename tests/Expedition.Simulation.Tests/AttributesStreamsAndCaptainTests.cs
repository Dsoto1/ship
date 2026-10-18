using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class AttributesStreamsAndCaptainTests
{
    private const int Hour = SimulationClock.TicksPerHour;

    private static GameContent Shipped() => ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    // --- Attributes -------------------------------------------------------------------------

    [Fact]
    public void UnrecordedAttributesAreOrdinaryNotZero()
    {
        var character = new Character(new CharacterId(1), "Pedro", 19, "Huelva");

        Assert.Equal(Attributes.Ordinary, character.Attributes.Get("attribute.strength"));
        Assert.Equal(0, character.Skills.Get("skill.rigging"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void AttributeLevelsOutOfRangeAreRejected(int level)
    {
        var character = new Character(new CharacterId(1), "Pedro", 19, "Huelva");

        Assert.Throws<ArgumentOutOfRangeException>(() => character.Attributes.Set("attribute.strength", level));
    }

    [Theory]
    [InlineData(0, 120)] // 0.5x speed: 60 work-minutes take 120
    [InlineData(2, 60)]
    [InlineData(4, 40)]  // 1.5x speed
    public void AttributeChangesWorkSpeedIndependentlyOfSkill(int strength, int expectedMinutes)
    {
        var shortHaul = TestContent.Haul with { DurationMinutes = 60, NeedsPerHour = NeedRates.None };
        var content = new GameContent(
            TestContent.Rules, [], TestContent.Items, [shortHaul], [],
            [new AttributeDefinition { Id = "attribute.strength", Name = "Strength" }]);
        var campaign = new CampaignState(CampaignPhase.Voyage);
        var runner = new SimulationRunner(campaign, content);
        var hauler = campaign.CreateCharacter("Pedro", 19, "Huelva");
        hauler.Attributes.Set("attribute.strength", strength);

        runner.AssignTask(hauler.Id, "task.haul");
        var outcome = Assert.Single(runner.Advance(200));

        Assert.Equal(TaskEnd.Completed, outcome.End);
        Assert.Equal(expectedMinutes, outcome.Tick);
    }

    [Fact]
    public void ShippedDataSeparatesAttributesFromSkills()
    {
        var content = Shipped();

        Assert.Equal(4, content.Attributes.Count);
        Assert.DoesNotContain(content.Skills.Keys, id => id is "skill.strength" or "skill.endurance" or "skill.coordination" or "skill.perception");
        Assert.Equal("attribute.strength", content.Tasks["task.pump_bilge"].Attribute);
    }

    [Fact]
    public void TaskNamingUnknownAttributeIsRejected()
    {
        var bad = TestContent.Pump with { Attribute = "attribute.charisma" };

        var ex = Assert.Throws<ContentValidationException>(() => new GameContent(TestContent.Rules, [], TestContent.Items, [bad]));

        Assert.Contains(ex.Errors, e => e.Contains("unknown attribute 'attribute.charisma'"));
    }

    // --- Random streams ---------------------------------------------------------------------

    [Fact]
    public void StreamsFromTheSameSeedDifferFromEachOther()
    {
        var streams = new RandomStreams(123);

        var firsts = new[] { streams.Tasks.NextUInt(), streams.Characters.NextUInt(), streams.Weather.NextUInt(), streams.Events.NextUInt() };

        Assert.Equal(4, firsts.Distinct().Count());
    }

    [Fact]
    public void UsingOneStreamDoesNotChangeAnother()
    {
        // Two identical storms; in one, a thousand weather rolls happen alongside.
        var quiet = StormInjuries(rollWeather: false);
        var stormy = StormInjuries(rollWeather: true);

        Assert.Equal(quiet, stormy);
        Assert.NotEmpty(quiet);
    }

    [Fact]
    public void StreamStateRoundTripsAndRejectsMissingStreams()
    {
        var streams = new RandomStreams(5);
        streams.Weather.NextUInt();
        var state = streams.SaveState();

        var restored = RandomStreams.FromState(state);

        Assert.Equal(streams.Weather.NextUInt(), restored.Weather.NextUInt());
        var missing = state.Where(p => p.Key != RandomStreams.EventsName).ToDictionary();
        Assert.Throws<ArgumentException>(() => RandomStreams.FromState(missing));
    }

    private static List<long> StormInjuries(bool rollWeather)
    {
        var campaign = new CampaignState(CampaignPhase.Voyage, seed: 31);
        var runner = new SimulationRunner(campaign, TestContent.Create());
        var crew = Enumerable.Range(1, 4).Select(i => campaign.CreateCharacter($"Sailor {i}", 20 + i, "Palos")).ToList();
        foreach (var sailor in crew)
        {
            runner.AssignTask(sailor.Id, "task.storm_work");
        }

        for (var minute = 0; minute < 6 * Hour; minute++)
        {
            if (rollWeather)
            {
                campaign.Randomness.Weather.NextDouble();
            }

            runner.Advance(1);
        }

        return campaign.Chronicle.Entries.Where(e => e.Kind == ChronicleEntryKind.Injury).Select(e => e.Tick).ToList();
    }

    // --- Campaign identity and captain ------------------------------------------------------

    [Fact]
    public void CampaignKeepsItsSeedAndId()
    {
        var derived = new CampaignState(seed: 0xABCDEF);
        var named = new CampaignState(seed: 1, campaignId: "campaign-test");

        Assert.Equal(0xABCDEFUL, derived.Seed);
        Assert.Equal("campaign-0000000000abcdef", derived.CampaignId);
        Assert.Equal("campaign-test", named.CampaignId);
    }

    [Fact]
    public void AppointingACaptainIsRecorded()
    {
        var campaign = new CampaignState();
        var alvaro = campaign.CreateCharacter("Álvaro", 41, "Seville");

        Assert.True(campaign.AppointCaptain(alvaro.Id, "named in the charter").Succeeded);

        Assert.Equal(alvaro.Id, campaign.CaptainId);
        var entry = Assert.Single(campaign.Chronicle.Entries);
        Assert.Equal(ChronicleEntryKind.CaptainAppointed, entry.Kind);
        Assert.Equal("Álvaro took command of the expedition (named in the charter).",
            new ChronicleFormatter(campaign, TestContent.Create()).Format(entry));
    }

    [Fact]
    public void TheDeadAndTheUnknownCannotBeCaptain()
    {
        var campaign = new CampaignState();
        var dead = campaign.CreateCharacter("Juan", 52, "Moguer");
        campaign.ChangeSurvivalState(dead.Id, SurvivalState.Dead, "fever");

        Assert.False(campaign.AppointCaptain(dead.Id, "test").Succeeded);
        Assert.False(campaign.AppointCaptain(new CharacterId(99), "test").Succeeded);
        Assert.Null(campaign.CaptainId);
    }

    [Fact]
    public void ScenarioHasACaptainAndVariedPhysiques()
    {
        var campaign = TestDeckScenario.Create(Shipped()).Campaign;

        Assert.Equal("Captain Álvaro", campaign.GetCharacter(campaign.CaptainId!.Value).Name);
        Assert.Contains(campaign.Characters.Values, c => c.Attributes.Get("attribute.strength") > Attributes.Ordinary);
    }

    // --- Saves ------------------------------------------------------------------------------

    [Fact]
    public void SeedIdCaptainAttributesAndStreamsAreSaved()
    {
        var runner = TestDeckScenario.Create(Shipped(), seed: 77);
        runner.Campaign.Randomness.Weather.NextUInt();
        var json = SaveSerializer.Serialize(runner.Campaign);

        var loaded = SaveSerializer.Deserialize(json, Shipped());

        Assert.Equal(json, SaveSerializer.Serialize(loaded));
        Assert.Equal(77UL, loaded.Seed);
        Assert.Equal(runner.Campaign.CaptainId, loaded.CaptainId);
        Assert.Equal(4, loaded.Characters.Values.Single(c => c.Name == "Pedro").Attributes.Get("attribute.strength"));
        Assert.Equal(runner.Campaign.Randomness.SaveState(), loaded.Randomness.SaveState());
    }

    [Fact]
    public void FormatThreeSaveUpgradesPhysicalSkillsAndKeepsTheInjurySequence()
    {
        // A format-3 file in which Pedro's strength was still a "skill".
        var runner = TestDeckScenario.Create(Shipped(), seed: 5);
        var current = SaveSerializer.Serialize(runner.Campaign);
        var formatThree = JsonNode.Parse(current)!.AsObject();
        SaveLoadTests.ToFormat3(formatThree);
        var pedro = formatThree["characters"]!.AsArray().Single(c => c!["name"]!.GetValue<string>() == "Pedro")!;
        pedro["skills"]!["skill.strength"] = 4;

        var upgraded = SaveSerializer.Deserialize(formatThree.ToJsonString(), Shipped());

        var upgradedPedro = upgraded.Characters.Values.Single(c => c.Name == "Pedro");
        Assert.Equal(4, upgradedPedro.Attributes.Get("attribute.strength"));
        Assert.DoesNotContain("skill.strength", upgradedPedro.Skills.Levels.Keys);
        Assert.Null(upgraded.CaptainId);
        Assert.StartsWith("campaign-upgraded-", upgraded.CampaignId);
        // The old single generator carries on as the tasks stream, so injury rolls continue unchanged.
        Assert.Equal(runner.Campaign.Randomness.Tasks.SaveState(), upgraded.Randomness.Tasks.SaveState());
    }

    [Theory]
    [InlineData("unknown skill")]
    [InlineData("unknown attribute")]
    [InlineData("unknown captain")]
    [InlineData("missing stream")]
    public void TamperedIdentityDataIsRejected(string tampering)
    {
        var root = JsonNode.Parse(SaveSerializer.Serialize(TestDeckScenario.Create(Shipped()).Campaign))!.AsObject();
        var character = root["characters"]!.AsArray()[0]!;
        switch (tampering)
        {
            case "unknown skill": character["skills"]!["skill.juggling"] = 3; break;
            case "unknown attribute": character["attributes"]!["attribute.charm"] = 3; break;
            case "unknown captain": root["captainId"] = 99; break;
            case "missing stream": root["randomStreams"]!.AsObject().Remove("weather"); break;
        }

        Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(root.ToJsonString(), Shipped()));
    }
}
