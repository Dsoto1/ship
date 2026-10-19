# Vision Alignment Review

**Date:** 2026-10-19
**Why:** after V4 the owner said the game is drifting from its vision. The port must feel alive, like
Hitman's crowds: people moving about their business, a world you can watch, people you click to learn
about. It must look amazing, not "so-so", and must not be a set of menus. This review measures what
exists against the three authority documents, section by section, and re-plans the work.

**Authorities:**
- GAME_VISION.md (gameplay);
- ART_BIBLE.md (visuals);
- TECHNICAL_SPEC.md (architecture);
- SETTING.md (approved setting).

**Short answer:**
- **The foundation is sound** and on vision: persistent people, truth versus knowledge, tasks,
  needs, ship systems, events, saves.
- **Three things drifted:**
  - the port became a menu;
  - every visual is code-built placeholder geometry, which the Art Bible forbids as a target;
  - the voyage ends where the vision says the game should continue.

---

## 1. Section by section

Verdicts: **Aligned**, **Partly**, **Drifted** (built, but against the documents), **Missing**.

### 1.1 Core identity and pillars (GAME_VISION §2)

| Requirement | Now | Verdict |
|---|---|---|
| The same people persist across every phase | One roster, stable IDs, knowledge, relationships and chronicle kept from hiring to the end | Aligned |
| "One person making consequential decisions in a living world, not an omniscient manager selecting units from a menu" | At sea, the captain is a figure on deck, but decisions are made from panels. In port it is entirely a menu | **Drifted** |
| Direct control in the world | Click to select and walk on the ship; nothing in port | Partly |
| Pillar 3: disasters create new problems, not endings | Foundering and landfall end on a screen ("a story for a later version") | **Drifted** |
| Pillar 4: the world remembers | Chronicle, knowledge, relationship history | Aligned |

### 1.2 Port preparation and recruitment (GAME_VISION §5, TECHNICAL_SPEC §15, ART_BIBLE §7.1, Test A)

