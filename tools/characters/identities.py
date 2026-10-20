#!/usr/bin/env python3
"""Writes the identity records of the port's named people (C-2, CHARACTER_ART §2, §37).

    python3 tools/characters/identities.py [--force] [person.gil ...]

Every recruitable person and every named adult of La Bocana gets a record, so none of them is drawn
as a disposable figure. Each record below is authored from the person's own description in
data/port/bocana.json (their look, occupation, status, age and origin). The garments are built from
a small wardrobe of period pieces, and their cuts (hems, necklines, the cap's brow) are measured on
the base body of their sex, so the same piece fits a man's body or a woman's. Records already on disk
are left alone unless --force is given: once a person has been seen, they are not rerolled (§40).
The children (Lucía, Tito) are not here: the base bodies are adults, and a child is not a small adult.
"""
import json
import pathlib
import sys

import numpy as np

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import body as bodylib  # noqa: E402
import build  # noqa: E402

OUT = build.OUT
BERNIS = {"source": "Carmen Bernis, Indumentaria española en tiempos de Carlos V (1962)", "region": "Castile/Andalusia",
          "dates": "c. 1500–1530", "confidence": "medium"}
MALLAINA = {"source": "Pablo E. Pérez-Mallaína, Spain's Men of the Sea (1998)", "region": "Andalusian ports",
            "dates": "16th c.", "confidence": "medium"}


def anchors(sex):
    """Heights and reaches on the base body that garment cuts are given against."""
    doc, skeleton = build.load_base(sex)
    world = skeleton.world()
    at = {n: world[skeleton.index(n)][:3, 3] for n in ("pelvis", "neck_01", "calf_l", "foot_l", "lowerarm_l", "hand_l")}
    for node, mesh, material, a, faces in doc.primitives():
        if material.startswith("MI_Superhero"):
            region, _ = bodylib.regions(skeleton, a["JOINTS_0"].astype(np.int64), a["WEIGHTS_0"].astype(np.float64))
            marks = bodylib.Landmarks(a["POSITION"].astype(np.float64), region == "head")
    p, neck, knee, foot = at["pelvis"][1], at["neck_01"][1], at["calf_l"][1], at["foot_l"][1]
    r = lambda v: round(float(v), 3)
    return {
        "bottom": r(p - 0.07), "waist": r(p + 0.12), "hip": r(p - 0.02), "knee": r(knee - 0.07), "midcalf": r(knee - 0.2),
        "ankle": r(foot + 0.04), "hose": r(foot + 0.004), "shoe": r(foot + 0.03), "collarFront": r(neck - 0.03),
        "collarBack": r(neck + 0.04), "top": r(neck + 0.08), "elbow": r(at["lowerarm_l"][0] + 0.007),
        "wrist": r(at["hand_l"][0] - 0.016), "brow": r(marks.brow[1]), "legTop": r(p + 0.1),
    }


# ---------------------------------------------------------------- the wardrobe

def hose(a, colour, top=None, wear=0.4):
    return {"id": "garment_wool_hose_01", "layer": "lower", "shape": "shell", "leg": [a["hose"], top or a["knee"] + 0.08],
            "material": "wool", "colour": colour, "thickness": 0.003, "wear": wear}


def shirt(a, colour="linen.unbleached", sleeve="wrist", wear=0.45, loose=0.03, low=0.0):
    rolled = sleeve == "elbow"
    return {"id": "garment_linen_shirt_01", "layer": "base", "shape": "shell", "torso": [a["bottom"], a["top"]],
            "collar": [a["collarFront"] - low, a["collarBack"]], "sleeve": a["elbow"] if rolled else a["wrist"], "material": "linen",
            "colour": colour, "thickness": 0.008, "loose": loose, "drape": 0.9, "band": 0.007 if rolled else 0.004,
            "bandWidth": 0.04 if rolled else 0.02, "wear": wear}


def breeches(a, colour, material="wool", loose=0.022, hem="knee", wear=0.55, iid="garment_wool_breeches_01"):
    return {"id": iid, "layer": "lower", "shape": "shell", "torso": [a["bottom"], a["waist"] + 0.01], "leg": [a[hem], a["legTop"]],
            "material": material, "colour": colour, "thickness": 0.011, "loose": loose, "band": 0.004, "bandWidth": 0.03, "wear": wear}


def doublet(a, colour, material="wool", sleeve=True, wear=0.5, skirt=True, iid="garment_wool_doublet_01"):
    pieces = [{"id": iid, "layer": "outer", "shape": "shell", "torso": [a["bottom"] + 0.07, a["top"]],
               "collar": [a["collarFront"] + 0.02, a["collarBack"] + 0.01], "material": material, "colour": colour,
               "thickness": 0.014, "loose": 0.006, "drape": 0.8, "wear": wear}]
    if sleeve:
        pieces[0].update({"sleeve": a["wrist"] - 0.02, "band": 0.004, "bandWidth": 0.025})
    if skirt:
        pieces.append({"id": iid + "_skirt", "layer": "outer", "shape": "skirt", "top": a["waist"] - 0.07, "bottom": a["hip"] - 0.1,
                       "flare": 0.35, "material": material, "colour": colour, "thickness": 0.022, "wear": wear})
    return pieces


def jerkin(a, colour, material="leather", wear=0.7):
    return {"id": "garment_leather_jerkin_01", "layer": "outer", "shape": "shell", "torso": [a["bottom"] + 0.04, a["top"]],
            "collar": [a["collarFront"] - 0.02, a["collarBack"]], "material": material, "colour": colour, "thickness": 0.02,
            "loose": 0.01, "drape": 1.0, "wear": wear}


