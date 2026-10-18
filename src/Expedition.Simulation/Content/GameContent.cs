using Expedition.Simulation.Tasks;

namespace Expedition.Simulation.Content;

/// <summary>
/// All immutable definitions the simulation needs. Content is validated when it is built,
/// so the rest of the simulation can rely on references being valid.
/// </summary>
public sealed class GameContent
{
    /// <summary>Builds and validates content.</summary>
    /// <exception cref="ContentValidationException">Any definition is invalid; all problems are listed.</exception>
    public GameContent(SimulationRules rules, IEnumerable<SkillDefinition> skills, IEnumerable<TaskDefinition> tasks)
    {
        var errors = new List<string>();
        Rules = rules;
        Skills = IndexById(skills, s => s.Id, "skill", errors);
        Tasks = IndexById(tasks, t => t.Id, "task", errors);

        ContentValidator.ValidateRules(rules, errors);
        foreach (var task in Tasks.Values)
        {
            ContentValidator.ValidateTask(task, Skills, errors);
        }

        if (errors.Count > 0)
        {
            throw new ContentValidationException(errors);
        }
    }

    /// <summary>Tunable rules for needs and work.</summary>
    public SimulationRules Rules { get; }

    /// <summary>Skill definitions by ID.</summary>
    public IReadOnlyDictionary<string, SkillDefinition> Skills { get; }

    /// <summary>Task definitions by ID.</summary>
    public IReadOnlyDictionary<string, TaskDefinition> Tasks { get; }

    private static Dictionary<string, T> IndexById<T>(IEnumerable<T> items, Func<T, string> id, string kind, List<string> errors)
    {
        var index = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!index.TryAdd(id(item), item))
            {
                errors.Add($"Duplicate {kind} ID '{id(item)}'.");
            }
        }

        return index;
    }
}

/// <summary>Thrown when content definitions are invalid. <see cref="Errors"/> lists every problem found.</summary>
public sealed class ContentValidationException(IReadOnlyList<string> errors)
    : Exception("Invalid game content:" + Environment.NewLine + string.Join(Environment.NewLine, errors))
{
    /// <summary>Every problem found, one per entry.</summary>
    public IReadOnlyList<string> Errors { get; } = errors;
}
