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
| 240× | 240 (4 hours per second) | about 4 minutes (added after V3, owner: "need faster sim speed") |
| 720× | 720 (12 hours per second) | about 80 seconds |

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

---

## D-010 — Task queues with two priorities; paused work resumes

**Date:** 2026-10-18
**Status:** Accepted (Ticket 3e)

Each character has one active task and an ordered queue of waiting orders (TECHNICAL_SPEC.md §7.3).

**Priorities.** There are two:
- **Normal** orders wait in the order given.
- **Urgent** orders pause normal work and start at once, or wait behind other urgent work.

**Pausing.** A paused task goes back to the front of the normal orders. It keeps its progress and
the materials it already used (they are never used twice), and releases its work spot. When it
resumes it finds a spot again.

**Starting queued work.**
- The next order starts automatically when the character is idle and able.
- A character who is too tired keeps waiting rather than dropping work.
- An order that cannot start for any other reason (no tool, no free spot) is dropped and recorded,
  so it never silently disappears.
- A walk the player ordered finishes before queued work starts.

**Refusals.** `QueueTask` refuses at once only what can never work: the dead or missing, an unknown
task, or a missing skill. Other conditions are checked when the order starts. `CheckOrder` reports
the same answer without changing anything, for the interface.

**Survival states.** The dead and the missing lose their queue. Incapacitated characters keep it.

**IDs.** Every order has a stable `TaskId` (`work-0001`), never reused.

Save format 6. The upgrade from format 5 gives existing active tasks IDs.

**Not yet:** standing orders and watch rotations (automatic routines). They will build on the queue
once ship systems exist (Ticket 6) and need the owner's input on how delegation should feel.

---

## D-011 — Appearance is saved simulation state, generated from data

**Date:** 2026-10-18
**Status:** Accepted (Ticket 4c). The palette is a placeholder pending research.

Each character has an `Appearance`: height, build, skin tone, hair colour and style, facial hair, two
clothing dyes and a distinguishing mark. It is stored as option IDs and saved, so a person looks the
same in every phase and after loading (ART_BIBLE.md §4, §15). Presentation decides how the IDs are
drawn and described.

**Generation.** `AppearanceGenerator` uses only the `characters` random stream and keeps combinations
plausible (ART_BIBLE.md §16):
- age-gated options (greying from 35, white hair from 50, receding from 28, bald from 35) grow likelier
  with age;
- stronger people tend to be broader;
- heights fall in a bell-shaped spread of 147–195 cm.

**Data.** Options live in `data/appearance.json` with weights and minimum ages. The current names and
colours are **placeholders**. Period clothing, dyes and regional appearance are [RESEARCH] items
(ART_BIBLE.md §4.6, §5) to replace once the departure region is approved.

**No sex or gender field yet.** Facial hair is generated for everyone in the all-male test crew. A
proper identity model for recruitment (sex, languages, origin detail) belongs to Ticket 5 and needs
the owner's input.

Save format 7. Older saves load with no appearance, and the game draws a plain fallback figure.

---

## D-012 — A fictional setting, inspired by history rather than reconstructing it

