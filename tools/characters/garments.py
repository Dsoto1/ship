"""Clothing made on the person's own body, so it fits every build and moves with the shared rig.

A garment is a shell: the part of the (heavily smoothed) body it covers, pushed out along the
surface by the cloth's thickness and its looseness, with clean hems (the hem vertices are moved
onto the cut). It keeps the body's skin weights, so it bends where the body bends. A few pieces that
do not follow the body (a skirt, a cap's crown, a hat brim, a pouch) are built as simple solids and
skinned to the bones they hang from. Garments are listed outermost last, and each body or inner
triangle that an outer layer fully covers is removed: hidden geometry costs triangles (§49).

In the T-pose the arms lie along x and the legs along y, so a hem is a value of |x| on an arm, of y
on a leg or the torso; that is how garments name their extents.
"""
import numpy as np

import body as bodylib

# Material kinds, the shader's second UV channel x (character.gdshader).
SKIN, HAIR, EYE, LINEN, WOOL, LEATHER, FELT, METAL, WOOD, STRAW, VELVET = range(11)
KINDS = {"skin": SKIN, "hair": HAIR, "eye": EYE, "linen": LINEN, "wool": WOOL, "leather": LEATHER,
         "felt": FELT, "metal": METAL, "wood": WOOD, "straw": STRAW, "velvet": VELVET}


class Base:
    """The smoothed body the garments are cut from, with what each vertex belongs to."""

    def __init__(self, positions, faces, normals, joints, weights, region, shares, ids):
        self.positions = positions
        self.faces = faces
        self.normals = normals
        self.joints = joints
        self.weights = weights
        self.region = region
        self.shares = shares
        self.ids = ids
        arm = np.isin(region, ["arm_l", "arm_r", "hand_l", "hand_r"])
        leg = np.isin(region, ["leg_l", "leg_r", "foot_l", "foot_r"])
        self.arm = arm
        self.leg = leg
        self.reach = np.abs(positions[:, 0])  # along the arm
        self.height = positions[:, 1]
        # Where a sleeveless garment's armhole is cut: in from the shoulder joint (set by build.py).
        self.armhole = 0.19


def covers(base, spec):
    """Which vertices of the base a garment covers, and the value each hem is measured in."""
    y = base.height
    x = base.reach
    r = base.region
    mask = np.zeros(len(y), bool)
    torso = spec.get("torso")
    if torso:
        low, high = torso
        inside = np.isin(r, ["torso", "neck"]) & (y >= low) & (y <= high)
        collar = spec.get("collar")
        if collar is not None:
            # A neckline lower at the front than at the back: a round, gathered neck.
            inside &= y <= neckline(base.positions[:, 2], collar)
        mask |= inside
    sleeve = spec.get("sleeve")
    if sleeve is not None:
        mask |= base.arm & (x <= sleeve) & ~np.isin(r, ["hand_l", "hand_r"]) | (np.isin(r, ["hand_l", "hand_r"]) & (x <= sleeve))
        if torso:
            mask |= np.isin(r, ["torso"]) & (x > 0.12) & (y >= torso[0]) & (y <= torso[1] + 0.06)
    if torso and sleeve is None:
        # A clean armhole, not the ragged line where the body's weights pass from torso to arm.
        mask &= ~(base.reach > base.armhole)
    if torso and spec.get("collar") is not None:
        mask &= ~(np.isin(r, ["torso", "neck"]) & (y > neckline(base.positions[:, 2], spec["collar"])))
    leg = spec.get("leg")
    if leg:
        low, high = leg
        mask |= base.leg & (y >= low) & (y <= high)
    if spec.get("feet"):
        mask |= np.isin(r, ["foot_l", "foot_r"]) | (base.leg & (y <= spec["feet"]))
    head = spec.get("head")
    if head is not None:
        # Above a plane tilted down at the back: a cap sits low on the nape and high on the brow.
        front, back = head
        z = base.positions[:, 2]
        t = np.clip((z + 0.1) / 0.2, 0, 1)
        cut = back + (front - back) * t
        mask |= np.isin(r, ["head"]) & (y >= cut)
    front = spec.get("front")
    if front is not None:
        z = base.positions[:, 2]
        mask &= z > front
    width = spec.get("width")
    if width is not None:
        mask &= base.reach <= width
    return mask


