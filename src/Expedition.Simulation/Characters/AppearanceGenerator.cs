using Expedition.Simulation.Content;
using Expedition.Simulation.Core;

namespace Expedition.Simulation.Characters;

/// <summary>
/// Generates plausible appearances from the characters random stream, constrained by age and body so
/// combinations stay coherent (ART_BIBLE.md §16): no white-haired youths, and strong people tend to be
/// broader. Every roll comes from <see cref="RandomStreams.Characters"/>, so it never disturbs task or
/// weather outcomes.
/// </summary>
public static class AppearanceGenerator
{
    /// <summary>Creates an appearance for <paramref name="character"/>.</summary>
    /// <exception cref="InvalidOperationException">The content has no appearance options.</exception>
    public static Appearance Generate(Character character, AppearanceOptions options, SimulationRandom random)
    {
        if (!options.CanGenerate)
        {
            throw new InvalidOperationException("The game content has no appearance options to generate from.");
        }

        // Sum of three rolls gives a bell-shaped spread: 147-195 cm, mostly around 171 cm.
        var height = 147 + random.NextInt(17) + random.NextInt(17) + random.NextInt(17);
        var strength = character.Attributes.Get("attribute.strength");
        var build = Pick(random, new (Build, double)[]
        {
            (Build.Slight, strength <= 1 ? 3 : 1),
            (Build.Average, 3),
            (Build.Stocky, strength >= 3 ? 3 : 1),
            (Build.Heavy, strength >= 4 ? 1.5 : 0.5),
        });

        var clothing = PickOption(random, options.Dyes, character);
        return new Appearance
        {
            HeightCm = height,
            Build = build,
            SkinTone = PickOption(random, options.SkinTones, character),
            HairColour = PickOption(random, options.HairColours, character),
            HairStyle = PickOption(random, options.HairStyles, character),
            FacialHair = PickOption(random, options.FacialHair, character),
            ClothingDye = clothing,
            AccentDye = PickOption(random, options.Dyes.Where(d => d.Id != clothing).DefaultIfEmpty(options.Dyes[0]).ToList(), character),
            Mark = PickOption(random, options.Marks, character),
        };
    }

    /// <summary>
    /// Weighted choice among options this character can have: old enough for age-gated options, which
    /// grow likelier for every decade past their minimum age, and matching any sex restriction.
    /// Characters whose sex is unrecorded can have any option.
    /// </summary>
    private static string PickOption(SimulationRandom random, IReadOnlyList<AppearanceOption> options, Character character)
    {
        var age = character.Age;
        var eligible = options
            .Where(o => o.MinAge <= age)
            .Where(o => o.OnlyFor is null || character.Sex == Sex.Unrecorded || o.OnlyFor == character.Sex)
            .Select(o => (o.Id, o.MinAge > 0 ? o.Weight * (1 + (age - o.MinAge) / 10.0) : o.Weight))
            .ToArray();
        return eligible.Length == 0 ? options[0].Id : Pick(random, eligible);
    }

    private static T Pick<T>(SimulationRandom random, (T Value, double Weight)[] choices)
    {
        var total = choices.Sum(c => c.Weight);
        var roll = random.NextDouble() * total;
        foreach (var (value, weight) in choices)
        {
            roll -= weight;
            if (roll < 0)
            {
                return value;
            }
        }

        return choices[^1].Value;
    }
}
