# Milestones and Ticket Plan

Milestone definitions (P0–P3) and acceptance criteria are in `GAME_VISION.md` §10–§11 and
`TECHNICAL_SPEC.md` §35–§38. The revised plan after the spec audit is in `SPEC_AUDIT.md` §6.
This file tracks status and breaks the next work into tickets.

## Status

| Ticket | Title | Status |
|---|---|---|
| 0 | Repository and feasibility audit | Done (2026-10-18) |
| 1 | Solution scaffold, core data model and invariants | Done (2026-10-18) |
| 2 | Simulation clock and task execution | Done (2026-10-18) |
| 3 | Inventory, supplies, and save/load | Done (2026-10-18) |
| 3a | CI: build and test on every push | Done (2026-10-18) |
| 3b | Structured chronicle, need diagnostics, long-run tests | Done (2026-10-18) |
| 3c | Attributes, random streams, campaign ID and captain | Done (2026-10-18) |
| 3d | Directed relationships; traits and conditions as content | Done (2026-10-18) |
| 3e | Task queue, priority and resume | Done (2026-10-18) |
| 4a | Positions, work spots and movement in the simulation | Done (2026-10-18) |
| 4b | Godot graybox interaction scene | Done (2026-10-18) — owner ran the manual test on macOS, all 8 steps passed |
| 4c | Appearance record | Done (2026-10-18) |
| 5a | Identity: sex, languages, social status | Done (2026-10-19) |
| 5b | Captain's knowledge: truth vs what is known | Done (2026-10-19) |
| 5b+ | Standing duties and self-care (owner: "the crew needs automating") | Done (2026-10-19) |
| V1 | The world around you: sea, sky, day and night, a real ship, people (D-015) | Done (2026-10-19) |
| V2 | The ship in danger: leaks, wear, supplies, distance, need-driven duties | Done (2026-10-19) |
| V3 | Things happen: events with choices, crew who speak up | Done (2026-10-19) |
| V4 | Choose who comes: simple pre-voyage hiring (the core of 5c) | Done (2026-10-19), **stopgap**: a menu, against GAME_VISION §5 (D-020) |
| R1 | The living port, simulated: waterfront places, ~25 people on schedules, observation, real deadline, candidates who weigh you | Done (2026-10-19) |
| R2a | Port greybox and play: five districts, the Garza moored, walk, click anyone, talk, ask, offer, demonstrate, chandler, set sail (PORT_EXPERIENCE §23 steps 1, 2, 7, 8) | Done (2026-10-19), awaiting the owner's manual test |
| R2b | Leads journal and the Chamber's notices; more work loops (steps 3–5) | Done (2026-10-19), awaiting the owner's manual test |
| UI-1 | UI/UX design system, first pass: tokens, HUD shell, management drawer, alerts, uncertainty vocabulary (UI_UX §68 steps 1–11) | Done (2026-10-19), awaiting the owner's manual test |
| VP-0 | Port visual pass, step 0: batching and lighter figures (PORT_VISUAL_PASS §5) | Done (2026-10-19) |
| A-1 | Asset pass: CC0 materials for stone, plaster, timber, roofs, deck and ground; recorded sounds of work (D-021) | Done (2026-10-19) |
| VP-1 | Port visual pass, first block, Chandler's Quay: lighting, ground, clusters, kits, ship, depth, rain, sound (PORT_VISUAL_PASS §5–6) | Done (2026-10-19), awaiting the owner's review |
| R2c | Tier C crowd, background ships, ship preparation states, visible cargo (steps 6, 9) | Done (2026-10-19) |
| R2d | Sound zones, port weather and reactions, ambient micro-events (steps 10–12) | Done (2026-10-19) |
| A-2 | Asset pass: recorded CC0 beds for surf, market crowd, tavern, wind and rain (Freesound) | Done (2026-10-19), awaiting the owner's listen |
| R2e | Recognition and the departure sequence (steps 13, 14) | Done (2026-10-19) |
| R3 | Visual validation: the four port scenes of PORT_EXPERIENCE §21, then Test B ship, with the hybrid pipeline (D-021) | Part one done (2026-10-19): A and C judged, all staged, baseline profiled; part two after C-1 (D-028) |
| R4a | Wind, heading and sailing: wind direction, visibility, warning signs; why she is slow (VOYAGE §5, §20) | Planned |
| R4b | True vs estimated position; the chart: dead reckoning, a latitude sight, uncertainty region, route legs, annotations (NAVIGATION) | Planned |
| R4c | Standing navigation orders and configurable interrupts; alerts by severity (VOYAGE §8, §35; NAV §7–9) | Planned |
| R4d | Watch rotation and the assignment board (VOYAGE §9, §17) | Planned |
| R4e | Localized damage, repair quality and history, cargo zones, the storm with preparation (VOYAGE §4, §21–23) | Planned |
| R4f | Medical crisis, grievances by stages, skill growth and mentorship; events become state-caused (VOYAGE §15, §18–19, §25–26, §42) | Planned |
| R4g | Landfall approach: soundings, sighting, identifying the coast, wrong landfall (NAV §30–31) | Planned |
| R4h | Voyage validation: calm morning, fog, storm, aftermath; sound; the ship's visible history (VOYAGE §46) | Planned |
| R5 | Shipwreck transition: casualties, location, salvage by accessibility, survivors' state kept (VOYAGE §39–40) | Planned |
| R6 | Landfall camp, the first 72 hours: assessment, water, shelter, fire, stores, sanitation (SETTLEMENT §3–6) | Planned |
| R7 | Establishment: construction projects, storage and spoilage, the first garden, seasons (SETTLEMENT §7–19) | Planned |
| R8 | The world around: exploration, the Ahuneri and Tesiwa, trade, the charter (SETTLEMENT §26–33) | Planned |
| 5c | Recruitment rules in full: investigation, references, counteroffers | Later (core moves into V4) |
| 5d | Godot waterfront recruitment scene | Later, once the voyage is fun |
| 6 | Voyage-to-crisis vertical slice | Becomes V2–V3 |
| 7 | Shipwreck or landfall continuation | Not started |

## Ticket 0 findings

- The repository was empty: no commits, branches, code, build or tests.
- Engine chosen: Godot 4 with a plain C# simulation core (see D-001).
- Cloud dev environment: .NET 8 SDK installs from the Ubuntu package feed, and NuGet is
  reachable, so simulation code can be built and tested here. Godot itself cannot be
  downloaded here because GitHub releases are blocked, so scene work must be run locally.
- Against the P0 and P1 plans, nothing is implemented yet.

## Ticket 1 — Solution scaffold, core data model and invariants

**Scope.** Create `Expedition.sln`, `src/Expedition.Simulation` (net8.0, nullable enabled,
warnings as errors) and `tests/Expedition.Simulation.Tests` (xUnit). Add minimal models:
- `CharacterId`, `ItemId` and similar strongly typed IDs, with a campaign-scoped ID allocator.
- `Character`: identity (name, age, origin), skills (`SkillId → 0..5`), needs (hunger, thirst,
  fatigue, stress as 0–100), health (0–100, plus a list of conditions), traits (IDs),
  survival state, and an inventory reference.
- `CampaignState`: phase, roster keyed by ID, chronicle.
- `CampaignPhase` enum and a transition table with `TryTransition(target, out reason)`.
- `Chronicle` with append-only entries.

**Out of scope:** tasks, time, items beyond IDs, save/load, Godot.

**Acceptance.**
- Models have no Godot dependency and carry XML doc comments on their public members.
- Validation rejects out-of-range skills and needs, duplicate IDs, and resurrection of the dead.
- Tests: ID uniqueness across many allocations; skill range validation; invalid phase
  transitions rejected and recorded; a dead character is reported ineligible to work.
- `dotnet build` and `dotnet test` pass.

**Risks.** Over-modelling fields that P1 does not need. Mitigation: only the fields listed above.

**Result.** Implemented as specified, except the character's inventory reference, which moves to
Ticket 3 together with inventories. Survival state changes go through
`CampaignState.ChangeSurvivalState`, so they are validated and recorded in the chronicle.
42 xUnit tests pass.

## Ticket 2 — Simulation clock and task execution

**Depends on:** Ticket 1.
**Scope.** Fixed-tick `SimulationClock` and the seeded serializable RNG (D-002).
`TaskDefinition` (requirements, duration, need effects, resource cost) loaded from `data/tasks`.
`TaskInstance` with progress, completion and interruption. Needs change according to the
current activity, so rest recovers fatigue and work adds it.
**Acceptance.** Headless tests cover: fatigue accumulating while working and recovering while
resting; a task interrupted when a character becomes incapacitated; a task that is ineligible
because a skill or tool is missing; identical results for an identical seed.
**Risks.** Tick granularity affects balance. Keep need rates in data so they can be tuned.

**Result.** Implemented: one-minute ticks (D-004), the PCG32 generator (D-002), and task
definitions, need rates and rules loaded from `data/`, where unknown or misspelled fields are
rejected. Tasks check skill, tools, materials and fatigue before they start; work speed scales
with skill and drops when tired. Exhaustion, incapacitation and injury interrupt work. Hazardous
tasks can injure, incapacitate or kill, and every such outcome is recorded in the chronicle.
82 xUnit tests pass.

