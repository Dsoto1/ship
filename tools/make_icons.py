#!/usr/bin/env python3
"""Makes the interface icons in game/Assets/UI/icons from the Kenney packs (fetched by fetch_assets.py):
each turned into a white silhouette on transparency at 64 px, so the game can tint it. The ship's
anchor, which neither pack has, is drawn from the anchor glyph of DejaVu Sans (public domain glyphs,
Bitstream Vera licence). Run after fetch_assets.py: python3 tools/make_icons.py
"""
import pathlib
from PIL import Image, ImageDraw, ImageFont

ICONS = pathlib.Path(__file__).resolve().parent.parent / "game" / "Assets" / "UI" / "icons"
NAMES = {"pawns": "people", "flag_triangle": "expedition", "pouch": "cargo", "direction_n": "chart",
         "book_open": "chronicle", "notepad_write": "objectives", "menuList": "menu", "checkmark": "tick", "cross": "cross"}


def white(source):
    im = Image.open(source).convert("RGBA")
    # Kenney's board icons are dark shapes; the game icons are white: either way, the shape is the alpha.
    alpha = im.getchannel("A")
    out = Image.new("RGBA", im.size, (255, 255, 255, 0))
    out.putalpha(alpha)
    return out.resize((64, 64), Image.LANCZOS)


def main():
    for source, name in NAMES.items():
        path = ICONS / f"{source}.png"
        if path.exists():
            white(path).save(ICONS / f"{name}.png")
            if name != source:
                path.unlink()
    anchor = Image.new("RGBA", (256, 256), (255, 255, 255, 0))
    font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 220)
    ImageDraw.Draw(anchor).text((128, 128), "\u2693", font=font, fill=(255, 255, 255, 255), anchor="mm")
    anchor.crop(anchor.getbbox()).resize((56, 64), Image.LANCZOS).save(ICONS / "ship.png")
    print("icons:", sorted(p.name for p in ICONS.glob("*.png")))


if __name__ == "__main__":
    main()
