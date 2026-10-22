using System.Globalization;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Knowledge;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// Navigation at sea: the ship steers by her reckoning, the sea sets her off it, and the noon sight
/// puts the latitude right. The captain takes the sight himself (the astrolabe, in the game) or leaves
/// it to a navigator, whose skill shows in how true it comes out.
/// </summary>
public sealed partial class SimulationRunner
{
    // One minute on the route: sail, mark the reckoning at noon, a new log error at midnight, and the
    // navigator's sight if the captain has not taken it. True on arrival.
    private bool SailTheRoute(VoyageState ship, Navigation navigation, RouteRules route)
    {
        SteerForIsland(navigation);
        SteerForLeg(navigation);
        navigation.Sail(ship.SpeedLeaguesPerHour, HoursPerTick, route.Coast?.StandOffLeagues ?? 0);
        CallAtIsland(ship, navigation, Content.Voyage!.Weather(ship.WeatherId));
        ReachLeg(navigation);
        bool arrived;
        if (route.Coast is { } coast)
        {
            arrived = Approach(ship, navigation, route, coast, Content.Voyage!.Weather(ship.WeatherId));
        }
        else
        {
            // No coast charted: she knows it when she fetches it, and runs along it to the destination.
            if (!navigation.Sighted && navigation.Offshore <= 0)
            {
                navigation.Sighted = true;
                navigation.Identified = "coast";
                navigation.Reckoned = navigation.True;
                navigation.CoastDirection = navigation.True.Y > navigation.Destination.Y ? -1 : 1;
            }

            arrived = navigation.Sighted && Math.Abs(navigation.True.Y - navigation.Destination.Y) <= route.LandfallTolerance;
        }

        var hour = Campaign.Clock.TimeOfDay.TotalHours;
        var minute = Campaign.Clock.Tick % SimulationClock.TicksPerDay;
        if (minute == 0)
        {
            navigation.LogError = 1 + Gaussian(Campaign.Randomness.Navigation) * route.SpeedErrorSd;
        }

        if (minute == 12 * SimulationClock.TicksPerHour)
        {
            navigation.Mark();
        }

        // The last minute of the window: the captain has not taken it, so the navigator does.
        if (navigation.LastSightDay != Campaign.Clock.Day && minute == (long)(route.SightUntil * SimulationClock.TicksPerHour) - 1
            && navigation.Navigator is { } navigator && Campaign.Characters.TryGetValue(navigator, out var who) && who.CanWork)
        {
            // A navigator's care is their skill, a little better or worse on the day.
            var care = Math.Clamp(0.2 + 0.15 * who.Skills.Get("skill.navigation") + (Campaign.Randomness.Navigation.NextDouble() - 0.5) * 0.2, 0, 1);
            TakeNoonSight(navigator, care);
        }

        return arrived;
    }

    private void SetTheSea(Navigation navigation, RouteRules route, Voyage.WeatherDefinition weather)
    {
        var random = Campaign.Randomness.Navigation;
        var angle = random.NextDouble() * Math.Tau;
        var strength = route.CurrentVariation * random.NextDouble() * (1 + 0.5 * weather.SeaState);
        navigation.Drift = new ChartPoint(route.Current[0] + Math.Sin(angle) * strength, route.Current[1] + Math.Cos(angle) * strength);
        // Each spell brings its own wind, mostly from the prevailing quarter.
        navigation.WindFrom = ((route.WindFrom + Gaussian(random) * route.WindVariation) % 360 + 360) % 360;
    }

    private static double Gaussian(SimulationRandom random)
    {
        var u = Math.Max(random.NextDouble(), 1e-9);
        return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(Math.Tau * random.NextDouble());
    }

    /// <summary>True between the hours the sun can be taken at noon, if today's sight is not yet taken and the sun can be seen.</summary>
    public bool CanTakeNoonSight => NoonNow && !SunHidden;

    /// <summary>True between the hours of the noon sight, if today's is not yet taken, whether or not the sun shows.</summary>
    public bool NoonNow =>
        Campaign.Voyage is { Outcome: VoyageOutcome.UnderWay, Navigation: { Sighted: false } navigation } && Content.Voyage?.Route is { } route
        && navigation.LastSightDay != Campaign.Clock.Day
        && Campaign.Clock.TimeOfDay.TotalHours >= route.SightFrom && Campaign.Clock.TimeOfDay.TotalHours < route.SightUntil;

    /// <summary>True when fog or a storm's cloud hides the sun: there is no sight to be had.</summary>
    public bool SunHidden => Campaign.Voyage is { } ship && Content.Voyage is { } rules && rules.Weather(ship.WeatherId).SunHidden;

