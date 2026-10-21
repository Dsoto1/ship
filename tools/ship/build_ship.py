#!/usr/bin/env python3
"""Builds the Garza's hull (game/Assets/Ship/garza.glb) from her written specification
(tools/ship/garza.json) and her deck plan (data/locations/garza.json).

The model is her outside and the parts no one walks on: the planked hull from keel to sheer, strake by
strake, with two wales, the painted band, the tallowed bottom and the dark boot-top at the waterline;
the stem and sternpost; the flat transom with its windows and mouldings over the tuck; the rudder; the
poop's deck and its front bulkhead with the cabin door; port lids and hawse holes. The game draws the
decks, bulwarks' inner faces, rails, masts, rigging and sails itself (ShipView), round the same deck plan,
so the hull here is shaped by the very rule ShipView uses: half-breadth from the walkable span of each
row, the sheer from the raised decks. Coordinates are ShipView's: x the grid column, z the row (bow at
-z), y up from the waist deck. Vertex colours carry the palette; the game's scenery material adds the
grain. Run: python3 tools/ship/build_ship.py
"""
import json
import math
import pathlib
import re
import struct

import numpy as np

ROOT = pathlib.Path(__file__).resolve().parents[2]
SPEC = json.loads((ROOT / "tools" / "ship" / "garza.json").read_text())
OUT = ROOT / "game" / "Assets" / "Ship" / "garza.glb"


def hexcolour(h, alpha=1.0):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)] + [alpha])


C = {k: hexcolour(v) for k, v in SPEC["colours"].items()}


def smoothstep(a, b, x):
    t = min(max((x - a) / (b - a), 0.0), 1.0)
    return t * t * (3 - 2 * t)


class Deck:
    """The deck plan, read the way ShipView reads it."""

    def __init__(self, path):
        text = re.sub(r"^\s*//.*$", "", path.read_text(), flags=re.M)
        loc = json.loads(text)[0]
        self.rows = loc["rows"]
        self.ground = loc.get("ground", [])
        wall = SPEC["wallThickness"]
        self.half = []
        last, left, right = 0, 99, 0
        for y, row in enumerate(self.rows):
            walk = [x for x, c in enumerate(row) if c in ".o"]
            if not walk:
                self.half.append(None)
                continue
            last = y
            left, right = min(left, min(walk)), max(right, max(walk))
            self.half.append((max(walk) - min(walk)) / 2 + 0.5 + wall)
        self.half = [h if h is not None else self.half[last] for h in self.half]
        self.centre = (left + right) / 2
        self.stern_z = last + 0.5
        self.bow, self.stern = -1.9, self.stern_z + 1.9

    def half_width(self, z):
        if z <= 0:
            t = min(max((z - self.bow) / (0 - self.bow), 0), 1)
            return 0.04 + (self.half[0] - 0.04) * math.sin(t * math.pi / 2)
        row = min(max(z, 0), len(self.half) - 1)
        below = int(math.floor(row))
        above = min(below + 1, len(self.half) - 1)
        return self.half[below] + (self.half[above] - self.half[below]) * (row - below)

    def height_at(self, x, z):
        for g in self.ground:
            if x < g["x0"] - 0.5 or x > g["x1"] + 0.5 or z < g["y0"] - 0.5 or z > g["y1"] + 0.5:
                continue
            if "toHeight" not in g:
                return g["height"]
            if g.get("along") == "x":
                t = (x - (g["x0"] - 0.5)) / (g["x1"] - g["x0"] + 1)
            else:
                t = (z - (g["y0"] - 0.5)) / (g["y1"] - g["y0"] + 1)
            return g["height"] + (g["toHeight"] - g["height"]) * min(max(t, 0), 1)
        return 0.0

    def bulwark_top(self, z):
        """ShipView.BulwarkTop, carried on past the stern to the poop rail and forward to the stem."""
        bow = 1 - smoothstep(-2, 4, z)
        stern = smoothstep(self.stern_z - 6, self.stern_z, z)
        sheer = 0.7 + bow * 0.7 + stern * 0.45
        side = self.height_at(self.centre - self.half_width(z) + 0.8, min(max(z, 0), len(self.half) - 1))
        top = max(sheer, side + 0.85)
        if z > self.stern_z:
            top = max(top, SPEC["poop"]["rail"] * smoothstep(self.stern_z, self.stern_z + 0.5, z) + top * (1 - smoothstep(self.stern_z, self.stern_z + 0.5, z)))
        return top

    def depth_scale(self, z):
        t = min(max((z - self.bow) / (self.stern_z + 0.6 - self.bow), 0), 1)
        return 0.72 + 0.28 * math.sin(t * math.pi)


