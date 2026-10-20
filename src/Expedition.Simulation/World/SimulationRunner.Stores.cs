using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Port;
using Expedition.Simulation.Tasks;

namespace Expedition.Simulation.World;

/// <summary>
/// The quality of stores (owner's request after R2e: "different sellers with quality"). Several people
/// on the waterfront sell stores, and what they sell is good, fair or poor. The quality is hidden: the
/// captain learns it by going to the seller and looking closely (his eye is his perception), from what
/// people say about them (leads), or at sea, when poor stores spoil and bad rations make the crew ill.
/// What quality means lives in the item: how much is lost each day at sea, and what it may give.
/// </summary>
public sealed partial class SimulationRunner
{
    /// <summary>How close the captain must stand to a seller to judge their goods, in cells.</summary>
    public const int InspectReach = 4;

    /// <summary>When at sea the stores are checked each day: at the morning's issue.</summary>
    private const int StoresHour = 6;

    /// <summary>Chance each hour of getting over an illness on one's own.</summary>
    private const double RecoveryPerHour = 1.0 / 60;

    /// <summary>Health an illness costs each hour it lasts.</summary>
    private const double IllnessHealthPerHour = 0.25;

    /// <summary>The seller's character, if they are in the port.</summary>
    public Character? SellerCharacter(PortSeller seller) => CharacterFor(seller.Person);

