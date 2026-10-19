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

---

## 5. The port brief (`PORT_EXPERIENCE.md`, added 2026-10-19)

The owner supplied a port specification. It is now the authority for the port (D-023), alongside the
three main documents. It confirms the direction of R1–R2 and adds a lot. Its historical rules apply
to the approved fictional analogue (SETTING.md, D-013): "early-16th-century" means Castrena and La
Bocana, judged against the real 1490–1530 Atlantic they are modelled on.

### 5.1 Acceptance criteria (§22) against what exists

| Criterion | Now | Verdict |
|---|---|---|
| Five recognizable, interconnected districts | Quay, square, tavern, chapel, Chamber, chandler and one yard on a flat strip | Partly; workshops thin, no customs height |
| Expedition ship visible and persistent | The *Garza* is a separate deck map; not drawn in port | Missing |
| Ship visibly changes with preparation | — | Missing |
| Candidates follow purposeful schedules | 12 candidates and 16 locals on daily schedules | Aligned (simulation) |
| A candidate found through a lead, not a menu | References and rumours exist; no leads journal; everyone's "seeking" is visible | Partly |
| Candidate information shows uncertainty and provenance | Sources, informants and levels in the knowledge book | Aligned (simulation); needs the card |
| At least three NPC activity loops visibly work | People sit at one spot for an hour; no carrying | Missing |
| Crowd tiers create apparent density | Tier A and B exist as characters; no Tier C | Missing |
| Time of day changes activity and sound | Activity yes; no sound | Partly |
| Weather changes visuals and behaviour | No port weather | Missing |
| Ambient events, without rewards | — | Missing |
| Purchases change authoritative cargo | `Buy` stores goods in the hold | Aligned |
| Representative cargo appears physically | — | Missing |
| Port recognition changes with the expedition | — | Missing |
| Departure uses the actual crew and cargo | Sailing moves the real crew; no departure sequence | Partly |
| Save and load keep persistent NPC and expedition state | Format 15 | Aligned |
| Performance within targets | Not measured | Missing |
| Nothing historical canonized from unverified generation | Fictional analogue; generated layout | Aligned |

### 5.2 The port work, in the brief's order (§23)

R2 is split so each step is playable and testable:

| Step | Brief §23 | Work |
|---|---|---|
| **R2a** | 1, 2, 7, 8 | Five-district greybox with sightlines (customs on a terrace over the harbour, market and tavern, a workshop street with cooper, smith, sail loft, ropewalk and shipwright, the quay with piers and vaults). The *Garza* moored as the landmark. Walking speed slowed so people can be watched. The captain walks; click anyone for a card with what is known and where it came from; talk, ask, offer and demonstrate in the world; the chandler; set sail. |
| **R2b** | 3, 4, 5 | Semantic activity nodes with reservation. Work loops: dockers carrying from the vaults to ships, fishermen landing catch to the stalls, the shipwright's timber–saw–hull round, the ropemaker walking the ropewalk. A leads journal: candidates are found by word of mouth, not marked. |
| **R2c** | 6, 9 | Tier C crowd (cheap visual walkers that are not characters), background vessels, ship preparation states, and cargo stacks drawn from the hold. |
| **R2d** | 10, 11, 12 | Sound zones (procedural until CC0 audio is reachable), one weather change in port with reactions, ambient micro-events. |
| **R2e** | 13, 14 | Recognition (merchants greet, candidates approach, creditors notice) and the departure sequence with the real crew and cargo. |
| **R3** | 15, 16 | The four validation scenes of §21 (dawn quay, midday market, rainy workshop street, departure morning) with the hybrid pipeline (D-021), and profiling. |

### 5.3 Needing the owner's approval (brief §19, §24)

- **Final NPC counts and performance budgets.** Working target: the brief's 20–30 meaningful people
  plus a Tier C crowd, until profiled in R3.
- **Port geography.** R2a reshapes the generated R1 map into the five districts the brief asks for.
  It stays a first draft; nothing about it is canon.
- **Sound assets.** No CC0 audio host is reachable from the build environment, so R2d starts with
  generated sound.

---

## 6. The voyage, navigation and settlement briefs (added 2026-10-19)

The owner supplied three more briefs, now the authorities for their phases (D-024):
- `VOYAGE_EXPERIENCE.md`: the voyage and the ship;
- `NAVIGATION.md`: the chart, position and time compression;
- `SETTLEMENT_EXPERIENCE.md`: landfall, camp and settlement.

Like the port brief, their historical rules apply to the fictional analogue (SETTING.md, D-013).
The settlement's region and peoples are already the approved Ember Coast, Punta Ceniza, the Ahuneri
and the Tesiwa. Each culture still needs its plausibility pass before content (SETTING.md §5).

### 6.1 Voyage acceptance criteria (VOYAGE §49) against what exists

| Criterion | Now | Verdict |
|---|---|---|
| Ship moves by a simplified wind and heading model | Speed from a weather state's wind and the sail plan; no wind direction or heading | Partly |
| Route and estimated position distinct from true position | A leagues counter | Missing |
| Standing and urgent tasks | Duties with conditions, urgent orders | Aligned |
| Watches and rest affect fatigue | Self-care rest at fatigue 70, a watch duty; no watch rotation | Partly |
| Food and water consumed | Yes, with rationing | Aligned |
| Health conditions persist | Conditions are saved and carried | Aligned |
| Multiple damageable systems | Hull, bilge, rigging | Partly: no sails vs lines, steering, pumps or cargo damage |
| Leaks create pumping pressure | Leaks from hull wear; a pump duty | Aligned |
| Repair consumes time and materials | Oakum, plank and rope | Aligned; no repair quality or history |
| Storm preparation affects outcomes | Heave to; nothing to secure beforehand | Partly |
| Severe weather changes visuals, audio and gameplay | Visuals and gameplay; no audio | Partly |
| Accelerated time stops and alerts appropriately | Auto-pause on danger and events | Aligned; thresholds not configurable |
| Social concerns emerge causally | Stress, quarrels, "a word with the captain"; no escalation stages | Partly |
| Chronicle records meaningful events | Yes | Aligned |
| Save and load in travel and crisis | Format 15 | Aligned |
| Port recruits remain the same characters | Yes | Aligned |
| Landfall preserves state | An end screen | Missing |
| Shipwreck preserves survivors, injuries, relationships, salvage | An end screen | Missing |
| Dead stay dead | Yes | Aligned |
| The player can understand major failure causes | Need explanations and end-screen advice; no causal storm story | Partly |