class Mesh:
    def __init__(self):
        self.p, self.c, self.i = [], [], []

    def tri(self, a, b, c, colour):
        base = len(self.p)
        self.p += [a, b, c]
        self.c += [colour] * 3
        self.i += [base, base + 1, base + 2]

    def quad(self, a, b, c, d, colour):
        self.tri(a, b, c, colour)
        self.tri(a, c, d, colour)

    def box(self, centre, size, colour, rot_y=0.0):
        cx, cy, cz = centre
        sx, sy, sz = (s / 2 for s in size)
        cr, sr = math.cos(rot_y), math.sin(rot_y)

        def v(x, y, z):
            return (cx + x * cr + z * sr, cy + y, cz - x * sr + z * cr)

        corners = [v(x, y, z) for x in (-sx, sx) for y in (-sy, sy) for z in (-sz, sz)]
        for a, b, c, d in [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]:
            self.quad(corners[a], corners[b], corners[c], corners[d], colour)


def section(deck, z, y):
    """Half-breadth of the hull at height y in the station at z: tumblehome above the greatest breadth,
    a round bilge and some deadrise below it, and aft of the quarterdeck the run closing to the
    sternpost under the tuck while the transom stands full above it."""
    w = deck.half_width(z)
    d = SPEC["keelDepth"] * deck.depth_scale(z)
    top = deck.bulwark_top(z)
    m = SPEC["maxBreadthAt"]
    if y >= m:
        f = 1.03 - SPEC["tumblehome"] * (y - m) / max(top - m, 0.1)
    else:
        u = min((m - y) / (d + m), 1.0)
        f = 1.03 * math.sqrt(max(1 - u ** 2.4, 0)) + 0.05 * (1 - u)
    if z > deck.stern_z and y < SPEC["sternTuck"]:
        t = (z - deck.stern_z) / (deck.stern - deck.stern_z)
        depth = (SPEC["sternTuck"] - y) / (d + SPEC["sternTuck"])
        f *= 1 - min(t * (0.4 + 1.2 * depth), 0.95)
    return max(w * f, 0.03)


def sheer(deck, z):
    """The line of the waist's rail (ShipView's sheer, without the castles): the strakes follow it."""
    bow = 1 - smoothstep(-2, 4, z)
    stern = smoothstep(deck.stern_z - 6, deck.stern_z, z)
    return 0.7 + bow * 0.7 + stern * 0.45


def strake_levels():
    """The strakes' edges at midships, from the keel up to the top of the band: every strake, with the
    waterline, the boot-top, the wales and the band among them."""
    water, step = SPEC["waterline"], SPEC["strake"]
    levels = {round(water + k * step, 3) for k in range(-6, 8)}
    levels |= {water - 0.12, water + 0.08, SPEC["band"][0] - 0.07, *SPEC["band"]}
    for w in SPEC["wales"]:
        levels |= {w - 0.08, w + 0.08}
    levels = sorted(l for l in levels if -SPEC["keelDepth"] < l <= SPEC["band"][1])
    return levels


def level_y(deck, level, z):
    """Where a strake edge lies at z: flat along the waterline, rising with the sheer towards the rail."""
    water = SPEC["waterline"]
    lift = min(max((level - water) / (0.7 - water), 0), 1)
    return level + (sheer(deck, z) - 0.7) * lift


