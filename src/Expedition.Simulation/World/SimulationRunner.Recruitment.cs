using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Knowledge;
using Expedition.Simulation.Recruitment;

namespace Expedition.Simulation.World;

/// <summary>Hiring a crew in port (V4, D-019).</summary>
public sealed partial class SimulationRunner
{
    /// <summary>Opens hiring: the expedition must be in port with recruitment rules and not already hiring.</summary>
    public CommandResult BeginRecruitment()
    {
        if (Content.Recruitment is not { } rules)
        {
            return CommandResult.Rejected("The game content has no recruitment rules (data/recruitment.json).");
        }

        if (Campaign.Phase != CampaignPhase.PortPreparation || Campaign.Recruitment is not null)
        {
            return CommandResult.Rejected("Hiring happens once, in port.");
        }

        Campaign.SetRecruitment(new RecruitmentState { Funds = rules.Funds, InquiriesLeft = rules.Inquiries });
        return CommandResult.Ok();
    }

    /// <summary>Candidates still looking for a berth: not hired and not gone.</summary>
    public IReadOnlyList<CandidateDefinition> AvailableCandidates() =>
        Content.Recruitment is { } rules && Campaign.Recruitment is { } state
            ? rules.Candidates.Where(c => !state.Hired.ContainsKey(c.Id) && !state.Gone.Contains(c.Id)).ToList()
            : [];

    /// <summary>What a candidate asks now, after any haggling.</summary>
    public int AskingPrice(CandidateDefinition candidate) =>
        Campaign.Recruitment?.Prices.TryGetValue(candidate.Id, out var price) == true ? price : candidate.Wage;

    /// <summary>Talks to a candidate: free, and reveals what they claim about themselves.</summary>
    public CommandResult TalkTo(string candidateId)
    {
        if (Lookup(candidateId, out var candidate, out var state) is { } refused)
        {
            return refused;
        }

        state.MarkTalked(candidate.Id);
        foreach (var (topic, level) in candidate.Claims.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            state.Learn(new CandidateFact(candidate.Id, topic, level, KnowledgeSource.Claim));
        }

        return CommandResult.Ok();
    }

    /// <summary>Asks around about a candidate: one inquiry, and reveals what someone says of them.</summary>
    public CommandResult AskAbout(string candidateId)
    {
        if (Lookup(candidateId, out var candidate, out var state) is { } refused)
        {
            return refused;
        }

        if (state.AskedAbout.Contains(candidate.Id))
        {
            return CommandResult.Rejected($"You have already asked about {candidate.Name}.");
        }

        if (state.InquiriesLeft <= 0)
        {
            return CommandResult.Rejected("There is no time left for inquiries before the convoy sails.");
        }

        state.InquiriesLeft--;
        state.MarkAsked(candidate.Id);
        foreach (var (topic, level) in candidate.Reference.Levels.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            state.Learn(new CandidateFact(candidate.Id, topic, level, KnowledgeSource.Reference));
        }

        return CommandResult.Ok();
    }

    /// <summary>
    /// Puts a candidate to the test in one skill or language: one inquiry, and shows their true level.
    /// The proud take it as an insult and refuse (the time is spent all the same).
    /// </summary>
    public CommandResult TestCandidate(string candidateId, string topic)
    {
        if (Lookup(candidateId, out var candidate, out var state) is { } refused)
        {
            return refused;
        }

        if (!Content.Skills.ContainsKey(topic) && !Content.Languages.ContainsKey(topic))
        {
            return CommandResult.Rejected($"'{topic}' is not a skill or language that can be tested.");
        }

        if (state.InquiriesLeft <= 0)
        {
            return CommandResult.Rejected("There is no time left for inquiries before the convoy sails.");
        }

        state.InquiriesLeft--;
        if (candidate.Traits.Contains("trait.proud"))
        {
            state.MarkRefused(candidate.Id);
            return CommandResult.Rejected($"{candidate.Name} refuses to be tested like an apprentice, and is offended you asked.");
        }

        var level = candidate.Skills.TryGetValue(topic, out var skill) ? skill : candidate.Languages.GetValueOrDefault(topic);
        state.Learn(new CandidateFact(candidate.Id, topic, level, KnowledgeSource.Test));
        return CommandResult.Ok();
    }

