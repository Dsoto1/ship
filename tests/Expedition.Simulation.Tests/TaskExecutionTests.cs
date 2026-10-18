using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.History;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class TaskExecutionTests
{
    private const int Hour = SimulationClock.TicksPerHour;
    private const double Tolerance = 1e-9;

    private static (SimulationRunner Runner, Character Sailor) Setup(ulong seed = 1)
    {
        var campaign = new CampaignState(CampaignPhase.Voyage, seed);
        var sailor = campaign.CreateCharacter("Pedro", 24, "Moguer");
        return (new SimulationRunner(campaign, TestContent.Create()), sailor);
    }

    [Fact]
    public void NeedsRiseWithBaselineWhenIdle()
    {
        var (runner, sailor) = Setup();

        runner.Advance(10 * Hour);

        Assert.Equal(5.0, sailor.Needs.Hunger, Tolerance);
        Assert.Equal(10.0, sailor.Needs.Thirst, Tolerance);
        Assert.Equal(10.0, sailor.Needs.Fatigue, Tolerance);
    }

    [Fact]
    public void FatigueAccumulatesWhileWorking()
    {
        var (runner, sailor) = Setup();

        Assert.True(runner.AssignTask(sailor.Id, "task.pump").Succeeded);
        var outcomes = runner.Advance(Hour);

        // Baseline 1 + pumping 6 = 7 fatigue per hour.
        Assert.Equal(7.0, sailor.Needs.Fatigue, Tolerance);
        var outcome = Assert.Single(outcomes);
        Assert.Equal(TaskEnd.Completed, outcome.End);
        Assert.Equal(Hour, outcome.Tick);
        Assert.Empty(runner.Campaign.ActiveTasks);
    }

    [Fact]
    public void FatigueRecoversWhileResting()
    {
        var (runner, sailor) = Setup();
        sailor.Needs.Fatigue = 50;

        runner.AssignTask(sailor.Id, "task.rest");
        runner.Advance(4 * Hour);

        // Baseline 1 + rest -9 = -8 per hour for 4 hours.
        Assert.Equal(18.0, sailor.Needs.Fatigue, Tolerance);
    }

    [Fact]
    public void FatigueNeverDropsBelowZero()
    {
        var (runner, sailor) = Setup();
        sailor.Needs.Fatigue = 5;

        runner.AssignTask(sailor.Id, "task.rest");
        runner.Advance(4 * Hour);

        Assert.Equal(0.0, sailor.Needs.Fatigue);
    }

    [Fact]
    public void ExhaustionInterruptsWorkAndIsRecorded()
    {
        var (runner, sailor) = Setup();
        sailor.Needs.Fatigue = 89;

        runner.AssignTask(sailor.Id, "task.haul");
        var outcomes = runner.Advance(10 * Hour);

        // 10 fatigue per hour from 89 reaches 100 after 66 minutes.
        var outcome = Assert.Single(outcomes);
        Assert.Equal(TaskEnd.Interrupted, outcome.End);
        Assert.Equal(66, outcome.Tick);
        Assert.Contains("exhausted", outcome.Reason);
        var entry = Assert.Single(runner.Campaign.Chronicle.Entries);
        Assert.Equal(ChronicleEntryKind.TaskInterrupted, entry.Kind);
        Assert.Equal(66, entry.Tick);
    }

    [Fact]
    public void ExhaustedCharacterCannotStartWorkButCanRest()
    {
        var (runner, sailor) = Setup();
        sailor.Needs.Fatigue = 90;

        var work = runner.AssignTask(sailor.Id, "task.pump");
        var rest = runner.AssignTask(sailor.Id, "task.rest");

        Assert.False(work.Succeeded);
        Assert.Contains("exhausted", work.Reason);
        Assert.True(rest.Succeeded);
    }

    [Fact]
    public void RestIsNotInterruptedByFatigue()
    {
        var (runner, sailor) = Setup();
        sailor.Needs.Fatigue = 100;

        runner.AssignTask(sailor.Id, "task.rest");
        var outcomes = runner.Advance(4 * Hour);

        Assert.Equal(TaskEnd.Completed, Assert.Single(outcomes).End);
    }

    [Fact]
    public void TaskNeedingMissingSkillIsIneligible()
    {
        var (runner, sailor) = Setup();
        runner.AddItems(runner.Campaign.SharedStoreId, "item.marlinspike", 1);
        runner.AddItems(runner.Campaign.SharedStoreId, "item.rope", 2);

        var result = runner.AssignTask(sailor.Id, "task.repair_rigging");

        Assert.False(result.Succeeded);
        Assert.Contains("skill.rigging", result.Reason);
        Assert.Equal(2, runner.Campaign.SharedStore.Count("item.rope"));
    }

    [Fact]
    public void TaskNeedingMissingToolIsIneligible()
    {
        var (runner, sailor) = Setup();
        sailor.Skills.Set("skill.rigging", 3);
        runner.AddItems(runner.Campaign.SharedStoreId, "item.rope", 2);

        var result = runner.AssignTask(sailor.Id, "task.repair_rigging");

        Assert.False(result.Succeeded);
        Assert.Contains("item.marlinspike", result.Reason);
    }

    [Fact]
    public void TaskNeedingMissingMaterialsIsIneligibleAndConsumesNothing()
    {
        var (runner, sailor) = Setup();
        sailor.Skills.Set("skill.rigging", 3);
        runner.AddItems(runner.Campaign.SharedStoreId, "item.marlinspike", 1);
        runner.AddItems(runner.Campaign.SharedStoreId, "item.rope", 1);

        var result = runner.AssignTask(sailor.Id, "task.repair_rigging");

        Assert.False(result.Succeeded);
        Assert.Contains("item.rope x2", result.Reason);
        Assert.Equal(1, runner.Campaign.SharedStore.Count("item.rope"));
    }

    [Fact]
    public void MaterialsAreConsumedOnStartToolsAreKeptAndCancelDoesNotRefund()
    {
        var (runner, sailor) = Setup();
        sailor.Skills.Set("skill.rigging", 2);
        runner.AddItems(runner.Campaign.SharedStoreId, "item.marlinspike", 1);
        runner.AddItems(runner.Campaign.SharedStoreId, "item.rope", 3);

        Assert.True(runner.AssignTask(sailor.Id, "task.repair_rigging").Succeeded);
        Assert.Equal(1, runner.Campaign.SharedStore.Count("item.rope"));
        Assert.Equal(1, runner.Campaign.SharedStore.Count("item.marlinspike"));

        Assert.True(runner.CancelTask(sailor.Id).Succeeded);
        Assert.Equal(1, runner.Campaign.SharedStore.Count("item.rope"));
        Assert.Empty(runner.Campaign.ActiveTasks);
    }

    [Theory]
    [InlineData(1, 160)]
    [InlineData(2, 120)]
    [InlineData(4, 80)]
    public void HigherSkillFinishesFaster(int level, int expectedMinutes)
    {
        var (runner, sailor) = Setup();
        sailor.Skills.Set("skill.rigging", level);
        runner.AddItems(runner.Campaign.SharedStoreId, "item.marlinspike", 1);
        runner.AddItems(runner.Campaign.SharedStoreId, "item.rope", 2);

        runner.AssignTask(sailor.Id, "task.repair_rigging");
        var outcome = Assert.Single(runner.Advance(5 * Hour));

        Assert.Equal(TaskEnd.Completed, outcome.End);
        Assert.Equal(expectedMinutes, outcome.Tick);
    }

    [Fact]
    public void TiredCharactersWorkSlower()
    {
        var (runner, sailor) = Setup();
        sailor.Needs.Fatigue = 80;

        runner.AssignTask(sailor.Id, "task.pump");
        var outcomes = runner.Advance(Hour);

        // Half speed when tired: one hour of pumping is only half done.
        Assert.Empty(outcomes);
        Assert.Equal(30.0, runner.Campaign.ActiveTasks[sailor.Id].Progress, Tolerance);
    }

    [Fact]
    public void BusyCharacterCannotTakeASecondTask()
    {
        var (runner, sailor) = Setup();
        runner.AssignTask(sailor.Id, "task.pump");

        var result = runner.AssignTask(sailor.Id, "task.haul");

        Assert.False(result.Succeeded);
        Assert.Contains("already doing task.pump", result.Reason);
    }

    [Fact]
    public void BecomingIncapacitatedInterruptsTheTaskImmediately()
    {
        var (runner, sailor) = Setup();
        runner.AssignTask(sailor.Id, "task.haul");

        runner.Campaign.ChangeSurvivalState(sailor.Id, SurvivalState.Incapacitated, "fever");

        Assert.Empty(runner.Campaign.ActiveTasks);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.TaskInterrupted);
        Assert.False(runner.AssignTask(sailor.Id, "task.pump").Succeeded);
    }

    [Fact]
    public void DeadCharacterCannotWorkAndNeedsStopChanging()
    {
        var (runner, sailor) = Setup();
        runner.Campaign.ChangeSurvivalState(sailor.Id, SurvivalState.Dead, "drowned");

        var result = runner.AssignTask(sailor.Id, "task.rest");
        runner.Advance(24 * Hour);

        Assert.False(result.Succeeded);
        Assert.Contains("Dead", result.Reason);
        Assert.Equal(0.0, sailor.Needs.Fatigue);
        Assert.Empty(runner.Campaign.ActiveTasks);
    }

    [Fact]
    public void HazardInjuryIsRecordedAndCanIncapacitate()
    {
        var (runner, sailor) = Setup(seed: 2024);
        sailor.Health.Value = 40;

        runner.AssignTask(sailor.Id, "task.storm_work");
        runner.Advance(10 * Hour);

        // One injury of 20 takes health from 40 to 20, at or below the incapacitation threshold of 25,
        // so work stops and no second injury can happen.
        Assert.Equal(20, sailor.Health.Value);
        Assert.Equal(SurvivalState.Incapacitated, sailor.SurvivalState);
        Assert.Contains("condition.bruised", sailor.Health.Conditions);
        Assert.Empty(runner.Campaign.ActiveTasks);
        Assert.Equal(
            [ChronicleEntryKind.Injury, ChronicleEntryKind.TaskInterrupted, ChronicleEntryKind.SurvivalStateChanged],
            runner.Campaign.Chronicle.Entries.Select(e => e.Kind));
    }

    [Fact]
    public void InjuryThatTakesHealthToZeroKills()
    {
        var (runner, sailor) = Setup(seed: 2024);
        sailor.Health.Value = 20;

        runner.AssignTask(sailor.Id, "task.storm_work");
        runner.Advance(10 * Hour);

        Assert.Equal(SurvivalState.Dead, sailor.SurvivalState);
        Assert.False(sailor.CanWork);
    }

    [Fact]
    public void IdenticalSeedGivesIdenticalResults()
    {
        var first = RunStormScenario(seed: 77);

        Assert.Equal(first, RunStormScenario(seed: 77));
        Assert.Contains(nameof(ChronicleEntryKind.Injury), first);
    }

    [Fact]
    public void DifferentSeedChangesHazardOutcomes()
    {
        Assert.NotEqual(RunStormScenario(seed: 77), RunStormScenario(seed: 78));
    }

    [Fact]
    public void AdvancingBackwardsIsRejected()
    {
        var (runner, _) = Setup();

        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Advance(-1));
    }

    /// <summary>Six sailors alternate storm work and rest for three days; returns a full snapshot.</summary>
    private static string RunStormScenario(ulong seed)
    {
        var campaign = new CampaignState(CampaignPhase.Voyage, seed);
        var runner = new SimulationRunner(campaign, TestContent.Create());
        var crew = Enumerable.Range(1, 6).Select(i => campaign.CreateCharacter($"Sailor {i}", 20 + i, "Palos")).ToList();

        for (var hour = 0; hour < 72; hour++)
        {
            foreach (var sailor in crew.Where(c => c.CanWork && !campaign.ActiveTasks.ContainsKey(c.Id)))
            {
                var task = sailor.Needs.Fatigue > 60 ? "task.rest" : "task.storm_work";
                runner.AssignTask(sailor.Id, task);
            }

            runner.Advance(Hour);
        }

        var people = crew.Select(c =>
            $"{c.Id}:{c.SurvivalState}:{c.Health.Value}:{c.Needs.Fatigue:R}:{c.Needs.Stress:R}:{string.Join(",", c.Health.Conditions)}");
        var history = campaign.Chronicle.Entries.Select(e => $"{e.Tick}:{e.Kind}:{string.Join(",", e.Parameters)}");
        return string.Join("\n", people.Concat(history));
    }
}
