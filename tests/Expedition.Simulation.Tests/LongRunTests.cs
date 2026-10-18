using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Long simulations from TECHNICAL_SPEC.md §38 (#2 and #15).</summary>
public class LongRunTests
{
    private const int Hour = SimulationClock.TicksPerHour;
    private const int Day = SimulationClock.TicksPerDay;

    private static GameContent Shipped() => ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    /// <summary>The crew keeps busy: rest when tired, otherwise each person's usual job.</summary>
    private static void AssignIdleCrew(SimulationRunner runner)
    {
        var jobs = new[] { "task.stand_watch", "task.repair_rigging", "task.cook", "task.pump_bilge", "task.patch_hull", "task.stand_watch" };
        var crew = runner.Campaign.Characters.Values.OrderBy(c => c.Id).ToList();
        for (var i = 0; i < crew.Count; i++)
        {
            var sailor = crew[i];
            if (sailor.CanWork && !runner.Campaign.ActiveTasks.ContainsKey(sailor.Id))
            {
                if (sailor.Needs.Fatigue > 60 || !runner.AssignTask(sailor.Id, jobs[i]).Succeeded)
                {
                    runner.AssignTask(sailor.Id, "task.rest");
                }
            }
        }
    }

    [Fact]
    public void TimeAccelerationDoesNotChangeResults()
    {
        var oneTickAtATime = TestDeckScenario.Create(Shipped());
        var oneHourAtATime = TestDeckScenario.Create(Shipped());

        for (var hour = 0; hour < 48; hour++)
        {
            AssignIdleCrew(oneTickAtATime);
            for (var minute = 0; minute < Hour; minute++)
            {
                oneTickAtATime.Advance(1);
            }

            AssignIdleCrew(oneHourAtATime);
            oneHourAtATime.Advance(Hour);
        }

        Assert.Equal(SaveSerializer.Serialize(oneTickAtATime.Campaign), SaveSerializer.Serialize(oneHourAtATime.Campaign));
    }

    [Fact]
    public void NinetyDayVoyageWithAmpleProvisionsKeepsEveryoneAlive()
    {
        var runner = TestDeckScenario.Create(Shipped());
        runner.AddItems(TestDeckScenario.HoldId, "item.water", 1000);
        runner.AddItems(TestDeckScenario.HoldId, "item.biscuit", 500);
        var waterBefore = runner.Campaign.SharedStore.Count("item.water");

        var previous = runner.Campaign.SharedStore.Quantities.ToDictionary();
        for (var day = 0; day < 90; day++)
        {
            for (var hour = 0; hour < 24; hour++)
            {
                AssignIdleCrew(runner);
                runner.Advance(Hour);
            }

            // Stores only ever go down at sea, and never below zero.
            var now = runner.Campaign.SharedStore.Quantities;
            Assert.All(now, pair => Assert.InRange(pair.Value, 1, previous.GetValueOrDefault(pair.Key)));
            previous = now.ToDictionary();
        }

        Assert.All(runner.Campaign.Characters.Values, c => Assert.Equal(SurvivalState.Alive, c.SurvivalState));
        Assert.DoesNotContain(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.SupplyShortage);

        // Six working sailors drink roughly 2-4 litres a day each (wine is drunk first; it sorts before water).
        var litresPerPersonPerDay = (waterBefore - runner.Campaign.SharedStore.Count("item.water")) / 6.0 / 90.0;
        Assert.InRange(litresPerPersonPerDay, 1.0, 4.0);
    }

    [Fact]
    public void NinetyDayVoyageThatRunsDryEndsInRecordedDeaths()
    {
        var runner = TestDeckScenario.Create(Shipped());

        for (var day = 0; day < 90; day++)
        {
            for (var hour = 0; hour < 24; hour++)
            {
                AssignIdleCrew(runner);
                runner.Advance(Hour);
            }
        }

        var campaign = runner.Campaign;
        Assert.Equal(0, campaign.SharedStore.Count("item.water"));
        Assert.Equal(0, campaign.SharedStore.Count("item.wine"));
        Assert.All(campaign.Characters.Values, c => Assert.Equal(SurvivalState.Dead, c.SurvivalState));
        var deaths = campaign.Chronicle.Entries
            .Where(e => e.Kind == ChronicleEntryKind.SurvivalStateChanged && e.Get(ChronicleParameters.To) == nameof(SurvivalState.Dead))
            .ToList();
        Assert.Equal(6, deaths.Count);
        Assert.All(deaths, d => Assert.Equal(ChronicleCauses.Dehydration, d.Get(ChronicleParameters.Cause)));
        var shortage = campaign.Chronicle.Entries.First(e => e.Kind == ChronicleEntryKind.SupplyShortage);
        Assert.True(shortage.Tick < deaths.Min(d => d.Tick));
        Assert.All(campaign.Inventories.Values.SelectMany(i => i.Quantities.Values), q => Assert.True(q > 0));
    }
}
