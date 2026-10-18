using Expedition.Simulation.Core;
using Expedition.Simulation.Items;

namespace Expedition.Simulation.Characters;

/// <summary>
/// A persistent person in the campaign. The same <see cref="Character"/> instance, identified by
/// <see cref="Id"/>, is carried through the port, voyage, shipwreck and settlement. Phases refer to
/// characters by ID and never copy them.
/// </summary>
/// <remarks>
/// Change survival state through <see cref="Campaign.CampaignState.ChangeSurvivalState"/>, which
/// enforces the rules and records the change in the chronicle.
/// </remarks>
public sealed class Character
{
    private readonly HashSet<string> _traits = new(StringComparer.Ordinal);

    /// <summary>Creates a living, healthy character with no skills, traits or unmet needs.</summary>
    /// <exception cref="ArgumentException">The name or origin is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The age is negative.</exception>
    public Character(CharacterId id, string name, int age, string origin)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name must not be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(origin))
        {
            throw new ArgumentException("Origin must not be empty.", nameof(origin));
        }

        if (age < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(age), age, "Age must not be negative.");
        }

        Id = id;
        Name = name;
        Age = age;
        Origin = origin;
    }

    /// <summary>Stable campaign-scoped identifier.</summary>
    public CharacterId Id { get; }

    /// <summary>Full name as known in the world.</summary>
    public string Name { get; }

    /// <summary>Age in years.</summary>
    public int Age { get; }

    /// <summary>Place of origin, for example a city or region.</summary>
    public string Origin { get; }

    /// <summary>The character's personal inventory: what they carry with them through every phase.</summary>
    public InventoryId InventoryId => InventoryId.ForCharacter(Id);

    /// <summary>Actual skill levels: what the character has learned.</summary>
    public Skills Skills { get; } = new();

    /// <summary>Physical capabilities: what the character's body can do.</summary>
    public Attributes Attributes { get; } = new();

    /// <summary>Current needs.</summary>
    public Needs Needs { get; } = new();

    /// <summary>Current health and conditions.</summary>
    public Health Health { get; } = new();

    /// <summary>Trait definition IDs, for example <c>trait.cautious</c>.</summary>
    public IReadOnlyCollection<string> Traits => _traits;

    /// <summary>Whether the character is present and able to act.</summary>
    public SurvivalState SurvivalState { get; private set; } = SurvivalState.Alive;

    /// <summary>True only for characters who are alive and not incapacitated or missing.</summary>
    public bool CanWork => SurvivalState == SurvivalState.Alive;

    /// <summary>Adds a trait. Returns false if the character already had it.</summary>
    /// <exception cref="ArgumentException">The trait ID is empty.</exception>
    public bool AddTrait(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            throw new ArgumentException("Trait ID must not be empty.", nameof(traitId));
        }

        return _traits.Add(traitId);
    }

    /// <summary>
    /// Applies a survival state change if the rules allow it. Dead is final.
    /// Called by <see cref="Campaign.CampaignState"/>, which records the change.
    /// </summary>
    internal CommandResult TrySetSurvivalState(SurvivalState newState)
    {
        if (SurvivalState == SurvivalState.Dead && newState != SurvivalState.Dead)
        {
            return CommandResult.Rejected($"{Name} ({Id}) is dead and cannot become {newState}.");
        }

        if (SurvivalState == newState)
        {
            return CommandResult.Rejected($"{Name} ({Id}) is already {newState}.");
        }

        SurvivalState = newState;
        return CommandResult.Ok();
    }

    /// <summary>Sets survival state directly while loading a save. Never use for gameplay.</summary>
    internal void RestoreSurvivalState(SurvivalState state) => SurvivalState = state;
}
