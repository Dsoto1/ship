# Port Visual Vertical Slice — Audit and Proposal

**Status:** APPROVED by the owner on 2026-10-19 with every recommendation in §4 (D-026). Built step by
step from §5; progress is in `docs/MILESTONES.md`.
**Authorities:**
- ART_BIBLE.md §1–3, §7.1, §8–9, §16, §19–20, §23;
- PORT_EXPERIENCE.md §3–8, §12–15, §18, §21;
- TECHNICAL_SPEC.md (the simulation stays separate from rendering).

**Baseline:** `docs/screenshots/baseline-chandlers-quay-{dawn,midday,night}.jpg`. These are the block
proposed below, at gameplay zoom, before any change. Rain cannot be shown yet: the port has no
weather.

---

## 1. Audit

### 1.1 How the port is built

| Layer | Now |
|---|---|
| Layout | `tools/gen_bocana_map.py` writes `data/locations/bocana.json`: an 80 × 46 walk grid, ~150 work spots with semantic kinds (`SitPoint`, `CarryPickup`, `HammerStation`, …), raised ground (the terrace and steps), and ~60 **features** (building style, stall, cargo, fence, boat, …). These are drawing hints that the simulation ignores. |
| Drawing | `game/Scripts/PortView.cs` turns each feature into code-built boxes and cylinders, one `MeshInstance3D` each, with a material cache keyed by colour. The town behind is seeded rows of houses. |
| Ground | `ground.gdshader`: procedural cobbles and quay slabs from world position, with a `wetness` parameter that nothing sets. It is the same everywhere: no paths, wear, dirt or edges. |
| Roofs | `roof.gdshader`: procedural tile rows. |
| Ship | `ShipView.cs` draws the *Garza* (the test-deck map) at the berth, with sails furled in port. |

### 1.2 Lighting and rendering
- **Renderer:** Forward+, MSAA 2×, filmic tone mapping, glow.
- **Lights:** one shadowed directional sun, driven by the clock (`SeaAndSky.cs`); an unshadowed
  moon; a flat colour background; an ambient colour blended between noon, sunset and night.
- **Not used:** no SSAO, fog or sky. Haze exists only on the ocean shader.
- **Night** works because lanterns and fires (`OmniLight3D`) make local pools of light.
- **Day** has nothing equivalent: one light colour, no atmospheric depth, no contact darkening. This
  is why it looks flat.

### 1.3 People and activity
- **Simulation:**
  - persistent characters on schedules;
  - tasks at reserved spots (one person per spot);
  - chained work loops (`then`) with carried loads (`carries`);
  - spot kinds.
- **Drawing:** `CrewFigure.cs` builds jointed primitive figures. The pose comes from the task and
  spot kind (`Main.WorkPose`).
- **Existing poses:** walk, carry, hammer, saw, sit, pray, drink, talk, rope, watch, lie.
- **Gaps:**
  - figures face only the way they last walked, never their work or the person they are talking to;
  - spots have no relation to each other, so people stand alone;
  - there is no customer at a stall, no listener in a conversation, no helper.

### 1.4 Props and buildings
- **No prop system.** Every prop is hand-placed in `PortView` per feature kind. Building styles
  (chamber, tavern, chapel, warehouse, cooperage, smithy, sail loft) add a little detail each.
- **Fences** are solid wooden panels: the tan rectangles in the baseline.
- **Stalls** are one shape with saturated striped awnings and three colours of goods.

### 1.5 UI
- **Code-built Godot `Control`s** in two layers:
  - `Hud.cs`: top bar, crew panel with the hold list, shipboard order panel, chronicle (bottom),
    voyage bar, alerts, events, ending;
  - `PortHud.cs`: departure panel (centre top), person card, dialogue, chandler, journal.
- **Screen space at 1600 × 900 in port:**
  - crew panel ~230 × 230 px;
  - departure panel ~660 × 130 px;
  - chronicle ~880 × 112 px, permanently;
  - the card, 340 × 700 px, when open.

  About **a quarter to a third of the view is covered** while walking.

### 1.6 Performance (measured with `--perf`, 1600 × 900)

