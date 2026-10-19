using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.Events;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class EventTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static (SimulationRunner Runner, List<CharacterId> Crew) Setup(ulong seed = CampaignState.DefaultSeed, bool withDuties = true)
    {
        var runner = TestDeckScenario.Create(Shipped, seed, withDuties);
        return (runner, runner.Campaign.Characters.Keys.Order().ToList());
    }

    private static void Raise(SimulationRunner runner, string id, CharacterId? subject = null, CharacterId? other = null) =>
        runner.Campaign.RaiseEvent(new PendingEvent(id, subject, other, runner.Campaign.Clock.Tick));

    [Fact]
    public void ShippedEventsLoad()
    {
        Assert.True(Shipped.Events.Count >= 12);
        Assert.Contains("event.man_overboard", Shipped.Events.Keys);
    }

    [Fact]
    public void EventsHappenAtSeaAndWaitForTheCaptain()
    {
        var (runner, _) = Setup(seed: 2);

        for (var hour = 0; hour < 20 * 24 && runner.Campaign.PendingEvent is null; hour++)
        {
            runner.Advance(60);
        }

        var pending = runner.Campaign.PendingEvent;
        Assert.NotNull(pending);
        runner.Advance(5 * 24 * 60);
        Assert.Equal(pending, runner.Campaign.PendingEvent);
        Assert.NotNull(runner.DescribePendingEvent());
    }

    [Fact]
    public void NothingHappensWithoutAVoyage()
    {
        var runner = TestDeckScenario.Create(Shipped, withVoyage: false);

        runner.Advance(10 * 24 * 60);

        Assert.Null(runner.Campaign.PendingEvent);
    }

    [Fact]
    public void EventsFollowTheSeed()
    {
        string Sequence(ulong seed)
        {
            var (runner, _) = Setup(seed);
            var seen = new List<string>();
            for (var hour = 0; hour < 15 * 24; hour++)
            {
                runner.Advance(60);
                if (runner.Campaign.PendingEvent is { } pending)
                {
                    seen.Add(pending.DefinitionId);
                    runner.ResolveEvent(runner.OfferedChoices().First(c => c.Available).Choice.Id);
                }
            }

            return string.Join(",", seen);
        }

        Assert.Equal(Sequence(9), Sequence(9));
        Assert.NotEqual(Sequence(9), Sequence(10));
    }

    [Fact]
    public void AVoyageHasAHandfulOfEventsNotAFlood()
    {
        var (runner, _) = Setup(seed: 4);
        var count = 0;
        while (runner.Campaign.Voyage!.Outcome == Voyage.VoyageOutcome.UnderWay && runner.Campaign.Clock.Day < 40)
        {
            runner.Advance(60);
            if (runner.Campaign.PendingEvent is not null)
            {
                count++;
                runner.ResolveEvent(runner.OfferedChoices().First(c => c.Available).Choice.Id);
            }
        }

        Assert.InRange(count, 4, 20);
    }

    [Fact]
    public void SailingOnLeavesTheManLostAndTheCrewResentful()
    {
        var (runner, crew) = Setup();
        Raise(runner, "event.man_overboard", crew[3]);

        var (result, resolution) = runner.ResolveEvent("on");

        Assert.True(result.Succeeded);
        Assert.Null(runner.Campaign.PendingEvent);
        Assert.Equal(SurvivalState.Missing, runner.Campaign.Characters[crew[3]].SurvivalState);
        Assert.True(runner.Campaign.Relationships[(crew[1], crew[0])].Trust < 30);
        Assert.Contains("Pedro", resolution!.Text);
    }

    [Fact]
    public void CatchingRainFillsTheCasks()
    {
        var (runner, _) = Setup();
        var before = runner.Campaign.SharedStore.Count("item.water");
        Raise(runner, "event.rain_squall");

        runner.ResolveEvent("catch");

        Assert.Equal(before + 30, runner.Campaign.SharedStore.Count("item.water"));
    }

    [Fact]
    public void AChoiceTheHoldCannotPayForIsRefused()
    {
        var (runner, crew) = Setup();
        runner.Campaign.SharedStore.TryRemove("item.wine", runner.Campaign.SharedStore.Count("item.wine"));
        Raise(runner, "event.fever", crew[2]);

        Assert.False(runner.OfferedChoices().Single(c => c.Choice.Id == "rest").Available);
        Assert.False(runner.ResolveEvent("rest").Result.Succeeded);
        Assert.NotNull(runner.Campaign.PendingEvent);
        Assert.True(runner.ResolveEvent("work").Result.Succeeded);
    }

    [Fact]
    public void OddsComeFromTheBestPersonForTheJob()
    {
        var (runner, crew) = Setup();
        Raise(runner, "event.man_overboard", crew[3]);

        var offered = runner.OfferedChoices().ToDictionary(c => c.Choice.Id);

        // The boat: 35% + 12% for each level of the crew's best seamanship (Martín, 3).
        Assert.Equal(0.71, offered["boat"].Chance!.Value, 3);
        // The line depends on Pedro himself, who has no seamanship.
        Assert.Equal(0.25, offered["line"].Chance!.Value, 3);
        Assert.Null(offered["on"].Chance);
        Assert.Equal("likely", SimulationRunner.ChanceInWords(0.71));
    }

    [Fact]
    public void AQuarrelInvolvesTwoPeopleWhoThinkBadlyOfEachOther()
    {
        var (runner, _) = Setup(seed: 3);
        for (var hour = 0; hour < 40 * 24; hour++)
        {
            runner.Advance(60);
            if (runner.Campaign.PendingEvent is { DefinitionId: "event.quarrel" } quarrel)
            {
                var relationship = runner.Campaign.Relationships[(quarrel.Subject!.Value, quarrel.Other!.Value)];
                var strained = runner.Campaign.Characters[quarrel.Subject.Value].Needs.Stress >= 55;
                Assert.True(relationship.Trust + relationship.Respect + relationship.Affection <= -15 || strained);
                return;
            }

            if (runner.Campaign.PendingEvent is not null)
            {
                runner.ResolveEvent(runner.OfferedChoices().First(c => c.Available).Choice.Id);
            }

            if (runner.Campaign.Voyage!.Outcome != Voyage.VoyageOutcome.UnderWay)
            {
                break;
            }
        }
    }

    [Fact]
    public void DecisionsAreWrittenIntoTheChronicle()
    {
        var (runner, crew) = Setup();
        Raise(runner, "event.quarrel", crew[2], crew[3]);
        runner.ResolveEvent("fists");

        var entry = runner.Campaign.Chronicle.Entries.Last(e => e.Kind == ChronicleEntryKind.EventResolved);
        var text = new ChronicleFormatter(runner.Campaign, Shipped).Format(entry);

        Assert.StartsWith("A knife drawn Let them settle it with fists, away from the knife: They fight it out", text);
        Assert.Equal([crew[2], crew[3]], entry.Characters);
    }

    [Fact]
    public void ShortRationsMakeTheStoresLastButStrainTheCrew()
    {
        var (full, _) = Setup(seed: 12);
        var (rationed, _) = Setup(seed: 12);
        rationed.SetRationing(true);

        full.Advance(6 * 24 * 60);
        rationed.Advance(6 * 24 * 60);

        Assert.True(rationed.Campaign.SharedStore.Count("item.water") > full.Campaign.SharedStore.Count("item.water") + 15);
        Assert.True(rationed.Campaign.Characters.Values.Average(c => c.Needs.Stress) > full.Campaign.Characters.Values.Average(c => c.Needs.Stress) + 20);
        Assert.Contains(rationed.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.RationingChanged);
    }

    [Fact]
    public void DisheartenedCrewWorkMoreSlowly()
    {
        double PumpedIn30Minutes(double stress)
        {
            var (runner, crew) = Setup(withDuties: false);
            runner.Campaign.Voyage!.Apply(new Voyage.ShipEffects { Bilge = 50 }, 1);
            runner.Campaign.Characters[crew[3]].Needs.Stress = stress;
            runner.AssignTask(crew[3], "task.pump_bilge");
            runner.Advance(5);
            var before = runner.Campaign.Voyage.Bilge;
            runner.Advance(30);
            return before - runner.Campaign.Voyage.Bilge;
        }

        Assert.True(PumpedIn30Minutes(80) < PumpedIn30Minutes(10) * 0.9);
    }

    [Fact]
    public void AWaitingEventSurvivesSaveAndLoad()
    {
        var (runner, crew) = Setup();
        runner.Advance(3 * 24 * 60);
        Raise(runner, "event.quarrel", crew[2], crew[3]);
        runner.SetRationing(true);
        var json = SaveSerializer.Serialize(runner.Campaign);

        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);

        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.Equal(runner.Campaign.PendingEvent, loaded.Campaign.PendingEvent);
        Assert.True(loaded.Campaign.Rationing);
        Assert.Equal(runner.ResolveEvent("hear").Resolution!.Succeeded, loaded.ResolveEvent("hear").Resolution!.Succeeded);
    }

    [Fact]
    public void FormatElevenSaveLoadsWithNothingWaiting()
    {
        var root = JsonNode.Parse(SaveSerializer.Serialize(Setup().Runner.Campaign))!.AsObject();
        root["schemaVersion"] = 11;
        foreach (var field in new[] { "pendingEvent", "eventHistory", "lastEventTick", "rationing" })
        {
            root.Remove(field);
        }

        var loaded = SaveSerializer.Deserialize(root.ToJsonString(), Shipped);

        Assert.Null(loaded.PendingEvent);
        Assert.Empty(loaded.EventHistory);
        Assert.False(loaded.Rationing);
    }

    [Theory]
    [InlineData("unknown event")]
    [InlineData("unknown subject")]
    [InlineData("future")]
    public void TamperedEventsAreRejected(string tampering)
    {
        var (runner, crew) = Setup();
        Raise(runner, "event.fever", crew[2]);
        var root = JsonNode.Parse(SaveSerializer.Serialize(runner.Campaign))!.AsObject();
        var pending = root["pendingEvent"]!;
        switch (tampering)
        {
            case "unknown event": pending["event"] = "event.kraken"; break;
            case "unknown subject": pending["subject"] = 999; break;
            case "future": pending["tick"] = 99_999_999; break;
        }

        Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(root.ToJsonString(), Shipped));
    }

    [Fact]
    public void EventsWithBadReferencesAreRejected()
    {
        var bad = Shipped.Events["event.rain_squall"] with
        {
            Choices = [new EventChoice { Id = "x", Text = "x", Success = new EventOutcome { Text = "x", Effects = [new EventEffect { Item = "item.ambrosia", Amount = 1 }] } }],
        };

        var ex = Assert.Throws<ContentValidationException>(() => new GameContent(Shipped.Rules, Shipped.Skills.Values, Shipped.Items.Values, Shipped.Tasks.Values,
            Shipped.Locations.Values, Shipped.Attributes.Values, Shipped.Traits.Values, Shipped.Conditions.Values, Shipped.Appearance,
            Shipped.Languages.Values, Shipped.Statuses.Values, Shipped.Voyage, [bad]));
        Assert.Contains(ex.Errors, e => e.Contains("item.ambrosia", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryEventTextNamesOnlyKnownPlaceholders()
    {
        var known = new[] { "{subject}", "{other}", "{captain}", "{ship}", "{Ship}", "{destination}" };
        foreach (var definition in Shipped.Events.Values)
        {
            var texts = new[] { definition.Title, definition.Text }
                .Concat(definition.Choices.SelectMany(c => new[] { c.Text, c.Success.Text, c.Failure?.Text ?? "" }));
            foreach (var text in texts)
            {
                var placeholders = System.Text.RegularExpressions.Regex.Matches(text, "\\{[^}]*\\}").Select(m => m.Value);
                Assert.All(placeholders, p => Assert.Contains(p, known));
            }
        }
    }
}
