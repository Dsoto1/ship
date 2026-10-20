"""The base body, its regions, and the changes that make one person's body and face from it.

The Quaternius base characters are sculpted as athletes ("Superhero" in the pack's own name). For
ordinary working people of 1519 (CHARACTER_ART §4–5) the build first relaxes the muscle definition
with Taubin smoothing (which, unlike plain averaging, does not shrink the body), narrows the heroic
taper of the torso, and then shapes the body from the identity record: slight, average, stocky or
heavy; a belly; and the face from a small set of named, bounded parameters (§17) applied as smooth
displacement fields around landmarks found on the head. The skin weights are untouched, so the
shared rig and animations still drive every body.
"""
import numpy as np

import gltf

GROUPS = {
    "head": ["Head"],
    "neck": ["neck_01"],
    "torso": ["pelvis", "spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r", "root"],
    "arm_l": ["upperarm_l", "lowerarm_l"],
    "arm_r": ["upperarm_r", "lowerarm_r"],
    "hand_l": ["hand_l"],
    "hand_r": ["hand_r"],
    "leg_l": ["thigh_l", "calf_l"],
    "leg_r": ["thigh_r", "calf_r"],
    "foot_l": ["foot_l", "ball_l", "ball_leaf_l"],
    "foot_r": ["foot_r", "ball_r", "ball_leaf_r"],
}


class Part:
    """One mesh with every attribute the character needs, all as numpy arrays."""

    def __init__(self, name, positions, normals, uv, joints, weights, faces, kind, colour, uv_region=None):
        self.name = name
        self.positions = np.asarray(positions, dtype=np.float64)
        self.normals = np.asarray(normals, dtype=np.float64)
        self.uv = np.asarray(uv, dtype=np.float64)
        self.joints = np.asarray(joints, dtype=np.int64)
        self.weights = np.asarray(weights, dtype=np.float64)
        self.faces = np.asarray(faces, dtype=np.int64)
        count = len(self.positions)
        self.kind = np.full(count, kind, dtype=np.float64) if np.isscalar(kind) else np.asarray(kind, dtype=np.float64)
        colour = np.asarray(colour, dtype=np.float64)
        self.colour = np.tile(colour, (count, 1)) if colour.ndim == 1 else colour
        self.uv_region = uv_region
        self.importance = np.ones(count)
        self.locked = np.zeros(count, bool)

    def subset_faces(self, faces):
        """This part with other faces over the same vertices (as a reduction returns them)."""
        part = Part(self.name, self.positions, self.normals, self.uv, self.joints, self.weights, faces, self.kind,
                    self.colour, self.uv_region)
        part.importance = self.importance
        part.locked = self.locked
        return part.subset(np.ones(len(faces), bool))

    def subset(self, keep_faces):
        faces = self.faces[keep_faces]
        used = np.unique(faces)
        remap = -np.ones(len(self.positions), np.int64)
        remap[used] = np.arange(len(used))
        part = Part(self.name, self.positions[used], self.normals[used], self.uv[used], self.joints[used],
                    self.weights[used], remap[faces], self.kind[used], self.colour[used], self.uv_region)
        part.importance = self.importance[used]
        part.locked = self.locked[used]
        return part


def weld(positions, tolerance=1e-5):
    """Maps each vertex to one id per distinct position, so seams do not split smoothing or normals."""
    keys = np.round(np.asarray(positions) / tolerance).astype(np.int64)
    _, ids = np.unique(keys, axis=0, return_inverse=True)
    return ids.ravel()


def neighbours(ids, faces):
    count = ids.max() + 1
    f = ids[faces]
    pairs = np.concatenate([f[:, [0, 1]], f[:, [1, 2]], f[:, [2, 0]]])
    pairs = np.concatenate([pairs, pairs[:, ::-1]])
    pairs = np.unique(pairs, axis=0)
    pairs = pairs[pairs[:, 0] != pairs[:, 1]]
    return pairs, count


