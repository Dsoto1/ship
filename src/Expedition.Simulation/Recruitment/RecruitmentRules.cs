using Expedition.Simulation.Characters;

namespace Expedition.Simulation.Recruitment;

/// <summary>
/// Hiring a crew before the voyage (V4, D-019), loaded from <c>data/recruitment.json</c>: the Company's
/// purse, the berths aboard, how much time there is to make inquiries, what the chandler sells, and the
/// people on the waterfront who want a berth.
/// </summary>
public sealed record RecruitmentRules
{
    /// <summary>Sueldos the Company advances for wages and stores.</summary>
    public required int Funds { get; init; }

    /// <summary>Most people who can be hired (berths aboard, besides the captain).</summary>
    public required int Berths { get; init; }

    /// <summary>Fewest hired hands the ship can sail with.</summary>
    public required int MinimumCrew { get; init; }

    /// <summary>Inquiries (asking around, tests) there is time for before the convoy sails.</summary>
    public required int Inquiries { get; init; }

    /// <summary>What the chandler sells, by the lot.</summary>
    public required IReadOnlyList<GoodsLot> Goods { get; init; }

    /// <summary>People looking for a berth.</summary>
    public required IReadOnlyList<CandidateDefinition> Candidates { get; init; }
}

/// <summary>A lot the chandler sells, for example a cask of 20 litres of water for 60 sueldos.</summary>
public sealed record GoodsLot
{
    /// <summary>Item ID.</summary>
    public required string Item { get; init; }

    /// <summary>Units in the lot.</summary>
    public required int Quantity { get; init; }

    /// <summary>Price in sueldos.</summary>
    public required int Price { get; init; }
}

/// <summary>
/// Someone who wants a berth. Their skills, attributes, traits and languages are the truth; their claims
/// and their reference are what the captain can learn, and either may be wrong (TECHNICAL_SPEC.md §5.2).
/// </summary>
public sealed record CandidateDefinition
{
    /// <summary>ID, for example <c>candidate.pilot</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Name.</summary>
    public required string Name { get; init; }

    /// <summary>Age in years.</summary>
    public required int Age { get; init; }

    /// <summary>Sex.</summary>
    public required Sex Sex { get; init; }

    /// <summary>Where they come from.</summary>
    public required string Origin { get; init; }

    /// <summary>Social standing.</summary>
    public required string Status { get; init; }

    /// <summary>Advance on wages asked, in sueldos. Negative means they pay for their passage.</summary>
    public required int Wage { get; init; }

    /// <summary>A line about who they seem to be, as anyone on the quay could see.</summary>
    public required string Look { get; init; }

    /// <summary>What they say for themselves when the captain talks to them.</summary>
    public required string Pitch { get; init; }

    /// <summary>True skills.</summary>
    public IReadOnlyDictionary<string, int> Skills { get; init; } = new Dictionary<string, int>();

    /// <summary>True attributes; unset means ordinary.</summary>
    public IReadOnlyDictionary<string, int> Attributes { get; init; } = new Dictionary<string, int>();

    /// <summary>True traits.</summary>
    public IReadOnlyList<string> Traits { get; init; } = [];

    /// <summary>True languages.</summary>
    public IReadOnlyDictionary<string, int> Languages { get; init; } = new Dictionary<string, int>();

    /// <summary>What they claim: skill, language or trait IDs with levels (traits 1 = has it).</summary>
    public IReadOnlyDictionary<string, int> Claims { get; init; } = new Dictionary<string, int>();

    /// <summary>What someone on the waterfront says about them.</summary>
    public required CandidateReference Reference { get; init; }
}

/// <summary>A reference: who gave it, what they said, and the levels it implies.</summary>
public sealed record CandidateReference
{
    /// <summary>Who speaks, for example "the harbour master".</summary>
    public required string Who { get; init; }

    /// <summary>What they say.</summary>
    public required string Text { get; init; }

    /// <summary>Levels the reference implies, for skills, languages or traits.</summary>
    public IReadOnlyDictionary<string, int> Levels { get; init; } = new Dictionary<string, int>();
}
