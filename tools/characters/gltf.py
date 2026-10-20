"""Minimal glTF 2.0 reading and writing for the character build (no dependencies but numpy).

Reads .gltf (with its .bin) and .glb: nodes, the first skin, mesh primitives as numpy arrays, and
animations as per-node keyframe tracks. Writes a .glb holding one skeleton (the same joint names and
hierarchy as the source, so the shared animations drive it), any number of skinned meshes on it, and
animations. Only what the Quaternius Universal Base Characters and Animation Library use is handled.
"""
import json
import pathlib
import struct

import numpy as np

COMPONENT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
WIDTH = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


class Document:
    """A loaded glTF file: its JSON and its binary buffer."""

    def __init__(self, path):
        path = pathlib.Path(path)
        data = path.read_bytes()
        if data[:4] == b"glTF":
            length = struct.unpack("<I", data[12:16])[0]
            self.json = json.loads(data[20:20 + length])
            self.bin = data[28 + length:]
        else:
            self.json = json.loads(data)
            self.bin = (path.parent / self.json["buffers"][0]["uri"]).read_bytes()

    def accessor(self, index):
        a = self.json["accessors"][index]
        view = self.json["bufferViews"][a["bufferView"]]
        dtype = COMPONENT[a["componentType"]]
        width = WIDTH[a["type"]]
        offset = view.get("byteOffset", 0) + a.get("byteOffset", 0)
        stride = view.get("byteStride", 0)
        item = np.dtype(dtype).itemsize * width
        if stride and stride != item:
            raw = np.frombuffer(self.bin, dtype=np.uint8, count=stride * (a["count"] - 1) + item, offset=offset)
            rows = np.lib.stride_tricks.as_strided(raw, shape=(a["count"], item), strides=(stride, 1))
            values = np.ascontiguousarray(rows).view(dtype).reshape(a["count"], width)
        else:
            values = np.frombuffer(self.bin, dtype=dtype, count=a["count"] * width, offset=offset).reshape(a["count"], width)
        if a.get("normalized"):
            values = values.astype(np.float32) / np.iinfo(dtype).max
        return values[:, 0] if width == 1 else values

    def node_names(self):
        return [n.get("name", f"node{i}") for i, n in enumerate(self.json["nodes"])]

    def primitives(self):
        """Every mesh primitive as (node name, mesh name, material name, {attribute: array}, indices)."""
        materials = self.json.get("materials", [])
        for node in self.json["nodes"]:
            if "mesh" not in node:
                continue
            mesh = self.json["meshes"][node["mesh"]]
            for p in mesh["primitives"]:
                attributes = {k: np.array(self.accessor(v)) for k, v in p["attributes"].items()}
                indices = np.array(self.accessor(p["indices"]), dtype=np.int64).reshape(-1, 3)
                material = materials[p["material"]]["name"] if "material" in p else ""
                yield node.get("name", ""), mesh.get("name", ""), material, attributes, indices

    def skeleton(self):
        """The first skin: joint names (in skin order), parent index per joint (-1 for the root),
        rest translation, rotation (x, y, z, w) and scale per joint, and the inverse bind matrices."""
        skin = self.json["skins"][0]
        joints = skin["joints"]
        nodes = self.json["nodes"]
        position = {node: i for i, node in enumerate(joints)}
        parent = [-1] * len(joints)
        for i, node in enumerate(joints):
            for child in nodes[node].get("children", []):
                if child in position:
                    parent[position[child]] = i
        names = [nodes[j]["name"] for j in joints]
        t = np.array([nodes[j].get("translation", [0, 0, 0]) for j in joints], dtype=np.float64)
        r = np.array([nodes[j].get("rotation", [0, 0, 0, 1]) for j in joints], dtype=np.float64)
        s = np.array([nodes[j].get("scale", [1, 1, 1]) for j in joints], dtype=np.float64)
        ibm = self.accessor(skin["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1).astype(np.float64)
        return Skeleton(names, parent, t, r, s, ibm)

    def animations(self):
        """{name: {(joint name, path): (times, values)}} for every animation."""
        names = self.node_names()
        result = {}
        for animation in self.json.get("animations", []):
            tracks = {}
            for channel in animation["channels"]:
                sampler = animation["samplers"][channel["sampler"]]
                target = channel["target"]
                times = np.array(self.accessor(sampler["input"]), dtype=np.float32)
                values = np.array(self.accessor(sampler["output"]), dtype=np.float32)
                tracks[(names[target["node"]], target["path"])] = (times, values)
            result[animation["name"]] = tracks
        return result


class Skeleton:
    def __init__(self, names, parent, translation, rotation, scale, inverse_bind):
        self.names = list(names)
        self.parent = list(parent)
        self.translation = np.array(translation, dtype=np.float64)
        self.rotation = np.array(rotation, dtype=np.float64)
        self.scale = np.array(scale, dtype=np.float64)
        self.inverse_bind = np.array(inverse_bind, dtype=np.float64)

    def index(self, name):
        return self.names.index(name)

    def world(self):
        """Rest world matrix per joint (from the bind matrices, which are what the skinning uses)."""
        return np.linalg.inv(self.inverse_bind)


def quat_matrix(q):
    x, y, z, w = q
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ])


