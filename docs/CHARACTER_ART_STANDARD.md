# THE EXPEDITION — CHARACTER ART & ASSET STANDARD

**Status:** Authoritative character visual-production specification  
**Purpose:** Defines how persistent people are represented consistently across portraits, isometric 3D models, clothing, equipment, animation, condition, UI thumbnails and generated/source assets.

**Authority:** `The_Expedition_Art_Bible.md` remains the overall visual north star. This document specializes it for characters.

## 0. Visual North Star
> **Historically grounded, illustrated human beings with recognizable individual identities, readable isometric silhouettes, believable imperfection, and visible continuity across months of hardship.**

Characters should feel like people inhabiting circa 1500–1520, not fantasy adventurers in historical decoration.

## 1. One Character, Many Representations
**Identity Record → Portrait → UI Thumbnail → Isometric 3D Model → Clothing/Equipment → Animation → Condition Variants**

[REQUIRED] Every representation derives from one shared visual identity.
[DON'T] Independently randomize portrait and model.
[DON'T] Reroll identity when a phase changes.

## 2. Visual Identity Record
Suggested fields:
`characterId, age, apparentAge, heightClass, bodyBuild, proportions, posture, faceShape, jaw, nose, eyes, brows, ears, skin, hairColor, hairTexture, hairStyle, facialHair, distinctiveFeatures, teethNotes, scars, marks, occupationVisuals, statusVisuals, clothingSet, accessories, equipment, conditionState, stable generation metadata/seed`.

## 3. Identity Priority
1. face/head
2. hair/facial hair
3. body silhouette/posture
4. clothing silhouette
5. distinctive accessory/equipment
6. scars/marks

The same person remains recognizable after changing clothes.

## 4. Human Proportions
Believable anatomy, generally around 7–7.5 heads tall with natural variation. Slight readability emphasis may be applied to head, hands, hairstyle and equipment silhouette.

Avoid heroic fantasy, anime or chibi proportions.

## 5. Body Variation
Controlled variation in height, shoulders, torso/limbs, muscularity, body fat, posture and age stance.
[DON'T] Use one male and one female body scaled uniformly.
[DON'T] Turn occupations into caricatures.

## 6. Faces
Prioritize individuality over conventional attractiveness. Allow asymmetry, crooked noses, uneven/missing teeth where plausible, wrinkles, weathering, scars, receding hairlines, tired eyes and distinctive facial structure.

Defining structure may be modestly exaggerated for isometric readability while remaining plausible.

## 7. Age
Age appears through face, skin, hair, posture, hands and motion—not simply gray hair on a young face.

## 8. Skin
Responds to age, outdoor exposure, weather, injury, illness and fatigue. Avoid plastic perfection and uniform dirt overlays.

## 9. Hair and Facial Hair
Hair is a major isometric identity cue. Support texture, hairline, grooming and facial-hair variation. Styles require period/region research. Campaign grooming changes may be supported if practical.

## 10. Portrait Standard
Highest identity detail. Use a consistent framing family, readable naturalistic lighting, restrained background, clothing/equipment context and neutral default expression.

Portrait and 3D model must visibly be the same person.

## 11. Portrait Detail
May show skin texture, wrinkles, scars, imperfect teeth, hair detail, cloth wear and subtle expression. Avoid glamour/fantasy-card treatment.

## 12. Portrait Lighting and Background
Naturalistic painterly light that reveals facial planes and eyes without beauty-photography glamour. Background should be neutral or softly contextual and never compete with the face.

## 13. Portrait State Variants
Base neutral/attentive; optional injured, exhausted, sick, angry, relieved. Prefer parameterized state changes where technology allows.

Identity geometry/features must not change between states.

## 14. Isometric 3D Model
Prioritize silhouette, head identity, hair, clothing shape, posture, equipment and task animation. Do not spend geometry on details invisible at normal gameplay distance.

## 15. Detail Hierarchy
Conceptual identity fidelity:
- Portrait: 100%
- Close inspection: 70–80%
- Normal gameplay: 40–50%
- Distant/background: 10–20%

These are design concepts, not literal texture percentages.

## 16. Mesh Strategy
Prefer modular shared human topology where feasible: bodies, heads/morphs, hair, facial hair, garments, footwear, headwear, accessories, equipment. Avoid visible seams/clipping. Final budgets are [APPROVAL REQUIRED].

## 17. Face Construction
Controlled morph/parameter system may vary face width, jaw, chin, cheekbones, nose, eyes, brow, ears and age. Generated combinations must pass plausibility review.

## 18. Clothing System
Recommended stack:
`BODY → BASE GARMENT → LOWER GARMENT → OUTER LAYER → FOOTWEAR → HEADWEAR → OCCUPATIONAL ITEM → PERSONAL ACCESSORY → EQUIPMENT → CONDITION`

Not every character uses every layer.

## 19. Clothing Historical Rule
Historical authenticity determines **what exists**. Stylization determines **how clearly it reads**.

Garments must match date, origin, occupation, status, climate and context.
Avoid generic medieval asset assumptions, Hollywood pirates, Victorian clothing and later naval uniforms.

## 20. Clothing Identity
Material, cut, wear, layering, equipment, cleanliness and restrained color communicate occupation/status. Avoid MMO class-color coding.

## 21. Materials and Color
Clearly distinguish linen, wool, leather, metal, wood, rope and canvas. Avoid synthetic gloss. Use historically plausible colors/dyes informed by research; strong accents should be intentional.

## 22. Clothing Condition
Reusable states:
**Clean/Maintained → Used → Dirty → Wet → Damaged → Patched**

States may combine and should be driven by simulation/context where feasible.

## 23. Campaign Visual Continuity
Example:
- Port Mateo: relatively clean, intact work clothing.
- Day 26 at sea: worn clothing, fatigue, salt/wetness, grooming change if supported.
- After storm: bandage, damaged sleeve, soaked/exhausted.
- Settlement month 4: repaired/patched garments, healed scar, changed tools/equipment.

The player watches history accumulate without losing identity.

## 24. Injuries and Scars
Where practical represent bandages, sling, limp, guarded arm, bruising, persistent scars, restrained immediate blood/dirt, illness and exhaustion.

[REQUIRED] Visual/animation state must not contradict simulation. A disabled arm cannot perform unrestricted two-handed work.

## 25. Dirt and Wetness
Use localized contextual effects on hems, knees, hands, sleeves and work-contact areas. Wetness responds to rain/storm/water. Avoid uniform overlays.

## 26. Equipment
Visible tools, bags, maritime gear, weapons and kits should correspond to authoritative inventory when practical. Do not imply gameplay equipment the character does not possess.

## 27. Skeleton / Rig
Prefer shared humanoid rig supporting locomotion, work, carrying, ship motion, injury modifiers, conversation and settlement labor. Standardize attachment sockets for hands/body/task props.

## 28. Animation Philosophy
Communicate occupation, weight, contact, condition and task purpose. Avoid theatrical fantasy-hero motion.

## 29. Animation Layering
Concept:
`Base Action + Body/Age Modifier + Condition Modifier + Context`

Examples:
`Walk + OlderPosture + FatigueHigh`
`CarryCrate + InjuryLeftArm`
`IdleWorker + Rain`
`Hammer + FatigueModerate`

Exact engine implementation is [APPROVAL REQUIRED].

## 30. Personality in Motion
Small restrained variation may support temperament—cautious observation, impatient idle, confident/reserved posture. Traits remain primarily behavioral systems, not cartoon animations.

## 31. Work Animation Library
High priority:
carry crate/sack; two-person carry; roll barrel; push/pull cart; rope work; hammer; saw; inspect; load/unload; sit/lean; eat/drink; converse/gesture; climb ladder; pump; medical treatment; dig; chop; plant/harvest.

## 32. Interaction Alignment
Use semantic interaction points and standardized prop sockets. Hands/tools should contact convincingly at normal camera distance. IK is preferred where practical.

## 33. Ship Motion
Calm: minimal balance adjustment. Heavy sea: wider stance/cautious movement. Storm: stronger reaction while retaining gameplay readability.

## 34. Portrait / Model Matching
Validate same apparent age, face shape, nose, hair/hairline, facial hair, skin identity, distinctive marks, build and compatible clothing identity. Reject mismatches.

## 35. UI Thumbnail
Derive from portrait/identity with a recognizable face at small size. Prefer UI condition indicators rather than baking every state into thumbnail.

## 36. LOD
Reduce geometry, material complexity, hair complexity, secondary accessories and animation update frequency before removing defining identity cues. Numerical budgets/distances require profiling and approval.

## 37. Background Population
Atmospheric Tier-C people may use reduced identity/detail. Persistent/recruitable people must never use disposable identity logic.

## 38. Asset Structure and Naming
Recommended:
```
characters/
  chr_0042_mateo_alvarez/
    identity.json
    portraits/
    model/
      lods/
    clothing/
    equipment/
    materials/
    thumbnails/
```

Use stable character ID in metadata/filenames. Display names are not unique keys.

Shared examples:
`chr_body_male_lean_01`
`chr_hair_wavy_short_04`
`garment_linen_shirt_02`
`equip_carpenter_kit_01`
`anim_carry_crate_01`

Adapt exact syntax to repository conventions.

## 39. Research Metadata
Historically significant assets should record source/reference, date range, region, confidence and source/license where practical.

## 40. AI-Assisted Asset Generation
AI may assist concept exploration, portrait drafts, textures, clothing variations and reference synthesis.

[REQUIRED] Generated output remains draft until validated.
[REQUIRED] Persistent characters use structured identity constraints/stable metadata.
[REQUIRED] Never independently reroll a persistent face.
[REQUIRED] Historical details require validation.

## 41. AI Portrait Prompt Structure
Include:
1. project style,
2. persistent identity,
3. age/build,
4. facial structure,
5. hair/facial hair,
6. distinctive features,
7. occupation/status,
8. validated clothing,
9. condition,
10. framing,
11. lighting,
12. exclusions.

Example:
> Illustrated historical realism, early 16th century. Preserve exact identity: long narrow face, crooked nose, dark wavy hair, receding temples, short uneven beard, missing upper tooth, weathered complexion. Working carpenter of modest means. Historically researched clothing for approved region/date. Head-and-shoulders, restrained painterly background, natural directional light. Avoid fantasy, pirate costume, modern grooming, glamour portrait and perfect teeth.

## 42. Rejection Criteria
Reject fantasy armor, Hollywood pirate styling, Victorian/later uniforms, modern hairstyles/makeup/tailoring, unjustified jewelry, flawless cosmetic skin, uniformly perfect teeth, anime/chibi, heroic fantasy anatomy, unrelated equipment and inconsistent facial identity.

## 43. Generated 3D Validation
Check topology, deformation, rig compatibility, silhouette, material consistency, historical plausibility, clipping and LOD suitability. Never ship raw generated geometry without technical review.

## 44. Portrait Consistency Test
Multiple states of one character pass only if an unfamiliar reviewer can reasonably identify them as the same person without reading the name.

## 45. Human Variety
Cast should reflect historically plausible human variety for approved routes/regions. Research movement of people, ports, languages and communities.
[DON'T] Treat culture as costume palette or personality archetype.

## 46. Indigenous Character Art
Must be specific to approved community, region, date, material culture, clothing, hairstyle, adornment and context. No generic pan-Indigenous or fantasy "tribal" vocabulary. Apply the same individuality standards used for Europeans.

## 47. Women, Families, Ages
Where scenario/history includes them, apply equal research/individuality standards. Avoid glamour stereotypes. Children require age-appropriate proportions and cannot simply be scaled adults.

## 48. Visual Condition Interface
Simulation may expose tags:
`Wet, Dirty, Fatigued, InjuredLeftArm, LimpRightLeg, BandagedHead, Ill, Cold, RecentlyWorked`.

Rendering/animation reads these tags; it never owns health truth.

## 49. Performance Priority
Protect:
1. persistent identity,
2. readable silhouette,
3. face/hair identity,
4. task animation,
5. condition cues.

Reduce tiny accessories, hidden geometry, distant material complexity and secondary animation first.

## 50. Character Art Vertical Slice
Create six representative persistent characters:
1. experienced sailor,
2. carpenter,
3. navigator,
4. medical-capable character,
5. laborer/generalist,
6. officer/merchant/sponsor-context character.

For each validate:
- identity record,
- neutral portrait,
- 3D model,
- base clothing,
- equipment,
- thumbnail,
- walk/idle,
- one work animation,
- wet state,
- fatigue state,
- one injury state.

Do not generate hundreds of final people until this slice passes.

## 51. Visual Validation Scenes
A. Port roster — six portraits.
B. Port street — same six world models.
C. Storm — wet/fatigued/injured states.
D. Settlement months later — changed clothing/equipment/scars while retaining identity.

## 52. Acceptance Criteria
- shared identity across representations
- portrait/model clearly match
- plausible body variation
- recognizable faces and age
- historically reviewed clothing
- occupation reads without class-color coding
- condition changes preserve identity
- injuries affect visuals/animation
- visible equipment aligns with state
- work animation reads isometrically
- LOD preserves identity
- generated assets pass identity/history review
- stable-ID naming
- long-term changes remain recognizable

## 53. Agent Guardrails
Read Art Bible and Character Design & Simulation first. Audit existing pipeline before replacing it. Preserve persistent IDs. Research garments/equipment. Major rig/rendering changes and final budgets require approval.

## 54. Final Design Test
> If Mateo changes clothes, gets injured, becomes weathered, and reaches the settlement months later, will I immediately recognize Mateo?

> Does the portrait look like the person walking around the world?

> Can I infer occupation, age and condition without caricature?

> Does every visual detail plausibly belong to this person, place and period?

**The player should watch people change without watching them become different people.**
