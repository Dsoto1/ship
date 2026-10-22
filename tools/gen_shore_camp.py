"""Generates data/locations/shore_camp.json: the beach where the expedition makes its first camp (R6,
docs/R6_LANDFALL_CAMP.md). One cell is about a metre. Row 0 is the water's edge, where the boat lands;
the beach rises to the forest's edge at about row 20. The same ground serves every landing site for now;
the site's own water, food and finds are in data/shore.json.
  the landing      row 1, middle: the boat drawn up, the stores landed
  the shore        rows 1-3, left: shellfish and fishing
  the camp         rows 6-12: the hearth, the stores, four shelters in a row, the sick shelter apart
  the latrine      right, below the forest and away from the water path
  the water path   up the right side into the forest, to the water (its true distance is the site's)
  the forest edge  rows 18-23, a scout's way in on the left; poles and thatch cut along it (R7)
  the plots        R7: huts in a row above the camp, the storehouse, cookhouse, infirmary, workshop, garden
Run: python3 tools/gen_shore_camp.py
"""
import json, os, random

W, H = 36, 26
rows = [["." for _ in range(W)] for _ in range(H)]
for x in range(W):
    rows[0][x] = "#"          # the sea
    rows[H - 1][x] = "#"      # the forest closes in
for y in range(H):
    rows[y][0] = rows[y][W - 1] = "#"

# The forest's edge: trees thicker as it goes inland, leaving the water path and the scout's way open.
rng = random.Random(1519)
for y in range(18, H - 1):
    for x in range(1, W - 1):
        open_way = x in (2, 3, 4, 5) or x in (30, 31, 32, 33)
        if not open_way and rng.random() < 0.18 + 0.1 * (y - 18):
            rows[y][x] = "#"
# Driftwood and rocks on the beach.
for x, y in [(6, 4), (28, 3), (2, 15), (34, 16)]:
    rows[y][x] = "o"
# Where poles and thatch are cut, at the forest's edge (R7): kept open.
CUTS = {"spot.poles_1": (8, 18), "spot.poles_2": (12, 19), "spot.poles_3": (20, 18), "spot.thatch_1": (16, 18), "spot.thatch_2": (24, 18)}
for x, y in CUTS.values():
    rows[y][x] = "."

spots = []
def spot(id_, x, y, tasks, **extra):
    assert rows[y][x] == ".", (id_, x, y)
    spots.append({"id": id_, "x": x, "y": y, "tasks": tasks, **extra})

spot("spot.landing_1", 17, 1, ["task.land_stores"], kind="CarryPickup", face="N")
spot("spot.landing_2", 19, 1, ["task.land_stores"], kind="CarryPickup", face="N")
spot("spot.shore_1", 8, 2, ["task.forage_shore"], kind="WatchWater", face="N")
spot("spot.shore_2", 11, 1, ["task.forage_shore"], kind="WatchWater", face="N")
spot("spot.hearth", 18, 7, ["task.make_fire", "task.tend_fire", "task.cook_ashore"], kind="Cook")
spot("spot.hearth_bench", 19, 6, ["task.mend_tools"], kind="HammerStation")
spot("spot.stores", 25, 6, ["task.cover_stores"], kind="HammerStation")
for i, x in enumerate([11, 15, 21, 25]):
    spot(f"spot.shelter_{'abcd'[i]}", x, 10, ["task.rig_sail_shelter", "task.cut_brush_shelter"], kind="HammerStation")
spot("spot.sick", 7, 12, ["task.build_sick_shelter"], kind="HammerStation")
spot("spot.latrine", 32, 14, ["task.dig_latrine"], kind="HammerStation")
spot("spot.water_1", 31, 23, ["task.fetch_water"], kind="CarryPickup", face="S")
spot("spot.water_2", 33, 23, ["task.fetch_water"], kind="CarryPickup", face="S")
spot("spot.scout_1", 3, 23, ["task.scout"], face="S")
spot("spot.scout_2", 5, 23, ["task.scout"], face="S")

# R7: the plots buildings are raised on, two places to work at each. A plot's spots list every phase of
# building, the thatch's repair, and what the building does once made (the workshop's mending, the
# garden's work); the simulation keeps each to its proper time.
PHASES = ["task.clear_ground", "task.set_posts", "task.raise_frame", "task.thatch_roof", "task.fit_out", "task.repair_building"]
PLOTS = {
    "hut_1": (10, 15), "hut_2": (14, 15), "hut_3": (18, 15), "hut_4": (22, 15),
    "store_1": (27, 12), "cook_1": (14, 6), "infirmary_1": (4, 16), "workshop_1": (29, 8), "garden_1": (26, 16),
}
for plot, (x, y) in PLOTS.items():
    extra = ["task.mend_tools"] if plot.startswith("workshop") else []
    if plot.startswith("garden"):
        tasks = ["task.clear_ground", "task.dig_beds", "task.fence_garden", "task.sow_garden", "task.tend_garden", "task.harvest"]
    else:
        tasks = PHASES + extra
    spot(f"plot.{plot}.a", x, y, tasks, kind="HammerStation")
    spot(f"plot.{plot}.b", x + 1, y, tasks, kind="HammerStation")
for id_, (x, y) in CUTS.items():
    spot(id_, x, y, ["task.cut_poles"] if "poles" in id_ else ["task.cut_thatch"], kind="HammerStation", face="S")

ground = [
    {"x0": 1, "y0": 4, "x1": W - 2, "y1": 8, "height": 0.0, "toHeight": 0.35, "along": "y"},
    {"x0": 1, "y0": 9, "x1": W - 2, "y1": 17, "height": 0.35, "toHeight": 0.8, "along": "y"},
    {"x0": 1, "y0": 18, "x1": W - 2, "y1": H - 1, "height": 0.8, "toHeight": 1.4, "along": "y"},
]
loc = {
    "id": "location.shore_camp",
    "name": "The camp ashore",
    "rows": ["".join(r) for r in rows],
    "spots": spots,
    "ground": ground,
}
out = os.path.join(os.path.dirname(__file__), "..", "data", "locations", "shore_camp.json")
with open(out, "w") as f:
    f.write("// Generated by tools/gen_shore_camp.py: the beach of the first camp (docs/R6_LANDFALL_CAMP.md). Do not edit by hand.\n")
    json.dump([loc], f, indent=2)
    f.write("\n")
print(out)