def strake_colour(lower, upper, band_index):
    mid = (lower + upper) / 2
    water = SPEC["waterline"]
    if SPEC["band"][0] <= mid <= SPEC["band"][1]:
        return C["band"]
    if SPEC["band"][0] - 0.07 <= mid < SPEC["band"][0]:
        return C["red"]
    if any(abs(mid - w) < 0.08 for w in SPEC["wales"]):
        return C["wale"]
    if mid < water - 0.12:
        return C["bottom"]
    if mid < water + 0.08:
        return C["boot"]
    return C["plank"] if band_index % 2 == 0 else C["plankAlt"]


def hull(deck, mesh):
    zs = np.arange(deck.bow, deck.stern + 0.001, 0.25)
    levels = strake_levels()
    cx = deck.centre
    for zi in range(len(zs) - 1):
        z0, z1 = zs[zi], zs[zi + 1]
        t0, t1 = deck.bulwark_top(z0), deck.bulwark_top(z1)
        k0, k1 = -SPEC["keelDepth"] * deck.depth_scale(z0), -SPEC["keelDepth"] * deck.depth_scale(z1)
        # The side as bands between strake edges: the keel, each strake edge, then the rail.
        edges = [(None, k0, k1)] + [(l, max(level_y(deck, l, z0), k0), max(level_y(deck, l, z1), k1)) for l in levels] + [(None, t0, t1)]
        rows = []
        for (la, a0, a1), (lb, b0, b1) in zip(edges, edges[1:]):
            # Bands are cut into a few rows each so the bilge stays round.
            pieces = max(1, int(abs(b0 - a0) / 0.22))
            for p in range(pieces):
                f, g = p / pieces, (p + 1) / pieces
                rows.append((a0 + (b0 - a0) * f, a1 + (b1 - a1) * f, a0 + (b0 - a0) * g, a1 + (b1 - a1) * g, la, lb))
        for index, (ya0, ya1, yb0, yb1, la, lb) in enumerate(rows):
            lower = la if la is not None else -SPEC["keelDepth"]
            upper = lb if lb is not None else 1.5
            band = int(math.floor((lower - SPEC["waterline"]) / SPEC["strake"] + 0.001))
            colour = strake_colour(lower, upper, band)
            if colour is C["plank"] or colour is C["plankAlt"]:
                if (int(z0 * 4) + band * 7) % 23 == 0:
                    colour = C["butt"]
            if min(ya0, ya1) >= min(t0, t1) - 0.001 and colour is C["band"]:
                colour = C["plank"]
            for side in (-1, 1):
                def p(y, z):
                    return (cx + side * section(deck, z, y), y, z)

                a, b, c, d = p(yb0, z0), p(yb1, z1), p(ya1, z1), p(ya0, z0)
                if side < 0:
                    mesh.quad(a, d, c, b, colour)
                else:
                    mesh.quad(a, b, c, d, colour)
                if colour is C["wale"] and deck.bow + 0.6 < z0 < deck.stern - 0.4:
                    o = 0.05 * side
                    a2, b2 = (a[0] + o, a[1], a[2]), (b[0] + o, b[1], b[2])
                    c2, d2 = (c[0] + o, c[1], c[2]), (d[0] + o, d[1], d[2])
                    if side < 0:
                        mesh.quad(a2, d2, c2, b2, C["wale"])
                    else:
                        mesh.quad(a2, b2, c2, d2, C["wale"])


