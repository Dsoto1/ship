using System.Globalization;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Items;
using Expedition.Simulation.Shore;

namespace Expedition.Simulation.World;

/// <summary>
/// The move (docs/THE_MOVE.md; ECONOMY_LOOP §11, §46): the first camp is where the boat could land, not where
/// the expedition was sent. Parties go and look over other sites; the camp is moved to one by the people
/// themselves, carrying what they can, the boat taking more along the shore; what is left still lies in the
/// old camp's stores, and is fetched by parties sent back for it. Nothing moves that no one carried.
/// </summary>
public sealed partial class SimulationRunner
{
    // What one person carries on the move or fetching, kg; what the boat takes along the shore.
    private const double PackKg = 25;
    private const double BoatLoadKg = 500;

    /// <summary>The camp left behind at <paramref name="placeId"/>'s site, if the expedition camped there before.</summary>
    public CampState? LeftCampAt(string placeId) =>
        Region?.Place(placeId)?.Site is { } site ? Campaign.LeftCamps.GetValueOrDefault(site) : null;

    /// <summary>What still lies in a camp's stores, by item.</summary>
    public IReadOnlyDictionary<string, int> StoresOf(CampState camp) =>
        camp.StoreId is { } id && Campaign.Inventories.TryGetValue(new InventoryId(id), out var store) ? store.Quantities : new Dictionary<string, int>();

    /// <summary>True if a party has been to <paramref name="placeId"/>'s site and looked it over (or the camp is there).</summary>
    public bool Surveyed(string placeId) =>
        Region?.Place(placeId)?.Site is { } site
        && (Campaign.Camp?.Site == site || Campaign.LeftCamps.ContainsKey(site) || Campaign.World?.Surveyed.Contains(site) == true);

    /// <summary>
    /// A site looked over as a place to settle: its water, food, building stuff, the fever, how it could be held,
    /// whose ground it is, how far the red wood is, and whether it is the charter's. Empty if it is not a
    /// landing site or no party has been there.
    /// </summary>
    public IReadOnlyList<(string Topic, string Text)> Survey(string placeId)
    {
        if (Region is not { } region || region.Place(placeId) is not { Site: { } siteId } place || Content.Shore is not { } shore
            || shore.Sites.All(s => s.Id != siteId) || !Surveyed(placeId))
        {
            return [];
        }

        var site = shore.Site(siteId);
        var lines = new List<(string, string)>
        {
            ("The charter", siteId == region.Charter.Site
                ? "The Company's own ground: the factory is to stand here again."
                : $"Not the charter's ground: the Company wants the factory on {SiteName(region.Charter.Site)}."),
        };
        var water = site.Water.OrderBy(w => w.Kind == "brackish" ? 1 : 0).ThenBy(w => w.Walk).FirstOrDefault();
        lines.Add(("Water", water is null ? "None found." : $"{Capitalise(water.Name)}: {(water.Kind == "brackish" ? "brackish" : "sweet")}, {water.Walk} minutes' walk."));
        lines.Add(("Food", site.Food.Count == 0 ? "Nothing to be had near." : string.Join("; ", site.Food.Select(f => f.Name)) + "."));
        var building = new List<string> { site.Brush ? "poles and brush at the forest's edge" : "no brush: poles must be brought" };
        building.Add(site.Stone ? "rock for stone" : "no stone");
        building.AddRange(site.Finds.Where(f => f.Planks > 0).Select(f => $"{f.Name}, its timbers to salvage"));
        lines.Add(("Building", Capitalise(string.Join("; ", building)) + "."));
        lines.Add(("Fever", site.Fever >= 0.004 ? "Low ground and standing water near: fever is likely."
            : site.Fever > 0.002 ? "Some fever, as anywhere on this coast." : "The sea breeze keeps the fever down."));
        lines.Add(("Holding it", site.Ground switch
        {
            "headland" => "A headland: the sea on three sides, one neck to close with a palisade.",
            "mangrove" => "Close growth all round: easy to come at unseen.",
            "river" => "The forest to the water's edge on both banks.",
            _ => "Open shore: seen from far off, and open to all sides.",
        }));
        if (place.Ground is { } owners && region.Community(owners) is { } community)
        {
            var leave = Campaign.World?.Community(owners).Leave.Contains("ground") == true;
            lines.Add(("Whose ground", $"{community.Name}'s{(leave ? ": leave given to stay." : ": no leave given to stay on it.")}"));
        }

        if (Distance(placeId, region.Places.FirstOrDefault(p => p.Wood)?.Id) is { } days)
        {
            lines.Add(("The red wood", $"About {days:0.#} days' walk to the dye-wood stands."));
        }

        return lines;
    }

