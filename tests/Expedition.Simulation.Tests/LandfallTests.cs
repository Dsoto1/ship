using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Voyage;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>The landfall approach: the bank and the lead, raising the coast, knowing it, the wrong landfall.</summary>
public sealed class LandfallTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
    private static readonly CoastRules Coast = Shipped.Voyage!.Route!.Coast!;

    // A ship put off the far coast: offshore leagues out, north leagues along it from the destination,
    // with a fair wind from astern that does not change, and the reckoning right.
    private static (SimulationRunner Runner, VoyageState Ship, Navigation Navigation) OffTheCoast(double offshore, double north, bool withDuties = true)
    {
        var runner = TestDeckScenario.Create(Shipped, CampaignState.DefaultSeed, withDuties);
        var ship = runner.Campaign.Voyage!;
        ship.WeatherId = "weather.fair";
        ship.WeatherUntilTick = long.MaxValue;
        var navigation = ship.Navigation!;
        navigation.WindFrom = 90;
        navigation.RunningDown = true;
        navigation.NearWarned = offshore <= Coast.NearLeagues;
        navigation.True = navigation.Reckoned = new ChartPoint(navigation.Destination.X + offshore, navigation.Destination.Y + north);
        return (runner, ship, navigation);
    }

    private static void AdvanceUntil(SimulationRunner runner, Func<bool> done, int hours = 72)
    {
        for (var i = 0; i < hours * 4 && !done(); i++)
        {
            runner.Advance(15);
        }
    }

    private static IEnumerable<ChronicleEntry> Entries(SimulationRunner runner, ChronicleEntryKind kind) =>
        runner.Campaign.Chronicle.Entries.Where(e => e.Kind == kind);

    [Fact]
    public void TheBankShoalsTowardsTheCoastAndTheDeepSeaLineFindsNoBottomBeyondIt()
    {
        Assert.Null(Coast.DepthAt(Coast.ShelfLeagues + 1));
        var depths = new[] { 24.0, 15, 8, 3, 0.5 }.Select(d => Coast.DepthAt(d)!.Value).ToList();
        Assert.Equal(depths.OrderByDescending(d => d), depths);
        Assert.InRange(depths[^1], Coast.ShoreFathoms, 10);
        Assert.Equal("shell and coarse sand", Coast.BottomAt(1));
        Assert.Equal("coral and broken rock", Coast.BottomAt(-12));
    }

    [Fact]
    public void NearingTheCoastSheIsWarnedTheLeadFindsTheBankAndTheMastheadRaisesIt()
    {
        var (runner, _, navigation) = OffTheCoast(35, 2);
        var crew = runner.Campaign.Characters.Keys.Order().ToList();
        Assert.True(runner.SetNavigator(crew[1]).Succeeded);

        AdvanceUntil(runner, () => navigation.Sighted);

        Assert.True(navigation.Sighted);
        Assert.Single(Entries(runner, ChronicleEntryKind.CoastNear));
        Assert.Single(Entries(runner, ChronicleEntryKind.LandSighted));
        var found = navigation.Soundings.Where(s => s.Fathoms is not null).ToList();
        Assert.True(found.Count >= 2, $"{navigation.Soundings.Count} casts");
        Assert.True(found[^1].Fathoms < found[0].Fathoms);
        Assert.Contains(navigation.Soundings, s => s.Fathoms is null);
        // Raised, she knows her distance off and stands in to her offing; Punta Ceniza is in view.
        Assert.Equal(navigation.True.X, navigation.Reckoned.X, 6);
        Assert.Equal("coast.punta_ceniza", navigation.Seen);
        Assert.True(navigation.Pending);
        Assert.Equal(0, navigation.CoastDirection);
    }

    [Fact]
    public void KnowingTheCoastRightlySheRunsAlongItAndArrives()
    {
        var (runner, ship, navigation) = OffTheCoast(4, 15);
        AdvanceUntil(runner, () => navigation.Sighted);
        Assert.Equal("coast.bajos_largos", navigation.Seen);

        Assert.True(runner.IdentifyCoast("coast.bajos_largos").Succeeded);
        Assert.Equal(-1, navigation.CoastDirection);
        AdvanceUntil(runner, () => ship.Outcome != VoyageOutcome.UnderWay);

        Assert.Equal(VoyageOutcome.Arrived, ship.Outcome);
        Assert.Equal(CampaignPhase.Landfall, runner.Campaign.Phase);
        Assert.Contains(Entries(runner, ChronicleEntryKind.LandmarkSighted), e => e.Get(ChronicleParameters.To) == "coast.punta_ceniza");
    }

    [Fact]
    public void TakingTheFalsePointForCenizaSheFindsNoRuinThereAndMustThinkAgain()
    {
        var (runner, ship, navigation) = OffTheCoast(4, -13);
        AdvanceUntil(runner, () => navigation.Sighted);
        Assert.Equal("coast.punta_falsa", navigation.Seen);

        Assert.True(runner.IdentifyCoast("coast.punta_ceniza").Succeeded);
        AdvanceUntil(runner, () => Entries(runner, ChronicleEntryKind.NotTheDestination).Any(), hours: 12);

        Assert.Single(Entries(runner, ChronicleEntryKind.NotTheDestination));
        Assert.Equal(VoyageOutcome.UnderWay, ship.Outcome);
        Assert.Null(navigation.Identified);
        Assert.True(navigation.Pending);

        // Known for what it is, she runs north for Ceniza and arrives.
        Assert.True(runner.IdentifyCoast("coast.punta_falsa").Succeeded);
        Assert.Equal(1, navigation.CoastDirection);
        AdvanceUntil(runner, () => ship.Outcome != VoyageOutcome.UnderWay);
        Assert.Equal(VoyageOutcome.Arrived, ship.Outcome);
    }

    [Fact]
    public void ACoastTakenForTheWrongMarkSendsHerTheWrongWay()
    {
        var (runner, _, navigation) = OffTheCoast(4, 16);
        AdvanceUntil(runner, () => navigation.Sighted);

        // The Bajos Largos taken for the Manglares: Ceniza must be north, so north she goes.
        Assert.True(runner.IdentifyCoast("coast.manglares").Succeeded);
        Assert.Equal(1, navigation.CoastDirection);
        AdvanceUntil(runner, () => navigation.Seen == "coast.rio_bermejo", hours: 24);
        Assert.Equal("coast.rio_bermejo", navigation.Seen);
        Assert.True(navigation.Pending);
        Assert.True(navigation.True.Y > navigation.Destination.Y + 20);
    }

    [Fact]
    public void LeftWithoutAWordTheBestHandAboardMakesTheCall()
    {
        var (runner, _, navigation) = OffTheCoast(4, 1);
        AdvanceUntil(runner, () => navigation.Sighted);
        Assert.True(navigation.Pending);
        Assert.NotNull(navigation.Opinion);
        var by = navigation.OpinionBy;
        Assert.NotNull(by);
        Assert.NotEqual(runner.Campaign.CaptainId, by);

        runner.Advance((int)(Coast.DecideAfterHours * 60) + 5);

        var decided = Assert.Single(Entries(runner, ChronicleEntryKind.CoastIdentified));
        Assert.Equal(by, decided.Characters.Single());
        Assert.NotEqual(0, navigation.CoastDirection);
    }

    [Fact]
    public void WithNoLookoutAtNightSheRunsOntoTheCoastUnseen()
    {
        var (runner, ship, navigation) = OffTheCoast(30, 0, withDuties: false);
        // To midnight, then put her a league off.
        runner.Advance((int)((SimulationClock.TicksPerDay - runner.Campaign.Clock.Tick % SimulationClock.TicksPerDay) % SimulationClock.TicksPerDay));
        navigation.True = navigation.Reckoned = new ChartPoint(navigation.Destination.X + 1, navigation.Destination.Y);
        var hull = ship.Hull;
        Assert.False(ship.WatchKept);

        AdvanceUntil(runner, () => navigation.Sighted, hours: 4);

        var struck = Assert.Single(Entries(runner, ChronicleEntryKind.StruckTheCoast));
        Assert.True(ship.Hull <= hull - Coast.StrikeDamage + 1);
        Assert.Empty(Entries(runner, ChronicleEntryKind.LandSighted));
        Assert.NotNull(struck);
    }

    [Fact]
    public void TheApproachSurvivesSaveAndContinuesIdentically()
    {
        var (runner, _, navigation) = OffTheCoast(12, 15);
        var crew = runner.Campaign.Characters.Keys.Order().ToList();
        runner.SetNavigator(crew[1]);
        AdvanceUntil(runner, () => navigation.Sighted);
        Assert.True(navigation.Soundings.Count > 0);

        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));

        runner.Advance(6 * 60);
        loaded.Advance(6 * 60);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
