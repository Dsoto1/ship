using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.Knowledge;
using Expedition.Simulation.Port;

namespace Expedition.Simulation.World;

/// <summary>A question the captain can put to someone, as the conversation offers it.</summary>
/// <param name="Id">Topic ID.</param>
/// <param name="Ask">What the captain says.</param>
/// <param name="Discussed">True if they have already answered it (it can be asked again).</param>
public sealed record TopicView(string Id, string Ask, bool Discussed);

/// <summary>
/// Conversations in port (PORT_EXPERIENCE §9–11): what people say to the captain, what he can ask them,
/// and how it changes what they think of him. A topic opens once what it needs is in place: something
/// talked over before, something the captain has seen or been told about them. Some they will only
/// answer once they trust him; pressing them before that costs trust. Those looking for a berth say
/// what they want only when asked, and the captain cannot make an offer before he has heard it.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>
    /// Comes up to someone and starts talking. Returns how they meet him: the first time, the line written
    /// for meeting them; after that, the line for coming back. Without a written conversation, the old
    /// way: they say what they do and whom they know.
    /// </summary>
    public (CommandResult Result, string Said) BeginConversation(CharacterId id)
    {
        if (WhyCannotSpeak(id) is { } why)
        {
            return (CommandResult.Rejected(why), "");
        }

        if (PersonOf(id)?.Conversation is not { } conversation || Campaign.Port is not { } port)
        {
            return TalkTo(id);
        }

        var met = port.TalkedTo.Contains(id);
        port.MarkTalked(id);
        return (CommandResult.Ok(), met ? conversation.Again ?? conversation.First : conversation.First);
    }

    /// <summary>What the captain can ask someone now: open topics first, then those already answered.</summary>
    public IReadOnlyList<TopicView> Topics(CharacterId id)
    {
        if (PersonOf(id)?.Conversation is not { } conversation || Campaign.Port is not { } port)
        {
            return [];
        }

        var open = conversation.Topics.Where(t => Opens(id, t)).ToList();
        return open.Where(t => !port.Discussed.Contains((id, t.Id))).Select(t => new TopicView(t.Id, t.Ask, false))
            .Concat(open.Where(t => port.Discussed.Contains((id, t.Id))).Select(t => new TopicView(t.Id, t.Ask, true)))
            .ToList();
    }

    private bool Opens(CharacterId id, TopicDefinition topic)
    {
        var port = Campaign.Port!;
        var seeking = PersonOf(id)?.Seeking is not null && !port.Hired.Contains(id) && !port.Gone.Contains(id);
        return (!topic.Seeking || seeking)
            && topic.After.All(t => port.Discussed.Contains((id, t)))
            && topic.Known.All(f => Campaign.Knowledge.Assess(id, f) is not null);
    }

    /// <summary>
    /// Asks someone about something. If they do not yet trust the captain enough, they put him off, and
    /// pressing them costs trust; he can ask again later. Otherwise they answer: what they claim and tell
    /// goes in his knowledge of them, whom they point him to in his journal, and how they take being
    /// asked in what they think of him.
    /// </summary>
    /// <returns>What they say, and whether they walk off after it.</returns>
    public (CommandResult Result, string Said, bool Ends) Discuss(CharacterId id, string topicId)
    {
        if (WhyCannotSpeak(id) is { } why)
        {
            return (CommandResult.Rejected(why), "", false);
        }

        if (PersonOf(id) is not { Conversation: { } conversation } person || Campaign.Port is not { } port || Campaign.CaptainId is not { } captain
            || conversation.Topics.FirstOrDefault(t => t.Id == topicId) is not { } topic || !Opens(id, topic))
        {
            return (CommandResult.Rejected("That is not something to ask them now."), "", false);
        }

        var first = !port.Discussed.Contains((id, topic.Id));
        if (topic.Guard is { } guard && TrustOf(id) < guard.Trust)
        {
            if (guard.Change != 0)
            {
                Campaign.ChangeRelationship(id, captain, RelationshipDimension.Trust, guard.Change, $"pressed about {topic.Id}");
            }

            return (CommandResult.Ok(), guard.Says, false);
        }

        port.MarkDiscussed(id, topic.Id);
        if (first)
        {
            // Of themselves it is their own word; of someone else, what this one says of them.
            var (subject, source, informant) = topic.About is { } about && CharacterFor(about) is { } other
                ? (other.Id, KnowledgeSource.Reference, (CharacterId?)id)
                : (id, KnowledgeSource.Claim, null);
            foreach (var (claim, level) in topic.Claims.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                Campaign.Learn(subject, claim, level, source, informant);
            }

            foreach (var fact in topic.Tells)
            {
                Campaign.Learn(subject, fact, 1, source, informant);
            }

            foreach (var lead in person.Leads.Where(l => topic.Leads.Contains(l.Id)))
            {
                Hear(lead);
            }

            foreach (var (dimension, delta) in new[] { (RelationshipDimension.Trust, topic.Trust), (RelationshipDimension.Respect, topic.Respect), (RelationshipDimension.Affection, topic.Affection) })
            {
                if (delta != 0)
                {
                    Campaign.ChangeRelationship(id, captain, dimension, delta, $"talked of {topic.Id}");
                }
            }
        }

        return (CommandResult.Ok(), topic.Says, topic.Ends);
    }

    /// <summary>True once someone looking for a berth has said what they want, or has nothing written to say it.</summary>
    public bool TermsHeard(CharacterId id) =>
        PersonOf(id)?.Conversation is not { } conversation || conversation.Topics.All(t => !t.Terms)
        || conversation.Topics.Any(t => t.Terms && Campaign.Port?.Discussed.Contains((id, t.Id)) == true);

    private int TrustOf(CharacterId id) =>
        Campaign.CaptainId is { } captain && Campaign.Relationships.TryGetValue((id, captain), out var view) ? view.Trust : 0;

    /// <summary>How someone seems to take the captain, in a few words: from what they think of him.</summary>
    public string Manner(CharacterId id)
    {
        if (Campaign.CaptainId is not { } captain || !Campaign.Relationships.TryGetValue((id, captain), out var view))
        {
            return Campaign.Port?.TalkedTo.Contains(id) == true ? "Polite, no more." : "A stranger to you.";
        }

        return (view.Trust, view.Respect, view.Affection) switch
        {
            ( <= -20, _, _) => "Wants nothing to do with you.",
            (_, _, <= -15) => "Cold with you.",
            ( >= 25, _, >= 15) => "Speaks to you as a friend.",
            ( >= 20, _, _) => "Trusts you.",
            (_, >= 15, _) => "Thinks well of you.",
            ( >= 8, _, _) => "Warming to you.",
            ( < 0, _, _) => "Wary of you.",
            _ => "Polite, no more.",
        };
    }
}
