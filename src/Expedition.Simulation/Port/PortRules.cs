using Expedition.Simulation.Characters;

namespace Expedition.Simulation.Port;

/// <summary>
/// The waterfront where the expedition prepares (R1, D-022), loaded from <c>data/port</c>: its people with
/// their lives and schedules, the purse, the berths, the chandler, and the convoy's departure.
/// </summary>
public sealed record PortRules
{
    /// <summary>Location of the waterfront.</summary>
    public required string Location { get; init; }

    /// <summary>Location of the ship's deck.</summary>
    public required string ShipLocation { get; init; }

    /// <summary>Spot where people go aboard.</summary>
    public required string Gangway { get; init; }

    /// <summary>Sueldos the Company advances.</summary>
    public required int Funds { get; init; }

    /// <summary>Most people who can be hired.</summary>
    public required int Berths { get; init; }

    /// <summary>Fewest hired hands to sail with.</summary>
    public required int MinimumCrew { get; init; }

    /// <summary>When the convoy sails.</summary>
    public required DayAndHour Departure { get; init; }

    /// <summary>Harbour fees and the sponsor's displeasure, per half day the ship stays after the convoy.</summary>
    public required int LateFeePerHalfDay { get; init; }

    /// <summary>What the chandler sells, and how fast it sells to others.</summary>
    public required IReadOnlyList<PortGoods> Goods { get; init; }

    /// <summary>The spot where the Chamber posts its notices; read there for leads.</summary>
    public string? NoticeBoard { get; init; }

    /// <summary>Notices on the Chamber's board: seamen seeking berths, passages wanted, pardons offered.</summary>
    public IReadOnlyList<LeadDefinition> Notices { get; init; } = [];

    /// <summary>Every lead in the port, from people and the notice board, by ID.</summary>
    public IEnumerable<(LeadDefinition Lead, string? From)> AllLeads() =>
        People.SelectMany(p => p.Leads.Select(l => (l, (string?)p.Id))).Concat(Notices.Select(n => (n, (string?)null)));

    /// <summary>Everyone on the waterfront.</summary>
    public required IReadOnlyList<PersonDefinition> People { get; init; }
}

/// <summary>A day of the campaign (1 is the first) and an hour.</summary>
public sealed record DayAndHour
{
    /// <summary>Day, from 1.</summary>
    public required int Day { get; init; }

    /// <summary>Hour, 0..23.</summary>
    public required int Hour { get; init; }

    /// <summary>As a simulation tick.</summary>
    public long Tick => (Day - 1L) * World.SimulationClock.TicksPerDay + Hour * World.SimulationClock.TicksPerHour;
}

/// <summary>A chandler's lot with a limited stock that others buy up over time.</summary>
public sealed record PortGoods
{
    /// <summary>Item ID.</summary>
    public required string Item { get; init; }

    /// <summary>Units per lot.</summary>
    public required int Quantity { get; init; }

    /// <summary>Price per lot in sueldos.</summary>
    public required int Price { get; init; }

    /// <summary>Lots in stock at the start.</summary>
    public required int Stock { get; init; }

    /// <summary>Lots other customers buy each day.</summary>
    public int SoldPerDay { get; init; }
}

/// <summary>
/// Someone on the waterfront. The skills, attributes, traits and languages are the truth. They live by
/// their schedule; if <see cref="Seeking"/> is set they want a berth.
/// </summary>
public sealed record PersonDefinition
{
    /// <summary>ID, for example <c>person.velho</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Name.</summary>
    public required string Name { get; init; }

    /// <summary>Age.</summary>
    public required int Age { get; init; }

    /// <summary>Sex.</summary>
    public required Sex Sex { get; init; }

    /// <summary>Where they come from.</summary>
    public required string Origin { get; init; }

    /// <summary>Social standing.</summary>
    public required string Status { get; init; }

    /// <summary>What they do, as anyone could see.</summary>
    public required string Occupation { get; init; }

    /// <summary>How they look, in a line.</summary>
    public required string Look { get; init; }

    /// <summary>Spot where they sleep.</summary>
    public required string Home { get; init; }

    /// <summary>Their day, as hour blocks.</summary>
    public required IReadOnlyList<ScheduleBlock> Schedule { get; init; }

    /// <summary>True skills.</summary>
    public IReadOnlyDictionary<string, int> Skills { get; init; } = new Dictionary<string, int>();

    /// <summary>True attributes.</summary>
    public IReadOnlyDictionary<string, int> Attributes { get; init; } = new Dictionary<string, int>();

    /// <summary>True traits.</summary>
    public IReadOnlyList<string> Traits { get; init; } = [];

