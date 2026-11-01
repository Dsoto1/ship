# How people walk

**Owner's ask** (playtest, 2026-10-23): "people look like they glide… the planted foot shifts… maybe different
speeds for different characters?" Then (2026-10-31): fix the feet, look at walking variations, and use Meshy.

## Drawing the walk (`CrewFigure.Walked`)
- **The path:** the simulation walks cell to cell, a minute at a time. The figure is drawn along those cells with
  the corners rounded (Chaikin, three passes), from wherever it is drawn.
- **Pace:** figures speed up and slow for the last steps. Sent back the way they came, they stop, turn and set off.
- **Timing:** each walks at their own pace and arrives when the simulation says, or a little before. If early,
  they stand instead of walking on the spot.
- **Where:** in port and in camp up to 10×. On the ship's decks, and at 60× and above, people move as the
  simulation has them.
- **Pause:** paused, everyone holds where they were drawn.

## Feet (`PersonModel`)
- **Clip speed:** a walk clip plays at the ground speed divided by its stride (the ground it covers at speed 1),
  scaled for height. The Quaternius walks measure 0.98 m/s (carrying, 0.65), tuned in the game to 1.01. Each
  Meshy walk's stride is measured when it is retargeted (`walks.json`) and given the same tuning.
- **The foot lock (`FootLock`):** a skeleton modifier pins a planted foot where it came down. It bends the
  thigh and calf to keep it there until the clip lifts it, and lets go of a foot that would have to stretch more
  than 18 cm. In the walk test, the ankle is still in 81% of its frames on the ground.

## The walks
- **Quaternius (CC0):** `walk`, `walk_formal`, `walk_carry`, in `Shared/animations.glb`.
- **Meshy (paid; see `docs/ASSETS.md`):**
  - **How they're made:** Meshy rigged the Quaternius base male, its library animated that rig, and
    `tools/characters/retarget_meshy.gd` carries each animation onto our rig. Each bone's turn from the shared
    T-pose carries over, and the hips' motion is scaled to our pelvis. A walk that travels is put in place.
  - **The output:** `.res` clips in `Shared/walks/`, loaded into every person's clip library.

| Clip | Stride (m/s) | Given to |
|---|---|---|
| `walk_meshy` (Meshy's basic walk) | 1.61 | men under 30, and others at random |
| `walk_quick` | 1.61 | men under 30 |
| `walk_plain` | 1.15 | anyone, at random |
| `walk_casual` | 0.80 | women, men over 55, and others at random |
| `walk_woman` | 1.48 | women |
| `walk_proud` | 0.98 | those whose record says they walk formally (officers, merchants, clerks) |
| `walk_limp` | 0.76 | anyone whose record gives them a limp |
| `walk_elderly` | 0.22 | anyone 65 or older (`Gait.Elderly`), at 0.4 of the pace |
| `walk_thoughtful` | 0.62 | anyone 60 to 64 (`Gait.Old`), at 0.7 |
| `walk_injured` | 0.55 | anyone with a sprain or a wound (`Gait.Injured`), at 0.6 more |
| `walk_unsteady` | 0.85 | anyone with fever, flux, scurvy or half drowned (`Gait.Ill`), at 0.8 more |
| `walk_stumble`, `walk_plod`, `walk_sneaky`, `walk_carry_heavy`, `walk_bucket`, `walk_looking` | 0.15–1.29 | not yet: for drink, thieves and errands to come |

- **How a walk is chosen:** each person's own walk is chosen from their record by their id
  (`PersonModel.ChooseWalk`) and kept. Their state now (`SimulationRunner.GaitOf`) can replace it with one of the
  slower walks above. Someone carrying keeps the carrying walk.

## Each at their own pace (`SimulationRunner.Pace`)
- **How a walk is timed:** a walk's time in the simulation is the cells over the location's pace, times
  `WalkPace`. That is 1 for most people, down to 0.35:
  - age: 0.85 from 55, 0.7 from 60, 0.4 from 65;
  - a sprain or a wound: 0.6;
  - fever, flux, scurvy or near drowning: 0.8;
  - a heavy load (planks, crates, casks, or more than 15 kg in hand ashore): 0.75.
- **Combining:** the reasons multiply together.
- **In the view:** figures are drawn at that pace, so the slow walks play near their own speed. Simón the
  sailmaker (66) walks at about 0.6 m/s, half the usual.
- **Saves:** walks already under way keep their timing, and nothing new is saved.

**Checking a walk:** add `--walkclip=walk_proud` to any launch, and the captain (and everyone walking) uses that
clip. With `--walktest=1:-7,1 --walklog=PATH`, the captain's drawn position and feet are logged every frame.
