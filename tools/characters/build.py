#!/usr/bin/env python3
"""Builds each persistent person's model from their identity record (CHARACTER_ART §1–2, C-1, D-028).

    python3 tools/characters/build.py [person.bartolo ...]

For every game/Assets/Characters/<character id>/identity.json (or the IDs named), it takes the
Quaternius base body for the record's sex, shapes the body and face from the record, dresses it in
the record's garments (shells made on that body, garments.py), adds the hair and beard, paints the
eyes, folds everything into one mesh with one material, reduces it to the triangle budget, and
writes <id>/model.glb with the full mesh ("LOD0") and a light one ("LOD1"), both on the shared
skeleton, plus the equipment as separate meshes. It also writes the shared pieces once:
Shared/chr_atlas.png (skin and hair detail on one sheet) and Shared/animations.glb (the clips the
game uses, from the Universal Animation Library, rotations only so every body keeps its own bone
lengths).

The source packs are CC0 and are not kept in the repository; tools/fetch_assets.py downloads them
into .cache/characters (see assets.json). Needs numpy and Pillow. Everything is deterministic: the
same record always builds the same person.
"""
import json
import pathlib
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import body as bodylib  # noqa: E402
import age  # noqa: E402
import decimate  # noqa: E402
import garments as g  # noqa: E402
import gltf  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parents[2]
CACHE = ROOT / ".cache" / "characters"
OUT = ROOT / "game" / "Assets" / "Characters"
UBC = CACHE / "Universal Base Characters[Standard]"
BODIES = UBC / "Base Characters" / "Godot - UE"
HAIRS = UBC / "Hairstyles" / "Rigged to Head Bone" / "glTF (Godot -Unreal)"
UAL = [CACHE / "Universal Animation Library[Standard]" / "Unreal-Godot" / "UAL1_Standard.glb",
       CACHE / "Universal Animation Library 2[Standard]" / "Unreal-Godot" / "UAL2_Standard.glb"]

# Quadrants of the shared sheet: (u offset, v offset) for each texture, each half the sheet.
ATLAS = {"skin_male": (0.0, 0.0), "skin_female": (0.5, 0.0), "hair_1": (0.0, 0.5), "hair_2": (0.5, 0.5)}
HAIR_TEXTURE = {"MI_Hair_1": "hair_1", "MI_Hair_2": "hair_2"}

# The clips the game plays, by the name the game knows them by (CharacterAnimations.cs).
CLIPS = {
    "idle": "Idle_Loop", "walk": "Walk_Loop", "walk_formal": "Walk_Formal_Loop", "walk_carry": "Walk_Carry_Loop",
    "talk": "Idle_Talking_Loop", "sit": "Sitting_Idle_Loop", "sit_talk": "Sitting_Talking_Loop",
    "kneel_work": "Fixing_Kneeling", "chop": "TreeChopping_Loop", "push": "Push_Loop", "pick_up": "PickUp_Table",
    "interact": "Interact", "fold_arms": "Idle_FoldArms_Loop", "lantern": "Idle_Lantern_Loop", "rail": "Idle_Rail_Loop",
    "drink": "Consume", "harvest": "Farm_Harvest", "lie": "LayToIdle", "crouch": "Crouch_Idle_Loop",
    "yes": "Yes", "call": "Idle_Rail_Call", "no": "Idle_No_Loop", "hit": "Hit_Chest", "chest_open": "Chest_Open", "torch": "Idle_Torch_Loop",
}

BUDGET = {"LOD0": 4600, "LOD1": 1500, "Crowd": 1300}

# Dye slots (C-2 crowd): the fraction of a cloth vertex's material number says which of the wearer's
# dyes it takes, so one crowd kit is worn in many colours (character.gdshader).
SLOTS = {"main": 0.1, "lower": 0.2, "head": 0.3}


def load_base(sex):
    name = "Superhero_Female_FullBody" if sex == "Female" else "Superhero_Male_FullBody"
    doc = gltf.Document(BODIES / f"{name}.gltf")
    return doc, doc.skeleton()


def remap_joints(joints, source_names, target_names):
    lookup = np.array([target_names.index(n) for n in source_names])
    return lookup[joints]


def atlas_uv(uv, quadrant):
    u0, v0 = ATLAS[quadrant]
    uv = np.clip(np.asarray(uv, dtype=np.float64), 0.002, 0.998)
    return uv * 0.5 + [u0, v0]


