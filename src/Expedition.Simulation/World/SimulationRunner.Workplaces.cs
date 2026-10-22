using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.Shore;
using Expedition.Simulation.Tasks;

namespace Expedition.Simulation.World;

/// <summary>
/// Step W (docs/WORKPLACES.md): each building and each building in hand is a workplace, and may have a
/// lead. The lead's skill sets the quality of the work, its pace and its safety, and the hands learn from
/// them (ECONOMY_LOOP §31). Each project has a priority (§29) and says why it has stopped (§28). Each
/// building keeps its history: when it was built, under whom, by whom, from what, and what it has been
/// through since (§45).
/// </summary>
public sealed partial class SimulationRunner
{
    // A lead's skill in the work, each level: this much faster, this much better made, this much safer.
    private const double LeadPacePerLevel = 0.04;
    private const double LeadQualityPerLevel = 0.03;
    private const double LeadSafetyPerLevel = 0.1;

    // Hours of storm that count as one storm in a building's history.
    private const long OneStorm = 2 * SimulationClock.TicksPerDay;

    /// <summary>Who leads the workplace on <paramref name="plot"/>, if anyone.</summary>
    public CharacterId? LeadOf(string plot) =>
        Campaign.Camp is not { } camp ? null
        : camp.Projects.FirstOrDefault(p => p.Plot == plot)?.Lead ?? camp.Buildings.FirstOrDefault(b => b.Plot == plot)?.Lead;

    /// <summary>
    /// Puts <paramref name="lead"/> in charge of the workplace on <paramref name="plot"/>, or no one. A
    /// person leads one workplace at a time: leading this one, they give up any other.
    /// </summary>
    public CommandResult SetLead(string plot, CharacterId? lead)
    {
        if (Campaign.Camp is not { } camp)
        {
            return CommandResult.Rejected("There is no camp.");
        }

        var project = camp.Projects.FirstOrDefault(p => p.Plot == plot);
        var building = camp.Buildings.FirstOrDefault(b => b.Plot == plot);
        if (project is null && building is null)
        {
            return CommandResult.Rejected("There is no such workplace.");
        }

        if (lead is { } id && (!Campaign.Characters.TryGetValue(id, out var person) || person.Membership != Membership.Crew
                               || person.SurvivalState is SurvivalState.Dead or SurvivalState.Missing))
        {
            return CommandResult.Rejected("Only one of the company can lead the work.");
        }

        if (lead is not null)
        {
            foreach (var other in camp.Projects.Where(p => p.Lead == lead))
            {
                other.Lead = null;
            }

            foreach (var other in camp.Buildings.Where(b => b.Lead == lead))
            {
                other.Lead = null;
            }
        }

        if (project is not null)
        {
            project.Lead = lead;
        }
        else
        {
            building!.Lead = lead;
        }

        return CommandResult.Ok();
    }

    /// <summary>Sets how a project stands against the others.</summary>
    public CommandResult SetProjectPriority(int projectId, ProjectPriority priority)
    {
        if (Campaign.Camp?.Projects.FirstOrDefault(p => p.Id == projectId) is not { } project)
        {
            return CommandResult.Rejected("There is no such work.");
        }

        project.Priority = priority;
        return CommandResult.Ok();
    }

    // The lead of the workplace a spot belongs to, if they are fit, ashore at the camp and not someone else's task.
    private Character? LeadAt(string? spotId)
    {
        if (PlotOf(spotId) is not { } plot || LeadOf(plot) is not { } id || !Campaign.Characters.TryGetValue(id, out var lead)
            || !lead.CanWork || !Campaign.Placements.TryGetValue(id, out var placement) || placement.LocationId != CampLocation)
        {
            return null;
        }

        return lead;
    }

    // The trade a building's lead teaches the hands beside them.
    private const string LeadTrade = "skill.carpentry";

    // The skill a lead brings to a work: the task's own, or for building without one, the carpenter's.
    private static int LeadSkill(Character lead, TaskDefinition task) =>
        task.Skill is { } skill ? lead.Skills.Get(skill) : lead.Skills.Get(LeadTrade);

    // How much faster the work goes under its lead: by their skill, and a little for having anyone to say
    // what comes next. A lead working at their own place organises themself as well.
    private double LeadPace(TaskInstance task, TaskDefinition definition) =>
        LeadAt(task.SpotId) is { } lead ? 1 + LeadPacePerLevel * LeadSkill(lead, definition) : 1;

    // How much likelier an accident is: a skilled lead keeps the work careful.
    private double LeadSafety(TaskInstance task, TaskDefinition definition) =>
        LeadAt(task.SpotId) is { } lead ? Math.Max(0.5, 1 - LeadSafetyPerLevel * LeadSkill(lead, definition)) : 1;

    // The quality a hand's work comes out at under a lead: the lead's eye on it, where it is better than their own.
    private double LedQuality(Character character, TaskDefinition task, string? spotId, double own) =>
        LeadAt(spotId) is { } lead && lead.Id != character.Id
            ? Math.Clamp(Math.Max(own, (own + WorkQuality(lead, task)) / 2) + LeadQualityPerLevel * LeadSkill(lead, task) / 2, 0.1, 1)
            : own;

