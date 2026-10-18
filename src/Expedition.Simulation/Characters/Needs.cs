namespace Expedition.Simulation.Characters;

/// <summary>
/// A character's physical and mental needs. Every value runs from 0 (fully satisfied) to 100 (critical):
/// hunger 100 is starving, fatigue 100 is collapsing from exhaustion.
/// Values are fractional because they change by small amounts every in-game minute.
/// </summary>
public sealed class Needs
{
    /// <summary>Lowest valid need value: fully satisfied.</summary>
    public const double Min = 0;

    /// <summary>Highest valid need value: critical.</summary>
    public const double Max = 100;

    private double _hunger;
    private double _thirst;
    private double _fatigue;
    private double _stress;

    /// <summary>0 = fed, 100 = starving.</summary>
    public double Hunger { get => _hunger; set => _hunger = Validate(value, nameof(Hunger)); }

    /// <summary>0 = hydrated, 100 = severely dehydrated.</summary>
    public double Thirst { get => _thirst; set => _thirst = Validate(value, nameof(Thirst)); }

    /// <summary>0 = rested, 100 = exhausted.</summary>
    public double Fatigue { get => _fatigue; set => _fatigue = Validate(value, nameof(Fatigue)); }

    /// <summary>0 = calm, 100 = breaking point.</summary>
    public double Stress { get => _stress; set => _stress = Validate(value, nameof(Stress)); }

    /// <summary>
    /// Clamps a value that results from simulation (for example, fatigue after hard work)
    /// into the valid range. Use this for deltas; use the property setters for authored values.
    /// </summary>
    public static double Clamp(double value) => Math.Clamp(value, Min, Max);

    /// <summary>Adds <paramref name="rates"/> scaled by <paramref name="hours"/>, clamping each need into range.</summary>
    public void Apply(NeedRates rates, double hours)
    {
        _hunger = Clamp(_hunger + rates.Hunger * hours);
        _thirst = Clamp(_thirst + rates.Thirst * hours);
        _fatigue = Clamp(_fatigue + rates.Fatigue * hours);
        _stress = Clamp(_stress + rates.Stress * hours);
    }

    private static double Validate(double value, string name)
    {
        if (double.IsNaN(value) || value is < Min or > Max)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be between {Min} and {Max}.");
        }

        return value;
    }
}
