using Expedition.Simulation.Content;
using Expedition.Simulation.Tasks;

namespace Expedition.Simulation.Tests;

public class ContentTests
{
    private const string Skills = """[{ "id": "skill.rigging", "name": "Rigging", "group": "maritime" }]""";

    private const string Rules = """
        {
          "baselineNeedsPerHour": { "hunger": 0.5, "thirst": 1, "fatigue": 1 },
          "workRateBySkillLevel": [0.5, 0.75, 1, 1.25, 1.5, 1.75],
          "workRateByAttributeLevel": [0.6, 0.8, 1, 1.1, 1.2, 1.3],
          "maxFatigueToStartWork": 90, "exhaustionFatigue": 100,
          "tiredFatigue": 75, "tiredWorkRateMultiplier": 0.75, "incapacitatedAtHealth": 25,
          "eatAtHunger": 35, "drinkAtThirst": 35,
          "starvationHealthLossPerHour": 0.5, "dehydrationHealthLossPerHour": 2,
          "walkCellsPerMinute": 60, "personalCarryCapacity": { "maxWeightKg": 25 }
        }
        """;

    private const string Items = """
        [
          { "id": "item.marlinspike", "name": "Marlinspike", "category": "tool", "weightKg": 0.3, "volumeLitres": 0.1 },
          { "id": "item.rope", "name": "Rope", "category": "material", "weightKg": 4, "volumeLitres": 6 }
        ]
        """;

    [Fact]
    public void ShippedDataLoadsAndValidates()
    {
        var content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

        Assert.Contains("task.rest", content.Tasks.Keys);
        Assert.Contains("skill.navigation", content.Skills.Keys);
        Assert.True(content.Tasks["task.rest"].IsRest);
        Assert.Equal(1.0, content.Rules.WorkRateBySkillLevel[2]);
        Assert.Contains(content.Foods, f => f.Id == "item.biscuit");
        Assert.Contains(content.Drinks, d => d.Id == "item.water");
        Assert.Equal(25, content.Rules.PersonalCarryCapacity.MaxWeightKg);
        Assert.Equal(8, content.Locations["location.test_deck"].Spots.Count);
    }

    [Fact]
    public void BadLocationsAreRejected()
    {
        var spotOnWall = TestContent.Deck with
        {
            Id = "location.bad",
            Spots = [new() { Id = "spot.x", X = 0, Y = 2, Tasks = ["task.spot_pump", "task.rest", "task.nope"] }],
        };
        var ragged = TestContent.Deck with { Id = "location.ragged", Rows = ["...", ".."] };
        var oddCharacters = TestContent.Deck with { Id = "location.odd", Rows = ["..~", "..."], Spots = [] };

        var ex = Assert.Throws<ContentValidationException>(() => new GameContent(
            TestContent.Rules, [], TestContent.Items,
            [TestContent.Rest, TestContent.SpotPump], [spotOnWall, ragged, oddCharacters]));

        Assert.Contains(ex.Errors, e => e.Contains("not on a walkable cell"));
        Assert.Contains(ex.Errors, e => e.Contains("does not use work spots"));
        Assert.Contains(ex.Errors, e => e.Contains("unknown task 'task.nope'"));
        Assert.Contains(ex.Errors, e => e.Contains("same length"));
        Assert.Contains(ex.Errors, e => e.Contains("unknown layout characters '~'"));
    }

    [Fact]
    public void TaskReferringToUnknownItemIsRejected()
    {
        var ex = Assert.Throws<ContentValidationException>(() => ContentLoader.LoadFromJson(Rules, Skills, Items, """
            [{ "id": "task.a", "name": "A", "durationMinutes": 10, "requiredTools": ["item.hammer"], "consumes": { "item.nails": 3 } }]
            """));

        Assert.Contains(ex.Errors, e => e.Contains("unknown item 'item.hammer'"));
        Assert.Contains(ex.Errors, e => e.Contains("unknown item 'item.nails'"));
    }

    [Fact]
    public void NegativeItemWeightIsRejected()
    {
        var ex = Assert.Throws<ContentValidationException>(() => ContentLoader.LoadFromJson(Rules, Skills,
            """[{ "id": "item.x", "name": "X", "category": "tool", "weightKg": -1, "volumeLitres": 1 }]""", "[]"));

        Assert.Contains(ex.Errors, e => e.Contains("item.x"));
    }

