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
                // A fact, seen or told: the person's definition holds its text.
                name = belief.Source == KnowledgeSource.Observation ? "Seen" : "Heard";
                value = topic;
            }

            var source = belief.Source switch
            {
                KnowledgeSource.Rumour => informant is null ? "gossip" : informant,
                KnowledgeSource.Claim => "their own word",
                KnowledgeSource.Reference => informant ?? "someone who knows them",
                KnowledgeSource.Observation => "seen at work",
                KnowledgeSource.Test => "shown in front of you",
                _ => belief.Source.ToString(),
            };
            lines.Add(new KnowledgeLine(topic, name, value, Status(belief.Source), source, Confidence(belief.Source, belief.Conflicting), belief.Conflicting,
                newest?.Tick ?? 0));
        }

        return lines;
    }

    /// <summary>A skill level in words (UI_UX §21): never the hidden number.</summary>
    public static string LevelWord(int level) => level switch
    {
        <= 0 => "none",
        1 => "novice",
        2 => "competent",
        3 => "experienced",
        4 => "expert",
        _ => "master",
    };

    /// <summary>How a belief was formed, in the shared vocabulary (UI_UX §22).</summary>
    public static string Status(KnowledgeSource source) => source switch
    {
        KnowledgeSource.Rumour => "Rumored",
        KnowledgeSource.Claim => "Claimed",
        KnowledgeSource.Reference => "Vouched for",
        KnowledgeSource.Observation => "Observed",
        KnowledgeSource.Test => "Confirmed",
        _ => "Unknown",
    };

    private static string AttributeWord(int level) => level switch
    {
        <= 1 => "poor",
        2 => "fair",
        3 => "good",
        4 => "strong",
        _ => "exceptional",
    };

    /// <summary>
    /// How far a belief can be trusted (UI_UX §22): Poor, Low, Fair, High, Very High. Equally good sources
    /// that disagree lower it a step.
    /// </summary>
    public static string Confidence(KnowledgeSource source, bool conflicting)
    {
        var step = source switch
        {
            KnowledgeSource.Rumour => 0,
            KnowledgeSource.Claim => 1,
            KnowledgeSource.Reference => 2,
            KnowledgeSource.Observation => 3,
            _ => 4,
        };
        return new[] { "Poor", "Low", "Fair", "High", "Very High" }[Math.Max(0, conflicting ? step - 1 : step)];
    }

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

/// <summary>One thing the captain believes about someone, in words, with its provenance (UI_UX §21–23).</summary>
/// <param name="Topic">The skill, attribute, trait, language or fact ID.</param>
/// <param name="Name">What it is, for example "Rigging".</param>
/// <param name="Value">What is believed, for example "experienced", or the fact ID for facts.</param>
/// <param name="Status">How it was formed: Rumored, Claimed, Vouched for, Observed or Confirmed.</param>
/// <param name="Source">Who or what it came from, for example "Andrés" or "their own word".</param>
/// <param name="Confidence">Poor, Low, Fair, High or Very High.</param>
/// <param name="Conflicting">True if equally good sources disagree.</param>
/// <param name="Tick">When it was last updated.</param>
public sealed record KnowledgeLine(string Topic, string Name, string Value, string Status, string Source, string Confidence, bool Conflicting, long Tick);
