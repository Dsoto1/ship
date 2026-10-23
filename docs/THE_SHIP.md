# The ship after landfall, her guns, and the privateers

**Spec:** ECONOMY_LOOP §22 ("Successful landfall also has a cost") and §56. This is step S of ECONOMY_AUDIT.
**Status:** first pass, 2026-10-23. The simulation is in `World/SimulationRunner.Ship.cs`. The Camp drawer
has a card for the ship.

## Who stays aboard

At anchor the ship is worth keeping, and keeping her costs hands (§22). The Camp drawer's **The ship** card
lists the company: click someone to send them aboard, click again to bring them ashore. The boat takes them
out and brings them back.

**The ship's company (those aboard):**
- They do none of the camp's work: like a party away, they are off the camp map.
- **Food and drink:** they live off what is still aboard: each morning they take up to three days' food and
  drink from what has not been landed, and the boat brings the rest from the camp's stores.
- **Upkeep:** with two or more aboard, her hull wears at 0.1% a day; left alone, at 0.35% a day, fouled
  with weed and worm. A hand aboard with ship repair pays a seam every other day while there is oakum
  (+1%).
- **Storms:** with two or more aboard, a storm wears her half as much, because they veer cable and lay out
  a second anchor. With no one aboard, each storm hour has a small chance she drags ashore and breaks up.
- **Privateers:** with four or more aboard, they hold her against boarders.

## Using her

With four hands aboard she can be taken out (§56: "ship supports fishing/trade/exploration"):

| Errand | Takes | What comes of it |
|---|---|---|
| **Fish at sea** | A day | Fish for the stores: 3 a hand, and 2 more for each level of fishing. Hull −0.5%. |
| **Along the coast** | Four days | Every stretch of shore, headland, cove, landing and river mouth not yet seen is seen from the sea. Hull −1.5%. |

She does not go:
- with her hull under 40%;
- with a storm coming;
- with a privateer at the anchorage.

While she is out, her company cannot change.

Trading by sea is not yet in.

## The boat shed and slip

A boat shed and slip at the water's edge lets the ship be careened: hove down on the slip at the tide, her
bottom scraped and her seams paid.
- **Careen the ship:** four hours of ship repair, 2 oakum and a plank, +10% hull.
- **When:** it is building work, taken up whenever she is in with her hull under 90%.

## The guns

The Garza carries six falconets. Powder (by the charge) and round shot are cargo: the Bocana chandler sells
both, about ten for 70 and ten for 40 sueldos.
- **Gun crews:** three hands to a gun. The gun crews are the best seamen, who have worked the guns in drill.
- **Aboard:** a gun is fought with a crew from the ship's company.
- **Ashore:** a **gun platform** mounts two guns landed from the ship ("Land guns"), fought by hands ashore.
- **Powder takes the damp:**

  | Where | Lost a day |
  |---|---|
  | In the open | 1% (3% in the wet) |
  | Aboard, in her powder room | 0.2% |
  | In a **powder magazine** | 0.1% |

## Privateers

The Gannet, under Captain Rook Daley's Brennish letter of marque (or so he says), comes about day 110, and
again every 150 days. She is met by decision, not fought gun by gun:
- **Warning:** with a **lookout** standing, her sail is seen a day before she anchors.
- **Without one:** she is at the anchorage when she is seen.

When she sends her boat in, the captain chooses:

| Choice | What it costs | What happens |
|---|---|---|
| **Fight her off** | Two charges of powder and two shot for each gun fought | A chance to drive her off, shown before choosing: more guns, better seamen at them, guns on the platform (+15%) and the lookout's warning (+10%), from 5% to 90%. Driven off, the company is the prouder for it. Beaten, she plunders as below, three tenths of it. Someone may be wounded either way. |
| **Pay her** | 200 sueldos | She goes. |
| **Pay in trade goods** | Ten of the cloth and knives | She goes. |
| **Hide the stores in the forest** | — | She takes a share of the food, trade goods, materials, munitions and purse: a tenth with the lookout's warning, over a third without. |

In any case:
- **The ship:** if fewer than four are aboard and she is not driven off or paid, the privateer cuts out the
  ship, with all still in her.
- **The chronicle** records the meeting and its outcome, and she comes again 150 days on.

## Saves

Format 46 keeps:
- the ship's company;
- the guns aboard and ashore;
- her errand;
- the privateer's coming.

An older camp has no one aboard, and an anchored ship has all six guns.

## Tests

`ShipTests` (8):
- the company keeps her, lives off what is aboard, and comes ashore again;
- with four aboard she fishes and coasts;
- she is careened at the slip;
- powder keeps dry in a magazine;
- guns landed on the platform are fought from the shore;
- a privateer is seen from the lookout, met by decision, and comes again;
- with too few aboard a privateer cuts out the ship;
- all of it saved and loaded alike.

## Not yet

- **Trading by sea:** taking her to Kessat's river landing or the Brennish cove with goods.
- **A second anchor and cable** as gear of their own.
- **Gunnery as a skill of its own:** seamanship stands in for it.
- **Taking her home**, or to another post.
