using Expedition.Simulation.Core;

namespace Expedition.Simulation.History;

/// <summary>
/// Append-only history of important campaign events. Entries are never edited or removed.
/// This is the source for the in-game journal and for explaining outcomes.
/// </summary>
public sealed class Chronicle
{
    private readonly List<ChronicleEntry> _entries = [];

    /// <summary>All entries in the order they were recorded.</summary>
    public IReadOnlyList<ChronicleEntry> Entries => _entries;

    /// <summary>Appends an entry stamped with <paramref name="tick"/> and returns it.</summary>
    /// <param name="tick">When it happened.</param>
    /// <param name="kind">What kind of event it is.</param>
    /// <param name="parameters">Named values; null values are left out.</param>
    /// <param name="characters">Characters involved, the main one first.</param>
    public ChronicleEntry Record(
        long tick, ChronicleEntryKind kind, IEnumerable<KeyValuePair<string, string?>> parameters, params CharacterId[] characters)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in parameters)
        {
            if (value is not null)
            {
                values[name] = value;
            }
        }

        var entry = new ChronicleEntry(_entries.Count + 1, tick, kind, characters.ToArray(), values);
        _entries.Add(entry);
        return entry;
    }

    /// <summary>Appends an entry read from a save, which must continue the sequence exactly.</summary>
    /// <exception cref="InvalidOperationException">The sequence number is out of order.</exception>
    internal void Restore(ChronicleEntry entry)
    {
        if (entry.Sequence != _entries.Count + 1)
        {
            throw new InvalidOperationException(
                $"Chronicle entry {entry.Sequence} is out of order; expected {_entries.Count + 1}.");
        }

        _entries.Add(entry);
    }
}
