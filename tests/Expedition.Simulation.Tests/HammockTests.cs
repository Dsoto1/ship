using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Hammocks in the hold: more berths, and the crowding that comes with them (docs/CREW_PROPOSAL.md §2).</summary>
public class HammockTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Port() => ExpeditionScenario.Create(Shipped, CampaignState.DefaultSeed);

    private static readonly string[] Seekers =
        ["person.velho", "person.pons", "person.bartolo", "person.leonor", "person.tome", "person.ximena", "person.gil", "person.rodrigo", "person.nuno", "person.wat"];

    [Fact]
    public void AHammockIsOneMoreBerthForItsPrice()
    {
        var runner = Port();
        var funds = runner.Campaign.Port!.Funds;
        Assert.Equal(7, runner.BerthsAboard);

        Assert.True(runner.SlingHammock().Succeeded);

        Assert.Equal(8, runner.BerthsAboard);
        Assert.Equal(funds - 150, runner.Campaign.Port!.Funds);
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.HammockSlung);
    }

    [Fact]
    public void TheHoldTakesThreeAtMost()
    {
        var runner = Port();
        for (var i = 0; i < 3; i++)
        {
            Assert.True(runner.SlingHammock().Succeeded);
        }

        Assert.False(runner.SlingHammock().Succeeded);
        Assert.Equal(10, runner.BerthsAboard);
    }

    [Fact]
    public void NotWithoutTheMoney()
    {
        var runner = Port();
        runner.Campaign.Port!.Funds = 100;

        Assert.Contains("150 sueldos", runner.SlingHammock().Reason);
    }

    [Fact]
    public void WithHammocksMoreCanSignAndTheShipIsCrowded()
    {
        var runner = Port();
        foreach (var person in Seekers.Take(7))
        {
            Assert.True(runner.DeveloperHire(runner.CharacterFor(person)!.Id).Succeeded);
        }

        Assert.Equal(0, runner.Crowded);

        PortTests.HearTerms(runner, Seekers[7]);
        Assert.Contains("hammocks", runner.Offer(runner.CharacterFor(Seekers[7])!.Id, 850).Reason);

        runner.SlingHammock();
        PortTests.ApproachPerson(runner, Seekers[7]);
        var offered = runner.Offer(runner.CharacterFor(Seekers[7])!.Id, 850);
        Assert.True(offered.Succeeded, offered.Reason);
        Assert.Equal(1, runner.Crowded);
    }

    [Fact]
    public void HammocksAreKeptInTheSave()
    {
        var runner = Port();
        runner.SlingHammock();
        runner.SlingHammock();

        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = SaveSerializer.Deserialize(json, Shipped);

        Assert.Equal(2, loaded.Port!.Hammocks);
    }

    [Fact]
    public void CrowdedAtSeaFeverComesEasier()
    {
        var runner = TestDeckScenario.Create(Shipped);
        var person = runner.Campaign.Crew.First(c => c.Id != runner.Campaign.CaptainId);
        double Fever() => runner.OnsetChances(person, [], wet: true).Single(c => c.Illness == "condition.fever").Chance;
        var roomy = Fever();
        Assert.Equal(0, runner.Crowded);

        var hands = runner.Campaign.Crew.Count(c => c.Id != runner.Campaign.CaptainId);
        for (var i = hands; i < Shipped.Port!.Berths + 2; i++)
        {
            runner.Campaign.CreateCharacter($"Extra {i}", 25, "Triana").Membership = Membership.Crew;
        }

        Assert.Equal(2, runner.Crowded);
        Assert.Equal(roomy * 1.2, Fever(), 6);
        Assert.Contains("crowded", runner.OnsetChances(person, [], wet: true).Single(c => c.Illness == "condition.fever").Reasons);
    }
}
