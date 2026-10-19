using System.Text.Json.Nodes;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;

namespace Expedition.Simulation.Tests;

public class IdentityTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    [Fact]
    public void ShippedLanguagesAndStatusesLoad()
    {
        Assert.Contains("language.ahuneri", Shipped.Languages.Keys);
        Assert.Equal(800, Shipped.Statuses["status.sailor"].TypicalMonthlyWage);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void LanguageLevelsOutOfRangeAreRejected(int level)
    {
        var character = new Character(new CharacterId(1), "Inés", 31, "Almara");

        Assert.Throws<ArgumentOutOfRangeException>(() => character.Languages.Set("language.ahuneri", level));
        Assert.Equal(0, character.Languages.Get("language.ahuneri"));
    }

    [Fact]
    public void WomenNeverGetBeards()
    {
        var random = new SimulationRandom(12);
        for (var i = 1; i <= 2000; i++)
        {
            var woman = new Character(new CharacterId(i), $"Woman {i}", 18 + i % 50, "Almara") { Sex = Sex.Female };

            Assert.Equal("beard.none", AppearanceGenerator.Generate(woman, Shipped.Appearance, random).FacialHair);
        }
    }

    [Fact]
    public void MenStillGetBeardsSometimes()
    {
        var random = new SimulationRandom(13);
        var bearded = Enumerable.Range(1, 500)
            .Select(i => new Character(new CharacterId(i), $"Man {i}", 30, "Almara") { Sex = Sex.Male })
            .Count(man => AppearanceGenerator.Generate(man, Shipped.Appearance, random).FacialHair != "beard.none");

        Assert.InRange(bearded, 250, 480);
    }

    [Fact]
    public void ScenarioCrewHaveIdentities()
    {
        var campaign = TestDeckScenario.Create(Shipped).Campaign;

        Assert.All(campaign.Characters.Values, c =>
        {
            Assert.Equal(Sex.Male, c.Sex);
            Assert.NotNull(c.StatusId);
            Assert.NotEmpty(c.Languages.Levels);
        });
        var goncalo = campaign.Characters.Values.Single(c => c.Origin == "Lusara");
        Assert.Equal(5, goncalo.Languages.Get("language.lusaran"));
        Assert.Equal(3, goncalo.Languages.Get("language.castrenan"));
    }

    [Fact]
    public void IdentitySurvivesSaveAndLoad()
    {
        var campaign = TestDeckScenario.Create(Shipped).Campaign;
        var json = SaveSerializer.Serialize(campaign);

        var loaded = SaveSerializer.Deserialize(json, Shipped);

        Assert.Equal(json, SaveSerializer.Serialize(loaded));
    }

    [Fact]
    public void FormatSevenSaveUpgradesWithIdentityUnrecorded()
    {
        var root = JsonNode.Parse(SaveSerializer.Serialize(TestDeckScenario.Create(Shipped).Campaign))!.AsObject();
        root["schemaVersion"] = 7;
        foreach (var character in root["characters"]!.AsArray())
        {
            foreach (var field in new[] { "sex", "languages", "statusId" })
            {
                character!.AsObject().Remove(field);
            }
        }

        var loaded = SaveSerializer.Deserialize(root.ToJsonString(), Shipped);

        Assert.All(loaded.Characters.Values, c =>
        {
            Assert.Equal(Sex.Unrecorded, c.Sex);
            Assert.Empty(c.Languages.Levels);
            Assert.Null(c.StatusId);
        });
    }

    [Theory]
    [InlineData("unknown language")]
    [InlineData("unknown status")]
    [InlineData("bad sex")]
    public void TamperedIdentityIsRejected(string tampering)
    {
        var root = JsonNode.Parse(SaveSerializer.Serialize(TestDeckScenario.Create(Shipped).Campaign))!.AsObject();
        var character = root["characters"]!.AsArray()[0]!;
        switch (tampering)
        {
            case "unknown language": character["languages"]!["language.klingon"] = 2; break;
            case "unknown status": character["statusId"] = "status.pope"; break;
            case "bad sex": character["sex"] = "Kraken"; break;
        }

        Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(root.ToJsonString(), Shipped));
    }

    [Fact]
    public void AppearanceDataWithAnUnknownSexIsRejected()
    {
        var json = """{ "skinTones": [], "hairColours": [], "hairStyles": [], "facialHair": [{ "id": "beard.x", "name": "x", "onlyFor": "Kraken" }], "dyes": [], "marks": [] }""";
        var directory = Directory.CreateTempSubdirectory("expedition-content-").FullName;
        try
        {
            foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "data"), "*.json"))
            {
                File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
            }

            foreach (var folder in new[] { "tasks", "locations" })
            {
                Directory.CreateDirectory(Path.Combine(directory, folder));
                foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "data", folder)))
                {
                    File.Copy(file, Path.Combine(directory, folder, Path.GetFileName(file)));
                }
            }

            File.WriteAllText(Path.Combine(directory, "appearance.json"), json);

            Assert.Throws<ContentValidationException>(() => ContentLoader.LoadFromDirectory(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
