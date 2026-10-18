namespace Expedition.Simulation.Content;

/// <summary>A physical attribute characters have, loaded from <c>data/attributes.json</c>.</summary>
public sealed record AttributeDefinition
{
    /// <summary>Unique ID, for example <c>attribute.strength</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }
}
