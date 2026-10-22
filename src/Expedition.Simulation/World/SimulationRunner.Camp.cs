using System.Globalization;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Shore;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// The camp ashore, the first 72 hours (docs/R6_LANDFALL_CAMP.md; SETTLEMENT_EXPERIENCE §3-6). The same
/// people go ashore with what they really have: from the ship at anchor, everything is still aboard and must
/// be landed by the boat; after a wreck, what the boat saved is ashore and what is left in the wreck may be
/// got at. The captain ranks the priorities and makes four hard choices; the hands take the first work
/// wanted; the weather and the shore decide how it goes. Water, food and signs of people are found by
/// scouting: the captain does not know the place.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>The camp's map.</summary>
    public const string CampLocation = "location.shore_camp";

    // The works each priority sets the hands to, in order.
    private static readonly IReadOnlyDictionary<string, string[]> PriorityTasks = new Dictionary<string, string[]>
    {
        [CampPriorities.Medical] = ["task.build_sick_shelter", "task.tend_sick"],
        [CampPriorities.Water] = ["task.fetch_water"],
        [CampPriorities.Shelter] = ["task.rig_sail_shelter", "task.cut_brush_shelter"],
        [CampPriorities.Stores] = ["task.land_stores"],
        [CampPriorities.Fire] = ["task.make_fire", "task.tend_fire", "task.cook_ashore"],
        [CampPriorities.Food] = ["task.cover_stores", "task.forage_shore"],
        [CampPriorities.Sanitation] = ["task.dig_latrine"],
        [CampPriorities.Scouting] = ["task.scout"],
        [CampPriorities.Building] = ["task.repair_building", "task.clear_ground", "task.set_posts", "task.raise_frame", "task.thatch_roof", "task.fit_out",
            "task.dig_beds", "task.fence_garden", "task.cut_poles", "task.cut_thatch", "task.mend_tools"],
        [CampPriorities.Garden] = ["task.harvest", "task.sow_garden", "task.tend_garden"],
    };

    // What is landed first, by category; then everything else.
    private static readonly string[] LandingOrder = ["water", "food", "tool", "material", "drink"];

    // Spare sail aboard her, in cloths, when she comes to anchor; the boat's own sail after a wreck.
    private const int SpareSailcloth = 6;
    private const int BoatSailcloth = 2;

    // Days of water and of food ashore the hands keep fetching and gathering towards.
    private const double WaterDaysWanted = 4;
    private const double FoodDaysWanted = 4;

    /// <summary>The landing site's description, once ashore.</summary>
    public ShoreSite? CampSite => Campaign.Camp is { } camp && Content.Shore is { } shore ? shore.Site(camp.Site) : null;

    /// <summary>True when the survivors can make camp ashore: the voyage is over and they have not yet.</summary>
    public bool CanGoAshore =>
        Campaign.Camp is null && Content.Shore is not null && Content.Locations.ContainsKey(CampLocation)
        && Campaign.Phase is CampaignPhase.Landfall or CampaignPhase.ShipwreckSurvival && Ashore().Count > 0;

    // Everyone alive of the company.
    private List<Character> Ashore() =>
        Campaign.Crew.Where(c => c.SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated).OrderBy(c => c.Id.Value).ToList();

    /// <summary>
    /// Goes ashore and makes camp. At anchor, the ship's stores are all still aboard her, with her spare
    /// sail, and the boat brings a first load in with the people; after a wreck, what the boat saved is
    /// ashore with the boat's sail, and what is left in a wreck on the coast may be landed.
    /// </summary>
    public CommandResult GoAshore() => GoAshore(null);

    private CommandResult GoAshore(string? atSite)
    {
        if (!CanGoAshore || Content.Shore is not { } shore)
        {
            return CommandResult.Rejected("There is no going ashore now.");
        }

        var wreck = Campaign.Wreck;
        var anchored = wreck is null;
        var siteId = atSite ?? wreck?.Ashore ?? (Coast is { } coast ? coast.Landmarks.OrderBy(l => Math.Abs(l.North)).First().Id : "default");
        var site = shore.Site(siteId);
        var camp = new CampState
        {
            Site = site.Id, Landed = Campaign.Clock.Tick, ShipAtAnchor = anchored, Boat = anchored || wreck!.BoatLaunched, LandedCount = Ashore().Count,
        };

        // What is still afloat: all her hold at anchor; what is left in a wreck on this coast.
        var store = Campaign.SharedStore;
        if (anchored)
        {
            foreach (var item in Content.Items.Values.OrderBy(i => i.Id, StringComparer.Ordinal).Where(i => store.Count(i.Id) > 0))
            {
                var count = store.Count(item.Id);
                camp.PutAfloat(item.Id, count);
                store.TryRemove(item.Id, count);
            }

            camp.PutAfloat("item.sailcloth", SpareSailcloth);
        }
        else
        {
            foreach (var (item, count) in wreck!.Remains.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                camp.PutAfloat(item, count);
            }

            if (wreck.Remains.Count > 0)
            {
                camp.PutAfloat("item.sailcloth", SpareSailcloth / 2);
            }

            if (wreck.BoatLaunched)
            {
                store.TryAdd("item.sailcloth", BoatSailcloth, Content.Items);
            }
        }

        // The carpenter's chest, the nets and the seed chest (R7).
        LandShipsChest(camp, wreck);

        foreach (var known in site.Water.Where(w => w.Known).Select(w => w.Id)
                     .Concat(site.Food.Where(f => f.Known).Select(f => f.Id)).Concat(site.Finds.Where(f => f.Known).Select(f => f.Id)))
        {
            camp.Find(known);
        }

        Campaign.Camp = camp;

        // Everyone to the beach, their ship's work and orders left behind, and the camp's work for duties.
        var location = Content.Locations[CampLocation];
        var people = Ashore();
        var cells = Enumerable.Range(2, 4).SelectMany(y => Enumerable.Range(12, 13).Select(x => new GridPosition(x, y)))
            .Where(location.IsWalkable).ToList();
        for (var i = 0; i < people.Count; i++)
        {
            var person = people[i];
            Campaign.AbandonTask(person.Id);
            Campaign.ClearQueue(person.Id);
            Campaign.SetPlacement(person.Id, new CharacterPlacement(CampLocation, cells[i % cells.Count]));
        }

        AssignCampDuties();

        // At anchor the boat brings the first load in with them.
        if (anchored)
        {
            Land(camp, null);
        }

        Campaign.Record(ChronicleEntryKind.WentAshore, [
            new(ChronicleParameters.To, site.Name), new(ChronicleParameters.Need, people.Count.ToString(CultureInfo.InvariantCulture)),
            new(ChronicleParameters.Reason, anchored ? "anchored" : "wreck"),
        ]);
        return CommandResult.Ok();
    }

    /// <summary>Sets the captain's priorities ashore: every one of <see cref="CampPriorities.Default"/>, in the order wanted.</summary>
    public CommandResult SetCampPriorities(IReadOnlyList<string> priorities)
    {
        if (Campaign.Camp is not { } camp)
        {
            return CommandResult.Rejected("There is no camp ashore.");
        }

        if (priorities.Count != CampPriorities.Default.Count || priorities.Distinct().Count() != priorities.Count
            || priorities.Any(p => !PriorityTasks.ContainsKey(p)))
        {
            return CommandResult.Rejected("Rank every priority once.");
        }

        camp.Priorities = priorities.ToList();
        AssignCampDuties();
        return CommandResult.Ok();
    }

    /// <summary>Sets the captain's four choices ashore.</summary>
    public CommandResult SetCampChoices(CampChoices choices)
    {
        if (Campaign.Camp is not { } camp)
        {
            return CommandResult.Rejected("There is no camp ashore.");
        }

        camp.Choices = choices;
        return CommandResult.Ok();
    }

    // Everyone's duties, in the order of the captain's priorities.
    private void AssignCampDuties()
    {
        if (Campaign.Camp is not { } camp)
        {
            return;
        }

        var duties = camp.Priorities.SelectMany(p => PriorityTasks[p]).Where(Content.Tasks.ContainsKey).ToList();
        foreach (var person in Ashore())
        {
            Campaign.SetDuties(person.Id, duties);
        }
    }

    // Whether the camp wants a work now (DutyCondition.Camp).
    private bool CampWants(string need, TaskDefinition task)
    {
        if (Campaign.Camp is not { } camp || CampSite is not { } site)
        {
            return false;
        }

        var people = Ashore().Count;
        var store = Campaign.SharedStore;
        // A work counts what is being built as well as what is done, so hands do not all set to the same.
        double Planned(string work) => camp.Work(work) + Campaign.ActiveTasks.Values
            .Select(t => Content.Tasks[t.DefinitionId]).Where(t => t.Builds == work).Sum(t => t.BuildsAmount);
        var shelterShort = Planned(CampWorks.Shelter) < people;
        var sailcloth = camp.Choices.UseSailcloth && store.Count("item.sailcloth") >= 2 && store.Count("item.rope") >= 1;
        return need switch
        {
            "sail_shelter" => shelterShort && sailcloth,
            "brush_shelter" => shelterShort && site.Brush && !sailcloth,
            "sick_shelter" => Planned(CampWorks.SickShelter) < 1 && Campaign.Crew.Any(SickOrHurt),
            "fire" => Planned(CampWorks.Fire) < 1,
            "fire_low" => camp.Work(CampWorks.Fire) >= 1 && camp.FireUntil < Campaign.Clock.Tick + SimulationClock.TicksPerHour,
            "cooking" => camp.Work(CampWorks.Fire) >= 1 && camp.FireUntil > Campaign.Clock.Tick,
            "water" => CampWater() is not null && Content.Drinks.Sum(d => store.Count(d.Id) * d.ThirstRelief) < WaterDaysWanted * people * DailyThirst,
            "land" => camp.Afloat.Count > 0 && !Raining,
            "cover" => Planned(CampWorks.Cover) < 1 && Content.Foods.Any(f => store.Count(f.Id) > 0),
            "latrine" => Planned(CampWorks.Latrine) < 1,
            "forage" => camp.Choices.Forage && CampFood() is not null
                        && Content.Foods.Sum(f => store.Count(f.Id) * f.HungerRelief) < FoodDaysWanted * people * DailyHunger,
            "scout" => camp.Choices.SendScouts && NextFind(camp, site) is not null
                       && !Campaign.ActiveTasks.Values.Any(t => Content.Tasks[t.DefinitionId].Scouts),
            _ => SettlementWants(need, task),
        };
    }

    // How much thirst and hunger a person works up in a day, at the baseline.
    private double DailyThirst => Content.Rules.BaselineNeedsPerHour.Thirst * 24;
    private double DailyHunger => Content.Rules.BaselineNeedsPerHour.Hunger * 24;

    /// <summary>The best water found, or null: sweet water before brackish, then by what a hand brings in.</summary>
    public WaterSource? CampWater() => Campaign.Camp is { } camp && CampSite is { } site
        ? site.Water.Where(w => camp.Found.Contains(w.Id)).OrderBy(w => w.Kind == "brackish").ThenByDescending(WaterPerHand).FirstOrDefault()
        : null;

    // Measures an hour a hand brings in from a source: less the further it is.
    private static double WaterPerHand(WaterSource source) => source.PerHour * Math.Min(1.5, 12.0 / Math.Max(1, source.Walk));

    /// <summary>The best food found, or null.</summary>
    public FoodSource? CampFood() => Campaign.Camp is { } camp && CampSite is { } site
        ? site.Food.Where(f => camp.Found.Contains(f.Id)).OrderByDescending(f => f.PerHour).FirstOrDefault()
        : null;

    // The next thing a scout can find: water first, then a ruin in plain sight, then food, then the rest,
    // each in the site's order.
    private static (string Id, string Name, string Kind, string? Text)? NextFind(CampState camp, ShoreSite site)
    {
        foreach (var water in site.Water.Where(w => !camp.Found.Contains(w.Id)))
        {
            return (water.Id, water.Name, "water", null);
        }

        foreach (var ruin in site.Finds.Where(f => f.Kind == "ruin" && !camp.Found.Contains(f.Id)))
        {
            return (ruin.Id, ruin.Name, ruin.Kind, ruin.Text);
        }

        foreach (var food in site.Food.Where(f => !camp.Found.Contains(f.Id)))
        {
            return (food.Id, food.Name, "food", null);
        }

        foreach (var find in site.Finds.Where(f => !camp.Found.Contains(f.Id)))
        {
            return (find.Id, find.Name, find.Kind, find.Text);
        }

        return null;
    }

    // What the camp's work brings in this minute (TaskYield.Ashore camp_water or camp_food): the item, and
    // how many an hour at normal pace. Null if there is nothing found to take.
    private (string Item, double PerHour)? CampYield(Character character, string ashore)
    {
        if (ashore == "camp_water" && CampWater() is { } water)
        {
            return (water.Kind == "brackish" ? "item.water_brackish" : "item.water", WaterPerHand(water));
        }

        if (ashore == "camp_food" && CampFood() is { } food)
        {
            // Strange shellfish, picked by someone who does not know which are good, may sicken.
            var knows = character.Skills.Get("skill.foraging") >= 2;
            var item = food.Unfamiliar && !knows && food.Item == "item.shellfish" ? "item.shellfish_strange" : food.Item;
            return (item, food.PerHour);
        }

        return null;
    }

    // Finished camp work: a work built up, the fire made up, a load landed, a scouting trip's finds.
    private void FinishCampWork(Character character, TaskDefinition definition, string? spotId)
    {
        if (Campaign.Camp is not { } camp || Content.Shore is not { } shore)
        {
            return;
        }

        FinishSettlementWork(character, definition, spotId);

        var now = Campaign.Clock.Tick;
        if (definition.Builds is { } work)
        {
            camp.AddWork(work, definition.BuildsAmount);
            if (work == CampWorks.Shelter)
            {
                // Which shelters stand, for the view: sail or brush.
                camp.AddWork(definition.Consumes.ContainsKey("item.sailcloth") ? CampWorks.SailShelters : CampWorks.BrushShelters, 1);
            }

            Campaign.Record(ChronicleEntryKind.CampWorkDone, [
                new(ChronicleParameters.To, work), new(ChronicleParameters.Level, camp.Work(work).ToString("0", CultureInfo.InvariantCulture)),
            ], character.Id);
        }

        if (definition.KeepsFire || definition.Builds == CampWorks.Fire)
        {
            var hours = (long)(shore.Rules.FireHours * SimulationClock.TicksPerHour);
            camp.FireUntil = Math.Min(Math.Max(camp.FireUntil, now) + hours, now + 2 * hours);
        }

        if (definition.Lands)
        {
            Land(camp, character);
        }

        if (definition.Scouts && CampSite is { } site && NextFind(camp, site) is { } next)
        {
            var skill = Math.Max(character.Skills.Get("skill.tracking"), character.Skills.Get("skill.foraging"));
            if (Campaign.Randomness.Tasks.Chance(shore.Rules.ScoutChance + shore.Rules.ScoutChancePerSkill * skill) && camp.Find(next.Id))
            {
                Campaign.Record(ChronicleEntryKind.CampFound, [
                    new(ChronicleParameters.To, next.Name), new(ChronicleParameters.From, next.Id), new(ChronicleParameters.Reason, next.Kind),
                    new(ChronicleParameters.Need, next.Text),
                ], character.Id);
            }
        }
    }

    // A load landed from the ship or the wreck: as much as the boat (or a swimmer) brings, water and food first.
    private void Land(CampState camp, Character? by)
    {
        if (Content.Shore is not { } shore)
        {
            return;
        }

        var room = camp.Boat ? shore.Rules.BoatTripKg : shore.Rules.SwimTripKg;
        // The sail and line for the shelters come first, if the captain means them for that and they are wanted.
        var shelterFirst = camp.Choices.UseSailcloth && camp.Work(CampWorks.Shelter) < Ashore().Count
            && Campaign.SharedStore.Count("item.sailcloth") < 2;
        var order = camp.Afloat.Keys
            .Select(id => Content.Items.TryGetValue(id, out var item) ? item : null).OfType<Items.ItemDefinition>()
            .OrderBy(i => shelterFirst && i.Id is "item.sailcloth" or "item.rope" ? 0 : 1)
            .ThenBy(i => Array.IndexOf(LandingOrder, i.Category) is var k && k >= 0 ? k : LandingOrder.Length).ThenBy(i => i.Id, StringComparer.Ordinal)
            .ToList();
        // No more than half a load of water, so food comes ashore with it.
        var waterRoom = room / 2;
        foreach (var item in order)
        {
            var space = item.Category == "water" ? Math.Min(room, waterRoom) : room;
            var take = Math.Min(camp.Afloat[item.Id], item.WeightKg > 0 ? (int)Math.Floor(space / item.WeightKg) : camp.Afloat[item.Id]);
            if (take <= 0)
            {
                continue;
            }

            camp.TakeAfloat(item.Id, take);
            Campaign.SharedStore.TryAdd(item.Id, take, Content.Items);
            room -= take * item.WeightKg;
            if (item.Category == "water")
            {
                waterRoom -= take * item.WeightKg;
            }
        }

        if (by is not null && room < (camp.Boat ? shore.Rules.BoatTripKg : shore.Rules.SwimTripKg))
        {
            camp.Trips++;
            if (camp.Afloat.Count == 0)
            {
                Campaign.Record(ChronicleEntryKind.CampAllLanded, [new(ChronicleParameters.Level, camp.Trips.ToString(CultureInfo.InvariantCulture))]);
            }
        }
    }

    // True while it rains ashore this hour, by the site's season; worked out from the seed and the hour.
    private bool RainingAshore
    {
        get
        {
            if (CampSite is not { } site)
            {
                return false;
            }

            var z = Campaign.Seed + (ulong)(Campaign.Clock.Tick / SimulationClock.TicksPerHour + 7) * 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            // The wet season rains far more often; the storm, all through.
            var chance = Settling is { } season && WetSeason ? Math.Min(0.6, site.Rain * season.WetRain) : site.Rain;
            return StormAshore || (z >> 11) * (1.0 / (1UL << 53)) < chance;
        }
    }

    /// <summary>True while the camp's fire burns.</summary>
    public bool CampFireBurning => Campaign.Camp is { } camp && camp.FireUntil > Campaign.Clock.Tick;

    /// <summary>Who has a berth under cover tonight: the sick in the sick shelter first, then everyone else, as far as the berths go.</summary>
    public IReadOnlySet<CharacterId> Sheltered()
    {
        var sheltered = new HashSet<CharacterId>();
        if (Campaign.Camp is not { } camp || Content.Shore is not { } shore)
        {
            return sheltered;
        }

        var people = Ashore();
        var sickBerths = (camp.Work(CampWorks.SickShelter) >= 1 ? shore.Rules.SickBerths : 0) + InfirmaryBerths;
        var berths = (int)camp.Work(CampWorks.Shelter) + HutBerths;
        foreach (var person in people.Where(SickOrHurt).Take(sickBerths))
        {
            sheltered.Add(person.Id);
        }

        foreach (var person in people.Where(p => !sheltered.Contains(p.Id)).OrderBy(p => !SickOrHurt(p)).ThenBy(p => p.Id.Value).Take(berths))
        {
            sheltered.Add(person.Id);
        }

        return sheltered;
    }

    // Once a minute ashore; the weather's work once an hour.
    private void StepCamp()
    {
        if (Campaign.Camp is not { } camp || Content.Shore is not { } shore || CampSite is not { } site
            || Campaign.Clock.Tick % SimulationClock.TicksPerHour != 0)
        {
            return;
        }

        var rules = shore.Rules;
        var hour = Campaign.Clock.TimeOfDay.TotalHours;
        var night = hour is >= 21 or < 6;
        var rain = Raining;
        var fire = CampFireBurning;
        var people = Ashore();
        var sheltered = Sheltered();
        var random = Campaign.Randomness.Crew;

        foreach (var person in people.Where(p => p.SurvivalState == SurvivalState.Alive))
        {
            var open = !sheltered.Contains(person.Id);

            // A wet night in the open: no rest, and a chill may take them; a fire takes the edge off.
            if (night && open && rain)
            {
                person.Needs.Fatigue = Needs.Clamp(person.Needs.Fatigue + rules.ExposedFatiguePerHour);
                person.Needs.Stress = Needs.Clamp(person.Needs.Stress + rules.ExposedStressPerHour);
            }

            var chill = rain && open ? rules.ChillPerWetHour * (fire ? 0.5 : 1) : 0;
            var swamp = night ? site.Fever * (fire ? 0.6 : 1) : 0;
            var roll = random.NextDouble();
            if (!person.Health.Conditions.Contains("condition.fever") && roll < chill + swamp && Content.Conditions.ContainsKey("condition.fever"))
            {
                person.Health.AddCondition("condition.fever");
                Campaign.Record(ChronicleEntryKind.FellIll, [
                    new(ChronicleParameters.Condition, "condition.fever"), new(ChronicleParameters.Reason, roll < chill ? "exposed" : "swamp"),
                ], person.Id);
            }

            // At first light, the night tallied: under cover or out in it.
            if (hour == 6)
            {
                camp.Night(person.Id, !open);
            }
        }

        // Rain on stores left out on the sand.
        if (rain && camp.Work(CampWorks.Cover) < 1)
        {
            var before = camp.Spoiled;
            foreach (var food in Content.Foods.Where(f => Campaign.SharedStore.Count(f.Id) > 0))
            {
                var expected = Campaign.SharedStore.Count(food.Id) * rules.SpoilPerRainHour;
                var lost = (int)Math.Floor(expected) + (Campaign.Randomness.Stores.Chance(expected - Math.Floor(expected)) ? 1 : 0);
                if (lost > 0 && Campaign.SharedStore.TryRemove(food.Id, lost))
                {
                    camp.Spoiled += lost;
                }
            }

            if (before == 0 && camp.Spoiled > 0)
            {
                Campaign.Record(ChronicleEntryKind.CampStoresWet, [new(ChronicleParameters.Level, camp.Spoiled.ToString(CultureInfo.InvariantCulture))]);
            }
        }

        // The camp grows foul without a latrine, and sickness comes and spreads as it does aboard.
        var life = Campaign.Life;
        life.Cleanliness = camp.Work(CampWorks.Latrine) >= 1
            ? Math.Min(85, life.Cleanliness + rules.CleanPerDay / 24)
            : Math.Max(0, life.Cleanliness - rules.FoulPerDay / 24);
        StepSickness(wet: rain && people.Any(p => !sheltered.Contains(p.Id)));
        StepSettlement(camp, rules);

        if (!camp.Reported && Campaign.Clock.Tick - camp.Landed >= rules.ReportHours * SimulationClock.TicksPerHour)
        {
            camp.Reported = true;
            var wanting = CampWanting();
            Campaign.Record(ChronicleEntryKind.CampFirstDays, [
                new(ChronicleParameters.Level, (rules.ReportHours / 24).ToString(CultureInfo.InvariantCulture)),
                new(ChronicleParameters.Need, people.Count.ToString(CultureInfo.InvariantCulture)),
                new(ChronicleParameters.Reason, string.Join(", ", wanting)),
            ]);
            Campaign.TryTransitionTo(CampaignPhase.SettlementSurvival, $"three days ashore at {site.Name}");
        }
    }

    /// <summary>What the camp still wants, in words: water, shelter, a fire, a latrine; empty when it has them.</summary>
    public IReadOnlyList<string> CampWanting()
    {
        var wanting = new List<string>();
        if (Campaign.Camp is not { } camp)
        {
            return wanting;
        }

        if (CampWater() is null) wanting.Add("water");
        else if (CampWater()!.Kind == "brackish") wanting.Add("sweet water");
        if (camp.Work(CampWorks.Shelter) < Ashore().Count) wanting.Add("shelter for all");
        if (camp.Work(CampWorks.Fire) < 1) wanting.Add("a fire");
        if (camp.Work(CampWorks.Latrine) < 1) wanting.Add("a latrine");
        return wanting;
    }

    /// <summary>
    /// What the captain can say of the landing (SETTLEMENT_EXPERIENCE §3), topic by topic: what is known, and
    /// what is only guessed. For the landing card and the camp panel.
    /// </summary>
    public IReadOnlyList<(string Topic, string Text, bool Known)> CampAssessment()
    {
        var lines = new List<(string, string, bool)>();
        if (Campaign.Camp is not { } camp || CampSite is not { } site)
        {
            return lines;
        }

        var people = Ashore();
        var sick = people.Where(SickOrHurt).ToList();
        lines.Add(("The company", $"{people.Count} ashore" + (sick.Count == 0 ? ", none sick or hurt." : $"; {sick.Count} sick or hurt: {string.Join(", ", sick.Select(s => s.Name))}."), true));
        lines.Add(("The place", string.Join(" ", site.Seen), true));

        var water = CampWater();
        var waterHint = site.Water.Where(w => !camp.Found.Contains(w.Id)).Select(w => w.Hint).OfType<string>().FirstOrDefault();
        lines.Add(("Water", water is null ? waterHint ?? "None found." : Capitalise(water.Name) + (water.Kind == "brackish" ? ", brackish: it may bring on the flux." : "."), water is not null));

        var food = CampFood();
        var foodHint = site.Food.Where(f => !camp.Found.Contains(f.Id)).Select(f => f.Hint).OfType<string>().FirstOrDefault();
        var (days, _) = SupplyForecast();
        lines.Add(("Food", $"Stores ashore for about {days:0} days." + (food is not null ? $" Found: {food.Name}{(food.Unfamiliar ? ", strange to them" : "")}." : foodHint is not null ? $" {foodHint}" : ""), food is not null));

        var berths = (int)camp.Work(CampWorks.Shelter);
        var cloth = Campaign.SharedStore.Count("item.sailcloth");
        lines.Add(("Shelter", berths >= people.Count ? "Shelter for everyone." : $"Berths under cover for {berths} of {people.Count}." +
            (cloth > 0 ? $" {cloth} cloths of sail ashore." : "") + (site.Brush ? " Brush and poles to be cut." : " Nothing much to build with."), true));

        var afloatKg = camp.Afloat.Sum(p => Content.Items.TryGetValue(p.Key, out var i) ? i.WeightKg * p.Value : 0);
        lines.Add(("The stores", camp.Afloat.Count == 0 ? "Everything there is, is ashore."
            : $"{afloatKg:0} kg still {(camp.ShipAtAnchor ? "aboard her" : "in the wreck")}, to be landed" + (camp.Boat ? " by the boat." : ": there is no boat, only what can be swum ashore."), true));

        lines.Add(("The weather", Raining ? "Raining." : "Dry for now." + (site.Fever > 0.006 ? " The air off the swamp is bad at night." : ""), true));

        var signs = site.Finds.Where(f => f.Kind == "people" && camp.Found.Contains(f.Id)).Select(f => f.Text).ToList();
        var signHint = site.Finds.Where(f => f.Kind == "people" && !camp.Found.Contains(f.Id)).Select(f => f.Hint).OfType<string>().FirstOrDefault();
        lines.Add(("People", signs.Count > 0 ? string.Join(" ", signs) : signHint ?? "No sign of anyone yet.", signs.Count > 0));

        var ruin = site.Finds.FirstOrDefault(f => f.Kind == "ruin");
        if (ruin is not null)
        {
            lines.Add((Capitalise(ruin.Name), camp.Found.Contains(ruin.Id) ? ruin.Text : ruin.Hint ?? "Not yet looked at.", camp.Found.Contains(ruin.Id)));
        }

        lines.Add(("The ship", camp.ShipAtAnchor ? $"At anchor off, her hull at {Campaign.Voyage?.Hull ?? 0:0}%."
            : Campaign.Wreck is { Remains.Count: > 0 } ? "The wreck lies close in, breaking up." : "Gone.", true));
        return lines;
    }

    private static string Capitalise(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Developer tool for tests and screenshots: the voyage is over at this landing, and they go ashore.</summary>
    public CommandResult DeveloperLand(string site = "coast.punta_ceniza")
    {
        var arrived = DeveloperArrive();
        return arrived.Succeeded ? GoAshore(site) : arrived;
    }

    /// <summary>Developer tool for tests and screenshots: she comes to anchor off the coast now, the company still aboard.</summary>
    public CommandResult DeveloperArrive()
    {
        if (Campaign.Voyage is not { } ship)
        {
            return CommandResult.Rejected("There is no voyage.");
        }

        if (ship.Outcome == VoyageOutcome.UnderWay)
        {
            ship.Outcome = VoyageOutcome.Arrived;
            Campaign.Record(ChronicleEntryKind.Landfall, [new(ChronicleParameters.To, ship.Destination)]);
            Campaign.TryTransitionTo(CampaignPhase.Landfall, $"sighted {ship.Destination}");
        }

        return CommandResult.Ok();
    }
}
