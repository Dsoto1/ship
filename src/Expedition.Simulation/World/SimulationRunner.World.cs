using System.Globalization;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.Events;
using Expedition.Simulation.History;
using Expedition.Simulation.Shore;

namespace Expedition.Simulation.World;

/// <summary>A thing the captain can say or do at a meeting.</summary>
/// <param name="Id">Its ID.</param>
/// <param name="Text">What the button says.</param>
/// <param name="Available">False if the camp lacks what it needs.</param>
/// <param name="Note">Why not, or what it may cost.</param>
public sealed record MeetingChoice(string Id, string Text, bool Available, string? Note);

/// <summary>
/// The world around the camp (R8, docs/R8_THE_WORLD_AROUND.md): the coast known by going and being told; the
/// peoples who were here first, each acting on its own interests; meetings at the camp, through whoever can
/// interpret; trade, gifts, leave asked and knowledge given; parties sent out; the Brennish ship; and the
/// Company's charter, judged when the relief ship comes.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>The coast's rules, or null.</summary>
    public RegionRules? Region => Content.Shore?.Region;

    // Grievance at which a people stays away, and at which it may turn against the camp.
    private const double AvoidAt = 35;
    private const double HostileAt = 65;

    // ================================================================ knowing the coast

    /// <summary>True once the expedition has learned <paramref name="topic"/> (a <c>know.*</c> ID) from the coast's peoples.</summary>
    public bool Learned(string topic) => Campaign.World?.Learned.ContainsKey(topic) == true;

    /// <summary>How well a place is known.</summary>
    public PlaceKnowledge PlaceKnown(string place) => Campaign.World?.Places.TryGetValue(place, out var known) == true ? known.Level : PlaceKnowledge.Unknown;

    // The coast as the expedition first knows it: what the sailing directions give, and where they are.
    private void BeginWorld(CampState camp)
    {
        if (Region is not { } region)
        {
            return;
        }

        var world = new WorldState();
        foreach (var place in region.Places.Where(p => p.Known is not null))
        {
            Tell(world, place.Id, place.Known == "seen" ? PlaceKnowledge.Seen : PlaceKnowledge.Reported, "the sailing directions");
        }

        foreach (var route in region.Routes.Where(r => r.Known))
        {
            world.KnowRoute(route.Key);
        }

        if (region.Places.FirstOrDefault(p => p.Site == camp.Site) is { } here)
        {
            Tell(world, here.Id, PlaceKnowledge.Visited, "the camp");
        }

        foreach (var community in region.Communities)
        {
            var state = world.Community(community.Id);
            state.Trust = community.Trust;
            state.NextVisit = camp.Landed + community.FirstVisitDay * (long)SimulationClock.TicksPerDay;
        }

        Campaign.World = world;
    }

    // A place made known, never less well than it was.
    private static void Tell(WorldState world, string place, PlaceKnowledge level, string source)
    {
        var known = world.Know(place);
        if (level > known.Level)
        {
            known.Level = level;
            known.Source = source;
        }
    }

    /// <summary>The place the camp is at, on the coast.</summary>
    public RegionPlace? CampPlace => Campaign.Camp is { } camp ? Region?.Places.FirstOrDefault(p => p.Site == camp.Site) : null;

    // ================================================================ the peoples

    /// <summary>How a people stands to the newcomers now, in a word.</summary>
    public string Stance(string communityId)
    {
        if (Campaign.World?.Communities.TryGetValue(communityId, out var state) != true || Region?.Community(communityId) is not { } community)
        {
            return "unknown";
        }

        if (state!.Grievance >= HostileAt && state.Trust < 0)
        {
            return "hostile";
        }

        if (state.Grievance >= AvoidAt)
        {
            return "avoiding";
        }

        if (!state.Met)
        {
            return "watching";
        }

        var standing = state.Trust - state.Grievance;
        return standing >= 25 ? "friendly"
            : standing >= 5 ? "trading"
            : standing >= -10 + 10 * community.Caution ? "curious"
            : "wary";
    }

    /// <summary>A people's trust, in words a captain would use.</summary>
    public static string TrustWords(double trust) => trust switch
    {
        >= 45 => "they trust you",
        >= 25 => "warm",
        >= 10 => "well disposed",
        >= -5 => "uncertain of you",
        >= -25 => "cool",
        _ => "distrustful",
    };

    private void Trust(string communityId, double change, double grievance = 0)
    {
        if (Campaign.World?.Communities.TryGetValue(communityId, out var state) == true)
        {
            state!.Trust = Math.Clamp(state.Trust + change, -100, 100);
            state.Grievance = Math.Clamp(state.Grievance + grievance, 0, 100);
        }
    }

    // How well the camp can speak to a people: the best interpreter at the camp, not away.
    private (int Level, Character? Who) Interpreter(CommunityDefinition community)
    {
        var atCamp = Ashore().Where(c => c.CanWork && !Away(c.Id)).ToList();
        var direct = atCamp.Select(c => (Level: c.Languages.Get(community.Language), Who: c)).OrderByDescending(x => x.Level).ThenBy(x => x.Who.Id.Value).FirstOrDefault();
        // The river traders speak the lagoon's tongue as a trade language, a little less well.
        if (community.Language != "language.ahuneri")
        {
            var trade = atCamp.Select(c => (Level: c.Languages.Get("language.ahuneri") - 1, Who: c)).OrderByDescending(x => x.Level).ThenBy(x => x.Who.Id.Value).FirstOrDefault();
            if (trade.Who is not null && trade.Level > direct.Level)
            {
                direct = trade;
            }
        }

        return direct.Who is null ? (0, null) : (Math.Max(0, direct.Level), direct.Who);
    }

    /// <summary>The chance that what is said at a meeting with a people goes astray (SETTLEMENT §29).</summary>
    public double MisunderstandingChance(string communityId) =>
        Region?.Community(communityId) is { } community
            ? Interpreter(community).Level switch { >= 4 => 0.02, 3 => 0.07, >= 1 => 0.18, _ => 0.35 }
            : 0.35;

    // Each day, each people does as it means to: watches, comes to the camp, stays away, tests the newcomers.
    private void StepPeoplesDay(WorldState world, CampState camp)
    {
        if (Region is not { } region)
        {
            return;
        }

        var now = Campaign.Clock.Tick;
        var ground = CampPlace?.Ground;
        foreach (var community in region.Communities)
        {
            var state = world.Community(community.Id);

            // Their ground used without leave, once they know the newcomers are on it.
            if (ground == community.Id && state.Met && !state.Leave.Contains("ground"))
            {
                state.Grievance = Math.Min(100, state.Grievance + 0.4);
            }

            // A gift unanswered too long is an insult.
            if (community.GiftCulture && state.GiftOwedSince > 0 && now - state.GiftOwedSince > 8 * SimulationClock.TicksPerDay)
            {
                state.GiftOwedSince = 0;
                Trust(community.Id, -6, 6);
                News($"{community.Name}'s gift was never answered. They will remember it.");
            }

            // Grievance fades slowly when there is no new cause.
            state.Grievance = Math.Max(0, state.Grievance - 0.15);

            if (!state.Met && now >= state.NextVisit - 3 * SimulationClock.TicksPerDay && state.Watched < 3)
            {
                state.Watched++;
                News(state.Watched == 1
                    ? $"Canoes off the point at first light, holding off the beach; people in them watching the camp. They are gone by noon."
                    : $"Smoke across the lagoon, and a canoe again, nearer.");
            }
        }

        // Thefts in the night by a people that has turned against the camp and is testing it.
        foreach (var community in region.Communities.Where(c => world.Community(c.Id).Met && Stance(c.Id) is "wary" or "avoiding" or "hostile"))
        {
            var odds = 0.04 + 0.002 * world.Community(community.Id).Grievance;
            odds *= camp.Work(CampWorks.Palisade) >= 1 ? 0.25 : 1 - 0.5 * camp.Work(CampWorks.Palisade);
            if (world.Meeting is null && Campaign.Randomness.Events.Chance(odds)
                && new[] { "item.axe", "item.cutlass", "item.trade_knives", "item.spade" }.FirstOrDefault(i => Campaign.SharedStore.Count(i) > 0) is { } tool)
            {
                Campaign.SharedStore.TryRemove(tool, 1);
                world.Thefts++;
                world.Meeting = new Meeting { Kind = "theft", Community = community.Id, Tick = now, Item = tool };
                return;
            }
        }

        if (world.Meeting is not null || Campaign.PendingEvent is not null)
        {
            return;
        }

        // A delegation, the first whose time has come and who is minded to come at all.
        foreach (var community in region.Communities)
        {
            var state = world.Community(community.Id);
            if (now < state.NextVisit || Stance(community.Id) is "avoiding" or "hostile")
            {
                continue;
            }

            var every = community.VisitEveryDays * (state.Trust < 0 ? 1.5 : 1) * (1.3 - 0.5 * community.Trade);
            state.NextVisit = now + (long)(every * SimulationClock.TicksPerDay);
            var meeting = new Meeting { Kind = "delegation", Community = community.Id, Tick = now };
            if (community.GiftCulture && state.Trust > -15)
            {
                // They open with a gift: of what they have, more as they trust more.
                var (item, cost) = community.Offers.Where(o => Content.Items[o.Key].HungerRelief > 0).OrderBy(o => o.Key, StringComparer.Ordinal).First();
                meeting.Gift = (item, Math.Max(4, (int)(6 + state.Trust / 5 / cost)));
            }

            world.Meeting = meeting;
            if (!state.Met)
            {
                state.Met = true;
                Tell(world, community.Place, PlaceKnowledge.Reported, community.Id);
                News($"People of {community.Name} have come to the camp: {Leaders(community)}.");
            }

            return;
        }
    }

    private static string Leaders(CommunityDefinition community) =>
        community.Persons.Count == 0 ? community.Name : $"{community.Persons[0].Name}, {community.Persons[0].Role}, and others";

    private void News(string text) => Campaign.Record(ChronicleEntryKind.CoastNews, [new(ChronicleParameters.Need, text)]);

    // ================================================================ meetings

    /// <summary>The meeting waiting for the captain's word: a heading and what is happening; null if none.</summary>
    public (string Title, string Text)? DescribeMeeting()
    {
        if (Campaign.World?.Meeting is not { } meeting)
        {
            return null;
        }

        var community = meeting.Community is { } id ? Region?.Community(id) : null;
        var state = community is null ? null : Campaign.World.Community(community.Id);
        var (level, who) = community is null ? (0, null) : Interpreter(community);
        var speech = community is null ? "" : who is null || level == 0
            ? " No one at the camp has their language: it is all signs and goodwill."
            : $" {who.Name} interprets{(level >= 4 ? ", easily" : level >= 3 ? "" : ", haltingly")}.";
        switch (meeting.Kind)
        {
            case "theft":
                return ($"A theft in the night", $"In the morning {Article(ItemName(meeting.Item!).ToLowerInvariant())} is gone from the stores, and there are tracks down to the lagoon's edge, and the mark of a canoe's keel. "
                    + $"{(community is null ? "" : $"It will be {community.Name}'s people: the camp has not stood well with them.")}");
            case "brennish" when Region?.Brennish is { } ship:
                return ($"{Capitalise(ship.Ship)}'s boat", $"A ship's boat comes in round the point under a Brennish flag: {ship.Master}, of {ship.Ship}, lying at the cove to the north. "
                    + "He has wine, biscuit and iron to sell, news a month old, and an eye on the camp's wood.");
        }

        if (community is null || state is null)
        {
            return null;
        }

        var leader = community.Persons.FirstOrDefault();
        var opening = meeting.Round switch
        {
            0 when meeting.Gift is { } gift => $"{leader?.Name ?? "Their speaker"}, {leader?.Role}, comes with others of {community.Name}, and lays down a gift: {gift.Count} {ItemName(gift.Item).ToLowerInvariant()}. "
                                                + (community.GiftCulture ? "With these people, a gift wants answering." : ""),
            0 => $"{leader?.Name ?? "Their speaker"}, {leader?.Role}, has come with others of {community.Name}, to see the newcomers and what they have.",
            _ => $"{community.Name}'s people are still at the camp ({TrustWords(state.Trust)}).",
        };
        return ($"{community.Name}", opening + speech);
    }

    /// <summary>What the captain can do at the meeting waiting, now.</summary>
    public IReadOnlyList<MeetingChoice> MeetingChoices()
    {
        if (Campaign.World is not { Meeting: { } meeting } world)
        {
            return [];
        }

        var choices = new List<MeetingChoice>();
        if (meeting.Kind == "theft")
        {
            choices.Add(new("let_go", "Let it go", true, "they will test the camp again"));
            choices.Add(new("take_up", "Take it up with them when they next come", true, null));
            choices.Add(new("seize", "Hold the next canoe's people until it is returned", true, "as the garrison did: it breaks the trade peace"));
            return choices;
        }

        if (meeting.Kind == "brennish" && Region?.Brennish is { } ship)
        {
            var funds = Campaign.Voyage?.Funds ?? 0;
            foreach (var (item, price) in ship.Sells.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                choices.Add(new($"buy:{item}", $"Buy {ItemName(item).ToLowerInvariant()} ({price} sueldos)", funds >= price, funds >= price ? null : "the purse is short"));
            }

            var logs = Campaign.SharedStore.Count("item.embrawood");
            choices.Add(new("sell_logs", $"Sell him logs of embrawood ({ship.BuysLog} sueldos each)", logs > 0, logs > 0 ? "against the charter" : "there are none"));
            if (world.LetterSent == 0)
            {
                choices.Add(new("letter", "Give him a letter for the Company", true, "it will be months reaching Almara"));
            }

            choices.Add(new("warn", "Warn him off in the Company's name", true, "Kessat will hear of it"));
            choices.Add(new("farewell", "See him off", true, null));
            return choices;
        }

        if (meeting.Community is not { } id || Region?.Community(id) is not { } community)
        {
            return choices;
        }

        var state = world.Community(id);
        if (meeting.Round == 0 && meeting.Gift is not null)
        {
            foreach (var (item, count) in GiftsToGive(community))
            {
                choices.Add(new($"answer:{item}:{count}", $"Answer their gift: {count} {ItemName(item).ToLowerInvariant()}", true, null));
            }

            choices.Add(new("accept", "Accept it with thanks, and give nothing yet", true, community.GiftCulture ? "a gift unanswered is an insult" : null));
            choices.Add(new("refuse", "Refuse it, and send them away", true, "they will take it ill"));
            return choices;
        }

        foreach (var (give, giveCount, get, getCount) in TradeOffers(community, state))
        {
            choices.Add(new($"trade:{give}:{giveCount}:{get}:{getCount}",
                $"Trade {giveCount} {ItemName(give).ToLowerInvariant()} for {getCount} {ItemName(get).ToLowerInvariant()}", true, null));
        }

        var (level, _) = Interpreter(community);
        foreach (var topic in Region.Knowledge.Where(k => k.From.Contains(id) && !Learned(k.Id)).Take(3))
        {
            var ready = state.Trust >= topic.Trust && level >= topic.Language;
            choices.Add(new($"ask:{topic.Id}", $"Ask about {topic.Name}", true, ready ? null : level < topic.Language ? "the interpreter may not manage it" : "they may not say yet"));
        }

        if (state.Grievous is { } stolen)
        {
            choices.Add(new("ask_back", $"Ask for the {ItemName(stolen).ToLowerInvariant()} back", true, null));
        }

        if (PlaceKnown("place.dyewood") >= PlaceKnowledge.Reported && id != "people.tesiwa" && !state.Leave.Contains("wood"))
        {
            choices.Add(new("leave:wood", "Ask leave to cut the red wood", true, null));
        }

        if (CampPlace?.Ground == id && Learned("know.ground") && !state.Leave.Contains("ground"))
        {
            choices.Add(new("leave:ground", "Ask leave to stay on this ground", true, null));
        }

        if (id == "people.mahenu" && world.PalisadeNoticed && !world.PalisadeExplained)
        {
            choices.Add(new("explain", "Tell them what the palisade is against", true, null));
        }

        choices.Add(new("farewell", meeting.Round <= 1 ? "Thank them, and see them off" : "That is all: see them off", true, null));
        return choices;
    }

    // Gifts the camp could answer with: what they value, in a fitting amount, of what the camp has; the
    // Company's trade goods before the tools the camp works with.
    private IEnumerable<(string Item, int Count)> GiftsToGive(CommunityDefinition community) =>
        community.Wants.OrderBy(w => Content.Items[w.Key].Category == "trade" ? 0 : 1).ThenByDescending(w => w.Value).ThenBy(w => w.Key, StringComparer.Ordinal)
            .Select(w => (w.Key, Count: GiftCount(w.Key)))
            .Where(g => Campaign.SharedStore.Count(g.Key) >= g.Count + (Content.Items[g.Key].Category == "tool" ? 1 : 0))
            .Take(2);

    private static int GiftCount(string item) => item switch
    {
        "item.fishhooks" => 30,
        "item.trade_knives" => 3,
        "item.sailcloth" => 1,
        _ => 1,
    };

    // What a people offers to trade this visit, at its own valuation: each thing it wants that the camp has,
    // for something it has; fairer as it trusts the camp more, and less for what it has had a lot of already.
    private List<(string Give, int GiveCount, string Get, int GetCount)> TradeOffers(CommunityDefinition community, CommunityState state)
    {
        var offers = new List<(string, int, string, int)>();
        var fairness = Math.Clamp(0.8 + state.Trust / 200 + 0.15 * community.Trade, 0.7, 1.05);
        var brennish = Campaign.World?.Brennish == 1 && community.Id == "people.tesiwa";
        var wanted = community.Wants.Where(w => Campaign.SharedStore.Count(w.Key) >= GiftCount(w.Key) + (Content.Items[w.Key].Category == "tool" ? 1 : 0))
            .OrderByDescending(w => w.Value).ThenBy(w => w.Key, StringComparer.Ordinal).Take(3).ToList();
        // Logs first if the charter wants them and they have any; then food.
        var gets = community.Offers.OrderBy(o => o.Key == "item.embrawood" ? 0 : Content.Items[o.Key].HungerRelief > 0 ? 1 : 2).ThenBy(o => o.Key, StringComparer.Ordinal).ToList();
        for (var i = 0; i < wanted.Count; i++)
        {
            var (give, worth) = wanted[i];
            var had = state.Received.GetValueOrDefault(give);
            worth *= Math.Max(0.4, 1 - 0.06 * had) * (brennish && give == "item.axe" ? 0.6 : 1);
            var (get, cost) = gets[i % gets.Count];
            var count = GiftCount(give);
            var getCount = Math.Max(1, (int)(worth * count * fairness / cost));
            if (get == "item.embrawood")
            {
                getCount = Math.Min(getCount, 8);
            }

            offers.Add((give, count, get, getCount));
        }

        return offers;
    }

    /// <summary>
    /// The captain's word at the meeting: what comes of it, and whether the meeting goes on. Refused if no
    /// meeting is waiting or the choice is not one of those offered now.
    /// </summary>
    public (CommandResult Result, EventResolution? Resolution) ResolveMeeting(string choiceId)
    {
        if (Campaign.World is not { Meeting: { } meeting } world || MeetingChoices().FirstOrDefault(c => c.Id == choiceId) is not { } choice)
        {
            return (CommandResult.Rejected("That is not one of the choices."), null);
        }

        if (!choice.Available)
        {
            return (CommandResult.Rejected(choice.Note ?? "That cannot be done now."), null);
        }

        var title = DescribeMeeting()?.Title ?? "";
        var (text, ends, ok) = meeting.Kind switch
        {
            "theft" => Theft(world, meeting, choiceId),
            "brennish" => BrennishChoice(world, choiceId),
            _ => Delegation(world, meeting, choiceId),
        };
        meeting.Round++;
        if (ends)
        {
            world.Meeting = null;
        }

        Campaign.Record(ChronicleEntryKind.CoastNews, [new(ChronicleParameters.Need, $"{title}: {text}")]);
        return (CommandResult.Ok(), new EventResolution(title, choice.Text, ok, text));
    }

    private (string Text, bool Ends, bool Ok) Delegation(WorldState world, Meeting meeting, string choiceId)
    {
        var community = Region!.Community(meeting.Community!)!;
        var state = world.Community(community.Id);
        var parts = choiceId.Split(':');
        var astray = Campaign.Randomness.Events.Chance(MisunderstandingChance(community.Id));
        LearnLanguage(community);
        switch (parts[0])
        {
            case "answer":
            {
                var (item, count) = (parts[1], int.Parse(parts[2], CultureInfo.InvariantCulture));
                TakeGift(meeting);
                Campaign.SharedStore.TryRemove(item, count);
                state.Received[item] = state.Received.GetValueOrDefault(item) + count;
                Trust(community.Id, 4 + community.Wants.GetValueOrDefault(item) / 4);
                state.GiftOwedSince = 0;
                return ($"The gift is answered with {count} {ItemName(item).ToLowerInvariant()}. {(community.GiftCulture ? "That is done properly, and they say so." : "They take it, and look at it a long while.")}", false, true);
            }

            case "accept":
                TakeGift(meeting);
                if (community.GiftCulture)
                {
                    state.GiftOwedSince = Campaign.Clock.Tick;
                }

                return ("The gift is taken in. Something is owed for it now.", false, true);
            case "refuse":
                Trust(community.Id, -8, 8);
                return ("They take their gift up again without a word and go back to their canoes.", true, false);
            case "trade":
            {
                var (give, giveCount, get, getCount) = (parts[1], int.Parse(parts[2], CultureInfo.InvariantCulture), parts[3], int.Parse(parts[4], CultureInfo.InvariantCulture));
                Campaign.SharedStore.TryRemove(give, giveCount);
                state.Received[give] = state.Received.GetValueOrDefault(give) + giveCount;
                if (astray)
                {
                    // Taken as a gift: they are pleased, and give a little, not what was meant.
                    Campaign.SharedStore.TryAdd(get, Math.Max(1, getCount / 3), Content.Items);
                    Trust(community.Id, 2);
                    return ($"Something goes astray in the telling: they take the {ItemName(give).ToLowerInvariant()} as a gift, are glad of it, and leave {Math.Max(1, getCount / 3)} {ItemName(get).ToLowerInvariant()} in return, not the {getCount} meant.", false, false);
                }

                Campaign.SharedStore.TryAdd(get, getCount, Content.Items);
                Trust(community.Id, 1.5);
                return ($"{giveCount} {ItemName(give).ToLowerInvariant()} for {getCount} {ItemName(get).ToLowerInvariant()}: a fair exchange, as they see it.", false, true);
            }

            case "ask":
            {
                var topic = Region.Topic(parts[1])!;
                var (level, _) = Interpreter(community);
                if (state.Trust < topic.Trust)
                {
                    return ($"They hear the question and look at one another. Not yet, is the answer, or nothing is.", false, false);
                }

                if (level < topic.Language || astray)
                {
                    Trust(community.Id, -1);
                    return ("They answer at length, and what comes across is not much: the words are not there for it, on one side or the other.", false, false);
                }

                Teach(world, topic, community);
                Trust(community.Id, 1);
                return (topic.Text, false, true);
            }

            case "ask_back":
            {
                var stolen = state.Grievous!;
                state.Grievous = null;
                if (state.Trust >= 0 && !astray)
                {
                    Campaign.SharedStore.TryAdd(stolen, 1, Content.Items);
                    Trust(community.Id, 1);
                    return ($"After some talk among themselves the {ItemName(stolen).ToLowerInvariant()} comes back, without a word about where it was.", false, true);
                }

                Trust(community.Id, -3, 3);
                return ("They deny all knowledge of it, and are offended to be asked.", false, false);
            }

            case "leave":
            {
                var what = parts[1];
                var needs = (what, community.Id) switch
                {
                    ("ground", _) => 25,
                    ("wood", "people.oruwai") => 18 - (state.GiftOwedSince == 0 && state.Received.Count > 0 ? 5 : 0),
                    _ => 8,
                };
                if (astray)
                {
                    return ("Something is agreed, but it is not what was asked: the matter will have to be put again.", false, false);
                }

                if (state.Trust < needs)
                {
                    Trust(community.Id, -0.5);
                    return (what == "ground" ? "Not yet. The ground's households must be asked, and they will want to know the newcomers better." : "Not yet: the wood is the households', and they do not know you well enough.", false, false);
                }

                state.Leave.Add(what);
                if (what == "ground")
                {
                    state.Grievance = Math.Max(0, state.Grievance - 15);
                }

                return (what == "ground" ? "Leave is given to stay on the ground, as long as the newcomers keep faith with its households." : "Leave is given to cut the red wood, by the households' trees as they show them.", false, true);
            }

            case "explain":
                world.PalisadeExplained = true;
                if (astray)
                {
                    Trust(community.Id, -2);
                    return ("The explanation does not land: they hear only that the newcomers fear someone, and look at the stakes again.", false, false);
                }

                Trust(community.Id, 4);
                return ("The palisade is against the forest at night, the Brennish, and the memory of the garrison's fate, not against Mahenu. Narehwa hears it out, and seems to accept it.", false, true);
            default:
                if (state.GiftOwedSince > 0 && community.GiftCulture)
                {
                    return ("They go back to their canoes. Their gift is still unanswered.", true, true);
                }

                return ("They go back to their canoes, in no hurry.", true, true);
        }
    }

    private void TakeGift(Meeting meeting)
    {
        if (meeting.Gift is { } gift)
        {
            Campaign.SharedStore.TryAdd(gift.Item, gift.Count, Content.Items);
            meeting.Gift = null;
        }
    }

    // A people tells what it knows: it is learned, its places made known, its gifts given; and what it does.
    private void Teach(WorldState world, KnowledgeTopic topic, CommunityDefinition from)
    {
        if (!world.Learn(topic.Id, from.Id))
        {
            return;
        }

        foreach (var place in topic.Reveals)
        {
            Tell(world, place, PlaceKnowledge.Reported, from.Id);
        }

        foreach (var route in Region!.Routes.Where(r => topic.Reveals.Contains(r.From) || topic.Reveals.Contains(r.To)))
        {
            if (PlaceKnown(route.From) >= PlaceKnowledge.Reported && PlaceKnown(route.To) >= PlaceKnowledge.Reported)
            {
                world.KnowRoute(route.Key);
            }
        }

        foreach (var (item, count) in topic.Gives)
        {
            Campaign.SharedStore.TryAdd(item, count, Content.Items);
        }

        Campaign.Record(ChronicleEntryKind.Learned, [new(ChronicleParameters.To, topic.Id), new(ChronicleParameters.From, from.Id), new(ChronicleParameters.Need, topic.Name)]);
    }

    // The best speaker short of functional may pick up a little more of the language at each meeting.
    private void LearnLanguage(CommunityDefinition community)
    {
        var learner = Ashore().Where(c => c.CanWork && !Away(c.Id) && c.Languages.Get(community.Language) < 3)
            .OrderByDescending(c => c.Languages.Get(community.Language)).ThenBy(c => c.Id.Value).FirstOrDefault();
        if (learner is not null && Campaign.Randomness.Tasks.Chance(0.06))
        {
            learner.Languages.Set(community.Language, learner.Languages.Get(community.Language) + 1);
            News($"{learner.Name} has more of {Content.Languages[community.Language].Name} now: enough to be understood a little better.");
        }
    }

    private (string Text, bool Ends, bool Ok) Theft(WorldState world, Meeting meeting, string choiceId)
    {
        var community = meeting.Community is { } id ? Region!.Community(id) : null;
        switch (choiceId)
        {
            case "take_up" when community is not null:
                world.Community(community.Id).Grievous = meeting.Item;
                return ($"It will be put to {community.Name} when they next come.", true, true);
            case "seize":
                world.PeaceBroken = true;
                foreach (var ahuneri in Region!.Communities.Where(c => c.People == community?.People))
                {
                    Trust(ahuneri.Id, -35, 40);
                }

                Campaign.SharedStore.TryAdd(meeting.Item!, 1, Content.Items);
                return ("Two men from a canoe at the weir are held at the camp under guard. The tool comes back that night, left on the beach. The men are let go at dawn. No one comes from the lagoon after that, and the Company's peace with its people is broken.", true, false);
            default:
                return ("It is let go. Whoever took it will know the camp can be taken from.", true, true);
        }
    }

    // ================================================================ the Brennish

    private void StepBrennishDay(WorldState world, CampState camp)
    {
        if (Region?.Brennish is not { } ship)
        {
            return;
        }

        var day = CampDay;
        if (world.Brennish == 0 && day >= ship.ArriveDay)
        {
            world.Brennish = 1;
            News(PlaceKnown("place.brennish_cove") >= PlaceKnowledge.Reported
                ? $"A ship under Brennish colours has put in at the cove to the north: {ship.Ship}."
                : "Topsails to the north, going in behind the land and not coming out: a ship has anchored somewhere up the coast.");
        }
        else if (world.Brennish == 1 && day >= ship.ArriveDay + 2 && world.Meeting is null && Campaign.PendingEvent is null)
        {
            world.Brennish = 2;
            world.Meeting = new Meeting { Kind = "brennish", Tick = Campaign.Clock.Tick };
        }
        else if (world.Brennish == 2 && day >= ship.ArriveDay + (world.BrennishWarned ? 4 : ship.StayDays))
        {
            world.Brennish = 3;
            News($"{Capitalise(ship.Ship)} has gone from the cove.");
            // Someone at the end of their tether may have gone with her.
            if (Ashore().Where(c => c.Id != Campaign.CaptainId && c.CanWork && !Away(c.Id) && c.Needs.Stress >= 85).OrderBy(c => c.Id.Value).FirstOrDefault() is { } gone
                && Campaign.Randomness.Events.Chance(0.4))
            {
                Campaign.InterruptTask(gone.Id, InterruptReasons.Cancelled);
                Campaign.ChangeSurvivalState(gone.Id, SurvivalState.Missing, "deserted");
                Campaign.RemovePlacement(gone.Id);
                News($"{gone.Name} is gone from the camp, and a canoe with them. Gone to the Brennish ship, they say.");
            }
        }
    }

    private (string Text, bool Ends, bool Ok) BrennishChoice(WorldState world, string choiceId)
    {
        var ship = Region!.Brennish!;
        var parts = choiceId.Split(':');
        switch (parts[0])
        {
            case "buy" when Campaign.Voyage is { } voyage:
            {
                var price = ship.Sells[parts[1]];
                voyage.Funds -= price;
                Campaign.SharedStore.TryAdd(parts[1], parts[1] == "item.axe" ? 1 : 10, Content.Items);
                return ($"Bought, at a Brennish price: {ItemName(parts[1]).ToLowerInvariant()}.", false, true);
            }

            case "sell_logs" when Campaign.Voyage is { } voyage:
            {
                var logs = Campaign.SharedStore.Count("item.embrawood");
                Campaign.SharedStore.TryRemove("item.embrawood", logs);
                voyage.Funds += logs * ship.BuysLog;
                world.LogsSold += logs;
                return ($"{logs} logs go out to the Brennish boat, for {logs * ship.BuysLog} sueldos. The Company would call it theft.", true, false);
            }

            case "letter":
                world.LetterSent = Campaign.Clock.Tick;
                return ($"{ship.Master} takes the letter for the Company, sealed, and says it will be in Almara by spring, if the Chamber's men do not open it first.", false, true);
            case "warn":
                world.BrennishWarned = true;
                Trust("people.tesiwa", -4, 6);
                return ($"{ship.Master} hears the Company's claim to the coast, laughs, and says the coast does not seem to know it. He will be gone in a few days, and Kessat will hear why.", true, true);
            default:
                return ("The Brennish boat pulls away round the point.", true, true);
        }
    }

    // ================================================================ journeys

    /// <summary>True while someone is away from the camp with a party.</summary>
    public bool Away(CharacterId id) => Campaign.World?.Journeys.Any(j => j.Members.Contains(id)) == true;

    /// <summary>The way a party would go to <paramref name="placeId"/>, by routes known, and how many days: or null.</summary>
    public (IReadOnlyList<string> Path, double Days, bool Guessed)? Way(string placeId)
    {
        if (Region is not { } region || CampPlace is not { } start || Campaign.World is not { } world || placeId == start.Id)
        {
            return null;
        }

        // Shortest by days, a route not known costing half as much again: guessed at, and easily lost.
        var best = new Dictionary<string, (double Days, string? From)> { [start.Id] = (0, null) };
        var open = new SortedSet<(double Days, string Place)> { (0, start.Id) };
        while (open.Count > 0)
        {
            var (days, here) = open.Min;
            open.Remove(open.Min);
            foreach (var route in region.Routes.Where(r => r.Other(here) is not null))
            {
                var there = route.Other(here)!;
                var cost = days + route.Days * (world.Routes.Contains(route.Key) ? 1 : 1.5);
                if (!best.TryGetValue(there, out var known) || cost < known.Days - 1e-9)
                {
                    if (known.From is not null)
                    {
                        open.Remove((known.Days, there));
                    }

                    best[there] = (cost, here);
                    open.Add((cost, there));
                }
            }
        }

        if (best.TryGetValue(placeId, out var found))
        {
            var path = new List<string> { placeId };
            while (best[path[^1]].From is { } from)
            {
                path.Add(from);
            }

            path.Reverse();
            var guessed = path.Zip(path.Skip(1)).Any(step => !world.Routes.Contains(region.Routes.First(r => r.Other(step.First) == step.Second).Key));
            return (path, found.Days, guessed);
        }

        return null;
    }

    /// <summary>
    /// Sends a party out from the camp to a place known or told of (SETTLEMENT §26): to see what is there
    /// (<c>explore</c>), to call on its people (<c>visit</c>), or to cut embrawood (<c>wood</c>). They take
    /// food for the days there and back from the stores.
    /// </summary>
    public CommandResult SendParty(IReadOnlyList<CharacterId> members, string placeId, string purpose)
    {
        if (Campaign.World is not { } world || Region?.Place(placeId) is not { } place)
        {
            return CommandResult.Rejected("There is nowhere to go from here.");
        }

        if (PlaceKnown(placeId) == PlaceKnowledge.Unknown)
        {
            return CommandResult.Rejected("No one knows of such a place.");
        }

        if (members.Count is < 1 or > 4 || members.Distinct().Count() != members.Count)
        {
            return CommandResult.Rejected("A party is one to four people.");
        }

        if (members.Any(m => !Campaign.Characters.TryGetValue(m, out var c) || !c.CanWork || Away(m) || !Campaign.Placements.ContainsKey(m)))
        {
            return CommandResult.Rejected("Everyone in the party must be at the camp and able to walk.");
        }

        if (purpose == "wood" && !place.Wood)
        {
            return CommandResult.Rejected($"No embrawood grows at {place.Name}.");
        }

        if (Way(placeId) is not { } way)
        {
            return CommandResult.Rejected($"No one knows the way to {place.Name}.");
        }

        // A guide shortens it: someone who reads the forest, or who can ask the way.
        var people = members.Select(m => Campaign.Characters[m]).ToList();
        var guided = people.Any(c => c.Skills.Get("skill.tracking") >= 2 || c.Languages.Get("language.ahuneri") >= 2);
        var days = way.Days * (guided ? 1 : 1.25);
        if (way.Guessed && Campaign.Randomness.Events.Chance(guided ? 0.12 : 0.25))
        {
            days += 1.5;
        }

        var working = purpose == "wood" ? 2 : purpose == "visit" ? 1 : 0.5;
        var total = 2 * days + working;
        // Food for the journey from the stores: about three rations a day each.
        var rations = (int)Math.Ceiling(total * 3) + 1;
        var foods = Content.Foods.Where(f => f.Sickens is null).OrderByDescending(f => f.SpoilsPerDay <= 0.005).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
        if (foods.Sum(f => Campaign.SharedStore.Count(f.Id)) < rations * members.Count)
        {
            return CommandResult.Rejected($"There is not food enough in the stores for {members.Count} for {total:0} days.");
        }

        var now = Campaign.Clock.Tick;
        foreach (var character in people)
        {
            var left = rations;
            foreach (var food in foods)
            {
                var take = Math.Min(left, Campaign.SharedStore.Count(food.Id));
                if (take > 0 && Campaign.SharedStore.TryRemove(food.Id, take))
                {
                    Campaign.Inventories[character.InventoryId].TryAdd(food.Id, take, Content.Items);
                    left -= take;
                }
            }

            Campaign.InterruptTask(character.Id, InterruptReasons.Moved);
            Campaign.RemovePlacement(character.Id);
        }

        var ticksPerDay = (double)SimulationClock.TicksPerDay;
        world.AddJourney(new Journey
        {
            Id = world.NextJourney++, Members = members, Destination = placeId, Purpose = purpose, Path = way.Path, Left = now,
            Arrives = now + (long)(days * ticksPerDay), Returns = now + (long)(total * ticksPerDay),
        });
        Campaign.Record(ChronicleEntryKind.CoastNews, [new(ChronicleParameters.Need,
            $"{string.Join(", ", people.Select(c => c.Name))} set out for {place.Name}{(way.Guessed ? ", guessing at the way" : "")}: about {total:0} days there and back.")]);
        return CommandResult.Ok();
    }

    // Each hour: parties arriving and coming home; on the road they live off their packs and drink from the
    // streams.
    private void StepJourneys(WorldState world, CampState camp)
    {
        var now = Campaign.Clock.Tick;
        foreach (var journey in world.Journeys.ToList())
        {
            foreach (var id in journey.Members)
            {
                Campaign.Characters[id].Needs.Thirst = Math.Min(Campaign.Characters[id].Needs.Thirst, 20);
            }

            if (!journey.Arrived && now >= journey.Arrives)
            {
                journey.Arrived = true;
                Arrive(world, journey);
            }

            if (now % SimulationClock.TicksPerDay == 0)
            {
                // The road's hazards: a fall, a fever caught in the low ground.
                foreach (var id in journey.Members)
                {
                    var character = Campaign.Characters[id];
                    if (Campaign.Randomness.Events.Chance(0.015) && !character.Health.Conditions.Contains("condition.sprain"))
                    {
                        character.Health.AddCondition("condition.sprain");
                        journey.Report.Add($"{character.Name} turned an ankle on the way.");
                    }
                    else if (Campaign.Randomness.Events.Chance(Learned("know.fever_lore") ? 0.01 : 0.02) && !character.Health.Conditions.Contains("condition.fever"))
                    {
                        character.Health.AddCondition("condition.fever");
                        journey.Report.Add($"{character.Name} came down with a fever on the road.");
                    }
                }
            }

            if (now >= journey.Returns)
            {
                Return(world, camp, journey);
            }
        }
    }

    private void Arrive(WorldState world, Journey journey)
    {
        var region = Region!;
        var place = region.Place(journey.Destination)!;
        foreach (var (from, to) in journey.Path.Zip(journey.Path.Skip(1)))
        {
            Tell(world, to, PlaceKnowledge.Visited, "a party");
            if (region.Routes.FirstOrDefault(r => r.Other(from) == to) is { } walked)
            {
                world.KnowRoute(walked.Key);
            }
        }

        journey.Report.Add($"At {place.Name}: {place.Text}");
        // From there they see the ways on, and what lies at the end of them.
        foreach (var route in region.Routes.Where(r => r.Other(place.Id) is not null))
        {
            Tell(world, route.Other(place.Id)!, PlaceKnowledge.Seen, "a party");
            world.KnowRoute(route.Key);
        }

        var party = journey.Members.Select(m => Campaign.Characters[m]).ToList();
        if (place.People is { } peopleId && region.Community(peopleId) is { } community)
        {
            // Come to their village unasked: it goes by how they stand to the newcomers, and who can speak.
            var state = world.Community(peopleId);
            var speaks = party.Max(c => c.Languages.Get(community.Language));
            state.Met = true;
            if (Stance(peopleId) is "hostile" or "avoiding")
            {
                Trust(peopleId, -2, 4);
                journey.Report.Add($"{community.Name} would not let them past the edge of the village: they were sent back the way they came.");
            }
            else
            {
                Trust(peopleId, speaks >= 2 ? 4 : 1);
                journey.Report.Add(speaks >= 2
                    ? $"{community.Name} took them in for a night, fed them, and asked a great many questions."
                    : $"{community.Name} fed them and watched them, and they understood little of what was said.");
                // Ximena among them at Mahenu.
                if (peopleId == "people.mahenu" && party.Any(c => c.PersonId == "person.ximena"))
                {
                    Trust(peopleId, 6);
                    journey.Report.Add("Ximena went to a house at the ring's edge and was a long time there, and came out with a boy of fourteen beside her.");
                }
            }
        }

        if (place.Sacred && place.Ground is { } keepers)
        {
            Trust(keepers, -15, 25);
            journey.Report.Add($"They went up {place.Name}. They should not have: those whose right it is saw them.");
        }

        if (journey.Purpose == "wood" && place.Wood)
        {
            journey.Logs = party.Count(c => c.CanWork) * 3;
            var leave = place.Ground is { } owners && world.Community(owners).Leave.Contains("wood");
            if (!leave)
            {
                foreach (var ahuneri in region.Communities.Where(c => c.Id == place.Ground || c.Id == "people.mahenu"))
                {
                    Trust(ahuneri.Id, -3, ahuneri.Id == place.Ground ? 14 : 4);
                }

                journey.Report.Add("They cut the red wood without leave, and were watched doing it.");
            }
        }
    }

    private void Return(WorldState world, CampState camp, Journey journey)
    {
        world.EndJourney(journey);
        var location = Content.Locations[CampLocation];
        var back = location.FindSpot("spot.scout_1")?.Cell ?? location.FindSpot("spot.hearth")!.Cell;
        foreach (var id in journey.Members.Where(id => Campaign.Characters[id].SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated))
        {
            Campaign.SetPlacement(id, new CharacterPlacement(CampLocation, back));
            // What is left of their packs goes back in the stores.
            var pack = Campaign.Inventories[Campaign.Characters[id].InventoryId];
            foreach (var food in Content.Foods.Where(f => pack.Count(f.Id) > 0).ToList())
            {
                var left = pack.Count(food.Id);
                pack.TryRemove(food.Id, left);
                Campaign.SharedStore.TryAdd(food.Id, left, Content.Items);
            }
        }

        if (journey.Logs > 0)
        {
            Campaign.SharedStore.TryAdd("item.embrawood", journey.Logs, Content.Items);
            journey.Report.Add($"They brought back {journey.Logs} logs of embrawood, dragged and carried.");
        }

        Campaign.Record(ChronicleEntryKind.CoastNews, [new(ChronicleParameters.Need,
            $"The party is back from {Region!.Place(journey.Destination)!.Name}. " + string.Join(" ", journey.Report))]);
    }

    // ================================================================ the charter

    /// <summary>What the relief ship will judge, as things stand: each of the charter's terms, and whether it is met.</summary>
    public IReadOnlyList<(string Term, bool Met)> CharterTerms()
    {
        if (Region is not { } region || Campaign.Camp is not { } camp || Campaign.World is not { } world)
        {
            return [];
        }

        var charter = region.Charter;
        var alive = Ashore().Count;
        var need = Math.Max(charter.AliveAtLeast, (int)Math.Ceiling(camp.LandedCount * charter.AliveShare));
        var terms = new List<(string, bool)>
        {
            ($"{charter.Logs} logs of embrawood ({Campaign.SharedStore.Count("item.embrawood")} now)", Campaign.SharedStore.Count("item.embrawood") >= charter.Logs),
            ($"a factory standing at Punta Ceniza", camp.Site == "coast.punta_ceniza" && CampStage() != "a landfall camp"),
            ($"{need} alive ({alive} now)", alive >= need),
            ("the trade peace kept", !world.PeaceBroken && world.LogsSold == 0),
        };
        if (charter.Palisade)
        {
            terms.Add(("a palisade", camp.Work(CampWorks.Palisade) >= 1));
        }

        return terms;
    }

    /// <summary>Days until the relief ship comes, or null once it has.</summary>
    public double? DaysToRelief => Region is { } region && Campaign.World is { Charter: null } ? Math.Max(0, region.Charter.ReliefDay - CampDay) : null;

    private void JudgeCharter(WorldState world, CampState camp)
    {
        var terms = CharterTerms();
        var met = terms.Count(t => t.Met);
        world.Charter = world.PeaceBroken || world.LogsSold > 0 ? "revoked" : met == terms.Count ? "kept" : met >= terms.Count - 2 ? "in part" : "revoked";
        var logs = Math.Min(Campaign.SharedStore.Count("item.embrawood"), Region!.Charter.Logs);
        Campaign.SharedStore.TryRemove("item.embrawood", logs);
        // A letter that reached the Company in time: she comes better found.
        if (world.LetterSent > 0 && world.LetterSent < camp.Landed + 120L * SimulationClock.TicksPerDay)
        {
            Campaign.SharedStore.TryAdd("item.biscuit", 200, Content.Items);
            Campaign.SharedStore.TryAdd("item.axe", 2, Content.Items);
        }

        Campaign.Record(ChronicleEntryKind.CharterJudged, [
            new(ChronicleParameters.Outcome, world.Charter),
            new(ChronicleParameters.Reason, string.Join(", ", terms.Where(t => !t.Met).Select(t => t.Term))),
            new(ChronicleParameters.Level, logs.ToString(CultureInfo.InvariantCulture)),
        ]);
    }

    // ================================================================ the palisade

    /// <summary>Orders the palisade raised round the camp, or stops it.</summary>
    public CommandResult OrderPalisade(bool on)
    {
        if (Campaign.Camp is not { } camp)
        {
            return CommandResult.Rejected("There is no camp to wall.");
        }

        camp.PalisadeOrdered = on;
        return CommandResult.Ok();
    }

    // ================================================================ each hour and day

    // Once an hour ashore: parties on the road; at first light the peoples' day, the ships, the charter.
    private void StepWorld(CampState camp)
    {
        if (Campaign.World is not { } world)
        {
            return;
        }

        StepJourneys(world, camp);
        if (Campaign.Clock.Tick % SimulationClock.TicksPerDay != 7 * SimulationClock.TicksPerHour)
        {
            return;
        }

        // Mahenu sees the palisade going up.
        if (camp.Work(CampWorks.Palisade) >= 0.3 && !world.PalisadeNoticed)
        {
            world.PalisadeNoticed = true;
            Trust("people.mahenu", -5);
            Trust("people.tesiwa", 3);
            News("The palisade is going up, and the lagoon has seen it: canoes stand off to look at the stakes.");
        }

        StepPeoplesDay(world, camp);
        StepBrennishDay(world, camp);
        if (world.Charter is null && Region is { } region && CampDay >= region.Charter.ReliefDay)
        {
            JudgeCharter(world, camp);
        }
    }

    private static string Article(string noun) => (noun.Length > 0 && "aeiou".Contains(noun[0]) ? "an " : "a ") + noun;

    /// <summary>Developer tool for tests and screenshots: a people at the camp now, whatever the day.</summary>
    public CommandResult DeveloperMeeting(string communityId)
    {
        if (Campaign.World is not { } world || Region?.Community(communityId) is null)
        {
            return CommandResult.Rejected("No such people.");
        }

        world.Community(communityId).NextVisit = 0;
        world.Meeting = null;
        StepPeoplesDay(world, Campaign.Camp!);
        return world.Meeting?.Community == communityId ? CommandResult.Ok() : CommandResult.Rejected("They would not come.");
    }

    /// <summary>Developer tool: something learned from the people who know it, as if told.</summary>
    public void DeveloperLearn(string topicId)
    {
        if (Campaign.World is { } world && Region?.Topic(topicId) is { } topic && Region.Community(topic.From.FirstOrDefault() ?? "") is { } from)
        {
            Teach(world, topic, from);
        }
    }

    /// <summary>Developer tool for screenshots: whatever meeting is waiting goes away unanswered.</summary>
    public void DeveloperDismissMeeting()
    {
        if (Campaign.World is { } world)
        {
            world.Meeting = null;
        }
    }

    /// <summary>Developer tool: sets a people's trust.</summary>
    public void DeveloperTrust(string communityId, double trust)
    {
        if (Campaign.World is { } world && Region?.Community(communityId) is not null)
        {
            world.Community(communityId).Trust = trust;
        }
    }
}
