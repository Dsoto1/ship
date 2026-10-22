using System.Globalization;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// The ship lost (VOYAGE_EXPERIENCE §39): a transition, not a reset. In order: who is lost at the wreck
/// (more when she breaks up suddenly on a coast, at night, in heavy weather; the helpless and those asleep
/// below most of all); whether the boat gets away; what can be got into it, by what is within reach in the
/// time there is and what the boat will carry; where they make for (the nearest land, or the coast she
/// struck); the days in the boat on what water they saved; and what is left in the wreck to be got at
/// later. The survivors keep their needs, injuries, knowledge and relationships, and the campaign moves
/// to survival ashore.
/// </summary>
public sealed partial class SimulationRunner
{
    private static readonly string[] Categories = ["water", "food", "tool", "material", "drink"];

    // She is lost: works out the wreck, records it, and moves the campaign on.
    private void Wreck(VoyageState ship, VoyageRules rules, string cause)
    {
        var wreckRules = rules.Wreck;
        var weather = rules.Weather(ship.WeatherId);
        var random = Campaign.Randomness.Events;
        var heavy = weather.SeaState >= rules.Storms.HeavySeaState;
        var night = Campaign.Clock.TimeOfDay.TotalHours is < 6 or >= 19;
        var sudden = cause == "struck";
        var boat = !(sudden && random.Chance(wreckRules.BoatStoveChance));

        // Who is lost at the wreck.
        var aboard = Campaign.Crew.Where(c => c.SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated).OrderBy(c => c.Id.Value).ToList();
        var survivors = new List<Character>();
        var lost = new List<CharacterId>();
        foreach (var person in aboard)
        {
            var below = Campaign.ActiveTasks.TryGetValue(person.Id, out var task) && Content.Tasks[task.DefinitionId].IsRest;
            var risk = (sudden ? wreckRules.StruckLoss : wreckRules.FounderLoss)
                       + (sudden && night && below ? wreckRules.BelowLoss : 0)
                       + (person.SurvivalState == SurvivalState.Incapacitated ? wreckRules.HelplessLoss : 0)
                       + (heavy ? wreckRules.HeavyWeatherLoss : 0)
                       + (boat ? 0 : 0.15)
                       - 0.01 * person.Skills.Get("skill.seamanship");
            if (random.Chance(Math.Clamp(risk, 0, 0.95)))
            {
                var missing = random.Chance(1 / 3.0);
                Campaign.ChangeSurvivalState(person.Id, missing ? SurvivalState.Missing : SurvivalState.Dead,
                    sudden ? "lost when she broke up" : "lost when she went down");
                lost.Add(person.Id);
                continue;
            }

            survivors.Add(person);
            // No one comes through it unmarked.
            person.Health.AddCondition(random.Chance(sudden ? 0.4 : 0.15) ? "condition.near_drowned" : "condition.bruised");
            Campaign.AbandonTask(person.Id);
            Campaign.RemovePlacement(person.Id);
        }

        // What is got into the boat: what can be reached, as much as she will carry with them in her.
        var store = Campaign.SharedStore;
        var room = boat ? wreckRules.BoatCapacityKg - wreckRules.PersonKg * survivors.Count : 150;
        var saved = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var held = Content.Items.Values.Where(i => store.Count(i.Id) > 0)
            .OrderBy(i => Array.IndexOf(Categories, i.Category) is var k && k < 0 ? Categories.Length : k).ThenBy(i => i.Id, StringComparer.Ordinal).ToList();
        foreach (var item in held)
        {
            var reach = wreckRules.Reach.GetValueOrDefault(item.Category, 0.15) * (sudden && (night || heavy) ? wreckRules.SuddenReach : 1);
            // By chance for the part of one: a single marlinspike is got out nine times in ten.
            var share = store.Count(item.Id) * reach;
            var take = (int)Math.Floor(share) + (random.Chance(share - Math.Floor(share)) ? 1 : 0);
            take = Math.Min(take, item.WeightKg > 0 ? (int)Math.Floor(Math.Max(0, room) / item.WeightKg) : take);
            if (take > 0 && survivors.Count > 0)
            {
                saved[item.Id] = take;
                room -= take * item.WeightKg;
            }
        }

        var remains = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in held)
        {
            var left = store.Count(item.Id) - saved.GetValueOrDefault(item.Id);
            if (sudden && left > 0)
            {
                remains[item.Id] = left;
            }

            store.TryRemove(item.Id, store.Count(item.Id));
        }

        // Where they make for, and the days in the boat.
        var (ashore, ashoreName, leagues) = sudden ? StruckAt(ship) : NearestLand(ship);
        var days = leagues / wreckRules.BoatLeaguesPerDay;
        var water = saved.Where(pair => Content.Items[pair.Key].ThirstRelief > 0).Sum(pair => pair.Value * Content.Items[pair.Key].ThirstRelief / 12.0);
        for (var day = 0; day < Math.Ceiling(days) && survivors.Count > 0; day++)
        {
            var need = survivors.Count * wreckRules.WaterPerDay;
            if (water >= need)
            {
                water -= need;
                continue;
            }

            water = 0;
            foreach (var person in survivors.ToList().Where(_ => random.Chance(wreckRules.ThirstLossPerDay)))
            {
                Campaign.ChangeSurvivalState(person.Id, SurvivalState.Dead, "died of thirst in the boat");
                survivors.Remove(person);
                lost.Add(person.Id);
            }
        }

