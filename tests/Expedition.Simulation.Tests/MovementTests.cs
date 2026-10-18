using System.Text.Json.Nodes;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

public class MovementTests
{
    private const string Deck = "location.deck";
    private static readonly GridPosition TopLeft = new(0, 0);
    private static readonly GridPosition BottomLeft = new(0, 4);

    private static (SimulationRunner Runner, Character Sailor) Setup(GridPosition? start = null)
    {
        var campaign = new CampaignState(CampaignPhase.Voyage, seed: 3);
        var runner = new SimulationRunner(campaign, TestContent.Create());
        var sailor = campaign.CreateCharacter("Rodrigo", 27, "Triana");
        Assert.True(runner.PlaceCharacter(sailor.Id, Deck, start ?? TopLeft).Succeeded);
        return (runner, sailor);
    }

    [Fact]
    public void PathGoesThroughTheGapAndIsShortest()
    {
        var path = Pathfinder.FindPath(TestContent.Deck, TopLeft, BottomLeft)!;

        // Around the wall through (5, 2): 6 steps to (5, 1), 2 down, 6 back to (0, 4).
        Assert.Equal(15, path.Count);
        Assert.Equal(TopLeft, path[0]);
        Assert.Equal(BottomLeft, path[^1]);
        Assert.Contains(new GridPosition(5, 2), path);
        Assert.All(path, cell => Assert.True(TestContent.Deck.IsWalkable(cell)));
        Assert.All(path.Zip(path.Skip(1)), step => Assert.Equal(1, step.First.ManhattanDistance(step.Second)));
    }

    [Fact]
    public void PathfindingIsDeterministic()
    {
        Assert.Equal(
            Pathfinder.FindPath(TestContent.Deck, TopLeft, new GridPosition(6, 4)),
            Pathfinder.FindPath(TestContent.Deck, TopLeft, new GridPosition(6, 4)));
    }

    [Fact]
    public void UnreachableAndBlockedTargetsHaveNoPath()
    {
        var sealedDeck = TestContent.Deck with { Rows = [".......", "#######", "......."] };

        Assert.Null(Pathfinder.FindPath(sealedDeck, TopLeft, new GridPosition(0, 2)));
        Assert.Null(Pathfinder.FindPath(TestContent.Deck, TopLeft, new GridPosition(0, 2)));
        Assert.Single(Pathfinder.FindPath(TestContent.Deck, TopLeft, TopLeft)!);
    }

    [Fact]
    public void PlacingOnAWallOrUnknownLocationIsRejected()
    {
        var (runner, sailor) = Setup();

        Assert.False(runner.PlaceCharacter(sailor.Id, Deck, new GridPosition(0, 2)).Succeeded);
        Assert.False(runner.PlaceCharacter(sailor.Id, Deck, new GridPosition(9, 9)).Succeeded);
        Assert.False(runner.PlaceCharacter(sailor.Id, "location.atlantis", TopLeft).Succeeded);
        Assert.Equal(TopLeft, runner.Campaign.Placements[sailor.Id].Cell);
    }

    [Fact]
    public void WalkTakesTimeAndArrives()
    {
        var (runner, sailor) = Setup();

        Assert.True(runner.MoveCharacter(sailor.Id, BottomLeft).Succeeded);
        var placement = runner.Campaign.Placements[sailor.Id];

        // 14 steps at 4 cells per minute: arrives after 4 ticks.
        Assert.Equal(4, placement.Movement!.ArriveTick);
        runner.Advance(3);
        Assert.True(placement.IsMoving);
        Assert.NotEqual(TopLeft, placement.CellAt(runner.Campaign.Clock.Tick));
        runner.Advance(1);
        Assert.False(placement.IsMoving);
        Assert.Equal(BottomLeft, placement.Cell);
    }

    [Fact]
    public void DrawingPositionMovesSmoothlyAlongThePath()
    {
        var movement = new Movement([new(0, 0), new(1, 0), new(2, 0)], StartTick: 10, ArriveTick: 14);

        Assert.Equal((0.0, 0.0), movement.PositionAt(10));
        Assert.Equal((1.0, 0.0), movement.PositionAt(12));
        Assert.Equal((1.5, 0.0), movement.PositionAt(13));
        Assert.Equal((2.0, 0.0), movement.PositionAt(99));
    }

    [Fact]
    public void SpotTaskWalksToTheNearestSpotThenWorks()
    {
        var (runner, sailor) = Setup();

        Assert.True(runner.AssignTask(sailor.Id, "task.spot_pump").Succeeded);
        var task = runner.Campaign.ActiveTasks[sailor.Id];

        // From (0, 0) the right-hand pump at (6, 4) is 10 steps away; the left one is 14.
        Assert.Equal("spot.pump_b", task.SpotId);
        runner.Advance(2);
        Assert.Equal(0, task.Progress);

        // 10 steps at 4 cells per minute: arrives at tick 3 and works from then on,
        // so 60 minutes of work finish at tick 62.
        var outcome = Assert.Single(runner.Advance(100));
        Assert.Equal(TaskEnd.Completed, outcome.End);
        Assert.Equal(62, outcome.Tick);
        Assert.Equal(new GridPosition(6, 4), runner.Campaign.Placements[sailor.Id].Cell);
    }

    [Fact]
    public void NearerSpotIsPreferred()
    {
        var (runner, sailor) = Setup(new GridPosition(0, 3));

        runner.AssignTask(sailor.Id, "task.spot_pump");

        Assert.Equal("spot.pump_a", runner.Campaign.ActiveTasks[sailor.Id].SpotId);
    }

