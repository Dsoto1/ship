using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Knowledge;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Port;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class PortTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Port(ulong seed = CampaignState.DefaultSeed) => ExpeditionScenario.Create(Shipped, seed);

    internal static CharacterId Id(SimulationRunner runner, string person) => runner.CharacterFor(person)!.Id;

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
    internal static void ApproachPerson(SimulationRunner runner, string person)
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

    // Walks over and talks someone through to what they want for a berth: the terms topic and what it needs.
    internal static void HearTerms(SimulationRunner runner, string person)
    {
        ApproachPerson(runner, person);
        var id = Id(runner, person);
        var topics = runner.CharacterFor(person)!.PersonId is { } pid ? Shipped.Port!.People.Single(p => p.Id == pid).Conversation?.Topics ?? [] : [];
        void Discuss(Port.TopicDefinition topic)
        {
            foreach (var before in topic.After)
            {
                Discuss(topics.Single(t => t.Id == before));
            }

            if (!runner.Campaign.Port!.Discussed.Contains((id, topic.Id)))
            {
                Assert.True(runner.Discuss(id, topic.Id).Result.Succeeded, topic.Id);
            }
        }

        runner.BeginConversation(id);
        foreach (var terms in topics.Where(t => t.Terms))
        {
            Discuss(terms);
        }

        Assert.True(runner.TermsHeard(id), person);
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
        Assert.Contains("Talk with them first", runner.Offer(bartolo, 1000).Reason);
        HearTerms(runner, "person.bartolo");
        var respect = runner.Campaign.Relationships[(bartolo, Captain(runner))].Respect;

        var low = runner.Offer(bartolo, 900);
        Assert.False(low.Succeeded);
        Assert.Contains("I might come for", low.Reason);

        var insult = runner.Offer(bartolo, 300);
        Assert.False(insult.Succeeded);
        Assert.Equal(respect - 10, runner.Campaign.Relationships[(bartolo, Captain(runner))].Respect);

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
        HearTerms(runner, "person.wat");
        Assert.True(runner.Offer(Id(runner, "person.wat"), 600).Succeeded);

        HearTerms(runner, "person.rodrigo");
        var refusal = runner.Offer(Id(runner, "person.rodrigo"), 850);

        Assert.False(refusal.Succeeded);
        Assert.Contains("Brennish", refusal.Reason);
    }

    [Fact]
    public void PeopleWhoWaitTooLongGoElsewhere()
    {
        var runner = Port();
        var velho = Id(runner, "person.velho");

        AdvanceTo(runner, 4, 14, 1);

        Assert.Contains(velho, runner.Campaign.Port!.Gone);
        Assert.False(runner.Campaign.Placements.ContainsKey(velho));
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.LeftPort && e.Characters[0] == velho);
    }

    [Fact]
    public void TheChandlerSellsOut()
    {
        var runner = Port();
        Assert.Equal(30, runner.Campaign.Port!.Stock[0]);

        Assert.True(runner.Buy(0).Succeeded);
        AdvanceTo(runner, 2, 0, 1);

        Assert.Equal(30 - 1 - 3, runner.Campaign.Port.Stock[0]);
        for (var i = 0; i < 5; i++)
        {
            Assert.True(runner.Buy(4).Succeeded || i >= 4);
        }

        Assert.False(runner.Buy(4).Succeeded);
    }

    [Fact]
    public void AFullCrewCanBeVictualledForTheLongPassage()
    {
        // The chandler's stock, bought on the first morning, must be able to carry every berth filled
        // through the slow end of the passage: the limit should be the purse and the days, not the shop.
        var runner = Port();
        var rules = runner.Content.Port!;
        var aboard = rules.Berths + 1;
        for (var lot = 0; lot < rules.Goods.Count; lot++)
        {
            if (runner.DaysOf(rules.Goods[lot].Item, 1, aboard) > 0)
            {
                while (runner.Buy(lot).Succeeded)
                {
                }
            }
        }

        var (food, water) = runner.SupplyForecast(aboard);
        Assert.True(Math.Min(food, water) > ExpeditionScenario.PassageDays.High, $"food {food:0}, water {water:0} days");
    }

    [Fact]
    public void EachLotSaysHowManyDaysItAdds()
    {
        var runner = Port();
        var before = runner.SupplyForecast(8).WaterDays;
        var adds = runner.DaysOf("item.water", runner.Content.Port!.Goods[0].Quantity, 8);
        Assert.True(runner.Buy(0).Succeeded);
        Assert.Equal(before + adds, runner.SupplyForecast(8).WaterDays, 3);
        Assert.Equal(0, runner.DaysOf("item.rope", 2, 8));
    }

    [Fact]
    public void TheConvoyWillNotWait()
    {
        var runner = Port();

        AdvanceTo(runner, 4, 18, 1);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.ConvoyWarning);

        AdvanceTo(runner, 5, 18, 1);
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
            HearTerms(runner, person);
            Assert.True(runner.Offer(Id(runner, person), advance).Succeeded, person);
        }

        Assert.False(ExpeditionScenario.SetSail(runner).Succeeded);
        HearTerms(runner, "person.wat");
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
        HearTerms(runner, "person.bartolo");
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
    [InlineData("person.bartolo", 9, "task.port_carpentry", "task.fetch_timber", "task.fit_timber")]
    [InlineData("person.catalina", 9, "task.land_catch", "task.carry_catch", "task.sell_fish")]
    [InlineData("person.marta", 9, "task.make_rope", "task.lay_rope")]
    [InlineData("person.gil", 9, "task.coopering", "task.deliver_cask")]
    [InlineData("person.simon", 8, "task.mend_sails", "task.deliver_canvas")]
    [InlineData("person.andres", 14, "task.haul_cargo", "task.deliver_cargo", "task.catch_breath")]
    [InlineData("person.bartolo", 13, "task.saw_planks", "task.fetch_plank", "task.fit_garza_plank")]
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

        Assert.Equal(("Rigging", "experienced", "Vouched for", "Andrés", "Fair"), (line.Name, line.Value, line.Status, line.Source, line.Confidence));
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

    private static (string? Spot, string? Face) At(SimulationRunner runner, string person)
    {
        var task = runner.Campaign.ActiveTasks[Id(runner, person)];
        var spot = runner.Content.Locations["location.bocana"].FindSpot(task.SpotId!);
        return (spot?.Id, spot?.Face);
    }

    [Fact]
    public void PeopleGatherInPairsAndGroups()
    {
        var runner = Port();

        // The carter and the fishwife gossip at the well, face to face.
        AdvanceTo(runner, 1, 13, 30);
        Assert.Equal("task.gossip", Doing(runner, "person.catalina"));
        Assert.Equal("task.gossip", Doing(runner, "person.paco"));
        var faces = new[] { At(runner, "person.catalina").Face, At(runner, "person.paco").Face }.Order().ToArray();
        Assert.Equal(["E", "W"], faces);

        // The young man steadies the plank while the carpenter works at the Garza's side.
        Assert.Equal("task.steady_plank", Doing(runner, "person.pascual"));
        Assert.StartsWith("task.", Doing(runner, "person.bartolo"));
        Assert.Contains(Doing(runner, "person.bartolo"), new[] { "task.saw_planks", "task.fetch_plank", "task.fit_garza_plank" });

        // The priest buys fish across the stall from the seller.
        var noon = Port();
        AdvanceTo(noon, 1, 12, 20);
        Assert.Equal("task.buy_at_market", Doing(noon, "person.anselmo"));
        Assert.Equal("E", At(noon, "person.anselmo").Face);
    }

    [Fact]
    public void FacingsMustBeCompassPoints()
    {
        var bad = Shipped.Locations["location.bocana"] with
        {
            Spots = [new WorkSpotDefinition { Id = "spot.odd", X = 31, Y = 32, Tasks = ["task.play"], Face = "up" }],
        };
        var errors = new List<string>();

        ContentValidator.ValidateLocation(bad, Shipped.Tasks, errors);

        Assert.Contains(errors, e => e.Contains("faces 'up'"));
    }

    private static int Packages(IReadOnlyList<CargoPackages> stacks, string item) =>
        stacks.Where(s => s.Item == item).Sum(s => s.Count);

    [Fact]
    public void TheGarzaWaitsWithTheCompanysStoresAboard()
    {
        var ready = ShipPreparation.Of(Port().Campaign, Shipped)!;

        Assert.Equal(PreparationStage.AwaitingExpedition, ready.Stage);
        Assert.Empty(ready.OnQuay);
        Assert.Empty(ready.OnDeck);
        Assert.Equal(4, Packages(ready.Aboard, "item.water"));
        Assert.Equal(4, Packages(ready.Aboard, "item.biscuit"));
        Assert.Equal("cask", ready.Aboard.Single(s => s.Item == "item.water").Package);
        Assert.Equal(0, ready.Belongings);
    }

    [Fact]
    public void BoughtStoresWaitOnTheQuayAndGoAboardOverTheNextHours()
    {
        var runner = Port();
        Assert.True(runner.Buy(4).Succeeded);
        Assert.True(runner.Buy(0).Succeeded);

        var bought = ShipPreparation.Of(runner.Campaign, Shipped)!;
        Assert.Equal(PreparationStage.InitialLoading, bought.Stage);
        Assert.Equal(2, Packages(bought.OnQuay, "item.rope"));
        Assert.Equal(1, Packages(bought.OnQuay, "item.water"));
        Assert.Equal(4, Packages(bought.Aboard, "item.water"));
        Assert.Equal(180, runner.Campaign.SharedStore.Count("item.water"));

        runner.Advance(100);
        var halfway = ShipPreparation.Of(runner.Campaign, Shipped)!;
        Assert.Equal(1, Packages(halfway.OnQuay, "item.rope"));
        Assert.Equal(5, Packages(halfway.Aboard, "item.rope"));
        Assert.Equal(1, Packages(halfway.OnDeck, "item.rope"));

        runner.Advance(90);
        var loaded = ShipPreparation.Of(runner.Campaign, Shipped)!;
        Assert.Empty(loaded.OnQuay);
        Assert.Equal(6, Packages(loaded.Aboard, "item.rope"));
        Assert.Equal(5, Packages(loaded.Aboard, "item.water"));
        Assert.Equal(2, Packages(loaded.OnDeck, "item.rope"));

        runner.Advance(5 * 60);
        var stowed = ShipPreparation.Of(runner.Campaign, Shipped)!;
        Assert.Empty(stowed.OnDeck);
        Assert.Equal(6, Packages(stowed.Aboard, "item.rope"));
    }

    [Fact]
    public void ThePreparationStageFollowsCrewStoresAndTheHour()
    {
        var runner = Port();
        var port = runner.Campaign.Port!;
        var rules = Shipped.Port!;
        PreparationStage Stage() => ShipPreparation.StageOf(runner.Campaign, rules);
        var seekers = rules.People.Where(p => p.Seeking is not null).Select(p => Id(runner, p.Id)).ToList();

        Assert.Equal(PreparationStage.AwaitingExpedition, Stage());
        port.MarkHired(seekers[0]);
        Assert.Equal(PreparationStage.InspectionUnderway, Stage());
        runner.Buy(0);
        Assert.Equal(PreparationStage.InitialLoading, Stage());
        port.MarkHired(seekers[1]);
        runner.Buy(1);
        runner.Buy(2);
        Assert.Equal(PreparationStage.TakingShape, Stage());
        port.MarkHired(seekers[2]);
        port.MarkHired(seekers[3]);
        runner.Buy(3);
        runner.Buy(5);
        Assert.Equal(PreparationStage.TakingShape, Stage());
        runner.Buy(0);
        Assert.Equal(PreparationStage.DepartureReady, Stage());

        AdvanceTo(runner, 5, 3);
        Assert.Equal(PreparationStage.DepartureMorning, Stage());
        Assert.Equal(4, ShipPreparation.Of(runner.Campaign, Shipped)!.Belongings);
    }

    [Fact]
    public void PurchasesAreSavedAndOldPortsLoadWithEverythingAboard()
    {
        var runner = Port();
        runner.Buy(0);
        runner.Advance(30);
        var json = SaveSerializer.Serialize(runner.Campaign);
        var restored = SaveSerializer.Deserialize(json, Shipped);

        Assert.Equal(runner.Campaign.Port!.Purchases, restored.Port!.Purchases);
        Assert.Equal(json, SaveSerializer.Serialize(restored));

        var old = JsonNode.Parse(json)!.AsObject();
        old["schemaVersion"] = 16;
        old["port"]!.AsObject().Remove("purchases");
        var upgraded = SaveSerializer.Deserialize(old.ToJsonString(), Shipped);
        Assert.Empty(upgraded.Port!.Purchases);
        Assert.Empty(ShipPreparation.Of(upgraded, Shipped)!.OnQuay);
    }

    [Fact]
    public void EveryItemSaysHowItIsPacked()
    {
        Assert.All(Shipped.Items.Values, item => Assert.Contains(item.PackedIn, Expedition.Simulation.Items.ItemDefinition.Packages));
    }

    private static List<(long Tick, string Weather)> WeatherOver(SimulationRunner runner, int days)
    {
        var seen = new List<(long, string)>();
        for (var minute = 0; minute < days * SimulationClock.TicksPerDay; minute += 10)
        {
            var now = runner.Campaign.Port!.WeatherId!;
            if (seen.Count == 0 || seen[^1].Item2 != now)
            {
                seen.Add((runner.Campaign.Clock.Tick, now));
            }

            runner.Advance(10);
        }

        return seen;
    }

    [Fact]
    public void ThePortsWeatherChangesTheSameWayEveryTime()
    {
        var first = WeatherOver(Port(), 2);
        var again = WeatherOver(Port(), 2);

        Assert.Equal(first, again);
        Assert.Equal("weather.fair", first[0].Weather);
        Assert.True(first.Count >= 3, string.Join(", ", first));
        Assert.NotEqual(first, WeatherOver(Port(seed: 99), 2));
    }

    [Fact]
    public void FogComesOnlyInTheMorning()
    {
        foreach (var seed in new ulong[] { 1, 2, 3, 4, 5, 6 })
        {
            foreach (var (tick, weather) in WeatherOver(Port(seed), 2).Where(w => w.Weather == "weather.fog"))
            {
                var hour = tick % SimulationClock.TicksPerDay / SimulationClock.TicksPerHour;
                Assert.InRange(hour, 3, 9);
            }
        }
    }

    [Fact]
    public void RainSendsPeopleToShelterAndTheyGoBackWhenItStops()
    {
        var runner = Port();
        AdvanceTo(runner, 1, 13, 20);
        Assert.Equal("task.gossip", Doing(runner, "person.catalina"));

        Assert.True(runner.DeveloperSetPortWeather("weather.rain").Succeeded);
        runner.Advance(1);
        Assert.Equal("task.shelter", Doing(runner, "person.catalina"));
        Assert.DoesNotContain(runner.Campaign.ActiveTasks.Values, t => Shipped.Tasks[t.DefinitionId].InRain is not null);

        Assert.True(runner.DeveloperSetPortWeather("weather.fair").Succeeded);
        runner.Advance(1);
        Assert.Equal("task.gossip", Doing(runner, "person.catalina"));
    }

    [Fact]
    public void FogShortensHowFarTheCaptainCanSee()
    {
        var runner = Port();
        AdvanceTo(runner, 1, 10);
        var captain = runner.Campaign.Placements[Captain(runner)].Cell;
        var far = runner.Campaign.Characters.Values
            .Where(c => c.Membership == Membership.Local && runner.CaptainSees(c.Id) && runner.Campaign.Placements.TryGetValue(c.Id, out var p)
                && Math.Max(Math.Abs(p.Cell.X - captain.X), Math.Abs(p.Cell.Y - captain.Y)) is > 7 and <= 14)
            .Select(c => c.Id)
            .ToList();
        Assert.NotEmpty(far);

        runner.DeveloperSetPortWeather("weather.fog");
        Assert.All(far, id => Assert.False(runner.CaptainSees(id)));
    }

    [Fact]
    public void TheWeatherIsSavedAndOldPortsStartFair()
    {
        var runner = Port();
        runner.DeveloperSetPortWeather("weather.rain");
        var json = SaveSerializer.Serialize(runner.Campaign);
        var restored = SaveSerializer.Deserialize(json, Shipped);
        Assert.Equal("weather.rain", restored.Port!.WeatherId);
        Assert.Equal(runner.Campaign.Port!.WeatherUntil, restored.Port.WeatherUntil);

        var old = JsonNode.Parse(json)!.AsObject();
        old["schemaVersion"] = 17;
        old["port"]!.AsObject().Remove("weather");
        old["port"]!.AsObject().Remove("weatherUntil");
        Assert.Equal("weather.fair", SaveSerializer.Deserialize(old.ToJsonString(), Shipped).Port!.WeatherId);
    }

    [Fact]
    public void ThePortHasAmbientScenesOfManyKinds()
    {
        var scenes = Shipped.Port!.Ambient;
        Assert.InRange(scenes.Count, 8, 12);
        Assert.True(scenes.Select(s => s.Kind).Distinct().Count() >= 8);
        Assert.Contains(scenes, s => s.FromHour > s.ToHour);
        Assert.True(scenes.Single(s => s.Id == "ambient.drunk").AtHour(1));
    }

    private static void Hire(SimulationRunner runner, params string[] people)
    {
        foreach (var person in people)
        {
            Assert.True(runner.DeveloperHire(Id(runner, person)).Succeeded, person);
        }
    }

    private static List<WorldLine> LinesOver(SimulationRunner runner, int minutes)
    {
        var lines = new List<WorldLine>();
        for (var i = 0; i < minutes; i++)
        {
            runner.Advance(1);
            lines.AddRange(runner.TakeLines());
        }

        return lines;
    }

    [Fact]
    public void ThePortComesToKnowTheExpeditionByWhatItDoes()
    {
        var runner = Port();
        Assert.Equal(0, runner.Standing);
        Hire(runner, "person.gil");
        Assert.Equal(1, runner.Standing);
        runner.Buy(0);
        runner.Buy(1);
        runner.Buy(2);
        Hire(runner, "person.tome");
        Assert.Equal(2, runner.Standing);
        Hire(runner, "person.wat", "person.pascual");
        runner.Buy(3);
        Assert.Equal(3, runner.Standing);
        Assert.Contains("knows", runner.DescribeStanding());
    }

    [Fact]
    public void NobodyGreetsAnUnknownCaptainButOnceNoticedTheyDo()
    {
        var runner = Port();
        AdvanceTo(runner, 1, 10);
        Approach(runner, new GridPosition(40, 22));
        Assert.DoesNotContain(LinesOver(runner, 90), l => !l.Approach);

        Hire(runner, "person.gil");
        var lines = LinesOver(runner, 120).Where(l => !l.Approach).ToList();
        Assert.NotEmpty(lines);
        Assert.Equal(lines.Count, lines.Select(l => l.Who).Distinct().Count());
        Assert.All(lines, l => Assert.StartsWith(runner.Campaign.Characters[l.Who].Name, l.Text));
    }

    [Fact]
    public void OnceTheExpeditionIsTalkedOfCandidatesComeToTheCaptain()
    {
        var runner = Port();
        AdvanceTo(runner, 1, 9);
        Hire(runner, "person.gil", "person.wat");
        runner.Buy(0);
        runner.Buy(1);
        Assert.Equal(2, runner.Standing);

        var came = LinesOver(runner, 6 * 60).Where(l => l.Approach).ToList();
        Assert.NotEmpty(came);
        var who = came[0].Who;
        Assert.NotNull(runner.PersonOf(who)!.Seeking);
        Assert.Contains(who, runner.Campaign.Port!.TalkedTo);
        Assert.Contains(runner.PersonOf(who)!.Seeking!.Pitch, came[0].Text);
    }

    [Fact]
    public void TheMoneylenderComesForPonsDebtAndMustBeAnswered()
    {
        var runner = Port();
        AdvanceTo(runner, 1, 10);
        Hire(runner, "person.pons");
        var grilo = Id(runner, "person.grilo");
        var lines = LinesOver(runner, 3 * 60);
        Assert.Contains(lines, l => l.Who == grilo && l.Approach && l.Text.Contains("four hundred"));
        Assert.Contains(runner.DeparturePlan(), b => b.Kind == "claim" && b.Who == grilo);

        var funds = runner.Campaign.Port!.Funds;
        Assert.True(runner.PayClaim(grilo).Succeeded);
        Assert.Equal(funds - 400, runner.Campaign.Port.Funds);
        Assert.Empty(runner.OpenClaims());
        Assert.DoesNotContain(runner.DeparturePlan(), b => b.Kind == "claim");
        Assert.Equal(10, runner.Campaign.Relationships[(Id(runner, "person.pons"), Captain(runner))].Trust);
    }

    [Fact]
    public void TheDepartureIsTheExpeditionAsItIs()
    {
        var runner = Port();
        Hire(runner, "person.pascual", "person.tome", "person.gil", "person.wat");
        runner.Buy(0);
        var plan = runner.DeparturePlan();

        var crew = plan.Single(b => b.Kind == "crew").Text;
        Assert.All(new[] { "person.pascual", "person.tome", "person.gil", "person.wat" }, p => Assert.Contains(runner.CharacterFor(p)!.Name, crew));
        Assert.Equal(4, plan.Count(b => b.Kind == "farewell"));
        Assert.Contains(plan, b => b.Kind == "farewell" && b.Who == Id(runner, "person.pascual") && b.Text.Contains("mother"));
        // Staged, not only told: who comes to the quay, and how many wave.
        Assert.Equal([Id(runner, "person.hob")], plan.Single(b => b.Kind == "farewell" && b.Who == Id(runner, "person.wat")).SeenOffBy);
        Assert.Equal([Id(runner, "person.mateo")], plan.Single(b => b.Kind == "farewell" && b.Who == Id(runner, "person.tome")).SeenOffBy);
        Assert.Equal(1, plan.Single(b => b.Kind == "farewell" && b.Who == Id(runner, "person.pascual")).Wavers);
        Assert.Empty(plan.Single(b => b.Kind == "farewell" && b.Who == Id(runner, "person.gil")).SeenOffBy);
        Assert.Contains(plan, b => b.Kind == "cargo" && b.Text.Contains("cask"));
        Assert.Contains(plan, b => b.Kind == "deadline" && b.Text.Contains("hours before the convoy"));
        Assert.Equal("away", plan[^1].Kind);
        Assert.DoesNotContain(plan, b => b.Kind == "claim");
    }

    [Fact]
    public void TheSamePeopleWithTheirHistoryAndStoresGoToSea()
    {
        // CHARACTER_DESIGN §29: port to voyage keeps IDs, relationships, knowledge and inventory.
        var runner = Port();
        AdvanceTo(runner, 1, 10);
        ApproachPerson(runner, "person.bartolo");
        HearTerms(runner, "person.bartolo");
        Assert.True(runner.Offer(Id(runner, "person.bartolo"), 1000).Succeeded);
        Hire(runner, "person.tome", "person.gil", "person.wat");
        runner.Buy(0);
        var crew = runner.Campaign.Port!.Hired.Select(id => (id, runner.Campaign.Characters[id].Name, runner.Campaign.Characters[id].PersonId)).OrderBy(c => c.id).ToList();
        var relationships = runner.Campaign.Relationships.ToDictionary(p => p.Key, p => (p.Value.Trust, p.Value.Respect, p.Value.Affection));
        var knowledge = runner.Campaign.Knowledge.Entries.Count;
        var hold = runner.Campaign.SharedStore.Quantities.ToDictionary(p => p.Key, p => p.Value);
        var chronicle = runner.Campaign.Chronicle.Entries.Count;

        Assert.True(ExpeditionScenario.SetSail(runner).Succeeded);

        Assert.Equal(crew, runner.Campaign.Crew.Where(c => c.Id != Captain(runner)).Select(c => (c.Id, c.Name, c.PersonId)).OrderBy(c => c.Id).ToList());
        Assert.All(relationships, p => Assert.Equal(p.Value, (runner.Campaign.Relationships[p.Key].Trust, runner.Campaign.Relationships[p.Key].Respect, runner.Campaign.Relationships[p.Key].Affection)));
        Assert.Equal(knowledge, runner.Campaign.Knowledge.Entries.Count);
        Assert.Equal(hold, runner.Campaign.SharedStore.Quantities.ToDictionary(p => p.Key, p => p.Value));
        Assert.True(runner.Campaign.Chronicle.Entries.Count >= chronicle);
    }

    [Fact]
    public void RecognitionIsSavedAndOldPortsStartUnknown()
    {
        var runner = Port();
        AdvanceTo(runner, 1, 10);
        Hire(runner, "person.pons");
        LinesOver(runner, 3 * 60);
        runner.RefuseClaim(Id(runner, "person.grilo"));
        var json = SaveSerializer.Serialize(runner.Campaign);
        var restored = SaveSerializer.Deserialize(json, Shipped);
        Assert.Equal(runner.Campaign.Port!.Approached, restored.Port!.Approached);
        Assert.Equal(runner.Campaign.Port.Claims, restored.Port.Claims);
        Assert.Equal(json, SaveSerializer.Serialize(restored));

        var old = JsonNode.Parse(json)!.AsObject();
        old["schemaVersion"] = 18;
        foreach (var field in new[] { "approached", "approaching", "approachingSince", "claims", "greeted", "lastGreeting" })
        {
            old["port"]!.AsObject().Remove(field);
        }

        Assert.Empty(SaveSerializer.Deserialize(old.ToJsonString(), Shipped).Port!.Claims);
    }
}