| View | Draw calls | Objects | Triangles | Nodes |
|---|---|---|---|---|
| Gameplay zoom (24) | ~1,900 | ~2,500 | ~1.1 M | ~3,300 |
| Zoomed out (45) | ~4,200 | ~4,800 | ~1.6 M | ~3,300 |

**Causes:**
- Every box is its own mesh and draw call, drawn again for the sun's shadows.
- The figures use Godot's default sphere (64 × 32 segments) for heads and hands, so 31 people
  account for most of the triangles.

**What this means:** the frame rate here is software rendering and means nothing. On a MacBook this
is likely still playable, but there is **no headroom for more detail**. Adding props the current way
would multiply draw calls. So the first step of the pass is a cheap foundation, not decoration.

### 1.7 What can reuse existing systems

| Improvement (your brief) | Reuses |
|---|---|
| 2. Activity clusters | Spots, kinds, schedules, task chains, carried loads |
| 3. Ground variation | The ground shader (add a data texture), the walk grid, spots |
| 4. Functional identity | Feature styles in the map data; `PortView` builders |
| 5. Believable blockers | Blocked cells and fence/cargo features: only how they are drawn changes |
| 6. NPC composition | Schedules and spots (adding a facing and "partner" spots) |
| 7. Work animation | `Pose` and `WorkPose`, already driven by the simulation |
| 9. Lighting | `SeaAndSky` time-of-day blending; the lantern/fire lights |
| 10. Stalls | The stall builder, with styles |
| 11. Ship anchor | `ShipView`, the berth, the furled-sail look |
| 13. UI | The existing panels: reorganized, nothing removed |

### 1.8 What needs something new

| New | Why | Size |
|---|---|---|
| **Static batching** in `PortView` | Merge all static props into one mesh per material, cutting draw calls by about 90% | Small (one helper) |
| **Low-segment figure parts** | Cut triangles by about 80% | Tiny |
| **Ground detail map** | A per-cell texture (wear, grime, wetness, debris type) computed from the map, sampled by the ground shader | Small to medium |
| **Spot facing and clusters** in the location data | People face their work and each other; paired spots (stall + customer, talker + listener, carpenter + helper) | Small (simulation data and a field) |
| **Dressing kits** | Reusable sets of props per building, stall or blocker style, so every tavern or chandler gets its own work story | Medium |
| **Lighting presets** | Dawn, morning, midday, evening and night key/fill/ambient/fog colours as data; SSAO; height fog for harbour haze | Small |
| **Contact shadows** | A soft dark disc under figures, casks, stalls and wall bases | Small |
| **Occlusion fade** | Foreground pieces (awning edges, crane, rigging) fade when they cover the captain or the selection | Small to medium |
| **Rain look** | A visual-only weather state for validation: wet ground, rain, overcast light. Simulation weather and NPC reactions stay R2d. | Small |
| **Sound zones** | No audio system or assets exist; CC0 audio hosts are blocked in this environment | See §4 |

---

## 2. The slice: "Chandler's Quay"

**Area:** x 26–60, y 13–42 (about 34 × 29 m). This is one screen at gameplay zoom, centred on the
quay east of the *Garza*'s gangway.

**It already holds every work story you named**, so the recipe gets proven on all of them:
- **Ship and loading:** the *Garza*'s bow half and gangway, and pier B with the lighter that dockers
  load.
- **Fish market:** the catch landed at the pier, carried to the two fish stalls, sold.
- **Cargo:** the chandler's warehouse with its hoist and yard, the cask staging, and the porters'
  route.
- **Rope and canvas:** the chandler's rope and canvas stock, and the sailmaker's deliveries to the
  *Garza*.
- **Ship repair:** a repair station on the *Garza*'s quay side (see §3, item 2).
- **Social:** the Three Anchors' front (benches, fire, drinkers) on its west edge.
- **Blockers:** the yard fence, the gaol cart and the cargo stacks: the prototype barriers you
  pointed out.

Everything built for it is a reusable system or kit. Applying it to the rest of La Bocana is mostly
placing the same kits.

---

## 3. What I intend to change, by your sections

Each item is tagged [reuse], [new system] or [content: this block only].

