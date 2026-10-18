# Milestones and Ticket Plan

Milestone definitions (P0–P3) and acceptance criteria are in `GAME_VISION.md` §10–§11.
This file tracks status and breaks the next work into tickets.

## Status

| Ticket | Title | Status |
|---|---|---|
| 0 | Repository and feasibility audit | Done (2026-10-18) |
| 1 | Solution scaffold, core data model and invariants | Done (2026-10-18) |
| 2 | Simulation clock and task execution | Proposed (next) |
| 3 | Inventory, supplies, and save/load | Proposed |
| 4 | Small playable Godot test scene | Not started |
| 5 | Recruitment prototype | Not started |
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

## Ticket 3 — Inventory, supplies, and save/load

**Depends on:** Tickets 1–2.
**Scope.** `ItemDefinition` (weight, volume, consumable flag) and stacks. Inventories with
capacity limits, including ship cargo. Food and water consumption over time. Versioned JSON
save/load (D-003) covering the clock, RNG state, roster, tasks, inventories and chronicle.
**Acceptance.** A 30-day provisions test consumes the expected amounts. A save followed by a
load produces an equivalent state (structural comparison). A corrupt or newer-version save
fails with a clear error. A dead character stays dead after loading.
**Risks.** Save schema churn while models are young. Mitigation: a version field and a
migration hook from the first save.
