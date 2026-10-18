namespace Expedition.Simulation.Core;

/// <summary>
/// Seeded PCG32 random number generator whose full state can be saved and restored, so a loaded
/// campaign continues the exact same random sequence (see docs/DECISIONS.md D-002).
/// All gameplay randomness must come from the campaign's instance of this class, never from
/// <see cref="System.Random"/>.
/// </summary>
public sealed class SimulationRandom
{
    private const ulong Multiplier = 6364136223846793005UL;
    private ulong _state;
    private readonly ulong _increment;

    /// <summary>Creates a generator from a seed. The same seed always yields the same sequence.</summary>
    public SimulationRandom(ulong seed, ulong stream = 0xda3e39cb94b95bdbUL)
    {
        _increment = (stream << 1) | 1UL;
        _state = 0;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    private SimulationRandom(ulong state, ulong increment, bool _)
    {
        _state = state;
        _increment = increment;
    }

    /// <summary>The generator's current internal state, for saving.</summary>
    public (ulong State, ulong Increment) SaveState() => (_state, _increment);

    /// <summary>Recreates a generator from a state returned by <see cref="SaveState"/>.</summary>
    /// <exception cref="ArgumentException">The increment is even, which a valid state never has.</exception>
    public static SimulationRandom FromState(ulong state, ulong increment)
    {
        if ((increment & 1UL) == 0)
        {
            throw new ArgumentException("A PCG32 increment must be odd; this state is corrupt.", nameof(increment));
        }

        return new SimulationRandom(state, increment, true);
    }

    /// <summary>Next uniformly distributed 32-bit value.</summary>
    public uint NextUInt()
    {
        var old = _state;
        _state = unchecked(old * Multiplier + _increment);
        var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        var rotation = (int)(old >> 59);
        return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
    }

    /// <summary>Uniform double in [0, 1).</summary>
    public double NextDouble() => NextUInt() / 4294967296.0;

    /// <summary>Uniform integer in [0, <paramref name="maxExclusive"/>), without modulo bias.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is not positive.</exception>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "Upper bound must be positive.");
        }

        var bound = (uint)maxExclusive;
        var threshold = (uint)(-bound % bound);
        while (true)
        {
            var value = NextUInt();
            if (value >= threshold)
            {
                return (int)(value % bound);
            }
        }
    }

    /// <summary>True with the given probability (0..1). Always consumes exactly one value.</summary>
    public bool Chance(double probability) => NextDouble() < probability;
}
