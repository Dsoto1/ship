using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.History;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class ProvisionTests
{
    private const int Hour = SimulationClock.TicksPerHour;
    private const int Day = SimulationClock.TicksPerDay;

    private static (SimulationRunner Runner, Character Sailor) Setup(int biscuits, int water)
    {
        var campaign = new CampaignState(CampaignPhase.Voyage, seed: 5);
        var runner = new SimulationRunner(campaign, TestContent.Create());
        var sailor = campaign.CreateCharacter("Alonso", 40, "Seville");
        if (biscuits > 0)
        {
            runner.AddItems(campaign.SharedStoreId, "item.biscuit", biscuits);
        }

        if (water > 0)
        {
            runner.AddItems(campaign.SharedStoreId, "item.water", water);
        }

        return (runner, sailor);
    }

    [Fact]
    public void ThirtyDayVoyageConsumesExpectedProvisions()
    {
        var (runner, sailor) = Setup(biscuits: 100, water: 100);

        runner.Advance(30 * Day);

        // Idle hunger rises 0.5/h. First meal at 30 (hour 60), then each biscuit (12) lasts 24 h:
        // meals at hours 60, 84, ..., 708 = 28 biscuits in 720 h.
        // Thirst rises 1/h. First drink at 30 (hour 30), then each litre (12) lasts 12 h:
        // drinks at hours 30, 42, ..., 714 = 58 litres.
        Assert.Equal(100 - 28, runner.Campaign.SharedStore.Count("item.biscuit"));
        Assert.Equal(100 - 58, runner.Campaign.SharedStore.Count("item.water"));
        Assert.Equal(Health.Max, sailor.Health.Value);
        Assert.InRange(sailor.Needs.Hunger, 18, 30);
        Assert.InRange(sailor.Needs.Thirst, 18, 30);
        Assert.Empty(runner.Campaign.Chronicle.Entries);
    }

    [Fact]
    public void HardWorkMakesTheCrewDrinkMore()
    {
        var (idleRunner, _) = Setup(biscuits: 0, water: 200);
        var (busyRunner, worker) = Setup(biscuits: 0, water: 200);

        idleRunner.Advance(2 * Day);
        for (var hour = 0; hour < 48; hour++)
        {
            if (!busyRunner.Campaign.ActiveTasks.ContainsKey(worker.Id))
            {
                busyRunner.AssignTask(worker.Id, worker.Needs.Fatigue > 60 ? "task.rest" : "task.pump");
            }

            busyRunner.Advance(Hour);
        }

        // Pumping adds 1 thirst per hour on top of the baseline, so the pumper drinks more.
        Assert.True(busyRunner.Campaign.SharedStore.Count("item.water") < idleRunner.Campaign.SharedStore.Count("item.water"));
    }

    [Fact]
    public void CharactersEatFromTheirOwnPackFirst()
    {
        var (runner, sailor) = Setup(biscuits: 10, water: 0);
        runner.AddItems(sailor.InventoryId, "item.biscuit", 1);

        runner.Advance(61 * Hour);

        Assert.Equal(0, runner.Campaign.Inventories[sailor.InventoryId].Count("item.biscuit"));
        Assert.Equal(10, runner.Campaign.SharedStore.Count("item.biscuit"));
    }

    [Fact]
    public void RunningOutOfFoodIsRecordedOnceThenStarvationIncapacitatesThenKills()
    {
        var (runner, sailor) = Setup(biscuits: 1, water: 1000);

        runner.Advance(310 * Hour);

        // One biscuit at hour 60 -> hunger 18; it reaches 30 again at hour 84 and there is nothing left.
        // Hunger hits 100 at hour 224; losing 1 health/h, health reaches 25 at hour 299.
        var shortage = Assert.Single(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.SupplyShortage);
        Assert.InRange(shortage.Tick, 84 * Hour, 84 * Hour + 1);
        Assert.Contains("no food", shortage.Description);
        Assert.Equal(SurvivalState.Incapacitated, sailor.SurvivalState);
        var collapse = Assert.Single(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.SurvivalStateChanged);
        Assert.Contains("starvation", collapse.Description);
        Assert.InRange(collapse.Tick, 299 * Hour - 2, 299 * Hour + 2);

        // Starvation continues while incapacitated: health 25 reaches 0 at hour 324.
        runner.Advance(20 * Hour);
        Assert.Equal(SurvivalState.Dead, sailor.SurvivalState);
        Assert.Equal(Health.Min, sailor.Health.Value);
    }

    [Fact]
    public void DehydrationKillsFasterThanStarvation()
    {
        var (runner, sailor) = Setup(biscuits: 1000, water: 0);

        runner.Advance(200 * Hour);

        // Thirst hits 100 at hour 100; 4 health/h means death at hour 125.
        Assert.Equal(SurvivalState.Dead, sailor.SurvivalState);
        var death = runner.Campaign.Chronicle.Entries.Last(e => e.Kind == ChronicleEntryKind.SurvivalStateChanged);
        Assert.Contains("Dead", death.Description);
        Assert.Contains("dehydration", death.Description);
        Assert.InRange(death.Tick, 125 * Hour - 2, 125 * Hour + 2);
    }

    [Fact]
    public void ShortageIsRecordedAgainAfterResupply()
    {
        var (runner, _) = Setup(biscuits: 0, water: 1000);
        runner.Advance(61 * Hour);
        runner.AddItems(runner.Campaign.SharedStoreId, "item.biscuit", 1);
        runner.Advance(Hour);
        runner.Advance(30 * Hour);

        Assert.Equal(2, runner.Campaign.Chronicle.Entries.Count(e => e.Kind == ChronicleEntryKind.SupplyShortage));
    }

    [Fact]
    public void IncapacitatedCrewAreStillFed()
    {
        var (runner, sailor) = Setup(biscuits: 10, water: 10);
        runner.Campaign.ChangeSurvivalState(sailor.Id, SurvivalState.Incapacitated, "fever");

        runner.Advance(61 * Hour);

        Assert.Equal(9, runner.Campaign.SharedStore.Count("item.biscuit"));
    }

    [Fact]
    public void TheDeadDoNotEat()
    {
        var (runner, sailor) = Setup(biscuits: 10, water: 10);
        runner.Campaign.ChangeSurvivalState(sailor.Id, SurvivalState.Dead, "fell from the yard");

        runner.Advance(10 * Day);

        Assert.Equal(10, runner.Campaign.SharedStore.Count("item.biscuit"));
        Assert.Equal(10, runner.Campaign.SharedStore.Count("item.water"));
    }
}
