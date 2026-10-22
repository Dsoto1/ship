using System.Globalization;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.Port;

namespace Expedition.Simulation.World;

/// <summary>Something someone says to the captain of their own accord: a greeting, or coming up to him.</summary>
/// <param name="Who">Who says it.</param>
/// <param name="Text">What they say.</param>
/// <param name="Approach">True if they sought the captain out (a candidate, a creditor), not just greeted in passing.</param>
public sealed record WorldLine(CharacterId Who, string Text, bool Approach)
{
}

/// <summary>One beat of the departure (PORT_EXPERIENCE §17), from the expedition as it actually is.</summary>
/// <param name="Kind">
/// <c>deadline</c>, <c>crew</c>, <c>farewell</c>, <c>claim</c> (needs the captain's answer), <c>cargo</c>,
/// <c>sponsor</c>, <c>board</c>, <c>gangway</c>, <c>lines</c> or <c>away</c>.
/// </param>
/// <param name="Text">The line.</param>
/// <param name="Who">The person it is about, if any.</param>
public sealed record DepartureBeat(string Kind, string Text, CharacterId? Who = null)
{
    /// <summary>For a farewell: the port's people who have come to the quay for this hand.</summary>
    public IReadOnlyList<CharacterId> SeenOffBy { get; init; } = [];

    /// <summary>For a farewell: how many others in the crowd wave to them.</summary>
    public int Wavers { get; init; }
}

public sealed partial class SimulationRunner
{
    private readonly List<WorldLine> _lines = [];

    /// <summary>
    /// How well the port knows the expedition (R2e, PORT_EXPERIENCE §16): 0 not yet noticed, 1 noticed,
    /// 2 talked of, 3 known. From the hands signed on, the lots bought and the money spent. It shows in
    /// what people do and say; the interface describes it in words, never as a number.
    /// </summary>
    public int Standing
    {
        get
        {
            if (Content.Port is not { } rules || Campaign.Port is not { } port)
            {
                return 0;
            }

            var score = port.Hired.Count * 2 + port.Purchases.Count + Math.Max(0, rules.Funds - port.Funds) / 1000;
            var thresholds = rules.Recognition;
            return score >= thresholds.KnownAt ? 3 : score >= thresholds.TalkedOfAt ? 2 : score >= thresholds.NoticedAt ? 1 : 0;
        }
    }

    /// <summary>The expedition's standing in a line, for the Expedition panel.</summary>
    public string DescribeStanding() => Standing switch
    {
        0 => "The port has hardly noticed the Garza yet.",
        1 => "People have noticed the Garza; some nod to you in the street.",
        2 => "The expedition is talked of: people looking for a berth may come to you.",
        _ => "Everyone on the waterfront knows the Garza's captain.",
    };

    /// <summary>Lines said to the captain since the last call, oldest first: greetings, approaches, claims.</summary>
    public IReadOnlyList<WorldLine> TakeLines()
    {
        var lines = _lines.ToList();
        _lines.Clear();
        return lines;
    }

    /// <summary>A creditor's claim the captain has yet to answer, against someone signed on: creditor and claim.</summary>
    public IEnumerable<(CharacterId Creditor, CreditorClaim Claim)> OpenClaims()
    {
        if (Content.Port is not { } rules || Campaign.Port is not { } port)
        {
            yield break;
        }

        foreach (var person in rules.People.Where(p => p.Claim is not null))
        {
            if (CharacterFor(person.Id) is { } creditor && CharacterFor(person.Claim!.Debtor) is { } debtor
                && port.Hired.Contains(debtor.Id) && !port.Claims.ContainsKey(creditor.Id))
            {
                yield return (creditor.Id, person.Claim);
            }
        }

        foreach (var debt in StoreDebts(rules, port))
        {
            yield return debt;
        }
    }