def colour_of(entry, palette, fallback="#808080"):
    """A colour from the record: a palette name or #RRGGBB, in sRGB 0..1 (the shader linearises)."""
    value = entry if isinstance(entry, str) else fallback
    value = palette.get(value, value)
    return bodylib.hex_colour(value)


def build_person(identity, palette, coarse=None):
    """The person's parts. With coarse (a triangle count) the base body is first reduced to it, so
    everything made from it (garments, hems, the cap) comes out coarse too: the light model."""
    sex = identity.get("sex", "Male")
    doc, skeleton = load_base(sex)
    looks = identity["looks"]
    names = skeleton.names
    parts = []

    body_prim = None
    eyes = None
    for node, mesh, material, a, faces in doc.primitives():
        if material.startswith("MI_Superhero"):
            body_prim = (a, faces)
        elif material == "MI_Eyes":
            eyes = (a, faces)
    a, faces = body_prim
    positions = a["POSITION"].astype(np.float64)
    joints = remap_joints(a["JOINTS_0"].astype(np.int64), names, names)
    weights = a["WEIGHTS_0"].astype(np.float64)
    region, shares = bodylib.regions(skeleton, joints, weights)
    if coarse:
        # Texture seams matter only where skin shows its painted detail: the head and the hands.
        detailed = (shares["head"] + shares["hand_l"] + shares["hand_r"]) > 0.5
        faces = decimate.reduce(positions, faces, coarse, decimate.seam_vertices(positions) & detailed,
                                1 + shares["head"] * 3)
    head_weight = shares["head"] + shares["neck"] * 0.3
    marks = bodylib.Landmarks(positions, region == "head")

    # The body and face for this person.
    shaped = bodylib.shape_body(positions, faces, region, shares, looks.get("build", "average"), marks)
    face_offset = bodylib.face_field(shaped, marks, looks.get("face", {}), np.clip(head_weight, 0, 1))
    shaped = shaped + face_offset
    ids = bodylib.weld(shaped)
    normals = bodylib.vertex_normals(shaped, faces, ids)

    skin = colour_of(looks["skin"], palette)
    weathering = float(looks.get("weathering", 0.3))
    body_part = bodylib.Part("body", shaped, normals, atlas_uv(a["TEXCOORD_0"], "skin_female" if sex == "Female" else "skin_male"),
                             joints, weights, faces, g.SKIN, np.array([*skin, weathering]))
    body_part.locked = decimate.seam_vertices(shaped)
    paint_marks(body_part, looks.get("marks", []), shares)
    # Sunken places of the face and hands, in the fraction of the material channel (character.gdshader).
    body_part.kind = body_part.kind + 0.45 * bodylib.cavity(shaped, faces, ids) * np.clip(shares["head"] + shares["hand_l"] + shares["hand_r"], 0, 1)
    # The face and hands carry identity and contact: keep more of them (§15, §49).
    body_part.importance = 1 + shares["head"] * 6 + (shares["hand_l"] + shares["hand_r"]) * 1.5

    # Garments are cut from a much smoother version of the same body.
    # Cloth hangs over the muscles rather than into them: smooth hard everywhere but the head.
    cloth_positions = bodylib.smooth(shaped, faces, np.clip(1 - shares["head"] * 0.8, 0, 1), iterations=80, ids=ids)
    cloth_normals = bodylib.vertex_normals(cloth_positions, faces, ids)
    base = g.Base(cloth_positions, faces, cloth_normals, joints, weights, region, shares, ids)
    base.armhole = float(skeleton.world()[skeleton.index("upperarm_l")][0, 3]) - 0.025

    covered_body = np.zeros(len(shaped), bool)
    layers = []
    for garment in identity.get("clothing", []):
        colour = np.array([*colour_of(garment.get("colour"), palette), float(garment.get("wear", 0.3))])
        kind = g.KINDS[garment.get("material", "linen")]
        shape = garment["shape"]
        if shape == "skirt":
            piece = g.skirt(skeleton, base, garment["top"], garment["bottom"], garment.get("flare", 0.3), colour, kind,
                            garment["id"], garment.get("thickness", 0.02), arc=garment.get("arc"))
            piece.kind = np.asarray(piece.kind, dtype=np.float64) + SLOTS.get(garment.get("slot"), 0.0)
            layers.append((piece, None, garment))
            continue
        piece, mask = g.shell(base, garment, colour, kind, garment["id"])
        if piece is None:
            continue
        piece.kind = piece.kind + SLOTS.get(garment.get("slot"), 0.0)
        if garment.get("layer") == "headwear":
            piece.importance[:] = 4
        layers.append((piece, mask, garment))
        if garment.get("hides", True):
            covered_body |= mask

    # Remove body triangles that clothing hides, keeping a margin so no skin shows at hems.
    hidden = covered_body[faces].all(1)
    body_part = body_part.subset(~hidden)

    # Inner layers under outer ones: drop what an outer layer fully covers.
    final_layers = []
    for i, (piece, mask, garment) in enumerate(layers):
        if mask is not None:
            outer = np.zeros(len(base.positions), bool)
            for later, later_mask, later_garment in layers[i + 1:]:
                if later_mask is not None and later_garment.get("hides", True) and \
                        later_garment.get("thickness", 0.006) > garment.get("thickness", 0.006):
                    outer |= later_mask & ~_hem_zone(base, later_garment)
            if outer.any():
                lookup = np.flatnonzero(mask[base.faces].all(1))
                body_faces = base.faces[lookup]
                inside = outer[body_faces].all(1)
                piece = piece.subset(~inside)
        final_layers.append(piece)
    parts.append(body_part)
    parts.extend(p for p in final_layers if len(p.faces))

    # Eyes, painted.
    if eyes is not None and not coarse:
        ea, ef = eyes
        ep = ea["POSITION"].astype(np.float64)
        iris = colour_of(looks.get("eyes", "#5a3a22"), palette)
        colours = bodylib.load_eye_colours(ep, iris)
        ej = remap_joints(ea["JOINTS_0"].astype(np.int64), names, names)
        eye_part = bodylib.Part("eyes", ep + bodylib.face_field(ep, marks, looks.get("face", {}), np.ones(len(ep))) * 0.6,
                                ea["NORMAL"], np.zeros((len(ep), 2)), ej, ea["WEIGHTS_0"], ef, g.EYE, colours)
        eye_part.importance[:] = 4
        parts.append(eye_part)

    # Hair, brows and beard from the pack's sculpted pieces, fitted to the changed head.
    hair_colour = colour_of(looks.get("hairColour", "#3a2a1c"), palette)
    covered = headwear_cover(identity, marks)
    for piece in looks.get("hair", []):
        if coarse and "Eyebrows" in piece["mesh"]:
            continue
        part = hair_piece(piece, skeleton, marks, looks.get("face", {}), hair_colour, palette, coarse, covered)
        if part is not None:
            parts.append(part)

    # Rigid pieces: hats, pouches, the tools on the belt.
    for extra in identity.get("accessories", []):
        part = accessory(extra, skeleton, base, marks, palette)
        if part is not None:
            part.kind = np.asarray(part.kind, dtype=np.float64) + SLOTS.get(extra.get("slot"), 0.0)
            parts.append(part)

    # Equipment, held in the hand only when the work calls for it; separate meshes.
    equipment = [tool(item, skeleton, palette) for item in identity.get("equipment", [])]
    equipment = [e for e in equipment if e is not None]

    # A child's head is larger for their body than an adult's (Lucía, Tito): everything worn on the
    # head grows about the head joint, by how much each vertex follows the head.
    head_scale = float(identity.get("proportions", {}).get("head", 1.0))
    if head_scale != 1.0:
        pivot = skeleton.world()[skeleton.index("Head")][:3, 3] - [0, 0.03, 0]
        head, neck = skeleton.index("Head"), skeleton.index("neck_01")
        for part in parts + equipment:
            follows = (part.weights * (part.joints == head)).sum(1) + 0.4 * (part.weights * (part.joints == neck)).sum(1)
            factor = 1 + (head_scale - 1) * np.clip(follows, 0, 1)
            part.positions = pivot + (part.positions - pivot) * factor[:, None]
    return skeleton, parts, equipment


