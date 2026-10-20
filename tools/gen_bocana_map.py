"""Generates data/locations/bocana.json: the Bocana waterfront in five districts (PORT_EXPERIENCE.md §3).

One cell is about one metre. Row 0 is the back lane (north); the water is south. Districts:
  customs  - the Chamber and the Company house on a terrace (x 0-18), over the harbour, with steps down
  market   - the square: stalls, the well, the chandler, the surgeon (x 20-55, y 14-29)
  tavern   - the Three Anchors and the chapel along the north of the square, the alley between
  workshop - the ropewalk, cooperage, smithy and sail loft, and the shipwright's yard (x 56-79)
  quay     - the quay (y 30-34), vaults under the terrace, two piers, the Garza alongside
'features' are visual hints for the renderer (buildings, stalls, cargo); the simulation ignores them.
Run: python3 tools/gen_bocana_map.py
"""
import json, os

W, H = 80, 46
g = [['.' for _ in range(W)] for _ in range(H)]
features = []


def block(x0, y0, x1, y1, ch='#'):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            g[y][x] = ch


def feature(kind, x0, y0, x1=None, y1=None, style=None, solid=True):
    x1 = x0 if x1 is None else x1
    y1 = y0 if y1 is None else y1
    if solid:
        block(x0, y0, x1, y1)
    f = {"kind": kind, "x0": x0, "y0": y0, "x1": x1, "y1": y1}
    if style:
        f["style"] = style
    features.append(f)


# ---- water, piers and the Garza's berth
block(0, 35, W - 1, H - 1)
feature("pier", 8, 35, 10, 42, solid=False); block(8, 35, 10, 42, '.')
feature("pier", 44, 35, 46, 41, solid=False); block(44, 35, 46, 41, '.')
feature("ship", 20, 36, 42, 41, style="garza", solid=False)
feature("boat", 11, 37, 15, 38, style="mast", solid=False)
feature("boat", 4, 38, 7, 39, solid=False)
feature("boat", 47, 36, 52, 37, style="mast", solid=False)
feature("vessel", 52, 39, 74, 44, style="merchantman", solid=False)

# ---- customs and sponsor: the terrace
feature("terrace", 0, 0, 18, 24, solid=False)
block(19, 0, 19, 24)                        # retaining wall, east
block(0, 25, 18, 25)                        # retaining wall, south
feature("steps", 19, 15, 22, 18, style="east", solid=False); block(19, 15, 22, 18, '.')
feature("steps", 6, 25, 9, 29, style="south", solid=False); block(6, 25, 9, 29, '.')
feature("building", 1, 3, 9, 11, style="chamber")
feature("building", 11, 3, 17, 11, style="counting")
feature("vault", 0, 26, 5, 29); feature("vault", 10, 26, 18, 29)
feature("notice_board", 8, 13, 9, 13, solid=False)
feature("chart_table", 3, 16)
feature("table", 15, 15, 16, 15)
feature("flagpole", 12, 20)
feature("balustrade", 0, 24, 18, 24, solid=False)

# ---- tavern and chapel along the north of the square, the chandler, the alley between
feature("building", 23, 3, 34, 13, style="tavern")
# The Three Anchors' common room (y 6-12 inside the walls; the kitchen behind stays solid), its door on
# the square at (28, 13). Inside: the hearth on the back wall, the counter, two long tables.
block(24, 6, 33, 12, '.')
g[13][28] = '.'
block(24, 6, 25, 6)            # hearth
block(30, 8, 33, 8)            # counter
block(25, 10, 27, 10)          # long table
block(31, 11, 32, 11)          # small table
rooms = [{"id": "room.three_anchors", "name": "the Three Anchors", "x0": 24, "y0": 6, "x1": 33, "y1": 12, "round": 30}]
feature("building", 37, 3, 44, 13, style="chapel")
feature("building", 47, 3, 54, 13, style="warehouse")
feature("building", 20, 3, 21, 13, style="house")
feature("table", 24, 17, 26, 17); feature("table", 31, 17, 33, 17)
feature("fire", 35, 17)

# ---- the market square
feature("stall", 44, 22, 45, 22, style="fish"); feature("stall", 48, 22, 49, 22, style="fish")
feature("stall", 38, 24, 39, 24, style="produce"); feature("stall", 24, 23, 25, 23, style="cloth")
feature("stall", 34, 26, 35, 26, style="tools")
feature("well", 30, 21, 31, 22)
feature("cargo", 52, 17, 53, 18, style="casks")       # the chandler's yard

