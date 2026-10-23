# The economy loop against the game as built

**Authority:** `docs/ECONOMY_LOOP.md`, the owner's cross-phase spec, 2026-10-22.
**Status:** audit, 2026-10-22. Each line of the spec's acceptance (§59) and guardrails (§58) is checked
against the code as it stands: **Yes**, **Partly** or **No**, with where it is or what is missing.

## What already holds

**One economy across every phase (§1, §21, §55)**
- **Yes.** The characters, tasks, tools, inventory, needs and chronicle are one model, from the port to the
  camp.
- Cargo bought in port is what is aboard, and what reaches land; nothing resets at landfall:
  - at anchor, the stores come ashore by the boat;
  - after a wreck, they are what the boat saved, plus what can be got at from the wreck.

**People, not workers (§2, §4, §5)**
- **Yes.** Every task is done by a named person with skills, injuries and relationships.
- Port candidates are multi-skilled, and what they claim is checked against what is known (claimed, rumoured,
  observed).
- Berths and the purse limit the roster.

**Watches are the ship's workplaces (§9)**
- **Yes:** the watch bill (R4d).

**Tools, knowledge and apprentices (§13–15)**
- **Yes.**
  - Tools are required by tasks, wear out and break, and can be mended at the workshop.
  - Knowledge lives in people (languages, skills) and in the coast's lore, which a people tells (R8).
  - Mentorship teaches by working alongside (R4f).

**Food, water and health (§16–17, §19)**
- **Yes.**
  - **Food:** bought, eaten, spoiling, foraged, fished, grown, traded.
  - **Water:** casks, then the spring, then the well.
  - **Health:** sickness and injury take people off work, nursing takes labour, and overwork hurts people.

**Crises from causes (§20)**
- **Partly.** The camp's cascades are real: wet season → fever → fewer hands → the work stops. The 180-day
  test runs show them.
- At sea many crises are still event cards.

**Construction (§24–25)**
- **Yes.** The camp grows by stages. Buildings go up phase by phase, with real materials, tools, labour and
  time, and their quality comes from the work.

**Organic paths (§26)**
- **Yes.** Paths wear where feet go and grow back (R7, NATURAL_GROUND).

**Exploration (§37)**
- **Yes.** Parties carry food, take days, and bring back the coast (R8).

**External communities (§38)**
- **Yes.** Three peoples with their own trust, grievances, wants, ground and lore.

**The rest of this group**
- **Short production chains (§27):** **Yes.**
- **Failure continues (§46):** **Partly.** The wreck continues the game, and so does a charter failed at day
  180. Relocation is built (THE_MOVE: actual goods carried, actual people moved, the old camp kept); moving
  after a disaster as such is not.
- **Save and load (§51):** **Yes**, mid-storm and mid-build.

## What is partly there

| Spec | Now | Missing |
|---|---|---|
| §7 Same goods, continuous state | Quantity persists. Provenance only where it matters: Hob's sacks and barrels are their own items, spoil and sicken. | Containers with a history (a cask that leaked, a barrel used for water ashore). |
| §11–12 Physical logistics, local storage | **Now kept** (THE_MOVE): each camp's stores lie in one place, the beach stack or the storehouse. What is brought in is carried there by hand, materials are taken up there on the way to the work, and between camps everything is carried. | Several stores in one camp (a workshop's tools, a cache by the work). |
| §18 Projection and rationing | Days of food and water at sea; short rations as a standing order at sea; **rations ashore (full, short, emergency) and the camp's projections in ranges** (COMMUNITY.md). | Ranges at sea too. |
| §22 An intact ship has a cost | The ship rides at anchor and her hull wears. | Choosing who stays aboard, and using her afterwards for fishing, trading or exploring by sea. |
| §28 Bottlenecks named | The camp's "still wanting" list; the first-days report; **each project says why it has stopped** (WORKPLACES.md). | For the camp's other work, not only buildings. |
| §29–30 Priorities and delegation | Camp priorities and choices; standing orders at sea; **priorities per project, critical to paused, and leads** (WORKPLACES.md). | Leads for the camp's other work. |
| §34 Policies | Priorities, sailcloth, scouts, foraging; **rations, the working day and reserve targets** (COMMUNITY.md). | Emergency labour, trade restrictions, exploration priority. |
| §43 Readiness | The port's checklist (hands, the way, stores for the passage). | The spec's two-part summary: voyage cover and landfall capability, in words, no score. |
| §44 Self-sufficiency | The camp's stage, and the 90-day verdict; **eight milestones** (COMMUNITY.md). | None: tools are forged now (STRUCTURES.md). |

