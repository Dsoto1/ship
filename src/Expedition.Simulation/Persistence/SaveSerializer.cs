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
    public const int CurrentSchemaVersion = 31;

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

            // Format 21 added credit with sellers. Older ports owe nothing.
            [20] = root =>
            {
                if (root["port"] is JsonObject port)
                {
                    port["owed"] = new JsonArray();
                }

                return root;
            },
            // Format 22 added conversations: what has been talked over with whom.
            [21] = root =>
            {
                if (root["port"] is JsonObject port)
                {
                    port["discussed"] = new JsonArray();
                }

                return root;
            },
            // Format 23 added rounds stood in the tavern.
            [22] = root =>
            {
                if (root["port"] is JsonObject port)
                {
                    port["lastRound"] = -1;
                }

                return root;
            },
            // Format 24 added songs in the tavern.
            // Format 26 added navigation: a random stream for it, and the voyage's position (placed on the
            // route by how far she had sailed, when the save is loaded).
            [25] = root =>
            {
                var seed = ulong.Parse(root["seed"]!.GetValue<string>(), CultureInfo.InvariantCulture);
                var (state, increment) = new SimulationRandom(seed, RandomStreams.StreamNumber(RandomStreams.NavigationName)).SaveState();
                if (root["randomStreams"] is JsonObject streams)
                {
                    streams[RandomStreams.NavigationName] = new JsonObject
                    {
                        ["state"] = state.ToString(CultureInfo.InvariantCulture),
                        ["increment"] = increment.ToString(CultureInfo.InvariantCulture),
                    };
                }

                return root;
            },

            // Format 31 added heavy weather met: the weather foreseen, preparations, leaks.
            [30] = root => root,

            // Format 30 added the watch bill: an older voyage keeps no watches until the captain sets them.
            [29] = root => root,

            // Format 29 added calling at islands: no voyage before it had made for one.
            [28] = root => root,

            // Format 28 added the landfall approach: a voyage that had fetched the coast knows where she is on it.
            [27] = root => root,

            // Format 27 gave the wind a direction: an older voyage takes the prevailing wind, on the port tack.
            [26] = root => root,

            // Format 25 added what heavy weather carries away aloft: nothing yet.
            [24] = root =>
            {
                if (root["voyage"] is JsonObject voyage)
                {
                    voyage["partedLines"] = new JsonArray();
                    voyage["yardSprung"] = false;
                    voyage["mending"] = 0;
                }

                return root;
            },
            [23] = root =>
            {
                if (root["port"] is JsonObject port)
                {
                    port["lastSong"] = -1;
                    port["sung"] = new JsonArray();
                }

                return root;
            },

            // Format 20 added the quality of stores: a random stream for them, what the captain judged of
            // each lot (nothing yet), and which spoilage the crew has found (none yet).
            [19] = root =>
            {
                var seed = ulong.Parse(root["seed"]!.GetValue<string>(), CultureInfo.InvariantCulture);
                var (state, increment) = new SimulationRandom(seed, RandomStreams.StreamNumber(RandomStreams.StoresName)).SaveState();
                if (root["randomStreams"] is JsonObject streams)
                {
                    streams[RandomStreams.StoresName] = new JsonObject
                    {
                        ["state"] = state.ToString(CultureInfo.InvariantCulture),
                        ["increment"] = increment.ToString(CultureInfo.InvariantCulture),
                    };
                }

                if (root["port"] is JsonObject port)
                {
                    port["inspected"] = new JsonArray();
                }

                root["spoilageNoted"] = new JsonArray();
                return root;
            },

            // Format 19 added the port's recognition of the expedition. Older ports start unknown to it.
            [18] = root =>
            {
                if (root["port"] is JsonObject port)
                {
                    port["approached"] = new JsonArray();
                    port["approaching"] = null;
                    port["approachingSince"] = 0;
                    port["claims"] = new JsonArray();
                    port["greeted"] = new JsonArray();
                    port["lastGreeting"] = long.MinValue / 2;
                }

                return root;
            },

            // Format 18 gave the port weather. Older ports start fair, changing at once.
            [17] = root =>
            {
                if (root["port"] is JsonObject port)
                {
                    port["weather"] = null;
                    port["weatherUntil"] = 0;
                }

                return root;
            },

            // Format 17 recorded purchases, for the stores waiting on the quay. Older saves count
            // everything bought as already aboard.
            [16] = root =>
            {
                if (root["port"] is JsonObject port)
                {
                    port["purchases"] = new JsonArray();
                }

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
                    Discussed = port.Discussed.OrderBy(p => p.Who).ThenBy(p => p.Topic, StringComparer.Ordinal)
                        .Select(p => new DiscussedData { Who = p.Who.Value, Topic = p.Topic }).ToList(),
                    Hired = port.Hired.Select(id => id.Value).Order().ToList(),
                    Gone = port.Gone.Select(id => id.Value).Order().ToList(),
                    LateFeesCharged = port.LateFeesCharged,
                    LastRound = port.LastRound,
                    LastSong = port.LastSong,
                    Sung = port.Sung.Select(id => id.Value).Order().ToList(),
                    Warned = port.Warned,
                    Leads = port.Leads.Select(l => new HeardLeadData { Lead = l.LeadId, Tick = l.Tick }).ToList(),
                    Purchases = port.Purchases.Select(p => new PurchaseData { Lot = p.Lot, Tick = p.Tick }).ToList(),
                    Inspected = port.Inspected.OrderBy(p => p.Key).Select(p => new InspectionData { Lot = p.Key, Seen = p.Value }).ToList(),
                    Owed = port.Owed.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new DebtData { Seller = p.Key, Amount = p.Value }).ToList(),
                    Weather = port.WeatherId,
                    WeatherUntil = port.WeatherUntil,
                    Approached = port.Approached.Select(id => id.Value).Order().ToList(),
                    Approaching = port.Approaching?.Value,
                    ApproachingSince = port.ApproachingSince,
                    Claims = port.Claims.OrderBy(c => c.Key).Select(c => new[] { c.Key.Value, c.Value ? 1 : 0 }).ToList(),
                    Greeted = port.Greeted.OrderBy(g => g.Day).ThenBy(g => g.Who).Select(g => new[] { g.Who.Value, g.Day }).ToList(),
                    LastGreeting = port.LastGreeting,
                }
                : null,
            PendingEvent = campaign.PendingEvent is { } pending
                ? new PendingEventData { Event = pending.DefinitionId, Subject = pending.Subject?.Value, Other = pending.Other?.Value, Tick = pending.Tick }
                : null,
            EventHistory = campaign.EventHistory.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new EventRecordData { Event = pair.Key, Count = pair.Value.Count, LastTick = pair.Value.LastTick }).ToList(),
            LastEventTick = campaign.LastEventTick,
            Rationing = campaign.Rationing,
            SpoilageNoted = campaign.SpoilageNoted.Order(StringComparer.Ordinal).ToList(),
            Voyage = campaign.Voyage is { } voyage
                ? new VoyageData
                {
                    ShipName = voyage.ShipName, Destination = voyage.Destination, TotalLeagues = voyage.TotalLeagues,
                    LeaguesSailed = voyage.LeaguesSailed, Hull = voyage.Hull, Bilge = voyage.Bilge, Rigging = voyage.Rigging,
                    Weather = voyage.WeatherId, WeatherUntilTick = voyage.WeatherUntilTick, Speed = voyage.SpeedLeaguesPerHour,
                    WatchKept = voyage.WatchKept, Outcome = voyage.Outcome.ToString(), SailPlan = voyage.SailPlan.ToString(),
                    PartedLines = voyage.PartedLines.ToList(), YardSprung = voyage.YardSprung, Mending = voyage.Mending,
                    Watches = new SortedDictionary<int, string>(voyage.Watches.ToDictionary(pair => pair.Key.Value, pair => pair.Value.ToString())), AllHands = voyage.AllHands, TurnedOut = voyage.TurnedOut,
                    NextWeather = voyage.NextWeatherId, Prepared = voyage.Prepared.Order(StringComparer.Ordinal).ToList(),
                    Leaks = voyage.Leaks.Select(l => new LeakData { Id = l.Id, Where = l.Where, Severity = l.Severity, Repair = l.Repair.ToString(), Tick = l.Tick, RepairedTick = l.RepairedTick }).ToList(),
                    Navigation = voyage.Navigation is { } n
                        ? new NavigationData
                        {
                            True = [n.True.X, n.True.Y], Reckoned = [n.Reckoned.X, n.Reckoned.Y], Drift = [n.Drift.X, n.Drift.Y],
                            LogError = n.LogError, Heading = n.Heading, RunningDown = n.RunningDown, LastSightDay = n.LastSightDay,
                            Navigator = n.Navigator?.Value,
                            Track = n.Track.Select(p => (IReadOnlyList<double>)[p.X, p.Y]).ToList(),
                            Sights = n.Sights.Select(s => new NoonSightData { Day = s.Day, Taker = s.Taker.Value, Y = s.Y, Error = s.Error }).ToList(),
                            WindFrom = n.WindFrom, Tack = n.Tack, BoardHours = n.BoardHours,
                            Approach = n.Sighted || n.NearWarned || n.Soundings.Count > 0
                                ? new ApproachData
                                {
                                    Sighted = n.Sighted, Direction = n.CoastDirection, Identified = n.Identified, Seen = n.Seen, Pending = n.Pending,
                                    PendingHours = n.PendingHours, Opinion = n.Opinion, OpinionBy = n.OpinionBy?.Value, NearWarned = n.NearWarned, RuledOut = n.RuledOut,
                                    Soundings = n.Soundings.Select(d => new SoundingData { Tick = d.Tick, Who = d.Who.Value, At = [d.At.X, d.At.Y], Fathoms = d.Fathoms, Bottom = d.Bottom }).ToList(),
                                }
                                : null,
                            Call = n.Call is { } call
                                ? new IslandCallData { Island = call.Island, Sighted = call.Sighted, SeenAt = call.SeenAt is { } seen ? [seen.X, seen.Y] : null, Anchored = call.Anchored, Searched = call.Searched }
                                : null,
                        }
                        : null,
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

            var port = new Port.PortState { Funds = savedPort.Funds, LateFeesCharged = savedPort.LateFeesCharged, Warned = savedPort.Warned, LastRound = savedPort.LastRound, LastSong = savedPort.LastSong };
            if (savedPort.Weather is { } weather && portRules.Weather?.Find(weather) is null)
            {
                throw new SaveLoadException($"The save's port weather '{weather}' is not in this game.");
            }

            // A port saved before it had weather starts fair and changes on the next minute.
            port.WeatherId = savedPort.Weather ?? portRules.Weather?.Start;
            port.WeatherUntil = savedPort.Weather is null ? data.Tick : savedPort.WeatherUntil;
            if (savedPort.Claims.Any(c => c.Length != 2) || savedPort.Greeted.Any(g => g.Length != 2))
            {
                throw new SaveLoadException("The port's claims or greetings are malformed.");
            }

            foreach (var id in savedPort.Approached) port.MarkApproached(Known(id));
            port.Approaching = savedPort.Approaching is { } walking ? Known(walking) : null;
            port.ApproachingSince = savedPort.ApproachingSince;
            foreach (var claim in savedPort.Claims) port.SettleClaim(Known(claim[0]), claim[1] != 0);
            foreach (var greeted in savedPort.Greeted) port.MarkGreeted(Known(greeted[0]), greeted[1]);
            port.LastGreeting = savedPort.LastGreeting;
            foreach (var (lot, lots) in savedPort.Stock) port.SetStock(lot, lots);
            // Lots added to the port since the save was made start with their full stock.
            for (var lot = 0; lot < portRules.Goods.Count; lot++)
            {
                if (!savedPort.Stock.ContainsKey(lot))
                {
                    port.SetStock(lot, portRules.Goods[lot].Stock);
                }
            }
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
            foreach (var purchase in savedPort.Purchases)
            {
                if (purchase.Lot < 0 || purchase.Lot >= portRules.Goods.Count || purchase.Tick < 0 || purchase.Tick > data.Tick)
                {
                    throw new SaveLoadException($"The save records a purchase of lot {purchase.Lot} this chandler does not sell, or at an impossible time.");
                }

                port.AddPurchase(purchase.Lot, purchase.Tick);
            }

            foreach (var inspection in savedPort.Inspected)
            {
                if (inspection.Lot < 0 || inspection.Lot >= portRules.Goods.Count
                    || !(inspection.Seen == "unsure" || Port.PortRules.Qualities.Contains(inspection.Seen)))
                {
                    throw new SaveLoadException($"The save records a look at lot {inspection.Lot} this port does not sell, or an unknown verdict '{inspection.Seen}'.");
                }

                port.MarkInspected(inspection.Lot, inspection.Seen);
            }

            foreach (var singer in savedPort.Sung)
            {
                port.MarkSung(Known(singer));
            }

            foreach (var talk in savedPort.Discussed)
            {
                var who = Known(talk.Who);
                var person = campaign.Characters[who].PersonId is { } pid ? portRules.People.FirstOrDefault(p => p.Id == pid) : null;
                if (person?.Conversation?.Topics.Any(t => t.Id == talk.Topic) != true || !port.MarkDiscussed(who, talk.Topic))
                {
                    throw new SaveLoadException($"The save says the captain talked over '{talk.Topic}' with character {talk.Who}: they have no such topic, or it is listed twice.");
                }
            }

            foreach (var debt in savedPort.Owed)
            {
                if (debt.Amount <= 0 || portRules.Sellers.All(s => s.Id != debt.Seller) || port.Owed.ContainsKey(debt.Seller))
                {
                    throw new SaveLoadException($"The save says the Garza owes '{debt.Seller}' {debt.Amount}: no such seller, a bad amount, or twice.");
                }

                port.AddDebt(debt.Seller, debt.Amount);
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
        foreach (var item in data.SpoilageNoted)
        {
            if (!content.Items.ContainsKey(item) || !campaign.NoteSpoilage(item))
            {
                throw new SaveLoadException($"The save notes spoilage of '{item}', which this game does not have, or twice.");
            }
        }

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
            if (savedVoyage.PartedLines.Any(l => l < 0 || l >= rules.PartingLines) || savedVoyage.PartedLines.Distinct().Count() != savedVoyage.PartedLines.Count
                || savedVoyage.Mending is < 0 or > 100 || double.IsNaN(savedVoyage.Mending))
            {
                throw new SaveLoadException("The voyage lists a parted line that is not aboard, or invalid mending.");
            }

            voyage.RestoreCarriedAway(savedVoyage.PartedLines, savedVoyage.YardSprung, savedVoyage.Mending);
            foreach (var (who, name) in savedVoyage.Watches)
            {
                if (!campaign.Characters.ContainsKey(new CharacterId(who)) || !Enum.TryParse<Voyage.Watch>(name, out var watch) || watch == Voyage.Watch.None)
                {
                    throw new SaveLoadException($"The watch bill names someone not aboard, or a watch that is not kept: {who} {name}.");
                }

                voyage.SetWatch(new CharacterId(who), watch);
            }

            voyage.AllHands = savedVoyage.AllHands;
            voyage.TurnedOut = savedVoyage.TurnedOut;
            if ((savedVoyage.NextWeather is { } foreseen && rules.Weathers.All(w => w.Id != foreseen))
                || savedVoyage.Prepared.Any(p => !Voyage.Preparations.All.Contains(p))
                || savedVoyage.Leaks.Any(l => !Enum.TryParse<Voyage.LeakRepair>(l.Repair, out _) || l.Severity < 0))
            {
                throw new SaveLoadException("The voyage's weather foreseen, preparations or leaks are invalid.");
            }

            voyage.RestoreStorm(savedVoyage.NextWeather, savedVoyage.Prepared, savedVoyage.Leaks.Select(l => new Voyage.Leak
            {
                Id = l.Id, Where = l.Where, Severity = l.Severity, Repair = Enum.Parse<Voyage.LeakRepair>(l.Repair), Tick = l.Tick, RepairedTick = l.RepairedTick,
            }));
            if (rules.Route is { } route)
            {
                var bearing = route.Bearing * Math.PI / 180;
                var navigation = new Voyage.Navigation(new Voyage.ChartPoint(Math.Sin(bearing) * savedVoyage.TotalLeagues, Math.Cos(bearing) * savedVoyage.TotalLeagues), route.Offing)
                {
                    ClosestToWind = route.ClosestToWind, TackMargin = route.TackMargin, LongestBoard = route.LongestBoard,
                };
                if (savedVoyage.Navigation is { } saved)
                {
                    if (new[] { saved.True, saved.Reckoned, saved.Drift }.Any(p => p.Count != 2 || p.Any(double.IsNaN))
                        || saved.Track.Any(p => p.Count != 2) || saved.LogError is <= 0 or > 2 || saved.Tack is not (1 or -1) || saved.BoardHours < 0
                        || saved.WindFrom is < 0 or >= 360 || (saved.WindFrom is { } wind && double.IsNaN(wind))
                        || (saved.Navigator is { } who && !campaign.Characters.ContainsKey(new CharacterId(who)))
                        || saved.Sights.Any(s => !campaign.Characters.ContainsKey(new CharacterId(s.Taker))))
                    {
                        throw new SaveLoadException("The voyage's navigation is invalid.");
                    }

                    var coast = route.Coast;
                    bool Known(string? id) => id is null || (coast is null ? id == "coast" : coast.Find(id) is not null);
                    if (saved.Approach is { } approach
                        && (approach.Direction is < -1 or > 1 || approach.PendingHours < 0 || !Known(approach.Identified) || !Known(approach.Seen) || !Known(approach.Opinion) || !Known(approach.RuledOut)
                            || (approach.OpinionBy is { } by && !campaign.Characters.ContainsKey(new CharacterId(by)))
                            || approach.Soundings.Any(d => d.At.Count != 2 || !campaign.Characters.ContainsKey(new CharacterId(d.Who)) || d.Fathoms is < 0)))
                    {
                        throw new SaveLoadException("The voyage's approach to the coast is invalid.");
                    }

                    Voyage.ChartPoint Point(IReadOnlyList<double> p) => new(p[0], p[1]);
                    navigation.Restore(Point(saved.True), Point(saved.Reckoned), Point(saved.Drift), saved.LogError, saved.Heading, saved.RunningDown,
                        saved.LastSightDay, saved.Navigator is { } navigator ? new CharacterId(navigator) : null, saved.Track.Select(Point),
                        saved.Sights.Select(s => new Voyage.NoonSight(s.Day, new CharacterId(s.Taker), s.Y, s.Error)), saved.WindFrom ?? route.WindFrom, saved.Tack, saved.BoardHours);
                    if (saved.Call is { } call)
                    {
                        if (!route.Islands.Any(i => i.Id == call.Island) || call.SeenAt is { Count: not 2 } || call.Searched < 0)
                        {
                            throw new SaveLoadException("The voyage's call at an island is invalid.");
                        }

                        navigation.Call = new Voyage.IslandCall
                        {
                            Island = call.Island, Sighted = call.Sighted, Anchored = call.Anchored, Searched = call.Searched,
                            SeenAt = call.SeenAt is { } seen ? new Voyage.ChartPoint(seen[0], seen[1]) : null,
                        };
                    }

                    if (saved.Approach is { } approach2)
                    {
                        navigation.RestoreApproach(approach2.Sighted, approach2.Direction, approach2.Identified, approach2.Seen, approach2.Pending, approach2.PendingHours,
                            approach2.Opinion, approach2.OpinionBy is { } opinionBy ? new CharacterId(opinionBy) : null, approach2.NearWarned,
                            approach2.Soundings.Select(d => new Voyage.Sounding(d.Tick, new CharacterId(d.Who), Point(d.At), d.Fathoms, d.Bottom)), approach2.RuledOut);
                    }
                    else if (saved.Landed)
                    {
                        // A format 26 or 27 voyage on the coast: she knew it, and was running along it for the destination.
                        var north = navigation.True.Y - navigation.Destination.Y;
                        var known = coast is null ? "coast" : coast.InView(north)?.Id ?? coast.Destination;
                        navigation.Reckoned = navigation.True;
                        navigation.RestoreApproach(true, north > 0 ? -1 : 1, known, coast?.InView(north)?.Id, false, 0, null, null, true, [], null);
                    }
                }
                else
                {
                    // An older voyage, reckoned by distance only: put her on the course, as far along it as she had come.
                    var along = Math.Min(savedVoyage.LeaguesSailed, savedVoyage.TotalLeagues);
                    var here = new Voyage.ChartPoint(Math.Sin(bearing) * along, Math.Cos(bearing) * along);
                    navigation.Restore(here, here, new Voyage.ChartPoint(route.Current[0], route.Current[1]), 1, route.Bearing, false, -1, null, [here], [], route.WindFrom, 1, 0);
                }

                voyage.Navigation = navigation;
            }
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