    private string SiteName(string siteId) => Content.Shore?.Sites.FirstOrDefault(s => s.Id == siteId)?.Name ?? siteId;

    // The shortest way between two places by every route there is, known or not, in days.
    private double? Distance(string from, string? to)
    {
        if (Region is not { } region || to is null)
        {
            return null;
        }

        var best = new Dictionary<string, double> { [from] = 0 };
        var open = new SortedSet<(double Days, string Place)> { (0, from) };
        while (open.Count > 0)
        {
            var (days, at) = open.Min;
            open.Remove(open.Min);
            if (at == to)
            {
                return days;
            }

            foreach (var route in region.Routes.Where(r => r.Other(at) is not null))
            {
                var next = route.Other(at)!;
                if (days + route.Days < best.GetValueOrDefault(next, double.MaxValue))
                {
                    best[next] = days + route.Days;
                    open.Add((days + route.Days, next));
                }
            }
        }

        return null;
    }

    // True if the way runs along the shore all the way, so the boat can go.
    private bool ByBoat(IReadOnlyList<string> path) =>
        Region is { } region && path.Zip(path.Skip(1)).All(leg => region.Routes.FirstOrDefault(r => r.Other(leg.First) == leg.Second)?.Way is "beach" or "lagoon");

    /// <summary>
    /// Moves the camp to <paramref name="placeId"/>'s site: everyone at the camp goes, the sick carried, each
    /// with a pack of the stores, the boat taking more if the way is along the shore. What they cannot carry
    /// stays in the old camp's stores; what was built there stays standing. If the expedition camped there
    /// before, it takes up that camp again.
    /// </summary>
    public CommandResult MoveCamp(string placeId)
    {
        if (Campaign.Camp is not { } camp || Campaign.World is not { } world || Region?.Place(placeId) is not { } place)
        {
            return CommandResult.Rejected("There is no camp to move.");
        }

        if (place.Site is not { } site || Content.Shore?.Sites.All(s => s.Id != site) != false)
        {
            return CommandResult.Rejected($"{Capitalise(place.Name)} is no place to make a camp.");
        }

        if (site == camp.Site)
        {
            return CommandResult.Rejected("The camp is there already.");
        }

        if (world.Journeys.Any(j => j.Purpose == "move"))
        {
            return CommandResult.Rejected("The camp is already on the move.");
        }

        if (Way(placeId) is not { } way)
        {
            return CommandResult.Rejected($"No one knows the way to {place.Name}.");
        }

        var people = Ashore().Where(c => !Away(c.Id) && Campaign.Placements.ContainsKey(c.Id)).ToList();
        var able = people.Count(c => c.CanWork);
        if (able == 0)
        {
            return CommandResult.Rejected("There is no one able to make the move.");
        }

        // The sick and hurt are carried: slower, and the packs lighter for it.
        var carried = people.Count - able;
        var days = way.Days * (carried > 0 ? 1.5 : 1);
        var boat = camp.Boat && ByBoat(way.Path);
        var now = Campaign.Clock.Tick;
        // Whatever is in hand is put with the stores first, and packed with the rest.
        foreach (var person in people)
        {
            PutAway(person.Id);
        }

        var journey = new Journey
        {
            Id = world.NextJourney++, Members = people.Select(c => c.Id).ToList(), Destination = placeId, Purpose = "move", Path = way.Path, Left = now,
            Arrives = now + (long)(days * SimulationClock.TicksPerDay), Returns = now + (long)(days * SimulationClock.TicksPerDay), Boat = boat,
        };
        var kg = LoadUp(Campaign.SharedStore, journey, Math.Max(0, able - carried) * PackKg + carried * PackKg / 2 + (boat ? BoatLoadKg : 0));
        if (boat)
        {
            camp.Boat = false;
        }

        foreach (var person in people)
        {
            Campaign.InterruptTask(person.Id, InterruptReasons.Moved);
            Campaign.ClearQueue(person.Id);
            Campaign.RemovePlacement(person.Id);
        }

        world.AddJourney(journey);
        News($"The camp is struck and moved to {place.Name}: {people.Count} go{(carried > 0 ? $", {carried} carried" : "")}, with {kg:0} kg of the stores{(boat ? ", the boat taking the heaviest along the shore" : "")}. "
             + "The rest stays where it lies, to be fetched.");
        return CommandResult.Ok();
    }