def stem_and_stern(deck, mesh):
    cx = deck.centre
    # The stem: a curved post rising from the keel ahead of the forefoot to above the forecastle rail.
    points = []
    for i in range(12):
        t = i / 11
        y = -SPEC["keelDepth"] * deck.depth_scale(deck.bow) * (1 - t) + (deck.bulwark_top(deck.bow) + 0.35) * t
        z = deck.bow - 0.15 - 0.55 * math.sin(t * math.pi / 2) ** 1.5
        points.append((y, z))
    for (y0, z0), (y1, z1) in zip(points, points[1:]):
        mesh.box((cx, (y0 + y1) / 2, (z0 + z1) / 2), (0.2, abs(y1 - y0) + 0.08, 0.26), C["wale"])
    # Hawse holes either side of the stem.
    for side in (-1, 1):
        z = deck.bow + 0.9
        x = cx + side * (section(deck, z, 0.95) + 0.01)
        mesh.box((x, 0.95, z), (0.03, 0.18, 0.18), C["window"])
    # The keel along the bottom.
    for z in np.arange(deck.bow, deck.stern, 0.5):
        y = -SPEC["keelDepth"] * deck.depth_scale(z)
        mesh.box((cx, y - 0.08, z + 0.25), (0.22, 0.16, 0.52), C["wale"])
    # The sternpost and the rudder hung on it.
    tuck = SPEC["sternTuck"]
    mesh.box((cx, (tuck - SPEC["keelDepth"] * 0.72) / 2, deck.stern + 0.05), (0.22, -tuck + SPEC["keelDepth"] * 0.72, 0.2), C["wale"])
    mesh.box((cx, -0.55, deck.stern + 0.32), (0.16, 2.9, 0.5), C["rudder"])
    for y in (-1.6, -0.9, -0.2, 0.5):
        mesh.box((cx, y, deck.stern + 0.15), (0.2, 0.06, 0.32), C["iron"])


def transom_and_poop(deck, mesh):
    cx = deck.centre
    z = deck.stern
    top = deck.bulwark_top(z)
    tuck = SPEC["sternTuck"]
    # The transom: flat, from the tuck up to the poop rail, between the two sides.
    ys = np.linspace(tuck, top, 16)
    for y0, y1 in zip(ys, ys[1:]):
        w0, w1 = section(deck, z, y0), section(deck, z, y1)
        ym = (y0 + y1) / 2
        colour = C["band"] if SPEC["band"][0] <= ym <= SPEC["band"][1] else C["transom"]
        mesh.quad((cx - w0, y0, z), (cx - w1, y1, z), (cx + w1, y1, z), (cx + w0, y0, z), colour)
    # The great cabin's windows, in moulded frames, and the mouldings across the transom.
    n = SPEC["transomWindows"]
    width = section(deck, z, 2.0) * 2
    for i in range(n):
        x = cx - width * 0.36 + i * width * 0.72 / (n - 1)
        mesh.box((x, 2.05, z + 0.03), (0.34, 0.42, 0.04), C["window"])
        for dy in (-0.25, 0.25):
            mesh.box((x, 2.05 + dy, z + 0.05), (0.44, 0.06, 0.05), C["frame"])
        for dx in (-0.2, 0.2):
            mesh.box((x + dx, 2.05, z + 0.05), (0.05, 0.5, 0.05), C["frame"])
    for y in (1.45, 2.6, 0.2):
        w = section(deck, z, y)
        mesh.box((cx, y, z + 0.04), (w * 1.9, 0.08, 0.06), C["frame"] if y != 0.2 else C["red"])
    # Carved rosettes either side of the windows.
    for side in (-1, 1):
        mesh.box((cx + side * width * 0.46, 2.05, z + 0.05), (0.16, 0.16, 0.05), C["frame"])
    # The poop deck, and its front bulkhead facing the quarterdeck with the cabin door and two lights.
    pd = SPEC["poop"]["deck"]
    zs = np.arange(deck.stern_z, z, 0.25)
    for z0 in zs:
        z1 = min(z0 + 0.25, z)
        w0 = section(deck, z0, pd) - SPEC["wallThickness"]
        w1 = section(deck, z1, pd) - SPEC["wallThickness"]
        mesh.quad((cx - w0, pd, z0), (cx - w1, pd, z1), (cx + w1, pd, z1), (cx + w0, pd, z0), C["deck"])
    qd = deck.height_at(cx, deck.stern_z - 0.2)
    w = section(deck, deck.stern_z, pd) - SPEC["wallThickness"]
    mesh.box((cx, (qd + pd) / 2, deck.stern_z), (w * 2, pd - qd, 0.08), C["bulkhead"])
    mesh.box((cx, qd + 0.5, deck.stern_z - 0.05), (0.62, 0.95, 0.04), C["wale"])
    for side in (-1, 1):
        mesh.box((cx + side * w * 0.6, qd + 0.7, deck.stern_z - 0.05), (0.36, 0.3, 0.04), C["window"])
    # The poop's own rail, inside the sides.
    for side in (-1, 1):
        for z0 in np.arange(deck.stern_z + 0.2, z, 0.5):
            x = cx + side * (section(deck, z0, top - 0.3) - 0.12)
            mesh.box((x, pd + 0.25, z0), (0.07, 0.5, 0.07), C["wale"])
    # Inside the poop's sides, above its deck, and the rail cap round it.
    inner = C["plank"] * np.array([1.25, 1.25, 1.25, 1])
    for z0 in np.arange(deck.stern_z, z, 0.25):
        z1 = min(z0 + 0.25, z)
        for side in (-1, 1):
            def q(y, zz):
                return (cx + side * (section(deck, zz, y) - SPEC["wallThickness"]), y, zz)
            mesh.quad(q(pd, z0), q(pd, z1), q(deck.bulwark_top(z1), z1), q(deck.bulwark_top(z0), z0), inner)
            ta, tb = deck.bulwark_top(z0), deck.bulwark_top(z1)
            mesh.quad(q(ta, z0), q(tb, z1), (cx + side * section(deck, z1, tb), tb, z1), (cx + side * section(deck, z0, ta), ta, z0), C["wale"])
    mesh.box((cx, top + 0.02, z - 0.04), (section(deck, z, top) * 2, 0.06, 0.1), C["wale"])