## What is not there yet

| Spec | Missing | Where it goes |
|---|---|---|
| §31 Workplaces led by a person | **Done for buildings (WORKPLACES.md):** a lead's skill sets pace, quality and safety, and teaches. Not yet for the hearth, the water or the garden as a whole. | Step W, later |
| §32 Households (now approved: couples and children) | **Done in part (PEOPLE.md):** couples, among the company and with people of the coast by their customs; children. | Dwellings by family, shared possessions. |
| §33 Community condition | **Done (COMMUNITY.md):** a word with its causes, + and −. The causes explain the strain; most do not yet add to it themselves. | Step P (households, belonging) |
| §39 Population growth as people | **Done (PEOPLE.md):** the relief ship's settlers, people of the coast who come to live, a Brennish hand who stays, births; the worn out go home if the charter is revoked. | Castaways. |
| §40 Distribution | People eat from the one pool. | Kitchens and households distributing. |
| §45 Buildings carry history | **Done (WORKPLACES.md):** when built, under whom, by whom, from what, storms and repairs. A person's record does not yet say what they led. | Step P |

## The guardrails (§58)

| Guardrail | Kept? |
|---|---|
| No separate settlement inventory | **Yes:** the shared store follows from ship to camp. |
| No reset at landfall | **Yes.** |
| No anonymous workers | **Yes.** |
| No teleporting resources globally | **Yes**, since THE_MOVE: carried within a camp and between camps. |
| No enormous production chains | **Yes.** |
| No dozens of meaningless resources | **Yes.** |
| No infinite labour without rest | **Yes:** fatigue and rest. |
| Not every crisis a random card | **Partly:** the camp's are causal; the sea's are mostly cards. |
| No vending-machine communities | **Yes:** they have their own minds, and trade is at their valuation. |
| Delegation keeps names | **Yes** (there is little delegation yet). |
| No opaque happiness number | **Yes:** there is no number at all yet. |

## The order I propose

Each step is one pull request, built on the last. It folds in the steps already agreed with the owner (the
first camp then a better site, people joining, couples and children, playing on past day 180).

1. **Step 2, the move: stores where they lie, and hauling.** *The move is done (THE_MOVE.md); stores held in
   places inside a camp are next.*
   - **The move:** the first camp is temporary. Parties find and judge sites; the charter scores where the
     settlement stands. Moving is done by the people themselves: packs, the boat, trips along the shore.
   - **Stores where they lie:** stores held in places (the beach cache, the storehouse, the workshop, the
     ship). Hauling moves them between places. A task draws from the nearest store that holds what it needs,
     and walks there (§11–12, §46).
   - This mends the guardrail that is broken now, and makes the move real: no goods teleport to the new site.
2. **Step W, workplaces and the history of things.** *Done (WORKPLACES.md).*
   - Workplaces have a lead, who sets quality, pace and teaching (§31).
   - Each project says why it has stopped (§28).
   - Buildings carry their history (§45).
   - Priorities per project (§29).
3. **Step C, the community's condition and its policies.** *Done (COMMUNITY.md).*
   - The community's condition in words, with its causes (§33).
   - Rations ashore, working hours and reserve targets (§18, §34).
   - Self-sufficiency milestones (§44).
   - Projections given in ranges.
4. **Step P, people.** *Done in first pass (PEOPLE.md); distribution and castaways still to come.*
   - **Arrivals:** the relief ship's settlers when the charter is kept, castaways, Brennish deserters, people of
     the coast who choose to come (§39).
   - **Households:** couples, including with people of the coast, shaped by both sides' customs (§32).
   - **Children and years:** births, and children growing up over years after day 180.
   - **Distribution:** by household and kitchen (§40).
5. **Step S, the ship after landfall.**
   - Who stays aboard.
   - The ship fishing, trading and exploring by sea.
   - Her own upkeep (§22, §56).
   - If approved: the guns, with powder and shot as cargo, gunners, and privateers met by decision rather than
     battle.
6. **Port readiness** in the spec's two-part summary (§43), and **containers with a history** (§7), as small
   passes alongside.
