using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.Knowledge;

namespace Expedition.Simulation.History;

/// <summary>English description of what the captain believes about someone. Part of the text layer.</summary>
public static class KnowledgeText
{
    /// <summary>
    /// One line per known topic, for example "Rigging 3 (seen at work)" or "Navigation 4 (says so; disputed)".
    /// Built only from the knowledge book, never from the character's true values.
    /// </summary>
    public static IReadOnlyList<string> Describe(KnowledgeBook book, CharacterId subject, GameContent content) =>
        book.TopicsAbout(subject)
            .Select(topic => (Topic: topic, Assessment: book.Assess(subject, topic)!))
            .Select(k => $"{TopicName(k.Topic, k.Assessment.Level, content)} ({SourceText(k.Assessment.Source)}{(k.Assessment.Conflicting ? "; disputed" : "")})")
            .ToList();

    /// <summary>How the belief was formed, in a few words.</summary>
    public static string SourceText(KnowledgeSource source) => source switch
    {
        KnowledgeSource.Rumour => "rumour",
        KnowledgeSource.Claim => "says so",
        KnowledgeSource.Reference => "vouched for",
        KnowledgeSource.Observation => "seen at work",
        KnowledgeSource.Test => "tested",
        _ => source.ToString(),
    };

    private static string TopicName(string topic, int level, GameContent content)
    {
        if (content.Traits.TryGetValue(topic, out var trait))
        {
            return level > 0 ? trait.Name : $"not {trait.Name.ToLowerInvariant()}";
        }

        var name = content.Skills.TryGetValue(topic, out var skill) ? skill.Name
            : content.Attributes.TryGetValue(topic, out var attribute) ? attribute.Name
            : content.Languages.TryGetValue(topic, out var language) ? language.Name
            : topic;
        return $"{name} {level}";
    }
}
