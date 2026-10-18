using Expedition.Simulation.Campaign;
using Expedition.Simulation.Items;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class InventoryTests
{
    private static readonly IReadOnlyDictionary<string, ItemDefinition> Items = TestContent.Create().Items;

    private static Inventory Hold(double? maxKg = null, double? maxLitres = null) =>
        new(new InventoryId("inv.hold"), "Hold", new InventoryCapacity { MaxWeightKg = maxKg, MaxVolumeLitres = maxLitres });

    [Fact]
    public void AddingWithinCapacitySucceeds()
    {
        var hold = Hold(maxKg: 10);

        Assert.True(hold.TryAdd("item.rope", 2, Items).Succeeded);
        Assert.Equal(8.0, hold.TotalWeightKg(Items));
    }

    [Fact]
    public void AddingOverWeightIsRejectedAndChangesNothing()
    {
        var hold = Hold(maxKg: 10);
        hold.TryAdd("item.rope", 2, Items);

        var result = hold.TryAdd("item.rope", 1, Items);

        Assert.False(result.Succeeded);
        Assert.Contains("12 kg exceeds 10 kg", result.Reason);
        Assert.Equal(2, hold.Count("item.rope"));
    }

    [Fact]
    public void AddingOverVolumeIsRejected()
    {
        var hold = Hold(maxLitres: 20);

        var result = hold.TryAdd("item.plank", 2, Items);

        Assert.False(result.Succeeded);
        Assert.Contains("L exceeds", result.Reason);
    }

    [Fact]
    public void ExactlyFullIsAllowedDespiteRounding()
    {
        // 0.25 kg x 40 = 10 kg; floating-point sums must not reject an exact fit.
        var hold = Hold(maxKg: 10);

        Assert.True(hold.TryAdd("item.biscuit", 40, Items).Succeeded);
        Assert.False(hold.TryAdd("item.biscuit", 1, Items).Succeeded);
    }

    [Fact]
    public void UnknownItemsAndNonPositiveQuantitiesAreRejected()
    {
        var hold = Hold();

        Assert.False(hold.TryAdd("item.unicorn", 1, Items).Succeeded);
        Assert.False(hold.TryAdd("item.rope", 0, Items).Succeeded);
        Assert.False(hold.TryAdd("item.rope", -3, Items).Succeeded);
        Assert.Empty(hold.Quantities);
    }

    [Fact]
    public void RemovingMoreThanHeldChangesNothing()
    {
        var hold = Hold();
        hold.TryAdd("item.rope", 2, Items);

        Assert.False(hold.TryRemove("item.rope", 3));
        Assert.Equal(2, hold.Count("item.rope"));
        Assert.True(hold.TryRemove("item.rope", 2));
        Assert.Empty(hold.Quantities);
    }

    [Fact]
    public void TransferConservesItems()
    {
        var hold = Hold();
        var boat = new Inventory(new InventoryId("inv.boat"), "Boat", InventoryCapacity.Unlimited);
        hold.TryAdd("item.water", 30, Items);

        Assert.True(Inventory.TryTransfer(hold, boat, "item.water", 12, Items).Succeeded);

        Assert.Equal(18, hold.Count("item.water"));
        Assert.Equal(12, boat.Count("item.water"));
    }

    [Fact]
    public void TransferThatDoesNotFitMovesNothing()
    {
        var hold = Hold();
        var pack = new Inventory(new InventoryId("inv.pack"), "Pack", new InventoryCapacity { MaxWeightKg = 5 });
        hold.TryAdd("item.rope", 2, Items);

        var result = Inventory.TryTransfer(hold, pack, "item.rope", 2, Items);

        Assert.False(result.Succeeded);
        Assert.Equal(2, hold.Count("item.rope"));
        Assert.Empty(pack.Quantities);
    }

    [Fact]
    public void TransferOfMissingItemsIsRejected()
    {
        var hold = Hold();
        var pack = new Inventory(new InventoryId("inv.pack"), "Pack", InventoryCapacity.Unlimited);

        Assert.False(Inventory.TryTransfer(hold, pack, "item.rope", 1, Items).Succeeded);
        Assert.False(Inventory.TryTransfer(hold, hold, "item.rope", 1, Items).Succeeded);
    }

    [Fact]
    public void EveryCharacterGetsAPersonalInventoryWithCarryLimits()
    {
        var campaign = new CampaignState();
        var carry = TestContent.Rules.PersonalCarryCapacity;

        var sailor = campaign.CreateCharacter("Ines", 30, "Lisbon", carry);

        var pack = campaign.Inventories[sailor.InventoryId];
        Assert.Equal("inv.char-0001", pack.Id.Value);
        Assert.Equal(20, pack.Capacity.MaxWeightKg);
    }

    [Fact]
    public void PersonalInventoryCannotBeTheSharedStore()
    {
        var campaign = new CampaignState();
        var sailor = campaign.CreateCharacter("Ines", 30, "Lisbon");

        Assert.False(campaign.SetSharedStore(sailor.InventoryId).Succeeded);
        Assert.False(campaign.SetSharedStore(new InventoryId("inv.nowhere")).Succeeded);
        Assert.Equal(CampaignState.DefaultStoreId, campaign.SharedStoreId);
    }

    [Fact]
    public void TaskMaterialsComeFromPersonalInventoryFirst()
    {
        var campaign = new CampaignState(CampaignPhase.Voyage);
        var runner = new SimulationRunner(campaign, TestContent.Create());
        var sailor = campaign.CreateCharacter("Ines", 30, "Lisbon");
        sailor.Skills.Set("skill.rigging", 2);
        runner.AddItems(sailor.InventoryId, "item.marlinspike", 1);
        runner.AddItems(sailor.InventoryId, "item.rope", 1);
        runner.AddItems(campaign.SharedStoreId, "item.rope", 5);

        Assert.True(runner.AssignTask(sailor.Id, "task.repair_rigging").Succeeded);

        // Needs 2 rope: 1 from the pack, 1 from the stores. The tool stays in the pack.
        Assert.Equal(0, campaign.Inventories[sailor.InventoryId].Count("item.rope"));
        Assert.Equal(4, campaign.SharedStore.Count("item.rope"));
        Assert.Equal(1, campaign.Inventories[sailor.InventoryId].Count("item.marlinspike"));
    }
}
