using Expedition.Simulation.Campaign;
using Expedition.Simulation.History;

namespace Expedition.Simulation.Tests;

public class PhaseTransitionTests
{
    [Fact]
    public void FullHappyPathIsAllowed()
    {
        var campaign = new CampaignState();

        Assert.True(campaign.TryTransitionTo(CampaignPhase.PortPreparation, "charter accepted").Succeeded);
        Assert.True(campaign.TryTransitionTo(CampaignPhase.Voyage, "departed at dawn").Succeeded);
        Assert.True(campaign.TryTransitionTo(CampaignPhase.Landfall, "sighted land").Succeeded);
        Assert.True(campaign.TryTransitionTo(CampaignPhase.SettlementSurvival, "camp founded").Succeeded);
        Assert.True(campaign.TryTransitionTo(CampaignPhase.CharterFulfilled, "two winters survived").Succeeded);

        Assert.Equal(CampaignPhase.CharterFulfilled, campaign.Phase);
        Assert.Equal(5, campaign.Chronicle.Entries.Count);
        Assert.All(campaign.Chronicle.Entries, e => Assert.Equal(ChronicleEntryKind.PhaseChanged, e.Kind));
    }

    [Fact]
    public void ShipwreckBranchCanContinueToSettlement()
    {
        var campaign = new CampaignState(CampaignPhase.Voyage);

        Assert.True(campaign.TryTransitionTo(CampaignPhase.ShipwreckSurvival, "ran aground in the storm").Succeeded);
        Assert.True(campaign.TryTransitionTo(CampaignPhase.SettlementSurvival, "no way off the island").Succeeded);
    }

    [Fact]
    public void InvalidTransitionIsRejectedLeavesPhaseAndIsRecorded()
    {
        var campaign = new CampaignState(CampaignPhase.PortPreparation);

        var result = campaign.TryTransitionTo(CampaignPhase.SettlementSurvival, "skip the voyage");

        Assert.False(result.Succeeded);
        Assert.Equal(CampaignPhase.PortPreparation, campaign.Phase);
        var entry = Assert.Single(campaign.Chronicle.Entries);
        Assert.Equal(ChronicleEntryKind.TransitionRejected, entry.Kind);
        Assert.Contains("skip the voyage", entry.Description);
    }

    [Theory]
    [InlineData(CampaignPhase.CharterFulfilled)]
    [InlineData(CampaignPhase.CampaignFailed)]
    public void TerminalPhasesHaveNoExits(CampaignPhase terminal)
    {
        Assert.True(PhaseTransitions.IsTerminal(terminal));
        foreach (var target in Enum.GetValues<CampaignPhase>())
        {
            Assert.False(PhaseTransitions.IsAllowed(terminal, target));
        }
    }

    [Fact]
    public void EveryPhaseHasAnEntryInTheTable()
    {
        foreach (var phase in Enum.GetValues<CampaignPhase>())
        {
            var isTerminal = phase is CampaignPhase.CharterFulfilled or CampaignPhase.CampaignFailed;
            Assert.Equal(isTerminal, PhaseTransitions.IsTerminal(phase));
        }
    }

    [Fact]
    public void NonTerminalPhasesCanAlwaysFail()
    {
        foreach (var phase in Enum.GetValues<CampaignPhase>().Where(p => !PhaseTransitions.IsTerminal(p)))
        {
            Assert.True(PhaseTransitions.IsAllowed(phase, CampaignPhase.CampaignFailed), $"{phase} cannot fail");
        }
    }

    [Fact]
    public void PhaseCannotTransitionToItself()
    {
        foreach (var phase in Enum.GetValues<CampaignPhase>())
        {
            Assert.False(PhaseTransitions.IsAllowed(phase, phase), $"{phase} -> {phase} should not be allowed");
        }
    }

    [Fact]
    public void ChronicleSequenceIsStrictlyIncreasing()
    {
        var campaign = new CampaignState();
        campaign.TryTransitionTo(CampaignPhase.Voyage, "invalid");
        campaign.TryTransitionTo(CampaignPhase.PortPreparation, "valid");

        Assert.Equal([1L, 2L], campaign.Chronicle.Entries.Select(e => e.Sequence));
    }
}
