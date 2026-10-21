# Asset credits

Every asset here is CC0 (public domain): no attribution is required, but it is given. Fetched by
`tools/fetch_assets.py` from the list in `assets.json`; nothing here was generated or altered
except by the game's shaders at run time; `Textures/scenery_array/`, which only stacks the
layered textures (plaster, stone, timber) into one image per map; and `Audio/ambience/`, Freesound
previews levelled to the same loudness with their ends crossfaded into their starts to loop; and
`Characters/`, built by `tools/characters/build.py` from the Quaternius packs (bodies reshaped,
clothing made on them, textures divided into one shared sheet, clips reduced to rotations).

| Asset | Used for | Author | Source | Licence | Files |
|---|---|---|---|---|---|
| Cobblestone Floor 08 | Cobbled streets and the quay | Rob Tuytel | https://polyhaven.com/a/cobblestone_floor_08 | CC0 | `Textures/cobblestone_floor_08/` (2.0×2.0 m) |
| Seaworn Sandstone Brick | Quay wall, terrace, vaults, stone buildings | Dimitrios Savva | https://polyhaven.com/a/seaworn_sandstone_brick | CC0 | `Textures/seaworn_sandstone_brick/` (1.69×1.69 m) |
| White Rough Plaster | Limewashed and ochre plaster walls | Rob Tuytel | https://polyhaven.com/a/white_rough_plaster | CC0 | `Textures/white_rough_plaster/` (1.0×1.0 m) |
| Weathered Brown Planks | Timber framing, stalls, fences, crates, piers | Dimitrios Savva, Rico Cilliers | https://polyhaven.com/a/weathered_brown_planks | CC0 | `Textures/weathered_brown_planks/` (1.8×1.8 m) |
| Clay Roof Tiles 02 | Roof tiles | Amal Kumar | https://polyhaven.com/a/clay_roof_tiles_02 | CC0 | `Textures/clay_roof_tiles_02/` (2.5×2.5 m) |
| Wood Floor Deck | The Garza's deck | Dimitrios Savva | https://polyhaven.com/a/wood_floor_deck | CC0 | `Textures/wood_floor_deck/` (1.8×1.8 m) |
| Impact Sounds | Mallets, planks, the anvil, the chapel bell, cups set down | Kenney | https://kenney.nl/assets/impact-sounds | CC0 | `Audio/impact/` (30 files) |
| RPG Audio | Coins at the stalls, the fishwife's knife, the adze, ship's timbers creaking | Kenney | https://kenney.nl/assets/rpg-audio | CC0 | `Audio/rpg/` (8 files) |
| Sea waves and rock splash, Málaga | Surf against the quay | soundandmelodies | https://freesound.org/s/784465/ | CC0 | `Audio/ambience/surf.ogg` |
| Outdoors Walla | The crowd at the market | IENBA | https://freesound.org/s/834339/ | CC0 | `Audio/ambience/market.ogg` |
| Small Town Bar 007 | Voices in the tavern | conleec | https://freesound.org/s/212094/ | CC0 | `Audio/ambience/tavern.ogg` |
| Strong Steady Wind | Wind in the streets | craigsmith | https://freesound.org/s/438878/ | CC0 | `Audio/ambience/wind.ogg` |
| Rain under a stone bridge | Rain | WilliamJMeyer | https://freesound.org/s/467169/ | CC0 | `Audio/ambience/rain.ogg` |
| Universal Base Characters [Standard] | The bodies, heads, hair and beards persistent people are built on (C-1) | Quaternius | https://quaternius.itch.io/universal-base-characters | CC0 | `Characters/*/model.glb`, `Characters/Shared/chr_atlas.png` |
| Universal Animation Library [Standard] | Walking, standing, sitting, kneeling work, pushing, lifting, talking | Quaternius | https://quaternius.itch.io/universal-animation-library | CC0 | `Characters/Shared/animations.glb` |
| Universal Animation Library 2 [Standard] | Carrying, chopping (the adze), folded arms, the rail, drinking, lying | Quaternius | https://quaternius.itch.io/universal-animation-library-2 | CC0 | `Characters/Shared/animations.glb` |
| dice roll_wooden table_1.wav | Two dice thrown on a tavern table | ekfink | https://freesound.org/s/235484/ | CC0 | `Audio/tavern/dice_roll.ogg` |
| Dice shaken in a wooden cup and rolled on a wooden board | Dice shaken in a cup and thrown | Oldome | https://freesound.org/s/854515/ | CC0 | `Audio/tavern/dice_cup.ogg` |
| Туман яром (Tuman yarom) | A man singing a folk song alone | ko_bx | https://freesound.org/s/369675/ | CC0 | `Audio/tavern/song_man.ogg` |
| hum.wav | A woman humming a cradle song | AlucardsBride | https://freesound.org/s/193938/ | CC0 | `Audio/tavern/song_cradle.ogg` |
| woman humming | A woman singing a working song | PSsoundproject | https://freesound.org/s/183565/ | CC0 | `Audio/tavern/song_woman.ogg` |
| Man singing in the kitchen / Hombre en la cocina cantando | The cook singing at his pots | GillianHonn | https://freesound.org/s/849096/ | CC0 | `Audio/tavern/song_kitchen.ogg` |
| Men chanting.wav | A room of men singing a chorus | BeeProductive | https://freesound.org/s/430022/ | CC0 | `Audio/tavern/song_chorus.ogg` |
| Board Game Icons | The screen buttons and the objectives checklist (recoloured white by tools/make_icons.py) | Kenney | https://kenney.nl/assets/board-game-icons | CC0 | `UI/icons/` (6 files) |
| Game Icons | The menu button and the checklist's ticks (recoloured by tools/make_icons.py) | Kenney | https://kenney.nl/assets/game-icons | CC0 | `UI/icons/` (3 files) |

## Generated props

`Props/` (capstan, bell, gun) were made with Artlist's AI tools on the project's AI Suite plan: a
concept image (Nano Banana 2), then image-to-3D (Hyper3D Rodin v2.5 Fast), then reduced and
recoloured into the ship's palette by `tools/props/import_prop.py`. Their sources are listed in
`tools/props/props.json`; check Artlist's licence terms before shipping them.