    // Loads a party with the stores, up to kg: tools first, then the food that keeps, the seed, water, what
    // builds, the trade goods, and what spoils last of all. The kg taken.
    private double LoadUp(Inventory from, Journey journey, double kg) =>
        LoadUp(from.Quantities, (item, count) => from.TryRemove(item, count), journey, kg);

    private double LoadUp(IReadOnlyDictionary<string, int> from, Func<string, int, bool> take, Journey journey, double kg)
    {
        int Rank(ItemDefinition item) => item switch
        {
            { Category: "tool" } => 0,
            { HungerRelief: > 0, SpoilsPerDay: <= 0.02 } => 1,
            { Category: "seed" } => 2,
            { Category: "water" or "drink" } or { ThirstRelief: > 0 } => 3,
            { Category: "material" } => 4,
            { Category: "trade" } => 5,
            { HungerRelief: > 0 } => 7,
            _ => 6,
        };

        var taken = 0.0;
        foreach (var item in from.Keys.Select(id => Content.Items[id]).OrderBy(Rank).ThenBy(i => i.Id, StringComparer.Ordinal).ToList())
        {
            var weight = Math.Max(0.05, item.WeightKg);
            var count = Math.Min(from.GetValueOrDefault(item.Id), (int)((kg - taken) / weight));
            if (count > 0 && take(item.Id, count))
            {
                journey.Loads[item.Id] = journey.Loads.GetValueOrDefault(item.Id) + count;
                taken += count * weight;
            }
        }

        return taken;
    }