**Also missing:**
- the voyage's emotional phases (departure, adjustment, open ocean and so on) as distinct pacing;
- cargo zones and securing;
- skill growth and mentorship;
- encounters with independent ships;
- fishing that feeds food;
- visible damage history;
- sound.

**At risk:** the 13 authored events are partly choice cards, which the brief forbids as the main
source of drama (§42, §51). They become state-caused in R4.

### 6.2 Navigation acceptance criteria (NAVIGATION §41)

None of the 16 exists yet: there is no chart, position, heading, observation or landfall approach.
What exists and will be reused:
- time compression (Pause to 720×);
- auto-pause on danger;
- seeded random streams;
- the chronicle;
- versioned saves;
- the knowledge model (truth against belief, with sources). This carries over to charts: charted,
  reported and observed are knowledge sources.

### 6.3 Settlement acceptance criteria (SETTLEMENT §53)

None exists yet; landfall is an end screen. What carries over:
- **Already ready:** persistent people with health, relationships and skills; inventories and the
  hold; the task system with duties and work spots; knowledge and provenance; the chronicle.
- **To build:** construction projects, water sources, storage and spoilage, sanitation, seasons,
  exploration, and external communities.

### 6.4 Revised plan

Port work stays first (R2b–R2e, then R3). The voyage brief's order (§50) and the navigation brief's
order (§42) are merged into R4, and the settlement brief's order (§54) into R5–R7.

| Milestone | Briefs | Work |
|---|---|---|
| **R4a** Wind, heading and sailing | VOYAGE §5, §20; NAV §29 | Wind direction and strength, visibility, rain and cloud as weather variables with warning signs. Heading and point of sail set the speed, and the ship says why she is slow. |
| **R4b** True and estimated position, the chart | NAV §3–7, §12, §16, §39 | A 2D Ocean Sea. True position against dead reckoning, with the error growing by weather, time and the navigator's skill. A latitude sight that cloud blocks. A parchment chart showing the estimate, an uncertainty region, the believed track, and charted, reported and observed marks. Route legs and annotations. |
| **R4c** Standing orders and interrupts | VOYAGE §8, §35; NAV §7–9 | "Wake me if…" orders with configurable thresholds. Alerts by severity that focus the person or place they concern. |
| **R4d** Watches, rest and assignment | VOYAGE §9, §17 | Watch rotation. An assignment board showing competence, fatigue, conflicts and substitutes. |
| **R4e** Damage, repair and the storm | VOYAGE §4, §21–23 | Sails and lines, steering, pumps, and cargo zones (secured, loose, wet). Emergency, temporary and proper repairs, whose history persists. A storm with warning, preparation, impact, cascade and recovery, where preparation changes the outcome. |
| **R4f** Health and social pressure | VOYAGE §15, §18–19, §25–26 | A medical crisis with treatment and medicine. Grievances that escalate by stages. Slow skill growth and mentorship. The authored events become state-caused. |
| **R4g** Landfall approach | NAV §30–31 | Approach, soundings, sighting, identifying the coast, the chance of a wrong landfall, and the transition with all state kept. |
| **R4h** Voyage validation | VOYAGE §46; NAV §40 | Calm morning, fog and uncertainty, full storm, aftermath at dawn; sound; the ship's visible history. |
| **R5** Shipwreck transition | VOYAGE §39–40 | Casualties, location, escape, and salvage by accessibility, keeping the survivors' state, onto a shore. |
| **R6** Landfall camp: the first 72 hours | SETTLEMENT §3–6, §13 | The landfall assessment, water sources that must be found and carried, temporary shelter, fire, protected stores, sanitation, and the canvas-or-repair trade-offs. |
| **R7** Establishment | SETTLEMENT §7–19 | Construction projects in phases (labour, materials, tools, quality), storage and spoilage, the first garden, and seasons. |
| **R8** The world around the camp | SETTLEMENT §26–33 | Exploration on a regional chart (reusing R4b), the Ahuneri and Tesiwa as independent communities, language and trade, and the charter's conditions. |

### 6.5 Needing the owner's approval

These are listed in the briefs. Each has a proposed default, used until the owner says otherwise.

| Item | Brief | Proposed default |
|---|---|---|
| Time-compression multipliers | NAV §8, §43 | Keep Pause, 1×, 10×, 60×, 240×, 720× at sea |
| How much manual plotting | NAV §43 | The captain places waypoints and legs; the navigator does the arithmetic |
| Navigation abstraction | VOYAGE §51 | A flat chart in leagues. Latitude can be observed by sun or pole star; east–west position only by dead reckoning, with no longitude fixes |
| Hero ship type | VOYAGE §51 | The *Garza*, a caravel of about 60 tons (SETTING §4), pending the plausibility pass |
| Full naval physics, combat-heavy play | VOYAGE §51 | Not planned |
| Spreading fire, deep politics | SETTLEMENT §25, §42 | Not planned for the first settlement slice |
| Succession rules | TECHNICAL_SPEC §18.1 | The placeholder stays |
