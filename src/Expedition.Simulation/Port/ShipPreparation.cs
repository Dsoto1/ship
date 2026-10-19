using Expedition.Simulation.Campaign;
using Expedition.Simulation.Content;
using Expedition.Simulation.World;

namespace Expedition.Simulation.Port;

/// <summary>How far the ship's preparation has come (PORT_EXPERIENCE §5), for what she looks like at her berth.</summary>
public enum PreparationStage
{
    /// <summary>Nothing done yet: a sparse deck, the Company's stores, no crew.</summary>
    AwaitingExpedition,

    /// <summary>The first hands are aboard, looking her over.</summary>
    InspectionUnderway,

    /// <summary>Stores bought at the chandler are coming aboard.</summary>
    InitialLoading,

    /// <summary>Much of the crew signed on and the stores stowing.</summary>
    TakingShape,

    /// <summary>The minimum crew and the stores aboard: cargo secured, ready for sea.</summary>
    DepartureReady,

    /// <summary>The last hours before the convoy sails: last cargo, farewells at the gangway.</summary>
    DepartureMorning,
}

/// <summary>A stack of one item as it looks: so many casks of water, sacks of biscuit, coils of rope.</summary>
/// <param name="Item">Item ID.</param>
/// <param name="Package">What it is packed in (see <see cref="Items.ItemDefinition.Packages"/>).</param>
/// <param name="Count">How many packages.</param>
public sealed record CargoPackages(string Item, string Package, int Count)
{
}

/// <summary>
/// What the ship's preparation looks like at a moment: the stage, the stores standing on the quay waiting
/// to go aboard, the stores aboard, and how many hands have brought their belongings. Derived from the
/// simulation and never stored, so the picture can never disagree with the hold (PORT_EXPERIENCE §5, §18).
/// </summary>
/// <param name="Stage">How far preparation has come.</param>
/// <param name="OnQuay">Bought lots still on the quay by the gangway.</param>
/// <param name="Aboard">The hold's contents, less what is still on the quay.</param>
/// <param name="Belongings">Hands signed on, each with a sea chest or a bundle.</param>
public sealed record ShipPreparation(PreparationStage Stage, IReadOnlyList<CargoPackages> OnQuay, IReadOnlyList<CargoPackages> Aboard, int Belongings)
{
    /// <summary>
    /// The ship's preparation in port, or null at sea. Stores bought at the chandler stand on the quay and
    /// go aboard steadily over <see cref="PreparationRules.LoadingHours"/>; the hold's quantities are
    /// already counted, so the picture is the only thing that waits.
    /// </summary>
    public static ShipPreparation? Of(CampaignState campaign, GameContent content)
    {
        if (content.Port is not { } rules || campaign.Port is not { } port)
        {
            return null;
        }

        var now = campaign.Clock.Tick;
        var loading = rules.Preparation.LoadingHours * SimulationClock.TicksPerHour;
        var onQuay = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var purchase in port.Purchases)
        {
            var waited = now - purchase.Tick;
            if (waited < 0 || waited >= loading)
            {
                continue;
            }

            var goods = rules.Goods[purchase.Lot];
            var packages = PackagesOf(content, goods.Item, goods.Quantity);
            var left = (int)Math.Ceiling(packages * (1 - waited / loading));
            onQuay[goods.Item] = onQuay.GetValueOrDefault(goods.Item) + left;
        }

        var aboard = campaign.SharedStore.Quantities
            .Where(pair => pair.Value > 0 && content.Items.ContainsKey(pair.Key))
            .Select(pair => (Item: pair.Key, Count: PackagesOf(content, pair.Key, pair.Value) - onQuay.GetValueOrDefault(pair.Key)))
            .Where(stack => stack.Count > 0);

        return new ShipPreparation(StageOf(campaign, rules), Stacks(content, onQuay.Select(p => (p.Key, p.Value))), Stacks(content, aboard), port.Hired.Count);
    }

    /// <summary>The stage, from the crew signed on, the lots bought and the hour (PORT_EXPERIENCE §5).</summary>
    public static PreparationStage StageOf(CampaignState campaign, PortRules rules)
    {
        if (campaign.Port is not { } port)
        {
            return PreparationStage.DepartureMorning;
        }

        var hired = port.Hired.Count;
        var bought = port.Purchases.Count;
        var thresholds = rules.Preparation;
        if (campaign.Clock.Tick >= rules.Departure.Tick - thresholds.DepartureMorningHours * SimulationClock.TicksPerHour)
        {
            return PreparationStage.DepartureMorning;
        }

        if (hired >= rules.MinimumCrew && bought >= thresholds.ReadyLots)
        {
            return PreparationStage.DepartureReady;
        }

        if (hired >= rules.MinimumCrew || (hired * 2 >= rules.MinimumCrew && bought >= thresholds.TakingShapeLots))
        {
            return PreparationStage.TakingShape;
        }

        return bought > 0 ? PreparationStage.InitialLoading
            : hired > 0 ? PreparationStage.InspectionUnderway
            : PreparationStage.AwaitingExpedition;
    }

    /// <summary>The stage in a line, for the Expedition panel.</summary>
    public static string Describe(PreparationStage stage) => stage switch
    {
        PreparationStage.AwaitingExpedition => "Awaiting the expedition: the Company's stores aboard, no crew yet.",
        PreparationStage.InspectionUnderway => "Inspection under way: the first hands are looking her over.",
        PreparationStage.InitialLoading => "Loading: stores from the chandler are coming aboard.",
        PreparationStage.TakingShape => "Taking shape: hands aboard with their belongings, the stores stowing.",
        PreparationStage.DepartureReady => "Ready for sea: cargo secured, crew assembled.",
        _ => "Departure morning: the last cargo and the farewells at the gangway.",
    };

    private static int PackagesOf(GameContent content, string item, int units) =>
        (int)Math.Ceiling(units / (double)content.Items[item].PerPackage);

    private static List<CargoPackages> Stacks(GameContent content, IEnumerable<(string Item, int Count)> stacks) =>
        stacks.Where(s => s.Count > 0)
            .OrderBy(s => s.Item, StringComparer.Ordinal)
            .Select(s => new CargoPackages(s.Item, content.Items[s.Item].PackedIn, s.Count))
            .ToList();
}