class Writer:
    """Builds a .glb: one skeleton under an 'Armature' node, skinned meshes, and animations."""

    def __init__(self, skeleton):
        self.skeleton = skeleton
        self.chunks = bytearray()
        self.views = []
        self.accessors = []
        self.meshes = []
        self.animations = []
        self.extras = {}

    def _view(self, data, target=None):
        while len(self.chunks) % 4:
            self.chunks.append(0)
        view = {"buffer": 0, "byteOffset": len(self.chunks), "byteLength": len(data)}
        if target:
            view["target"] = target
        self.chunks += data
        self.views.append(view)
        return len(self.views) - 1

    def _accessor(self, array, kind, component, target=None, bounds=False, normalized=False):
        array = np.ascontiguousarray(array, dtype=COMPONENT[component])
        count = array.shape[0]
        a = {"bufferView": self._view(array.tobytes(), target), "componentType": component, "count": count, "type": kind}
        if normalized:
            a["normalized"] = True
        if bounds:
            flat = array.reshape(count, -1)
            a["min"] = flat.min(0).tolist()
            a["max"] = flat.max(0).tolist()
        self.accessors.append(a)
        return len(self.accessors) - 1

    def add_mesh(self, name, positions, normals, uv, uv2, colours, joints, weights, indices):
        """A skinned mesh. colours are linear RGBA floats; joints index the skeleton."""
        attributes = {
            "POSITION": self._accessor(positions, "VEC3", 5126, 34962, bounds=True),
            "NORMAL": self._accessor(normals, "VEC3", 5126, 34962),
            "TEXCOORD_0": self._accessor(uv, "VEC2", 5126, 34962),
            "TEXCOORD_1": self._accessor(uv2, "VEC2", 5126, 34962),
            "COLOR_0": self._accessor(colours, "VEC4", 5126, 34962),
            "JOINTS_0": self._accessor(joints, "VEC4", 5123, 34962),
            "WEIGHTS_0": self._accessor(weights, "VEC4", 5126, 34962),
        }
        index = self._accessor(np.asarray(indices).reshape(-1), "SCALAR", 5125, 34963)
        self.meshes.append({"name": name, "primitives": [{"attributes": attributes, "indices": index, "material": 0}]})

    def add_animation(self, name, tracks):
        """tracks: {(joint name, 'rotation' | 'translation'): (times, values)}."""
        samplers, channels = [], []
        for (joint, path), (times, values) in tracks.items():
            node = self.skeleton.index(joint)
            kind = "VEC4" if path == "rotation" else "VEC3"
            samplers.append({
                "input": self._accessor(times, "SCALAR", 5126, bounds=True),
                "output": self._accessor(values, kind, 5126),
                "interpolation": "LINEAR",
            })
            channels.append({"sampler": len(samplers) - 1, "target": {"node": node, "path": path}})
        self.animations.append({"name": name, "samplers": samplers, "channels": channels})

    def write(self, path):
        sk = self.skeleton
        joints = len(sk.names)
        nodes = []
        for i, name in enumerate(sk.names):
            node = {"name": name, "translation": sk.translation[i].tolist(), "rotation": sk.rotation[i].tolist()}
            if not np.allclose(sk.scale[i], 1):
                node["scale"] = sk.scale[i].tolist()
            children = [c for c in range(joints) if sk.parent[c] == i]
            if children:
                node["children"] = children
            nodes.append(node)
        roots = [i for i in range(joints) if sk.parent[i] == -1]
        skins = []
        mesh_nodes = []
        if self.meshes:
            ibm = self._accessor(sk.inverse_bind.transpose(0, 2, 1).reshape(joints, 16), "MAT4", 5126)
            skins.append({"joints": list(range(joints)), "inverseBindMatrices": ibm, "skeleton": roots[0]})
            for m, mesh in enumerate(self.meshes):
                nodes.append({"name": mesh["name"], "mesh": m, "skin": 0})
                mesh_nodes.append(len(nodes) - 1)
        nodes.append({"name": "Armature", "children": mesh_nodes + roots})
        doc = {
            "asset": {"version": "2.0", "generator": "The Expedition tools/characters"},
            "scene": 0,
            "scenes": [{"name": "Scene", "nodes": [len(nodes) - 1]}],
            "nodes": nodes,
            "buffers": [{"byteLength": 0}],
            "bufferViews": self.views,
            "accessors": self.accessors,
        }
        if self.meshes:
            doc["meshes"] = self.meshes
            doc["skins"] = skins
            doc["materials"] = [{"name": "character", "pbrMetallicRoughness": {"metallicFactor": 0, "roughnessFactor": 0.9}}]
        if self.animations:
            doc["animations"] = self.animations
        if self.extras:
            doc["extras"] = self.extras
        while len(self.chunks) % 4:
            self.chunks.append(0)
        doc["buffers"][0]["byteLength"] = len(self.chunks)
        text = json.dumps(doc, separators=(",", ":")).encode()
        text += b" " * ((4 - len(text) % 4) % 4)
        total = 12 + 8 + len(text) + 8 + len(self.chunks)
        out = struct.pack("<III", 0x46546C67, 2, total)
        out += struct.pack("<II", len(text), 0x4E4F534A) + text
        out += struct.pack("<II", len(self.chunks), 0x004E4942) + bytes(self.chunks)
        pathlib.Path(path).write_bytes(out)
