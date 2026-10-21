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
| R4a | Wind, heading and sailing: wind direction, visibility, warning signs; why she is slow (VOYAGE §5, §20) | Largely done (2026-10-21): she turns to her course; wind direction, points of sail, tacking, braced yards, heel. Visibility and warning signs not yet |
| R4b | True vs estimated position; the chart: dead reckoning, a latitude sight, uncertainty region, route legs, annotations (NAVIGATION) | Largely done (2026-10-21): truth and reckoning, the noon sight and astrolabe, the chart with its doubt. Route legs and annotations not yet |
| R4c | Standing navigation orders and configurable interrupts; alerts by severity (VOYAGE §8, §35; NAV §7–9) | Planned |
| R4d | Watch rotation and the assignment board (VOYAGE §9, §17) | Done (2026-10-21): the watch bill, the round, the day's work, all hands, the turned-out watch, tired hands' mistakes |
| R4e | Localized damage, repair quality and history, cargo zones, the storm with preparation (VOYAGE §4, §21–23) | Planned |
| R4f | Medical crisis, grievances by stages, skill growth and mentorship; events become state-caused (VOYAGE §15, §18–19, §25–26, §42) | Planned |
| R4g | Landfall approach: soundings, sighting, identifying the coast, wrong landfall (NAV §30–31) | Done (2026-10-21) |
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

## Crowd — the waterfront's passers-by as people (owner: "multiples of the same guys by the docks")

The Tier C crowd was one MultiMesh of simple walkers in a handful of colours, so the same figure
kept coming past. Now it is drawn from 24 crowd kits built by the character pipeline, and each
passer-by is dressed again every time they set out.

- **Kits** (`tools/characters/identities.py`, `crowd()`): docker, seaman, porter, townsman,
  craftsman, carter, fisherman and friar; townswoman, fishwife, servant and widow, every third a
  woman. They are built at about 2,500–3,000 triangles, with no portrait, eyes or brows (seen small).
  They live in `Assets/Characters/Crowd/kit_NN`.
- **Dyes:** cloth is built with a dye slot in the fraction of its material number: main garment,
  lower garment, headwear and shirt. `character.gdshader` swaps in a per-figure colour from four
  period palettes (madder, woad, russet, tawny, the browns and greys, unbleached and greyed linen),
  each a shade lighter or darker. Two of the same kit no longer look like the same man.
- **PortCrowd** keeps a pool of 72 figures. Walkers take one when a trip starts and give it back
  when they are gone. Porters carry a load, people stop at stalls and the well, and the departure
  crowd waves with the `call` clip. Without the kits it falls back to the old MultiMesh.
- **Lineup:** `res://Lineup.tscn -- --crowd` stands every kit twice, dressed differently;
  `--rest` shows the bind pose for checking how cloth sits.
- **Builder fixes:**
  - the clean armhole cut stopped at the shoulder joint's width, so it cut away the sides of a broad
    torso; it now stops at the armpit, which mended the bodices and jerkins of heavy builds;
  - under a bodice the crowd's shirt lies close, so it no longer breaks through.

## The ends of the port (owner: "the edges feel strange and empty")

Past the west end of the map the terrace ran on as a bare plain. Past the east end the quay
stopped over open water beside a flat sandbar. Neither is walkable, but both now look like the
town going on (`PortView.BuildOutskirts`).
- **West, on the terrace:**
  - a lane of houses running off into the old town;
  - a row facing the harbour along the promenade, with benches, lanterns and orange trees;
  - the old fish market's arcade, and a plaza with a fountain.
- **West, below the terrace:** vaults in the wall, cargo, shear legs, boats along the quay, fish
  stores, and a round sea tower with a mole closing the harbour.
- **East:**
  - a warehouse and a boat shed, and a slip with a boat on it;
  - then a shingle beach going down into the water, with a wet band and stones;
  - fishermen's huts with smoke, boats drawn up, nets on frames, a capstan;
  - a rocky headland with a watchtower;
  - the town behind runs on along the hill.
- **Life:** five crowd lanes run out there, people walking them end to end, more by day. Twelve
  people are at work: mending nets, caulking, talking at the fountain, sitting on the benches.
- **The camera** keeps to the port and about ten metres past its ends, except during the departure
  and the profiler's circle.
- **Cost** in the midday market (software renderer here, so frame time says nothing): about 115k
  more primitives, 7 more draw calls and about 49 MB more video memory. On the owner's Mac, the
  profile is the one to check.

Screenshots: `docs/screenshots/crowd-kits.jpg`, `crowd-market.jpg`, `edges-west.jpg`,
`edges-east.jpg`.

**Fixed after (owner: "fix the bun hair and skirt poke-through"):**
- **Hair:** hair is reduced by dropping its smallest locks whole, not by collapsing them, which had
  shredded long hair and buns into spikes; the servants also wear a kerchief.
- **Skirts:**
  - each side of a skirt's hem follows its own thigh, and the legs under a long skirt are not
    built, so nothing comes through in the stride;
  - an apron takes the shape of the skirt beneath and lies just outside it, and skirts are not
    reduced, so the two no longer cross.
- Everyone was rebuilt with these fixes.

**Not yet:** the crowd's lanes past the ends are fixed lines, not paths.

## Conversations, and a calmer port (owner: "I don't feel the connection, the need to speak to anyone")

Talking used to be one line: a pitch and a name or two, and you could sign anyone from their card.
Now it is a conversation, and what you learn and earn in it decides whom you can sign and on what
terms.

**Simulation** (`SimulationRunner.Conversation.cs`, `PortRules.ConversationDefinition`):
- **Meeting and topics:** each person has a first-meeting line, a line for coming back, and topics:
  things the captain can ask.
- **What opens a topic:**
  - something talked over first;
  - a fact the captain already holds about them, seen with his own eyes or heard from someone else;
  - looking for a berth.
- **Answers:** they carry claims (skills and languages), facts and leads, and they move the
  person's trust, respect and liking for the captain.
- **Guarded topics** need trust first. Before that you get put off, and pressing costs trust
  ("That is my business, Captain."). You can come back when they know you better.
- **Facts heard from others** are noted as hearsay from that person and open topics with the person
  they concern. Example: Hernando says Ximena was at Mahenu "a season, not a year"; ask Ximena about
  it, once she trusts you, and she tells you about the son she left there.
- **Terms first:** no offer until they have said what they want. Velho and Ximena will not sign at
  all with a captain they do not yet trust. Anyone who trusts you well settles for about a tenth less.
- **Save format 22** keeps what was talked over with whom.
- **Content checks:** content loading checks every topic's needs and gifts.

