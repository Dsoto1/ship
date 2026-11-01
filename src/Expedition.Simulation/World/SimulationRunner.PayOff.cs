using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;

namespace Expedition.Simulation.World;

// Paying a hand off in port (docs/CREW_PROPOSAL.md §1, the owner's choices 1a and 2a): the berth is free again, at a
// price in respect, and for a day the port talks of it.
public sealed partial class SimulationRunner
{
    /// <summary>How long the port talks of a captain who turns men off, and how much more they ask meanwhile.</summary>
    private const long TalkOfPayingOff = SimulationClock.TicksPerDay;

    private const double PaidOffPremium = 1.1;

    /// <summary>
    /// Pays off someone signed on, before the ship sails. They keep their advance (it is spent), take their chest
    /// ashore and go back to the port, and will not sign on again. The rest of the crew think less of the captain,
    /// and those close to the one turned off think a good deal less. For a day, those seeking a berth ask a tenth more.
    /// </summary>
    public CommandResult PayOff(CharacterId id)
    {
        if (Content.Port is not { } rules || Campaign.Port is not { } port)
        {
            return CommandResult.Rejected("Only in port, before she sails.");
        }

        if (!port.Hired.Contains(id) || !Campaign.Characters.TryGetValue(id, out var character))
        {
            return CommandResult.Rejected("They are not signed on.");
        }

        var captain = Campaign.CaptainId;
        port.Unhire(id);
        port.MarkGone(id);
        character.Membership = Membership.Local;
        Campaign.AbandonTask(id);
        Campaign.SetDuties(id, []);
        var quay = Location(rules.Location);
        var gangway = quay.Spots.FirstOrDefault(s => s.Id == rules.Gangway)?.Cell;
        SetCell(id, quay.Id, gangway is { } at ? NearestFree(quay, at) : FreeCell(quay));

        if (captain is { } master)
        {
            Campaign.ChangeRelationship(id, master, RelationshipDimension.Trust, -20, "was paid off");
            foreach (var hand in port.Hired.Where(h => h != master).OrderBy(h => h))
            {
                var close = Campaign.Relationships.TryGetValue((hand, id), out var bond) && bond.Affection >= 20;
                Campaign.ChangeRelationship(hand, master, RelationshipDimension.Respect, close ? -10 : -5, $"turned off {character.Name}");
                if (close)
                {
                    Campaign.ChangeRelationship(hand, master, RelationshipDimension.Trust, -5, $"turned off {character.Name}");
                }
            }
        }

        Campaign.Record(ChronicleEntryKind.CrewPaidOff, [], id);
        return CommandResult.Ok();
    }

    /// <summary>Whether this person was paid off (and so will not sign on again).</summary>
    public bool PaidOff(CharacterId id) => Campaign.Chronicle.Entries.Any(e => e.Kind == ChronicleEntryKind.CrewPaidOff && e.Characters.Contains(id));

    /// <summary>True for a day after the captain paid someone off: the port talks, and berths cost a tenth more.</summary>
    public bool TalkedOfForPayingOff => Campaign.Chronicle.Entries.Any(e =>
        e.Kind == ChronicleEntryKind.CrewPaidOff && Campaign.Clock.Tick - e.Tick < TalkOfPayingOff);

    // The nearest free walkable cell to a point.
    private GridPosition NearestFree(LocationDefinition location, GridPosition near)
    {
        var taken = Campaign.Placements.Values.Where(p => p.LocationId == location.Id).Select(p => p.Cell).ToHashSet();
        return Enumerable.Range(0, location.Height)
            .SelectMany(y => Enumerable.Range(0, location.Width).Select(x => new GridPosition(x, y)))
            .Where(cell => location.IsWalkable(cell) && !taken.Contains(cell))
            .OrderBy(cell => Math.Abs(cell.X - near.X) + Math.Abs(cell.Y - near.Y))
            .ThenBy(cell => cell.Y).ThenBy(cell => cell.X)
            .First();
    }
}