def gown(a, colour, hem="midcalf", material="wool", sleeve=True, wear=0.3, open_front=0.14, iid="garment_wool_gown_01"):
    """The long outer garment of townsmen, clerks, merchants and priests (ropa, sotana)."""
    top = {"id": iid, "layer": "outer", "shape": "shell", "torso": [a["bottom"] + 0.07, a["top"]],
           "collar": [a["collarFront"] - open_front, a["collarBack"] + 0.005], "material": material, "colour": colour,
           "thickness": 0.024, "loose": 0.045, "drape": 1.0, "band": 0.008, "bandWidth": 0.04, "wear": wear}
    if sleeve:
        top["sleeve"] = a["wrist"] - 0.06
    return [top, {"id": iid + "_skirt", "layer": "outer", "shape": "skirt", "top": a["waist"] - 0.07, "bottom": a[hem],
                  "flare": 0.6, "material": material, "colour": colour, "thickness": 0.034, "wear": wear}]


def belt(a, colour="leather.dark", material="leather"):
    return {"id": "garment_leather_belt_01", "layer": "outer", "shape": "shell", "torso": [a["waist"] - 0.035, a["waist"] + 0.01],
            "material": material, "colour": colour, "thickness": 0.03, "hides": False, "wear": 0.5}


def shoes(a, colour="leather.dark", wear=0.6):
    return {"id": "garment_leather_shoes_01", "layer": "footwear", "shape": "shell", "feet": a["shoe"], "material": "leather",
            "colour": colour, "thickness": 0.008, "wear": wear}


def apron(a, colour, material="leather", bottom="knee", iid="garment_apron_01"):
    return {"id": iid, "layer": "outer", "shape": "skirt", "arc": 62, "top": a["waist"] - 0.01, "bottom": a[bottom] + 0.02,
            "flare": 0.25, "material": material, "colour": colour, "thickness": 0.032, "wear": 0.75}


def cap(a, colour, material="wool", deep=0.0, iid="garment_wool_cap_01"):
    return {"id": iid, "layer": "headwear", "shape": "shell", "head": [a["brow"] + 0.022 - deep, a["brow"] - 0.023 - deep],
            "material": material, "colour": colour, "thickness": 0.018, "band": 0.008, "bandWidth": 0.03, "wear": 0.5}


def toca(a, colour="linen.bleached", iid="garment_linen_toca_01"):
    """A woman's headcloth: over the crown and down the back of the head to the nape."""
    return {"id": iid, "layer": "headwear", "shape": "shell", "head": [a["brow"] + 0.015, a["brow"] - 0.09],
            "material": "linen", "colour": colour, "thickness": 0.016, "band": 0.005, "bandWidth": 0.02, "wear": 0.4}


def bodice(a, colour, material="wool", wear=0.5):
    return {"id": "garment_wool_bodice_01", "layer": "outer", "shape": "shell", "torso": [a["waist"] - 0.08, a["top"]],
            "collar": [a["collarFront"] - 0.06, a["collarBack"]], "material": material, "colour": colour, "thickness": 0.014,
            "loose": 0.004, "drape": 0.9, "wear": wear}


def saya(a, colour, material="wool", hem="ankle", wear=0.5):
    """A woman's long skirt, from the waist to the ankle."""
    return {"id": "garment_wool_saya_01", "layer": "outer", "shape": "skirt", "top": a["waist"] - 0.03, "bottom": a[hem],
            "flare": 0.75, "material": material, "colour": colour, "thickness": 0.03, "wear": wear}


def hat(brim, crown, colour="felt.black", material="felt", droop=0.01, raise_=0.012):
    return [{"id": "acc_hat_brim_01", "shape": "brim", "radius": brim, "droop": droop, "material": material, "colour": colour, "raise": raise_},
            {"id": "acc_hat_crown_01", "shape": "crown", "radius": 0.1, "topRadius": 0.095, "height": crown, "material": material, "colour": colour}]


def flatcap(colour="felt.black", material="felt"):
    return [{"id": "acc_gorra_brim_01", "shape": "brim", "radius": 0.13, "droop": 0.0, "material": material, "colour": colour, "raise": 0.02},
            {"id": "acc_gorra_crown_01", "shape": "crown", "radius": 0.103, "topRadius": 0.106, "height": 0.03, "material": material, "colour": colour}]


def hair(*pieces):
    return [dict(p) for p in pieces]


def flatten(items):
    out = []
    for item in items:
        out.extend(item if isinstance(item, list) else [item])
    return out


# ---------------------------------------------------------------- the people