1. **Hierarchy (characters → ship → interactive → background).**
   - Characters: slightly warmer, more saturated clothing against desaturated stone and plaster; a
     contact shadow; clear silhouettes.
   - Background: the town behind gets lower contrast and a little haze.
   - Ship: see item 11.
   - No outlines. [reuse + lighting presets]
2. **Activity clusters.** [content + spot clusters]
   - **Cargo:** vault/warehouse door → staged casks and crates (stacks shrink and grow with the loop)
     → porter route → lighter at the pier. Porters wait their turn sitting on a cask beside the
     stack, instead of standing on the drop spot.
   - **Fish:** boat at the pier → baskets on the boards → gutting table beside the stall → seller →
     a customer spot → a waste bucket with gulls.
   - **Rope and canvas:** coils and canvas bolts in the chandler's yard → the sailmaker's delivery
     route to the *Garza*.
   - **Ship repair:** a plank stack, sawhorse and offcuts on the quay at the *Garza*'s side. The
     shipwright's afternoon loop moves there from the stocks, so the repair she needs is visibly
     happening.
     - *Simulation content change:* one schedule and two spots.
3. **Ground.** [new system: ground detail map]
   - **Computed from the map:**
     - worn paths along the routes people actually walk (from spot-to-spot paths);
     - darker grime against walls and blocked cells;
     - straw near the stalls;
     - fish-wet stone and scales by the fish stalls;
     - sawdust by the repair station;
     - dock grime and damp on the quay edge;
     - a drainage gutter line down the quay;
     - cart ruts on the main route.
   - **Restraint:** subtle, and only where it explains use. Wetness rises in rain.
4. **Building function.** [new system: dressing kits; content]
   - **Chandler:** hoist beam with block and line over open doors, rope coils, canvas bolts, casks,
     a weighing beam.
   - **Tavern:** sign, benches, barrels by the door, lantern, a drinker's cup on the table.
   - **Cooperage front:** staves, hoops, a finished cask row.
   - No rebuilding.
5. **Blockers.** [reuse cells; new drawing]
   - The yard fence becomes post-and-rail with stacked timber against it, and a rope barrier at the
     gate.
   - Cargo blocks become lashed stacks.
   - The gaol cart gets its shafts and a chained wheel.
   - Every blocked cell has an obvious reason to be blocked.
6. **NPC composition.** [spot clusters; content]
   - Spots gain a **facing** (a direction or a target spot) and optional **partner** spots.
   - Clusters in this block, all from existing people's schedules at plausible hours (Lucía and Tito
     at the stalls, the priest buying fish, the guard chatting with the gaoler):
     - seller + customer at the fish stalls;
     - two drinkers facing each other on the tavern bench;
     - porters queueing;
     - shipwright + helper at the repair station.
   - **No new people.**
7. **Work animation.**
   - People face their work and partners.
   - **Five new clips:** roll cask (for `deliver_cask`), lift/set-down at pickups and drops,
     inspect (bent forward), lean, and gut fish.
   - Every clip is chosen from a simulation task and spot kind, never scripted decoration.
     [reuse Pose]
8. **Grounding.** [new: contact shadows]
   - Soft contact discs under figures, casks, stalls and crates.
   - A darkened band where walls meet the ground (from the detail map).
   - SSAO at low strength.
9. **Daylight.** [lighting presets + SSAO + fog]
   - A data table of key, fill, ambient, fog and exposure per time of day:
     - **dawn:** cool haze and a warm low sun;
     - **morning:** clean light, mild harbour haze;
     - **midday:** brighter stone, short shadows;
     - **evening:** long, warm light;
     - **night:** cool ambient against warm lantern pools.
   - The sun's direction is chosen so the camera's view has readable shadows.
   - Height fog over the water gives depth.
   - *Approval:* SSAO and fog are standard Godot environment settings, not a renderer change, but
     the Bible asks for approval on "major shader-stack changes", so I am flagging them.
10. **Stalls.** [dressing kits]
    - Undyed and faded canvas awnings with sag and patches (saturated dyes were costly; colour only
      where it means something).
    - Each stall identified by goods:
      - **fish:** flat baskets, a gutting board;
      - **produce:** sacks, onions and garlic strings;
      - **cloth:** bolts, a measuring rod;
      - **tools:** ironmongery on a board.
    - Tables, trestles, crates and baskets vary.
