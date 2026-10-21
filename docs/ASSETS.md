
## Artlist credits (AI generation)

Artlist's AI credits (16,500 a month, renewing on 21 November 2026) are shared by four games:
Flavius, Expedition, FutureBall and goodgarden. Each game's cap is **4,125 credits this month**.

Rules for this repo's sessions:
- Get the exact price of every generation first, and generate only with the user's go-ahead.
- Log every spend below.
- Before each batch, check the shared balance. If spending would leave less than the other games'
  remaining caps, stop and ask the user.
- Searching catalogue music and sound effects works; downloading them is not on the AI Suite plan.
- No AI concept images (the user's rule, 2026-10-21). For image-to-3D, write the user a short photo
  brief (object, view, details that must show); the user sends a free photo with a clear background.
  Upload it with upload_image (no credits), then use Hyper3D Rodin Fast (300 credits). Record each
  photo's source and licence; a photo of unknown source is prototype-only until cleared.

| Date | What | Model | Credits | Expedition total |
|---|---|---|---|---|
| 2026-10-21 | Concept images: capstan, ship's bell, gun on its carriage (pilot) | Nano Banana 2 | 270 | 270 / 4,125 |
| 2026-10-21 | 3D models of the three concepts (pilot) | Hyper3D Rodin v2.5 Fast | 900 | 1,170 / 4,125 |
| 2026-10-21 | 3D models of the market stall and the galley firebox, from the user's reference sheets (upload free) | Hyper3D Rodin v2.5 Fast | 600 | 1,770 / 4,125 |

Pilot notes (2026-10-21):
- The plan is AI Suite: catalogue sound effects cannot be downloaded on it (`subscription_required`).
- This cloud environment cannot download Artlist files: `cms-toolkit-artifacts.artlist.io`,
  `ai-toolkit-generations.imgix.net` and `mcp.artlist.io` are refused by the network policy.
  The user then allowed those hosts and the pilot went ahead. (`freesound.org` still refuses, so
  the line, yard and bell sounds wait.)
- The pilot props are in `game/Assets/Props/` (capstan, bell, gun), made by
  `tools/props/import_prop.py` from the Rodin GLBs; sources and settings in `tools/props/props.json`.
  Each went from about 19,500 textured triangles to 700-900 flat-shaded ones in five palette tones.

Reference photos used:
- `stall` and `galley` come from multi-view reference sheets the user sent on 2026-10-21. One view
  was cropped from each (the stall's 3/4 view, the firebox's front). The source and licence of those
  sheets are not recorded, so treat both props as **prototype-only** until the source is cleared.
