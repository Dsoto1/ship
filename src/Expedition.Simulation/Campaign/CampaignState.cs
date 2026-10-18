using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Items;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Campaign;

/// <summary>
/// Root of all persistent campaign state. Owns the single roster of characters, their inventories and
/// the shared stores, the clock, the random generator, active tasks and the chronicle. State changes
/// that matter to the story go through methods here or through <see cref="SimulationRunner"/> so they
/// are validated and recorded.
/// </summary>
public sealed class CampaignState
{
    /// <summary>Seed used when none is given. Tests and scenarios should pass their own.</summary>
    public const ulong DefaultSeed = 0x5EA_F00D;

    /// <summary>ID of the shared store every new campaign starts with.</summary>
    public static readonly InventoryId DefaultStoreId = new("inv.stores");

    private readonly Dictionary<CharacterId, Character> _characters = [];
    private readonly Dictionary<CharacterId, TaskInstance> _activeTasks = [];
    private readonly Dictionary<InventoryId, Inventory> _inventories = [];
    private readonly Dictionary<CharacterId, CharacterPlacement> _placements = [];

    /// <summary>
    /// Creates a campaign in the given starting phase with a seeded random generator and one
    /// unlimited shared store (<see cref="DefaultStoreId"/>).
    /// </summary>
    public CampaignState(CampaignPhase startingPhase = CampaignPhase.SponsorCommission, ulong seed = DefaultSeed)
        : this(startingPhase, new SimulationRandom(seed))
    {
        CreateInventory(DefaultStoreId, "Stores", InventoryCapacity.Unlimited);
        SharedStoreId = DefaultStoreId;
    }

    private CampaignState(CampaignPhase phase, SimulationRandom random)
    {
        Phase = phase;
        Random = random;
    }

    /// <summary>Current campaign phase.</summary>
    public CampaignPhase Phase { get; private set; }

    /// <summary>ID source for this campaign.</summary>
    public IdAllocator Ids { get; } = new();

    /// <summary>Campaign time.</summary>
    public SimulationClock Clock { get; } = new();

    /// <summary>The only source of gameplay randomness. Its state is saved with the campaign.</summary>
    public SimulationRandom Random { get; }

    /// <summary>History of important events.</summary>
    public Chronicle Chronicle { get; } = new();

    /// <summary>Every character in the campaign, living or dead, keyed by ID.</summary>
    public IReadOnlyDictionary<CharacterId, Character> Characters => _characters;

    /// <summary>The task each busy character is doing. Characters without an entry are idle.</summary>
    public IReadOnlyDictionary<CharacterId, TaskInstance> ActiveTasks => _activeTasks;

    /// <summary>Where each placed character is. Characters without an entry are not on any map.</summary>
    public IReadOnlyDictionary<CharacterId, CharacterPlacement> Placements => _placements;

    /// <summary>Every inventory: personal ones (one per character) and shared stores.</summary>
    public IReadOnlyDictionary<InventoryId, Inventory> Inventories => _inventories;

    /// <summary>
    /// The store the crew eats from and draws task materials from after their own packs:
    /// the ship's hold at sea, the camp store ashore.
    /// </summary>
    public InventoryId SharedStoreId { get; private set; }

    /// <summary>The inventory named by <see cref="SharedStoreId"/>.</summary>
    public Inventory SharedStore => _inventories[SharedStoreId];

    /// <summary>True while a food shortage has been recorded and nobody has eaten since. Avoids repeating the entry every minute.</summary>
    public bool FoodShortageRecorded { get; internal set; }

    /// <summary>True while a water shortage has been recorded and nobody has drunk since.</summary>
    public bool WaterShortageRecorded { get; internal set; }

    /// <summary>
    /// Creates a new character with a freshly issued ID and an empty personal inventory, and adds
    /// both to the campaign.
    /// </summary>
    /// <param name="name">Full name.</param>
    /// <param name="age">Age in years.</param>
    /// <param name="origin">Place of origin.</param>
    /// <param name="carryCapacity">Limits on what the character can carry; unlimited if null.</param>
    public Character CreateCharacter(string name, int age, string origin, InventoryCapacity? carryCapacity = null)
    {
        var character = new Character(Ids.NextCharacterId(), name, age, origin);
        AddCharacter(character, carryCapacity);
        return character;
    }

    /// <summary>
    /// Adds an existing character (for example, one restored from a save). The ID is reserved so
    /// it will never be issued again. A personal inventory is created if one does not already exist.
    /// </summary>
    /// <exception cref="InvalidOperationException">A character with the same ID is already in the campaign.</exception>
    public void AddCharacter(Character character, InventoryCapacity? carryCapacity = null)
    {
        if (_characters.ContainsKey(character.Id))
        {
            throw new InvalidOperationException($"A character with ID {character.Id} is already in the campaign.");
        }

        Ids.Reserve(character.Id);
        _characters.Add(character.Id, character);
        if (!_inventories.ContainsKey(character.InventoryId))
        {
            CreateInventory(character.InventoryId, $"{character.Name}'s belongings", carryCapacity ?? InventoryCapacity.Unlimited);
        }
    }

