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
          "maxFatigueToStartWork": 90, "exhaustionFatigue": 100,
          "tiredFatigue": 75, "tiredWorkRateMultiplier": 0.75, "incapacitatedAtHealth": 25
        }
        """;

    [Fact]
    public void ShippedDataLoadsAndValidates()
    {
        var content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

        Assert.Contains("task.rest", content.Tasks.Keys);
        Assert.Contains("skill.navigation", content.Skills.Keys);
        Assert.True(content.Tasks["task.rest"].IsRest);
        Assert.Equal(1.0, content.Rules.WorkRateBySkillLevel[2]);
    }

    [Fact]
    public void TasksParseFromJson()
    {
        var content = ContentLoader.LoadFromJson(Rules, Skills, """
            [{
              "id": "task.repair", "name": "Repair", "durationMinutes": 30, "skill": "skill.rigging", "minSkill": 2,
              "requiredTools": ["item.marlinspike"], "consumes": { "item.rope": 1 },
              "needsPerHour": { "fatigue": 4 },
              "hazard": { "chancePerHour": 0.1, "healthDamage": 5, "condition": "condition.cut" }
            }]
            """);

        var task = content.Tasks["task.repair"];
        Assert.Equal(2, task.MinSkill);
        Assert.Equal(1, task.Consumes["item.rope"]);
        Assert.Equal(4, task.NeedsPerHour.Fatigue);
        Assert.Equal("condition.cut", task.Hazard!.Condition);
    }

    [Fact]
    public void AllProblemsAreReportedTogether()
    {
        var ex = Assert.Throws<ContentValidationException>(() => ContentLoader.LoadFromJson(Rules, Skills, """
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

        var ex = Assert.Throws<ContentValidationException>(() => new GameContent(badRules, [], []));

        Assert.Contains(ex.Errors, e => e.Contains("workRateBySkillLevel"));
        Assert.Contains(ex.Errors, e => e.Contains("tiredWorkRateMultiplier"));
    }

    [Fact]
    public void MalformedJsonIsReportedAsContentError()
    {
        Assert.Throws<ContentValidationException>(() => ContentLoader.LoadFromJson(Rules, Skills, "[{ not json"));
    }

    [Fact]
    public void MisspelledFieldIsRejectedRatherThanIgnored()
    {
        var ex = Assert.Throws<ContentValidationException>(() => ContentLoader.LoadFromJson(Rules, Skills,
            """[{ "id": "task.a", "name": "A", "durationMinutes": 10, "durationMinuts": 99 }]"""));

        Assert.Contains("durationMinuts", ex.Message);
    }

    [Fact]
    public void MissingRequiredFieldIsRejected()
    {
        Assert.Throws<ContentValidationException>(() => ContentLoader.LoadFromJson(Rules, Skills, """[{ "id": "task.a" }]"""));
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
