# Technical Design

Status: proposed architecture for P0. Update this file when architecture or behavior changes.
See `DECISIONS.md` for why each choice was made, and `GAME_VISION.md` for the product vision.

## 1. Solution layout (proposed)

```text
Expedition.sln
src/
  Expedition.Simulation/        net8.0 class library — NO Godot references
    Campaign/                   CampaignState, CampaignPhase, transitions, objectives
    Characters/                 Character, Skills, Needs, Health, Traits, Relationships, Knowledge
    Tasks/                      TaskDefinition, TaskInstance, eligibility, execution
    Inventory/                  ItemDefinition, ItemStack, Inventory, cargo capacity
    Ship/                       Ship, hull/rigging/bilge condition
    World/                      SimulationClock, weather (later)
    History/                    Chronicle (event history)
    Content/                    loaders for data/ definitions
    Persistence/                save/load, schema version, migrations
    Core/                       IDs, RNG, validation results
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
- **Deterministic time.** The simulation advances in fixed ticks (for example, 1 tick = 1 in-game
  minute; to be confirmed in Ticket 2). Real-time speed is a presentation concern that maps
  real seconds to N ticks.
- **Chronicle.** Important outcomes append an entry to the chronicle: tick, type, the IDs
  involved, and a causal note. This is the source for the journal and for debugging.

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
