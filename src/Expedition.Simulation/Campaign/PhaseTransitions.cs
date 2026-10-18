namespace Expedition.Simulation.Campaign;

/// <summary>
/// The single table of allowed campaign phase transitions. Any move not listed here is invalid.
/// Whether an allowed move is actually possible in a given situation (for example, whether a
/// rescuer exists) is decided by the systems that request it, not by this table.
/// </summary>
public static class PhaseTransitions
{
    private static readonly IReadOnlyDictionary<CampaignPhase, CampaignPhase[]> Allowed =
        new Dictionary<CampaignPhase, CampaignPhase[]>
        {
            [CampaignPhase.SponsorCommission] = [CampaignPhase.PortPreparation, CampaignPhase.CampaignFailed],
            [CampaignPhase.PortPreparation] = [CampaignPhase.Voyage, CampaignPhase.CampaignFailed],
            [CampaignPhase.Voyage] =
            [
                CampaignPhase.Landfall, CampaignPhase.ShipwreckSurvival, CampaignPhase.AlternatePort, CampaignPhase.CampaignFailed,
            ],
            // Resupply and sail on.
            [CampaignPhase.AlternatePort] = [CampaignPhase.Voyage, CampaignPhase.CampaignFailed],
            // Rescue or a rebuilt boat leads back to sea; otherwise reach a port or settle where stranded.
            [CampaignPhase.ShipwreckSurvival] =
            [
                CampaignPhase.Voyage, CampaignPhase.AlternatePort, CampaignPhase.SettlementSurvival, CampaignPhase.CampaignFailed,
            ],
            [CampaignPhase.Landfall] = [CampaignPhase.SettlementSurvival, CampaignPhase.Voyage, CampaignPhase.CampaignFailed],
            [CampaignPhase.SettlementSurvival] = [CampaignPhase.CharterFulfilled, CampaignPhase.CampaignFailed],
            [CampaignPhase.CharterFulfilled] = [],
            [CampaignPhase.CampaignFailed] = [],
        };

    /// <summary>True if the campaign may move directly from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static bool IsAllowed(CampaignPhase from, CampaignPhase to) =>
        Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

    /// <summary>The phases reachable directly from <paramref name="from"/>.</summary>
    public static IReadOnlyList<CampaignPhase> TargetsFrom(CampaignPhase from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>True for phases that end the campaign.</summary>
    public static bool IsTerminal(CampaignPhase phase) => TargetsFrom(phase).Count == 0;
}
