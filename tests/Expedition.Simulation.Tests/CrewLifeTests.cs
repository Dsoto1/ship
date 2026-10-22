using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Voyage;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>R4f: sickness and healing, grievances by stages, skills learned by practice and from a mentor.</summary>
public sealed class CrewLifeTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private const int Hour = 60;
    private const int Day = 24 * Hour;

    private static SimulationRunner FairDeck(ulong seed = CampaignState.DefaultSeed, bool withDuties = true)
    {
        var runner = TestDeckScenario.Create(Shipped, seed, withDuties);
        var ship = runner.Campaign.Voyage!;
        ship.WeatherId = "weather.fair";
        ship.WeatherUntilTick = long.MaxValue;
        return runner;
    }

    private static Character Hand(SimulationRunner runner, string name) => runner.Campaign.Crew.First(c => c.Name.StartsWith(name, StringComparison.Ordinal));

    // Events wait for the captain; these tests answer them with the first choice to hand, unless told otherwise.
    private static void Sail(SimulationRunner runner, int minutes, Func<string, string?>? answer = null)
    {
        for (var done = 0; done < minutes; done += Hour)
        {
            runner.Advance(Math.Min(Hour, minutes - done));
            if (runner.Campaign.PendingEvent is { } pending)
            {
                var choice = answer?.Invoke(pending.DefinitionId) ?? runner.OfferedChoices().First(o => o.Available).Choice.Id;
                runner.ResolveEvent(choice);
            }
        }
    }

    [Fact]
    public void InjuriesHealAndHealthComesBack()
    {
        var runner = FairDeck();
        var pedro = Hand(runner, "Pedro");
        pedro.Health.Value = 60;
        pedro.Health.AddCondition("condition.bruised");

        Sail(runner, 6 * Day);

        Assert.DoesNotContain("condition.bruised", pedro.Health.Conditions);
        Assert.True(pedro.Health.Value > 65, $"health {pedro.Health.Value:0.0}");
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.Healed && e.Characters.Contains(pedro.Id));
    }

    [Fact]
    public void TheIncapacitatedGetBackOnTheirFeet()
    {
        var runner = FairDeck();
        var pedro = Hand(runner, "Pedro");
        pedro.Health.Value = 20;
        runner.Campaign.ChangeSurvivalState(pedro.Id, SurvivalState.Incapacitated, "test");

        Sail(runner, 12 * Day);

        Assert.Equal(SurvivalState.Alive, pedro.SurvivalState);
    }

    [Fact]
    public void TendingTheSickSpeedsTheirRecovery()
    {
        double MeanDays(bool tended)
        {
            var total = 0.0;
            foreach (var seed in Enumerable.Range(1, 12))
            {
                var runner = FairDeck((ulong)seed, withDuties: false);
                var pedro = Hand(runner, "Pedro");
                var martin = Hand(runner, "Martín");
                pedro.Health.AddCondition("condition.flux");
                var start = runner.Campaign.Clock.Tick;
                while (pedro.Health.Conditions.Contains("condition.flux") && runner.Campaign.Clock.Tick - start < 20 * Day)
                {
                    if (tended && !runner.Campaign.ActiveTasks.ContainsKey(martin.Id))
                    {
                        runner.QueueTask(martin.Id, "task.tend_sick");
                    }

                    runner.Advance(30);
                    runner.Campaign.Characters[martin.Id].Needs.Fatigue = 0;
                }

                total += (runner.Campaign.Clock.Tick - start) / (double)SimulationClock.TicksPerDay;
            }

            return total / 12;
        }

        var alone = MeanDays(tended: false);
        var cared = MeanDays(tended: true);
        Assert.True(cared < alone * 0.8, $"tended {cared:0.0} d, untended {alone:0.0} d");
    }

    [Fact]
    public void AFoulShipBreedsTheFluxAndSaysWhy()
    {
        var caught = 0;
        foreach (var seed in Enumerable.Range(1, 6))
        {
            var runner = FairDeck((ulong)seed, withDuties: false);
            runner.Campaign.Life.Restore(5, 0);
            Sail(runner, 6 * Day);
            caught += runner.Campaign.Chronicle.Entries.Count(e => e.Kind == ChronicleEntryKind.FellIll && e.Get(ChronicleParameters.Condition) == "condition.flux"
                                                                    && e.Get(ChronicleParameters.Reason)!.Contains("foul"));
        }

        Assert.True(caught > 0);
    }

    [Fact]
    public void SicknessPassesBetweenShipmates()
    {
        var caught = 0;
        foreach (var seed in Enumerable.Range(1, 8))
        {
            var runner = FairDeck((ulong)seed, withDuties: false);
            runner.Campaign.Life.Restore(20, 0);
            foreach (var hand in runner.Campaign.Crew.Take(2))
            {
                hand.Health.AddCondition("condition.fever");
            }

            Sail(runner, 3 * Day);
            var entries = runner.Campaign.Chronicle.Entries.Where(e => e.Kind == ChronicleEntryKind.FellIll && e.Get(ChronicleParameters.Reason)!.StartsWith("caught", StringComparison.Ordinal)).ToList();
            caught += entries.Count;
            var formatter = new ChronicleFormatter(runner.Campaign, runner.Content);
            Assert.All(entries, e => Assert.Contains("caught from", formatter.Format(e)));
        }

        Assert.True(caught > 0);
    }

    [Fact]
    public void WeeksWithoutAnythingFreshBringTheScurvyAndFreshFoodCuresIt()
    {
        var runner = FairDeck(withDuties: false);
        var life = runner.Campaign.Life;
        foreach (var hand in runner.Campaign.Crew)
        {
            life.NoteFresh(hand.Id, -45L * SimulationClock.TicksPerDay);
        }

        Sail(runner, 4 * Day);
        var sick = runner.Campaign.Crew.Where(c => c.Health.Conditions.Contains("condition.scurvy")).ToList();
        Assert.NotEmpty(sick);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.FellIll && e.Get(ChronicleParameters.Reason) == "nofresh");

        // Fish in the hold: eaten first, as it spoils first, and the scurvy goes.
        runner.Campaign.SharedStore.TryAdd("item.fish", 80, Shipped.Items);
        Sail(runner, 5 * Day);
        Assert.All(sick, c => Assert.DoesNotContain("condition.scurvy", c.Health.Conditions));
    }

    [Fact]
    public void ShortRationsAreMutteredAboutThenBroughtToTheCaptain()
    {
        var runner = FairDeck();
        runner.SetRationing(true);
        string? raised = null;
        Sail(runner, 8 * Day, id =>
        {
            raised ??= id;
            // Keep the cause going: the captain silences them.
            return id == "event.complaint_rations" ? "silence" : null;
        });

        var kinds = runner.Campaign.Chronicle.Entries.Where(e => e.Kind == ChronicleEntryKind.GrievanceRose).Select(e => e.Get(ChronicleParameters.To)).ToList();
        Assert.Contains("Concern", kinds);
        Assert.Contains("Complaint", kinds);
        Assert.True(kinds.IndexOf("Concern") < kinds.IndexOf("Complaint"));
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.EventResolved && e.Get(ChronicleParameters.Event) == "event.complaint_rations");
    }

    [Fact]
    public void PuttingItRightEasesTheGrievance()
    {
        var runner = FairDeck();
        runner.SetRationing(true);
        Sail(runner, 8 * Day, id => id == "event.complaint_rations" ? "restore" : null);

        Assert.False(runner.Campaign.Rationing);
        Sail(runner, 6 * Day);
        Assert.All(runner.Campaign.Crew, c => Assert.True(runner.Campaign.Life.GrievanceOf(c.Id).Stage <= GrievanceStage.Concern));
    }

    [Fact]
    public void ARefusingHandDoesOnlyWhatKeepsThemAlive()
    {
        var runner = FairDeck();
        var pedro = Hand(runner, "Pedro");
        runner.DeveloperAggrieve(pedro.Id, GrievanceCauses.Harsh, 95);
        var grievance = runner.Campaign.Life.Hold(pedro.Id);
        grievance.Stage = GrievanceStage.Faction;
        grievance.StageTick = -100 * SimulationClock.TicksPerHour;
        grievance.Raised = GrievanceStage.Complaint;
        // Hold the captain's answer: the refusal waits.
        runner.Advance(2 * Hour);

        Assert.True(runner.Refusing(pedro.Id));
        Assert.Equal("event.refusal", runner.Campaign.PendingEvent?.DefinitionId);
        var order = runner.CheckOrder(pedro.Id, "task.lash_cargo");
        Assert.False(order.Succeeded);
        Assert.Contains("refuses", order.Reason);

        // Rest, and the pumps when she is filling, they will still do.
        Assert.True(runner.CheckOrder(pedro.Id, "task.rest").Succeeded);
        runner.Campaign.Voyage!.Apply(new ShipEffects { Bilge = 60 }, 1);
        Assert.True(runner.CheckOrder(pedro.Id, "task.pump_bilge").Succeeded);
    }

    [Fact]
    public void ABrokenPromiseIsRemembered()
    {
        var runner = FairDeck();
        runner.SetRationing(true);
        var captain = runner.Campaign.CaptainId!.Value;
        var pedro = Hand(runner, "Pedro");
        var before = runner.Campaign.GetRelationship(pedro.Id, captain)?.Trust ?? 0;
        Sail(runner, 12 * Day, id => id == "event.complaint_rations" ? "promise" : null);

        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.PromiseBroken);
        Assert.True(runner.Campaign.GetRelationship(pedro.Id, captain)!.Trust < before);
    }

    [Fact]
    public void PracticeRaisesASkillSlowlyAndOnlySoFarAlone()
    {
        var runner = FairDeck(withDuties: false);
        var pedro = Hand(runner, "Pedro");
        Assert.Equal(0, pedro.Skills.Get("skill.fishing"));
        for (var i = 0; i < 40 && pedro.Skills.Get("skill.fishing") == 0; i++)
        {
            runner.QueueTask(pedro.Id, "task.fish");
            runner.Advance(3 * Hour);
            pedro.Needs.Fatigue = 0;
        }

        Assert.Equal(1, pedro.Skills.Get("skill.fishing"));
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.SkillImproved && e.Get(ChronicleParameters.To) == "skill.fishing");
        Assert.Equal(25, SimulationRunner.HoursToNextLevel(0));
        Assert.True(SimulationRunner.HoursToNextLevel(2) > 8 * SimulationRunner.HoursToNextLevel(0));
    }

    [Fact]
    public void AMentorTeachesFasterAndSomeoneTheyLookDownOnWillNotLearn()
    {
        int HoursToLevel(bool mentored)
        {
            var runner = FairDeck(withDuties: false);
            var pedro = Hand(runner, "Pedro");
            var diego = Hand(runner, "Diego");
            if (mentored)
            {
                Assert.True(runner.SetMentor(pedro.Id, diego.Id, "skill.rigging").Succeeded);
            }

            var hours = 0;
            runner.Campaign.SharedStore.TryAdd("item.rope", 200, Shipped.Items);
            while (pedro.Skills.Get("skill.rigging") < 1 && hours < 400)
            {
                runner.Campaign.Voyage!.Apply(new ShipEffects { Rigging = -20 }, 1);
                if (!runner.Campaign.ActiveTasks.ContainsKey(pedro.Id))
                {
                    var result = runner.QueueTask(pedro.Id, "task.repair_rigging");
                }

                runner.Advance(Hour);
                pedro.Needs.Fatigue = 0;
                diego.Needs.Fatigue = 0;
                hours++;
            }

            return hours;
        }

        // Pedro has no rigging, and repairing it needs a little: Diego teaches him by his side.
        var mentoredHours = HoursToLevel(true);
        Assert.True(mentoredHours < 400, "Pedro never learned");

        var runner = FairDeck(withDuties: false);
        var pedro = Hand(runner, "Pedro");
        var juan = Hand(runner, "Juan");
        // Juan thinks Pedro young and careless; Pedro will learn from him, but not the other way.
        runner.Campaign.ChangeRelationship(pedro.Id, juan.Id, RelationshipDimension.Respect, -40, "test");
        var refused = runner.SetMentor(pedro.Id, juan.Id, "skill.carpentry");
        Assert.False(refused.Succeeded);
        Assert.Contains("will not learn", refused.Reason);
        Assert.False(runner.SetMentor(juan.Id, pedro.Id, "skill.carpentry").Succeeded);
    }

    [Fact]
    public void CargoBreakingLooseCatchesTheWornOutAndSaysWhy()
    {
        var hurt = 0;
        foreach (var seed in Enumerable.Range(1, 10))
        {
            var runner = TestDeckScenario.Create(Shipped, (ulong)seed);
            var ship = runner.Campaign.Voyage!;
            ship.WeatherId = "weather.storm";
            ship.WeatherUntilTick = long.MaxValue;
            foreach (var hand in runner.Campaign.Crew)
            {
                hand.Needs.Fatigue = 70;
            }

            runner.Advance(12 * Hour);
            var entries = runner.Campaign.Chronicle.Entries.Where(e => e.Kind == ChronicleEntryKind.CargoInjury).ToList();
            hurt += entries.Count;
            Assert.All(entries, e => Assert.Contains("unlashed", e.Get(ChronicleParameters.Reason)));
            if (entries.Count > 0)
            {
                Assert.Contains("the cargo not lashed", new ChronicleFormatter(runner.Campaign, runner.Content).Format(entries[0]));
            }
        }

        Assert.True(hurt > 0);
    }

    [Fact]
    public void TheCrewsLifeSurvivesSaveAndContinuesIdentically()
    {
        var runner = FairDeck();
        runner.SetRationing(true);
        var pedro = Hand(runner, "Pedro");
        runner.SetMentor(pedro.Id, Hand(runner, "Diego").Id, "skill.rigging");
        pedro.Health.AddCondition("condition.fever");
        Sail(runner, 3 * Day + 7 * Hour, id => id.StartsWith("event.complaint", StringComparison.Ordinal) ? "promise" : null);

        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.NotEmpty(loaded.Campaign.Life.Grievances);
        Assert.True(loaded.Campaign.Life.Mentors.ContainsKey(pedro.Id));

        runner.Advance(12 * Hour);
        loaded.Advance(12 * Hour);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
