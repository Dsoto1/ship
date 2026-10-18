namespace Expedition.Simulation.Characters;

/// <summary>
/// A character's overall condition plus named conditions such as injuries or illnesses.
/// </summary>
public sealed class Health
{
    /// <summary>Lowest valid health value.</summary>
    public const int Min = 0;

    /// <summary>Highest valid health value: uninjured and well.</summary>
    public const int Max = 100;

    private readonly HashSet<string> _conditions = new(StringComparer.Ordinal);
    private int _value = Max;

    /// <summary>0 = near death, 100 = fully healthy.</summary>
    public int Value
    {
        get => _value;
        set
        {
            if (value is < Min or > Max)
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
}
