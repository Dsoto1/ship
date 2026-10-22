using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Shore;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>R6: the landfall camp, the first 72 hours (docs/R6_LANDFALL_CAMP.md).</summary>
public sealed class CampTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Landed(string site = "coast.punta_ceniza", ulong seed = CampaignState.DefaultSeed)
    {
        var runner = TestDeckScenario.Create(Shipped, seed);
        runner.Advance(60);
        Assert.True(runner.DeveloperLand(site).Succeeded);
        return runner;
    }

    private static int People(SimulationRunner runner) =>
        runner.Campaign.Crew.Count(c => c.SurvivalState is Characters.SurvivalState.Alive or Characters.SurvivalState.Incapacitated);

    [Fact]
    public void AtAnchorEverythingIsStillAboardAndTheBoatLandsIt()
    {
        var runner = Landed();
        var camp = runner.Campaign.Camp!;
        Assert.True(camp.ShipAtAnchor);
        Assert.True(camp.Afloat.Count > 0);
        // Her spare sail: brought in with the first load, for the shelters.
        Assert.Equal(6, runner.Campaign.SharedStore.Count("item.sailcloth") + camp.Afloat.GetValueOrDefault("item.sailcloth"));
        Assert.True(runner.Campaign.SharedStore.Count("item.sailcloth") >= 2);

        // The first boatload came in with them: no more than a boat carries, and not all of it water.
        var ashore = Shipped.Items.Values.Sum(i => runner.Campaign.SharedStore.Count(i.Id) * i.WeightKg);
        var water = Shipped.Items.Values.Where(i => i.Category == "water").Sum(i => runner.Campaign.SharedStore.Count(i.Id) * i.WeightKg);
        Assert.InRange(ashore, 1, Shipped.Shore!.Rules.BoatTripKg);
        Assert.True(water <= Shipped.Shore.Rules.BoatTripKg / 2 + 1);
        Assert.All(runner.Campaign.Crew.Where(c => c.SurvivalState == Characters.SurvivalState.Alive),
            c => Assert.Equal(SimulationRunner.CampLocation, runner.Campaign.Placements[c.Id].LocationId));

        runner.Advance(24 * 60);
        Assert.Empty(camp.Afloat);
        Assert.Single(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.CampAllLanded);
    }

    [Fact]
    public void AfterAWreckWhatTheBoatSavedIsAshore()
    {
        var runner = TestDeckScenario.Create(Shipped);
        runner.Advance(60);
        Assert.True(runner.DeveloperFounder().Succeeded);
        Assert.True(runner.CanGoAshore);
        Assert.True(runner.GoAshore().Succeeded);
        var camp = runner.Campaign.Camp!;
        Assert.False(camp.ShipAtAnchor);
        Assert.Equal(runner.Campaign.Wreck!.Ashore, camp.Site);
        // The boat's sail comes ashore with them; she went down at sea, so nothing is left to land.
        Assert.Equal(2, runner.Campaign.SharedStore.Count("item.sailcloth"));
        Assert.Empty(camp.Afloat);
        Assert.False(runner.CanGoAshore);
        Assert.False(runner.GoAshore().Succeeded);
    }

    [Fact]
    public void TheHandsMakeTheCampAndBuildNoMoreShelterThanIsWanted()
    {
        var runner = Landed();
        runner.Advance(24 * 60);
        var camp = runner.Campaign.Camp!;
        var people = People(runner);
        Assert.InRange(camp.Work(CampWorks.Shelter), people, people + Shipped.Shore!.Rules.SailBerths - 1);
        Assert.Equal(1, camp.Work(CampWorks.Fire));
        Assert.Equal(1, camp.Work(CampWorks.Latrine));
        Assert.Equal(1, camp.Work(CampWorks.Cover));
        Assert.Equal(people, runner.Sheltered().Count);
        // Sail shelters, while there was cloth and the captain allowed it.
        Assert.True(runner.Campaign.SharedStore.Count("item.sailcloth") < 6);
    }

    [Fact]
    public void KeepingTheSailclothMeansBrushShelters()
    {
        var runner = Landed();
        Assert.True(runner.SetCampChoices(new CampChoices { UseSailcloth = false, SendScouts = false }).Succeeded);
        runner.Advance(30 * 60);
        Assert.Equal(6, runner.Campaign.SharedStore.Count("item.sailcloth"));
        Assert.True(runner.Campaign.Camp!.Work(CampWorks.Shelter) >= People(runner));
        Assert.Empty(runner.Campaign.Chronicle.Entries.Where(e => e.Kind == ChronicleEntryKind.CampFound));
    }

    [Fact]
    public void WithNothingToBuildWithTheyLieOutInTheOpen()
    {
        // The Bajos Largos have no brush; with the sailcloth kept, there is no shelter to be had.
        var runner = Landed("coast.bajos_largos");
        runner.SetCampChoices(new CampChoices { UseSailcloth = false });
        runner.Advance(3 * 24 * 60);
        var camp = runner.Campaign.Camp!;
        Assert.Equal(0, camp.Work(CampWorks.Shelter));
        // Only the sick, if any, under the sick shelter's roof; everyone else out in it.
        Assert.True(camp.NightsSheltered.Count <= Shipped.Shore!.Rules.SickBerths);
        Assert.True(camp.NightsExposed.Count >= People(runner) - camp.NightsSheltered.Count);
        Assert.All(camp.NightsExposed.Values, nights => Assert.InRange(nights, 1, 4));
        Assert.Contains("shelter for all", runner.CampWanting());
    }

    [Fact]
    public void WhereNoWaterIsKnownScoutsFindItAndItIsFetched()
    {
        var runner = Landed("coast.bajos_largos");
        Assert.Null(runner.CampWater());
        Assert.Contains(runner.CampAssessment(), line => line.Topic == "Water" && !line.Known);
        for (var day = 0; day < 8 && runner.CampWater() is null; day++)
        {
            runner.Advance(24 * 60);
        }

        Assert.NotNull(runner.CampWater());
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.CampFound && e.Get(ChronicleParameters.Reason) == "water");
        Assert.Contains(runner.CampAssessment(), line => line.Topic == "Water" && line.Known);
    }

    [Fact]
    public void TheAssessmentSaysWhatIsKnownAndWhatIsGuessed()
    {
        var lines = Landed().CampAssessment();
        Assert.Contains(lines, l => l.Topic == "Water" && l.Known && l.Text.Contains("spring", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Topic == "People" && !l.Known);
        Assert.Contains(lines, l => l.Topic == "The burned factory" && !l.Known);
        Assert.Contains(lines, l => l.Topic == "The ship" && l.Text.StartsWith("At anchor", StringComparison.Ordinal));
    }

    [Fact]
    public void PrioritiesMustRankEveryOneOnce()
    {
        var runner = Landed();
        Assert.False(runner.SetCampPriorities([CampPriorities.Water]).Succeeded);
        Assert.False(runner.SetCampPriorities([.. CampPriorities.Default.Take(7), CampPriorities.Water]).Succeeded);
        var reversed = CampPriorities.Default.Reverse().ToList();
        Assert.True(runner.SetCampPriorities(reversed).Succeeded);
        var captain = runner.Campaign.CaptainId!.Value;
        Assert.Equal("task.scout", runner.Campaign.DutiesOf(captain)[0]);
    }

    [Fact]
    public void AfterThreeDaysTheFirstDaysAreReportedAndTheSettlementBegins()
    {
        var runner = Landed();
        runner.Advance(71 * 60);
        Assert.False(runner.Campaign.Camp!.Reported);
        runner.Advance(2 * 60);
        Assert.True(runner.Campaign.Camp.Reported);
        Assert.Equal(CampaignPhase.SettlementSurvival, runner.Campaign.Phase);
        var report = runner.Campaign.Chronicle.Entries.Single(e => e.Kind == ChronicleEntryKind.CampFirstDays);
        Assert.StartsWith("Three days ashore", new ChronicleFormatter(runner.Campaign, runner.Content).Format(report), StringComparison.Ordinal);
    }

    [Fact]
    public void TheCampSurvivesSaveAndContinuesIdentically()
    {
        var runner = Landed("coast.bajos_largos");
        runner.SetCampChoices(new CampChoices { Forage = false });
        runner.Advance(19 * 60 + 7);
        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.False(loaded.Campaign.Camp!.Choices.Forage);

        runner.Advance(30 * 60);
        loaded.Advance(30 * 60);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
