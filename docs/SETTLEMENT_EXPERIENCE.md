# THE EXPEDITION — SETTLEMENT EXPERIENCE & SIMULATION SPECIFICATION

**Status:** Authoritative system-specific design brief  
**Companion documents:** `The_Expedition_Agent_Handoff.md`, `The_Expedition_Art_Bible.md`, `The_Expedition_Technical_Spec.md`, `Port_Experience_and_Simulation.md`, `Voyage_and_Ship_Experience_and_Simulation.md`

# 0. Purpose

This document defines the final major phase of the main expedition campaign: landfall, survival, settlement formation, growth, external relations, and charter completion.

The settlement is not a separate city-builder unlocked after the voyage.

**It is the expedition continuing on land.**

Every surviving person, injury, relationship, skill, tool, supply, obligation, discovery, loss, and decision should matter after arrival.

# 1. Experience North Star

[REQUIRED] The actual survivors become the founding population.

[REQUIRED] Actual surviving cargo, tools, food, medicine, seeds, weapons, trade goods, and ship salvage become the settlement's starting resources.

[REQUIRED] Relationships, injuries, illnesses, fatigue, skills, promises, grievances, and leadership history persist.

[REQUIRED] Early settlement gameplay begins as survival and gradually becomes management.

[REQUIRED] Construction uses physical labor, materials, tools, time, and appropriate skills—not abstract currency alone.

[REQUIRED] The surrounding world already exists independently of the expedition.

[REQUIRED] Indigenous communities must have their own identities, territories, economies, relationships, goals, and agency.

