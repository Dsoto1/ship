using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Shore;

namespace Expedition.Simulation.World;

/// <summary>Short, as served at sea and ashore; emergency is short with food only every other day (step C).</summary>
public enum Rations
{
    /// <summary>Eat and drink when hungry and thirsty.</summary>
    Full,

    /// <summary>Food and water once a day each, about half.</summary>
    Short,

    /// <summary>Water once a day, food every other day.</summary>
    Emergency,
}

/// <summary>A cause of how the community stands: good or bad, in words (ECONOMY_LOOP §33).</summary>
/// <param name="Good">True for what lifts it, false for what weighs on it.</param>
/// <param name="Text">"Two dead in the last fortnight".</param>
public sealed record CommunityCause(bool Good, string Text);

/// <summary>How the community stands, in a word, and why (ECONOMY_LOOP §33).</summary>
/// <param name="Word">Good, steady, strained, low or breaking.</param>
/// <param name="Strain">The company's strain, 0..100 (their stress, on average).</param>
/// <param name="Causes">What weighs on it first, then what lifts it.</param>
public sealed record CommunityCondition(string Word, double Strain, IReadOnlyList<CommunityCause> Causes);

/// <summary>A self-sufficiency milestone (ECONOMY_LOOP §44): what it is, and when it was reached.</summary>
/// <param name="Id">Its name.</param>
/// <param name="Title">"Reliable water".</param>
/// <param name="Wants">What reaching it takes.</param>
/// <param name="Reached">When, or null.</param>
public sealed record Milestone(string Id, string Title, string Wants, long? Reached);

/// <summary>
/// Step C (docs/COMMUNITY.md): the community's condition in words with its causes, the captain's policies
/// ashore (rations, working hours, the reserves kept), self-sufficiency milestones, and the stores'
/// projections given as ranges.
/// </summary>
public sealed partial class SimulationRunner
{
    // The working day's end moved this many hours, and the fatigue hands rest at moved this much.
    private const double HoursShift = 3;
    private const double RestShift = 15;

    // Strain an hour: working long hours; resting in short ones; emergency rations on top of short.
    private static readonly NeedRates LongHoursStress = new() { Stress = 0.3 };
    private static readonly NeedRates ShortHoursEase = new() { Stress = -0.2 };

    /// <summary>The ledger's name for long hours' strain, and short hours' ease.</summary>
    public const string LongHoursSource = "long hours", ShortHoursSource = "short hours";

    // Relief to everyone ashore when the camp reaches a milestone.
    private const double MilestoneRelief = 6;

    /// <summary>The reserves the captain may set: days of food or water kept in hand.</summary>
    public static readonly IReadOnlyList<int> ReserveChoices = [4, 10, 20];

    /// <summary>The rations now.</summary>
    public Rations Rations => !Campaign.Rationing ? Rations.Full : Campaign.EmergencyRations ? Rations.Emergency : Rations.Short;

    /// <summary>Sets the rations: full, short, or emergency (food only every other day).</summary>
    public CommandResult SetRations(Rations rations)
    {
        if (rations == Rations)
        {
            return CommandResult.Ok();
        }

        var wasShort = Campaign.Rationing;
        Campaign.EmergencyRations = rations == Rations.Emergency;
        if (wasShort != (rations != Rations.Full))
        {
            SetRationing(rations != Rations.Full);
        }
        else
        {
            Campaign.Record(ChronicleEntryKind.RationingChanged, [new(ChronicleParameters.To, rations == Rations.Emergency ? "emergency" : "short")]);
        }

        return CommandResult.Ok();
    }

    /// <summary>Sets the camp's working day.</summary>
    public CommandResult SetWorkHours(WorkHours hours)
    {
        if (Campaign.Camp is not { } camp)
        {
            return CommandResult.Rejected("There is no camp ashore.");
        }

        camp.Hours = hours;
        return CommandResult.Ok();
    }

