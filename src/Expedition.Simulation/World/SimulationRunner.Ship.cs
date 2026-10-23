using System.Globalization;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Shore;
using Expedition.Simulation.Voyage;

namespace Expedition.Simulation.World;

/// <summary>
/// Step S (docs/THE_SHIP.md): the ship after landfall (ECONOMY_LOOP §22, §56). Who stays aboard her keeps her:
/// a ship left alone fouls and wears, and drags in a storm. With hands enough she fishes and coasts. Her guns,
/// powder and shot, and the privateers who come for what a new post has, met by the captain's decision, not
/// fought out gun by gun.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>The ship's guns: the Garza's six falconets.</summary>
    public const int GunsAboard = 6;

    /// <summary>Hands to work a gun.</summary>
    public const int GunCrew = 3;

    /// <summary>Hands aboard to take her out, fishing or along the coast, or to hold her against boarders.</summary>
    public const int CoastingCrew = 4;

    // Powder and shot a gun fires in a fight.
    private const int ChargesPerGun = 2;

    // The hull's wear a day at anchor, foul and worm-eaten, with no one aboard and with hands to keep her.
    private const double WearAlone = 0.35, WearKept = 0.1;

    // What a day's careening at the slip gives back.
    private const double CareenHull = 10;

    // A day's provisions for each hand aboard, taken from what is still aboard, else from the stores.
    private const int AboardFood = 3, AboardDrink = 3;

    // Places the ship sees coasting.
    private static readonly HashSet<string> CoastKinds = new(["shore", "headland", "cove", "landing", "river", "camp"], StringComparer.Ordinal);

    // ================================================================ the ship's company

    /// <summary>Whether someone is aboard the ship.</summary>
    public bool Aboard(CharacterId id) => Campaign.Camp?.Keepers.Contains(id) == true;

    /// <summary>The ship's company aboard her.</summary>
    public IReadOnlyList<Character> ShipsCompany() =>
        Campaign.Camp?.Keepers.Select(k => Campaign.Characters[k]).Where(c => c.SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated).ToList() ?? [];

    /// <summary>
    /// Sets who stays aboard the ship at anchor to keep her: the rest come ashore. Those going aboard leave
    /// the camp's work; the boat brings those coming ashore to the hearth.
    /// </summary>
    public CommandResult SetKeepers(IReadOnlyList<CharacterId> keepers)
    {
        if (Campaign.Camp is not { } camp || !camp.ShipAtAnchor)
        {
            return CommandResult.Rejected("There is no ship at anchor to keep.");
        }

        if (camp.Errand is not null)
        {
            return CommandResult.Rejected("She is away: her company can change when she is back.");
        }

        foreach (var id in keepers.Where(k => !camp.Keepers.Contains(k)))
        {
            if (!Campaign.Characters.TryGetValue(id, out var person) || person.Membership != Membership.Crew || !person.CanWork || IsChild(person))
            {
                return CommandResult.Rejected("Only those of the company fit to work can keep the ship.");
            }

            if (Campaign.World?.Journeys.Any(j => j.Members.Contains(id)) == true)
            {
                return CommandResult.Rejected($"{person.Name} is away from the camp.");
            }
        }

        if (!camp.Boat && keepers.ToHashSet().SetEquals(camp.Keepers) is false)
        {
            return CommandResult.Rejected("There is no boat to take them out to her or bring them ashore.");
        }

        var coming = camp.Keepers.Where(k => !keepers.Contains(k)).ToList();
        var going = keepers.Where(k => !camp.Keepers.Contains(k)).ToList();
        foreach (var id in going)
        {
            Campaign.InterruptTask(id, InterruptReasons.Moved);
            Campaign.RemovePlacement(id);
        }

        camp.SetKeepers(keepers);
        foreach (var id in coming)
        {
            ComeAshore(id);
        }

        AssignCampDuties();
        return CommandResult.Ok();
    }

    // Someone set down at the camp's hearth from the boat.
    private void ComeAshore(CharacterId id)
    {
        if (Campaign.Characters[id].SurvivalState is SurvivalState.Dead or SurvivalState.Missing || Campaign.Placements.ContainsKey(id))
        {
            return;
        }

        var location = Location(CampLocation);
        Campaign.SetPlacement(id, new CharacterPlacement(CampLocation, location.FindSpot("spot.hearth")?.Cell ?? location.Spots[0].Cell));
    }

    // ================================================================ the day

    // At first light: the ship's company provisioned, her hull worn or mended, the powder kept or spoiled, her
    // errand's end, and the privateers.
    internal void StepShipDay(CampState camp)
    {
        var now = Campaign.Clock.Tick;
        if (camp.Errand is not null && now >= camp.ErrandBack)
        {
            ErrandDone(camp);
        }

        // Provisions out to her: from what is aboard first, else the stores ashore by the boat.
        foreach (var hand in ShipsCompany())
        {
            Provision(camp, hand, Content.Foods.Where(f => f.Sickens is null).OrderBy(f => f.SpoilsPerDay).ThenBy(f => f.Id, StringComparer.Ordinal), AboardFood);
            Provision(camp, hand, Content.Drinks.OrderBy(d => d.Id, StringComparer.Ordinal), AboardDrink);
        }

        if (camp.ShipAtAnchor && camp.Errand is null && Campaign.Voyage is { } ship)
        {
            var company = ShipsCompany();
            ship.Apply(new ShipEffects { Hull = -(company.Count >= 2 ? WearKept : WearAlone) }, 1);
            // A hand who knows the work pays a seam or two every other day, while there is oakum.
            if (ship.Hull < 95 && Campaign.Clock.Day % 2 == 0 && company.Any(c => c.CanWork && c.Skills.Get("skill.ship_repair") >= 1) && TakeShipStore(camp, "item.oakum", 1))
            {
                ship.Apply(new ShipEffects { Hull = 1 }, 1);
            }
        }

        // Powder takes the damp: in the open, aboard, or in a magazine.
        var damp = StandingOf(b => b.Magazine).Any() ? 0.001 : WetSeason ? 0.03 : 0.01;
        Spoil(Campaign.SharedStore.Count("item.powder"), damp, lost => Campaign.SharedStore.TryRemove("item.powder", lost));
        Spoil(camp.Afloat.GetValueOrDefault("item.powder"), 0.002, lost => camp.TakeAfloat("item.powder", lost));

        if (Campaign.World is { } world)
        {
            StepPrivateerDay(world, camp);
        }
    }

    private void Spoil(int count, double rate, Action<int> lose)
    {
        var expected = count * rate;
        var lost = Math.Min(count, (int)Math.Floor(expected) + (Campaign.Randomness.Stores.Chance(expected - Math.Floor(expected)) ? 1 : 0));
        if (lost > 0)
        {
            lose(lost);
        }
    }

    private void Provision(CampState camp, Character hand, IEnumerable<Items.ItemDefinition> kinds, int wanted)
    {
        var pack = Campaign.Inventories[hand.InventoryId];
        var list = kinds.ToList();
        var need = wanted - list.Sum(k => pack.Count(k.Id));
        foreach (var kind in list)
        {
            var aboard = Math.Min(Math.Max(0, need), camp.Afloat.GetValueOrDefault(kind.Id));
            if (aboard > 0)
            {
                camp.TakeAfloat(kind.Id, aboard);
                pack.TryAdd(kind.Id, aboard, Content.Items);
                need -= aboard;
            }

            var ashore = camp.Boat ? Math.Min(Math.Max(0, need), Campaign.SharedStore.Count(kind.Id)) : 0;
            if (ashore > 0 && Campaign.SharedStore.TryRemove(kind.Id, ashore))
            {
                pack.TryAdd(kind.Id, ashore, Content.Items);
                need -= ashore;
            }
        }
    }

    // Takes something the ship's company wants from aboard her, else from the stores ashore.
    private bool TakeShipStore(CampState camp, string item, int count)
    {
        if (camp.Afloat.GetValueOrDefault(item) >= count)
        {
            camp.TakeAfloat(item, count);
            return true;
        }

        return Campaign.SharedStore.TryRemove(item, count);
    }

    // In a storm at anchor: hands aboard veer cable and lay out a second anchor; with none, she may drag ashore.
    private void ShipInStorm(CampState camp)
    {
        if (!camp.ShipAtAnchor || camp.Errand is not null || Campaign.Voyage is not { } ship)
        {
            return;
        }

        var kept = ShipsCompany().Count >= 2;
        ship.Apply(new ShipEffects { Hull = kept ? -0.2 : -0.4 }, 1);
        if (!kept && Campaign.Randomness.Events.Chance(0.01))
        {
            ShipLost(camp, "storm");
        }
    }

    // The ship gone: driven ashore and broken, or cut out by a privateer. Her company comes ashore as best it can.
    private void ShipLost(CampState camp, string how)
    {
        camp.ShipAtAnchor = false;
        camp.Errand = null;
        if (how == "privateer")
        {
            foreach (var (item, count) in camp.Afloat.ToList())
            {
                camp.TakeAfloat(item, count);
            }
        }

        camp.Guns = 0;
        var company = camp.Keepers.ToList();
        camp.SetKeepers([]);
        foreach (var id in company)
        {
            ComeAshore(id);
        }

        if (Campaign.Voyage is { } ship && how == "storm")
        {
            ship.Apply(new ShipEffects { Hull = -100 }, 1);
        }

        AssignCampDuties();
        Campaign.Record(ChronicleEntryKind.ShipLost, [new(ChronicleParameters.Reason, how)]);
    }

    // ================================================================ errands

    /// <summary>
    /// Takes the ship out with her company: <c>fish</c> for a day off the coast, or <c>coast</c> for four days
    /// along it, seeing what lies there.
    /// </summary>
    public CommandResult SendShip(string errand)
    {
        if (Campaign.Camp is not { } camp || !camp.ShipAtAnchor || Campaign.Voyage is not { } ship)
        {
            return CommandResult.Rejected("There is no ship at anchor.");
        }

        if (camp.Errand is not null)
        {
            return CommandResult.Rejected("She is away already.");
        }

        if (errand is not ("fish" or "coast"))
        {
            return CommandResult.Rejected("She can go fishing, or along the coast.");
        }

        var company = ShipsCompany().Count(c => c.CanWork);
        if (company < CoastingCrew)
        {
            return CommandResult.Rejected($"She wants {CoastingCrew} hands aboard to work her; she has {company}.");
        }

        if (ship.Hull < 40)
        {
            return CommandResult.Rejected($"Her hull is at {ship.Hull:0}%: she is not fit to go out until she is careened.");
        }

        if (StormComing || Campaign.World?.Privateer == 2)
        {
            return CommandResult.Rejected(StormComing ? "Not with the weather coming." : "Not with a privateer at the anchorage.");
        }

        camp.Errand = errand;
        camp.ErrandBack = Campaign.Clock.Tick + (errand == "fish" ? 1 : 4) * SimulationClock.TicksPerDay;
        Campaign.Record(ChronicleEntryKind.ShipErrand, [new(ChronicleParameters.To, errand)]);
        return CommandResult.Ok();
    }

    private void ErrandDone(CampState camp)
    {
        var errand = camp.Errand;
        camp.Errand = null;
        var company = ShipsCompany();
        if (errand == "fish")
        {
            var fish = company.Where(c => c.CanWork).Sum(c => 3 + 2 * c.Skills.Get("skill.fishing"));
            Campaign.SharedStore.TryAdd("item.fish", fish, Content.Items);
            Campaign.Voyage?.Apply(new ShipEffects { Hull = -0.5 }, 1);
            News($"The ship is back from a day's fishing off the coast, with {fish} fish.");
            return;
        }

        if (Campaign.World is { } world && Region is { } region)
        {
            var seen = region.Places.Where(p => CoastKinds.Contains(p.Kind) && PlaceKnown(p.Id) < PlaceKnowledge.Seen).ToList();
            foreach (var place in seen)
            {
                Tell(world, place.Id, PlaceKnowledge.Seen, "the ship, coasting");
            }

            Campaign.Voyage?.Apply(new ShipEffects { Hull = -1.5 }, 1);
            News(seen.Count == 0
                ? "The ship is back from four days along the coast: nothing that was not known already."
                : $"The ship is back from four days along the coast. Seen from the sea: {string.Join(", ", seen.Select(p => p.Name))}.");
        }
    }

    // ================================================================ the guns

    /// <summary>Guns that can be fought now: those aboard with a crew for each, those on the platform with hands ashore.</summary>
    public (int Aboard, int Ashore, double Skill) GunsManned()
    {
        if (Campaign.Camp is not { } camp)
        {
            return (0, 0, 0);
        }

        var company = ShipsCompany().Where(c => c.CanWork).ToList();
        var aboard = camp.ShipAtAnchor && camp.Errand is null ? Math.Min(camp.Guns, company.Count / GunCrew) : 0;
        var ashoreHands = Ashore().Where(c => c.CanWork && !Away(c.Id) && !IsChild(c)).ToList();
        var mounted = StandingOf(b => b.Battery > 0).Any() ? camp.GunsAshore : 0;
        var ashore = Math.Min(mounted, ashoreHands.Count / GunCrew);
        // The gun crews are the best seamen: they have worked the guns in drill.
        var crews = company.Concat(ashoreHands).Select(c => c.Skills.Get("skill.seamanship")).OrderByDescending(s => s).Take((aboard + ashore) * GunCrew).ToList();
        return (aboard, ashore, crews.Count == 0 ? 0 : crews.Average());
    }

    /// <summary>Lands guns from the ship to the gun platform, as many as it mounts.</summary>
    public CommandResult LandGuns()
    {
        if (Campaign.Camp is not { } camp || !camp.ShipAtAnchor || camp.Errand is not null)
        {
            return CommandResult.Rejected("There is no ship at anchor to land guns from.");
        }

        var mounts = StandingOf(b => b.Battery > 0).Sum(b => Settling!.Building(b.Building)!.Battery) - camp.GunsAshore;
        if (mounts <= 0)
        {
            return CommandResult.Rejected(camp.GunsAshore > 0 ? "The gun platform has its guns." : "There is no gun platform to mount them on.");
        }

        if (!camp.Boat || camp.Guns == 0)
        {
            return CommandResult.Rejected(camp.Guns == 0 ? "There are no guns aboard." : "There is no boat to bring them ashore.");
        }

        var landed = Math.Min(mounts, camp.Guns);
        camp.Guns -= landed;
        camp.GunsAshore += landed;
        News($"{landed} {(landed == 1 ? "gun" : "guns")} swayed out of the ship into the boat, landed, and mounted on the platform over the anchorage.");
        return CommandResult.Ok();
    }

    // ================================================================ privateers

    private void StepPrivateerDay(WorldState world, CampState camp)
    {
        if (Region?.Privateer is not { } rules)
        {
            return;
        }

        var now = Campaign.Clock.Tick;
        if (world.PrivateerNext == 0)
        {
            world.PrivateerNext = camp.Landed + rules.FirstDay * SimulationClock.TicksPerDay;
        }

        if (world.Privateer == 0 && now >= world.PrivateerNext)
        {
            if (StandingOf(b => b.Lookout).Any())
            {
                world.Privateer = 1;
                News($"From the lookout: a sail to the south, standing in under no colours. A Brennish-built sloop, by her rig. She will be off the anchorage by tomorrow.");
                return;
            }

            world.Privateer = 2;
        }
        else if (world.Privateer == 1)
        {
            world.Privateer = 2;
        }

        if (world.Privateer == 2 && world.Meeting is null && Campaign.PendingEvent is null)
        {
            world.Meeting = new Meeting { Kind = "privateer", Tick = now };
        }
    }

    // The privateer's boat, and what the camp has to meet her with.
    private (string Title, string Text) DescribePrivateer(PrivateerRules rules)
    {
        var (aboard, ashore, _) = GunsManned();
        var camp = Campaign.Camp!;
        var warned = StandingOf(b => b.Lookout).Any();
        var guns = aboard + ashore == 0 ? "No gun can be fought: there are not the hands for them, aboard or ashore."
            : $"Guns that can be fought: {aboard} aboard{(ashore > 0 ? $", {ashore} on the platform" : "")}. Powder for {Campaign.SharedStore.Count("item.powder") + camp.Afloat.GetValueOrDefault("item.powder")} charges, {Campaign.SharedStore.Count("item.shot") + camp.Afloat.GetValueOrDefault("item.shot")} shot.";
        var ship = !camp.ShipAtAnchor ? "" : ShipsCompany().Count >= CoastingCrew ? " The ship's company can hold her against boarders." : " There are too few aboard the ship to hold her if they board.";
        return ($"{Capitalise(rules.Ship)}",
            $"{(warned ? "The sail the lookout saw" : "A sloop no one saw coming")} has anchored off, her guns run out. A boat comes in under a white rag: {rules.Master}, who holds a Brennish letter of marque, or says he does. "
            + $"{rules.Demand} sueldos, or the worth of it from the stores, and he will go. {guns}{ship}");
    }

    private List<MeetingChoice> PrivateerChoices(PrivateerRules rules)
    {
        var (aboard, ashore, skill) = GunsManned();
        var manned = FightableGuns(aboard + ashore);
        var chance = FightChance(manned, ashore > 0, skill);
        var funds = Campaign.Voyage?.Funds ?? 0;
        var goods = Campaign.SharedStore.Count("item.trade_cloth") + Campaign.SharedStore.Count("item.trade_knives");
        var warned = StandingOf(b => b.Lookout).Any();
        return
        [
            new("fight", "Run out the guns and fight her off", manned > 0,
                manned > 0 ? $"{manned} {(manned == 1 ? "gun" : "guns")} fought: about {Math.Round(chance * 10):0} in 10" : "no gun can be fought: hands, powder and shot are wanting"),
            new("pay", $"Pay him {rules.Demand} sueldos", funds >= rules.Demand, funds >= rules.Demand ? null : "the purse is short"),
            new("goods", "Pay him in trade goods instead", goods >= 10, goods >= 10 ? "ten of the cloth and knives" : "there are not enough"),
            new("hide", "Carry the stores into the forest and let him take what he finds", true,
                warned ? "the lookout's warning gave a day to hide them" : "with no warning, much will be found"),
        ];
    }

    // Guns there is powder and shot for.
    private int FightableGuns(int manned)
    {
        var camp = Campaign.Camp!;
        var powder = Campaign.SharedStore.Count("item.powder") + camp.Afloat.GetValueOrDefault("item.powder");
        var shot = Campaign.SharedStore.Count("item.shot") + camp.Afloat.GetValueOrDefault("item.shot");
        return Math.Min(manned, Math.Min(powder, shot) / ChargesPerGun);
    }

    /// <summary>The chance the camp's guns drive a privateer off: more guns, better crews, a platform ashore, warning.</summary>
    public double FightChance(int guns, bool battery, double skill) =>
        guns <= 0 ? 0 : Math.Clamp(0.15 + 0.12 * guns * (0.6 + skill / 5) + (battery ? 0.15 : 0) + (StandingOf(b => b.Lookout).Any() ? 0.1 : 0), 0.05, 0.9);

    private (string Text, bool Ends, bool Ok) PrivateerChoice(WorldState world, string choiceId)
    {
        var rules = Region!.Privateer!;
        var camp = Campaign.Camp!;
        world.Privateer = 0;
        world.PrivateerNext = Campaign.Clock.Tick + rules.EveryDays * SimulationClock.TicksPerDay;
        world.PrivateerVisits++;
        var holds = ShipsCompany().Count(c => c.CanWork) >= CoastingCrew;
        switch (choiceId)
        {
            case "fight":
            {
                var (aboard, ashore, skill) = GunsManned();
                var guns = FightableGuns(aboard + ashore);
                var chance = FightChance(guns, ashore > 0, skill);
                Fire(camp, guns * ChargesPerGun);
                var won = Campaign.Randomness.Events.Chance(chance);
                Wound(0.35);
                Campaign.Record(ChronicleEntryKind.PrivateerMet, [new(ChronicleParameters.Outcome, won ? "fought off" : "beaten"), new(ChronicleParameters.Level, guns.ToString(CultureInfo.InvariantCulture))]);
                if (won)
                {
                    foreach (var person in Ashore())
                    {
                        person.Needs.Stress = Needs.Clamp(person.Needs.Stress - 3);
                    }

                    return ($"The guns speak, {guns} of them, and the smoke hangs over the anchorage. {Capitalise(rules.Ship)} takes a shot through her bows, cuts her cable and stands out to sea. She will remember this camp.", true, true);
                }

                return ($"The guns are fought, but not well enough: {rules.Ship} closes, and her people come ashore. " + Plunder(camp, 0.3, holds), true, false);
            }

            case "pay":
                Campaign.Voyage!.Funds -= rules.Demand;
                Campaign.Record(ChronicleEntryKind.PrivateerMet, [new(ChronicleParameters.Outcome, "paid")]);
                return ($"{rules.Master} counts the {rules.Demand} sueldos twice, wishes the camp a long life, and goes. He will be back.", true, true);
            case "goods":
            {
                var left = 10;
                foreach (var item in new[] { "item.trade_cloth", "item.trade_knives" })
                {
                    var give = Math.Min(left, Campaign.SharedStore.Count(item));
                    Campaign.SharedStore.TryRemove(item, give);
                    left -= give;
                }

                Campaign.Record(ChronicleEntryKind.PrivateerMet, [new(ChronicleParameters.Outcome, "paid")]);
                return ($"Ten bolts and boxes of the trade goods go out to the boat. {rules.Master} grumbles at the bargain, and goes.", true, true);
            }

            default:
                Campaign.Record(ChronicleEntryKind.PrivateerMet, [new(ChronicleParameters.Outcome, "hid")]);
                return ("The camp takes to the forest with what it can carry. " + Plunder(camp, StandingOf(b => b.Lookout).Any() ? 0.1 : 0.35, holds), true, false);
        }
    }

    // Powder and shot fired, from the stores ashore and then what is aboard.
    private void Fire(CampState camp, int charges)
    {
        foreach (var item in new[] { "item.powder", "item.shot" })
        {
            var ashore = Math.Min(charges, Campaign.SharedStore.Count(item));
            Campaign.SharedStore.TryRemove(item, ashore);
            camp.TakeAfloat(item, Math.Min(charges - ashore, camp.Afloat.GetValueOrDefault(item)));
        }
    }

    // Someone hurt in it, as likely as given.
    private void Wound(double chance)
    {
        var hands = Ashore().Concat(ShipsCompany()).Where(c => c.SurvivalState == SurvivalState.Alive && !IsChild(c)).Distinct().OrderBy(c => c.Id.Value).ToList();
        if (hands.Count > 0 && Campaign.Randomness.Events.Chance(chance))
        {
            var hurt = hands[Campaign.Randomness.Events.NextInt(hands.Count)];
            hurt.Health.AddCondition("condition.wounded");
            News($"{hurt.Name} is wounded.");
        }
    }

    // What the privateer's people take: a share of the food, the trade goods and the purse; the ship too if
    // there are not hands aboard to hold her.
    private string Plunder(CampState camp, double share, bool holds)
    {
        var taken = 0;
        foreach (var item in Content.Items.Values.Where(i => i.HungerRelief > 0 || i.Category is "trade" or "munition" or "material").OrderBy(i => i.Id, StringComparer.Ordinal))
        {
            var take = (int)Math.Floor(Campaign.SharedStore.Count(item.Id) * share);
            if (take > 0 && Campaign.SharedStore.TryRemove(item.Id, take))
            {
                taken += take;
            }
        }

        var purse = Campaign.Voyage is { } voyage ? (int)(voyage.Funds * share) : 0;
        if (Campaign.Voyage is { } paying)
        {
            paying.Funds -= purse;
        }

        Wound(0.25);
        var text = $"They take what they find: {taken} lots of the stores{(purse > 0 ? $" and {purse} sueldos" : "")}.";
        if (camp.ShipAtAnchor && !holds)
        {
            ShipLost(camp, "privateer");
            return text + " And they cut out the ship: there were too few aboard her to hold her. She is gone, with all that was still in her.";
        }

        return text + (camp.ShipAtAnchor ? " The ship's company holds her, and they leave her be." : "");
    }

    // ================================================================ the ship, in words

    /// <summary>The ship after landfall, for the Camp drawer: her state, her company, her guns, powder and shot.</summary>
    public IReadOnlyList<string> ShipReport()
    {
        if (Campaign.Camp is not { } camp)
        {
            return [];
        }

        if (!camp.ShipAtAnchor)
        {
            return ["There is no ship: the camp has its boat, if that.", $"Powder {Campaign.SharedStore.Count("item.powder")} charges, shot {Campaign.SharedStore.Count("item.shot")}."
                + (camp.GunsAshore > 0 ? $" {camp.GunsAshore} guns on the platform." : "")];
        }

        var company = ShipsCompany();
        var (aboard, ashore, _) = GunsManned();
        var hull = Campaign.Voyage?.Hull ?? 0;
        return
        [
            camp.Errand switch
            {
                "fish" => "Away fishing, back tomorrow.",
                "coast" => $"Away along the coast, back in {Math.Ceiling((camp.ErrandBack - Campaign.Clock.Tick) / (double)SimulationClock.TicksPerDay):0} days.",
                _ => $"At anchor off, her hull at {hull:0}%: {(company.Count >= 2 ? "kept, and wearing slowly" : "left alone, fouling and wearing")}.",
            },
            company.Count == 0 ? "No one aboard her." : $"Aboard: {string.Join(", ", company.Select(c => c.Name))}."
                + (company.Count < CoastingCrew ? $" She wants {CoastingCrew} to go out or to hold her against boarders." : ""),
            $"Guns: {camp.Guns} aboard{(camp.GunsAshore > 0 ? $", {camp.GunsAshore} on the platform" : "")}; {aboard + ashore} can be fought now ({GunCrew} hands to a gun). "
                + $"Powder {Campaign.SharedStore.Count("item.powder") + camp.Afloat.GetValueOrDefault("item.powder")} charges, shot {Campaign.SharedStore.Count("item.shot") + camp.Afloat.GetValueOrDefault("item.shot")}.",
        ];
    }
}