def paint_marks(part, marks, shares):
    """Scars (CHARACTER_ART §24): a pale, slightly pink line on the skin along a stroke given in face
    coordinates (x across, y up, metres on the base head), painted into the vertex colours so it is the
    person's own and follows them into every representation."""
    p = part.positions
    front = (shares["head"] > 0.5) & (p[:, 2] > 0.0)
    for mark in marks:
        if mark.get("kind") != "scar":
            continue
        points = np.asarray(mark["points"], dtype=np.float64)
        width = mark.get("width", 0.004)
        q = p[:, :2]
        distance = np.full(len(p), np.inf)
        for a, b in zip(points[:-1], points[1:]):
            ab = b - a
            t = np.clip(((q - a) @ ab) / max(ab @ ab, 1e-12), 0, 1)
            distance = np.minimum(distance, np.linalg.norm(q - (a + t[:, None] * ab), axis=1))
        w = np.clip(1 - distance / width, 0, 1) * front
        scar = np.clip(part.colour[:, :3] * [1.1, 0.93, 0.9] + [0.07, 0.04, 0.04], 0, 1)
        part.colour[:, :3] = part.colour[:, :3] * (1 - w[:, None]) + scar * w[:, None]


def _hem_zone(base, garment):
    """The last few centimetres inside an outer garment's hems, where the inner layer must stay."""
    zone = np.zeros(len(base.positions), bool)
    margin = 0.025
    if garment.get("sleeve") is not None:
        zone |= base.arm & (base.reach > garment["sleeve"] - margin)
    if garment.get("leg"):
        zone |= base.leg & (base.height < garment["leg"][0] + margin)
    if garment.get("torso"):
        low, high = garment["torso"]
        zone |= ~base.arm & ~base.leg & ((base.height < low + margin) | (base.height > high - margin))
    return zone


