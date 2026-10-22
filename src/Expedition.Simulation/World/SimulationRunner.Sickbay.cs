using System.Globalization;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// Sickness and healing aboard (VOYAGE_EXPERIENCE §15–16, §42). Injuries heal over days, faster for the
/// resting and the tended; health comes back when nothing is wearing it down. Illness follows from the
/// state of the ship and the crew: fever comes to the worn out and those wet through below, flux to a foul
/// ship, the scurvy after weeks without anything fresh, and sickness passes between shipmates, more in a
/// dirty ship. Someone set to tend the sick speeds every recovery by their skill. Each onset says why.
/// </summary>
public sealed partial class SimulationRunner
{
    // Health comes back this fast with nothing wearing it down: about two points a day at work.
    private const double MendPerHour = 0.08;

    // Days without anything fresh before the scurvy may come.
    private const double ScurvyAfterDays = 28;

    // Fever's chance each hour for anyone, before what makes it likelier.
    private const double FeverPerHour = 0.00012;

    // In a foul ship (cleanliness below FoulBelow) the flux breeds of itself.
    private const double FluxPerHour = 0.0015;
    private const double FoulBelow = 30;

    /// <summary>Above this, an incapacitated person is back on their feet.</summary>
    public double OnTheirFeetAt => Content.Rules.IncapacitatedAtHealth + 15;

    /// <summary>Whether someone is sick or hurt: any condition, laid up, or badly down in health.</summary>
    public static bool SickOrHurt(Character character) =>
        character.SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated
        && (character.Health.Conditions.Count > 0 || character.SurvivalState == SurvivalState.Incapacitated || character.Health.Value < 75);

    /// <summary>The one tending the sick now, the best at it if several: or null if no one is.</summary>
    public Character? Tender() =>
        Campaign.Crew.Where(c => c.CanWork && Campaign.ActiveTasks.TryGetValue(c.Id, out var task) && Content.Tasks[task.DefinitionId].TendsSick
                                 && !(Campaign.Placements.TryGetValue(c.Id, out var placement) && placement.IsMoving))
            .OrderByDescending(CareLevel).ThenBy(c => c.Id.Value).FirstOrDefault();

    /// <summary>How well someone tends the sick: the better of their first aid and treatment.</summary>
    public static int CareLevel(Character character) =>
        Math.Max(character.Skills.Get("skill.first_aid"), character.Skills.Get("skill.treatment"));

    // How much faster the sick mend for being tended: 1 untended, up to about 2.75 by a surgeon.
    private double CareFactor(Character patient) =>
        Tender() is { } tender && tender.Id != patient.Id ? 1 + 0.35 * CareLevel(tender) : 1;

    private bool Resting(Character character) =>
        character.SurvivalState == SurvivalState.Incapacitated
        || (Campaign.ActiveTasks.TryGetValue(character.Id, out var task) && Content.Tasks[task.DefinitionId].IsRest);

    private IEnumerable<string> Illnesses(Character character) =>
        character.Health.Conditions.Where(id => Content.Conditions.TryGetValue(id, out var condition) && condition.Kind == "illness").Order(StringComparer.Ordinal);