# ---- workshops: ropewalk, cooperage, smithy, sail loft, shipwright's yard
feature("ropewalk", 56, 3, 79, 5, solid=False)
block(56, 3, 79, 3); block(56, 5, 79, 5)
for x in range(58, 80, 6):
    g[5][x] = '.'                                       # openings onto the street
feature("building", 56, 7, 61, 12, style="cooperage")
feature("building", 63, 7, 68, 12, style="smithy")
feature("building", 70, 7, 78, 12, style="sailloft")
feature("cargo", 60, 14, 61, 15, style="barrels")
feature("anvil", 67, 14)
feature("canvas", 72, 15, 76, 16, solid=False)
feature("fence", 56, 20, 56, 29, solid=False)
for y in range(20, 30):
    if not 23 <= y <= 25:
        g[y][56] = '#'
feature("fence", 56, 20, 79, 20, solid=False)
block(57, 20, 79, 20)
for x in range(60, 64):
    g[20][x] = '.'                                      # yard gate from the street
feature("hull_on_stocks", 66, 25, 71, 33)
feature("timber", 75, 22, 78, 24)
feature("sawpit", 62, 31, 63, 32)

# ---- quayside
feature("cargo", 22, 31, 23, 32, style="crates"); feature("cargo", 38, 31, 39, 32, style="casks")
feature("cargo", 2, 31, 3, 31, style="bales"); feature("cargo", 49, 31, 50, 32, style="crates")
feature("net_frame", 12, 31, 13, 31)
feature("gaol_cart", 53, 32, 54, 32)
feature("planks", 25, 30, 27, 30)
feature("sawhorse", 28, 31)
for x in [1, 14, 19, 27, 35, 41, 48, 53]:
    feature("bollard", x, 34)

rows = [''.join(r) for r in g]
spots = []


def spot(id, x, y, tasks, kind, indoors=False, face=None):
    """face: the compass direction a person here looks (N is up the map, towards row 0)."""
    assert g[y][x] == '.', (id, x, y)
    d = {"id": id, "x": x, "y": y, "tasks": tasks, "kind": kind}
    if indoors:
        d["indoors"] = True
    if face:
        d["face"] = face
    spots.append(d)


# Shelter from the rain (R2d): under the eaves along the square, under the ropewalk's roof, in the
# mouths of the vaults. People caught out in rain wait here, looking out at it.
for i, (x, y) in enumerate([(24, 14), (27, 14), (32, 14), (39, 14), (42, 14), (48, 14), (52, 14),
                            (60, 4), (66, 4), (72, 4), (3, 30), (12, 30), (15, 30)]):
    spot(f"spot.shelter_{i + 1}", x, y, ["task.shelter"], "ShelterPoint", face="S")

# Three Anchors: benches outside on the square, and the common room inside
for i, x in enumerate([24, 26, 31, 33]):
    spot(f"spot.tavern_bench_{i + 1}", x, 16, ["task.drink", "task.gamble"], "SitPoint", face="S")
spot("spot.tavern_serving", 31, 7, ["task.serve_drinks"], "WorkBench", face="S")
seats = [(25, 9, "S"), (26, 9, "S"), (27, 9, "S"), (25, 11, "N"), (26, 11, "N"), (27, 11, "N"), (31, 10, "S"), (32, 10, "S"), (31, 12, "N"), (32, 12, "N")]
for i, (x, y, face) in enumerate(seats):
    spot(f"spot.tavern_inside_{i + 1}", x, y, ["task.drink", "task.gamble"], "SitPoint", face=face)
spot("spot.tavern_bar_1", 30, 9, ["task.drink"], "DrinkPoint", face="N"); spot("spot.tavern_bar_2", 32, 9, ["task.drink"], "DrinkPoint", face="N")
for i, (x, y) in enumerate([(24, 18), (26, 18), (31, 18), (33, 18), (23, 16), (34, 16)]):
    spot(f"spot.tavern_table_{i + 1}", x, y, ["task.drink", "task.gamble"], "SitPoint", face="N" if y == 18 else ("E" if x < 28 else "W"))
