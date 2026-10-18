using Expedition.Simulation.Core;

namespace Expedition.Simulation.History;

/// <summary>One recorded event in the campaign's history.</summary>
/// <param name="Sequence">Position in the chronicle, starting at 1. Strictly increasing.</param>
/// <param name="Kind">Category of the event.</param>
/// <param name="Description">Readable account of what happened and why.</param>
/// <param name="Characters">Characters involved, if any.</param>
public sealed record ChronicleEntry(
    long Sequence,
    ChronicleEntryKind Kind,
    string Description,
    IReadOnlyList<CharacterId> Characters);
