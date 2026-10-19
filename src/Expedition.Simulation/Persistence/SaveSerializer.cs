using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Items;
using Expedition.Simulation.Knowledge;
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
    public const int CurrentSchemaVersion = 16;

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

            // Format 4 split the single random generator into named streams, added the seed, campaign ID
            // and captain, and moved physical attributes out of skills.
            [3] = MigrateFromFormat3,

            // Format 5 added relationships. Earlier campaigns had none.
            [4] = root =>
            {
                root["relationships"] = new JsonArray();
                return root;
            },

            // Format 6 added task IDs, priorities and queues. Existing active tasks get IDs in save order;
            // they were normal-priority work whose materials had already been used.
            [5] = root =>
            {
                var next = 0;
                foreach (var task in root["activeTasks"]?.AsArray() ?? [])
                {
                    if (task is JsonObject fields)
                    {
                        fields["id"] = ++next;
                        fields["priority"] = nameof(TaskPriority.Normal);
                        fields["materialsConsumed"] = true;
                    }
                }

                root["lastTaskId"] = next;
                root["queuedTasks"] = new JsonArray();
                return root;
            },

            // Format 7 added optional appearance. Older characters have none; the game draws a fallback.
            [6] = root => root,

            // Format 8 added sex, languages and social status. Older characters have none recorded.
            [7] = root =>
            {
                foreach (var character in root["characters"]?.AsArray() ?? [])
                {
                    if (character is JsonObject fields)
                    {
                        fields["sex"] = nameof(Sex.Unrecorded);
                        fields["languages"] = new JsonObject();
                    }
                }

                return root;
            },

            // Format 9 added the captain's knowledge. Earlier campaigns knew nothing recorded.
            [8] = root =>
            {
                root["knowledge"] = new JsonArray();
                return root;
            },

            // Format 10 added standing duties and why each task is being done. All earlier work was ordered.
            [9] = root =>
            {
                foreach (var list in new[] { "activeTasks", "queuedTasks" })
                {
                    foreach (var task in root[list]?.AsArray() ?? [])
                    {
                        task!["origin"] = nameof(TaskOrigin.Order);
                    }
                }

                root["duties"] = new JsonArray();
                return root;
            },

            // Format 11 added the voyage, and duties became a priority list instead of a rotation.
            [10] = root =>
            {
                foreach (var duty in root["duties"]?.AsArray() ?? [])
                {
                    duty!.AsObject().Remove("next");
                }

                root["voyage"] = null;
                return root;
            },

            // Format 12 added events and short rations. Nothing had happened yet.
            [11] = root =>
            {
                root["pendingEvent"] = null;
                root["eventHistory"] = new JsonArray();
                root["lastEventTick"] = long.MinValue / 2;
                root["rationing"] = false;
                return root;
            },

            // Format 16 added the journal of leads. Earlier ports had heard none.
            [15] = root =>
            {
                if (root["port"] is JsonObject port)
                {
                    port["leads"] = new JsonArray();
                }

                return root;
            },

            // Format 15 replaced hiring with the living port, and marked everyone crew or local. Hiring
            // in progress from format 14 cannot carry over; the campaign continues without it.
            [14] = root =>
            {
                root.Remove("recruitment");
                root["port"] = null;
                foreach (var character in root["characters"]?.AsArray() ?? [])
                {
                    character!["membership"] = nameof(Characters.Membership.Crew);
                }

                return root;
            },

            // Format 14 added hiring in port. Earlier campaigns had none in progress.
            [13] = root =>
            {
                root["recruitment"] = null;
                return root;
            },

            // Format 13 added the sail plan. Earlier voyages carried sail for the weather.
            [12] = root =>
            {
                if (root["voyage"] is JsonObject voyage)
                {
                    voyage["sailPlan"] = nameof(Voyage.SailPlan.Normal);
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

    // Before format 4 only task hazards used randomness, so the old single generator continues exactly
    // as the tasks stream. The other streams are new; they are derived from the old state so the
    // upgrade is deterministic. Physical "skills" become attributes, keeping their levels.
    private static readonly IReadOnlyDictionary<string, string> PhysicalSkillsToAttributes = new Dictionary<string, string>
    {
        ["skill.strength"] = "attribute.strength",
        ["skill.endurance"] = "attribute.endurance",
        ["skill.coordination"] = "attribute.coordination",
        ["skill.perception"] = "attribute.perception",
    };

    private static JsonObject MigrateFromFormat3(JsonObject root)
    {
        var old = root["random"] as JsonObject ?? throw new SaveLoadException("The format-3 save has no random generator state.");
        root.Remove("random");
        var derivedSeed = ulong.Parse(old["state"]!.GetValue<string>(), CultureInfo.InvariantCulture);
        var streams = new JsonObject();
        foreach (var name in RandomStreams.Names)
        {
            if (name == RandomStreams.TasksName)
            {
                streams[name] = old.DeepClone();
                continue;
            }

            var (state, increment) = new SimulationRandom(derivedSeed, RandomStreams.StreamNumber(name)).SaveState();
            streams[name] = new JsonObject
            {
                ["state"] = state.ToString(CultureInfo.InvariantCulture),
                ["increment"] = increment.ToString(CultureInfo.InvariantCulture),
            };
        }

        root["randomStreams"] = streams;
        root["seed"] = derivedSeed.ToString(CultureInfo.InvariantCulture);
        root["campaignId"] = $"campaign-upgraded-{derivedSeed:x16}";

        foreach (var character in root["characters"]?.AsArray() ?? [])
        {
            if (character is not JsonObject fields)
            {
                continue;
            }

            var attributes = new JsonObject();
            if (fields["skills"] is JsonObject skills)
            {
                foreach (var (skillId, attributeId) in PhysicalSkillsToAttributes)
                {
                    if (skills[skillId] is { } level)
                    {
                        attributes[attributeId] = level.DeepClone();
                        skills.Remove(skillId);
                    }
                }
            }

            fields["attributes"] = attributes;
        }

        return root;
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
        return new SaveData
        {
            SchemaVersion = CurrentSchemaVersion,
            CampaignId = campaign.CampaignId,
            Seed = campaign.Seed.ToString(CultureInfo.InvariantCulture),
            CaptainId = campaign.CaptainId?.Value,
            Phase = campaign.Phase.ToString(),
            Tick = campaign.Clock.Tick,
            LastCharacterId = campaign.Ids.LastCharacterId,
            RandomStreams = new SortedDictionary<string, RandomData>(
                campaign.Randomness.SaveState().ToDictionary(
                    pair => pair.Key,
                    pair => new RandomData
                    {
                        State = pair.Value.State.ToString(CultureInfo.InvariantCulture),
                        Increment = pair.Value.Increment.ToString(CultureInfo.InvariantCulture),
                    }),
                StringComparer.Ordinal),
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
                Attributes = new SortedDictionary<string, int>(c.Attributes.Levels.ToDictionary(), StringComparer.Ordinal),
                Sex = c.Sex.ToString(),
                Languages = new SortedDictionary<string, int>(c.Languages.Levels.ToDictionary(), StringComparer.Ordinal),
                StatusId = c.StatusId,
                Membership = c.Membership.ToString(),
                PersonId = c.PersonId,
                Traits = c.Traits.Order(StringComparer.Ordinal).ToList(),
                Needs = new NeedsData { Hunger = c.Needs.Hunger, Thirst = c.Needs.Thirst, Fatigue = c.Needs.Fatigue, Stress = c.Needs.Stress },
                Health = c.Health.Value,
                Appearance = c.Appearance is { } looks
                    ? new AppearanceData
                    {
                        HeightCm = looks.HeightCm, Build = looks.Build.ToString(), SkinTone = looks.SkinTone,
                        HairColour = looks.HairColour, HairStyle = looks.HairStyle, FacialHair = looks.FacialHair,
                        ClothingDye = looks.ClothingDye, AccentDye = looks.AccentDye, Mark = looks.Mark,
                    }
                    : null,
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
            Relationships = campaign.Relationships.Values
                .OrderBy(r => r.From.Value).ThenBy(r => r.To.Value)
                .Select(r => new RelationshipData
                {
                    From = r.From.Value,
                    To = r.To.Value,
                    Trust = r.Trust,
                    Respect = r.Respect,
                    Affection = r.Affection,
                    History = r.History.Select(h => new RelationshipChangeData
                    {
                        Tick = h.Tick, Dimension = h.Dimension.ToString(), Delta = h.Delta, Cause = h.Cause,
                    }).ToList(),
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
            LastTaskId = campaign.Ids.LastTaskId,
            ActiveTasks = campaign.ActiveTasks.Values.OrderBy(t => t.CharacterId.Value).Select(ToData).ToList(),
            QueuedTasks = campaign.CharactersWithQueues.Order()
                .SelectMany(id => campaign.QueueOf(id))
                .Select(ToData)
                .ToList(),
            Duties = campaign.Duties.OrderBy(pair => pair.Key).Select(pair => new DutyData
            {
                CharacterId = pair.Key.Value, Tasks = pair.Value.Tasks.ToList(),
            }).ToList(),
            Port = campaign.Port is { } port
                ? new PortData
                {
                    Funds = port.Funds,
                    Stock = new SortedDictionary<int, int>(port.Stock.ToDictionary()),
                    TalkedTo = port.TalkedTo.Select(id => id.Value).Order().ToList(),
                    Asked = port.Asked.OrderBy(p => p.Informant).ThenBy(p => p.About).Select(p => new[] { p.Informant.Value, p.About.Value }).ToList(),
                    Hired = port.Hired.Select(id => id.Value).Order().ToList(),
                    Gone = port.Gone.Select(id => id.Value).Order().ToList(),
                    LateFeesCharged = port.LateFeesCharged,
                    Warned = port.Warned,
                    Leads = port.Leads.Select(l => new HeardLeadData { Lead = l.LeadId, Tick = l.Tick }).ToList(),
                }
                : null,
            PendingEvent = campaign.PendingEvent is { } pending
                ? new PendingEventData { Event = pending.DefinitionId, Subject = pending.Subject?.Value, Other = pending.Other?.Value, Tick = pending.Tick }
                : null,
            EventHistory = campaign.EventHistory.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new EventRecordData { Event = pair.Key, Count = pair.Value.Count, LastTick = pair.Value.LastTick }).ToList(),
            LastEventTick = campaign.LastEventTick,
            Rationing = campaign.Rationing,
            Voyage = campaign.Voyage is { } voyage
                ? new VoyageData
                {
                    ShipName = voyage.ShipName, Destination = voyage.Destination, TotalLeagues = voyage.TotalLeagues,
                    LeaguesSailed = voyage.LeaguesSailed, Hull = voyage.Hull, Bilge = voyage.Bilge, Rigging = voyage.Rigging,
                    Weather = voyage.WeatherId, WeatherUntilTick = voyage.WeatherUntilTick, Speed = voyage.SpeedLeaguesPerHour,
                    WatchKept = voyage.WatchKept, Outcome = voyage.Outcome.ToString(), SailPlan = voyage.SailPlan.ToString(),
                }
                : null,
            Knowledge = campaign.Knowledge.Entries.Select(k => new KnowledgeData
            {
                Tick = k.Tick, Subject = k.Subject.Value, Topic = k.Topic, Level = k.Level,
                Source = k.Source.ToString(), Informant = k.Informant?.Value,
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

    private static TaskData ToData(TaskInstance task) => new()
    {
        Id = task.Id.Value,
        CharacterId = task.CharacterId.Value,
        DefinitionId = task.DefinitionId,
        Priority = task.Priority.ToString(),
        Origin = task.Origin.ToString(),
        StartedAtTick = task.StartedAtTick,
        MaterialsConsumed = task.MaterialsConsumed,
        Progress = task.Progress,
        SpotId = task.SpotId,
    };

    /// <summary>Checks the parts of a saved task that do not depend on whether it is active or waiting.</summary>
    private static TaskInstance RestoreTask(TaskData saved, CampaignState campaign, GameContent content, long now, HashSet<int> seenIds)
    {
        var id = new CharacterId(saved.CharacterId);
        if (!campaign.Characters.TryGetValue(id, out var worker))
        {
            throw new SaveLoadException($"A task belongs to {id}, who is not in the save.");
        }

        if (!content.Tasks.ContainsKey(saved.DefinitionId))
        {
            throw new SaveLoadException($"{worker.Name} has '{saved.DefinitionId}' to do, which no longer exists in the game content.");
        }

        if (saved.Id <= 0 || saved.Id > campaign.Ids.LastTaskId || !seenIds.Add(saved.Id))
        {
            throw new SaveLoadException($"{worker.Name}'s task {saved.Id} has an invalid or duplicate ID.");
        }

        if (saved.Progress < 0 || saved.StartedAtTick < 0 || saved.StartedAtTick > now || (saved.StartedAtTick is null && saved.Progress > 0))
        {
            throw new SaveLoadException($"{worker.Name}'s task '{saved.DefinitionId}' has invalid progress or start time.");
        }

        return new TaskInstance(new TaskId(saved.Id), id, saved.DefinitionId, ParseEnum<TaskPriority>(saved.Priority, "task priority"))
        {
            Origin = ParseEnum<TaskOrigin>(saved.Origin, "task origin"),
            StartedAtTick = saved.StartedAtTick,
            MaterialsConsumed = saved.MaterialsConsumed,
            Progress = saved.Progress,
        };
    }

    private static CampaignState FromData(SaveData data, GameContent content)
    {
        if (data.Tick < 0)
        {
            throw new SaveLoadException($"The save has a negative tick ({data.Tick}).");
        }

        var randomness = RandomStreams.FromState(data.RandomStreams.ToDictionary(
            pair => pair.Key,
            pair => (ulong.Parse(pair.Value.State, CultureInfo.InvariantCulture), ulong.Parse(pair.Value.Increment, CultureInfo.InvariantCulture))));
        var campaign = CampaignState.CreateForRestore(
            ParseEnum<CampaignPhase>(data.Phase, "phase"),
            ulong.Parse(data.Seed, CultureInfo.InvariantCulture),
            data.CampaignId,
            randomness,
            data.Tick);

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
            var character = RestoreCharacter(saved, content);
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
        if (data.CaptainId is { } captainNumber)
        {
            var captain = new CharacterId(captainNumber);
            if (!campaign.Characters.ContainsKey(captain))
            {
                throw new SaveLoadException($"The captain ({captain}) is not among the saved characters.");
            }

            campaign.RestoreCaptain(captain);
        }
        campaign.FoodShortageRecorded = data.FoodShortageRecorded;
        campaign.WaterShortageRecorded = data.WaterShortageRecorded;

        foreach (var saved in data.Placements)
        {
            RestorePlacement(campaign, saved, content);
        }

        foreach (var saved in data.Relationships)
        {
            var from = new CharacterId(saved.From);
            var to = new CharacterId(saved.To);
            if (!campaign.Characters.ContainsKey(from) || !campaign.Characters.ContainsKey(to) || from == to)
            {
                throw new SaveLoadException($"A relationship between {from} and {to} refers to a missing character or to one person twice.");
            }

            if (campaign.GetRelationship(from, to) is not null)
            {
                throw new SaveLoadException($"The relationship from {from} to {to} is saved twice.");
            }

            var relationship = new Relationship(from, to);
            relationship.Restore(saved.Trust, saved.Respect, saved.Affection, saved.History.Select(h =>
                new RelationshipChange(h.Tick, ParseEnum<RelationshipDimension>(h.Dimension, "relationship dimension"), h.Delta, h.Cause)));
            campaign.RestoreRelationship(relationship);
        }

        if (data.LastTaskId < 0)
        {
            throw new SaveLoadException($"lastTaskId {data.LastTaskId} is invalid.");
        }

        campaign.Ids.ReserveTaskId(data.LastTaskId);
        var taskIds = new HashSet<int>();
        var reservedSpots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var saved in data.ActiveTasks)
        {
            var task = RestoreTask(saved, campaign, content, data.Tick, taskIds);
            var worker = campaign.Characters[task.CharacterId];
            if (!worker.CanWork)
            {
                throw new SaveLoadException($"{worker.Name} ({task.CharacterId}) is {worker.SurvivalState} but has an active task.");
            }

            if (campaign.ActiveTasks.ContainsKey(task.CharacterId))
            {
                throw new SaveLoadException($"{worker.Name} ({task.CharacterId}) has more than one active task.");
            }

            if (task.StartedAtTick is null)
            {
                throw new SaveLoadException($"{worker.Name}'s active task '{saved.DefinitionId}' has no start time.");
            }

            if (saved.SpotId is not null)
            {
                var spot = campaign.Placements.TryGetValue(task.CharacterId, out var where)
                    ? content.Locations[where.LocationId].FindSpot(saved.SpotId)
                    : null;
                if (spot is null || !spot.Tasks.Contains(saved.DefinitionId) || !reservedSpots.Add(saved.SpotId))
                {
                    throw new SaveLoadException($"{worker.Name}'s task uses work spot '{saved.SpotId}', which is missing, wrong for the task, or used twice.");
                }

                task.SpotId = saved.SpotId;
            }

            campaign.StartTask(task);
        }

        foreach (var saved in data.QueuedTasks)
        {
            var task = RestoreTask(saved, campaign, content, data.Tick, taskIds);
            var worker = campaign.Characters[task.CharacterId];
            if (worker.SurvivalState is SurvivalState.Dead or SurvivalState.Missing)
            {
                throw new SaveLoadException($"{worker.Name} is {worker.SurvivalState} but has orders waiting.");
            }

            if (saved.SpotId is not null)
            {
                throw new SaveLoadException($"{worker.Name}'s waiting task '{saved.DefinitionId}' holds a work spot.");
            }

            // Saved in queue order, so appending each one rebuilds the queue exactly.
            campaign.Enqueue(task, aheadOfSamePriority: false);
            var queue = campaign.QueueOf(task.CharacterId);
            if (queue[^1] != task)
            {
                throw new SaveLoadException($"{worker.Name}'s waiting orders are out of priority order.");
            }
        }

        foreach (var saved in data.Duties)
        {
            var id = new CharacterId(saved.CharacterId);
            if (!campaign.Characters.TryGetValue(id, out var worker) || worker.SurvivalState is SurvivalState.Dead or SurvivalState.Missing)
            {
                throw new SaveLoadException($"Duties are saved for {id}, who is not in the save or cannot work.");
            }

            if (campaign.Duties.ContainsKey(id) || saved.Tasks.Count == 0 || saved.Tasks.Distinct().Count() != saved.Tasks.Count
                || saved.Tasks.Any(task => !content.Tasks.TryGetValue(task, out var definition) || definition.IsRest))
            {
                throw new SaveLoadException($"{worker.Name}'s duties are invalid or refer to tasks that no longer exist.");
            }

            campaign.SetDuties(id, saved.Tasks.ToList());
        }

        if (data.Port is { } savedPort)
        {
            CharacterId Known(int number) => campaign.Characters.ContainsKey(new CharacterId(number))
                ? new CharacterId(number)
                : throw new SaveLoadException($"The port refers to character {number}, who is not in the save.");
            if (content.Port is not { } portRules || savedPort.Stock.Any(s => s.Key < 0 || s.Key >= portRules.Goods.Count || s.Value < 0)
                || savedPort.LateFeesCharged < 0 || savedPort.Asked.Any(pair => pair.Length != 2))
            {
                throw new SaveLoadException("The port preparations have invalid stock, fees or inquiries, or the content has no port.");
            }

            var port = new Port.PortState { Funds = savedPort.Funds, LateFeesCharged = savedPort.LateFeesCharged, Warned = savedPort.Warned };
            foreach (var (lot, lots) in savedPort.Stock) port.SetStock(lot, lots);
            foreach (var id in savedPort.TalkedTo) port.MarkTalked(Known(id));
            foreach (var pair in savedPort.Asked) port.MarkAsked(Known(pair[0]), Known(pair[1]));
            foreach (var id in savedPort.Hired) port.MarkHired(Known(id));
            foreach (var id in savedPort.Gone) port.MarkGone(Known(id));
            var leadIds = portRules.AllLeads().Select(l => l.Lead.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var lead in savedPort.Leads)
            {
                if (!leadIds.Contains(lead.Lead) || lead.Tick < 0 || lead.Tick > data.Tick || !port.AddLead(lead.Lead, lead.Tick))
                {
                    throw new SaveLoadException($"The save's journal names a lead '{lead.Lead}' this game does not have, or twice.");
                }
            }
            campaign.SetPort(port);
        }

        Events.PendingEvent? pendingEvent = null;
        if (data.PendingEvent is { } savedEvent)
        {
            CharacterId? Person(int? number) => number is { } n ? new CharacterId(n) : null;
            var involved = new[] { Person(savedEvent.Subject), Person(savedEvent.Other) };
            if (!content.Events.ContainsKey(savedEvent.Event) || savedEvent.Tick < 0 || savedEvent.Tick > data.Tick
                || involved.Any(id => id is { } who && !campaign.Characters.ContainsKey(who)))
            {
                throw new SaveLoadException($"The waiting event '{savedEvent.Event}' is unknown or refers to someone not in the save.");
            }

            pendingEvent = new Events.PendingEvent(savedEvent.Event, involved[0], involved[1], savedEvent.Tick);
        }

        if (data.EventHistory.Any(e => e.Count <= 0 || e.LastTick > data.Tick) || data.EventHistory.Select(e => e.Event).Distinct().Count() != data.EventHistory.Count)
        {
            throw new SaveLoadException("The event history has an invalid or repeated entry.");
        }

        campaign.RestoreEvents(pendingEvent,
            data.EventHistory.Select(e => new KeyValuePair<string, Events.EventRecord>(e.Event, new Events.EventRecord(e.Count, e.LastTick))),
            data.LastEventTick);
        campaign.Rationing = data.Rationing;

        if (data.Voyage is { } savedVoyage)
        {
            var values = new[] { savedVoyage.Hull, savedVoyage.Bilge, savedVoyage.Rigging };
            if (content.Voyage is not { } rules || rules.Weathers.All(w => w.Id != savedVoyage.Weather))
            {
                throw new SaveLoadException($"The voyage's weather '{savedVoyage.Weather}' is not in the game content.");
            }

            if (values.Any(v => v is < 0 or > 100 || double.IsNaN(v)) || savedVoyage.TotalLeagues <= 0 || savedVoyage.LeaguesSailed < 0
                || savedVoyage.Speed < 0 || string.IsNullOrWhiteSpace(savedVoyage.ShipName))
            {
                throw new SaveLoadException("The voyage has an invalid hull, bilge, rigging, distance or speed.");
            }

            var voyage = new Voyage.VoyageState(savedVoyage.ShipName, savedVoyage.Destination, savedVoyage.TotalLeagues, savedVoyage.Weather, savedVoyage.WeatherUntilTick);
            voyage.Restore(savedVoyage.LeaguesSailed, savedVoyage.Hull, savedVoyage.Bilge, savedVoyage.Rigging, savedVoyage.Speed,
                savedVoyage.WatchKept, ParseEnum<Voyage.VoyageOutcome>(savedVoyage.Outcome, "voyage outcome"));
            voyage.SailPlan = ParseEnum<Voyage.SailPlan>(savedVoyage.SailPlan, "sail plan");
            campaign.SetVoyage(voyage);
        }

        foreach (var saved in data.Knowledge)
        {
            var subject = new CharacterId(saved.Subject);
            CharacterId? informant = saved.Informant is { } number ? new CharacterId(number) : null;
            if (!campaign.Characters.ContainsKey(subject) || (informant is { } who && !campaign.Characters.ContainsKey(who)))
            {
                throw new SaveLoadException($"Knowledge about {subject} refers to someone who is not in the save.");
            }

            if (saved.Level is < 0 or > 5 || saved.Tick < 0 || saved.Tick > data.Tick || string.IsNullOrWhiteSpace(saved.Topic))
            {
                throw new SaveLoadException($"Knowledge about {subject} ('{saved.Topic}') has an invalid level, time or topic.");
            }

            campaign.Knowledge.Restore(new KnowledgeEntry(
                saved.Tick, subject, saved.Topic, saved.Level, ParseEnum<KnowledgeSource>(saved.Source, "knowledge source"), informant));
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

    private static Character RestoreCharacter(CharacterData saved, GameContent content)
    {
        if (saved.Id <= 0)
        {
            throw new SaveLoadException($"{saved.Name} has an invalid ID ({saved.Id}).");
        }

        // The constructor and setters validate names and ranges exactly as during play.
        var character = new Character(new CharacterId(saved.Id), saved.Name, saved.Age, saved.Origin);

        foreach (var (skillId, level) in saved.Skills)
        {
            if (!content.Skills.ContainsKey(skillId))
            {
                throw new SaveLoadException($"{saved.Name} has unknown skill '{skillId}'. The game content may have changed since this save was made.");
            }

            character.Skills.Set(skillId, level);
        }

        foreach (var (attributeId, level) in saved.Attributes)
        {
            if (!content.Attributes.ContainsKey(attributeId))
            {
                throw new SaveLoadException($"{saved.Name} has unknown attribute '{attributeId}'. The game content may have changed since this save was made.");
            }

            character.Attributes.Set(attributeId, level);
        }

        foreach (var trait in saved.Traits)
        {
            if (!content.Traits.ContainsKey(trait))
            {
                throw new SaveLoadException($"{saved.Name} has unknown trait '{trait}'. The game content may have changed since this save was made.");
            }

            character.AddTrait(trait);
        }

        character.Needs.Hunger = saved.Needs.Hunger;
        character.Needs.Thirst = saved.Needs.Thirst;
        character.Needs.Fatigue = saved.Needs.Fatigue;
        character.Needs.Stress = saved.Needs.Stress;
        character.Health.Value = saved.Health;
        foreach (var condition in saved.Conditions)
        {
            if (!content.Conditions.ContainsKey(condition))
            {
                throw new SaveLoadException($"{saved.Name} has unknown condition '{condition}'. The game content may have changed since this save was made.");
            }

            character.Health.AddCondition(condition);
        }

        character.Sex = ParseEnum<Sex>(saved.Sex, "sex");
        foreach (var (languageId, level) in saved.Languages)
        {
            if (!content.Languages.ContainsKey(languageId))
            {
                throw new SaveLoadException($"{saved.Name} speaks unknown language '{languageId}'. The game content may have changed since this save was made.");
            }

            character.Languages.Set(languageId, level);
        }

        if (saved.StatusId is not null && !content.Statuses.ContainsKey(saved.StatusId))
        {
            throw new SaveLoadException($"{saved.Name} has unknown status '{saved.StatusId}'.");
        }

        character.StatusId = saved.StatusId;
        character.Membership = ParseEnum<Membership>(saved.Membership, "membership");
        character.PersonId = saved.PersonId;

        if (saved.Appearance is { } looks)
        {
            var appearance = new Appearance
            {
                HeightCm = looks.HeightCm, Build = ParseEnum<Build>(looks.Build, "build"), SkinTone = looks.SkinTone,
                HairColour = looks.HairColour, HairStyle = looks.HairStyle, FacialHair = looks.FacialHair,
                ClothingDye = looks.ClothingDye, AccentDye = looks.AccentDye, Mark = looks.Mark,
            };
            if (appearance.HeightCm is < Appearance.MinHeightCm or > Appearance.MaxHeightCm)
            {
                throw new SaveLoadException($"{saved.Name} has an impossible height ({appearance.HeightCm} cm).");
            }

            foreach (var optionId in appearance.OptionIds.Where(id => content.Appearance.Find(id) is null))
            {
                throw new SaveLoadException($"{saved.Name}'s appearance uses unknown option '{optionId}'. The game content may have changed since this save was made.");
            }

            character.Appearance = appearance;
        }

        character.RestoreSurvivalState(ParseEnum<SurvivalState>(saved.SurvivalState, "survival state"));
        return character;
    }

    private static T ParseEnum<T>(string value, string what) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: false, out var result) && Enum.IsDefined(result)
            ? result
            : throw new SaveLoadException($"Unknown {what} '{value}' in save file.");
}
