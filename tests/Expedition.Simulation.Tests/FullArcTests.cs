using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.History;
using Expedition.Simulation.Scenarios;

namespace Expedition.Simulation.Tests;

/// <summary>A campaign played out ashore to the relief ship by a careful captain (docs/SETTLEMENT_VALIDATION.md).</summary>
public sealed class FullArcTests
{
    private static Content.GameContent Load() => ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    [Fact]
    public void ACarefulCaptainAtPuntaCenizaKeepsTheCharter()
    {
        var runner = TestDeckScenario.Create(Load(), Campaign.CampaignState.DefaultSeed);
        runner.Advance(60);
        runner.DeveloperLand("coast.punta_ceniza");
        // An interpreter, as the expedition would have hired one.
        runner.Campaign.Crew.First(c => c.Id != runner.Campaign.CaptainId).Languages.Set("language.ahuneri", 3);
        var captain = new TestCaptain(runner);
        captain.Play(185 * 24);

        var judged = Assert.Single(runner.Campaign.Chronicle.Entries, e => e.Kind == ChronicleEntryKind.CharterJudged);
        Assert.True(judged.Get(ChronicleParameters.Outcome) == "kept", $"{judged.Get(ChronicleParameters.Reason)}; survivor {runner.Campaign.World!.Survivor}; " + string.Join(" | ", captain.Log.Where(l => l.Contains("visit") || l.Contains("Gaspar") || l.Contains("wood party"))));
        Assert.True(runner.Campaign.Crew.Count(c => c.SurvivalState != SurvivalState.Dead) >= 4);
        Assert.True(runner.Campaign.Camp!.Buildings.Count >= 6);
        // He met Gaspar Lezo at Mahenu and took him in.
        Assert.Equal(3, runner.Campaign.World!.Survivor);
        Assert.Contains(runner.Campaign.Crew, c => c.Name == "Gaspar Lezo" && c.SurvivalState != SurvivalState.Dead);
        // Meetings end: no people stays talking for days.
        Assert.True(captain.Log.Count(l => l.StartsWith("day", StringComparison.Ordinal)) < 400);
    }
}
