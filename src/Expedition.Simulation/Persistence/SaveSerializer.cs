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
using Expedition.Simulation.World;

namespace Expedition.Simulation.Persistence;

/// <summary>
/// Converts a campaign to and from versioned JSON (see docs/DECISIONS.md D-003).
/// Loading validates the whole file against the current content and either returns a complete
/// campaign or throws <see cref="SaveLoadException"/>; it never returns a partial one.
/// </summary>
public static class SaveSerializer
{
    /// <summary>Version written by this build. Increase it whenever <see cref="SaveData"/> changes, and add a migration.</summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary>
    /// Upgrades from version N (the key) to N + 1. Each step edits the raw JSON so old files never need
    /// old C# types.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, Func<JsonObject, JsonObject>> Migrations =
        new Dictionary<int, Func<JsonObject, JsonObject>>
        {
            // Format 2 added character positions. Format 1 campaigns had no maps, so nobody was placed.
            [1] = root =>
            {
                root["placements"] = new JsonArray();
                return root;
            },
            // Format 3 replaced each chronicle entry's English description with structured parameters.
            // Old entries keep their text as a legacy parameter, which the formatter shows unchanged.
            [2] = root =>
            {
                foreach (var entry in root["chronicle"]?.AsArray() ?? [])
                {
                    if (entry is JsonObject fields)
                    {
                        var text = fields["description"]?.GetValue<string>() ?? "";
                        fields.Remove("description");
                        fields["parameters"] = new JsonObject { [ChronicleParameters.LegacyText] = text };
                    }
                }

                return root;
            },
        };

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
            Placements = campaign.Placements.OrderBy(pair => pair.Key.Value).Select(pair => new PlacementData
            {
                CharacterId = pair.Key.Value,
                LocationId = pair.Value.LocationId,
                X = pair.Value.Cell.X,
                Y = pair.Value.Cell.Y,
                Movement = pair.Value.Movement is { } movement
                    ? new MovementData
                    {
                        Path = movement.Path.Select(cell => new[] { cell.X, cell.Y }).ToList(),
                        StartTick = movement.StartTick,
                        ArriveTick = movement.ArriveTick,
                    }
                    : null,
            }).ToList(),
            ActiveTasks = campaign.ActiveTasks.Values.OrderBy(t => t.CharacterId.Value).Select(t => new TaskData
            {
                CharacterId = t.CharacterId.Value,
                DefinitionId = t.DefinitionId,
                StartedAtTick = t.StartedAtTick,
                Progress = t.Progress,
                SpotId = t.SpotId,
            }).ToList(),
            Chronicle = campaign.Chronicle.Entries.Select(e => new ChronicleEntryData
            {
                Sequence = e.Sequence,
                Tick = e.Tick,
                Kind = e.Kind.ToString(),
                Characters = e.Characters.Select(id => id.Value).ToList(),
                Parameters = new SortedDictionary<string, string>(e.Parameters.ToDictionary(), StringComparer.Ordinal),
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

        foreach (var saved in data.Placements)
        {
            RestorePlacement(campaign, saved, content);
        }

        var reservedSpots = new HashSet<string>(StringComparer.Ordinal);
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

            if (saved.SpotId is not null)
            {
                var spot = campaign.Placements.TryGetValue(id, out var where)
                    ? content.Locations[where.LocationId].FindSpot(saved.SpotId)
                    : null;
                if (spot is null || !spot.Tasks.Contains(saved.DefinitionId) || !reservedSpots.Add(saved.SpotId))
                {
                    throw new SaveLoadException($"{worker.Name}'s task uses work spot '{saved.SpotId}', which is missing, wrong for the task, or used twice.");
                }
            }

            campaign.StartTask(new TaskInstance(id, saved.DefinitionId, saved.StartedAtTick) { Progress = saved.Progress, SpotId = saved.SpotId });
        }

        foreach (var saved in data.Chronicle)
        {
            var characters = saved.Characters.Select(n => new CharacterId(n)).ToArray();
            foreach (var id in characters.Where(id => !campaign.Characters.ContainsKey(id)))
            {
                throw new SaveLoadException($"Chronicle entry {saved.Sequence} refers to {id}, who is not in the save.");
            }

            campaign.Chronicle.Restore(new ChronicleEntry(
                saved.Sequence, saved.Tick, ParseEnum<ChronicleEntryKind>(saved.Kind, "chronicle kind"), characters,
                new SortedDictionary<string, string>(saved.Parameters.ToDictionary(), StringComparer.Ordinal)));
        }

        return campaign;
    }

    private static void RestorePlacement(CampaignState campaign, PlacementData saved, GameContent content)
    {
        var id = new CharacterId(saved.CharacterId);
        if (!campaign.Characters.TryGetValue(id, out var character))
        {
            throw new SaveLoadException($"A position is saved for {id}, who is not in the save.");
        }

        if (campaign.Placements.ContainsKey(id))
        {
            throw new SaveLoadException($"{character.Name} ({id}) has more than one saved position.");
        }

        if (!content.Locations.TryGetValue(saved.LocationId, out var location))
        {
            throw new SaveLoadException($"{character.Name} is in '{saved.LocationId}', which no longer exists in the game content.");
        }

        var cell = new GridPosition(saved.X, saved.Y);
        if (!location.IsWalkable(cell))
        {
            throw new SaveLoadException($"{character.Name} stands on {cell}, which is not walkable in {location.Name}.");
        }

        var placement = new CharacterPlacement(saved.LocationId, cell);
        if (saved.Movement is { } walk)
        {
            var path = walk.Path.Select(pair => pair.Length == 2
                ? new GridPosition(pair[0], pair[1])
                : throw new SaveLoadException($"{character.Name}'s walk has a malformed path cell.")).ToList();
            var contiguous = path.Zip(path.Skip(1)).All(step => step.First.ManhattanDistance(step.Second) == 1);
            if (!character.CanWork || path.Count < 2 || path[0] != cell || !contiguous
                || !path.All(location.IsWalkable) || walk.ArriveTick <= walk.StartTick)
            {
                throw new SaveLoadException($"{character.Name}'s walk in progress is invalid.");
            }

            placement.Movement = new Movement(path, walk.StartTick, walk.ArriveTick);
        }

        campaign.SetPlacement(id, placement);
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
