using System.Globalization;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// The landfall approach (NAVIGATION §14, §15, §30, §31). Nearing the coast by the reckoning, the captain
/// is told; signs of land show; the lead finds the bank and its depth shoals as she closes. The masthead
/// raises the coast (far off by day, only the surf at night, hardly at all with no lookout; unseen, she
/// strikes it). Then the coast must be known: each landmark that comes into view waits for the captain
/// to say what it is, and the best hand aboard says what they make of it. She runs along the coast to
/// where the captain takes the destination to be, and has arrived only if it is truly there.
/// </summary>
public sealed partial class SimulationRunner
{
    private static readonly string[] LandSigns = ["birds", "weed", "water", "cloud"];

    // One minute of the approach, after she has sailed. True on arrival.
    private bool Approach(VoyageState ship, Navigation navigation, RouteRules route, CoastRules coast, WeatherDefinition weather)
    {
        var random = Campaign.Randomness.Navigation;
        var minute = Campaign.Clock.Tick % SimulationClock.TicksPerDay;
        if (!navigation.Sighted)
        {
            var reckonedOff = navigation.Reckoned.X - navigation.Destination.X;
            if (!navigation.NearWarned && reckonedOff <= coast.NearLeagues)
            {
                navigation.NearWarned = true;
                Campaign.Record(ChronicleEntryKind.CoastNear, [new(ChronicleParameters.Level, Number(Math.Max(reckonedOff, 0)))]);
            }

            if (minute % SimulationClock.TicksPerHour == 0 && ship.WatchKept && navigation.Offshore <= coast.LandSignLeagues
                && random.Chance(coast.LandSignChancePerHour))
            {
                Campaign.Record(ChronicleEntryKind.LandSign, [new(ChronicleParameters.To, LandSigns[random.NextInt(LandSigns.Length)])]);
            }

            // The navigator keeps the lead going once the reckoning puts her on the bank.
            if (navigation.Navigator is { } navigator && reckonedOff <= coast.ShelfLeagues + 5
                && (navigation.LastSoundingTick < 0 || Campaign.Clock.Tick - navigation.LastSoundingTick >= coast.SoundEveryHours * SimulationClock.TicksPerHour))
            {
                HeaveTheLead(navigator);
            }

            var hour = Campaign.Clock.TimeOfDay.TotalHours;
            var range = (hour is >= 6 and < 19 ? coast.SightingLeagues * (1 - 0.6 * weather.Gloom) : coast.NightSightingLeagues)
                        * (ship.WatchKept ? 1 : coast.NoLookout);
            if (navigation.Offshore <= coast.StrikeLeagues && range < navigation.Offshore + 0.01)
            {
                // Nobody saw it: she is on the reef before anyone knows.
                ship.Apply(new ShipEffects { Hull = -coast.StrikeDamage }, 1);
                Campaign.Record(ChronicleEntryKind.StruckTheCoast, [
                    new(ChronicleParameters.Damage, Number(coast.StrikeDamage)), new(ChronicleParameters.Level, Number(ship.Hull)),
                ]);
                if (ship.Hull <= 0)
                {
                    // She broke her back on it.
                    ship.Outcome = VoyageOutcome.Foundered;
                    Wreck(ship, Content.Voyage!, "struck");
                    return false;
                }

                Raise(navigation, coast);
            }
            else if (navigation.Offshore <= range)
            {
                Campaign.Record(ChronicleEntryKind.LandSighted, [new(ChronicleParameters.Level, navigation.Offshore.ToString("0.0", CultureInfo.InvariantCulture))]);
                Raise(navigation, coast);
            }

            return false;
        }

        var view = coast.InView(navigation.True.Y - navigation.Destination.Y);
        if (view is not null && view.Id != navigation.Seen)
        {
            navigation.Seen = view.Id;
            navigation.RuledOut = null;
            navigation.Pending = true;
            navigation.PendingHours = 0;
            (navigation.OpinionBy, navigation.Opinion) = OpinionOf(navigation, coast, view);
            Campaign.Record(ChronicleEntryKind.LandmarkSighted, [new(ChronicleParameters.To, view.Id)]);
        }
        else if (view is null && navigation.Seen is not null)
        {
            // Past it, along a plain stretch.
            navigation.Seen = null;
            navigation.RuledOut = null;
            navigation.Pending = false;
        }

        if (navigation.Pending || navigation.CoastDirection == 0)
        {
            navigation.PendingHours += HoursPerTick;
            if (navigation.PendingHours >= coast.DecideAfterHours)
            {
                // Nobody has said; the best hand aboard makes the call and she stands on.
                if (navigation.Pending && navigation.Opinion is { } opinion && view is not null)
                {
                    Identify(navigation, coast, opinion, navigation.OpinionBy);
                }
                else if (navigation.CoastDirection == 0)
                {
                    navigation.CoastDirection = navigation.Reckoned.Y > navigation.Destination.Y ? -1 : 1;
                    navigation.PendingHours = 0;
                }
            }
        }

        var there = Math.Abs(navigation.True.Y - navigation.Destination.Y) <= route.LandfallTolerance;
        var knows = Math.Abs(navigation.Reckoned.Y - navigation.True.Y) <= route.LandfallTolerance;
        if (there && knows && navigation.Identified is not null)
        {
            return true;
        }

        if (navigation.Identified is not null && navigation.CoastDirection != 0 && Math.Abs(navigation.Reckoned.Y - navigation.Destination.Y) < 1e-6)
        {
            // Where the destination should be, it is not: no ruin, no lagoon. Think again.
            Campaign.Record(ChronicleEntryKind.NotTheDestination, [new(ChronicleParameters.To, coast.Destination)]);
            navigation.Identified = null;
            navigation.RuledOut = view?.Id;
            navigation.CoastDirection = 0;
            navigation.PendingHours = 0;
            navigation.Pending = view is not null;
            if (view is not null)
            {
                (navigation.OpinionBy, navigation.Opinion) = OpinionOf(navigation, coast, view);
            }
        }

        return false;
    }

