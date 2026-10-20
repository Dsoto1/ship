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

    /// <summary>How prices move.</summary>
    public PortPrices Prices { get; init; } = new();

    /// <summary>Who sells stores; the first is the chandler, whose lots name no seller.</summary>
    public IReadOnlyList<PortSeller> Sellers { get; init; } = [];

    /// <summary>The seller of a lot.</summary>
    public PortSeller? SellerOf(PortGoods goods) =>
        goods.Seller is { } id ? Sellers.FirstOrDefault(s => s.Id == id) : Sellers.FirstOrDefault();

    /// <summary>The qualities a lot may have.</summary>
    public static readonly IReadOnlyList<string> Qualities = ["good", "fair", "poor"];

    /// <summary>The spot where the Chamber posts its notices; read there for leads.</summary>
    public string? NoticeBoard { get; init; }

    /// <summary>Notices on the Chamber's board: seamen seeking berths, passages wanted, pardons offered.</summary>
    public IReadOnlyList<LeadDefinition> Notices { get; init; } = [];

    /// <summary>Every lead in the port, from people and the notice board, by ID.</summary>
    public IEnumerable<(LeadDefinition Lead, string? From)> AllLeads() =>
        People.SelectMany(p => p.Leads.Select(l => (l, (string?)p.Id))).Concat(Notices.Select(n => (n, (string?)null)));

    /// <summary>How the ship's preparation shows: when stores are carried aboard, and the stages.</summary>
    public PreparationRules Preparation { get; init; } = new();

    /// <summary>The port's weather (R2d), or null for always fair.</summary>
    public PortWeatherRules? Weather { get; init; }

    /// <summary>Small scenes the port plays out around the player, with no reward (PORT_EXPERIENCE §12).</summary>
    public IReadOnlyList<AmbientScene> Ambient { get; init; } = [];

    /// <summary>How the port comes to know the expedition (R2e, PORT_EXPERIENCE §16).</summary>
    public RecognitionRules Recognition { get; init; } = new();

    /// <summary>Everyone on the waterfront.</summary>
    public required IReadOnlyList<PersonDefinition> People { get; init; }
}

/// <summary>
/// Thresholds for the ship's preparation stages (PORT_EXPERIENCE §5) and how long bought stores wait on
/// the quay before they are aboard. Placeholders to tune.
/// </summary>
public sealed record PreparationRules
{
    /// <summary>Hours a bought lot stands on the quay; it goes aboard steadily over that time.</summary>
    public double LoadingHours { get; init; } = 3;

    /// <summary>Hours stores lie on deck once aboard, before they are struck down into the hold.</summary>
    public double StowingHours { get; init; } = 5;

    /// <summary>Lots bought, with half the crew signed on, for the expedition to be taking shape.</summary>
    public int TakingShapeLots { get; init; } = 3;

    /// <summary>Lots bought, with the minimum crew, for her to be ready.</summary>
    public int ReadyLots { get; init; } = 6;

    /// <summary>Hours before the convoy sails that departure morning begins.</summary>
    public double DepartureMorningHours { get; init; } = 3;
}

/// <summary>
/// The port's weather (R2d, PORT_EXPERIENCE §14): kinds of weather, each lasting a while and followed by
/// another, chosen by weights from those that can begin at that hour (fog only in the morning).
/// </summary>
public sealed record PortWeatherRules
{
    /// <summary>The weather when the port opens.</summary>
    public required string Start { get; init; }

    /// <summary>Every kind of weather.</summary>
    public required IReadOnlyList<PortWeatherKind> Kinds { get; init; }

    /// <summary>A kind by ID, or null.</summary>
    public PortWeatherKind? Find(string id) => Kinds.FirstOrDefault(k => k.Id == id);
}

/// <summary>A kind of port weather and what it does.</summary>
public sealed record PortWeatherKind
{
    /// <summary>ID, for example <c>weather.rain</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Name, for the status chip.</summary>
    public required string Name { get; init; }

    /// <summary>A line when it sets in, for the player.</summary>
    public required string Says { get; init; }

