using System.Text.Json.Nodes;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.Voyage;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class DutyTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static (SimulationRunner Runner, List<CharacterId> Crew) Setup(bool withDuties = false)
    {
        var runner = TestDeckScenario.Create(Shipped, withDuties: withDuties);
        return (runner, runner.Campaign.Characters.Keys.Order().ToList());
    }

    [Fact]
    public void AtDawnTheWatchIsSetAndNobodyPumpsADryBilge()
    {
        var (runner, _) = Setup(withDuties: true);

        runner.Advance(1);

        var working = runner.Campaign.ActiveTasks.Values.ToList();
        Assert.All(working, task => Assert.Equal(TaskOrigin.Duty, task.Origin));
        Assert.Equal(2, working.Count(task => task.DefinitionId == "task.stand_watch"));
        Assert.DoesNotContain(working, task => task.DefinitionId == "task.pump_bilge");
    }

    [Fact]
    public void TheMostImportantNeededDutyComesFirst()
    {
        var (runner, crew) = Setup();
        runner.SetDuties(crew[3], ["task.pump_bilge", "task.stand_watch"]);
        runner.Campaign.Voyage!.Apply(new ShipEffects { Bilge = 40 }, 1);

        runner.Advance(1);

        Assert.Equal("task.pump_bilge", runner.Campaign.ActiveTasks[crew[3]].DefinitionId);
    }

    [Fact]
    public void ARisingBilgeCallsTheWatchToThePump()
    {
        var (runner, crew) = Setup();
        runner.SetDuties(crew[3], ["task.pump_bilge", "task.stand_watch"]);
        runner.Advance(10);
        Assert.Equal("task.stand_watch", runner.Campaign.ActiveTasks[crew[3]].DefinitionId);

        runner.Campaign.Voyage!.Apply(new ShipEffects { Bilge = 40 }, 1);
        runner.Advance(10);

        Assert.Equal("task.pump_bilge", runner.Campaign.ActiveTasks[crew[3]].DefinitionId);
        Assert.DoesNotContain(runner.Campaign.QueueOf(crew[3]), task => task.DefinitionId == "task.stand_watch");
    }

    [Fact]
    public void TheCookCooksAtMealTimesOnly()
    {
        var (runner, crew) = Setup();
        runner.SetDuties(crew[2], ["task.cook"]);
        var started = new List<long>();
        for (var minute = 0; minute < 24 * 60; minute++)
        {
            var wasCooking = runner.Campaign.ActiveTasks.ContainsKey(crew[2]);
            runner.Advance(1);
            if (!wasCooking && runner.Campaign.ActiveTasks.ContainsKey(crew[2]))
            {
                started.Add(runner.Campaign.Clock.Tick % SimulationClock.TicksPerDay / SimulationClock.TicksPerHour);
            }
        }

        Assert.Equal([10L, 17L], started.Order());
    }

    [Fact]
    public void AHotMealEasesEveryonesStress()
    {
        var (runner, crew) = Setup();
        foreach (var character in runner.Campaign.Characters.Values)
        {
            character.Needs.Stress = 30;
        }

        Assert.True(runner.AssignTask(crew[2], "task.cook").Succeeded);
        runner.Advance(4 * 60);

        Assert.All(runner.Campaign.Characters.Values, c => Assert.True(c.Needs.Stress < 30, $"{c.Name}: {c.Needs.Stress}"));
    }

    [Fact]
    public void ADutyThatCannotStartIsSkipped()
    {
        var (runner, crew) = Setup();
        runner.Campaign.SharedStore.TryRemove("item.rope", runner.Campaign.SharedStore.Count("item.rope"));
        runner.SetDuties(crew[1], ["task.repair_rigging", "task.stand_watch"]);

        runner.Advance(1);

        Assert.Equal("task.stand_watch", runner.Campaign.ActiveTasks[crew[1]].DefinitionId);
    }

    [Fact]
    public void AnIdleCrewMemberWithNoPossibleDutySaysWhy()
    {
        var (runner, crew) = Setup();
        runner.Campaign.SharedStore.TryRemove("item.rope", runner.Campaign.SharedStore.Count("item.rope"));
        runner.SetDuties(crew[1], ["task.repair_rigging"]);

        runner.Advance(1);

        Assert.False(runner.Campaign.ActiveTasks.ContainsKey(crew[1]));
        Assert.Contains("Rope", runner.WhyNoDuty(crew[1]));
    }

    [Fact]
    public void AnOrderTakesOverFromDutyWorkWhichResumesAfterwards()
    {
        var (runner, crew) = Setup();
        runner.SetDuties(crew[1], ["task.stand_watch"]);
        runner.Advance(30);

        Assert.True(runner.QueueTask(crew[1], "task.repair_rigging").Succeeded);
        Assert.True(runner.QueueTask(crew[1], "task.cook").Succeeded);

        Assert.Equal(TaskOrigin.Order, runner.Campaign.ActiveTasks[crew[1]].Origin);
        var queue = runner.Campaign.QueueOf(crew[1]);
        Assert.Equal(["task.cook", "task.stand_watch"], queue.Select(t => t.DefinitionId));
        Assert.True(queue[1].Progress > 0);
        Assert.Equal(TaskOrigin.Duty, queue[1].Origin);
    }

    [Fact]
    public void TiredIdleCrewRestOfTheirOwnAccord()
    {
        var (runner, crew) = Setup();
        runner.Campaign.Characters[crew[3]].Needs.Fatigue = 75;

        runner.Advance(1);

        var task = runner.Campaign.ActiveTasks[crew[3]];
        Assert.Equal("task.rest", task.DefinitionId);
        Assert.Equal(TaskOrigin.SelfCare, task.Origin);
        Assert.False(runner.Campaign.ActiveTasks.ContainsKey(crew[2]));
    }

    [Fact]
    public void WithoutTheRuleTiredCrewWaitForOrders()
    {
        var content = new GameContent(Shipped.Rules with { RestAtFatigue = null }, Shipped.Skills.Values, Shipped.Items.Values, Shipped.Tasks.Values,
            Shipped.Locations.Values, Shipped.Attributes.Values, Shipped.Traits.Values, Shipped.Conditions.Values, Shipped.Appearance,
            Shipped.Languages.Values, Shipped.Statuses.Values);
        var runner = TestDeckScenario.Create(content, withDuties: false);
        var pedro = runner.Campaign.Characters.Keys.Order().ElementAt(3);
        runner.Campaign.Characters[pedro].Needs.Fatigue = 95;

        runner.Advance(1);

        Assert.False(runner.Campaign.ActiveTasks.ContainsKey(pedro));
    }

    [Fact]
    public void AnOrderForSomeoneRestingWhoIsTooTiredWaitsInsteadOfBeingRefused()
    {
        var (runner, crew) = Setup();
        runner.Campaign.Characters[crew[3]].Needs.Fatigue = 95;
        runner.Advance(1);

        var result = runner.QueueTask(crew[3], "task.pump_bilge");

        Assert.True(result.Succeeded);
        Assert.Equal("task.rest", runner.Campaign.ActiveTasks[crew[3]].DefinitionId);
        Assert.Equal("task.pump_bilge", runner.Campaign.QueueOf(crew[3])[0].DefinitionId);
    }

    [Theory]
    [InlineData("task.nonsense", "Unknown task")]
    [InlineData("task.rest", "not a duty")]
    [InlineData("task.patch_hull", "lacks the skill")]
    public void ImpossibleDutiesAreRefused(string task, string reason)
    {
        var (runner, crew) = Setup();

        var result = runner.SetDuties(crew[3], [task]);

        Assert.False(result.Succeeded);
        Assert.Contains(reason, result.Reason);
        Assert.Empty(runner.Campaign.DutiesOf(crew[3]));
    }

    [Fact]
    public void RepeatedDutiesAndTheDeadAreRefused()
    {
        var (runner, crew) = Setup();
        Assert.False(runner.SetDuties(crew[3], ["task.pump_bilge", "task.pump_bilge"]).Succeeded);

        runner.Campaign.ChangeSurvivalState(crew[3], SurvivalState.Dead, "fell from the yard");

        Assert.False(runner.SetDuties(crew[3], ["task.pump_bilge"]).Succeeded);
    }

    [Fact]
    public void ClearingDutiesLeavesTheCrewIdle()
    {
        var (runner, crew) = Setup(withDuties: true);
        foreach (var id in crew)
        {
            runner.SetDuties(id, []);
        }

        runner.Advance(1);

        Assert.Empty(runner.Campaign.ActiveTasks);
    }

    [Fact]
    public void ACrewOnDutiesKeepsItselfGoingForThreeDays()
    {
        var (runner, crew) = Setup(withDuties: true);

        var outcomes = runner.Advance(3 * 24 * 60);

        Assert.All(crew, id =>
        {
            var character = runner.Campaign.Characters[id];
            Assert.Equal(SurvivalState.Alive, character.SurvivalState);
            Assert.True(character.Needs.Fatigue < Shipped.Rules.ExhaustionFatigue, $"{character.Name} is exhausted");
            Assert.Contains(outcomes, o => o.CharacterId == id && o.End == TaskEnd.Completed && o.DefinitionId != "task.rest");
        });
    }

    [Fact]
    public void DutiesSurviveSaveAndContinueIdentically()
    {
        var (original, _) = Setup(withDuties: true);
        original.Advance(500);
        var json = SaveSerializer.Serialize(original.Campaign);

        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);

        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        original.Advance(700);
        loaded.Advance(700);
        Assert.Equal(SaveSerializer.Serialize(original.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }

    [Fact]
    public void FormatNineSaveUpgradesWithOrderedWorkAndNoDuties()
    {
        var (runner, crew) = Setup();
        runner.QueueTask(crew[1], "task.repair_rigging");
        runner.QueueTask(crew[1], "task.stand_watch");
        var root = JsonNode.Parse(SaveSerializer.Serialize(runner.Campaign))!.AsObject();
        root["schemaVersion"] = 9;
        root.Remove("duties");
        foreach (var task in root["activeTasks"]!.AsArray().Concat(root["queuedTasks"]!.AsArray()))
        {
            task!.AsObject().Remove("origin");
        }

        var loaded = SaveSerializer.Deserialize(root.ToJsonString(), Shipped);

        Assert.Empty(loaded.Duties);
        Assert.Equal(TaskOrigin.Order, loaded.ActiveTasks[crew[1]].Origin);
        Assert.Equal(TaskOrigin.Order, loaded.QueueOf(crew[1])[0].Origin);
    }

    [Theory]
    [InlineData("unknown character")]
    [InlineData("unknown task")]
    [InlineData("rest as duty")]
    [InlineData("empty")]
    [InlineData("bad origin")]
    public void TamperedDutiesAreRejected(string tampering)
    {
        var (runner, _) = Setup(withDuties: true);
        runner.Advance(1);
        var root = JsonNode.Parse(SaveSerializer.Serialize(runner.Campaign))!.AsObject();
        var duty = root["duties"]!.AsArray()[1]!;
        switch (tampering)
        {
            case "unknown character": duty["characterId"] = 999; break;
            case "unknown task": duty["tasks"]!.AsArray().Add("task.dance"); break;
            case "rest as duty": duty["tasks"]!.AsArray().Add("task.rest"); break;
            case "empty": duty["tasks"] = new JsonArray(); break;
            case "bad origin": root["activeTasks"]!.AsArray()[0]!["origin"] = "Whim"; break;
        }

        Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(root.ToJsonString(), Shipped));
    }
}
