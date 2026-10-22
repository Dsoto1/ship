"""Generates data/locations/garza.json: the deck of the Garza, a caravela redonda of about 22 m.

One cell is about one metre; the bow is row 0. She carries a square-rigged fore and main and a lateen
mizzen. From bow to stern:
  forecastle   rows 1-4, raised 1.8 m (standing room under it), the lookout in its eyes and the galley fire box at its break
  waist        rows 5-16: foremast, the main hatch over the hold, mainmast with a pump either side
  quarterdeck  rows 18-23, raised 1.9 m (the steerage under it): the whipstaff (helm) at its break, the mizzen, the stern watch
Ladders join the waist to each deck. 'ground' gives the heights; the simulation's grid is flat.
Run: python3 tools/gen_garza_deck.py
"""
import json, os

ROWS = [
    "###...###",  # 0  the eyes of the ship
    "##.....##",  # 1  forecastle
    "#.......#",  # 2
    "#.......#",  # 3
    "#o......#",  # 4  forecastle break, the galley below its rail; the bell to port
    "#.....o.#",  # 5  ladder down to the waist; the galley firebox against the break
    "#...#...#",  # 6  foremast
    "#o.....o#",  # 7  a gun either side
    "#.......#",  # 8
    "#..###..#",  # 9  main hatch
    "#..###..#",  # 10
    "#o.....o#",  # 11 a gun either side
    "#.......#",  # 12
    "#...#...#",  # 13 mainmast, the pumps either side
    "#.......#",  # 14
    "#o..o..o#",  # 15 a gun either side; the capstan
    "#.......#",  # 16
    "#.......#",  # 17 ladder up to the quarterdeck
    "#.......#",  # 18 quarterdeck, the whipstaff
    "#.......#",  # 19
    "#...#...#",  # 20 mizzen
    "#.......#",  # 21
    "#.......#",  # 22
    "##.....##",  # 23 the taffrail
    "#########",
]
assert all(len(r) == 9 for r in ROWS)

# Below decks (docs/GARZA_INTERIOR.md): two more blocks of the same grid, side by side with the deck,
# each drawn by the game in its true place under the deck (its 'levels' entry gives the shift). A
# caravel of her size has no deck between: the covered spaces are at the waist's level under the raised
# decks, and the hold is under the whole waist, on the ballast. Ladders join them ('links').
#   covered  columns 10-18: the forecastle space (rows 1-4), the steerage under the quarterdeck
#            (rows 18-22), the captain's cabin under the poop (rows 24-25, at the quarterdeck's level)
#   hold     columns 20-28: forepeak (cables, sails), the casks, the pump well, the provisions, the
#            bread room and the powder room aft
COVERED = [
    "#########",  # 0
    "##.....##",  # 1  forecastle space: pallets and the sick berths
    "#.......#",  # 2
    "#.......#",  # 3
    "#.......#",  # 4  its door to the waist, under the forecastle's break
] + ["#########"] * 13 + [  # 5-17: the waist is open above, the hold below
    "#.......#",  # 18 steerage, under the quarterdeck: the pilot's and master's berths
    "#.......#",  # 19
    "#...#...#",  # 20 the mizzen passes through
    "#.......#",  # 21
    "#.......#",  # 22
    "#########",  # 23 the bulkhead under the taffrail
    "##.....##",  # 24 the captain's cabin under the poop, its windows astern
    "###...###",  # 25
]
HOLD = [
    "#########",  # 0
    "###ooo###",  # 1  forepeak: the cable tier
    "##o...o##",  # 2  the sail locker either side
    "##.....##",  # 3
    "##.....##",  # 4
    "##.....##",  # 5
    "##..#..##",  # 6  the foremast's step
    "##o...o##",  # 7  water and wine casks in tiers on the ballast
    "##o...o##",  # 8
    "##o...o##",  # 9
    "##o...o##",  # 10
    "##o...o##",  # 11 the ladder from the main hatch comes down here
    "##o...o##",  # 12
    "##..#..##",  # 13 the mainmast's step and the pump well
    "##o...o##",  # 14 barrels: pork, oil, vinegar
    "##o...o##",  # 15
    "##o...o##",  # 16
    "##.....##",  # 17 the bread room's bulkhead, its door
    "##o...o##",  # 18 the bread room; the scuttle up to the steerage
    "##o..o###",  # 19 and the powder room, aft and dry
    "#########",  # 20
] + ["#########"] * 5
DECK = ROWS + ["#########"]
assert len(DECK) == len(COVERED) == len(HOLD) == 26
GRID = [d + "#" + c + "#" + h for d, c, h in zip(DECK, COVERED, HOLD)]
COVERED_X, HOLD_X = 10, 20
spots = []


