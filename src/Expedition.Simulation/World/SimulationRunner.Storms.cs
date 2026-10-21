using System.Globalization;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// Heavy weather met as it comes (VOYAGE_EXPERIENCE §4, §21–23). Its signs are seen some hours ahead and
/// the most seasoned hand says so. The crew can strike the topmasts, lash the cargo and batten down; each
/// changes what the blow does. Heavy seas spring leaks at named places; a leak plugged in a hurry can burst
/// in the next blow, one patched with plank is made good, and the ship keeps the history. Cargo not lashed
/// shifts and stoves casks; stores below open hatches are wetted. In fair weather again the crew sway up
/// the topmasts and cast off the lashings.
/// </summary>
public sealed partial class SimulationRunner
{
    private static readonly string[] LeakPlaces =
        ["the starboard bow", "the larboard bow", "the starboard waist", "the larboard waist", "under the counter", "at the garboard strake"];

    // The weather that follows this spell: foreseen some hours ahead, or rolled now if not.
    private string NextWeather(VoyageState ship, WeatherDefinition weather)
    {
        if (ship.NextWeatherId is { } foreseen)
        {
            ship.NextWeatherId = null;
            return foreseen;
        }

        return RollWeather(weather);
    }

    private string RollWeather(WeatherDefinition weather)
    {
        var roll = Campaign.Randomness.Weather.NextDouble() * weather.Next.Values.Sum();
        return weather.Next.OrderBy(n => n.Key, StringComparer.Ordinal).SkipWhile(n => (roll -= n.Value) > 0).Select(n => n.Key).FirstOrDefault()
               ?? weather.Next.Keys.Order(StringComparer.Ordinal).Last();
    }

    // Some hours before the spell ends, what comes next is decided, and if it is heavy weather its signs
    // are seen and the most seasoned hand aboard says what they mean.
    private void Forewarn(VoyageState ship, VoyageRules rules, WeatherDefinition weather)
    {
        var storms = rules.Storms;
        if (ship.NextWeatherId is not null || Campaign.Clock.Tick < ship.WeatherUntilTick - (long)(storms.WarningHours * SimulationClock.TicksPerHour))
        {
            return;
        }

        var next = RollWeather(weather);
        ship.NextWeatherId = next;
        var coming = rules.Weather(next);
        if (coming.SeaState >= 2 && coming.SeaState > weather.SeaState + 0.3)
        {
            var seaman = Campaign.Crew.Where(c => c.CanWork && c.Id != Campaign.CaptainId)
                .OrderByDescending(c => c.Skills.Get("skill.seamanship")).ThenBy(c => c.Id.Value).FirstOrDefault();
            Campaign.Record(ChronicleEntryKind.StormWarning, [new(ChronicleParameters.To, next)], seaman is null ? [] : [seaman.Id]);
        }
    }

    /// <summary>True while heavy weather is foreseen or blowing: the time to prepare.</summary>
    public bool StormComing =>
        Campaign.Voyage is { Outcome: VoyageOutcome.UnderWay } ship && Content.Voyage is { } rules
        && (rules.Weather(ship.WeatherId).SeaState >= rules.Storms.HeavySeaState
            || (ship.NextWeatherId is { } next && rules.Weather(next).SeaState >= rules.Storms.HeavySeaState));

    // Heavy weather on the ship, once a minute: water through open hatches, plugs bursting, cargo shifting,
    // stores wetted; in fair weather again the preparations are undone.
    private void MeetTheWeather(VoyageState ship, VoyageRules rules, WeatherDefinition weather)
    {
        var storms = rules.Storms;
        var random = Campaign.Randomness.Weather;
        var heavyNext = ship.NextWeatherId is { } next && rules.Weather(next).SeaState >= storms.HeavySeaState;
        if (ship.Prepared.Count > 0 && weather.SeaState < storms.FairSeaState && !heavyNext)
        {
            ship.ClearPreparations();
            Campaign.Record(ChronicleEntryKind.PreparationsUndone, []);
        }

        if (weather.SeaState < storms.HeavySeaState)
        {
            return;
        }

        foreach (var plugged in ship.Leaks.Where(l => l.Repair == LeakRepair.Plugged).ToList())
        {
            if (random.Chance(storms.PlugBurstChancePerHour * HoursPerTick))
            {
                ship.Mend(plugged, LeakRepair.Open, Campaign.Clock.Tick);
                Campaign.Record(ChronicleEntryKind.LeakReopened, [new(ChronicleParameters.To, plugged.Where)]);
            }
        }

        if (!ship.Prepared.Contains(Preparations.Cargo)
            && random.Chance(storms.ShiftChancePerHour * (weather.SeaState - storms.HeavySeaState + 0.5) * HoursPerTick))
        {
            var (item, lost) = LoseStores(storms.StoresLost, random);
            ship.Apply(new ShipEffects { Hull = -storms.ShiftHullDamage }, 1);
            Campaign.Record(ChronicleEntryKind.CargoShifted, [new(ChronicleParameters.To, item ?? ""), new(ChronicleParameters.Level, Number(lost))]);
        }

        if (!ship.Prepared.Contains(Preparations.Hatches) && random.Chance(storms.WetChancePerHour * HoursPerTick))
        {
            var (item, lost) = LoseStores(storms.StoresLost, random);
            if (lost > 0)
            {
                Campaign.Record(ChronicleEntryKind.StoresWetted, [new(ChronicleParameters.To, item ?? ""), new(ChronicleParameters.Level, Number(lost))]);
            }
        }
    }