def people(m, f):
    """Every record, from the person's description. m and f are the male and female anchors."""
    r = {}

    r["person.leonor"] = dict(sex="Female", age=26, apparent=27, height=162, build="slight",
        posture={"stoop": 0.0, "headForward": 0.1, "shoulders": 0.1},
        face={"width": -0.2, "jaw": -0.3, "chin": 0.2, "nose": -0.1, "noseLength": 0.2, "brow": -0.2, "cheeks": -0.2, "cheekbones": 0.5, "mouth": -0.2},
        skin="#c39c80", weathering=0.15, eyes="eyes.darkbrown",
        hair=hair({"mesh": "Hair_Buns", "colour": "hair.black"}, {"mesh": "Eyebrows_Female", "colour": "hair.black"}),
        clothing=flatten([shirt(f, "linen.bleached"), saya(f, "wool.black", wear=0.2), bodice(f, "wool.black", wear=0.2),
                          toca(f, "wool.black"), shoes(f)]),
        accessories=[{"id": "equip_company_ledger_01", "shape": "pouch", "side": "left", "height": f["waist"] + 0.05, "size": [0.04, 0.22, 0.16], "material": "leather", "colour": "leather.brown"}],
        walk="walk_formal", work="interact",
        features=["a young widow in mourning black, black headcloth", "the Company's ledger under her arm", "a fine, composed face; she does not smile easily"],
        research=[("saya and corpiño in mourning black, the widow's toca", BERNIS)])

    r["person.tome"] = dict(sex="Male", age=30, apparent=33, height=186, build="heavy",
        posture={"stoop": 0.15, "headForward": 0.3, "shoulders": 0.4},
        face={"width": 0.6, "jaw": 1.0, "chin": 0.3, "nose": 0.5, "noseBend": 0.6, "noseBridge": 0.5, "brow": 1.0, "cheeks": 0.3, "ears": 0.4},
        skin="#a47a5c", weathering=0.55, eyes="eyes.darkbrown",
        hair=hair({"mesh": "Hair_Buzzed", "colour": "hair.black", "grey": 0.05}, {"mesh": "Hair_Beard", "trim": 0.7, "colour": "hair.black"}, {"mesh": "Eyebrows_Regular", "colour": "hair.black"}),
        clothing=flatten([shirt(m, "linen.grey", sleeve="elbow", wear=0.9, loose=0.02), breeches(m, "wool.grey", wear=0.9)]),
        accessories=[], walk="walk", work="push", walk_speed=0.9,
        features=["a big man, broad-jawed, heavy-browed, a nose broken more than once", "barefoot and ragged from the prison", "irons on him until the pardon is signed"],
        research=[("prisoners pardoned to serve at sea", MALLAINA)])

    r["person.ximena"] = dict(sex="Female", age=33, apparent=36, height=166, build="wiry",
        posture={"stoop": 0.1, "headForward": 0.2, "shoulders": 0.0},
        face={"width": -0.3, "jaw": 0.2, "chin": 0.3, "nose": 0.3, "noseLength": 0.3, "noseBridge": 0.4, "brow": 0.3, "cheeks": -0.6, "cheekbones": 0.8, "mouth": -0.1},
        skin="#9a6e52", weathering=0.7, eyes="eyes.hazel",
        hair=hair({"mesh": "Hair_Long", "bottom": f["collarBack"] - 0.05, "colour": "hair.darkbrown", "grey": 0.05}, {"mesh": "Eyebrows_Female", "colour": "hair.darkbrown"}),
        clothing=flatten([shirt(f, "linen.unbleached", wear=0.6), saya(f, "wool.russet", hem="midcalf", wear=0.7), bodice(f, "wool.brown", wear=0.6),
                          apron(f, "linen.grey", "linen", "midcalf"), toca(f, "linen.unbleached")]),
        accessories=[], walk="walk", work="harvest", walk_speed=1.05,
        features=["lean and sun-darkened, hollow-cheeked: years castaway on the far coast", "a tattooed wrist she keeps covered with her sleeve", "a lagoon shell on a thong"],
        research=[("fishwives' saya, apron and headcloth", BERNIS)])

    r["person.gil"] = dict(sex="Male", age=40, apparent=42, height=171, build="stocky",
        posture={"stoop": 0.15, "headForward": 0.1, "shoulders": 0.2},
        face={"width": 0.4, "jaw": 0.6, "chin": -0.2, "nose": 0.4, "noseLength": -0.2, "brow": 0.3, "cheeks": 0.5, "ears": 0.3, "sag": 0.2},
        skin="#a8785a", weathering=0.55, eyes="eyes.brown",
        hair=hair({"mesh": "Hair_SimpleParted", "colour": "hair.brown", "grey": 0.15}, {"mesh": "Hair_Beard", "trim": 0.8, "colour": "hair.brown", "grey": 0.1}, {"mesh": "Eyebrows_Regular", "colour": "hair.brown"}),
        clothing=flatten([hose(m, "wool.brown"), shirt(m, "linen.unbleached", sleeve="elbow", wear=0.6), breeches(m, "wool.tawny"),
                          apron(m, "leather.tan"), belt(m), cap(m, "wool.grey"), shoes(m)]),
        accessories=[], walk="walk", work="chop",
        equipment=[{"id": "equip_cooper_mallet_01", "shape": "mallet", "socket": "hand_r", "length": 0.42, "handle": "ash", "head": "oak", "when": ["Hammering", "Working"]}],
        features=["forearms like hams, sleeves rolled", "a round, cheerful face and a cropped beard", "leather apron, a mallet at work"],
        research=[("coopers' dress and the cooper aboard", MALLAINA)])

    r["person.nuno"] = dict(sex="Male", age=50, apparent=52, height=168, build="heavy",
        posture={"stoop": 0.1, "headForward": 0.2, "shoulders": 0.1},
        face={"width": 0.8, "jaw": 0.4, "chin": -0.4, "nose": 0.5, "noseLength": -0.3, "brow": 0.0, "cheeks": 1.0, "mouth": 0.4, "ears": 0.2, "sag": 0.7},
        skin="#b08467", weathering=0.45, eyes="eyes.brown",
        hair=hair({"mesh": "Hair_Buzzed", "recede": 0.7, "crown": 0.6, "colour": "hair.darkbrown", "grey": 0.4}, {"mesh": "Eyebrows_Regular", "colour": "hair.darkbrown", "grey": 0.3}),
        clothing=flatten([hose(m, "wool.grey"), shirt(m, "linen.unbleached", sleeve="elbow", wear=0.7), breeches(m, "wool.brown"),
                          apron(m, "linen.unbleached", "linen", "knee", "garment_linen_apron_01"), shoes(m)]),
        accessories=[], walk="walk", work="harvest", walk_speed=0.85,
        features=["round and bald, clean-shaven, a heavy jowl", "a stained linen apron", "smells of onions and smoke"],
        research=[("ship's cooks and tavern servants", MALLAINA)])

    r["person.wat"] = dict(sex="Male", age=35, apparent=36, height=178, build="average",
        posture={"stoop": 0.1, "headForward": 0.1, "shoulders": 0.1},
        face={"width": 0.1, "jaw": 0.5, "chin": 0.4, "nose": 0.2, "noseLength": 0.4, "brow": 0.4, "cheeks": -0.2, "cheekbones": 0.4, "mouth": -0.2, "ears": 0.5},
        skin="#d0a58c", weathering=0.6, eyes="eyes.blue",
        hair=hair({"mesh": "Hair_SimpleParted", "colour": "#b39a6a", "grey": 0.0}, {"mesh": "Hair_Beard", "trim": 0.6, "colour": "#a88c5e"}, {"mesh": "Eyebrows_Regular", "colour": "#a88c5e"}),
        clothing=flatten([shirt(m, "linen.unbleached", wear=0.6), breeches(m, "linen.grey", "linen", loose=0.035, iid="garment_linen_slops_01"),
                          jerkin(m, "wool.grey", "wool", 0.6), cap(m, "wool.woad"), shoes(m, "leather.brown")]),
        accessories=[{"id": "equip_sheath_knife_01", "shape": "knife", "side": "right", "height": m["waist"] - 0.07, "material": "leather", "colour": "leather.dark"}],
        walk="walk", work="push",
        features=["fair-haired and fair-skinned, sunburnt red", "a Brennish seaman's grey wool jerkin and a blue cap", "long-faced, big-eared"],
        research=[("northern seamen's slops and woollen jerkins", MALLAINA)])

    r["person.pascual"] = dict(sex="Male", age=19, apparent=18, height=174, build="slight",
        posture={"stoop": -0.1, "headForward": -0.1, "shoulders": -0.1},
        face={"width": -0.1, "jaw": -0.4, "chin": -0.2, "nose": -0.3, "noseLength": -0.2, "brow": -0.4, "cheeks": 0.4, "mouth": 0.1, "ears": 0.3},
        skin="#bb8f71", weathering=0.15, eyes="eyes.brown",
        hair=hair({"mesh": "Hair_SimpleParted", "colour": "hair.darkbrown"}, {"mesh": "Eyebrows_Regular", "colour": "hair.darkbrown"}),
        clothing=flatten([shirt(m, "linen.bleached", wear=0.05), breeches(m, "linen.bleached", "linen", loose=0.035, wear=0.05, iid="garment_linen_slops_01"),
                          cap(m, "wool.madder"), shoes(m, "leather.tan", 0.1)]),
        accessories=[], walk="walk", work="push", walk_speed=1.1,
        features=["eager, boyish, beardless", "sea clothes too clean and too new: borrowed", "a red cap he keeps touching"],
        research=[("ship's boys and young seamen", MALLAINA)])

    r["person.ulloa"] = dict(sex="Male", age=44, apparent=46, height=176, build="average",
        posture={"stoop": -0.3, "headForward": -0.2, "shoulders": -0.2},
        face={"width": -0.1, "jaw": 0.4, "chin": 0.7, "chinForward": 0.5, "nose": 0.6, "noseLength": 0.7, "noseBridge": 0.9, "brow": 0.5, "cheeks": -0.4, "cheekbones": 0.6, "mouth": -0.5, "sag": 0.3},
        skin="#b88d70", weathering=0.45, eyes="eyes.grey",
        hair=hair({"mesh": "Hair_SimpleParted", "colour": "hair.black", "grey": 0.3}, {"mesh": "Hair_Beard", "trim": 0.75, "colour": "hair.black", "grey": 0.35}, {"mesh": "Eyebrows_Regular", "colour": "hair.black", "grey": 0.2}),
        clothing=flatten([hose(m, "wool.black", m["legTop"]), shirt(m, "linen.bleached", wear=0.2), doublet(m, "wool.madder", wear=0.4),
                          gown(m, "wool.black", hem="knee", sleeve=False, open_front=0.2, wear=0.45, iid="garment_wool_capa_01"), shoes(m)]),
        accessories=flatten([flatcap("felt.black"), [{"id": "equip_sword_01", "shape": "tool", "side": "left", "height": m["waist"] - 0.02, "material": "metal", "colour": "iron"}]]),
        walk="walk_formal", work="talk",
        features=["a hidalgo's long, hawk-nosed face and pointed beard, greying", "a good black cloak over a madder doublet", "a sword he wears like a habit; no servant"],
        research=[("hidalgo dress: capa, jubón, gorra, sword", BERNIS)])

    r["person.corbalan"] = dict(sex="Male", age=55, apparent=56, height=169, build="heavy",
        posture={"stoop": 0.2, "headForward": 0.2, "shoulders": 0.1},
        face={"width": 0.6, "jaw": 0.3, "chin": -0.3, "nose": 0.6, "noseLength": 0.1, "noseBridge": 0.3, "brow": 0.2, "cheeks": 0.8, "mouth": 0.2, "sag": 0.8},
        skin="#ad8064", weathering=0.5, eyes="eyes.brown",
        hair=hair({"mesh": "Hair_Buzzed", "recede": 0.8, "colour": "hair.grey", "grey": 0.7}, {"mesh": "Hair_Beard", "trim": 0.85, "colour": "hair.grey", "grey": 0.7}, {"mesh": "Eyebrows_Regular", "colour": "hair.grey", "grey": 0.6}),
        clothing=flatten([hose(m, "wool.brown"), shirt(m, "linen.unbleached", sleeve="elbow", wear=0.5), doublet(m, "wool.tawny", sleeve=False, skirt=False, wear=0.6),
                          breeches(m, "wool.brown"), apron(m, "leather.brown"), belt(m), shoes(m)]),
        accessories=[], walk="walk", work="interact", walk_speed=0.85,
        features=["stout and grey, a short grey beard", "a leather apron over a sleeveless tawny doublet", "a pencil behind his ear"],
        research=[("shopkeepers' and chandlers' working dress", BERNIS)])

    r["person.ines"] = dict(sex="Female", age=44, apparent=46, height=163, build="heavy",
        posture={"stoop": 0.1, "headForward": 0.1, "shoulders": -0.1},
        face={"width": 0.6, "jaw": 0.4, "chin": 0.0, "nose": 0.3, "noseLength": -0.1, "brow": 0.2, "cheeks": 0.8, "mouth": 0.3, "sag": 0.6},
        skin="#b6886a", weathering=0.45, eyes="eyes.darkbrown",
        hair=hair({"mesh": "Hair_Buns", "colour": "hair.darkbrown", "grey": 0.3}, {"mesh": "Eyebrows_Female", "colour": "hair.darkbrown", "grey": 0.2}),
        clothing=flatten([shirt(f, "linen.unbleached", sleeve="elbow", wear=0.5), saya(f, "wool.woad", wear=0.6), bodice(f, "wool.brown", wear=0.6),
                          apron(f, "linen.bleached", "linen", "midcalf"), toca(f, "linen.bleached")]),
        accessories=[{"id": "acc_keys_01", "shape": "pouch", "side": "right", "height": f["waist"] - 0.04, "size": [0.06, 0.1, 0.03], "material": "metal", "colour": "iron"}],
        walk="walk", work="harvest", walk_speed=0.9,
        features=["broad and strong-armed, sleeves rolled", "a woad-blue saya, white apron and headcloth", "keys at her belt; hears everything"],
        research=[("women's dress of the tavern and street: saya, corpiño, toca, apron", BERNIS)])

    r["person.catalina"] = dict(sex="Female", age=38, apparent=40, height=160, build="stocky",
        posture={"stoop": 0.05, "headForward": 0.2, "shoulders": -0.1},
        face={"width": 0.4, "jaw": 0.5, "chin": 0.3, "nose": 0.4, "noseLength": 0.1, "brow": 0.3, "cheeks": 0.4, "mouth": 0.6, "sag": 0.3},
        skin="#a47657", weathering=0.65, eyes="eyes.brown",
        hair=hair({"mesh": "Hair_Buns", "colour": "hair.black", "grey": 0.15}, {"mesh": "Eyebrows_Female", "colour": "hair.black"}),
        clothing=flatten([shirt(f, "linen.grey", sleeve="elbow", wear=0.8), saya(f, "wool.madder", hem="midcalf", wear=0.7), bodice(f, "wool.black", wear=0.7),
                          apron(f, "leather.tan", "leather", "midcalf"), toca(f, "linen.unbleached")]),
        accessories=[], walk="walk", work="harvest",
        features=["loud, wide-mouthed, weathered", "a madder-red skirt and a fish-stained leather apron", "scales on her sleeves"],
        research=[("fishwives' dress", BERNIS)])

    r["person.anselmo"] = dict(sex="Male", age=63, apparent=66, height=164, build="slight",
        posture={"stoop": 0.6, "headForward": 0.5, "shoulders": 0.3},
        face={"width": -0.2, "jaw": -0.2, "chin": 0.1, "nose": 0.5, "noseLength": 0.6, "brow": 0.4, "cheeks": -0.7, "cheekbones": 0.4, "ears": 0.8, "sag": 1.0},
        skin="#b38a6e", weathering=0.7, eyes="eyes.grey",
        hair=hair({"mesh": "Hair_Buzzed", "recede": 1.0, "crown": 1.0, "colour": "hair.white", "grey": 0.9}, {"mesh": "Eyebrows_Regular", "colour": "hair.white", "grey": 0.9}),
        clothing=flatten([shirt(m, "linen.grey"), gown(m, "wool.brown", hem="ankle", wear=0.85, open_front=0.0, iid="garment_wool_cassock_01"), shoes(m)]),
        accessories=[{"id": "acc_skullcap_brim_01", "shape": "crown", "radius": 0.085, "topRadius": 0.06, "height": -0.03, "material": "wool", "colour": "wool.black"}],
        walk="walk", work="kneel_work", walk_speed=0.75,
        features=["old, bent, bald, big-eared", "a brown cassock patched at the elbows and hem", "a black skullcap"],
        research=[("secular clergy's sotana and bonete", BERNIS)])

    r["person.clerk_chamber"] = dict(sex="Male", age=48, apparent=50, height=177, build="slight",
        posture={"stoop": 0.35, "headForward": 0.4, "shoulders": 0.3},
        face={"width": -0.5, "jaw": -0.4, "chin": 0.4, "nose": 0.5, "noseLength": 0.8, "noseBridge": 0.3, "brow": 0.2, "cheeks": -0.8, "cheekbones": 0.6, "mouth": -0.6, "ears": 0.2, "sag": 0.4},
        skin="#c09a80", weathering=0.25, eyes="eyes.darkbrown",
        hair=hair({"mesh": "Hair_SimpleParted", "colour": "hair.black", "grey": 0.35}, {"mesh": "Eyebrows_Regular", "colour": "hair.black", "grey": 0.3}),
        clothing=flatten([hose(m, "wool.black", m["legTop"]), shirt(m, "linen.bleached"), doublet(m, "wool.black", skirt=False),
                          gown(m, "wool.black", hem="midcalf", wear=0.35), shoes(m)]),
        accessories=flatcap("felt.black"), walk="walk_formal", work="interact", walk_speed=0.9,
        features=["thin, long-nosed, clean-shaven, a pinched mouth", "the Chamber's black gown and flat cap", "ink-black fingers"],
        research=[("officials of the Casa de la Contratación", BERNIS)])

    r["person.hob"] = dict(sex="Male", age=42, apparent=43, height=175, build="stocky",
        posture={"stoop": -0.2, "headForward": 0.0, "shoulders": -0.2},
        face={"width": 0.5, "jaw": 0.5, "chin": 0.0, "nose": 0.5, "noseLength": 0.0, "noseBridge": 0.2, "brow": 0.3, "cheeks": 0.6, "mouth": 0.4, "sag": 0.3},
        skin="#d4a68c", weathering=0.55, eyes="eyes.blue",
        hair=hair({"mesh": "Hair_SimpleParted", "colour": "#8f4626", "grey": 0.1}, {"mesh": "Hair_Beard", "trim": 0.0, "colour": "#8f4626", "grey": 0.1}, {"mesh": "Eyebrows_Regular", "colour": "#8f4626"}),
        clothing=flatten([hose(m, "wool.woad", m["legTop"]), shirt(m, "linen.unbleached"), doublet(m, "wool.madder", wear=0.4),
                          gown(m, "wool.brown", hem="knee", sleeve=False, open_front=0.22, wear=0.5, iid="garment_wool_coat_01"), belt(m), shoes(m, "leather.brown")]),
        accessories=flatten([hat(0.15, 0.04, "felt.brown"), [{"id": "acc_leather_purse_01", "shape": "pouch", "side": "left", "height": m["waist"] - 0.03, "material": "leather", "colour": "leather.tan"}]]),
        walk="walk", work="talk",
        features=["a full red beard, ruddy and fair", "a madder doublet under an open brown coat; too many rings", "a brown felt hat"],
        research=[("northern merchants abroad", BERNIS)])

    r["person.grilo"] = dict(sex="Male", age=51, apparent=53, height=170, build="average",
        posture={"stoop": 0.2, "headForward": 0.3, "shoulders": 0.1},
        face={"width": 0.0, "jaw": -0.2, "chin": -0.3, "nose": 0.8, "noseLength": 0.6, "noseBend": 0.2, "noseBridge": 0.8, "brow": 0.4, "cheeks": 0.2, "mouth": -0.6, "sag": 0.6},
        skin="#c29a7e", weathering=0.3, eyes="eyes.darkbrown",
        hair=hair({"mesh": "Hair_Long", "bottom": m["collarBack"], "colour": "hair.grey", "grey": 0.6}, {"mesh": "Eyebrows_Regular", "colour": "hair.grey", "grey": 0.5}),
        clothing=flatten([hose(m, "wool.black", m["legTop"]), shirt(m, "linen.bleached"), doublet(m, "velvet.black", "velvet", skirt=False, wear=0.2),
                          gown(m, "wool.tawny", hem="ankle", wear=0.3, open_front=0.18),
                          {"id": "garment_fur_collar_01", "layer": "outer", "shape": "shell", "torso": [m["collarFront"] - 0.14, m["top"]], "collar": [m["collarFront"] - 0.02, m["collarBack"] + 0.01],
                           "sleeve": 0.24, "material": "felt", "colour": "#4b3424", "thickness": 0.045, "loose": 0.02, "drape": 1.0, "hides": False, "wear": 0.2}, shoes(m)]),
        accessories=flatcap("velvet.black", "velvet"), walk="walk_formal", work="talk",
        features=["a long hooked nose and a small hard mouth, clean-shaven", "grey hair cut at the jaw in the Italian way", "a tawny gown with a fur collar"],
        research=[("Genoese merchants' gowns and fur collars", {"source": "Rosita Levi Pisetzky, Storia del costume in Italia vol. 3 (1966)", "region": "Liguria", "dates": "c. 1500–1525", "confidence": "medium"})])

    def guard(pid, age, height, build, face, skin, hairp, lantern=False, features=()):
        kit = flatten([hose(m, "wool.madder", m["legTop"]), shirt(m, "linen.unbleached"), doublet(m, "wool.woad", wear=0.5),
                       breeches(m, "wool.madder", loose=0.012), belt(m),
                       {"id": "garment_iron_morion_01", "layer": "headwear", "shape": "shell", "head": [m["brow"] + 0.02, m["brow"] - 0.01],
                        "material": "metal", "colour": "iron", "thickness": 0.024, "band": 0.012, "bandWidth": 0.02, "wear": 0.4}, shoes(m)])
        return dict(sex="Male", age=age, apparent=age, height=height, build=build, posture={"stoop": 0.1, "headForward": 0.2, "shoulders": 0.1},
                    face=face, skin=skin, weathering=0.45, eyes="eyes.brown", hair=hairp, clothing=kit,
                    accessories=[{"id": "equip_guard_sword_01", "shape": "tool", "side": "left", "height": m["waist"] - 0.02, "material": "metal", "colour": "iron"}],
                    walk="walk", work="lantern" if lantern else "fold_arms", features=list(features),
                    research=[("town guards: the city's colours, an iron cap, a halberd", BERNIS)])

    r["person.mateo"] = guard("person.mateo", 33, 175, "average",
        {"width": 0.2, "jaw": 0.3, "nose": 0.2, "brow": 0.2, "cheeks": 0.3, "mouth": -0.1}, "#a97b5d",
        hair({"mesh": "Hair_Beard", "trim": 0.5, "colour": "hair.darkbrown"}, {"mesh": "Eyebrows_Regular", "colour": "hair.darkbrown"}),
        features=["a bored face, a trimmed dark beard", "the town's madder and woad, an iron cap"])
    r["person.lope"] = guard("person.lope", 28, 172, "slight",
        {"width": -0.4, "jaw": -0.3, "nose": 0.3, "noseLength": 0.4, "cheeks": -0.6, "cheekbones": 0.4, "mouth": -0.2}, "#b48b6f",
        hair({"mesh": "Eyebrows_Regular", "colour": "hair.brown"}), lantern=True,
        features=["thin and pale from the night watch, a cough", "clean-shaven, hollow-cheeked", "the town's colours, an iron cap, a lantern"])

    r["person.bastian"] = dict(sex="Male", age=26, apparent=25, height=180, build="heavy",
        posture={"stoop": 0.1, "headForward": 0.1, "shoulders": 0.0},
        face={"width": 0.3, "jaw": 0.6, "chin": 0.2, "nose": 0.0, "brow": 0.3, "cheeks": 0.2, "ears": 0.4},
        skin="#9c6f51", weathering=0.5, eyes="eyes.darkbrown",
        hair=hair({"mesh": "Hair_Buzzed", "colour": "hair.black"}, {"mesh": "Eyebrows_Regular", "colour": "hair.black"}),
        clothing=flatten([shirt(m, "linen.grey", sleeve="elbow", wear=0.7, loose=0.015), breeches(m, "wool.natural", wear=0.7)]),
        accessories=[], walk="walk", work="push",
        features=["young, all shoulders, shaven-headed", "barefoot, sleeves rolled to the shoulder"],
        research=[("dock labourers", MALLAINA)])

    r["person.hernando"] = dict(sex="Male", age=40, apparent=44, height=172, build="wiry",
        posture={"stoop": 0.3, "headForward": 0.3, "shoulders": 0.3},
        face={"width": -0.1, "jaw": 0.3, "chin": 0.2, "nose": 0.4, "noseBend": -0.4, "brow": 0.5, "cheeks": -0.6, "cheekbones": 0.6, "sag": 0.4},
        skin="#8f6247", weathering=0.85, eyes="eyes.darkbrown",
        hair=hair({"mesh": "Hair_SimpleParted", "colour": "hair.black", "grey": 0.3}, {"mesh": "Hair_Beard", "trim": 0.4, "colour": "hair.black", "grey": 0.3}, {"mesh": "Eyebrows_Regular", "colour": "hair.black", "grey": 0.2}),
        clothing=flatten([shirt(m, "linen.unbleached", sleeve="elbow", wear=0.8), breeches(m, "linen.grey", "linen", loose=0.03, wear=0.8, iid="garment_linen_slops_01"),
                          cap(m, "wool.madder"), belt(m)]),
        accessories=[], walk="walk", work="push",
        features=["burnt dark by the far coast, gaunt", "the Company's tattoo on his forearm", "a red cap and slops: still dressed for the sea"],
        research=[("returning seamen of the Carrera", MALLAINA)])

    r["person.paco"] = dict(sex="Male", age=45, apparent=48, height=167, build="stocky",
        posture={"stoop": 0.3, "headForward": 0.2, "shoulders": 0.2},
        face={"width": 0.4, "jaw": 0.3, "chin": -0.2, "nose": 0.6, "noseLength": -0.2, "brow": 0.1, "cheeks": 0.5, "mouth": 0.2, "sag": 0.5},
        skin="#a87b5e", weathering=0.7, eyes="eyes.brown",
        hair=hair({"mesh": "Hair_Buzzed", "colour": "hair.brown", "grey": 0.3}, {"mesh": "Hair_Beard", "trim": 0.3, "colour": "hair.brown", "grey": 0.3}, {"mesh": "Eyebrows_Regular", "colour": "hair.brown", "grey": 0.2}),
        clothing=flatten([hose(m, "wool.grey"), shirt(m, "linen.unbleached", wear=0.7), jerkin(m, "wool.brown", "wool", 0.7), breeches(m, "wool.brown"),
                          shoes(m, "leather.brown")]),
        accessories=flatten([hat(0.19, 0.035, "straw", "straw", droop=0.02)]),
        walk="walk", work="push", walk_speed=0.9,
        features=["a weathered, bulb-nosed carter with a grizzled beard", "a broad straw hat against the sun", "talks to his mule"],
        research=[("carters and muleteers", BERNIS)])

    r["person.benito"] = dict(sex="Male", age=47, apparent=49, height=173, build="heavy",
        posture={"stoop": 0.2, "headForward": 0.3, "shoulders": 0.1},
        face={"width": 0.5, "jaw": 0.7, "chin": 0.2, "nose": 0.5, "noseBend": 0.3, "brow": 0.8, "cheeks": 0.3, "ears": 0.3, "sag": 0.5},
        skin="#9d7155", weathering=0.6, eyes="eyes.darkbrown",
        hair=hair({"mesh": "Hair_Buzzed", "recede": 0.5, "colour": "hair.black", "grey": 0.3}, {"mesh": "Hair_Beard", "trim": 0.65, "colour": "hair.black", "grey": 0.25}, {"mesh": "Eyebrows_Regular", "colour": "hair.black", "grey": 0.2}),
        clothing=flatten([hose(m, "wool.grey"), shirt(m, "linen.grey", sleeve="elbow", wear=0.8), breeches(m, "wool.brown"),
                          apron(m, "leather.dark", "leather", "midcalf", "garment_leather_smith_apron_01"), shoes(m)]),
        accessories=[], walk="walk", work="chop",
        equipment=[{"id": "equip_smith_hammer_01", "shape": "mallet", "socket": "hand_r", "length": 0.38, "handle": "ash", "head": "iron", "when": ["Hammering", "Working"]}],
        features=["heavy-browed, singed forearms", "a long leather smith's apron", "deaf in one ear: turns his head to listen"],
        research=[("smiths' aprons", BERNIS)])

    r["person.marta"] = dict(sex="Female", age=35, apparent=37, height=168, build="wiry",
        posture={"stoop": 0.2, "headForward": 0.2, "shoulders": 0.1},
        face={"width": -0.1, "jaw": 0.3, "chin": 0.2, "nose": 0.3, "noseLength": 0.2, "brow": 0.2, "cheeks": -0.3, "cheekbones": 0.5, "mouth": 0.0},
        skin="#a97c5f", weathering=0.6, eyes="eyes.hazel",
        hair=hair({"mesh": "Hair_Long", "bottom": f["collarBack"] - 0.1, "colour": "hair.brown", "grey": 0.1}, {"mesh": "Eyebrows_Female", "colour": "hair.brown"}),
        clothing=flatten([shirt(f, "linen.unbleached", sleeve="elbow", wear=0.7), saya(f, "wool.tawny", hem="midcalf", wear=0.7), bodice(f, "wool.grey", wear=0.6),
                          apron(f, "linen.grey", "linen", "midcalf")]),
        accessories=[], walk="walk", work="push",
        features=["lean, strong-handed, hemp in her loose hair", "sleeves rolled, no headcloth at the ropewalk", "walks backwards spinning yarn"],
        research=[("ropemakers (cordeleros) at work", BERNIS)])

    r["person.simon"] = dict(sex="Male", age=66, apparent=70, height=166, build="slight",
        posture={"stoop": 0.8, "headForward": 0.6, "shoulders": 0.4},
        face={"width": -0.2, "jaw": -0.1, "chin": 0.3, "nose": 0.7, "noseLength": 0.7, "noseBend": 0.2, "brow": 0.5, "cheeks": -0.8, "cheekbones": 0.5, "ears": 0.9, "sag": 1.0},
        skin="#b08669", weathering=0.8, eyes="eyes.grey",
        hair=hair({"mesh": "Hair_SimpleParted", "recede": 0.6, "colour": "hair.white", "grey": 0.95}, {"mesh": "Hair_Beard", "trim": 0.5, "colour": "hair.white", "grey": 0.95}, {"mesh": "Eyebrows_Regular", "colour": "hair.white", "grey": 0.9}),
        clothing=flatten([hose(m, "wool.grey"), shirt(m, "linen.unbleached", wear=0.7), doublet(m, "wool.brown", wear=0.8), breeches(m, "wool.grey"), shoes(m, "leather.brown")]),
        accessories=flatten([hat(0.14, 0.05, "felt.brown")]), walk="walk", work="kneel_work", walk_speed=0.7,
        features=["very old and stooped, white-bearded", "a palm-thimble on his hand, needles stuck in his brown hat", "big ears, a long nose"],
        research=[("sailmakers", MALLAINA)])

    return r


