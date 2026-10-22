using System.Text.Json;
using Expedition.Simulation.Items;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Content;

/// <summary>
/// Reads content definitions from JSON. Expected layout under the data directory:
/// <c>simulation_rules.json</c>, <c>skills.json</c>, <c>attributes.json</c>, <c>traits.json</c>,
/// <c>conditions.json</c>, <c>appearance.json</c>, <c>languages.json</c>, <c>statuses.json</c>,
/// <c>items.json</c>, and any number of <c>tasks/*.json</c>
/// and <c>locations/*.json</c> files,
/// each holding a JSON array of definitions.
/// </summary>
public static class ContentLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false) },
    };

    /// <summary>Loads and validates all content from <paramref name="dataDirectory"/>.</summary>
    /// <exception cref="ContentValidationException">A file is missing, malformed or invalid.</exception>
    public static GameContent LoadFromDirectory(string dataDirectory)
    {
        var rules = Read<SimulationRules>(Path.Combine(dataDirectory, "simulation_rules.json"));
        var skills = Read<List<SkillDefinition>>(Path.Combine(dataDirectory, "skills.json"));
        var items = Read<List<ItemDefinition>>(Path.Combine(dataDirectory, "items.json"));
        var attributes = Read<List<AttributeDefinition>>(Path.Combine(dataDirectory, "attributes.json"));
        var traits = Read<List<TraitDefinition>>(Path.Combine(dataDirectory, "traits.json"));
        var conditions = Read<List<ConditionDefinition>>(Path.Combine(dataDirectory, "conditions.json"));
        var appearance = Read<AppearanceOptions>(Path.Combine(dataDirectory, "appearance.json"));
        var languages = Read<List<LanguageDefinition>>(Path.Combine(dataDirectory, "languages.json"));
        var statuses = Read<List<StatusDefinition>>(Path.Combine(dataDirectory, "statuses.json"));
        if (!appearance.CanGenerate)
        {
            throw new ContentValidationException(["appearance.json: every option list needs at least one entry."]);
        }
        var tasks = Directory.EnumerateFiles(Path.Combine(dataDirectory, "tasks"), "*.json")
            .Order(StringComparer.Ordinal)
            .SelectMany(Read<List<TaskDefinition>>)
            .ToList();
        var locationsDirectory = Path.Combine(dataDirectory, "locations");
        var locations = Directory.Exists(locationsDirectory)
            ? Directory.EnumerateFiles(locationsDirectory, "*.json").Order(StringComparer.Ordinal).SelectMany(Read<List<LocationDefinition>>).ToList()
            : [];
        var voyagePath = Path.Combine(dataDirectory, "voyage.json");
        var voyage = File.Exists(voyagePath) ? Read<Voyage.VoyageRules>(voyagePath) : null;
        var eventsDirectory = Path.Combine(dataDirectory, "events");
        var events = Directory.Exists(eventsDirectory)
            ? Directory.EnumerateFiles(eventsDirectory, "*.json").Order(StringComparer.Ordinal).SelectMany(Read<List<Events.EventDefinition>>).ToList()
            : [];
        var portPath = Path.Combine(dataDirectory, "port", "bocana.json");
        var port = File.Exists(portPath) ? Read<Port.PortRules>(portPath) : null;
        var shorePath = Path.Combine(dataDirectory, "shore.json");
        var shore = File.Exists(shorePath) ? Read<Shore.ShoreRules>(shorePath) : null;
        return new GameContent(rules, skills, items, tasks, locations, attributes, traits, conditions, appearance, languages, statuses, voyage, events, port, shore);
    }

    /// <summary>Parses content from JSON strings. Intended for tests and tools.</summary>
    /// <exception cref="ContentValidationException">The JSON is malformed or the content is invalid.</exception>
    public static GameContent LoadFromJson(
        string rulesJson, string skillsJson, string itemsJson, string tasksJson, string conditionsJson = "[]") =>
        new(Parse<SimulationRules>(rulesJson, "rules"),
            Parse<List<SkillDefinition>>(skillsJson, "skills"),
            Parse<List<ItemDefinition>>(itemsJson, "items"),
            Parse<List<TaskDefinition>>(tasksJson, "tasks"),
            conditions: Parse<List<ConditionDefinition>>(conditionsJson, "conditions"));

    private static T Read<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new ContentValidationException([$"Missing content file '{path}'."]);
        }

        return Parse<T>(File.ReadAllText(path), path);
    }

    private static T Parse<T>(string json, string source)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options)
                ?? throw new ContentValidationException([$"{source}: file is empty or null."]);
        }
        catch (JsonException ex)
        {
            throw new ContentValidationException([$"{source}: {ex.Message}"]);
        }
    }
}
