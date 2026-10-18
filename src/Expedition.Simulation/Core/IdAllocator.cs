namespace Expedition.Simulation.Core;

/// <summary>
/// Issues unique IDs within one campaign. IDs are never reused, including IDs of dead characters.
/// </summary>
public sealed class IdAllocator
{
    private int _lastCharacterId;

    /// <summary>The highest character ID issued or reserved so far. Persisted with the campaign.</summary>
    public int LastCharacterId => _lastCharacterId;

    /// <summary>Issues a new character ID that has never been issued by this allocator.</summary>
    public CharacterId NextCharacterId() => new(++_lastCharacterId);

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
