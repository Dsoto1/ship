using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.Items;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Scenarios;

/// <summary>
/// Starting state for the graybox interaction scene (Ticket 4b): a small crew at sea on the
/// placeholder test deck with a stocked hold. Names and origins are placeholders, not researched
/// historical content.
/// </summary>
public static class TestDeckScenario
{
    /// <summary>Location the scenario uses.</summary>
    public const string LocationId = "location.test_deck";

    /// <summary>The ship (SETTING.md 4).</summary>
    public const string ShipName = "the Garza";

    /// <summary>Where she is bound (SETTING.md 5).</summary>
    public const string Destination = "the Ember Coast";

    /// <summary>Length of the crossing. A placeholder tuned for about three weeks at sea (D-016).</summary>
    public const double VoyageLeagues = 560;

    /// <summary>ID of the scenario's ship's hold.</summary>
    public static readonly InventoryId HoldId = new("inv.hold");

    private static readonly (string Name, int Age, string Origin, int X, int Y, (string Id, int Level)[] Skills, (string Id, int Level)[] Attributes, string[] Traits)[] Crew =
    [
        ("Captain Álvaro", 41, "Almara", 4, 18,
            [("skill.navigation", 3), ("skill.leadership", 3), ("skill.sailing", 2)], [("attribute.perception", 3)], ["trait.proud", "trait.cautious"]),
        ("Diego", 29, "La Bocana", 3, 5,
            [("skill.rigging", 3), ("skill.sailing", 3)], [("attribute.coordination", 4)], ["trait.loyal"]),
        ("Juan the cooper", 52, "Almara", 5, 15,
            [("skill.cooking", 2), ("skill.carpentry", 3)], [("attribute.endurance", 1)], ["trait.resourceful", "trait.cautious"]),
        ("Pedro", 19, "La Bocana", 2, 8,
            [("skill.sailing", 1)], [("attribute.strength", 4), ("attribute.endurance", 3)], ["trait.impulsive"]),
        ("Gonçalo", 34, "Lusara", 6, 12,
            [("skill.ship_repair", 3), ("skill.carpentry", 2)], [("attribute.strength", 3)], ["trait.proud"]),
        ("Martín", 45, "Almara", 2, 17,
            [("skill.first_aid", 2), ("skill.seamanship", 3)], [], ["trait.loyal", "trait.resourceful"]),
    ];

    // Starting history between the crew: (from, to, dimension, change, cause), by index into Crew.
    private static readonly (int From, int To, Characters.RelationshipDimension Dimension, int Delta, string Cause)[] History =
    [
        (1, 0, Characters.RelationshipDimension.Trust, 30, "sailed under him before"),
        (0, 1, Characters.RelationshipDimension.Respect, 25, "the best rigger he knows"),
        (2, 3, Characters.RelationshipDimension.Respect, -20, "young and careless"),
        (3, 2, Characters.RelationshipDimension.Affection, 15, "fed him when he first came aboard"),
        (4, 0, Characters.RelationshipDimension.Trust, -10, "a Lusaran stranger to this captain"),
    ];

    // Standing duties, by index into Crew: what each does when idle with no orders (in turn).
    private static readonly string[][] Duties =
    [
        ["task.stand_watch"],
        ["task.strike_topmasts", "task.repair_rigging", "task.stand_watch"],
        ["task.cook", "task.pump_bilge", "task.scrub_decks"],
        ["task.batten_down", "task.lash_cargo", "task.pump_bilge", "task.stand_watch"],
        ["task.plug_leak", "task.patch_hull", "task.pump_bilge"],
        ["task.pump_bilge", "task.tend_sick", "task.stand_watch"],
    ];

    /// <summary>Creates the scenario. Requires the shipped content (<c>data/</c>).</summary>
    /// <exception cref="InvalidOperationException">The content lacks something the scenario needs.</exception>
    /// <param name="content">The shipped content.</param>
    /// <param name="seed">Campaign seed.</param>
    /// <param name="withDuties">False leaves the crew without standing duties, so they only do what they are told.</param>
    /// <param name="withVoyage">False leaves the ship without a voyage (no leaks, weather or distance).</param>
    public static SimulationRunner Create(GameContent content, ulong seed = CampaignState.DefaultSeed, bool withDuties = true, bool withVoyage = true)
    {
        if (!content.Locations.ContainsKey(LocationId))
        {
            throw new InvalidOperationException($"The test deck scenario needs location '{LocationId}'.");
        }

        var campaign = new CampaignState(CampaignPhase.SponsorCommission, seed);

        // The voyage begins at dawn, with the morning tide.
        campaign.Clock.Restore(6 * SimulationClock.TicksPerHour);
        var runner = new SimulationRunner(campaign, content);
        campaign.TryTransitionTo(CampaignPhase.PortPreparation, "accepted the sponsor's charter");
        campaign.TryTransitionTo(CampaignPhase.Voyage, "sailed with the morning tide");

        campaign.CreateInventory(HoldId, "Ship's hold", new InventoryCapacity { MaxWeightKg = 2000, MaxVolumeLitres = 3000 });
        Require(campaign.SetSharedStore(HoldId));
        foreach (var (item, quantity) in new[]
                 {
                     ("item.biscuit", 170), ("item.salt_pork", 60), ("item.water", 300), ("item.wine", 40),
                     ("item.rope", 8), ("item.oakum", 10), ("item.plank", 6),
                     ("item.marlinspike", 2), ("item.caulking_mallet", 1),
                 })
        {
            Require(runner.AddItems(HoldId, item, quantity));
        }

        foreach (var (name, age, origin, x, y, skills, attributes, traits) in Crew)
        {
            var character = campaign.CreateCharacter(name, age, origin, content.Rules.PersonalCarryCapacity);
            character.Sex = Characters.Sex.Male;
            character.StatusId = name.StartsWith("Captain", StringComparison.Ordinal) ? "status.officer" : "status.sailor";
            character.Languages.Set(origin == "Lusara" ? "language.lusaran" : "language.castrenan", 5);
            if (origin == "Lusara")
            {
                character.Languages.Set("language.castrenan", 3);
            }

            foreach (var (skill, level) in skills)
            {
                character.Skills.Set(skill, level);
            }

            foreach (var (attribute, level) in attributes)
            {
                character.Attributes.Set(attribute, level);
            }

            foreach (var trait in traits)
            {
                if (!content.Traits.ContainsKey(trait))
                {
                    throw new InvalidOperationException($"The test deck scenario needs trait '{trait}'.");
                }

                character.AddTrait(trait);
            }

            character.Appearance = Characters.AppearanceGenerator.Generate(character, content.Appearance, campaign.Randomness.Characters);
            Require(runner.PlaceCharacter(character.Id, LocationId, new GridPosition(x, y)));
        }

        Require(campaign.AppointCaptain(campaign.Characters.Keys.Min(), "named in the sponsor's charter"));
        var ids = campaign.Characters.Keys.Order().ToList();
        foreach (var (from, to, dimension, delta, cause) in History)
        {
            Require(campaign.ChangeRelationship(ids[from], ids[to], dimension, delta, cause));
        }

        for (var i = 0; i < Duties.Length && withDuties; i++)
        {
            Require(runner.SetDuties(ids[i], Duties[i]));
        }

        if (withVoyage && content.Voyage is not null)
        {
            Require(runner.StartVoyage(ShipName, Destination, VoyageLeagues));
        }

        return runner;
    }

    private static void Require(Core.CommandResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Test deck scenario setup failed: {result.Reason}");
        }
    }
}