def smooth(positions, faces, amount, iterations=10, ids=None):
    """Taubin smoothing (alternating shrink and inflate) weighted per vertex by amount (0..1)."""
    ids = weld(positions) if ids is None else ids
    pairs, count = neighbours(ids, faces)
    welded = np.zeros((count, 3))
    np.add.at(welded, ids, positions)
    welded /= np.bincount(ids, minlength=count)[:, None]
    strength = np.zeros(count)
    np.maximum.at(strength, ids, amount)
    degree = np.bincount(pairs[:, 0], minlength=count).astype(np.float64)
    for _ in range(iterations):
        for factor in (0.55, -0.58):
            mean = np.zeros((count, 3))
            np.add.at(mean, pairs[:, 0], welded[pairs[:, 1]])
            mean /= np.maximum(degree, 1)[:, None]
            welded += (mean - welded) * (factor * strength)[:, None]
    return welded[ids]


def vertex_normals(positions, faces, ids=None):
    ids = weld(positions) if ids is None else ids
    count = ids.max() + 1
    p = positions[faces]
    n = np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0])
    welded = np.zeros((count, 3))
    for corner in range(3):
        np.add.at(welded, ids[faces[:, corner]], n)
    welded /= np.maximum(np.linalg.norm(welded, axis=1), 1e-12)[:, None]
    return welded[ids]


def group_weights(skeleton, joints, weights):
    """Per vertex, the share of its skinning on each body group."""
    index = {name: i for i, name in enumerate(skeleton.names)}
    result = {}
    for group, bones in GROUPS.items():
        if group in ("hand_l", "hand_r"):
            side = group[-1]
            bones = [b for b in skeleton.names if b.endswith("_" + side) and any(
                b.startswith(f) for f in ("hand", "index", "middle", "ring", "pinky", "thumb"))]
        ids = [index[b] for b in bones if b in index]
        mask = np.isin(joints, ids)
        result[group] = (weights * mask).sum(1)
    return result


def regions(skeleton, joints, weights):
    shares = group_weights(skeleton, joints, weights)
    names = list(shares)
    stacked = np.stack([shares[n] for n in names], axis=1)
    return np.array(names)[stacked.argmax(1)], shares


class Landmarks:
    """Points on the base head the face parameters move around, found from the geometry."""

    def __init__(self, positions, head_mask):
        p = positions[head_mask]
        front = p[np.abs(p[:, 0]) < 0.006]
        self.top = p[:, 1].max()
        self.nose = front[front[:, 2].argmax()]
        below = front[front[:, 1] < self.nose[1] - 0.02]
        self.chin = below[below[:, 2] > self.nose[2] - 0.07]
        self.chin = self.chin[self.chin[:, 1].argmin()] if len(self.chin) else self.nose - [0, 0.1, 0.02]
        self.centre = np.array([0.0, (self.top + self.chin[1]) / 2, self.nose[2] - 0.1])
        self.width = np.abs(p[:, 0]).max()
        self.brow = np.array([0.0, self.nose[1] + 0.066, self.nose[2] - 0.02])
        self.mouth = np.array([0.0, (self.nose[1] + self.chin[1]) / 2 + 0.005, self.nose[2] - 0.012])
        self.cheek = np.array([self.width * 0.62, self.nose[1] - 0.008, self.nose[2] - 0.045])
        self.ear = np.array([self.width * 0.98, self.nose[1] + 0.005, self.centre[2] + 0.01])
        self.jaw = np.array([self.width * 0.7, self.chin[1] + 0.03, self.centre[2] + 0.035])


def _bump(positions, centre, radius, mirror=False):
    d = positions - centre
    w = np.exp(-np.sum(d * d, axis=1) / (radius * radius))
    if mirror:
        m = centre * [-1, 1, 1]
        d2 = positions - m
        w = np.maximum(w, np.exp(-np.sum(d2 * d2, axis=1) / (radius * radius)))
    return w


