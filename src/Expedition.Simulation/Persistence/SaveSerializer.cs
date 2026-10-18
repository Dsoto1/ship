using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Items;
using Expedition.Simulation.Tasks;

namespace Expedition.Simulation.Persistence;

/// <summary>
/// Converts a campaign to and from versioned JSON (see docs/DECISIONS.md D-003).
/// Loading validates the whole file against the current content and either returns a complete
/// campaign or throws <see cref="SaveLoadException"/>; it never returns a partial one.
/// </summary>
public static class SaveSerializer
{
    /// <summary>Version written by this build. Increase it whenever <see cref="SaveData"/> changes, and add a migration.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Upgrades from version N (the key) to N + 1. Empty until the format first changes. Each step edits the
    /// raw JSON so old files never need old C# types.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, Func<JsonObject, JsonObject>> Migrations =
        new Dictionary<int, Func<JsonObject, JsonObject>>();

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Writes the complete campaign state as JSON.</summary>
    public static string Serialize(CampaignState campaign) =>
        JsonSerializer.Serialize(ToData(campaign), Options);

    /// <summary>Reads a campaign from JSON, upgrading older formats and validating against <paramref name="content"/>.</summary>
    /// <exception cref="SaveLoadException">The save is corrupt, from a newer version, or refers to content that does not exist.</exception>
    public static CampaignState Deserialize(string json, GameContent content) =>
        Deserialize(json, content, Migrations, CurrentSchemaVersion);

    /// <summary>Overload that takes the migration table and target version, so tests can exercise upgrades.</summary>
    internal static CampaignState Deserialize(
        string json, GameContent content, IReadOnlyDictionary<int, Func<JsonObject, JsonObject>> migrations, int currentVersion)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject ?? throw new SaveLoadException("The save file is not a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new SaveLoadException($"The save file is corrupt and cannot be read: {ex.Message}", ex);
        }

        var version = ReadVersion(root);
        if (version > currentVersion)
        {
            throw new SaveLoadException(
                $"This save was made by a newer version of the game (save format {version}; this build reads up to {currentVersion}). Update the game to load it.");
        }

        for (var v = version; v < currentVersion; v++)
        {
            if (!migrations.TryGetValue(v, out var migrate))
            {
                throw new SaveLoadException($"Save format {version} is too old: there is no upgrade from format {v}.");
            }

            root = migrate(root);
            root["schemaVersion"] = v + 1;
        }

        // The data shapes always describe the newest format; the stored number is checked above.
        root["schemaVersion"] = CurrentSchemaVersion;

        SaveData data;
        try
        {
            data = root.Deserialize<SaveData>(Options) ?? throw new SaveLoadException("The save file is empty.");
        }
        catch (JsonException ex)
        {
            throw new SaveLoadException($"The save file is corrupt: {ex.Message}", ex);
        }