    /// <summary>Sets the days of food and water the camp keeps in hand (one of <see cref="ReserveChoices"/>).</summary>
    public CommandResult SetReserves(int foodDays, int waterDays)
    {
        if (Campaign.Camp is not { } camp)
        {
            return CommandResult.Rejected("There is no camp ashore.");
        }

        if (!ReserveChoices.Contains(foodDays) || !ReserveChoices.Contains(waterDays))
        {
            return CommandResult.Rejected($"Keep {string.Join(", ", ReserveChoices)} days.");
        }

        (camp.ReserveFoodDays, camp.ReserveWaterDays) = (foodDays, waterDays);
        AssignCampDuties();
        return CommandResult.Ok();
    }

    // The hours the day's work may be begun until, moved by the camp's working day.
    private double DayEndShift => Campaign.Camp?.Hours switch { WorkHours.Short => -HoursShift, WorkHours.Long => HoursShift, _ => 0 };

    // The fatigue a hand rests at ashore, moved by the working day.
    private double RestHoursShift => Campaign.Camp?.Hours switch { WorkHours.Short => -RestShift, WorkHours.Long => RestShift, _ => 0 };

    // An hour's strain or ease from the working day, for someone ashore.
    private void WorkingDayStrain(Character character, bool working)
    {
        if (Campaign.Camp?.Hours is WorkHours.Long && working)
        {
            character.Needs.Apply(LongHoursStress, HoursPerTick);
            RecordRates(character.Id, LongHoursSource, LongHoursStress);
        }
        else if (Campaign.Camp?.Hours is WorkHours.Short && !working)
        {
            character.Needs.Apply(ShortHoursEase, HoursPerTick);
            RecordRates(character.Id, ShortHoursSource, ShortHoursEase);
        }
    }

    // Whether food is served today on emergency rations: every other day.
    private bool FoodDay => !Campaign.EmergencyRations || Campaign.Clock.Day % 2 == 0;

    // The camp's priorities as worked now: water or food first while the store is below half what is to be kept.
    private IReadOnlyList<string> WorkedPriorities(CampState camp)
    {
        var people = Math.Max(1, Ashore().Count);
        var store = Campaign.SharedStore;
        var waterLow = Content.Drinks.Sum(d => store.Count(d.Id) * d.ThirstRelief) < camp.ReserveWaterDays / 2.0 * people * DailyThirst && CampWater() is not null;
        var foodLow = Content.Foods.Sum(f => store.Count(f.Id) * f.HungerRelief) < camp.ReserveFoodDays / 2.0 * people * DailyHunger && CampFood() is not null;
        var first = new List<string>();
        if (waterLow) first.Add(CampPriorities.Water);
        if (foodLow) first.Add(CampPriorities.Food);
        return first.Count == 0 ? camp.Priorities : [.. first, .. camp.Priorities.Where(p => !first.Contains(p))];
    }

    private string _workedPriorities = "";

    // ================================================================ the hour

    // Once an hour ashore: the reserves' order of work, and the milestones.
    private void StepCommunity(CampState camp)
    {
        var worked = string.Join(",", WorkedPriorities(camp));
        if (worked != _workedPriorities)
        {
            _workedPriorities = worked;
            AssignCampDuties();
        }

        foreach (var milestone in Milestones().Where(m => m.Reached is null && MilestoneMet(camp, m.Id)))
        {
            camp.Reached(milestone.Id, Campaign.Clock.Tick);
            Campaign.Record(ChronicleEntryKind.MilestoneReached, [new(ChronicleParameters.To, milestone.Id)]);
            foreach (var person in Ashore())
            {
                person.Needs.Stress = Needs.Clamp(person.Needs.Stress - MilestoneRelief);
            }
        }
    }

    // ================================================================ milestones

    private static readonly (string Id, string Title, string Wants)[] MilestoneList =
    [
        ("water", "Reliable water", "A well, or sweet water found within ten minutes' walk"),
        ("storehouse", "A storehouse", "The first storehouse standing: the stores off the ground and dry"),
        ("homes", "Homes for all", "Dry berths in huts for everyone ashore"),
        ("harvest", "A harvest", "The first crop brought in from the garden"),
        ("tool", "A tool mended", "A broken tool mended at the camp, not lost"),
        ("apprentice", "An apprentice trained", "Someone taught a level of a skill ashore, by a mentor or a workplace's lead"),
        ("trade", "Stable trade", "A people of the coast who trust the camp and have traded with it"),
        ("season", "The first season", "The first season survived (judged on its day)"),
    ];

