using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;

namespace Expedition.Simulation.History;

/// <summary>English description of a character's looks, for records and tooltips. Part of the text layer.</summary>
public static class AppearanceText
{
    /// <summary>For example "tall (184 cm), stocky, olive skin, greying hair, receding, full beard; madder red clothing; a scar across one cheek".</summary>
    public static string Describe(Appearance appearance, AppearanceOptions options)
    {
        string Name(string id) => options.Find(id)?.Name ?? id;

        var stature = appearance.HeightCm switch
        {
            < 160 => "short",
            < 178 => "of middling height",
            _ => "tall",
        };
        var hair = appearance.HairStyle is "style.bald"
            ? "bald"
            : $"{Name(appearance.HairColour)} hair, {Name(appearance.HairStyle)}";
        var mark = appearance.Mark is "mark.none" ? "" : $"; {Name(appearance.Mark)}";
        return $"{stature} ({appearance.HeightCm} cm), {appearance.Build.ToString().ToLowerInvariant()}, " +
               $"{Name(appearance.SkinTone)} skin, {hair}, {Name(appearance.FacialHair)}; " +
               $"{Name(appearance.ClothingDye)} and {Name(appearance.AccentDye)} clothing{mark}";
    }
}
