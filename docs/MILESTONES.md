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
| R2 | The living port, playable: walk the waterfront, click anyone, talk and offer in the world, inspect the Garza, chandler and stowage | Next |
| R3 | Visual validation, Test A port then Test B ship, with the chosen art pipeline | Waiting on owner (art pipeline) |
| R4 | Voyage gaps: chart and estimated position, cargo, medical crisis, chained storms, state-driven events | Planned |
| R5 | Shipwreck continuation: survivors and salvage on a beach | Planned |
| R6 | Landfall camp at Punta Ceniza | Planned |
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

