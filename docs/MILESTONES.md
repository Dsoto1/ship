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
| 5a | Identity: sex, languages, social status | In progress |
| 5b | Captain's knowledge: truth vs what is known | Proposed |
| 5c | Recruitment rules: candidates, investigation, offers, deadline | Proposed |
| 5d | Godot waterfront recruitment scene | Proposed |
| 6 | Voyage-to-crisis vertical slice | Not started |
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

