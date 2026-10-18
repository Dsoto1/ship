namespace Expedition.Simulation.Core;

/// <summary>
/// Issues unique IDs within one campaign. IDs are never reused, including IDs of dead characters.
/// </summary>
public sealed class IdAllocator
{
    private int _lastCharacterId;
    private int _lastTaskId;

    /// <summary>The highest character ID issued or reserved so far. Persisted with the campaign.</summary>
    public int LastCharacterId => _lastCharacterId;

    /// <summary>Issues a new character ID that has never been issued by this allocator.</summary>
    public CharacterId NextCharacterId() => new(++_lastCharacterId);

    /// <summary>The highest task ID issued or reserved so far. Persisted with the campaign.</summary>
    public int LastTaskId => _lastTaskId;

    /// <summary>Issues a new task ID that has never been issued by this allocator.</summary>
    public Tasks.TaskId NextTaskId() => new(++_lastTaskId);

    /// <summary>Records that task ID number <paramref name="value"/> is in use, so it is never issued again.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public void ReserveTaskId(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Task IDs cannot be negative.");
        }

        _lastTaskId = Math.Max(_lastTaskId, value);
    }

    /// <summary>
    /// Records that <paramref name="id"/> is in use (for example, after loading a character),
    /// so it can never be issued again.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The ID is not positive.</exception>
    public void Reserve(CharacterId id)
    {
        if (id.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id.Value, "Character IDs must be positive.");
        }

        _lastCharacterId = Math.Max(_lastCharacterId, id.Value);
    }
}
