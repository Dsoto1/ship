# Playtest guide: the settlement, its people, its buildings, the ship

This guide is for checking the latest work in the running game: the menus and port fixes, the building kit trial, workplaces (step W), the community (C),
people (P), the new buildings, and the ship (S). Set up Git, .NET and Godot first, as `TESTING_ON_MAC.md`
describes.

You can test two ways:
1. **Play it through:** Godot's ▶ Run button, from the port, over the sea, to the landing. That is the real
   game, but the later parts come weeks of game time in. Even at 240× a game day takes about six minutes.
2. **Jump straight to a moment** with launch shortcuts (below). This is the way to test the later parts:
   the relief ship's settlers, the privateer, the buildings.

## Launching with shortcuts

Build once, then launch from the Terminal. Everything after the lone `--` is a shortcut:

```bash
cd ~/Documents/ship
git pull
cd game
dotnet build
/Applications/Godot_mono.app/Contents/MacOS/Godot --path . -- --testdeck --ashore=coast.punta_ceniza --camphours=30 --meeting=none
```

If your Godot app has another name, type the start of the line, `/Applications/`, and drag the Godot app
from Finder into the Terminal window: it fills in the path. Then add `/Contents/MacOS/Godot`.

| Shortcut | What it does |
|---|---|
| `--testdeck` | The test captain's ship and company (six people), at sea. |
| `--ashore=coast.punta_ceniza` | Landed at Punta Ceniza, the ship at anchor, camp made. |
| `--camphours=N` | N hours of camp life run before you see it (720 is 30 days). |
| `--meeting=none` | No delegation waiting when it opens. |
| `--meeting=privateer` | The privateer comes now. |
| `--raise=house,smokehouse` | Buildings already standing. Names: `hut`, `house`, `storehouse`, `cookhouse`, `infirmary`, `workshop`, `smokehouse`, `forge`, `salt_pans`, `chapel`, `trading_house`, `lookout`, `boat_shed`, `magazine`, `battery`, `garden`, `well`, `landing`. |
| `--give=item.powder:20,item.shot:20` | Put in the camp's stores. |
| `--charter=kept` | The charter judged now (`kept`, `in_part`, `revoked`), and the relief ship's settlers ashore. |
| `--learn=know.salt` | Something the coast's peoples know, as if told. |
| `--trust=people.mahenu:60` | A people's trust set. |
| `--panel=ship` | The Camp drawer open (People: `--panel=people`). |

Save with **Cmd + S** and load with **Cmd + L** at any point, or from the menu (**Esc**). Launched with shortcuts, the game skips the title screen.

## 0a. Menus and saving

