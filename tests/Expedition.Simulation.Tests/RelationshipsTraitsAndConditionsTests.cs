using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;

namespace Expedition.Simulation.Tests;

public class RelationshipsTraitsAndConditionsTests
{
    private static GameContent Shipped() => ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static (CampaignState Campaign, Character Diego, Character Captain) Pair()
    {
        var campaign = new CampaignState();
        return (campaign, campaign.CreateCharacter("Diego", 29, "Palos"), campaign.CreateCharacter("Álvaro", 41, "Seville"));
    }

    // --- Relationships ----------------------------------------------------------------------

    [Fact]
    public void RelationshipsAreDirected()
    {
        var (campaign, diego, captain) = Pair();

        campaign.ChangeRelationship(diego.Id, captain.Id, RelationshipDimension.Trust, 30, "sailed under him before");

        Assert.Equal(30, campaign.GetRelationship(diego.Id, captain.Id)!.Trust);
        Assert.Null(campaign.GetRelationship(captain.Id, diego.Id));
    }

    [Fact]
    public void ChangesAccumulateClampAndKeepEveryReason()
    {
        var (campaign, diego, captain) = Pair();

        campaign.ChangeRelationship(diego.Id, captain.Id, RelationshipDimension.Trust, 80, "saved his life");
        campaign.ChangeRelationship(diego.Id, captain.Id, RelationshipDimension.Trust, 50, "kept his promise");
        campaign.ChangeRelationship(diego.Id, captain.Id, RelationshipDimension.Affection, -20, "humiliated him on deck");

        var relationship = campaign.GetRelationship(diego.Id, captain.Id)!;
        Assert.Equal(Relationship.Max, relationship.Trust);
        Assert.Equal(-20, relationship.Affection);
        Assert.Equal(0, relationship.Respect);
        Assert.Equal(["saved his life", "kept his promise", "humiliated him on deck"], relationship.History.Select(h => h.Cause));
    }

    [Fact]
    public void ChangesAreRecordedAndReadable()
    {
        var (campaign, diego, captain) = Pair();
        campaign.ChangeRelationship(diego.Id, captain.Id, RelationshipDimension.Trust, 30, "sailed under him before");
        campaign.ChangeRelationship(captain.Id, diego.Id, RelationshipDimension.Respect, -15, "late to his watch");

        var formatter = new ChronicleFormatter(campaign, TestContent.Create());
        var text = campaign.Chronicle.Entries.Select(formatter.Format).ToList();

        Assert.Equal(
        [
            "Diego's trust in Álvaro rose by 30 (sailed under him before).",
            "Álvaro's respect for Diego fell by 15 (late to his watch).",
        ], text);
        Assert.Equal([diego.Id, captain.Id], campaign.Chronicle.Entries[0].Characters);
    }

    [Fact]
    public void TheDeadFormNoNewViewsButViewsOfTheDeadCanChange()
    {
        var (campaign, diego, captain) = Pair();
        campaign.ChangeSurvivalState(captain.Id, SurvivalState.Dead, "fever");

        Assert.False(campaign.ChangeRelationship(captain.Id, diego.Id, RelationshipDimension.Trust, 10, "test").Succeeded);
        Assert.True(campaign.ChangeRelationship(diego.Id, captain.Id, RelationshipDimension.Respect, 20, "died at his post").Succeeded);
    }

    [Theory]
    [InlineData("self")]
    [InlineData("zero")]
    [InlineData("huge")]
    [InlineData("no cause")]
    [InlineData("unknown")]
    public void InvalidChangesAreRejected(string invalid)
    {
        var (campaign, diego, captain) = Pair();

        var result = invalid switch
        {
            "self" => campaign.ChangeRelationship(diego.Id, diego.Id, RelationshipDimension.Trust, 10, "test"),
            "zero" => campaign.ChangeRelationship(diego.Id, captain.Id, RelationshipDimension.Trust, 0, "test"),
            "huge" => campaign.ChangeRelationship(diego.Id, captain.Id, RelationshipDimension.Trust, 500, "test"),
            "no cause" => campaign.ChangeRelationship(diego.Id, captain.Id, RelationshipDimension.Trust, 10, " "),
            _ => campaign.ChangeRelationship(diego.Id, new Core.CharacterId(99), RelationshipDimension.Trust, 10, "test"),
        };

        Assert.False(result.Succeeded);
        Assert.Empty(campaign.Relationships);
        Assert.Empty(campaign.Chronicle.Entries);
    }

