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
            if (item.SpoilsPerDay is < 0 or > 1)
            {
                errors.Add($"Item '{item.Id}': spoilsPerDay must be between 0 and 1.");
            }

            if (item.Sickens is { } sickness && (!Conditions.ContainsKey(sickness.Condition) || sickness.Chance is < 0 or > 1))
            {
                errors.Add($"Item '{item.Id}': sickens needs a known condition and a chance between 0 and 1.");
            }
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

            if (task.InRain is not null && !Tasks.ContainsKey(task.InRain))
            {
                errors.Add($"Task '{task.Id}': inRain names unknown task '{task.InRain}'.");
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
            ValidateEvent(definition, port, errors);
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

        if (rules.Weather is { } weather)
        {
            if (weather.Find(weather.Start) is null)
            {
                errors.Add($"Port weather: the start '{weather.Start}' is not one of the kinds.");
            }

            foreach (var kind in weather.Kinds)
            {
                if (kind.MinHours <= 0 || kind.MaxHours < kind.MinHours || kind.FromHour < 0 || kind.ToHour > 24 || kind.FromHour >= kind.ToHour
                    || kind.Rain is < 0 or > 1 || kind.Fog is < 0 or > 1 || kind.Wind is < 0 or > 1 || kind.Gloom is < 0 or > 1 || kind.Sight is <= 0 or > 1)
                {
                    errors.Add($"Port weather '{kind.Id}': needs 0 < minHours <= maxHours, 0 <= fromHour < toHour <= 24, rain, fog, wind and gloom in 0..1, sight in (0, 1].");
                }

                foreach (var (next, weight) in kind.Next)
                {
                    if (weather.Find(next) is null || weight < 0)
                    {
                        errors.Add($"Port weather '{kind.Id}': next names unknown weather '{next}' or a negative weight.");
                    }
                }
            }

            if (weather.Kinds.Select(k => k.Id).Distinct().Count() != weather.Kinds.Count)
            {
                errors.Add("Port weather: two kinds share an ID.");
            }
        }

        var personIds = rules.People.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var person in rules.People)
        {
            if (person.Claim is { } claim && (!personIds.Contains(claim.Debtor) || claim.Amount <= 0))
            {
                errors.Add($"Person '{person.Id}': their claim names unknown debtor '{claim.Debtor}' or a non-positive amount.");
            }

            if (person.Greetings.Concat(rules.Recognition.Greetings).Any(g => g.Standing is < 1 or > 3 || string.IsNullOrWhiteSpace(g.Text)))
            {
                errors.Add($"Greetings (for '{person.Id}' or the port): standing must be 1, 2 or 3, with a text.");
            }
        }

        if (rules.Recognition is { NoticedAt: < 1 } or { ApproachEveryMinutes: < 1 } or { ApproachRange: < 1 }
            || rules.Recognition.TalkedOfAt < rules.Recognition.NoticedAt || rules.Recognition.KnownAt < rules.Recognition.TalkedOfAt)
        {
            errors.Add("Port recognition: thresholds must rise (noticed <= talked of <= known), with a positive interval and range.");
        }

        foreach (var scene in rules.Ambient)
        {
            bool Walkable(IReadOnlyList<int>? cell) => cell is [var x, var y] && waterfront is not null && waterfront.IsWalkable(new World.GridPosition(x, y));
            if (!Expedition.Simulation.Port.AmbientScene.Kinds.Contains(scene.Kind) || !Walkable(scene.At) || (scene.To is not null && !Walkable(scene.To))
                || scene.Weather is not ("any" or "dry" or "wet") || scene.FromHour is < 0 or > 23 || scene.ToHour is < 1 or > 24 || string.IsNullOrWhiteSpace(scene.Text))
            {
                errors.Add($"Ambient scene '{scene.Id}': needs a known kind, walkable 'at' (and 'to') cells, weather any, dry or wet, hours in 0..24 and a text.");
            }
        }

        if (rules.Preparation is { LoadingHours: <= 0 } or { StowingHours: < 0 } or { DepartureMorningHours: < 0 } or { TakingShapeLots: < 0 }
            || rules.Preparation.ReadyLots < rules.Preparation.TakingShapeLots)
        {
            errors.Add("Port preparation: loadingHours must be positive, the other hours not negative, and readyLots at least takingShapeLots.");
        }

        foreach (var lot in rules.Goods.Where(g => !Items.ContainsKey(g.Item) || g.Quantity <= 0 || g.Price < 0 || g.Stock < 0 || g.SoldPerDay < 0))
        {
            errors.Add($"Port: chandler lot '{lot.Item}' needs a known item, a positive quantity, and non-negative price, stock and sales.");
        }

        var ids = rules.People.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        if (rules.Prices is { RiseHours: <= 0 } or { Rise: < 0 } or { Scarcity: < 0 } || rules.Prices.StandingDiscount.Any(d => d is < 0 or >= 1)
            || rules.Prices.CreditLimit.Any(c => c < 0))
        {
            errors.Add("Port prices: riseHours must be positive; rise and scarcity not negative; discounts from 0 to under 1; credit not negative.");
        }

        foreach (var person in rules.People.Where(p => p.FarewellBy.Any(by => !ids.Contains(by)) || p.FarewellWavers < 0))
        {
            errors.Add($"Port: '{person.Id}' is seen off by someone not in the port, or by a negative number of wavers.");
        }

        foreach (var seller in rules.Sellers.Where(s => !ids.Contains(s.Person)))
        {
            errors.Add($"Port: seller '{seller.Id}' is sold by '{seller.Person}', who is not one of the port's people.");
        }

        foreach (var lot in rules.Goods.Where(g => (g.Seller is { } seller && rules.Sellers.All(s => s.Id != seller)) || !Expedition.Simulation.Port.PortRules.Qualities.Contains(g.Quality)))
        {
            errors.Add($"Port: lot '{lot.Item}' names an unknown seller or quality '{lot.Quality}' (good, fair or poor).");
        }
        if (ids.Count != rules.People.Count)
        {
            errors.Add("Port: person IDs must be unique.");
        }

        var leads = rules.AllLeads().ToList();
        if (leads.Select(l => l.Lead.Id).Distinct(StringComparer.Ordinal).Count() != leads.Count)
        {
            errors.Add("Port: lead IDs must be unique.");
        }

        foreach (var (lead, _) in leads.Where(l => !ids.Contains(l.Lead.About) || string.IsNullOrWhiteSpace(l.Lead.Text) || string.IsNullOrWhiteSpace(l.Lead.Where)))
        {
            errors.Add($"Port: lead '{lead.Id}' needs a known person, text and where.");
        }

        if (rules.NoticeBoard is { } board && waterfront.FindSpot(board) is null)
        {
            errors.Add($"Port: notice board '{board}' is not a spot on the waterfront.");
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

            // Conversations: every topic's needs and gifts must exist, so nothing opens on a typo.
            if (person.Conversation is { } conversation)
            {
                var topics = conversation.Topics.Select(t => t.Id).ToList();
                if (topics.Distinct(StringComparer.Ordinal).Count() != topics.Count)
                {
                    errors.Add($"{where}: conversation topic IDs must be unique.");
                }

                foreach (var topic in conversation.Topics)
                {
                    var wrong = topic.After.Where(t => !topics.Contains(t))
                        .Concat(topic.Known.Where(f => person.FactText(f) is null))
                        .Concat(topic.Tells.Where(f => (topic.About is { } about ? rules.People.FirstOrDefault(p => p.Id == about) : person)?.FactText(f) is null))
                        .Concat(topic.About is { } other && !ids.Contains(other) ? [other] : [])
                        .Concat(topic.Leads.Where(l => person.Leads.All(lead => lead.Id != l)))
                        .Concat(topic.Claims.Keys.Where(k => !KnownTopic(k)))
                        .ToList();
                    if (wrong.Count > 0 || string.IsNullOrWhiteSpace(topic.Ask) || string.IsNullOrWhiteSpace(topic.Says) || topic.Claims.Values.Any(v => v is < 0 or > 5))
                    {
                        errors.Add($"{where}: topic '{topic.Id}' needs words and known things{(wrong.Count > 0 ? $"; unknown {string.Join(", ", wrong)}" : "")}.");
                    }
                }
            }

            if (person.Song is { } song && (string.IsNullOrWhiteSpace(song.Title) || string.IsNullOrWhiteSpace(song.Says)
                    || song.Voice is not ("man" or "woman" or "cradle" or "kitchen" or "chorus")
                    || song.Shows.Any(pair => !KnownTopic(pair.Key) || pair.Value is < 0 or > 5)))
            {
                errors.Add($"{where}: their song needs a title, words for how it is sung, and known topics at 0..5 for what it shows.");
            }

            foreach (var reference in person.Knows.Where(k => k.Fact is not null))
            {
                if (rules.People.FirstOrDefault(p => p.Id == reference.About)?.FactText(reference.Fact!) is null)
                {
                    errors.Add($"{where}: what they know of '{reference.About}' tells '{reference.Fact}', which is not one of that person's facts.");
                }
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

    private void ValidateEvent(Events.EventDefinition definition, Port.PortRules? port, List<string> errors)
    {
        var where = $"Event '{definition.Id}'";
        if (definition.Trigger.Subject?.Person is { } person && port?.People.All(p => p.Id != person) != false)
        {
            errors.Add($"{where}: subject person '{person}' is not one of the port's people.");
        }

        if (definition.Trigger.LeaguesBelow is <= 0)
        {
            errors.Add($"{where}: leaguesBelow must be positive.");
        }
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
