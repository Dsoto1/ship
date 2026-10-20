"""Quadric-error mesh reduction that keeps every vertex's own attributes.

Each step folds one vertex into a neighbour (a half-edge collapse), choosing the fold that changes
the surface least by the Garland-Heckbert quadric error. Because the surviving vertex is an original
one, its UV, skin weights, colour and material are kept exactly: no resampling, no smeared seams.
Vertices on a texture seam or an open edge (those listed in `locked`) never move, so seams and
garment hems stay where they were and the pieces still meet. Folds that would turn a face over or
make a sliver are refused.
"""
import heapq

import numpy as np


def _plane_quadrics(positions, faces):
    p = positions[faces]
    n = np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0])
    area = np.linalg.norm(n, axis=1)
    unit = n / np.maximum(area, 1e-12)[:, None]
    d = -np.einsum("ij,ij->i", unit, p[:, 0])
    plane = np.concatenate([unit, d[:, None]], axis=1)
    q = np.einsum("fi,fj->fij", plane, plane) * area[:, None, None]
    quadrics = np.zeros((len(positions), 4, 4))
    for corner in range(3):
        np.add.at(quadrics, faces[:, corner], q)
    return quadrics


def reduce(positions, faces, target_faces, locked=None, importance=None, keep_open_edges=True):
    """Returns the faces (indexing the original vertices) after folding down to about target_faces.

    importance: optional per-vertex multiplier on the error, so faces and hands keep more detail.
    """
    positions = np.asarray(positions, dtype=np.float64)
    faces = np.array(faces, dtype=np.int64)
    count = len(positions)
    locked = np.zeros(count, bool) if locked is None else np.asarray(locked, bool).copy()
    weight = np.ones(count) if importance is None else np.asarray(importance, dtype=np.float64)
    quadrics = _plane_quadrics(positions, faces)

    # Open edges (used by one face) pin their vertices, unless far-view detail may let hems move.
    if keep_open_edges:
        edges = np.sort(np.concatenate([faces[:, [0, 1]], faces[:, [1, 2]], faces[:, [2, 0]]]), axis=1)
        unique, uses = np.unique(edges, axis=0, return_counts=True)
        locked[unique[uses == 1].ravel()] = True

    alive = np.ones(len(faces), bool)
    vertex_faces = [set() for _ in range(count)]
    for f, (a, b, c) in enumerate(faces):
        vertex_faces[a].add(f)
        vertex_faces[b].add(f)
        vertex_faces[c].add(f)
    version = np.zeros(count, np.int64)
    homogeneous = np.concatenate([positions, np.ones((count, 1))], axis=1)

    def cost(u, v):
        q = quadrics[u] + quadrics[v]
        x = homogeneous[v]
        return float(x @ q @ x) * max(weight[u], weight[v])

    heap = []

    def push_edges_of(u):
        if locked[u]:
            return
        neighbours = set()
        for f in vertex_faces[u]:
            neighbours.update(faces[f])
        neighbours.discard(u)
        for v in neighbours:
            heapq.heappush(heap, (cost(u, v), u, v, version[u], version[v]))

    for u in range(count):
        push_edges_of(u)

    faces_left = int(alive.sum())
    while faces_left > target_faces and heap:
        _, u, v, vu, vv = heapq.heappop(heap)
        if vu != version[u] or vv != version[v] or locked[u]:
            continue
        shared = vertex_faces[u] & vertex_faces[v]
        if not shared:
            continue
        moving = vertex_faces[u] - shared
        # Refuse folds that flip a face or leave a sliver.
        ok = True
        for f in moving:
            a, b, c = faces[f]
            before = np.cross(positions[b] - positions[a], positions[c] - positions[a])
            corners = [v if x == u else x for x in (a, b, c)]
            pa, pb, pc = positions[corners[0]], positions[corners[1]], positions[corners[2]]
            after = np.cross(pb - pa, pc - pa)
            area = np.linalg.norm(after)
            if area < 1e-12 or np.dot(before, after) <= 0.3 * np.linalg.norm(before) * area:
                ok = False
                break
            longest = max(np.sum((pb - pa) ** 2), np.sum((pc - pb) ** 2), np.sum((pa - pc) ** 2))
            if area * area / (longest * longest + 1e-20) < 0.0004:
                ok = False
                break
        if not ok:
            continue
        # Fold u into v.
        for f in shared:
            alive[f] = False
            for x in faces[f]:
                vertex_faces[x].discard(f)
        faces_left -= len(shared)
        for f in moving:
            faces[f][faces[f] == u] = v
            vertex_faces[v].add(f)
        vertex_faces[u] = set()
        quadrics[v] += quadrics[u]
        locked[u] = True
        version[u] += 1
        version[v] += 1
        push_edges_of(v)
        neighbours = set()
        for f in vertex_faces[v]:
            neighbours.update(faces[f])
        for n in neighbours:
            if n != v:
                version[n] += 1
                push_edges_of(n)
    return faces[alive]


def seam_vertices(positions, tolerance=1e-6):
    """Vertices that share a position with another vertex: the two sides of a UV or normal seam."""
    keys = np.round(np.asarray(positions) / tolerance).astype(np.int64)
    _, inverse, counts = np.unique(keys, axis=0, return_inverse=True, return_counts=True)
    return counts[inverse.ravel()] > 1
