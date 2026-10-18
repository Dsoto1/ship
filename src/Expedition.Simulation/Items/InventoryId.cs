using Expedition.Simulation.Core;

namespace Expedition.Simulation.Items;

/// <summary>Stable identifier for an inventory, for example <c>inv.stores</c> or <c>inv.char-0003</c>.</summary>
/// <param name="Value">The identifier text.</param>
public readonly record struct InventoryId(string Value)
{
    /// <summary>The personal inventory of a character: what they carry.</summary>
    public static InventoryId ForCharacter(CharacterId id) => new($"inv.{id}");

    /// <summary>Returns <see cref="Value"/>.</summary>
    public override string ToString() => Value;
}
