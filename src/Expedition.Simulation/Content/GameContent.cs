using Expedition.Simulation.Items;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Content;

/// <summary>
/// All immutable definitions the simulation needs. Content is validated when it is built,
/// so the rest of the simulation can rely on references being valid.
/// </summary>
public sealed class GameContent
{
    /// <summary>Builds and validates content.</summary>
    /// <exception cref="ContentValidationException">Any definition is invalid; all problems are listed.</exception>
    public GameContent(
        SimulationRules rules,
        IEnumerable<SkillDefinition> skills,
        IEnumerable<ItemDefinition> items,
        IEnumerable<TaskDefinition> tasks,
        IEnumerable<LocationDefinition>? locations = null,
        IEnumerable<AttributeDefinition>? attributes = null,
        IEnumerable<TraitDefinition>? traits = null,
        IEnumerable<ConditionDefinition>? conditions = null,
        AppearanceOptions? appearance = null,
        IEnumerable<LanguageDefinition>? languages = null,
        IEnumerable<StatusDefinition>? statuses = null)
    {
        var errors = new List<string>();
        Rules = rules;
        Skills = IndexById(skills, s => s.Id, "skill", errors);
        Items = IndexById(items, i => i.Id, "item", errors);
        Tasks = IndexById(tasks, t => t.Id, "task", errors);
        Locations = IndexById(locations ?? [], l => l.Id, "location", errors);
        Attributes = IndexById(attributes ?? [], a => a.Id, "attribute", errors);
        Traits = IndexById(traits ?? [], t => t.Id, "trait", errors);
        Conditions = IndexById(conditions ?? [], c => c.Id, "condition", errors);
        Appearance = appearance ?? AppearanceOptions.Empty;
        Languages = IndexById(languages ?? [], l => l.Id, "language", errors);
        Statuses = IndexById(statuses ?? [], st => st.Id, "status", errors);
        foreach (var status in Statuses.Values.Where(st => st.TypicalMonthlyWage < 0))
        {
            errors.Add($"Status '{status.Id}': typicalMonthlyWage must not be negative.");
        }
        var appearanceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (listName, options) in Appearance.AllLists)
        {
            foreach (var option in options)
            {
                if (!appearanceIds.Add(option.Id))
                {
                    errors.Add($"Appearance: duplicate option ID '{option.Id}'.");
                }

                if (!option.HasValidColour)
                {
                    errors.Add($"Appearance: '{option.Id}' in {listName} has colour '{option.Colour}'; use #RRGGBB.");
                }

                if (option.Weight <= 0 || option.MinAge < 0)
                {
                    errors.Add($"Appearance: '{option.Id}' needs a positive weight and a non-negative minAge.");
                }
            }
        }

        ContentValidator.ValidateRules(rules, errors);
        foreach (var item in Items.Values)
        {
            ContentValidator.ValidateItem(item, errors);
        }

        foreach (var task in Tasks.Values)
        {
            ContentValidator.ValidateTask(task, Skills, Items, errors);
            if (task.Attribute is not null && !Attributes.ContainsKey(task.Attribute))
            {
                errors.Add($"Task '{task.Id}': unknown attribute '{task.Attribute}'.");
            }

            if (task.Hazard is { } hazard && !Conditions.ContainsKey(hazard.Condition))
            {
                errors.Add($"Task '{task.Id}': hazard causes unknown condition '{hazard.Condition}'.");
            }
        }

        foreach (var location in Locations.Values)
        {
            ContentValidator.ValidateLocation(location, Tasks, errors);
        }

        if (errors.Count > 0)
        {
            throw new ContentValidationException(errors);
        }

        Foods = Items.Values.Where(i => i.HungerRelief > 0).OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
        Drinks = Items.Values.Where(i => i.ThirstRelief > 0).OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>Tunable rules for needs, work and provisions.</summary>
    public SimulationRules Rules { get; }

    /// <summary>Skill definitions by ID.</summary>
    public IReadOnlyDictionary<string, SkillDefinition> Skills { get; }

    /// <summary>Attribute definitions by ID.</summary>
    public IReadOnlyDictionary<string, AttributeDefinition> Attributes { get; }

    /// <summary>Trait definitions by ID.</summary>
    public IReadOnlyDictionary<string, TraitDefinition> Traits { get; }

    /// <summary>Condition (injury and illness) definitions by ID.</summary>
    public IReadOnlyDictionary<string, ConditionDefinition> Conditions { get; }

    /// <summary>Language definitions by ID.</summary>
    public IReadOnlyDictionary<string, LanguageDefinition> Languages { get; }

    /// <summary>Social status definitions by ID.</summary>
    public IReadOnlyDictionary<string, StatusDefinition> Statuses { get; }

    /// <summary>Options characters' appearance is generated from.</summary>
    public AppearanceOptions Appearance { get; }

    /// <summary>Item definitions by ID.</summary>
    public IReadOnlyDictionary<string, ItemDefinition> Items { get; }

    /// <summary>Task definitions by ID.</summary>
    public IReadOnlyDictionary<string, TaskDefinition> Tasks { get; }

    /// <summary>Location definitions by ID.</summary>
    public IReadOnlyDictionary<string, LocationDefinition> Locations { get; }

    /// <summary>Items that relieve hunger, in ID order. Characters eat the first one available.</summary>
    public IReadOnlyList<ItemDefinition> Foods { get; }

    /// <summary>Items that relieve thirst, in ID order. Characters drink the first one available.</summary>
    public IReadOnlyList<ItemDefinition> Drinks { get; }

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
