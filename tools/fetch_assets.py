#!/usr/bin/env python3
"""Fetches the game's CC0 assets into game/Assets and records where each came from.

The list is game/Assets/assets.json: Poly Haven textures by ID (at 1K, colour, OpenGL normal and
roughness maps) and Kenney packs by URL with the files to keep. Everything is CC0 (public domain);
the script still writes game/Assets/CREDITS.md naming authors and sources, as a courtesy and so the
provenance of every file is on record (D-021). Re-running skips files already present.

    python3 tools/fetch_assets.py
"""
import io
import json
import pathlib
import sys
import urllib.request
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
ASSETS = ROOT / "game" / "Assets"
MAPS = {"Diffuse": "diff", "nor_gl": "nor_gl", "Rough": "rough"}


def get(url):
    request = urllib.request.Request(url, headers={"User-Agent": "the-expedition-asset-fetch/1"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def polyhaven(entry, credits):
    asset = entry["id"]
    info = json.loads(get(f"https://api.polyhaven.com/info/{asset}"))
    files = json.loads(get(f"https://api.polyhaven.com/files/{asset}"))
    folder = ASSETS / "Textures" / asset
    folder.mkdir(parents=True, exist_ok=True)
    for key, suffix in MAPS.items():
        target = folder / f"{asset}_{suffix}_1k.jpg"
        if not target.exists():
            target.write_bytes(get(files[key]["1k"]["jpg"]["url"]))
            print("fetched", target.relative_to(ROOT))
    size = [round(d / 1000, 2) for d in info["dimensions"]]
    credits.append(f"| {info['name']} | {entry['use']} | {', '.join(info['authors'])} | "
                   f"https://polyhaven.com/a/{asset} | CC0 | `Textures/{asset}/` ({size[0]}×{size[1]} m) |")


def kenney(entry, credits):
    folder = ASSETS / entry["folder"]
    wanted = entry["keep"]
    if all((folder / pathlib.Path(name).name).exists() for name in wanted):
        pass
    else:
        folder.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(io.BytesIO(get(entry["url"]))) as archive:
            names = archive.namelist()
            for name in wanted:
                match = next((n for n in names if n.endswith(name)), None)
                if match is None:
                    sys.exit(f"{entry['name']}: no {name} in the pack")
                (folder / pathlib.Path(name).name).write_bytes(archive.read(match))
                print("fetched", (folder / pathlib.Path(name).name).relative_to(ROOT))
    credits.append(f"| {entry['name']} | {entry['use']} | Kenney | {entry['page']} | CC0 | `{entry['folder']}/` ({len(wanted)} files) |")


def stack_arrays(manifest):
    """Stacks the scenery textures into one image per map, for a Texture2DArray: one material for
    plaster, stone and timber, so built scenery stays one draw call per chunk."""
    from PIL import Image
    layers = [e["id"] for e in manifest if e.get("layer") is not None]
    layers.sort(key=lambda i: next(e["layer"] for e in manifest if e.get("id") == i))
    folder = ASSETS / "Textures" / "scenery_array"
    folder.mkdir(parents=True, exist_ok=True)
    for suffix in MAPS.values():
        images = [Image.open(ASSETS / "Textures" / i / f"{i}_{suffix}_1k.jpg").convert("RGB").resize((1024, 1024)) for i in layers]
        sheet = Image.new("RGB", (1024, 1024 * len(images)))
        for n, image in enumerate(images):
            sheet.paste(image, (0, 1024 * n))
        sheet.save(folder / f"scenery_{suffix}.jpg", quality=92)
    print("stacked", ", ".join(layers))


def main():
    manifest = json.loads((ASSETS / "assets.json").read_text())
    credits = []
    for entry in manifest:
        {"polyhaven": polyhaven, "kenney": kenney}[entry["source"]](entry, credits)

    stack_arrays(manifest)
    (ASSETS / "CREDITS.md").write_text(
        "# Asset credits\n\n"
        "Every asset here is CC0 (public domain): no attribution is required, but it is given. Fetched by\n"
        "`tools/fetch_assets.py` from the list in `assets.json`; nothing here was generated or altered\n"
        "except by the game's shaders at run time, and `Textures/scenery_array/`, which only stacks the\n"
        "layered textures (plaster, stone, timber) into one image per map.\n\n"
        "| Asset | Used for | Author | Source | Licence | Files |\n|---|---|---|---|---|---|\n"
        + "\n".join(credits) + "\n")


if __name__ == "__main__":
    main()