    /// <summary>Why the captain cannot look at a seller's goods now, or null if he can.</summary>
    public string? WhyCannotInspect(PortSeller seller)
    {
        if (Campaign.Port is null || Content.Port is null)
        {
            return "The ship has sailed.";
        }

        if (Campaign.CaptainId is not { } captain || !Campaign.Placements.TryGetValue(captain, out var here))
        {
            return "There is no captain to look.";
        }

        if (SellerCharacter(seller) is not { } person || !Campaign.Placements.TryGetValue(person.Id, out var there)
            || IsIndoors(person.Id) || there.LocationId != here.LocationId)
        {
            return $"{seller.Name} is not out on the waterfront now.";
        }

        var a = here.CellAt(Campaign.Clock.Tick);
        var b = there.CellAt(Campaign.Clock.Tick);
        return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) > InspectReach
            ? $"Walk over to {seller.Name} to look at the goods: you cannot judge them from here."
            : null;
    }

    /// <summary>
    /// The captain looks closely at everything a seller has: for each lot he has not yet judged, he
    /// sees what it is really like with a chance from his perception, or cannot tell. Once judged, a lot
    /// stays judged: a second look at the same casks shows nothing new.
    /// </summary>
    public (CommandResult Result, string Seen) Inspect(string sellerId)
    {
        if (Content.Port is not { } rules || Campaign.Port is not { } port || rules.Sellers.FirstOrDefault(s => s.Id == sellerId) is not { } seller)
        {
            return (CommandResult.Rejected("There is no such seller."), "");
        }

        if (WhyCannotInspect(seller) is { } why)
        {
            return (CommandResult.Rejected(why), "");
        }

        var eye = Campaign.Characters[Campaign.CaptainId!.Value].Attributes.Get("attribute.perception");
        var chance = Math.Clamp(0.4 + 0.12 * eye, 0.4, 0.95);
        var lines = new List<string>();
        for (var lot = 0; lot < rules.Goods.Count; lot++)
        {
            var goods = rules.Goods[lot];
            if (rules.SellerOf(goods) != seller)
            {
                continue;
            }

            if (!port.Inspected.ContainsKey(lot))
            {
                port.MarkInspected(lot, Campaign.Randomness.Stores.Chance(chance) ? goods.Quality : "unsure");
            }

            var seen = port.Inspected[lot];
            lines.Add(seen == "unsure"
                ? $"{ItemName(goods.Item)}: you turn it over and cannot tell."
                : $"{ItemName(goods.Item)}: {goods.Looks}");
        }

        return (CommandResult.Ok(), string.Join("\n", lines));
    }

    // Each morning at sea, stores that do not keep lose a share; the first loss of each is recorded,
    // which is how the captain finds out what he bought.
    private void StepStores()
    {
        if (Campaign.Clock.Tick % SimulationClock.TicksPerDay != StoresHour * SimulationClock.TicksPerHour)
        {
            return;
        }

        var store = Campaign.SharedStore;
        foreach (var item in Content.Items.Values.Where(i => i.SpoilsPerDay > 0).OrderBy(i => i.Id, StringComparer.Ordinal))
        {
            var count = store.Count(item.Id);
            if (count <= 0)
            {
                continue;
            }

            var expected = count * item.SpoilsPerDay;
            var lost = (int)Math.Floor(expected) + (Campaign.Randomness.Stores.Chance(expected - Math.Floor(expected)) ? 1 : 0);
            lost = Math.Min(lost, count);
            if (lost <= 0 || !store.TryRemove(item.Id, lost))
            {
                continue;
            }

            if (Campaign.NoteSpoilage(item.Id))
            {
                Campaign.Record(ChronicleEntryKind.StoresSpoiled, [new(ChronicleParameters.To, item.Id), new(ChronicleParameters.Level, lost.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
            }
        }
    }

    // A bad ration may make whoever took it ill.
    private void Sicken(Character character, Items.ItemDefinition ration)
    {
        if (ration.Sickens is not { } sickness || character.Health.Conditions.Contains(sickness.Condition)
            || !Campaign.Randomness.Stores.Chance(sickness.Chance))
        {
            return;
        }

        character.Health.AddCondition(sickness.Condition);
        LoseHealth(character, 6, sickness.Condition);
        Campaign.Record(ChronicleEntryKind.SickenedByRation,
            [new(ChronicleParameters.Condition, sickness.Condition), new(ChronicleParameters.To, ration.Id)], character.Id);
    }

    // An illness wears the sick down until it passes; most get over it in two or three days.
    private void StepIllness(Character character, List<TaskOutcome> outcomes)
    {
        var illnesses = character.Health.Conditions
            .Where(id => Content.Conditions.TryGetValue(id, out var condition) && condition.Kind == "illness")
            .Order(StringComparer.Ordinal).ToList();
        if (illnesses.Count == 0)
        {
            return;
        }

        LoseHealth(character, IllnessHealthPerHour * HoursPerTick * illnesses.Count, illnesses[0]);
        CheckHealthThresholds(character, illnesses[0], outcomes);
        if (Campaign.Clock.Tick % SimulationClock.TicksPerHour != 0 || character.SurvivalState == SurvivalState.Dead)
        {
            return;
        }

        foreach (var illness in illnesses)
        {
            if (Campaign.Randomness.Stores.Chance(RecoveryPerHour))
            {
                character.Health.RemoveCondition(illness);
                Campaign.Record(ChronicleEntryKind.Recovered, [new(ChronicleParameters.Condition, illness)], character.Id);
            }
        }
    }

    /// <summary>
    /// What a lot costs now: its price, more as the convoy's sailing nears (every ship buying at once)
    /// and as the seller's stock runs low, less for a captain the port knows (standing).
    /// </summary>
    public int PriceOf(int lot)
    {
        if (Content.Port is not { } rules || Campaign.Port is not { } port || lot < 0 || lot >= rules.Goods.Count)
        {
            return 0;
        }

        var goods = rules.Goods[lot];
        return (int)Math.Round(goods.Price * PriceFactor(lot, rules, port), MidpointRounding.AwayFromZero);
    }

    /// <summary>How far a lot's price is above (or below) its usual price: 1.2 is a fifth more.</summary>
    public double PriceFactor(int lot) =>
        Content.Port is { } rules && Campaign.Port is { } port && lot >= 0 && lot < rules.Goods.Count ? PriceFactor(lot, rules, port) : 1;

    private double PriceFactor(int lot, PortRules rules, PortState port)
    {
        var prices = rules.Prices;
        var goods = rules.Goods[lot];
        var hoursLeft = (rules.Departure.Tick - Campaign.Clock.Tick) / (double)SimulationClock.TicksPerHour;
        var rush = Math.Clamp(1 - hoursLeft / Math.Max(prices.RiseHours, 1), 0, 1);
        rush = rush * rush * (3 - 2 * rush);
        var left = goods.Stock <= 0 ? 0 : 1 - Math.Clamp(port.Stock.GetValueOrDefault(lot) / (double)goods.Stock, 0, 1);
        var standing = Math.Clamp(Standing, 0, prices.StandingDiscount.Count - 1);
        var discount = prices.StandingDiscount.Count > 0 ? prices.StandingDiscount[standing] : 0;
        return (1 + prices.Rise * rush) * (1 + prices.Scarcity * Math.Pow(left, 1.5)) * (1 - discount);
    }

    /// <summary>How much credit a seller extends to the expedition at its standing (0 if none).</summary>
    public int CreditLimit(PortSeller seller)
    {
        if (!seller.Credit || Content.Port is not { } rules || rules.Prices.CreditLimit.Count == 0)
        {
            return 0;
        }

        return rules.Prices.CreditLimit[Math.Clamp(Standing, 0, rules.Prices.CreditLimit.Count - 1)];
    }

    /// <summary>Credit left with a seller: the limit less what is owed.</summary>
    public int CreditLeft(PortSeller seller) => Math.Max(0, CreditLimit(seller) - (Campaign.Port?.Owed.GetValueOrDefault(seller.Id) ?? 0));

    /// <summary>Pays a seller what the expedition owes them.</summary>
    public CommandResult PayDebt(string sellerId)
    {
        if (Campaign.Port is not { } port || Content.Port is not { } rules || !port.Owed.TryGetValue(sellerId, out var owed))
        {
            return CommandResult.Rejected("Nothing is owed.");
        }

        if (port.Funds < owed)
        {
            return CommandResult.Rejected($"That is {owed} sueldos and only {port.Funds} are left.");
        }

        port.Funds -= owed;
        port.ClearDebt(sellerId);
        if (rules.Sellers.FirstOrDefault(s => s.Id == sellerId) is { } seller && SellerCharacter(seller) is { } person && Campaign.CaptainId is { } captain)
        {
            Campaign.ChangeRelationship(person.Id, captain, Characters.RelationshipDimension.Trust, 5, "settled the Garza's account");
        }

        return CommandResult.Ok();
    }

    // What the Garza still owes sellers, as claims at the gangway (the debtor is the expedition itself).
    private IEnumerable<(CharacterId Creditor, CreditorClaim Claim)> StoreDebts(PortRules rules, PortState port)
    {
        foreach (var (sellerId, owed) in port.Owed.OrderBy(o => o.Key, StringComparer.Ordinal))
        {
            if (owed > 0 && rules.Sellers.FirstOrDefault(s => s.Id == sellerId) is { } seller && SellerCharacter(seller) is { } person
                && !port.Claims.ContainsKey(person.Id))
            {
                var amount = owed.ToString(System.Globalization.CultureInfo.InvariantCulture);
                yield return (person.Id, new CreditorClaim
                {
                    Debtor = "",
                    Amount = owed,
                    Says = seller.AtGangway.Replace("{amount}", amount, StringComparison.Ordinal),
                    AtGangway = seller.AtGangway.Replace("{amount}", amount, StringComparison.Ordinal),
                });
            }
        }
    }
}
