using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.History;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class DiagnosticsAndChronicleTextTests
{
    private const int Hour = SimulationClock.TicksPerHour;
    private const double Tolerance = 1e-6;

    private static (SimulationRunner Runner, Character Sailor) Setup()
    {
        var campaign = new CampaignState(CampaignPhase.Voyage, seed: 9);
        var runner = new SimulationRunner(campaign, TestContent.Create());
        return (runner, campaign.CreateCharacter("Diego", 29, "Palos"));
    }

    [Fact]
    public void FatigueIsExplainedByWorkAndDailyLife()
    {
        var (runner, sailor) = Setup();
        runner.AssignTask(sailor.Id, "task.pump");
        runner.Advance(Hour);

        var why = runner.ExplainNeed(sailor.Id, TrackedNeed.Fatigue);

        // Pumping adds 6 per hour, the baseline 1: the bigger cause comes first.
        Assert.Equal(["task.pump", NeedLedger.Baseline], why.Select(c => c.Source));
        Assert.Equal(6.0, why[0].Amount, Tolerance);
        Assert.Equal(1.0, why[1].Amount, Tolerance);
    }

    [Fact]
    public void EatingShowsUpAsRelief()
    {
        var (runner, sailor) = Setup();
        runner.AddItems(runner.Campaign.SharedStoreId, "item.biscuit", 5);
        runner.Advance(61 * Hour);

        var why = runner.ExplainNeed(sailor.Id, TrackedNeed.Hunger);

        // The window is 23 full hours plus the current one: about +11.5 from the baseline (0.5/h),
        // and -12 from the biscuit eaten at hour 60.
        Assert.Equal(-12.0, why.Single(c => c.Source == "item.biscuit").Amount, Tolerance);
        Assert.InRange(why.Single(c => c.Source == NeedLedger.Baseline).Amount, 11.5, 12.0);
    }

    [Fact]
    public void OnlyTheLastDayIsExplained()
    {
        var (runner, sailor) = Setup();
        runner.AssignTask(sailor.Id, "task.pump");
        runner.Advance(Hour);
        runner.Advance(30 * Hour);

        var why = runner.ExplainNeed(sailor.Id, TrackedNeed.Fatigue);

        // Only 23-24 hours of daily life remain; the pumping 30 hours ago has dropped out.
        Assert.DoesNotContain(why, c => c.Source == "task.pump");
        Assert.InRange(why.Single().Amount, 23.0, 24.0);
    }

    [Fact]
    public void HealthLossIsExplainedByItsCause()
    {
        var (runner, sailor) = Setup();
        runner.AddItems(runner.Campaign.SharedStoreId, "item.biscuit", 100);
        runner.Advance(110 * Hour);

        var why = runner.ExplainNeed(sailor.Id, TrackedNeed.Health);

        // No water: thirst hits 100 at hour 100, then 4 health per hour for 10 hours.
        var dehydration = Assert.Single(why);
        Assert.Equal(ChronicleCauses.Dehydration, dehydration.Source);
        Assert.Equal(-40.0, dehydration.Amount, 0.2);
    }

    [Fact]
    public void UnknownCharacterHasNoExplanation()
    {
        var (runner, _) = Setup();

        Assert.Empty(runner.ExplainNeed(new Core.CharacterId(42), TrackedNeed.Fatigue));
    }

    [Fact]
    public void FormatterResolvesNamesAndWording()
    {
        var (runner, sailor) = Setup();
        var campaign = runner.Campaign;
        var formatter = new ChronicleFormatter(campaign, runner.Content);
        campaign.TryTransitionTo(CampaignPhase.SettlementSurvival, "skip the voyage");
        runner.AssignTask(sailor.Id, "task.pump");
        runner.CancelTask(sailor.Id);
        campaign.ChangeSurvivalState(sailor.Id, SurvivalState.Incapacitated, "condition.fever");
        campaign.ChangeSurvivalState(sailor.Id, SurvivalState.Dead, ChronicleCauses.Starvation);

        var text = campaign.Chronicle.Entries.Select(formatter.Format).ToList();

        Assert.Equal(
        [
            "Could not move from voyage to settlement survival (skip the voyage).",
            "Diego stopped pump: ordered to stop.",
            "Diego is now incapacitated (fever).",
            "Diego died (starvation).",
        ], text);
    }

    [Fact]
    public void FormatterDescribesInjuriesShortagesAndInterruptions()
    {
        var (runner, sailor) = Setup();
        sailor.Health.Value = 30;
        runner.AssignTask(sailor.Id, "task.storm_work");
        runner.Advance(10 * Hour);
        runner.Advance(30 * Hour);
        var formatter = new ChronicleFormatter(runner.Campaign, runner.Content);

        var text = runner.Campaign.Chronicle.Entries.Select(formatter.Format).ToList();

        Assert.Contains("Diego suffered bruised while doing secure the yard (-20 health, now 10).", text);
        Assert.Contains("Diego stopped secure the yard: incapacitated (bruised).", text);
        Assert.Contains("Diego went thirsty: there is nothing left to drink.", text);
    }

    [Fact]
    public void RenamingContentChangesOldEntriesWithoutTouchingThem()
    {
        var (runner, sailor) = Setup();
        runner.AssignTask(sailor.Id, "task.pump");
        runner.CancelTask(sailor.Id);
        var renamed = new Content.GameContent(
            TestContent.Rules, [], TestContent.Items,
            [TestContent.Pump with { Name = "Work the pump" }]);

        var text = new ChronicleFormatter(runner.Campaign, renamed).Format(runner.Campaign.Chronicle.Entries.Single());

        Assert.Equal("Diego stopped work the pump: ordered to stop.", text);
    }
}
