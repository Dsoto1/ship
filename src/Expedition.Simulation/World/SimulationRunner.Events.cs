using System.Globalization;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.Events;
using Expedition.Simulation.History;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>Events at sea (D-017): when they fire, who they involve, and what the captain's choices do.</summary>
public sealed partial class SimulationRunner
{
    /// <summary>Hours that must pass after one event before another can fire, so crises do not pile up.</summary>
    public const int HoursBetweenEvents = 8;

    // Checked on the hour while at sea and nothing is waiting for the captain.
    private void ConsiderEvents()
    {
        if (Campaign.Clock.Tick % SimulationClock.TicksPerHour != 0 || Campaign.PendingEvent is not null
            || Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } ship || Content.Events.Count == 0
            || Campaign.Clock.Tick - Campaign.LastEventTick < HoursBetweenEvents * SimulationClock.TicksPerHour)
        {
            return;
        }

        var day = Campaign.Clock.Day + 1;
        var water = SupplyForecast().WaterDays;
        foreach (var definition in Content.Events.Values.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            // Roll for every event every hour, eligible or not, so one event's conditions never change
            // the dice another event sees.
            var roll = Campaign.Randomness.Events.NextDouble();
            var trigger = definition.Trigger;
            Campaign.EventHistory.TryGetValue(definition.Id, out var history);
            if (roll >= trigger.ChancePerDay / 24 || day < trigger.MinDay
                || (trigger.Weather is { } weathers && !weathers.Contains(ship.WeatherId))
                || (trigger.WaterDaysBelow is { } below && water >= below)
                || (trigger.BilgeAbove is { } bilge && ship.Bilge <= bilge)
                || (trigger.Rationing is { } rationing && rationing != Campaign.Rationing)
                || (trigger.LeaguesBelow is { } leagues && ship.LeaguesRemaining >= leagues)
                || (history is not null && (trigger.Once
                    || Campaign.Clock.Tick - history.LastTick < trigger.CooldownDays * SimulationClock.TicksPerDay)))
            {
                continue;
            }

            if (!PickInvolved(trigger.Subject, out var subject, out var other)
                || (subject is { } about && (trigger.Known.Any(t => Campaign.Knowledge.Assess(about, t) is null)
                                             || trigger.NotKnown.Any(t => Campaign.Knowledge.Assess(about, t) is not null))))
            {
                continue;
            }

            Campaign.RaiseEvent(new PendingEvent(definition.Id, subject, other, Campaign.Clock.Tick));
            return;
        }
    }

    /// <summary>
    /// Developer tool for testing and screenshots: raises an event now, whatever its chance, day,
    /// weather or cooldown, if someone fits it. Its knowledge conditions still apply.
    /// </summary>
    public CommandResult DeveloperRaiseEvent(string eventId)
    {
        if (!Content.Events.TryGetValue(eventId, out var definition))
        {
            return CommandResult.Rejected($"No event '{eventId}'.");
        }

        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay } || Campaign.PendingEvent is not null)
        {
            return CommandResult.Rejected("Events happen at sea, one at a time.");
        }

        if (!PickInvolved(definition.Trigger.Subject, out var subject, out var other)
            || (subject is { } about && (definition.Trigger.Known.Any(t => Campaign.Knowledge.Assess(about, t) is null)
                                         || definition.Trigger.NotKnown.Any(t => Campaign.Knowledge.Assess(about, t) is not null))))
        {
            return CommandResult.Rejected($"No one aboard fits '{eventId}'.");
        }

        Campaign.RaiseEvent(new PendingEvent(definition.Id, subject, other, Campaign.Clock.Tick));
        return CommandResult.Ok();
    }

    private bool PickInvolved(SubjectRule? rule, out CharacterId? subject, out CharacterId? other)
    {
        subject = null;
        other = null;
        if (rule is null)
        {
            return true;
        }

        var able = Campaign.Crew
            .Where(c => c.SurvivalState == SurvivalState.Alive)
            .Where(c => rule.Origin is null || c.Origin == rule.Origin)
            .Where(c => rule.Trait is null || c.Traits.Contains(rule.Trait))
            .Where(c => rule.Condition is null || c.Health.Conditions.Contains(rule.Condition))
            .Where(c => rule.Person is null || c.PersonId == rule.Person)
            .OrderBy(c => c.Id)
            .ToList();
        if (rule.Pick == "feud")
        {
            var feuds = Campaign.Relationships.Values
                .Where(r => r.Trust + r.Respect + r.Affection <= -15)
                .Where(r => able.Any(c => c.Id == r.From) && able.Any(c => c.Id == r.To))
                .OrderBy(r => r.From).ThenBy(r => r.To)
                .ToList();
            if (feuds.Count == 0)
            {
                // Hard days make enemies: two of the most strained people aboard.
                var strained = able.Where(c => c.Needs.Stress >= 55).ToList();
                if (strained.Count < 2)
                {
                    return false;
                }

                var first = strained[Campaign.Randomness.Events.NextInt(strained.Count)];
                var rest = strained.Where(c => c.Id != first.Id).ToList();
                (subject, other) = (first.Id, rest[Campaign.Randomness.Events.NextInt(rest.Count)].Id);
                return true;
            }

            var feud = feuds[Campaign.Randomness.Events.NextInt(feuds.Count)];
            (subject, other) = (feud.From, feud.To);
            return true;
        }

        var pool = rule.Pick switch
        {
            "anyone" => able,
            "stressed" => able.Where(c => c.Needs.Stress >= rule.MinStress && c.Id != Campaign.CaptainId).ToList(),
            _ => able.Where(c => c.Id != Campaign.CaptainId).ToList(),
        };
        if (pool.Count == 0)
        {
            return false;
        }

        subject = pool[Campaign.Randomness.Events.NextInt(pool.Count)].Id;
        return true;
    }

    /// <summary>The waiting event's choices: whether each is possible now and its chance of success.</summary>
    public IReadOnlyList<OfferedChoice> OfferedChoices()
    {
        if (Campaign.PendingEvent is not { } pending || !Content.Events.TryGetValue(pending.DefinitionId, out var definition))
        {
            return [];
        }

        return definition.Choices
            .Where(choice => choice.Known.Count == 0 || (pending.Subject is { } about && choice.Known.All(t => Campaign.Knowledge.Assess(about, t) is not null)))
            .Select(choice => new OfferedChoice(
            choice,
            choice.RequiresItem is null || Campaign.SharedStore.Count(choice.RequiresItem) >= choice.RequiresAmount,
            choice.Check is { } check ? ChanceOf(check, pending) : null)).ToList();
    }

    private double ChanceOf(SkillCheck check, PendingEvent pending)
    {
        if (check.Skill is null)
        {
            return Math.Clamp(check.Base, 0.05, 0.95);
        }

        var level = check.Who switch
        {
            "subject" when pending.Subject is { } id => Campaign.Characters[id].Skills.Get(check.Skill),
            "captain" when Campaign.CaptainId is { } id => Campaign.Characters[id].Skills.Get(check.Skill),
            _ => Campaign.Crew.Where(c => c.CanWork).Select(c => c.Skills.Get(check.Skill)).DefaultIfEmpty(0).Max(),
        };
        return Math.Clamp(check.Base + check.PerLevel * level, 0.05, 0.95);
    }

    /// <summary>
    /// The captain decides: applies the chosen outcome (rolling any skill check on the events stream),
    /// records it in the chronicle, and clears the event. Refused if nothing is waiting, the choice is
    /// unknown, or the hold lacks what it needs.
    /// </summary>
    public (CommandResult Result, EventResolution? Resolution) ResolveEvent(string choiceId)
    {
        if (Campaign.PendingEvent is not { } pending || !Content.Events.TryGetValue(pending.DefinitionId, out var definition))
        {
            return (CommandResult.Rejected("Nothing is waiting for the captain's decision."), null);
        }

        var offered = OfferedChoices().FirstOrDefault(o => o.Choice.Id == choiceId);
        if (offered is null)
        {
            return (CommandResult.Rejected($"'{choiceId}' is not one of the choices."), null);
        }

        if (!offered.Available)
        {
            return (CommandResult.Rejected($"There is not enough {ItemName(offered.Choice.RequiresItem!)} in the hold for that."), null);
        }

        var succeeded = offered.Chance is not { } chance || Campaign.Randomness.Events.NextDouble() < chance;
        var outcome = succeeded || offered.Choice.Failure is null ? offered.Choice.Success : offered.Choice.Failure;
        var text = EventText.Fill(outcome.Text, Campaign, pending);
        Campaign.ResolveEvent();
        Campaign.Record(ChronicleEntryKind.EventResolved, [
            new(ChronicleParameters.Event, definition.Id), new(ChronicleParameters.Choice, choiceId),
            new(ChronicleParameters.Outcome, succeeded ? "success" : "failure"),
        ], Involved(pending));
        foreach (var effect in outcome.Effects)
        {
            Apply(effect, pending);
        }

        return (CommandResult.Ok(), new EventResolution(EventText.Fill(definition.Title, Campaign, pending), offered.Choice.Text, succeeded, text));
    }

    private static CharacterId[] Involved(PendingEvent pending) =>
        new[] { pending.Subject, pending.Other }.Where(id => id is not null).Select(id => id!.Value).ToArray();

    private IEnumerable<Character> Resolve(string? who, PendingEvent pending)
    {
        var aboard = Campaign.Crew.Where(c => c.SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated);
        return who switch
        {
            "subject" when pending.Subject is { } id => [Campaign.Characters[id]],
            "other" when pending.Other is { } id => [Campaign.Characters[id]],
            "captain" when Campaign.CaptainId is { } id => [Campaign.Characters[id]],
            "crew" => aboard.Where(c => c.Id != Campaign.CaptainId).ToList(),
            "all" => aboard.ToList(),
            _ => [],
        };
    }

    private void Apply(EventEffect effect, PendingEvent pending)
    {
        var cause = Content.Events[pending.DefinitionId].Title.ToLowerInvariant().TrimEnd('!', '.', '?');
        if (effect.Item is { } item && effect.Amount != 0)
        {
            if (effect.Amount > 0)
            {
                Campaign.SharedStore.TryAdd(item, effect.Amount, Content.Items);
            }
            else
            {
                Campaign.SharedStore.TryRemove(item, Math.Min(-effect.Amount, Campaign.SharedStore.Count(item)));
            }
        }

        if (effect.Ship is { } ship && Campaign.Voyage is { Outcome: VoyageOutcome.UnderWay } voyage)
        {
            voyage.Apply(ship, 1);
        }

        if (effect.Leagues != 0 && Campaign.Voyage is { Outcome: VoyageOutcome.UnderWay } passage)
        {
            passage.LeaguesSailed = Math.Clamp(passage.LeaguesSailed + effect.Leagues, 0, passage.TotalLeagues);
            passage.Navigation?.Shift(effect.Leagues);
        }

        foreach (var character in Resolve(effect.Who, pending).ToList())
        {
            if (effect.Needs is { } needs)
            {
                character.Needs.Apply(needs, 1);
            }

            if (effect.Condition is { } condition)
            {
                character.Health.AddCondition(condition);
            }

            if (effect.Health > 0)
            {
                character.Health.Gain(effect.Health);
            }
            else if (effect.Health < 0)
            {
                LoseHealth(character, -effect.Health, pending.DefinitionId);
                CheckHealthThresholds(character, cause, []);
            }

            if (effect.Learn is { } learn)
            {
                var level = learn.Level ?? TruthOf(character, learn.Topic);
                if (learn.Source is Knowledge.KnowledgeSource.Observation or Knowledge.KnowledgeSource.Test
                    && (Content.Skills.ContainsKey(learn.Topic) || Content.Languages.ContainsKey(learn.Topic)))
                {
                    Witness(character, learn.Topic, level, learn.Source);
                }
                else
                {
                    Campaign.Learn(character.Id, learn.Topic, level, learn.Source);
                }
            }

            if (effect.Becomes is { } state && character.SurvivalState is not (SurvivalState.Dead or SurvivalState.Missing))
            {
                Campaign.InterruptTask(character.Id, InterruptReasons.Incapacitated, [new(ChronicleParameters.Cause, cause)]);
                Campaign.ChangeSurvivalState(character.Id, state, cause);
            }
        }

        if (effect.Relationship is { } change)
        {
            var targets = Resolve(change.To, pending).ToList();
            foreach (var holder in Resolve(change.From, pending).ToList())
            {
                foreach (var target in targets.Where(t => t.Id != holder.Id))
                {
                    Campaign.ChangeRelationship(holder.Id, target.Id, change.Dimension, change.Amount, cause);
                }
            }
        }

        if (effect.Rationing is { } rationing)
        {
            SetRationing(rationing);
        }

        if (effect.Grievance is { } grievance)
        {
            foreach (var character in Resolve(effect.Who ?? "crew", pending).ToList())
            {
                ApplyGrievance(character, grievance);
            }
        }

        if (effect.Promise is { } promise)
        {
            MakePromise(promise);
        }

        if (effect.SailPlan is { } plan)
        {
            SetSailPlan(plan);
        }

        if (effect.Order is { } order)
        {
            foreach (var character in Resolve(effect.Who, pending).Where(c => c.CanWork).ToList())
            {
                QueueTask(character.Id, order, Tasks.TaskPriority.Urgent);
            }
        }
    }

    // The truth about someone, for what an event shows of them: a skill or language level, a trait, or
    // a fact of their past (1 if it is theirs, else 0).
    private int TruthOf(Character character, string topic)
    {
        if (Content.Skills.ContainsKey(topic))
        {
            return character.Skills.Get(topic);
        }

        if (Content.Languages.ContainsKey(topic))
        {
            return character.Languages.Get(topic);
        }

        if (Content.Traits.ContainsKey(topic))
        {
            return character.Traits.Contains(topic) ? 1 : 0;
        }

        return PersonOf(character.Id) is { } person
               && (person.Facts.Any(f => f.Id == topic) || person.Secrets.Any(s => s.Id == topic)) ? 1 : 0;
    }

    /// <summary>
    /// Short rations: the crew eat and drink less often, so stores last longer, but everyone grows more
    /// stressed while it lasts (D-017).
    /// </summary>
    public CommandResult SetRationing(bool on)
    {
        if (Campaign.Rationing == on)
        {
            return CommandResult.Ok();
        }

        Campaign.Rationing = on;
        Campaign.Record(ChronicleEntryKind.RationingChanged, [new(ChronicleParameters.To, on ? "short" : "full")]);
        return CommandResult.Ok();
    }

    /// <summary>The waiting event's title and text with names filled in, or null if none is waiting.</summary>
    public (string Title, string Text)? DescribePendingEvent() =>
        Campaign.PendingEvent is { } pending && Content.Events.TryGetValue(pending.DefinitionId, out var definition)
            ? (EventText.Fill(definition.Title, Campaign, pending), EventText.Fill(definition.Text, Campaign, pending))
            : null;

    /// <summary>A chance of success in words, for choices: "likely", "an even chance", "a long shot".</summary>
    public static string ChanceInWords(double chance) => chance switch
    {
        >= 0.8 => "very likely",
        >= 0.62 => "likely",
        >= 0.42 => "an even chance",
        >= 0.25 => "unlikely",
        _ => "a long shot",
    };

}
