using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>Paying a hand off in port (docs/CREW_PROPOSAL.md §1).</summary>
public class PayOffTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static SimulationRunner Port() => ExpeditionScenario.Create(Shipped, CampaignState.DefaultSeed);

    private static Core.CharacterId Id(SimulationRunner runner, string person) => runner.CharacterFor(person)!.Id;

    private static int Respect(SimulationRunner runner, Core.CharacterId of) =>
        runner.Campaign.Relationships.TryGetValue((of, runner.Campaign.CaptainId!.Value), out var view) ? view.Respect : 0;

    [Fact]
    public void ThePaidOffGoAshoreAndTheBerthIsFree()
    {
        var runner = Port();
        var gil = Id(runner, "person.gil");
        Assert.True(runner.DeveloperHire(gil).Succeeded);
        var funds = runner.Campaign.Port!.Funds;

        Assert.True(runner.PayOff(gil).Succeeded);

        var port = runner.Campaign.Port!;
        Assert.DoesNotContain(gil, port.Hired);
        Assert.Equal(Membership.Local, runner.Campaign.Characters[gil].Membership);
        Assert.Equal(Shipped.Port!.Location, runner.Campaign.Placements[gil].LocationId);
        Assert.Equal(funds, port.Funds);
        Assert.True(runner.PaidOff(gil));
        Assert.Contains(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.CrewPaidOff && e.Characters.Contains(gil));
    }

    [Fact]
    public void TheyWillNotSignOnAgain()
    {
        var runner = Port();
        var gil = Id(runner, "person.gil");
        PortTests.HearTerms(runner, "person.gil");
        Assert.True(runner.Offer(gil, 900).Succeeded);
        Assert.True(runner.PayOff(gil).Succeeded);

        Assert.False(runner.Offer(gil, 900).Succeeded);
        Assert.False(runner.PayOff(gil).Succeeded);
    }

    [Fact]
    public void TheCrewThinksLessOfTheCaptainAndFriendsMore()
    {
        var runner = Port();
        var (gil, bartolo, benito) = (Id(runner, "person.gil"), Id(runner, "person.bartolo"), Id(runner, "person.pascual"));
        foreach (var hand in new[] { gil, bartolo, benito })
        {
            Assert.True(runner.DeveloperHire(hand).Succeeded);
        }

        runner.Campaign.ChangeRelationship(bartolo, gil, RelationshipDimension.Affection, 40, "test");
        var (before, friendBefore) = (Respect(runner, benito), Respect(runner, bartolo));

        Assert.True(runner.PayOff(gil).Succeeded);

        Assert.Equal(before - 5, Respect(runner, benito));
        Assert.Equal(friendBefore - 10, Respect(runner, bartolo));
    }

    [Fact]
    public void ForADayThePortTalksAndHandsAskMore()
    {
        var runner = Port();
        var gil = Id(runner, "person.gil");
        var bartolo = Id(runner, "person.bartolo");
        runner.DeveloperHire(gil);
        PortTests.HearTerms(runner, "person.bartolo");
        Assert.True(runner.PayOff(gil).Succeeded);

        // Bartolo would take 1000; while the port talks, he wants 1100.
        var asked = runner.Offer(bartolo, 1000);
        Assert.False(asked.Succeeded);
        Assert.Contains("turn men off", asked.Reason);
        Assert.Contains("1100", asked.Reason);

        runner.Advance((int)SimulationClock.TicksPerDay);
        Assert.False(runner.TalkedOfForPayingOff);
        PortTests.ApproachPerson(runner, "person.bartolo");
        var later = runner.Offer(bartolo, 1000);
        Assert.True(later.Succeeded, later.Reason);
    }

    [Fact]
    public void OnlyInPortAndOnlyThoseSignedOn()
    {
        var runner = Port();
        Assert.False(runner.PayOff(Id(runner, "person.gil")).Succeeded);
    }
}
