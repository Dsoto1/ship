# Technical Design

Status: P0 implemented in graybox (Tickets 1–4b, 3a–3b). Save format 3. Update this file when architecture or behavior changes.
See `DECISIONS.md` for why each choice was made, and `GAME_VISION.md` for the product vision.

## 1. Solution layout (proposed)

```text
Expedition.sln
src/
  Expedition.Simulation/        net8.0 class library — NO Godot references
    Campaign/                   CampaignState, CampaignPhase, transitions, objectives
    Characters/                 Character, Skills, Needs, Health, Traits, Relationships, Knowledge
    Tasks/                      TaskDefinition, TaskInstance, eligibility, execution
    Items/                      ItemDefinition, Inventory, InventoryId, InventoryCapacity
    Ship/                       Ship, hull/rigging/bilge condition
    World/                      SimulationClock, SimulationRunner (time + task execution), weather (later)
    History/                    Chronicle (append-only event history)
    Content/                    loaders for data/ definitions
    Persistence/                SaveData (file shape), SaveSerializer (versioned JSON + validation), SaveStore (slot files)
    Core/                       IDs, ID allocation, command results, SimulationRandom (PCG32)
tests/
  Expedition.Simulation.Tests/  xUnit — unit and headless simulation scenarios
data/                           JSON definitions: skills, traits, items, tasks, ships, buildings
game/                           Godot 4 .NET project (created in Ticket 4) — presentation only
docs/
```

Folders inside `Expedition.Simulation` follow the logical layers in GAME_VISION §9.1. Each folder
is added only when a ticket needs it.

## 2. Core rules

- **One campaign, one roster.** `CampaignState` owns every `Character` by a stable `CharacterId`.
  Phases (port, voyage, wreck, settlement) refer to characters by ID. They never copy them.
- **Definitions are separate from runtime state.** `*Definition` types are loaded from `data/`
  and are immutable. Runtime objects refer to definitions by string ID (e.g. `"item.biscuit"`).
- **Commands, not mutation.** The presentation layer sends commands (`AssignTask`,
  `AdvanceTime`, `Recruit`). The simulation validates each command and either applies it or
  rejects it with a reason. UI code never sets fields on simulation objects directly.
- **Explicit phase transitions.** A single transition table lists the allowed
  `CampaignPhase → CampaignPhase` moves. An invalid transition is rejected and logged.
- **Deterministic time.** The simulation advances in fixed ticks of one in-game minute (D-004).
  Real-time speed is a presentation concern that maps real seconds to N ticks.
  `SimulationRunner.Advance(ticks)` is the only way time moves. Each tick it processes
  characters in ascending ID order:
  1. Needs change: the baseline rate plus the current task's rate.
  2. Work stops if the character is exhausted.
  3. Otherwise work progresses, at a speed set by skill and slowed when tired.
  4. Any hazard is rolled.
  5. The task completes once its work is done.
- **Tasks.** `TaskDefinition` (data) describes the work. `TaskInstance` (state, in
  `CampaignState.ActiveTasks`) tracks progress. `SimulationRunner.AssignTask` checks eligibility
  (alive, idle, not exhausted, skill, tools, materials) and consumes materials when the task
  starts. `Advance` returns the tasks that completed or were interrupted.
- **Supplies.** Every character has a personal inventory (`InventoryId.ForCharacter`), and the
  campaign names one shared store (`SharedStoreId`): the hold at sea, a camp store ashore.
  Characters take what they need from their own pack first, then from the shared store.
  Items move only through `Inventory.TryTransfer`, which either moves everything or nothing.
- **Eating and drinking** happen inside `Advance`. A character eats or drinks one unit when
  hunger or thirst reaches the threshold set in the rules. At 100, starvation or dehydration
  costs health, which leads to incapacitation and then death, each recorded with its cause.
- **Saving.** `SaveSerializer.Serialize` and `Deserialize(json, content)` convert the entire
  `CampaignState`, including the random generator, to and from JSON. The presentation layer
  chooses the folder and passes it to `SaveStore`.
- **Positions and movement (Ticket 4a).** Locations (`World/LocationDefinition`, from
  `data/locations`) are walkable grids of about one metre per cell, with work spots for specific
  tasks. `CampaignState.Placements` holds each character's location, cell and any walk in
  progress, and is saved.
  - Pathfinding is a deterministic breadth-first search.
  - A walk takes `ceil(steps / walkCellsPerMinute)` ticks, at least one.
  - Tasks with `needsWorkSpot` reserve the nearest free reachable spot. The worker walks there
    and only starts working on arrival.
  - Ordering a move interrupts the current task. Incapacitation stops a walk where it is.
  - The presentation draws positions between cells with `Movement.PositionAt(fractionalTick)`;
    it never decides where anyone is.
- **Survival state through the campaign.** `Character.SurvivalState` can only be changed by
  `CampaignState.ChangeSurvivalState`, which enforces "dead is final" and records the cause.
- **Chronicle.** Important outcomes append an entry: tick, kind, the character IDs involved, and
  named parameters holding IDs, enum names or numbers (see `History/ChronicleParameters`). Entries
  never contain prose. `History/ChronicleFormatter` turns them into English and is the only place
  chronicle wording lives (TECHNICAL_SPEC.md §23, §33).
- **Diagnostics.** `World/NeedLedger` keeps the last day of need and health changes per character,
  by source, in hourly buckets. `SimulationRunner.ExplainNeed` answers "why is this character
  exhausted / hungry / losing health?". Not saved.

## 3. Invariants (enforced and tested from Ticket 1)

1. Every persistent entity ID is unique within a campaign.
2. Skill levels are integers in `0..5`. Need values are within their defined ranges.
3. Survival state is one of `Alive`, `Incapacitated`, `Dead`, `Missing`. Dead characters cannot
   take tasks, move, or return to `Alive` (including after a load).
4. An item quantity exists in exactly one inventory. Phase transitions move items and record
   losses. They never duplicate items.
5. A phase transition never adds or drops characters, except for losses the transition
   explicitly models and records.

## 4. Build and test

Prerequisite: .NET 8 SDK. Godot 4.x .NET is needed only for `game/`, from Ticket 4 onward.

```bash
dotnet build
dotnet test
```

## 5. Open questions deferred to the ticket that needs them

- Tick length and normal/accelerated speeds (Ticket 2).
- Save file location and the policy for keeping older schema versions (Ticket 3).
- Godot version pin, 2D isometric TileMap vs 3D orthographic camera (Ticket 4).
- Historical port and destination (before Ticket 5 content authoring).
