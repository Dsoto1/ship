namespace Expedition.Simulation.Characters;

/// <summary>
/// A character's physical and mental needs. Every value runs from 0 (fully satisfied) to 100 (critical):
/// hunger 100 is starving, fatigue 100 is collapsing from exhaustion.
/// </summary>
public sealed class Needs
{
    /// <summary>Lowest valid need value: fully satisfied.</summary>
    public const int Min = 0;

    /// <summary>Highest valid need value: critical.</summary>
    public const int Max = 100;

    private int _hunger;
    private int _thirst;
    private int _fatigue;
    private int _stress;

    /// <summary>0 = fed, 100 = starving.</summary>
    public int Hunger { get => _hunger; set => _hunger = Validate(value, nameof(Hunger)); }

    /// <summary>0 = hydrated, 100 = severely dehydrated.</summary>
    public int Thirst { get => _thirst; set => _thirst = Validate(value, nameof(Thirst)); }

    /// <summary>0 = rested, 100 = exhausted.</summary>
    public int Fatigue { get => _fatigue; set => _fatigue = Validate(value, nameof(Fatigue)); }

    /// <summary>0 = calm, 100 = breaking point.</summary>
    public int Stress { get => _stress; set => _stress = Validate(value, nameof(Stress)); }

    /// <summary>
    /// Clamps a value that results from simulation (for example, fatigue after hard work)
    /// into the valid range. Use this for deltas; use the property setters for authored values.
    /// </summary>
    public static int Clamp(int value) => Math.Clamp(value, Min, Max);

    private static int Validate(int value, string name)
    {
        if (value is < Min or > Max)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be between {Min} and {Max}.");
        }

        return value;
    }
}
