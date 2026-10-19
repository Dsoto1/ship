using Expedition.Simulation.Port;
using Godot;

namespace Expedition.Game;

/// <summary>
/// What the Garza's preparation looks like (PORT_EXPERIENCE §5, R2c), drawn from
/// <see cref="ShipPreparation"/> so it can never disagree with the simulation. Stores bought at the
/// chandler stand on the quay by the gangway, cross to her deck, and are struck down into the hold;
/// each stack is so many casks, sacks, chests or coils, not the exact quantity. The stage dresses her:
/// <list type="bullet">
/// <item>awaiting: a sparse deck, a tar pot and a loose coil from the maintenance;</item>
/// <item>inspection: the main hatch open, a lantern lowered to look at the hold;</item>
/// <item>loading and taking shape: a whip rigged from the main yard over the open hatch;</item>
/// <item>taking shape on: a sea chest or a bundle for each hand signed on;</item>
/// <item>ready and departure morning: the hatch closed, the deck load under a lashed tarpaulin;</item>
/// <item>departure morning: people on the quay to see her off.</item>
/// </list>
/// The deck parts are in the ship's own coordinates (the deck map: x across, row from the bow).
/// </summary>
public partial class PreparationView : Node3D
{
    private static readonly Color CaskWood = new(0.5f, 0.35f, 0.2f);
    private static readonly Color Hoop = new(0.22f, 0.2f, 0.18f);
    private static readonly Color Sacking = new(0.66f, 0.58f, 0.43f);
    private static readonly Color ChestWood = new(0.34f, 0.23f, 0.15f);
    private static readonly Color CrateWood = new(0.62f, 0.5f, 0.34f);
    private static readonly Color Rope = new(0.55f, 0.47f, 0.33f);
    private static readonly Color Tarpaulin = new(0.42f, 0.38f, 0.3f);
    private static readonly Color Bale = new(0.78f, 0.73f, 0.62f);
    private static readonly Color Wicker = new(0.6f, 0.48f, 0.3f);
    private static readonly Color Dark = new(0.06f, 0.05f, 0.04f);

    // Places for stacks on the quay beside the gangway, clear of the spots and bollards (world cells).
    private static readonly Vector2[] QuaySlots =
    [
        new(35.0f, 33.1f), new(35.8f, 33.1f), new(36.6f, 33.1f), new(37.4f, 33.1f),
        new(35.0f, 32.3f), new(35.8f, 32.3f), new(36.6f, 32.3f),
    ];

    // On deck round the main hatch and along the bulwarks, clear of the masts, pumps and work spots.
    private static readonly Vector2[] DeckSlots =
    [
        new(1.9f, 9.1f), new(2.8f, 9.1f), new(3.7f, 9.0f), new(1.9f, 12.4f), new(2.8f, 12.4f),
        new(6.5f, 8.2f), new(6.5f, 9.0f), new(6.5f, 11.4f), new(6.5f, 12.2f), new(1.2f, 8.2f), new(1.2f, 7.4f), new(6.5f, 7.4f),
    ];

    // Sea chests and bundles: forward under the forecastle, and aft.
    private static readonly Vector2[] BelongingSlots =
    [
        new(2.2f, 3.4f), new(5.8f, 3.4f), new(3.0f, 3.2f), new(5.0f, 3.2f),
        new(2.0f, 4.3f), new(6.0f, 4.3f), new(1.6f, 17.6f), new(6.4f, 17.6f),
    ];

    private static readonly Vector2 Hatch = new(2.5f, 10.5f);

    private Node3D _deck = null!;
    private Node3D _quay = null!;
    private string _shown = "";

    /// <summary>The stage last drawn, for the panel and the farewells.</summary>
    public PreparationStage? Stage { get; private set; }

    /// <summary>Puts the deck parts under the ship and the quay parts on the waterfront.</summary>
    public void Attach(Node3D ship, Node3D waterfront)
    {
        _deck = new Node3D { Name = "Preparation" };
        ship.AddChild(_deck);
        _quay = new Node3D { Name = "QuayStores" };
        waterfront.AddChild(_quay);
    }

