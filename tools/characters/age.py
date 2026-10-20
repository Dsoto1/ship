"""Age lines painted onto the shared skin sheet (CHARACTER_ART §6–7: age shows in the face).

Every man shares the male base head's UV layout and every woman the female's, so one map of lines
per sex serves everyone: the forehead's furrows, the frown between the brows, crow's feet, the bags
under the eyes, the folds from nose to mouth and from the mouth's corners down. Each line is drawn
as a curve on the front of the 3D head, carried into the texture through the nearest vertices'
UVs, and stored as darkness in the sheet's alpha. The shader deepens them with each person's
weathering (age and exposure together), so the young show none and Bartolo shows them all.
"""
import numpy as np
from PIL import Image, ImageDraw, ImageFilter


def surface_point(head, x, y):
    """The front surface of the head at (x, y)."""
    near = head[(np.abs(head[:, 0] - x) < 0.006) & (np.abs(head[:, 1] - y) < 0.006) & (head[:, 2] > 0.03)]
    if not len(near):
        near = head[np.argsort((head[:, 0] - x) ** 2 + (head[:, 1] - y) ** 2)[:3]]
    return np.array([x, y, near[:, 2].max()])


def to_uv(points, positions, uv):
    out = []
    for q in points:
        d = np.sum((positions - q) ** 2, axis=1)
        k = np.argsort(d)[:4]
        # Only neighbours on the same UV island as the nearest vertex.
        k = k[np.linalg.norm(uv[k] - uv[k[0]], axis=1) < 0.02]
        w = 1 / np.maximum(np.sqrt(d[k]), 1e-5)
        out.append((uv[k] * w[:, None]).sum(0) / w.sum())
    return np.array(out)


def strokes(marks, eye_y, eye_x):
    """The lines, as polylines of (x, y) on the face, each with a weight 0..1 and a width."""
    lines = []
    brow = eye_y + 0.024
    for i, dy in enumerate((0.014, 0.026, 0.038)):
        y = brow + dy
        xs = np.linspace(-0.042 + i * 0.004, 0.042 - i * 0.004, 12)
        lines.append(([(x, y + 0.004 * np.cos(x / 0.045 * np.pi / 2) - 0.004) for x in xs], 0.7 - i * 0.12, 3))
    for side in (-1, 1):
        lines.append(([(side * 0.006, brow - 0.004), (side * 0.005, brow + 0.012)], 0.8, 3))
        corner = (side * (eye_x + 0.017), eye_y - 0.001)
        for angle in (-28, -6, 18):
            a = np.radians(angle)
            lines.append(([corner, (corner[0] + side * 0.013 * np.cos(a), corner[1] + 0.013 * np.sin(a))], 0.75, 2))
        bag = [(side * (eye_x - 0.012 + t * 0.026), eye_y - 0.013 - 0.004 * np.sin(t * np.pi)) for t in np.linspace(0, 1, 8)]
        lines.append((bag, 0.65, 3))
        nose = marks.nose
        fold = [(side * (0.017 + 0.014 * t), nose[1] - 0.004 - 0.042 * t) for t in np.linspace(0, 1, 9)]
        lines.append((fold, 0.85, 4))
        mouth = marks.mouth
        lines.append(([(side * 0.024, mouth[1] - 0.004), (side * 0.027, mouth[1] - 0.024)], 0.7, 3))
    return lines


def paint(positions, uv, faces_mask, marks, eyes, size):
    head = positions[faces_mask]
    head_uv = uv[faces_mask]
    image = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(image)
    eye_y = eyes[1]
    eye_x = abs(eyes[0])
    for line, weight, width in strokes(marks, eye_y, eye_x):
        pts = []
        for a, b in zip(line[:-1], line[1:]):
            for t in np.linspace(0, 1, 6, endpoint=False):
                pts.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
        pts.append(line[-1])
        surface = np.array([surface_point(head, x, y) for x, y in pts])
        coords = to_uv(surface, head, head_uv) * size
        draw.line([tuple(c) for c in coords], fill=int(255 * weight), width=width, joint="curve")
    image = image.filter(ImageFilter.GaussianBlur(2.2))
    return 255 - np.asarray(image, dtype=np.float64)
