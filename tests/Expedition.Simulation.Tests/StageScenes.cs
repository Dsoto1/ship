using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.World;
using Xunit.Abstractions;

namespace Expedition.Simulation.Tests;

/// <summary>
/// Stages the settlement validation scenes (docs/SETTLEMENT_VALIDATION.md): the careful captain plays the
/// arc at Punta Ceniza and the campaign is saved at each scene's moment, to be opened in the game with
/// <c>--load=PATH</c>. Runs only with STAGE_SCENES set to the folder to write to.
/// </summary>
public sealed class StageScenes(ITestOutputHelper output)
{
    [Fact]
    public void SavesEachSceneForTheGame()
    {
        if (Environment.GetEnvironmentVariable("STAGE_SCENES") is not { Length: > 0 } dir)
        {
            return;
        }

        Directory.CreateDirectory(dir);
        var content = Content.ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        var runner = TestDeckScenario.Create(content, Campaign.CampaignState.DefaultSeed);
        runner.Advance(60);
        runner.DeveloperLand("coast.punta_ceniza");
        runner.Campaign.Crew.First(c => c.Id != runner.Campaign.CaptainId).Languages.Set("language.ahuneri", 3);
        var captain = new TestCaptain(runner);
        void Save(string name)
        {
            File.WriteAllText(Path.Combine(dir, name + ".json"), SaveSerializer.Serialize(runner.Campaign));
            output.WriteLine($"{name}: day {runner.CampDay:0.00} hour {runner.Campaign.Clock.Tick % 1440 / 60.0}");
        }

        void PlayTo(double day)
        {
            while (runner.CampDay < day)
            {
                captain.Play(1);
            }
        }

        var crisis = false;
        PlayTo(14 / 24.0);
        Save("a-first-night");
        PlayTo(14.25);
        Save("b-two-weeks");
        while (runner.CampDay < 170)
        {
            var seen = runner.Campaign.Chronicle.Entries.Count;
            captain.Play(1);
            var fresh = runner.Campaign.Chronicle.Entries.Skip(seen).ToList();
            foreach (var e in fresh.Where(e => e.Kind is ChronicleEntryKind.StormAshore or ChronicleEntryKind.SicknessSpreads or ChronicleEntryKind.SupplyShortage))
            {
                output.WriteLine($"  day {runner.CampDay:0.0}: {e.Kind}");
            }

            if (!crisis && fresh.Any(e => e.Kind == ChronicleEntryKind.StormAshore))
            {
                crisis = true;
                captain.Play(2);
                Save("c-crisis");
            }
        }

        PlayTo(170.3);
        Save("d-established");
        PlayTo(179.9);
        Save("relief-eve");
    }
}