    /// <summary>Redraws if the preparation has changed since the last call.</summary>
    /// <param name="raining">In rain the stores waiting on the quay are under a tarpaulin.</param>
    public void Show(ShipPreparation? preparation, bool raining = false)
    {
        var signature = preparation is null ? "" : $"{preparation.Stage}|{Stacks(preparation.OnQuay)}|{Stacks(preparation.OnDeck)}|{preparation.Belongings}|{raining}";
        if (signature == _shown)
        {
            return;
        }

        _shown = signature;
        Stage = preparation?.Stage;
        foreach (var child in _deck.GetChildren().Concat(_quay.GetChildren()))
        {
            child.QueueFree();
        }

        if (preparation is null)
        {
            return;
        }

        var stage = preparation.Stage;
        var quay = new MeshBatcher();
        Lay(quay, Packages(preparation.OnQuay), QuaySlots, 0f, covered: raining);
        quay.Commit(_quay);

        var deck = new MeshBatcher();
        var covered = stage is PreparationStage.DepartureReady or PreparationStage.DepartureMorning || raining;
        var hatchOpen = stage is PreparationStage.InspectionUnderway or PreparationStage.InitialLoading or PreparationStage.TakingShape
            || preparation.OnDeck.Count > 0 && !covered;
        // Ready for sea, the deck load is lashed under tarpaulins: what is still being stowed, or else a
        // few casks of water kept on deck as ships did.
        var deckLoad = Packages(preparation.OnDeck);
        if (covered && deckLoad.Count == 0)
        {
            deckLoad = Packages(preparation.Aboard.Where(s => s.Package == "cask").ToList()).Take(4).ToList();
        }

        Lay(deck, deckLoad, DeckSlots, 0f, covered);

        if (stage == PreparationStage.AwaitingExpedition)
        {
            // The ship-keeper's work: a tar pot with its brush, a coil left out, a bucket.
            Shape(deck, new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.15f, Height = 0.3f, RadialSegments = 8, Rings = 1 }, new Vector3(6.4f, 0.15f, 5.1f), Dark);
            Shape(deck, new BoxMesh { Size = new Vector3(0.04f, 0.5f, 0.04f) }, new Vector3(6.45f, 0.4f, 5.1f), CaskWood, new Vector3(0, 0, 0.4f));
            Shape(deck, new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.13f, Height = 0.28f, RadialSegments = 8, Rings = 1 }, new Vector3(6.3f, 0.14f, 5.7f), CaskWood);
            Coil(deck, new Vector3(1.4f, 0f, 6.0f), 1);
        }

        if (hatchOpen)
        {
            // The hatch gratings lifted off and stacked aside, the hold dark below.
            Shape(deck, new BoxMesh { Size = new Vector3(1.75f, 0.02f, 1.75f) }, new Vector3(Hatch.X, 0.3f, Hatch.Y), Dark);
            Shape(deck, new BoxMesh { Size = new Vector3(0.86f, 0.06f, 0.86f) }, new Vector3(3.9f, 0.03f, 11.6f), CaskWood.Darkened(0.3f));
            Shape(deck, new BoxMesh { Size = new Vector3(0.86f, 0.06f, 0.86f) }, new Vector3(3.92f, 0.09f, 11.58f), CaskWood.Darkened(0.35f), new Vector3(0, 0.1f, 0));
            foreach (var x in new[] { 2.2f, 2.6f })
            {
                Shape(deck, new BoxMesh { Size = new Vector3(0.05f, 1.0f, 0.05f) }, new Vector3(x, 0.55f, Hatch.Y - 0.6f), CaskWood, new Vector3(0.5f, 0, 0));
            }
        }

        if (stage == PreparationStage.InspectionUnderway)
        {
            Shape(deck, new BoxMesh { Size = new Vector3(0.2f, 0.28f, 0.2f) }, new Vector3(3.3f, 0.14f, 9.6f), new Color(0.85f, 0.7f, 0.4f));
        }

        var loading = preparation.OnQuay.Count > 0 || stage is PreparationStage.InitialLoading or PreparationStage.TakingShape && preparation.OnDeck.Count > 0;
        if (loading)
        {
            // A whip from the main yard over the hatch, a sling of stores on its hook.
            var yard = new Vector3(Hatch.X, 7.9f, 13f);
            var hook = new Vector3(Hatch.X, 2.3f, Hatch.Y);
            Line(deck, yard, hook);
            Line(deck, yard, new Vector3(5.6f, 0.9f, 13.6f));
            Shape(deck, new BoxMesh { Size = new Vector3(0.18f, 0.28f, 0.12f) }, yard - new Vector3(0, 0.2f, 0), Hoop);
            Shape(deck, new SphereMesh { Radius = 0.42f, Height = 0.6f, RadialSegments = 8, Rings = 4 }, hook - new Vector3(0, 0.35f, 0), Sacking.Darkened(0.12f));
        }

        if (stage >= PreparationStage.TakingShape)
        {
            for (var i = 0; i < Math.Min(preparation.Belongings, BelongingSlots.Length); i++)
            {
                var at = BelongingSlots[i];
                if (i % 3 == 2)
                {
                    // A seaman's bundle: clothes rolled in a blanket.
                    Shape(deck, new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.16f, Height = 0.6f, RadialSegments = 7, Rings = 1 }, new Vector3(at.X, 0.16f, at.Y), Bale.Darkened(0.25f), new Vector3(0, 0, Mathf.Pi / 2f));
                }
                else
                {
                    Shape(deck, new BoxMesh { Size = new Vector3(0.75f, 0.42f, 0.45f) }, new Vector3(at.X, 0.21f, at.Y), ChestWood.Lightened(0.05f * (i % 3)));
                    Shape(deck, new BoxMesh { Size = new Vector3(0.78f, 0.05f, 0.48f) }, new Vector3(at.X, 0.44f, at.Y), ChestWood.Darkened(0.2f));
                }
            }
        }

        if (covered)
        {
            // Everything secured: coils flemished down at the mast feet.
            Coil(deck, new Vector3(3.2f, 0f, 6.8f), 1);
            Coil(deck, new Vector3(4.9f, 0f, 13.8f), 1);
        }

        deck.Commit(_deck);
    }

    private static string Stacks(IReadOnlyList<CargoPackages> stacks) => string.Join(",", stacks.Select(s => $"{s.Package}{s.Count}"));

    // One package kind per entry, like kinds together, with a cap so a full hold never floods the deck.
    private static List<string> Packages(IReadOnlyList<CargoPackages> stacks) =>
        stacks.OrderBy(s => s.Package, StringComparer.Ordinal).SelectMany(s => Enumerable.Repeat(s.Package, Math.Min(s.Count, 12))).Take(30).ToList();

    private static int MostHigh(string package) => package switch
    {
        "cask" => 1,
        "barrel" => 2,
        "coil" => 4,
        "plank" => 6,
        _ => 3,
    };

    // Stacks packages of a kind on the slots in turn, each slot as high as that kind is stacked.
    private void Lay(MeshBatcher batch, List<string> packages, Vector2[] slots, float baseHeight, bool covered)
    {
        var slot = 0;
        var height = 0f;
        var count = 0;
        string? kind = null;
        var tops = new Dictionary<int, float>();
        foreach (var package in packages)
        {
            if (kind != package || count >= MostHigh(package))
            {
                if (kind is not null)
                {
                    slot++;
                }

                kind = package;
                count = 0;
                height = 0;
            }

            if (slot >= slots.Length)
            {
                break;
            }

            var at = slots[slot];
            var turn = (slot * 37 + count * 11) % 7 * 0.12f;
            height += Package(batch, package, new Vector3(at.X, baseHeight + height, at.Y), turn);
            tops[slot] = height;
            count++;
        }

        if (!covered)
        {
            return;
        }

        foreach (var (index, top) in tops)
        {
            var at = slots[index];
            Shape(batch, new BoxMesh { Size = new Vector3(0.86f, top + 0.04f, 0.86f) }, new Vector3(at.X, baseHeight + (top + 0.04f) / 2f, at.Y), Tarpaulin);
            Line(batch, new Vector3(at.X - 0.5f, baseHeight + 0.02f, at.Y), new Vector3(at.X + 0.5f, baseHeight + top + 0.06f, at.Y));
        }
    }

    // Draws one package standing at the given point; returns its height for stacking.
    private float Package(MeshBatcher batch, string package, Vector3 at, float turn)
    {
        var yaw = new Vector3(0, turn, 0);
        switch (package)
        {
            case "cask":
            case "barrel":
                var r = package == "cask" ? 0.3f : 0.23f;
                var h = package == "cask" ? 0.78f : 0.58f;
                Shape(batch, new CylinderMesh { TopRadius = r * 0.88f, BottomRadius = r * 0.88f, Height = h, RadialSegments = 9, Rings = 1 }, at + new Vector3(0, h / 2f, 0), CaskWood.Lightened((turn * 3f) % 0.1f));
                Shape(batch, new CylinderMesh { TopRadius = r, BottomRadius = r, Height = h * 0.4f, RadialSegments = 9, Rings = 1 }, at + new Vector3(0, h / 2f, 0), CaskWood.Darkened(0.06f));
                foreach (var y in new[] { 0.12f, 0.88f })
                {
                    Shape(batch, new CylinderMesh { TopRadius = r * 0.92f, BottomRadius = r * 0.92f, Height = 0.04f, RadialSegments = 9, Rings = 1 }, at + new Vector3(0, h * y, 0), Hoop);
                }

                return h;
            case "sack":
                Shape(batch, new SphereMesh { Radius = 0.3f, Height = 0.36f, RadialSegments = 8, Rings = 4 }, at + new Vector3(0, 0.17f, 0), Sacking, yaw, new Vector3(1.3f, 1f, 1f));
                return 0.3f;
            case "chest":
                Shape(batch, new BoxMesh { Size = new Vector3(0.7f, 0.42f, 0.45f) }, at + new Vector3(0, 0.21f, 0), ChestWood, yaw);
                Shape(batch, new BoxMesh { Size = new Vector3(0.72f, 0.04f, 0.47f) }, at + new Vector3(0, 0.43f, 0), Hoop, yaw);
                return 0.45f;
            case "coil":
                Coil(batch, at, 1);
                return 0.12f;
            case "bale":
                Shape(batch, new BoxMesh { Size = new Vector3(0.7f, 0.38f, 0.5f) }, at + new Vector3(0, 0.19f, 0), Bale, yaw);
                Shape(batch, new BoxMesh { Size = new Vector3(0.04f, 0.4f, 0.52f) }, at + new Vector3(0.18f, 0.19f, 0), Rope, yaw);
                return 0.38f;
            case "plank":
                Shape(batch, new BoxMesh { Size = new Vector3(0.3f, 0.07f, 2.4f) }, at + new Vector3(0, 0.035f, 0), CrateWood.Lightened(0.08f), yaw * 0.2f);
                return 0.07f;
            case "basket":
                Shape(batch, new CylinderMesh { TopRadius = 0.27f, BottomRadius = 0.22f, Height = 0.3f, RadialSegments = 9, Rings = 1 }, at + new Vector3(0, 0.15f, 0), Wicker);
                return 0.3f;
            default:
                Shape(batch, new BoxMesh { Size = new Vector3(0.6f, 0.52f, 0.6f) }, at + new Vector3(0, 0.26f, 0), CrateWood, yaw);
                Shape(batch, new BoxMesh { Size = new Vector3(0.62f, 0.06f, 0.62f) }, at + new Vector3(0, 0.4f, 0), CrateWood.Darkened(0.2f), yaw);
                return 0.52f;
        }
    }

    private static void Coil(MeshBatcher batch, Vector3 at, int layers)
    {
        for (var i = 0; i < layers; i++)
        {
            Shape(batch, new TorusMesh { InnerRadius = 0.12f, OuterRadius = 0.32f, Rings = 10, RingSegments = 5 }, at + new Vector3(0, 0.05f + i * 0.09f, 0), Rope);
        }
    }

    private static void Shape(MeshBatcher batch, Mesh mesh, Vector3 at, Color colour, Vector3? rotation = null, Vector3? scale = null) =>
        batch.AddColoured(mesh, new Transform3D(Basis.FromEuler(rotation ?? Vector3.Zero).Scaled(scale ?? Vector3.One), at), colour);

    private static void Line(MeshBatcher batch, Vector3 from, Vector3 to)
    {
        var direction = to - from;
        var length = direction.Length();
        var mesh = new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.018f, Height = length, RadialSegments = 4, Rings = 1 };
        batch.AddColoured(mesh, new Transform3D(new Basis(new Quaternion(Vector3.Up, direction / length)), (from + to) / 2f), Rope.Darkened(0.3f));
    }
}
