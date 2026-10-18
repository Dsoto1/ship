using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.History;

namespace Expedition.Simulation.Tests;

public class SurvivalStateTests
{
    [Theory]
    [InlineData(SurvivalState.Incapacitated)]
    [InlineData(SurvivalState.Missing)]
    [InlineData(SurvivalState.Dead)]
    public void OnlyAliveCharactersCanWork(SurvivalState state)
    {
        var campaign = new CampaignState();
        var character = campaign.CreateCharacter("Juan", 35, "Huelva");

        campaign.ChangeSurvivalState(character.Id, state, "test");

        Assert.False(character.CanWork);
    }

    [Theory]
    [InlineData(SurvivalState.Alive)]
    [InlineData(SurvivalState.Incapacitated)]
    [InlineData(SurvivalState.Missing)]
    public void DeadCharactersCannotBeRevived(SurvivalState attempted)
    {
        var campaign = new CampaignState();
        var character = campaign.CreateCharacter("Juan", 35, "Huelva");
        campaign.ChangeSurvivalState(character.Id, SurvivalState.Dead, "drowned in the storm");

        var result = campaign.ChangeSurvivalState(character.Id, attempted, "test");

        Assert.False(result.Succeeded);
        Assert.Contains("dead", result.Reason);
        Assert.Equal(SurvivalState.Dead, character.SurvivalState);
        Assert.False(character.CanWork);
    }

    [Fact]
    public void MissingCharacterCanBeFoundAlive()
    {
        var campaign = new CampaignState();
        var character = campaign.CreateCharacter("Juan", 35, "Huelva");
        campaign.ChangeSurvivalState(character.Id, SurvivalState.Missing, "swept overboard");

        var result = campaign.ChangeSurvivalState(character.Id, SurvivalState.Alive, "found on the beach");

        Assert.True(result.Succeeded);
        Assert.True(character.CanWork);
    }

    [Fact]
    public void SurvivalChangeIsRecordedWithCauseAndCharacter()
    {
        var campaign = new CampaignState();
        var character = campaign.CreateCharacter("Juan", 35, "Huelva");

        campaign.ChangeSurvivalState(character.Id, SurvivalState.Incapacitated, "struck by a falling spar");

        var entry = Assert.Single(campaign.Chronicle.Entries);
        Assert.Equal(ChronicleEntryKind.SurvivalStateChanged, entry.Kind);
        Assert.Equal("struck by a falling spar", entry.Get(ChronicleParameters.Cause));
        Assert.Equal(nameof(SurvivalState.Incapacitated), entry.Get(ChronicleParameters.To));
        Assert.Equal([character.Id], entry.Characters);
    }

    [Fact]
    public void RejectedSurvivalChangeIsNotRecorded()
    {
        var campaign = new CampaignState();
        var character = campaign.CreateCharacter("Juan", 35, "Huelva");

        var result = campaign.ChangeSurvivalState(character.Id, SurvivalState.Alive, "already alive");

        Assert.False(result.Succeeded);
        Assert.Empty(campaign.Chronicle.Entries);
    }

    [Fact]
    public void UnknownCharacterIsRejected()
    {
        var campaign = new CampaignState();

        var result = campaign.ChangeSurvivalState(new Core.CharacterId(99), SurvivalState.Dead, "test");

        Assert.False(result.Succeeded);
    }
}
