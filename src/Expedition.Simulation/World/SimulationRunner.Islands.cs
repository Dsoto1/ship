using System.Globalization;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// Calling at an island for water (VOYAGE_EXPERIENCE §7, §27; NAVIGATION §21). The captain may make for
/// a charted island off the route, or go looking for one only reported. She steers for where the chart
/// puts it; the masthead raises it (by day, with a lookout; at night near it she lies to for daylight),
/// and she runs in by eye and anchors. At anchor the boat goes in to water and forage; a charted island
/// also puts the reckoning right. A reported island may not be there at all, or not where it is drawn:
/// reaching its place on the chart with nothing in sight, she gives it up.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>The island she lies at anchor off, or null.</summary>
    public Island? AnchoredAt =>
        Campaign.Voyage is { Outcome: VoyageOutcome.UnderWay, Navigation.Call: { Anchored: true } call } ? IslandById(call.Island) : null;

    private Island? IslandById(string id) => Islands.FirstOrDefault(i => i.Id == id);

    /// <summary>
    /// The islands of this voyage's ocean: those every voyage has, and one of each band, picked by the
    /// campaign's seed so every replay agrees and nothing need be saved.
    /// </summary>
    public IReadOnlyList<Island> Islands =>
        _islands ??= (Content.Voyage?.Route?.Islands ?? [])
            .Where(i => i.Band is null || PickedInBand(i)).ToList();

    private IReadOnlyList<Island>? _islands;

    private bool PickedInBand(Island island)
    {
        var band = (Content.Voyage?.Route?.Islands ?? []).Where(i => i.Band == island.Band).OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
        var pick = (int)(SeedHash(island.Band!, Campaign.Seed) % (ulong)band.Count);
        return band[pick].Id == island.Id;
    }

    // A number from a name and the seed, the same on every replay.
    private static ulong SeedHash(string name, ulong seed)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in name)
        {
            hash = (hash ^ c) * 1099511628211UL;
        }

        var z = (seed ^ hash) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>
    /// Where an island truly is, and whether it is there at all: a charted island is where the chart says;
    /// a reported one is decided by the voyage's seed and its ID, so every replay agrees, and nothing is saved.
    /// </summary>
    public (bool Exists, ChartPoint At) IslandTruth(Island island)
    {
        if (!island.Reported)
        {
            return (true, island.Charted);
        }

        var hash = 14695981039346656037UL;
        foreach (var c in island.Id)
        {
            hash = (hash ^ c) * 1099511628211UL;
        }

        double Next()
        {
            hash += 0x9E3779B97F4A7C15UL;
            var z = (Campaign.Seed ^ hash) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (z >> 11) * (1.0 / (1UL << 53));
        }

        var exists = Next() < island.ExistsChance;
        var angle = Next() * Math.Tau;
        var off = Math.Sqrt(Next()) * island.PositionError;
        return (exists, new ChartPoint(island.X + Math.Sin(angle) * off, island.Y + Math.Cos(angle) * off));
    }

    // How near where the chart puts an island (by her reckoning) she lies to and looks about for it, leagues.
    private const double SearchLeagues = 0.8;

    // Hours of daylight she searches before giving it up.
    private const double SearchHours = 3;

    // True while she makes no way for an island: at anchor, or lying to for daylight near where it should be.
    private bool HeldForIsland(VoyageState ship)
    {
        if (ship.Navigation is not { Call: { } call } navigation || IslandById(call.Island) is not { } island)
        {
            return false;
        }

        if (call.Anchored)
        {
            return true;
        }

        // Near where it should be and not yet seen: by night she lies to for daylight, by day she cruises
        // about the place looking for it rather than running past.
        return !call.Sighted && navigation.Reckoned.DistanceTo(island.Charted) <= SearchLeagues;
    }

    // Before she sails: the course for the island, if she has made for one.
    private void SteerForIsland(Navigation navigation)
    {
        navigation.Target = navigation.Call is { } call && IslandById(call.Island) is { } island
            ? call.SeenAt ?? island.Charted
            : null;
    }

    // After she sails: raising the island, anchoring off it, or giving up a reported one not there.
    private void CallAtIsland(VoyageState ship, Navigation navigation, WeatherDefinition weather)
    {
        if (navigation.Call is not { Anchored: false } call || IslandById(call.Island) is not { } island)
        {
            return;
        }

        var (exists, at) = IslandTruth(island);
        if (!call.Sighted)
        {
            var day = Campaign.Clock.TimeOfDay.TotalHours is >= 6 and < 19;
            var range = (day ? island.SightingLeagues * weather.Seeing : 1.2 * (1 - 0.7 * weather.Fog)) * (ship.WatchKept ? 1 : 0.15);
            if (exists && navigation.True.DistanceTo(at) <= range)
            {
                var seen = new ChartPoint(navigation.Reckoned.X + at.X - navigation.True.X, navigation.Reckoned.Y + at.Y - navigation.True.Y);
                navigation.Call = call with { Sighted = true, SeenAt = seen };
                Campaign.Record(ChronicleEntryKind.IslandSighted, [
                    new(ChronicleParameters.To, island.Id), new(ChronicleParameters.Level, navigation.True.DistanceTo(at).ToString("0", CultureInfo.InvariantCulture)),
                ]);
            }
            else if (day && navigation.Reckoned.DistanceTo(island.Charted) <= SearchLeagues)
            {
                // Where the chart puts it, nothing in sight from the masthead: she searches a while, then gives it up.
                navigation.Call = call with { Searched = call.Searched + HoursPerTick };
                if (call.Searched + HoursPerTick >= SearchHours)
                {
                    navigation.Call = null;
                    navigation.Target = null;
                    Campaign.Record(ChronicleEntryKind.IslandNotFound, [new(ChronicleParameters.To, island.Id)]);
                    MarkChart($"No {island.Name} here", island.Charted);
                }
            }

            return;
        }

        // In sight, she steers for it by eye: wherever the reckoning has got to, it bears where it is seen.
        navigation.Call = call = call with { SeenAt = new ChartPoint(navigation.Reckoned.X + at.X - navigation.True.X, navigation.Reckoned.Y + at.Y - navigation.True.Y) };
        if (navigation.True.DistanceTo(at) <= 0.8)
        {
            navigation.Call = call with { Anchored = true };
            navigation.Target = null;
            if (!island.Reported)
            {
                // A charted island fixes her place: latitude and longitude both.
                navigation.Reckoned = navigation.True;
                navigation.Mark();
            }

            Campaign.Record(ChronicleEntryKind.Anchored, [new(ChronicleParameters.To, island.Id), new(ChronicleParameters.Reason, island.Reported ? "found" : "fix")]);
            MarkChart($"{island.Name}: anchored here", navigation.Reckoned);
        }
    }

    /// <summary>
    /// The captain makes for an island: she leaves the passage and steers for where the chart puts it.
    /// Recorded in the chronicle. <see cref="ResumePassage"/> takes her back to it.
    /// </summary>
    public CommandResult MakeForIsland(string islandId)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay, Navigation: { Sighted: false } navigation } || IslandById(islandId) is not { } island)
        {
            return CommandResult.Rejected("There is no such island to make for, or the coast is already in sight.");
        }

        if (navigation.Call is { Anchored: true })
        {
            return CommandResult.Rejected("She is at anchor: weigh first.");
        }

        navigation.Call = new IslandCall { Island = island.Id };
        SteerForIsland(navigation);
        navigation.Heading = navigation.Steer(navigation.CourseFrom(navigation.Reckoned));
        Campaign.Record(ChronicleEntryKind.MakingForIsland, [new(ChronicleParameters.To, island.Id)]);
        return CommandResult.Ok();
    }

    /// <summary>
    /// Developer tool for testing and screenshots: puts her some leagues up-wind of an island (where it truly
    /// is), the reckoning right, and makes for it.
    /// </summary>
    public CommandResult DeveloperPlaceNearIsland(string islandId)
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay, Navigation: { Sighted: false } navigation } || IslandById(islandId) is not { } island)
        {
            return CommandResult.Rejected("No voyage, or no such island.");
        }

        var (_, at) = IslandTruth(island);
        navigation.Call = null;
        navigation.True = navigation.Reckoned = new ChartPoint(at.X + 4, at.Y + 3);
        return MakeForIsland(islandId);
    }

    /// <summary>How many lots of this the islanders where she lies still have, or 0.</summary>
    public int LotsLeft(IslandGoods goods) =>
        AnchoredAt is { } island && Campaign.Voyage is { } ship ? goods.Stock - ship.Bought.GetValueOrDefault($"{island.Id}|{goods.Item}") : 0;

    /// <summary>
    /// Buys a lot from the islanders where she lies at anchor, from the purse: rafted out to her and stowed.
    /// Recorded in the chronicle.
    /// </summary>
    public CommandResult BuyFromIslanders(string itemId)
    {
        if (AnchoredAt is not { } island || Campaign.Voyage is not { } ship || island.Sells.FirstOrDefault(g => g.Item == itemId) is not { } goods)
        {
            return CommandResult.Rejected("There is no one here selling that.");
        }

        if (LotsLeft(goods) <= 0)
        {
            return CommandResult.Rejected("They have no more of that to sell.");
        }

        if (ship.Funds < goods.Price)
        {
            return CommandResult.Rejected($"That is {goods.Price} sueldos, and the purse holds {ship.Funds}.");
        }

        var stowed = Campaign.SharedStore.TryAdd(goods.Item, goods.Quantity, Content.Items);
        if (!stowed.Succeeded)
        {
            return CommandResult.Rejected($"There is no room for it below: {stowed.Reason}");
        }

        ship.Funds -= goods.Price;
        ship.Buy($"{island.Id}|{goods.Item}");
        Campaign.Record(ChronicleEntryKind.IslandTrade, [
            new(ChronicleParameters.To, goods.Item), new(ChronicleParameters.Level, goods.Quantity.ToString(CultureInfo.InvariantCulture)),
            new(ChronicleParameters.Need, goods.Price.ToString(CultureInfo.InvariantCulture)), new(ChronicleParameters.From, island.Id),
        ]);
        return CommandResult.Ok();
    }

    /// <summary>Back to the passage: from making for an island, or weighing anchor off one.</summary>
    public CommandResult ResumePassage()
    {
        if (Campaign.Voyage is not { Outcome: VoyageOutcome.UnderWay, Navigation: { Call: { } call } navigation })
        {
            return CommandResult.Rejected("She is on her passage already.");
        }

        navigation.Call = null;
        navigation.Target = null;
        navigation.Heading = navigation.Steer(navigation.CourseFrom(navigation.Reckoned));
        Campaign.Record(ChronicleEntryKind.WeighedAnchor, [new(ChronicleParameters.To, call.Island), new(ChronicleParameters.Reason, call.Anchored ? "weighed" : "gave up")]);
        return CommandResult.Ok();
    }
}