    // Each minute: an illness wears the sick down until it passes; injuries heal; health comes back.
    private void StepIllness(Character character, List<TaskOutcome> outcomes)
    {
        if (character.Membership != Membership.Crew || character.SurvivalState is SurvivalState.Dead or SurvivalState.Missing)
        {
            return;
        }

        var illnesses = Illnesses(character).ToList();
        var care = illnesses.Count > 0 || character.Health.Conditions.Count > 0 || character.Health.Value < Health.Max ? CareFactor(character) : 1;
        var resting = Resting(character);
        foreach (var illness in illnesses)
        {
            var condition = Content.Conditions[illness];
            LoseHealth(character, condition.HealthPerHour * HoursPerTick, illness);
            // The sick tire: they go below sooner.
            character.Needs.Fatigue = Needs.Clamp(character.Needs.Fatigue + 1.5 * HoursPerTick);
        }

        if (illnesses.Count > 0)
        {
            CheckHealthThresholds(character, illnesses[0], outcomes);
            if (character.SurvivalState == SurvivalState.Dead)
            {
                return;
            }
        }

        var random = Campaign.Randomness.Crew;
        if (Campaign.Clock.Tick % SimulationClock.TicksPerHour == 0)
        {
            var freshLately = Campaign.Clock.Tick - Campaign.Life.LastFresh.GetValueOrDefault(character.Id, long.MinValue / 2) < SimulationClock.TicksPerDay;
            foreach (var illness in illnesses)
            {
                var condition = Content.Conditions[illness];
                var chance = condition.CuredByFresh
                    ? (freshLately ? 1.0 / 12 : 0)
                    : condition.RecoveryPerHour * (resting ? 1.5 : 1) * care;
                if (random.Chance(Math.Min(1, chance)))
                {
                    character.Health.RemoveCondition(illness);
                    Campaign.Record(ChronicleEntryKind.Recovered, [new(ChronicleParameters.Condition, illness)], character.Id);
                }
            }
        }

        // Injuries heal: in about their days resting and tended, slower at work and untended.
        foreach (var injury in character.Health.Conditions.Where(id => Content.Conditions.TryGetValue(id, out var c) && c.Kind != "illness").Order(StringComparer.Ordinal).ToList())
        {
            var days = Content.Conditions[injury].HealDays;
            var chance = HoursPerTick / (Math.Max(0.1, days) * 24) * (resting ? 1.4 : 0.7) * Math.Min(care, 2);
            if (random.Chance(chance))
            {
                character.Health.RemoveCondition(injury);
                Campaign.Record(ChronicleEntryKind.Healed, [new(ChronicleParameters.Condition, injury)], character.Id);
            }
        }

        // Health comes back when nothing is wearing it down.
        if (illnesses.Count == 0 && character.Needs.Hunger < Needs.Max && character.Needs.Thirst < Needs.Max && character.Health.Value < Health.Max)
        {
            var before = character.Health.Value;
            character.Health.Gain(MendPerHour * HoursPerTick * (resting ? 2 : 1) * care);
            Ledger(character.Id, TrackedNeed.Health, "mending", character.Health.Value - before);
        }

        if (character.SurvivalState == SurvivalState.Incapacitated && character.Health.Value >= OnTheirFeetAt && illnesses.Count == 0)
        {
            Campaign.ChangeSurvivalState(character.Id, SurvivalState.Alive, "recovered");
        }
    }

    // How fast someone works with what ails them: the worst of their conditions.
    private double ConditionWorkRate(Character character) =>
        character.Health.Conditions.Select(id => Content.Conditions.TryGetValue(id, out var c) ? c.WorkRate : 1).DefaultIfEmpty(1).Min();

    // How foul the ship is, as a multiplier on sickness passing: 0.7 kept clean, nearly 2 when foul.
    private double Filth => 0.5 + (100 - Campaign.Life.Cleanliness) / 50;

    // Whether those below are wet through: heavy weather with the hatches open.
    private bool WetBelow(VoyageState ship, VoyageRules rules) =>
        rules.Weather(ship.WeatherId).SeaState >= rules.Storms.HeavySeaState && !ship.Prepared.Contains(Preparations.Hatches);

    // Once an hour at sea: the ship grows fouler, then sickness comes and spreads.
    private void StepShipSickness(VoyageState ship, VoyageRules rules)
    {
        var wet = WetBelow(ship, rules);
        var sick = Campaign.Crew.Count(c => c.SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated && Illnesses(c).Any());

        // A ship grows foul by itself, faster with the sick aboard and the sea coming below.
        Campaign.Life.Cleanliness = Math.Clamp(Campaign.Life.Cleanliness - (3 + sick + (wet ? 3 : 0)) / 24.0, 0, 100);
        StepSickness(wet);
    }

