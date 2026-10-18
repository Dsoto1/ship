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