    /// <summary>The self-sufficiency milestones, reached or not (ECONOMY_LOOP §44).</summary>
    public IReadOnlyList<Milestone> Milestones() =>
        MilestoneList.Select(m => new Milestone(m.Id, m.Title, m.Wants, Campaign.Camp?.Milestones.TryGetValue(m.Id, out var at) == true ? at : null)).ToList();

    // Whether the camp meets a milestone now.
    private bool MilestoneMet(CampState camp, string id)
    {
        bool Since(ChronicleEntryKind kind, Func<ChronicleEntry, bool>? also = null) =>
            Campaign.Chronicle.Entries.Any(e => e.Kind == kind && e.Tick >= camp.Landed && (also is null || also(e)));
        return id switch
        {
            "water" => WellSource() is { Kind: not "brackish" } || CampWater() is { Kind: not "brackish", Walk: <= 10 },
            "storehouse" => StorehouseStands,
            "homes" => HutBerths > 0 && HutBerths >= Ashore().Count,
            "harvest" => Since(ChronicleEntryKind.Harvested),
            "tool" => Since(ChronicleEntryKind.ToolMended),
            "apprentice" => Since(ChronicleEntryKind.SkillImproved, e => e.Get(ChronicleParameters.From) is { Length: > 0 }),
            "trade" => Campaign.World?.Communities.Values.Any(c => c.Met && c.Trust >= 20 && c.Grievance < 25 && c.Received.Values.Sum() >= 3) == true,
            "season" => camp.Survived == true,
            _ => false,
        };
    }

    // ================================================================ the community's condition

