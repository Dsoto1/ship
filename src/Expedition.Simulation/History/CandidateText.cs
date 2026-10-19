using Expedition.Simulation.Content;
using Expedition.Simulation.Knowledge;
using Expedition.Simulation.Recruitment;

namespace Expedition.Simulation.History;

/// <summary>What the captain has learned about a candidate, in English. Part of the text layer.</summary>
public static class CandidateText
{
    /// <summary>
    /// One line per topic: the most reliable source wins, then the latest; "disputed" if sources disagree.
    /// For example "Navigation 5 (says so; disputed)", "Rigging 2 (tested)". Never the hidden truth.
    /// </summary>
    public static IReadOnlyList<string> Describe(RecruitmentState state, string candidateId, GameContent content)
    {
        var facts = state.Facts.Select((fact, order) => (Fact: fact, Order: order)).Where(f => f.Fact.Candidate == candidateId).ToList();
        return facts.GroupBy(f => f.Fact.Topic)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var best = group.OrderByDescending(f => f.Fact.Source).ThenByDescending(f => f.Order).First().Fact;
                var disputed = group.Any(f => f.Fact.Level != best.Level) ? "; disputed" : "";
                return $"{Topic(best.Topic, best.Level, content)} ({Source(best.Source)}{disputed})";
            })
            .ToList();
    }

    private static string Source(KnowledgeSource source) => source switch
    {
        KnowledgeSource.Claim => "says so",
        KnowledgeSource.Reference => "by reputation",
        KnowledgeSource.Test => "tested",
        _ => KnowledgeText.SourceText(source),
    };

    private static string Topic(string topic, int level, GameContent content)
    {
        if (content.Traits.TryGetValue(topic, out var trait))
        {
            return level > 0 ? trait.Name : $"not {trait.Name.ToLowerInvariant()}";
        }

        var name = content.Skills.TryGetValue(topic, out var skill) ? skill.Name
            : content.Languages.TryGetValue(topic, out var language) ? language.Name
            : topic;
        return $"{name} {level}";
    }
}
