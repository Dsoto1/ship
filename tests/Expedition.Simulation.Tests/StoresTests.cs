using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Voyage;
using Expedition.Simulation.World;
using Xunit;

namespace Expedition.Simulation.Tests;

/// <summary>Sellers of different quality: judging goods, and what poor stores do at sea.</summary>
public class StoresTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Port(ulong seed = CampaignState.DefaultSeed) => ExpeditionScenario.Create(Shipped, seed);

    private static int Lot(SimulationRunner runner, string item) => runner.Content.Port!.Goods.ToList().FindIndex(g => g.Item == item);

    private static void StandBy(SimulationRunner runner, string person)
    {
        var them = runner.CharacterFor(person)!.Id;
        var placement = runner.Campaign.Placements[them];
        var cell = placement.CellAt(runner.Campaign.Clock.Tick);
        var location = runner.Content.Locations[placement.LocationId];
        var next = new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1) }
            .Select(d => new GridPosition(cell.X + d.Item1, cell.Y + d.Item2)).First(location.IsWalkable);
        Assert.True(runner.PlaceCharacter(runner.Campaign.CaptainId!.Value, placement.LocationId, next).Succeeded);
    }

    private static SimulationRunner AtSeaWith(params (string Item, int Lots)[] buys) => AtSeaWith(CampaignState.DefaultSeed, buys);

    private static SimulationRunner AtSeaWith(ulong seed, params (string Item, int Lots)[] buys)
    {
        var runner = Port(seed);
        foreach (var seeker in runner.Content.Port!.People.Where(p => p.Seeking is not null).Take(5))
        {
            runner.DeveloperHire(runner.CharacterFor(seeker.Id)!.Id);
        }

        foreach (var (item, lots) in buys)
        {
            for (var i = 0; i < lots; i++)
            {
                Assert.True(runner.Buy(Lot(runner, item)).Succeeded);
            }
        }

        Assert.True(ExpeditionScenario.SetSail(runner).Succeeded);
        return runner;
    }

    [Fact]
    public void EveryLotHasASellerAndAQuality()
    {
        var rules = Shipped.Port!;
        Assert.Equal(3, rules.Sellers.Count);
        Assert.All(rules.Goods, g => Assert.NotNull(rules.SellerOf(g)));
        Assert.Contains(rules.Goods, g => g.Quality == "poor");
        Assert.Equal("seller.corbalan", rules.SellerOf(rules.Goods[0])!.Id);
    }

    [Fact]
    public void YouMustGoToTheSellerToJudgeTheGoods()
    {
        var runner = Port();
        runner.Advance(120);
        var hob = runner.Content.Port!.Sellers.Single(s => s.Id == "seller.hob");

        Assert.Contains("Walk over", runner.WhyCannotInspect(hob));
        Assert.False(runner.Inspect("seller.hob").Result.Succeeded);

        StandBy(runner, "person.hob");
        Assert.Null(runner.WhyCannotInspect(hob));
        var (result, seen) = runner.Inspect("seller.hob");

        Assert.True(result.Succeeded);
        Assert.Equal(4, runner.Content.Port.Goods.Where((g, lot) => g.Seller == "seller.hob" && runner.Campaign.Port!.Inspected.ContainsKey(lot)).Count());
        Assert.Contains("Water (Hob's casks", seen);
    }

    [Fact]
    public void ACloseLookIsRightOrUnsureNeverWrong()
    {
        var verdicts = new List<(string Truth, string Seen)>();
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var runner = Port(seed);
            runner.Advance(120);
            StandBy(runner, "person.hob");
            runner.Inspect("seller.hob");
            // A second look at the same goods changes nothing.
            var first = runner.Campaign.Port!.Inspected.ToDictionary();
            runner.Inspect("seller.hob");
            Assert.Equal(first, runner.Campaign.Port.Inspected);
            verdicts.AddRange(first.Select(p => (runner.Content.Port!.Goods[p.Key].Quality, p.Value)));
        }

        Assert.All(verdicts, v => Assert.True(v.Seen == v.Truth || v.Seen == "unsure", $"{v.Truth} seen as {v.Seen}"));
        Assert.Contains(verdicts, v => v.Seen == v.Truth);
    }

    [Fact]
    public void PoorBiscuitSpoilsAtSeaAndIsFoundOnce()
    {
        var runner = AtSeaWith(("item.biscuit_hob", 4));
        var store = runner.Campaign.SharedStore;
        var bought = store.Count("item.biscuit_hob");

        runner.Advance(4 * SimulationClock.TicksPerDay);

        // The good biscuit is eaten first; Hob's rots in the meantime.
        Assert.InRange(store.Count("item.biscuit_hob"), 1, bought - 8);
        Assert.Single(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.StoresSpoiled && e.Get(ChronicleParameters.To) == "item.biscuit_hob");
        Assert.Equal(runner.Content.Items.Values.Where(i => i.SpoilsPerDay > 0 && store.Count(i.Id) >= 0).Count(i => runner.Campaign.SpoilageNoted.Contains(i.Id)),
            runner.Campaign.Chronicle.Entries.Count(e => e.Kind == ChronicleEntryKind.StoresSpoiled));
    }

    [Fact]
    public void BadWaterMakesSomeOfTheCrewIllAndTheyGetOverIt()
    {
        var entries = new List<ChronicleEntry>();
        string? text = null;
        for (ulong seed = 1; seed <= 3; seed++)
        {
            var runner = AtSeaWith(seed, ("item.water_hob", 14));
            var store = runner.Campaign.SharedStore;
            store.TryRemove("item.water", store.Count("item.water"));
            store.TryRemove("item.wine", store.Count("item.wine"));

            runner.Advance(10 * SimulationClock.TicksPerDay);

            entries.AddRange(runner.Campaign.Chronicle.Entries);
            if (text is null && runner.Campaign.Chronicle.Entries.FirstOrDefault(e => e.Kind == ChronicleEntryKind.SickenedByRation) is { } sick)
            {
                text = new ChronicleFormatter(runner.Campaign, runner.Content).Format(sick);
            }
        }

        Assert.Contains(entries, e => e.Kind == ChronicleEntryKind.SickenedByRation && e.Get(ChronicleParameters.To) == "item.water_hob");
        Assert.Contains(entries, e => e.Kind == ChronicleEntryKind.Recovered);
        Assert.Contains("down with the flux, from the water (hob's casks, litre)", text);
    }

    [Fact]
    public void JudgementsAndSpoilageSurviveSaving()
    {
        var runner = Port();
        runner.Advance(120);
        StandBy(runner, "person.hob");
        runner.Inspect("seller.hob");
        var json = SaveSerializer.Serialize(runner.Campaign);

        var loaded = SaveSerializer.Deserialize(json, runner.Content);

        Assert.Equal(runner.Campaign.Port!.Inspected, loaded.Port!.Inspected);
        Assert.Equal(json, SaveSerializer.Serialize(loaded));

        var sea = AtSeaWith(("item.biscuit_hob", 4));
        sea.Advance(2 * SimulationClock.TicksPerDay);
        var seaJson = SaveSerializer.Serialize(sea.Campaign);
        Assert.Equal(sea.Campaign.SpoilageNoted, SaveSerializer.Deserialize(seaJson, sea.Content).SpoilageNoted);
    }

    [Fact]
    public void AFormat19SaveLoadsWithNothingJudgedYet()
    {
        var runner = Port();
        var root = JsonNode.Parse(SaveSerializer.Serialize(runner.Campaign))!.AsObject();
        root["schemaVersion"] = 19;
        root["randomStreams"]!.AsObject().Remove("stores");
        root["port"]!.AsObject().Remove("inspected");
        root["port"]!.AsObject().Remove("owed");
        root.Remove("spoilageNoted");

        var loaded = SaveSerializer.Deserialize(root.ToJsonString(), runner.Content);

        Assert.Empty(loaded.Port!.Inspected);
        Assert.Empty(loaded.SpoilageNoted);
    }

    private static void Hire(SimulationRunner runner, int count)
    {
        foreach (var seeker in runner.Content.Port!.People.Where(p => p.Seeking is not null).Take(count))
        {
            runner.DeveloperHire(runner.CharacterFor(seeker.Id)!.Id);
        }
    }

    [Fact]
    public void PricesClimbAsTheConvoySailingNears()
    {
        var runner = Port();
        var water = Lot(runner, "item.water");
        var first = runner.PriceOf(water);
        Assert.Equal(runner.Content.Port!.Goods[water].Price, first);

        var departure = runner.Content.Port.Departure.Tick;
        runner.Advance((int)(departure - 12 * SimulationClock.TicksPerHour - runner.Campaign.Clock.Tick));
        var late = runner.PriceOf(water);

        Assert.True(late > first, $"{first} then {late}");
        Assert.True(runner.PriceFactor(water) <= (1 + runner.Content.Port.Prices.Rise) * (1 + runner.Content.Port.Prices.Scarcity));
    }

    [Fact]
    public void ScarceGoodsCostMoreAndYouPayWhatTheyCost()
    {
        var runner = Port();
        var gil = Lot(runner, "item.water_gil");
        var before = runner.PriceOf(gil);
        var funds = runner.Campaign.Port!.Funds;
        Assert.True(runner.Buy(gil).Succeeded);
        Assert.Equal(funds - before, runner.Campaign.Port.Funds);
        for (var i = 0; i < 5; i++)
        {
            runner.Buy(gil);
        }

        Assert.True(runner.PriceOf(gil) > before);
    }

    [Fact]
    public void AKnownCaptainPaysLessAndIsGivenCredit()
    {
        var runner = Port();
        var corbalan = runner.Content.Port!.Sellers.Single(s => s.Id == "seller.corbalan");
        var hob = runner.Content.Port.Sellers.Single(s => s.Id == "seller.hob");
        var pork = Lot(runner, "item.salt_pork");
        var usual = runner.PriceOf(pork);
        Assert.Equal(0, runner.CreditLimit(corbalan));
        Assert.False(runner.Buy(pork, onCredit: true).Succeeded);

        Hire(runner, 4);
        Assert.True(runner.Standing >= 2);

        Assert.True(runner.PriceOf(pork) < usual);
        Assert.True(runner.CreditLimit(corbalan) > 0);
        Assert.Equal(0, runner.CreditLimit(hob));
        var funds = runner.Campaign.Port!.Funds;
        var price = runner.PriceOf(pork);
        Assert.True(runner.Buy(pork, onCredit: true).Succeeded);
        Assert.Equal(funds, runner.Campaign.Port.Funds);
        Assert.Equal(price, runner.Campaign.Port.Owed["seller.corbalan"]);
        Assert.Contains("wants coin", runner.Buy(Lot(runner, "item.water_hob"), onCredit: true).Reason);
    }

    [Fact]
    public void ADebtUnpaidMeetsTheCaptainAtTheGangway()
    {
        var runner = Port();
        Hire(runner, 4);
        var water = Lot(runner, "item.water");
        Assert.True(runner.Buy(water, onCredit: true).Succeeded);
        var owed = runner.Campaign.Port!.Owed["seller.corbalan"];
        var corbalan = runner.CharacterFor("person.corbalan")!.Id;

        var (creditor, claim) = Assert.Single(runner.OpenClaims(), c => c.Creditor == corbalan);
        Assert.Equal(owed, claim.Amount);
        Assert.Contains(runner.DeparturePlan(), b => b.Kind == "claim" && b.Who == corbalan && b.Text.Contains(owed.ToString()));

        var funds = runner.Campaign.Port.Funds;
        Assert.True(runner.PayClaim(creditor).Succeeded);
        Assert.Equal(funds - owed, runner.Campaign.Port.Funds);
        Assert.Empty(runner.Campaign.Port.Owed);
        Assert.DoesNotContain(runner.OpenClaims(), c => c.Creditor == corbalan);
    }

    [Fact]
    public void SailingOwingCostsTheSellersTrust()
    {
        var runner = Port();
        Hire(runner, 4);
        Assert.True(runner.Buy(Lot(runner, "item.biscuit"), onCredit: true).Succeeded);
        var corbalan = runner.CharacterFor("person.corbalan")!.Id;
        var captain = runner.Campaign.CaptainId!.Value;
        var before = (runner.Campaign.Relationships.TryGetValue((corbalan, captain), out var r1) ? r1.Trust : 0);

        Assert.True(runner.RefuseClaim(corbalan).Succeeded);

        Assert.True((runner.Campaign.Relationships.TryGetValue((corbalan, captain), out var r2) ? r2.Trust : 0) < before);
        Assert.DoesNotContain(runner.OpenClaims(), c => c.Creditor == corbalan);
    }

    [Fact]
    public void PayingOffTheAccountInPortSettlesIt()
    {
        var runner = Port();
        Hire(runner, 4);
        Assert.True(runner.Buy(Lot(runner, "item.wine"), onCredit: true).Succeeded);
        var json = SaveSerializer.Serialize(runner.Campaign);
        Assert.Equal(runner.Campaign.Port!.Owed, SaveSerializer.Deserialize(json, runner.Content).Port!.Owed);

        Assert.True(runner.PayDebt("seller.corbalan").Succeeded);
        Assert.Empty(runner.Campaign.Port.Owed);
        Assert.False(runner.PayDebt("seller.corbalan").Succeeded);
    }
}
