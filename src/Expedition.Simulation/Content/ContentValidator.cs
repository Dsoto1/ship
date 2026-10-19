using Expedition.Simulation.Characters;
using Expedition.Simulation.Items;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Content;

/// <summary>Checks definitions for values the simulation cannot handle.</summary>
internal static class ContentValidator
{
    public static void ValidateRules(SimulationRules rules, List<string> errors)
    {
        if (rules.WorkRateBySkillLevel.Count != Skills.MaxLevel + 1)
        {
            errors.Add($"Rules: workRateBySkillLevel needs {Skills.MaxLevel + 1} entries (levels 0..{Skills.MaxLevel}).");
        }
        else if (rules.WorkRateBySkillLevel.Any(rate => rate <= 0))
        {
            errors.Add("Rules: every workRateBySkillLevel entry must be positive.");
        }

        if (rules.WorkRateByAttributeLevel.Count != Attributes.MaxLevel + 1 || rules.WorkRateByAttributeLevel.Any(rate => rate <= 0))
        {
            errors.Add($"Rules: workRateByAttributeLevel needs {Attributes.MaxLevel + 1} positive entries (levels 0..{Attributes.MaxLevel}).");
        }

        if (rules.TiredWorkRateMultiplier is <= 0 or > 1)
        {
            errors.Add("Rules: tiredWorkRateMultiplier must be in (0, 1].");
        }

        if (rules.MaxFatigueToStartWork > rules.ExhaustionFatigue)
        {
            errors.Add("Rules: maxFatigueToStartWork must not exceed exhaustionFatigue.");
        }

        if (rules.RestAtFatigue is { } restAt && (restAt <= Needs.Min || restAt > rules.MaxFatigueToStartWork))
        {
            errors.Add("Rules: restAtFatigue must be above 0 and no higher than maxFatigueToStartWork.");
        }

        if (rules.IncapacitatedAtHealth is < Health.Min or >= Health.Max)
        {
            errors.Add($"Rules: incapacitatedAtHealth must be in {Health.Min}..{Health.Max - 1}.");
        }

        if (rules.EatAtHunger is <= Needs.Min or > Needs.Max || rules.DrinkAtThirst is <= Needs.Min or > Needs.Max)
        {
            errors.Add("Rules: eatAtHunger and drinkAtThirst must be in (0, 100].");
        }

        if (rules.WalkCellsPerMinute <= 0)
        {
            errors.Add("Rules: walkCellsPerMinute must be positive.");
        }

        if (rules.StarvationHealthLossPerHour < 0 || rules.DehydrationHealthLossPerHour < 0)
        {
            errors.Add("Rules: starvation and dehydration health loss must not be negative.");
        }
    }

    public static void ValidateItem(ItemDefinition item, List<string> errors)
    {
        if (item.WeightKg < 0 || item.VolumeLitres < 0)
        {
            errors.Add($"Item '{item.Id}': weightKg and volumeLitres must not be negative.");
        }

        if (!ItemDefinition.Packages.Contains(item.PackedIn) || item.PerPackage < 1)
        {
            errors.Add($"Item '{item.Id}': packedIn must be one of {string.Join(", ", ItemDefinition.Packages)}, and perPackage at least 1.");
        }

        if (item.HungerRelief < 0 || item.ThirstRelief < 0)
        {
            errors.Add($"Item '{item.Id}': hungerRelief and thirstRelief must not be negative.");
        }
    }

    public static void ValidateTask(
        TaskDefinition task,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        IReadOnlyDictionary<string, ItemDefinition> items,
        List<string> errors)
    {
        var where = $"Task '{task.Id}'";
        if (task.DurationMinutes <= 0)
        {
            errors.Add($"{where}: durationMinutes must be positive.");
        }

        if (task.Skill is not null && !skills.ContainsKey(task.Skill))
        {
            errors.Add($"{where}: unknown skill '{task.Skill}'.");
        }

        if (task.MinSkill is < Skills.MinLevel or > Skills.MaxLevel)
        {
            errors.Add($"{where}: minSkill must be {Skills.MinLevel}..{Skills.MaxLevel}.");
        }

        if (task.MinSkill > 0 && task.Skill is null)
        {
            errors.Add($"{where}: minSkill is set but no skill is named.");
        }

        foreach (var itemId in task.RequiredTools.Concat(task.Consumes.Keys).Where(id => !items.ContainsKey(id)))
        {
            errors.Add($"{where}: unknown item '{itemId}'.");
        }

        if (task.Consumes.Any(pair => pair.Value <= 0))
        {
            errors.Add($"{where}: consumed quantities must be positive.");
        }

        if (task.Hazard is { } hazard)
        {
            if (hazard.ChancePerHour is < 0 or > 1)
            {
                errors.Add($"{where}: hazard chancePerHour must be 0..1.");
            }

            if (hazard.HealthDamage <= 0)
            {
                errors.Add($"{where}: hazard healthDamage must be positive.");
            }
        }
    }

    public static void ValidateLocation(
        LocationDefinition location, IReadOnlyDictionary<string, TaskDefinition> tasks, List<string> errors)
    {
        var where = $"Location '{location.Id}'";
        if (location.Rows.Count == 0 || location.Rows.Any(row => row.Length != location.Width) || location.Width == 0)
        {
            errors.Add($"{where}: rows must be non-empty and all the same length.");
            return;
        }

        var badCharacters = location.Rows.SelectMany(row => row)
            .Where(c => c is not LocationDefinition.Walkable and not LocationDefinition.Blocked)
            .Distinct()
            .ToList();
        if (badCharacters.Count > 0)
        {
            errors.Add($"{where}: unknown layout characters '{string.Join("", badCharacters)}' (use '.' and '#').");
        }

        foreach (var duplicate in location.Spots.GroupBy(s => s.Id).Where(g => g.Count() > 1))
        {
            errors.Add($"{where}: duplicate spot ID '{duplicate.Key}'.");
        }

        foreach (var spot in location.Spots)
        {
            if (!location.IsWalkable(spot.Cell))
            {
                errors.Add($"{where}: spot '{spot.Id}' at {spot.Cell} is not on a walkable cell.");
            }

            if (spot.Face is { } face && !WorkSpotDefinition.Compass.Contains(face))
            {
                errors.Add($"{where}: spot '{spot.Id}' faces '{face}'; use N, NE, E, SE, S, SW, W or NW.");
            }

            foreach (var taskId in spot.Tasks)
            {
                if (!tasks.TryGetValue(taskId, out var task))
                {
                    errors.Add($"{where}: spot '{spot.Id}' lists unknown task '{taskId}'.");
                }
                else if (!task.NeedsWorkSpot)
                {
                    errors.Add($"{where}: spot '{spot.Id}' lists '{taskId}', which does not use work spots.");
                }
            }
        }
    }
}