    // Some of one store lost (casks stove, sacks wetted): the item chosen by chance among those aboard.
    private (string? Item, int Lost) LoseStores(double share, SimulationRandom random)
    {
        var held = Content.Items.Values.Where(i => i.Category is "food" or "water" && Campaign.SharedStore.Count(i.Id) > 0)
            .OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
        if (held.Count == 0)
        {
            return (null, 0);
        }

        var item = held[random.NextInt(held.Count)];
        var lost = Math.Max(1, (int)Math.Round(Campaign.SharedStore.Count(item.Id) * share));
        Campaign.SharedStore.TryRemove(item.Id, lost);
        return (item.Id, lost);
    }

    // A heavy sea strikes: a leak sprung at some place, as bad as the blow.
    private void SpringALeak(VoyageState ship, VoyageRules rules, WeatherDefinition weather)
    {
        var place = LeakPlaces[Campaign.Randomness.Weather.NextInt(LeakPlaces.Length)];
        var leak = new Leak
        {
            Id = ship.Leaks.Count + 1, Where = place, Severity = weather.HullStrikeDamage * rules.Storms.LeakPerStrikeDamage, Tick = Campaign.Clock.Tick,
        };
        ship.Spring(leak);
        Campaign.Record(ChronicleEntryKind.LeakSprung, [new(ChronicleParameters.To, place), new(ChronicleParameters.Level, leak.Severity.ToString("0.0", CultureInfo.InvariantCulture))]);
    }

    // What the leaks let in this hour, before the sea's multiplier.
    private static double LeakInflow(VoyageState ship) => ship.Leaks.Sum(l => l.Inflow);

    // The leak a repair goes to: a plug to the worst open one, a patch to the worst not yet made good.
    private static Leak? LeakFor(VoyageState ship, string repair) =>
        ship.Leaks.Where(l => repair == "plug" ? l.Repair == LeakRepair.Open : l.Repair != LeakRepair.Patched)
            .OrderByDescending(l => l.Severity).ThenBy(l => l.Id).FirstOrDefault();

    // Why a preparation or a leak repair cannot be done now, or null.
    private string? StormWorkProblem(TaskDefinition task)
    {
        if ((task.Prepares is not null || task.RepairsLeak == "plug") && Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay })
        {
            return $"{task.Name} is work at sea.";
        }

        if (task.Prepares is { } preparation && Campaign.Voyage!.Prepared.Contains(preparation))
        {
            return "That is done already.";
        }

        return task.RepairsLeak == "plug" && LeakFor(Campaign.Voyage!, "plug") is null ? "There is no open leak to plug." : null;
    }

    // When the work is done: the preparation in place, or the leak dealt with, and recorded.
    private void FinishStormWork(Character character, TaskDefinition task)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship)
        {
            return;
        }

        if (task.Prepares is { } preparation)
        {
            ship.Prepare(preparation);
            Campaign.Record(ChronicleEntryKind.Prepared, [new(ChronicleParameters.To, preparation)], character.Id);
        }

        if (task.RepairsLeak is { } repair && LeakFor(ship, repair) is { } leak)
        {
            ship.Mend(leak, repair == "plug" ? LeakRepair.Plugged : LeakRepair.Patched, Campaign.Clock.Tick);
            Campaign.Record(repair == "plug" ? ChronicleEntryKind.LeakPlugged : ChronicleEntryKind.LeakPatched, [new(ChronicleParameters.To, leak.Where)], character.Id);
        }
    }

    /// <summary>
    /// Has a piece of heavy-weather work done now, by the best hand free for it: a preparation
    /// ("aloft", "cargo", "hatches") or plugging a leak ("plug"). For the captain's orders from the Ship
    /// panel, without choosing who. The hand is one on deck, not at the helm, least tired.
    /// </summary>
    public CommandResult OrderStormWork(string work)
    {
        var task = Content.Tasks.Values.FirstOrDefault(t => work == "plug" ? t.RepairsLeak == "plug" : t.Prepares == work);
        if (task is null)
        {
            return CommandResult.Rejected("No such work.");
        }

        if (StormWorkProblem(task) is { } problem)
        {
            return CommandResult.Rejected(problem);
        }

        var hand = Campaign.Crew
            .Where(c => c.CanWork && c.Id != Campaign.CaptainId && OnDeck(c.Id) && c.Needs.Fatigue < Content.Rules.MaxFatigueToStartWork
                        && !(Campaign.ActiveTasks.TryGetValue(c.Id, out var doing) && (Content.Tasks[doing.DefinitionId].KeepsWatch || doing.Origin == TaskOrigin.Order))
                        && Campaign.QueueOf(c.Id).Count == 0)
            .OrderBy(c => c.Needs.Fatigue).ThenBy(c => c.Id.Value).FirstOrDefault();
        return hand is null
            ? CommandResult.Rejected("Every hand on deck is busy or worn out: call all hands, or take someone from other work.")
            : QueueTask(hand.Id, task.Id, TaskPriority.Urgent);
    }
}
