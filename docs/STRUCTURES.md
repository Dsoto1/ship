# Structures: what the camp can build, and what it still lacks

**Status:** 2026-10-23. The house, smokehouse, chapel, trading house, salt pans and forge are new. The rest were built in R7, step 2 and R8.
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
| **Chapel** (new) | `chapel: true` | Sunday kept there. See below. |
| **Trading house** (new) | `trades: true` | The coast's peoples received under a roof. See below. |
| **Salt pans** (new) | `salts: true`, `knowledge: know.salt` | Salt made, fish salted. See below. |
| **Forge** (new) | `forges: true` | Tools made from bar iron. See below. |
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

### The chapel

A cross, an altar and a roof:
- **Sunday:** each Sunday at first light everyone ashore eases (stress −3). The days of the week run from
  the voyage's first day, 12 April 1501.
- **The community's condition** counts it: "Sunday kept at the chapel" (+).

### The trading house

A roofed place to receive a people's delegations and lay out the goods:
- **Visits:** every people comes a quarter sooner.
- **Trade:** a fair trade there raises trust by 2.5 rather than 1.5.

### The salt pans

Kessat makes salt, and trades it along the coast. The camp can build salt pans only once someone has
learned how from Kessat ("how they make salt": trust 15, a little of their language). Until then, ordering
them is refused, and the refusal says who to learn it from.
- **Boil salt:** a cooking job, two hours, one pole of firewood for two salt. The camp keeps about ten salt
  laid by.
- **Salt fish:** ten fish and two salt make ten salt fish in two hours. Salt fish loses 0.4% a day, so it
  keeps well over a year.
- **When:** both are among the Food duties.
- **Trade:** Kessat also trades salt.

### The forge

A hearth, bellows, and an anvil on a stump:
- **Forge a tool:** four hours of crafting (at least level 1), three bar iron and two poles burned for
  charcoal. It makes the tool the camp is shortest of for its size: axes first, then spades, mattocks,
  adzes and augers.
- **Milestone:** a forged tool counts for "a tool mended" as well.
- **Where the iron comes from:**
  - bought in Bocana before sailing (the chandler has six bars);
  - one bar from each turn of salvage at the burned factory (nails, hinges, hoops in the ash);
  - bought from the Brennish;
  - traded from Kessat, who have it of the Brennish;
  - 20 bars on the relief ship if the charter is kept, 10 if kept in part.

## What could still be built, and when

| Structure | What it would do | When |
|---|---|---|
| **Lookout** | Sees a sail sooner: the relief ship, the Brennish, privateers. | Step S |
| **Boat shed and slip** | The boat kept out of the weather; the ship careened and her hull mended. | Step S |
| **Powder magazine** | Powder kept dry and apart, away from fire. | Step S, with the guns |
| **Gun platform** | A battery on the headland covering the anchorage. | Step S, if approved |
| **Kitchen in each house** | Households cooking for themselves (§40, distribution by household). | With distribution |

## Saves

Format 45 keeps which household lives in which house. The other new buildings keep nothing beyond what every building keeps, so they need no new save format. An older camp has none of them.

## Tests

`StructuresTests` (7):
- a house goes to the household longest without one, eases them, names it in the community's condition,
  and is saved;
- fish is smoked at the smokehouse;
- Sunday is kept at the chapel, and only on a Sunday;
- a fair trade at the trading house counts for more;
- the salt pans are refused until Kessat's knowledge is learned, then make salt and salt fish;
- the forge makes the tool the camp is shortest of, and reaches the milestone;
- the relief ship brings iron.