def forecastle(deck, mesh):
    # Its deck ahead of the grid (the grid's forecastle runs from row 0).
    cx = deck.centre
    h = deck.height_at(cx, 1)
    for z0 in np.arange(deck.bow + 0.3, -0.5, 0.25):
        z1 = min(z0 + 0.25, -0.5)
        w0 = max(section(deck, z0, h) - SPEC["wallThickness"], 0.02)
        w1 = max(section(deck, z1, h) - SPEC["wallThickness"], 0.02)
        mesh.quad((cx - w0, h, z0), (cx - w1, h, z1), (cx + w1, h, z1), (cx + w0, h, z0), C["deck"])


def gunports(deck, mesh):
    cx = deck.centre
    for z in SPEC["gunports"]:
        for side in (-1, 1):
            x = cx + side * (section(deck, z, 0.32) + 0.02)
            mesh.box((x, 0.32, z), (0.05, 0.42, 0.5), C["lid"])
            mesh.box((x + side * 0.02, 0.32, z), (0.03, 0.46, 0.06), C["iron"])


def spar(mesh, a, b, r0, r1, colour, sides=8):
    """A tapered spar from a (radius r0) to b (radius r1), octagonal like a dressed mast."""
    a, b = np.array(a, float), np.array(b, float)
    axis = b - a
    length = np.linalg.norm(axis)
    if length < 1e-6:
        return
    axis /= length
    other = np.array([1.0, 0, 0]) if abs(axis[0]) < 0.9 else np.array([0, 0, 1.0])
    u = np.cross(axis, other)
    u /= np.linalg.norm(u)
    v = np.cross(axis, u)
    ring = [u * math.cos(k * 2 * math.pi / sides) + v * math.sin(k * 2 * math.pi / sides) for k in range(sides)]
    for k in range(sides):
        q0, q1 = ring[k], ring[(k + 1) % sides]
        mesh.quad(tuple(a + q0 * r0), tuple(b + q0 * r1), tuple(b + q1 * r1), tuple(a + q1 * r0), colour)
    for k in range(1, sides - 1):
        mesh.tri(tuple(b + ring[0] * r1), tuple(b + ring[k] * r1), tuple(b + ring[k + 1] * r1), colour)


