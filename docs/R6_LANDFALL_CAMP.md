# R6 — The landfall camp: the first 72 hours

**Status:** being built (2026-10-22), on the owner's word: "cleanup pass first, then R6".
**Authority:** SETTLEMENT_EXPERIENCE §1, §3–6 (the assessment, the first 72 hours, the site, the
camp stage); SETTING §5 (the Ember Coast, Punta Ceniza); VOYAGE_EXPERIENCE §39–40 (the wreck as a
transition).

## What it is

The voyage ends in one of two ways:
- **Arrived:** she comes to anchor off the coast.
- **Wrecked:** the survivors come ashore from the wreck or the boat.

Either way the campaign goes on, on the shore, with the same people. They keep their injuries,
fatigue, quarrels and skills, and the stores they actually have. The first three days are survival,
not building: water, shelter, fire, the stores, the sick. The captain sets the priorities; the hands
do the work; the weather and the shore decide how it goes.

## The shore

**Every landing site is described in `data/shore.json`:** each coast landmark, the coast of Castrena,
and the islands, with a default for anywhere else. For each site:
- **Water:** its sources (a spring, a stream, a brackish seep). Each has how far it is, how much it
  gives, and whether it is known on landing. Punta Ceniza's spring is in the sailing directions;
  most others have to be found.
- **Food:** what can be taken (shellfish, fish, fruit), and whether it is unfamiliar. Strange food eaten
  without anyone who knows it can bring on the flux.
- **What there is to build with:** brush and poles, and at Punta Ceniza the burned factory's timbers.
- **Hazards:** fever from the lagoon at night; surf that stops the boat.
- **Signs of people:** paths, a fish weir, smoke. These are signs only. Meeting the coast's people is
  R8; R6 never makes them up as enemies or as empty land.

**The captain does not know the site** (SETTLEMENT §3). The assessment says what is seen, and what is
only guessed: "Water: none seen. Birds go inland at dusk." Scouting finds the rest.

## The camp (simulation)

`Campaign.Camp` (save format 37) holds:
- the site and when they landed;
- each work of the camp and how far along it is;
- what has been found;
- what is still afloat: the wreck's remains, or, when she is at anchor, her hold;
- the nights each person spent sheltered or out in the open.

**The works** (SETTLEMENT §6), each a task done at its place in the camp:

| Work | Task | What it does |
|---|---|---|
| Shelter | Rig a sail shelter (uses the spare sailcloth) or cut a brush shelter | Each holds so many. Without one, a wet night brings chills and fever, and no rest. The sail's is quicker and drier, but the sail is gone for mending her. |
| Fire | Make a fire, then keep it | Hot food (more from a meal); a warmer night; heart. |
| Water | Fetch water from a found source | Water casks filled. Brackish water risks the flux. |
| Stores | Land the stores (the boat, from the ship or the wreck), then cover them | Stores ashore, in trips, as the surf allows. Uncovered in rain, food spoils. |
| Sanitation | Dig a latrine | Without one the camp grows foul, and the flux comes, as on board. |
| The sick | A sick shelter; tend the sick | As on board (R4f), out of the rain. |
| Reconnaissance | Scout | Finds the next thing about the site: water, food, signs, the factory. Alone and away, with some risk. |

**The captain's priorities:** the eight of SETTLEMENT §4, ranked. Idle hands take the first unmet
work they can do. There are also four choices, each one of §4's tradeoffs:
- use the sailcloth for shelter, or keep it for the ship;
- rest the exhausted, or work them;
- scout, or keep together;
- eat the ship's stores, or forage.

**Weather ashore:** rain by the hour, from the site's season. It is as deterministic as at sea.

**After 72 hours:** the first days' report. Is there water, and how far? How many slept sheltered?
Who fell sick, who was lost? What was landed, and what is still afloat? What was found? What signs of
people were seen? The camp then carries on; establishing a settlement is R7.

## The camp (view)

- **The scene:** a strip of beach with the forest behind it. Offshore lies the wreck, or the Garza at
  anchor. The boat runs between them.
- **The camp's works appear as they are made:** a sail or brush shelter, the fire and its smoke, casks
  at the water place, the stores under canvas, the latrine, the sick shelter.
- **The HUD:**
  - **"The first days":** the priorities with their state, the choices, and what is afloat.
  - **The landing:** the assessment card.
  - **The report:** after 72 hours.
- **Sound:** surf, wind, rain, fire. These are the recorded beds already in hand.

## Not in R6

- Building in earnest, storage and spoilage over seasons, gardens (R7).
- Meeting the Ahuneri or the Tesiwa (R8).
- Moving the camp to another site (§5 site selection). R6 records the site's merits so R7 can offer
  the move.
- Repairing the Garza to sail again.