        // What the boat came ashore with: the saved stores, less the water drunk on the way.
        var loaded = new SortedDictionary<string, int>(saved, StringComparer.Ordinal);
        var drunk = saved.Where(pair => Content.Items[pair.Key].ThirstRelief > 0).Sum(pair => pair.Value * Content.Items[pair.Key].ThirstRelief / 12.0) - water;
        var drunkUnits = 0;
        foreach (var (item, count) in saved.ToList())
        {
            var units = count;
            if (Content.Items[item].ThirstRelief > 0 && drunk > 0)
            {
                var used = (int)Math.Min(units, Math.Ceiling(drunk));
                units -= used;
                drunk -= used;
                drunkUnits += used;
            }

            saved[item] = units;
            if (units > 0)
            {
                store.TryAdd(item, units, Content.Items);
            }
        }

        foreach (var person in survivors)
        {
            // Worn out by the boat, thirsty if the water ran short.
            person.Needs.Fatigue = Needs.Clamp(person.Needs.Fatigue + 8 * days + 20);
            person.Needs.Thirst = Needs.Clamp(person.Needs.Thirst + (water > 0 ? 10 : 40 + 10 * days));
            person.Needs.Stress = Needs.Clamp(person.Needs.Stress + 30);
        }

        Campaign.Clock.Restore(Campaign.Clock.Tick + (long)(days * SimulationClock.TicksPerDay));
        Campaign.Wreck = new WreckState
        {
            Cause = cause, Tick = Campaign.Clock.Tick, Ashore = ashore, AshoreName = ashoreName, Leagues = leagues, DaysInBoat = days,
            BoatLaunched = boat, Survivors = survivors.Select(s => s.Id).ToList(), Lost = lost, Saved = loaded.Where(p => p.Value > 0).ToDictionary(), Drunk = drunkUnits,
            Remains = remains,
        };
        Campaign.Record(ChronicleEntryKind.Wrecked, [
            new(ChronicleParameters.Cause, cause), new(ChronicleParameters.To, ashoreName),
            new(ChronicleParameters.Level, days.ToString("0.#", CultureInfo.InvariantCulture)), new(ChronicleParameters.Need, survivors.Count.ToString(CultureInfo.InvariantCulture)),
            new(ChronicleParameters.Reason, boat ? "boat" : "noBoat"),
        ]);

        if (survivors.Count == 0)
        {
            ship.Outcome = VoyageOutcome.AllHandsLost;
            Campaign.Record(ChronicleEntryKind.AllHandsLost, []);
            Campaign.TryTransitionTo(CampaignPhase.CampaignFailed, $"no one came ashore from {ship.ShipName}");
            return;
        }

        Campaign.TryTransitionTo(CampaignPhase.ShipwreckSurvival, sudden ? $"{ship.ShipName} broke up on the coast" : $"{ship.ShipName} filled and foundered");
    }

    /// <summary>Developer tool for testing and screenshots: she strikes unseen on the coast and breaks up now.</summary>
    public CommandResult DeveloperStrike()
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship)
        {
            return CommandResult.Rejected("There is no voyage under way.");
        }

        ship.Outcome = VoyageOutcome.Foundered;
        Wreck(ship, Content.Voyage!, "struck");
        return CommandResult.Ok();
    }

    /// <summary>Developer tool for testing and screenshots: she fills and founders now.</summary>
    public CommandResult DeveloperFounder()
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship)
        {
            return CommandResult.Rejected("There is no voyage under way.");
        }

        ship.Apply(new ShipEffects { Bilge = 100 }, 1);
        ship.Outcome = VoyageOutcome.Foundered;
        Campaign.Record(ChronicleEntryKind.Foundered, [new(ChronicleParameters.To, ship.ShipName)]);
        Wreck(ship, Content.Voyage!, "foundered");
        return CommandResult.Ok();
    }

    // Broken up on the coast: they are ashore where she struck, the wreck close by.
    private (string Id, string Name, double Leagues) StruckAt(VoyageState ship)
    {
        if (ship.Navigation is { } navigation && Coast is { } coast)
        {
            var north = navigation.True.Y - navigation.Destination.Y;
            var mark = coast.InView(north) ?? coast.Landmarks.OrderBy(l => Math.Abs(l.North - north)).First();
            return (mark.Id, coast.InView(north) is null ? $"the coast near {mark.Name}" : mark.Name, 0);
        }

        return ("coast", "the coast", 0);
    }

    // Foundered at sea: the boat makes for the nearest land there truly is. An island she lay off is the
    // nearest of all.
    private (string Id, string Name, double Leagues) NearestLand(VoyageState ship)
    {
        if (ship.Navigation is not { } navigation)
        {
            return ("coast", ship.Destination, Math.Max(0, ship.LeaguesRemaining));
        }

        var choices = new List<(string Id, string Name, double Leagues)>();
        if (Coast is { } coast)
        {
            var north = navigation.True.Y - navigation.Destination.Y;
            var mark = coast.Landmarks.OrderBy(l => Math.Abs(l.North - north)).First();
            choices.Add((mark.Id, coast.InView(north) is null ? $"the Ember Coast, near {mark.Name}" : mark.Name, Math.Max(0, navigation.Offshore)));
        }
        else
        {
            choices.Add(("coast", ship.Destination, Math.Max(0, navigation.TrueRemaining)));
        }

        choices.Add(("castrena", "the coast of Castrena", navigation.True.DistanceTo(new ChartPoint(0, 0))));
        foreach (var island in Content.Voyage?.Route?.Islands ?? [])
        {
            var (exists, at) = IslandTruth(island);
            if (exists)
            {
                choices.Add((island.Id, island.Name, navigation.True.DistanceTo(at)));
            }
        }

        return choices.OrderBy(c => c.Leagues).First();
    }
}
