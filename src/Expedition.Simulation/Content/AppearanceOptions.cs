using System.Text.RegularExpressions;

namespace Expedition.Simulation.Content;

/// <summary>
/// The choices appearance is generated from, loaded from <c>data/appearance.json</c>. The current file is a
/// placeholder palette pending period research (ART_BIBLE.md §4.6).
/// </summary>
public sealed record AppearanceOptions
{
    /// <summary>No options; used by content built in code without appearance data.</summary>
    public static AppearanceOptions Empty { get; } = new();

    /// <summary>Skin tones.</summary>
    public IReadOnlyList<AppearanceOption> SkinTones { get; init; } = [];

    /// <summary>Hair colours.</summary>
    public IReadOnlyList<AppearanceOption> HairColours { get; init; } = [];

    /// <summary>Hair styles, including receding and bald.</summary>
    public IReadOnlyList<AppearanceOption> HairStyles { get; init; } = [];

    /// <summary>Facial hair.</summary>
    public IReadOnlyList<AppearanceOption> FacialHair { get; init; } = [];

    /// <summary>Clothing dye colours.</summary>
    public IReadOnlyList<AppearanceOption> Dyes { get; init; } = [];

    /// <summary>Distinguishing marks, including "none".</summary>
    public IReadOnlyList<AppearanceOption> Marks { get; init; } = [];

    /// <summary>True if every option list has at least one entry, so characters can be generated.</summary>
    public bool CanGenerate =>
        SkinTones.Count > 0 && HairColours.Count > 0 && HairStyles.Count > 0 && FacialHair.Count > 0 && Dyes.Count > 0 && Marks.Count > 0;

    /// <summary>Every option list with its name, for validation and lookup.</summary>
    public IEnumerable<(string Name, IReadOnlyList<AppearanceOption> Options)> AllLists =>
    [
        ("skinTones", SkinTones), ("hairColours", HairColours), ("hairStyles", HairStyles),
        ("facialHair", FacialHair), ("dyes", Dyes), ("marks", Marks),
    ];

    /// <summary>The option with <paramref name="id"/> in any list, or null.</summary>
    public AppearanceOption? Find(string id) => AllLists.SelectMany(list => list.Options).FirstOrDefault(o => o.Id == id);
}

/// <summary>One appearance choice.</summary>
public sealed partial record AppearanceOption
{
    /// <summary>Unique ID, for example <c>hair.greying</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Words used in descriptions, for example <c>greying</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Drawing colour as <c>#RRGGBB</c>, for options that have one.</summary>
    public string? Colour { get; init; }

    /// <summary>Youngest age this option can appear at (greying hair, baldness). Older people are likelier to have it.</summary>
    public int MinAge { get; init; }

    /// <summary>If set, only characters of this sex can have the option (for example beards).</summary>
    public Characters.Sex? OnlyFor { get; init; }

    /// <summary>Relative likelihood; 1 is ordinary.</summary>
    public double Weight { get; init; } = 1;

    /// <summary>True if <see cref="Colour"/> is absent or a valid <c>#RRGGBB</c> value.</summary>
    public bool HasValidColour => Colour is null || HexColour().IsMatch(Colour);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColour();
}
