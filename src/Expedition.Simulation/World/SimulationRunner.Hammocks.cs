using System.Globalization;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;

namespace Expedition.Simulation.World;

// More room aboard (docs/CREW_PROPOSAL.md §2, the owner's choice 3a): hammocks slung in the hold for up to three more
// hands, at a price in sueldos, and the crowding that comes with them.
public sealed partial class SimulationRunner
{
    /// <summary>How much likelier fever and flux are for each hand beyond the Garza's own berths.</summary>
    internal const double CrowdedIllness = 0.1;

    /// <summary>How much faster grievances grow in a storm for each hand beyond the Garza's own berths.</summary>
    internal const double CrowdedStormTemper = 0.1;

    /// <summary>Berths for hired hands now: the Garza's own and any hammocks slung in the hold.</summary>
    public int BerthsAboard => Content.Port is { } rules ? rules.Berths + (Campaign.Port?.Hammocks ?? 0) : 0;

    /// <summary>
    /// Has the chandler sling a hammock in the hold, with the carpentry for it: one more berth, for the price, up to
    /// the most the hold will take. Only in port.
    /// </summary>
    public CommandResult SlingHammock()
    {
        if (Content.Port is not { } rules || Campaign.Port is not { } port)
        {
            return CommandResult.Rejected("Only in port, before she sails.");
        }

        if (port.Hammocks >= rules.MostHammocks)
        {
            return CommandResult.Rejected($"The hold will take no more: {rules.MostHammocks} hammocks are slung already.");
        }

        if (port.Funds < rules.HammockPrice)
        {
            return CommandResult.Rejected($"A hammock and the carpentry cost {rules.HammockPrice} sueldos; {port.Funds} are left in the purse.");
        }

        port.Funds -= rules.HammockPrice;
        port.Hammocks++;
        Campaign.Record(ChronicleEntryKind.HammockSlung, [new(ChronicleParameters.Level, BerthsAboard.ToString(CultureInfo.InvariantCulture))]);
        return CommandResult.Ok();
    }

    /// <summary>
    /// Hands aboard beyond the Garza's own berths (0 when not crowded): each makes fever and flux likelier at sea and
    /// tempers shorter in a storm.
    /// </summary>
    public int Crowded =>
        Content.Port is { } rules
            ? Math.Max(0, (Campaign.Port is { } port ? port.Hired.Count : Campaign.Crew.Count(c => c.Id != Campaign.CaptainId && c.SurvivalState == SurvivalState.Alive)) - rules.Berths)
            : 0;
}