def _open_edge_vertices(faces, ids):
    edges = np.sort(np.concatenate([faces[:, [0, 1]], faces[:, [1, 2]], faces[:, [2, 0]]]), axis=1)
    welded = np.sort(ids[edges], axis=1)
    _, inverse, counts = np.unique(welded, axis=0, return_inverse=True, return_counts=True)
    mask = np.zeros(faces.max() + 1 if len(faces) else 0, bool)
    mask[edges[counts[inverse.ravel()] == 1].ravel()] = True
    return mask


def neckline(z, collar):
    front, back = collar
    t = np.clip((z + 0.09) / 0.17, 0, 1)
    return back + (front - back) * t


def shell(base, spec, colour, kind, name):
    """A garment shell: faces of the base all of whose corners the garment covers, pushed out."""
    mask = covers(base, spec)
    keep = mask[base.faces].all(1)
    faces = base.faces[keep]
    if not len(faces):
        return None, mask
    used = np.unique(faces)
    remap = -np.ones(len(base.positions), np.int64)
    remap[used] = np.arange(len(used))
    p = base.positions[used].copy()
    n = base.normals[used]
    ids = base.ids[used]
    y = p[:, 1]
    x = np.abs(p[:, 0])

    offset = np.full(len(p), spec.get("thickness", 0.006))
    loose = spec.get("loose", 0.0)
    if loose:
        if spec.get("leg"):
            low, high = spec["leg"]
            t = np.clip((y - low) / max(high - low, 1e-6), 0, 1)
            offset += loose * np.sin(t * np.pi) ** 0.7 * base.leg[used]
        if spec.get("sleeve") is not None:
            t = np.clip((x - 0.2) / max(spec["sleeve"] - 0.2, 1e-6), 0, 1)
            offset += loose * np.sin(np.clip(t * 1.1, 0, 1) * np.pi * 0.8) * base.arm[used]
        if spec.get("torso"):
            low, high = spec["torso"]
            t = np.clip((y - low) / max(high - low, 1e-6), 0, 1)
            offset += loose * 0.6 * np.sin(t * np.pi) * np.isin(base.region[used], ["torso"])
    # Hems: a rolled cuff or a band is thicker over its last few centimetres.
    band = spec.get("band", 0.0)
    if band:
        width = spec.get("bandWidth", 0.035)
        near = np.zeros(len(p))
        if spec.get("sleeve") is not None:
            near = np.maximum(near, base.arm[used] * np.clip(1 - (spec["sleeve"] - x) / width, 0, 1))
        if spec.get("leg"):
            near = np.maximum(near, base.leg[used] * np.clip(1 - (y - spec["leg"][0]) / width, 0, 1))
        if spec.get("torso") and spec.get("bandTorso"):
            low = spec["torso"][0]
            near = np.maximum(near, np.isin(base.region[used], ["torso"]) * np.clip(1 - (y - low) / width, 0, 1))
        if spec.get("head") is not None:
            front, back = spec["head"]
            t = np.clip((base.positions[used][:, 2] + 0.1) / 0.2, 0, 1)
            rim = back + (front - back) * t
            near = np.maximum(near, np.clip(1 - (y - rim) / width, 0, 1))
        offset += band * (near > 0.01)
    p += n * offset[:, None]
    drape = spec.get("drape", 0.0)
    if drape:
        before = p.copy()
        region = base.region[used].copy()
        if spec.get("collar") is not None:
            # Leave the neckline alone: it is gathered to the neck below.
            rim = neckline(base.positions[used][:, 2], spec["collar"])
            region[(y > rim - 0.05) & np.isin(region, ["torso", "neck"])] = "collar"
        p = hang(p, region, drape)
        p = fall(p, region, drape, spec.get("collar"))
        # The open edges (armholes, hems) stay where they were cut, and the cloth eases out from
        # them over a couple of rings, so a sleeveless garment does not throw points at the shoulder.
        edge = _open_edge_vertices(remap[faces], ids)
        hold = edge.astype(float)
        f = remap[faces]
        for _ in range(3):
            near = np.zeros(len(p))
            np.maximum.at(near, f[:, 0], np.maximum(hold[f[:, 1]], hold[f[:, 2]]))
            np.maximum.at(near, f[:, 1], np.maximum(hold[f[:, 0]], hold[f[:, 2]]))
            np.maximum.at(near, f[:, 2], np.maximum(hold[f[:, 0]], hold[f[:, 1]]))
            hold = np.maximum(hold, near * 0.6)
        p = before + (p - before) * (1 - hold)[:, None]
    if spec.get("collar") is not None:
        # Gathered at the neck: within a few centimetres of the neckline the cloth comes in to the skin.
        rim = neckline(base.positions[used][:, 2], spec["collar"])
        close = np.clip(1 - (rim - y) / 0.05, 0, 1) * ~base.arm[used]
        p -= n * (offset * close * 0.75)[:, None]

    part_faces = remap[faces]
    # Clean hems: move each open-edge vertex onto its cut.
    edges = np.sort(np.concatenate([part_faces[:, [0, 1]], part_faces[:, [1, 2]], part_faces[:, [2, 0]]]), axis=1)
    welded_edges = np.sort(ids[edges], axis=1)
    unique, inverse, counts = np.unique(welded_edges, axis=0, return_inverse=True, return_counts=True)
    open_edges = edges[(counts[inverse.ravel()] == 1)]
    hem = np.zeros(len(p), bool)
    hem[open_edges.ravel()] = True
    arm = base.arm[used]
    leg = base.leg[used]
    if spec.get("sleeve") is not None:
        snap = hem & arm & (x > spec["sleeve"] - 0.05)
        p[snap, 0] = np.sign(p[snap, 0]) * spec["sleeve"]
    if spec.get("leg"):
        low, high = spec["leg"]
        snap = hem & leg & (y < low + 0.05)
        p[snap, 1] = low
        if not spec.get("torso"):
            snap = hem & leg & (y > high - 0.05)
            p[snap, 1] = high
    if spec.get("torso"):
        low, high = spec["torso"]
        torso = ~arm & ~leg
        if not spec.get("leg"):
            snap = hem & torso & (y < low + 0.04)
            p[snap, 1] = low
    if spec.get("collar") is not None:
        # The neckline only: armhole and hem edges are not pulled up to it.
        snap = hem & ~arm & ~leg & (y > spec["torso"][0] + 0.2) & (x < 0.11)
        p[snap, 1] = neckline(base.positions[used][:, 2], spec["collar"])[snap]
    if spec.get("head") is not None:
        # A cap's edge runs level round the brow and dips to the nape.
        front, back = spec["head"]
        t = np.clip((base.positions[used][:, 2] + 0.1) / 0.2, 0, 1)
        snap = hem  # a cap has one edge, its brim
        p[snap, 1] = (back + (front - back) * t)[snap] - spec.get("thickness", 0.006) * 0.5
    normals = bodylib.vertex_normals(p, part_faces, ids)
    part = bodylib.Part(name, p, normals, cylinder_uv(p), base.joints[used], base.weights[used], part_faces, kind, colour)
    part.locked = hem
    return part, mask