    // The coast is raised: from now the distance off is known, and she stands in to her offing.
    private void Raise(Navigation navigation, CoastRules coast)
    {
        navigation.Sighted = true;
        navigation.CoastDirection = 0;
        navigation.PendingHours = 0;
        navigation.Reckoned = navigation.Reckoned with { X = navigation.True.X };
        if (coast.InView(navigation.True.Y - navigation.Destination.Y) is { } view)
        {
            navigation.Seen = view.Id;
            navigation.Pending = true;
            (navigation.OpinionBy, navigation.Opinion) = OpinionOf(navigation, coast, view);
            Campaign.Record(ChronicleEntryKind.LandmarkSighted, [new(ChronicleParameters.To, view.Id)]);
        }
    }

    // What the best hand aboard (the navigator, else whoever knows navigation best) takes a landmark for:
    // what it looks like, weighed against where the reckoning puts her along the coast. The reckoned
    // latitude is good to a few leagues just after a sight or a known landmark, worse with each day since;
    // the more they know, the less a look-alike fools them. Never a mark standing in has ruled out.
    private (CharacterId?, string?) OpinionOf(Navigation navigation, CoastRules coast, Landmark view)
    {
        var who = navigation.Navigator is { } appointed && Campaign.Characters.TryGetValue(appointed, out var navigator) && navigator.CanWork
            ? navigator
            : Campaign.Crew.Where(c => c.CanWork && c.Id != Campaign.CaptainId).OrderByDescending(c => c.Skills.Get("skill.navigation")).ThenBy(c => c.Id.Value).FirstOrDefault();
        if (who is null)
        {
            return (null, null);
        }

        var random = Campaign.Randomness.Navigation;
        var skill = who.Skills.Get("skill.navigation");
        var since = navigation.Identified is not null ? 0
            : (Campaign.Clock.Day - (navigation.LastSightDay >= 0 ? navigation.LastSightDay : 0)) + (navigation.LastSightDay >= 0 ? 0 : 6);
        var doubt = 3 + 2.5 * since;
        var reckoned = navigation.Reckoned.Y - navigation.Destination.Y;
        var confusion = Math.Max(0.05, 0.6 - 0.1 * skill);
        var best = coast.Landmarks
            .Where(l => !(l.Id == coast.Destination && navigation.RuledOut == view.Id))
            .Select(l =>
            {
                var looks = l.Id == view.Id ? 1 : l.Id == view.Like || l.Like == view.Id ? confusion : confusion * 0.2;
                var place = Math.Exp(-Math.Pow(l.North - reckoned, 2) / (2 * doubt * doubt));
                return (Landmark: l, Score: looks * (place + 0.02) * (0.75 + 0.5 * random.NextDouble()));
            })
            .OrderByDescending(c => c.Score).ThenBy(c => c.Landmark.Id, StringComparer.Ordinal).FirstOrDefault();
        return (who.Id, best.Landmark?.Id);
    }