    /// <summary>Pays a creditor what someone signed on owes him, from the purse.</summary>
    public CommandResult PayClaim(CharacterId creditor)
    {
        if (OpenClaims().FirstOrDefault(c => c.Creditor == creditor) is not { Claim: { } claim } || Campaign.Port is not { } port)
        {
            return CommandResult.Rejected("There is no claim to pay.");
        }

        if (port.Funds < claim.Amount)
        {
            return CommandResult.Rejected($"That is {claim.Amount} sueldos and only {port.Funds} are left.");
        }

        port.Funds -= claim.Amount;
        port.SettleClaim(creditor, true);
        if (claim.Debtor.Length == 0)
        {
            // A store debt: the account is settled.
            if (Content.Port?.Sellers.FirstOrDefault(s => CharacterFor(s.Person)?.Id == creditor) is { } seller)
            {
                port.ClearDebt(seller.Id);
            }

            if (Campaign.CaptainId is { } payer)
            {
                Campaign.ChangeRelationship(creditor, payer, RelationshipDimension.Respect, 5, "paid what the Garza owed");
            }

            return CommandResult.Ok();
        }

        if (Campaign.CaptainId is { } captain && CharacterFor(claim.Debtor) is { } debtor)
        {
            var creditorName = Campaign.Characters[creditor].Name;
            Campaign.ChangeRelationship(debtor.Id, captain, RelationshipDimension.Trust, 10, $"paid his debt to {creditorName}");
            Campaign.ChangeRelationship(creditor, captain, RelationshipDimension.Respect, 5, "paid what he was owed");
        }

        return CommandResult.Ok();
    }

    /// <summary>Refuses a creditor's claim: the debtor sails, and the creditor will not forget it.</summary>
    public CommandResult RefuseClaim(CharacterId creditor)
    {
        if (OpenClaims().FirstOrDefault(c => c.Creditor == creditor) is not { Claim: { } claim } || Campaign.Port is not { } port)
        {
            return CommandResult.Rejected("There is no claim to refuse.");
        }

        port.SettleClaim(creditor, false);
        if (claim.Debtor.Length == 0 && Campaign.CaptainId is { } owing)
        {
            Campaign.ChangeRelationship(creditor, owing, RelationshipDimension.Trust, -25, "sailed owing him for stores");
            return CommandResult.Ok();
        }

        if (Campaign.CaptainId is { } captain && CharacterFor(claim.Debtor) is { } debtor)
        {
            Campaign.ChangeRelationship(creditor, captain, RelationshipDimension.Trust, -15, $"refused to pay {debtor.Name}'s debt");
            Campaign.ChangeRelationship(debtor.Id, captain, RelationshipDimension.Affection, 5, $"stood by him against {Campaign.Characters[creditor].Name}");
        }

        return CommandResult.Ok();
    }

    // Once a minute in port: greetings in passing, candidates and creditors seeking the captain out.
    private void StepRecognition(PortState port, PortRules rules)
    {
        if (Campaign.CaptainId is not { } captain || !Campaign.Placements.TryGetValue(captain, out var here) || here.LocationId != rules.Location)
        {
            port.Approaching = null;
            return;
        }

        var tick = Campaign.Clock.Tick;
        var standing = Standing;

        // A creditor comes as soon as his debtor is signed on, whatever the expedition's standing.
        if (port.Approaching is null)
        {
            // (A seller the Garza owes for stores waits for her at the gangway instead.)
            foreach (var (creditor, claim) in OpenClaims())
            {
                if (claim.Debtor.Length > 0 && !port.Approached.Contains(creditor) && Available(creditor, rules))
                {
                    StartApproach(port, creditor);
                    break;
                }
            }
        }

        if (port.Approaching is null && standing >= 2 && tick % rules.Recognition.ApproachEveryMinutes == 30)
        {
            var captainCell = here.CellAt(tick);
            var seeker = Campaign.Characters.Values
                .Where(c => c.Membership == Membership.Local && PersonOf(c.Id)?.Seeking is not null)
                .Where(c => !port.TalkedTo.Contains(c.Id) && !port.Hired.Contains(c.Id) && !port.Gone.Contains(c.Id) && !port.Approached.Contains(c.Id))
                .Where(c => Available(c.Id, rules) && Distance(Campaign.Placements[c.Id].CellAt(tick), captainCell) <= rules.Recognition.ApproachRange)
                .OrderBy(c => c.Id)
                .FirstOrDefault();
            if (seeker is not null)
            {
                StartApproach(port, seeker.Id);
            }
        }

        StepApproach(port, rules);

        if (standing >= 1 && tick - port.LastGreeting >= 3)
        {
            Greet(port, rules, captain, here.CellAt(tick), standing);
        }
    }

    // Someone free to walk over: on the waterfront, out of doors, not under orders or asleep.
    private bool Available(CharacterId id, PortRules rules) =>
        Campaign.Characters[id].CanWork && Campaign.Placements.TryGetValue(id, out var placement) && placement.LocationId == rules.Location
        && !IsIndoors(id) && !(Campaign.ActiveTasks.TryGetValue(id, out var task) && (task.Origin == Tasks.TaskOrigin.Order || task.DefinitionId == "task.sleep_at_home"));

    private void StartApproach(PortState port, CharacterId id)
    {
        port.Approaching = id;
        port.ApproachingSince = Campaign.Clock.Tick;
        Campaign.AbandonTask(id);
        WalkTowardCaptain(id);
    }

