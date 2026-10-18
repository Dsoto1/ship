# Specification Audit — Technical Spec and Art Bible

**Date:** 2026-10-18
**Scope:** The repository after Tickets 0–3, measured against `TECHNICAL_SPEC.md` and
`ART_BIBLE.md` (added today), together with `GAME_VISION.md`. No code was changed for this audit.

## 1. Repository snapshot (Technical Spec §0)

| Item | Current state |
|---|---|
| Engine / runtime | Godot 4 .NET chosen (D-001); no Godot project yet. Simulation library targets .NET 8 (C# latest). |
| Structure | `src/Expedition.Simulation` (rules, no Godot reference), `tests/Expedition.Simulation.Tests` (xUnit), `data/` (JSON content), `docs/`. |
| Build / test | `dotnet build`, `dotnet test`. Warnings are errors; public APIs need doc comments. |
| Playable functionality | None. Everything runs headless through tests. |
| Persistence | Versioned JSON with migrations, full validation and atomic slot writes (D-003, D-005). |
| Rendering / UI | None yet. |
| Data formats | JSON with comments; unknown fields rejected; content validated on load. |
| Tests / CI | 129 tests, all passing. **No CI.** |
| Warnings / errors | None. |
| Dependencies / licences | .NET 8 (MIT), xUnit (Apache-2.0), coverlet (MIT), Godot (MIT, planned). Nothing restrictive. |

## 2. What already meets the spec

- **Stable IDs (§2.5):** `CharacterId` and `InventoryId` are never derived from names or array positions, and are never reused.
- **Simulation separate from presentation (§2.1, §31):** the rules library cannot reference Godot, and UI-facing changes go through commands (`SimulationRunner`, `CampaignState`).
- **Explicit phase transitions (§4.1):** one table. Invalid transitions are rejected and recorded.
- **Death is persistent (§5.8):**
  - the dead get no tasks and no provisions;
  - death survives save/load;
  - a loaded save that gives a dead character a task is rejected.
- **Data-driven, validated content (§2.2, §25):** skills, items, tasks and rules live in data. Duplicate IDs, unknown item or skill references, bad ranges and misspelled fields all fail loudly.
- **Seeded, reproducible randomness (§2.4, §24):** one PCG32 generator. Its full state is saved.
- **Needs respond to activity and supplies (§5.6, P0.6):** a baseline rate plus the task's rate. Eating and drinking work, and starvation and dehydration have consequences.
- **Generic containers (§9.2):** a character's pack and a hold are the same `Inventory` type. Transfers move all items or none.
- **Save/load (§22):** schema version, migrations, atomic writes. Tests cover save → load → continue identically, including through a storm-damaged crew.
- **Invariant tests (§26.4):**
  - unique IDs;
  - no negative quantities;
  - no work by the dead;
  - transfers conserve items;
  - load doesn't duplicate items.

## 3. Gaps against the spec

Ordered by how soon they block work. Each **[REQUIRED]** item in the spec is marked R.

| # | Spec | Gap | When it matters |
|---|---|---|---|
| G1 | §23, §33 (R for IDs) | **Chronicle entries bake names into English text.** The spec wants entries to reference stable IDs internally and resolve names and text at presentation. Today a later rename, translation or richer journal UI would need to parse strings. | Before Ticket 4. The Godot chronicle panel and existing saves will lock in the format. |
| G2 | §23.1 (R) | **Diagnostics can't answer "why is this character exhausted?"** Interruptions and injuries are recorded, but need changes have no record of their causes. | Before Ticket 4's character panel. |
| G3 | §5.4 (R) vs §5.3 / Handoff §4.3 | **Physical attributes are stored as skills.** See conflict C1. | Before recruitment content (Ticket 5). Cheaper now, while saves are young. |
| G4 | §24 (preferred) | **One random stream for everything.** Adding weather rolls later would shift every injury roll and change the outcome of existing seeds. | Before weather (Ticket 6). Changing it later is more disruptive. |
| G5 | §7.3 (R) | **No task queue, priority, or resume after an emergency.** Each character has at most one task. | Before the storm crisis. The UI design in Ticket 4 depends on it. |
| G6 | §6, Handoff §4.6, P0 | **No relationships** (asymmetric, with reasons). The Handoff's P0 character model includes them. | P0 exit, and before recruitment conflicts. |
| G7 | §20, P0.9 (R) | **No positions, locations or movement in the simulation.** Tasks have no location, so characters can't walk to work. The spec forbids tying identity to navigation agents, so position must be simulation state. | Ticket 4. |
| G8 | §25 | **Traits and conditions aren't defined or validated.** They are free-form strings today. | Before traits change behaviour (Ticket 5). |
| G9 | §38 #15, #2 | **Missing named tests:** "time acceleration gives the same results" (it holds by construction, but isn't tested) and a 90-day voyage (we test 30 days). | Cheap; add now. |
| G10 | §0, §26 | **No CI.** Tests only run when someone runs them. | Now. |
| G11 | §4 | **Campaign has no `campaignId` and no `captainCharacterId`.** The captain is a persistent character in the design. | Before Ticket 4, so direct control has someone to control. |
| G12 | §5.2 (R) | **No split between truth, player knowledge and belief.** | Recruitment (Ticket 5). Not needed earlier. |
| G13 | §9 | **Items are counted stacks with no instance state** (condition, freshness). The spec allows this for P0 ("instances as needed"). | Spoilage and damaged cargo (Ticket 6). |
| G14 | §5.7 | **Health has only named conditions:** no severity, work restrictions or recovery. | Medical crisis (Ticket 6). |

Not gaps yet: ship, weather, navigation, charter, settlement, events, simulation LOD and succession all belong to P1 milestones that haven't started.

## 4. Conflicts between documents

- **C1: Are physical attributes skills?** Handoff §4.3 and Tech Spec §5.3 list Strength, Endurance, Coordination and Perception as a skill group. Tech Spec §5.4 **[REQUIRED]** says training and physical capability are separate. The current `data/skills.json` follows §5.3.
  *Recommendation:* follow §5.4, the more specific and stricter rule. Model `Attributes` separately from skills, on the same 0–5 scale and with stable IDs, and let task speed depend on both (for example, hauling uses Strength while rigging uses skill).
- **C2: What to build first visually.** Handoff Ticket 4 calls for a "small playable test scene". The Art Bible §25 says the first art task is the **Test A — Port Recruitment** feasibility pass. Tech Spec P0.9 puts the interaction layer before the P1 port.
  *Recommendation:* these are sequential, not contradictory. Ticket 4 is a **graybox** interaction layer (placeholder shapes, P0.9), which the Art Bible allows ("temporary/modular assets"). Test A follows once the port region is approved, because Test A needs real historical research.
- **C3: How often needs update.** Tech Spec §8.1 *prefers* hunger on a coarse interval; we update every one-minute tick. That's fine at a crew of about 30 (measured in tests: a 30-day voyage runs in milliseconds). Revisit with simulation LOD (§21). No action now.

## 5. Art Bible implications for the technical plan

- **Projection and renderer are approval-required** (Art Bible §24 items 5–6, Tech Spec §40).
  - **The requirements:**
    - weather and time-of-day lighting that affects the whole ship (§9, §10);
    - wetness, damage and repair states accumulating on characters and the ship (§4.7, §6.3, §15);
    - modular characters recognisable by silhouette (§16);
    - ship motion (§12);
    - two zoom levels (§3).
  - **Why they point to 3D:** each of those multiplies the hand-drawn frames a 2D sprite pipeline needs. In a 3D orthographic camera they are material and lighting changes.
  - **Recommendation:** a 3D scene with an orthographic isometric-style camera, Godot's Forward+ renderer (Metal on Apple Silicon), and a painterly look from hand-painted textures plus a light stylised shading pass. This avoids photorealism and cartoon outlines, both of which the Art Bible forbids.
  - **Status:** proposed until the owner approves.
- **Characters need appearance data in the simulation** (Handoff §4.1 identity: appearance; Art Bible §4).
  - **Why it belongs in the simulation:** the same person must look the same in every phase, which makes appearance persistent state.
  - **What it holds:** height, build, age cues, hair and facial hair, and clothing choices.
  - **Approach:** store the appearance in the character record and the save, and render it from modular parts.
- **Condition must be visible** (Art Bible §4.7). Injuries, exhaustion and wet clothing should be shown through state overlays and posture driven by simulation state. The data model already exposes needs and conditions; G14 (severity) feeds this.
- **Historical research is a blocker for Test A** (Art Bible §24.1–4, Tech Spec §41). The departure port, the hero ship type and the destination region need the owner's approval, informed by research. They don't block Tickets 4a–4c below, which use placeholder shapes.
- **Readability rules can be adopted now.** For example: no permanent outlines, and gameplay state colours kept distinct from environment colours. The graybox scene will follow them from the start.

## 6. Recommended next tickets (revised plan)

Smallest first. Tickets 3a–3e are headless, so I can build and test them in the cloud while the owner installs Godot.

| Ticket | Scope | Closes |
|---|---|---|
| **3a** CI | GitHub Actions: `dotnet build` + `dotnet test` on every push. | G10 |
| **3b** Structured chronicle and diagnostics | Chronicle entries become a kind, the IDs involved and typed parameters; English text is formatted by a separate presenter. A per-character "recent need changes by cause" log answers "why exhausted / hungry". Add the time-acceleration equivalence test and the 90-day voyage test. Save format v2, with the first real migration. | G1, G2, G9 |
| **3c** Attributes and random streams | Separate physical attributes from skills (C1); tasks can name an attribute. Split the random generator into named streams (tasks, characters, weather, events), each saved. Add `campaignId` and `captainCharacterId`. Save format v3. | G3, G4, G11 |
| **3d** Relationships, traits and conditions as content | Asymmetric relationships (trust, respect, affection) with recorded reasons, saved. Trait and condition definitions in `data/`, validated, and references to them checked. | G6, G8 |
| **3e** Task queue and priority | Each character gets an ordered task queue with priorities. An emergency task pre-empts routine work, and the routine work resumes afterwards with its progress kept. Task instances get stable IDs. | G5 |
| **4a** Positions and movement in the simulation | A walkable grid per location (a ship's deck first), character positions, task work spots, and deterministic pathfinding in the simulation, so movement is headless, testable and saved. Godot only animates between grid steps. | G7 |
| **4b** Godot graybox scene (P0.9) | The Ticket 4 already planned, on top of 4a: placeholder deck and crew, selection, task assignment with refusal reasons, time controls, save/load, chronicle panel. **Run locally by the owner.** Completes the P0 gate. | P0 exit |
| **4c** Appearance record | Persistent appearance data per character, saved; graybox figures vary by height and build so individuals can be told apart. | Art Bible §4 groundwork |

Then P1. Test A (port visual feasibility) follows the research and approval of the departure port.

## 7. Decisions needed from the owner

1. **[APPROVAL REQUIRED] Projection and renderer:** 3D with an orthographic camera and Forward+ (recommended), or 2D isometric sprites.
2. **Revised order:** do the headless alignment tickets 3a–3e before the Godot scene (recommended), or go straight to the Godot scene and fit these in later.
3. **Later, not now:** departure port and region, hero ship type, destination region and Indigenous communities. I'll prepare a short researched options brief when we reach P1. These are not to be decided casually.
