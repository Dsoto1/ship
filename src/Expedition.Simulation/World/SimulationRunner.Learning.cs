using System.Globalization;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// Learning aboard (VOYAGE_EXPERIENCE §25–26). Work practises the skills it uses: hours of it raise a skill
/// a level, more hours each level, so that recruiting the skilled still matters; worn-out practice teaches
/// less, and work in a crisis more. By practice alone a hand comes to routine competence and a little
/// beyond (level 3). Learning from a mentor, a shipmate more skilled than they are and on deck with them,
/// goes two and a half times as fast and can rise to the mentor's own level, if the learner will learn
/// from them. Each level gained is told, and the captain sees it if they saw the work.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>How far practice alone takes anyone.</summary>
    public const int PracticeAloneTo = 3;

    /// <summary>How much faster a mentor teaches than practice alone.</summary>
    public const double MentorFactor = 2.5;

    /// <summary>Hours of practice from one level to the next: 25, 100, 225, 400, 625.</summary>
    public static double HoursToNextLevel(int level) => 25.0 * (level + 1) * (level + 1);

    // Each minute of work: practice in the skills it uses, the task's own in full and those it shows by half.
    private void Practise(Character character, Tasks.TaskDefinition task)
    {
        if (task.IsRest || character.Membership != Membership.Crew)
        {
            return;
        }

        var factor = character.Needs.Fatigue >= Content.Rules.TiredFatigue ? 0.5 : 1;
        if (Campaign.Voyage is { Outcome: VoyageOutcome.UnderWay } ship && Content.Voyage is { } rules && rules.Weather(ship.WeatherId).SeaState >= rules.Storms.HeavySeaState)
        {
            factor *= 1.5;
        }

        if (task.Skill is { } own)
        {
            Practise(character, own, HoursPerTick * factor);
        }

        foreach (var shown in task.Shows.Where(s => s != task.Skill))
        {
            Practise(character, shown, HoursPerTick * factor * 0.5);
        }

        // Working beside the lead of a workplace, a hand picks up the lead's trade, whatever the task in hand
        // (step W): the carpenter's ways learned setting posts or thatching under a master carpenter.
        if (Campaign.Camp is not null && Campaign.ActiveTasks.TryGetValue(character.Id, out var doing) && LeadAt(doing.SpotId) is { } lead
            && lead.Id != character.Id && task.Skill != LeadTrade && !task.Shows.Contains(LeadTrade))
        {
            Practise(character, LeadTrade, HoursPerTick * factor * 0.5);
        }
    }

    private void Practise(Character character, string skill, double hours)
    {
        var level = character.Skills.Get(skill);
        if (level >= Skills.MaxLevel)
        {
            return;
        }

        var mentor = ActiveMentor(character, skill) ?? WorkplaceTeacher(character, skill);
        var cap = mentor is null ? PracticeAloneTo : mentor.Skills.Get(skill);
        if (level >= cap)
        {
            return;
        }

        var life = Campaign.Life;
        life.AddPractice(character.Id, skill, hours * (mentor is null ? 1 : MentorFactor));
        if (life.PracticeOf(character.Id, skill) < HoursToNextLevel(level))
        {
            return;
        }

        character.Skills.Set(skill, level + 1);
        life.ResetPractice(character.Id, skill);
        Campaign.Record(ChronicleEntryKind.SkillImproved, [
            new(ChronicleParameters.To, skill), new(ChronicleParameters.Level, (level + 1).ToString(CultureInfo.InvariantCulture)),
            new(ChronicleParameters.From, mentor?.Id.Value.ToString(CultureInfo.InvariantCulture)),
        ], mentor is null ? [character.Id] : [character.Id, mentor.Id]);
        if (mentor is not null)
        {
            Campaign.ChangeRelationship(character.Id, mentor.Id, RelationshipDimension.Respect, 6, $"taught {SkillName(skill).ToLowerInvariant()}");
            Campaign.ChangeRelationship(character.Id, mentor.Id, RelationshipDimension.Affection, 3, $"taught {SkillName(skill).ToLowerInvariant()}");
            Campaign.ChangeRelationship(mentor.Id, character.Id, RelationshipDimension.Affection, 3, $"learned {SkillName(skill).ToLowerInvariant()}");
        }

        if (CaptainSees(character.Id))
        {
            Witness(character, skill, level + 1, Knowledge.KnowledgeSource.Observation);
        }
    }

    // The mentor teaching someone this skill now: theirs, alive, more skilled, and on deck with them.
    private Character? ActiveMentor(Character learner, string skill)
    {
        if (!Campaign.Life.Mentors.TryGetValue(learner.Id, out var mentorship) || mentorship.Skill != skill
            || !Campaign.Characters.TryGetValue(mentorship.Mentor, out var mentor) || !mentor.CanWork
            || mentor.Skills.Get(skill) <= learner.Skills.Get(skill))
        {
            return null;
        }

        return Campaign.Voyage is { Outcome: VoyageOutcome.UnderWay } && !(OnDeck(mentor.Id) && OnDeck(learner.Id)) ? null : mentor;
    }

    /// <summary>
    /// Has someone learn a skill from a shipmate, or (with no mentor) stop. Refused if the mentor is not
    /// more skilled, knows it only a little (below routine), or one of them will not have the other: a learner
    /// who thinks little of the mentor, a mentor who cannot abide the learner. Recorded in the chronicle.
    /// </summary>
    public CommandResult SetMentor(CharacterId learnerId, CharacterId? mentorId, string skill)
    {
        if (!Campaign.Characters.TryGetValue(learnerId, out var learner) || learner.Membership != Membership.Crew || !Content.Skills.ContainsKey(skill))
        {
            return CommandResult.Rejected("No such person or skill.");
        }

        if (mentorId is not { } id)
        {
            Campaign.Life.SetMentor(learnerId, null);
            return CommandResult.Ok();
        }

        if (id == learnerId || !Campaign.Characters.TryGetValue(id, out var mentor) || mentor.Membership != Membership.Crew || !mentor.CanWork)
        {
            return CommandResult.Rejected("They cannot teach.");
        }

        var name = SkillName(skill).ToLowerInvariant();
        if (mentor.Skills.Get(skill) < Skills.RoutineLevel || mentor.Skills.Get(skill) <= learner.Skills.Get(skill))
        {
            return CommandResult.Rejected($"{mentor.Name} knows no more {name} than {learner.Name} does.");
        }

        if (Campaign.GetRelationship(learnerId, id) is { Respect: < -20 })
        {
            return CommandResult.Rejected($"{learner.Name} will not learn from {mentor.Name}: they think too little of them.");
        }

        if (Campaign.GetRelationship(id, learnerId) is { Affection: < -30 })
        {
            return CommandResult.Rejected($"{mentor.Name} will not waste their time on {learner.Name}.");
        }

        Campaign.Life.SetMentor(learnerId, new Mentorship(id, skill, Campaign.Clock.Tick));
        Campaign.Record(ChronicleEntryKind.MentorshipBegun, [new(ChronicleParameters.To, skill)], learnerId, id);
        return CommandResult.Ok();
    }

    // Whether someone has the skill a task needs: their own, or, learning it, with their mentor to show them.
    private bool MeetsSkill(Character character, Tasks.TaskDefinition task) =>
        task.Skill is null || character.Skills.Get(task.Skill) >= task.MinSkill
        || (Campaign.Life.Mentors.TryGetValue(character.Id, out var mentorship) && mentorship.Skill == task.Skill
            && Campaign.Characters.TryGetValue(mentorship.Mentor, out var mentor) && mentor.CanWork && mentor.Skills.Get(task.Skill) >= task.MinSkill);
}
