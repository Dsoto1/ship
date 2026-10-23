# Structures: what the camp can build, and what it still lacks

**Status:** 2026-10-23. The house and the smokehouse are new. The rest were built in R7, step 2 and R8.
Buildings are data (`data/shore.json`, `settlement.buildings`), and each one's effect comes from a flag.

## What there is

| Building | Flag | What it does |
|---|---|---|
| Hut | `berths: 4` | Dry berths for four, for good. |
| **House** (new) | `berths: 4, home: true` | A household's own home. See below. |
| Storehouse | `stores` | Keeps the stores off the ground and dry: they spoil far slower. |
| Cookhouse | `cooks` | A roofed hearth: the fire never goes out, and the food is hot. |
| Infirmary | `sickBerths: 6` | The sick laid apart and tended. |
| Workshop | `mends` | Broken tools mended at a bench; planks sawn over the pit. |
| **Smokehouse** (new) | `smokes: true` | Fresh fish smoked, to keep. See below. |
| Garden | `beds` | Beds sown from the seed chest. |
| Well | `water` | Water at the camp, where the site has it. |
| Landing | `landing` | The boat lands half as much again each trip. |

The palisade and the latrine are camp works, not placed buildings.

### The house

People came, and couples made households (step P). A hut sleeps whoever is in it, but a household wants a
home of its own:
- **Cost:** a little more than a hut: more framing, and fitted out (floors, a door).
- **Who lives there:** a house standing empty goes to the household that has waited longest. When no one
  of a household is left alive, its house is empty again.
- **Effect:** a household with its own house eases a little each day. One without strains a little.
- **The community's condition** names it either way: "a household with no house of its own" (−), or "every
  household in a house of its own" (+).
- **Sleeping:** the household sleeps there, and walks to the fire and back from its own door.
- **Berths:** its four berths count as dry berths, like a hut's, for "homes for all".

### The smokehouse

Fresh fish spoils at 30% a day; smoked fish at 1%. Mahenu and Oruwai trade smoked fish, but until now the
camp could not make its own:
- **Size:** small and quick to raise: racks over a slow fire, under a low roof.
- **The work:** "Smoke fish" turns ten fish into ten smoked fish in three hours, at the smokehouse. It is
  a cooking job, one batch at a time.
- **When:** it is among the camp's Food duties, and is taken up whenever there are ten fish in the stores.
  It makes the reserves (step C) something the camp can lay by.

## What could still be built, and when

| Structure | What it would do | When |
|---|---|---|
| **Lookout** | Sees a sail sooner: the relief ship, the Brennish, privateers. | Step S |
| **Boat shed and slip** | The boat kept out of the weather; the ship careened and her hull mended. | Step S |
| **Powder magazine** | Powder kept dry and apart, away from fire. | Step S, with the guns |
| **Gun platform** | A battery on the headland covering the anchorage. | Step S, if approved |
| **Chapel** | A day's rest each week; the dead buried, the married wed. Relief, belonging. | Optional |
| **Forge** | Tools made, not only mended (ECONOMY_AUDIT §44). Needs iron, which the camp has no source of yet. | Needs iron first |
| **Trading house** | Where a people's visits and trade are held: trust and trade come easier. | Optional |
| **Salt pans** | Salt boiled from the sea, as Kessat does: fish and meat salted to keep. | With knowledge from Kessat |
| **Kitchen in each house** | Households cooking for themselves (§40, distribution by household). | With distribution |

## Saves

Format 45 keeps which household lives in which house. An older camp has no houses or smokehouse.

## Tests

`StructuresTests` (2):
- a house goes to the household longest without one, eases them, names it in the community's condition,
  and is saved;
- fish is smoked at the smokehouse.
