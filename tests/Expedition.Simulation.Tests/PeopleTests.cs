using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Step P (docs/PEOPLE.md): people joining, households, children, the years.</summary>
public sealed class PeopleTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Landed(int days = 1)
    {
        var runner = TestDeckScenario.Create(Shipped, CampaignState.DefaultSeed);
        runner.Advance(60);
        Assert.True(runner.DeveloperLand("coast.punta_ceniza").Succeeded);
        for (var hour = 0; hour < days * 24; hour++)
        {
            runner.Advance(60);
            runner.DeveloperDismissMeeting();
        }

        return runner;
    }

    private static void Days(SimulationRunner runner, int days, Func<bool> until)
    {
        for (var day = 0; day < days && !until(); day++)
        {
            runner.StepPeopleDay(runner.Campaign.Camp!);
        }
    }

    private static List<Character> Relief(SimulationRunner runner, string charter)
    {
        var before = runner.Campaign.Characters.Keys.ToHashSet();
        runner.Campaign.World!.Charter = charter;
        runner.ReliefArrivals(runner.Campaign.World, Shipped.Shore!.Region!);
        return runner.Campaign.Characters.Values.Where(c => !before.Contains(c.Id)).ToList();
    }

    [Fact]
    public void TheReliefShipBringsSettlersWhenTheCharterIsKept()
    {
        var runner = Landed();
        var settlers = Relief(runner, "kept");
        Assert.Equal(8, settlers.Count);
        Assert.All(settlers, s => Assert.Equal(("relief", Membership.Crew), (s.Came, s.Membership)));
        Assert.Equal(4, settlers.Count(s => s.Sex == Sex.Female));
        Assert.Equal(2, runner.Campaign.Households.Count);
        Assert.All(settlers, s => Assert.NotEmpty(runner.Campaign.DutiesOf(s.Id)));
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.HouseholdFormed);

        Assert.Equal(4, Relief(Landed(), "in part").Count);

        // Revoked: no settlers, and the worn out take passage home.
        var revoked = Landed();
        var weary = revoked.Campaign.Crew.First(c => c.Id != revoked.Campaign.CaptainId);
        weary.Needs.Stress = 70;
        Assert.Empty(Relief(revoked, "revoked"));
        Assert.Equal(SurvivalState.Missing, weary.SurvivalState);
        Assert.Contains(revoked.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.WentHome && e.Characters[0] == weary.Id);
    }

    [Fact]
    public void TwoWhoAreFondOfEachOtherMakeAHome()
    {
        var runner = Landed();
        var crew = runner.Campaign.Crew.Where(c => c.Id != runner.Campaign.CaptainId).ToList();
        var (a, b) = (crew[0], crew[1]);
        Assert.Null(runner.PartnerOf(a.Id));
        runner.Campaign.ChangeRelationship(a.Id, b.Id, RelationshipDimension.Affection, 60, "test");
        runner.Campaign.ChangeRelationship(b.Id, a.Id, RelationshipDimension.Affection, 60, "test");
        Days(runner, 400, () => runner.Campaign.Households.Count > 0);
        var home = Assert.Single(runner.Campaign.Households);
        Assert.Equal(new[] { a.Id, b.Id }.Order(), home.Adults.Order());
        Assert.Null(home.Customs);
        Assert.Equal(b.Id, runner.PartnerOf(a.Id)?.Id);
        Assert.Same(home, runner.HouseholdOf(b.Id));
    }

    [Fact]
    public void APartnerFromAGiftGivingPeopleComesWithGiftsToTheFamily()
    {
        var runner = Landed(31);
        var mahenu = runner.Campaign.World!.Community("people.mahenu");
        mahenu.Met = true;
        mahenu.Trust = 60;
        runner.Campaign.SharedStore.TryAdd("item.trade_cloth", 4, Shipped.Items);
        var cloth = runner.Campaign.SharedStore.Count("item.trade_cloth");
        Days(runner, 1000, () => runner.Campaign.Households.Any(h => h.Customs == "people.mahenu"));
        var home = runner.Campaign.Households.Single(h => h.Customs == "people.mahenu");
        var partner = home.Adults.Select(id => runner.Campaign.Characters[id]).Single(c => c.Came == "people.mahenu");
        Assert.True(partner.Languages.Get("language.ahuneri") >= 4);
        Assert.Equal(cloth - 2, runner.Campaign.SharedStore.Count("item.trade_cloth"));
        Assert.True(mahenu.Trust > 60);
    }

    [Fact]
    public void ChildrenAreBornGrowUpAndDoNotWork()
    {
        var runner = Landed();
        Relief(runner, "kept");
        var home = runner.Campaign.Households[0];
        var mother = home.Adults.Select(id => runner.Campaign.Characters[id]).Single(c => c.Sex == Sex.Female);
        Days(runner, 2000, () => home.Due is not null);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.Expecting && e.Characters[0] == mother.Id);
        Assert.True(home.Due > runner.Campaign.Clock.Tick + 260 * SimulationClock.TicksPerDay);

        home.Due = runner.Campaign.Clock.Tick;
        runner.StepPeopleDay(runner.Campaign.Camp!);
        var child = runner.Campaign.Characters[Assert.Single(home.Children)];
        Assert.Equal((0, "born"), (child.Age, child.Came));
        Assert.NotNull(child.Born);
        Assert.True(SimulationRunner.IsChild(child));
        Assert.Empty(runner.Campaign.DutiesOf(child.Id));
        Assert.False(runner.CheckEligibility(child.Id, "task.fetch_water").Succeeded);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.ChildBorn && e.Characters[1] == mother.Id);

        // Twelve years on, they are old enough to work.
        child.Born -= 12L * 365 * SimulationClock.TicksPerDay;
        runner.StepPeopleDay(runner.Campaign.Camp!);
        Assert.Equal(12, child.Age);
        Assert.False(SimulationRunner.IsChild(child));
    }

    [Fact]
    public void PeopleOfTheCoastAndAHandOfTheBrennishShipComeToLive()
    {
        var runner = Landed(61);
        var kessat = runner.Campaign.World!.Community("people.tesiwa");
        kessat.Met = true;
        kessat.Trust = 48;
        Days(runner, 1500, () => runner.Campaign.Characters.Values.Count(c => c.Came == "people.tesiwa") >= 2);
        Assert.Equal(2, runner.Campaign.Characters.Values.Count(c => c.Came == "people.tesiwa"));
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.Joined && e.Get(ChronicleParameters.From) == "people.tesiwa");

        foreach (var person in runner.Campaign.Crew)
        {
            person.Needs.Stress = 0;
        }

        for (var tries = 0; tries < 30 && !runner.Campaign.Characters.Values.Any(c => c.Came == "brennish"); tries++)
        {
            runner.BrennishStays(Shipped.Shore!.Region!.Brennish!);
        }

        Assert.Single(runner.Campaign.Characters.Values, c => c.Came == "brennish");
    }

    [Fact]
    public void HouseholdsAndChildrenAreSaved()
    {
        var runner = Landed();
        Relief(runner, "kept");
        runner.Campaign.Households[0].Due = runner.Campaign.Clock.Tick;
        runner.StepPeopleDay(runner.Campaign.Camp!);
        runner.Campaign.Households[1].Due = runner.Campaign.Clock.Tick + 1000;

        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.Equal(2, loaded.Campaign.Households.Count);
        Assert.Single(loaded.Campaign.Households[0].Children);
        Assert.Contains(loaded.Campaign.Characters.Values, c => c.Came == "born" && c.Born is not null);
        for (var hour = 0; hour < 12; hour++)
        {
            runner.Advance(60);
            loaded.Advance(60);
        }

        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