def face_field(positions, marks, face, head_weight):
    """Displacement of each point for the named face parameters (each roughly -1..1).

    Applied to the head and to everything worn on it (hair, beard, brows), so they stay fitted.
    """
    p = positions
    offset = np.zeros_like(p)
    near = head_weight[:, None]

    # Face and head breadth.
    width = face.get("width", 0.0)
    offset[:, 0] += p[:, 0] * 0.07 * width * _smooth_y(p[:, 1], marks.chin[1] - 0.02, marks.top)
    # Jaw: broad and square, or narrow; chin: longer or shorter, forward or back.
    jaw = face.get("jaw", 0.0)
    w = _bump(p, marks.jaw, 0.045, mirror=True)
    offset[:, 0] += np.sign(p[:, 0]) * w * 0.009 * jaw
    chin = face.get("chin", 0.0)
    w = _bump(p, marks.chin + [0, 0.01, -0.01], 0.035)
    offset[:, 1] -= w * 0.01 * chin
    offset[:, 2] += w * 0.006 * face.get("chinForward", 0.0)
    # Nose: size, length (down and out), a bend to one side, and a bump on the bridge.
    nose = face.get("nose", 0.0)
    w = _bump(p, marks.nose - [0, 0.005, 0.008], 0.022)
    offset += (p - (marks.nose - [0, 0.006, 0.03])) * (w * 0.35 * nose)[:, None]
    length = face.get("noseLength", 0.0)
    offset[:, 1] -= w * 0.005 * length
    offset[:, 2] += w * 0.004 * length
    offset[:, 0] += w * 0.005 * face.get("noseBend", 0.0)
    w = _bump(p, marks.nose + [0, 0.022, -0.012], 0.012)
    offset[:, 2] += w * 0.003 * face.get("noseBridge", 0.0)
    # Brow ridge forward; cheeks full or hollow; cheekbones.
    w = _bump(p, marks.brow, 0.03)
    offset[:, 2] += w * 0.005 * face.get("brow", 0.0)
    w = _bump(p, marks.cheek - [0, 0.02, 0], 0.025, mirror=True)
    offset += _outward(p, marks.centre) * (w * 0.006 * face.get("cheeks", 0.0))[:, None]
    w = _bump(p, marks.cheek + [0, 0.008, 0], 0.02, mirror=True)
    offset += _outward(p, marks.centre) * (w * 0.004 * face.get("cheekbones", 0.0))[:, None]
    # Mouth wider or narrower; ears out.
    w = _bump(p, marks.mouth, 0.02)
    offset[:, 0] += p[:, 0] * w * 0.25 * face.get("mouth", 0.0)
    w = _bump(p, marks.ear, 0.03, mirror=True)
    offset[:, 0] += np.sign(p[:, 0]) * w * 0.006 * face.get("ears", 0.0)
    # Age: the jowl and the flesh under the chin sag a little.
    age = face.get("sag", 0.0)
    w = _bump(p, marks.jaw + [-0.01, -0.005, 0.01], 0.03, mirror=True)
    offset[:, 1] -= w * 0.004 * age
    return offset * near


def _outward(p, centre):
    d = p - centre
    d[:, 1] *= 0.3
    return d / np.maximum(np.linalg.norm(d, axis=1), 1e-9)[:, None]


def _smooth_y(y, low, high):
    t = np.clip((y - low) / max(high - low, 1e-6), 0, 1)
    return np.sin(t * np.pi) ** 0.5


def shape_body(positions, faces, region, shares, build, marks):
    """Relaxes the athlete sculpt and gives the body its build. Returns new positions."""
    head_like = shares["head"] + shares["neck"] * 0.5
    hands = shares["hand_l"] + shares["hand_r"]
    feet = shares["foot_l"] + shares["foot_r"]
    amount = np.clip(1 - head_like * 1.6 - hands * 1.5 - feet * 0.6, 0, 1) * 0.85
    p = smooth(positions, faces, amount, iterations=14)

    torso = shares["torso"]
    y = p[:, 1]
    # The heroic V: narrow the chest and lats, fill the waist.
    chest = np.exp(-((y - 1.33) / 0.11) ** 2)
    waist = np.exp(-((y - 1.08) / 0.09) ** 2)
    scale_x = 1 - 0.10 * chest * torso + 0.05 * waist * torso
    scale_z = 1 - 0.08 * chest * torso + 0.03 * waist * torso
    p[:, 0] *= scale_x
    p[:, 2] = (p[:, 2] - 0.0) * scale_z

    # The athlete's deltoids and biceps: bring the upper arm in towards its own axis, most at the
    # shoulder's cap, so the shoulders are a working man's and not a statue's.
    arms = shares["arm_l"] + shares["arm_r"] + torso * (np.abs(p[:, 0]) > 0.14)
    reach = np.abs(p[:, 0])
    axis_y, axis_z = 1.455, -0.065
    cap = np.exp(-((reach - 0.22) / 0.07) ** 2) * 0.2 + np.exp(-((reach - 0.34) / 0.08) ** 2) * 0.1
    shrink = 1 - cap * np.clip(arms, 0, 1) * (reach > 0.12)
    p[:, 1] = axis_y + (p[:, 1] - axis_y) * np.where(reach > 0.12, shrink, 1)
    p[:, 2] = axis_z + (p[:, 2] - axis_z) * np.where(reach > 0.12, shrink, 1)

    build = build.lower()
    factor = {"slight": -1.0, "wiry": -0.7, "average": 0.0, "stocky": 0.7, "heavy": 1.2}.get(build, 0.0)
    normals = vertex_normals(p, faces)
    limb = 1 - head_like - hands * 0.7 - feet * 0.6
    p += normals * (np.clip(limb, 0, 1) * 0.006 * factor)[:, None]
    # A belly for the heavy and the stocky, and for age; pushed out at the front of the waist.
    belly = max(0.0, factor) * 0.55
    if belly:
        front = np.clip(p[:, 2] / 0.12, 0, 1)
        bump = np.exp(-((y - 1.05) / 0.12) ** 2) * front * torso
        p[:, 2] += bump * 0.045 * belly
        p[:, 0] *= 1 + bump * 0.06 * belly
    return p