    // Takes the coast in view for a landmark: the reckoning goes there (as far off it as the view truly
    // is), and she runs for where that puts the destination.
    private void Identify(Navigation navigation, CoastRules coast, string landmarkId, CharacterId? by)
    {
        var chosen = coast.Find(landmarkId)!;
        var view = coast.InView(navigation.True.Y - navigation.Destination.Y);
        var offset = view is null ? 0 : navigation.True.Y - (navigation.Destination.Y + view.North);
        navigation.Identified = chosen.Id;
        navigation.Reckoned = navigation.Reckoned with { Y = navigation.Destination.Y + chosen.North + offset };
        navigation.Pending = false;
        navigation.PendingHours = 0;
        var still = navigation.Destination.Y - navigation.Reckoned.Y;
        navigation.CoastDirection = Math.Abs(still) < 1e-6 ? 1 : Math.Sign(still);
        Campaign.Record(ChronicleEntryKind.CoastIdentified, [new(ChronicleParameters.To, chosen.Id)], by is { } who ? [who] : []);
    }

    /// <summary>
    /// Developer tool for testing and screenshots: puts her <paramref name="offshore"/> leagues off the far
    /// coast and <paramref name="north"/> leagues along it from the destination, the reckoning right, running down.
    /// </summary>
    public CommandResult DeveloperPlaceOffCoast(double offshore, double north)
    {
        if (Campaign.Voyage?.Navigation is not { Sighted: false } navigation || Coast is not { } coast)
        {
            return CommandResult.Rejected("There is no voyage short of the coast.");
        }

        navigation.True = navigation.Reckoned = new ChartPoint(navigation.Destination.X + Math.Max(offshore, 0.5), navigation.Destination.Y + north);
        navigation.RunningDown = true;
        navigation.NearWarned = offshore <= coast.NearLeagues;
        return CommandResult.Ok();
    }

    /// <summary>The far coast's rules, if the route has one.</summary>
    public CoastRules? Coast => Content.Voyage?.Route?.Coast;

    /// <summary>
    /// The captain says what the coast in view is: the reckoning is put there, and she runs along the coast
    /// for where that puts the destination. Rightly or wrongly: the sailing directions are all he has.
    /// </summary>
    public CommandResult IdentifyCoast(string landmarkId)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay, Navigation: { Sighted: true } navigation } || Coast is not { } coast)
        {
            return CommandResult.Rejected("There is no coast in sight to know.");
        }

        if (coast.Find(landmarkId) is null)
        {
            return CommandResult.Rejected("That is not on the chart.");
        }

        if (coast.InView(navigation.True.Y - navigation.Destination.Y) is null)
        {
            return CommandResult.Rejected("There is nothing on this stretch of coast to know it by.");
        }

        Identify(navigation, coast, landmarkId, Campaign.CaptainId);
        return CommandResult.Ok();
    }

    /// <summary>With the coast in sight, the captain orders her along it north (+1) or south (-1) by eye, whatever he took it for.</summary>
    public CommandResult CoastAlong(int direction)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay, Navigation: { Sighted: true } navigation } || direction is not (1 or -1))
        {
            return CommandResult.Rejected("There is no coast in sight to run along.");
        }

        navigation.Identified = null;
        navigation.CoastDirection = direction;
        navigation.PendingHours = 0;
        return CommandResult.Ok();
    }

    /// <summary>
    /// Heaves the lead: the depth, if the deep-sea line finds bottom, and what the tallow brings up.
    /// Recorded on the chart at the reckoned place, and in the chronicle.
    /// </summary>
    public CommandResult HeaveTheLead(CharacterId who)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay, Navigation: { } navigation } || Coast is not { } coast)
        {
            return CommandResult.Rejected("The ship is not under way.");
        }

        if (!Campaign.Characters.TryGetValue(who, out var hand) || !hand.CanWork || !Campaign.Crew.Any(c => c.Id == who))
        {
            return CommandResult.Rejected("They cannot heave the lead now.");
        }

        var depth = coast.DepthAt(navigation.Offshore) is { } fathoms
            ? Math.Max(1, Math.Round(fathoms * (1 + (Campaign.Randomness.Navigation.NextDouble() - 0.5) * 0.2)))
            : (double?)null;
        var bottom = depth is null ? null : coast.BottomAt(navigation.True.Y - navigation.Destination.Y);
        navigation.Sound(new Sounding(Campaign.Clock.Tick, who, navigation.Reckoned, depth, bottom));
        Campaign.Record(ChronicleEntryKind.Sounding, [
            new(ChronicleParameters.Level, depth is { } d ? Number(d) : "none"), new(ChronicleParameters.To, bottom ?? ""),
        ], who);
        return CommandResult.Ok();
    }
}
