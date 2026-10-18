using Expedition.Simulation.Characters;
using Expedition.Simulation.Items;
using Expedition.Simulation.Tasks;

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

        if (rules.TiredWorkRateMultiplier is <= 0 or > 1)
        {
            errors.Add("Rules: tiredWorkRateMultiplier must be in (0, 1].");
        }

        if (rules.MaxFatigueToStartWork > rules.ExhaustionFatigue)
        {
            errors.Add("Rules: maxFatigueToStartWork must not exceed exhaustionFatigue.");
        }

        if (rules.IncapacitatedAtHealth is < Health.Min or >= Health.Max)
        {
            errors.Add($"Rules: incapacitatedAtHealth must be in {Health.Min}..{Health.Max - 1}.");
        }

        if (rules.EatAtHunger is <= Needs.Min or > Needs.Max || rules.DrinkAtThirst is <= Needs.Min or > Needs.Max)
        {
            errors.Add("Rules: eatAtHunger and drinkAtThirst must be in (0, 100].");
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
}
