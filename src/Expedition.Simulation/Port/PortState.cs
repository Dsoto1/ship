using Expedition.Simulation.Core;

namespace Expedition.Simulation.Port;

/// <summary>Preparations in port while the expedition waits for the convoy. Persistent; cleared on sailing.</summary>
public sealed class PortState
{
    private readonly Dictionary<int, int> _stock = [];
    private readonly Dictionary<int, string> _inspected = [];
    private readonly Dictionary<string, int> _owed = new(StringComparer.Ordinal);
    private readonly HashSet<CharacterId> _talkedTo = [];
    private readonly HashSet<(CharacterId Informant, CharacterId About)> _asked = [];
    private readonly HashSet<CharacterId> _hired = [];
    private readonly HashSet<CharacterId> _gone = [];
    private readonly List<HeardLead> _leads = [];
    private readonly List<Purchase> _purchases = [];
    private readonly HashSet<CharacterId> _approached = [];
    private readonly Dictionary<CharacterId, bool> _claims = [];
    private readonly HashSet<(CharacterId Who, int Day)> _greeted = [];
    private readonly HashSet<(CharacterId Who, string Topic)> _discussed = [];

    /// <summary>Sueldos left.</summary>
    public int Funds { get; internal set; }

    /// <summary>Lots left at the chandler, by index into the goods list.</summary>
    public IReadOnlyDictionary<int, int> Stock => _stock;

    /// <summary>People the captain has spoken with.</summary>
    public IReadOnlySet<CharacterId> TalkedTo => _talkedTo;

    /// <summary>What the captain made of each lot he looked at closely: a quality, or <c>unsure</c>.</summary>
    public IReadOnlyDictionary<int, string> Inspected => _inspected;

    /// <summary>Who has been asked about whom.</summary>
    public IReadOnlySet<(CharacterId Informant, CharacterId About)> Asked => _asked;

    /// <summary>People hired here.</summary>
    public IReadOnlySet<CharacterId> Hired => _hired;

    /// <summary>People who stopped waiting and went elsewhere.</summary>
    public IReadOnlySet<CharacterId> Gone => _gone;

    /// <summary>Leads heard, oldest first: the captain's journal of people worth finding.</summary>
    public IReadOnlyList<HeardLead> Leads => _leads;

    /// <summary>Lots bought from the chandler, oldest first, and when: what is being loaded (R2c).</summary>
    public IReadOnlyList<Purchase> Purchases => _purchases;

    /// <summary>The weather now (R2d), or null where the port has none.</summary>
    public string? WeatherId { get; internal set; }

    /// <summary>When the weather next changes.</summary>
    public long WeatherUntil { get; internal set; }

    /// <summary>People who have come up to the captain of their own accord (R2e), creditors included.</summary>
    public IReadOnlySet<CharacterId> Approached => _approached;

    /// <summary>Someone walking over to the captain now, or null.</summary>
    public CharacterId? Approaching { get; internal set; }

    /// <summary>When they set off.</summary>
    public long ApproachingSince { get; internal set; }

    /// <summary>When someone last greeted the captain in passing.</summary>
    public long LastGreeting { get; internal set; } = long.MinValue / 2;

    /// <summary>Creditors' claims the captain has answered: true paid, false refused.</summary>
    public IReadOnlyDictionary<CharacterId, bool> Claims => _claims;

    /// <summary>Who has greeted the captain, and on which day.</summary>
    public IReadOnlySet<(CharacterId Who, int Day)> Greeted => _greeted;

    /// <summary>Half days of late fees already charged.</summary>
    public int LateFeesCharged { get; internal set; }

    /// <summary>Whether the eve-of-sailing warning has been given.</summary>
    public bool Warned { get; internal set; }

    internal void SetStock(int lot, int lots) => _stock[lot] = Math.Max(0, lots);

    internal void MarkTalked(CharacterId id) => _talkedTo.Add(id);

    internal void MarkInspected(int lot, string seen) => _inspected[lot] = seen;

    /// <summary>What the expedition owes each seller who gave it credit, by seller ID.</summary>
    public IReadOnlyDictionary<string, int> Owed => _owed;

    internal void AddDebt(string seller, int amount) => _owed[seller] = _owed.GetValueOrDefault(seller) + amount;

    internal void ClearDebt(string seller) => _owed.Remove(seller);

    internal void MarkAsked(CharacterId informant, CharacterId about) => _asked.Add((informant, about));

    internal void MarkHired(CharacterId id) => _hired.Add(id);

    internal void MarkGone(CharacterId id) => _gone.Add(id);

    internal void AddPurchase(int lot, long tick) => _purchases.Add(new Purchase(lot, tick));

    internal void MarkApproached(CharacterId id) => _approached.Add(id);

    internal void SettleClaim(CharacterId creditor, bool paid) => _claims[creditor] = paid;

    internal void MarkGreeted(CharacterId id, int day) => _greeted.Add((id, day));

    /// <summary>What the captain has talked over with whom: person and topic ID.</summary>
    public IReadOnlySet<(CharacterId Who, string Topic)> Discussed => _discussed;

    internal bool MarkDiscussed(CharacterId id, string topic) => _discussed.Add((id, topic));

    internal bool AddLead(string leadId, long tick)
    {
        if (_leads.Any(l => l.LeadId == leadId))
        {
            return false;
        }

        _leads.Add(new HeardLead(leadId, tick));
        return true;
    }
}

/// <summary>A lead the captain has heard, and when.</summary>
/// <param name="LeadId">The lead's content ID.</param>
/// <param name="Tick">When it was heard.</param>
public sealed record HeardLead(string LeadId, long Tick)
{
}

/// <summary>A lot bought from the chandler.</summary>
/// <param name="Lot">Index into the port's goods.</param>
/// <param name="Tick">When it was bought.</param>
public sealed record Purchase(int Lot, long Tick)
{
}
