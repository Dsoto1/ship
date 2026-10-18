using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;

namespace Expedition.Simulation.Campaign;

/// <summary>
/// Root of all persistent campaign state. Owns the single roster of characters and the chronicle.
/// State changes that matter to the story go through methods here so they are validated and recorded.
/// </summary>
public sealed class CampaignState
{
    private readonly Dictionary<CharacterId, Character> _characters = [];

    /// <summary>Creates a campaign in the given starting phase.</summary>
    public CampaignState(CampaignPhase startingPhase = CampaignPhase.SponsorCommission)
    {
        Phase = startingPhase;
    }

    /// <summary>Current campaign phase.</summary>
    public CampaignPhase Phase { get; private set; }

    /// <summary>ID source for this campaign.</summary>
    public IdAllocator Ids { get; } = new();

    /// <summary>History of important events.</summary>
    public Chronicle Chronicle { get; } = new();

    /// <summary>Every character in the campaign, living or dead, keyed by ID.</summary>
    public IReadOnlyDictionary<CharacterId, Character> Characters => _characters;

    /// <summary>Creates a new character with a freshly issued ID and adds it to the campaign.</summary>
    public Character CreateCharacter(string name, int age, string origin)
    {
        var character = new Character(Ids.NextCharacterId(), name, age, origin);
        _characters.Add(character.Id, character);
        return character;
    }

    /// <summary>
    /// Adds an existing character (for example, one restored from a save). The ID is reserved so
    /// it will never be issued again.
    /// </summary>
    /// <exception cref="InvalidOperationException">A character with the same ID is already in the campaign.</exception>
    public void AddCharacter(Character character)
    {
        if (_characters.ContainsKey(character.Id))
        {
            throw new InvalidOperationException($"A character with ID {character.Id} is already in the campaign.");
        }

        Ids.Reserve(character.Id);
        _characters.Add(character.Id, character);
    }

    /// <summary>Returns the character with <paramref name="id"/>.</summary>
    /// <exception cref="KeyNotFoundException">No such character.</exception>
    public Character GetCharacter(CharacterId id) =>
        _characters.TryGetValue(id, out var character)
            ? character
            : throw new KeyNotFoundException($"No character with ID {id} in the campaign.");

    /// <summary>
    /// Moves the campaign to <paramref name="target"/> if <see cref="PhaseTransitions"/> allows it.
    /// Both successful and rejected transitions are recorded in the chronicle.
    /// </summary>
    /// <param name="target">Phase to move to.</param>
    /// <param name="cause">Why the transition is happening, for the chronicle.</param>
    public CommandResult TryTransitionTo(CampaignPhase target, string cause)
    {
        if (!PhaseTransitions.IsAllowed(Phase, target))
        {
            var reason = $"Cannot move from {Phase} to {target}.";
            Chronicle.Record(ChronicleEntryKind.TransitionRejected, $"{reason} Requested because: {cause}");
            return CommandResult.Rejected(reason);
        }

        var previous = Phase;
        Phase = target;
        Chronicle.Record(ChronicleEntryKind.PhaseChanged, $"{previous} -> {target}: {cause}");
        return CommandResult.Ok();
    }

    /// <summary>
    /// Changes a character's survival state and records it. Rejected if the character is dead
    /// (death is final) or already in that state.
    /// </summary>
    /// <param name="id">Character to change.</param>
    /// <param name="newState">State to move to.</param>
    /// <param name="cause">What caused the change, for the chronicle.</param>
    public CommandResult ChangeSurvivalState(CharacterId id, SurvivalState newState, string cause)
    {
        if (!_characters.TryGetValue(id, out var character))
        {
            return CommandResult.Rejected($"No character with ID {id} in the campaign.");
        }

        var previous = character.SurvivalState;
        var result = character.TrySetSurvivalState(newState);
        if (result.Succeeded)
        {
            Chronicle.Record(
                ChronicleEntryKind.SurvivalStateChanged,
                $"{character.Name}: {previous} -> {newState}. Cause: {cause}",
                id);
        }

        return result;
    }
}