    // Once an hour, aboard or ashore: sickness comes and spreads, and a spreading sickness is told.
    private void StepSickness(bool wet)
    {
        var life = Campaign.Life;
        var aboard = Campaign.Crew.Where(c => c.SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated).ToList();
        var sick = aboard.Where(c => Illnesses(c).Any()).ToList();

        var random = Campaign.Randomness.Crew;
        foreach (var person in aboard.Where(c => c.SurvivalState == SurvivalState.Alive).OrderBy(c => c.Id.Value))
        {
            foreach (var (illness, chance, reasons, from) in OnsetChances(person, sick, wet))
            {
                // One roll for every illness every hour, taken or not, so one never shifts another's dice.
                var roll = random.NextDouble();
                if (person.Health.Conditions.Contains(illness) || roll >= chance)
                {
                    continue;
                }

                person.Health.AddCondition(illness);
                Campaign.Record(ChronicleEntryKind.FellIll, [
                    new(ChronicleParameters.Condition, illness), new(ChronicleParameters.Reason, string.Join(",", reasons)),
                    new(ChronicleParameters.From, from?.Value.ToString(CultureInfo.InvariantCulture)),
                ], person.Id);
            }
        }

        // When it spreads, the captain hears how many are down and why.
        var count = aboard.Count(c => Illnesses(c).Any());
        if (count >= 2 && count > life.SickNoted)
        {
            var worst = aboard.SelectMany(Illnesses).GroupBy(i => i).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First().Key;
            var why = new List<string>();
            if (life.Cleanliness < 50) why.Add("foul");
            if (Tender() is null) why.Add("untended");
            if (wet) why.Add("wet");
            Campaign.Record(ChronicleEntryKind.SicknessSpreads, [
                new(ChronicleParameters.Level, count.ToString(CultureInfo.InvariantCulture)), new(ChronicleParameters.Condition, worst),
                new(ChronicleParameters.Reason, string.Join(",", why)),
                new(ChronicleParameters.Health, life.Cleanliness.ToString("0", CultureInfo.InvariantCulture)),
            ]);
        }

        life.SickNoted = count;
    }

    // For one person this hour: each illness that might come to them, how likely, and why.
    private IEnumerable<(string Illness, double Chance, List<string> Reasons, CharacterId? From)> OnsetChances(Character person, List<Character> sick, bool wet)
    {
        var life = Campaign.Life;
        foreach (var illness in new[] { "condition.fever", "condition.flux", "condition.scurvy" })
        {
            if (!Content.Conditions.TryGetValue(illness, out var condition))
            {
                continue;
            }

            var reasons = new List<string>();
            var chance = 0.0;
            if (illness == "condition.fever")
            {
                var factor = 1.0;
                if (person.Needs.Fatigue >= 65) { factor *= 2; reasons.Add("tired"); }
                if (wet) { factor *= 2; reasons.Add("wet"); }
                if (life.Cleanliness < 40) { factor *= 2; reasons.Add("foul"); }
                chance = FeverPerHour * factor;
            }
            else if (illness == "condition.flux" && life.Cleanliness < FoulBelow)
            {
                chance = FluxPerHour;
                reasons.Add("foul");
            }
            else if (illness == "condition.scurvy")
            {
                var since = (Campaign.Clock.Tick - life.LastFresh.GetValueOrDefault(person.Id, 0)) / (double)SimulationClock.TicksPerDay;
                if (since > ScurvyAfterDays)
                {
                    chance = 0.04 / 24 * (since - ScurvyAfterDays);
                    reasons.Add("nofresh");
                }
            }

            // Caught from a shipmate: more in a foul ship.
            CharacterId? from = null;
            if (condition.Contagion > 0)
            {
                var carriers = sick.Where(s => s.Id != person.Id && s.Health.Conditions.Contains(illness)).OrderBy(s => s.Id.Value).ToList();
                if (carriers.Count > 0)
                {
                    var caught = carriers.Count * condition.Contagion * Filth;
                    if (caught > chance)
                    {
                        from = carriers[0].Id;
                        reasons.Insert(0, "caught");
                    }

                    chance += caught;
                }
            }

            yield return (illness, chance, reasons, from);
        }
    }