def record(pid, spec, rules):
    person = next(p for p in rules["people"] if p["id"] == pid)
    looks = {"build": spec["build"], "posture": spec["posture"], "face": spec["face"], "skin": spec["skin"],
             "weathering": spec["weathering"], "eyes": spec["eyes"], "hair": spec["hair"]}
    return {
        "characterId": pid,
        "name": person["name"],
        "about": f"Visual identity record (CHARACTER_ART §2), written for C-2 from {pid}'s description in data/port/bocana.json: "
                 f"\"{person['look']}\" The simulation is the truth for who they are; this says how they look. Never reroll it (§40).",
        "sex": spec["sex"],
        "age": spec["age"],
        "apparentAge": spec["apparent"],
        "heightCm": spec["height"],
        "seed": abs(hash(pid)) % 10_000_000 if False else sum(ord(c) * (i + 1) for i, c in enumerate(pid)),
        "looks": looks,
        "distinctiveFeatures": spec["features"],
        "marks": spec.get("marks", []),
        "occupationVisuals": person["occupation"],
        "statusVisuals": person["status"],
        "clothing": spec["clothing"],
        "accessories": spec.get("accessories", []),
        "equipment": spec.get("equipment", []),
        "animation": {"walk": spec.get("walk", "walk"), "walkSpeed": spec.get("walk_speed", 1.0), "work": spec.get("work", "kneel_work"), "idle": "idle"},
        "research": [dict(item=item, **source) for item, source in spec.get("research", [])],
    }


def main(argv):
    force = "--force" in argv
    wanted = [a for a in argv if a.startswith("person.")]
    import re
    rules = json.loads(re.sub(r"^\s*//.*$", "", (build.ROOT / "data" / "port" / "bocana.json").read_text(), flags=re.M))
    specs = people(anchors("Male"), anchors("Female"))
    for pid, spec in specs.items():
        if wanted and pid not in wanted:
            continue
        path = OUT / pid / "identity.json"
        if path.exists() and not force:
            continue
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(record(pid, spec, rules), indent=1, ensure_ascii=False) + "\n")
        print("wrote", path.relative_to(build.ROOT))


if __name__ == "__main__":
    main(sys.argv[1:])