    [Fact]
    public void RelationshipsSurviveSaveAndLoad()
    {
        var runner = TestDeckScenario.Create(Shipped());
        var json = SaveSerializer.Serialize(runner.Campaign);

        var loaded = SaveSerializer.Deserialize(json, Shipped());

        Assert.Equal(json, SaveSerializer.Serialize(loaded));
        var ids = loaded.Characters.Keys.Order().ToList();
        var diegosView = loaded.GetRelationship(ids[1], ids[0])!;
        Assert.Equal(30, diegosView.Trust);
        Assert.Equal("sailed under him before", Assert.Single(diegosView.History).Cause);
    }

    [Fact]
    public void FormatFourSaveUpgradesWithNoRelationships()
    {
        var root = JsonNode.Parse(SaveSerializer.Serialize(TestDeckScenario.Create(Shipped()).Campaign))!.AsObject();
        root["schemaVersion"] = 4;
        root.Remove("knowledge");
        root.Remove("relationships");

        var loaded = SaveSerializer.Deserialize(root.ToJsonString(), Shipped());

        Assert.Empty(loaded.Relationships);
    }

    [Theory]
    [InlineData("missing character")]
    [InlineData("self")]
    [InlineData("duplicate")]
    [InlineData("out of range")]
    [InlineData("bad dimension")]
    [InlineData("unknown trait")]
    [InlineData("unknown condition")]
    public void TamperedRelationshipsTraitsAndConditionsAreRejected(string tampering)
    {
        var root = JsonNode.Parse(SaveSerializer.Serialize(TestDeckScenario.Create(Shipped()).Campaign))!.AsObject();
        var relationships = root["relationships"]!.AsArray();
        var first = relationships[0]!;
        switch (tampering)
        {
            case "missing character": first["to"] = 99; break;
            case "self": first["to"] = first["from"]!.DeepClone(); break;
            case "duplicate": relationships.Add(first.DeepClone()); break;
            case "out of range": first["trust"] = 150; break;
            case "bad dimension": first["history"]!.AsArray()[0]!["dimension"] = "Envy"; break;
            case "unknown trait": root["characters"]!.AsArray()[0]!["traits"]!.AsArray().Add("trait.heroic"); break;
            case "unknown condition": root["characters"]!.AsArray()[0]!["conditions"]!.AsArray().Add("condition.plague"); break;
        }

        Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(root.ToJsonString(), Shipped()));
    }

    // --- Traits and conditions as content ---------------------------------------------------

    [Fact]
    public void ShippedTraitsAndConditionsLoad()
    {
        var content = Shipped();

        Assert.Equal(5, content.Traits.Count);
        Assert.Equal("Rope burn", content.Conditions["condition.rope_burn"].Name);
        Assert.All(content.Tasks.Values.Where(t => t.Hazard is not null),
            t => Assert.True(content.Conditions.ContainsKey(t.Hazard!.Condition)));
    }

    [Fact]
    public void HazardWithUnknownConditionIsRejected()
    {
        var bad = TestContent.StormWork with { Hazard = TestContent.StormWork.Hazard! with { Condition = "condition.plague" } };

        var ex = Assert.Throws<ContentValidationException>(() => new GameContent(TestContent.Rules, [], TestContent.Items, [bad]));

        Assert.Contains(ex.Errors, e => e.Contains("unknown condition 'condition.plague'"));
    }

    [Fact]
    public void ScenarioCrewHaveDefinedTraitsAndAHistoryTogether()
    {
        var runner = TestDeckScenario.Create(Shipped());

        Assert.All(runner.Campaign.Characters.Values, c => Assert.NotEmpty(c.Traits));
        Assert.All(runner.Campaign.Characters.Values.SelectMany(c => c.Traits), t => Assert.True(runner.Content.Traits.ContainsKey(t)));
        Assert.Equal(5, runner.Campaign.Relationships.Count);
    }
}
