using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Shore;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Step C (docs/COMMUNITY.md): the community's condition, the captain's policies ashore, milestones, ranges.</summary>
public sealed class CommunityTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Landed()
    {
        var runner = TestDeckScenario.Create(Shipped, CampaignState.DefaultSeed);
        runner.Advance(60);
        Assert.True(runner.DeveloperLand("coast.punta_ceniza").Succeeded);
        runner.Advance(24 * 60);
        return runner;
    }

    private static void Play(SimulationRunner runner, int hours)
    {
        for (var hour = 0; hour < hours; hour++)
        {
            runner.Advance(60);
            runner.DeveloperDismissMeeting();
        }
    }

    private static double Food(SimulationRunner runner) => Shipped.Foods.Sum(f => runner.Campaign.SharedStore.Count(f.Id) * f.HungerRelief);

    private static double Strain(SimulationRunner runner) => runner.Campaign.Crew.Average(c => c.Needs.Stress);

    [Fact]
    public void EmergencyRationsServeFoodEveryOtherDayAndWeighHeavier()
    {
        (double Eaten, double Strain) Run(Rations rations)
        {
            var runner = Landed();
            var camp = runner.Campaign.Camp!;
            // Nothing gathered: what is eaten comes from the stores alone.
            Assert.True(runner.SetCampChoices(camp.Choices with { Forage = false }).Succeeded);
            Assert.True(runner.SetRations(rations).Succeeded);
            Assert.Equal(rations, runner.Rations);
            var before = Food(runner);
            Play(runner, 4 * 24);
            return (before - Food(runner), Strain(runner));
        }

        var shortRations = Run(Rations.Short);
        var emergency = Run(Rations.Emergency);
        Assert.True(emergency.Eaten < shortRations.Eaten, $"eaten: emergency {emergency.Eaten:0}, short {shortRations.Eaten:0}");
        Assert.True(emergency.Strain > shortRations.Strain);

        var runner = Landed();
        Assert.True(runner.SetRations(Rations.Emergency).Succeeded);
        Assert.True(runner.SetRations(Rations.Full).Succeeded);
        Assert.False(runner.Campaign.EmergencyRations);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.RationingChanged && e.Get(ChronicleParameters.To) == "emergency");
    }

    [Fact]
    public void TheWorkingDaySetsWhenTheWorkMayStillBeBegun()
    {
        var runner = Landed();
        while (runner.Campaign.Clock.TimeOfDay.TotalHours is < 17 or >= 18)
        {
            runner.Advance(15);
        }

        // Ordered after the day's work is done: no one is at it yet.
        Assert.True(runner.OrderBuilding("building.hut").Succeeded);

        var clearing = Shipped.Tasks["task.clear_ground"];
        Assert.False(runner.DutyNeeded(clearing));
        Assert.True(runner.SetWorkHours(WorkHours.Long).Succeeded);
        Assert.True(runner.DutyNeeded(clearing));
        Assert.True(runner.SetWorkHours(WorkHours.Short).Succeeded);
        Assert.False(runner.DutyNeeded(clearing));

        // Long hours tell on them; short ones ease them.
        double After(WorkHours hours)
        {
            var again = Landed();
            Assert.True(again.SetWorkHours(hours).Succeeded);
            Play(again, 3 * 24);
            return Strain(again);
        }

        Assert.True(After(WorkHours.Long) > After(WorkHours.Short));
    }

    [Fact]
    public void BelowHalfTheReserveWaterComesFirst()
    {
        var runner = Landed();
        Assert.False(runner.SetReserves(15, 10).Succeeded);
        Assert.True(runner.SetReserves(10, 20).Succeeded);
        var camp = runner.Campaign.Camp!;
        Assert.Equal((10, 20), (camp.ReserveFoodDays, camp.ReserveWaterDays));
        foreach (var drink in Shipped.Drinks)
        {
            runner.Campaign.SharedStore.TryRemove(drink.Id, runner.Campaign.SharedStore.Count(drink.Id));
        }

        runner.Advance(60);
        var hand = runner.Campaign.Crew.First(c => c.Id != runner.Campaign.CaptainId);
        Assert.Equal("task.fetch_water", runner.Campaign.DutiesOf(hand.Id)[0]);
    }

    [Fact]
    public void AMilestoneIsReachedOnceAndLiftsThem()
    {
        var runner = Landed();
        Assert.Null(runner.Milestones().Single(m => m.Id == "storehouse").Reached);
        var strain = Strain(runner);
        Assert.True(runner.DeveloperRaise("building.storehouse").Succeeded);
        runner.Advance(60);
        Assert.NotNull(runner.Milestones().Single(m => m.Id == "storehouse").Reached);
        Assert.Single(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.MilestoneReached && e.Get(ChronicleParameters.To) == "storehouse");
        Assert.True(Strain(runner) < strain + 1);
        runner.Advance(120);
        Assert.Single(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.MilestoneReached && e.Get(ChronicleParameters.To) == "storehouse");
        Assert.Equal(8, runner.Milestones().Count);
    }

    [Fact]
    public void TheCommunitysConditionSaysWhy()
    {
        var runner = Landed();
        Assert.True(runner.SetRations(Rations.Short).Succeeded);
        Assert.True(runner.SetWorkHours(WorkHours.Long).Succeeded);
        var condition = runner.Community();
        Assert.Contains(condition.Word, new[] { "Good", "Steady", "Strained", "Low", "Breaking" });
        Assert.Contains(condition.Causes, c => !c.Good && c.Text == "on short rations");
        Assert.Contains(condition.Causes, c => !c.Good && c.Text == "long hours");
        // What weighs on them first.
        Assert.DoesNotContain(condition.Causes.SkipWhile(c => !c.Good), c => !c.Good);

        foreach (var _ in Enumerable.Range(0, 2))
        {
            Assert.True(runner.DeveloperRaise("building.hut").Succeeded);
        }

        Assert.Contains(runner.Community().Causes, c => c.Good && c.Text == "dry huts for all");
    }

    [Fact]
    public void TheStoresAreProjectedAsARange()
    {
        var runner = Landed();
        var (foodLow, foodHigh, waterLow, waterHigh) = runner.SupplyRange();
        Assert.True(foodLow < foodHigh && waterLow < waterHigh);
        Assert.Equal(runner.SupplyForecast().FoodDays, foodHigh, 3);
        Assert.True(runner.SetRations(Rations.Short).Succeeded);
        Assert.True(runner.SupplyRange().FoodHigh > foodHigh * 1.5);
        Assert.Matches(@"^~\d+–\d+ d$", SimulationRunner.DaysRange(18.2, 22.4));
        Assert.Equal("~20 d", SimulationRunner.DaysRange(19.6, 20.2));
    }

    [Fact]
    public void PoliciesAndMilestonesAreSaved()
    {
        var runner = Landed();
        Assert.True(runner.SetRations(Rations.Emergency).Succeeded);
        Assert.True(runner.SetWorkHours(WorkHours.Short).Succeeded);
        Assert.True(runner.SetReserves(20, 10).Succeeded);
        Assert.True(runner.DeveloperRaise("building.storehouse").Succeeded);
        Play(runner, 6);

        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.Equal(Rations.Emergency, loaded.Rations);
        Assert.Equal((WorkHours.Short, 20, 10), (loaded.Campaign.Camp!.Hours, loaded.Campaign.Camp.ReserveFoodDays, loaded.Campaign.Camp.ReserveWaterDays));
        Assert.Contains("storehouse", loaded.Campaign.Camp.Milestones.Keys);
        Play(runner, 12);
        Play(loaded, 12);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
