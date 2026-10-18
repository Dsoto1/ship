namespace Expedition.Simulation.Characters;

/// <summary>Overall body shape.</summary>
public enum Build
{
    /// <summary>Lean or slight.</summary>
    Slight,

    /// <summary>Ordinary build.</summary>
    Average,

    /// <summary>Broad and solid.</summary>
    Stocky,

    /// <summary>Heavy-set.</summary>
    Heavy,
}

/// <summary>
/// How a character looks, so the same person is recognisable in every phase and after loading
/// (ART_BIBLE.md §4). The simulation stores option IDs from <c>data/appearance.json</c>; drawing and
/// describing them is the presentation's job. Age cues come from the character's age, not from here.
/// </summary>
public sealed record Appearance
{
    /// <summary>Shortest allowed height.</summary>
    public const int MinHeightCm = 140;

    /// <summary>Tallest allowed height.</summary>
    public const int MaxHeightCm = 210;

    /// <summary>Height in centimetres.</summary>
    public required int HeightCm { get; init; }

    /// <summary>Body shape.</summary>
    public required Build Build { get; init; }

    /// <summary>Skin tone option ID.</summary>
    public required string SkinTone { get; init; }

    /// <summary>Hair colour option ID.</summary>
    public required string HairColour { get; init; }

    /// <summary>Hair style option ID.</summary>
    public required string HairStyle { get; init; }

    /// <summary>Facial hair option ID.</summary>
    public required string FacialHair { get; init; }

    /// <summary>Main clothing colour option ID.</summary>
    public required string ClothingDye { get; init; }

    /// <summary>Second clothing colour option ID (hat, belt, sleeves).</summary>
    public required string AccentDye { get; init; }

    /// <summary>Distinguishing mark option ID.</summary>
    public required string Mark { get; init; }

    /// <summary>Every option ID the appearance uses.</summary>
    public IEnumerable<string> OptionIds => [SkinTone, HairColour, HairStyle, FacialHair, ClothingDye, AccentDye, Mark];
}