    [Fact]
    public void SpotsHoldOneWorkerEachAndAreFreedWhenWorkEnds()
    {
        var (runner, first) = Setup();
        var second = runner.Campaign.CreateCharacter("Beatriz", 33, "Huelva");
        var third = runner.Campaign.CreateCharacter("Martin", 45, "Palos");
        runner.PlaceCharacter(second.Id, Deck, TopLeft);
        runner.PlaceCharacter(third.Id, Deck, TopLeft);

        Assert.True(runner.AssignTask(first.Id, "task.spot_pump").Succeeded);
        Assert.True(runner.AssignTask(second.Id, "task.spot_pump").Succeeded);
        var refused = runner.AssignTask(third.Id, "task.spot_pump");

        Assert.False(refused.Succeeded);
        Assert.Contains("already in use", refused.Reason);
        Assert.NotEqual(runner.Campaign.ActiveTasks[first.Id].SpotId, runner.Campaign.ActiveTasks[second.Id].SpotId);

        runner.CancelTask(first.Id);
        Assert.True(runner.AssignTask(third.Id, "task.spot_pump").Succeeded);
    }

    [Fact]
    public void UnplacedCharacterCannotDoSpotWorkButCanRest()
    {
        var campaign = new CampaignState(CampaignPhase.Voyage);
        var runner = new SimulationRunner(campaign, TestContent.Create());
        var sailor = campaign.CreateCharacter("Rodrigo", 27, "Triana");

        var result = runner.AssignTask(sailor.Id, "task.spot_pump");

        Assert.False(result.Succeeded);
        Assert.Contains("not at any location", result.Reason);
        Assert.True(runner.AssignTask(sailor.Id, "task.rest").Succeeded);
    }

    [Fact]
    public void OrderingAMoveInterruptsTheCurrentTask()
    {
        var (runner, sailor) = Setup();
        runner.AssignTask(sailor.Id, "task.spot_pump");

        Assert.True(runner.MoveCharacter(sailor.Id, new GridPosition(3, 0)).Succeeded);

        Assert.Empty(runner.Campaign.ActiveTasks);
        var entry = Assert.Single(runner.Campaign.Chronicle.Entries);
        Assert.Equal(ChronicleEntryKind.TaskInterrupted, entry.Kind);
        Assert.Equal(InterruptReasons.Moved, entry.Get(ChronicleParameters.Reason));
        Assert.Equal("task.spot_pump", entry.Get(ChronicleParameters.Task));
    }

    [Fact]
    public void IncapacitationStopsAWalkWhereTheCharacterIs()
    {
        var (runner, sailor) = Setup();
        runner.MoveCharacter(sailor.Id, BottomLeft);
        runner.Advance(2);
        var placement = runner.Campaign.Placements[sailor.Id];
        var reached = placement.CellAt(runner.Campaign.Clock.Tick);

        runner.Campaign.ChangeSurvivalState(sailor.Id, SurvivalState.Incapacitated, "slipped on the wet deck");

        Assert.False(placement.IsMoving);
        Assert.Equal(reached, placement.Cell);
        Assert.False(runner.MoveCharacter(sailor.Id, TopLeft).Succeeded);
    }

    [Fact]
    public void UnreachableMoveIsRejected()
    {
        var (runner, sailor) = Setup();

        var result = runner.MoveCharacter(sailor.Id, new GridPosition(0, 2));

        Assert.False(result.Succeeded);
        Assert.False(runner.Campaign.Placements[sailor.Id].IsMoving);
    }

    [Fact]
    public void SaveDuringAWalkContinuesIdentically()
    {
        var (original, sailor) = Setup();
        var second = original.Campaign.CreateCharacter("Beatriz", 33, "Huelva");
        original.PlaceCharacter(second.Id, Deck, new GridPosition(6, 0));
        original.AssignTask(sailor.Id, "task.spot_pump");
        original.MoveCharacter(second.Id, new GridPosition(6, 4));
        original.Advance(2);

        var json = SaveSerializer.Serialize(original.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, TestContent.Create()), TestContent.Create());
        Assert.Equal(json, SaveSerializer.Serialize(loaded.Campaign));
        Assert.True(loaded.Campaign.Placements[sailor.Id].IsMoving);

        original.Advance(120);
        loaded.Advance(120);
        Assert.Equal(SaveSerializer.Serialize(original.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }

    [Theory]
    [InlineData("wall cell")]
    [InlineData("unknown location")]
    [InlineData("broken path")]
    [InlineData("unknown spot")]
    [InlineData("unknown character")]
    public void TamperedPositionsAreRejected(string tampering)
    {
        var (runner, sailor) = Setup();
        runner.AssignTask(sailor.Id, "task.spot_pump");
        runner.Advance(1);
        var json = SaveSerializer.Serialize(runner.Campaign);
        var root = JsonNode.Parse(json)!.AsObject();
        var placement = root["placements"]!.AsArray()[0]!;
        switch (tampering)
        {
            case "wall cell": placement["y"] = 2; break;
            case "unknown location": placement["locationId"] = "location.atlantis"; break;
            case "broken path": placement["movement"]!["path"]!.AsArray()[1] = new JsonArray(6, 4); break;
            case "unknown spot": root["activeTasks"]!.AsArray()[0]!["spotId"] = "spot.nowhere"; break;
            case "unknown character": placement["characterId"] = 99; break;
        }

        Assert.Throws<SaveLoadException>(() => SaveSerializer.Deserialize(root.ToJsonString(), TestContent.Create()));
    }
}