def load_eye_colours(positions, iris_colour):
    """The eyes have no texture here: sclera, iris and pupil are painted into the vertex colours."""
    colours = np.zeros((len(positions), 4))
    for side in (-1, 1):
        mask = np.sign(positions[:, 0]) == side
        if not mask.any():
            continue
        centre = positions[mask].mean(0)
        centre[2] = positions[mask][:, 2].min() + (positions[mask][:, 2].max() - positions[mask][:, 2].min()) * 0.35
        d = positions[mask] - centre
        d /= np.maximum(np.linalg.norm(d, axis=1), 1e-9)[:, None]
        forward = np.array([0.04 * side, 0.0, 1.0])
        forward /= np.linalg.norm(forward)
        c = d @ forward
        sclera = np.array([0.78, 0.72, 0.64, 1])
        iris = np.array([*iris_colour, 1])
        pupil = np.array([0.03, 0.025, 0.02, 1])
        out = np.where((c > 0.972)[:, None], pupil, np.where((c > 0.83)[:, None], iris, sclera))
        colours[mask] = out
    return colours


def srgb_to_linear(c):
    c = np.asarray(c, dtype=np.float64)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def hex_colour(text):
    text = text.lstrip("#")
    return np.array([int(text[i:i + 2], 16) / 255 for i in (0, 2, 4)])


def cavity(positions, faces, ids=None):
    """0..1 per vertex: how far the surface is sunk below its neighbours (eye sockets, the fold from
    nose to mouth, under the lip). The shader darkens these with age and weathering, which ages the
    face where age shows first without a separate texture."""
    ids = weld(positions) if ids is None else ids
    pairs, count = neighbours(ids, faces)
    welded = np.zeros((count, 3))
    np.add.at(welded, ids, positions)
    welded /= np.bincount(ids, minlength=count)[:, None]
    normals = vertex_normals(positions, faces, ids)
    wn = np.zeros((count, 3))
    np.add.at(wn, ids, normals)
    wn /= np.maximum(np.linalg.norm(wn, axis=1), 1e-9)[:, None]
    mean = np.zeros((count, 3))
    np.add.at(mean, pairs[:, 0], welded[pairs[:, 1]])
    degree = np.bincount(pairs[:, 0], minlength=count)
    mean /= np.maximum(degree, 1)[:, None]
    spread = np.zeros(count)
    np.add.at(spread, pairs[:, 0], np.linalg.norm(welded[pairs[:, 1]] - welded[pairs[:, 0]], axis=1))
    spread /= np.maximum(degree, 1)
    sunk = np.einsum("ij,ij->i", mean - welded, wn) / np.maximum(spread, 1e-6)
    # Two rings of averaging so it reads as soft shading, not speckle.
    for _ in range(2):
        acc = np.zeros(count)
        np.add.at(acc, pairs[:, 0], sunk[pairs[:, 1]])
        sunk = 0.5 * sunk + 0.5 * acc / np.maximum(degree, 1)
    return np.clip(sunk * 4.0, 0, 1)[ids]


def skinning_matrix(skeleton):
    return skeleton.world()


__all__ = ["Part", "weld", "smooth", "vertex_normals", "regions", "Landmarks", "face_field", "shape_body",
           "load_eye_colours", "hex_colour", "srgb_to_linear", "gltf"]