[DON'T] spawn generic colonists to replace losses simply because settlement mode began.

[DON'T] begin with a "Town Hall" abstraction.

[DON'T] treat the Americas as empty wilderness awaiting European ownership.

# 2. Settlement Emotional Arc

## Landfall
Relief is immediately replaced by practical questions: water, shelter, injuries, weather, cargo, local conditions.

## First Nights
The expedition creates a temporary camp from whatever survived.

## Survival
Reliable water, food, sanitation, storage, shelter, medical care, and security become priorities.

## Establishment
Temporary solutions become durable structures. Paths and work areas emerge.

## Community
People acquire routines, roles, grievances, attachments, homes, gardens, workshops, and shared history.

## External World
Trade, diplomacy, neighboring communities, sponsor demands, other expeditions, and regional politics become increasingly important.

## Stability / Charter
The player proves the settlement can persist and fulfills or renegotiates the sponsor's mission.

# 3. Landfall Assessment

Immediately establish:

- survivor count and condition,
- accessible cargo,
- damaged/lost cargo,
- fresh-water prospects,
- shelter prospects,
- food prospects,
- weather,
- terrain,
- local hazards,
- ship/boat condition,
- known nearby people/settlements,
- immediate medical emergencies.

[REQUIRED] Do not give the player perfect knowledge of the surrounding region.

# 4. First 72 Hours

The first days should feel closer to expedition survival than RTS optimization.

Likely priorities:

1. urgent medical treatment,
2. drinking water,
3. weather shelter,
4. food protection,
5. fire/cooking,
6. sanitation,
7. tool/cargo security,
8. reconnaissance.

The player should confront tradeoffs such as:
- use canvas for shelter or preserve it for ship repair,
- spend exhausted carpenter labor now or allow rest,
- explore for better water or remain together,
- consume preserved food or risk unfamiliar local sources.

# 5. Site Selection

If landfall permits movement, settlement location should matter.

Consider:
- fresh water,
- drainage,
- flood/storm exposure,
- arable ground,
- timber/materials,
- landing/dock access,
- defensibility where relevant,
- travel routes,
- existing land use,
- proximity to other communities,
- disease/environmental risk,
- expansion space.

[REQUIRED] A visually attractive site is not automatically a viable one.

[REQUIRED] Existing human occupation and land use must be recognized.

# 6. Camp Stage

Initial placeables/work areas may include:
- temporary shelter,
- fire/cooking area,
- medical shelter,
- protected stores,
- water collection/storage,
- latrine/sanitation zone,
- tool/work area,
- lookout/signaling point.

These are functional expedition objects, not polished buildings.

# 7. Settlement Progression

## Stage A — Landfall Camp
Temporary shelters and salvaged materials.

## Stage B — Survival Camp
Reliable water, protected stores, sanitation, food acquisition.

## Stage C — Established Outpost
Durable shelter, workshop, infirmary, garden/field, dock/landing.

## Stage D — Settlement
Specialized workspaces, agriculture, storage, trade, governance, external relationships.

## Stage E — Stable Charter Community
Can survive predictable seasonal pressures and meet campaign charter conditions.

[REQUIRED] Stages emerge from capabilities rather than arbitrary XP levels.

# 8. Construction Model

A construction project defines:
- footprint/site,
- required materials,
- required tools,
- labor,
- desired skills,
- work phases,
- weather restrictions,
- dependencies,
- completion quality.

Example:
```
Project: Durable Storehouse
Phases:
1. Clear / prepare site
2. Foundation/support
3. Frame
4. Walls/roof
5. storage fittings
Inputs: timber, fasteners/materials as researched, tools
Skills: carpentry preferred
```

[REQUIRED] Skilled workers improve speed/quality but remain physical people who can become tired, injured, sick, or reassigned.

# 9. Building Quality

Construction quality may depend on:
- skill,
- material quality,
- tool quality,
- weather,
- haste,
- design knowledge,
- fatigue.

Consequences:
- durability,
- weather resistance,
- storage protection,
- maintenance burden,
- comfort.

Temporary emergency construction should remain useful but imperfect.

# 10. Initial Building Set

Prototype:
- shelter,
- storehouse,
- water station/well only where viable,
- cooking/fire area,
- infirmary,
- workshop,
- garden/field,
- dock/landing.

[REQUIRED] Do not add dozens of building types before these create a functioning survival loop.

# 11. Materials

Initial categories may include:
- timber,
- stone,
- fibers/rope,
- salvage,
- fasteners/hardware as historically appropriate,
- cloth/canvas,
- fuel,
- soil/agricultural inputs.

[RESEARCH] Construction techniques and materials must reflect region, culture, available resources, and period.

# 12. Tools

Tools are important productivity multipliers and finite expedition assets.

Examples:
- axes,
- saws,
- hammers,
- digging tools,
- knives,
- carpentry tools,
- medical instruments.

Tools:
- occupy inventory,
- wear,
- may be repaired,
- can be lost,
- can be shared,
- affect task performance.

# 13. Water

Fresh water is a foundational settlement system.

Sources may include:
- spring,
- stream/river,
- collected rain,
- well where viable,
- stored ship water.

Track:
- access,
- quantity,
- transport labor,
- contamination risk,
- storage.

[REQUIRED] "Build well anywhere" is prohibited.

# 14. Food

Food phases:

## Immediate
preserved ship provisions, fishing, gathering, hunting where appropriate.

## Transitional
gardens, local trade, improved fishing, managed food stores.

## Long-term
agriculture/animal systems appropriate to region and expedition resources.

[RESEARCH] Crops, seasons, introduced species, local food systems, and agricultural practices require region-specific validation.

# 15. Agriculture

Agriculture is seasonal and knowledge-dependent.

Factors:
- crop suitability,
- planting window,
- soil,
- rainfall,
- labor,
- tools,
- seed condition,
- pests,
- local knowledge.

[REQUIRED] Planting seeds does not guarantee harvest.

[PREFERRED] Local knowledge can be far more valuable than imported assumptions.

# 16. Storage and Spoilage

Storehouses protect:
- food,
- seed,
- medicine,
- tools,
- trade goods,
- materials.

Storage quality affects:
- moisture,
- pests,
- spoilage,
- theft/loss,
- organization/access.

# 17. Sanitation and Disease

Settlement sanitation should include:
- waste placement,
- clean-water protection,
- food storage,
- sick isolation where useful,
- crowding,
- drainage.

[REQUIRED] Disease risk should emerge from conditions and contact patterns, not pure random punishment.

# 18. Health Continuity

Voyage injuries and illnesses persist.

Settlement adds:
- work injuries,
- infection,
- nutritional issues,
- environmental exposure,
- disease,
- recovery opportunities.

The infirmary improves organization/treatment but does not magically heal.

# 19. Work Assignment

Use shared task system.

Work categories:
- construction,
- gathering,
- water transport,
- cooking,
- farming,
- fishing,
- hunting where appropriate,
- exploration,
- medical care,
- crafting/repair,
- guard/watch,
- trade/diplomacy,
- ship salvage.

Standing priorities should prevent constant micromanagement.

# 20. Direct Control

Captain remains directly controllable.

Direct control useful for:
- exploration,
- inspection,
- negotiation,
- urgent work,
- personally significant moments.

Delegation remains essential for settlement scale.

# 21. Character Roles Emerge

Do not rigidly transform people into classes.

A voyage carpenter may naturally become construction lead.

A navigator may become:
- explorer,
- mapmaker,
- teacher,
- administrator.

A sailor may become:
- fisherman,
- builder,
- farmer,
- boat operator.

Roles emerge from skill, need, preference, and experience.

# 22. Skill Transfer and Apprenticeship

Settlement survival encourages cross-training.

Experienced people can mentor others.

[REQUIRED] Specialists remain valuable, but the settlement can develop redundancy over time.

# 23. Personal Space and Belonging

As survival stabilizes, characters care increasingly about:
- sleeping arrangements,
- privacy,
- household relationships,
- meaningful work,
- fairness,
- belonging,
- security.

[PREFERRED] This transition helps the colony feel like a community rather than a labor spreadsheet.

# 24. Relationships and Factions

Existing relationships continue.

New pressures:
- resource allocation,
- land decisions,
- work burden,
- leadership,
- religion/culture,
- sponsor loyalty,
- trade,
- relations with neighbors,
- punishment/dispute resolution.

[DON'T] reduce settlement politics to a single morale meter.

# 25. Leadership and Governance

Early:
captain's expedition authority dominates.

Later:
questions emerge about whether shipboard authority still applies.

Potential systems:
- officer council,
- sponsor representative,
- influential specialists,
- household/family interests,
- informal factions.

[APPROVAL REQUIRED] Deep political simulation beyond initial charter phase.

# 26. Exploration

Exploration reveals:
- terrain,
- water,
- resources,
- routes,
- hazards,
- existing settlements,
- signs of land use,
- coastlines,
- potential agricultural areas.

Exploration teams require:
- people,
- supplies,
- time,
- skills,
- route planning.

[REQUIRED] Fog-of-knowledge, not magical fog-of-existence: unknown things exist independently of player discovery.

# 27. Regional Map

The settlement should connect to a larger regional map.

Track:
- known routes,
- travel time,
- terrain,
- water,
- known communities,
- trade routes,
- reported information,
- uncertain areas.

Reuse principles from `Navigation_and_Chart_System.md`.

# 28. Indigenous Communities

[REQUIRED] Communities are distinct entities, not a generic "Native" faction.

Each relevant community requires researched:
- identity,
- language(s),
- settlement pattern,
- political relationships,
- economy,
- agriculture/food systems,
- territory/use patterns,
- material culture,
- current goals,
- historical context.

[REQUIRED] They act independently of the player.

Possible interactions:
- observation,
- diplomacy,
- trade,
- translation,
- knowledge exchange,
- requests,
- disputes,
- alliance,
- avoidance,
- conflict.

[DON'T] assume hostility.

[DON'T] assume friendship.

[DON'T] frame land as unowned simply because Europeans lack knowledge of ownership/use.

[RESEARCH] Final region and communities require dedicated historical research before content implementation.

# 29. Language

Language matters.

Possible states:
- fluent,
- functional,
- limited,
- no shared language.

Communication can use:
- multilingual crew,
- interpreters,
- gradual learning,
- gesture/context,
- intermediaries.

[REQUIRED] Translation quality can affect understanding without turning people into caricatures.

# 30. Trade

Trade is item/resource exchange plus relationship.

Possible trade:
- food,
- tools,
- materials,
- information,
- services,
- access,
- transport,
- specialty goods.

Value is contextual, not universal.

# 31. Knowledge Exchange

Knowledge may be more important than material goods.

Examples:
- water locations,
- seasonal conditions,
- routes,
- local foods,
- agricultural techniques,
- political relationships,
- hazards.

[REQUIRED] Local expertise should be mechanically meaningful.

# 32. Other European Expeditions / Settlements

Other European actors may:
- trade,
- compete,
- claim authority,
- request help,
- recruit,
- threaten sponsor objectives,
- carry news.

They have independent missions.

# 33. Sponsor Relationship

The sponsor/charter remains active.

Possible obligations:
- settlement survival,
- trade objective,
- resource/report,
- territorial or diplomatic instruction,
- population target,
- return communication,
- time requirement.

Sponsor expectations may conflict with survival or local realities.

# 34. Communication Delay

If communication with Europe occurs, it should respect travel time.

Orders, news, money, supplies, or settlers do not teleport.

[PREFERRED] Messages arriving months later may be outdated.

# 35. New Arrivals

Additional settlers are opportunities and complications.

They may bring:
- skills,
- labor,
- disease,
- supplies,
- political interests,
- families,
- sponsor demands,
- conflicts.

[REQUIRED] New arrivals are persistent people, not population-number increments.

# 36. Seasons

Seasonal change affects:
- planting,
- harvest,
- temperature/weather,
- disease,
- travel,
- food,
- workday,
- water.

[REQUIRED] Surviving the first difficult season/winter may be a major campaign milestone depending on region.

# 37. Weather

Weather affects:
- construction,
- agriculture,
- health,
- travel,
- water,
- storage,
- morale/comfort.

Reuse world-weather architecture from voyage while adapting variables to land.

# 38. Environmental Change

The settlement visibly changes its environment:
- paths appear,
- trees cleared,
- fields cultivated,
- buildings weather,
- docks expand,
- refuse/working areas emerge.

[REQUIRED] Growth should be visually legible without relying solely on UI.

# 39. Visual Progression

### Day 1
Cargo, tents/canvas, fires, exhausted survivors.

### Weeks
Rough shelters, organized storage, paths, work zones.

### Months
Durable buildings, cultivated ground, dock/workshop.

### Long-term
Recognizable lived-in community.

[PREFERRED] Preserve early structures or remnants so the player can see settlement history.

# 40. Settlement Sound

Early:
surf, wind, chopping, fire, sparse voices.

Established:
saws, hammering, animals if present, children/families if present, workshops, bells/calls where appropriate.

[REQUIRED] Sound density grows with actual population/activity.

# 41. Ambient Events

Examples:
- tool breaks,
- roof leaks,
- argument,
- successful harvest,
- injury,
- missing worker,
- visitor,
- trade arrival,
- birth/death where simulation supports it,
- storm damage,
- fire,
- animal intrusion,
- food spoilage.

Prefer state-driven causality.

# 42. Fire

Fire is both essential and dangerous.

Uses:
- warmth,
- cooking,
- craft,
- light.

Risks:
- dry materials,
- poor placement,
- wind,
- unattended flame.

[APPROVAL REQUIRED] Complex spreading-fire simulation.

# 43. Security

Security may concern:
- wildlife,
- theft,
- external conflict,
- fire watch,
- dangerous terrain.

[REQUIRED] Do not default immediately to fortress-building.

Security needs must emerge from actual regional circumstances.

# 44. Death and Burial

Death continues to reshape the settlement.

Track:
- skill loss,
- relationships,
- work impact,
- chronicle,
- burial/mourning according to context.

Settlement memory should preserve founders who died.

# 45. Settlement Chronicle

Record:
- landfall,
- first shelter,
- first reliable water,
- deaths,
- first harvest,
- first contact/trade,
- major construction,
- crises,
- leadership changes,
- charter milestones.

[PREFERRED] The settlement can later display founding history derived from this chronicle.

# 46. Main Mission Completion

Victory should prove viability, not require map domination.

Example charter criteria:
- survive defined period,
- maintain viable population,
- reliable water,
- sustainable food,
- durable shelter,
- functioning health/storage,
- fulfill sponsor obligation,
- establish required communication/trade relationship.

[REQUIRED] Exact criteria depend on scenario charter.

# 47. Continuation After Victory

[PREFERRED] Allow continued play after main charter completion.

Potential post-charter goals:
- expand,
- explore,
- trade,
- resolve relationships,
- pursue new sponsor mission.

Do not require this for initial vertical slice.

# 48. Settlement Failure

Failure may include:
- population collapse,
- abandonment,
- relocation,
- evacuation,
- absorption into another community,
- return voyage if possible.

[REQUIRED] Not every failing settlement needs immediate game-over if a plausible continuation exists.

# 49. Performance Strategy

Use:
- task batching,
- simulation LOD,
- building/worksite state,
- reduced distant AI updates,
- pooled decorative activity,
- pathfinding zones,
- job reservation.

Protect persistent people and authoritative simulation first.

# 50. Settlement Vertical Slice

Target:
- 8–15 survivors,
- one landfall site,
- water search,
- temporary shelter,
- stores,
- sanitation,
- food acquisition,
- workshop,
- infirmary,
- one crop/garden experiment,
- one exploration route,
- one external contact placeholder pending historical region research,
- one severe weather event,
- one social/resource dispute,
- survival through defined period.

# 51. Four Validation Scenes

## A — First Night
Tests cargo continuity, survivor state, shelter, fire, injury, exhaustion.

## B — Two Weeks Later
Tests work routines, paths, storage, water, early construction.

## C — First Major Crisis
Storm, illness, food shortage, or equivalent interacting systems.

## D — Established Settlement
Tests visual progression, persistent founders, agriculture, workshop, external relationship, charter UI.

# 52. Visual Reference Board

Research categories:
- archaeology/reconstructions of relevant early settlements,
- regional Indigenous settlements and material culture,
- early-modern timber construction,
- temporary expedition camps,
- historical agriculture,
- coastal environments for final region.

[REQUIRED] Reference final geography and cultures specifically once scenario location is approved.

[DON'T] use Jamestown/Plymouth imagery as generic reference for an earlier/different region without separating transferable composition from anachronistic detail.

# 53. Acceptance Criteria

- [ ] actual voyage survivors form starting population,
- [ ] health/relationships/skills persist,
- [ ] surviving cargo becomes settlement inventory,
- [ ] first-day survival decisions matter,
- [ ] water source is physically/logistically meaningful,
- [ ] construction uses labor/material/tools/time,
- [ ] work can be delegated,
- [ ] storage affects resources,
- [ ] sanitation affects risk,
- [ ] agriculture is seasonal/conditional,
- [ ] settlement visibly evolves,
- [ ] exploration updates knowledge,
- [ ] external communities exist independently,
- [ ] local knowledge can matter mechanically,
- [ ] sponsor charter remains relevant,
- [ ] save/load preserves settlement state,
- [ ] deaths remain persistent,
- [ ] charter completion can be evaluated causally.

# 54. Agent Implementation Order

1. Transfer voyage state into landfall state.
2. Greybox landfall/camp.
3. Water and shelter survival loop.
4. Physical inventory/cargo access.
5. Construction project framework.
6. Work assignment.
7. Storage/spoilage.
8. Sanitation/health.
9. Food gathering.
10. Basic agriculture.
11. Settlement visual progression.
12. Exploration/regional knowledge.
13. External-contact framework.
14. Sponsor/charter tracking.
15. Seasonal pressure.
16. Main-mission completion.
17. Validate four scenes.
18. Expand only after loop is proven.

# 55. Agent Guardrails

[REQUIRED] Reuse persistent campaign systems.

[REQUIRED] Preserve exact character IDs.

[REQUIRED] Historical/cultural content requires research.

[APPROVAL REQUIRED] Final destination region.

[APPROVAL REQUIRED] Specific Indigenous communities represented.

[APPROVAL REQUIRED] Deep political simulation.

[DON'T] import generic colony-builder assumptions that conflict with people-first survival.

# 56. Final Design Test

Ask:

> If only fourteen people survive the voyage, does the settlement genuinely feel like those fourteen people trying to build a future?

Ask:

> Can I trace present strengths and weaknesses back to choices made in Europe and at sea?

Ask:

> Does the surrounding world feel like it existed before we arrived?

Ask:

> Does a successful settlement feel earned through survival, relationships, knowledge, labor, diplomacy, and adaptation rather than simply accumulating building resources?

If yes, the final phase remains part of the same game.

**The settlement is not the reward after the expedition. It is what the expedition becomes.**