    private void WalkTowardCaptain(CharacterId id)
    {
        if (Campaign.CaptainId is not { } captain || !Campaign.Placements.TryGetValue(captain, out var here))
        {
            return;
        }

        var location = Location(here.LocationId);
        var centre = here.CellAt(Campaign.Clock.Tick);
        var from = Campaign.Placements[id].CellAt(Campaign.Clock.Tick);
        var beside = new[] { new GridPosition(1, 0), new GridPosition(-1, 0), new GridPosition(0, 1), new GridPosition(0, -1), new GridPosition(1, 1), new GridPosition(-1, -1) }
            .Select(d => new GridPosition(centre.X + d.X, centre.Y + d.Y))
            .Where(location.IsWalkable)
            .OrderBy(c => Distance(c, from))
            .FirstOrDefault(centre);
        MoveCharacter(id, beside);
    }

    private void StepApproach(PortState port, PortRules rules)
    {
        if (port.Approaching is not { } id)
        {
            return;
        }

        var tick = Campaign.Clock.Tick;
        if (WhyCannotSpeak(id) is null)
        {
            Arrive(port, rules, id);
        }
        else if (tick - port.ApproachingSince > 120 || !Campaign.Characters[id].CanWork)
        {
            // The captain could not be found; they give it up.
            port.MarkApproached(id);
            port.Approaching = null;
        }
        else if (tick % 5 == 0)
        {
            WalkTowardCaptain(id);
        }
    }

    private void Arrive(PortState port, PortRules rules, CharacterId id)
    {
        port.MarkApproached(id);
        port.Approaching = null;
        var character = Campaign.Characters[id];
        if (PersonOf(id) is { Claim: { } claim } && OpenClaims().Any(c => c.Creditor == id))
        {
            _lines.Add(new WorldLine(id, $"{character.Name} plants himself in front of you. {Fill(claim.Says, rules)}", true));
            return;
        }

        var (_, said) = TalkTo(id);
        _lines.Add(new WorldLine(id, $"{character.Name} comes up to you: \"You're the Garza's captain?\" {said}", true));
    }

    private void Greet(PortState port, PortRules rules, CharacterId captain, GridPosition captainCell, int standing)
    {
        var tick = Campaign.Clock.Tick;
        var day = (int)(tick / SimulationClock.TicksPerDay);
        foreach (var local in Campaign.Characters.Values.Where(c => c.Membership == Membership.Local).OrderBy(c => c.Id))
        {
            if (local.Id == port.Approaching || port.Greeted.Contains((local.Id, day)) || PersonOf(local.Id) is not { } person
                || (person.Seeking is not null && !port.Hired.Contains(local.Id)) || !Campaign.Placements.TryGetValue(local.Id, out var there)
                || there.LocationId != rules.Location || Distance(there.CellAt(tick), captainCell) > 3 || !CaptainSees(local.Id))
            {
                continue;
            }

            var lines = (person.Greetings.Count > 0 ? person.Greetings : rules.Recognition.Greetings).Where(g => g.Standing <= standing).ToList();
            if (lines.Count == 0)
            {
                continue;
            }

            // The most knowing line they have for how well the expedition is known, varied by day.
            var best = lines.Max(g => g.Standing);
            var choice = lines.Where(g => g.Standing == best).ToList();
            var line = choice[(local.Id.Value + day) % choice.Count];
            port.MarkGreeted(local.Id, day);
            port.LastGreeting = tick;
            _lines.Add(new WorldLine(local.Id, $"{local.Name}: {Fill(line.Text, rules)}", false));
            return;
        }
    }