11. **The ship as anchor (composition, not size).**
    - A fuller *Garza* in port: shrouds and stays drawn as lines, yards with furled sails, an ensign
      and pennant moving in the wind, a boat alongside.
    - The default camera frames her in the lower third, with her masts crossing the roofs from the
      market side.
    - The merchantman's oversized white yards are fixed: they currently cut across the foreground.
    - The pier approach makes her scale readable.
12. **Depth.** [new: occlusion fade]
    - Occasional foreground pieces at the near edge of the block: an awning edge, a hoist beam,
      rigging lines, a hanging sign.
    - They fade when they cover the captain or the selection.
    - Height fog separates the far town.
13. **UI.** [reuse panels]
    - **Top bar:** time, speeds and a ☰ menu (New, Save, Load, Controls).
    - **Departure strip** (one line): "Convoy sails in 41 h · 7,000 sueldos · 0/4 hands · stores
      75 / 86 d". Clicking it opens the details and the Notices, Journal, Chandler and Set sail
      buttons.
    - **Crew** collapses to a small tab; the hold moves to a **Cargo** panel.
    - **Chronicle:** closed by default. Important entries surface as short toasts and fade after a
      few seconds; a **Log** button opens it.
    - **The card** stays contextual and gets narrower.
    - **Target:** about 90% of the view clear while walking.
    - Nothing is removed.
14. **Sound zones.** No audio exists, and the CC0 hosts are blocked here. Two options in §4.
15. **Four conditions.** The same block at dawn, midday, rain and night. Rain is a visual-only state
    (`--weather=rain` and a debug key) until port weather comes in R2d. Screenshots are compared
    with the baseline.

**Out of scope:**
- rebuilding the port;
- new engine features or dependencies;
- photoreal textures;
- more people;
- redesigning other districts. They get only what the shared systems bring for free (batching,
  lighting, ground map, contact shadows, UI).

---

## 4. Decisions for the owner

| # | Decision | Recommendation |
|---|---|---|
| A | Approve the slice: Chandler's Quay | Yes |
| B | SSAO (low) and height fog in the environment | Yes; measured cost is reported with the result |
| C | The shipwright's afternoon loop moves to a repair station beside the *Garza*, and existing locals gain short shopping and chatting blocks to form clusters | Yes (simulation content, tested) |
| D | Rain is visual-only for this pass; NPC weather reactions stay R2d | Yes |
| E | Sound: (1) generated ambience (surf, crowd murmur, tool knocks tied to work poses) now, or (2) wait until audio hosts are allowed (e.g. freesound.org, kenney.nl, sonniss.com) and use CC0 recordings | (2), plus a simple zone framework now, with placeholder generated surf and murmur |
| F | Crane: a wheel-driven quay crane is plausible (one is depicted at Seville's Arenal c. 1519, in a modern reconstruction), but for a small outport a warehouse hoist and manual porters are safer. Add a crane later only after research. | Hoist now, crane deferred |
| G | UI layout as in item 13 (layout only; the Bible's visual language is unchanged) | Yes |

---

## 5. Implementation order (after approval)

Each step ends with screenshots of the block and a `--perf` measurement.

0. **Foundation:**
   - static batching in `PortView`;
   - low-segment figure parts;
   - **budget:** at most ~400 draw calls and ~400 k triangles at gameplay zoom.
1. **UI declutter** (item 13). Quick to do, and every later screenshot is judged with the world
   visible.
2. **Lighting presets, SSAO, fog and contact shadows** (items 8–9, 1).
3. **Ground detail map** (item 3).
4. **Spot facing and clusters, and the new poses** (items 6–7), with simulation tests.
5. **Dressing kits:** chandler, tavern front, cooperage front, stalls, blockers, repair station
   (items 2, 4, 5, 10).
6. **Ship anchor and foreground depth** with occlusion fade (items 11–12).
7. **Rain look and the four-condition validation** (item 15), compared against the baseline.
8. **Sound zones**, per decision E (item 14).

**Definition of done:** your list, judged on side-by-side screenshots of the block in four conditions
against the baseline, with draw calls within budget.