**Writing** (`data/port/bocana.json`), 20 people, built on what the data already said of them:
- **The twelve recruits,** each with a past that can be found out: Velho and the Santa Clara, Pons
  and Almara, Tomé and the Caridad, Ximena's son, Ulloa's son at Corvera, Pascual's lie, Gil's
  family, Bartolo and the Garza's soft plank.
- **The people who know them:** Inés, Hernando, Quintana, Grilo, Andrés, Father Anselmo, Mateo and
  Hob.

**Game** (`ConversationView.cs`):
- Talking opens a conversation screen: their portrait, how they take you ("Wary of you", "Warming to
  you", "Trusts you"), what has been said, what you can ask now (already answered dimmed, to ask
  again), ask-about, and the offer once you have heard their terms.
- The port stands still while you talk.
- The person card no longer shows the asking price before they have told you.
- Developer: `--select=person.velho --talk --topics=topic.velho.charts,topic.velho.coast`.

**Calmer port** (`PortCrowd`):
- passers-by walk slower, stop more often and stay longer at stalls and the well, and fewer are on
  the move at once (30, from 40);
- small groups stand about the quay and market, two or three together, one talking and the others
  listening or watching. They stay half an hour to an hour and a half and then break up, and another
  group forms elsewhere.

Tests: 425 (9 new in `ConversationTests`). Screenshots: `docs/screenshots/talk-velho.jpg`,
`calm-market.jpg`.

**Not yet:**
- interiors (the tavern first);
- the other named townsfolk (Corbalán, Varzi, Catalina, the dockers, the smith, the ropemaker and
  others) still pass the time of day in one line;
- talking takes no game time.

## The Three Anchors: a room to walk into (owner: "there are no buildings to go in")

The tavern was a solid box, and anyone who went "indoors" vanished at its door. Now its common room is
part of the map, and walking in lifts the roof off.

- **The map** (`tools/gen_bocana_map.py`): the common room (cells x 24–33, y 6–12), with the door
  on the square at (28, 13). Inside are the hearth, the counter, two long tables with seats, and two
  standing places at the bar. Inés serves behind the counter, Nuño cooks at the hearth, and drinkers
  and gamblers take the seats. The kitchen behind stays solid.
- **Rooms in the simulation** (`LocationDefinition.Rooms`):
  - walls hide what goes on inside: the captain sees and speaks only with people in the same room
    as him, and from outside sees no one in it;
  - "Ilario Pons is inside the Three Anchors. Go in.";
  - nothing is noticed through a wall.
- **A round:** "Stand the room a round" (30 sueldos, once a day). Everyone named who is drinking
  there likes the captain a little more (+4) and trusts him a little more (+2), and it goes in the
  chronicle. Inside, a bar across the top of the screen names the room and offers the round.
- **Save format 23** keeps the day of the last round.
- **The view** (`PortView.Tavern`):
  - from outside, the tavern looks as it did, lit from within at night;
  - with the captain inside, its upper floor, roof and the front and east walls above the sill lift
    away, showing the plank floor, the hearth and its fire, the counter with casks racked behind and
    cups on the shelf, the tables and benches with candles, and whoever is in there;
  - people inside are drawn only while the captain is in there too.
- **Sound:** inside, the port outside drops to a murmur and the room's voices and cups come close.
- **Developer:** `--captain=28,9` walks the captain in before the screenshot.

Tests: 428 (3 new in `TavernTests`). Screenshots: `docs/screenshots/tavern-inside.jpg`,
`tavern-outside.jpg`.

**Not yet:**
- only the tavern opens; the chandlery, the Chamber and a workshop could follow the same way;
- the cutaway is drawn for the camera's usual angle: turned to look from the north or west, the
  back walls stay up;
- nothing yet happens in the tavern that happens nowhere else (a game of dice, a song, a fight at
  closing).

## Evenings in the Three Anchors: dice and a song, and four days in port (owner's request)

- **More time:** the convoy now sails at dawn on day 5, not day 3: four evenings in port. The
  deadlines of those who will not wait move with it (Velho and Pons on day 4, Pascual on the morning
  of day 5), as do the price rise in the last 36 hours, the eve-of-sailing warning and the late fees.
- **Dice** (from 17:00 to 02:00):
  - sit in with whoever is drinking or gambling in the room (up to three, gamblers first), for 10,
    20 or 50 sueldos a throw;
  - everyone throws two dice and the highest takes the pot (ties throw again), the same throws for
    the same moment, so reloading changes nothing;
  - sitting down warms them to you, and losing graciously warms them more than taking their money;
  - between throws someone may talk of someone they know, as hearsay in the captain's knowledge;
  - a game holds the table for half an hour as the clock runs, and the players are seen talking
    over it.
- **A song** (from 19:00 to 01:00, once an evening, 5 sueldos to the singer):
  - someone in the room with a song of their own, who has not yet sung, stands and sings it for
    twenty minutes;
  - written songs: Wat's Brennish ballad, Ximena's cradle song from Mahenu, Velho's "Salt Sea",
    Nuño's "Garlic and Onions", Rodrigo's bawdy "Abbess of Almara", Andrés's capstan shanty,
    Bartolo's caulkers' song, Gil's cooper's round and Marta's ropewalk song;
  - the room warms to the captain and the singer more;
  - what a song shows the captain hears for himself: Ximena singing in Ahuneri confirms she speaks it.
- **The room bar** inside the tavern now has Dice (with the stake) and Call for a song beside the round.
- **Save format 24** keeps who has sung and the last song's evening.
- **Developer:** `--captain=29,12 --tavern=song` (or `dice`).

Tests: 433 (5 new in `TavernEveningTests`). Screenshots: `docs/screenshots/tavern-song.jpg`,
`tavern-dice.jpg`.

## Heard and seen: dice and song sound, the captain's own model, benches (owner's request)

- **Sound** (7 CC0 Freesound recordings; `tools/fetch_assets.py` fetches one-shots without looping):
  - dice: a rattle in the cup when a game starts, then throws on the table every few seconds while
    it goes round;
  - songs: each written song names its voice (a man alone, a woman, a cradle song, the cook at his
    pots, or a room of men in chorus) and plays while it is sung. It is full inside the tavern and
    faint from the square, and the room's talk drops under it.
- **The captain** had no model of his own and was still drawn as the old box figure. He now has one
  (`person.captain`): lean and sea-weathered, a short dark beard going grey, a woad doublet with black
  breeches and hose, a brimmed felt hat and a sword. His portrait is baked too.
- **Benches:** the seats on the square in front of the tavern had tables but nothing to sit on.
  Every outdoor seat now has a bench under it (seats in a row share one) or a stool at a table's end,
  and the porters resting at the pier head sit on crates.

