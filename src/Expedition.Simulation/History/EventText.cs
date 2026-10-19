using Expedition.Simulation.Campaign;
using Expedition.Simulation.Core;
using Expedition.Simulation.Events;

namespace Expedition.Simulation.History;

/// <summary>Fills names into authored event text. Part of the text layer.</summary>
public static class EventText
{
    /// <summary>Replaces {subject}, {other}, {captain}, {ship}, {Ship} and {destination}.</summary>
    public static string Fill(string template, CampaignState campaign, PendingEvent pending) =>
        Fill(template, campaign, pending.Subject, pending.Other);

    /// <summary>Replaces {subject}, {other}, {captain}, {ship}, {Ship} and {destination}.</summary>
    public static string Fill(string template, CampaignState campaign, CharacterId? subject, CharacterId? other)
    {
        string Name(CharacterId? id) => id is { } value && campaign.Characters.TryGetValue(value, out var character) ? character.Name : "someone";
        var ship = campaign.Voyage?.ShipName ?? "the ship";
        return template
            .Replace("{subject}", Name(subject), StringComparison.Ordinal)
            .Replace("{other}", Name(other), StringComparison.Ordinal)
            .Replace("{captain}", Name(campaign.CaptainId), StringComparison.Ordinal)
            .Replace("{Ship}", ship.Length > 0 ? char.ToUpperInvariant(ship[0]) + ship[1..] : ship, StringComparison.Ordinal)
            .Replace("{ship}", ship, StringComparison.Ordinal)
            .Replace("{destination}", campaign.Voyage?.Destination ?? "the coast", StringComparison.Ordinal);
    }
}