    /// <summary>
    /// Haggles once over the advance. Success takes a fifth off; failure raises the price a tenth, and the
    /// proud walk away. The chance depends on the captain's negotiation.
    /// </summary>
    public CommandResult Haggle(string candidateId)
    {
        if (Lookup(candidateId, out var candidate, out var state) is { } refused)
        {
            return refused;
        }

        var price = AskingPrice(candidate);
        if (state.Haggled.Contains(candidate.Id) || price <= 0)
        {
            return CommandResult.Rejected(price <= 0 ? $"{candidate.Name} is paying for the passage; there is nothing to haggle over." : $"{candidate.Name} has named a final price.");
        }

        state.MarkHaggled(candidate.Id);
        var negotiation = Campaign.CaptainId is { } captain ? Campaign.Characters[captain].Skills.Get("skill.negotiation") : 0;
        if (Campaign.Randomness.Events.Chance(Math.Clamp(0.35 + negotiation * 0.1, 0.05, 0.9)))
        {
            state.SetPrice(candidate.Id, RoundToTen(price * 0.8));
            return CommandResult.Ok();
        }

        if (candidate.Traits.Contains("trait.proud"))
        {
            state.MarkGone(candidate.Id);
            return CommandResult.Rejected($"{candidate.Name} will not be haggled over like a fish, and walks away.");
        }

        state.SetPrice(candidate.Id, RoundToTen(price * 1.1));
        return CommandResult.Rejected($"{candidate.Name} is insulted and now asks {RoundToTen(price * 1.1)} sueldos.");
    }

    private static int RoundToTen(double value) => (int)(Math.Round(value / 10) * 10);

    /// <summary>
    /// Hires a candidate: pays the advance, gives them a berth, puts them on deck with duties to suit their
    /// skills, and carries over everything learned about them into the captain's knowledge.
    /// </summary>
    public CommandResult Hire(string candidateId)
    {
        if (Lookup(candidateId, out var candidate, out var state) is { } refused)
        {
            return refused;
        }

        var rules = Content.Recruitment!;
        var price = AskingPrice(candidate);
        if (state.Hired.Count >= rules.Berths)
        {
            return CommandResult.Rejected($"Every berth is taken ({rules.Berths}).");
        }

        if (state.Funds < price)
        {
            return CommandResult.Rejected($"{candidate.Name} asks {price} sueldos and only {state.Funds} are left.");
        }

        if (Campaign.CaptainId is not { } captain || !Campaign.Placements.TryGetValue(captain, out var captainPlace))
        {
            return CommandResult.Rejected("There is no captain aboard to sign the articles.");
        }

        var character = Campaign.CreateCharacter(candidate.Name, candidate.Age, candidate.Origin, Content.Rules.PersonalCarryCapacity);
        character.Sex = candidate.Sex;
        character.StatusId = candidate.Status;
        foreach (var (skill, level) in candidate.Skills) character.Skills.Set(skill, level);
        foreach (var (attribute, level) in candidate.Attributes) character.Attributes.Set(attribute, level);
        foreach (var (language, level) in candidate.Languages) character.Languages.Set(language, level);
        foreach (var trait in candidate.Traits) character.AddTrait(trait);
        character.Appearance = AppearanceGenerator.Generate(character, Content.Appearance, Campaign.Randomness.Characters);

        var location = Content.Locations[captainPlace.LocationId];
        var cell = FreeCell(location);
        PlaceCharacter(character.Id, location.Id, cell);
        state.Funds -= price;
        state.MarkHired(candidate.Id, character.Id);

        foreach (var fact in state.Facts.Where(f => f.Candidate == candidate.Id))
        {
            Campaign.Learn(character.Id, fact.Topic, fact.Level, fact.Source);
        }

        if (state.Haggled.Contains(candidate.Id) && price < candidate.Wage)
        {
            Campaign.ChangeRelationship(character.Id, captain, RelationshipDimension.Affection, -8, "beaten down over the advance");
        }

        SetDuties(character.Id, DutiesFor(character));
        Campaign.Record(ChronicleEntryKind.CrewHired, [new(ChronicleParameters.Level, price.ToString(System.Globalization.CultureInfo.InvariantCulture))], character.Id);
        return CommandResult.Ok();
    }

