using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Houses and the smokehouse (docs/STRUCTURES.md).</summary>
public sealed class StructuresTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Landed()
    {
        var runner = TestDeckScenario.Create(Shipped, CampaignState.DefaultSeed);
        runner.Advance(60);
        Assert.True(runner.DeveloperLand("coast.punta_ceniza").Succeeded);
        for (var hour = 0; hour < 24; hour++)
        {
            runner.Advance(60);
            runner.DeveloperDismissMeeting();
        }

        return runner;
    }

    private static SimulationRunner Settled()
    {
        var runner = Landed();
        runner.Campaign.World!.Charter = "kept";
        runner.ReliefArrivals(runner.Campaign.World, Shipped.Shore!.Region!);
        Assert.Equal(2, runner.Campaign.Households.Count);
        return runner;
    }

    [Fact]
    public void AHouseGoesToTheHouseholdLongestWithoutOne()
    {
        var runner = Settled();
        var (first, second) = (runner.Campaign.Households[0], runner.Campaign.Households[1]);
        Assert.Contains(runner.Community().Causes, c => !c.Good && c.Text == "2 households with no house of their own");

        Assert.True(runner.DeveloperRaise("building.house").Succeeded);
        runner.StepPeopleDay(runner.Campaign.Camp!);
        Assert.NotNull(runner.HomeOf(first));
        Assert.Null(runner.HomeOf(second));
        Assert.Equal(1, runner.HouseholdsWithoutHomes);
        // A house's berths are dry berths like a hut's.
        Assert.Equal(4, runner.HutBerths);

        Assert.True(runner.DeveloperRaise("building.house").Succeeded);
        runner.StepPeopleDay(runner.Campaign.Camp!);
        Assert.NotNull(runner.HomeOf(second));
        Assert.Contains(runner.Community().Causes, c => c.Good && c.Text == "every household in a house of its own");

        // Living in it eases them; making do without tells on them.
        var adult = runner.Campaign.Characters[first.Adults[0]];
        adult.Needs.Stress = 30;
        runner.StepPeopleDay(runner.Campaign.Camp!);
        Assert.True(adult.Needs.Stress < 30);

        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.Equal(runner.HomeOf(first)!.Plot, loaded.HomeOf(loaded.Campaign.Households[0])!.Plot);
    }

    [Fact]
    public void FishIsSmokedAtTheSmokehouseToKeep()
    {
        var runner = Landed();
        var smoking = Shipped.Tasks["task.smoke_fish"];
        Assert.True(runner.Campaign.SharedStore.TryAdd("item.fish", 30, Shipped.Items).Succeeded);
        var before = runner.Campaign.SharedStore.Count("item.smoked_fish");
        Assert.True(Shipped.Items["item.smoked_fish"].SpoilsPerDay < Shipped.Items["item.fish"].SpoilsPerDay / 10);

        Assert.True(runner.DeveloperRaise("building.smokehouse").Succeeded);
        Assert.True(runner.DutyNeeded(smoking));
        for (var hour = 0; hour < 36 && runner.Campaign.SharedStore.Count("item.smoked_fish") <= before; hour++)
        {
            runner.Advance(60);
            runner.DeveloperDismissMeeting();
        }

        Assert.True(runner.Campaign.SharedStore.Count("item.smoked_fish") >= before + 10);
    }

    [Fact]
    public void SundayIsKeptAtTheChapel()
    {
        var runner = Landed();
        Assert.True(runner.DeveloperRaise("building.chapel").Succeeded);
        Assert.Contains(runner.Community().Causes, c => c.Good && c.Text == "Sunday kept at the chapel");
        double Eased()
        {
            var hand = runner.Campaign.Crew.First(c => c.Id != runner.Campaign.CaptainId);
            hand.Needs.Stress = 30;
            runner.StepPeopleDay(runner.Campaign.Camp!);
            return 30 - hand.Needs.Stress;
        }

        while (runner.Sunday)
        {
            runner.Advance(24 * 60);
            runner.DeveloperDismissMeeting();
        }

        Assert.Equal(0, Eased());
        while (!runner.Sunday)
        {
            runner.Advance(24 * 60);
            runner.DeveloperDismissMeeting();
        }

        Assert.True(Eased() > 0);
        Assert.Equal(DayOfWeek.Sunday, new DateTime(1501, 4, 12).AddDays(runner.Campaign.Clock.Day).DayOfWeek);
    }

    [Fact]
    public void AFairTradeAtTheTradingHouseCountsForMore()
    {
        double Gain(bool tradingHouse)
        {
            var runner = Landed();
            runner.Campaign.Crew.First(c => c.Id != runner.Campaign.CaptainId).Languages.Set("language.ahuneri", 4);
            if (tradingHouse)
            {
                Assert.True(runner.DeveloperRaise("building.trading_house").Succeeded);
            }

            Assert.True(runner.DeveloperMeeting("people.mahenu").Succeeded);
            runner.ResolveMeeting(runner.MeetingChoices().First(c => c.Id.StartsWith("answer:", StringComparison.Ordinal)).Id);
            var trust = runner.Campaign.World!.Communities["people.mahenu"].Trust;
            Assert.True(runner.ResolveMeeting(runner.MeetingChoices().First(c => c.Id.StartsWith("trade:", StringComparison.Ordinal)).Id).Result.Succeeded);
            return runner.Campaign.World.Communities["people.mahenu"].Trust - trust;
        }

        Assert.True(Gain(true) > Gain(false));
    }

    [Fact]
    public void SaltPansWantKessatsKnowledgeAndSaltFishKeepsAYear()
    {
        var runner = Landed();
        var refused = runner.OrderBuilding("building.salt_pans");
        Assert.False(refused.Succeeded);
        Assert.Contains("Kessat", refused.Reason);

        runner.Campaign.World!.Learn("know.salt", "people.tesiwa");
        Assert.True(runner.DeveloperRaise("building.salt_pans").Succeeded);
        Assert.True(runner.Campaign.SharedStore.TryAdd("item.fish", 20, Shipped.Items).Succeeded);
        for (var hour = 0; hour < 120 && runner.Campaign.SharedStore.Count("item.salt_fish") == 0; hour++)
        {
            runner.Advance(60);
            runner.DeveloperDismissMeeting();
        }

        Assert.True(runner.Campaign.SharedStore.Count("item.salt_fish") >= 10,
            $"salt {runner.Campaign.SharedStore.Count("item.salt")}, fish {runner.Campaign.SharedStore.Count("item.fish")}, poles {runner.Campaign.SharedStore.Count("item.poles")}, boil {runner.DutyNeeded(Shipped.Tasks["task.boil_salt"])}, check {runner.CheckEligibility(runner.Campaign.Crew.First(c => c.Id != runner.Campaign.CaptainId).Id, "task.boil_salt").Reason}, time {runner.Campaign.Clock}, chron {string.Join(" | ", runner.Campaign.Chronicle.Entries.TakeLast(3).Select(e => e.Kind))}");
        Assert.True(Shipped.Items["item.salt_fish"].SpoilsPerDay * 365 < 2);
    }

    [Fact]
    public void TheForgeMakesTheToolTheCampIsShortestOf()
    {
        var runner = Landed();
        foreach (var tool in new[] { "item.spade", "item.mattock" })
        {
            runner.Campaign.SharedStore.TryRemove(tool, runner.Campaign.SharedStore.Count(tool));
        }

        var wanted = runner.ToolWanted();
        Assert.NotNull(wanted);
        Assert.True(runner.Campaign.SharedStore.TryAdd("item.iron", 6, Shipped.Items).Succeeded);
        Assert.True(runner.DeveloperRaise("building.forge").Succeeded);
        var hand = runner.Campaign.Crew.First(c => c.Id != runner.Campaign.CaptainId);
        hand.Skills.Set("skill.crafting", 2);
        var before = runner.Campaign.SharedStore.Count(wanted!);
        for (var hour = 0; hour < 120 && !runner.Campaign.Chronicle.Entries.Any(e => e.Kind == ChronicleEntryKind.ToolMade); hour++)
        {
            runner.Advance(60);
            runner.DeveloperDismissMeeting();
        }

        var forging = Shipped.Tasks["task.forge_tool"];
        Assert.True(runner.Campaign.Chronicle.Entries.Any(e => e.Kind == ChronicleEntryKind.ToolMade),
            $"needed {runner.DutyNeeded(forging)}, wanted {runner.ToolWanted()}, iron {runner.Campaign.SharedStore.Count("item.iron")}, poles {runner.Campaign.SharedStore.Count("item.poles")}, check {runner.CheckEligibility(hand.Id, forging.Id).Reason}, duties {string.Join(",", runner.Campaign.DutiesOf(hand.Id))}");
        var made = runner.Campaign.Chronicle.Entries.First(e => e.Kind == ChronicleEntryKind.ToolMade);
        Assert.Equal(wanted, made.Get(ChronicleParameters.To));
        Assert.Equal(before + 1, runner.Campaign.SharedStore.Count(wanted!));
        Assert.True(runner.Campaign.SharedStore.Count("item.iron") <= 3);
        runner.Advance(60);
        Assert.NotNull(runner.Milestones().Single(m => m.Id == "tool").Reached);
    }

    [Fact]
    public void TheReliefShipBringsIron()
    {
        var runner = Settled();
        Assert.True(runner.Campaign.SharedStore.Count("item.iron") >= 20);
    }
}
