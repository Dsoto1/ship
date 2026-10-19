using Expedition.Simulation.Core;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class ClockAndRandomTests
{
    [Fact]
    public void ClockReportsDayAndTimeOfDay()
    {
        var clock = new SimulationClock(SimulationClock.TicksPerDay * 3 + 14 * 60 + 5);

        Assert.Equal(3, clock.Day);
        Assert.Equal(new TimeSpan(14, 5, 0), clock.TimeOfDay);
        Assert.Equal("Day 4, 14:05", clock.ToString());
    }

    [Fact]
    public void ClockCannotStartBeforeZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationClock(-1));
    }

    [Fact]
    public void SameSeedGivesSameSequence()
    {
        var a = new SimulationRandom(42);
        var b = new SimulationRandom(42);

        Assert.Equal(Enumerable.Range(0, 1000).Select(_ => a.NextUInt()), Enumerable.Range(0, 1000).Select(_ => b.NextUInt()));
    }

    [Fact]
    public void DifferentSeedsGiveDifferentSequences()
    {
        var a = new SimulationRandom(1);
        var b = new SimulationRandom(2);

        Assert.NotEqual(Enumerable.Range(0, 10).Select(_ => a.NextUInt()), Enumerable.Range(0, 10).Select(_ => b.NextUInt()));
    }

    [Fact]
    public void RestoredStateContinuesTheSameSequence()
    {
        var original = new SimulationRandom(7);
        for (var i = 0; i < 123; i++)
        {
            original.NextUInt();
        }

        var (state, increment) = original.SaveState();
        var restored = SimulationRandom.FromState(state, increment);

        Assert.Equal(Enumerable.Range(0, 100).Select(_ => original.NextUInt()), Enumerable.Range(0, 100).Select(_ => restored.NextUInt()));
    }

    [Fact]
    public void CorruptStateIsRejected()
    {
        Assert.Throws<ArgumentException>(() => SimulationRandom.FromState(123, 2));
    }

    [Fact]
    public void NextIntStaysInRangeAndCoversIt()
    {
        var random = new SimulationRandom(99);
        var counts = new int[6];

        for (var i = 0; i < 60_000; i++)
        {
            counts[random.NextInt(6)]++;
        }

        Assert.All(counts, count => Assert.InRange(count, 9_000, 11_000));
    }

    [Fact]
    public void NextDoubleIsInUnitInterval()
    {
        var random = new SimulationRandom(3);

        Assert.All(Enumerable.Range(0, 10_000).Select(_ => random.NextDouble()), d => Assert.InRange(d, 0.0, 0.9999999999));
    }
}
