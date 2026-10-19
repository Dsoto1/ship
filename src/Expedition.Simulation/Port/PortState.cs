using Expedition.Simulation.Core;

namespace Expedition.Simulation.Port;

/// <summary>Preparations in port while the expedition waits for the convoy. Persistent; cleared on sailing.</summary>
public sealed class PortState
{
    private readonly Dictionary<int, int> _stock = [];
    private readonly HashSet<CharacterId> _talkedTo = [];
    private readonly HashSet<(CharacterId Informant, CharacterId About)> _asked = [];
    private readonly HashSet<CharacterId> _hired = [];
    private readonly HashSet<CharacterId> _gone = [];
    private readonly List<HeardLead> _leads = [];

    /// <summary>Sueldos left.</summary>
    public int Funds { get; internal set; }

    /// <summary>Lots left at the chandler, by index into the goods list.</summary>
    public IReadOnlyDictionary<int, int> Stock => _stock;

    /// <summary>People the captain has spoken with.</summary>
    public IReadOnlySet<CharacterId> TalkedTo => _talkedTo;

    /// <summary>Who has been asked about whom.</summary>
    public IReadOnlySet<(CharacterId Informant, CharacterId About)> Asked => _asked;

    /// <summary>People hired here.</summary>
    public IReadOnlySet<CharacterId> Hired => _hired;

    /// <summary>People who stopped waiting and went elsewhere.</summary>
    public IReadOnlySet<CharacterId> Gone => _gone;

    /// <summary>Leads heard, oldest first: the captain's journal of people worth finding.</summary>
    public IReadOnlyList<HeardLead> Leads => _leads;

    /// <summary>Half days of late fees already charged.</summary>
    public int LateFeesCharged { get; internal set; }

    /// <summary>Whether the eve-of-sailing warning has been given.</summary>
    public bool Warned { get; internal set; }

    internal void SetStock(int lot, int lots) => _stock[lot] = Math.Max(0, lots);

    internal void MarkTalked(CharacterId id) => _talkedTo.Add(id);

    internal void MarkAsked(CharacterId informant, CharacterId about) => _asked.Add((informant, about));

    internal void MarkHired(CharacterId id) => _hired.Add(id);

    internal void MarkGone(CharacterId id) => _gone.Add(id);

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