    /// <summary>Buys a lot from the chandler into the shared store.</summary>
    public CommandResult Buy(int lot)
    {
        if (Content.Recruitment is not { } rules || Campaign.Recruitment is not { } state)
        {
            return CommandResult.Rejected("The chandler is in port; the ship has sailed.");
        }

        if (lot < 0 || lot >= rules.Goods.Count)
        {
            return CommandResult.Rejected("The chandler does not sell that.");
        }

        var goods = rules.Goods[lot];
        if (state.Funds < goods.Price)
        {
            return CommandResult.Rejected($"That costs {goods.Price} sueldos and only {state.Funds} are left.");
        }

        var stowed = Campaign.SharedStore.TryAdd(goods.Item, goods.Quantity, Content.Items);
        if (!stowed.Succeeded)
        {
            return stowed;
        }

        state.Funds -= goods.Price;
        return CommandResult.Ok();
    }

    /// <summary>
    /// Sails with the convoy: needs the minimum crew. Closes hiring, moves the expedition to the voyage
    /// phase and starts the passage.
    /// </summary>
    public CommandResult SetSail(string shipName, string destination, double leagues)
    {
        if (Content.Recruitment is not { } rules || Campaign.Recruitment is not { } state)
        {
            return CommandResult.Rejected("The ship is not in port taking on crew.");
        }

        if (state.Hired.Count < rules.MinimumCrew)
        {
            return CommandResult.Rejected($"{Capital(shipName)} needs at least {rules.MinimumCrew} hands to sail; {state.Hired.Count} are signed on.");
        }

        var moved = Campaign.TryTransitionTo(CampaignPhase.Voyage, "sailed with the convoy from La Bocana");
        if (!moved.Succeeded)
        {
            return moved;
        }

        Campaign.SetRecruitment(null);
        return StartVoyage(shipName, destination, leagues);
    }

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private CommandResult? Lookup(string candidateId, out CandidateDefinition candidate, out RecruitmentState state)
    {
        candidate = null!;
        state = null!;
        if (Campaign.Recruitment is not { } open || Content.Recruitment is not { } rules)
        {
            return CommandResult.Rejected("Hiring is over: the ship has sailed.");
        }

        state = open;
        if (rules.Candidates.FirstOrDefault(c => c.Id == candidateId) is not { } found)
        {
            return CommandResult.Rejected($"No one called '{candidateId}' is looking for a berth.");
        }

        candidate = found;
        if (open.Hired.ContainsKey(found.Id))
        {
            return CommandResult.Rejected($"{found.Name} has already signed on.");
        }

        return open.Gone.Contains(found.Id) ? CommandResult.Rejected($"{found.Name} has gone.") : null;
    }

    // The first free walkable cell, searching outward from the middle of the deck.
    private GridPosition FreeCell(LocationDefinition location)
    {
        var taken = Campaign.Placements.Values.Where(p => p.LocationId == location.Id).Select(p => p.Cell).ToHashSet();
        var spots = location.Spots.Select(s => s.Cell).ToHashSet();
        var middle = new GridPosition(location.Width / 2, location.Height / 2);
        return Enumerable.Range(0, location.Height)
            .SelectMany(y => Enumerable.Range(0, location.Width).Select(x => new GridPosition(x, y)))
            .Where(cell => location.IsWalkable(cell) && !taken.Contains(cell) && !spots.Contains(cell))
            .OrderBy(cell => Math.Abs(cell.X - middle.X) + Math.Abs(cell.Y - middle.Y))
            .ThenBy(cell => cell.Y).ThenBy(cell => cell.X)
            .First();
    }

    /// <summary>
    /// Standing duties to suit someone's skills: their trade first, then the pumps and the watch that
    /// everyone shares. Tasks they cannot do are left out.
    /// </summary>
    public IReadOnlyList<string> DutiesFor(Character character)
    {
        var duties = new List<string>();
        if (character.Skills.Get("skill.ship_repair") >= 1) duties.Add("task.patch_hull");
        if (character.Skills.Get("skill.rigging") >= 1) duties.Add("task.repair_rigging");
        if (character.Skills.Get("skill.cooking") >= 2) duties.Add("task.cook");
        duties.AddRange(["task.pump_bilge", "task.stand_watch"]);
        return duties.Where(id => Content.Tasks.TryGetValue(id, out var task)
                && (task.Skill is null || character.Skills.Get(task.Skill) >= task.MinSkill))
            .ToList();
    }
}