    // The party with the camp comes to the new site: the old camp left as it stands, with what was not
    // carried; the new one made (or the old one there taken up again), the loads stacked in its stores.
    private void Resettle(WorldState world, Journey journey)
    {
        var old = Campaign.Camp!;
        var place = Region!.Place(journey.Destination)!;
        var site = Content.Shore!.Site(place.Site!);
        world.EndJourney(journey);
        old.StoreId ??= Campaign.SharedStoreId.Value;
        Campaign.LeaveCamp(old);
        var camp = Campaign.TakeUpCamp(site.Id) ?? NewCamp(site, old);
        camp.Boat |= journey.Boat;
        // Along the shore, the ship comes round to lie off the new camp, with what is still aboard her.
        if (old.ShipAtAnchor && ByBoat(journey.Path))
        {
            foreach (var (item, count) in old.Afloat.ToList())
            {
                old.TakeAfloat(item, count);
                camp.PutAfloat(item, count);
            }

            old.ShipAtAnchor = false;
            camp.ShipAtAnchor = true;
        }
        Campaign.Camp = camp;
        Campaign.SetSharedStore(new InventoryId(camp.StoreId!));
        foreach (var (item, count) in journey.Loads)
        {
            Campaign.SharedStore.TryAdd(item, count, Content.Items);
        }

        var kg = journey.Loads.Sum(l => l.Value * Content.Items[l.Key].WeightKg);
        var location = Location(CampLocation);
        var cells = Enumerable.Range(3, 6).SelectMany(y => Enumerable.Range(12, 13).Select(x => new GridPosition(x, y))).Where(location.IsWalkable).ToList();
        for (var i = 0; i < journey.Members.Count; i++)
        {
            Campaign.SetPlacement(journey.Members[i], new CharacterPlacement(CampLocation, cells[i % cells.Count]));
        }

        AssignCampDuties();
        Tell(world, place.Id, PlaceKnowledge.Visited, "the camp");
        world.Survey(site.Id);
        Campaign.Record(ChronicleEntryKind.CampMoved, [
            new(ChronicleParameters.From, SiteName(old.Site)), new(ChronicleParameters.To, site.Name),
            new(ChronicleParameters.Need, journey.Members.Count.ToString(CultureInfo.InvariantCulture)),
            new(ChronicleParameters.Level, kg.ToString("0", CultureInfo.InvariantCulture)),
        ]);
    }

    // A camp made at a new site by people already ashore: no first days to report, the season counted from
    // the first landing, what is known of the site found already, and its own stores.
    private CampState NewCamp(ShoreSite site, CampState old)
    {
        var camp = new CampState { Site = site.Id, Landed = old.Landed, LandedCount = old.LandedCount, Boat = false };
        camp.Reported = true;
        camp.Survived = old.Survived;
        camp.Priorities = old.Priorities;
        camp.Choices = old.Choices;
        var store = new InventoryId($"inv.camp.{site.Id}");
        if (!Campaign.Inventories.ContainsKey(store))
        {
            Campaign.CreateInventory(store, $"The stores at {site.Name}", InventoryCapacity.Unlimited);
        }

        camp.StoreId = store.Value;
        foreach (var known in site.Water.Where(w => w.Known).Select(w => w.Id)
                     .Concat(site.Food.Where(f => f.Known).Select(f => f.Id)).Concat(site.Finds.Where(f => f.Known).Select(f => f.Id)))
        {
            camp.Find(known);
        }

        // A ruin's timbers already salvaged from another camp are gone from it here too.
        var ruins = site.Finds.Where(f => f.Planks > 0).Select(f => f.Id).ToHashSet();
        camp.TimberSalvaged = Campaign.LeftCamps.Values.Where(c => Content.Shore!.Site(c.Site).Finds.Any(f => ruins.Contains(f.Id))).Sum(c => c.TimberSalvaged);
        return camp;
    }

    // A party sent to fetch from an old camp loads up there: packs, and the boat if it came.
    private void FetchFrom(Journey journey)
    {
        if (LeftCampAt(journey.Destination) is not { StoreId: { } id } left || !Campaign.Inventories.TryGetValue(new InventoryId(id), out var store))
        {
            return;
        }

        var room = journey.Members.Count(m => Campaign.Characters[m].CanWork) * PackKg + (journey.Boat ? BoatLoadKg : 0);
        var kg = LoadUp(store, journey, room);
        // With the boat, what is still in the ship or the wreck off the old camp can be got at too.
        if (journey.Boat && kg < room && left.Afloat.Count > 0)
        {
            var more = LoadUp(left.Afloat, (item, count) =>
            {
                left.TakeAfloat(item, count);
                return true;
            }, journey, room - kg);
            kg += more;
        }

        journey.Report.Add(kg > 0
            ? $"At the old camp they loaded {kg:0} kg of what was left there{(journey.Boat ? ", the boat taking most of it" : "")}."
            : "There was nothing left at the old camp to bring.");
    }
}