def spot(id, x, y, tasks, kind=None, face=None, **extra):
    assert GRID[y][x] == '.', (id, x, y)
    d = {"id": id, "x": x, "y": y, "tasks": tasks}
    if kind:
        d["kind"] = kind
    if face:
        d["face"] = face
    d.update(extra)
    spots.append(d)


spot("spot.watch_bow", 4, 1, ["task.stand_watch"], "WatchWater", "N")
spot("spot.galley", 6, 6, ["task.cook"], "WorkBench", "N")
spot("spot.rigging_fore", 2, 6, ["task.repair_rigging"], "RopeWork", "W")
spot("spot.hull_patch", 2, 10, ["task.patch_hull"], "HammerStation", "E")
spot("spot.pump", 2, 13, ["task.pump_bilge"], "HelpPoint", "E")
spot("spot.pump_aft", 6, 13, ["task.pump_bilge"], "HelpPoint", "W")
spot("spot.rigging_main", 6, 14, ["task.repair_rigging"], "RopeWork", "E")
spot("spot.helm", 4, 18, ["task.stand_watch"], "WorkBench", "N")
spot("spot.watch_stern", 4, 22, ["task.stand_watch"], "WatchWater", "S")

# Below decks: where the watch below sleeps (in the forecastle space in foul weather, on deck in the
# waist in fair), the sick berths by the galley's warmth, the officers aft, the captain in his cabin;
# lashing the cargo and plugging leaks in the hold.
C, H = COVERED_X, HOLD_X
for i, (x, y) in enumerate([(C + 2, 1), (C + 3, 1), (C + 5, 1), (C + 6, 1), (C + 1, 2), (C + 7, 2), (C + 1, 3), (C + 2, 3)]):
    spot(f"spot.berth_fore_{i + 1}", x, y, ["task.rest"], "Lying", indoors=True)
for i, (x, y) in enumerate([(C + 6, 3), (C + 7, 3)]):
    spot(f"spot.sick_{i + 1}", x, y, ["task.rest"], "Lying", indoors=True, sick=True)
spot("spot.tend_sick", C + 5, 3, ["task.tend_sick"], "HelpPoint", "E", indoors=True)
for i, (x, y) in enumerate([(2, 8), (6, 8), (2, 12), (6, 12), (3, 16), (5, 16)]):
    spot(f"spot.sleep_waist_{i + 1}", x, y, ["task.rest"], "Lying")
for i, (x, y) in enumerate([(C + 1, 19), (C + 7, 19)]):
    spot(f"spot.berth_aft_{i + 1}", x, y, ["task.rest"], "Lying", indoors=True)
spot("spot.cabin", C + 3, 24, ["task.rest"], "Lying", indoors=True, reserved="captain")
for i, (x, y) in enumerate([(H + 3, 9), (H + 5, 15)]):
    spot(f"spot.hold_lash_{i + 1}", x, y, ["task.lash_cargo"], "RopeWork", indoors=True)
# Where each leak is met from inside: by its name (SimulationRunner.LeakPlaces).
for name, (x, y) in {"starboard_bow": (H + 6, 3), "larboard_bow": (H + 2, 3), "starboard_waist": (H + 5, 10),
                     "larboard_waist": (H + 3, 10), "counter": (H + 4, 18), "garboard": (H + 4, 12)}.items():
    spot(f"spot.leak_{name}", x, y, ["task.plug_leak"], "HammerStation", indoors=True)

