using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Inventory;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Campaign;

/// <summary>
/// Root of all persistent campaign state. Owns the single roster of characters, the clock, the random
/// generator, active tasks, supplies and the chronicle. State changes that matter to the story go
/// through methods here or through <see cref="SimulationRunner"/> so they are validated and recorded.
/// </summary>
public sealed class CampaignState
{
    /// <summary>Seed used when none is given. Tests and scenarios should pass their own.</summary>
    public const ulong DefaultSeed = 0x5EA_F00D;

    private readonly Dictionary<CharacterId, Character> _characters = [];
    private readonly Dictionary<CharacterId, TaskInstance> _activeTasks = [];

    /// <summary>Creates a campaign in the given starting phase with a seeded random generator.</summary>
    public CampaignState(CampaignPhase startingPhase = CampaignPhase.SponsorCommission, ulong seed = DefaultSeed)
    {
        Phase = startingPhase;
        Random = new SimulationRandom(seed);
    }

    /// <summary>Current campaign phase.</summary>
    public CampaignPhase Phase { get; private set; }

    /// <summary>ID source for this campaign.</summary>
    public IdAllocator Ids { get; } = new();

    /// <summary>Campaign time.</summary>
    public SimulationClock Clock { get; } = new();

    /// <summary>The only source of gameplay randomness. Its state is saved with the campaign.</summary>
    public SimulationRandom Random { get; }

    /// <summary>Shared supplies that tasks draw tools and materials from. Replaced by real inventories in Ticket 3.</summary>
    public Stockpile Supplies { get; } = new();

    /// <summary>History of important events.</summary>
    public Chronicle Chronicle { get; } = new();

    /// <summary>Every character in the campaign, living or dead, keyed by ID.</summary>
    public IReadOnlyDictionary<CharacterId, Character> Characters => _characters;

    /// <summary>The task each busy character is doing. Characters without an entry are idle.</summary>
    public IReadOnlyDictionary<CharacterId, TaskInstance> ActiveTasks => _activeTasks;

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
            Record(ChronicleEntryKind.TransitionRejected, $"{reason} Requested because: {cause}");
            return CommandResult.Rejected(reason);
        }

        var previous = Phase;
        Phase = target;
        Record(ChronicleEntryKind.PhaseChanged, $"{previous} -> {target}: {cause}");
        return CommandResult.Ok();
    }

    /// <summary>
    /// Changes a character's survival state and records it. Rejected if the character is dead
    /// (death is final) or already in that state. A character who can no longer work stops their
    /// current task, and the interruption is recorded.
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
        if (!result.Succeeded)
        {
            return result;
        }

        Record(ChronicleEntryKind.SurvivalStateChanged, $"{character.Name}: {previous} -> {newState}. Cause: {cause}", id);
        if (!character.CanWork)
        {
            InterruptTask(id, $"{character.Name} is {newState}: {cause}");
        }

        return result;
    }

    /// <summary>Appends a chronicle entry stamped with the current tick.</summary>
    internal void Record(ChronicleEntryKind kind, string description, params CharacterId[] characters) =>
        Chronicle.Record(Clock.Tick, kind, description, characters);

    /// <summary>Starts a task. Eligibility must already have been checked by <see cref="SimulationRunner"/>.</summary>
    internal void StartTask(TaskInstance task) => _activeTasks.Add(task.CharacterId, task);

    /// <summary>Removes a finished task without recording anything.</summary>
    internal void EndTask(CharacterId id) => _activeTasks.Remove(id);

    /// <summary>
    /// Stops the character's current task, if any, and records why. Materials already used are not refunded.
    /// Returns the outcome, or null if the character was idle.
    /// </summary>
    internal TaskOutcome? InterruptTask(CharacterId id, string reason)
    {
        if (!_activeTasks.Remove(id, out var task))
        {
            return null;
        }

        Record(ChronicleEntryKind.TaskInterrupted, $"{_characters[id].Name} stopped {task.DefinitionId}: {reason}", id);
        return new TaskOutcome(Clock.Tick, id, task.DefinitionId, TaskEnd.Interrupted, reason);
    }
}