    // Once an hour at sea: sickness and grievances.
    private void StepShipboardLife()
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship || Content.Voyage is not { } rules)
        {
            return;
        }

        if (Campaign.Clock.Tick % 10 == 0)
        {
            FollowStandingOrders(ship, rules);
        }

        if (Campaign.Clock.Tick % SimulationClock.TicksPerHour != 0)
        {
            return;
        }

        StepShipSickness(ship, rules);
        StepGrievances(ship, rules);
    }

    /// <summary>Developer tool for testing and screenshots: how clean the ship is kept, 0..100.</summary>
    public CommandResult DeveloperSetCleanliness(double cleanliness)
    {
        Campaign.Life.Restore(cleanliness, Campaign.Life.SickNoted);
        return CommandResult.Ok();
    }

    // A ship's work done: cleaner below.
    private void FinishCleaning(TaskDefinition task)
    {
        if (task.Cleans > 0)
        {
            Campaign.Life.Cleanliness = Math.Min(100, Campaign.Life.Cleanliness + task.Cleans);
        }
    }

    // Where work done anywhere is best done, if the place has spots for it (the Garza below decks): the
    // captain to his cabin, the sick to the sick berths, the watch below under the forecastle in foul
    // weather and on deck in the waist in fair; a plug to the leak's own place in the hold. Null to do it
    // where they stand.
    private (WorkSpotDefinition Spot, IReadOnlyList<GridPosition> Path)? ProperPlace(Character character, TaskDefinition task)
    {
        if (character.Membership != Membership.Crew || !Campaign.Placements.TryGetValue(character.Id, out var placement)
            || !Content.Locations.TryGetValue(placement.LocationId, out var location))
        {
            return null;
        }

        var captain = character.Id == Campaign.CaptainId;
        var taken = Campaign.ActiveTasks.Values.Where(t => t.CharacterId != character.Id).Select(t => t.SpotId).OfType<string>().ToHashSet();
        var spots = location.Spots.Where(s => s.Tasks.Contains(task.Id) && !taken.Contains(s.Id) && (s.Reserved is null || (s.Reserved == "captain" && captain))).ToList();
        if (spots.Count == 0)
        {
            return null;
        }

        var sick = Illnesses(character).Any() || character.SurvivalState == SurvivalState.Incapacitated;
        var foul = Raining || (Campaign.Voyage is { } ship && Content.Voyage is { } rules && rules.Weather(ship.WeatherId).SeaState >= 1.25);
        var leak = task.RepairsLeak == "plug" && Campaign.Voyage is { } leaking && LeakFor(leaking, "plug") is { } worst ? LeakSpot(worst.Where) : null;
        int Preference(WorkSpotDefinition spot) =>
            spot.Id == leak ? 0
            : task.IsRest && captain && spot.Reserved == "captain" ? 0
            : task.IsRest && spot.Sick ? (sick ? 0 : 9)
            : task.IsRest && sick ? 5
            : task.IsRest ? (spot.Indoors == foul ? 1 : 2)
            : 1;
        var start = placement.CellAt(Campaign.Clock.Tick);
        return spots
            .Where(s => Preference(s) < 9)
            .Select(s => (Spot: s, Path: Pathfinder.FindPath(location, start, s.Cell), Rank: Preference(s)))
            .Where(c => c.Path is not null)
            .OrderBy(c => c.Rank).ThenBy(c => c.Path!.Count).ThenBy(c => c.Spot.Id, StringComparer.Ordinal)
            .Select(c => ((WorkSpotDefinition, IReadOnlyList<GridPosition>)?)(c.Spot, c.Path!))
            .FirstOrDefault();
    }

    // The spot inside the hull where a leak of that name is met: "the starboard bow" is spot.leak_starboard_bow.
    private static string LeakSpot(string where) =>
        "spot.leak_" + where.Replace("the ", "").Replace("under ", "").Replace("at ", "").Replace(" strake", "").Replace(' ', '_');
}
