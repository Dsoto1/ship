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

    /// <summary>
    /// What the captain believes about someone, topic by topic, in words rather than numbers, with where
    /// it came from and how far to trust it (PORT_EXPERIENCE §10): "Rigging: skilled. Vouched for by
    /// Andrés. Fairly sure." Built only from the knowledge book, never from the truth.
    /// </summary>
    public static IReadOnlyList<KnowledgeLine> Lines(KnowledgeBook book, CharacterId subject, GameContent content,
        IReadOnlyDictionary<CharacterId, Characters.Character> characters)
    {
        var lines = new List<KnowledgeLine>();
        foreach (var topic in book.TopicsAbout(subject))
        {
            var belief = book.Assess(subject, topic)!;
            var newest = book.About(subject).Where(e => e.Topic == topic && e.Source == belief.Source).MaxBy(e => e.Tick);
            var informant = newest?.Informant is { } who && characters.TryGetValue(who, out var person) ? person.Name : null;
            string name;
            string value;
            if (content.Traits.TryGetValue(topic, out var trait))
            {
                name = trait.Name;
                value = belief.Level > 0 ? "yes" : "no";
            }
            else if (content.Skills.TryGetValue(topic, out var skill))
            {
                name = skill.Name;
                value = LevelWord(belief.Level);
            }
            else if (content.Attributes.TryGetValue(topic, out var attribute))
            {
                name = attribute.Name;
                value = AttributeWord(belief.Level);
            }
            else if (content.Languages.TryGetValue(topic, out var language))
            {
                name = language.Name;
                value = LevelWord(belief.Level);
            }
            else
            {
                // A fact, such as something seen: the person's definition holds its text.
                name = "Seen";
                value = topic;
            }

            var source = belief.Source switch
            {
                KnowledgeSource.Rumour => informant is null ? "Rumour" : $"Rumour, from {informant}",
                KnowledgeSource.Claim => "Their own claim",
                KnowledgeSource.Reference => informant is null ? "Vouched for" : $"Vouched for by {informant}",
                KnowledgeSource.Observation => "Seen with your own eyes",
                KnowledgeSource.Test => "Tested in front of you",
                _ => belief.Source.ToString(),
            };
            lines.Add(new KnowledgeLine(topic, name, value, source, Confidence(belief.Source, belief.Conflicting), belief.Conflicting));
        }

        return lines;
    }

    /// <summary>A skill level in words: 0 none, 1 a beginner, up to 5 a master.</summary>
    public static string LevelWord(int level) => level switch
    {
        <= 0 => "none",
        1 => "a beginner",
        2 => "competent",
        3 => "skilled",
        4 => "expert",
        _ => "a master",
    };

    private static string AttributeWord(int level) => level switch
    {
        <= 1 => "poor",
        2 => "fair",
        3 => "good",
        4 => "strong",
        _ => "exceptional",
    };

    /// <summary>How far a belief can be trusted, from its source.</summary>
    public static string Confidence(KnowledgeSource source, bool conflicting) => conflicting ? "Disputed" : source switch
    {
        KnowledgeSource.Rumour => "Hearsay",
        KnowledgeSource.Claim => "Unverified",
        KnowledgeSource.Reference => "Fairly sure",
        KnowledgeSource.Observation => "Sure",
        KnowledgeSource.Test => "Certain",
        _ => "",
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

/// <summary>One thing the captain believes about someone, in words, with its provenance.</summary>
/// <param name="Topic">The skill, attribute, trait, language or fact ID.</param>
/// <param name="Name">What it is, for example "Rigging".</param>
/// <param name="Value">What is believed, for example "skilled", or the fact ID for facts.</param>
/// <param name="Source">Where it came from, for example "Vouched for by Andrés".</param>
/// <param name="Confidence">How far to trust it: Hearsay, Unverified, Fairly sure, Sure, Certain, or Disputed.</param>
/// <param name="Conflicting">True if equally good sources disagree.</param>
public sealed record KnowledgeLine(string Topic, string Name, string Value, string Source, string Confidence, bool Conflicting);