    [Fact]
    public void TasksParseFromJson()
    {
        var content = ContentLoader.LoadFromJson(Rules, Skills, Items, """
            [{
              "id": "task.repair", "name": "Repair", "durationMinutes": 30, "skill": "skill.rigging", "minSkill": 2,
              "requiredTools": ["item.marlinspike"], "consumes": { "item.rope": 1 },
              "needsPerHour": { "fatigue": 4 },
              "hazard": { "chancePerHour": 0.1, "healthDamage": 5, "condition": "condition.cut" }
            }]
            """, """[{ "id": "condition.cut", "name": "Cut", "kind": "injury" }]""");

        var task = content.Tasks["task.repair"];
        Assert.Equal(2, task.MinSkill);
        Assert.Equal(1, task.Consumes["item.rope"]);
        Assert.Equal(4, task.NeedsPerHour.Fatigue);
        Assert.Equal("condition.cut", task.Hazard!.Condition);
    }

    [Fact]
    public void AllProblemsAreReportedTogether()
    {
        var ex = Assert.Throws<ContentValidationException>(() => ContentLoader.LoadFromJson(Rules, Skills, Items, """
            [
              { "id": "task.a", "name": "A", "durationMinutes": 0 },
              { "id": "task.a", "name": "A again", "durationMinutes": 10 },
              { "id": "task.b", "name": "B", "durationMinutes": 10, "skill": "skill.juggling" },
              { "id": "task.c", "name": "C", "durationMinutes": 10, "skill": "skill.rigging", "minSkill": 6 }
            ]
            """));

        Assert.Contains(ex.Errors, e => e.Contains("Duplicate task ID 'task.a'"));
        Assert.Contains(ex.Errors, e => e.Contains("durationMinutes must be positive"));
        Assert.Contains(ex.Errors, e => e.Contains("unknown skill 'skill.juggling'"));
        Assert.Contains(ex.Errors, e => e.Contains("minSkill must be"));
    }

    [Fact]
    public void BadRulesAreRejected()
    {
        var badRules = TestContent.Rules with { WorkRateBySkillLevel = [1.0, 1.0], TiredWorkRateMultiplier = 0 };

        var ex = Assert.Throws<ContentValidationException>(() => new GameContent(badRules, [], [], []));

        Assert.Contains(ex.Errors, e => e.Contains("workRateBySkillLevel"));
        Assert.Contains(ex.Errors, e => e.Contains("tiredWorkRateMultiplier"));
    }

    [Fact]
    public void MalformedJsonIsReportedAsContentError()
    {
        Assert.Throws<ContentValidationException>(() => ContentLoader.LoadFromJson(Rules, Skills, Items, "[{ not json"));
    }

    [Fact]
    public void MisspelledFieldIsRejectedRatherThanIgnored()
    {
        var ex = Assert.Throws<ContentValidationException>(() => ContentLoader.LoadFromJson(Rules, Skills, Items,
            """[{ "id": "task.a", "name": "A", "durationMinutes": 10, "durationMinuts": 99 }]"""));

        Assert.Contains("durationMinuts", ex.Message);
    }

    [Fact]
    public void MissingRequiredFieldIsRejected()
    {
        Assert.Throws<ContentValidationException>(() => ContentLoader.LoadFromJson(Rules, Skills, Items, """[{ "id": "task.a" }]"""));
    }

    [Fact]
    public void MissingDataDirectoryFileIsReported()
    {
        var ex = Assert.Throws<ContentValidationException>(() => ContentLoader.LoadFromDirectory("/nonexistent"));

        Assert.Contains("simulation_rules.json", ex.Message);
    }

    [Fact]
    public void TaskDefinitionsDefaultToSafeEmptyValues()
    {
        var task = new TaskDefinition { Id = "task.x", Name = "X", DurationMinutes = 1 };

        Assert.Empty(task.RequiredTools);
        Assert.Empty(task.Consumes);
        Assert.Null(task.Hazard);
    }
}
