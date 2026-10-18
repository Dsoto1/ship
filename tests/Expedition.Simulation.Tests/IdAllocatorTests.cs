using Expedition.Simulation.Campaign;
using Expedition.Simulation.Core;

namespace Expedition.Simulation.Tests;

public class IdAllocatorTests
{
    [Fact]
    public void IssuedIdsAreUniqueAcrossManyAllocations()
    {
        var allocator = new IdAllocator();

        var ids = Enumerable.Range(0, 10_000).Select(_ => allocator.NextCharacterId()).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.True(id.Value > 0));
    }

    [Fact]
    public void ReservedIdIsNeverIssuedAgain()
    {
        var allocator = new IdAllocator();
        allocator.Reserve(new CharacterId(50));

        var next = allocator.NextCharacterId();

        Assert.Equal(51, next.Value);
    }

    [Fact]
    public void ReservingNonPositiveIdIsRejected()
    {
        var allocator = new IdAllocator();

        Assert.Throws<ArgumentOutOfRangeException>(() => allocator.Reserve(new CharacterId(0)));
    }

    [Fact]
    public void CampaignCharactersGetUniqueIds()
    {
        var campaign = new CampaignState();

        var created = Enumerable.Range(0, 100).Select(i => campaign.CreateCharacter($"Sailor {i}", 20, "Seville")).ToList();

        Assert.Equal(100, created.Select(c => c.Id).Distinct().Count());
        Assert.Equal(100, campaign.Characters.Count);
    }

    [Fact]
    public void AddingCharacterWithDuplicateIdIsRejected()
    {
        var campaign = new CampaignState();
        var existing = campaign.CreateCharacter("Ana", 30, "Lisbon");

        var impostor = new Characters.Character(existing.Id, "Not Ana", 40, "Cadiz");

        Assert.Throws<InvalidOperationException>(() => campaign.AddCharacter(impostor));
        Assert.Same(existing, campaign.GetCharacter(existing.Id));
    }

    [Fact]
    public void AddedCharacterIdIsReservedInCampaign()
    {
        var campaign = new CampaignState();
        campaign.AddCharacter(new Characters.Character(new CharacterId(7), "Restored", 25, "Palos"));

        var next = campaign.CreateCharacter("New", 18, "Palos");

        Assert.Equal(8, next.Id.Value);
    }
}
