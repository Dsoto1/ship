# THE EXPEDITION — ART BIBLE

**Document Type:** Agent-Facing Visual Specification  
**Status:** Working Visual Authority  
**Target Era:** c. 1500–1520  
**Primary View:** Isometric / elevated gameplay camera  
**Art Direction:** Illustrated Historical Realism  

---

## 0. PURPOSE OF THIS DOCUMENT

This document defines the visual language of **The Expedition** for human artists, technical artists, designers, and autonomous development agents.

It is not merely a collection of inspirations. It establishes implementation constraints, visual priorities, research expectations, and acceptance criteria.

The companion document `The_Expedition_Agent_Handoff.md` defines the game's experience, systems, simulation, and development priorities. This document governs how those systems are visually represented.

### Priority Tags

- **[REQUIRED]** — Must be followed unless a documented technical impossibility prevents it.
- **[PREFERRED]** — Default direction. Deviate only for a clear gameplay, production, or technical reason.
- **[EXPERIMENTAL]** — Safe area for prototypes and alternatives.
- **[RESEARCH]** — Do not invent a definitive answer. Verify against credible historical references before implementation.
- **[APPROVAL REQUIRED]** — Do not make a permanent project-wide change without owner approval.
- **[DON'T]** — Explicitly prohibited direction.

### Conflict Resolution

**[REQUIRED]** Gameplay readability takes precedence over decorative historical accuracy when the two cannot coexist.

**[REQUIRED]** Historical authenticity determines what should plausibly exist; stylization determines how clearly it is presented.

**[REQUIRED]** If this Art Bible conflicts with the core game-design handoff on a nonvisual gameplay requirement, the core game-design handoff wins. Flag the conflict rather than silently rewriting either design.

---

# 1. VISUAL NORTH STAR

## Illustrated Historical Realism

**The Expedition should feel like a historical adventure painting transformed into an explorable miniature world.**

The visual target is grounded and researched but deliberately illustrated rather than photorealistic. Human anatomy, ships, architecture, clothing, tools, vegetation, and materials should be believable. Shapes, proportions, contrast, lighting, and detail are selectively exaggerated to remain legible from an isometric strategy-game camera.

The game should not look like generic medieval fantasy, a glossy modern AAA historical drama, or a cartoon. Its world should feel physical, weathered, handmade, dangerous, and inhabited.

### Emotional Targets

The visual presentation should reinforce:

- wonder at departure and discovery;
- attachment to individual people;
- vulnerability against the ocean and weather;
- the physical cost of a long expedition;
- relief when safety is found;
- melancholy when people, supplies, or places are lost;
- gradual transformation from expedition to settlement.

### Visual Hierarchy

**[REQUIRED]** Visual detail follows gameplay importance:

1. People and their readable identity.
2. The expedition's ship and immediate working environment.
3. Interactive structures, equipment, resources, and hazards.
4. Important terrain and navigation landmarks.
5. Decorative background scenery.

**Core rule:** Nothing should look more detailed than it is important.

---

# 2. NON-NEGOTIABLE ART PRINCIPLES

**[REQUIRED] People are the visual priority.** Characters are the persistent emotional center of the campaign. The player must be able to recognize important individuals repeatedly across port, voyage, shipwreck, and settlement phases.

**[REQUIRED] Historical forms, illustrated presentation.** Objects should be based on plausible period forms while their shapes and visual contrast may be simplified for gameplay.

**[REQUIRED] Readability before ornamental detail.** A beautifully researched object that cannot be understood at gameplay distance has failed its purpose.

**[REQUIRED] The world accumulates history.** Characters, ships, equipment, camps, and settlements should visibly change through wear, weather, injury, repair, construction, and time.

**[REQUIRED] Regional specificity.** European locations, Atlantic environments, American environments, and Indigenous communities must not be represented as interchangeable visual kits.

**[DON'T]** Generic medieval-fantasy visual shorthand.

**[DON'T]** High-fantasy armor, weapons, buildings, heraldry, or magical visual effects unless a future explicitly nonhistorical mode is separately approved.

**[DON'T]** Clean theme-park history where every object appears newly manufactured.

**[DON'T]** Photorealism as the target.

**[DON'T]** Chibi, anime, caricature, or exaggerated heroic-fantasy anatomy.

**[DON'T]** Excessive permanent outlines, glowing interactables, or MMO-style rarity colors as substitutes for environmental readability.

---

# 3. CAMERA & PRESENTATION

## Gameplay Camera

**[REQUIRED]** The primary world presentation is isometric/elevated and must support both direct character control and strategy/management interactions.

**[REQUIRED]** Art decisions must be evaluated at normal gameplay zoom before being approved based on close-up appearance.

**[PREFERRED]** Camera framing should make environments feel like detailed living dioramas without making characters resemble tiny anonymous tokens.

**[REQUIRED]** Interactive spaces need clean silhouettes and navigable negative space.

### Occlusion

**[REQUIRED]** Architecture, sails, masts, vegetation, and roofs must not routinely obscure selected characters or critical interactions.

**[PREFERRED]** Use contextual fading, cutaways, roof hiding, transparency, or camera-aware presentation instead of permanently removing visually important structures.

### Zoom

**[PREFERRED]** Support at least two useful visual contexts:

- gameplay distance for navigation and management;
- closer inspection for characters, ship condition, and environmental storytelling.

Close zoom is not permission to increase asset density until gameplay zoom becomes noisy.

---

# 4. CHARACTER ART

## 4.1 Character Goal

Every persistent crew member should feel like a particular person rather than a generated unit.

At normal gameplay distance, recognition should come primarily from silhouette, body shape, clothing mass, hair/headwear, equipment, posture, and color grouping. Facial detail supports recognition at closer distances and in portraits.

## 4.2 Proportions

**[REQUIRED]** Use believable human anatomy.

**[PREFERRED]** Target approximately 7–7.5 heads in total height.

**[PREFERRED]** Slightly enlarge or clarify heads, hands, hairstyles, tools, and major equipment where necessary for isometric readability.

**[DON'T]** 8+ head heroic superhero proportions as the default body.

**[DON'T]** Identical base bodies with only texture swaps.

## 4.3 Body Variation

**[REQUIRED]** Characters should vary meaningfully in:

- height;
- shoulder width;
- build/body mass;
- age;
- posture;
- facial structure;
- hair and facial hair;
- visible occupational wear.

**[PREFERRED]** Occupation and life history may influence physique without creating rigid class stereotypes. A laborer may plausibly carry different physical development from a clerk, but individuals should still vary.

## 4.4 Faces

**[REQUIRED]** Avoid uniform modern beauty standards.

Faces may include asymmetry, crooked noses, wrinkles, scars, missing or damaged teeth where appropriate, weathering, receding hairlines, uneven facial hair, and distinct cranial/facial shapes.

**[PREFERRED]** Important identifying features may be subtly exaggerated beyond strict realism so they remain recognizable.

**[REQUIRED]** Age should visibly matter.

## 4.5 Portraits

**[PREFERRED]** Character portraits may contain more detail and expression than world models while clearly depicting the same person.

Portraits should reinforce:

- identity;
- age;
- occupation/background;
- current health and condition where appropriate;
- accumulated history.

**[EXPERIMENTAL]** Portrait presentation may evolve with scars, illness, aging, clothing, or major campaign events.

## 4.6 Clothing

**[RESEARCH]** Clothing silhouettes, construction, textiles, footwear, hats, armor, and accessories should be appropriate to approximately 1500–1520 and the character's geographic/social background.

**[REQUIRED]** Do not use a generalized thousand-year "medieval" wardrobe.

Clothing can communicate:

- wealth;
- occupation;
- region/origin;
- shipboard role;
- social status;
- wear and repair;
- weather exposure.

**[PREFERRED]** Use period-plausible color variation rather than assigning gameplay classes a fixed color.

## 4.7 Condition & Wear

Characters should visually respond to their circumstances when technically feasible:

- wet clothing;
- mud/dirt;
- blood/injury;
- bandaging;
- exhaustion/posture;
- illness;
- repaired garments;
- equipment changes.

**[PREFERRED]** Prefer modular state overlays/material changes and animation states over unique bespoke models for every condition.

---

# 5. HISTORICAL CLOTHING, TOOLS & EQUIPMENT

**[RESEARCH]** Before introducing a major clothing family, armor type, weapon, navigation instrument, tool, ship component, or architectural feature, establish that it plausibly existed in the target place and period.

### Research Questions

For an asset, determine where relevant:

1. Did it exist c. 1500–1520?
2. In what region was it used?
3. Who could plausibly own/use it?
4. What materials and construction methods were typical?
5. What later-period visual shorthand should be avoided?

**[PREFERRED]** Record important research sources in asset notes or project documentation so later agents can understand why a design was chosen.

**[DON'T]** Add a recognizable historical object merely because it "looks old."

---

# 6. SHIPS

The ship is effectively the expedition's moving home and should receive unusually high visual attention.

## 6.1 Historical Basis

**[RESEARCH]** Ship type, hull form, rig, sail plan, deck equipment, steering method, cargo handling, boats, and living/work spaces must be appropriate to the chosen expedition origin and date.

**[APPROVAL REQUIRED]** Final hero ship type and major visual configuration.

## 6.2 Gameplay Readability

**[REQUIRED]** Deck work areas must remain understandable from the gameplay camera.

Important areas should be identifiable through shape and layout, not floating labels alone:

- helm/steering area;
- sails/rigging work zones;
- cargo access;
- pumps/bilge access;
- repair areas;
- cooking/food area where applicable;
- crew rest areas;
- officer/captain space where applicable.

## 6.3 Ship Condition

**[REQUIRED]** Ship damage must have visible consequences where practical.

Potential states include:

- torn or reefed sails;
- damaged rigging;
- patched hull/deck;
- accumulated water;
- damaged cargo;
- broken spars/components;
- emergency repairs;
- grime and weathering.

Repairs should often remain visually legible. The ship should accumulate a history rather than returning to pristine condition after every repair.

---

# 7. ENVIRONMENTS

## 7.1 European Port

Visual character: compressed, busy, human-made, noisy, economically active.

Use combinations of:

- timber and masonry buildings;
- workshops;
- warehouses;
- docks and piers;
- shipyards;
- cranes/hoists where historically appropriate;
- markets;
- inns/taverns;
- customs/administrative spaces;
- cargo and working clutter;
- animals, carts, workers, sailors, travelers.

**[REQUIRED]** The port should look like a functioning place where people already have jobs and lives, not a lobby built around the player.

## 7.2 Open Ocean

The ocean must not read as a static blue floor.

**[REQUIRED]** Sea presentation responds meaningfully to weather, wind, light, and time of day.

Desired visual states include:

- calm reflective water;
- directional wind texture;
- moderate chop;
- whitecaps;
- fog;
- heavy rain;
- severe storm seas;
- dawn/sunset reflections;
- dark night water.

**[PREFERRED]** Ocean state should visually communicate information the simulation is already producing rather than inventing disconnected decorative weather.

## 7.3 Landfall / Islands

Landfall should provide a dramatic visual contrast with long periods at sea.

**[REQUIRED]** Coastlines and vegetation reflect the selected geography rather than generic "tropical island" imagery.

**[PREFERRED]** Use landmarks, coast shapes, vegetation masses, water color, surf, and terrain to make locations visually memorable for navigation.

## 7.4 Americas

**[RESEARCH]** Select and document the actual geographic region before creating definitive architecture, vegetation, material culture, or Indigenous settlement assets.

**[REQUIRED]** The Americas must not be visually framed as an empty wilderness waiting for European ownership.

Land may show existing paths, agriculture, managed landscapes, settlements, travel routes, fishing activity, trade, territorial use, or other evidence of human presence appropriate to the region.

## 7.5 Indigenous Communities

**[REQUIRED]** Represent distinct peoples as communities with their own material culture, architecture, clothing, agriculture, tools, trade, social spaces, and objectives.

**[RESEARCH]** Visual representation must be specific to the chosen people, date, and region.

**[DON'T]** Create a reusable generic "Native village" kit intended to stand in for unrelated peoples.

**[DON'T]** Use visual coding that presents Indigenous people primarily as hostile encounter units.

## 7.6 Settlement Growth

The player's settlement should visibly evolve from expedition survival to durable habitation.

Stages should feel materially connected:

1. temporary landing/camp;
2. improvised shelter and salvage;
3. durable survival structures;
4. workshops/agriculture/storage;
5. established settlement.

**[REQUIRED]** Early structures should visibly use the resources and construction capabilities available to the expedition.

---

# 8. MATERIAL LANGUAGE

Materials should feel tactile but simplified enough to survive isometric viewing.

### Wood

Weathered, hand-worked, structurally readable. Avoid uniform procedural grain covering every surface.

### Stone

Regional and construction-specific. Use mass and edge variation more than noisy micro-detail.

### Metal

Restrained highlights; distinguish forged iron, steel, brass/bronze where relevant. Avoid chrome-like modern reflections.

### Cloth

Readable folds and silhouette first. Canvas and sailcloth should respond clearly to wind and wetness.

### Leather

Matte to modest sheen; worn edges and deformation where useful.

### Rope

Important rigging must remain visible without making every rope unrealistically thick. Prioritize gameplay-relevant lines.

### Water

Painterly-naturalistic rather than hyper-real simulation. Surface state, direction, depth, weather, and lighting are more important than expensive microphysics.

### Mud / Dirt

Used to ground characters and structures in lived environments. Avoid universal brown overlays.

### Vegetation

Strong massing and species/biome identity. Avoid high-frequency foliage noise that hides characters.

---

# 9. LIGHTING

## Lighting Philosophy

**[REQUIRED]** Lighting is naturalistic in source but theatrical in composition.

Lighting should communicate time, weather, danger, safety, and mood while preserving gameplay information.

### Dawn / Port Departure

Mist, long shadows, selective warmth, cool ambient air. Potential emotional association: anticipation and departure.

### Midday Ocean

Harder sunlight, bright canvas, strong directional shadows, glare and exposure without destroying visibility.

### Sunset

Warm directional light and longer reflections. Use sparingly enough that it remains emotionally meaningful.

### Night

Darkness should feel consequential, but essential interactions must remain usable. Lantern/fire pools can establish working spaces.

### Storm

Cool/diffuse ambient light, dramatic cloud cover, reduced visibility, occasional directional breaks where appropriate.

**[REQUIRED]** A storm affects the lighting of the entire ship/world, not merely a sky texture.

### Interiors

Lantern/fire illumination against dark timber and masonry. Preserve silhouettes and navigation routes.

**[DON'T]** Apply uniform cinematic darkness that makes strategy information unreadable.

---

# 10. WEATHER & ATMOSPHERE

Weather is both simulation feedback and visual storytelling.

**[REQUIRED]** Where possible, weather visuals should be driven by the same game state that affects navigation, ship performance, character condition, and tasks.

Relevant presentation systems may include:

- clouds;
- rain intensity/direction;
- fog/visibility;
- wind response in sails, clothing, vegetation, smoke;
- ocean state;
- surface wetness;
- puddles/runoff where appropriate;
- lightning where meteorologically appropriate;
- changing ambient light.

**[PREFERRED]** Build weather from composable layers rather than unique scenes for every condition.

---

# 11. COLOR

## General Direction

**[PREFERRED]** Use a grounded material palette with controlled moments of richer period color in clothing, flags, goods, interiors, vegetation, sky, and water.

The game should not be universally brown, gray, and desaturated simply because it is historical.

**[REQUIRED]** Gameplay state colors must remain distinguishable from natural environmental colors.

**[DON'T]** Use strong saturation everywhere.

**[DON'T]** Turn professions into rigid color-coded classes unless required by a specific UI abstraction.

### Environmental Contrast

European port: dense mixture of materials and human activity.  
Ocean: large simplified color masses and atmospheric variation.  
Landfall: stronger natural forms and regional vegetation.  
Settlement: increasing mixture of expedition-made geometry and local environment.

---

# 12. ANIMATION

## Animation Philosophy

Animation should communicate **weight, occupation, condition, and intention**.

**[REQUIRED]** Prioritize gameplay-readable actions over cinematic animation complexity.

Characters should visibly communicate major tasks such as:

- hauling;
- climbing/working rigging;
- steering;
- repairing;
- pumping;
- cooking;
- treating injury;
- fishing;
- carrying cargo;
- building;
- gathering;
- resting.

### Character State

**[PREFERRED]** Use locomotion/posture variations for exhaustion, injury, illness, age, and heavy loads where feasible.

### Personality

**[EXPERIMENTAL]** Small posture, idle, or gesture differences may reinforce traits without turning characters into caricatures.

### Ship Motion

**[REQUIRED]** Characters and shipboard objects should visually acknowledge ship movement sufficiently that the vessel feels afloat.

**[DON'T]** Pursue full physical simulation if a simpler authored/procedural response communicates the same state more clearly and cheaply.

---

# 13. VFX

VFX should communicate physical events and state changes, not overwhelm them.

Potential systems:

- surf/foam;
- wake;
- rain impacts;
- spray;
- smoke;
- fire;
- construction dust/debris;
- leaks/water ingress;
- blood/injury feedback;
- repair feedback;
- interaction highlights.

**[PREFERRED]** Keep effects painterly and integrated with the environment.

**[DON'T]** Use fantasy-like particles, oversized sparks, or exaggerated combat effects that contradict the grounded direction.

---

# 14. USER INTERFACE

## UI North Star

The interface should feel inspired by **the captain's working materials** while behaving like a modern strategy-game interface.

Historical presentation must never be an excuse for poor usability.

Possible visual vocabulary:

- charts/maps;
- manifests;
- crew records;
- contracts;
- letters;
- journals;
- ship diagrams;
- seals and annotations;
- inventory records.

**[REQUIRED]** Modern UX principles govern hierarchy, responsiveness, legibility, tooltips, filtering, comparison, and input feedback.

**[DON'T]** Cover every panel with distressed parchment texture.

**[DON'T]** Use difficult historical typefaces for body text.

**[PREFERRED]** Historical flavor should come from composition, framing, illustration, iconography, and selective materials rather than reducing readability.

## Character Records

The UI should reinforce persistent personal history.

A character record may eventually show:

- portrait;
- known skills and confidence;
- health/injuries;
- relationships;
- contract/role;
- important possessions;
- major history entries.

**[PREFERRED]** Significant events can accumulate as annotations or journal entries, visually turning a crew record into a history of that person.

---

# 15. VISUAL STORYTELLING & PERSISTENCE

The world should remember what happened.

Examples:

- a repaired sail remains patched;
- a crew member keeps a scar;
- clothing becomes repaired/replaced;
- salvaged ship timber appears in early settlement structures;
- graves or memorials remain;
- a camp becomes a settlement instead of disappearing between phases;
- damaged equipment is visibly repaired;
- repeated foot traffic can establish paths where technically reasonable.

**[PREFERRED]** Prefer persistent visual consequences for major simulation events over one-time cinematic effects.

---

# 16. TECHNICAL ART RULES

Exact budgets depend on engine and target hardware and should be established after technical profiling.

## General

**[REQUIRED]** Asset production must account for normal gameplay camera distance and expected on-screen population.

**[REQUIRED]** Reuse materials, modular components, atlases, instancing, LODs, and state overlays where they improve performance without making important characters visually interchangeable.

**[REQUIRED]** Establish a consistent world scale early and document it.

**[REQUIRED]** Test visual systems under realistic scenes containing characters, structures, props, weather, lighting, and UI simultaneously.

## Characters

**[PREFERRED]** Build characters from controlled modular systems that permit individual combinations of body, face, hair, clothing, equipment, wear, and condition.

**[REQUIRED]** Modularity must not create obvious random-generator incoherence.

## Textures & Materials

**[PREFERRED]** Favor deliberate medium-frequency detail visible from gameplay camera over expensive micro-detail visible only in extreme close-up.

## LOD

**[REQUIRED]** LOD strategy must preserve silhouette and identity before small surface detail.

## Performance

**[REQUIRED]** Do not select expensive visual techniques solely because they are more "AAA." Every major rendering feature must justify its cost in readability, atmosphere, or simulation feedback.

**[APPROVAL REQUIRED]** Project-wide renderer changes, major shader-stack changes, or art-pipeline changes that substantially increase minimum hardware requirements.

---

# 17. HISTORICAL RESEARCH RULES

Historical grounding is part of the visual identity, but this is a game rather than a museum reconstruction.

## Source Preference

**[PREFERRED]** Prioritize:

1. museum collections and object records;
2. archaeological/historical scholarship;
3. period artwork interpreted carefully;
4. academic or specialist publications;
5. reputable historical reconstructions;
6. secondary popular references for orientation only.

**[REQUIRED]** Treat modern reenactment imagery, films, television, other games, Pinterest boards, and AI-generated historical imagery as inspiration rather than authoritative evidence.

## Uncertainty

**[REQUIRED]** If evidence is uncertain, document the uncertainty and choose a plausible interpretation rather than claiming certainty.

## Anachronisms

**[REQUIRED]** Flag suspected anachronisms instead of silently accepting familiar imagery.

## Indigenous Representation

**[REQUIRED]** Research the specific community/region/date before producing definitive visual assets.

**[REQUIRED]** Material culture must not be collapsed across centuries or unrelated peoples for convenience without explicit design justification.

---

# 18. DO / DON'T REFERENCE

## DO

- Make characters identifiable by silhouette.
- Show age, work, weather, injury, and history.
- Research period-specific clothing and objects.
- Simplify shapes that become unreadable isometrically.
- Let the ship accumulate repairs and damage.
- Let weather transform the scene.
- Use dramatic natural lighting while retaining gameplay clarity.
- Make port spaces crowded and economically active.
- Make the ocean visually spacious and changeable.
- Make landfall visually significant.
- Base Indigenous environments on specific peoples and regions.
- Use UI that feels historically inspired but functions quickly.
- Evaluate assets at actual gameplay distance.

## DON'T

- Make everything brown because it is historical.
- Use generic "medieval" assets spanning unrelated centuries.
- Make every male character muscular and heroic.
- Make every character conventionally attractive.
- Use fantasy armor or weapons.
- Add Viking, pirate, conquistador, or later Age-of-Sail stereotypes without verifying date/context.
- Make the ocean a decorative blue plane.
- Hide gameplay information in realistic darkness.
- Solve interaction readability by permanently outlining everything.
- Build enormous texture detail the camera cannot show.
- Depict the Americas as empty land.
- Create a generic Indigenous faction/village art kit.
- Let AI-generated historical imagery become factual reference without validation.

---

# 19. AI-GENERATED ASSET RULES

AI tools may be useful for concept exploration, ideation, texture studies, portrait prototypes, variation studies, and reference composition.

**[REQUIRED]** AI-generated historical details are untrusted until validated.

**[REQUIRED]** Generated assets must be checked for:

- historical plausibility;
- anatomy;
- material consistency;
- perspective;
- repeated/impossible objects;
- weapon/tool function;
- clothing construction;
- ship/rigging logic;
- cultural specificity;
- consistency with established game assets.

**[DON'T]** Use an AI-generated image as proof that an object existed historically.

**[DON'T]** allow separate asset-generation sessions to establish conflicting visual styles.

**[PREFERRED]** Feed generators this Art Bible plus asset-specific constraints rather than relying on short style phrases such as "stylized historical game."

---

# 20. AGENT DECISION AUTHORITY

## Agent May Decide Independently

Provided the decision follows this document and existing technical constraints, the agent may normally decide:

- minor prop variations;
- modular asset reuse;
- LOD implementation details;
- small material adjustments;
- minor lighting tuning;
- optimization techniques that preserve appearance;
- prototype implementations of [EXPERIMENTAL] features.

## Agent Should Research Before Deciding

- historical clothing details;
- tools;
- armor/weapons;
- ship equipment;
- architectural details;
- regional vegetation;
- Indigenous material culture;
- historically specific signage/symbols;
- period navigation equipment.

## Approval Required

The project owner should approve before the agent permanently changes:

- overall art style;
- camera identity;
- character proportion philosophy;
- hero ship type;
- geographic/cultural setting;
- rendering approach with major hardware implications;
- UI visual language;
- global color philosophy;
- historical period;
- major departures from illustrated historical realism.

**[REQUIRED]** An agent must not silently reinterpret "stylized" into cartoon, low-poly, photoreal, or generic AAA realism.

---

# 21. ASSET ACCEPTANCE CHECKLISTS

## Character

Before approving a character or modular character set:

- [ ] Recognizable at gameplay distance.
- [ ] Believable human proportions.
- [ ] Does not rely on face texture alone for identity.
- [ ] Clothing is plausible for period/origin/role.
- [ ] No obvious fantasy or later-period elements.
- [ ] Body/age variation works with the broader population.
- [ ] Equipment has a plausible function.
- [ ] Materials remain readable without excessive detail.
- [ ] Condition/wear system can affect the character where required.
- [ ] Portrait/world representation can plausibly depict the same individual.

## Ship

- [ ] Historical basis documented.
- [ ] Silhouette fits chosen vessel type.
- [ ] Important work areas readable isometrically.
- [ ] Rigging is visually understandable rather than indiscriminate line noise.
- [ ] Damage states can be represented.
- [ ] Crew can navigate/work without constant occlusion.
- [ ] Materials and scale are consistent.
- [ ] Performance tested with crew and weather active.

## Environment

- [ ] Region identified.
- [ ] Architecture/vegetation appropriate to region and period.
- [ ] Navigation routes readable.
- [ ] Interactive objects distinguishable without permanent outlines.
- [ ] Background detail does not compete with characters.
- [ ] Weather/time-of-day states tested.
- [ ] Scene tells a plausible story of how people use it.

## Lighting

- [ ] Time of day readable.
- [ ] Weather state readable.
- [ ] Character silhouettes preserved.
- [ ] Important work surfaces visible.
- [ ] Mood supports current phase without hiding information.
- [ ] Performance acceptable in representative scene.

## UI

- [ ] Primary action obvious.
- [ ] Body text easily readable.
- [ ] Historical styling does not obstruct information.
- [ ] Important comparisons are fast.
- [ ] Icons are understandable.
- [ ] Character identity/history is reinforced.
- [ ] UI remains visually distinct from world gameplay-state colors.

---

# 22. REFERENCE GENERATION PROMPTS

These prompts are starting structures for concept generation, not replacements for historical research.

## 22.1 Character Concept

> Create concept art for a persistent character in **The Expedition**, an isometric historical survival and expedition strategy game set approximately 1500–1520. Use illustrated historical realism: believable anatomy and period-grounded clothing rendered with painterly simplification, strong silhouette, readable equipment, expressive but non-cartoon facial features, visible age and occupational wear. The character must remain recognizable from an elevated isometric gameplay camera. Avoid fantasy, generic medieval costume, modern beauty standards, heroic superhero anatomy, excessive ornament, and photorealism. Clothing and equipment should communicate occupation, social status, region, and personal history. Treat any uncertain historical details as requiring research rather than invention.

## 22.2 European Port Environment

> Design an isometric European port environment for **The Expedition**, c. 1500–1520, in illustrated historical realism. It is a functioning economic space rather than a decorative backdrop: docks, cargo, workers, sailors, merchants, workshops, warehouses, ship repair, animals, carts, inns, markets, and administrative activity. Use historically plausible construction and equipment appropriate to the specified region. Favor readable silhouettes, navigable negative space, painterly materials, atmospheric natural lighting, and strong environmental storytelling. Avoid fantasy-medieval architecture, theme-park cleanliness, empty plazas, photorealism, and excessive surface noise.

## 22.3 Ship Concept

> Design a historically grounded expedition vessel for **The Expedition**, c. 1500–1520, shown from an elevated isometric gameplay perspective. The ship is both transportation and a persistent working home for the crew. Deck zones, cargo access, steering, rigging work, repair areas, and crew movement must remain readable. Use illustrated historical realism with painterly materials and simplified visual hierarchy. Show plausible wear and the ability to accumulate torn sails, patched surfaces, damaged rigging, leaks, and improvised repairs. Do not invent rigging or equipment merely for visual drama; historically uncertain features require research.

## 22.4 Character Portrait

> Paint a character portrait for **The Expedition** that matches an isometric world character while allowing greater facial detail. Illustrated historical realism, c. 1500–1520. Emphasize individuality, age, weathering, occupation, personality, and current condition rather than conventional attractiveness. Use believable asymmetry and restrained exaggeration of identifying features. Clothing and accessories must correspond to the same character's world model. Avoid fantasy portrait conventions, glamour lighting, modern hairstyles, and generic medieval styling.

## 22.5 UI Concept

> Design a strategy-game UI for **The Expedition**, inspired by a captain's working materials from the early 16th century: charts, manifests, contracts, crew records, correspondence, journals, and ship diagrams. Historical inspiration should shape composition and visual texture while interaction follows modern usability standards. Prioritize legibility, hierarchy, filtering, comparison, tooltips, and quick decision-making. Avoid covering every panel in parchment, illegible historical fonts, excessive ornament, fantasy RPG framing, or skeuomorphism that slows interaction.

---

# 23. INITIAL VISUAL VALIDATION SCENES

Before producing large quantities of assets, build a small set of visual test scenes.

## Test A — Port Recruitment

Contains:

- captain;
- 8–12 visually varied NPCs;
- dock/market/workshop architecture;
- cargo and tools;
- morning or late-afternoon lighting;
- recruitment UI overlay.

Validate character recognition, crowd readability, materials, architecture, and UI/world coexistence.

## Test B — Ship at Sea

Contains:

- hero ship;
- 8+ crew;
- multiple active work tasks;
- cargo;
- sail/rigging states;
- calm and storm weather variants;
- day/night lighting variants.

Validate ocean, ship readability, weather, lighting, crew visibility, and performance.

## Test C — Shipwreck / Landfall

Contains:

- damaged survivors;
- salvaged cargo;
- damaged boat/ship material;
- coastline;
- regional vegetation;
- temporary shelter;
- changing weather.

Validate continuity: these must visibly feel like the same people and possessions from Test B, not a new game mode.

## Test D — Early Settlement

Contains:

- shelter;
- storage;
- water source/work area;
- workshop;
- food-production activity;
- 10+ persistent characters.

Validate visual evolution from expedition camp to settlement and management readability.

**[REQUIRED]** Do not scale content production aggressively until these scenes establish a coherent visual language.

---

# 24. OPEN VISUAL DECISIONS

These are intentionally unresolved and should not be silently decided by an implementation agent.

1. Exact European departure port and region.
2. Exact American destination/primary campaign region.
3. Specific Indigenous peoples represented in the first campaign.
4. Hero expedition ship type and precise date/origin.
5. Exact camera angle, zoom range, and projection implementation.
6. Engine-specific rendering pipeline.
7. Final character mesh/poly/texture budgets.
8. Final environment texture/material budgets.
9. Portrait production method: hand-authored, procedural, AI-assisted, 3D-rendered/painted, or hybrid.
10. Exact UI typefaces and icon system.
11. Degree of persistent dirt/wetness/damage simulation.
12. Whether seasons materially alter environment rendering during the first campaign.
13. How much character aging is visually represented over campaign timescales.

These decisions should be resolved through prototypes, historical research, performance tests, or project-owner approval as appropriate.

---

# 25. FIRST ART IMPLEMENTATION TASK FOR AN AGENT

Do **not** begin by creating hundreds of final assets.

The first art-focused implementation should be a **Visual Feasibility & Style Validation Pass**.

The agent should:

1. Inspect the existing project, engine, renderer, camera, asset pipeline, and performance constraints.
2. Identify which requirements in this document are already supported.
3. Flag technical conflicts or expensive requirements.
4. Research only the minimum historical material needed for the first validation scene.
5. Build or propose **Test A — Port Recruitment** using temporary/modular assets.
6. Evaluate the result at actual gameplay zoom.
7. Document deviations and unresolved questions.
8. Do not lock project-wide asset budgets until representative performance data exists.

### Definition of Done

The visual feasibility pass is complete when:

- the intended illustrated historical realism is recognizable;
- characters remain individually readable from gameplay camera;
- environment detail does not overpower characters;
- lighting communicates atmosphere without compromising play;
- UI and world can coexist visually;
- the asset pipeline is repeatable;
- representative performance has been measured;
- major historical unknowns are documented rather than invented;
- the next art-production tasks can be estimated with evidence.

---

# 26. AGENT KICKOFF INSTRUCTION

When this document is provided to an autonomous development or art agent, use the following instruction with it:

> Treat `The_Expedition_Art_Bible.md` as the visual authority for The Expedition and `The_Expedition_Agent_Handoff.md` as the gameplay/simulation authority. First inspect the existing repository and identify the engine, renderer, camera, asset pipeline, current art assets, and technical constraints. Do not immediately replace existing art or implement a new rendering stack. Compare the current project against the Art Bible, identify gaps and conflicts, and propose the smallest visual validation milestone that tests the intended style at actual gameplay distance. Historical details marked [RESEARCH] must be verified rather than invented. Decisions marked [APPROVAL REQUIRED] must be surfaced before permanent implementation. Optimize for coherent style, character identity, gameplay readability, historical plausibility, and sustainable production rather than maximum graphical complexity.

---

## FINAL VISUAL STATEMENT

**The Expedition uses illustrated historical realism: a painterly, grounded interpretation of the early 16th century designed specifically for an isometric strategy-survival game. Historically researched forms and materials are simplified into strong silhouettes, expressive individuals, and readable environments. Naturalistic but dramatic lighting, atmospheric weather, physical wear, and restrained exaggeration give the world the character of a historical adventure painting without sacrificing gameplay clarity. Characters receive the greatest visual individuality because the player's emotional connection to people is the visual priority of the game.**
