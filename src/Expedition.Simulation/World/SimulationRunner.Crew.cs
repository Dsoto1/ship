using Expedition.Simulation.Characters;
using Expedition.Simulation.History;
using Expedition.Simulation.Knowledge;

namespace Expedition.Simulation.World;

/// <summary>
/// The crew at sea: what they said of themselves in port meets what they do. The first time the
/// captain sees someone at work in a skill or a language they were vouched for, and it is two levels or more from
/// what he was told, it is noted: the crew notice too, and think the better or the worse of them.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>How far what is seen must differ from what was told before it is remarked on.</summary>
    public const int ClaimMargin = 2;

    // Learns what the captain sees someone do, and at sea, the first time it is seen, sets it against
    // what he was told: their own word first, else what others said.
    private void Witness(Character character, string topic, int level, KnowledgeSource source)
    {
        var about = Campaign.Knowledge.About(character.Id).Where(e => e.Topic == topic).ToList();
        var told = about.Where(e => e.Source == KnowledgeSource.Claim).MaxBy(e => e.Tick)
                   ?? about.Where(e => e.Source is KnowledgeSource.Reference or KnowledgeSource.Rumour).MaxBy(e => e.Tick);
        var seenBefore = about.Any(e => e.Source >= KnowledgeSource.Observation);
        Campaign.Learn(character.Id, topic, level, source);
        if (told is null || seenBefore || Campaign.Voyage is null || Math.Abs(told.Level - level) < ClaimMargin)
        {
            return;
        }

        var worse = level < told.Level;
        Campaign.Record(ChronicleEntryKind.ClaimTested, [
            new(ChronicleParameters.Need, topic),
            new(ChronicleParameters.From, told.Level.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ChronicleParameters.Level, level.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ], character.Id);
        foreach (var shipmate in Campaign.Crew.Where(c => c.Id != character.Id && c.Id != Campaign.CaptainId && c.SurvivalState == SurvivalState.Alive))
        {
            Campaign.ChangeRelationship(shipmate.Id, character.Id, RelationshipDimension.Respect, worse ? -6 : 5,
                worse ? "found wanting at sea" : "better than they let on");
        }

        if (worse)
        {
            character.Needs.Apply(new NeedRates { Stress = 8 }, 1);
        }
    }
}