spot("spot.tavern_fire", 25, 7, ["task.port_cooking"], "WorkBench", face="N")
# chapel
spot("spot.chapel_door", 40, 14, ["task.pray"], "PrayerPoint", True)
spot("spot.chapel_steps_1", 38, 15, ["task.pray"], "PrayerPoint"); spot("spot.chapel_steps_2", 42, 15, ["task.pray"], "PrayerPoint")
for i in range(8):
    spot(f"spot.chapel_inside_{i + 1}", 40, 14, ["task.pray"], "PrayerPoint", True)
# the Chamber and the Company house, on the terrace
spot("spot.chamber_door", 5, 12, ["task.office_work"], "WorkBench", True)
for i, (x, y) in enumerate([(8, 14), (9, 14), (7, 15), (10, 15)]):
    spot("spot.chamber_board" + ("" if i == 0 else f"_{i + 1}"), x, y, ["task.read_notices"], "LeanPoint")
spot("spot.chart_table", 3, 17, ["task.study_charts"], "WorkBench")
spot("spot.counting_door", 14, 12, ["task.keep_accounts"], "WorkBench", True)
spot("spot.counting_table", 15, 16, ["task.keep_accounts"], "WorkBench"); spot("spot.counting_table_2", 16, 16, ["task.keep_accounts"], "WorkBench")
for i, x in enumerate([4, 11, 16]):
    spot(f"spot.overlook_{i + 1}", x, 23, ["task.watch_water"], "WatchWater")
# chandler
spot("spot.chandler_counter", 50, 14, ["task.trade_goods"], "MarketStall"); spot("spot.chandler_yard", 51, 17, ["task.trade_goods"], "MarketStall")
# market
# sellers at the stall's east end (seen from the camera), customers at the west end facing them across the board
spot("spot.fish_stall_1", 46, 22, ["task.sell_fish"], "MarketStall", face="W"); spot("spot.fish_stall_2", 50, 22, ["task.sell_fish"], "MarketStall", face="W")
spot("spot.fish_customer_1", 43, 22, ["task.buy_at_market"], "ConversationPoint", face="E"); spot("spot.fish_customer_2", 47, 22, ["task.buy_at_market"], "ConversationPoint", face="E")
# two people at the well, facing each other
spot("spot.well_talk_1", 30, 23, ["task.gossip"], "ConversationPoint", face="E"); spot("spot.well_talk_2", 31, 23, ["task.gossip"], "ConversationPoint", face="W")
spot("spot.surgeon_stool", 22, 26, ["task.treat_patients"], "WorkBench")
spot("spot.dun", 21, 27, ["task.dun_debtor"], "ConversationPoint")
for i, (x, y) in enumerate([(27, 24), (33, 23), (28, 27), (41, 27)]):
    spot(f"spot.square_{i + 1}", x, y, ["task.play"], "ConversationPoint")
# workshops
spot("spot.cooper_bench", 58, 14, ["task.coopering"], "HammerStation")
spot("spot.yard_bench_1", 60, 23, ["task.port_carpentry"], "SawStation"); spot("spot.yard_bench_2", 60, 27, ["task.port_carpentry"], "HammerStation")
spot("spot.ropewalk_1", 57, 4, ["task.make_rope"], "RopeWork")
spot("spot.forge", 66, 13, ["task.smithing"], "HammerStation")
spot("spot.sail_loft", 73, 14, ["task.mend_sails"], "RopeWork")
# quay
cargo = [(21, 30), (24, 30), (37, 30), (40, 30), (48, 30), (51, 30), (2, 30), (13, 30), (16, 30)]
for i, (x, y) in enumerate(cargo):
    spot(f"spot.cargo_{i + 1}", x, y, ["task.haul_cargo"], "CarryPickup")
spot("spot.boat_rigging_1", 10, 37, ["task.port_rigging"], "RopeWork"); spot("spot.boat_rigging_2", 46, 37, ["task.port_rigging"], "RopeWork")
for i, (x, y) in enumerate([(11, 32), (13, 32), (12, 33), (14, 32)]):
    spot(f"spot.nets_{i + 1}", x, y, ["task.mend_nets"], "RopeWork")
spot("spot.gaol_cart", 54, 31, ["task.wait_in_irons"], "SitPoint", face="N"); spot("spot.guard_post", 55, 30, ["task.stand_guard"], "LeanPoint", face="SW")
# where work loops pick up and set down (PORT_EXPERIENCE §7)
for i, (x, y) in enumerate([(45, 41), (44, 40), (46, 40), (44, 38)]):
    spot(f"spot.lighter_{i + 1}", x, y, ["task.deliver_cargo"], "CarryDropoff", face="S")