    private string Fill(string text, PortRules rules)
    {
        var port = Campaign.Port!;
        var hours = Math.Max(0, (rules.Departure.Tick - Campaign.Clock.Tick) / SimulationClock.TicksPerHour);
        return text
            .Replace("{ship}", "the Garza", StringComparison.Ordinal)
            .Replace("{crew}", port.Hired.Count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{hours}", hours.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    /// <summary>
    /// The departure as it will be (PORT_EXPERIENCE §17), from the expedition as it is: the hour against
    /// the convoy, the crew signed on, who has come to see each of them off, any creditor at the
    /// gangway, the last stores and what is in the hold, the Company's clearance, then the lines cast
    /// off. Nothing here changes the simulation; a <c>claim</c> beat needs <see cref="PayClaim"/> or
    /// <see cref="RefuseClaim"/> before sailing.
    /// </summary>
    public IReadOnlyList<DepartureBeat> DeparturePlan()
    {
        var beats = new List<DepartureBeat>();
        if (Content.Port is not { } rules || Campaign.Port is not { } port)
        {
            return beats;
        }

        var tick = Campaign.Clock.Tick;
        var hours = (rules.Departure.Tick - tick) / (double)SimulationClock.TicksPerHour;
        beats.Add(new DepartureBeat("deadline", hours > 1
            ? $"{Math.Floor(hours):0} hours before the convoy, the Garza makes ready to sail ahead of it."
            : hours > -1
                ? "Dawn: beyond the bar the convoy is making sail, and the Garza goes with it."
                : $"The convoy sailed {Math.Floor(-hours):0} hours ago. The Garza follows it alone."));

        var crew = port.Hired.Select(id => Campaign.Characters[id]).OrderBy(c => c.Id).ToList();
        beats.Add(new DepartureBeat("crew", crew.Count > 0 ? $"Aboard and waiting: {Join(crew.Select(c => c.Name).ToList())}." : "No crew are aboard."));
        var alone = 0;
        foreach (var hand in crew)
        {
            if (hand.SurvivalState != SurvivalState.Alive)
            {
                beats.Add(new DepartureBeat("farewell", $"{hand.Name} is carried aboard.", hand.Id));
            }
            else if (PersonOf(hand.Id)?.Farewell is { } farewell)
            {
                beats.Add(new DepartureBeat("farewell", farewell.Replace("{name}", hand.Name, StringComparison.Ordinal), hand.Id)
                {
                    SeenOffBy = (PersonOf(hand.Id)?.FarewellBy ?? []).Select(CharacterFor).OfType<Characters.Character>()
                        .Where(c => Campaign.Placements.ContainsKey(c.Id) && !port.Hired.Contains(c.Id)).Select(c => c.Id).ToList(),
                    Wavers = PersonOf(hand.Id)?.FarewellWavers ?? 0,
                });
            }
            else
            {
                beats.Add(new DepartureBeat("farewell", alone++ % 2 == 0
                    ? $"No one has come down to see {hand.Name} off."
                    : $"{hand.Name} looks back once at the town, and then not again.", hand.Id));
            }
        }

        foreach (var (creditor, claim) in OpenClaims())
        {
            beats.Add(new DepartureBeat("claim", $"{Campaign.Characters[creditor].Name}, at the foot of the gangway: {Fill(claim.AtGangway, rules)}", creditor));
        }

        if (ShipPreparation.Of(Campaign, Content) is { } preparation)
        {
            if (preparation.OnQuay.Count > 0)
            {
                beats.Add(new DepartureBeat("cargo", $"The last of the stores go up the gangway: {Stacks(preparation.OnQuay)}."));
            }

            beats.Add(new DepartureBeat("cargo", preparation.Aboard.Count > 0 ? $"In her hold: {Stacks(preparation.Aboard)}." : "Her hold is all but empty."));
        }

        var sponsor = rules.People.FirstOrDefault(p => p.Id == "person.varzi");
        var factor = sponsor is null ? null : CharacterFor(sponsor.Id);
        var inPerson = port.LateFeesCharged == 0 && factor is not null && Campaign.Placements.ContainsKey(factor.Id);
        beats.Add(new DepartureBeat("sponsor", port.LateFeesCharged > 0
            ? $"A clerk brings a letter from {sponsor?.Name ?? "the factor"}: the Company has paid {port.LateFeesCharged * rules.LateFeePerHalfDay} sueldos in harbour dues for the delay, and will remember it."
            : $"{sponsor?.Name ?? "The factor"} signs the clearance for the Company and wishes you a fair wind.", inPerson ? factor!.Id : null)
        {
            // He comes down to the gangway himself to sign.
            SeenOffBy = inPerson ? [factor!.Id] : [],
        });
        beats.Add(new DepartureBeat("board", "You go up the gangway last."));
        beats.Add(new DepartureBeat("gangway", "The gangway comes in."));
        beats.Add(new DepartureBeat("lines", "Bow and stern lines are cast off; the boats tow her head round to the bar."));
        beats.Add(new DepartureBeat("away", "The Garza gathers way. La Bocana falls astern."));
        return beats;
    }

    private string Stacks(IReadOnlyList<CargoPackages> stacks) => stacks.Count == 0 ? "nothing" :
        Join(stacks.Take(6).Select(s => $"{s.Count} {(s.Count == 1 ? s.Package : s.Package + "s")} of {Content.Items[s.Item].Name.Split(" (")[0].ToLowerInvariant()}").ToList())
        + (stacks.Count > 6 ? " and more" : "");
}