    /// <summary>Shortest and longest it lasts, in hours.</summary>
    public required double MinHours { get; init; }

    /// <summary>Longest it lasts, in hours.</summary>
    public required double MaxHours { get; init; }

    /// <summary>Hour from which it can begin (inclusive), and before which (exclusive); 0 and 24 for any time.</summary>
    public int FromHour { get; init; }

    /// <summary>Hour before which it can begin.</summary>
    public int ToHour { get; init; } = 24;

    /// <summary>What may follow, by weight.</summary>
    public required IReadOnlyDictionary<string, int> Next { get; init; }

    /// <summary>0 dry .. 1 heavy rain. From 0.5 people caught outside take shelter.</summary>
    public double Rain { get; init; }

    /// <summary>0 clear .. 1 thick fog.</summary>
    public double Fog { get; init; }

    /// <summary>0 still .. 1 a strong breeze.</summary>
    public double Wind { get; init; }

    /// <summary>0 bright .. 1 dark sky.</summary>
    public double Gloom { get; init; }

    /// <summary>How far one can see, as a share of the usual: fog shortens it.</summary>
    public double Sight { get; init; } = 1;

    /// <summary>True if it rains hard enough to send people to shelter.</summary>
    public bool Raining => Rain >= 0.5;
}

/// <summary>
/// A small scene the port plays out (PORT_EXPERIENCE §12): sailors arguing, a porter dropping a sack, a
/// procession. Presentation only, with no reward; it says when and where it can happen and in what
/// weather, and the game draws it.
/// </summary>
public sealed record AmbientScene
{
    /// <summary>The kinds of scene the game can draw.</summary>
    public static readonly IReadOnlySet<string> Kinds = new HashSet<string>(
        ["argument", "dropped_load", "procession", "funeral", "thief", "cart_wheel", "farewell", "drunk", "dispute", "children"], StringComparer.Ordinal);

    /// <summary>ID, for example <c>ambient.argument_tavern</c>.</summary>
    public required string Id { get; init; }

    /// <summary>One of <see cref="Kinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>What the player is told if they are near, in a line.</summary>
    public required string Text { get; init; }

    /// <summary>Where it happens: a walkable cell [x, y].</summary>
    public required IReadOnlyList<int> At { get; init; }

    /// <summary>For scenes that move, where to: a walkable cell [x, y].</summary>
    public IReadOnlyList<int>? To { get; init; }

    /// <summary>Hour from which it can happen.</summary>
    public int FromHour { get; init; }

    /// <summary>Hour before which it can happen; may be less than <see cref="FromHour"/> to wrap past midnight.</summary>
    public int ToHour { get; init; } = 24;

    /// <summary><c>any</c>, <c>dry</c> (not in rain) or <c>wet</c> (only in rain).</summary>
    public string Weather { get; init; } = "any";

    /// <summary>True if the scene can happen at <paramref name="hour"/>.</summary>
    public bool AtHour(int hour) => FromHour <= ToHour ? hour >= FromHour && hour < ToHour : hour >= FromHour || hour < ToHour;
}

/// <summary>
/// How the port comes to know the expedition (R2e, PORT_EXPERIENCE §16): its standing grows with the
/// hands signed on, the stores bought and the money spent, and shows in what people do and say, never
/// as a number. At "noticed" people greet the captain in passing; at "talked of" those looking for a
/// berth who have not met the captain seek him out.
/// </summary>
public sealed record RecognitionRules
{
    /// <summary>Score for being noticed: each hand signed on counts 2, each lot bought 1, each 1,000 sueldos spent 1.</summary>
    public int NoticedAt { get; init; } = 2;

    /// <summary>Score for the expedition to be talked of around the port.</summary>
    public int TalkedOfAt { get; init; } = 6;

    /// <summary>Score for everyone to know the Garza's captain.</summary>
    public int KnownAt { get; init; } = 11;

    /// <summary>What anyone without lines of their own says in passing, by standing.</summary>
    public IReadOnlyList<Greeting> Greetings { get; init; } = [];

    /// <summary>Minutes between candidates seeking the captain out.</summary>
    public int ApproachEveryMinutes { get; init; } = 60;