Deliberately left for later:
- `CampaignState.Supplies` is a plain stockpile with no weight or volume yet (Ticket 3).
- Item IDs in task data are not validated until item definitions exist (Ticket 3).
- Hunger and thirst do not yet harm health at 100; that comes with eating and drinking (Ticket 3).
- Incapacitated characters do not recover yet; that needs treatment tasks (Ticket 6).
- Materials are used up when a task starts and are not refunded if it is interrupted.

## Ticket 3 — Inventory, supplies, and save/load

**Depends on:** Tickets 1–2.
**Scope.** `ItemDefinition` (weight, volume, consumable flag) and stacks. Inventories with
capacity limits, including ship cargo. Food and water consumption over time. Versioned JSON
save/load (D-003) covering the clock, RNG state, roster, tasks, inventories and chronicle.
Also: replace the Ticket 2 `Stockpile` with real inventories; validate item IDs in task data;
eating and drinking reduce hunger and thirst, and health suffers when either stays at 100.
A save made with task definitions that no longer exist must fail with a clear error.
**Acceptance.** A 30-day provisions test consumes the expected amounts. A save followed by a
load produces an equivalent state (structural comparison). A corrupt or newer-version save
fails with a clear error. A dead character stays dead after loading.
**Risks.** Save schema churn while models are young. Mitigation: a version field and a
migration hook from the first save.

**Result.** Implemented:
- **Items.** `data/items.json` holds 9 items, each with weight, volume and food or drink value.
  Task data that names unknown items is rejected.