    /// <summary>
    /// Takes the sun at noon: <paramref name="care"/> (0..1) is how well it was done, from the astrolabe in
    /// the game or a navigator's skill. Puts the reckoned latitude right, within an error that grows with
    /// the sea running and shrinks with care. Recorded in the chronicle; a navigator's work is seen.
    /// </summary>
    public (CommandResult Result, NoonSight? Sight) TakeNoonSight(CharacterId taker, double care)
    {
        if (!CanTakeNoonSight || Campaign.Voyage?.Navigation is not { } navigation || Content.Voyage?.Route is not { } route)
        {
            return (CommandResult.Rejected(NoonNow && SunHidden ? "The sun is hidden: there is no sight to be had today."
                : "The noon sight is taken once a day, between half past eleven and half past twelve."), null);
        }

        if (!Campaign.Characters.TryGetValue(taker, out var who) || !who.CanWork)
        {
            return (CommandResult.Rejected("They cannot take the sight now."), null);
        }

        var weather = Content.Voyage.Weather(Campaign.Voyage.WeatherId);
        var spread = route.SightError * (1 + 0.6 * weather.SeaState) * (1.4 - Math.Clamp(care, 0, 1));
        var error = Gaussian(Campaign.Randomness.Navigation) * spread;
        var sight = new NoonSight(Campaign.Clock.Day, taker, navigation.True.Y + error, error);
        navigation.Sight(sight);
        Campaign.Record(ChronicleEntryKind.NoonSight, [
            new(ChronicleParameters.Level, Latitude(sight.Y)),
            new(ChronicleParameters.Need, spread.ToString("0.0", CultureInfo.InvariantCulture)),
        ], taker);
        if (taker != Campaign.CaptainId && Math.Abs(error) <= spread)
        {
            // A sight that agrees with the next days' run shows the navigator knows the work.
            Witness(who, "skill.navigation", who.Skills.Get("skill.navigation"), KnowledgeSource.Observation);
        }

        return (CommandResult.Ok(), sight);
    }

    /// <summary>Developer tool for testing and screenshots: this weather at sea, held for a day.</summary>
    public CommandResult DeveloperSetSeaWeather(string id)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship || Content.Voyage?.Weathers.FirstOrDefault(w => w.Id == id) is null)
        {
            return CommandResult.Rejected("No voyage, or no such weather.");
        }

        ship.WeatherId = id;
        ship.WeatherUntilTick = Campaign.Clock.Tick + SimulationClock.TicksPerDay;
        return CommandResult.Ok();
    }

    /// <summary>Developer tool for screenshots: the wind from <paramref name="from"/> degrees until the weather next changes.</summary>
    public CommandResult DeveloperSetWind(double from)
    {
        if (Campaign.Voyage?.Navigation is not { } navigation)
        {
            return CommandResult.Rejected("There is no voyage.");
        }

        navigation.WindFrom = ((from % 360) + 360) % 360;
        if (!navigation.Sighted)
        {
            navigation.Heading = navigation.Steer(navigation.CourseFrom(navigation.Reckoned));
        }

        return CommandResult.Ok();
    }

        /// <summary>Appoints who takes the noon sight when the captain does not (null: no one; he takes it or it is missed).</summary>
    public CommandResult SetNavigator(CharacterId? id)
    {
        if (Campaign.Voyage?.Navigation is not { } navigation)
        {
            return CommandResult.Rejected("There is no reckoning to keep.");
        }

        if (id is { } who && (!Campaign.Characters.TryGetValue(who, out var character) || character.SurvivalState != SurvivalState.Alive
                              || !Campaign.Crew.Any(c => c.Id == who)))
        {
            return CommandResult.Rejected("Only someone aboard can keep the reckoning.");
        }

        navigation.Navigator = id;
        return CommandResult.Ok();
    }

    /// <summary>A latitude for the chart and the log, from leagues north of the port: "36°30′ N".</summary>
    public string Latitude(double y)
    {
        var route = Content.Voyage?.Route;
        var degrees = (route?.StartLatitude ?? 0) + y / (route?.LeaguesPerDegree ?? 17.5);
        var whole = (int)Math.Floor(Math.Abs(degrees));
        var minutes = (int)Math.Round((Math.Abs(degrees) - whole) * 60);
        if (minutes == 60)
        {
            (whole, minutes) = (whole + 1, 0);
        }

        return $"{whole}°{minutes:00}′ {(degrees >= 0 ? "N" : "S")}";
    }

    /// <summary>A longitude in the same way: "12°05′ W".</summary>
    public string Longitude(double x, double y)
    {
        var route = Content.Voyage?.Route;
        var latitude = ((route?.StartLatitude ?? 0) + y / (route?.LeaguesPerDegree ?? 17.5)) * Math.PI / 180;
        var degrees = (route?.StartLongitude ?? 0) + x / ((route?.LeaguesPerDegree ?? 17.5) * Math.Max(Math.Cos(latitude), 0.2));
        var whole = (int)Math.Floor(Math.Abs(degrees));
        var minutes = (int)Math.Round((Math.Abs(degrees) - whole) * 60);
        if (minutes == 60)
        {
            (whole, minutes) = (whole + 1, 0);
        }

        return $"{whole}°{minutes:00}′ {(degrees >= 0 ? "E" : "W")}";
    }

    /// <summary>The course steered, as a point of the compass: "SW by S".</summary>
    public static string CompassPoint(double degrees)
    {
        string[] points =
        [
            "N", "N by E", "NNE", "NE by N", "NE", "NE by E", "ENE", "E by N", "E", "E by S", "ESE", "SE by E", "SE", "SE by S", "SSE", "S by E",
            "S", "S by W", "SSW", "SW by S", "SW", "SW by W", "WSW", "W by S", "W", "W by N", "WNW", "NW by W", "NW", "NW by N", "NNW", "N by W",
        ];
        return points[(int)Math.Round(((degrees % 360) + 360) % 360 / 11.25) % 32];
    }
}
