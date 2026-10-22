using Expedition.Simulation.Content;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Shore;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Tests;

/// <summary>The camp's ground, made for each site and voyage (Shore.CampGround).</summary>
public sealed class CampGroundTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
    private static LocationDefinition Plan => Shipped.Locations[SimulationRunner.CampLocation];
    private static readonly string[] Grounds = ["forest", "headland", "river", "dunes", "mangrove"];

    [Fact]
    public void TheSameSiteAndSeedAlwaysGiveTheSameGround()
    {
        var once = CampGround.Make(Plan, "coast.punta_ceniza", "headland", 1519);
        var again = CampGround.Make(Plan, "coast.punta_ceniza", "headland", 1519);
        Assert.Equal(once.Rows, again.Rows);
    }

    [Fact]
    public void EachVoyageAndSiteHasItsOwnForestEdge()
    {
        var maps = Enumerable.Range(0, 8).Select(seed => string.Concat(CampGround.Make(Plan, "coast.punta_ceniza", "headland", (ulong)seed).Rows)).ToHashSet();
        Assert.True(maps.Count >= 7);
        Assert.NotEqual(CampGround.Make(Plan, "coast.punta_ceniza", "headland", 7).Rows, CampGround.Make(Plan, "coast.rio_bermejo", "headland", 7).Rows);
    }

    [Fact]
    public void TheRiverIsThickAndTheDunesOpen()
    {
        int Trees(string ground) => Enumerable.Range(0, 10).Sum(seed => CampGround.Make(Plan, "coast.x", ground, (ulong)seed).Rows.Skip(14).Sum(r => r.Count(c => c == '#')));
        Assert.True(Trees("river") > Trees("forest"));
        Assert.True(Trees("forest") > Trees("dunes"));
    }

    [Fact]
    public void EveryWorkPlaceStaysOpenAndReachableFromTheHearth()
    {
        foreach (var ground in Grounds)
        {
            for (ulong seed = 0; seed < 40; seed++)
            {
                var map = CampGround.Make(Plan, "coast.punta_ceniza", ground, seed);
                var hearth = map.FindSpot("spot.hearth")!;
                var reached = new HashSet<(int, int)> { (hearth.X, hearth.Y) };
                var queue = new Queue<(int X, int Y)>([(hearth.X, hearth.Y)]);
                while (queue.Count > 0)
                {
                    var (x, y) = queue.Dequeue();
                    foreach (var next in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    {
                        if (map.IsWalkable(new GridPosition(next.Item1, next.Item2)) && reached.Add(next))
                        {
                            queue.Enqueue(next);
                        }
                    }
                }

                Assert.All(map.Spots, s => Assert.Contains((s.X, s.Y), reached));
                // The camp's own ground, where the shelters go up, is never wooded.
                Assert.All(map.Rows.Skip(1).Take(13), r => Assert.DoesNotContain('#', r[1..^1]));
            }
        }
    }

    [Fact]
    public void TheCampIsOnItsOwnGroundAndItIsMadeAgainOnLoading()
    {
        var runner = TestDeckScenario.Create(Shipped, 7);
        runner.Advance(60);
        Assert.True(runner.DeveloperLand("coast.rio_bermejo").Succeeded);
        var ground = runner.Location(SimulationRunner.CampLocation);
        Assert.Equal(CampGround.Make(Plan, "coast.rio_bermejo", "river", 7).Rows, ground.Rows);
        Assert.NotEqual(Plan.Rows, ground.Rows);
        runner.Advance(12 * 60);
        var json = SaveSerializer.Serialize(runner.Campaign);
        var loaded = new SimulationRunner(SaveSerializer.Deserialize(json, Shipped), Shipped);
        Assert.Equal(ground.Rows, loaded.Location(SimulationRunner.CampLocation).Rows);
        runner.Advance(24 * 60);
        loaded.Advance(24 * 60);
        Assert.Equal(SaveSerializer.Serialize(runner.Campaign), SaveSerializer.Serialize(loaded.Campaign));
    }
}
