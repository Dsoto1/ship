using System.Globalization;
using Expedition.Simulation.Campaign;
using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Shore;

namespace Expedition.Simulation.World;

/// <summary>
/// Step P (docs/PEOPLE.md): the community grows by people (ECONOMY_LOOP §39). Settlers come with the relief
/// ship when the charter is kept, people of the coast choose to come and live at the camp, a hand leaves an
/// interloper's ship for it. Couples make households, some with people of the coast, whose customs come with
/// them (§32). Children are born, and grow up; everyone ages a year each year, and the seasons come round.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>Under this age a person is a child, and does no work.</summary>
    public const int ChildUntil = 12;

    // Adults, for making a home; a woman bears children within these years.
    private const int AdultFrom = 16;
    private const int BearsFrom = 16, BearsUntil = 42;

    // Affection each way for two of the company to make a home together, and the chance a day once they would.
    private const int CoupleAffection = 35;
    private const double CoupleChance = 1.0 / 30;

    // A people's trust for one of them to come and live with one of the company, and the chance a day.
    private const double PartnerTrust = 50;
    private const double PartnerChance = 1.0 / 80;

    // A people's trust for one of them to come and live at the camp, the chance a day, and how many in all.
    private const double JoinTrust = 45;
    private const double JoinChance = 1.0 / 60;
    private const int JoinAtMost = 2;
    private const int JoinFromDay = 60;

    // The chance a day a household that can have a child begins to expect one, and the days it takes.
    private const double ExpectChance = 1.0 / 120;
    private const int CarryDays = 270;

    // A gift-giving people expects a gift to the family when one of theirs comes to live with a newcomer.
    private static readonly (string Item, int Count)[] FamilyGifts = [("item.trade_cloth", 2), ("item.trade_knives", 2)];

    /// <summary>Whether someone is a child: they do no work.</summary>
    public static bool IsChild(Character character) => character.Age < ChildUntil;

    /// <summary>The household someone is of, as one of the couple or a child, or null.</summary>
    public Household? HouseholdOf(CharacterId id) =>
        Campaign.Households.FirstOrDefault(h => h.Adults.Contains(id) || h.Children.Contains(id));

    /// <summary>The house a household lives in, or null.</summary>
    public CampBuilding? HomeOf(Household household) =>
        Campaign.Camp?.Buildings.FirstOrDefault(b => b.Household == household.Id);

    /// <summary>Someone's partner, or null.</summary>
    public Character? PartnerOf(CharacterId id) =>
        Campaign.Households.FirstOrDefault(h => h.Adults.Contains(id)) is { } home && home.Adults.FirstOrDefault(a => a != id) is { Value: > 0 } other
            ? Campaign.Characters[other]
            : null;

    // ================================================================ the day

    // At first light ashore: ages and the year, households, the coast's people coming, births.
    internal void StepPeopleDay(CampState camp)
    {
        var now = Campaign.Clock.Tick;
        var year = (Settling?.YearDays ?? 365) * SimulationClock.TicksPerDay;
        // A year older: those born here on their birthday, the rest each year from the day she sailed.
        foreach (var person in Campaign.Characters.Values.Where(c => c.SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated))
        {
            if (person.Born is { } born)
            {
                person.Age = (int)((now - born) / year);
            }
            else if (Campaign.Clock.Day > 0 && Campaign.Clock.Day % (Settling?.YearDays ?? 365) == 0)
            {
                person.Age++;
            }
        }

        if (Campaign.World is not { } world || Region is not { } region)
        {
            return;
        }

        Homes(camp);
        var random = Campaign.Randomness.Characters;
        var here = Ashore().Where(c => c.SurvivalState is SurvivalState.Alive && !Away(c.Id)).ToList();
        var single = here.Where(c => c.Age >= AdultFrom && !Campaign.Households.Any(h => h.Adults.Contains(c.Id))).ToList();

        // Two of the company make a home together.
        foreach (var a in single)
        {
            var b = single.Where(o => o.Id.Value > a.Id.Value && Affection(a.Id, o.Id) >= CoupleAffection && Affection(o.Id, a.Id) >= CoupleAffection)
                .OrderByDescending(o => Affection(a.Id, o.Id) + Affection(o.Id, a.Id)).FirstOrDefault();
            if (b is not null && random.Chance(CoupleChance))
            {
                Household(a, b, null);
                return;
            }
        }

        // One of a people of the coast comes to live with one of the company, by both sides' customs.
        if (CampDay >= 30)
        {
            foreach (var community in region.Communities.Where(c => world.Communities.GetValueOrDefault(c.Id) is { Met: true } s && s.Trust >= PartnerTrust))
            {
                if (single.FirstOrDefault(c => c.Membership == Membership.Crew && c.Came != community.Id) is { } one && random.Chance(PartnerChance))
                {
                    var partner = Newcomer(community, one.Sex == Sex.Male ? Sex.Female : Sex.Male, Between(random, 18, 30));
                    Household(one, partner, community.Id);
                    Customs(community, world, one, partner);
                    return;
                }
            }
        }

        // Someone of a people of the coast comes to live at the camp, curious, or to learn the newcomers' craft.
        if (CampDay >= JoinFromDay)
        {
            foreach (var community in region.Communities.Where(c => world.Communities.GetValueOrDefault(c.Id) is { Met: true } s && s.Trust >= JoinTrust))
            {
                if (Campaign.Characters.Values.Count(c => c.Came == community.Id) < JoinAtMost && random.Chance(JoinChance))
                {
                    var comer = Newcomer(community, random.Chance(0.5) ? Sex.Male : Sex.Female, Between(random, 16, 28));
                    Campaign.Record(ChronicleEntryKind.Joined, [new(ChronicleParameters.From, community.Id)], comer.Id);
                    News($"{comer.Name} of {community.Name} has come to live at the camp.");
                    return;
                }
            }
        }

        // Households: a child expected, a child born.
        foreach (var home in Campaign.Households.ToList())
        {
            var adults = home.Adults.Select(id => Campaign.Characters[id]).ToList();
            var mother = adults.FirstOrDefault(a => a.Sex == Sex.Female);
            if (home.Due is { } due && now >= due)
            {
                home.Due = null;
                if (mother is { SurvivalState: SurvivalState.Alive or SurvivalState.Incapacitated })
                {
                    Born(home, mother, region);
                }
            }
            else if (home.Due is null && mother is { SurvivalState: SurvivalState.Alive, Age: >= BearsFrom and <= BearsUntil }
                     && adults.Any(a => a.Sex == Sex.Male && a.SurvivalState == SurvivalState.Alive) && random.Chance(ExpectChance))
            {
                home.Due = now + CarryDays * SimulationClock.TicksPerDay;
                Campaign.Record(ChronicleEntryKind.Expecting, [], mother.Id);
            }
        }
    }

    private static int Between(SimulationRandom random, int low, int highExclusive) => low + random.NextInt(highExclusive - low);

    // Houses: one standing and empty goes to the household longest without one; a house whose household has
    // no one left alive is empty again. A home of their own eases them; making do in the huts tells on them.
    private void Homes(CampState camp)
    {
        bool Living(Household home) => home.Adults.Concat(home.Children).Any(id => Campaign.Characters[id].SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated);
        foreach (var house in camp.Buildings.Where(b => b.Household is { } id && (Campaign.Households.FirstOrDefault(h => h.Id == id) is not { } home || !Living(home))))
        {
            house.Household = null;
        }

        foreach (var house in camp.Buildings.Where(b => b.Household is null && Settling?.Building(b.Building) is { Home: true } && b.Condition > StandsAbove).ToList())
        {
            if (Campaign.Households.Where(h => Living(h) && HomeOf(h) is null).OrderBy(h => h.Formed).ThenBy(h => h.Id).FirstOrDefault() is not { } home)
            {
                break;
            }

            house.Household = home.Id;
            News($"{string.Join(" and ", home.Adults.Select(a => Campaign.Characters[a].Name))} have moved into a house of their own.");
        }

        foreach (var home in Campaign.Households.Where(Living))
        {
            var relief = HomeOf(home) is not null ? -HomeRelief : HomeWanted;
            foreach (var adult in home.Adults.Select(a => Campaign.Characters[a]).Where(a => a.SurvivalState == SurvivalState.Alive))
            {
                adult.Needs.Stress = Needs.Clamp(adult.Needs.Stress + relief);
            }
        }
    }

    // A day's ease for a household in a house of its own, and strain for one without.
    private const double HomeRelief = 1, HomeWanted = 0.5;

    /// <summary>Households living and without a house of their own.</summary>
    public int HouseholdsWithoutHomes => Campaign.Households.Count(h => HomeOf(h) is null
        && h.Adults.Any(a => Campaign.Characters[a].SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated));

    private int Affection(CharacterId from, CharacterId to) =>
        Campaign.Relationships.TryGetValue((from, to), out var relationship) ? relationship.Affection : 0;

    // ================================================================ households

    private Household Household(Character a, Character b, string? customs)
    {
        var home = new Household { Id = Campaign.NextHousehold++, Formed = Campaign.Clock.Tick, Customs = customs };
        home.AddAdult(a.Id);
        home.AddAdult(b.Id);
        Campaign.AddHousehold(home);
        Campaign.ChangeRelationship(a.Id, b.Id, RelationshipDimension.Affection, 10, "made a home together");
        Campaign.ChangeRelationship(b.Id, a.Id, RelationshipDimension.Affection, 10, "made a home together");
        Campaign.Record(ChronicleEntryKind.HouseholdFormed, [new(ChronicleParameters.From, customs)], a.Id, b.Id);
        return home;
    }

    // The customs of a people when one of theirs makes a home with a newcomer: a gift-giving people expects
    // gifts to the family, and takes it ill if there are none; with the rest, word goes home and trust grows.
    private void Customs(CommunityDefinition community, WorldState world, Character one, Character partner)
    {
        var state = world.Community(community.Id);
        if (!community.GiftCulture)
        {
            Trust(community.Id, 5);
            News($"{partner.Name} of {community.Name} has come to live with {one.Name}.");
            return;
        }

        foreach (var (item, count) in FamilyGifts)
        {
            if (Campaign.SharedStore.TryRemove(item, count))
            {
                state.Received[item] = state.Received.GetValueOrDefault(item) + count;
                Trust(community.Id, 6);
                News($"{partner.Name} of {community.Name} has come to live with {one.Name}. Gifts went to the family, {count} {ItemName(item).ToLowerInvariant()}, as their custom is.");
                return;
            }
        }

        Trust(community.Id, -4, 5);
        News($"{partner.Name} of {community.Name} has come to live with {one.Name}, but there was nothing to give the family, as their custom is. It is talked about at {community.Name}.");
    }

    // ================================================================ newcomers

    // Someone of a people of the coast, come to the camp: their language, the coast's skills.
    private Character Newcomer(CommunityDefinition community, Sex sex, int age)
    {
        var names = sex == Sex.Female ? community.FemaleNames : community.MaleNames;
        var person = NewPerson(FreshName(names, community.Name), sex, age, community.Name, community.Id);
        person.Languages.Set(community.Language, 4);
        person.Languages.Set("language.castrenan", 1);
        foreach (var (skill, level) in new[] { ("skill.fishing", 2 + Between(Campaign.Randomness.Characters, 0, 2)), ("skill.foraging", 2 + Between(Campaign.Randomness.Characters, 0, 2)), ("skill.tracking", 1), ("skill.crafting", 1) })
        {
            person.Skills.Set(skill, level);
        }

        return person;
    }

    // A person come to the company: made, given looks, set down at the camp, given the camp's duties.
    private Character NewPerson(string name, Sex sex, int age, string origin, string came)
    {
        var person = Campaign.CreateCharacter(name, age, origin, Content.Rules.PersonalCarryCapacity);
        person.Sex = sex;
        person.Membership = Membership.Crew;
        person.Came = came;
        person.Joined = Campaign.Clock.Tick;
        person.Appearance = AppearanceGenerator.Generate(person, Content.Appearance, Campaign.Randomness.Characters);
        var location = Location(CampLocation);
        Campaign.SetPlacement(person.Id, new CharacterPlacement(CampLocation, location.FindSpot("spot.hearth")?.Cell ?? location.Spots[0].Cell));
        AssignCampDuties();
        return person;
    }

    // A name from a list not already borne by someone of the company, else the first with a number.
    private string FreshName(IReadOnlyList<string> names, string fallback)
    {
        var taken = Campaign.Characters.Values.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var free = names.Where(n => !taken.Contains(n)).ToList();
        if (free.Count > 0)
        {
            return free[Between(Campaign.Randomness.Characters, 0, free.Count)];
        }

        var first = names.Count > 0 ? names[0] : fallback;
        return Enumerable.Range(2, 50).Select(i => $"{first} {ToRoman(i)}").First(n => !taken.Contains(n));
    }

    private static string ToRoman(int n) => n switch { 2 => "the Younger", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString(CultureInfo.InvariantCulture) };

    // A child born into a household: their mother's people's names, the household's language.
    private void Born(Household home, Character mother, RegionRules region)
    {
        var sex = Campaign.Randomness.Characters.Chance(0.5) ? Sex.Male : Sex.Female;
        var community = region.Communities.FirstOrDefault(c => c.Id == mother.Came) ?? region.Communities.FirstOrDefault(c => c.Id == home.Customs);
        var names = community is not null
            ? (sex == Sex.Female ? community.FemaleNames : community.MaleNames)
            : (sex == Sex.Female ? region.Charter.SettlerWomen : region.Charter.SettlerMen).Select(n => n.Split(' ')[0]).ToList();
        var child = NewPerson(FreshName(names, "child"), sex, 0, CampSite?.Name ?? "the camp", "born");
        child.Born = Campaign.Clock.Tick;
        foreach (var language in home.Adults.Select(a => Campaign.Characters[a].Languages).SelectMany(l => new[] { "language.castrenan", community?.Language }).OfType<string>().Distinct())
        {
            child.Languages.Set(language, 1);
        }

        home.AddChild(child.Id);
        Campaign.Record(ChronicleEntryKind.ChildBorn, [], child.Id, mother.Id);
        foreach (var person in Ashore())
        {
            person.Needs.Stress = Needs.Clamp(person.Needs.Stress - 4);
        }
    }

    // ================================================================ the relief ship

    // When the charter is judged: the settlers she brings if it is kept, or passage home for the worn out if not.
    internal void ReliefArrivals(WorldState world, RegionRules region)
    {
        var count = world.Charter switch { "kept" => region.Charter.SettlersKept, "in part" => region.Charter.SettlersInPart, _ => 0 };
        if (count == 0)
        {
            foreach (var weary in Ashore().Where(c => c.Id != Campaign.CaptainId && c.SurvivalState == SurvivalState.Alive && c.Needs.Stress >= 60 && !IsChild(c)).ToList())
            {
                Campaign.InterruptTask(weary.Id, InterruptReasons.Cancelled);
                Campaign.ChangeSurvivalState(weary.Id, SurvivalState.Missing, "went home");
                Campaign.RemovePlacement(weary.Id);
                Campaign.Record(ChronicleEntryKind.WentHome, [], weary.Id);
            }

            return;
        }

        // Settlers in pairs: husband and wife as the lists pair them, half of them families.
        var random = Campaign.Randomness.Characters;
        string[] trades = ["skill.carpentry", "skill.foraging", "skill.cooking", "skill.crafting", "skill.fishing", "skill.first_aid", "skill.hunting", "skill.sanitation"];
        var couples = count / 4;
        var made = new List<Character>();
        for (var i = 0; i < count; i++)
        {
            var woman = i % 2 == 1;
            var names = woman ? region.Charter.SettlerWomen : region.Charter.SettlerMen;
            var name = i / 2 < names.Count ? names[i / 2] : FreshName(names, "settler");
            var settler = NewPerson(name, woman ? Sex.Female : Sex.Male, Between(random, 20, 40), "Castrena", "relief");
            settler.StatusId = "status.craftsman";
            settler.Languages.Set("language.castrenan", 4);
            settler.Skills.Set(trades[i % trades.Length], 2 + Between(random, 0, 2));
            settler.Skills.Set(trades[(i + 3) % trades.Length], 1 + Between(random, 0, 2));
            Campaign.Record(ChronicleEntryKind.Joined, [new(ChronicleParameters.From, "relief")], settler.Id);
            made.Add(settler);
        }

        for (var pair = 0; pair < couples && pair * 2 + 1 < made.Count; pair++)
        {
            Household(made[pair * 2], made[pair * 2 + 1], null);
        }
    }

    // ================================================================ the Brennish

    // A hand of the interlopers' ship who would rather stay: if the camp is a place worth staying in.
    internal void BrennishStays(BrennishRules ship)
    {
        if (Community().Word is not ("Good" or "Steady") || !Campaign.Randomness.Events.Chance(0.5) || ship.Names.Count == 0)
        {
            return;
        }

        var hand = NewPerson(FreshName(ship.Names, "Brennish"), Sex.Male, Between(Campaign.Randomness.Characters, 20, 34), "Brenland", "brennish");
        hand.StatusId = "status.sailor";
        hand.Languages.Set("language.brennish", 4);
        hand.Languages.Set("language.castrenan", 1);
        foreach (var (skill, level) in new[] { ("skill.seamanship", 3), ("skill.sailing", 2), ("skill.rigging", 2), ("skill.carpentry", 1) })
        {
            hand.Skills.Set(skill, level);
        }

        Campaign.Record(ChronicleEntryKind.Joined, [new(ChronicleParameters.From, "brennish")], hand.Id);
        News($"{hand.Name} of {ship.Ship} would not sail with her: he has come over to the camp, and asks to stay.");
    }
}