    /// <summary>True languages.</summary>
    public IReadOnlyDictionary<string, int> Languages { get; init; } = new Dictionary<string, int>();

    /// <summary>What they will say about others when asked.</summary>
    public IReadOnlyList<PersonReference> Knows { get; init; } = [];

    /// <summary>Things about them that can be seen, not told.</summary>
    public IReadOnlyList<PersonSecret> Secrets { get; init; } = [];

    /// <summary>Their terms if they want a berth, or null.</summary>
    public BerthTerms? Seeking { get; init; }

    /// <summary>Who they will point the captain to when talked with (PORT_EXPERIENCE §9).</summary>
    public IReadOnlyList<LeadDefinition> Leads { get; init; } = [];
}

/// <summary>
/// Word of someone worth finding: "There's a Lusaran pilot between ships", and where they are said to
/// be. Given by a person when talked with, or posted on the Chamber's notice board.
/// </summary>
public sealed record LeadDefinition
{
    /// <summary>ID, unique across the port, for example <c>lead.ines_velho</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The person it points to.</summary>
    public required string About { get; init; }

    /// <summary>What is said.</summary>
    public required string Text { get; init; }

    /// <summary>Where they are said to be found. Hearsay: it may be out of date.</summary>
    public required string Where { get; init; }
}

/// <summary>An hour block of someone's day: [from, to), wrapping past midnight.</summary>
public sealed record ScheduleBlock
{
    /// <summary>First hour.</summary>
    public required int From { get; init; }

    /// <summary>Hour it ends (exclusive).</summary>
    public required int To { get; init; }

    /// <summary>What they do.</summary>
    public required string Task { get; init; }

    /// <summary>Where they prefer to do it, or null for the nearest free place.</summary>
    public string? Spot { get; init; }

    /// <summary>Whether <paramref name="hour"/> falls in this block.</summary>
    public bool Contains(int hour) => From < To ? hour >= From && hour < To : hour >= From || hour < To;
}

/// <summary>What someone says about another person.</summary>
public sealed record PersonReference
{
    /// <summary>Person ID it is about.</summary>
    public required string About { get; init; }

    /// <summary>What they say.</summary>
    public required string Text { get; init; }

    /// <summary>Levels it implies (skills, languages, traits).</summary>
    public IReadOnlyDictionary<string, int> Levels { get; init; } = new Dictionary<string, int>();

    /// <summary>True for gossip rather than first-hand knowledge.</summary>
    public bool Rumour { get; init; }
}

/// <summary>Something about a person the captain can notice by watching.</summary>
public sealed record PersonSecret
{
    /// <summary>Fact ID, also the knowledge topic.</summary>
    public required string Id { get; init; }

    /// <summary>What the captain notices.</summary>
    public required string Text { get; init; }

    /// <summary>Visible while they do this task.</summary>
    public required string During { get; init; }

    /// <summary>Only when this person is busy close by, for example a creditor; or null.</summary>
    public string? WithPerson { get; init; }
}

/// <summary>What someone looking for a berth wants, and what will put them off.</summary>
public sealed record BerthTerms
{
    /// <summary>Advance they ask (negative: they pay for the passage).</summary>
    public required int Asks { get; init; }

    /// <summary>The least they will take (hidden); for passengers, the least they will pay.</summary>
    public required int Lowest { get; init; }

    /// <summary>What they say for themselves.</summary>
    public required string Pitch { get; init; }

    /// <summary>What they claim: topic to level.</summary>
    public IReadOnlyDictionary<string, int> Claims { get; init; } = new Dictionary<string, int>();

    /// <summary>When they give up and go elsewhere, if not hired; or null.</summary>
    public Departure? Leaves { get; init; }

    /// <summary>People they will not sail with.</summary>
    public IReadOnlyList<Aversion> WontSailWith { get; init; } = [];
}

/// <summary>When and why someone stops waiting for a berth.</summary>
public sealed record Departure
{
    /// <summary>Day, from 1.</summary>
    public required int Day { get; init; }

    /// <summary>Hour.</summary>
    public required int Hour { get; init; }

    /// <summary>What became of them.</summary>
    public required string Reason { get; init; }

    /// <summary>As a simulation tick.</summary>
    public long Tick => (Day - 1L) * World.SimulationClock.TicksPerDay + Hour * World.SimulationClock.TicksPerHour;
}

/// <summary>Someone a candidate will not sail with, and what they say about it.</summary>
public sealed record Aversion
{
    /// <summary>Person ID.</summary>
    public required string Person { get; init; }

    /// <summary>Their words.</summary>
    public required string Reason { get; init; }
}
