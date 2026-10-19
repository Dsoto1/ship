# R3 — The four port validation scenes

**Authority:** PORT_EXPERIENCE §21 (four scenes), §23 steps 15–16 (validate, then profile); D-028
(R3 in two parts).
**Status:**
- **Part one** (2026-10-19): scenes A and C are judged; all four are staged; the profiling baseline
  is taken here.
- **Part two,** after the character slice C-1: scenes B and D, and the owner's Mac profile.

The brief asks that these scenes be proved *before* asset production is scaled. Each verdict below
is honest: **Yes**, **Partly** or **No**, against the tests the brief names.

## How to stage a scene

Each scene is staged the same way every time: a fixed seed (1519), run on to its hour, with its
weather, hands and stores, and the camera on it.

| Scene | Launch option | In-game (debug build) |
|---|---|---|
| A: the quay at dawn | `--stage=dawn-quay` | Ctrl+1 |
| B: the market at midday | `--stage=midday-market` | Ctrl+2 |
| C: the workshop street in rain | `--stage=rainy-workshops` | Ctrl+3 |
| D: departure morning | `--stage=departure-morning` | Ctrl+4 |

Options go after `--` on the Godot command line. From the editor, put them in Project → Project
Settings → Editor → Run → Main Run Args.

## Scene A: the quay at dawn — *Partly*

![Scene A](screenshots/r3-scene-a-dawn-quay.jpg)

Day 2, 06:18, in the new **mist** weather. Mist is a morning weather that lies low on the water,
thins the far ships and burns off to fair. The camera looks at the east pier, where the dockers carry
to the lighter.

| Test | Verdict | Notes |
|---|---|---|
| Lighting | **Yes** | Low warm sun from the east, long shadows, cool haze. It reads as early morning. |
| Water | **Partly** | Haze lies on the harbour and the far water fades. But the mist is a thin even veil, not banks drifting on the water. |
| Ship scale | **Yes** | The Garza dominates the quay; the lighter and boats read small against her. |
| Silhouettes | **Partly** | Masts, shear-legs and stalls read clearly. People are too small and too alike to read at this distance (C-1). |
| Sound layering | **Partly** | Recorded surf, creaking timbers and the first mallets layer by distance. Gulls, oars and the fish market starting up are missing. |
| Fishermen and dockers starting the day | **Partly** | The schedules start at 06:00 and a few dockers are moving. But at 06:18 the quay is still thin, and the catch is landed at the west pier, out of this frame. |

**Fixed in this pass:**
- Mist added as its own weather. The anchored nao vanishes into it, and the caravel stays a shape.
- Light mist now lies low (height fog). Only thick fog closes in on everything.
- The scene is framed on the work, not the empty middle of the quay.

**Still to do:**
- Drifting mist banks on the water. These need a cheap animated mist layer.
- The catch landing and the dockers' first carries together in one frame. This needs either a
  wider establishing view, or the fishing boats landing nearer the lighter.
- More dawn sound.

## Scene C: the workshop street in rain — *Partly*

![Scene C](screenshots/r3-scene-c-rainy-workshops.jpg)

Day 1, 15:33, in rain, on the yard and the workshops (cooperage, smithy, sail loft).

| Test | Verdict | Notes |
|---|---|---|
| Wetness | **Yes** | The stone darkens and shines, roofs and timber darken, and everything dries slowly afterwards. |
| Rain response | **Partly** | Rain falls, the sound bed is recorded, stalls are covered and the sailcloth is taken in. Puddles and dripping eaves are missing. |
| Interiors and exteriors | **No** | The workshops are solid boxes with dark doorways. You cannot see in, so "the smith at work inside" reads only as a glow. |
| Weather behaviour | **Partly** | Idlers and outdoor menders go to shelter. The smith works on at the forge door, and the cooper and ropemaker work on. But the street looks empty rather than adapted: no one hurries, holds a sack over their head, or works under an eave. |

**Fixed in this pass:**
- The sailcloth spread in the yard is taken in when it rains.
- Cloth (awnings, banners, the ensign) ripples in the wind.

**Still to do:**
- Open-fronted workshops, so the work is visible from the street. This is a new building kit, needed
  for every craft.
- Craftsmen moving to work under cover, not only stopping.
- Puddles, and drips from the eaves.

## Scenes B and D — *after C-1*

Both scenes are about people: crowd readability and persistent identities. They are staged and
profiled now, and judged once the six characters of C-1 exist. Scene D's departure itself is built
(R2e) and is shown in `docs/screenshots/r2e-departure-*.jpg`.

## Profiling

**Here** (a CPU software renderer, so frame times mean nothing; the counts are the baseline). Each
capture is 20 seconds, with the camera circling slowly over the scene.

| Scene | Draw calls (mean / most) | Primitives | Objects | Crowd |
|---|---|---|---|---|
| A: dawn quay | 370 / 406 | 283k | 648 | 24 |
| B: midday market | 575 / 601 | 389k | 858 | 40 |
| C: rainy workshops | 322 / 367 | 331k | 614 | 10 |

These leave room under common desktop budgets (about 1–2,000 draw calls, a few million primitives),
but the real test is the Mac.

**On the Mac (the owner):**
1. Run each scene with profiling:

   ```
   /Applications/Godot_mono.app/Contents/MacOS/Godot --path game -- --stage=midday-market --profile
   ```

   Or press F10 in the game on any view.
2. Each capture saves a report and prints its path. The path is under
   `~/Library/Application Support/Godot/app_userdata/The Expedition/profiles/`.
3. Send the four reports. They give the frame time (mean, median, 95th and 99th percentile), what was
   drawn, and the GPU.

C-1's budgets (D-028) are confirmed or adjusted against these numbers.
