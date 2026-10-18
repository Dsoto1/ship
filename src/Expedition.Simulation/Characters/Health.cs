namespace Expedition.Simulation.Characters;

/// <summary>
/// A character's overall condition plus named conditions such as injuries or illnesses.
/// The value is fractional because starvation and dehydration wear it down a little every minute.
/// </summary>
public sealed class Health
{
    /// <summary>Lowest valid health value: dead.</summary>
    public const double Min = 0;

    /// <summary>Highest valid health value: uninjured and well.</summary>
    public const double Max = 100;

    private readonly HashSet<string> _conditions = new(StringComparer.Ordinal);
    private double _value = Max;

    /// <summary>0 = dead, 100 = fully healthy.</summary>
    public double Value
    {
        get => _value;
        set
        {
            if (double.IsNaN(value) || value is < Min or > Max)
            {
                throw new ArgumentOutOfRangeException(nameof(Value), value, $"Health must be between {Min} and {Max}.");
            }

            _value = value;
        }
    }

    /// <summary>Condition definition IDs currently affecting the character, for example <c>condition.broken_arm</c>.</summary>
    public IReadOnlyCollection<string> Conditions => _conditions;

    /// <summary>Adds a condition. Returns false if the character already had it.</summary>
    public bool AddCondition(string conditionId) => _conditions.Add(conditionId);

    /// <summary>Removes a condition. Returns false if the character did not have it.</summary>
    public bool RemoveCondition(string conditionId) => _conditions.Remove(conditionId);

    /// <summary>Reduces health by <paramref name="amount"/>, stopping at <see cref="Min"/>.</summary>
    public void Lose(double amount) => _value = Math.Max(Min, _value - amount);
}
