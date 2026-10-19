using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Items;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Scenarios;

/// <summary>
/// The start of a campaign (V4, D-019): Captain Álvaro aboard the <i>Garza</i> at La Bocana with the stores
/// the Company provides, hiring open, and the convoy due to sail. The player hires a crew, buys what else
/// the purse allows, and sets sail with <see cref="SimulationRunner.SetSail"/>.
/// </summary>
public static class ExpeditionScenario
{
    /// <summary>Stores the Company puts aboard; the rest is bought from the chandler.</summary>
    private static readonly (string Item, int Quantity)[] CompanyStores =
    [
        ("item.biscuit", 100), ("item.salt_pork", 30), ("item.water", 160), ("item.wine", 20),
        ("item.rope", 4), ("item.oakum", 4), ("item.plank", 2), ("item.marlinspike", 2), ("item.caulking_mallet", 1),
    ];

    /// <summary>Creates the scenario. Requires the shipped content, with recruitment rules.</summary>
    /// <exception cref="InvalidOperationException">The content lacks something the scenario needs.</exception>
    public static SimulationRunner Create(GameContent content, ulong seed = CampaignState.DefaultSeed)
    {
        if (content.Recruitment is null || !content.Locations.ContainsKey(TestDeckScenario.LocationId))
        {
            throw new InvalidOperationException("The expedition scenario needs data/recruitment.json and the deck location.");
        }

        var campaign = new CampaignState(CampaignPhase.SponsorCommission, seed);
        campaign.Clock.Restore(8 * SimulationClock.TicksPerHour);
        var runner = new SimulationRunner(campaign, content);
        Require(campaign.TryTransitionTo(CampaignPhase.PortPreparation, "accepted the Ledra–Varzi Company's charter"));

        campaign.CreateInventory(TestDeckScenario.HoldId, "Ship's hold", new InventoryCapacity { MaxWeightKg = 2000, MaxVolumeLitres = 3000 });
        Require(campaign.SetSharedStore(TestDeckScenario.HoldId));
        foreach (var (item, quantity) in CompanyStores)
        {
            Require(runner.AddItems(TestDeckScenario.HoldId, item, quantity));
        }

        var captain = campaign.CreateCharacter("Captain Álvaro", 41, "Almara", content.Rules.PersonalCarryCapacity);
        captain.Sex = Sex.Male;
        captain.StatusId = "status.officer";
        captain.Languages.Set("language.castrenan", 5);
        captain.Languages.Set("language.lusaran", 2);
        foreach (var (skill, level) in new[] { ("skill.navigation", 3), ("skill.leadership", 3), ("skill.sailing", 2), ("skill.negotiation", 1) })
        {
            captain.Skills.Set(skill, level);
        }

        captain.Attributes.Set("attribute.perception", 3);
        captain.AddTrait("trait.proud");
        captain.AddTrait("trait.cautious");
        captain.Appearance = AppearanceGenerator.Generate(captain, content.Appearance, campaign.Randomness.Characters);
        Require(runner.PlaceCharacter(captain.Id, TestDeckScenario.LocationId, new GridPosition(4, 18)));
        Require(campaign.AppointCaptain(captain.Id, "named in the Company's charter"));
        Require(runner.SetDuties(captain.Id, ["task.stand_watch"]));
        Require(runner.BeginRecruitment());
        return runner;
    }

    /// <summary>Sails with the convoy once the crew is hired.</summary>
    public static Core.CommandResult SetSail(SimulationRunner runner) =>
        runner.SetSail(TestDeckScenario.ShipName, TestDeckScenario.Destination, TestDeckScenario.VoyageLeagues);

    private static void Require(Core.CommandResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Expedition scenario setup failed: {result.Reason}");
        }
    }
}