    /// <summary>How far away a candidate will come from, in cells.</summary>
    public int ApproachRange { get; init; } = 30;
}

/// <summary>
/// A line said in passing once the expedition has at least <see cref="Standing"/>. <c>{captain}</c>,
/// <c>{ship}</c>, <c>{crew}</c> and <c>{days}</c> are filled in.
/// </summary>
public sealed record Greeting
{
    /// <summary>Least standing: 1 noticed, 2 talked of, 3 known.</summary>
    public required int Standing { get; init; }

    /// <summary>The line.</summary>
    public required string Text { get; init; }
}

/// <summary>A debt a creditor means to have from the wages of someone the captain signs on.</summary>
public sealed record CreditorClaim
{
    /// <summary>Who owes it.</summary>
    public required string Debtor { get; init; }

    /// <summary>Sueldos owed.</summary>
    public required int Amount { get; init; }

    /// <summary>What they say when they come to the captain about it.</summary>
    public required string Says { get; init; }

    /// <summary>What they say at the gangway if it is still unpaid on departure.</summary>
    public required string AtGangway { get; init; }
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

    /// <summary>Who sells it (<see cref="PortSeller.Id"/>); null for the first seller, the chandler.</summary>
    public string? Seller { get; init; }

    /// <summary>
    /// What it is really like: <c>good</c>, <c>fair</c> or <c>poor</c>. Hidden: the captain learns it by
    /// looking closely, from what people say, or at sea. The item itself carries what that means.
    /// </summary>
    public string Quality { get; init; } = "good";

    /// <summary>What a close look shows, when the captain can tell.</summary>
    public string Looks { get; init; } = "";
}

/// <summary>Someone who sells stores on the waterfront (R2: sellers of different quality).</summary>
public sealed record PortSeller
{
    /// <summary>ID, for example <c>seller.hob</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The person who sells (<see cref="PersonDefinition.Id"/>): to look closely, go to them.</summary>
    public required string Person { get; init; }

    /// <summary>How the stall is named, for example "Hob the Brennish trader".</summary>
    public required string Name { get; init; }

    /// <summary>What they say of their goods: the claim, not the truth.</summary>
    public string Says { get; init; } = "";

    /// <summary>Whether they give credit to a captain the port knows (Corbalán does; Hob does not).</summary>
    public bool Credit { get; init; }

    /// <summary>What they say at the gangway if the Garza sails owing them; {amount} is the debt.</summary>
    public string AtGangway { get; init; } = "\"You owe me {amount} sueldos, Captain.\"";
}

/// <summary>
/// How prices move in port (owner's request): they climb as the convoy's sailing nears and every ship
/// buys at once, and as a seller's stock runs low; a captain the port knows pays less, and is trusted
/// on credit with those who give it.
/// </summary>
public sealed record PortPrices
{
    /// <summary>Hours before the convoy's sailing over which prices climb.</summary>
    public int RiseHours { get; init; } = 36;

    /// <summary>How much more a lot costs at the convoy's sailing (0.35: a third more).</summary>
    public double Rise { get; init; } = 0.35;

    /// <summary>How much more the last lot costs than the first (0.4).</summary>
    public double Scarcity { get; init; } = 0.4;

    /// <summary>Discount at each standing, 0 to 3 (the port's knowledge of the expedition).</summary>
    public IReadOnlyList<double> StandingDiscount { get; init; } = [0, 0, 0.05, 0.1];

    /// <summary>Credit each seller who gives it will extend, at each standing.</summary>
    public IReadOnlyList<int> CreditLimit { get; init; } = [0, 0, 600, 1500];
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

    /// <summary>What they say passing the captain, once the expedition is known (R2e); else the port's lines.</summary>
    public IReadOnlyList<Greeting> Greetings { get; init; } = [];

    /// <summary>A debt they mean to collect from someone the captain signs on (R2e), or null.</summary>
    public CreditorClaim? Claim { get; init; }

    /// <summary>How they are seen off if they sail (R2e departure), or null for the usual.</summary>
    public string? Farewell { get; init; }
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
