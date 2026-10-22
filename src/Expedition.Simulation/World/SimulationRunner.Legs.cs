using System.Globalization;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// Laying off a course in legs, and writing on the chart (NAVIGATION §6, §22, §27). The captain puts points
/// on the chart; she steers for each in turn by the reckoning, then the passage's own course takes her on.
/// Each leg's length and a range of days at the passage's usual pace are given, never a promise. The chart
/// keeps the captain's notes, and marks what the voyage met where it met it: a gale, an island not found,
/// a watering place, the land raised.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>Most legs laid off at once.</summary>
    public const int MostLegs = 8;

    // How near a leg's end, by the reckoning, counts as reaching it.
    private const double LegReachedLeagues = 2;

    /// <summary>
    /// Lays off a leg to <paramref name="at"/> (the chart's leagues east and north of the port), after any
    /// already laid. Refused ashore, near the coast, with the coast in sight, or with too many legs.
    /// </summary>
    public CommandResult PlotLeg(ChartPoint at)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay, Navigation: { Sighted: false } navigation })
        {
            return CommandResult.Rejected("Legs are laid off at sea, before the coast is raised.");
        }

        if (navigation.Legs.Count >= MostLegs)
        {
            return CommandResult.Rejected($"No more than {MostLegs} legs at once.");
        }

        if (at.X < navigation.Destination.X + 3 || at.X > 4)
        {
            return CommandResult.Rejected("That is on the land, or too near it to steer for.");
        }

        navigation.AddLeg(at);
        Campaign.Record(ChronicleEntryKind.LegPlotted, [new(ChronicleParameters.Level, navigation.Legs.Count.ToString(CultureInfo.InvariantCulture))]);
        return CommandResult.Ok();
    }

    /// <summary>Takes away the legs laid off: she goes back to the passage's own course.</summary>
    public CommandResult ClearLegs()
    {
        if (Campaign.Voyage?.Navigation is not { } navigation || navigation.Legs.Count == 0)
        {
            return CommandResult.Rejected("No legs are laid off.");
        }

        navigation.ClearLegs();
        return CommandResult.Ok();
    }

    /// <summary>
    /// The course in legs from where she is reckoned to be: each leg's end, its length, and a range of days at
    /// the passage's usual pace (NAVIGATION §27). The last leg is the passage's own, to the destination.
    /// </summary>
    public IReadOnlyList<(ChartPoint To, double Leagues, double LowDays, double HighDays)> LegPlan()
    {
        if (Campaign.Voyage is not { Navigation: { } navigation } ship)
        {
            return [];
        }

        var (fast, slow) = Scenarios.ExpeditionScenario.PassageDays;
        var perDayHigh = ship.TotalLeagues / fast;
        var perDayLow = ship.TotalLeagues / slow;
        var plan = new List<(ChartPoint, double, double, double)>();
        var from = navigation.Reckoned;
        foreach (var leg in navigation.Legs)
        {
            var length = from.DistanceTo(leg);
            plan.Add((leg, length, length / perDayHigh, length / perDayLow));
            from = leg;
        }

        var offing = new ChartPoint(navigation.Destination.X + navigation.Offing, navigation.Destination.Y);
        var rest = from.X > offing.X ? from.DistanceTo(offing) + offing.DistanceTo(navigation.Destination) : from.DistanceTo(navigation.Destination);
        plan.Add((navigation.Destination, rest, rest / perDayHigh, rest / perDayLow));
        return plan;
    }

    /// <summary>Writes a note on the chart at <paramref name="at"/>. Refused if empty or long.</summary>
    public CommandResult WriteOnChart(ChartPoint at, string text)
    {
        if (Campaign.Voyage?.Navigation is not { } navigation)
        {
            return CommandResult.Rejected("There is no chart to write on.");
        }

        text = text.Trim();
        if (text.Length is 0 or > 60)
        {
            return CommandResult.Rejected("A note on the chart is a few words: no more than sixty letters.");
        }

        navigation.Note(new ChartNote(at, text, Campaign.Clock.Tick, true));
        return CommandResult.Ok();
    }

    /// <summary>Rubs out one of the captain's own notes (the voyage's own marks stay).</summary>
    public CommandResult RubOut(int index)
    {
        if (Campaign.Voyage?.Navigation is not { } navigation || index < 0 || index >= navigation.Notes.Count || !navigation.Notes[index].ByCaptain)
        {
            return CommandResult.Rejected("No such note of yours.");
        }

        navigation.RemoveNote(index);
        return CommandResult.Ok();
    }

    // What the voyage marks on the chart where it happened, by the reckoning then.
    private void MarkChart(string text, ChartPoint? at = null)
    {
        if (Campaign.Voyage?.Navigation is { } navigation)
        {
            navigation.Note(new ChartNote(at ?? navigation.Reckoned, $"{text}, day {Campaign.Clock.Day + 1}", Campaign.Clock.Tick, false));
        }
    }

    // Before she sails: the course for the next leg's end, if legs are laid off and she is not making for an island.
    private void SteerForLeg(Navigation navigation)
    {
        if (navigation.Call is null && navigation.Target is null && !navigation.Sighted && navigation.Legs.Count > 0)
        {
            navigation.Target = navigation.Legs[0];
        }
    }

    // After she sails: a leg's end reached, by the reckoning, and the next taken up.
    private void ReachLeg(Navigation navigation)
    {
        if (navigation.Call is not null || navigation.Legs.Count == 0 || navigation.Reckoned.DistanceTo(navigation.Legs[0]) > LegReachedLeagues)
        {
            return;
        }

        navigation.RemoveLeg(0);
        navigation.Target = null;
        Campaign.Record(ChronicleEntryKind.LegReached, [new(ChronicleParameters.Level, navigation.Legs.Count.ToString(CultureInfo.InvariantCulture))]);
    }
}