**Date:** 2026-10-18
**Status:** Accepted (owner's direction). Supersedes the "pick one real package" framing in
P1_OPTIONS_BRIEF.md, which becomes a source of inspiration.

**Decision.** The world is a fictional amalgam of early-1500s Atlantic history, not one real
voyage, place and year:
- **Places, powers and people are invented.** The port city, crown, sponsor and destination coast
  draw freely on Seville, Lisbon, Palos, the Caribbean, Tierra Firme, Brazil and the North Atlantic.
  Names should sound right for their culture, but the game never claims to depict a specific
  real place or date.
- **Indigenous peoples are fictional, but specific.** Each people has its own coherent language
  sound, politics, economy, settlements, material culture and goals. They are inspired by real
  coastal societies in broad strokes (environment, subsistence, trade, how authority works), but
  never copy sacred or identifying elements of a real people, and never form a generic kit or a
  collage of unrelated real cultures. Each is an actor with its own interests: it can trade, refuse,
  ally, bargain and leave.

**What still applies from the Art Bible and Technical Spec.**
- **Plausibility, not certainty.** Tools, ships, clothing, food and technology should fit roughly
  1490–1530. Avoid obvious anachronisms such as later-period pirates or 18th-century rigging. The
  [RESEARCH] tags become "check that it's plausible and coherent" rather than "verify against one
  source".
- **No empty land.** The coast is lived-in. Indigenous communities are never the hostile default,
  and violence and exploitation are not airbrushed.
- **Coherence across the setting.** Each culture, real-inspired or invented, stays consistent within
  itself, and the decisions are recorded in a setting document.

**Why.** It gives design freedom: compressed timelines, a sponsor and charter shaped for gameplay,
and a destination built for the survival and settlement loop. It also removes research blockers,
and it avoids misrepresenting a specific real people.

**Consequences.**
- The next document is a short **setting outline** (`docs/SETTING.md`): names and one-paragraph
  sketches of the port, crown, sponsor, ship, destination coast and its peoples, for the owner's
  approval.
- Content IDs and data stay neutral, so names can change later without breaking saves.

---

## D-013 — Setting outline approved

**Date:** 2026-10-18
**Status:** Accepted (owner approved `docs/SETTING.md` as drafted)

The setting outline is approved:
- **The Old World:** Castrena, Almara and its outport La Bocana, the Chamber of the Ocean Sea,
  Lusara and the Brennish.
- **The sponsor and hook:** the Ledra–Varzi Company's embrawood lease, and the silent trading post
  at Punta Ceniza.
- **The ship:** the caravel *Garza*.
- **The far coast:** the Ember Coast, with the Ahuneri and the Tesiwa.
- **The recruits:** the identity fields and the candidate list.

It is now the content source for Ticket 5 onward. Names remain changeable, since content IDs are
neutral.

---

## D-014 — Standing duties and self-care: crew act on their own when idle

**Date:** 2026-10-19
**Status:** Accepted (owner: "the crew needs automating")

**Decision.** What a character does each minute is chosen in this order (TECHNICAL_SPEC.md §19.1):
1. **Player orders** (the queue, D-010), urgent before normal.
2. **Self-care:** idle and fatigue at or above `restAtFatigue` (70) → rest.
3. **Standing duties:** idle with no orders → the next duty in their list that can start, in turn.

Each task records its origin (Order, Duty, SelfCare). Any player order takes over from duty or
self-care work at once; that work is paused behind the orders and resumes later. Work the character
took up themselves never delays an order.

**Why.** It removes clicking for routine work while keeping the player's word final, and it is the
base for watch rotations and roles (bosun, cook) once ship systems exist (Ticket 6).

**Not yet:** time-of-day watches (the dog watches, night watch), duties assigned by role rather than
per person, and duties that respond to need (pump only when the bilge is high). These need ship
systems.

---

## D-015 — Playable voyage before the port

**Date:** 2026-10-19
**Status:** Accepted (owner: "there is no gameplay yet… I have 0 investment in these people or the
ship, nothing seems to happen and we see nothing around us"; approved the plan)

**Decision.** Build one short, playable voyage before finishing recruitment and the port, in four
steps the owner can play after each:
1. **V1, the world around you:** sea, sky, day and night, a real ship, people instead of pills.
2. **V2, the ship in danger:** leaks and the bilge, rigging wear, supplies running down, distance to
   the Ember Coast, a way to win or lose; duties that respond to need.
3. **V3, things happen:** authored events that pause and offer choices with lasting consequences;
   crew who voice concerns.
4. **V4, choose who comes:** a simple hiring screen before sailing, using the 5b knowledge model.

**Why.** The simulation foundation (P0, 5a, 5b, duties) is what the voyage runs on, but without
stakes, events and a visible world it is not a game. The full port (5c, 5d) waits until the voyage
is worth playing.

---

## D-016 — The voyage: leaks, wear, weather and distance

**Date:** 2026-10-19
**Status:** Accepted (V2; numbers are placeholders to tune)

**Decision.**
- **The ship:** a voyage is persistent state: hull, bilge and rigging (0–100), distance, and the
  weather, all saved.
- **Weather:** five kinds, from calm to storm, on the weather random stream, defined in
  `data/voyage.json`.
- **Each minute:**
  - the bilge rises by `(baseLeak + hullLost × leakPerHullPoint) × weather.leakMultiplier`;
  - the hull and rigging wear;
  - the ship moves at `wind × (floor + (1 − floor) × rigging) × (watch kept ? 1 : 0.4)`.
- **Endings:** a full bilge founders her (phase: Shipwreck survival); running the distance makes
  landfall (phase: Landfall).
- **Tasks affect the ship** through data: `shipPerHour`, `shipOnComplete`, `crewOnComplete`,
  `keepsWatch`.
- **Duties (amends D-014):** they are a priority list, each with an optional `dutyWhen` condition.
  They are no longer a rotation.

**Why.** It gives the routine work real consequences and gives the player things to watch and
decide: how many hands on the pumps, when to spend planks, whether the stores will last.

---

## D-017 — Events: authored situations with choices, decided by the captain

**Date:** 2026-10-19
**Status:** Accepted (V3; content and numbers are a first pass)

**Decision.**
- **Content:** events are data (`data/events`), each with a trigger, the people involved, text, and
  choices.
- **Rolls:** a choice may depend on a skill check, rolled on the events random stream. The odds are
  shown to the player in words.
- **Waiting:** an event waits in the campaign (saved) until the captain decides, and the decision is
  recorded in the chronicle.
- **Effects** are a fixed set: items, ship, distance, needs, health, conditions, relationships,
  survival state, and rations. They are applied through the same rules as the rest of the
  simulation.
- **Short rations** serve water and food once a day each.
- **Stress:** at 70 or more, people work more slowly.

**Why.** It gives the player decisions with consequences that the simulation carries forward:
trust, health, stores and distance. That is what makes the crew and the voyage worth caring about
(GAME_VISION.md §8).

---

## D-018 — Sail orders, and repairs the player can see

**Date:** 2026-10-19
**Status:** Accepted (owner feedback after V3: "I did not manage the hull properly and just repaired
it whenever and eventually we ran out of supplies to repair, storms took the ship out")

**Decision.**
- **Sail plan.** The captain orders one of three plans, which is saved and recorded:

  | Plan | Speed | Hull and rigging wear, sea strikes | Leaks |
  |---|---|---|---|
  | Press on | ×1.2 | ×1.5 | ×1.25 |
  | Normal | ×1 | ×1 | ×1 |
  | Heave to | ×0.08 | ×0.35 | ×0.55 |

- **Repairs left.** The voyage bar shows how many hull and rigging repairs the stores can still make.
- **Later repairs.** Duty repairs wait for real damage: hull below 75%, rigging below 75% (they were
  85% and 88%).

**Why.** Running out of materials was the right kind of failure, but the player could neither see it
coming nor do anything about storms except patch. Simulated over 30 seeds:

| Captain | Arrived (of 30) |
|---|---|
| Never touches the sails | about 17 |
| Heaves to in storms | 24–25 |
| Heaves to in storms and presses on in fair weather | 25–28, about two days sooner |

---

## D-019 — Hiring in port: truth, claims and references

**Date:** 2026-10-19
**Status:** Accepted (V4; candidates and prices are a first pass)

**Decision.**
- **Start in port:** a campaign starts in PortPreparation with only the captain aboard and hiring
  open (`CampaignState.Recruitment`, saved).
- **Candidates are data,** and are not campaign characters until hired.
- **What the captain learns** about a candidate is kept as facts with a source: claim, reference or
  test. On hiring, those facts become knowledge entries for the new character.
- **Time is the limit:** inquiries are limited. Talking is free; asking around and testing cost one
  each.
- **Money is shared:** wages and the chandler's stores come out of the same purse.
- **What the HUD shows:** for anyone but the captain, skills and traits as the captain knows them,
  never the truth (TECHNICAL_SPEC.md §5.2).
- **Lost captain:** if the captain dies or goes missing, the able crew member the others trust and
  respect most (plus leadership) takes command. **This is a placeholder;** the succession rules need
  the owner's approval (TECHNICAL_SPEC.md §18.1).
- **All hands lost:** if no one is left alive aboard, the voyage ends and the campaign fails.

**Why.** It gives the player a stake in the crew from the first minute: they chose these people,
on what they knew, with money they could have spent on water.

---

## D-020 — Realign with the vision: a living port, real art, continuation after disaster

**Date:** 2026-10-19
**Status:** Accepted (owner: "we are getting away from the game's vision… the first part should feel
alive (a la Hitman, with all the NPCs in this port). It needs to look amazing not so-so or be a bunch of
menus"). The review is in `docs/VISION_ALIGNMENT.md`.

**Decision.**
- **Port first.** The port becomes a living district before more voyage features:
  - about 25 people with schedules and visible work;
  - a captain who walks it;
  - click anyone for a record of what you know;
  - investigation by watching, talking, asking and testing in the world;
  - a deadline with real consequences.
- **The V4 hiring menu is a stopgap.** Its rules and data stay; its presentation moves into the
  world (R1–R2).
- **The art pipeline is decided before any more art.** The code-built visuals are a graybox, not the
  target. The pipeline is chosen with the owner, then proven in validation scenes, port first
  (ART_BIBLE §23 and §25).
- **Disasters continue the game.** Shipwreck and landfall lead into survival and camp (R5–R6), not
  end screens (pillar 3).

**Why.** GAME_VISION §2, §5, §7 and ART_BIBLE §1, §7.1, §20 and §23, as reviewed.

---

## D-021 — Art pipeline: hybrid

**Date:** 2026-10-19
**Status:** Accepted (owner chose "Hybrid" when asked, under ART_BIBLE §20 and §24)

**Decision.**
- **Now:**
  - free CC0 assets: textures and materials, props, and a rigged, animated human base;
  - AI-generated props and buildings, each checked against the Art Bible's AI rules (§19:
    plausibility, construction, consistency) before use.
- **Later:** a commissioned artist for the hero pieces: the *Garza*, key characters and portraits.
- **Rules:**
  - Every asset records its source and licence in an asset manifest.
  - Nothing is scaled up until the port validation scene (Test A, R3) shows a coherent illustrated
    historical realism at gameplay zoom.
  - Paid generation services are an owner decision (TECHNICAL_SPEC §41); none is used without asking.

**Why.** Code-built shapes cannot reach the target. This gets the biggest quality jump at the
lowest cost, and keeps hand-made art for what matters most.

---

## D-022 — The living port: everyone is a character; investigation happens in the world

**Date:** 2026-10-19
**Status:** Accepted (R1, following D-020)

**Decision.**
- **Everyone is a real character.** All port people are campaign characters, marked Local, with a
  daily schedule. Hiring changes them to Crew. There is one roster, as GAME_VISION §2.1 asks.
- **What the captain learns comes from the world:**
  - work seen within sight range (observation);
  - demonstrations watched (test);
  - what the person says (claim);
  - what particular people say about them (reference, or rumour);
  - things seen happening (secrets).

  Facts are only learned when the captain is close enough. Speaking needs the captain within two
  cells.
- **Time replaces inquiries:** people leave at set times, stock sells, and the convoy sails at
  dawn on day 3. Lateness is allowed, at a price.
- **The V4 hiring menu is removed.** Its candidates' truths, claims and references live on in
  the people data.

**Why.** GAME_VISION §5 and TECHNICAL_SPEC §15: a compact, active port, NPCs with schedules, and
investigation by observing, conversing, references and demonstrations.


---

## D-023 — The port brief is the authority for the port

**Date:** 2026-10-19
**Status:** Accepted (owner supplied `docs/PORT_EXPERIENCE.md` to guide the build)

**Decision.**
- `PORT_EXPERIENCE.md` governs the port: districts, the ship as landmark, simulation tiers,
  activity loops, leads, ambient events, weather, sound, recognition and departure.
- Its historical rules apply to the fictional analogue of SETTING.md (D-013).
- R2 is split into R2a–R2e following its §23 order; its four validation scenes become R3
  (`VISION_ALIGNMENT.md` §5).
- **Port time is slowed so people can be watched.** Walking in port is about 2.5 m per game
  minute, and the slowest port speed is half a game minute per second. A day still passes in
  minutes at the faster speeds.

**Why.** The brief's north star is a port that is crowded, purposeful and watchable (§1, §7), with
the ship always in view (§5).

---

## D-024 — The voyage, navigation and settlement briefs are authorities

**Date:** 2026-10-19
**Status:** Accepted (owner supplied the three briefs)

**Decision.**
- `VOYAGE_EXPERIENCE.md`, `NAVIGATION.md` and `SETTLEMENT_EXPERIENCE.md` govern their phases, with
  the main documents.
- Their historical rules apply to the fictional analogue (D-013). The settlement's region and peoples
  are the approved Ember Coast, Ahuneri and Tesiwa.
- The old R4 ("voyage gaps") becomes R4a–R4h, following the briefs' implementation orders. The old R5
  stays the shipwreck transition. The old R6 becomes R6–R8: camp, establishment, and the world
  around it (`VISION_ALIGNMENT.md` §6).
- **Navigation reuses the knowledge model:** true position is simulation truth; the chart is
  belief, with sources (charted, reported, observed, estimated).
- The briefs' approval items run on proposed defaults until the owner decides
  (`VISION_ALIGNMENT.md` §6.5).

**Why.** The briefs ask for a voyage driven by interacting systems rather than event cards (VOYAGE
§1, §42), a chart of belief rather than truth (NAV §1), and a settlement that is the expedition
continuing (SETTLEMENT §0). The current build is measured against them in `VISION_ALIGNMENT.md` §6.

---

## D-025 — Navigation choices, and port first

**Date:** 2026-10-19
**Status:** Accepted (owner's answers to the briefs' approval items)

**Decision.**
- **Order:** finish the port (R2b–R2e) before the voyage work.
- **Plotting:** the captain places waypoints and route legs and orders observations; the navigator
  works out heading, distance and the estimate, and their skill shapes the error.
- **Model:**
  - a flat chart in leagues;
  - a clear-sky sun or pole-star sight fixes the latitude;
  - east–west position comes only from dead reckoning, so its error grows until land is sighted.
- **Time compression at sea** stays Pause, 1×, 10×, 60×, 240×, 720×. Alerts and standing orders
  drop back to pause.

**Why.** The owner chose the recommended options under NAVIGATION §8 and §43 and VOYAGE §51.
