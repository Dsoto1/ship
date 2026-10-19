using System.Text.Json.Nodes;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;

namespace Expedition.Simulation.Tests;

public class AppearanceTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static List<(Character Person, Appearance Looks)> Population(int count, ulong seed, int? age = null, int? strength = null)
    {
        var random = new SimulationRandom(seed);
        var people = new List<(Character, Appearance)>();
        for (var i = 1; i <= count; i++)
        {
            var person = new Character(new CharacterId(i), $"Sailor {i}", age ?? 16 + i % 50, "Palos");
            if (strength is { } level)
            {
                person.Attributes.Set("attribute.strength", level);
            }

            people.Add((person, AppearanceGenerator.Generate(person, Shipped.Appearance, random)));
        }

        return people;
    }

    [Fact]
    public void ShippedAppearanceOptionsLoad()
    {
        Assert.True(Shipped.Appearance.CanGenerate);
        Assert.Equal(6, Shipped.Appearance.AllLists.Count());
        Assert.Equal("greying", Shipped.Appearance.Find("hair.greying")!.Name);
    }

    [Fact]
    public void SameSeedGivesTheSameFaces()
    {
        Assert.Equal(Population(50, seed: 8).Select(p => p.Looks), Population(50, seed: 8).Select(p => p.Looks));
        Assert.NotEqual(Population(50, seed: 8).Select(p => p.Looks), Population(50, seed: 9).Select(p => p.Looks));
    }

    [Fact]
    public void AgeLimitsAreRespected()
    {
        foreach (var (person, looks) in Population(3000, seed: 1))
        {
            if (person.Age < 35)
            {
                Assert.NotEqual("hair.greying", looks.HairColour);
                Assert.NotEqual("style.bald", looks.HairStyle);
            }

            if (person.Age < 50)
            {
                Assert.NotEqual("hair.white", looks.HairColour);
            }

            if (person.Age < 28)
            {
                Assert.NotEqual("style.receding", looks.HairStyle);
            }
        }
    }

    [Fact]
    public void OlderPeopleGreyMoreOften()
    {
        int Grey(int age) => Population(2000, seed: 2, age: age).Count(p => p.Looks.HairColour is "hair.greying" or "hair.white");

        Assert.True(Grey(65) > Grey(40));
    }

    [Fact]
    public void StrongPeopleTendToBeBroader()
    {
        int Broad(int strength) => Population(2000, seed: 3, strength: strength).Count(p => p.Looks.Build is Build.Stocky or Build.Heavy);

        Assert.True(Broad(4) > Broad(1) * 2);
    }

    [Fact]
    public void HeightsAreHumanAndVaried()
    {
        var heights = Population(2000, seed: 4).Select(p => p.Looks.HeightCm).ToList();

        Assert.All(heights, h => Assert.InRange(h, 147, 195));
        Assert.InRange(heights.Average(), 166, 176);
        Assert.True(heights.Distinct().Count() > 30);
    }

    [Fact]
    public void PopulationUsesTheWholePalette()
    {
        var people = Population(500, seed: 5);

        Assert.Equal(Shipped.Appearance.SkinTones.Count, people.Select(p => p.Looks.SkinTone).Distinct().Count());
        Assert.Equal(Shipped.Appearance.Dyes.Count, people.Select(p => p.Looks.ClothingDye).Distinct().Count());
        Assert.All(people, p => Assert.NotEqual(p.Looks.ClothingDye, p.Looks.AccentDye));
    }

    [Fact]
    public void GeneratingFacesDoesNotDisturbOtherRandomness()
    {
        var a = TestDeckScenario.Create(Shipped, seed: 6).Campaign.Randomness;
        var b = new RandomStreams(6);

        // The scenario generated six faces from the characters stream; the task stream is untouched.
        Assert.Equal(b.Tasks.SaveState(), a.Tasks.SaveState());
        Assert.NotEqual(b.Characters.SaveState(), a.Characters.SaveState());
    }

    [Fact]
    public void DescriptionReadsNaturally()
    {
        var looks = new Appearance
        {
            HeightCm = 184, Build = Build.Stocky, SkinTone = "skin.olive", HairColour = "hair.greying",
            HairStyle = "style.receding", FacialHair = "beard.full", ClothingDye = "dye.madder",
            AccentDye = "dye.black", Mark = "mark.scar_cheek",
        };

        Assert.Equal(
            "tall (184 cm), stocky, olive skin, greying hair, receding, full beard; madder red and black clothing; a scar across one cheek",
            AppearanceText.Describe(looks, Shipped.Appearance));
        Assert.Contains("bald,", AppearanceText.Describe(looks with { HairStyle = "style.bald" }, Shipped.Appearance));
    }

    [Fact]
    public void AppearanceSurvivesSaveAndLoad()
    {
        var campaign = TestDeckScenario.Create(Shipped).Campaign;
        var json = SaveSerializer.Serialize(campaign);

        var loaded = SaveSerializer.Deserialize(json, Shipped);

        Assert.Equal(json, SaveSerializer.Serialize(loaded));
        Assert.All(loaded.Characters.Values, c => Assert.Equal(campaign.GetCharacter(c.Id).Appearance, c.Appearance));
    }

    [Fact]
    public void FormatSixSaveLoadsWithoutAppearance()
    {
        var root = JsonNode.Parse(SaveSerializer.Serialize(TestDeckScenario.Create(Shipped).Campaign))!.AsObject();
        root["schemaVersion"] = 6;
        root.Remove("knowledge");
        foreach (var character in root["characters"]!.AsArray())
        {
            foreach (var field in new[] { "appearance", "sex", "languages", "statusId" })
            {
                character!.AsObject().Remove(field);
            }
        }

        var loaded = SaveSerializer.Deserialize(root.ToJsonString(), Shipped);

        Assert.All(loaded.Characters.Values, c => Assert.Null(c.Appearance));
    }

    [Theory]
    [InlineData("unknown option")]
    [InlineData("impossible height")]
    [InlineData("bad build")]
    public void TamperedAppearanceIsRejected(string tampering)
    {
        var root = JsonNode.Parse(SaveSerializer.Serialize(TestDeckScenario.Create(Shipped).Campaign))!.AsObject();
        var looks = root["characters"]!.AsArray()[0]!["appearance"]!;
        switch (tampering)
        {
            case "unknown option": looks["hairColour"] = "hair.blue"; break;
            case "impossible height": looks["heightCm"] = 260; break;
            case "bad build": looks["build"] = "Gigantic"; break;
        }

        Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(root.ToJsonString(), Shipped));
    }

    [Fact]
    public void BadAppearanceContentIsRejected()
    {
        var bad = new AppearanceOptions
        {
            SkinTones = [new AppearanceOption { Id = "skin.a", Name = "a", Colour = "orange" }],
            HairColours = [new AppearanceOption { Id = "skin.a", Name = "duplicate" }],
            Marks = [new AppearanceOption { Id = "mark.x", Name = "x", Weight = 0 }],
        };

        var ex = Assert.Throws<ContentValidationException>(() => new GameContent(TestContent.Rules, [], [], [], appearance: bad));

        Assert.Contains(ex.Errors, e => e.Contains("use #RRGGBB"));
        Assert.Contains(ex.Errors, e => e.Contains("duplicate option ID 'skin.a'"));
        Assert.Contains(ex.Errors, e => e.Contains("positive weight"));
    }
}