def hang(p, region, drape):
    """Cloth bridges hollows instead of following them: each slice of the torso (by height) and of
    each sleeve (by reach) is pushed out towards its own convex outline, by `drape` (0..1). A shirt
    then hangs from the chest and shoulder blades as linen does, and no muscle shows through it."""
    p = p.copy()
    groups = [(np.isin(region, ["torso", "neck"]), 1, (0, 2)),
              (np.isin(region, ["arm_l"]), 0, (1, 2)), (np.isin(region, ["arm_r"]), 0, (1, 2))]
    for mask, axis, plane in groups:
        if mask.sum() < 8:
            continue
        idx = np.flatnonzero(mask)
        key = np.round(np.abs(p[idx, axis]) / 0.012).astype(int)
        for k in np.unique(key):
            ring = idx[key == k]
            if len(ring) < 5:
                continue
            pts = p[ring][:, plane]
            centre = pts.mean(0)
            d = pts - centre
            r = np.linalg.norm(d, axis=1)
            angle = np.arctan2(d[:, 1], d[:, 0])
            hull_r = _hull_radius(pts - centre, angle)
            grow = np.maximum(hull_r - r, 0) * drape
            unit = d / np.maximum(r, 1e-9)[:, None]
            moved = pts + unit * grow[:, None]
            p[ring[:, None], np.array(plane)[None, :]] = moved
    return p


