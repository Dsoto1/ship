using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Knowledge;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class PortTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Port(ulong seed = CampaignState.DefaultSeed) => ExpeditionScenario.Create(Shipped, seed);

    private static CharacterId Id(SimulationRunner runner, string person) => runner.CharacterFor(person)!.Id;

    private static CharacterId Captain(SimulationRunner runner) => runner.Campaign.CaptainId!.Value;

    private static string? Doing(SimulationRunner runner, string person) =>
        runner.Campaign.ActiveTasks.TryGetValue(Id(runner, person), out var task) ? task.DefinitionId : null;

    /// <summary>Walks the captain to stand next to someone (or a cell) and waits until there.</summary>
    private static void Approach(SimulationRunner runner, GridPosition target)
    {
        var location = runner.Content.Locations[runner.Campaign.Placements[Captain(runner)].LocationId];
        var taken = runner.Campaign.Placements.Values.Select(p => p.Cell).ToHashSet();
        var next = new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1) }
            .Select(d => new GridPosition(target.X + d.Item1, target.Y + d.Item2))
            .First(cell => location.IsWalkable(cell) && !taken.Contains(cell));
        Assert.True(runner.MoveCharacter(Captain(runner), next).Succeeded);
        for (var minute = 0; minute < 90 && runner.Campaign.Placements[Captain(runner)].IsMoving; minute++)
        {
            runner.Advance(1);
        }

        Assert.False(runner.Campaign.Placements[Captain(runner)].IsMoving);
    }

    // People at work move about (a docker carries his crate away), so keep after them until close.
    private static void ApproachPerson(SimulationRunner runner, string person)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            Approach(runner, runner.Campaign.Placements[Id(runner, person)].CellAt(runner.Campaign.Clock.Tick));
            if (runner.WhyCannotSpeak(Id(runner, person)) is null)
            {
                return;
            }
        }

        Assert.Fail(runner.WhyCannotSpeak(Id(runner, person)) ?? person);
    }

    private static void AdvanceTo(SimulationRunner runner, int day, int hour, int minute = 0) =>
        runner.Advance((int)((day - 1L) * SimulationClock.TicksPerDay + hour * 60 + minute - runner.Campaign.Clock.Tick));

    [Fact]
    public void TheCampaignOpensOnABusyWaterfront()
    {
        var runner = Port();

        Assert.Equal(CampaignPhase.PortPreparation, runner.Campaign.Phase);
        Assert.Single(runner.Campaign.Crew);
        Assert.Equal(31, runner.Campaign.Characters.Values.Count(c => c.Membership == Membership.Local));
        Assert.Equal(7000, runner.Campaign.Port!.Funds);
        Assert.Null(runner.Campaign.Voyage);
        Assert.Equal("task.port_carpentry", Doing(runner, "person.bartolo"));
        Assert.Equal("task.study_charts", Doing(runner, "person.velho"));
        Assert.Equal("task.keep_accounts", Doing(runner, "person.leonor"));
    }

    [Fact]
    public void PeopleGoAboutTheirDay()
    {
        var runner = Port();

        AdvanceTo(runner, 1, 12, 30);
        Assert.Equal("task.drink", Doing(runner, "person.bartolo"));
        Assert.Equal("task.pray", Doing(runner, "person.leonor"));

        AdvanceTo(runner, 2, 3);
        Assert.Equal("task.sleep_at_home", Doing(runner, "person.bartolo"));
        Assert.True(runner.IsIndoors(Id(runner, "person.bartolo")));
        Assert.Equal("task.stand_guard", Doing(runner, "person.lope"));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(15)]
    [InlineData(19)]
    [InlineData(23)]
    public void AtEveryHourAlmostEveryoneHasSomewhereToBe(int hour)
    {
        var runner = Port();
        AdvanceTo(runner, 2, hour, 20);

        var idle = runner.Campaign.Characters.Values
            .Where(c => c.Membership == Membership.Local && runner.Campaign.Placements.ContainsKey(c.Id) && !runner.Campaign.ActiveTasks.ContainsKey(c.Id))
            .Select(c => c.Name).ToList();

        Assert.True(idle.Count <= 1, $"Idle at {hour}:20: {string.Join(", ", idle)}");
    }

    [Fact]
    public void LocalsAreNotCrew()
    {
        var runner = Port();
        AdvanceTo(runner, 2, 8);

        var bartolo = runner.Campaign.Characters[Id(runner, "person.bartolo")];
        Assert.Equal(0, bartolo.Needs.Hunger);
        Assert.Equal(0, bartolo.Needs.Thirst);
        Assert.True(runner.Campaign.Characters[Captain(runner)].Needs.Hunger > 0);
    }

    [Fact]
    public void WatchingSomeoneWorkShowsTheirRealSkill()
    {
        var runner = Port();
        var bartolo = Id(runner, "person.bartolo");
        Assert.False(runner.CaptainSees(bartolo));
        Assert.Null(runner.Campaign.Knowledge.Assess(bartolo, "skill.carpentry"));

        ApproachPerson(runner, "person.bartolo");
        Assert.True(runner.CaptainSees(bartolo));
        runner.Advance(75);

        Assert.Equal(new Assessment(4, KnowledgeSource.Observation, false), runner.Campaign.Knowledge.Assess(bartolo, "skill.carpentry"));
    }

    [Fact]
    public void WorkOutOfSightTeachesNothing()
    {
        var runner = Port();

        runner.Advance(3 * 60);

        Assert.Null(runner.Campaign.Knowledge.Assess(Id(runner, "person.bartolo"), "skill.carpentry"));
    }

    [Fact]
    public void ASecretMeetingCanBeSeen()
    {
        var runner = Port();
        AdvanceTo(runner, 1, 21, 10);
        Approach(runner, new GridPosition(35, 11));
        runner.Advance(10);

        var velho = Id(runner, "person.velho");
        Assert.NotNull(runner.Campaign.Knowledge.Assess(velho, "fact.velho_brennish"));
        var noticed = runner.Campaign.Chronicle.Entries.Single(e => e.Kind == ChronicleEntryKind.Noticed && e.Characters[0] == velho);
        Assert.Equal("You noticed something about Duarte Velho: seen meeting the Brennish trader in the alley after dark.",
            new ChronicleFormatter(runner.Campaign, Shipped).Format(noticed));
    }

    [Fact]
    public void TalkingNeedsYouToWalkOver()
    {
        var runner = Port();
        var velho = Id(runner, "person.velho");

        var far = runner.TalkTo(velho);
        Assert.False(far.Result.Succeeded);
        Assert.Contains("Walk over", far.Result.Reason);

        ApproachPerson(runner, "person.velho");
        var near = runner.TalkTo(velho);

        Assert.True(near.Result.Succeeded);
        Assert.Contains("Ember Coast", near.Said);
        Assert.Equal(new Assessment(5, KnowledgeSource.Claim, false), runner.Campaign.Knowledge.Assess(velho, "skill.navigation"));
    }

    [Fact]
    public void WhoYouAskMatters()
    {
        var runner = Port();
        var rodrigo = Id(runner, "person.rodrigo");

        ApproachPerson(runner, "person.leonor");
        var clerk = runner.AskAbout(Id(runner, "person.leonor"), rodrigo);
        Assert.Contains("couldn't say", clerk.Said);
        Assert.Null(runner.Campaign.Knowledge.Assess(rodrigo, "skill.rigging"));

        AdvanceTo(runner, 1, 13, 15);
        ApproachPerson(runner, "person.andres");
        var bosun = runner.AskAbout(Id(runner, "person.andres"), rodrigo);

        Assert.Contains("Talks a good deal more than he knows", bosun.Said);
        var entry = runner.Campaign.Knowledge.About(rodrigo).Single(e => e.Topic == "skill.rigging" && e.Source == KnowledgeSource.Reference);
        Assert.Equal((3, KnowledgeSource.Reference, Id(runner, "person.andres")), (entry.Level, entry.Source, entry.Informant!.Value));
    }

    [Fact]
    public void AWatchedDemonstrationIsATestAndThePrideRefuse()
    {
        var runner = Port();
        var rodrigo = Id(runner, "person.rodrigo");
        ApproachPerson(runner, "person.rodrigo");

        Assert.True(runner.RequestDemonstration(rodrigo, "skill.rigging").Succeeded);
        ApproachPerson(runner, "person.rodrigo");
        runner.Advance(75);

        Assert.Equal(new Assessment(2, KnowledgeSource.Test, false), runner.Campaign.Knowledge.Assess(rodrigo, "skill.rigging"));

        var velho = Id(runner, "person.velho");
        ApproachPerson(runner, "person.velho");
        Assert.False(runner.RequestDemonstration(velho, "skill.navigation").Succeeded);
        Assert.Equal(-10, runner.Campaign.Relationships[(velho, Captain(runner))].Trust);
    }

    [Fact]
    public void OffersAreWeighed()
    {
        var runner = Port();
        var bartolo = Id(runner, "person.bartolo");
        ApproachPerson(runner, "person.bartolo");

        var low = runner.Offer(bartolo, 900);
        Assert.False(low.Succeeded);
        Assert.Contains("I might come for", low.Reason);

        var insult = runner.Offer(bartolo, 300);
        Assert.False(insult.Succeeded);
        Assert.Equal(-10, runner.Campaign.Relationships[(bartolo, Captain(runner))].Respect);

        Assert.True(runner.Offer(bartolo, 1000).Succeeded);
        var hired = runner.Campaign.Characters[bartolo];
        Assert.Equal(Membership.Crew, hired.Membership);
        Assert.Equal(TestDeckScenario.LocationId, runner.Campaign.Placements[bartolo].LocationId);
        Assert.Contains("task.patch_hull", runner.Campaign.DutiesOf(bartolo));
        Assert.Equal(6000, runner.Campaign.Port!.Funds);
    }

    [Fact]
    public void SomeWillNotSailWithOthers()
    {
        var runner = Port();
        ApproachPerson(runner, "person.wat");
        Assert.True(runner.Offer(Id(runner, "person.wat"), 600).Succeeded);

        ApproachPerson(runner, "person.rodrigo");
        var refusal = runner.Offer(Id(runner, "person.rodrigo"), 850);

        Assert.False(refusal.Succeeded);
        Assert.Contains("Brennish", refusal.Reason);
    }

    [Fact]
    public void PeopleWhoWaitTooLongGoElsewhere()
    {
        var runner = Port();
        var velho = Id(runner, "person.velho");

        AdvanceTo(runner, 2, 14, 1);

        Assert.Contains(velho, runner.Campaign.Port!.Gone);
        Assert.False(runner.Campaign.Placements.ContainsKey(velho));
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.LeftPort && e.Characters[0] == velho);
    }

    [Fact]
    public void TheChandlerSellsOut()
    {
        var runner = Port();
        Assert.Equal(16, runner.Campaign.Port!.Stock[0]);

        Assert.True(runner.Buy(0).Succeeded);
        AdvanceTo(runner, 2, 0, 1);

        Assert.Equal(16 - 1 - 3, runner.Campaign.Port.Stock[0]);
        for (var i = 0; i < 5; i++)
        {
            Assert.True(runner.Buy(4).Succeeded || i >= 4);
        }

        Assert.False(runner.Buy(4).Succeeded);
    }

    [Fact]
    public void TheConvoyWillNotWait()
    {
        var runner = Port();

        AdvanceTo(runner, 2, 18, 1);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.ConvoyWarning);

        AdvanceTo(runner, 3, 18, 1);
        Assert.Equal(2, runner.Campaign.Chronicle.Entries.Count(e => e.Kind == ChronicleEntryKind.LateFee));
        Assert.Equal(7000 - 600, runner.Campaign.Port!.Funds);
    }

    [Fact]
    public void TheShipSailsWithEnoughHandsAndLeavesThePortBehind()
    {
        var runner = Port();
        var hires = new[] { ("person.tome", 150), ("person.gil", 800), ("person.pascual", 450) };
        foreach (var (person, advance) in hires)
        {
            ApproachPerson(runner, person);
            Assert.True(runner.Offer(Id(runner, person), advance).Succeeded, person);
        }

        Assert.False(ExpeditionScenario.SetSail(runner).Succeeded);
        ApproachPerson(runner, "person.wat");
        Assert.True(runner.Offer(Id(runner, "person.wat"), 600).Succeeded);

        Assert.True(ExpeditionScenario.SetSail(runner).Succeeded);
        Assert.Equal(CampaignPhase.Voyage, runner.Campaign.Phase);
        Assert.Null(runner.Campaign.Port);
        Assert.Equal(5, runner.Campaign.Crew.Count());
        Assert.All(runner.Campaign.Placements, p => Assert.Equal(TestDeckScenario.LocationId, p.Value.LocationId));
        runner.Advance(24 * 60);
        Assert.All(runner.Campaign.Characters.Values.Where(c => c.Membership == Membership.Local), c => Assert.False(runner.Campaign.ActiveTasks.ContainsKey(c.Id)));
    }

    [Fact]
    public void ThePortSurvivesSaveAndContinuesIdentically()
    {
        var runner = Port(3);
        ApproachPerson(runner, "person.bartolo");
        runner.TalkTo(Id(runner, "person.bartolo"));
        runner.Offer(Id(runner, "person.bartolo"), 1000);
        runner.Buy(1);
        runner.Advance(5 * 60 + 13);
        var json = SaveSerializer.Serialize(runner.Campaign);

        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);

        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        runner.Advance(20 * 60);
        loaded.Advance(20 * 60);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }

    [Fact]
    public void FormatFourteenSaveLoadsWithEveryoneCrew()
    {
        var root = JsonNode.Parse(SaveSerializer.Serialize(TestDeckScenario.Create(Shipped).Campaign))!.AsObject();
        root["schemaVersion"] = 14;
        root.Remove("port");
        root["recruitment"] = null;
        foreach (var character in root["characters"]!.AsArray())
        {
            character!.AsObject().Remove("membership");
            character.AsObject().Remove("personId");
        }

        var loaded = SaveSerializer.Deserialize(root.ToJsonString(), Shipped);

        Assert.Null(loaded.Port);
        Assert.All(loaded.Characters.Values, c => Assert.Equal(Membership.Crew, c.Membership));
    }

    // What someone does, minute by minute, from now for the given minutes.
    private static List<string> Watch(SimulationRunner runner, string person, int minutes)
    {
        var seen = new List<string>();
        for (var minute = 0; minute < minutes; minute++)
        {
            runner.Advance(1);
            if (Doing(runner, person) is { } task && (seen.Count == 0 || seen[^1] != task))
            {
                seen.Add(task);
            }
        }

        return seen;
    }

    [Theory]
    [InlineData("person.andres", 9, "task.haul_cargo", "task.deliver_cargo")]
    [InlineData("person.bartolo", 9, "task.port_carpentry", "task.fetch_timber", "task.fit_timber")]
    [InlineData("person.catalina", 9, "task.land_catch", "task.carry_catch", "task.sell_fish")]
    [InlineData("person.marta", 9, "task.make_rope", "task.lay_rope")]
    [InlineData("person.gil", 9, "task.coopering", "task.deliver_cask")]
    [InlineData("person.simon", 8, "task.mend_sails", "task.deliver_canvas")]
    public void WorkGoesRoundInLoops(string person, int hour, params string[] loop)
    {
        var runner = Port();
        AdvanceTo(runner, 1, hour);

        var seen = Watch(runner, person, 150);

        // Each step in turn, and round again.
        var expected = Enumerable.Repeat(loop, 40).SelectMany(l => l).ToList();
        var start = expected.IndexOf(seen[0]);
        Assert.True(start >= 0, string.Join(", ", seen));
        Assert.True(expected.Skip(start).Take(seen.Count).SequenceEqual(seen), string.Join(", ", seen));
        Assert.True(seen.Count > loop.Length, string.Join(", ", seen));
    }

    [Fact]
    public void CarriedLoadsGoFromPickupToDropoff()
    {
        var runner = Port();
        AdvanceTo(runner, 1, 9);
        var andres = Id(runner, "person.andres");
        var location = runner.Content.Locations["location.bocana"];

        for (var minute = 0; minute < 120; minute++)
        {
            runner.Advance(1);
            if (runner.Campaign.ActiveTasks.TryGetValue(andres, out var task) && task.SpotId is { } spot && !runner.Campaign.Placements[andres].IsMoving)
            {
                var kind = location.FindSpot(spot)!.Kind;
                var carries = runner.Content.Tasks[task.DefinitionId].Carries;
                Assert.Equal(kind == "CarryDropoff", carries == "crate");
            }
        }
    }

    [Fact]
    public void ALoopEndsWhenTheDayMovesOn()
    {
        var runner = Port();
        AdvanceTo(runner, 1, 12, 5);

        Assert.Equal("task.drink", Doing(runner, "person.bartolo"));
    }

    [Fact]
    public void TheRecordSaysWhoToldYouAndHowSureYouCanBe()
    {
        var runner = Port();
        var rodrigo = Id(runner, "person.rodrigo");
        AdvanceTo(runner, 1, 13, 15);
        ApproachPerson(runner, "person.andres");
        runner.AskAbout(Id(runner, "person.andres"), rodrigo);

        var line = KnowledgeText.Lines(runner.Campaign.Knowledge, rodrigo, Shipped, runner.Campaign.Characters).Single(l => l.Topic == "skill.rigging");

        Assert.Equal(("Rigging", "skilled", "Vouched for by Andrés", "Fairly sure"), (line.Name, line.Value, line.Source, line.Confidence));
    }

    private static IReadOnlyList<string> Journal(SimulationRunner runner) => runner.Campaign.Port!.Leads.Select(l => l.LeadId).ToList();

    [Fact]
    public void EveryoneLookingForABerthCanBeFoundThroughALead()
    {
        var rules = Shipped.Port!;
        var pointedTo = rules.AllLeads().Select(l => l.Lead.About).ToHashSet();

        Assert.All(rules.People.Where(p => p.Seeking is not null), p => Assert.Contains(p.Id, pointedTo));
    }

    [Fact]
    public void TheLandladyPointsYouToPeopleWorthFinding()
    {
        var runner = Port();
        AdvanceTo(runner, 1, 10);
        ApproachPerson(runner, "person.ines");

        var (result, said) = runner.TalkTo(Id(runner, "person.ines"));

        Assert.True(result.Succeeded);
        Assert.Equal(["lead.ines_velho", "lead.ines_rodrigo", "lead.ines_nuno"], Journal(runner));
        Assert.Contains("tells you of Duarte Velho, Rodrigo Alba and Nuño Pardo", said);
        var velho = Id(runner, "person.velho");
        Assert.Equal("lead.ines_velho", Assert.Single(runner.LeadsAbout(velho)).Id);
        Assert.Equal(Id(runner, "person.ines"), runner.LeadGiver("lead.ines_velho"));
        Assert.Equal(3, runner.Campaign.Chronicle.Entries.Count(e => e.Kind == ChronicleEntryKind.LeadHeard));
        Assert.StartsWith("Heard of someone worth finding: \"There's a Lusaran pilot",
            new ChronicleFormatter(runner.Campaign, Shipped).Format(runner.Campaign.Chronicle.Entries.First(e => e.Kind == ChronicleEntryKind.LeadHeard)));

        // A lead is a pointer, not knowledge: nothing is yet known of what Velho can do.
        Assert.Empty(runner.Campaign.Knowledge.About(velho));

        var (_, again) = runner.TalkTo(Id(runner, "person.ines"));
        Assert.DoesNotContain("tells you of", again);
        Assert.Equal(3, Journal(runner).Count);
    }

    [Fact]
    public void TheNoticeBoardMustBeReadInPerson()
    {
        var runner = Port();

        Assert.False(runner.ReadNotices().Result.Succeeded);

        Approach(runner, Shipped.Locations["location.bocana"].FindSpot("spot.chamber_board")!.Cell);
        var (result, said) = runner.ReadNotices();

        Assert.True(result.Succeeded);
        Assert.Contains("Ask for Wat", said);
        Assert.Equal(["lead.notice_wat", "lead.notice_ulloa", "lead.notice_tome", "lead.notice_velho"], Journal(runner));
        Assert.Null(runner.LeadGiver("lead.notice_wat"));
        Assert.StartsWith("Nothing new", runner.ReadNotices().Said);
    }

    [Fact]
    public void TheJournalIsSavedAndOldPortsLoadWithAnEmptyOne()
    {
        var runner = Port();
        AdvanceTo(runner, 1, 10);
        ApproachPerson(runner, "person.ines");
        runner.TalkTo(Id(runner, "person.ines"));
        var json = SaveSerializer.Serialize(runner.Campaign);

        Assert.Equal(Journal(runner), Journal(new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped)));

        var old = JsonNode.Parse(json)!.AsObject();
        old["schemaVersion"] = 15;
        old["port"]!.AsObject().Remove("leads");
        Assert.Empty(new SimulationRunner(SaveSerializer.Deserialize(old.ToJsonString(), Shipped), Shipped).Campaign.Port!.Leads);
    }

    [Fact]
    public void ThePilotLooksTheGarzaOverInTheAfternoon()
    {
        var runner = Port();
        AdvanceTo(runner, 1, 14, 30);

        Assert.Equal("task.inspect_ship", Doing(runner, "person.velho"));
    }
}