    // The lead of the place someone is working, as their teacher in the work's skill, where they know it better.
    private Character? WorkplaceTeacher(Character learner, string skill) =>
        Campaign.ActiveTasks.TryGetValue(learner.Id, out var task) && LeadAt(task.SpotId) is { } lead && lead.Id != learner.Id
        && lead.Skills.Get(skill) > learner.Skills.Get(skill)
            ? lead
            : null;

    // Where a lead works first: at the workplace they lead.
    private string? LedSpot(Character character, IEnumerable<WorkSpotDefinition> free) =>
        Campaign.Camp is { } camp && (camp.Projects.FirstOrDefault(p => p.Lead == character.Id)?.Plot ?? camp.Buildings.FirstOrDefault(b => b.Lead == character.Id)?.Plot) is { } plot
            ? free.FirstOrDefault(s => PlotOf(s.Id) == plot)?.Id
            : null;

    // ================================================================ priorities

    // Whether a project could be worked now, people aside: not paused, work left, the weather, the materials.
    private bool Workable(CampProject project)
    {
        // Work left, counting what is under way as left: giving up a task must not change which project is in turn.
        if (project.Priority == ProjectPriority.Paused || PhaseOf(project) is not { } phase || PhaseTimes(project, phase) - project.Done <= 0
            || !Content.Tasks.TryGetValue(phase.Task, out var task))
        {
            return false;
        }

        return WeatherAllows(task) && StormWorkProblem(task) is null && !(Raining && task.DutyWhen?.Camp == "build_dry")
               && (project.Laid > 0 || task.Consumes.All(c => Campaign.SharedStore.Count(c.Key) >= c.Value));
    }

    // The highest priority any project that could be worked now has: the hands take those first.
    private ProjectPriority TopPriority() =>
        Campaign.Camp?.Projects.Where(Workable).Select(p => p.Priority).DefaultIfEmpty(ProjectPriority.Normal).Min() ?? ProjectPriority.Normal;

    // The priority of the project a spot belongs to: its spots are taken first when it matters most.
    private ProjectPriority SpotPriority(WorkSpotDefinition spot) =>
        Campaign.Camp?.Projects.FirstOrDefault(p => p.Plot == PlotOf(spot.Id))?.Priority ?? ProjectPriority.Normal;

    // Whether a project is among those to be worked now: not paused, and none above it could be worked instead.
    private bool InTurn(CampProject project) => project.Priority != ProjectPriority.Paused && project.Priority <= TopPriority();

    // A hand free to work: the critical project's work before anything else the camp wants of them.
    private bool StartCritical(Character character)
    {
        if (Campaign.Camp is not { } camp || !camp.Projects.Any(p => p.Priority == ProjectPriority.Critical && Workable(p)))
        {
            return false;
        }

        foreach (var project in camp.Projects.Where(p => p.Priority == ProjectPriority.Critical && Workable(p)))
        {
            if (Content.Tasks.TryGetValue(PhaseOf(project)!.Task, out var task) && Campaign.DutiesOf(character.Id).Contains(task.Id)
                && Check(character, task, materialsAlreadyUsed: false).Succeeded)
            {
                Start(character, new TaskInstance(Campaign.Ids.NextTaskId(), character.Id, task.Id, TaskPriority.Normal) { Origin = TaskOrigin.Duty },
                    CampSpots().FirstOrDefault(s => PlotOf(s.Id) == project.Plot && SpotServes(task, s))?.Id);
                return true;
            }
        }

        return false;
    }

    // ================================================================ why it has stopped

