using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Knowledge;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Voyage;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Navigation: dead reckoning, the set of the sea, the noon sight, landfall.</summary>
public sealed class NavigationTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static (SimulationRunner Runner, VoyageState Ship, Navigation Navigation, List<CharacterId> Crew) Setup(ulong seed = CampaignState.DefaultSeed)
    {
        var runner = TestDeckScenario.Create(Shipped, seed, withDuties: true);
        var ship = runner.Campaign.Voyage!;
        return (runner, ship, ship.Navigation!, runner.Campaign.Characters.Keys.Order().ToList());
    }

    // Advances to the given hour of the day (today, or tomorrow if past).
    private static void AdvanceToHour(SimulationRunner runner, double hour)
    {
        var target = (long)(hour * SimulationClock.TicksPerHour);
        var now = runner.Campaign.Clock.Tick % SimulationClock.TicksPerDay;
        runner.Advance((int)((target - now + SimulationClock.TicksPerDay) % SimulationClock.TicksPerDay));
    }

    [Fact]
    public void SheSetsOutForTheEmberCoastOnHerCourse()
    {
        var (_, ship, navigation, _) = Setup();
        Assert.InRange(navigation.Heading, 200, 240);
        Assert.Equal(ship.TotalLeagues, navigation.Destination.DistanceTo(new ChartPoint(0, 0)), 3);
        Assert.True(navigation.Destination.X < 0 && navigation.Destination.Y < 0);
        Assert.StartsWith("S", SimulationRunner.CompassPoint(navigation.Heading), StringComparison.Ordinal);
        Assert.Single(navigation.Track);
    }

    [Fact]
    public void WithoutSightsTheReckoningDriftsFromTheTruth()
    {
        var (runner, _, navigation, _) = Setup();
        runner.Advance(10 * 24 * 60);
        Assert.True(navigation.True.DistanceTo(navigation.Reckoned) > 1, $"{navigation.True} vs {navigation.Reckoned}");
        Assert.True(navigation.Track.Count >= 6);
        Assert.Empty(navigation.Sights);
    }

    [Fact]
    public void TheNoonSightPutsTheLatitudeRightOnceADayAtNoon()
    {
        var (runner, _, navigation, crew) = Setup();
        runner.Advance(4 * 24 * 60);
        AdvanceToHour(runner, 10);
        var captain = runner.Campaign.CaptainId!.Value;
        Assert.False(runner.CanTakeNoonSight);
        Assert.False(runner.TakeNoonSight(captain, 1).Result.Succeeded);

        AdvanceToHour(runner, 12);
        Assert.True(runner.CanTakeNoonSight);
        var before = Math.Abs(navigation.Reckoned.Y - navigation.True.Y);
        var (result, sight) = runner.TakeNoonSight(captain, 1);
        Assert.True(result.Succeeded);
        Assert.Equal(navigation.Reckoned.Y, sight!.Y, 6);
        Assert.InRange(Math.Abs(navigation.Reckoned.Y - navigation.True.Y), 0, 12);
        Assert.False(runner.TakeNoonSight(captain, 1).Result.Succeeded);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.NoonSight);
        Assert.StartsWith("Noon sight by", new ChronicleFormatter(runner.Campaign, Shipped).Format(runner.Campaign.Chronicle.Entries.Last(e => e.Kind == ChronicleEntryKind.NoonSight)), StringComparison.Ordinal);
        _ = before;
    }

    [Fact]
    public void AnAppointedNavigatorTakesTheSightAndShowsTheirSkill()
    {
        var (runner, _, navigation, crew) = Setup();
        var navigator = crew.First(id => id != runner.Campaign.CaptainId);
        Assert.True(runner.SetNavigator(navigator).Succeeded);
        runner.Advance(2 * 24 * 60);

        Assert.True(navigation.Sights.Count >= 2);
        Assert.All(navigation.Sights, s => Assert.Equal(navigator, s.Taker));
        Assert.Contains(runner.Campaign.Knowledge.About(navigator), e => e.Topic == "skill.navigation" && e.Source == KnowledgeSource.Observation);
    }

    [Fact]
    public void CarefulSightsBringHerToTheCoastCloserToHerMark()
    {
        double Landing(bool sights, ulong seed)
        {
            var (runner, ship, navigation, crew) = Setup(seed);
            if (sights)
            {
                runner.SetNavigator(runner.Campaign.CaptainId);
            }

            while (!navigation.Landed && ship.Outcome == VoyageOutcome.UnderWay && runner.Campaign.Clock.Day < 45)
            {
                runner.Advance(60);
            }

            return navigation.Landed ? Math.Abs(navigation.True.Y - navigation.Destination.Y) : double.NaN;
        }

        var careful = Enumerable.Range(1, 6).Select(s => Landing(true, (ulong)s)).Where(d => !double.IsNaN(d)).Average();
        var blind = Enumerable.Range(1, 6).Select(s => Landing(false, (ulong)s)).Where(d => !double.IsNaN(d)).Average();
        Assert.True(careful < blind, $"with sights {careful:0.0} leagues off, without {blind:0.0}");
    }

    [Fact]
    public void NavigationSurvivesSaveAndOlderVoyagesArePlacedOnTheRoute()
    {
        var (runner, _, navigation, _) = Setup(5);
        runner.SetNavigator(runner.Campaign.CaptainId);
        runner.Advance(3 * 24 * 60 + 400);
        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = SaveSerializer.Deserialize(json, Shipped).Voyage!.Navigation!;
        Assert.Equal(navigation.True, loaded.True);
        Assert.Equal(navigation.Reckoned, loaded.Reckoned);
        Assert.Equal(navigation.Sights.Count, loaded.Sights.Count);
        Assert.Equal(navigation.Navigator, loaded.Navigator);

        var old = JsonNode.Parse(json)!.AsObject();
        old["schemaVersion"] = 25;
        old["voyage"]!.AsObject().Remove("navigation");
        old["randomStreams"]!.AsObject().Remove(RandomStreams.NavigationName);
        var placed = SaveSerializer.Deserialize(old.ToJsonString(), Shipped).Voyage!;
        Assert.Equal(placed.LeaguesSailed, placed.Navigation!.True.DistanceTo(new ChartPoint(0, 0)), 3);
        Assert.Equal(placed.Navigation.True, placed.Navigation.Reckoned);
    }

    [Theory]
    [InlineData(0, "N")]
    [InlineData(225, "SW")]
    [InlineData(213.75, "SW by S")]
    [InlineData(270, "W")]
    public void CoursesReadAsPointsOfTheCompass(double degrees, string point) => Assert.Equal(point, SimulationRunner.CompassPoint(degrees));

    [Fact]
    public void LatitudesReadInDegreesAndMinutes()
    {
        var (runner, _, _, _) = Setup();
        Assert.Equal("36°30′ N", runner.Latitude(0));
        Assert.Equal("35°30′ N", runner.Latitude(-17.5));
    }
}