    /// <summary>Returns the character with <paramref name="id"/>.</summary>
    /// <exception cref="KeyNotFoundException">No such character.</exception>
    public Character GetCharacter(CharacterId id) =>
        _characters.TryGetValue(id, out var character)
            ? character
            : throw new KeyNotFoundException($"No character with ID {id} in the campaign.");

    /// <summary>Creates an empty inventory, for example a ship's hold.</summary>
    /// <exception cref="InvalidOperationException">An inventory with that ID already exists.</exception>
    public Inventory CreateInventory(InventoryId id, string name, InventoryCapacity capacity)
    {
        if (_inventories.ContainsKey(id))
        {
            throw new InvalidOperationException($"An inventory with ID {id} already exists.");
        }

        var inventory = new Inventory(id, name, capacity);
        _inventories.Add(id, inventory);
        return inventory;
    }

    /// <summary>Makes <paramref name="id"/> the store the crew draws on, for example after landing.</summary>
    public CommandResult SetSharedStore(InventoryId id)
    {
        if (!_inventories.ContainsKey(id))
        {
            return CommandResult.Rejected($"No inventory with ID {id}.");
        }

        if (_characters.Values.Any(c => c.InventoryId == id))
        {
            return CommandResult.Rejected($"{id} is a personal inventory and cannot be the shared store.");
        }

        SharedStoreId = id;
        return CommandResult.Ok();
    }

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
            Record(ChronicleEntryKind.TransitionRejected, [
                new(ChronicleParameters.From, Phase.ToString()), new(ChronicleParameters.To, target.ToString()), new(ChronicleParameters.Cause, cause),
            ]);
            return CommandResult.Rejected(reason);
        }

        var previous = Phase;
        Phase = target;
        Record(ChronicleEntryKind.PhaseChanged, [
            new(ChronicleParameters.From, previous.ToString()), new(ChronicleParameters.To, target.ToString()), new(ChronicleParameters.Cause, cause),
        ]);
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

        Record(ChronicleEntryKind.SurvivalStateChanged, [
            new(ChronicleParameters.From, previous.ToString()), new(ChronicleParameters.To, newState.ToString()), new(ChronicleParameters.Cause, cause),
        ], id);
        if (!character.CanWork)
        {
            InterruptTask(id, InterruptReasons.Incapacitated, [
                new(ChronicleParameters.To, newState.ToString()), new(ChronicleParameters.Cause, cause),
            ]);
            StopMovement(id);
        }

        return result;
    }

    /// <summary>Appends a chronicle entry stamped with the current tick.</summary>
    internal void Record(ChronicleEntryKind kind, IEnumerable<KeyValuePair<string, string?>> parameters, params CharacterId[] characters) =>
        Chronicle.Record(Clock.Tick, kind, parameters, characters);

    /// <summary>Starts a task. Eligibility must already have been checked by <see cref="SimulationRunner"/>.</summary>
    internal void StartTask(TaskInstance task) => _activeTasks.Add(task.CharacterId, task);

    /// <summary>Removes a finished task without recording anything.</summary>
    internal void EndTask(CharacterId id) => _activeTasks.Remove(id);

    /// <summary>
    /// Stops the character's current task, if any, and records why. Materials already used are not refunded.
    /// Returns the outcome, or null if the character was idle.
    /// </summary>
    /// <param name="id">Character whose task stops.</param>
    /// <param name="reason">One of <see cref="InterruptReasons"/>.</param>
    /// <param name="details">Extra chronicle parameters, such as the cause.</param>
    internal TaskOutcome? InterruptTask(CharacterId id, string reason, IEnumerable<KeyValuePair<string, string?>>? details = null)
    {
        if (!_activeTasks.Remove(id, out var task))
        {
            return null;
        }

        Record(ChronicleEntryKind.TaskInterrupted, [
            new(ChronicleParameters.Task, task.DefinitionId), new(ChronicleParameters.Reason, reason), .. details ?? [],
        ], id);
        return new TaskOutcome(Clock.Tick, id, task.DefinitionId, TaskEnd.Interrupted, reason);
    }

    /// <summary>Puts a character at a cell, replacing any earlier placement. Callers validate the cell.</summary>
    internal void SetPlacement(CharacterId id, CharacterPlacement placement) => _placements[id] = placement;

    /// <summary>Ends any walk in progress, leaving the character on the cell reached so far.</summary>
    internal void StopMovement(CharacterId id)
    {
        if (_placements.TryGetValue(id, out var placement) && placement.Movement is not null)
        {
            placement.Cell = placement.CellAt(Clock.Tick);
            placement.Movement = null;
        }
    }

    /// <summary>
    /// Creates an empty campaign shell for the save loader to fill. It has no inventories and no
    /// shared store until the loader restores them.
    /// </summary>
    internal static CampaignState CreateForRestore(CampaignPhase phase, SimulationRandom random, long tick)
    {
        var campaign = new CampaignState(phase, random);
        campaign.Clock.Restore(tick);
        return campaign;
    }

    /// <summary>Restores the shared store pointer while loading.</summary>
    internal void RestoreSharedStore(InventoryId id) => SharedStoreId = id;
}
