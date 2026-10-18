using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;

namespace Expedition.Simulation.Tests;

public class CharacterTests
{
    private static Character NewCharacter() => new(new CharacterId(1), "Diego", 28, "Palos");

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void SkillLevelsInRangeAreAccepted(int level)
    {
        var character = NewCharacter();

        character.Skills.Set("skill.carpentry", level);

        Assert.Equal(level, character.Skills.Get("skill.carpentry"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void SkillLevelsOutOfRangeAreRejected(int level)
    {
        var character = NewCharacter();

        Assert.Throws<ArgumentOutOfRangeException>(() => character.Skills.Set("skill.carpentry", level));
        Assert.Equal(0, character.Skills.Get("skill.carpentry"));
    }

    [Fact]
    public void UnknownSkillDefaultsToNoTraining()
    {
        Assert.Equal(0, NewCharacter().Skills.Get("skill.navigation"));
    }

    [Fact]
    public void SettingSkillToZeroRemovesIt()
    {
        var character = NewCharacter();
        character.Skills.Set("skill.sailing", 2);

        character.Skills.Set("skill.sailing", 0);

        Assert.Empty(character.Skills.Levels);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void NeedsOutOfRangeAreRejected(int value)
    {
        var needs = NewCharacter().Needs;

        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Hunger = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Thirst = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Fatigue = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Stress = value);
    }

    [Fact]
    public void NeedClampKeepsSimulatedValuesInRange()
    {
        Assert.Equal(100, Needs.Clamp(140));
        Assert.Equal(0, Needs.Clamp(-20));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void HealthOutOfRangeIsRejected(int value)
    {
        var health = NewCharacter().Health;

        Assert.Throws<ArgumentOutOfRangeException>(() => health.Value = value);
        Assert.Equal(Health.Max, health.Value);
    }

    [Fact]
    public void NewCharacterIsAliveHealthyAndAbleToWork()
    {
        var character = NewCharacter();

        Assert.Equal(SurvivalState.Alive, character.SurvivalState);
        Assert.Equal(100, character.Health.Value);
        Assert.True(character.CanWork);
    }

    [Theory]
    [InlineData("", 20, "Palos")]
    [InlineData("Diego", -1, "Palos")]
    [InlineData("Diego", 20, " ")]
    public void InvalidIdentityIsRejected(string name, int age, string origin)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Character(new CharacterId(1), name, age, origin));
    }

    [Fact]
    public void TraitsAreNotDuplicated()
    {
        var character = NewCharacter();

        Assert.True(character.AddTrait("trait.cautious"));
        Assert.False(character.AddTrait("trait.cautious"));
        Assert.Single(character.Traits);
    }
}
