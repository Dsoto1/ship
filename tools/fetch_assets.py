#!/usr/bin/env python3
"""Fetches the game's CC0 assets into game/Assets and records where each came from.

The list is game/Assets/assets.json: Poly Haven textures by ID (at 1K, colour, OpenGL normal and
roughness maps), Kenney packs by URL with the files to keep, Quaternius packs from itch.io (into
.cache/characters, for tools/characters/build.py), and Freesound recordings by ID (the
high-quality Ogg preview; the API needs FREESOUND_API_KEY unless a proxy supplies it). Everything is CC0 (public domain);
the script still writes game/Assets/CREDITS.md naming authors and sources, as a courtesy and so the
provenance of every file is on record (D-021). Re-running skips files already present.

    python3 tools/fetch_assets.py
"""
import io
import json
import os
import pathlib
import subprocess
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


def freesound(entry, credits):
    """One recorded bed, looped by the game. Refuses anything that is not CC0. The preview is kept in
    .cache/; the game's copy is levelled to -24 LUFS and its end crossfaded into its start (3 s), so
    the beds sit together and loop without a seam (needs ffmpeg)."""
    target = ASSETS / entry["folder"] / entry["file"]
    if not target.exists():
        raw = ROOT / ".cache" / "freesound" / f"{entry['id']}.ogg"
        if not raw.exists():
            key = os.environ.get("FREESOUND_API_KEY")
            url = f"https://freesound.org/apiv2/sounds/{entry['id']}/?fields=name,username,license,previews"
            info = json.loads(get(url + (f"&token={key}" if key else "")))
            if "publicdomain/zero" not in info["license"]:
                sys.exit(f"Freesound {entry['id']}: not CC0 ({info['license']})")
            raw.parent.mkdir(parents=True, exist_ok=True)
            raw.write_bytes(get(info["previews"]["preview-hq-ogg"]))
        fade = 3
        target.parent.mkdir(parents=True, exist_ok=True)
        if entry.get("oneshot"):
            # A sound played once (dice, a song): levelled, faded in and out, never looped.
            length = float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(raw)],
                                          capture_output=True, text=True, check=True).stdout.strip())
            subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(raw), "-af",
                            f"afade=t=in:d=0.05,afade=t=out:st={max(0.0, length - 0.4):.2f}:d=0.4,loudnorm=I=-20:TP=-2:LRA=20,aresample=44100",
                            "-c:a", "libvorbis", "-q:a", "5", str(target)], check=True)
            print("fetched", target.relative_to(ROOT))
            credits.append(f"| {entry['name']} | {entry['use']} | {entry['author']} | "
                           f"https://freesound.org/s/{entry['id']}/ | CC0 | `{entry['folder']}/{entry['file']}` |")
            return
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(raw), "-i", str(raw), "-filter_complex",
                        f"[0:a]atrim=start={fade},asetpts=PTS-STARTPTS[body];"
                        f"[1:a]atrim=end={fade},asetpts=PTS-STARTPTS[head];"
                        f"[body][head]acrossfade=d={fade}:c1=tri:c2=tri,loudnorm=I=-24:TP=-2:LRA=20,aresample=44100",
                        "-c:a", "libvorbis", "-q:a", "5", str(target)], check=True)
        print("fetched", target.relative_to(ROOT))
    credits.append(f"| {entry['name']} | {entry['use']} | {entry['author']} | "
                   f"https://freesound.org/s/{entry['id']}/ | CC0 | `{entry['folder']}/{entry['file']}` |")


def itch(entry, credits):
    """A free pack from itch.io, into .cache/characters/ (the sources are large and stay out of the
    repository; tools/characters/build.py makes the game's files from them). itch has no public API
    for free downloads, so this does what the page does: ask for a download page, then for the file
    marked as the pack's free edition. Refuses a pack whose licence file is not CC0."""
    import http.cookiejar
    import re
    import time
    import urllib.parse
    folder = ROOT / ".cache" / "characters"
    if not (folder / entry["folder"]).exists():
        jar = http.cookiejar.CookieJar()
        opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
        opener.addheaders = [("User-Agent", "the-expedition-asset-fetch/1")]
        page = entry["page"]
        token = re.search(r'csrf_token" value="([^"]+)"', opener.open(page).read().decode()).group(1)
        form = urllib.parse.urlencode({"csrf_token": token}).encode()
        listing = json.loads(opener.open(page + "/download_url", form).read())["url"]
        time.sleep(2)
        html = opener.open(listing).read().decode()
        uploads = dict(zip(re.findall(r'<strong title="([^"]+)" class="name"', html), re.findall(r'data-upload_id="(\d+)"', html)))
        upload = uploads.get(entry["file"]) or sys.exit(f"{entry['name']}: no {entry['file']} on {page}")
        time.sleep(2)
        request = urllib.request.Request(f"{page}/file/{upload}?source=view_game&as_props=1&after_download_lightbox=true", data=form)
        url = json.loads(opener.open(request).read())["url"]
        with zipfile.ZipFile(io.BytesIO(opener.open(url, timeout=600).read())) as archive:
            licence = next((n for n in archive.namelist() if "license" in n.lower()), None)
            if licence is None or "CC0" not in archive.read(licence).decode(errors="ignore"):
                sys.exit(f"{entry['name']}: not CC0")
            archive.extractall(folder)
        print("fetched", entry["name"], "into", folder.relative_to(ROOT))
    credits.append(f"| {entry['name']} | {entry['use']} | {entry['author']} | {entry['page']} | CC0 | {entry['files']} |")


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


# The props are not CC0 downloads: they were generated, so they are credited apart (tools/props).
PROPS_NOTE = (
    "\n## Generated props\n\n"
    "`Props/` (capstan, bell, gun) were made with Artlist's AI tools on the project's AI Suite plan: a\n"
    "concept image (Nano Banana 2), then image-to-3D (Hyper3D Rodin v2.5 Fast), then reduced and\n"
    "recoloured into the ship's palette by `tools/props/import_prop.py`. Their sources are listed in\n"
    "`tools/props/props.json`; check Artlist's licence terms before shipping them.\n")


def main():
    manifest = json.loads((ASSETS / "assets.json").read_text())
    credits = []
    for entry in manifest:
        {"polyhaven": polyhaven, "kenney": kenney, "freesound": freesound, "itch": itch}[entry["source"]](entry, credits)

    stack_arrays(manifest)
    (ASSETS / "CREDITS.md").write_text(
        "# Asset credits\n\n"
        "Every asset here is CC0 (public domain): no attribution is required, but it is given. Fetched by\n"
        "`tools/fetch_assets.py` from the list in `assets.json`; nothing here was generated or altered\n"
        "except by the game's shaders at run time; `Textures/scenery_array/`, which only stacks the\n"
        "layered textures (plaster, stone, timber) into one image per map; and `Audio/ambience/`, Freesound\n"
        "previews levelled to the same loudness with their ends crossfaded into their starts to loop; and\n"
        "`Characters/`, built by `tools/characters/build.py` from the Quaternius packs (bodies reshaped,\n"
        "clothing made on them, textures divided into one shared sheet, clips reduced to rotations).\n\n"
        "| Asset | Used for | Author | Source | Licence | Files |\n|---|---|---|---|---|---|\n"
        + "\n".join(credits) + "\n" + PROPS_NOTE)


if __name__ == "__main__":
    main()
