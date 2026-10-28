# Handoff

For the next agent (or the next session of this one) picking up The Expedition. Read this first, then
`README.md`, `docs/DECISIONS.md` and the doc for whatever you are touching. Last updated 2026-10-28,
at commit c82b907 on `main`.

## Working with the owner

- **Git:** develop on `claude/new-session-sodvj3`. Run the tests locally, then push to both the branch
  and `main`. No pull requests, and the owner does no merging:
  ```bash
  git fetch -q origin main && git merge-base --is-ancestor origin/main HEAD \
    && git push -q origin claude/new-session-sodvj3 && git push -q origin HEAD:main
  ```
  After each push, check CI on `main` (GitHub Actions, workflow `CI`) and tell the owner the result in one
  line. If CI fails, fix it on `main`.
- **Commits** end with the `Co-Authored-By` and `Claude-Session` lines the session gives you. Never put a
  model name or ID in commits, code or docs.
- **Paid services (Artlist, Meshy):** get an exact price first, spend only with the owner's go-ahead, and
  log every spend in `docs/ASSETS.md`. No AI concept images. Paid voices and music: "nothing for now".
- **Free assets** (CC0 or similar) are fine. Record where each came from in `game/Assets/CREDITS.md`
  and `game/Assets/assets.json`, and fetch them with `tools/fetch_assets.py`.
- **API keys:** never ask the owner to paste a key into chat.
- **Style:** the owner tests on a Mac. They want plain explanations, screenshots of visual changes, and
  a playtest checklist for anything to try in game.

## The project

- **What it is:** a single-player isometric historical survival game, set around 1500 to 1520. It runs
  from port, to the voyage, to landfall and the settlement, with the same crew throughout.
- **Simulation:** `src/Expedition.Simulation`, plain .NET 8 C#, fully headless.
  - `World/SimulationRunner.*.cs` are partial classes, one per area: People, Structures, Ship and so on.
  - Content is data-driven:
    - `data/shore.json` holds the buildings and their flags.
    - `data/tasks/*.json` holds the tasks.
    - `data/region.json` holds the peoples, the privateer and the like.
  - **Saves:** `Persistence/SaveSerializer.cs`, with migrations. `CurrentSchemaVersion` is **46**.
  - **When you bump the save format:** add a migration, move the expectations in `SaveLoadTests` (the
    migration key, `currentVersion`, and "no upgrade from format N"), and note it in the step's doc.
- **Game:** `game/`, Godot **4.7.2 .NET**. The views live in `game/Scripts`:
  - `Main.cs` handles start-up and the launch flags.
  - `Hud.cs` and `PortHud.cs` are the interface; `UiTokens.cs` is the theme.
  - `PortView.cs` is the port, and `CampView.cs` and `ShipView.cs` are the settlement and the ship.
  - `GameMenu.cs` is the title screen, pause menu, saves and settings.
  - `KitBuilding.cs` is the trial of the building kit.
  - Everything is built in code; there are almost no hand-made scenes.
- **Tests:** `tests/Expedition.Simulation.Tests`, 633 passing. A full run takes about 5 minutes.
- **CI:** a Release build with warnings as errors, plus the tests. Godot is not needed for CI.

## Build, run, screenshot (Linux container)

```bash
dotnet test tests/Expedition.Simulation.Tests          # ~5 min; run in the background
dotnet build Expedition.sln -c Release                 # must be 0 warnings
cd game && dotnet build TheExpedition.csproj
```

Godot is not in the repo. Download `Godot_v4.7.2-stable_mono_linux_x86_64.zip` from the Godot GitHub
releases and unzip it under `/tmp` (the last session used `/tmp/claude-0/godot/`). After adding assets,
run an import:

```bash
G=/path/to/Godot_v4.7.2-stable_mono_linux.x86_64
cd game && $G --headless --path . --import
# Screenshot (software Vulkan under Xvfb):
VK_ICD_FILENAMES=/usr/share/vulkan/icd.d/lvp_icd.json xvfb-run -a -s "-screen 0 1600x900x24" \
  $G --path . --rendering-driver vulkan --resolution 1600x900 -- --menu=none --advance=60 --screenshot=/tmp/out.png
```

Four ERROR lines in the log are already there and harmless: ERR_CANT_OPEN, UVs, surfaces.size and
ARRAY_MAX. View screenshots before you claim a visual change works.

**Launch flags** (everything after the `--`) are listed in `docs/PLAYTEST.md`. Useful ones:
- `--testdeck`, `--ashore=coast.punta_ceniza`, `--camphours=N`, `--meeting=none|privateer`;
- `--raise=…`, `--give=item:n`, `--charter=…`, `--learn=…`, `--trust=…`, `--panel=…`;
- `--menu=title|pause|settings|none`, `--kit` / `--kit=alternate`;
- `--look=x,z`, `--zoom=n`, `--advance=n`, `--screenshot=path`.