    /// <summary>
    /// How the community stands, in a word, and why (ECONOMY_LOOP §33): "Strained: two dead in the last
    /// fortnight; on short rations; + dry huts for all".
    /// </summary>
    public CommunityCondition Community()
    {
        var living = Campaign.Crew.Where(c => c.SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated).ToList();
        var strain = living.Count == 0 ? 0 : living.Average(c => c.Needs.Stress);
        var word = strain switch { < 25 => "Good", < 45 => "Steady", < 65 => "Strained", < 80 => "Low", _ => "Breaking" };
        var bad = new List<string>();
        var good = new List<string>();
        var now = Campaign.Clock.Tick;
        var fortnight = 14 * SimulationClock.TicksPerDay;
        var camp = Campaign.Camp;

        var dead = Campaign.Chronicle.Entries.Count(e => e.Kind == ChronicleEntryKind.SurvivalStateChanged && e.Get(ChronicleParameters.To) == nameof(SurvivalState.Dead) && now - e.Tick <= fortnight);
        if (dead > 0)
        {
            bad.Add(dead == 1 ? "a death in the last fortnight" : $"{dead} dead in the last fortnight");
        }

        var sick = living.Count(SickOrHurt);
        if (sick > 0)
        {
            bad.Add(sick == 1 ? "one sick or hurt" : $"{sick} sick or hurt");
        }

        if (living.Count > 0 && living.Average(c => c.Needs.Hunger) >= 50) bad.Add("hungry");
        if (living.Count > 0 && living.Average(c => c.Needs.Thirst) >= 50) bad.Add("thirsty");
        if (living.Count > 0 && living.Average(c => c.Needs.Fatigue) >= 60) bad.Add("worn out");
        var (foodLow, _, waterLow, _) = SupplyRange();
        if (living.Count > 0 && foodLow < 7) bad.Add($"food running low (~{Math.Floor(foodLow):0}+ days)");
        if (living.Count > 0 && waterLow < 4) bad.Add($"water running low (~{Math.Floor(waterLow):0}+ days)");
        switch (Rations)
        {
            case Rations.Short: bad.Add("on short rations"); break;
            case Rations.Emergency: bad.Add("on emergency rations"); break;
        }

        if (camp is not null)
        {
            switch (camp.Hours)
            {
                case WorkHours.Long: bad.Add("long hours"); break;
                case WorkHours.Short: good.Add("short hours: time to rest"); break;
            }

            var people = Ashore().Count;
            var berths = (int)camp.Work(CampWorks.Shelter) + HutBerths;
            if (people > 0 && berths < people) bad.Add($"{people - berths} with no cover at night");
            else if (people > 0 && HutBerths >= people) good.Add("dry huts for all");
            if (CookhouseStands) good.Add("hot food from the cookhouse");
            if (HouseholdsWithoutHomes > 0) bad.Add(HouseholdsWithoutHomes == 1 ? "a household with no house of its own" : $"{HouseholdsWithoutHomes} households with no house of their own");
            else if (Campaign.Households.Count > 0) good.Add("every household in a house of its own");
            foreach (var reached in camp.Milestones.Where(m => now - m.Value <= 7 * SimulationClock.TicksPerDay))
            {
                good.Add($"{MilestoneList.First(m => m.Id == reached.Key).Title.ToLowerInvariant()}, lately");
            }

            if (Campaign.Chronicle.Entries.Any(e => e.Kind == ChronicleEntryKind.BuildingFinished && now - e.Tick <= 7 * SimulationClock.TicksPerDay))
            {
                good.Add("a building finished this week");
            }
        }

        var grieved = Campaign.Life.Grievances.Count(g => g.Value.Stage >= GrievanceStage.Complaint && living.Any(c => c.Id == g.Key));
        if (grieved > 0)
        {
            bad.Add(grieved == 1 ? "one holds a grievance against the voyage" : $"{grieved} hold grievances against the voyage");
        }

        // Fresh food: most have had none in ten days, or most have had some this week.
        var fresh = living.Count(c => Campaign.Life.LastFresh.TryGetValue(c.Id, out var at) && now - at <= 7 * SimulationClock.TicksPerDay);
        var stale = living.Count(c => !Campaign.Life.LastFresh.TryGetValue(c.Id, out var at) || now - at > 10 * SimulationClock.TicksPerDay);
        if (living.Count > 0 && camp is not null && stale * 2 > living.Count) bad.Add("no fresh food in a long while");
        else if (living.Count > 0 && fresh * 2 > living.Count) good.Add("fresh food");

        // How the company regards the captain.
        if (Campaign.CaptainId is { } captain)
        {
            var regard = living.Where(c => c.Id != captain && Campaign.Relationships.ContainsKey((c.Id, captain)))
                .Select(c => Campaign.Relationships[(c.Id, captain)]).Select(r => (r.Trust + r.Respect) / 2.0).DefaultIfEmpty(0).Average();
            if (regard >= 30) good.Add("the captain is trusted");
            else if (regard <= -10) bad.Add("the captain is not trusted");
        }

        // The peoples of the coast.
        if (Campaign.World is { } world && Content.Shore?.Region is { } region)
        {
            foreach (var (id, state) in world.Communities.Where(c => c.Value.Met))
            {
                var name = region.Communities.FirstOrDefault(c => c.Id == id)?.Name ?? id;
                if (state.Trust <= -20) bad.Add($"the {name} are hostile");
                else if (state.Trust >= 40) good.Add($"friends among the {name}");
            }
        }

        return new CommunityCondition(word, strain, [.. bad.Select(b => new CommunityCause(false, b)), .. good.Select(g => new CommunityCause(true, g))]);
    }

    // ================================================================ projections in ranges

    /// <summary>
    /// How many days the food and the water will last, as a range (ECONOMY_LOOP §18): from hard work at the
    /// worst to the everyday rate at the best, as the rations now serve them, before anything gathered.
    /// </summary>
    public (double FoodLow, double FoodHigh, double WaterLow, double WaterHigh) SupplyRange()
    {
        var (food, water) = SupplyForecast();
        var (foodRations, waterRations) = Rations switch { Rations.Short => (1.8, 1.8), Rations.Emergency => (3.0, 1.8), _ => (1.0, 1.0) };
        const double hardWork = 1.15 / 1.6;
        return (food * hardWork * foodRations, food * foodRations, water * hardWork * waterRations, water * waterRations);
    }

    /// <summary>A projection in words: "~18–22 d".</summary>
    public static string DaysRange(double low, double high) =>
        high - low < 1 ? $"~{Math.Round(high):0} d" : $"~{Math.Floor(low):0}–{Math.Ceiling(high):0} d";
}
