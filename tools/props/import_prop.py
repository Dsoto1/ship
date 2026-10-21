"""Brings an AI-generated prop (a textured, dense GLB from Artlist's image-to-3D) into the game's look.

The generators give tens of thousands of triangles painted with a photographic texture; the game is
low-poly and flat-shaded in a fixed palette (ART_BIBLE.md). For each prop in props.json this:
  1. reads the GLB and samples its base-colour texture at every vertex;
  2. welds the texture seams, so the surface is one piece;
  3. folds it down to the prop's triangle budget (tools/characters/decimate.py, which keeps original
     vertices, so their colours survive);
  4. reduces the colours to a few flat tones, each pulled towards the nearest colour of the Garza's
     palette (tools/ship/garza.json), so props sit with the ship;
  5. stands it on the ground at its real size, faces it the right way and writes a flat-shaded,
     vertex-coloured GLB like the ship's (tools/ship/build_ship.py's writer).

The source GLBs are kept out of the repo (.cache/props/); props.json records where each came from.
Run: python3 tools/props/import_prop.py [name ...]
"""
import io
import json
import pathlib
import struct
import sys

import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "characters"))
sys.path.insert(0, str(ROOT / "tools" / "ship"))
import decimate  # noqa: E402
from build_ship import Mesh, hexcolour, write_glb, SPEC  # noqa: E402

PROPS = json.loads((ROOT / "tools" / "props" / "props.json").read_text())
CACHE = ROOT / ".cache" / "props"
OUT = ROOT / "game" / "Assets" / "Props"
PALETTE = np.array([hexcolour(v)[:3] for v in list(SPEC["colours"].values()) + PROPS["extraPalette"]])


def read_glb(path):
    """Positions, UVs, triangles and the base-colour image of every primitive, joined into one mesh."""
    data = path.read_bytes()
    length = struct.unpack("<I", data[12:16])[0]
    doc = json.loads(data[20:20 + length])
    binary = data[20 + length + 8:]

    def view(index):
        v = doc["bufferViews"][index]
        return binary[v.get("byteOffset", 0):v.get("byteOffset", 0) + v["byteLength"]]

    def accessor(index):
        a = doc["accessors"][index]
        kind = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}[a["componentType"]]
        width = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
        raw = view(a["bufferView"])[a.get("byteOffset", 0):]
        return np.frombuffer(raw, kind, a["count"] * width).reshape(a["count"], width) if width > 1 \
            else np.frombuffer(raw, kind, a["count"])

    positions, uvs, faces, colours = [], [], [], []
    for mesh in doc["meshes"]:
        for prim in mesh["primitives"]:
            p = accessor(prim["attributes"]["POSITION"]).astype(np.float64)
            uv = accessor(prim["attributes"]["TEXCOORD_0"]).astype(np.float64)
            f = accessor(prim["indices"]).astype(np.int64).reshape(-1, 3) + sum(len(x) for x in positions)
            material = doc["materials"][prim.get("material", 0)]["pbrMetallicRoughness"]
            image = doc["images"][doc["textures"][material["baseColorTexture"]["index"]]["source"]]
            texture = np.asarray(Image.open(io.BytesIO(view(image["bufferView"]))).convert("RGB"), np.float64) / 255
            h, w = texture.shape[:2]
            px = np.clip((uv[:, 0] % 1) * (w - 1), 0, w - 1).astype(int)
            py = np.clip((uv[:, 1] % 1) * (h - 1), 0, h - 1).astype(int)
            colours.append(texture[py, px] * np.array(material.get("baseColorFactor", [1, 1, 1, 1])[:3]))
            positions.append(p)
            uvs.append(uv)
            faces.append(f)
    return np.concatenate(positions), np.concatenate(faces), np.concatenate(colours)


def weld(positions, faces, colours, tolerance=1e-5):
    keys = np.round(positions / tolerance).astype(np.int64)
    _, first, inverse = np.unique(keys, axis=0, return_index=True, return_inverse=True)
    inverse = inverse.ravel()
    welded = positions[first]
    colour = np.zeros((len(first), 3))
    np.add.at(colour, inverse, colours)
    colour /= np.bincount(inverse, minlength=len(first))[:, None]
    f = inverse[faces]
    f = f[(f[:, 0] != f[:, 1]) & (f[:, 1] != f[:, 2]) & (f[:, 0] != f[:, 2])]
    return welded, f, colour


def tones(colours, weights, count, pull, seed=7):
    """A few flat tones for the prop (weighted k-means), each pulled towards the nearest palette colour."""
    rng = np.random.default_rng(seed)
    centres = colours[rng.choice(len(colours), count, replace=False, p=weights / weights.sum())]
    for _ in range(30):
        label = np.argmin(((colours[:, None] - centres[None]) ** 2).sum(-1), axis=1)
        for k in range(count):
            mask = label == k
            if mask.any():
                centres[k] = np.average(colours[mask], axis=0, weights=weights[mask])
    nearest = PALETTE[np.argmin(((centres[:, None] - PALETTE[None]) ** 2).sum(-1), axis=1)]
    return label, centres * (1 - pull) + nearest * pull


def build(name, spec):
    source = CACHE / f"{name}.glb"
    if not source.exists():
        sys.exit(f"{name}: put the generated GLB at {source.relative_to(ROOT)} (see props.json for its source)")
    positions, faces, colours = read_glb(source)
    positions, faces, colours = weld(positions, faces, colours)
    print(f"{name}: {len(faces)} triangles in, budget {spec['triangles']}")
    faces = decimate.reduce(positions, faces, spec["triangles"], keep_open_edges=False)
    used = np.unique(faces)
    remap = np.full(len(positions), -1)
    remap[used] = np.arange(len(used))
    positions, colours, faces = positions[used], colours[used], remap[faces]

    # Orientation: turn about y so the prop faces the way the game expects, then stand it on y = 0,
    # centred, at its real height.
    turn = np.radians(spec.get("turnY", 0))
    c, s = np.cos(turn), np.sin(turn)
    positions = positions @ np.array([[c, 0, -s], [0, 1, 0], [s, 0, c]])
    low, high = positions.min(0), positions.max(0)
    positions = (positions - [(low[0] + high[0]) / 2, low[1], (low[2] + high[2]) / 2]) * (spec["height"] / (high[1] - low[1]))

    tri = positions[faces]
    area = np.linalg.norm(np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]), axis=1) / 2
    face_colour = colours[faces].mean(1)
    label, flat = tones(face_colour, area + 1e-9, spec.get("tones", 5), spec.get("pull", 0.35))
    mesh = Mesh()
    for (a, b, cc), k in zip(tri, label):
        mesh.tri(tuple(a), tuple(b), tuple(cc), np.append(flat[k], 1.0))
    OUT.mkdir(parents=True, exist_ok=True)
    write_glb(mesh, OUT / f"{name}.glb")
    print(f"  -> {(OUT / f'{name}.glb').relative_to(ROOT)}: {len(faces)} triangles, "
          f"{spec['height']} m tall, {(high - low)[[0, 2]] * spec['height'] / (high[1] - low[1])} m across")


if __name__ == "__main__":
    wanted = sys.argv[1:] or list(PROPS["props"])
    for prop in wanted:
        build(prop, PROPS["props"][prop])