spot("spot.catch_1", 10, 38, ["task.land_catch"], "CarryPickup", face="E"); spot("spot.catch_2", 8, 39, ["task.land_catch"], "CarryPickup", face="W")
# porters catch their breath beside the pier head between loads (a worker resting by an active worksite)
spot("spot.cargo_rest_1", 43, 33, ["task.catch_breath"], "SitPoint", face="S"); spot("spot.cargo_rest_2", 47, 33, ["task.catch_breath"], "SitPoint", face="S")
spot("spot.cargo_rest_3", 42, 33, ["task.catch_breath"], "SitPoint", face="SE")
spot("spot.stall_drop_1", 45, 23, ["task.carry_catch"], "CarryDropoff", face="N"); spot("spot.stall_drop_2", 49, 23, ["task.carry_catch"], "CarryDropoff", face="N")
spot("spot.timber_stack", 74, 23, ["task.fetch_timber"], "CarryPickup")
spot("spot.hull_1", 65, 27, ["task.fit_timber"], "HammerStation"); spot("spot.hull_2", 72, 29, ["task.fit_timber"], "HammerStation")
spot("spot.ropewalk_2", 78, 4, ["task.lay_rope"], "RopeWork")
spot("spot.chandler_drop", 53, 16, ["task.deliver_cask"], "CarryDropoff")
spot("spot.garza_side_1", 29, 33, ["task.deliver_canvas", "task.inspect_ship"], "ShipInspection", face="S")
spot("spot.garza_side_2", 34, 33, ["task.deliver_canvas", "task.inspect_ship"], "ShipInspection", face="S")
# the repair station at the Garza's side: plank stack -> sawhorse -> fitting at the hull; a helper steadies the work
spot("spot.repair_saw", 28, 32, ["task.saw_planks"], "SawStation", face="N")
spot("spot.repair_plank", 25, 31, ["task.fetch_plank"], "CarryPickup", face="N")
spot("spot.repair_fit", 26, 34, ["task.fit_garza_plank"], "HammerStation", face="S")
spot("spot.repair_helper", 29, 31, ["task.steady_plank"], "HelpPoint", face="W")
# the alley between the tavern and the chapel
spot("spot.alley_1", 35, 8, ["task.meet_in_secret"], "ConversationPoint"); spot("spot.alley_2", 36, 8, ["task.meet_in_secret"], "ConversationPoint")
# homes: doors on the back lane
for i, x in enumerate(range(21, 80, 2)):
    spot(f"spot.home_{i + 1:02d}", x, 1, ["task.sleep_at_home"], "RestPoint", True)
# fine houses up on the terrace lane
for i, x in enumerate([2, 6, 10, 14]):
    spot(f"spot.home_{i + 31:02d}", x, 1, ["task.sleep_at_home"], "RestPoint", True)
spot("spot.gangway", 31, 34, ["task.board"], "BoardShip")

ground = [
    {"x0": 0, "y0": 0, "x1": 18, "y1": 24, "height": 2.6},
    {"x0": 19, "y0": 15, "x1": 22, "y1": 18, "height": 2.6, "toHeight": 0, "along": "x"},
    {"x0": 6, "y0": 25, "x1": 9, "y1": 29, "height": 2.6, "toHeight": 0, "along": "y"},
]
loc = [{"id": "location.bocana", "name": "The Bocana waterfront", "sightRange": 14, "walkCellsPerMinute": 2.5,
        "rows": rows, "spots": spots, "ground": ground, "rooms": rooms, "features": features}]
here = os.path.dirname(os.path.abspath(__file__))
with open(os.path.join(here, '..', 'data', 'locations', 'bocana.json'), 'w') as f:
    f.write("// The Bocana waterfront at La Bocana (SETTING.md §7) in five districts (PORT_EXPERIENCE.md §3, R2a).\n"
            "// Generated by tools/gen_bocana_map.py: edit that and re-run. 1 cell is about 1 m; row 0 is the back\n"
            "// lane (north), the water is south. 'ground' raises the customs terrace; 'features' are for drawing only.\n"
            + json.dumps(loc, ensure_ascii=False, indent=1))
print("\n".join(rows))
