namespace Expedition.Simulation.Content;

/// <summary>A skill that characters can have, loaded from <c>data/skills.json</c>.</summary>
public sealed record SkillDefinition
{
    /// <summary>Unique ID, for example <c>skill.carpentry</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Skill group, for example <c>maritime</c> or <c>medical</c>.</summary>
    public required string Group { get; init; }
}