def disc(mesh, centre, radius, thickness, colour, sides=14):
    cx, cy, cz = centre
    for k in range(sides):
        a0, a1 = k * 2 * math.pi / sides, (k + 1) * 2 * math.pi / sides
        p0 = (cx + math.cos(a0) * radius, cy, cz + math.sin(a0) * radius)
        p1 = (cx + math.cos(a1) * radius, cy, cz + math.sin(a1) * radius)
        mesh.quad(p0, p1, (p1[0], cy + thickness, p1[2]), (p0[0], cy + thickness, p0[2]), colour)
        mesh.tri((cx, cy + thickness, cz), (p1[0], cy + thickness, p1[2]), (p0[0], cy + thickness, p0[2]), colour)
        mesh.tri((cx, cy, cz), (p0[0], cy, p0[2]), (p1[0], cy, p1[2]), colour)


def masts(deck, mesh):
    """The fore and main, square-rigged, and the lateen mizzen, with their tops, topmast, lateen yard and the
    bowsprit. Where they stand and how high the yards hang are ShipView's rule (it hangs the sails and
    rigging from them): the mast cells of the deck plan; the main 12 m, the fore 9.5 m."""
    spar_c, iron, rope = C["mast"], C["iron"], C["woolding"]
    cells = [(x, y) for y, row in enumerate(deck.rows) for x, ch in enumerate(row)
             if ch == "#" and 0 < x < len(row) - 1 and 0 < y < len(deck.rows) - 1
             and all(deck.rows[y + dy][x + dx] == "." for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    cells.sort(key=lambda c: c[1])
    square = cells[:-1] if len(cells) >= 3 else cells
    for i, (x, y) in enumerate(square):
        main = i == min(1, len(square) - 1)
        height = 12.0 if main else 9.5
        foot = np.array([x, deck.height_at(x, y), y], float)
        beam = deck.half_width(y)
        r = 0.22 if main else 0.18
        spar(mesh, foot - [0, 0.3, 0], foot + [0, height * 0.74, 0], r, r * 0.78, spar_c)
        # Wooldings: rope bands round the lower mast.
        for t in np.arange(0.12, 0.62, 0.09):
            disc(mesh, tuple(foot + [0, height * t, 0]), r * 0.97 + 0.02, 0.07, rope, 10)
        # The top: a round platform with a low rail on the crosstrees, and the cap above it.
        top = foot + [0, height * 0.72, 0]
        size = 0.7 if main else 0.55
        spar(mesh, top + [-size, -0.08, 0], top + [size, -0.08, 0], 0.06, 0.06, spar_c, 6)
        spar(mesh, top + [0, -0.08, -size * 0.8], top + [0, -0.08, size * 0.8], 0.06, 0.06, spar_c, 6)
        disc(mesh, tuple(top), size, 0.08, spar_c)
        for k in range(12):
            ang = k * math.pi / 6
            post = top + [math.cos(ang) * size * 0.95, 0, math.sin(ang) * size * 0.95]
            spar(mesh, post, post + [0, 0.38, 0], 0.025, 0.025, spar_c, 5)
        disc(mesh, tuple(top + [0, 0.38, 0]), size * 0.98, 0.04, spar_c)
        mesh.box(tuple(top + [0, 0.62, 0]), (r * 2.6, 0.22, r * 3.4), spar_c)
        # The topmast above the cap, to the masthead.
        spar(mesh, top + [0, -0.4, -r * 0.9], foot + [0, height + 0.15, -r * 0.9 * 0.3], r * 0.62, r * 0.38, spar_c)
        mesh.box(tuple(foot + [0, height + 0.18, 0]), (0.16, 0.1, 0.16), spar_c)
        # (The yards are ShipView's: they are braced round to the wind, and the main topsail yard can be
        # sprung in a blow.)
    if len(cells) >= 3:
        x, y = cells[-1]
        foot = np.array([x, deck.height_at(x, y), y], float)
        spar(mesh, foot - [0, 0.2, 0], foot + [0, 6.5, 0], 0.14, 0.09, spar_c)
        for t in (0.15, 0.3, 0.45):
            disc(mesh, tuple(foot + [0, 6.5 * t, 0]), 0.15, 0.06, rope, 10)
        # The lateen yard, in two pieces fished together as they were.
        low, high = foot + [0, 1.4, 2.2], foot + [0, 7.6, -3.2]
        mid = (low + high) / 2
        spar(mesh, low, mid, 0.05, 0.08, spar_c)
        spar(mesh, mid, high, 0.08, 0.035, spar_c)
        mesh.box(tuple(foot + [0, 6.2, 0.05]), (0.22, 0.24, 0.22), C["block"])
    # The bowsprit, from the forecastle out over the stem, with its gammoning.
    cx = deck.centre
    a, b = np.array([cx, 1.7, 0.8]), np.array([cx, 3.2, deck.bow - 4.5])
    spar(mesh, a, b, 0.14, 0.07, spar_c)
    for t in (0.45, 0.52):
        p = a + (b - a) * t
        disc(mesh, tuple(p - [0, 0.05, 0]), 0.17, 0.1, rope, 10)


def write_glb(mesh, path):
    p = np.array(mesh.p, np.float32)
    c = np.array(mesh.c, np.float32)
    idx = np.array(mesh.i, np.uint32)
    # Flat normals, per triangle: the planks read as planks.
    n = np.zeros_like(p)
    tri = idx.reshape(-1, 3)
    fn = np.cross(p[tri[:, 1]] - p[tri[:, 0]], p[tri[:, 2]] - p[tri[:, 0]])
    fn /= np.maximum(np.linalg.norm(fn, axis=1, keepdims=True), 1e-9)
    for k in range(3):
        n[tri[:, k]] = fn
    blobs = [p.tobytes(), n.tobytes(), c.tobytes(), idx.tobytes()]
    offsets, data = [], b""
    for blob in blobs:
        offsets.append(len(data))
        data += blob + b"\0" * ((4 - len(blob) % 4) % 4)
    views = [{"buffer": 0, "byteOffset": o, "byteLength": len(b), **({"target": 34962} if i < 3 else {"target": 34963})}
             for i, (o, b) in enumerate(zip(offsets, blobs))]
    accessors = [
        {"bufferView": 0, "componentType": 5126, "count": len(p), "type": "VEC3", "min": p.min(0).tolist(), "max": p.max(0).tolist()},
        {"bufferView": 1, "componentType": 5126, "count": len(p), "type": "VEC3"},
        {"bufferView": 2, "componentType": 5126, "count": len(p), "type": "VEC4"},
        {"bufferView": 3, "componentType": 5125, "count": len(idx), "type": "SCALAR"},
    ]
    gltf = {
        "asset": {"version": "2.0", "generator": "tools/ship/build_ship.py"},
        "scene": 0, "scenes": [{"nodes": [0]}],
        "nodes": [{"name": "Hull", "mesh": 0}],
        "meshes": [{"name": "Hull", "primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1, "COLOR_0": 2}, "indices": 3}]}],
        "buffers": [{"byteLength": len(data)}], "bufferViews": views, "accessors": accessors,
        "extras": {"triangles": int(len(idx) // 3), "spec": "tools/ship/garza.json"},
    }
    js = json.dumps(gltf).encode()
    js += b" " * ((4 - len(js) % 4) % 4)
    out = struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(data))
    out += struct.pack("<II", len(js), 0x4E4F534A) + js + struct.pack("<II", len(data), 0x004E4942) + data
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(out)


def main():
    deck = Deck(ROOT / SPEC["deck"])
    mesh = Mesh()
    hull(deck, mesh)
    stem_and_stern(deck, mesh)
    transom_and_poop(deck, mesh)
    forecastle(deck, mesh)
    gunports(deck, mesh)
    masts(deck, mesh)
    write_glb(mesh, OUT)
    print(f"{OUT.relative_to(ROOT)}: {len(mesh.i) // 3} triangles")


if __name__ == "__main__":
    main()
