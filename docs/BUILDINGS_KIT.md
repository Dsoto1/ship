# Trial: houses from the Medieval Village MegaKit

The port's ordinary houses can be built from the modular pieces of Quaternius's Medieval Village MegaKit
(CC0, credited in `game/Assets/CREDITS.md`) instead of from boxes. It is a trial, off by default.

- `--kit`: every ordinary house (two storeys or more, not a room you can enter, not the sail loft) is a kit house.
- `--kit=alternate`: every other house, to compare the two side by side.

`game/Scripts/KitBuilding.cs` assembles a house from 2 m wall pieces (a 3 m storey), stretched a little to
fit the footprint: a round street door in the middle of the ground floor, windows with open shutters above,
an occasional balcony, dressed corners, the kit's nearest roof scaled to fit with brick gable ends, and a
chimney. The pieces are fetched by `tools/fetch_assets.py` (source `itch-kit`), which copies only the 35
pieces listed in `game/Assets/assets.json` and scales the textures to 1024 px. It also tones the wood, tile,
plaster and stone colours down (`tone`: saturation, brightness) to sit with the rest of the port. About 15 MB.

Known limits: the kit is a storybook style; its gables face the street, where the code-built houses have
theirs at the sides; stretched pieces skew the windows slightly on odd widths; rooms you enter stay code-built.
