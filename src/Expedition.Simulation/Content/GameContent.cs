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
        IEnumerable<StatusDefinition>? statuses = null,
        Voyage.VoyageRules? voyage = null,
        IEnumerable<Events.EventDefinition>? events = null,
        Port.PortRules? port = null)
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
        Voyage = voyage;
        Events = IndexById(events ?? [], e => e.Id, "event", errors);
        Port = port;
        if (voyage is not null)
        {
            errors.AddRange(voyage.Validate());
        }

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

            if (task.Then is not null && !Tasks.ContainsKey(task.Then))
            {
                errors.Add($"Task '{task.Id}': then names unknown task '{task.Then}'.");
            }

            if (task.DutyWhen?.StartBetween is { } windows && windows.Any(w => w.Length != 2 || w[0] < 0 || w[1] > 24 || w[0] >= w[1]))
            {
                errors.Add($"Task '{task.Id}': dutyWhen.startBetween windows must be [from, to) hours with 0 <= from < to <= 24.");
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

        foreach (var definition in Events.Values)
        {
            ValidateEvent(definition, errors);
        }

        if (port is not null)
        {
            ValidatePort(port, errors);
        }

        if (errors.Count > 0)
        {
            throw new ContentValidationException(errors);
        }

        Foods = Items.Values.Where(i => i.HungerRelief > 0).OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
        Drinks = Items.Values.Where(i => i.ThirstRelief > 0).OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>The port and its people, or null if the content has none.</summary>
    public Port.PortRules? Port { get; }

    private void ValidatePort(Port.PortRules rules, List<string> errors)
    {
        if (!Locations.TryGetValue(rules.Location, out var waterfront) || !Locations.ContainsKey(rules.ShipLocation))
        {
            errors.Add($"Port: unknown location '{rules.Location}' or ship location '{rules.ShipLocation}'.");
            return;
        }

        if (waterfront.FindSpot(rules.Gangway) is null)
        {
            errors.Add($"Port: gangway '{rules.Gangway}' is not a spot on the waterfront.");
        }

        if (rules.Funds < 0 || rules.Berths < 1 || rules.MinimumCrew < 1 || rules.MinimumCrew > rules.Berths || rules.LateFeePerHalfDay < 0)
        {
            errors.Add("Port: needs funds >= 0, 1 <= minimumCrew <= berths, and a late fee >= 0.");
        }

        foreach (var lot in rules.Goods.Where(g => !Items.ContainsKey(g.Item) || g.Quantity <= 0 || g.Price < 0 || g.Stock < 0 || g.SoldPerDay < 0))
        {
            errors.Add($"Port: chandler lot '{lot.Item}' needs a known item, a positive quantity, and non-negative price, stock and sales.");
        }

        var ids = rules.People.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != rules.People.Count)
        {
            errors.Add("Port: person IDs must be unique.");
        }

        bool KnownTopic(string topic) => Skills.ContainsKey(topic) || Languages.ContainsKey(topic) || Traits.ContainsKey(topic) || Attributes.ContainsKey(topic);
        foreach (var person in rules.People)
        {
            var where = $"Person '{person.Id}'";
            if (!Statuses.ContainsKey(person.Status))
            {
                errors.Add($"{where}: unknown status '{person.Status}'.");
            }

            if (waterfront.FindSpot(person.Home) is not { } home || !home.Tasks.Contains("task.sleep_at_home"))
            {
                errors.Add($"{where}: home '{person.Home}' must be a waterfront spot for task.sleep_at_home.");
            }

            foreach (var block in person.Schedule)
            {
                if (!Tasks.ContainsKey(block.Task) || block.From is < 0 or > 23 || block.To is < 0 or > 24 || block.From == block.To)
                {
                    errors.Add($"{where}: schedule block {block.From}-{block.To} '{block.Task}' needs a known task and valid hours.");
                }
                else if (block.Spot is { } spot && waterfront.FindSpot(spot)?.Tasks.Contains(block.Task) != true)
                {
                    errors.Add($"{where}: spot '{spot}' does not offer '{block.Task}'.");
                }
                else if (!waterfront.Spots.Any(s => s.Tasks.Contains(block.Task)))
                {
                    errors.Add($"{where}: nowhere on the waterfront offers '{block.Task}'.");
                }
            }

            var unknown = person.Skills.Keys.Where(k => !Skills.ContainsKey(k))
                .Concat(person.Attributes.Keys.Where(k => !Attributes.ContainsKey(k)))
                .Concat(person.Languages.Keys.Where(k => !Languages.ContainsKey(k)))
                .Concat(person.Traits.Where(t => !Traits.ContainsKey(t)))
                .Concat((person.Seeking?.Claims.Keys ?? []).Concat(person.Knows.SelectMany(k => k.Levels.Keys)).Where(k => !KnownTopic(k)))
                .Concat(person.Knows.Select(k => k.About).Concat(person.Secrets.Select(s => s.WithPerson).OfType<string>())
                    .Concat(person.Seeking?.WontSailWith.Select(a => a.Person) ?? []).Where(p => !ids.Contains(p)))
                .Concat(person.Secrets.Select(s => s.During).Where(t => !Tasks.ContainsKey(t)))
                .ToList();
            if (unknown.Count > 0)
            {
                errors.Add($"{where}: unknown {string.Join(", ", unknown)}.");
            }

            var levels = person.Skills.Concat(person.Attributes).Concat(person.Languages)
                .Concat(person.Seeking?.Claims ?? new Dictionary<string, int>()).Concat(person.Knows.SelectMany(k => k.Levels));
            if (levels.Any(pair => pair.Value is < 0 or > 5))
            {
                errors.Add($"{where}: levels must be 0..5.");
            }
        }
    }

    /// <summary>Event definitions by ID.</summary>
    public IReadOnlyDictionary<string, Events.EventDefinition> Events { get; }

    private static readonly string[] EffectTargets = ["subject", "other", "captain", "crew", "all"];

    private void ValidateEvent(Events.EventDefinition definition, List<string> errors)
    {
        var where = $"Event '{definition.Id}'";
        if (definition.Choices.Count == 0 || definition.Choices.Select(c => c.Id).Distinct().Count() != definition.Choices.Count)
        {
            errors.Add($"{where}: needs at least one choice, with unique IDs.");
        }

        if (definition.Trigger.ChancePerDay is < 0 or > 24)
        {
            errors.Add($"{where}: chancePerDay must be 0..24.");
        }

        if (definition.Trigger.Weather?.Any(w => Voyage?.Weathers.All(known => known.Id != w) ?? true) == true)
        {
            errors.Add($"{where}: unknown weather in trigger.");
        }

        if (definition.Trigger.Subject is { Pick: not ("crew" or "anyone" or "stressed" or "feud") })
        {
            errors.Add($"{where}: subject pick must be crew, anyone, stressed or feud.");
        }

        if (definition.Trigger.Subject?.Trait is { } trait && !Traits.ContainsKey(trait))
        {
            errors.Add($"{where}: unknown trait '{trait}'.");
        }

        foreach (var choice in definition.Choices)
        {
            var at = $"{where}, choice '{choice.Id}'";
            if (choice.RequiresItem is { } required && !Items.ContainsKey(required))
            {
                errors.Add($"{at}: unknown item '{required}'.");
            }

            if (choice.Check is { } check && ((check.Skill is { } skill && !Skills.ContainsKey(skill)) || check.Who is not ("best" or "subject" or "captain")))
            {
                errors.Add($"{at}: check needs a known skill and who = best, subject or captain.");
            }

            if (choice.Check is not null && choice.Failure is null)
            {
                errors.Add($"{at}: a choice with a check needs a failure outcome.");
            }

            foreach (var effect in new[] { choice.Success, choice.Failure }.Where(o => o is not null).SelectMany(o => o!.Effects))
            {
                if (effect.Item is { } item && !Items.ContainsKey(item))
                {
                    errors.Add($"{at}: unknown item '{item}'.");
                }

                if (effect.Condition is { } condition && !Conditions.ContainsKey(condition))
                {
                    errors.Add($"{at}: unknown condition '{condition}'.");
                }

                if ((effect.Who is { } who && !EffectTargets.Contains(who))
                    || (effect.Relationship is { } change && (!EffectTargets.Contains(change.From) || change.To is not ("subject" or "other" or "captain"))))
                {
                    errors.Add($"{at}: who, from and to must be subject, other, captain, crew or all (to: subject, other or captain).");
                }
            }
        }
    }

    /// <summary>Rules for sea passages, or null if the content has none.</summary>
    public Voyage.VoyageRules? Voyage { get; }

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