- **Inventories.** `Items/Inventory` has weight and volume limits, all-or-nothing removal, and
  transfers that cannot create or destroy items. Every character has a personal inventory with a
  carry limit. The campaign has a shared store (the ship's hold at sea).
- **Supplies.** Tasks take materials from the character's own pack first, then from the shared
  store.
- **Eating and drinking.** Crew eat or drink one unit at a threshold, so hard work uses more
  provisions. Incapacitated crew are fed; the dead are not. Starvation and dehydration wear
  health down until the character is incapacitated, then dead. A shortage is recorded once and
  again only after resupply.
- **Saves.** Versioned JSON with full validation (D-003), and `SaveStore` for slot files with
  atomic writes, in the standard per-user folder (D-005).

129 xUnit tests pass. Coverage includes a 30-day provisions count, save → load → identical
document, a storm-damaged crew that continues exactly the same way after loading, the dead
staying dead, a real format upgrade, and 16 corrupt or tampered save cases.

Deliberately left for later:
- Food does not spoil, and there is no rationing policy (full or half rations) yet.
- Characters always eat the first available food in ID order; there are no preferences.
- Condition IDs (injuries) and trait IDs are not defined in content or validated yet.
- There is no ship model yet, so ship condition is not saved (Ticket 6).
- The Godot side of saving (resolving `user://`, save and load buttons) is Ticket 4.

## Ticket 4 — Small playable Godot test scene

**Depends on:** Tickets 1–3. **Must be run locally** (Godot cannot be installed in the cloud
environment).
**Scope.**
- Create `game/`, a Godot 4 .NET project that references `Expedition.Simulation`, with a custom
  user dir of `The Expedition` (D-005).
- One deck-sized isometric test scene with a few characters drawn as placeholder shapes. Clicking
  a character shows their needs, health, task and inventory.
- Assign a task from a list, which shows the eligibility reason when it is refused.
- Pause, normal and fast time controls. Settle the real-time speeds left open in D-004.
- Save and load buttons that use `SaveStore` in `user://saves`.
- A chronicle panel.

**Acceptance.** The scene runs on macOS. The simulation stays the only source of state: the
scene only reads it and sends commands. A save made in the scene can be loaded back.
**Risks.** Godot .NET version and SDK mismatches on macOS. I can't run Godot here, so the
developer must run each step and report back what they see.

## P0 status

With Ticket 4b accepted, the P0 completion gate in TECHNICAL_SPEC.md §35 is met in graybox form.
Characters move, perform tasks, consume resources, become tired, hungry and thirsty, keep their state
through save/load, and are simulated independently of the renderer. Still outstanding from the
spec audit: 3a–3e and 4c, before P1 work begins.

## Ticket 4b — Godot graybox interaction scene

**What exists.**
- **Project:** `game/` is a Godot 4.7.2 .NET project (Forward+, custom user dir `The Expedition`).
  It compiles against `Godot.NET.Sdk` 4.7.2 and is part of `Expedition.sln`, so `dotnet build`
  catches C# breakage.
- **Code-built scene:** the scene is built in code (`Scripts/Main.cs`) so almost no hand-written
  scene format can go wrong.
- **Starting state:** comes from `Scenarios/TestDeckScenario` in the simulation library, which is
  tested headless.
- **Drawing:** orthographic isometric camera (D-006). Graybox deck, masts, fittings, work spots
  and water. The ship rolls gently.
- **Crew figures:** vary in height, build and clothing colour. They lie down when incapacitated
  or dead, and sit lower when resting.
- **Interface:** time controls (D-007), quick save and load to `user://saves`, crew list, ship's
  hold contents, a character record (needs bars, skills, conditions, pack, current task and
  progress), task orders with refusal reasons, and the chronicle.
- **Boundary:** the scene never changes game state directly. It calls `SimulationRunner` commands
  and redraws from `CampaignState` every frame (TECHNICAL_SPEC.md §31).

**Verified.** The owner ran the manual test below on macOS (Godot 4.7.2 .NET, .NET 10 SDK) and all
8 steps passed: selection, task orders with walking to work spots, refusal reasons, direct walking
with interruption, accelerated time with eating and drinking, and save/load within a session and
after restarting the game.

**Manual test (TECHNICAL_SPEC.md §26.5).** Run the scene, then check each step:
1. The deck shows six labelled crew, two masts and seven work-spot markers. The camera looks down
   at an angle.
2. Click Diego (forward, near the first mast). His record shows rigging 3 and sailing 3.
3. Click **Repair rigging**. He walks to the "rigging fore" marker, and his label reads
   "Repair rigging". The hold's rope count drops by 1.
4. Select Pedro and click **Patch the hull**. It is dimmed; clicking shows that he lacks
   ship repair skill.
5. Right-click an open plank. The selected person walks there. If they had a task, the chronicle
   records that it stopped.
6. Press 4 (60×). The clock runs about an hour per second. Hunger and thirst bars rise and drop as
   people eat and drink, and the hold's biscuit and water counts fall.
7. Press F5, wait, then press F9. Everyone returns to where they were at the save, mid-task
   progress included.
8. Quit and run again, then press F9. The saved voyage loads, because saves persist between runs.

**Known limitations.**
- Exported builds can't find `data/` yet, because the scene reads it from the repository folder.
  Packaging content for export is a later ticket.
- Only one save slot (the quick save).
- Godot's default theme. The captain's-papers UI language (ART_BIBLE.md §14) needs approval
  before it's built.
- Placeholder names and appearance. Ticket 4c adds saved appearance data.

## Tickets 3a and 3b — CI, structured chronicle, diagnostics

**3a.** `.github/workflows/ci.yml` restores, builds in Release with warnings treated as errors, and
tests `Expedition.sln` on every push and pull request. The Godot project is included; it compiles
against the `Godot.NET.Sdk` package, so CI needs no Godot install.

**3b.**
- **Structured chronicle.** Entries store a kind, the character IDs involved, and named parameters
  (`task`, `cause`, `condition`, `from`, `to`, `reason`, …), never prose.
  `History/ChronicleFormatter` is the single English text layer. It resolves current names, so
  renaming a task or character changes old entries' wording without touching saves.
- **Save format 3, with migrations.** Format-2 and format-1 entries keep their old sentence as
  `legacyText`.
- **Need diagnostics.** `SimulationRunner.ExplainNeed(character, need)` lists what changed hunger,
  thirst, fatigue, stress or health over the last day, by source: daily life, a task, an item eaten,
  starvation, dehydration, an injury. The Godot record panel shows a "Why (last day)" section. The
  ledger is a diagnostic and isn't saved, so it starts empty after a load.
- **Missing tests from TECHNICAL_SPEC.md §38:**
  - one-tick and one-hour stepping give identical saves over 48 hours (#15);
  - a 90-day voyage with ample provisions keeps everyone alive, and stores only fall (#2);
  - a 90-day voyage that runs dry ends in six recorded dehydration deaths, each after the shortage
    entry.

165 tests pass.

## Ticket 3c — Attributes, random streams, campaign identity, captain

See D-008.
- `data/attributes.json` defines the four physical attributes, and `skills.json` no longer lists
  them.
- `task.pump_bilge` uses strength and `task.repair_rigging` uses coordination.
- The test deck crew have varied physiques. Captain Álvaro is appointed captain, and Godot marks
  him "(captain)" and selects him first.
- The character record shows a "Physique" line.
- Save format 4, with a tested upgrade from format 3.
- Loading now also rejects unknown skill and attribute IDs.

186 tests pass.

## Ticket 3d — Relationships, traits and conditions

See D-009.
- **Test deck crew:** every crew member has traits, and the crew start with five relationships, for
  example Diego trusts the captain (+30, "sailed under him before").
- **Godot record panel:** shows traits, injury names and "Views of others" with the latest reason.
- **Chronicle wording:** uses condition names ("suffered bruising"), and relationship entries read
  like "Diego's trust in Álvaro rose by 30 (sailed under him before)".
- **Save format 5.**

207 tests pass.

## Ticket 3e — Task queue, priority and resume

See D-010.
- **Order buttons in Godot now queue:** an idle person starts at once, a busy one does it next.
- **The "Urgent" checkbox** pauses current work for the new order. The paused work resumes
  afterwards with its progress.
- **The record panel** lists the queue, with each item's priority and how far paused work got, and
  has a "Clear queued orders" button.
- **Save format 6.**

229 tests pass. All of the spec-alignment tickets (3a–3e) are now done.

## Ticket 4c — Appearance record

See D-011.
- **Appearances:** generated for the test-deck crew and saved.
- **Godot figures:** built from appearance. Height and build set the body. Skin tone, hair (or none
  if bald), a beard if any, and clothing in the main dye with a belt in the accent dye.
- **Record panel:** shows a "Looks:" description.
- **Save format 7.**

244 tests pass.

## P0 complete — next is P1 (vertical slice)

All P0 and spec-alignment tickets are done (TECHNICAL_SPEC.md §35; SPEC_AUDIT.md gaps G1–G11).
Remaining audit items, which belong to P1:
- truth vs knowledge (G12, recruitment);
- item instances (G13, spoilage and damage);
- health severity (G14, medical crisis).

**Setting direction decided (D-012):** fictional, but clearly inspired by history, with fictional
but specific Indigenous peoples. Before Ticket 5, the owner approves a short setting outline
(`docs/SETTING.md`):
- port, crown and sponsor;
- the charter goal;
- the ship;
- the destination coast and its peoples;
- the identity fields candidates need (sex, languages with levels, status).


## Ticket 5a — Identity: sex, languages, social status

- **Sex:** `Character.Sex` is Male, Female, or Unrecorded for older saves.
- **Languages:** `Character.Languages` on a 0–5 scale, from `data/languages.json`: Castrenan, Lusaran,
  Brennish, Ligurese, Ahuneri and Tesiwa.
- **Status:** `Character.StatusId` from `data/statuses.json`, each with a typical monthly wage in
  sueldos as the starting point for negotiation.
- **Beards:** appearance options can be limited to one sex (`onlyFor`), so women are never generated
  with beards.
- **Content loader:** now reads enum names such as "Male" in data files.
- **Test deck crew:** use the setting's origins (Almara, La Bocana, Lusara), and have sex, status and
  languages.
- **Godot record:** shows sex, standing and languages.
- **Save format 8:** older characters load with identity unrecorded.

256 tests pass.

## Ticket 5b — Captain's knowledge: truth vs what is known

Closes audit gap G12.
- **Knowledge book:** `CampaignState.Knowledge` keeps every fact the captain learns about a person:
  topic (skill, attribute, language or trait), level, source, informant and time. Sources, weakest
  first: rumour, the person's own claim, a reference, observation at work, a test.
- **Belief:** the most reliable source wins, and among equal sources the newest. Disagreeing
  evidence marks the belief "disputed". Repeating the same fact is not recorded again.
- **Truth is untouched:** learning never changes a character, and a claim can be a lie.
- **Observation:** finishing a task records the worker's real skill and attribute for that task.
- **Text:** `History/KnowledgeText` describes beliefs ("Rigging 3 (seen at work)").
- **Godot record:** a "Captain knows" line. It fills in as the crew finish work.
- **Save format 9:** older saves load knowing nothing; tampered knowledge is rejected.

274 tests pass.

## UI pass (after 5b, owner feedback "UI hard to use")

- **Fits the window:** the game scales its interface to the window (1280×720 base), and the selected
  character's panel is split into scrolling tabs (Orders, Record, Needs) so nothing falls off the
  bottom. The name, current work and need bars stay visible above the tabs.
- **Keys keep working:** buttons no longer take keyboard focus, so Space pauses and Tab cycles crew
  even after clicking a button (before, Space re-clicked the last button).
- **Refusals are visible:** a dimmed order says why on the button itself, not only in a tooltip.
- **Mac shortcuts:** Cmd+S / Cmd+L save and load. A controls reminder sits under the hold.
- **Opens maximized:** a 1280×720-pixel window looked tiny on a Retina screen, so the game now opens
  maximized and the interface scales up to fill it.

## Standing duties and self-care (owner feedback "the crew needs automating")

D-014. Crew now keep themselves busy without clicking.
- **Standing duties:** each crew member has a list of tasks they take up in turn whenever they are
  idle with no orders. A duty that cannot start (no rope, spot taken) is skipped for the next one.
  The test deck crew start with sensible duties (watch, rigging, cooking, pumping, hull).
- **Self-care:** an idle crew member at fatigue 70 or more rests on their own (`restAtFatigue` in
  `data/simulation_rules.json`; remove it to turn self-care off). Eating and drinking were already
  automatic.
- **Orders come first:** an order takes over from duty work or resting at once; the interrupted
  work waits behind the player's orders and resumes later with its progress kept. If an order cannot
  start yet (too tired to work), it waits instead of being refused.
- **Explained:** activities read "Stand watch (duty)" or "Rest (tired)", and an idle crew member with
  duties says why none can start.
- **Godot:** a new **Duties** tab with a checkbox per task.
- **Save format 10:** each task records why it is being done; duties are saved. Older saves load
  with all work counted as ordered and no duties.

296 tests pass, including three days of the crew running on duties alone with nobody exhausted.

## V1 — The world around you (owner: "just watching pills move on a boxy ship")

D-015. Visual only: the simulation is unchanged except clearer refusal messages.
- **Sea:** a shader ocean (`game/Shaders/ocean.gdshader`): rolling swells, ripples, whitecaps, a
  wake along the hull and astern, drifting cloud shadows and haze. It follows the camera, so it never
  ends, and takes a sea state (calm to heavy) ready for weather in V2.
- **Day and night** from the simulation clock (`SeaAndSky`): the sun crosses the sky, dusk warms the
  light, and night is blue moonlight with lanterns on the masts and stern and the galley fire.
- **The ship** (`ShipView`, replaces `DeckView`): a curved hull with a wale and pale bottom that hugs
  the walkable deck, bulwarks with sheer, planked deck, raised stern with cabin and lantern, bowsprit,
  fore and main masts with yards and bellied square sails, a lateen mizzen, shrouds and stays, a
  pennant, hatch gratings, lashed casks, and fittings at each work spot (pump, galley fire box, rope
  coils, planks and tar). Sails fade at working zoom so they never hide the crew; zoom out to see
  them whole.
- **People** (`CrewFigure`): legs, body, arms, head, hair and beard from the appearance record;
  officers wear a brimmed hat, sailors a cap. They walk with a stride, work with their arms, sit to
  rest, lie down when hurt, and turn to face where they are going. Seabirds wheel overhead.
- **Interface:** smaller text, see-through panels, a compact hold line, a foldable chronicle and a
  "Controls" button instead of a permanent help block.
- **Messages:** refusals name skills and items ("needs Rigging 1", "Rope ×2"), not IDs.
- **Developer screenshots:** `-- --screenshot=PATH --advance=MINUTES --zoom=N --seed=N` renders the
  scene without a person at the screen, so changes can be checked before the owner tests them.
  Screenshots in `docs/screenshots/`.

296 tests pass. Rendered and checked with Godot 4.7.2 (Compatibility renderer, software GPU) at day,
dusk and night and at three zoom levels; the owner's Mac uses Forward+, which may look a little
different.

## V2 — The ship in danger

D-016. The crossing now has stakes, and the crew's work has a reason.
- **The voyage** (`Voyage/VoyageState`, rules in `data/voyage.json`): the *Garza* sails 560 leagues
  for the Ember Coast. Each minute the hull leaks into the bilge (faster when the hull is damaged and
  in heavy weather), the hull and rigging wear, and she makes way at a speed set by the wind, the
  rigging, and whether anyone is standing watch.
- **Weather** changes on its own random stream: calm, fair, fresh breeze, gale, storm, each with its
  own wind, sea, leaks and wear. Gales and storms can strike the hull and open a seam.
- **Arrive or founder:** the destination sighted moves the campaign to Landfall; a full bilge sinks
  her and moves it to Shipwreck survival.
- **Work matters:** pumping lowers the bilge while it goes on, patching the hull and repairing the
  rigging restore them (using oakum, planks and rope, which run out), standing watch keeps her
  sailing, and a hot meal eases everyone's stress. The deck now has two pumps, as the *Garza* does.
- **Duties respond to need, in priority order** (amends D-014): pump when the bilge is above 15%,
  repair below 88%, patch below 85%, cook at 10:00 and 17:00. A crew member on routine duty drops it
  for a more important one that becomes needed (the watch goes to the pumps when the bilge rises).
- **Tighter stores:** about 24 days of water and 26 of food for a crossing of 20–30 days.
- **Godot:** a voyage bar (distance and estimate, wind, watch, hull, bilge, rigging, water, food,
  amber and red when in danger); the game pauses with a banner when a gale or storm breaks, the hull
  is struck, the bilge passes 50% or 80%, someone is hurt or dies, or stores run out; an end screen
  for landfall or foundering, with a new voyage button. Weather shows: rougher sea, overcast light,
  rain, lightning, topsails furled and courses reefed; she rides lower as the bilge fills and sinks
  when she founders. Days are counted from 1.
- **Save format 11.** Older saves load without a voyage; duties lose their rotation pointer.

Balance (30 seeds, crew left on their default duties): 24 arrive in 20–32 days, often with water
nearly gone, and 6 founder after a run of storms uses up the planks and oakum. A captain who puts
more hands on the pumps in a storm and saves materials should do better. 319 tests pass, including
a ten-seed balance check.

## V3 — Things happen

D-017. The voyage now throws situations at the captain, and the choices last.
- **Events as data** (`data/events/voyage.json`, `Events/EventDefinition`): 13 to start. Man
  overboard, foul water, fever, a knife drawn between two crew who dislike each other, a Brennish
  sail to windward, the Lusaran questioning your reckoning, the casks running low, a rain squall,
  dolphins, a crewman asking for a word when the strain is high, the feast of Saint Brendo, Saint
  Elmo's fire in a storm, and a choked pump. Each has a trigger (chance per day, weather, day,
  water, bilge, rationing, once or with a cool-down) and picks who it is about (anyone, the
  strained, people from one place, or two who think badly of each other).
- **Choices** may need something from the hold (wine, biscuit), and may hang on a skill roll by the
  best person for it, the subject, or the captain; the odds are shown in words ("likely", "a long
  shot"). Outcomes change supplies, the ship, distance, health and conditions, stress and fatigue,
  relationships, and who is still aboard (a man lost overboard is Missing).
- **Pacing:** checked hourly at sea on the events stream; at most one waiting at a time and none
  within 8 hours of another. About 8–12 per crossing.
- **Short rations** (from an event or the voyage bar): water once a day at dawn, food once at
  noon. The stores last longer, but hunger and thirst climb and stress rises.
- **Stress matters:** at 70 or more people work 15% slower; strained people quarrel and speak up.
- **Godot:** an event window pauses the game with the story and the choices; after choosing it shows
  what happened, then carries on. The chronicle records every decision.
- **Save format 12:** the waiting event, event history and rations are saved.

Balance (30 seeds, a captain choosing at random): 22 arrive; fewer than one person lost per
voyage. 339 tests pass.

## After V3 — Faster time, sail orders, repairs you can see

Owner playtest: "need faster sim speed", and storms sank the ship after the repair stores ran out.
- **Speeds 240× and 720×** (keys 5 and 6): a day in 6 or 2 seconds. Events and danger still pause.
- **Sail orders** on the voyage bar (D-018): Press on, Normal sail, Heave to. The sails on screen
  follow: everything set when pressing on, bare yards when hove to.
- **Repairs left** on the voyage bar: hull patches and rigging repairs the hold can still pay for.
- **Repair duties wait for real damage** (75%), so materials last.
- **Save format 13** (sail plan). 342 tests pass.

## V4 — Choose who comes

D-019. A new game starts at La Bocana with Captain Álvaro alone aboard, 7,000 sueldos from the
Company, the Company's basic stores, and the convoy due to sail.
- **Twelve candidates** (`data/recruitment.json`) from the setting: Duarte Velho the Lusaran pilot,
  Ilario Pons the barber-surgeon fleeing debts, old Bartolo the carpenter, Leonor Alarcón the
  Company's widowed clerk, Tomé Arcos the convict, Ximena Durán the interpreter, Gil the cooper, Nuño
  the cook, Rodrigo the topman, Wat Harrow the Brennish seaman, young Pascual, and Don Fernán the
  disgraced gentleman (who pays for his passage). Each has true skills, attributes, traits and
  languages, and separately what they claim and what someone on the waterfront says of them. Some
  boast, some undersell themselves, one hides what he was.
- **Inquiries:** talking is free (their claims); asking around and putting someone to the test cost
  one of six inquiries (a reference, or the true level). The proud refuse to be tested.
- **Offers:** each asks an advance; haggling once can take a fifth off or add a tenth, and a proud
  candidate may walk away. Seven berths; at least four hands to sail.
- **The chandler** sells water, biscuit, pork, wine, rope, oakum and planks from the same purse. The
  screen shows how long the stores will last the crew chosen so far.
- **Aboard,** hired people keep what the captain learned as knowledge (claims, references, tests),
  get duties to suit their real skills, and their true skills come out as they work. The crew
  panel now shows skills and character as the captain knows them, not the hidden truth.
- **Also fixed:** the voyage ends when all hands are lost, and command passes to the most respected
  crew member if the captain dies (placeholder until succession rules are approved).
- **Save format 14** (hiring in progress).

Balance (8 seeds, a sensible captain choosing events at random): a well-found ship with a balanced
crew and bought stores arrives 5–8 times; the same crew with only the Company's stores, never.
363 tests pass. Developer options: `--testdeck` starts the old test scenario directly; `--hire=`
hires, talks (`talk:id`), asks (`ask:id`) and sails (`sail`) for screenshots.

## R1 — The living port, simulated

D-022. Replaces V4's hiring menu underneath; the playable scene is R2.
- **The Bocana waterfront** (`data/locations/bocana.json`, 60 × 36 m):
  - the quay with the gangway;
  - the square with stalls and a well;
  - the Chamber office;
  - the Company counting house;
  - the *Three Anchors*;
  - the chapel;
  - the chandler;
  - the carpenter's yard;
  - the alley;
  - homes on the back street.

  Spots say what can be done where, and whether it is indoors (out of sight and reach).
- **28 people** (`data/port/bocana.json`): the 12 candidates and 16 townsfolk. Each has a true
  record, a home and a daily schedule (`data/tasks/port.json`).
- **Everyone is a real campaign character.** Locals (`Membership.Local`) live by their schedule,
  with no needs simulated. Hiring makes them crew. Only crew count for events, rations and command.
- **Watching is investigating:**
  - Work finished where the captain can see it (14 m, not indoors) shows the true skill or attribute.
  - A demonstration the captain asks for and watches counts as a test.
  - Secrets are seen, not told: Velho meeting the Brennish trader after dark; Pons cornered by the
    moneylender.
- **Who you ask matters:** each reference belongs to a person (the bosun knows Rodrigo, the gaoler
  knows Tomé); the landlady knows gossip; others shrug. You must walk over to speak.
- **Offers are weighed:**
  - Below someone's hidden lowest they counter, and a lowball offer insults them.
  - Rodrigo will not sail with the Brennish man.
  - A captain they distrust is refused.
  - The proud refuse demonstrations, and take offence.
- **Time is the limit:** Velho takes another ship at 14:00 on day 2, Pons flees that night, and
  Pascual's mother takes him home before dawn on day 3. The chandler's stock sells to others each day.
  The convoy sails at dawn on day 3, with a warning 12 hours before; staying costs 300 sueldos per
  half day.
- **Sailing** takes the crew and captain aboard, leaves the locals behind unsimulated, and starts
  the voyage.
- **Save format 15** (port state, membership). Format 14 saves lose hiring in progress.

366 tests pass, including every hour of a port day leaving at most one person without a place to be.


## R2a — The port, greybox and playable

D-023, following `PORT_EXPERIENCE.md` §23 steps 1, 2, 7 and 8, with steps 3–5 started.
- **Five districts** (`tools/gen_bocana_map.py` → `data/locations/bocana.json`, 80 × 46 m):
  - **customs:** the Chamber and the Company house on a terrace over the harbour, with a
    balustrade and two flights of steps;
  - **tavern and chapel** on the square, the alley between;
  - **market:** stalls, the well, the chandler's warehouse;
  - **workshops:** ropewalk, cooperage, smithy, sail loft, and the shipwright's yard with a hull on
    the stocks;
  - **quayside:** vaults under the terrace, two piers, boats, a merchantman at anchor.
- **The *Garza* lies alongside** as the landmark, sails furled. Hired crew go aboard and are seen on
  her deck.
- **Watchable time:** walking in port is 2.5 m per game minute; port speeds are Pause, Watch (half a
  game minute a second), 2×, 10×, 60× and 240×.
- **Work loops** (tasks chain with `then`; `carries` names the load):
  - dockers shoulder crates at the vaults and carry them down the pier to the lighter;
  - the fishwife lands the catch, carries the basket to the stalls and sells;
  - the shipwright saws at the bench, fetches timber, and fits planks to the hull;
  - the ropemaker spins yarn and walks it down the ropewalk.

  Spots have semantic kinds (`SitPoint`, `CarryPickup`, `HammerStation`, …) that choose the pose.
  New poses: carrying, hammering, sawing, sitting, praying, drinking, talking, roping, watching.
- **Three craftspeople join:** Benito the smith, Marta the ropemaker and Old Simón the sailmaker
  (31 people).
- **The person card** shows only what the captain knows, in words, with its source and how far to
  trust it: for example "Navigation: a master — their own claim — unverified", or "Rigging: skilled —
  vouched for by Andrés — fairly sure". Strangers are "a docker" until you speak or hear of them.
  Whether someone wants work is learned by talking.
- **Acting in the world:** Talk, Ask about (people you know by name), Ask to show (what they claim)
  and Offer a berth. The captain walks over first, following someone who moves, and gives up after
  three game hours or if they go indoors.
- **The port bar:** time to the convoy, purse, berths, a stores forecast, the chandler's list, and
  Set sail. Sailing takes the crew to sea and hides the port.
- **Card text:** the chronicle no longer lower-cases names in things noticed.

373 tests pass. New: four work loops, carried loads, loops giving way to the schedule, and the
card's words and provenance.

Screenshots: `docs/screenshots/r2a-waterfront.jpg`, `r2a-person-card.jpg`, `r2a-dockers.jpg`,
`r2a-dusk.jpg`, `r2a-hired-aboard.jpg`.

**Not yet** (later R2 steps):
- leads and a journal;
- Tier C crowd;
- the ship changing as she is prepared, and cargo shown;
- sound, port weather and ambient events;
- recognition, and the departure sequence.

Roof occlusion is not handled: the captain's name shows through roofs.

**Manual test (macOS):**
1. `git pull`, open the project in Godot, Build, then Cmd+B. The game opens on the quay beside the
   *Garza*, at Watch speed.
2. Watch for a minute: dockers carry crates from the vaults down the pier; people walk to their work.
3. Right-click up the steps onto the terrace; the camera follows. Velho is at the chart table in the
   morning.
4. Left-click Velho. The card says "A pilot" until you talk. Click **Talk**: the captain walks over,
   and Velho's pitch appears. The card lists his claims as unverified.
5. Right-click to the docks and click Andrés (a docker, after 13:00). **Ask about** Rodrigo: it
   becomes "vouched for by Andrés".
6. Open **Chandler's list** and buy water; the stores forecast rises.
7. Offer berths until four are signed (try Gil, Wat, Tomé and Pascual), then **Set sail**. The port
   disappears and the voyage starts.

## R2b — Found by word of mouth

`PORT_EXPERIENCE.md` §9 ("discover candidates through leads, not a menu") and §7 (work loops).
- **Leads** (`leads` on people and `notices` on the port in `data/port/bocana.json`):
  - Talking with someone who sees a lot of the waterfront points you to people worth finding, and
    to where they are said to be.
  - Givers: Inés the landlady (Velho, Rodrigo, Nuño), Corbalán the chandler (Bartolo, Gil), Father
    Anselmo (Pascual), Mateo the guard (Tomé), Hernando (Ximena), Catalina (Pons), and Varzi
    (Leonor).
  - The Chamber's notice board on the terrace must be read in person ("Read the notices" walks the
    captain there). It lists Wat, Don Fernán, the convict's pardon, and a pilot.
  - Every candidate can be found through at least one lead (a test checks this).
- **A lead is a pointer, not knowledge.** It tells you nothing of what someone can do, and "where"
  is hearsay. The person card says "Could this be the one you heard of?" with the lead and who
  gave it.
- **The journal:** each lead with what was said, where to look, from whom and when, and whether
  they have been found, signed on, or gone. Leads are chronicled.
- **New work loops:**
  - Gil makes casks and rolls them to the chandler's yard;
  - Old Simón mends canvas and carries it to the *Garza*;
  - Velho looks the *Garza* over in the early afternoon.
- **Save format 16** (the journal). Format 15 ports load with an empty one. (Now 17: see R2c.)

380 tests pass.

**Manual test (macOS):**
1. Walk to the Three Anchors after 09:00 and talk with Inés. She names three people.
2. Open **Journal** to see the leads.
3. Click **Read the notices**. The captain walks up to the terrace board and four notices appear.
4. Find Velho at the chart table in the morning. His card shows the lead before you talk to him.

## VP-0 — Performance foundation (port visual pass, step 0)

- **`MeshBatcher`** merges static scenery into one mesh per material per 16 m chunk. Flat colours
  share one vertex-coloured material. `PortView` and `ShipView` build through it.
- **Figures** merge their rigid parts per joint and use low-segment meshes.
- **Measured** with `--perf` at gameplay zoom in port (the scene looks the same):

  | | Before | After |
  |---|---|---|
  | Draw calls | ~1,900 | ~400 |
  | Triangles | ~1.1 M | ~270 k |
  | Nodes | ~3,300 | ~960 |

  At sea it is ~230 draw calls.

## UI-1 — The interface, world first (UI_UX_DESIGN_SYSTEM.md, D-026)

- **Design tokens** (`UiTokens.cs`):
  - palette, type roles, spacing scale, minimum hit size, transition time;
  - semantic severities, each with its own icon as well as its colour;
  - a theme giving every control its default, hover, pressed, focus and disabled states.
- **Persistent HUD** (`Hud.cs`):
  - **top left:** day, time and speed (current speed highlighted; tooltips explain each);
  - **top right:** the phase's few gauges as chips: departure, crew minimum and purse in port;
    wind, the ship's worst system, water, food and the watch at sea. Sail orders stay one click away
    at sea;
  - **bottom left:** the selected person (task, progress, concerns in words) and the current
    objective;
  - **bottom right:** People, Expedition, Cargo, Ship, Chart (with R4b), Chronicle and ☰.
- **Management drawer**, one screen at a time (P, X, C, H, L; Esc backs out):
  - **People:** the roster and the record, with orders, duties and needs;
  - **Expedition:** objectives (now, expedition, charter), the journal, notices, Set sail;
  - **Cargo:** a manifest by category with estimated ranges, and the chandler in port;
  - **Ship:** condition with causes, and rations;
  - **Chronicle:** newest first, with filters, and an unread count on its button.
- **Alerts:**
  - severity, cause, who it concerns and when, with Focus;
  - critical alerts stay until dismissed; the rest fade;
  - only the categories ticked in the menu pause the game (injury and death, storms, damage and the
    bilge, shortages);
  - Carry on resumes at no more than 10× (§16).
- **Notices** fade on their own; the chronicle no longer sits on the screen.
- **What the captain knows:**
  - beliefs use the shared vocabulary (Rumored, Claimed, Vouched for, Observed, Confirmed) with
    Confidence Poor, Low, Fair, High or Very High, the source and the date;
  - skill levels are words ("experienced"), not numbers;
  - **fixed a leak:** the crew record printed everyone's true physique and languages; it now shows
    only what is known. Shipmates' feelings are shown in words.
- **Set sail** asks for confirmation when hands or stores fall short (§43).
- **Saves** are reported only after the file is written, with the day and place (§46).
- **Menu:** interface size (90–130%) and auto-pause choices.
- `--panel=people|expedition|cargo|ship|chronicle` opens a screen, for screenshots.

Screenshots: `docs/screenshots/ui-a-port-exploration.jpg`, `ui-b-recruit-inspection.jpg`,
`ui-c-voyage-storm.jpg`, `ui-cargo-720p.jpg`.

**Not yet:**
- ship management with a diagram (R4e);
- the chart (R4b);
- settlement screens (R6);
- the crew screen's sorting and filters;
- drawn icons in place of glyphs;
- UI sounds;
- the workflow usability tests (§60).

**Manual test (macOS):**
1. Start: only the clock, the status chips, the captain and the objective, and the navigation are
   showing.
2. Press X: the objectives and the journal. Press C: the chandler and the manifest. Esc closes.
3. Talk to Velho: his card shows claims as "Claimed master · Confidence: Low · Source: their own
   word".
4. Sign four hands, set sail, and run at 240×. When a storm or a leak comes, an alert appears with
   Focus, the game pauses, and Carry on resumes at 10×.

## VP-1 — Chandler's Quay, the first representative block

`PORT_VISUAL_PASS.md` §5 steps 2–8; results and the definition-of-done check are in its §6.
- **Lighting:**
  - presets by hour (dawn, morning, midday, afternoon, evening, dusk, night);
  - SSAO, haze and contact shadows;
  - the sun's day widened to about 05:30–19:30.
- **Ground:** wear along real routes and loops, grime, damp, a gutter, and litter by trade.
- **People:**
  - spots face their work;
  - clusters: seller and customer, gossip at the well, porters resting, drinkers across a table,
    the carpenter and a helper at a new repair station by the *Garza*;
  - poses: lifting, inspecting, leaning, gutting, rolling a cask, steadying.
- **Kits:**
  - chandler, tavern (the green branch over the door), cooperage;
  - stalls by trade with undyed or faded, sagging awnings;
  - post-and-rail fences, the cart's shafts and chain, lashed crates.
- **The *Garza*:** moored with lines, gangway, fenders and ensign; sails furled.
- **Depth:** shear-legs over the lighter; foreground pieces fade over the captain or selection.
- **Rain look:** `--weather=rain` (Shift+R in debug builds).
- **Sound zones** (generated): surf, market, workshop, tavern, street, rain.
- **Fixed:** washed-out colours in Forward+ (vertex colours are sRGB).

383 tests pass. Screenshots: `docs/screenshots/vp-before-after-*.jpg`, `vp7-*.jpg`.

**Manual test (macOS):**
1. `git pull`, Build, Cmd+B.
2. Walk east from the gangway: the repair station by the *Garza*, the casks and dockers, the
   shear-legs over the lighter, the fish stalls and the chandler.
3. Run to dusk at 60×: long warm light, then the lanterns.
4. Listen near the quay, the stalls and the yard: the sound changes.
5. Report the frame rate (Godot's Debugger ▸ Monitors), and how it compares with before.

## R2c — The crowd, the harbour, and the Garza taking shape

PORT_EXPERIENCE §5, §6, §18–19; VISION_ALIGNMENT §5.2.

**Tier C crowd** (`PortCrowd`, `CrowdMesh`, `crowd.gdshader`):
- passers-by who each make a trip with a reason: in from a street end or out of a door, sometimes
  stopping at a stall, the well or the notice board, then off to another door, street or pier;
- about a quarter carry a sack;
- about 40 at the morning market, a handful at night;
- the whole crowd is one mesh drawn in one batch and animated in the shader: two draw calls with its
  shadows;
- they walk at the port's pace in game time, so they stop when paused;
- never characters: no names, no clicks, nothing saved.

**The harbour** (`HarbourView`):
- a nao, a caravel and a fishing caravel at anchor, swinging and rolling a little, with hands working
  on deck and stern lanterns lit at night;
- two boats rowing between the piers and the ships by day.

**The Garza taking shape** (`ShipPreparation` in the simulation, `PreparationView` in the game):
- **Stages** come from the crew signed on, the lots bought and the hour, with thresholds in
  `data/port` → `preparation`:
  - awaiting the expedition: a tar pot and a loose coil;
  - inspection under way: the hatch open, a lantern;
  - loading;
  - taking shape: a whip from the main yard, a sea chest or bundle for each hand;
  - ready for sea: hatch closed, deck load under lashed tarpaulins, coils at the masts;
  - departure morning: from first light, people gather on the quay to see her off.
- **Stores** bought at the chandler are counted in the hold at once, as before. They are drawn on the
  quay by the gangway and cross to the deck over 3 hours. They lie on deck for 5 more hours while
  they are struck below.
- **Packages:** each item says what it is packed in (`items.json` → `packedIn`, `perPackage`): a
  cask of 40 litres of water, a sack of 25 rations, a coil of rope.
- **The Expedition panel** names the stage and what is still on the quay.

**Save format 17** records purchases and when they were made. Older saves load with everything
already aboard.

**Developer options:** `--hire=N` signs on the first N who want a berth; `--buy=0,0,4` buys those
lots; `--after=MINUTES` then lets time pass.

**Tests:** 388 pass, including:
- the Company's stores;
- stores waiting on the quay, then on deck, then below;
- the stage thresholds;
- purchases saved and old saves migrated;
- every item packed.

**Screenshots:** `docs/screenshots/r2c-{market,loading,ready-day,departure,harbour}.jpg`.

**Limits:**
- the crowd walks through named people and the captain rather than around them;
- only some carry, and they do not react to rain yet (R2d sets `Density`);
- the anchored ships are greybox hulls;
- no livestock;
- repairs are not yet visible on the hull.

**Manual test (macOS):**
1. `git pull`, Build, Cmd+B.
2. At Watch speed on the market, see passers-by stop at stalls and leave by streets and doors.
3. Buy water and rope at the chandler (C): casks and coils appear by the gangway, cross to the
   deck, then disappear below over the next hours at 60×.
4. Sign on hands: sea chests appear forward.
5. Run to day 3 at dawn: people on the quay to see her off.
6. Pan south to see the ships at anchor and the boats rowing.

## A-1 — First asset pass: real materials and recorded sounds

The asset half of the hybrid pipeline (D-021), now that the CC0 hosts are reachable.

**Where the assets come from:**
- `tools/fetch_assets.py` fetches everything listed in `game/Assets/assets.json` and writes
  `game/Assets/CREDITS.md`, giving each file's author, source page and licence.
- All of it is CC0: six Poly Haven textures at 1K and 38 Kenney sound files.
- `game/Assets` is about 11 MB.
- Textures are imported VRAM-compressed with mipmaps; the `.import` settings are committed.

**One scenery material** (`SceneryMaterial`, `scenery.gdshader`):
- Every built box keeps its palette colour. Plaster, stone and timber add photographed detail:
  variation, relief and roughness.
- Each photograph is divided by its own average colour, so the art direction keeps the colour.
- Textures are mapped in world space from the three axes, at their true size in metres; nothing
  needs UVs.
- The surface follows from the colour: pale and warm is plaster, grey is stone, brown is timber, and
  cloth stays flat. The choice rides in the vertex alpha, so draw calls are unchanged (about 560 at
  gameplay zoom, as before).
- The Garza's hull and the ships at anchor use it too.

**Ground, roofs and deck:**
- **Ground:** photographed cobbles in the streets and sea-worn blocks on the quay. About half the
  photograph's contrast is kept, and a turned second sample hides the repeat. The worn routes, grime,
  damp, litter and gutter from VP-1 sit on top.
- **Roofs:** photographed clay tiles with light lichen.
- **Deck:** photographed planks running fore and aft.
- **Rain:** wets all of them.

**Recorded sounds of work** (`PortSound`), each played when and where the work is:
- mallets, planks and the adze for each carpenter or cooper at work;
- the anvil while the smith works;
- cups set down in the tavern;
- coins at the market;
- the fishwife's knife;
- the Garza's timbers creaking;
- the chapel bell striking the hour by day (not at the faster speeds).

The surf, crowd, wind and rain beds were still generated here; A-2 replaces them with recordings.

**Screenshots:** `docs/screenshots/a1-before-after-market.jpg`, `a1-rain-dusk-yard.jpg`,
`a1-close.jpg`.

**Limits:**
- figures are still primitive: the rigged human base needs Quaternius, through itch.io, which is
  not yet allowed;
- props are still built from code;
- ambientCG needs `acg-download.struffelproductions.com` allowed;
- the ground at far zoom is a little paler and greyer than the old procedural stone.

**Manual test (macOS):**
1. `git pull`. If it refuses because of untracked `.uid` files, delete those files and pull again.
2. Open the project once in the Godot editor so it imports the new textures, then Build and Cmd+B.
3. Zoom in on the market: the cobbles, plaster, cask staves and roof tiles are photographed.
4. Walk to the yard and listen: mallets and the anvil.
5. At the top of an hour by day, the chapel bell.
6. Report the frame rate.

## R2d — Port weather, its reactions, and the small scenes of the port

PORT_EXPERIENCE §12, §14; VISION_ALIGNMENT §5.2.

**Weather, simulated and saved (save format 18):**
- The kinds of weather are in `data/port/bocana.json` → `weather`: fair, breezy, overcast, rain,
  and fog that comes only in the morning.
- Each kind lasts a range of hours and is followed by another, by weight. The rolls come from the
  campaign's weather stream, so a campaign's weather is the same every time it is played.
- **Rain:** tasks with an `inRain` alternative give way to sheltering: gossip at the well, shopping,
  children's play, watching the harbour, mending nets and sails out in the open.
  - People wait under the eaves along the square, under the ropewalk's roof and in the mouths of the
    vaults: 13 new shelter spots, where they lean and look out.
  - They go back to what they were doing when it stops.
  - If every shelter spot is taken, the rest carry on in the wet.
  - Dockers, fishermen and craftsmen work on.
- **Fog:** the captain sees less than half as far, so people further off are out of sight.
- **Older saves:** they start fair, and the weather changes on the next minute.
- **Developer:** `--weather=rain|fog|breezy|overcast|fair` sets it. In debug builds Shift+R toggles
  rain.

**Weather in the scene:**
- **Rain:** wet stone and timber dry slowly afterwards. Stallholders' goods go under cloth, and the
  stores waiting on the quay and on the Garza's deck go under tarpaulins. The crowd thins to about a
  third.
- **Fog:** pale haze closes in and the far water is gone. The ships at anchor vanish into it, the
  furthest first. Lanterns are lit, and the boats stay at the piers.
- **Wind:** the wind sound rises.
- **Interface:** a weather chip sits among the status chips, and a line appears when the weather
  changes ("Rain sweeps in off the sea…").

**Ten small scenes** (`AmbientScenes`; data in `ambient`):
- the scenes: sailors arguing outside the tavern; a porter's sack splitting; a procession behind its
  cross; a funeral to the burial ground; a boy chased across the market; a cart losing a wheel; a
  farewell on the west pier; a drunk; a dispute on the terrace; children round the well;
- each has its place, hours and weather;
- one plays only where the camera is looking, while time runs at Watch or 2×, about every 20–50
  seconds, and not again for five minutes;
- each lasts 10–30 seconds, with crowd figures, a few props and recorded sounds (bell, coins, cups,
  creaks);
- they give nothing and change nothing; a line says what is happening;
- developer options `--scene=ambient.procession` and `--shotdelay=SECONDS` stage one for a
  screenshot.

**Tests:** 394 pass, including:
- the weather is deterministic and changes;
- fog comes only in the morning;
- rain sends gossipers to shelter and back;
- fog hides people further off;
- the weather is saved and migrated;
- the scene data.

**Screenshots:** `docs/screenshots/r2d-rain.jpg`, `r2d-fog.jpg`, `r2d-scenes.jpg`.

**Limits:**
- wind moves no flags or canvas yet;
- no approaching-storm sequence;
- scenes have no lasting consequences if the player gets involved (the brief's "preferred");
- scene figures walk through other people;
- the funeral bier is carried by its front bearer's position, so it can lag a little round corners.

**Manual test (macOS):**
1. `git pull`, Build, Cmd+B.
2. Run at 10× until the weather chip says Rain (or use Shift+R in a debug build): the square empties
   into doorways and the vault arches, and the stalls are covered.
3. Back at Watch speed near the chapel, the square or the tavern, wait for a scene and its line.
4. Start a new campaign and run to dawn on day 2 a few times with different seeds to catch the fog.

## A-2 — Recorded beds for the port

D-021; PORT_VISUAL_PASS §4 E.

**The beds are recordings now** (`PortSound`), each a CC0 Freesound recording looped under the
existing zones and volumes:
- **surf:** waves and splashes against rocks at Málaga, with distant voices;
- **market:** Spanish-speaking people talking and laughing outdoors;
- **tavern:** a busy small-town bar;
- **wind (street):** strong, steady wind with faint creaking, rising with the breeze;
- **rain:** rain heard from under a stone bridge (Freesound 467169).

**Levels, measured in the game** (recorded with Godot's movie writer): rain on the quay RMS 0.05, the
market by day 0.04, the tavern at night 0.08 with cups peaking at 0.66. The recordings are lifted 6 dB
(the tavern 1 dB) to sit with the work sounds.

**How they are made** (`tools/fetch_assets.py`, entries in `assets.json`):
- the script fetches each Freesound high-quality preview by ID and refuses anything that is not CC0;
- each is levelled to −24 LUFS and its last 3 seconds are crossfaded into its start, so the beds sit
  together and loop without a seam (needs ffmpeg);
- `CREDITS.md` names each recording, its author and its page;
- each loop starts at a random point;
- if a file is missing, its bed falls back to the generated noise.

**Limits:**
- the beds were chosen from their descriptions; their levels are checked, but nobody has listened for
  anything out of period: the tavern recording is a modern bar and may carry a word of English or a
  poker chip; the rain was recorded in Los Angeles;
- one recording per bed, so a long listen at one spot hears the loop (40 s for the market, 1–6 min for
  the others).

**Manual test (macOS):**
1. `git pull`, then open the project once in the Godot editor so it imports the new sounds; Build,
   Cmd+B.
2. Stand on the quay: the surf. Walk to the market by day: the crowd. The tavern at night: voices.
3. `--weather=rain`: the rain. `--weather=breezy`: the wind rises.
4. Say if any bed sounds wrong for 1519 (a modern noise, music, a language) and it will be swapped.

## R2e — The port comes to know the expedition, and the Garza sails

PORT_EXPERIENCE §16–17; CHARACTER_DESIGN §29 (D-027 adopts the character briefs).

**Recognition** (`SimulationRunner.Recognition.cs`; data in `data/port/bocana.json` → `recognition`
and per person):
- **Standing** comes from the hands signed on (2 each), the lots bought (1 each) and every 1,000
  sueldos spent. It has four levels: not yet noticed, noticed, talked of, known. It is shown only in
  words (the Expedition panel) and in what people do.
- **Greetings:** once noticed, people the captain passes greet him, once a day each, with lines by
  standing that use the crew count and the hours to the convoy. The chandler, the landlady, the
  priest and the factor have lines of their own. The line passes as a toast.
- **Candidates come to the captain:** once the expedition is talked of, about every hour one person
  looking for a berth whom the captain has not met walks over and introduces themselves, with their
  pitch and claims, as if spoken to. They are selected and their line is shown. They give up after
  two hours if they cannot reach him.
- **A creditor notices:** sign on Pons and Grilo the moneylender comes straight away, whatever the
  standing: Pons owes him 400 sueldos. Pay it (Pons's trust +10) or refuse it (Grilo's trust −15) from
  his card. If it is still open at departure, he is at the gangway.

**The departure** (`DeparturePlan` in the simulation, `DepartureSequence` in the game). "Set sail"
now plays it. It is written from the expedition as it is:
1. the hour against the convoy: ahead of it, with it at dawn, or hours behind it;
2. the crew aboard by name;
3. for each hand, who has come to see them off. These farewells are written per person in the data:
   - Pascual's mother;
   - Tomé's irons unlocked and his pardon handed up;
   - Inés in the tavern door for Nuño;
   - Pons hurrying aboard;
   - and others.

   Otherwise no one has come;
4. Grilo at the gangway if his claim is open, answered there by paying or refusing;
5. the last stores going up the gangway, and what is in the hold, in casks, sacks and coils;
6. the Company's clearance, or the factor's letter about the harbour dues paid for the delay;
7. the captain boards last, the gangway comes in, the lines are cast off;
8. the Garza is towed round and gathers way while the camera rises after her, then the voyage
   begins.

The management interface steps aside meanwhile, and people gather on the quay. Escape or "Skip"
cuts to the sailing; an open claim is then refused.

**Save format 19** records who has approached, any approach under way, the claims answered and the
greetings given.

**Developer:** `--depart` plays the departure at once.

**Tests:** 401 pass, including:
- standing;
- no greetings before the expedition is noticed, then greetings once each;
- a candidate seeking the captain out with their pitch;
- Grilo's claim, paid;
- the departure plan from the real crew;
- **port-to-voyage continuity:** the same IDs, names, relationships, knowledge, hold and chronicle;
- saving and migration.

**Screenshots:** `docs/screenshots/r2e-departure-farewell.jpg`, `r2e-departure-cast-off.jpg`.

**Limits:**
- the crew are already aboard (they board when signed on), so the departure shows them at the rail,
  not walking up the gangway;
- farewells are lines with the gathered crowd, not the named relatives as figures;
- recognition does not yet change prices;
- the sponsor does not visit the ship before departure day.

**Manual test (macOS):**
1. `git pull`, Build, Cmd+B.
2. Sign on two or three people and buy stores: people start to greet you as you pass the market.
3. Keep going: someone looking for a berth comes to find you.
4. Sign on Pons: Grilo comes for his money. Pay or refuse him from his card.
5. Set sail with at least four hands: watch the departure through to the open sea, or press
   Escape to skip.

## R3, part one — Staging, scenes A and C, profiling

See `docs/R3_VALIDATION.md` for the verdicts, the screenshots and what is still to do.

- **Staging:** `--stage=dawn-quay|midday-market|rainy-workshops|departure-morning`, or Ctrl+1–4 in
  debug builds. A fixed seed, the hour, the weather, the hands and stores, and the camera.
- **Mist:** a new morning weather that lies low on the water and burns off. Light fog is height fog;
  only thick fog closes in.
- **Wind:** cloth (awnings, banners, the ensign) ripples, more in a breeze.
- **Rain:** the sailcloth in the yard is taken in.
- **Profiler:** F10, or `--profile`. A 20-second capture gives frame-time percentiles, draw calls,
  primitives, nodes, video memory and the GPU, saved under the user data folder.
- **Verdicts:** A, *partly* (lighting and ship scale yes). C, *partly* (wetness yes, interiors no).
  B and D wait for the characters.
- **Also fixed:** the departure's course out of the harbour no longer runs through the nao at anchor.

**Manual test (macOS):** run the four profiles in `R3_VALIDATION.md` and send the reports.

## C-1 (first stage) — The character pipeline, shown on Bartolo

The character slice (CHARACTER_ART §50) starts with one person carried all the way through, for the
owner to judge before the other five are converted (D-029).

- **Identity record:** `game/Assets/Characters/person.bartolo/identity.json`. It covers build,
  height, posture, face parameters, skin, eyes, hair and beard, distinctive features, garments with
  their historical sources and confidence, accessories, equipment and when it shows, and
  animations. Colours come from the shared palette, `Characters/Shared/palette.json`.
- **Model:** `model.glb`, about 4,750 triangles plus a light model of about 3,000. It is one draw call,
  plus the adze when it is in his hand. Its parts:
  - wiry body;
  - bent posture;
  - hollow cheeks and a long broken nose;
  - grey beard and brows;
  - linen shirt with rolled sleeves;
  - russet wool breeches, grey hose, leather shoes;
  - a leather apron, belt and pouch, and a mallet at the belt;
  - a brown wool bonete.
- **Animation:** idle, walk and work (the adze, from the chopping clip) all play. Each other pose
  maps to a clip: sawing pushes, carrying, sitting, kneeling, drinking, talking, lifting.
- **States:**
  - *Wet* darkens and glosses the cloth from the shoulders down and the hems up. Outdoors in port
    rain, figures soak in and dry out.
  - *Tired* comes from the fatigue need: a slumped back, a dropped head, a slower walk.
  - *Injured arm* comes from a sprain, bruise or rope burn: the arm is held across the body and two-handed
    work stops.
- **Portrait and thumbnail:** `portraits/neutral.png` (512×640) and `thumbnails/face.png` (128),
  rendered from his own head (`--bake`).
- **In the game:** a persistent person with a model is drawn with it. Everyone else keeps the
  low-poly figure.
- **Lineup:** `Godot --path game res://Lineup.tscn` shows the states side by side. Options:
  `-- --view=close|portrait`, `--lod=1`, `--people=ID`.

Screenshots: `docs/screenshots/c1-bartolo-{states,portrait,yard}.jpg`.

**Known limits:**
- **Face:** the base face is still young under the age work. The age lines and creases help, but
  forehead lines hide under his cap. Better ageing needs a sculpted older head or per-person texture
  work.
- **Beard:** the pack's beard has a ragged sculpted edge.
- **Light model:** about 3,000 triangles, not 1,500. Reducing further tore the clothes, so the clean
  version was kept.
- **Shoulders:** they still read broader than a 61-year-old's.
- **Not yet built:** rain at sea (only port rain wets
  people). (The other five and the card portrait followed in the second stage.)

**Manual test (macOS):**
1. `git pull`, open the project in the editor once (to import the models), then Build.
2. Run `res://Lineup.tscn`.
3. In the game, at 08:00, find Bartolo in the shipwright's yard.

## C-1 (second stage) — The six, and who can be signed on

- **The other five**, each built from their own identity record:
  - **Velho:** woad doublet with short skirts, a black brimmed felt hat, and brass dividers on a cord.
  - **Pons:** a long tawny coat over a brown doublet, a flat black cap, and the surgeon's case. He
    walks quick and hunched.
  - **Rodrigo:** loose shirt and slops, a woad sash, a madder-red cap, a knife on the belt. He is
    barefoot and walks with a swagger.
  - **Andrés:** a leather jerkin, a full dark beard, a nose broken flat and a scar down the left
    cheek. He walks with a stiff right knee, and carries a cargo hook at work.
  - **Varzi:** black velvet doublet, a long black gown, hair cut level at the jaw and a black
    berretta. He walks the formal walk.

  All six are 4,650–4,950 triangles, with a light model of 2,600–3,050.
- **New in the builder:**
  - painted scars;
  - hair cut away under caps and hats;
  - a knife and dividers;
  - long hair cut to a bob.
- **New in the game:** a limp in the posture layer, from the identity record, for an old injury.
- **Portraits** for all six (`portraits/neutral.png`, `thumbnails/face.png`). Each person's card now
  shows their portrait.
- **Recruiting rim** (owner's request):
  - **brass** round anyone who has told you they want a berth and is still free;
  - **pale blue** round someone you have heard of (a lead) but not yet spoken to.

  It goes by what the captain knows, not by the truth. Crew and those who have left have none.
- **Lineup:** `--roster` shows everyone side by side.

Screenshots: `docs/screenshots/c1-six-{roster,portraits}.jpg`, `c1-card-portrait-rim.jpg`.

**Known limits:**
- **Faces:** all six share the pack's one young face. Who they are comes from build, nose, jaw,
  hair, beard, headwear and clothes; age shows only through grey, lines and posture.
- **Rim:** on the simple figures it breaks at their sharp corners.
- **Scenes B and D** (R3 part two) are still to do with the six in place.

**Manual test (macOS):**
1. `git pull`, open the editor once (to import), then Build.
2. Run `res://Lineup.tscn` with `-- --roster`.
3. In the game, talk to Bartolo or Velho: the brass rim appears and their portraits show in the card.

## Stores that mean something (owner: "didn't have enough room for enough supplies")

The hold was never the limit: a full purchase fills about a third of it. The chandler's stock was.
Bought on the first morning it just covered a full crew; a day later, as other ships bought, it no
longer could. Nothing on screen said how far the stores would go.

- **Stock raised** (water 30 lots, biscuit 22, pork 14, wine 12). Others still buy 1–3 lots a day,
  so waiting costs you; the purse is now the real limit.
- **The chandler's panel** gives the verdict for a full crew: how many days the water and food last,
  against the passage (22–30 days). It is red when short, amber when only enough for a fair passage,
  green with a margin. Each lot shows the days it adds.
- **The manifest** shows how full the hold is, in litres and kilograms.
- **Tests:** a full crew can be victualled for the slow passage from first-morning stock, and each
  lot's days add up to the forecast.

## Sellers of different quality (owner's request)

Stores now come from three sellers, and what they sell is good, fair or poor. Quality is hidden:
the price and the seller's own claim are all you see until you find out.

- **The sellers:**
  - **Maese Corbalán,** the chandler: everything, all sound, at full price.
  - **Gil the cooper:** spring water in his own casks, sound and cheaper, but only 8 lots.
  - **Hob the Brennish trader:** about half price, out in the chandler's yard in the mornings only.
    His water is brackish (filled at the river mouth) and his biscuit has weevils. His pork and
    wine are fair: they spoil slowly, and the pork can sicken.
- **Finding out:**
  - **Look closely:** walk the captain to the seller, then press *Look closely* in the stores panel.
    For each lot he sees what it is really like, with a chance from his perception, or cannot tell.
    A look is never wrong, and a second look at the same goods shows nothing new.
  - **Gossip:** the guard has seen Hob fill his casks at the river mouth, and vouches for Gil's
    water; the fishwife says Corbalán is dear but his biscuit keeps. These come as leads, like
    those about people.
  - **At sea:** the first time each store spoils, the chronicle says so ("Weevils and rot in the
    ship's biscuit (Hob's sacks)…"). Anyone made ill by a bad ration is recorded too.
- **What quality does:**
  - **Spoiling:** each morning at sea a share is lost: Hob's biscuit 5% a day, his wine 3%, his
    pork about 1%.
  - **Sickness:** each litre of Hob's water has a 2% chance of giving the flux, and his pork a
    smaller one.
  - **The flux** costs health while it lasts. Most get over it in two or three days. (Before this,
    illnesses had no effect at all; the events' flux and fever now run this course too.)
- **Stores are named by where they came from,** never by how good they are. The manifest says
  "Water (Hob's casks)", not "Foul water".
- **Save format 20:** what was judged, which spoilage has been found, and a new random stream for
  the stores. Older saves upgrade: nothing judged, and new lots start with full stock.
- **Tests:** seven new, 410 in all.
- **Developer option:** `--inspect=seller.hob` stands the captain by a seller and looks.

Screenshot: `docs/screenshots/stores-sellers-quality.jpg`.

**Manual test (macOS):** `git pull`, Build. Open Cargo (C). Walk the captain to Hob in the
chandler's yard before noon and press *Look closely*.

## Prices that move, and credit for a captain the port knows (owner's request)

- **Prices climb as the convoy's sailing nears:** up to a third more by the morning it sails,
  rising over the last 36 hours as every ship buys at once.
- **Scarcity:** as a seller's stock runs down, the last lots cost up to 40% more.
- **Standing:** once the expedition is talked of, the port knocks 5% off; once everyone knows the
  Garza, 10%.
- **Credit:** at the same two standings, Corbalán and Gil let the Garza buy on account, up to 600
  and then 1,500 sueldos each. Hob wants coin.
- **What's owed** shows by each seller's name, with *Settle* to pay it off from the purse. Owing
  sends nobody after you in port. But anything still owed at sailing is waiting at the foot of the
  gangway, in the departure's claim beat:
  - **pay**, and the seller respects you for it;
  - **refuse**, and he trusts you much less ("sailed owing him for stores").
- **The stores panel:**
  - each lot shows today's price, ▲ when it is up and ▼ when it is down;
  - a line says why prices have moved, and warns before the rise begins;
  - *Buy on credit* turns on once someone gives it.
- **Save format 21:** what is owed. Older saves owe nothing.
- **Tests:** six new, 416 in all.

Screenshot: `docs/screenshots/stores-prices-credit.jpg`.

**Manual test (macOS):**
1. `git pull`, Build.
2. Hire four hands: the Cargo panel offers credit.
3. Buy on credit and sail without settling: Corbalán is at the gangway.

## C-2 — Everyone recruitable, and every named adult of the port

R3 found the largest gap was people: the six of C-1 looked like people, and everyone beside them
like boxes (CHARACTER_ART §37: recruitable people never use disposable identity). Now all 29 named
adults of La Bocana have their own model and portrait.
- **Recruitable:** Velho, Pons, Bartolo, Rodrigo, Leonor, Tomé, Ximena, Gil, Nuño, Wat, Pascual and
  Ulloa.
- **The rest of the town:** Andrés, Varzi, Corbalán, Inés, Catalina, Father Anselmo, Quintana, Hob,
  Grilo, the two guards Mateo and Lope, Bastián, Hernando, Paco, Benito, Marta and Old Simón.

- **Identity records** come from `tools/characters/identities.py`. Each is written from the
  person's own description, occupation, status, age and origin in `data/port/bocana.json`. Records
  already on disk are never rewritten without `--force`, so no one is rerolled.
- **The wardrobe** is a small set of period pieces:
  - shirt, hose, breeches or slops, doublet, jerkin, gown, cassock, cloak;
  - aprons in leather or linen, belts, shoes;
  - caps, the flat gorra, brimmed hats in felt or straw, iron caps for the guards, a fur collar;
  - for women, the long saya, the bodice and the toca.

  Their cuts are measured on the base body of each sex, so one piece fits a man or a woman.
- **Women** are built on the female base, with their own hair, brows and age lines, and scaled
  from its height.
- **Builder fixes:**
  - armholes are a clean cut, not the ragged line of the body's weights;
  - garment edges stay where they were cut when cloth is draped;
  - the neckline snap no longer pulls the armholes up.

  These also fixed the points on Andrés's jerkin.
- **Budgets:** 4,600–5,060 triangles each, with light models of 2,500–3,150.
- **Size:** the full-detail portrait meshes are rebuilt, not kept (`.gitignore`). What is committed comes to
  about 35 MB: models 19 MB, the shared sheet and clips 5 MB, and portraits.
- **Not yet:**
  - the two children, Lucía and Tito: the base bodies are adults, and a child is not a small
    adult;
  - the unnamed crowd (Tier C), which keeps its simple walkers;
  - all 29 share the pack's two young faces, so who they are comes from build, nose, jaw, hair,
    beard, headwear and clothes.

Screenshots: `docs/screenshots/c2-portraits.jpg` (all 29), `c2-roster.jpg`.

**Manual test (macOS):**
1. `git pull`, then open the editor once (to import 23 new models), then Build.
2. `res://Lineup.tscn -- --roster` shows everyone side by side.
3. In the game, every named adult is drawn as themselves, with their portrait in their card.