ground = [
    {"x0": 0, "y0": 0, "x1": 8, "y1": 4, "height": 1.8},
    {"x0": 3, "y0": 5, "x1": 5, "y1": 5, "height": 1.8, "toHeight": 0, "along": "y"},
    {"x0": 3, "y0": 17, "x1": 5, "y1": 17, "height": 0, "toHeight": 1.9, "along": "y"},
    {"x0": 0, "y0": 18, "x1": 8, "y1": 24, "height": 1.9},
    # The cabin under the poop is on the quarterdeck's level; the hold's floor (planks over the ballast)
    # is deepest amidships.
    {"x0": COVERED_X, "y0": 24, "x1": COVERED_X + 8, "y1": 25, "height": 1.9},
    {"x0": HOLD_X, "y0": 0, "x1": HOLD_X + 8, "y1": 4, "height": -1.4},
    {"x0": HOLD_X, "y0": 5, "x1": HOLD_X + 8, "y1": 16, "height": -1.75},
    {"x0": HOLD_X, "y0": 17, "x1": HOLD_X + 8, "y1": 20, "height": -1.5},
]
# Each block below and where the game draws it: shifted back under the deck.
levels = [
    {"id": "covered", "name": "under the raised decks", "x0": COVERED_X, "x1": COVERED_X + 8, "offsetX": -COVERED_X},
    {"id": "hold", "name": "the hold", "x0": HOLD_X, "x1": HOLD_X + 8, "offsetX": -HOLD_X},
]
# The ladders and doors between them, both ways.
links = [
    {"x0": 2, "y0": 5, "x1": COVERED_X + 2, "y1": 4},   # the forecastle space's door, from the waist
    {"x0": 5, "y0": 11, "x1": HOLD_X + 4, "y1": 11},    # down the main hatch, by the boat's side
    {"x0": 2, "y0": 17, "x1": COVERED_X + 2, "y1": 18},  # the steerage's door, under the quarterdeck's break
    {"x0": COVERED_X + 4, "y0": 22, "x1": HOLD_X + 4, "y1": 18},  # the scuttle down to the bread room
    {"x0": 4, "y0": 23, "x1": COVERED_X + 4, "y1": 24},  # the cabin's door, from the quarterdeck under the poop
]
for l in links:
    assert GRID[l["y0"]][l["x0"]] == '.' and GRID[l["y1"]][l["x1"]] == '.', l
# What stands on the 'o' cells (drawn by ShipView from game/Assets/Props, made by tools/props).
features = [{"kind": "fitting", "style": "bell", "x0": 1, "y0": 4, "x1": 1, "y1": 4},
            {"kind": "fitting", "style": "galley", "x0": 6, "y0": 5, "x1": 6, "y1": 5},
            {"kind": "fitting", "style": "capstan", "x0": 4, "y0": 15, "x1": 4, "y1": 15}]
features += [{"kind": "fitting", "style": "gun", "x0": x, "y0": y, "x1": x, "y1": y} for y in (7, 11, 15) for x in (1, 7)]
assert all(ROWS[f["y0"]][f["x0"]] == "o" for f in features) and sum(r.count("o") for r in ROWS) == len(features)
# Below, 'o' cells are stowage (casks, barrels, the cable tier): drawn by ShipView, not props.

loc = [{"id": "location.garza", "name": "The Garza", "rows": GRID, "spots": spots, "ground": ground, "features": features,
        "levels": levels, "links": links}]
here = os.path.dirname(os.path.abspath(__file__))
with open(os.path.join(here, "..", "data", "locations", "garza.json"), "w") as f:
    f.write("// The deck of the Garza (SETTING.md §4): generated by tools/gen_garza_deck.py, edit that and re-run.\n"
            "// 1 cell is about 1 m, the bow is row 0; 'ground' raises the forecastle and the quarterdeck.\n"
            + json.dumps(loc, ensure_ascii=False, indent=1))
print("\n".join(GRID))
