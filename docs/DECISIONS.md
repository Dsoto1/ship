# Decisions

Consequential project decisions: what was chosen, the alternatives, and why.
Add new entries at the bottom. Do not rewrite old entries; supersede them with a new one.

---

## D-001 — Engine: Godot 4 (.NET) with an engine-independent C# simulation core

**Date:** 2026-10-18
**Status:** Accepted (chosen by project owner)

**Decision.** Presentation, input and scenes use Godot 4 (.NET build). All gameplay rules
(campaign state, characters, tasks, inventory, ship, world clock, persistence) live in a plain
C# class library that has **no reference to Godot**. The Godot project references that library
and only reads state from it or sends commands to it.

**Alternatives considered.**
- *Godot 4 + GDScript:* fastest to iterate in the editor, but weaker typing, and the simulation
  would be tied to the engine. Headless tests would need GUT running inside Godot.
- *Unity (C#):* mature tooling, but a proprietary licence and a heavier editor. It offers no
  advantage for a sim-first prototype.
- *TypeScript + web renderer:* easiest to run in CI, but a lower ceiling for a long-term
  isometric game.

**Why.** The handoff requires that simulation is independent of presentation and is testable
headless (§9.1, §9.4). A plain C# library tested with xUnit meets both requirements directly
and runs with only the .NET SDK, without Godot. Godot is free and open source, and handles
isometric 2D/3D well.

**Consequences.**
- Target framework `net8.0` for the simulation library, which Godot 4.x .NET supports.
- The Godot editor/runtime cannot be downloaded in the current cloud dev environment (GitHub
  releases are blocked by the network policy). Simulation work and tests are unaffected.
  Scene work (Ticket 4+) must be run locally by the developer.
- **No browser build for now.** Godot 4 cannot export C# projects to the web. As of late 2026
  this is still unsupported, or at best experimental. Distribution targets desktop
  (macOS, Windows, Linux) through itch.io and later Steam. If a browser demo becomes important,
  revisit this decision rather than working around it.

---

## D-002 — Deterministic, serializable RNG

**Status:** Accepted (Ticket 2)

`System.Random`'s internal state cannot be serialized, so a save could not resume the same
random sequence. Instead, `Core/SimulationRandom` is a PCG32 generator owned by
`CampaignState`, with `SaveState()`/`FromState()` for persistence. All gameplay randomness
must come from it. Characters are processed in ascending ID order each tick, so a given seed
and starting state always produce the same results. Outcomes of random rolls (such as injuries)
are recorded in the chronicle.

---

## D-003 — Save format: versioned JSON

**Status:** Accepted (Ticket 3)

Saves are JSON files written with `System.Text.Json` (`Persistence/SaveSerializer`). The file
shape is spelled out in `Persistence/SaveData.cs`, separate from the runtime classes, so any
format change shows up in review.
- **Versioning.** Each file has a top-level `schemaVersion`. Older files are upgraded one version
  at a time by migration functions that edit the raw JSON, so old C# types never need keeping.
  Files from a newer build are refused with a message telling the player to update.
- **Validation.** Loading checks the whole file against the current content: unknown tasks or
  items, dead characters with tasks, duplicate IDs, out-of-range values, a broken chronicle
  sequence, an invalid random-generator state, unknown fields. Any problem raises
  `SaveLoadException` with a readable reason. The loader never returns a partial or fresh campaign.
- **Capacity.** Inventory capacity is not re-checked on load, so retuning item weights cannot make
  old saves unloadable.
- **Random generator.** Its 64-bit state is stored as decimal strings, because some JSON tools
  lose precision on large integers.

JSON is readable during development. A binary or compressed format can come later if size or
speed requires it.

---

## D-004 — One tick is one in-game minute; needs are fractional

**Date:** 2026-10-18
**Status:** Accepted (Ticket 2)

**Decision.** The simulation advances in fixed ticks of one in-game minute. Content defines need
rates per hour, and the runner applies 1/60 of the rate each tick. Needs are therefore stored as
`double` (still in the 0–100 range) rather than `int`.

**Alternatives considered.** Ten-minute or hourly ticks are cheaper to simulate, but too coarse
for crises (a storm, a fall from the rigging) where minutes matter. Integer needs at one-minute
ticks would round small rates such as 1 fatigue per hour down to zero.

**Why.** With a 30-person crew, one-minute ticks are cheap: a 40-day voyage is about 58,000 ticks.
Accelerated time is a presentation concern, for example 1 real second = 1 in-game hour =
60 ticks. Real-time speeds are not decided yet; Ticket 4 will settle them.

---

## D-005 — Save location: the standard per-user folder

**Date:** 2026-10-18
**Status:** Accepted (chosen by project owner)

**Decision.** Saves live in the operating system's standard per-user application data folder,
under `saves/`. The game resolves the folder through Godot's `user://` path and passes it to
`Persistence/SaveStore`. The simulation library never decides where files go.

In Ticket 4 the Godot project will enable `application/config/use_custom_user_dir` with the
name `The Expedition`, so the paths are:
- macOS: `~/Library/Application Support/The Expedition/saves/`
- Windows: `%APPDATA%\The Expedition\saves\`
- Linux: `~/.local/share/The Expedition/saves/`

Each slot is one `<slot>.expedition.json` file. Writes go to a temporary file first and are then
moved over the old save, so a crash while saving keeps the previous save.

**Alternative considered.** A folder the player chooses: more flexible, but an extra setup step,
and Steam Cloud and itch.io expect the standard location.

---

## D-006 — 3D world with an orthographic isometric camera, Forward+ renderer

**Date:** 2026-10-18
**Status:** Accepted (chosen by project owner; ART_BIBLE.md §24 items 5–6)

**Decision.** The world is a 3D scene viewed through an orthographic camera at an isometric-style
angle, rendered with Godot's Forward+ renderer (Metal on Apple Silicon). The painterly
"illustrated historical realism" look comes from hand-painted textures and light stylised
shading, not from photoreal materials or cartoon outlines. Early scenes use graybox shapes.

**Alternative considered.** 2D isometric sprites. Simpler at first, but the Art Bible's weather
lighting, wear and damage states, modular characters, ship motion and two zoom levels would each
multiply the hand-drawn frames needed.

**Consequences.** One grid cell maps to one world metre. Simulation grid X maps to world X, and
grid Y to world Z (row 0 is the bow). Exact camera angle and zoom range are tuned in the graybox
scene and remain open until the Art Bible's Test A.

---

## D-007 — Real-time speeds

**Date:** 2026-10-18
**Status:** Accepted for the graybox; to be tuned in playtesting (resolves the open item in D-004)

| Speed | In-game minutes per real second | 40-day voyage takes |
|---|---|---|
| Pause | 0 | — |
| 1× | 1 | about 16 hours |
| 10× | 10 | about 1.6 hours |
| 60× | 60 (1 hour per second) | about 16 minutes |

1× is for direct control and crises, and 60× is for routine sailing. A frame never simulates more
than 600 ticks, so a slow frame cannot snowball. Automatic slowdown on critical events
(TECHNICAL_SPEC.md §8) comes with the event system.

---

## D-008 — Named random streams; physical attributes separate from skills

**Date:** 2026-10-18
**Status:** Accepted (Ticket 3c; resolves SPEC_AUDIT.md conflict C1)

**Random streams.** `Core/RandomStreams` holds one PCG32 stream per domain: `tasks`, `characters`,
`weather` and `events` (TECHNICAL_SPEC.md §24). Each is derived from the campaign seed and a
fixed per-name stream number, so adding rolls in one domain never changes another's results; a
test checks that weather rolls leave injury outcomes untouched. This supersedes the single
generator in D-002, which was otherwise unchanged. Saves store the seed and every stream. New games
in Godot use a fresh random seed.

**Attributes.** Strength, endurance, coordination and perception are `Characters/Attributes`, not
skills (TECHNICAL_SPEC.md §5.4 over the skill list in §5.3 and Handoff §4.3). Same 0–5 scale, but an
unrecorded attribute is 2 (ordinary), not 0. A task can name an attribute, which multiplies work
speed alongside skill, so a strong novice and a weak expert differ. Pumping uses strength;
repairing rigging uses coordination.

**Campaign identity and captain.** Campaigns have a `CampaignId` and keep their `Seed`.
`CaptainId` names the character the player leads as. Appointment is recorded and requires a living,
able character. Succession rules remain approval-required (TECHNICAL_SPEC.md §18).

Save format 4. The upgrade from format 3 moves physical "skills" into attributes and continues the
old generator as the `tasks` stream, so injury rolls carry on exactly.

---

## D-009 — Directed relationships with reasons; traits and conditions as content

**Date:** 2026-10-18
**Status:** Accepted (Ticket 3d)

**Relationships** (TECHNICAL_SPEC.md §6) are directed: `CampaignState.Relationships` keys them by
(from, to), and A→B is independent of B→A.
- **Dimensions:** three, trust, respect and affection, each -100..100 with 0 neutral. Unrecorded
  pairs are neutral.
- **Changing them:** every change goes through `ChangeRelationship` with a cause. The cause is kept
  in the relationship's history, so the UI can explain why, and recorded in the chronicle.
- **Who can change:** the dead form no new views, but others' views of the dead can still change.
  Relationships with the dead remain valid history.
- **Scoring:** exact amounts are tunable content, not hardcoded rules. Nothing changes relationships
  automatically yet; events, promises and conflicts will (P1).

**Traits and conditions** are content: `data/traits.json` (the five prototype traits) and
`data/conditions.json` (injuries and illnesses with display names).
- **Checks:** hazards must name a defined condition, and loading rejects unknown traits or conditions
  on characters.
- **Behaviour:** traits don't change behaviour yet. Each trait's behaviour arrives with the system it
  affects (orders, recruitment, events), as TECHNICAL_SPEC.md §5.5 requires.

Save format 5. The upgrade from format 4 adds an empty relationship list.