With no flags, the game opens on the title screen.

## What is done

Everything in `docs/MILESTONES.md` up to and including these, newest last:
- **Step P (people):** households, couples, births, coast people joining, relief settlers.
  See `docs/PEOPLE.md`.
- **Structures:** house, smokehouse, chapel, trading house, salt pans, forge, with iron and tools.
  See `docs/STRUCTURES.md`.
- **Step S (the ship after landfall):** keepers aboard, fishing and coasting errands, careening,
  guns, powder and shot, the lookout, battery and magazine, and the privateer *Gannet*.
  See `docs/THE_SHIP.md`.
- **Menus:** title screen, pause menu, named save slots and settings (`user://settings.cfg`).
- **Port fixes:**
  - click the ground to walk;
  - the preparation card folds, and event windows can be tucked away;
  - trackpad and middle-drag camera;
  - walk clips matched to ground speed;
  - 35 cm room walls without flicker, and rooms open early.
- **House fronts:** laid out in bays, with windows, doors and other detail.
- **Building kit trial:** Quaternius Medieval Village MegaKit (CC0), behind `--kit`.
  See `docs/BUILDINGS_KIT.md`.

## What to do next: the owner's playtest results

The owner worked through the checklist at https://claude.ai/artifact/QezyaerLcikgHfGyEjQeHg on
2026-10-23. The results are in the artifact's database, collection `results`: one document per
checklist item, holding `status` (works / broken / skipped) and `note`. Re-read it, because the owner
may have added more. The page's list is in `docs/PLAYTEST.md`. These notes are the backlog, in rough
priority order:

1. **Movement feel** (`u-walk`, `u-gait`):
   - **Stutter steps:** the captain stutter-steps when clicked around.
   - **Re-clicking:** a click near the current goal should carry on the same way, not restart the walk.
   - **Turning around:** they should slow, turn naturally and walk off, not snap round.
   - **Pathfinding:** they should take the best path to where they're sent.
   - **Planted feet:** they still glide, because the planted foot shifts. Fix the foot lock, or match
     the stride to the clip exactly.
   - **Watch speed:** at normal speed the walk looks robotic or zombie-like.
   - **Pace:** people should walk at different paces.

   The code is in `PersonModel.cs` (WalkRate, StrideSpeed), `CrewFigure.cs`, `PortCrowd.cs` and the
   captain's walk in `Main.cs`.
2. **Pause moves the captain** (`m-pause`): pausing moves the character back a spot, and on resume
   they jump to where they were. This is probably the view interpolating against the paused
   simulation position.
3. **UI/UX, recruiting** (`u-event`):
   - **What's wrong:** in the dock recruiting screens, all the text blends together and buttons can't be
     told from text.
   - **What the owner wants:** follow the owner's UI/UX reference images closely, with a clear
     difference between information and actions. Hiring should be fun, with a feedback screen when
     someone signs on.
   - **Where the references are:** they're described in `docs/UI_STYLE.md` but were uploads, not
     repo files. Ask the owner to re-share them if you need them.
4. **Crew management and capacity** (`p-roster`):
   - Let the player dismiss crew while recruiting, at some cost (respect or the like). The case: the
     ship is full, but the checklist still wants a navigator.
   - Think about more berths at a resource cost.
   - Think about a cabin or passengers: extra settlers who bring their own food and water.

   These are design decisions; propose them to the owner before building.
5. **Hats** (`p-roster`): they don't sit right. Look at the back of the captain's head while he wears one.
6. **The port chapel** (`u-fronts`): it needs a full facelift. Things float on its front and roof. It is
   `case "chapel"` in `PortView.cs`.
7. **Shadows** (`u-camera`): they flicker, with jagged edges, under the shutters. Check the shadow
   bias, the split distances, or the shutters' thickness.
8. **Building kit** (`k-look`, `k-fit` marked not right, no notes; `k-all`, `k-pref` marked works):
   - The owner would **keep the kit**.
   - Something about how it looks and how the pieces fit is wrong; ask, or take screenshots and look
     for gaps and floating pieces.
   - Likely work:
     - make the kit the default;
     - turn the roofs so their gables are at the sides;
     - stop stretching pieces on odd widths;
     - perhaps rebuild the chapel and other buildings with it.

Not yet tested by the owner: sections 4 to 9 of the checklist (workplaces, community, people beyond
the roster, buildings, ship, guns). Update the checklist page and `docs/PLAYTEST.md` together when you
change what is to be tested. The page source is not in the repo: read it back with the Artifact tool,
then publish to the same URL.