def headwear_cover(identity, marks):
    """Where a cap or hat sits, as a test on points: hair there is under it and is cut away, so the
    cap sits on the head and no lock pokes through."""
    tests = []
    for garment in identity.get("clothing", []):
        if garment.get("head") is not None:
            front, back = garment["head"]
            tests.append(lambda p, f=front, b=back: p[:, 1] > b + (f - b) * np.clip((p[:, 2] + 0.1) / 0.2, 0, 1) - 0.004)
    for extra in identity.get("accessories", []):
        if extra["shape"] == "crown":
            bottom = marks.brow[1] + 0.04
            tests.append(lambda p, y=bottom: p[:, 1] > y)
    if not tests:
        return None
    return lambda p: np.any([t(p) for t in tests], axis=0)


def hair_piece(piece, skeleton, marks, face, colour, palette, coarse=None, covered=None):
    doc = gltf.Document(HAIRS / f"{piece['mesh']}.gltf")
    source = doc.skeleton().names
    for node, mesh, material, a, faces in doc.primitives():
        p = a["POSITION"].astype(np.float64)
        p = p + bodylib.face_field(p, marks, face, np.ones(len(p)))
        uv = atlas_uv(a["TEXCOORD_0"], HAIR_TEXTURE.get(material, "hair_1"))
        joints = remap_joints(a["JOINTS_0"].astype(np.int64), source, skeleton.names)
        keep = np.ones(len(faces), bool)
        # A receding hairline or a bald crown: cut the sculpt away at the front and top.
        if "recede" in piece:
            r = piece["recede"]
            centre = p[faces].mean(1)
            front = centre[:, 2] - (marks.nose[2] - 0.05)
            top = centre[:, 1] - (marks.top - 0.02)
            keep &= ~((front > -r * 0.12) & (centre[:, 1] > marks.brow[1] + 0.02 + (1 - r) * 0.03))
            if piece.get("crown"):
                keep &= ~(top > -piece["crown"] * 0.05)
        if "trim" in piece:
            # A shorter beard: the sculpt pulled in towards the face.
            centre = marks.centre + [0, -0.03, 0.02]
            d = p - centre
            p = centre + d * (1 - piece["trim"] * 0.12)
        if covered is not None and "Eyebrows" not in piece["mesh"] and "Beard" not in piece["mesh"]:
            keep &= ~covered(p[faces].mean(1))
        if "bottom" in piece:
            centre = p[faces].mean(1)
            keep &= centre[:, 1] > piece["bottom"]
        lift = piece.get("lift", 0.0)
        if lift:
            n = a["NORMAL"].astype(np.float64)
            p = p + n * lift
        tint = colour_of(piece["colour"], palette) if "colour" in piece else colour
        part = bodylib.Part(piece["mesh"], p, a["NORMAL"], uv, joints, a["WEIGHTS_0"], faces[keep], g.HAIR,
                            np.array([*tint, piece.get("grey", 0.0)]))
        part = part.subset(np.ones(len(part.faces), bool))
        part.importance[:] = 3.0 if "Eyebrows" in piece["mesh"] else 2.0
        # The hair texture is a noise of strands: its seams do not show, so only the brows keep theirs.
        part.locked = decimate.seam_vertices(part.positions) if "Eyebrows" in piece["mesh"] else np.zeros(len(part.positions), bool)
        if coarse:
            faces = decimate.reduce(part.positions, part.faces, max(len(part.faces) // 5, 40), keep_open_edges=False)
            part = part.subset_faces(faces)
        return part
    return None


def accessory(extra, skeleton, base, marks, palette):
    colour = np.array([*colour_of(extra.get("colour"), palette), float(extra.get("wear", 0.3))])
    kind = g.KINDS[extra.get("material", "wool")]
    shape = extra["shape"]
    if shape == "brim":
        # A hat's brim round the head at brow height.
        centre = [0, marks.brow[1] + 0.045 + extra.get("raise", 0.0), marks.centre[2] + 0.01]
        p, f = g.disc(centre, extra.get("radius", 0.17), extra.get("radius", 0.17) * 0.95, 0.008,
                      segments=22, droop=extra.get("droop", 0.01), inner=0.45)
        return g.rigid(skeleton, "Head", p, f, colour, kind, extra["id"])
    if shape == "crown":
        # A hat's crown: a short tapered drum.
        top = marks.top + extra.get("height", 0.06)
        bottom = marks.brow[1] + 0.04
        centre = marks.centre + [0, 0, 0.01]
        p, f = g.cylinder([centre[0], bottom, centre[2]], [centre[0], top, centre[2]], extra.get("radius", 0.105),
                          segments=16, end_radius=extra.get("topRadius", 0.095))
        return g.rigid(skeleton, "Head", p, f, colour, kind, extra["id"])
    if shape == "pouch":
        side = 1 if extra.get("side", "left") == "left" else -1
        y = extra.get("height", 1.0)
        ring = np.abs(base.positions[:, 1] - y) < 0.02
        x = 0.13 * side
        near = ring & (np.sign(base.positions[:, 0]) == side)
        z = base.positions[near][:, 2].max() - 0.04 if near.any() else 0.05
        p, f = g.box([x, y - 0.04, z], extra.get("size", [0.09, 0.11, 0.04]), rotation_y=0.4 * side)
        part = g.rigid(skeleton, "pelvis", p, f, colour, kind, extra["id"])
        return part
    if shape == "tool":
        # A tool carried on the belt: handle and head, hung at the hip.
        side = 1 if extra.get("side", "right") == "left" else -1
        y = extra.get("height", 0.98)
        hx = 0.17 * side
        handle = g.cylinder([hx, y + 0.06, -0.02], [hx, y - 0.2, 0.0], 0.013, segments=6)
        head = g.box([hx, y - 0.21, 0.02], [0.03, 0.04, 0.09])
        p, f = g.merge(handle, head)
        return g.rigid(skeleton, "pelvis", p, f, colour, g.KINDS[extra.get("material", "wood")], extra["id"])
    if shape == "knife":
        # A sheath knife on the belt: the sailor's tool for everything.
        side = 1 if extra.get("side", "right") == "left" else -1
        y = extra.get("height", 1.0)
        x = 0.16 * side
        sheath = g.box([x, y - 0.09, 0.04], [0.035, 0.16, 0.02], rotation_y=0.3 * side)
        grip = g.box([x, y + 0.03, 0.04], [0.025, 0.08, 0.025], rotation_y=0.3 * side)
        p, f = g.merge(sheath, grip)
        return g.rigid(skeleton, "pelvis", p, f, colour, g.KINDS[extra.get("material", "leather")], extra["id"])
    if shape == "dividers":
        # A pilot's brass dividers hanging on a cord at the chest (Velho).
        top = marks.chin[1] - 0.1
        cord_l = g.cylinder([-0.05, top + 0.05, 0.06], [0.0, top - 0.06, 0.125], 0.003, segments=4)
        cord_r = g.cylinder([0.05, top + 0.05, 0.06], [0.0, top - 0.06, 0.125], 0.003, segments=4)
        leg_l = g.cylinder([0.0, top - 0.06, 0.128], [-0.018, top - 0.17, 0.13], 0.004, segments=4)
        leg_r = g.cylinder([0.0, top - 0.06, 0.128], [0.018, top - 0.17, 0.13], 0.004, segments=4)
        p, f = g.merge(cord_l, cord_r, leg_l, leg_r)
        return g.rigid(skeleton, "spine_03", p, f, colour, g.METAL, extra["id"])
    if shape == "cord":
        # A cord round the neck with something hanging from it (Velho's dividers).
        y = extra.get("height", 1.36)
        p, f = g.disc([0, marks.chin[1] - 0.09, marks.centre[2] + 0.02], 0.085, 0.09, 0.006, segments=14, inner=0.85)
        return g.rigid(skeleton, "spine_03", p, f, colour, g.LEATHER, extra["id"])
    return None


def tool(item, skeleton, palette):
    """A tool held in the hand while working (§26, §32): placed in the closed right hand of the rest pose."""
    hand = skeleton.world()[skeleton.index(item.get("socket", "hand_r"))][:3, 3]
    side = -1 if item.get("socket", "hand_r").endswith("_r") else 1
    grip = hand + [0.075 * side, -0.01, 0.025]
    wood = np.array([*colour_of(item.get("handle", "#6b4a2b"), palette), 0.4])
    metal = np.array([*colour_of(item.get("head", "#55575a"), palette), 0.3])
    shape = item["shape"]
    pieces = []
    if shape in ("adze", "mallet", "axe"):
        length = item.get("length", 0.55)
        handle = g.cylinder(grip - [0, 0, 0.12], grip + [0, 0, length - 0.12], 0.014, segments=6)
        pieces.append((handle, g.WOOD, wood))
        tip = grip + [0, 0, length - 0.13]
        if shape == "mallet":
            head = g.cylinder(tip - [0.07, 0, 0], tip + [0.07, 0, 0], 0.045, segments=8)
            pieces.append((head, g.WOOD, wood * [0.85, 0.85, 0.85, 1]))
        else:
            blade = g.box(tip + [0, -0.05, 0.0], [0.02, 0.12, 0.05])
            pieces.append((blade, g.METAL, metal))
    elif shape == "satchel":
        pass
    if not pieces:
        return None
    result = []
    for (p, f), kind, colour in pieces:
        result.append(g.rigid(skeleton, item.get("socket", "hand_r"), p, f, colour, kind, item["id"]))
    combined = combine(result, item["id"])
    return combined


def combine(parts, name):
    positions = np.concatenate([p.positions for p in parts])
    offsets = np.cumsum([0] + [len(p.positions) for p in parts[:-1]])
    faces = np.concatenate([p.faces + o for p, o in zip(parts, offsets)])
    part = bodylib.Part(name, positions, np.concatenate([p.normals for p in parts]), np.concatenate([p.uv for p in parts]),
                        np.concatenate([p.joints for p in parts]), np.concatenate([p.weights for p in parts]), faces,
                        np.concatenate([p.kind for p in parts]), np.concatenate([p.colour for p in parts]))
    part.importance = np.concatenate([p.importance for p in parts])
    part.locked = np.concatenate([p.locked for p in parts])
    return part


def reduce_parts(parts, budget, keep_open_edges=True, strict=True):
    """Reduces each part in proportion to its share of the triangles, weighted by importance."""
    total = sum(len(p.faces) for p in parts)
    if total <= budget:
        return parts
    # Each part's share of the budget grows with how much it matters (face, hands, hair, headwear).
    shares = np.array([len(p.faces) * np.sqrt(p.importance.mean()) for p in parts])
    result = []
    for p, share in zip(parts, shares):
        target = max(int(budget * share / shares.sum()), 12)
        if len(p.faces) <= target:
            result.append(p)
            continue
        # Hair is many small sculpted locks, each with an open edge: those may move.
        free = p.kind[0] == g.HAIR and "Eyebrows" not in p.name
        faces = decimate.reduce(p.positions, p.faces, target, p.locked, p.importance, keep_open_edges and not free, strict)
        reduced = bodylib.Part(p.name, p.positions, p.normals, p.uv, p.joints, p.weights, faces, p.kind, p.colour)
        reduced.importance = p.importance
        reduced.locked = p.locked
        result.append(reduced.subset(np.ones(len(faces), bool)))
    return result


def lod1(parts, budget):
    """The light model: no eyes or brows, the head and hands no longer favoured, everything reduced
    as far as it goes while hems and the skin's texture seams stay put. (Letting hems move, or cutting
    the garments from a coarser body, tears the clothes; see MILESTONES C-1.)"""
    keep = [p for p in parts if p.kind[0] != g.EYE and "Eyebrows" not in p.name]
    simpler = []
    for p in keep:
        q = bodylib.Part(p.name, p.positions, p.normals, p.uv, p.joints, p.weights, p.faces, p.kind, p.colour)
        q.importance = np.minimum(p.importance, 2.0)
        q.locked = p.locked
        simpler.append(q)
    return reduce_parts(simpler, budget)


def write_model(skeleton, lods, equipment, path, identity):
    writer = gltf.Writer(skeleton)
    for name, parts in lods.items():
        writer.add_mesh(name, *pack(parts))
    for item in equipment:
        writer.add_mesh(item.name, *pack([item]))
    writer.extras = {"characterId": identity["characterId"],
                     "triangles": {name: int(sum(len(p.faces) for p in parts)) for name, parts in lods.items()}}
    writer.write(path)


def pack(parts):
    positions = np.concatenate([p.positions for p in parts]).astype(np.float32)
    normals = np.concatenate([p.normals for p in parts]).astype(np.float32)
    uv = np.concatenate([p.uv for p in parts]).astype(np.float32)
    kind = np.concatenate([p.kind for p in parts])
    height = np.concatenate([p.positions[:, 1] for p in parts])
    uv2 = np.stack([kind, height], axis=1).astype(np.float32)
    colours = np.concatenate([p.colour for p in parts]).astype(np.float32)
    joints = np.concatenate([p.joints for p in parts]).astype(np.uint16)
    weights = np.concatenate([p.weights for p in parts]).astype(np.float64)
    weights /= np.maximum(weights.sum(1, keepdims=True), 1e-9)
    offsets = np.cumsum([0] + [len(p.positions) for p in parts[:-1]])
    faces = np.concatenate([p.faces + o for p, o in zip(parts, offsets)]).astype(np.uint32)
    return positions, normals, uv, uv2, colours, joints, weights.astype(np.float32), faces


def build_atlas():
    """The shared sheet: each texture divided by its own typical colour, so the record's skin tone or
    hair colour is the colour and the texture only adds its variation (as scenery.gdshader does)."""
    sources = {
        "skin_male": BODIES / "T_Superhero_Male_Dark.png",
        "skin_female": BODIES / "T_Superhero_Female_Dark_BaseColor.png",
        "hair_1": HAIRS / "T_Hair_1_BaseColor.png",
        "hair_2": HAIRS / "T_Hair_2_BaseColor.png",
    }
    size = 1024
    sheet = Image.new("RGBA", (size * 2, size * 2), (128, 128, 128, 255))
    for key, path in sources.items():
        image = np.asarray(Image.open(path).convert("RGB").resize((size, size), Image.LANCZOS), dtype=np.float64) / 255
        lum = image.mean(2)
        typical = np.median(image[lum > np.percentile(lum, 30)], axis=0)
        detail = np.clip(image / np.maximum(typical, 0.02) * 0.5, 0, 1)
        if key.startswith("skin"):
            # Keep the painted features (lips, brows, the shading round the eyes) but soften the
            # sculpted muscle shading the athlete's texture carries.
            detail = 0.5 + (detail - 0.5) * 0.8
        u0, v0 = ATLAS[key]
        alpha = np.full((size, size), 255.0)
        if key.startswith("skin"):
            alpha = age_lines("Female" if key == "skin_female" else "Male", size)
        rgba = np.dstack([detail * 255, alpha]).astype(np.uint8)
        sheet.paste(Image.fromarray(rgba, "RGBA"), (int(u0 * 2 * size), int(v0 * 2 * size)))
    shared = OUT / "Shared"
    shared.mkdir(parents=True, exist_ok=True)
    sheet.save(shared / "chr_atlas.png", optimize=True)


def age_lines(sex, size):
    doc, skeleton = load_base(sex)
    eyes = None
    for node, mesh, material, a, faces in doc.primitives():
        if material == "MI_Eyes":
            p = a["POSITION"]
            eyes = p[p[:, 0] > 0].mean(0)
    for node, mesh, material, a, faces in doc.primitives():
        if material.startswith("MI_Superhero"):
            positions = a["POSITION"].astype(np.float64)
            region, shares = bodylib.regions(skeleton, a["JOINTS_0"].astype(np.int64), a["WEIGHTS_0"].astype(np.float64))
            head = region == "head"
            marks = bodylib.Landmarks(positions, head)
            return age.paint(positions, a["TEXCOORD_0"].astype(np.float64), head, marks, eyes, size)
    return np.full((size, size), 255.0)


def build_animations():
    """The clips the game uses, on the shared skeleton: rotations for every bone (so each person's
    own bone lengths stand) and the pelvis's movement, sampled as the pack has them."""
    doc = gltf.Document(UAL[0])
    skeleton = doc.skeleton()
    writer = gltf.Writer(skeleton)
    wanted = {v: k for k, v in CLIPS.items()}
    found = set()
    for path in UAL:
        source = gltf.Document(path)
        for name, tracks in source.animations().items():
            if name not in wanted or name in found:
                continue
            found.add(name)
            kept = {}
            for (joint, kind), (times, values) in tracks.items():
                if joint not in skeleton.names:
                    continue
                if kind == "rotation" or (kind == "translation" and joint == "pelvis"):
                    kept[(joint, kind)] = (times, values)
            writer.add_animation(wanted[name], kept)
    missing = set(wanted) - found
    if missing:
        sys.exit(f"animation clips not found: {sorted(missing)}")
    # One hidden triangle on the skin, so the importer makes a Skeleton3D like a person's model has
    # and the clips' track paths ("Armature/Skeleton3D:bone") are the same on both.
    tri = np.array([[0, 0, 0], [0.001, 0, 0], [0, 0.001, 0]], np.float32)
    writer.add_mesh("Rig", tri, np.tile([0, 0, 1], (3, 1)), np.zeros((3, 2)), np.zeros((3, 2)), np.ones((3, 4)),
                    np.zeros((3, 4)), np.tile([1, 0, 0, 0], (3, 1)), np.array([0, 1, 2]))
    writer.write(OUT / "Shared" / "animations.glb")


def main(argv):
    if not UBC.exists():
        sys.exit("The Quaternius packs are not in .cache/characters; run tools/fetch_assets.py first.")
    palette = json.loads((OUT / "Shared" / "palette.json").read_text())["colours"]
    build_atlas()
    build_animations()
    folders = sorted(p.parent for p in OUT.glob("*/identity.json")) + sorted(p.parent for p in (OUT / "Crowd").glob("*/identity.json"))
    if argv:
        folders = [f for f in folders if f.name in argv or (f.parent.name == "Crowd" and "crowd" in argv)]
    for folder in folders:
        identity = json.loads((folder / "identity.json").read_text())
        skeleton, parts, equipment = build_person(identity, palette)
        if identity.get("crowd"):
            # Tier C: one light mesh, no portrait (PORT_EXPERIENCE §6, CHARACTER_ART §37).
            light = [p for p in parts if p.kind[0] != g.EYE and "Eyebrows" not in p.name]
            for p in light:
                if p.kind[0] in (g.SKIN, g.HAIR):
                    # Seen small and in passing: texture seams may move; garment hems may not.
                    p.locked = np.zeros(len(p.positions), bool)
                    p.importance = np.minimum(p.importance, 1.5)
            lod0 = reduce_parts(light, BUDGET["Crowd"], strict=False)
            write_model(skeleton, {"LOD0": lod0}, equipment, folder / "model.glb", identity)
            print(f"{folder.parent.name}/{folder.name}: {sum(len(p.faces) for p in lod0)} triangles")
            continue
        lod0 = reduce_parts(parts, identity.get("budget", BUDGET["LOD0"]))
        light = lod1(lod0, BUDGET["LOD1"])
        write_model(skeleton, {"LOD0": lod0, "LOD1": light}, equipment, folder / "model.glb", identity)
        # The portrait is rendered from the same person at full detail, never reduced (§10, §34).
        write_model(skeleton, {"Portrait": parts}, [], folder / "portrait.glb", identity)
        counts = {p.name: len(p.faces) for p in lod0}
        print(f"{folder.name}: LOD0 {sum(counts.values())} triangles, LOD1 {sum(len(p.faces) for p in light)}; {counts}")


if __name__ == "__main__":
    main(sys.argv[1:])
