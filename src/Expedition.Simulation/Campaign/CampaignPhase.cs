namespace Expedition.Simulation.Campaign;

/// <summary>The high-level situation the campaign is in. See <see cref="PhaseTransitions"/> for allowed moves.</summary>
public enum CampaignPhase
{
    /// <summary>Learning the sponsor's terms before accepting the charter.</summary>
    SponsorCommission,

    /// <summary>In port: recruiting, provisioning, inspecting the ship.</summary>
    PortPreparation,

    /// <summary>At sea.</summary>
    Voyage,

    /// <summary>Survivors of a wreck trying to stay alive and find a way forward.</summary>
    ShipwreckSurvival,

    /// <summary>Put in at a port other than the destination.</summary>
    AlternatePort,

    /// <summary>Arrived at the destination coast.</summary>
    Landfall,

    /// <summary>Building and sustaining a settlement.</summary>
    SettlementSurvival,

    /// <summary>Terminal: the charter's conditions were met.</summary>
    CharterFulfilled,

    /// <summary>Terminal: the campaign has no plausible way forward.</summary>
    CampaignFailed,
}
