using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// The captain's standing orders, kept by the officer of the watch (VOYAGE_EXPERIENCE §8, §35; NAVIGATION
/// §7, §9): heave to in a storm, take in sail when it blows hard, make ready for heavy weather, call all
/// hands for the bilge, cut to short rations when the stores run low; and call the captain when the stores,
/// the bilge, the reckoning, noon or the wind say so. Each is acted on once as its condition comes and told
/// in the chronicle, so time can run fast and nothing passes silently.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>The sea at which "heave to in a storm" heaves her to.</summary>
    public const double StormSea = 2.4;

    /// <summary>Gives the captain's standing orders. Refused unless at sea, or with a number out of range.</summary>
    public CommandResult SetStandingOrders(StandingOrders orders)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship)
        {
            return CommandResult.Rejected("Standing orders are given at sea.");
        }

        if (orders.AllHandsAtBilge is < 5 or > 95 || orders.WakeBilgeOver is < 5 or > 95 || orders.ShortRationsUnderDays is < 1 or > 60
            || orders.WakeStoresUnderDays is < 1 or > 60 || orders.WakeDoubtOver is < 1 or > 200 || orders.WakeWindShift is < 10 or > 180)
        {
            return CommandResult.Rejected("That order is out of reason.");
        }

        ship.Orders = orders;
        ship.WindNoted = ship.Navigation?.WindFrom;
        return CommandResult.Ok();
    }

    /// <summary>
    /// How far out the reckoning may be, in leagues: across (east and west: every day's run since port, with
    /// no fix of longitude at sea) and along (north and south: the days since the last sight, on top of
    /// that sight's own error). Small once the coast is in sight.
    /// </summary>
    public (double Across, double Along) ReckoningDoubt()
    {
        if (Campaign.Voyage?.Navigation is not { } navigation)
        {
            return (0, 0);
        }

        var day = Campaign.Clock.Tick / (double)SimulationClock.TicksPerDay;
        var across = 1.5 + 1.6 * day;
        var last = navigation.Sights.LastOrDefault();
        var along = last is null ? 1.5 + 1.6 * day : 3 + 1.6 * Math.Max(0, day - last.Day - 0.5);
        return navigation.Sighted ? (0.3, navigation.Identified is null ? along : 1) : (across, along);
    }

    // Every ten minutes at sea: the officer of the watch keeps the captain's orders.
    private void FollowStandingOrders(VoyageState ship, VoyageRules rules)
    {
        var orders = ship.Orders;
        var sea = rules.Weather(ship.WeatherId).SeaState;

        if (orders.HeaveToInStorm)
        {
            if (sea >= StormSea && ship.SailPlan != SailPlan.HoveTo)
            {
                SetSailPlan(SailPlan.HoveTo);
                ship.Flag(StandingOrderReasons.HoveTo);
                Acted(StandingOrderReasons.HoveTo, ship.WeatherId);
            }
            else if (sea < rules.Storms.HeavySeaState && ship.SailPlan == SailPlan.HoveTo && ship.Unflag(StandingOrderReasons.HoveTo))
            {
                SetSailPlan(SailPlan.Normal);
                Acted(StandingOrderReasons.MadeSail, ship.WeatherId);
            }
        }

        if (orders.EaseInHeavyWeather && sea >= rules.Storms.HeavySeaState && ship.SailPlan == SailPlan.PressOn)
        {
            SetSailPlan(SailPlan.Normal);
            Acted(StandingOrderReasons.Eased, ship.WeatherId);
        }

        if (orders.PrepareForWeather && StormComing && ship.Flag(StandingOrderReasons.Preparing))
        {
            foreach (var work in Preparations.All.Where(p => !ship.Prepared.Contains(p)).Order(StringComparer.Ordinal))
            {
                OrderStormWork(work);
            }

            Acted(StandingOrderReasons.Preparing, ship.NextWeatherId ?? ship.WeatherId);
        }
        else if (!StormComing)
        {
            ship.Unflag(StandingOrderReasons.Preparing);
        }

        if (orders.AllHandsAtBilge is { } bilge)
        {
            if (ship.Bilge > bilge && !ship.AllHands && ship.Flag(StandingOrderReasons.AllHands))
            {
                CallAllHands(true);
                Acted(StandingOrderReasons.AllHands, Number(ship.Bilge));
            }
            else if (ship.Bilge < Math.Min(10, bilge / 2) && ship.Unflag(StandingOrderReasons.AllHands) && ship.AllHands)
            {
                CallAllHands(false);
                Acted(StandingOrderReasons.Dismissed, Number(ship.Bilge));
            }
        }

        var (food, water) = SupplyForecast();
        var stores = Math.Min(food, water);
        if (orders.ShortRationsUnderDays is { } short_ && !Campaign.Rationing && stores < short_)
        {
            SetRationing(true);
            Acted(StandingOrderReasons.Rations, Number(stores));
        }

        // The calls: each once as its condition comes, and again only after it has passed.
        Call(StandingOrderReasons.WakeStores, orders.WakeStoresUnderDays is { } low && stores < low,
            orders.WakeStoresUnderDays is not { } back || stores > back + 2, Number(stores), food < water ? "food" : "water");
        Call(StandingOrderReasons.WakeBilge, orders.WakeBilgeOver is { } high && ship.Bilge > high,
            orders.WakeBilgeOver is not { } clear || ship.Bilge < clear - 10, Number(ship.Bilge));
        var doubt = ReckoningDoubt();
        var worst = Math.Max(doubt.Across, doubt.Along);
        Call(StandingOrderReasons.WakeDoubt, orders.WakeDoubtOver is { } far && worst > far,
            orders.WakeDoubtOver is not { } near || worst < near - 3, Number(worst), doubt.Across > doubt.Along ? "across" : "along");
        Call(StandingOrderReasons.WakeNoon, orders.WakeAtNoon && CanTakeNoonSight, !CanTakeNoonSight, "");

        if (ship.Navigation is { } navigation)
        {
            ship.WindNoted ??= navigation.WindFrom;
            var shift = Math.Abs(Navigation.Wrap(navigation.WindFrom - ship.WindNoted.Value));
            if (orders.WakeWindShift is { } turn && shift > turn)
            {
                Campaign.Record(ChronicleEntryKind.CaptainCalled, [
                    new(ChronicleParameters.Reason, StandingOrderReasons.WakeWind), new(ChronicleParameters.Level, Number(shift)),
                    new(ChronicleParameters.To, CompassPoint(navigation.WindFrom)),
                ]);
                ship.WindNoted = navigation.WindFrom;
            }
        }

        void Call(string reason, bool now, bool passed, string level, string? what = null)
        {
            if (now && ship.Flag(reason))
            {
                Campaign.Record(ChronicleEntryKind.CaptainCalled, [
                    new(ChronicleParameters.Reason, reason), new(ChronicleParameters.Level, level), new(ChronicleParameters.Need, what),
                ]);
            }
            else if (!now && passed)
            {
                ship.Unflag(reason);
            }
        }
    }

    private void Acted(string reason, string? detail) =>
        Campaign.Record(ChronicleEntryKind.StandingOrderActed, [new(ChronicleParameters.Reason, reason), new(ChronicleParameters.To, detail)]);
}