Launch with nothing after the `--` (or Godot's Run button).

- [ ] **Title screen:** New voyage, Continue, Load, Settings, Quit.
- [ ] **Pause menu:** Esc (or the Menu tab) gives Resume, Save, Load, Settings, Controls, Main menu, Quit.
- [ ] **Save slots:** save under a name, save over it, delete one. The list shows day, place and when saved.
- [ ] **Loading:** a save from the list puts you back where you were; Continue loads the latest.
- [ ] **Settings:** volume, window, interface size, edge panning, what pauses the game. Kept after a restart.

## 0b. The port: moving and looking

```
-- --menu=none
```

- [ ] **Click to walk:** left-click the ground, the captain walks there, with the preparation card open.
- [ ] **Preparation card:** the – button folds it; Readiness has its own toggle.
- [ ] **Event windows:** can be tucked to a bar at the top.
- [ ] **Camera:** middle-drag, two-finger drag and pinch; edge panning if on.
- [ ] **Walking:** feet keep pace with the ground at 1×, 2× and 10×.
- [ ] **The tavern:** thin walls, framed doorway, the inside shows as you reach the door, no flicker.
- [ ] **House fronts:** no beams across windows or doors; windows set back with sills and shutters.

## 0c. Port houses from the free kit (trial)

```
-- --menu=none --kit=alternate
```

- [ ] Every other house is built from the kit; no gaps or floating pieces.
- [ ] With `--kit` instead, every ordinary house is, and the game runs smoothly.
- [ ] Which do you prefer, and why?

## 1. Workplaces (step W)

```
-- --testdeck --ashore=coast.punta_ceniza --camphours=30 --meeting=none --panel=ship
```

- [ ] **Camp drawer:** has Build, Workplaces, The ship, Self-sufficiency.
- [ ] **Placing a hut:** choose Hut and click **Place**, then set it on the sand. A shape follows the mouse,
      and you cannot place it touching another building.
- [ ] **Workplaces card:** the hut shows, with a lead and a priority you can change.
- [ ] **Waiting reason:** when no one is at it, it says why in orange (hands, materials, night, weather…).
- [ ] **History:** once it is built, open its history: who built it, from what, when.

## 2. The community (step C)

Same launch.

- [ ] **The community card:** a word (Good, Steady…) with its reasons, minus first, plus after.
- [ ] **Policies:** change Rations to Short, and the top bar's Food reading lengthens. Change it back.
- [ ] **Working day:** Long ends the work later. Watch the clock and who is still working at 19:00.
- [ ] **Reserves:** set Keep water to 20 days, and more people fetch water.
- [ ] **Self-sufficiency:** after `--raise=storehouse`, "A storehouse" is ticked and the Chronicle says so.

## 3. People (step P)

```
-- --testdeck --ashore=coast.punta_ceniza --camphours=30 --meeting=none --charter=kept --panel=people
```

- [ ] **Roster:** fourteen people, the six and eight settlers (Andrés Pardo, Tomé Casal…).
- [ ] **Households:** two couples, e.g. "Andrés Pardo and Leonor Pardo".
- [ ] **Settler's record:** says they came with the relief ship to settle, and whom they live with.
- [ ] **Chronicle:** the settlers coming ashore, and the homes made.
- [ ] **Revoked charter:** launch with `--charter=revoked` instead. No settlers come, and anyone badly
      strained "took passage home".

Children take months to come: a woman in a household expects at about one chance in 120 a day, and a
child comes 270 days later. To see one, leave a save with settlers running at 240× for a long while.

## 4. The new buildings

Each line is its own launch. Add them after `--testdeck --ashore=coast.punta_ceniza --camphours=30 --meeting=none --panel=ship`.

| Add | Check |
|---|---|
| `--charter=kept --raise=house` | [ ] Next morning (run at 240× to 06:00) the Chronicle says a couple "moved into a house of their own". The community card has "a household with no house of its own" for the other couple. |
| `--raise=smokehouse --give=item.fish:30` | [ ] Within the day someone is at **Smoke fish**. The Cargo screen shows smoked fish. |
| *(nothing extra)* | [ ] Choose **Salt pans** and Place: it is refused, saying it is to be learned of Kessat. |
| `--learn=know.salt --raise=salt_pans --give=item.fish:20` | [ ] Someone **Boils salt**, then **Salts fish**. Salt fish shows in Cargo. |
| `--raise=forge --give=item.iron:6` | [ ] Someone **Forges a tool**. The Chronicle names the tool. The "A tool mended" milestone is ticked. |
| `--raise=chapel` | [ ] The community card shows "Sunday kept at the chapel". |
| `--raise=trading_house --trust=people.mahenu:30 --meeting=people.mahenu` | [ ] Trade with them, and their trust grows a little more than without it. That is hard to see, so this one is optional. |

## 5. The ship (step S)

```
-- --testdeck --ashore=coast.punta_ceniza --camphours=30 --meeting=none --panel=ship
```

**The ship card**, in the Camp drawer, under Workplaces:
- [ ] **Status:** "At anchor off, her hull at 100%: left alone, fouling and wearing."
- [ ] **Sending aboard:** click two names. They go aboard, leave the beach, and the line says "kept,
      and wearing slowly".
- [ ] **Fish at sea:** with three aboard it is refused (she wants four). With four aboard, she goes. Run a
      day at 240×: "back from a day's fishing", with fish in Cargo.
- [ ] **Along the coast:** with four aboard, run four days. The Chronicle lists the shore seen from the sea,
      and the Chart (M) shows more coast.
- [ ] **Bringing them ashore:** click the names again, and they are back at the fire.

**Careening:** launch with `--camphours=720 --raise=boat_shed --give=item.oakum:6,item.plank:3`. Thirty days
left alone wear her under 90%.
- [ ] Someone **Careens the ship**, and her hull goes up 10% a turn.

**Guns and privateers:**

```
-- --testdeck --ashore=coast.punta_ceniza --camphours=30 --panel=ship --raise=lookout,battery,magazine --give=item.powder:20,item.shot:20,item.trade_cloth:10 --meeting=privateer
```

- [ ] **The lookout's warning:** the Chronicle says "From the lookout: a sail to the south…". You have a day.
- [ ] **Land guns:** click it. The card says 2 on the platform.
- [ ] **Holding the ship:** send four aboard. The card says how many guns can be fought.
- [ ] **Her boat:** run to the next morning. The Gannet's boat comes, a meeting with four choices. Fight
      says "about N in 10".
- [ ] **Fight her off:** the outcome is in the Chronicle. Powder and shot are used. With four aboard, the
      ship is still at anchor whatever happened.

**Without a lookout or anyone aboard:** launch with `--meeting=privateer` alone.
- [ ] The Gannet is there at once, and Fight is greyed out.
- [ ] **Carry the stores into the forest:** the stores are plundered and the ship is cut out. The ship card
      says there is no ship.

## Reporting back

For each section, tell me which boxes worked. For anything wrong, send:
- a screenshot;
- what you did just before;
- any red lines in the Terminal.

A save that shows it helps most. It is in
`~/Library/Application Support/The Expedition/saves/quicksave.expedition.json`.
