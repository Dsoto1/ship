using Expedition.Simulation.Campaign;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Each at their own pace (docs/WALKS.md): the old, the hurt, the sick and the laden walk slower.</summary>
public class PaceTests
{
    private const string Deck = "location.deck";
    private static readonly GridPosition TopLeft = new(0, 0);
    private static readonly GridPosition BottomLeft = new(0, 4);

    private static (SimulationRunner Runner, Core.CharacterId Walker) Setup(int age)
    {
        var campaign = new CampaignState(CampaignPhase.Voyage, seed: 3);
        var runner = new SimulationRunner(campaign, TestContent.Create());
        var walker = campaign.CreateCharacter("Rodrigo", age, "Triana");
        Assert.True(runner.PlaceCharacter(walker.Id, Deck, TopLeft).Succeeded);
        return (runner, walker.Id);
    }

    [Theory]
    [InlineData(27, 1.0, Gait.Usual)]
    [InlineData(56, 0.85, Gait.Usual)]
    [InlineData(61, 0.7, Gait.Old)]
    [InlineData(66, 0.4, Gait.Elderly)]
    public void AgeSlowsTheWalk(int age, double pace, Gait gait)
    {
        var (runner, walker) = Setup(age);

        Assert.Equal(pace, runner.WalkPace(walker), 3);
        Assert.Equal(gait, runner.GaitOf(walker));
    }

    [Fact]
    public void AnInjuryOrAnIllnessSlowsItFurther()
    {
        var (runner, walker) = Setup(61);
        var health = runner.Campaign.Characters[walker].Health;

        health.AddCondition("condition.fever");
        Assert.Equal(0.56, runner.WalkPace(walker), 3);
        Assert.Equal(Gait.Ill, runner.GaitOf(walker));

        health.AddCondition("condition.sprain");
        Assert.Equal(0.42, runner.WalkPace(walker), 3);
        Assert.Equal(Gait.Injured, runner.GaitOf(walker));
    }

    [Fact]
    public void NoOneIsSlowerThanAThirdOfThePace()
    {
        var (runner, walker) = Setup(70);
        runner.Campaign.Characters[walker].Health.AddCondition("condition.wounded");

        Assert.Equal(0.35, runner.WalkPace(walker), 3);
    }

    [Fact]
    public void ASlowWalkerTakesLongerOverTheSameGround()
    {
        var (young, youth) = Setup(27);
        var (old, elder) = Setup(66);

        Assert.True(young.MoveCharacter(youth, BottomLeft).Succeeded);
        Assert.True(old.MoveCharacter(elder, BottomLeft).Succeeded);

        // 14 steps: at 4 cells a minute, 4 ticks; at 0.4 of that, 9.
        Assert.Equal(4, young.Campaign.Placements[youth].Movement!.ArriveTick);
        Assert.Equal(9, old.Campaign.Placements[elder].Movement!.ArriveTick);
    }
}
