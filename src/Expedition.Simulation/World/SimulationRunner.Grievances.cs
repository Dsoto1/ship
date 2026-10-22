using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.Events;
using Expedition.Simulation.History;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// Grievances by stages (VOYAGE_EXPERIENCE §18–19). Each hand holds what the voyage does to them, cause by
/// cause: short rations, hunger and thirst, being worked to exhaustion, pressing on in heavy weather, the
/// sick left untended, the captain's hard hand, a promise broken. A cause weighs more while it lasts and
/// fades once it is put right. As it grows the hand mutters, then comes to the captain (an event, answered
/// by putting it right, explaining, promising or silencing them), then others with the same complaint
/// gather round them, and at last they refuse all work but what keeps them alive. Each step comes in turn
/// and is told, so the captain always hears of it before the worst. Mutiny itself is not yet modelled.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>The weight at which each stage is reached.</summary>
    public static readonly IReadOnlyDictionary<GrievanceStage, double> StageAt = new Dictionary<GrievanceStage, double>
    {
        [GrievanceStage.Concern] = 25, [GrievanceStage.Complaint] = 45, [GrievanceStage.Faction] = 65, [GrievanceStage.Refusal] = 85,
    };

    // A cause fades this much an hour once it is put right: about five a day.
    private const double GrievanceFades = 0.2;

    // Hours at least between one stage and the next.
    private const double HoursBetweenStages = 12;

    /// <summary>Whether someone refuses to work.</summary>
    public bool Refusing(CharacterId id) => Campaign.Life.GrievanceOf(id).Stage == GrievanceStage.Refusal;

    // What a refusing hand will still do: rest, and pump to save their own lives.
    private bool StillDoes(Tasks.TaskDefinition task) =>
        task.IsRest || (task.ShipPerHour?.Bilge < 0 && Campaign.Voyage is { Bilge: > 50 });

    // Why someone will not take an order, or null.
    private string? RefusalProblem(Character character, Tasks.TaskDefinition task)
    {
        if (!Refusing(character.Id) || StillDoes(task))
        {
            return null;
        }

        var cause = Campaign.Life.GrievanceOf(character.Id).Main ?? GrievanceCauses.Harsh;
        return $"{character.Name} refuses to turn to over {GrievanceWords(cause)}. Hear them out, or put it right.";
    }

    /// <summary>A grievance cause in words: "short rations".</summary>
    public static string GrievanceWords(string cause) => cause switch
    {
        GrievanceCauses.Rations => "short rations",
        GrievanceCauses.Hunger => "hunger and thirst",
        GrievanceCauses.Overwork => "being worked to exhaustion",
        GrievanceCauses.Danger => "pressing on in heavy weather",
        GrievanceCauses.Sick => "the sick left untended",
        GrievanceCauses.Harsh => "the captain's hard hand",
        GrievanceCauses.Promise => "a promise broken",
        _ => cause,
    };

    // Whether a cause is going on now, for someone, and so weighs more each hour.
    private bool CauseHolds(string cause, Character person, VoyageState ship, VoyageRules rules, int untended)
    {
        return cause switch
        {
            GrievanceCauses.Rations => Campaign.Rationing,
            GrievanceCauses.Hunger => person.Needs.Hunger >= 85 || person.Needs.Thirst >= 85,
            GrievanceCauses.Overwork => person.Needs.Fatigue >= 75,
            GrievanceCauses.Danger => ship.SailPlan == SailPlan.PressOn && rules.Weather(ship.WeatherId).SeaState >= rules.Storms.HeavySeaState,
            GrievanceCauses.Sick => untended > 0,
            GrievanceCauses.Harsh => Campaign.CaptainId is { } captain && Campaign.GetRelationship(person.Id, captain) is { } view && view.Trust + view.Affection <= -30,
            _ => false,
        };
    }

    // How much a cause adds each hour while it holds.
    private static double CauseWeight(string cause, int untended) => cause switch
    {
        GrievanceCauses.Rations => 0.25,
        GrievanceCauses.Hunger => 0.6,
        GrievanceCauses.Overwork => 0.5,
        GrievanceCauses.Danger => 1.0,
        GrievanceCauses.Sick => 0.12 * untended,
        GrievanceCauses.Harsh => 0.3,
        _ => 0,
    };

    // Once an hour at sea: grievances grow or fade, stages change and are told, promises are kept or broken.
    private void StepGrievances(VoyageState ship, VoyageRules rules)
    {
        var life = Campaign.Life;
        var hands = Campaign.Crew.Where(c => c.Id != Campaign.CaptainId && c.SurvivalState == SurvivalState.Alive).OrderBy(c => c.Id.Value).ToList();
        var tended = Tender() is not null;
        foreach (var person in hands)
        {
            var untended = tended ? 0 : Campaign.Crew.Count(c => c.Id != person.Id && Illnesses(c).Any());
            var grievance = life.Hold(person.Id);
            var mood = (person.Needs.Stress >= 70 ? 1.5 : 1)
                       * (person.Traits.Contains("trait.loyal") ? 0.6 : 1)
                       * (grievance.Stage >= GrievanceStage.Faction ? 1.3 : 1);
            foreach (var cause in GrievanceCauses.All)
            {
                if (CauseHolds(cause, person, ship, rules, untended))
                {
                    grievance.Add(cause, CauseWeight(cause, untended) * mood * (cause == GrievanceCauses.Harsh && person.Traits.Contains("trait.proud") ? 1.3 : 1));
                }
                else if (grievance.Causes.ContainsKey(cause))
                {
                    grievance.Add(cause, -(cause == GrievanceCauses.Promise ? GrievanceFades / 4 : GrievanceFades));
                }
            }
        }

        foreach (var person in hands)
        {
            ChangeStage(person, life.Hold(person.Id), hands);
        }

        KeepPromises(ship, rules, hands);
    }

    private void ChangeStage(Character person, Grievance grievance, List<Character> hands)
    {
        var total = grievance.Total;
        var cause = grievance.Main ?? GrievanceCauses.Harsh;
        var now = Campaign.Clock.Tick;
        var settled = now - grievance.StageTick >= HoursBetweenStages * SimulationClock.TicksPerHour || grievance.Stage == GrievanceStage.None;

        // Easing: below a stage by a margin, it falls back one step.
        if (grievance.Stage > GrievanceStage.None && total < StageAt[grievance.Stage] - 10)
        {
            var from = grievance.Stage;
            grievance.Stage = from - 1;
            grievance.StageTick = now;
            if (grievance.Stage < GrievanceStage.Complaint)
            {
                grievance.Raised = GrievanceStage.None;
            }

            if (from == GrievanceStage.Refusal || grievance.Stage == GrievanceStage.None)
            {
                Campaign.Record(ChronicleEntryKind.GrievanceEased, [
                    new(ChronicleParameters.To, grievance.Stage.ToString()), new(ChronicleParameters.Reason, cause),
                ], person.Id);
            }

            return;
        }

        var next = grievance.Stage + 1;
        if (grievance.Stage < GrievanceStage.Refusal && total >= StageAt[next] && settled)
        {
            // Others with the same complaint gather round: a party, if there are any; a lone hand goes on alone.
            var party = hands.Where(h => h.Id != person.Id && Campaign.Life.GrievanceOf(h.Id).Stage >= GrievanceStage.Concern && Campaign.Life.GrievanceOf(h.Id).Main == cause).ToList();
            if (next == GrievanceStage.Faction && party.Count == 0)
            {
                if (total < StageAt[GrievanceStage.Refusal])
                {
                    return;
                }

                next = GrievanceStage.Refusal;
            }

            grievance.Stage = next;
            grievance.StageTick = now;
            if (next == GrievanceStage.Faction)
            {
                Campaign.Record(ChronicleEntryKind.FactionFormed, [new(ChronicleParameters.Reason, cause)], [person.Id, .. party.Select(p => p.Id)]);
            }
            else
            {
                Campaign.Record(ChronicleEntryKind.GrievanceRose, [new(ChronicleParameters.To, next.ToString()), new(ChronicleParameters.Reason, cause)], person.Id);
            }

            if (next == GrievanceStage.Refusal && Campaign.ActiveTasks.TryGetValue(person.Id, out var task) && !StillDoes(Content.Tasks[task.DefinitionId]))
            {
                Campaign.InterruptTask(person.Id, InterruptReasons.Refused);
            }
        }

        // The complaint brought aft, and the refusal faced: each once, when the captain is free to hear it.
        var hearing = grievance.Stage switch
        {
            GrievanceStage.Complaint or GrievanceStage.Faction when grievance.Raised < GrievanceStage.Complaint => (Event: $"event.complaint_{cause}", Stage: GrievanceStage.Complaint),
            GrievanceStage.Refusal when grievance.Raised < GrievanceStage.Refusal => (Event: "event.refusal", Stage: GrievanceStage.Refusal),
            _ => (Event: (string?)null, Stage: GrievanceStage.None),
        };
        if (hearing.Event is { } id && Campaign.PendingEvent is null)
        {
            var definition = Content.Events.TryGetValue(id, out var found) ? found : Content.Events.GetValueOrDefault("event.complaint_harsh");
            if (definition is not null)
            {
                grievance.Raised = hearing.Stage;
                Campaign.RaiseEvent(new PendingEvent(definition.Id, person.Id, null, now));
            }
        }
    }

    // A promise is kept when its cause is put right in time; past its day with the cause still there, it is broken.
    private void KeepPromises(VoyageState ship, VoyageRules rules, List<Character> hands)
    {
        foreach (var promise in Campaign.Life.Promises.ToList())
        {
            var holds = hands.Any(h => CauseHolds(promise.Cause, h, ship, rules, Tender() is null ? Campaign.Crew.Count(c => c.Id != h.Id && Illnesses(c).Any()) : 0));
            if (!holds)
            {
                Campaign.Life.EndPromise(promise);
                Campaign.Record(ChronicleEntryKind.PromiseKept, [new(ChronicleParameters.Reason, promise.Cause)]);
                foreach (var hand in hands.Where(_ => Campaign.CaptainId is not null))
                {
                    Campaign.ChangeRelationship(hand.Id, Campaign.CaptainId!.Value, RelationshipDimension.Trust, 4, "kept his word");
                }

                continue;
            }

            if (Campaign.Clock.Tick >= promise.Due)
            {
                Campaign.Life.EndPromise(promise);
                Campaign.Record(ChronicleEntryKind.PromiseBroken, [new(ChronicleParameters.Reason, promise.Cause)]);
                foreach (var hand in hands)
                {
                    Campaign.Life.Hold(hand.Id).Add(GrievanceCauses.Promise, 20);
                    if (Campaign.CaptainId is { } captain)
                    {
                        Campaign.ChangeRelationship(hand.Id, captain, RelationshipDimension.Trust, -10, "broke his word");
                    }
                }
            }
        }
    }

    // An event's outcome on grievances: eased or worsened, cause by cause.
    private void ApplyGrievance(Character character, GrievanceEffect effect)
    {
        var grievance = Campaign.Life.Hold(character.Id);
        foreach (var cause in effect.Cause == "all" ? grievance.Causes.Keys.ToList() : [effect.Cause])
        {
            grievance.Add(cause, effect.Amount);
        }
    }

    private void MakePromise(PromiseEffect effect) =>
        Campaign.Life.Promise(new Promise(effect.Cause, Campaign.Clock.Tick + (long)(effect.Days * SimulationClock.TicksPerDay)));

    /// <summary>Developer tool for testing and screenshots: adds to someone's grievance now.</summary>
    public CommandResult DeveloperAggrieve(CharacterId id, string cause, double amount)
    {
        if (!Campaign.Characters.ContainsKey(id) || !GrievanceCauses.All.Contains(cause))
        {
            return CommandResult.Rejected("No such person or cause.");
        }

        Campaign.Life.Hold(id).Add(cause, amount);
        return CommandResult.Ok();
    }
}
