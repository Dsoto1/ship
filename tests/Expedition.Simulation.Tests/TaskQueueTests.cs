using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class TaskQueueTests
{
    private const double Tolerance = 1e-9;

    private static (SimulationRunner Runner, Character Sailor) Setup()
    {
        var campaign = new CampaignState(CampaignPhase.Voyage, seed: 4);
        var runner = new SimulationRunner(campaign, TestContent.Create());
        return (runner, campaign.CreateCharacter("Pedro", 19, "Huelva"));
    }

    private static string Current(SimulationRunner runner, Character sailor) =>
        runner.Campaign.ActiveTasks.TryGetValue(sailor.Id, out var task) ? task.DefinitionId : "idle";

    [Fact]
    public void AnIdleCharacterStartsAQueuedOrderAtOnce()
    {
        var (runner, sailor) = Setup();

        Assert.True(runner.QueueTask(sailor.Id, "task.pump").Succeeded);

        Assert.Equal("task.pump", Current(runner, sailor));
        Assert.Empty(runner.Campaign.QueueOf(sailor.Id));
    }

    [Fact]
    public void QueuedOrdersRunOneAfterAnother()
    {
        var (runner, sailor) = Setup();
        runner.QueueTask(sailor.Id, "task.pump");
        runner.QueueTask(sailor.Id, "task.rest");

        Assert.Equal(["task.rest"], runner.Campaign.QueueOf(sailor.Id).Select(t => t.DefinitionId));

        // Pumping (60 min) finishes at tick 60; rest starts on the next tick.
        runner.Advance(61);
        Assert.Equal("task.rest", Current(runner, sailor));
        Assert.Empty(runner.Campaign.QueueOf(sailor.Id));
    }

    [Fact]
    public void UrgentOrderPausesNormalWorkWhichResumesWithItsProgress()
    {
        var (runner, sailor) = Setup();
        runner.QueueTask(sailor.Id, "task.haul");
        runner.Advance(100);
        var haul = runner.Campaign.ActiveTasks[sailor.Id];
        Assert.Equal(100, haul.Progress, Tolerance);

        Assert.True(runner.QueueTask(sailor.Id, "task.pump", TaskPriority.Urgent).Succeeded);

        Assert.Equal("task.pump", Current(runner, sailor));
        var paused = Assert.Single(runner.Campaign.QueueOf(sailor.Id));
        Assert.Same(haul, paused);
        Assert.Equal(100, paused.Progress, Tolerance);
        var entry = runner.Campaign.Chronicle.Entries.Single(e => e.Kind == ChronicleEntryKind.TaskPaused);
        Assert.Equal("Pedro paused haul cargo to pump.", new ChronicleFormatter(runner.Campaign, runner.Content).Format(entry));

        // Pump finishes at tick 160; hauling resumes at 161 with 100 of its 600 minutes already done.
        runner.Advance(61);
        Assert.Same(haul, runner.Campaign.ActiveTasks[sailor.Id]);
        Assert.Equal(101, haul.Progress, Tolerance);
    }

    [Fact]
    public void MaterialsAreNotUsedAgainWhenPausedWorkResumes()
    {
        var (runner, sailor) = Setup();
        sailor.Skills.Set("skill.rigging", 2);
        runner.AddItems(runner.Campaign.SharedStoreId, "item.marlinspike", 1);
        runner.AddItems(runner.Campaign.SharedStoreId, "item.rope", 2);
        runner.QueueTask(sailor.Id, "task.repair_rigging");
        Assert.Equal(0, runner.Campaign.SharedStore.Count("item.rope"));

        runner.QueueTask(sailor.Id, "task.pump", TaskPriority.Urgent);
        runner.Advance(61);

        // The rigging resumed although there is no rope left: its rope was already used.
        Assert.Equal("task.repair_rigging", Current(runner, sailor));
    }

    [Fact]
    public void PausingReleasesAWorkSpotAndResumingTakesOneAgain()
    {
        var campaign = new CampaignState(CampaignPhase.Voyage);
        var runner = new SimulationRunner(campaign, TestContent.Create());
        var pedro = campaign.CreateCharacter("Pedro", 19, "Huelva");
        var diego = campaign.CreateCharacter("Diego", 29, "Palos");
        runner.PlaceCharacter(pedro.Id, "location.deck", new GridPosition(0, 3));
        runner.PlaceCharacter(diego.Id, "location.deck", new GridPosition(0, 3));
        runner.QueueTask(pedro.Id, "task.spot_pump");
        runner.QueueTask(diego.Id, "task.spot_pump");
        Assert.NotNull(campaign.ActiveTasks[pedro.Id].SpotId);

        runner.QueueTask(pedro.Id, "task.rest", TaskPriority.Urgent);

        Assert.Null(campaign.QueueOf(pedro.Id).Single().SpotId);
        var taken = campaign.ActiveTasks.Values.Select(t => t.SpotId).Where(s => s is not null).ToList();
        Assert.Single(taken);
    }

    [Fact]
    public void UrgentOrdersQueueAheadOfNormalOnesButBehindOtherUrgentOnes()
    {
        var (runner, sailor) = Setup();
        runner.QueueTask(sailor.Id, "task.pump", TaskPriority.Urgent);
        runner.QueueTask(sailor.Id, "task.rest");
        runner.QueueTask(sailor.Id, "task.haul", TaskPriority.Urgent);
        runner.QueueTask(sailor.Id, "task.storm_work", TaskPriority.Urgent);

        Assert.Equal("task.pump", Current(runner, sailor));
        Assert.Equal(["task.haul", "task.storm_work", "task.rest"], runner.Campaign.QueueOf(sailor.Id).Select(t => t.DefinitionId));
    }

    [Fact]
    public void ATiredCharacterKeepsWorkWaitingInsteadOfDroppingIt()
    {
        var (runner, sailor) = Setup();
        runner.QueueTask(sailor.Id, "task.pump");
        runner.QueueTask(sailor.Id, "task.haul");
        sailor.Needs.Fatigue = 95;

        runner.Advance(61);

        Assert.Equal("idle", Current(runner, sailor));
        Assert.Equal(["task.haul"], runner.Campaign.QueueOf(sailor.Id).Select(t => t.DefinitionId));
        Assert.DoesNotContain(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.TaskDropped);
    }

    [Fact]
    public void AnOrderThatCannotStartIsDroppedAndRecorded()
    {
        var (runner, sailor) = Setup();
        sailor.Skills.Set("skill.rigging", 2);
        runner.QueueTask(sailor.Id, "task.pump");
        runner.QueueTask(sailor.Id, "task.repair_rigging");
        runner.QueueTask(sailor.Id, "task.rest");

        runner.Advance(61);

        // No marlinspike: the rigging order is dropped and the next order starts instead.
        Assert.Equal("task.rest", Current(runner, sailor));
        var dropped = runner.Campaign.Chronicle.Entries.Single(e => e.Kind == ChronicleEntryKind.TaskDropped);
        Assert.Equal("task.repair_rigging", dropped.Get(ChronicleParameters.Task));
        Assert.Contains("item.marlinspike", dropped.Get(ChronicleParameters.Cause));
    }

    [Fact]
    public void OrdersThatCanNeverBeCarriedOutAreRefusedAtOnce()
    {
        var (runner, sailor) = Setup();
        runner.QueueTask(sailor.Id, "task.pump");

        Assert.False(runner.QueueTask(sailor.Id, "task.repair_rigging").Succeeded);
        Assert.False(runner.QueueTask(sailor.Id, "task.nonsense").Succeeded);
        Assert.Empty(runner.Campaign.QueueOf(sailor.Id));
    }

    [Fact]
    public void DeathClearsTheQueueButIncapacityKeepsIt()
    {
        var (runner, sailor) = Setup();
        var other = runner.Campaign.CreateCharacter("Juan", 52, "Moguer");
        foreach (var who in new[] { sailor, other })
        {
            runner.QueueTask(who.Id, "task.pump");
            runner.QueueTask(who.Id, "task.rest");
        }

        runner.Campaign.ChangeSurvivalState(sailor.Id, SurvivalState.Dead, "fell from the yard");
        runner.Campaign.ChangeSurvivalState(other.Id, SurvivalState.Incapacitated, "condition.fever");

        Assert.Empty(runner.Campaign.QueueOf(sailor.Id));
        Assert.Single(runner.Campaign.QueueOf(other.Id));
        Assert.False(runner.QueueTask(sailor.Id, "task.rest").Succeeded);
    }

    [Fact]
    public void WaitingOrdersCanBeRemovedOrCleared()
    {
        var (runner, sailor) = Setup();
        runner.QueueTask(sailor.Id, "task.pump");
        runner.QueueTask(sailor.Id, "task.rest");
        runner.QueueTask(sailor.Id, "task.haul");
        var rest = runner.Campaign.QueueOf(sailor.Id)[0];

        Assert.True(runner.RemoveQueuedTask(sailor.Id, rest.Id).Succeeded);
        Assert.False(runner.RemoveQueuedTask(sailor.Id, rest.Id).Succeeded);
        Assert.Equal(["task.haul"], runner.Campaign.QueueOf(sailor.Id).Select(t => t.DefinitionId));

        runner.ClearQueue(sailor.Id);
        Assert.Empty(runner.Campaign.QueueOf(sailor.Id));
        Assert.Equal("task.pump", Current(runner, sailor));
    }

    [Fact]
    public void CancellingStartsTheNextOrderStraightAway()
    {
        var (runner, sailor) = Setup();
        runner.QueueTask(sailor.Id, "task.pump");
        runner.QueueTask(sailor.Id, "task.rest");

        runner.CancelTask(sailor.Id);

        Assert.Equal("task.rest", Current(runner, sailor));
    }

    [Fact]
    public void AWalkOrderedByThePlayerFinishesBeforeQueuedWorkStarts()
    {
        var campaign = new CampaignState(CampaignPhase.Voyage);
        var runner = new SimulationRunner(campaign, TestContent.Create());
        var pedro = campaign.CreateCharacter("Pedro", 19, "Huelva");
        runner.PlaceCharacter(pedro.Id, "location.deck", new GridPosition(0, 0));
        runner.QueueTask(pedro.Id, "task.pump");
        runner.QueueTask(pedro.Id, "task.rest");

        runner.MoveCharacter(pedro.Id, new GridPosition(0, 4));
        runner.Advance(2);
        Assert.Equal("idle", Current(runner, pedro));

        runner.Advance(3);
        Assert.Equal("task.rest", Current(runner, pedro));
    }

    [Fact]
    public void CheckOrderPredictsQueueTaskWithoutChangingAnything()
    {
        var (runner, sailor) = Setup();
        sailor.Skills.Set("skill.rigging", 2);
        runner.QueueTask(sailor.Id, "task.pump");
        var before = SaveSerializer.Serialize(runner.Campaign);

        // Queued behind the pump, tools are checked later; started urgently now, they are checked at once.
        Assert.True(runner.CheckOrder(sailor.Id, "task.repair_rigging").Succeeded);
        Assert.False(runner.CheckOrder(sailor.Id, "task.repair_rigging", TaskPriority.Urgent).Succeeded);
        Assert.False(runner.CheckOrder(sailor.Id, "task.nonsense").Succeeded);
        Assert.Equal(before, SaveSerializer.Serialize(runner.Campaign));

        Assert.False(runner.QueueTask(sailor.Id, "task.repair_rigging", TaskPriority.Urgent).Succeeded);
        Assert.Equal("task.pump", Current(runner, sailor));
    }

    [Fact]
    public void TaskIdsAreUniqueAndNeverReused()
    {
        var (runner, sailor) = Setup();
        runner.QueueTask(sailor.Id, "task.pump");
        runner.QueueTask(sailor.Id, "task.rest");
        var first = runner.Campaign.ActiveTasks[sailor.Id].Id;
        runner.ClearQueue(sailor.Id);

        runner.QueueTask(sailor.Id, "task.haul");

        var ids = new[] { first }.Concat(runner.Campaign.QueueOf(sailor.Id).Select(t => t.Id)).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(3, runner.Campaign.Ids.LastTaskId);
    }

    [Fact]
    public void QueuesAndPausedWorkSurviveSaveAndContinueIdentically()
    {
        var original = TestDeckScenario.Create(Content.ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data")), withDuties: false);
        var crew = original.Campaign.Characters.Keys.Order().ToList();
        original.QueueTask(crew[1], "task.repair_rigging");
        original.QueueTask(crew[1], "task.stand_watch");
        original.Advance(30);
        original.QueueTask(crew[1], "task.pump_bilge", TaskPriority.Urgent);
        original.QueueTask(crew[3], "task.rest");
        original.Advance(5);

        var json = SaveSerializer.Serialize(original.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, original.Content), original.Content);

        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.Equal(["task.repair_rigging", "task.stand_watch"], loaded.Campaign.QueueOf(crew[1]).Select(t => t.DefinitionId));
        Assert.True(loaded.Campaign.QueueOf(crew[1])[0].Progress > 0);
        original.Advance(300);
        loaded.Advance(300);
        Assert.Equal(SaveSerializer.Serialize(original.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }

    [Theory]
    [InlineData("duplicate id")]
    [InlineData("id above last")]
    [InlineData("queued spot")]
    [InlineData("wrong order")]
    [InlineData("bad priority")]
    public void TamperedQueuesAreRejected(string tampering)
    {
        var (runner, sailor) = Setup();
        runner.QueueTask(sailor.Id, "task.pump");
        runner.QueueTask(sailor.Id, "task.rest");
        runner.QueueTask(sailor.Id, "task.haul");
        var root = JsonNode.Parse(SaveSerializer.Serialize(runner.Campaign))!.AsObject();
        var queued = root["queuedTasks"]!.AsArray();
        switch (tampering)
        {
            case "duplicate id": queued[1]!["id"] = queued[0]!["id"]!.DeepClone(); break;
            case "id above last": queued[0]!["id"] = 99; break;
            case "queued spot": queued[0]!["spotId"] = "spot.pump_a"; break;
            case "wrong order": queued[1]!["priority"] = "Urgent"; break;
            case "bad priority": queued[0]!["priority"] = "Whenever"; break;
        }

        Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(root.ToJsonString(), TestContent.Create()));
    }

    [Fact]
    public void FormatFiveSaveGivesActiveTasksIds()
    {
        var (runner, sailor) = Setup();
        runner.QueueTask(sailor.Id, "task.pump");
        var root = JsonNode.Parse(SaveSerializer.Serialize(runner.Campaign))!.AsObject();
        root["schemaVersion"] = 5;
        root.Remove("knowledge");
        root.Remove("lastTaskId");
        root.Remove("queuedTasks");
        var active = root["activeTasks"]!.AsArray()[0]!.AsObject();
        foreach (var field in new[] { "id", "priority", "materialsConsumed" })
        {
            active.Remove(field);
        }

        var loaded = SaveSerializer.Deserialize(root.ToJsonString(), TestContent.Create());

        var task = loaded.ActiveTasks[sailor.Id];
        Assert.Equal(1, task.Id.Value);
        Assert.Equal(TaskPriority.Normal, task.Priority);
        Assert.True(task.MaterialsConsumed);
        Assert.Equal(2, loaded.Ids.NextTaskId().Value);
    }
}
