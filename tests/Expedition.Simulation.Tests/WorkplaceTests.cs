using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Shore;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Step W (docs/WORKPLACES.md): workplaces led by a person, priorities, why work stops, the history of buildings.</summary>
public sealed class WorkplaceTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Landed(ulong seed = CampaignState.DefaultSeed)
    {
        var runner = TestDeckScenario.Create(Shipped, seed);
        runner.Advance(60);
        Assert.True(runner.DeveloperLand("coast.punta_ceniza").Succeeded);
        runner.Advance(24 * 60);
        return runner;
    }

    // Hours on, the meetings that come waved off.
    private static void Play(SimulationRunner runner, int hours, Func<bool>? until = null)
    {
        for (var hour = 0; hour < hours && until?.Invoke() != true; hour++)
        {
            runner.Advance(60);
            runner.DeveloperDismissMeeting();
        }
    }

    // The best carpenter of the company, not the captain.
    private static Expedition.Simulation.Characters.Character Carpenter(SimulationRunner runner) =>
        runner.Campaign.Crew.Where(c => c.Id != runner.Campaign.CaptainId && c.CanWork).OrderByDescending(c => c.Skills.Get("skill.carpentry")).First();

    [Fact]
    public void ABuildingRecordsWhoBuiltItUnderWhomAndFromWhat()
    {
        var runner = Landed();
        var lead = Carpenter(runner);
        Assert.True(runner.OrderBuilding("building.hut").Succeeded);
        var project = Assert.Single(runner.Campaign.Camp!.Projects);
        Assert.True(runner.SetLead(project.Plot, lead.Id).Succeeded);
        Assert.Equal(lead.Id, runner.LeadOf(project.Plot));
        var begun = runner.Campaign.Clock.Tick;
        Play(runner, 30 * 24, () => runner.Campaign.Camp!.Projects.Count == 0);

        var hut = Assert.Single(runner.Campaign.Camp!.Buildings);
        Assert.Equal(lead.Id, hut.BuiltUnder);
        Assert.Equal(lead.Id, hut.Lead);
        Assert.Equal(begun, hut.Begun);
        Assert.True(hut.Built > hut.Begun);
        Assert.NotEmpty(hut.Builders);
        Assert.True(hut.Used.GetValueOrDefault("item.poles") > 0 && hut.Used.GetValueOrDefault("item.thatch") > 0);
        var history = runner.BuildingHistory(hut);
        Assert.Contains(history, l => l.StartsWith("Built day", StringComparison.Ordinal));
        Assert.Contains(history, l => l == $"Led by {lead.Name}.");
        Assert.Contains(history, l => l.StartsWith("From ", StringComparison.Ordinal) && l.Contains("poles", StringComparison.Ordinal));
    }

    [Fact]
    public void ASkilledLeadMakesBetterWorkAndTeaches()
    {
        double Quality(bool led, out double practice)
        {
            var runner = Landed();
            var lead = Carpenter(runner);
            // Carpentry learned, in hours of practice: the levels reached and the practice toward the next.
            double Learned() => runner.Campaign.Crew.Where(c => c.Id != lead.Id).Sum(c =>
                Enumerable.Range(0, c.Skills.Get("skill.carpentry")).Sum(SimulationRunner.HoursToNextLevel) + runner.Campaign.Life.PracticeOf(c.Id, "skill.carpentry"));
            var before = Learned();
            Assert.True(runner.OrderBuilding("building.hut").Succeeded);
            var project = runner.Campaign.Camp!.Projects[0];
            if (led)
            {
                // A master carpenter leading.
                lead.Skills.Set("skill.carpentry", 5);
                Assert.True(runner.SetLead(project.Plot, lead.Id).Succeeded);
            }

            // Learning is counted per hour the others work on the hut.
            var hours = 0.0;
            for (var minute = 0; minute < 96 * 60; minute++)
            {
                runner.Advance(1);
                if (minute % 60 == 0)
                {
                    runner.DeveloperDismissMeeting();
                }

                hours += runner.Campaign.ActiveTasks.Values.Count(t => t.CharacterId != lead.Id && t.SpotId?.StartsWith($"plot.{project.Plot}.", StringComparison.Ordinal) == true) / 60.0;
            }

            practice = (Learned() - before) / hours;
            Play(runner, 30 * 24, () => runner.Campaign.Camp!.Projects.Count == 0);
            return Assert.Single(runner.Campaign.Camp!.Buildings).Quality;
        }

        var plain = Quality(led: false, out var alone);
        var better = Quality(led: true, out var taught);
        Assert.True(better > plain, $"led {better:0.000}, not led {plain:0.000}");
        Assert.True(taught > alone, $"carpentry learned per hour on the hut under a lead {taught:0.00}, alone {alone:0.00}");
    }

    [Fact]
    public void APersonLeadsOneWorkplaceAtATime()
    {
        var runner = Landed();
        var lead = Carpenter(runner);
        Assert.True(runner.DeveloperRaise("building.hut").Succeeded);
        Assert.True(runner.OrderBuilding("building.storehouse").Succeeded);
        var hut = runner.Campaign.Camp!.Buildings[0];
        var store = runner.Campaign.Camp.Projects[0];
        Assert.True(runner.SetLead(hut.Plot, lead.Id).Succeeded);
        Assert.True(runner.SetLead(store.Plot, lead.Id).Succeeded);
        Assert.Null(hut.Lead);
        Assert.Equal(lead.Id, store.Lead);
        Assert.False(runner.SetLead("nowhere", lead.Id).Succeeded);
        Assert.True(runner.SetLead(store.Plot, null).Succeeded);
        Assert.Null(runner.LeadOf(store.Plot));
    }

    [Fact]
    public void ThePausedAndTheLowWaitForWhatMattersMore()
    {
        var runner = Landed();
        Assert.True(runner.OrderBuilding("building.hut").Succeeded);
        Assert.True(runner.OrderBuilding("building.hut").Succeeded);
        var camp = runner.Campaign.Camp!;
        var (first, second) = (camp.Projects[0], camp.Projects[1]);
        Assert.True(runner.SetProjectPriority(first.Id, ProjectPriority.Paused).Succeeded);
        Assert.False(runner.SetProjectPriority(999, ProjectPriority.High).Succeeded);
        Play(runner, 48);
        Assert.Equal((0, 0), (first.Phase, first.Done));
        Assert.Equal("Paused.", runner.Stalled(first));
        Assert.True(second.Phase > 0 || second.Done > 0);

        // Taken up again, but low: the other still comes first while it can be worked.
        Assert.True(runner.SetProjectPriority(first.Id, ProjectPriority.Low).Succeeded);
        var (phase, done) = (first.Phase, first.Done);
        Play(runner, 12, () => runner.Stalled(second) is not null && runner.Stalled(second)!.StartsWith("Waiting for", StringComparison.Ordinal) && !runner.Stalled(second)!.StartsWith("Waiting for hands", StringComparison.Ordinal));
        if (runner.Stalled(second) is null || runner.Stalled(second)!.StartsWith("Waiting for hands", StringComparison.Ordinal))
        {
            Assert.Equal((phase, done), (first.Phase, first.Done));
        }
    }

    [Fact]
    public void ACriticalProjectIsTakenUpBeforeOtherCampWork()
    {
        var runner = Landed();
        Assert.True(runner.OrderBuilding("building.hut").Succeeded);
        var hut = runner.Campaign.Camp!.Projects[0];
        Assert.True(runner.SetProjectPriority(hut.Id, ProjectPriority.Critical).Succeeded);
        Play(runner, 24);
        Assert.True(hut.Phase > 0 || hut.Done > 0 || runner.Stalled(hut) is not null);
    }

    [Fact]
    public void AProjectSaysWhyItHasStopped()
    {
        var runner = Landed();
        Assert.True(runner.OrderBuilding("building.hut").Succeeded);
        var hut = runner.Campaign.Camp!.Projects[0];
        var store = runner.Campaign.SharedStore;

        // No mattock: the ground cannot be cleared.
        var mattocks = store.Count("item.mattock");
        foreach (var person in runner.Campaign.Crew)
        {
            runner.Campaign.Inventories[person.InventoryId].TryRemove("item.mattock", runner.Campaign.Inventories[person.InventoryId].Count("item.mattock"));
        }

        Assert.True(store.TryRemove("item.mattock", mattocks));
        Assert.StartsWith("No mattock", runner.Stalled(hut));
        Assert.True(store.TryAdd("item.mattock", mattocks, Shipped.Items).Succeeded);

        // Past the clearing, no poles: the posts wait for them.
        hut.Restore(1, 0, 0, 0, 0);
        store.TryRemove("item.poles", store.Count("item.poles"));
        Assert.StartsWith("Waiting for poles: 4 more wanted", runner.Stalled(hut));

        // At night nothing is begun.
        while (runner.Campaign.Clock.TimeOfDay.TotalHours is > 4 and < 22)
        {
            runner.Advance(30);
        }

        Assert.Matches("^Night: .* is begun again at 06:00", runner.Stalled(hut));
    }

    [Fact]
    public void AStormAndARepairGoIntoABuildingsHistory()
    {
        var runner = Landed();
        Assert.True(runner.DeveloperRaise("building.hut", 0.4).Succeeded);
        var hut = runner.Campaign.Camp!.Buildings[0];
        hut.Weathered(runner.Campaign.Clock.Tick, 5, 2 * SimulationClock.TicksPerDay);
        hut.Weathered(runner.Campaign.Clock.Tick + SimulationClock.TicksPerHour, 3, 2 * SimulationClock.TicksPerDay);
        hut.Happened(new BuildingEvent(runner.Campaign.Clock.Tick + SimulationClock.TicksPerDay, "repaired", 8));
        Assert.Equal(2, hut.History.Count);
        var history = runner.BuildingHistory(hut);
        Assert.Contains(history, l => l.StartsWith("Weathered the storm of day", StringComparison.Ordinal) && l.EndsWith("(−8).", StringComparison.Ordinal));
        Assert.Contains(history, l => l.StartsWith("Repaired day", StringComparison.Ordinal));
    }

    [Fact]
    public void LeadsPrioritiesAndHistoryAreSaved()
    {
        var runner = Landed();
        var lead = Carpenter(runner);
        Assert.True(runner.DeveloperRaise("building.hut").Succeeded);
        Assert.True(runner.OrderBuilding("building.storehouse").Succeeded);
        var camp = runner.Campaign.Camp!;
        Assert.True(runner.SetLead(camp.Projects[0].Plot, lead.Id).Succeeded);
        Assert.True(runner.SetProjectPriority(camp.Projects[0].Id, ProjectPriority.High).Succeeded);
        camp.Buildings[0].Weathered(runner.Campaign.Clock.Tick, 4, 1);
        Play(runner, 24);

        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        var again = loaded.Campaign.Camp!;
        Assert.Equal(ProjectPriority.High, again.Projects[0].Priority);
        Assert.Equal(lead.Id, again.Projects[0].Lead);
        Assert.Single(again.Buildings[0].History);
        Play(runner, 12);
        Play(loaded, 12);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
