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
    /// <exception cref="ArgumentException">The description is empty.</exception>
    public ChronicleEntry Record(long tick, ChronicleEntryKind kind, string description, params CharacterId[] characters)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Chronicle entries need a description.", nameof(description));
        }

        var entry = new ChronicleEntry(_entries.Count + 1, tick, kind, description, characters.ToArray());
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
