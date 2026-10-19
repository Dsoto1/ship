using Expedition.Simulation.Knowledge;

namespace Expedition.Simulation.Recruitment;

/// <summary>One thing learned about a candidate before they are hired.</summary>
/// <param name="Candidate">Candidate ID.</param>
/// <param name="Topic">Skill, language or trait ID.</param>
/// <param name="Level">Level.</param>
/// <param name="Source">Claim, reference or test.</param>
public sealed record CandidateFact(string Candidate, string Topic, int Level, KnowledgeSource Source);

/// <summary>
/// Hiring in progress, while the expedition is in port (CampaignPhase.PortPreparation). Part of
/// persistent campaign state; cleared when the ship sails.
/// </summary>
public sealed class RecruitmentState
{
    private readonly List<CandidateFact> _facts = [];
    private readonly HashSet<string> _talkedTo = new(StringComparer.Ordinal);
    private readonly HashSet<string> _askedAbout = new(StringComparer.Ordinal);
    private readonly HashSet<string> _refusedTest = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _prices = new(StringComparer.Ordinal);
    private readonly HashSet<string> _haggled = new(StringComparer.Ordinal);
    private readonly HashSet<string> _gone = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Core.CharacterId> _hired = new(StringComparer.Ordinal);

    /// <summary>Sueldos left.</summary>
    public int Funds { get; internal set; }

    /// <summary>Inquiries left before the convoy sails.</summary>
    public int InquiriesLeft { get; internal set; }

    /// <summary>Everything learned about candidates, oldest first.</summary>
    public IReadOnlyList<CandidateFact> Facts => _facts;

    /// <summary>Candidates the captain has spoken to.</summary>
    public IReadOnlySet<string> TalkedTo => _talkedTo;

    /// <summary>Candidates the captain has asked about.</summary>
    public IReadOnlySet<string> AskedAbout => _askedAbout;

    /// <summary>Candidates who refused to be tested.</summary>
    public IReadOnlySet<string> RefusedTest => _refusedTest;

    /// <summary>Candidates the captain has haggled with.</summary>
    public IReadOnlySet<string> Haggled => _haggled;

    /// <summary>Candidates who walked away.</summary>
    public IReadOnlySet<string> Gone => _gone;

    /// <summary>Hired candidates and the crew members they became.</summary>
    public IReadOnlyDictionary<string, Core.CharacterId> Hired => _hired;

    /// <summary>Asking prices changed by haggling, by candidate.</summary>
    public IReadOnlyDictionary<string, int> Prices => _prices;

    internal void Learn(CandidateFact fact)
    {
        if (!_facts.Contains(fact))
        {
            _facts.Add(fact);
        }
    }

    internal void MarkTalked(string candidate) => _talkedTo.Add(candidate);

    internal void MarkAsked(string candidate) => _askedAbout.Add(candidate);

    internal void MarkRefused(string candidate) => _refusedTest.Add(candidate);

    internal void MarkHaggled(string candidate) => _haggled.Add(candidate);

    internal void MarkGone(string candidate) => _gone.Add(candidate);

    internal void SetPrice(string candidate, int price) => _prices[candidate] = price;

    internal void MarkHired(string candidate, Core.CharacterId id) => _hired[candidate] = id;
}