def fall(p, region, drape, collar=None):
    """Down the front and back the cloth falls from the chest and the shoulder blades rather than
    following the body in under them: going down each column of the torso, the cloth's distance from
    the body's axis can only shrink slowly (a shirt bloused over a belt)."""
    p = p.copy()
    torso = np.flatnonzero(np.isin(region, ["torso"]) & (p[:, 1] < 1.45) & (p[:, 1] > 0.9))
    if len(torso) < 10:
        return p
    centre = np.array([0.0, p[torso][:, 2].mean()])
    d = p[torso][:, [0, 2]] - centre
    angle = np.arctan2(d[:, 0], d[:, 1])
    r = np.linalg.norm(d, axis=1)
    column = np.round(angle / np.radians(9)).astype(int)
    for c in np.unique(column):
        members = np.flatnonzero(column == c)
        order = members[np.argsort(-p[torso[members], 1])]
        best = -1.0
        last_y = None
        for m in order:
            y = p[torso[m], 1]
            if last_y is not None:
                best -= (last_y - y) * 0.12
            last_y = y
            if r[m] < best:
                grow = (best - r[m]) * drape
                unit = d[m] / max(r[m], 1e-9)
                p[torso[m], 0] += unit[0] * grow
                p[torso[m], 2] += unit[1] * grow
            else:
                best = r[m]
    return p


def _hull_radius(points, angles):
    """Distance from the origin to the convex outline of points, along each angle."""
    from math import atan2
    pts = sorted(map(tuple, points))
    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for q in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], q) <= 0:
            lower.pop()
        lower.append(q)
    for q in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], q) <= 0:
            upper.pop()
        upper.append(q)
    hull = np.array(lower[:-1] + upper[:-1])
    if len(hull) < 3:
        return np.linalg.norm(points, axis=1)
    result = np.zeros(len(angles))
    a = hull
    b = np.roll(hull, -1, axis=0)
    for i, t in enumerate(angles):
        ray = np.array([np.cos(t), np.sin(t)])
        best = 0.0
        e = b - a
        denom = ray[0] * e[:, 1] - ray[1] * e[:, 0]
        ok = np.abs(denom) > 1e-12
        s_ = np.where(ok, (a[:, 0] * e[:, 1] - a[:, 1] * e[:, 0]) / np.where(ok, denom, 1), -1)
        u = np.where(ok, (a[:, 0] * ray[1] - a[:, 1] * ray[0]) / np.where(ok, denom, 1), -1)
        hit = ok & (s_ > 0) & (u >= -1e-6) & (u <= 1 + 1e-6)
        if hit.any():
            best = s_[hit].max()
        result[i] = best
    return result


def cylinder_uv(p):
    """Metres around and along the body, for the shader's weave and wear (cloth has no texture)."""
    angle = np.arctan2(p[:, 0], p[:, 2])
    return np.stack([angle * 0.15 + p[:, 0] * 0.0, p[:, 1]], axis=1)