        try
        {
            return FromData(data, content);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new SaveLoadException($"The save file contains invalid data: {ex.Message}", ex);
        }
        catch (NullReferenceException ex)
        {
            // A required field was present but null; System.Text.Json in .NET 8 only checks presence.
            throw new SaveLoadException("The save file is missing required data (a field is null).", ex);
        }
    }

    private static int ReadVersion(JsonObject root)
    {
        if (root["schemaVersion"] is not JsonValue value || !value.TryGetValue<int>(out var version))
        {
            throw new SaveLoadException("The save file has no valid schemaVersion and cannot be identified.");
        }

        if (version < 1)
        {
            throw new SaveLoadException($"The save file has an invalid schemaVersion ({version}).");
        }

        return version;
    }

    private static SaveData ToData(CampaignState campaign)
    {
        var (state, increment) = campaign.Random.SaveState();
        return new SaveData
        {
            SchemaVersion = CurrentSchemaVersion,
            Phase = campaign.Phase.ToString(),
            Tick = campaign.Clock.Tick,
            LastCharacterId = campaign.Ids.LastCharacterId,
            Random = new RandomData
            {
                State = state.ToString(CultureInfo.InvariantCulture),
                Increment = increment.ToString(CultureInfo.InvariantCulture),
            },
            SharedStoreId = campaign.SharedStoreId.Value,
            FoodShortageRecorded = campaign.FoodShortageRecorded,
            WaterShortageRecorded = campaign.WaterShortageRecorded,
            Characters = campaign.Characters.Values.OrderBy(c => c.Id.Value).Select(c => new CharacterData
            {
                Id = c.Id.Value,
                Name = c.Name,
                Age = c.Age,
                Origin = c.Origin,
                SurvivalState = c.SurvivalState.ToString(),
                Skills = new SortedDictionary<string, int>(c.Skills.Levels.ToDictionary(), StringComparer.Ordinal),
                Traits = c.Traits.Order(StringComparer.Ordinal).ToList(),
                Needs = new NeedsData { Hunger = c.Needs.Hunger, Thirst = c.Needs.Thirst, Fatigue = c.Needs.Fatigue, Stress = c.Needs.Stress },
                Health = c.Health.Value,
                Conditions = c.Health.Conditions.Order(StringComparer.Ordinal).ToList(),
            }).ToList(),
            Inventories = campaign.Inventories.Values.OrderBy(i => i.Id.Value, StringComparer.Ordinal).Select(i => new InventoryData
            {
                Id = i.Id.Value,
                Name = i.Name,
                MaxWeightKg = i.Capacity.MaxWeightKg,
                MaxVolumeLitres = i.Capacity.MaxVolumeLitres,
                Items = new SortedDictionary<string, int>(i.Quantities.ToDictionary(), StringComparer.Ordinal),
            }).ToList(),
            ActiveTasks = campaign.ActiveTasks.Values.OrderBy(t => t.CharacterId.Value).Select(t => new TaskData
            {
                CharacterId = t.CharacterId.Value,
                DefinitionId = t.DefinitionId,
                StartedAtTick = t.StartedAtTick,
                Progress = t.Progress,
            }).ToList(),
            Chronicle = campaign.Chronicle.Entries.Select(e => new ChronicleEntryData
            {
                Sequence = e.Sequence,
                Tick = e.Tick,
                Kind = e.Kind.ToString(),
                Description = e.Description,
                Characters = e.Characters.Select(id => id.Value).ToList(),
            }).ToList(),
        };
    }

    private static CampaignState FromData(SaveData data, GameContent content)
    {
        if (data.Tick < 0)
        {
            throw new SaveLoadException($"The save has a negative tick ({data.Tick}).");
        }

        var random = SimulationRandom.FromState(
            ulong.Parse(data.Random.State, CultureInfo.InvariantCulture),
            ulong.Parse(data.Random.Increment, CultureInfo.InvariantCulture));
        var campaign = CampaignState.CreateForRestore(ParseEnum<CampaignPhase>(data.Phase, "phase"), random, data.Tick);

        foreach (var saved in data.Inventories)
        {
            var inventory = campaign.CreateInventory(
                new InventoryId(saved.Id), saved.Name,
                new InventoryCapacity { MaxWeightKg = saved.MaxWeightKg, MaxVolumeLitres = saved.MaxVolumeLitres });
            foreach (var (itemId, quantity) in saved.Items)
            {
                if (!content.Items.ContainsKey(itemId))
                {
                    throw new SaveLoadException($"Inventory {saved.Id} holds unknown item '{itemId}'. The game content may have changed since this save was made.");
                }

                if (quantity <= 0)
                {
                    throw new SaveLoadException($"Inventory {saved.Id} has a non-positive quantity of {itemId}.");
                }

                inventory.Restore(itemId, quantity);
            }
        }

        foreach (var saved in data.Characters)
        {
            var character = RestoreCharacter(saved);
            if (!campaign.Inventories.ContainsKey(character.InventoryId))
            {
                throw new SaveLoadException($"{character.Name} ({character.Id}) has no saved personal inventory.");
            }

            campaign.AddCharacter(character);
        }

        if (data.LastCharacterId < campaign.Ids.LastCharacterId)
        {
            throw new SaveLoadException($"lastCharacterId {data.LastCharacterId} is lower than a saved character's ID.");
        }

        if (data.LastCharacterId > 0)
        {
            campaign.Ids.Reserve(new CharacterId(data.LastCharacterId));
        }

        var sharedStore = new InventoryId(data.SharedStoreId);
        if (!campaign.Inventories.ContainsKey(sharedStore) || campaign.Characters.Values.Any(c => c.InventoryId == sharedStore))
        {
            throw new SaveLoadException($"The shared store '{data.SharedStoreId}' is missing or is a personal inventory.");
        }

        campaign.RestoreSharedStore(sharedStore);
        campaign.FoodShortageRecorded = data.FoodShortageRecorded;
        campaign.WaterShortageRecorded = data.WaterShortageRecorded;

        foreach (var saved in data.ActiveTasks)
        {
            var id = new CharacterId(saved.CharacterId);
            if (!campaign.Characters.TryGetValue(id, out var worker))
            {
                throw new SaveLoadException($"An active task belongs to {id}, who is not in the save.");
            }

            if (!worker.CanWork)
            {
                throw new SaveLoadException($"{worker.Name} ({id}) is {worker.SurvivalState} but has an active task.");
            }

            if (!content.Tasks.ContainsKey(saved.DefinitionId))
            {
                throw new SaveLoadException($"{worker.Name} is doing '{saved.DefinitionId}', which no longer exists in the game content.");
            }

            if (campaign.ActiveTasks.ContainsKey(id))
            {
                throw new SaveLoadException($"{worker.Name} ({id}) has more than one active task.");
            }

            if (saved.Progress < 0 || saved.StartedAtTick < 0 || saved.StartedAtTick > data.Tick)
            {
                throw new SaveLoadException($"{worker.Name}'s task '{saved.DefinitionId}' has invalid progress or start time.");
            }

            campaign.StartTask(new TaskInstance(id, saved.DefinitionId, saved.StartedAtTick) { Progress = saved.Progress });
        }

        foreach (var saved in data.Chronicle)
        {
            var characters = saved.Characters.Select(n => new CharacterId(n)).ToArray();
            foreach (var id in characters.Where(id => !campaign.Characters.ContainsKey(id)))
            {
                throw new SaveLoadException($"Chronicle entry {saved.Sequence} refers to {id}, who is not in the save.");
            }

            campaign.Chronicle.Restore(new ChronicleEntry(
                saved.Sequence, saved.Tick, ParseEnum<ChronicleEntryKind>(saved.Kind, "chronicle kind"), saved.Description, characters));
        }

        return campaign;
    }

    private static Character RestoreCharacter(CharacterData saved)
    {
        if (saved.Id <= 0)
        {
            throw new SaveLoadException($"{saved.Name} has an invalid ID ({saved.Id}).");
        }

        // The constructor and setters validate names and ranges exactly as during play.
        var character = new Character(new CharacterId(saved.Id), saved.Name, saved.Age, saved.Origin);

        foreach (var (skillId, level) in saved.Skills)
        {
            character.Skills.Set(skillId, level);
        }

        foreach (var trait in saved.Traits)
        {
            character.AddTrait(trait);
        }

        character.Needs.Hunger = saved.Needs.Hunger;
        character.Needs.Thirst = saved.Needs.Thirst;
        character.Needs.Fatigue = saved.Needs.Fatigue;
        character.Needs.Stress = saved.Needs.Stress;
        character.Health.Value = saved.Health;
        foreach (var condition in saved.Conditions)
        {
            character.Health.AddCondition(condition);
        }

        character.RestoreSurvivalState(ParseEnum<SurvivalState>(saved.SurvivalState, "survival state"));
        return character;
    }

    private static T ParseEnum<T>(string value, string what) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: false, out var result) && Enum.IsDefined(result)
            ? result
            : throw new SaveLoadException($"Unknown {what} '{value}' in save file.");
}
