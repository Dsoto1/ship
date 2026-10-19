using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.History;
using Expedition.Simulation.Items;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class SaveLoadTests
{
    private const int Hour = SimulationClock.TicksPerHour;

    /// <summary>
    /// Six sailors in a hold-limited ship work through four hours of storm with real provisions.
    /// Leaves a mix of injured, incapacitated, working and resting crew, active tasks, partly used
    /// stores and a populated chronicle.
    /// </summary>
    private static SimulationRunner StormDamagedVoyage(ulong seed = 11)
    {
        var content = TestContent.Create();
        var campaign = new CampaignState(CampaignPhase.SponsorCommission, seed);
        var runner = new SimulationRunner(campaign, content);
        campaign.TryTransitionTo(CampaignPhase.PortPreparation, "charter accepted");
        campaign.TryTransitionTo(CampaignPhase.Voyage, "sailed at dawn");

        var hold = campaign.CreateInventory(new InventoryId("inv.hold"), "Ship's hold", new InventoryCapacity { MaxWeightKg = 400 });
        campaign.SetSharedStore(hold.Id);
        runner.AddItems(hold.Id, "item.biscuit", 200);
        runner.AddItems(hold.Id, "item.water", 150);
        runner.AddItems(hold.Id, "item.rope", 6);
        runner.AddItems(hold.Id, "item.marlinspike", 1);

        var crew = Enumerable.Range(1, 6)
            .Select(i => campaign.CreateCharacter($"Sailor {i}", 20 + i, "Palos", content.Rules.PersonalCarryCapacity))
            .ToList();
        crew[0].Skills.Set("skill.rigging", 3);
        crew[1].AddTrait("trait.cautious");
        runner.AddItems(crew[2].InventoryId, "item.biscuit", 2);
        campaign.ChangeSurvivalState(crew[5].Id, SurvivalState.Dead, "washed overboard");

        for (var hour = 0; hour < 4; hour++)
        {
            foreach (var sailor in crew.Where(c => c.CanWork && !campaign.ActiveTasks.ContainsKey(c.Id)))
            {
                runner.AssignTask(sailor.Id, sailor.Needs.Fatigue > 50 ? "task.rest" : "task.storm_work");
            }

            runner.Advance(Hour);
        }

        runner.AssignTask(crew[0].Id, "task.repair_rigging");
        runner.Advance(17);
        return runner;
    }

    private static CampaignState RoundTrip(CampaignState campaign) =>
        SaveSerializer.Deserialize(SaveSerializer.Serialize(campaign), TestContent.Create());

    [Fact]
    public void ScenarioActuallyContainsTheStateWeWantToPreserve()
    {
        var campaign = StormDamagedVoyage().Campaign;

        Assert.Contains(campaign.Characters.Values, c => c.Health.Conditions.Count > 0);
        Assert.Contains(campaign.Characters.Values, c => c.SurvivalState == SurvivalState.Dead);
        Assert.NotEmpty(campaign.ActiveTasks);
        Assert.Contains(campaign.ActiveTasks.Values, t => t.Progress > 0);
        Assert.True(campaign.Chronicle.Entries.Count > 5);
    }

    [Fact]
    public void SaveThenLoadProducesEquivalentState()
    {
        var original = StormDamagedVoyage().Campaign;
        var json = SaveSerializer.Serialize(original);

        var loaded = SaveSerializer.Deserialize(json, TestContent.Create());

        // Structural comparison: the loaded campaign serializes to exactly the same document.
        Assert.Equal(json, SaveSerializer.Serialize(loaded));
        Assert.Equal(original.Phase, loaded.Phase);
        Assert.Equal(original.Clock.Tick, loaded.Clock.Tick);
        Assert.Equal(original.SharedStoreId, loaded.SharedStoreId);
        Assert.Equal(400, loaded.SharedStore.Capacity.MaxWeightKg);
        Assert.Equal(original.Randomness.SaveState(), loaded.Randomness.SaveState());
        Assert.Equal(original.CampaignId, loaded.CampaignId);
        Assert.Equal(original.Seed, loaded.Seed);
    }

    [Fact]
    public void LoadedCampaignContinuesExactlyLikeTheOriginal()
    {
        var originalRunner = StormDamagedVoyage();
        var loadedRunner = new SimulationRunner(RoundTrip(originalRunner.Campaign), TestContent.Create());

        foreach (var runner in new[] { originalRunner, loadedRunner })
        {
            for (var hour = 0; hour < 24; hour++)
            {
                foreach (var sailor in runner.Campaign.Characters.Values.OrderBy(c => c.Id.Value)
                             .Where(c => c.CanWork && !runner.Campaign.ActiveTasks.ContainsKey(c.Id)))
                {
                    runner.AssignTask(sailor.Id, sailor.Needs.Fatigue > 50 ? "task.rest" : "task.storm_work");
                }

                runner.Advance(Hour);
            }
        }

        Assert.Equal(SaveSerializer.Serialize(originalRunner.Campaign), SaveSerializer.Serialize(loadedRunner.Campaign));
    }

    [Fact]
    public void DeadCharacterStaysDeadAfterLoading()
    {
        var loaded = RoundTrip(StormDamagedVoyage().Campaign);
        var runner = new SimulationRunner(loaded, TestContent.Create());
        var dead = loaded.Characters.Values.Single(c => c.Name == "Sailor 6");

        Assert.Equal(SurvivalState.Dead, dead.SurvivalState);
        Assert.False(dead.CanWork);
        Assert.False(runner.AssignTask(dead.Id, "task.rest").Succeeded);
        Assert.False(loaded.ChangeSurvivalState(dead.Id, SurvivalState.Alive, "loaded").Succeeded);
    }

    [Fact]
    public void LoadingKeepsIdentitiesAndNeverReusesIds()
    {
        var original = StormDamagedVoyage().Campaign;
        var loaded = RoundTrip(original);

        Assert.Equal(original.Characters.Keys.Order(), loaded.Characters.Keys.Order());
        Assert.Equal(7, loaded.CreateCharacter("Newcomer", 19, "Huelva").Id.Value);
    }

    [Fact]
    public void LoadingDoesNotDuplicateItems()
    {
        var original = StormDamagedVoyage().Campaign;
        var loaded = RoundTrip(original);

        int Total(CampaignState c, string item) => c.Inventories.Values.Sum(i => i.Count(item));
        foreach (var item in new[] { "item.biscuit", "item.water", "item.rope", "item.marlinspike" })
        {
            Assert.Equal(Total(original, item), Total(loaded, item));
        }
    }

    [Fact]
    public void CorruptJsonFailsClearly()
    {
        var ex = Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize("{ \"schemaVersion\": 1, ", TestContent.Create()));

        Assert.Contains("corrupt", ex.Message);
    }

    [Fact]
    public void TruncatedSaveFailsClearly()
    {
        var json = SaveSerializer.Serialize(StormDamagedVoyage().Campaign);

        Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(json[..(json.Length / 2)], TestContent.Create()));
    }

    [Fact]
    public void MissingVersionFailsClearly()
    {
        var ex = Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize("{ \"phase\": \"Voyage\" }", TestContent.Create()));

        Assert.Contains("schemaVersion", ex.Message);
    }

    [Fact]
    public void NewerVersionFailsWithUpgradeAdvice()
    {
        var json = Edit(SaveSerializer.Serialize(StormDamagedVoyage().Campaign), root => root["schemaVersion"] = 99);

        var ex = Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(json, TestContent.Create()));

        Assert.Contains("newer version", ex.Message);
    }

    [Fact]
    public void OlderVersionIsUpgradedThroughMigrations()
    {
        // Pretend this build reads format 9, and format 8 called the phase field "stage".
        var current = SaveSerializer.Serialize(StormDamagedVoyage().Campaign);
        var formatOne = Edit(current, root =>
        {
            root["stage"] = root["phase"]!.DeepClone();
            root.Remove("phase");
        });
        var migrations = new Dictionary<int, Func<JsonObject, JsonObject>>
        {
            [8] = root =>
            {
                root["phase"] = root["stage"]!.DeepClone();
                root.Remove("stage");
                return root;
            },
        };

        var loaded = SaveSerializer.Deserialize(formatOne, TestContent.Create(), migrations, currentVersion: 9);

        Assert.Equal(CampaignPhase.Voyage, loaded.Phase);
        Assert.Equal(current, SaveSerializer.Serialize(loaded));
    }

    [Fact]
    public void OlderVersionWithoutAMigrationFailsClearly()
    {
        var json = SaveSerializer.Serialize(StormDamagedVoyage().Campaign);

        var ex = Assert.Throws<SaveLoadException>(() =>
            SaveSerializer.Deserialize(json, TestContent.Create(), new Dictionary<int, Func<JsonObject, JsonObject>>(), currentVersion: 9));

        Assert.Contains("no upgrade from format 8", ex.Message);
    }

    [Fact]
    public void FormatOneSaveIsUpgradedToTheCurrentFormat()
    {
        // A real format-1 file: no placements, no spot IDs, prose chronicle descriptions.
        var formatOne = Edit(SaveSerializer.Serialize(StormDamagedVoyage().Campaign), root =>
        {
            ToFormat3(root);
            root["schemaVersion"] = 1;
            root.Remove("placements");
            foreach (var task in root["activeTasks"]!.AsArray())
            {
                task!.AsObject().Remove("spotId");
            }

            ToProseChronicle(root);
        });

        var loaded = SaveSerializer.Deserialize(formatOne, TestContent.Create());

        Assert.Empty(loaded.Placements);
        Assert.Contains($"\"schemaVersion\": {SaveSerializer.CurrentSchemaVersion}", SaveSerializer.Serialize(loaded));
        Assert.All(loaded.Chronicle.Entries, e => Assert.StartsWith("Old entry", e.Get(ChronicleParameters.LegacyText)));
    }

    [Fact]
    public void FormatTwoChronicleTextIsKeptAsLegacyText()
    {
        var formatTwo = Edit(SaveSerializer.Serialize(StormDamagedVoyage().Campaign), root =>
        {
            ToFormat3(root);
            root["schemaVersion"] = 2;
            ToProseChronicle(root);
        });

        var loaded = SaveSerializer.Deserialize(formatTwo, TestContent.Create());
        var formatter = new ChronicleFormatter(loaded, TestContent.Create());

        Assert.Equal("Old entry 1", formatter.Format(loaded.Chronicle.Entries[0]));
    }

    /// <summary>
    /// Rewrites a current save the way format 3 stored it: one random generator (the tasks stream),
    /// no seed, campaign ID, captain, attributes or relationships.
    /// </summary>
    internal static void ToFormat3(JsonObject root)
    {
        root["schemaVersion"] = 3;
        root["random"] = root["randomStreams"]!["tasks"]!.DeepClone();
        foreach (var field in new[] { "randomStreams", "seed", "campaignId", "captainId", "relationships", "lastTaskId", "queuedTasks" })
        {
            root.Remove(field);
        }

        foreach (var character in root["characters"]!.AsArray())
        {
            character!.AsObject().Remove("attributes");
            character.AsObject().Remove("appearance");
            foreach (var field in new[] { "sex", "languages", "statusId" })
            {
                character.AsObject().Remove(field);
            }
        }

        foreach (var task in root["activeTasks"]!.AsArray())
        {
            foreach (var field in new[] { "id", "priority", "materialsConsumed" })
            {
                task!.AsObject().Remove(field);
            }
        }
    }

    /// <summary>Rewrites chronicle entries the way formats 1 and 2 stored them: a description, no parameters.</summary>
    private static void ToProseChronicle(JsonObject root)
    {
        foreach (var entry in root["chronicle"]!.AsArray())
        {
            var fields = entry!.AsObject();
            fields.Remove("parameters");
            fields["description"] = $"Old entry {fields["sequence"]}";
        }
    }

    [Fact]
    public void UnknownTaskDefinitionFailsClearly()
    {
        var json = Edit(SaveSerializer.Serialize(StormDamagedVoyage().Campaign),
            root => root["activeTasks"]!.AsArray()[0]!["definitionId"] = "task.removed_in_update");

        var ex = Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(json, TestContent.Create()));

        Assert.Contains("task.removed_in_update", ex.Message);
        Assert.Contains("no longer exists", ex.Message);
    }

    [Fact]
    public void UnknownItemFailsClearly()
    {
        var json = Edit(SaveSerializer.Serialize(StormDamagedVoyage().Campaign),
            root => root["inventories"]!.AsArray()[0]!["items"]!["item.removed"] = 3);

        var ex = Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(json, TestContent.Create()));

        Assert.Contains("item.removed", ex.Message);
    }

    [Fact]
    public void DeadCharacterWithActiveTaskIsRejected()
    {
        var json = Edit(SaveSerializer.Serialize(StormDamagedVoyage().Campaign), root =>
        {
            var id = root["lastTaskId"]!.GetValue<int>() + 1;
            root["lastTaskId"] = id;
            root["activeTasks"]!.AsArray().Add(new JsonObject
            {
                ["id"] = id, ["characterId"] = 6, ["definitionId"] = "task.rest", ["priority"] = "Normal",
                ["startedAtTick"] = 0, ["materialsConsumed"] = true, ["progress"] = 0,
            });
        });

        var ex = Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(json, TestContent.Create()));

        Assert.Contains("Dead", ex.Message);
    }

    [Theory]
    [InlineData("duplicate character")]
    [InlineData("skill out of range")]
    [InlineData("bad survival state")]
    [InlineData("even rng increment")]
    [InlineData("chronicle out of order")]
    [InlineData("missing shared store")]
    [InlineData("null characters")]
    [InlineData("unknown field")]
    public void TamperedSavesAreRejected(string tampering)
    {
        var json = Edit(SaveSerializer.Serialize(StormDamagedVoyage().Campaign), root =>
        {
            var characters = root["characters"]!.AsArray();
            switch (tampering)
            {
                case "duplicate character": characters.Add(characters[0]!.DeepClone()); break;
                case "skill out of range": characters[0]!["skills"]!["skill.rigging"] = 9; break;
                case "bad survival state": characters[0]!["survivalState"] = "Undead"; break;
                case "even rng increment": root["randomStreams"]!["tasks"]!["increment"] = "2"; break;
                case "chronicle out of order": root["chronicle"]!.AsArray()[0]!["sequence"] = 5; break;
                case "missing shared store": root["sharedStoreId"] = "inv.char-0001"; break;
                case "null characters": root["characters"] = null; break;
                case "unknown field": root["cheatMode"] = true; break;
            }
        });

        Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(json, TestContent.Create()));
    }

    [Fact]
    public void SaveStoreWritesReadsAndListsSlots()
    {
        var directory = Path.Combine(Path.GetTempPath(), "expedition-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SaveStore(directory);
            var json = SaveSerializer.Serialize(StormDamagedVoyage().Campaign);

            store.Write("autosave", json);
            store.Write("autosave", json);
            store.Write("before-storm", json);

            Assert.Equal(["autosave", "before-storm"], store.ListSlots());
            Assert.Equal(json, store.Read("autosave"));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
            Assert.Equal(json, SaveSerializer.Serialize(SaveSerializer.Deserialize(store.Read("autosave"), TestContent.Create())));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SaveStoreRejectsUnsafeSlotNamesAndMissingSlots()
    {
        var store = new SaveStore(Path.Combine(Path.GetTempPath(), "expedition-tests-" + Guid.NewGuid().ToString("N")));
        try
        {
            Assert.Throws<ArgumentException>(() => store.Write("../escape", "{}"));
            Assert.Throws<ArgumentException>(() => store.Write("", "{}"));
            Assert.Throws<SaveLoadException>(() => store.Read("nothing-here"));
        }
        finally
        {
            Directory.Delete(store.Directory, recursive: true);
        }
    }

    private static string Edit(string json, Action<JsonObject> change)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        change(root);
        return root.ToJsonString();
    }
}