def skirt(skeleton, base, top, bottom, flare, colour, kind, name, thickness=0.014, segments=28, arc=None):
    """A skirt (a sayo's or a coat's below the belt) as a flared tube hung from the pelvis; with an
    arc (degrees either side of the front) it is an open panel instead: a waist apron.

    Its radius at each angle follows the body's own section at the waist, so it sits on any build;
    the lower edge is weighted towards the thighs so it moves with the stride.
    """
    p = base.positions
    ring = (np.abs(p[:, 1] - top) < 0.02) & np.isin(base.region, ["torso"])
    centre = p[ring].mean(0) if ring.any() else np.array([0, top, 0])
    closed = arc is None
    if closed:
        angles = np.linspace(0, 2 * np.pi, segments, endpoint=False)
    else:
        segments = max(6, segments // 2)
        angles = np.linspace(-np.radians(arc), np.radians(arc), segments)
    radius = []
    for a in angles:
        direction = np.array([np.sin(a), 0, np.cos(a)])
        d = (p[ring] - centre) @ direction
        radius.append(max(d.max() if ring.any() else 0.15, 0.05) + thickness)
    radius = np.array(radius)
    rows = 5
    positions, joints, weights = [], [], []
    names = skeleton.names
    pelvis = names.index("pelvis")
    thigh_l = names.index("thigh_l")
    thigh_r = names.index("thigh_r")
    for row in range(rows):
        t = row / (rows - 1)
        y = top + (bottom - top) * t
        for i, a in enumerate(angles):
            r = radius[i] * (1 + flare * t * t) + 0.01 * t
            positions.append([centre[0] + np.sin(a) * r, y, centre[2] + np.cos(a) * r])
            side = np.sin(a)
            leg_share = 0.65 * t
            left = leg_share * np.clip(0.5 + side * 0.9, 0, 1)
            right = leg_share - left
            joints.append([pelvis, thigh_l, thigh_r, 0])
            weights.append([1 - leg_share, left, right, 0])
    faces = []
    for row in range(rows - 1):
        for i in range(segments if closed else segments - 1):
            a = row * segments + i
            b = row * segments + (i + 1) % segments
            c = a + segments
            d = b + segments
            faces += [[a, c, b], [b, c, d]]
    positions = np.array(positions)
    faces = np.array(faces)
    normals = bodylib.vertex_normals(positions, faces)
    part = bodylib.Part(name, positions, normals, cylinder_uv(positions), joints, weights, faces, kind, colour)
    part.locked[:] = False
    part.locked[:segments] = True
    part.locked[-segments:] = True
    if not closed:
        part.locked[::segments] = True
        part.locked[segments - 1::segments] = True
        # One sheet: draw it from both sides by doubling it, facing in.
        back = part.faces[:, ::-1] + 0
        inner = bodylib.Part(name, positions - normals * 0.004, -normals, part.uv, part.joints, part.weights, back, kind, colour)
        inner.locked = part.locked.copy()
        count = len(positions)
        merged = bodylib.Part(name, np.concatenate([positions, inner.positions]), np.concatenate([normals, inner.normals]),
                              np.concatenate([part.uv, inner.uv]), np.concatenate([part.joints, inner.joints]),
                              np.concatenate([part.weights, inner.weights]), np.concatenate([part.faces, back + count]),
                              kind, np.concatenate([part.colour, inner.colour]))
        merged.locked = np.concatenate([part.locked, inner.locked])
        return merged
    return part


def rigid(skeleton, bone, positions, faces, colour, kind, name):
    """A solid piece moving with one bone: a hat brim, a pouch, a tool."""
    positions = np.asarray(positions, dtype=np.float64)
    faces = np.asarray(faces, dtype=np.int64)
    count = len(positions)
    joints = np.tile([skeleton.names.index(bone), 0, 0, 0], (count, 1))
    weights = np.tile([1.0, 0, 0, 0], (count, 1))
    normals = bodylib.vertex_normals(positions, faces, np.arange(count))
    return bodylib.Part(name, positions, normals, cylinder_uv(positions), joints, weights, faces, kind, colour)


def disc(centre, radius_x, radius_z, thickness, segments=20, droop=0.0, inner=0.0):
    """A flat ring (a hat brim) or disc: radius_x/z at the outer edge, an inner hole if inner > 0."""
    angles = np.linspace(0, 2 * np.pi, segments, endpoint=False)
    rings = [inner, 1.0] if inner > 0 else [0.0, 1.0]
    positions = []
    for k, r in enumerate(rings):
        for a in angles:
            dy = -droop * r * r
            positions.append([centre[0] + np.sin(a) * radius_x * r, centre[1] + dy + thickness / 2, centre[2] + np.cos(a) * radius_z * r])
        for a in angles:
            dy = -droop * r * r
            positions.append([centre[0] + np.sin(a) * radius_x * r, centre[1] + dy - thickness / 2, centre[2] + np.cos(a) * radius_z * r])
    faces = []
    s = segments
    top_in, bot_in, top_out, bot_out = 0, s, 2 * s, 3 * s
    for i in range(s):
        j = (i + 1) % s
        faces += [[top_in + i, top_out + i, top_in + j], [top_in + j, top_out + i, top_out + j]]
        faces += [[bot_in + i, bot_in + j, bot_out + i], [bot_in + j, bot_out + j, bot_out + i]]
        faces += [[top_out + i, bot_out + i, top_out + j], [top_out + j, bot_out + i, bot_out + j]]
        if inner > 0:
            faces += [[top_in + i, top_in + j, bot_in + i], [top_in + j, bot_in + j, bot_in + i]]
    return np.array(positions), np.array(faces)


def box(centre, size, rotation_y=0.0):
    cx, cy, cz = centre
    sx, sy, sz = np.asarray(size) / 2
    corners = np.array([[x, y, z] for x in (-sx, sx) for y in (-sy, sy) for z in (-sz, sz)])
    c, s = np.cos(rotation_y), np.sin(rotation_y)
    corners = corners @ np.array([[c, 0, -s], [0, 1, 0], [s, 0, c]])
    positions = corners + [cx, cy, cz]
    faces = np.array([[0, 1, 3], [0, 3, 2], [4, 6, 7], [4, 7, 5], [0, 4, 5], [0, 5, 1],
                      [2, 3, 7], [2, 7, 6], [0, 2, 6], [0, 6, 4], [1, 5, 7], [1, 7, 3]])
    # Split corners so each face has its own (flat) normals.
    positions = positions[faces.ravel()]
    return positions, np.arange(len(positions)).reshape(-1, 3)


def cylinder(start, end, radius, segments=8, end_radius=None):
    start, end = np.asarray(start, float), np.asarray(end, float)
    axis = end - start
    length = np.linalg.norm(axis)
    axis /= length
    helper = np.array([1.0, 0, 0]) if abs(axis[0]) < 0.9 else np.array([0, 1.0, 0])
    u = np.cross(axis, helper)
    u /= np.linalg.norm(u)
    v = np.cross(axis, u)
    end_radius = radius if end_radius is None else end_radius
    angles = np.linspace(0, 2 * np.pi, segments, endpoint=False)
    ring0 = [start + (u * np.cos(a) + v * np.sin(a)) * radius for a in angles]
    ring1 = [end + (u * np.cos(a) + v * np.sin(a)) * end_radius for a in angles]
    positions = np.array(ring0 + ring1 + [start, end])
    faces = []
    s = segments
    for i in range(s):
        j = (i + 1) % s
        faces += [[i, j, s + i], [j, s + j, s + i], [2 * s, j, i], [2 * s + 1, s + i, s + j]]
    return positions, np.array(faces)


def merge(*pieces):
    positions, faces = [], []
    count = 0
    for p, f in pieces:
        positions.append(p)
        faces.append(np.asarray(f) + count)
        count += len(p)
    return np.concatenate(positions), np.concatenate(faces)
