using System.Text.Json;
using Expedition.Simulation.Items;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Content;

/// <summary>
/// Reads content definitions from JSON. Expected layout under the data directory:
/// <c>simulation_rules.json</c>, <c>skills.json</c>, <c>items.json</c>, and any number of <c>tasks/*.json</c>
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
    };

    /// <summary>Loads and validates all content from <paramref name="dataDirectory"/>.</summary>
    /// <exception cref="ContentValidationException">A file is missing, malformed or invalid.</exception>
    public static GameContent LoadFromDirectory(string dataDirectory)
    {
        var rules = Read<SimulationRules>(Path.Combine(dataDirectory, "simulation_rules.json"));
        var skills = Read<List<SkillDefinition>>(Path.Combine(dataDirectory, "skills.json"));
        var items = Read<List<ItemDefinition>>(Path.Combine(dataDirectory, "items.json"));
        var tasks = Directory.EnumerateFiles(Path.Combine(dataDirectory, "tasks"), "*.json")
            .Order(StringComparer.Ordinal)
            .SelectMany(Read<List<TaskDefinition>>)
            .ToList();
        var locationsDirectory = Path.Combine(dataDirectory, "locations");
        var locations = Directory.Exists(locationsDirectory)
            ? Directory.EnumerateFiles(locationsDirectory, "*.json").Order(StringComparer.Ordinal).SelectMany(Read<List<LocationDefinition>>).ToList()
            : [];
        return new GameContent(rules, skills, items, tasks, locations);
    }

    /// <summary>Parses content from JSON strings. Intended for tests and tools.</summary>
    /// <exception cref="ContentValidationException">The JSON is malformed or the content is invalid.</exception>
    public static GameContent LoadFromJson(string rulesJson, string skillsJson, string itemsJson, string tasksJson) =>
        new(Parse<SimulationRules>(rulesJson, "rules"),
            Parse<List<SkillDefinition>>(skillsJson, "skills"),
            Parse<List<ItemDefinition>>(itemsJson, "items"),
            Parse<List<TaskDefinition>>(tasksJson, "tasks"));

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
