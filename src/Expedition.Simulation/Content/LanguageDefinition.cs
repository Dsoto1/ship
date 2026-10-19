namespace Expedition.Simulation.Content;

/// <summary>A language spoken in the setting, loaded from <c>data/languages.json</c>.</summary>
public sealed record LanguageDefinition
{
    /// <summary>Unique ID, for example <c>language.castrenan</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }
}

/// <summary>A social standing, loaded from <c>data/statuses.json</c>.</summary>
public sealed record StatusDefinition
{
    /// <summary>Unique ID, for example <c>status.sailor</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>What someone of this standing usually expects per month at sea, in sueldos. A starting point for negotiation.</summary>
    public required int TypicalMonthlyWage { get; init; }
}
