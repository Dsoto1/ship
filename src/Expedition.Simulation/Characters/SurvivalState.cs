namespace Expedition.Simulation.Characters;

/// <summary>Whether a character is present and able to act.</summary>
public enum SurvivalState
{
    /// <summary>Present and able to act.</summary>
    Alive,

    /// <summary>Alive but unable to work or move unaided (unconscious, gravely injured, very ill).</summary>
    Incapacitated,

    /// <summary>Lost at sea, separated or otherwise unaccounted for. May later be found.</summary>
    Missing,

    /// <summary>Dead. This is final: a dead character never returns to any other state.</summary>
    Dead,
}