Screenshots: `docs/screenshots/captain-model.jpg`, `tavern-benches.jpg`.

## The chandlery and the Chamber open like the tavern (owner's request)

- **Corbalán's chandlery** (cells x 48–53, y 7–12, door on the square at (50, 13)). Corbalán now
  works behind his counter inside, not at its door. In the room:
  - the counter with the steelyard, his account book and a candle;
  - shelves of jars, tallow and lamps on the back wall;
  - rope coils on pegs and canvas bolts;
  - casks along the west wall and biscuit sacks along the east.

  You must go in to look closely at his goods ("Maese Corbalán is inside Corbalán's chandlery. Go
  in to look at the goods.").
- **The Chamber of the Ocean Sea** (cells x 2–8, y 6–10, on the terrace, door at (5, 11)). Quintana
  works at his desk across the hall instead of vanishing indoors. In the room:
  - the desk with the ledger of licences, ink, the seal and a candle;
  - three presses of pigeonholes full of rolled licences under the Crown's banner;
  - a bench for those who wait, and an iron-bound strongbox.
- **Both behave like the Three Anchors:** walls hide who is inside, and walking in lifts the roof
  and the walls above the sill. The bar across the top names the room; there is no round, dice or
  song where no drink is sold.
- **The view code** for a room is shared now (`RoomFloorAndWalls`, `RoomShell`, `Candle`), the
  tavern included.

Tests: 436 (3 new in `ShopAndChamberTests`). Screenshots: `docs/screenshots/chandlery-inside.jpg`,
`chamber-inside.jpg`.

**Afterwards (owner's request):**
- **The Company's counting house** opens too (cells x 12–16, y 6–10, door on the terrace at
  (14, 11)). Varzi and Leonor keep the books inside: Leonor used to work at a table on the terrace.
  The room has:
  - the long desk under a chequered counting cloth, with stacks of coin, the scales, ledgers and a
    candle;
  - three shelves of letter-books;
  - the Company's iron-bound chest;
  - goods from the coast: a bale of sample cloth, a sugar loaf and a sack of pepper.
- **Neighbouring buildings fade:** any building just east of a room (within four cells, the same
  rows) is built into a node of its own. It fades to a ghost while the captain is in that room, so
  the camera sees the whole room:
  - the counting house beside the Chamber;
  - the tall house beside the counting house;
  - the chapel beside the Three Anchors;
  - the cooperage beside the chandlery.

## Port polish towards the owner's board (the "Port & Preparation" panel)

- **Preparation checklist** (top left, in port), ticked from the simulation as it is done:
  - sign on hands (n/4);
  - find someone who knows the way;
  - find someone to treat the sick (optional);
  - buy stores for the passage (days of the 22 needed);
  - settle with the sellers, when anything is owed;
  - sail with the convoy, with the time left.

  Each item has a tooltip saying how. The old objective line at the bottom left now shows only at sea.
- **Icon bar:** People, Expedition, Cargo, Ship, Chart, Chronicle and Menu each have an icon above
  the label. They are Kenney's CC0 Board Game Icons and Game Icons, made white by
  `tools/make_icons.py`; the ship's anchor is drawn from the DejaVu glyph.
- **Hover label:** pointing at someone in port shows:
  - their name (or their look, if you have not met);
  - their trade;
  - "Available for hire" once you know they want a berth, or which room they are in;
  - what the mouse does.

  **Right-click on a person** now walks over and talks with them.
- **Light and colour:**
  - daylight is more golden, shade warmer, haze lighter;
  - saturation and contrast are lifted a little (`SeaAndSky` adjustments).
- **Dressing:**
  - market awnings are dyed (madder red, saffron, woad) where they were all undyed canvas;
  - six date palms stand on the square and the terrace;
  - more goods on the quay: oil and wine jars in straw, a heap of sacks, the catch in baskets
    beside a net.
- **Developer:** `--hover=person.gil` shows the hover label in a screenshot.

Screenshots: `docs/screenshots/port-ui.jpg`, `port-warm-morning.jpg`, `port-warm-afternoon.jpg`.

**Not yet** (from the board): the board's painterly lighting and denser rooftops would need a
lighting and post-processing pass of their own; the people are still smaller on screen than the
board draws them.

## The dock, pushed further (owner: "keep pushing the dock")

- **The Garza at her berth:**
  - furled sails are drawn as canvas rolls lashed along the yards with gaskets; they had been
    squashed nearly flat;
  - in port her sails stay solid at every zoom, because furled canvas hides no one;
  - a painted strake runs under the rail: ochre over a red line, with an ochre band and red on the
    transom;
  - a small raised forecastle with its rail stands in the bows.

  She now reads as a rigged ship rather than a barge.
- **A mule cart** is being unloaded on the quay for the Garza: two-wheeled, sacks in the bed and two
  on the stones, the mule in the shafts with collar and saddle pad. It is a blocked feature on the
  map, so people walk round it.
- **The camera** starts the port closer (zoom 17, from 24), so people read as people.
- **The harbour water** is a clearer blue-green by day.

Screenshot: `docs/screenshots/dock-garza.jpg`.

## The dock, continued: the Garza's look, a second ship, the hoist, washing, gulls, light

- **The Garza** (`ShipView`), still a placeholder until the hero ship is approved (ART_BIBLE §6.1),
  now looks a good deal more like one:
  - **Stern:** a poop, a second, narrower tier on the stern castle, with a painted band, transom
    windows framed in ochre, its own rail and two stern lanterns on iron brackets. The mizzen now
    steps from it.
  - **Sheer:** the line of the rail rises higher to bow and stern.
  - **Rigging:** ratlines across the shrouds, the rope ladders up the masts, and the shrouds come
    down to channels outside the hull with their deadeyes.
  - **Flag:** a red flag with a gold cross flies at the foremast head.
- **A merchantman** is moored at the west end of the quay, below the terrace. She is warped to two
  bollards, her yards have furled canvas lashed with gaskets, a plank runs down to the quay, and
  bales and sacks are coming off with a docker lifting.
- **The shear legs work:** the load on the fall swings, is lowered into the lighter, comes up empty
  and goes down again, with two dockers hauling on the fall.
- **Washing:** shirts, sheets, a red petticoat and smocks hang on lines across the alley between
  the tavern and the chapel and between the houses behind.
- **Gulls** perch on the bollards, a ridge, the chapel and the merchantman's stern.
- **Light:**
  - a stronger sun, with a little less fill in the shade, for more of the board's contrast;
  - a little more glow and contact shadow.

Screenshots: `docs/screenshots/dock-garza.jpg`, `garza-stern.jpg`, `dock-merchantman.jpg`.

**Not yet:** a proper hero model of the Garza (a modelled caravel with a carved stern, built and
approved like the characters), rather than one put together from boxes in code.

## The Garza's deck plan (first step of the ship model)

The Garza had been drawn round the early test deck. She now has a deck of her own,
`location.garza` (`tools/gen_garza_deck.py` writes `data/locations/garza.json`): a caravela redonda
of about 22 m.
- **Forecastle** (rows 1–4), raised 1.3 m: the lookout in its eyes, the galley fire box at its
  break, a ladder down to the waist.
- **Waist** (rows 5–16):
  - the foremast with its rigging station;
  - the main hatch over the hold, with the carpenter's repair stack beside it;
  - the mainmast, with a pump on either side and its rigging station.
- **Quarterdeck** (rows 18–23), raised 1.5 m, up a ladder: the helm (whipstaff) at its break, the
  mizzen and the stern watch. The poop rises behind it.
- Every station the voyage uses (watch, rigging, pumps, hull repair, galley) has its place. The test
  deck stays for the test scenario.
- **The view** draws:
  - the raised decks at their heights, with planking, a break face and a rail that stops either side
    of the ladder (owner: "railings on ship block the stairs");
  - the ladders as stringers and steps, and bulwarks standing a rail's height above each deck;
  - the mizzen stepped on the quarterdeck, the main hatch as one coaming, and the fittings at deck
    height.

  Crew stand at the height of the deck they are on.

Screenshot: `docs/screenshots/garza-deck.jpg`.

**Next:** the Garza's model itself, built offline from a written specification like the characters.

## The Garza's hull, built from her specification

`tools/ship/build_ship.py` builds `game/Assets/Ship/garza.glb` (about 14,000 triangles) from her written
specification, `tools/ship/garza.json`, and her deck plan. It shapes the hull by the same rule the game
uses for her decks, so the two meet exactly.
- **Hull:**
  - carvel planking strake by strake, laid along the sheer so every line runs clean from stem to
    stern, with staggered plank butts;
  - two wales standing proud, the ochre band with its red line beneath, a dark boot-top at the
    waterline and a pale tallowed bottom;
  - tumblehome above the greatest breadth and a round bilge;
  - aft, the run closing to the sternpost under a flat tuck.
- **Ends:**
  - a raked stem with hawse holes either side, and the keel;
  - the sternpost with the rudder hung on iron pintles.
- **Stern:**
  - the flat transom, with four framed windows of the great cabin, mouldings, carved rosettes and the
    red line;
  - the poop's deck, its front bulkhead with the cabin door and two lights, and its sides and rail
    inside.
- **Gun-port lids** in red along the waist.

The game (`ShipView`) loads it for the Garza and still draws the decks, the inner faces of the
bulwarks, the rails, the raised decks and ladders, the masts, yards, rigging and sails, and the lanterns.
The test deck keeps its generated hull.

Screenshots: `docs/screenshots/garza-model.jpg`, `garza-model-stern.jpg`.

**Not yet:**
- the model has no texture of its own: the scenery material gives the timber grain, mapped in world
  space.

### The Garza's masts, and the voyage's wear on her

**Masts in the model.** `build_ship.py` now builds the spars too:
- tapered, octagonal lower masts with rope wooldings;
- the tops on their crosstrees, with a low rail, and the caps;
- the topmasts to the mastheads;
- the yards, tapered to the arms, with slings and blocks;
- the mizzen with its lateen yard fished in two pieces;
- the bowsprit with its gammoning.

The masts stand where the deck plan puts them, at the heights ShipView hangs the sails from (main
12 m, fore 9.5 m). ShipView no longer draws spars for the Garza. It still draws the sails, furls,
shrouds, ratlines, stays, flag, pennant and lanterns, because those move or change.

**She shows what the voyage does to her.** `ShipView.ShowCondition(hull, rigging)` reads the
voyage's condition each frame. The simulation already wore her down: hours at sea, storms, strikes,
and the repairs and pumping the crew do.
- **Rigging:** as it falls, the sails grey and stain. Then sewn patches appear, then holes open and
  the foot frays. Below about 30% they hang in rags. This is done in `sail.gdshader` (`wear`, with a
  `seed` for each sail).
- **Hull:** one stove-in, splintered wound on her sides for every tenth of the hull lost. When the
  carpenter makes a wound good, it becomes two pale new planks nailed over the hole, and those stay
  for the rest of the session.
- **Storms carry things away.** Gales and storms now have a chance each hour of a squall carrying
  something away aloft (`riggingStrikeChancePerHour`: gale 0.06, storm 0.22). It is scaled by the
  sail plan's wear, so heaving to is safer. Each strike costs rigging (4 in a gale, 6 in a storm).
  - It either parts one of the seven lines (`VoyageRules.PartingLines`: the middle shrouds, the
    forestay and the two stays between the masts),
  - or springs the main topsail yard (`YardSpringChance` 0.25, doubled below 30% rigging).

  A sprung yard means no topsail, and she makes 12% less way.
- **The crew mend it.** Rigging work mends what was carried away, the yard first and then the lines.
  Fishing the yard takes 12 points of work and splicing a line takes 5 (`VoyageState.Apply`). The
  state is in `VoyageState.PartedLines`, `YardSprung` and `Mending`, which are saved (format 25).
- **Alerts and HUD.** The chronicle records each one, and there is an alert: "The topsail yard is
  sprung" (urgent) or "A line has parted aloft". The ship chip and the ship panel say what is
  carried away.
- **In the view,** a parted line hangs in two slack ends, frayed at the tip and swinging with the
  sea. When the yard is sprung, each half hangs from its yardarm with splintered inner ends and rags
  of the topsail, and the topsail itself is gone. ShipView draws this yard, not the model, so that it
  can break.
- **Bilge:** as before, she rides lower; she now also lists as the water rises.

Developer flags: `--sea` sails at once with no departure; `--hull=N` and `--rigging=N` force her
condition. `--carryaway=0,3,yard` parts those lines and springs the yard. For example:
`--hire=6 --sea --zoom=18 --hull=40 --rigging=20 --bilge=20`, or
`--sea --weather=weather.storm --carryaway=1,3,5,yard`.

Screenshots: `docs/screenshots/garza-at-sea.jpg`, `garza-damaged.jpg`, `garza-sprung.jpg`, `garza-storm-carried.jpg`.

**Not yet:**
- only the main topsail yard can break, and nothing falls to the deck or hurts anyone below;
- patches made in an earlier session are not remembered after a load;
- wounds are on the hull sides only, not on the deck or the bulwarks.

### The Garza's fittings: the first props made with Artlist

A pilot of the Artlist workflow (`docs/ASSETS.md` keeps the credits; Expedition's share is 4,125 a
month):
- **Concepts** (Nano Banana 2, 90 credits each): a capstan, the ship's bell in its belfry, and a gun
  on its sea carriage. Each is one object on a plain background, in a low-poly look.
- **Models** (Hyper3D Rodin v2.5 Fast image-to-3D, 300 credits each): textured GLBs of about 19,500
  triangles each.
- **`tools/props/import_prop.py`** makes them the game's. For each prop it:
  - samples the texture into vertex colours and welds the seams;
  - reduces the mesh to its budget (700–900 triangles) with the character pipeline's decimator;
  - cuts the colours to five flat tones, each pulled towards the Garza's palette;
  - stands it on the ground at its real size, and writes it with the ship builder's writer.

  Sources and settings are in `tools/props/props.json`, and the generated GLBs are kept in
  `.cache/props/`.
- **On deck.** A new cell type, `o`, marks a fitting: blocked to walkers, but deck, so the hull keeps
  its shape. The location's `features` (kind `fitting`) say which prop stands on each one, and
  ShipView places it.
  - Six guns stand at the gun ports, which have moved to rows 7, 11 and 15. They run out through the
    ports.
  - The capstan stands in the waist aft of the mainmast.
  - The bell hangs by the port rail at the forecastle break.

Screenshots: `docs/screenshots/garza-fittings.jpg`, `props-pilot.jpg`.

**Not yet:**
- the bell isn't rung, and the lines and yard make no sound when they go, because `freesound.org`
  is refused by this environment's network and the plan doesn't cover Artlist's catalogue sounds;
- the props have no wear or wet look of their own (they use the scenery material).

### Weakest first: furled sails, the galley, the market stalls

I judged what to replace by what the player sees most and what looks worst: the quay on arrival and
the Garza's deck at sea.
- **Furled sails** (code, no credits). At her berth they had been smooth white capsules, the same
  thickness from end to end, the first thing in the opening view. Now each is gathered canvas on top
  of its yard: fat in the bunt, thinning to the yardarms, lumpy between the gaskets, sagging a little
  at the middle, and shaded duller than set canvas.
- **Galley firebox** (Artlist, 300 credits). It replaces the glowing orange box. It is a fitting on
  cell (6,5), backed against the forecastle break, with its open front to the cook, whose spot moves
  to (6,6). The fire's light is kept. The importer gained `gain` and `saturate` settings for models
  whose texture comes back dim (this one's brick lining had gone brown).
- **Market stalls** (Artlist, 300 credits). The model gives the frame: braced trestles, posts,
  counter, and a scalloped awning. It is stretched to each stall's width, and its canvas is recoloured
  to that stall's dye (`PortView.StallFrame`, one mesh per dye). The goods by trade and the rain cover
  are unchanged.

Both models came from reference sheets the user supplied; they are prototype-only until their source
is cleared (`docs/ASSETS.md`). Screenshots: `docs/screenshots/port-stalls-furls.jpg`,
`garza-galley.jpg`.

- **Tables and benches** (Artlist, 300 credits each). The trestle table with braced feet and the
  splay-legged bench. A new `PortView.Prop(name, at, length, yaw)` stretches a prop along its length
  and falls back to the old boxes when the prop is missing. It is used by:
  - the Three Anchors' tables and benches;
  - the quay's `table` features;
  - every `Bench(...)`: the benches outside the tavern, the Chamber's waiting bench, and the short
    seats for side-facing sitters.

  People sit at the right height on them, and the cups and candles stand on the new tops.
  Screenshot: `docs/screenshots/tavern-tables-benches.jpg`.

### The Garza's sounds

Freesound is still refused by this session's network proxy ("upstream denied") even after it was
allowed; a new session may pick it up. Meanwhile the sounds use CC0 clips from the Kenney packs the
game already credits: four more clips from RPG Audio (`clothBelt`, `cloth`), fetched by
`tools/fetch_assets.py` as before.

`ShipSound` (new; PortSound is switched off at sea):
- **The ship's bell** (`impactBell_heavy`) strikes the watch: one bell at half past, up to eight at
  the change of watch (four, eight and twelve o'clock), struck in pairs. Every half hour at 1x; only
  the change of watch at 10x; silent faster than that.
- **A line parting** (on the chronicle's RiggingCarriedAway, line): the crack of it going (`clothBelt`),
  then its end flogging (`cloth`).
- **The topsail yard springing** (RiggingCarriedAway, yard): the split of the timber
  (`impactWood_heavy`, `impactPlank_medium`) and the creak of what is left working loose.

**Not yet:** these are stand-ins from foley packs. Real recordings of a ship's bell and a parting
line from Freesound would be better, once it can be reached.

### The crew at sea: claims tested, secrets surfacing

The port is where the captain learns who people say they are; the voyage is now where he finds out.

**Claims tested by work** (`SimulationRunner.Crew.cs`).
- The first time the captain sees someone at work in a skill or language, it is set against what he
  was told in port: their own word first, else references.
- If the two are two levels or more apart (`ClaimMargin`), it is recorded (`ClaimTested`) and raises
  an alert: "X is not what they claimed" or "X is better than they said".
- Everyone else aboard changes their respect for that person (-6 or +5). Someone found wanting also
  carries the stress of it.
- Duties gain `shows`: skills the work reveals without setting its pace. Standing watch now shows
  seamanship and sailing.

So with the port's people:
- Tomé, who swore he had never sailed, shows himself a seaman on his first watch.
- Rodrigo's rigging and seamanship come out at half what he claimed.
- Pascual's sailing gives him away.

**Events written for people** (`data/events/crew.json`). The event system gains:
- a named subject (`subject.person`), so an event fires only if that person sailed;
- `known` and `notKnown` triggers, for what the captain was told in port;
- `leaguesBelow`, for when the coast is near;
- choices offered only if the captain knows something (`known`), such as "ask him about the ship
  that came home without her master";
- a `learn` effect that shows the truth (skill, language or fact) or records what someone now tells.

The six events:
- **Velho, *A Brennish sail*.** He asks to pass letters to an interloper. You can let him, ask him
  what Hob carries (if you were told of the meetings), open the packet, or refuse.
- **Velho, *The bar at Punta Ceniza*.** Near the coast, he says the chart will put her on the bar.
  His navigation is tested by your choice; trusting the chart can ground her.
- **Pons, *The medicine chest*.** The sick-bay wine is going. If you know of his debts you can ask
  about Grilo, and hear Almara.
- **Tomé, *The convict in the blow*.** He works the ship like a seaman. If you were told of his past,
  you can ask about the Caridad, and he tells you.
- **Pascual, *Frozen in the shrouds*.** He was never past the river bar. Send a topman, go up
  yourself, or order him.
- **Ximena, *Before the coast*.** If you know it was a season and not a year, she tells you about her
  son at Mahenu. Otherwise, asking for the greetings shows her Ahuneri for what it is.

`DeveloperRaiseEvent` and the `--event=` flag raise one on demand, for example
`--hire=11 --sea --event=event.pascual_aloft`. Five tests are in `CrewAtSeaTests`.
Screenshot: `docs/screenshots/crew-pascual-aloft.jpg`.

**Not yet:**
- talking to the crew at sea (the conversation view is port-only);
- events for the others (Rodrigo, Wat, Leonor, Ulloa, Bartolo, Gil, Nuño);
- friction between shipmates beyond the existing quarrel event.

### Navigation: the chart, the noon sight, the compass

Until now the voyage was a distance, leagues sailed out of 560. Now it has a place on the sea.

**The model** (`Voyage/Navigation.cs`, `SimulationRunner.Navigation.cs`, `data/voyage.json` "route").
- **Positions** are in leagues east and north of La Bocana (36°30′ N, 6°18′ W). The Ember Coast runs
  north and south through Punta Ceniza, at bearing 222° and 560 leagues.
- **How she is steered.** She steers by the captain's reckoning, the way it was done: first for the
  destination's latitude at an offing 60 leagues out, then due west down that latitude until the
  coast is raised.
- **Why the reckoning goes wrong.** The truth moves with the set of the sea: a steady northerly
  current, plus each spell of weather's own. The reckoning moves by course and log, and the log is
  never quite right. So the two drift apart.
- **The noon sight** puts the reckoned latitude right, within an error that grows with the sea
  running and shrinks with care. Nothing at sea gives the longitude.
- **Landfall.** Fetching the coast off the mark means coasting along it. Measured with the test crew:
  daily sights bring her in about 1.5 leagues from her mark (2.4 at worst); with none, about 20
  (30 at worst).
- **The navigator.** The captain can appoint one, who takes the sight at 12:29 if he has not. Their
  care follows their skill, and taking the sight shows that skill: Velho's or Wat's navigation is seen.
- **Saving.** Navigation has its own random stream, so the weather and events dice are unchanged. It
  is saved (format 26); older voyages are placed on the route by how far they had sailed.

**The chart** (Chart, M; `ChartView`). It shows only the captain's reckoning, never the truth:
- the coasts and a graticule of degrees;
- the reckoned track, with a red bar for each noon sight's latitude;
- the Garza where you reckon her, inside an ellipse of doubt: the latitude since the last sight, the
  longitude since the port;
- the course to steer, dashed: to the latitude, then down it;
- a compass rose with the course needle;
- beside it, the reckoning in words (position, course, speed by the log, leagues to go, how far it
  may be out), the last sight, the navigator, and the astrolabe.

Time stands still while the chart is open.

**The astrolabe** (`AstrolabeView`). Between 11:30 and 12:30 you take the sun yourself. It climbs
through the alidade's sights to the noon line and falls again, while the ship's roll swings the ring,
harder in a seaway. Mark the reading (Space) when it stands highest and the ring is still: the nearer
the true height, the better the latitude.

**The compass.** At sea the HUD shows the course as a point of the compass ("SW by S"), and "Noon: take
the sun" while the sight can be taken. The HUD's leagues to go are now the captain's reckoning, not
the truth.

Developer flags: `--seadays=N` sails on N days with the captain keeping the reckoning; `--chart` and
`--astrolabe` open those views. For example: `--hire=6 --sea --seadays=9 --chart`. Six tests are in
`NavigationTests`. Screenshots: `docs/screenshots/voyage-chart.jpg`, `voyage-astrolabe.jpg`.

**Not yet:**
- sounding with the lead near the coast;
- the true track drawn after landfall, to show how far out the reckoning was;
- a binnacle and compass on the deck.

### Turning: she swings to her course, and the wind has a direction

The ship used to point the same way whatever course she steered. Now she turns.

**Her swing** (`Main`, `SeaAndSky`, `ocean.gdshader`). At sea the ship turns to the course steered,
measured from the course she set out on, so the view opens as it always has. She wanders a degree or
two either side as the helmsman works the wheel. The wake and the flow of the water turn with her,
and clicks on the deck are taken into her own frame.

**The wind** (`Navigation`, `data/voyage.json` "route").
- **Direction.** Each spell of weather brings a wind from its own quarter, mostly from the prevailing
  NNE (25°, give or take 85°), so head winds come often. The wind is drawn from the navigation stream.
- **How near the wind she can sail.** No nearer than six points (67.5°). When the course she wants lies
  nearer the wind than that, she beats to windward. She lies as close as she will on one tack, and
  goes about once the wanted course is 12° the other side of the wind, or once she has held that
  board for 10 hours with the course across the wind. A head wind makes her zigzag, a board of most of
  a day each.
- **Points of sail** set her speed: 0.88 close-hauled, 1.02 with the wind on the beam, 1.12 on the
  quarter, 1.02 dead astern.
- **Balance.** Measured over the ten test crews: five of ten arrive, as before the wind had a direction.
  They beat 7–29% of the time, with up to 11 tacks a passage. (A spread of 90° left only four arriving.)
- **Saving.** Saved in format 27 (wind, tack, hours on the board). A format 26 voyage takes the
  prevailing wind.

**On the ship** (`ShipView.SetWind`).
- **Yards.** The yards and their sails are braced round to the wind: square before it, up to 40°
  close-hauled, the windward yardarm forward.
- **Colours.** The pennant and the flag stream away from the wind, with the air her own way makes.
- **Heel.** She heels to leeward with the wind on her beam, more as it freshens, and sails upright
  before it or hove to.
- **The model.** The course yards came out of `garza.glb`: ShipView draws them now, so they can be
  braced.

**On the HUD and chart.**
- **The compass chip** reads "SW by S · wind NNE", and adds "· beating" when she cannot steer her course.
- **The chart's rose** has a blue arrow flying in from the wind's quarter.
- **The chart's reckoning** says where the wind is and how she sails to it: close-hauled, on the beam,
  on the quarter, or beating on the port or starboard tack.

Developer flag: `--wind=DEG` sets the wind's quarter until the weather changes; `--wind=222` puts it
dead ahead. Three new tests are in `NavigationTests`. Screenshots: `docs/screenshots/voyage-beating.jpg`,
`voyage-beam-reach.jpg`.

### R4g — Landfall: the bank, the lead, raising the coast and knowing it

Reaching the coast used to end the voyage on the spot. Now landfall is a piece of play (NAVIGATION
§14, §15, §30, §31): the coast must be found, raised and known for what it is.

**The far coast** (`Voyage/Coast.cs`, `data/voyage.json` "route" → "coast"). The sailing directions
give five marks along the Ember Coast, each with how to know it and what the lead brings up off it:

| Mark | Where | How to know it | Bottom |
|---|---|---|---|
| the Río Bermejo | 32 leagues north | a broad river mouth, the sea red off it | soft red mud |
| the Bajos Largos | 16 leagues north | low sand spits, surf on the bars | fine grey sand |
| **Punta Ceniza** | the destination | a rocky headland, a blackened ruin, a lagoon behind | shell and coarse sand |
| Punta Falsa | 13 leagues south | a headland much like Ceniza, reefs breaking, no ruin, no lagoon | coral and broken rock |
| the Manglares | 29 leagues south | an endless low wall of mangrove | black mud |

Punta Falsa is Ceniza's look-alike, and the Bajos Largos and the Manglares are easily taken for each
other.

**The approach** (`SimulationRunner.Landfall.cs`):
- **The coast is near.** Once the reckoning puts her within 30 leagues, the captain is told once.
- **Signs of land.** Within 40 leagues, with a watch kept: land birds, weed and green branches, the
  sea changing colour, a bank of cloud standing still in the west. They are clues, not a detector.
- **The lead.** The bank runs out 25 leagues: beyond it the deep-sea line finds no bottom at 100
  fathoms, and inside it the depth shoals to 4 fathoms under the coast. The cast also brings up the
  bottom of that stretch. The captain heaves it from the chart; a navigator keeps it going every two
  hours once the reckoning puts her on the bank. Each cast is marked on the chart at the reckoned
  place.
- **Raising the coast.** The masthead raises it 6 leagues off by day (less in thick weather), 1.2 at
  night (only the surf), and hardly at all with no watch kept. Unseen, she strikes: 25 off the hull.
- **Standing off.** Raised, the coast gives her distance off (her longitude at last) but not where
  along it she is. She stands in to 2 leagues and stands off and on.
- **Knowing it.** Each mark that comes into view waits for the captain's word. The best hand aboard
  (the navigator, else whoever knows navigation best) says what they make of it. Their opinion weighs
  what it looks like against where the reckoning puts her: a fresh sight or a known mark makes it
  sure, many days without one leave the look to decide. The more they know, the less a look-alike
  fools them. Left three hours without a word, they make the call.
- **Running along it.** Taking the coast for a mark puts the reckoning there, and she runs along the
  coast for where that puts Punta Ceniza. She has arrived only when she is truly there and knows it.
- **The wrong landfall.** Taking Punta Falsa for Ceniza, she stands in and finds no ruin and no
  lagoon: "It is not here", and the captain must think again (a headland ruled out stays ruled out).
  Taking the Bajos for the Manglares, she runs north, the wrong way, until the Río Bermejo says so.
- **Saving.** Saved in format 28. A voyage that had fetched the coast in an older save knows where she
  is on it.

**Balance.** Over the ten test crews left alone, five arrive, as before, and each knows every mark for
what it is. The five that founder never reach the coast. A first version that gave the opinion by
skill alone, ignoring the reckoning, had crews dithering along the coast for days (two arrived).

**On the chart** (`ChartView`). Near the coast the chart becomes a coast sheet, about 60 by 85 leagues
round Punta Ceniza, with the marks, the soundings in fathoms (a ring with a bar for no bottom), and a
degree graticule. The side panel scrolls. Its coast section has:
- the last cast and the Heave the lead button;
- with land in sight, what is in view and what the best hand takes it for;
- the marks, with the sailing directions for the one chosen (it opens on the best hand's opinion);
- "That is what we see", and Run north / Run south to coast by eye.

The noon sight steps aside once land is raised.

**In the view** (`CoastView`, `coast.gdshader`). Once raised, the coast stands off to the west: grey in
the haze at first, clearer as she closes. It is drawn from the sailing directions:
- the Río Bermejo a gap through the land, with a red-brown stain spreading off it;
- the Bajos Largos low sand with surf on the bars;
- Punta Ceniza a rocky headland with a blackened ruin and the lagoon behind;
- Punta Falsa the same headland, with reefs breaking white and no ruin;
- the Manglares a flat dark wall.

The distances are the view's, not the chart's. The land stands at the edge of the camera's view when
first raised and nearer as she stands in, never on top of her.

**The HUD.**
- **Chips:** "Coast near: sound", "Land: what coast is this?", "Land: standing off" and "Coasting
  north" or "Coasting south".
- **The objective** asks which mark is in view, then counts the leagues to Ceniza by what you took it for.
- **Alerts:** the coast is near, bottom found, land, a mark on the coast, "This is not Punta Ceniza",
  and she has struck. A new "The coast" pause category is on by default.
- **The ending** says how many tries it took to know Ceniza.

Developer flag: `--offcoast=OFF,NORTH` puts her that many leagues off the coast and along it from
Ceniza (for example `--offcoast=2.5,-13` off Punta Falsa). Eight tests are in `LandfallTests`.
Screenshots: `docs/screenshots/landfall-punta-ceniza.jpg`, `landfall-punta-falsa.jpg`,
`landfall-rio-bermejo.jpg`, `landfall-coast-sheet.jpg`.

**Not yet:**
- the lead as a crew task with someone in the chains, seen on deck;
- night approaches drawn as darkness and surf only;
- anchoring, the boat sent in, and what is found ashore (R6);
- the true track drawn after landfall.

### What the sea gives, and watering at the islands (owner: "can the ship find land for supplies if they run out?")

Until now nothing at sea could refill a cask: once the water ran out, short rations only stretched it.
There are now two ways to get more: the sea itself, and the islands.

**1. Rain and fish** (`TaskDefinition.NeedsWeather`, `TaskDefinition.Yield`, `data/tasks/shipboard.json`).

*Rain.* It now rains at sea for each weather's share of the hours:

| Weather | Hours with rain |
|---|---|
| calm | none |
| fair | 4% |
| fresh breeze | 22% (squalls) |
| gale | 50% |
| storm | 85% |

Whether a given hour rains comes from the seed and the hour, so it uses no dice and needs no save. You
see and hear it: the rain particles follow the hour's rain, a shower greys the sky, and the port's
rain recording plays at sea.

*Two new tasks.* Both can be ordered or kept as standing duties.
- **Catch the rain**, only while it rains: about 14 litres an hour a hand, into the casks while there
  is room. It stops when the rain stops, or when the casks are full.
- **Fish over the side**: paced by fishing skill, best in a calm, two and a half times better over
  the bank off the far coast, and impossible in a gale. An easy hour that eases the mind.

*Tasks can now need weather* (rain; a sea below some state; at anchor), and stop when it goes. *Tasks
can now yield stores*, unit by unit at their pace.

*Eating.* Fresh fish spoils (30% a day), and the crew now eat what spoils soonest first. That order
changed no other test.

*The HUD.* A chip says "Rain: catch it" while it rains, or how many hands are catching it.

**2. The islands** (`Voyage/Islands.cs`, `SimulationRunner.Islands.cs`, "route" → "islands").

| Island | Kind | Where | Water | Forage |
|---|---|---|---|---|
| Isla de la Aguada | charted and settled: a high green island, a beach, a few houses by a stream | about 25 leagues west of the first leg | spring 1.0 | 0.35 |
| Isla Esquiva | only reported: low and wooded, birds in thousands | on the chart near the second half of the first leg | spring 0.45 | 1.0 |

Whether Esquiva is there at all (55%), and how far from its charted place (up to 12 leagues), comes
from the voyage's seed and is never saved.

*Making for an island.* From the chart: "Make for", or "Search for" a reported island. She steers for
its charted place.
- **Sighting.** The masthead raises it (10 leagues by day for the Aguada, 7 for Esquiva; little at
  night; hardly with no lookout). Once raised, she steers for it by eye.
- **Searching.** Reaching its charted place unseen, she lies to (through the night, if need be) and
  searches three hours of daylight before giving it up: "an empty sea all round".
- **Anchoring.** At anchor off a charted island the reckoning is put right, longitude and all.

*Ashore.* At anchor she makes no way, and two shore tasks open:
- **Water the ship ashore**: about 45 litres an hour a hand, times the spring; strong backs help, and
  the surf can bruise.
- **Forage ashore**: island fruit and greens (a new food that spoils at 20% a day), times what the
  island has.

"Weigh anchor" (or "Give it up") takes her back to the passage. A detour to the Aguada costs a day or
two; Esquiva is a gamble.

*Saving.* Saved in format 29: the island, sighted, where she saw it, how long she searched, anchored.

*On the chart* (`ChartView`).
- The Aguada is drawn as land; Esquiva as a dashed ring with "(reported) ?".
- A dashed course runs to the island she is bound for.
- An Islands section gives each island's distance and bearing by the reckoning, the water left, and
  the orders.

*In the view* (`IslandView`). Once raised, the island stands in the direction it truly bears: grey
and small at first, nearer as she runs in, close aboard at anchor. The Aguada is a high green hill
with its houses; Esquiva is low and wooded.

*The HUD.* Chips read "Bound for …", "… in sight" and "At anchor: …", and the objective gives the next
step. Alerts: land, the island; at anchor; no island there.

Developer flags: `--seaweather=ID` (that weather at sea, and on until a shower if it rains in it);
`--island=island.aguada` (put her a few leagues off and make for it), `--island=island.aguada,anchor`
(on until she anchors). Tests: `SeaSuppliesTests` (5) and `IslandTests` (4). Screenshots:
`docs/screenshots/sea-rain.jpg`, `island-anchored-aguada.jpg`, `island-chart.jpg`.

**Not yet:**
- buying water and food with coin at the Aguada (the purse stays with the port);
- the boat itself, seen going in;
- islanders and news;
- islands along the coast's approach.

### R4d — The watches

The crew used to work round the clock, each hand resting only when worn down. Now the ship keeps
watches (VOYAGE_EXPERIENCE §9, §17).

**The watch bill** (`Voyage/Watches.cs`, `SimulationRunner.Watches.cs`). When she sails, the mate
splits the crew into the starboard and larboard watches in turn, with the cook a day-worker. The
captain can change anyone's watch in the Ship panel, or leave a hand out of the bill ("works when
needed").

**The round.**
- **Seven watches a day:** the middle, morning, forenoon, afternoon, first dog, last dog and first
  watches. Being an odd number, the round shifts each day, so no one keeps the same hours.
- **The day's work.** From 06:00 to 18:00 both watches turn to. Through the evening and night they
  stand four hours on deck and four below.
- **Day-workers** are on deck 06:00–18:00 and below at night.

**On deck and below.**
- **On deck.** The watch on deck works its standing duties, and one hand always keeps the helm and
  lookout before any other work. On deck a hand rests at fatigue 80 rather than 70.
- **Below.** The watch below turns in once at all tired (fatigue 25). A rested hand below may still turn
  to a needed duty, but never the helm.
- **The change of watch.** At the bell the routine work of the watch going below is dropped, and the
  sleepers of the watch coming up are turned out.
- **Orders always go first**, whatever the watch.

**All hands.** The captain can call all hands (both watches on deck, sleep spent for the work in hand)
and dismiss them. The officer of the watch also turns the watch below out to the pumps when the bilge
passes 35%, and sends them below again once it is pumped under 12%. A storm alone does not turn them
out: watch and watch keeps the crew fit to ride it out.

**Fatigue.**
- **Mistakes.** Tired hands make mistakes: the chance of an accident climbs to four times as fatigue
  rises past about 50.
- **Fresh hands.** Hands keeping watches, fresh from below (fatigue under 30), work 15% faster.

**Balance.** Over the ten test crews:

| | Arrive | Average fatigue |
|---|---|---|
| Driven round the clock (no watch bill) | 5 of 10 | about 55 |
| Keeping watches | 4 of 10 | about 27 |

The watches give fewer hands on deck but fresh ones, and a helm always kept. Getting there took four
changes, each forced by a measured failure:
- **Turning out for the pumps.** At first, with the watch below asleep, the bilge won and one crew
  foundered on day 3. The officer now turns them out.
- **Storms.** A storm turning out both watches wore everyone out together. Storms no longer do.
- **The day's work.** Half the crew asleep left too little repair done. Both watches now work by day.
- **The helm first.** The helm going unmanned while the watch was busy cost speed, so it is now kept
  before any other work.

The older tests keep their crews without a watch bill. The game's ship, sailing from port or the test
deck, draws one up.

**On the HUD.**
- **A chip** says "Starboard watch on deck", "Day's work", "Watch below turned out" or "All hands".
- **The Ship panel** has the watch bill: the watch in progress, All hands on deck, and each hand's
  watch.
- **Alerts** for the watch below turned out.

Saved in format 30: the bill, all hands, turned out. Tests: `WatchTests` (7). Screenshot:
`docs/screenshots/watch-bill.jpg`.

**Not yet:**
- named posts (helmsman, lookout aloft);
- the bosun's call heard at the change of watch;
- a watch officer of the crew's own choosing.