| Requirement | Now | Verdict |
|---|---|---|
| Compact, active, explorable port with districts (quay, market, tavern, workshops, customs or sponsor's office) | A full-screen hiring panel over the ship | **Drifted** |
| 20–30 NPCs with schedules, jobs and goals, "not standing permanently at recruitment markers" | 12 candidates exist only as data rows; nobody else lives in the port | **Missing** |
| "A functioning place where people already have jobs and lives, not a lobby built around the player" (Art Bible) | No place at all | **Missing** |
| Encounter, investigate (observe, converse, references, demonstrations, test tasks), negotiate, recruit or refuse | Talk, ask, test and haggle exist as buttons; observing them at work does not exist in port | Partly |
| Candidates evaluate the captain and may refuse (wages, distrust, destination, unsafe ship, crew conflicts) | Only the proud refuse a test or a haggle | Partly |
| Deadline: candidates take other work, stock sells out, fees rise, telegraphed | A fixed count of "inquiries"; nobody leaves; stock is endless | **Drifted** |
| Inspect the ship before sailing; cargo by volume and weight | No inspection; the hold has a capacity but stowage is never shown | Missing |
| UI shows known skills with confidence, wages and terms, health concerns, relationships with the roster, provisions consumed, critical skill gaps, cargo limits | Known skills with source and provisions consumed, yes; skill gaps, relationships and cargo limits, no | Partly |
| Truth versus knowledge | Claims, references and tests stay separate from the truth and carry over on hiring | Aligned |

**What to keep:**
- the twelve candidates with their truth, claims and references;
- the knowledge model;
- haggling;
- the purse shared between wages and stores.

**What changes:** these all move into the world. The menu becomes at most a contract or roster
document opened from the captain's papers.

### 1.3 Voyage (GAME_VISION §6, TECHNICAL_SPEC §10–14)

| Requirement | Now | Verdict |
|---|---|---|
| Hull, leaks, bilge, pumps, rigging, sail configuration, repairs with materials | Yes, including sail plans and repairs left | Aligned |
| Navigation: estimated versus actual position, chart confidence, route choice, detours | A distance counter; no chart, no estimate, no route | **Missing** |
| Cargo: placement, securing, damage | Inventory with capacity only | Missing |
| Watches and duty rotations | Duties with conditions; no watch rotation by time | Partly |
| Time controls with slow-down on crises | Pause to 720×, auto-pause on danger and events | Aligned |
| Weather causing cascading crises with warning signs | Weather damages the ship; little warning, and crises do not chain | Partly |
| Events emerge from state, "not primarily a deck of unrelated random popups" | Thirteen authored events, some state-driven (feuds, stress, water, bilge), most chance-based text cards | Partly; at risk |
| Conflict without a mutiny meter; concerns voiced first | Stress, quarrels and "a word with the captain"; no refusals or broken promises | Partly |
| Medical crisis and treatment | Fever and flux are text choices; no treatment task, no medicine | Missing |

### 1.4 Shipwreck and landfall (GAME_VISION §7, TECHNICAL_SPEC §16, P1 items 7–8)

| Requirement | Now | Verdict |
|---|---|---|
| A shipwreck transitions to survival with the actual survivors and salvage | End screen | **Drifted** |
| Landfall leads to camp: water, food, shelter, gathering, building | End screen | Missing |
| Captain death leads to a credible successor or a leadership crisis | Placeholder: the most respected takes over | Partly; needs approval |

### 1.5 Art (ART_BIBLE §1–3, 7, 12, 16, 19–25)

| Requirement | Now | Verdict |
|---|---|---|
| Illustrated historical realism: "a historical adventure painting transformed into an explorable miniature world" | Code-built primitives: boxes, cylinders and spheres, with good lighting and a shader sea | **Drifted.** Fine as a graybox, but it cannot reach the target, and the Bible forbids reinterpreting "stylized" as low-poly |
| People are the visual priority: recognizable, expressive faces | Jointed primitive figures with colour and hats; no faces, no portraits | Partly |
| Animation of work: hauling, climbing, pumping, cooking, rest | Generic arm swing for any work, sitting for rest | Partly |
| The world accumulates history (patched sails, scars) | Not shown | Missing |
| Validation scenes A–D before asset production; a repeatable asset pipeline | Test B (ship at sea) exists as a graybox; no pipeline | Partly |
| UI as the captain's working materials (manifests, journals, charts), modern usability | Dark translucent panels | Partly |
| Camera: isometric, two useful zooms, occlusion handling | Orthographic isometric, zoom, sails fade | Aligned |

### 1.6 Architecture (TECHNICAL_SPEC)

Aligned throughout:
- simulation independent of Godot;
- data-driven content;
- versioned saves with migrations;
- seeded random streams;
- chronicle;
- 363 tests.

This is what makes the re-plan cheap: the port, the wreck and the camp are new *situations* on the same
campaign state, as GAME_VISION §2.1 intends.

---

## 2. Why it drifted

1. **Playable first, then reshape.** After "there is no gameplay yet" I built the fastest playable
   loop (V1–V4). That gave the voyage stakes and decisions, which was right. But I filled the port
   with a menu instead of the living district the documents describe, and I did not flag that it
   broke §5.
2. **No art pipeline.** Everything visible is generated by code. It can look tidy, but not
   "amazing". The Art Bible wants an asset pipeline decided and proven in validation scenes, and that
   decision is the owner's (§20, §24).
3. **Endings instead of continuations.** Wreck and landfall screens close the loop for testing, but
   pillar 3 says disasters create new problems.

---

## 3. Re-plan

The P1 completion gate (TECHNICAL_SPEC §36) is unchanged: recruit in a living port, provision,
depart, survive a crisis, reach land or wreck, and keep playing with the actual survivors. The order
now puts the port first, because it is the opening, the hook, and the furthest off.

### R1 — The living port, simulated (no art dependency)

- **The waterfront at La Bocana** as one map with places:
  - the quay with the *Garza*;
  - the Chamber office;
  - the chandler and the market;
  - the *Three Anchors*;
  - the chapel;
  - the Company counting house;
  - a carpenter's yard and cooperage.
- **About 25 people:**
  - the 12 candidates;
  - townsfolk: dockers, the chandler, the landlord, a fishwife, the Chamber clerk, the priest, a
    carter, a guard, the Company factor, a Brennish trader, a moneylender, children.
- **Daily schedules:** sleep, work at their place, eat, the tavern at night, mass in the morning.
  People walk between places and do visible work.
- **Candidates live their stories:**
  - Bartolo works at the yard;
  - Rodrigo boasts in the tavern and is drunk at night;
  - Tomé waits in the gaol cart;
  - Ximena sells at the fish stalls;
  - Pons avoids the moneylender;
  - Velho meets the Brennish trader after dark.
- **Watching is investigating:** a candidate working where the captain can see them yields
  observation knowledge, the strongest source after a test. Seeing Velho with the Brennish is a
  discovery.
- **The deadline is real:** the convoy sails at dawn on day 3. Other masters hire people, and some
  candidates leave if you wait. The chandler's stock runs down. All of it is telegraphed, through
  rumours and the Chamber's notices.
- **Candidates weigh you:** wage, destination, the ship, your reputation, and who else you have
  hired (a Castrenan sailor may refuse to berth with the Brennish man).
- **Headless tests** for schedules, observation, departures and refusals.

### R2 — The living port, playable

- **The captain walks the waterfront.** Click to move; follow the camera. Others go about their day.
- **Click anyone:** a record card with name, look, occupation, a "seeking a berth" mark, what you
  know with its sources, rumours you've heard, and relations with your crew.
- **Act in the world:** walk over and talk (a dialogue panel), ask a bystander (who you ask colours
  the answer), offer terms, or set a demonstration at a work spot.
- **Inspect the *Garza*** on the quay, finding weak planks or a sprung yard to fix (at a price)
  before sailing.
- **Buy stores at the chandler;** porters carry them aboard, and stowage shows space used.
- **The menu goes:** the hiring rules from V4 stay; their presentation moves into the world.

### R3 — Visual validation: Test A, the port (needs the art pipeline decision)

- **Following ART_BIBLE §23 and §25:** morning and late-afternoon light, 8–12 visually distinct
  people, dock, market and workshop architecture, cargo and tools, and the UI over the world.
- **Measured** at gameplay zoom, with performance recorded.
- **Repeat for the ship (Test B)** with the same pipeline.

### R4 — Voyage gaps

- a chart with estimated versus actual position, route and detours;
- cargo stowage;
- a medical crisis with treatment and medicine;
- storms that chain into second crises;
- events rooted in state rather than chance;
- watch rotation.

### R5 — Shipwreck continuation

The wreck strands the actual survivors on a beach with salvage, and the survival loop begins
(TECHNICAL_SPEC §16).

### R6 — Landfall camp at Punta Ceniza

A camp with water, food, shelter and gathering, and the first contact with the Ahuneri. Ximena's and
Velho's secrets begin to matter here.

---

## 4. Decisions needed from the owner

1. **Art pipeline (blocks R3; ART_BIBLE §20 and §24).** How assets are made for illustrated
   historical realism. The options are compared in the conversation that produced this review.
2. **Succession rules (TECHNICAL_SPEC §18.1).** The placeholder stays until approved.
3. **Camera.** Keep the orthographic isometric view (D-006). "A la Hitman" is read here as *living
   crowds with routines you can watch and exploit*, not as a third-person camera.
