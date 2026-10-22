using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;

namespace Expedition.Simulation.World;

/// <summary>
/// Stores where they lie, inside the camp (ECONOMY_LOOP §11-12, §48): the camp's stores lie in one place, the
/// stack under the sailcloth on the beach, then the storehouse once it stands. What is cut, gathered, drawn
/// or caught is in the worker's hands until they carry it there, as much as one person carries; the
/// materials for a piece of work are taken up there on the way to it. So the storehouse's place matters:
/// near the work, the walks are short.
/// </summary>
public sealed partial class SimulationRunner
{
    // What one person carries in their hands and on their back, kg.
    private double CarryKg => Content.Rules.PersonalCarryCapacity.MaxWeightKg ?? 25;

    private bool Holding(CharacterId id) => Campaign.Camp?.InHand.ContainsKey(id.Value) == true;

    private double InHandKg(CharacterId id) =>
        Campaign.Camp?.InHand.GetValueOrDefault(id.Value) is { } held ? held.Sum(h => h.Value * Content.Items[h.Key].WeightKg) : 0;

    /// <summary>Where the camp's stores lie: at the storehouse once one stands, else the stack on the beach. Null away from a camp.</summary>
    public GridPosition? StoreCell()
    {
        if (Campaign.Camp is null || !TryLocation(CampLocation, out var location))
        {
            return null;
        }

        if (StandingOf(b => b.Stores).FirstOrDefault() is { } storehouse && CampSpots().FirstOrDefault(s => s.Id == $"plot.{storehouse.Plot}.a") is { } door)
        {
            return new GridPosition(door.X, door.Y);
        }

        return (location.FindSpot("spot.stores") ?? location.FindSpot("spot.hearth")) is { } stack ? new GridPosition(stack.X, stack.Y) : null;
    }

    // Someone with a load in hand takes it to the stores before anything else: true while they are on the
    // way; at the stores it is put away and they go on to their work.
    private bool Stow(Character character)
    {
        if (!Holding(character.Id) || !Campaign.Placements.TryGetValue(character.Id, out var placement) || placement.LocationId != CampLocation
            || StoreCell() is not { } store)
        {
            return false;
        }

        var here = placement.CellAt(Campaign.Clock.Tick);
        if (here.ManhattanDistance(store) <= 1 || PathIn(Location(CampLocation), here, store) is not { Count: >= 2 } path)
        {
            PutAway(character.Id);
            return false;
        }

        StartWalk(character.Id, path);
        return true;
    }

    // What someone holds goes into the stores: carried there, or left for others to take up when they
    // cannot (sick, hurt, gone off with a party, or dead).
    internal void PutAway(CharacterId id)
    {
        if (Campaign.Camp?.Release(id.Value) is { } held)
        {
            foreach (var (item, count) in held)
            {
                Campaign.SharedStore.TryAdd(item, count, Content.Items);
            }
        }
    }

    // The way to a piece of work by way of the stores, where its materials are taken up; null if it is no
    // longer that way round, or there is no such way.
    private IReadOnlyList<GridPosition>? ByWayOfStores(Character character, WorkSpotDefinition spot)
    {
        if (StoreCell() is not { } store || !Campaign.Placements.TryGetValue(character.Id, out var placement) || placement.LocationId != CampLocation)
        {
            return null;
        }

        var location = Location(CampLocation);
        var here = placement.CellAt(Campaign.Clock.Tick);
        var work = new GridPosition(spot.X, spot.Y);
        if (here.ManhattanDistance(store) <= 1 || PathIn(location, here, store) is not { } there || PathIn(location, store, work) is not { } on)
        {
            return null;
        }

        return [.. there, .. on.Skip(1)];
    }

    // Each hour: loads left in the hands of those who cannot carry them go into the stores.
    private void StepCarrying()
    {
        if (Campaign.Camp is not { } camp)
        {
            return;
        }

        foreach (var who in camp.InHand.Keys.ToList())
        {
            var id = new CharacterId(who);
            if (!Campaign.Characters.TryGetValue(id, out var character) || !character.CanWork || Away(id))
            {
                PutAway(id);
            }
        }
    }
}
