using Expedition.Simulation.Core;

namespace Expedition.Simulation.Campaign;

/// <summary>
/// A household (step P, ECONOMY_LOOP §32): a couple who have made a home together, and their children. A
/// partner from a people of the coast brings that people's customs with them.
/// </summary>
public sealed class Household
{
    /// <summary>Its number.</summary>
    public required int Id { get; init; }

    private readonly List<CharacterId> _adults = [];
    private readonly List<CharacterId> _children = [];

    /// <summary>The couple.</summary>
    public IReadOnlyList<CharacterId> Adults => _adults;

    /// <summary>Their children, eldest first.</summary>
    public IReadOnlyList<CharacterId> Children => _children;

    /// <summary>When they made a home together.</summary>
    public required long Formed { get; init; }

    /// <summary>The people of the coast one of them is of, whose customs the household keeps too; null for none.</summary>
    public string? Customs { get; init; }

    /// <summary>When a child is due, or null.</summary>
    public long? Due { get; internal set; }

    internal void AddAdult(CharacterId id) => _adults.Add(id);

    internal void AddChild(CharacterId id) => _children.Add(id);
}