    /// <summary>
    /// Why a project is not being worked (ECONOMY_LOOP §28): "Waiting for thatch: 12 more wanted", "No
    /// axe free". Null while someone is at it.
    /// </summary>
    public string? Stalled(CampProject project)
    {
        if (Campaign.Camp is null || PhaseOf(project) is not { } phase || !Content.Tasks.TryGetValue(phase.Task, out var task))
        {
            return null;
        }

        if (project.Priority == ProjectPriority.Paused)
        {
            return "Paused.";
        }

        if (Campaign.ActiveTasks.Values.Any(t => PlotOf(t.SpotId) == project.Plot))
        {
            return null;
        }

        if (StormWorkProblem(task) is { } storm)
        {
            return storm;
        }

        if (WeatherProblem(task) is { } weather)
        {
            return weather;
        }

        if (Raining && task.DutyWhen?.Camp == "build_dry")
        {
            return $"Waiting for a dry spell: {task.Name.ToLowerInvariant()} is not done in the rain.";
        }

        // Out of the hours the work is begun in: night, or too late in the day to begin it.
        var hour = Campaign.Clock.TimeOfDay.TotalHours;
        if (task.DutyWhen?.StartBetween is { Count: > 0 } windows && !windows.Any(w => hour >= w[0] && hour < w[1]))
        {
            var next = windows.Select(w => w[0]).Order().FirstOrDefault(h => h > hour, windows.Min(w => w[0]));
            return $"{(hour >= 20 || hour < 5 ? "Night" : "Too late in the day")}: {task.Name.ToLowerInvariant()} is begun again at {(int)next:00}:00.";
        }

        if (project.Laid == 0)
        {
            var short_ = task.Consumes.Where(c => Campaign.SharedStore.Count(c.Key) < c.Value).ToList();
            if (short_.Count > 0)
            {
                var (item, want) = short_[0];
                return $"Waiting for {ItemName(item).ToLowerInvariant()}: {want - Campaign.SharedStore.Count(item)} more wanted.";
            }
        }

        foreach (var tool in task.RequiredTools)
        {
            var whole = Campaign.SharedStore.Count(tool) + Campaign.Characters.Values.Sum(c => Campaign.Inventories[c.InventoryId].Count(tool));
            if (whole == 0)
            {
                var broken = Campaign.Camp.Broken.GetValueOrDefault(tool);
                return $"No {ItemName(tool).ToLowerInvariant()}: " + (broken > 0 ? $"{broken} broken, waiting to be mended." : "there is none whole.");
            }

            if (whole - ToolsInUse(tool, default) <= 0)
            {
                return $"No {ItemName(tool).ToLowerInvariant()} free: all in use at other work.";
            }
        }

        var able = Campaign.Crew.Where(c => c.CanWork && Campaign.Placements.TryGetValue(c.Id, out var at) && at.LocationId == CampLocation).ToList();
        if (task.Skill is { } skill && task.MinSkill > 0 && !able.Any(c => c.Skills.Get(skill) >= task.MinSkill))
        {
            return $"No one here can {task.Name.ToLowerInvariant()}: it wants {SkillName(skill).ToLowerInvariant()} {task.MinSkill}.";
        }

        if (!able.Any(c => Campaign.DutiesOf(c.Id).Contains(task.Id)))
        {
            return $"No one has {task.Name.ToLowerInvariant()} among their duties.";
        }

        return !InTurn(project) ? "Waiting: work of higher priority comes first." : "Waiting for hands: they are at work that comes first.";
    }

    // ================================================================ history

    // A building's work done, for its record: who did it, and what it used.
    private static void RecordWork(CampProject project, Character character, TaskDefinition definition) =>
        project.Built(character.Id, definition.Consumes);

    // The building finished from a project, with the project's record.
    private CampBuilding Finished(CampProject project, double quality) => new()
    {
        Building = project.Building, Plot = project.Plot, X = project.X, Y = project.Y, Turned = project.Turned, Quality = quality,
        Lead = project.Lead, BuiltUnder = project.Lead, Begun = project.Begun, Built = Campaign.Clock.Tick,
        Builders = new SortedDictionary<int, int>(project.Builders.ToDictionary()),
        Used = new SortedDictionary<string, int>(project.Used.ToDictionary(), StringComparer.Ordinal),
    };

    /// <summary>
    /// A building's history in lines (ECONOMY_LOOP §45): "Built day 12, begun day 9", "Led by Diego", "Built by
    /// Diego, Pedro and Martín", "From 20 poles and 36 thatch", "Weathered the storm of day 30 (−12)",
    /// "Repaired day 31".
    /// </summary>
    public IReadOnlyList<string> BuildingHistory(CampBuilding building)
    {
        string Day(long tick) => $"day {tick / SimulationClock.TicksPerDay + 1}";
        string Name(int id) => Campaign.Characters.TryGetValue(new CharacterId(id), out var c) ? c.Name : "someone gone";
        var lines = new List<string>();
        if (building.Built is { } built)
        {
            lines.Add($"Built {Day(built)}" + (building.Begun is { } begun ? $", begun {Day(begun)}." : "."));
        }

        if (building.BuiltUnder is { } under)
        {
            lines.Add($"Led by {Name(under.Value)}.");
        }

        if (building.Builders.Count > 0)
        {
            var hands = building.Builders.OrderByDescending(b => b.Value).ThenBy(b => b.Key).Select(b => Name(b.Key)).ToList();
            lines.Add("Built by " + (hands.Count == 1 ? hands[0] : string.Join(", ", hands[..^1]) + " and " + hands[^1]) + ".");
        }

        if (building.Used.Count > 0)
        {
            var from = building.Used.Select(u => $"{u.Value} {ItemName(u.Key).ToLowerInvariant()}").ToList();
            lines.Add("From " + (from.Count == 1 ? from[0] : string.Join(", ", from[..^1]) + " and " + from[^1]) + ".");
        }

        foreach (var happened in building.History)
        {
            lines.Add(happened.Kind == "storm" ? $"Weathered the storm of {Day(happened.Tick)} (−{happened.Amount:0})." : $"Repaired {Day(happened.Tick)} (+{happened.Amount:0}).");
        }

        if (building.Lead is { } now && now != building.BuiltUnder)
        {
            lines.Add($"Now in the charge of {Name(now.Value)}.");
        }

        return lines;
    }
}
