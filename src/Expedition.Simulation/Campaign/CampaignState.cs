using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Items;
using Expedition.Simulation.Knowledge;
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
    private readonly Dictionary<CharacterId, List<TaskInstance>> _queues = [];
    private readonly Dictionary<CharacterId, DutyRoster> _duties = [];
    private readonly Dictionary<InventoryId, Inventory> _inventories = [];
    private readonly Dictionary<CharacterId, CharacterPlacement> _placements = [];
    private readonly Dictionary<(CharacterId From, CharacterId To), Relationship> _relationships = [];

    /// <summary>
    /// Creates a campaign in the given starting phase with seeded random streams and one unlimited
    /// shared store (<see cref="DefaultStoreId"/>).
    /// </summary>
    /// <param name="startingPhase">Phase to start in.</param>
    /// <param name="seed">Seed for every random stream. New games should pass a fresh random seed.</param>
    /// <param name="campaignId">Stable campaign identifier; derived from the seed if not given.</param>
    public CampaignState(CampaignPhase startingPhase = CampaignPhase.SponsorCommission, ulong seed = DefaultSeed, string? campaignId = null)
        : this(startingPhase, seed, campaignId ?? $"campaign-{seed:x16}", new RandomStreams(seed))
    {
        CreateInventory(DefaultStoreId, "Stores", InventoryCapacity.Unlimited);
        SharedStoreId = DefaultStoreId;
    }

    private CampaignState(CampaignPhase phase, ulong seed, string campaignId, RandomStreams randomness)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            throw new ArgumentException("Campaign ID must not be empty.", nameof(campaignId));
        }

        Phase = phase;
        Seed = seed;
        CampaignId = campaignId;
        Randomness = randomness;
    }

    /// <summary>Stable identifier for this campaign, kept in every save of it.</summary>
    public string CampaignId { get; }

    /// <summary>The seed the campaign started with. Kept for reproducing and reporting problems.</summary>
    public ulong Seed { get; }

    /// <summary>The character the player leads as, or null before one is appointed (TECHNICAL_SPEC.md §18).</summary>
    public CharacterId? CaptainId { get; private set; }

    /// <summary>Current campaign phase.</summary>
    public CampaignPhase Phase { get; private set; }

    /// <summary>ID source for this campaign.</summary>
    public IdAllocator Ids { get; } = new();

    /// <summary>Campaign time.</summary>
    public SimulationClock Clock { get; } = new();

    /// <summary>The only sources of gameplay randomness, one stream per domain. Saved with the campaign.</summary>
    public RandomStreams Randomness { get; }

    /// <summary>History of important events.</summary>
    public Chronicle Chronicle { get; } = new();

    private readonly Dictionary<string, Events.EventRecord> _eventHistory = new(StringComparer.Ordinal);

    /// <summary>An event waiting for the captain's decision, or null.</summary>
    public Events.PendingEvent? PendingEvent { get; private set; }

    /// <summary>How often each event has happened.</summary>
    public IReadOnlyDictionary<string, Events.EventRecord> EventHistory => _eventHistory;

    /// <summary>When the last event fired (long.MinValue / 2 if none has).</summary>
    public long LastEventTick { get; internal set; } = long.MinValue / 2;

    /// <summary>True while the crew are on short rations.</summary>
    public bool Rationing { get; internal set; }

    /// <summary>Makes an event wait for the captain and counts it.</summary>
    internal void RaiseEvent(Events.PendingEvent pending)
    {
        PendingEvent = pending;
        LastEventTick = pending.Tick;
        _eventHistory[pending.DefinitionId] = new Events.EventRecord(
            (_eventHistory.TryGetValue(pending.DefinitionId, out var record) ? record.Count : 0) + 1, pending.Tick);
    }

    /// <summary>Clears the waiting event once decided.</summary>
    internal void ResolveEvent() => PendingEvent = null;

    /// <summary>Restores event state while loading a save.</summary>
    internal void RestoreEvents(Events.PendingEvent? pending, IEnumerable<KeyValuePair<string, Events.EventRecord>> history, long lastEventTick)
    {
        PendingEvent = pending;
        LastEventTick = lastEventTick;
        foreach (var (id, record) in history)
        {
            _eventHistory[id] = record;
        }
    }

    /// <summary>The ship at sea, or null when the expedition is not on a voyage.</summary>
    public Voyage.VoyageState? Voyage { get; private set; }

    /// <summary>Sets the voyage. The runner checks it may start; the loader restores it.</summary>
    internal void SetVoyage(Voyage.VoyageState? voyage) => Voyage = voyage;

    /// <summary>What the captain knows about people, separate from the truth (TECHNICAL_SPEC.md §5.2).</summary>
    public KnowledgeBook Knowledge { get; } = new();

    /// <summary>Records something the captain has learned, stamped with the current tick. Returns false if already known.</summary>
    public bool Learn(CharacterId subject, string topic, int level, KnowledgeSource source, CharacterId? informant = null)
    {
        if (!_characters.ContainsKey(subject))
        {
            throw new KeyNotFoundException($"No character with ID {subject} in the campaign.");
        }

        return Knowledge.Learn(new KnowledgeEntry(Clock.Tick, subject, topic, level, source, informant));
    }

    /// <summary>Every character in the campaign, living or dead, keyed by ID.</summary>
    public IReadOnlyDictionary<CharacterId, Character> Characters => _characters;

    /// <summary>The task each busy character is doing. Characters without an entry are idle.</summary>
    public IReadOnlyDictionary<CharacterId, TaskInstance> ActiveTasks => _activeTasks;

    /// <summary>
    /// Orders waiting for <paramref name="id"/>, next first: urgent orders before normal ones, and within
    /// a priority in the order given, except that a paused task goes back to the front of its priority.
    /// </summary>
    public IReadOnlyList<TaskInstance> QueueOf(CharacterId id) => _queues.TryGetValue(id, out var queue) ? queue : [];

    /// <summary>A character's standing duties, most important first; empty if none.</summary>
    public IReadOnlyList<string> DutiesOf(CharacterId id) => _duties.TryGetValue(id, out var roster) ? roster.Tasks : [];

    /// <summary>Every character's standing duties, by character.</summary>
    public IReadOnlyDictionary<CharacterId, DutyRoster> Duties => _duties;

    /// <summary>Replaces a character's standing duties. The runner validates the task IDs.</summary>
    internal void SetDuties(CharacterId id, IReadOnlyList<string> tasks)
    {
        if (tasks.Count == 0)
        {
            _duties.Remove(id);
        }
        else
        {
            _duties[id] = new DutyRoster(tasks);
        }
    }

    /// <summary>Characters with waiting orders.</summary>
    public IEnumerable<CharacterId> CharactersWithQueues => _queues.Where(pair => pair.Value.Count > 0).Select(pair => pair.Key);

    /// <summary>Every directed relationship that has ever changed from neutral, keyed by (from, to).</summary>
    public IReadOnlyDictionary<(CharacterId From, CharacterId To), Relationship> Relationships => _relationships;

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

    /// <summary>
    /// Makes <paramref name="id"/> the captain. The captain must be alive and able to act. Recorded in
    /// the chronicle. Succession after a captain's death is a later, approval-required design.
    /// </summary>
    public CommandResult AppointCaptain(CharacterId id, string cause)
    {
        if (!_characters.TryGetValue(id, out var character))
        {
            return CommandResult.Rejected($"No character with ID {id} in the campaign.");
        }

        if (!character.CanWork)
        {
            return CommandResult.Rejected($"{character.Name} is {character.SurvivalState} and cannot lead.");
        }

        if (CaptainId == id)
        {
            return CommandResult.Rejected($"{character.Name} is already the captain.");
        }

        CaptainId = id;
        Record(ChronicleEntryKind.CaptainAppointed, [new(ChronicleParameters.Cause, cause)], id);
        return CommandResult.Ok();
    }

    /// <summary>How <paramref name="from"/> regards <paramref name="to"/>, or null if still neutral and unrecorded.</summary>
    public Relationship? GetRelationship(CharacterId from, CharacterId to) =>
        _relationships.GetValueOrDefault((from, to));

    /// <summary>
    /// Changes how <paramref name="from"/> regards <paramref name="to"/> in one dimension, keeping the reason
    /// and recording it in the chronicle. Only living characters form new views; views about the dead may
    /// still change (grief, respect).
    /// </summary>
    /// <param name="from">Who holds the view.</param>
    /// <param name="to">Who it is about.</param>
    /// <param name="dimension">Trust, respect or affection.</param>
    /// <param name="delta">Change, -200..200 and not zero. The value stays within -100..100.</param>
    /// <param name="cause">Why it changed, for explanations and the chronicle.</param>
    public CommandResult ChangeRelationship(CharacterId from, CharacterId to, RelationshipDimension dimension, int delta, string cause)
    {
        if (!_characters.TryGetValue(from, out var holder) || !_characters.ContainsKey(to))
        {
            return CommandResult.Rejected("Both characters must be in the campaign.");
        }

        if (from == to)
        {
            return CommandResult.Rejected($"{holder.Name} cannot have a relationship with themselves.");
        }

        if (holder.SurvivalState == SurvivalState.Dead)
        {
            return CommandResult.Rejected($"{holder.Name} is dead and forms no new opinions.");
        }

        if (delta == 0 || Math.Abs(delta) > 2 * Relationship.Max)
        {
            return CommandResult.Rejected($"A relationship change must be between -{2 * Relationship.Max} and {2 * Relationship.Max}, and not zero.");
        }

        if (string.IsNullOrWhiteSpace(cause))
        {
            return CommandResult.Rejected("A relationship change needs a cause.");
        }

        if (!_relationships.TryGetValue((from, to), out var relationship))
        {
            relationship = new Relationship(from, to);
            _relationships[(from, to)] = relationship;
        }

        relationship.Apply(new RelationshipChange(Clock.Tick, dimension, delta, cause));
        Record(ChronicleEntryKind.RelationshipChanged, [
            new(ChronicleParameters.Dimension, dimension.ToString()),
            new(ChronicleParameters.Delta, delta.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ChronicleParameters.Cause, cause),
        ], from, to);
        return CommandResult.Ok();
    }

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

        // The dead and the missing hold no orders. Incapacitated characters keep theirs for when they recover.
        if (newState is SurvivalState.Dead or SurvivalState.Missing)
        {
            _queues.Remove(id);
            _duties.Remove(id);
        }

        return result;
    }

    /// <summary>Appends a chronicle entry stamped with the current tick.</summary>
    internal void Record(ChronicleEntryKind kind, IEnumerable<KeyValuePair<string, string?>> parameters, params CharacterId[] characters) =>
        Chronicle.Record(Clock.Tick, kind, parameters, characters);

    /// <summary>Starts a task. Eligibility must already have been checked by <see cref="SimulationRunner"/>.</summary>
    internal void StartTask(TaskInstance task)
    {
        task.StartedAtTick ??= Clock.Tick;
        _activeTasks.Add(task.CharacterId, task);
    }

    /// <summary>
    /// Adds a waiting order. Urgent orders come before normal ones, and within a priority the player's
    /// orders come before paused duty or self-care work. With <paramref name="aheadOfSamePriority"/> it goes
    /// before others of the same kind (a paused task resuming first); otherwise after them.
    /// </summary>
    internal void Enqueue(TaskInstance task, bool aheadOfSamePriority)
    {
        if (!_queues.TryGetValue(task.CharacterId, out var queue))
        {
            queue = [];
            _queues[task.CharacterId] = queue;
        }

        var rank = QueueRank(task);
        var index = aheadOfSamePriority
            ? queue.FindIndex(waiting => QueueRank(waiting) <= rank)
            : queue.FindIndex(waiting => QueueRank(waiting) < rank);
        queue.Insert(index < 0 ? queue.Count : index, task);
    }

    // Higher goes first: priority, then the player's orders ahead of work the character took up themselves.
    private static int QueueRank(TaskInstance task) => (int)task.Priority * 2 + (task.Origin == TaskOrigin.Order ? 1 : 0);

    /// <summary>Removes and returns the next waiting order, or null.</summary>
    internal TaskInstance? Dequeue(CharacterId id)
    {
        if (!_queues.TryGetValue(id, out var queue) || queue.Count == 0)
        {
            return null;
        }

        var next = queue[0];
        queue.RemoveAt(0);
        return next;
    }

    /// <summary>Removes a waiting order by ID. Returns false if it is not queued for that character.</summary>
    internal bool RemoveQueued(CharacterId id, TaskId taskId) =>
        _queues.TryGetValue(id, out var queue) && queue.RemoveAll(task => task.Id == taskId) > 0;

    /// <summary>
    /// Pauses the active task: it goes back to the front of its priority in the queue with its progress
    /// and used materials kept, and its work spot is released. Recorded in the chronicle.
    /// </summary>
    internal void PauseTask(CharacterId id, string pausedBy)
    {
        if (!_activeTasks.Remove(id, out var task))
        {
            return;
        }

        task.SpotId = null;
        StopMovement(id);
        Enqueue(task, aheadOfSamePriority: true);
        Record(ChronicleEntryKind.TaskPaused, [
            new(ChronicleParameters.Task, task.DefinitionId), new(ChronicleParameters.PausedBy, pausedBy),
        ], id);
    }

    /// <summary>Removes a finished task without recording anything.</summary>
    internal void EndTask(CharacterId id) => _activeTasks.Remove(id);

    /// <summary>Drops duty work for more pressing duty work, without recording it (it is routine).</summary>
    internal void AbandonTask(CharacterId id)
    {
        if (_activeTasks.Remove(id))
        {
            StopMovement(id);
        }
    }

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
    internal static CampaignState CreateForRestore(CampaignPhase phase, ulong seed, string campaignId, RandomStreams randomness, long tick)
    {
        var campaign = new CampaignState(phase, seed, campaignId, randomness);
        campaign.Clock.Restore(tick);
        return campaign;
    }

    /// <summary>Adds a relationship read from a save. The loader validates it first.</summary>
    internal void RestoreRelationship(Relationship relationship) =>
        _relationships.Add((relationship.From, relationship.To), relationship);

    /// <summary>Restores the captain while loading. The loader checks the character exists.</summary>
    internal void RestoreCaptain(CharacterId? id) => CaptainId = id;

    /// <summary>Restores the shared store pointer while loading.</summary>
    internal void RestoreSharedStore(InventoryId id) => SharedStoreId = id;
}
