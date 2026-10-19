# THE EXPEDITION — VOYAGE & SHIP EXPERIENCE / SIMULATION SPECIFICATION

**Status:** Authoritative system-specific design brief  
**Companion documents:** `The_Expedition_Agent_Handoff.md`, `The_Expedition_Art_Bible.md`, `The_Expedition_Technical_Spec.md`, `Port_Experience_and_Simulation.md`

---

# 0. Purpose

This document defines the ocean-voyage and shipboard portion of **The Expedition**.

The voyage is intended to be the longest major phase of the campaign. It must therefore function as more than travel between the European port and the Americas.

The ship is simultaneously:

1. the expedition's home,
2. its transportation,
3. its workplace,
4. its primary survival system,
5. its storage system,
6. its social environment,
7. and, during disaster, potentially its greatest threat.

The voyage should transform the people and resources established during the port phase into a persistent human survival story.

The player should remember not only storms and discoveries, but also:

- who stood which watches,
- who became exhausted,
- who learned new skills,
- who proved dependable,
- who argued with whom,
- who became sick,
- who saved another person's life,
- which supplies ran low,
- which repairs were improvised,
- and which people never reached land.

---

# 1. Experience North Star

[REQUIRED] The ship must feel inhabited and operational even when no crisis is occurring.

[REQUIRED] Routine sailing should create context and attachment without requiring the player to watch uneventful ocean travel in real time.

[REQUIRED] Major emergencies should emerge from interacting systems wherever possible rather than isolated random event popups.

[REQUIRED] Preparation decisions made in port must materially affect the voyage.

[REQUIRED] Crew skills, health, fatigue, relationships, tools, and assignments must influence outcomes.

[REQUIRED] The ocean must change visually, mechanically, and acoustically.

[REQUIRED] Navigation should involve uncertainty rather than a perfect GPS-like world map.

[REQUIRED] Shipwreck is a possible campaign transition, not automatically a game-over screen.

[DON'T] Turn the voyage into a sequence of unrelated choice-card events.

[DON'T] Require constant micromanagement of routine sailing.

[DON'T] make every storm a scripted cinematic with predetermined consequences.

[DON'T] use a single global "ship health" value as the entire damage model.

---

# 2. Emotional Arc of a Long Voyage

The voyage should develop through phases rather than maintaining constant intensity.

## Departure
The port recedes. The expedition feels exciting and uncertain. Crew settle into watches and routines.

## Adjustment
Seasickness, cramped quarters, work routines, personalities, and early competence become visible.

## Open Ocean
Land disappears. The ship becomes the crew's entire world. Navigation uncertainty and resource planning become psychologically important.

## Accumulation
Minor injuries, fatigue, equipment wear, interpersonal friction, food quality, weather, and uncertainty begin accumulating.

## Crisis
A storm, illness, navigation error, damage event, resource shortage, social conflict, encounter, or combination creates a meaningful test.

## Recovery
The crew repairs, treats injuries, rests, mourns, recalculates, and changes routines.

## Approach / Discovery
Signs of land or changed waters create anticipation.

## Landfall or Disaster
The voyage transitions into arrival, alternate harbor, wreck survival, rescue, relocation, or another plausible state.

[REQUIRED] The game should regularly alternate tension and recovery. Constant catastrophe makes catastrophe meaningless.

---

# 3. The Ship as a Physical Play Space

The player should understand the ship spatially.

Core spaces should include, where historically appropriate for the selected vessel:

- weather deck,
- helm/steering area,
- mast/rigging work zones,
- bow/forward work area,
- cargo hold,
- provision storage,
- water storage,
- crew sleeping/resting area,
- officer/captain working area,
- galley/cooking area if appropriate,
- sick/treatment area where improvised or designated,
- bilge/pump access,
- repair/material storage.

[RESEARCH] Final compartment layout must match the chosen ship type, date, region, and available historical evidence.

[REQUIRED] Gameplay spaces must remain readable from the isometric camera even when historical architecture is cramped.

[PREFERRED] Use selective cutaway/roof/deck transparency rather than unrealistically enlarging every interior.

---

# 4. Ship System Model

Avoid a single abstract health bar.

Initial ship systems:

## Hull
State:
- structural condition,
- local damage,
- leak severity,
- repair quality,
- accumulated wear.

Actions:
- inspect,
- patch,
- reinforce,
- emergency plug,
- pump/manage resulting water.

## Rigging and Sails
State:
- sail condition,
- line/rigging condition,
- current sail configuration,
- wind exposure,
- temporary repairs.

Actions:
- set sail,
- reduce sail,
- reef/adjust where appropriate,
- repair canvas,
- replace/repair line,
- inspect damage.

## Steering / Handling
State:
- steering condition,
- handling penalties,
- current heading,
- response to wind/sea state.

## Navigation
State:
- estimated position,
- confidence/error,
- heading,
- chart knowledge,
- recent observations,
- environmental clues.

## Cargo
State:
- mass/volume,
- placement,
- secured/unsecured,
- condition,
- water exposure,
- spoilage/damage.

## Bilge / Water Intrusion
State:
- water accumulation,
- pump effectiveness,
- leak input,
- crew assigned.

## Provisions
State:
- food,
- fresh water,
- medicine,
- fuel where relevant,
- repair material,
- tools,
- trade goods,
- personal stores.

## Crew Operations
State:
- watch coverage,
- task coverage,
- fatigue,
- available skill,
- morale/social pressure,
- injuries/illness.

[REQUIRED] Damage to one system may create pressure on another.

Example:
storm -> sail damage -> reduced handling -> longer exposure -> fatigue -> slower repair -> worsening leak -> pumping labor -> less rest.

This cascading behavior is central to the voyage.

---

# 5. Sailing Model

The first implementation does not require full naval physics.

The sailing model should provide meaningful relationships between:

- wind direction,
- wind strength,
- sail configuration,
- heading,
- vessel capability,
- sea state,
- damage,
- crew competence,
- cargo/load condition.

[REQUIRED] The player should understand why the ship is moving slowly or handling poorly.

[PREFERRED] Present understandable nautical concepts rather than hidden simulation complexity.

[RESEARCH] Historical sailing limitations and terminology must be validated for the final vessel.

[DON'T] create a modern motorboat-like steering model.

---

# 6. Navigation and Uncertainty

Navigation is a core voyage activity.

The player should not always know exact position.

Track separately:

**True Position** — simulation truth.  
**Estimated Position** — expedition's current best estimate.  
**Player Map Knowledge** — what geographic information is available.  
**Confidence** — uncertainty surrounding the estimate.

Inputs may include:
- heading and estimated speed,
- elapsed time,
- celestial observations where historically appropriate,
- latitude-related observations where appropriate,
- coastline,
- landmarks,
- depth/sounding information,
- currents,
- winds,
- birds,
- vegetation/debris,
- water characteristics,
- other sailors/ships,
- charts and written knowledge.

Factors degrading confidence:
- storms,
- cloud cover,
- poor visibility,
- inexperienced navigator,
- missed observations,
- damaged instruments,
- uncertain currents,
- accumulated dead-reckoning error.

[REQUIRED] Skilled navigation improves information quality rather than revealing magical certainty.

[PREFERRED] The map visually communicates uncertainty with ranges, faded marks, annotations, confidence regions, or equivalent period-inspired UI.

---

# 7. Route Planning

Before and during voyage, the captain can choose or revise route intent.

Route considerations:
- expected winds,
- known currents where appropriate,
- known ports,
- reported islands,
- hazards,
- water/provisions,
- political risk,
- weather,
- ship condition,
- crew health,
- sponsor objectives.

Potential choices:
- direct route,
- safer known route,
- detour toward harbor,
- investigate reported land,
- avoid storm region,
- seek assistance,
- turn back,
- continue despite risk.

[REQUIRED] Route decisions should trade time, uncertainty, supplies, opportunity, and risk.

---

# 8. Time Model

Long voyages require aggressive control of pacing.

Modes:

### Paused Planning
Assign work, inspect state, review map, examine crew, issue orders.

### Normal Time
Direct character control, work observation, conversations, local ship interaction.

### Accelerated Routine
Compress uneventful travel while simulation continues.

### Critical Slowdown
Optional automatic return to normal/pause when significant conditions occur.

Examples triggering slowdown:
- person injured,
- severe weather warning,
- fire,
- major leak,
- sighted vessel,
- land sighting,
- illness escalation,
- task failure,
- social confrontation,
- navigation concern,
- resource threshold.

[REQUIRED] The player can configure some alert thresholds.

[REQUIRED] Accelerated time cannot skip consequences silently.

---

# 9. Watches and Duty

Shipboard life is organized through recurring responsibilities.

Possible assignments:
- steering/helm,
- lookout,
- sail/rigging duty,
- navigation assistance,
- maintenance,
- cooking,
- pumping,
- medical care,
- cargo inspection,
- rest.

[RESEARCH] Exact watch terminology and structure should match historical evidence and vessel needs.

Crew assignment UI should communicate:
- who is assigned,
- competence,
- fatigue,
- health,
- conflicting duties,
- replacement availability.

[REQUIRED] The player can create standing assignments so routine work does not require repeated manual orders.

[PREFERRED] Crew can intelligently substitute within captain-defined limits.

---

# 10. Task System

Ship operations use the shared project task framework.

Each task may define:
- location,
- required/desired skill,
- minimum participants,
- tools,
- materials,
- duration,
- environmental modifiers,
- interruption conditions,
- urgency,
- risk,
- result quality.

Example:

```
Task: Patch Hull Leak
Location: Lower Hull / Leak Node
Desired Skill: Carpentry 3
Tool: Repair tools
Material: Timber + sealing material
Participants: 1–3
Urgency: Critical
Interruptions: flooding, injury, loss of access
```

Outcome depends on:
- skill,
- physical condition,
- fatigue,
- stress,
- tools,
- materials,
- environment,
- assistance,
- time available.

---

# 11. Direct Control vs Delegation

The player remains a directly controllable captain.

The player may also temporarily directly control another crew member when needed.

Reasons:
- perform specialized task,
- inspect an area,
- personally intervene,
- execute precision interaction,
- respond to emergency.

[REQUIRED] Direct control must not make delegation obsolete.

[REQUIRED] Skilled AI should be capable of routine competent work.

[PREFERRED] Direct control gives precision, situational awareness, and personal involvement rather than arbitrary statistical bonuses.

---

# 12. Needs and Physical Condition

Prototype needs:
- hunger,
- thirst,
- fatigue,
- health,
- stress.

Later possibilities:
- temperature exposure,
- comfort,
- belonging,
- sanitation effects.

Needs should respond to:
- workload,
- weather,
- sleep,
- rationing,
- sickness,
- injury,
- role,
- environment.

[REQUIRED] Avoid identical flat-decay meters for every need.

Example:
heavy storm labor increases thirst/fatigue faster than resting below deck.

---

# 13. Food and Fresh Water

Provision management must be understandable but consequential.

Track:
- quantity,
- consumption rate,
- condition/spoilage where appropriate,
- storage,
- ration policy,
- special medical needs.

Captain policies may include:
- normal ration,
- reduced ration,
- emergency ration,
- priority allocation.

[REQUIRED] Rationing creates physical and social consequences.

[REQUIRED] Water should generally be strategically more important than generic "food points."

[RESEARCH] Period-appropriate preservation and shipboard food must be historically validated.

---

# 14. Cooking and Meal Routine

Meals provide more than hunger reduction.

They create:
- schedule rhythm,
- social interaction,
- morale opportunities,
- resource decisions,
- illness/sanitation risk,
- character moments.

[PREFERRED] Cooking skill and ingredient condition affect meal quality.

[PREFERRED] Shared meals occasionally produce conversations or conflicts.

---

# 15. Health, Injury, and Illness

Shipboard health should include:
- injury,
- infection risk,
- illness,
- exhaustion,
- dehydration,
- malnutrition over time,
- treatment quality,
- recovery.

Medical outcomes depend on:
- medical knowledge,
- available supplies,
- sanitation,
- rest,
- severity,
- environmental conditions.

[RESEARCH] Medical practices must be historically grounded without presenting harmful historical practices as modern medical advice.

[REQUIRED] Health conditions persist into later phases if the character survives.

---

# 16. Sanitation

Sanitation should exist as a systemic risk without becoming a toilet-management simulator.

Possible contributors:
- contaminated water,
- spoiled food,
- poor waste handling,
- cramped quarters,
- sick crew,
- wet bedding,
- pests,
- insufficient cleaning.

Represent primarily through:
- policies,
- tasks,
- environmental state,
- disease risk.

---

# 17. Fatigue and Sleep

Fatigue is a key bridge between routine and crisis.

Crew need adequate rest.

Causes:
- long watches,
- emergency work,
- storms,
- illness,
- poor sleeping conditions,
- repeated interruptions.

Consequences:
- slower work,
- mistakes,
- accidents,
- social irritability,
- poorer judgment,
- reduced physical performance.

[REQUIRED] Emergencies can force the player to knowingly spend crew endurance now at the cost of later capability.

---

# 18. Relationships and Shipboard Social Pressure

The ship is a confined social environment.

Conflict sources:
- rationing,
- unequal workload,
- broken promises,
- leadership decisions,
- fear,
- exhaustion,
- cultural/language differences,
- interpersonal history,
- perceived favoritism,
- dangerous orders,
- destination disagreement,
- loss of trusted crew.

Positive relationships may produce:
- voluntary assistance,
- mentoring,
- emotional support,
- warnings,
- loyalty,
- better teamwork.

[REQUIRED] Relationships remain character-to-character and may be asymmetric.

[DON'T] reduce mutiny to "morale < 20 = mutiny."

---

# 19. Social Crisis and Mutiny

Serious unrest should emerge from understandable causes.

Escalation example:
1. concern,
2. complaint,
3. faction formation,
4. refusal,
5. confrontation,
6. leadership crisis,
7. mutiny or reconciliation.

Player responses:
- explain decision,
- change policy,
- redistribute duties,
- increase ration,
- promise reward,
- discipline,
- negotiate,
- turn back,
- seek harbor,
- replace officer.

[REQUIRED] Characters should communicate grievances before most severe social crises.

[REQUIRED] Some demands may be impossible or mutually contradictory.

---

# 20. Weather System

Weather must affect:
- visuals,
- audio,
- ship movement,
- navigation,
- tasks,
- fatigue,
- safety,
- crew behavior.

Core states/variables:
- wind direction,
- wind strength,
- precipitation,
- visibility,
- cloud cover,
- sea state,
- temperature abstraction where useful,
- storm severity.

Weather transitions should have plausible warning signs where possible:
- changing clouds,
- wind shift,
- pressure proxies if appropriate to period perception,
- swell,
- animal/bird behavior,
- experienced sailor observations.

[REQUIRED] Skilled characters may recognize danger earlier.

---

# 21. Storm Experience

Storms should be systemic set pieces.

Possible progression:

### Warning
Weather deteriorates. Experienced crew voice concern.

### Preparation
Reduce sail, secure cargo, close/protect openings, prepare pumps, assign emergency teams.

### Impact
Heavy sea, poor visibility, violent ship motion, damage risk.

### Cascading Problems
Sail tears, cargo shifts, person injured, leak worsens, pump fails, navigator loses confidence.

### Decision
Player prioritizes limited labor.

### Recovery
Inspect damage, treat injuries, rest crew, recalculate position, mourn losses.

[REQUIRED] Pre-storm preparation materially changes outcomes.

[REQUIRED] Storm consequences are not fully predetermined.

---

# 22. Damage and Repair

Damage should be localized enough to create decisions.

Examples:
- hull leak,
- damaged plank/structure,
- torn sail,
- damaged line,
- steering damage,
- broken pump component,
- damaged storage,
- spoiled/wet cargo.

Repairs can be:
- emergency,
- temporary,
- proper.

Temporary repair may:
- consume fewer resources,
- take less time,
- restore partial function,
- carry failure risk.

[REQUIRED] Repair quality and history should persist.

The ship can accumulate a story:
"Main sail patched after Storm 2."
"Port hull reinforcement improvised at sea."

---

# 23. Cargo Security

Cargo placement should matter enough to support:
- secure/loose state,
- water exposure,
- accessibility,
- dangerous shifting,
- spoilage/damage.

[DON'T] require the player to manually Tetris every barrel unless that interaction proves genuinely fun.

Use zones, priorities, and representative placement.

---

# 24. Fishing and Small Shipboard Activities

Small activities help vary pacing.

Candidates:
- fishing,
- equipment maintenance,
- map annotation,
- navigation observations,
- conversation,
- reading/recordkeeping,
- training,
- minor crafting,
- cleaning,
- repairing clothing/netting.

[REQUIRED] These activities should feed larger systems rather than exist as disconnected minigames.

Example:
fishing -> food supplement + skill use + character interaction + weather dependency.

---

# 25. Skill Development

The voyage provides repeated practical experience.

Skills may improve through:
- successful tasks,
- mentorship,
- repeated routine,
- crisis performance.

[REQUIRED] Skill growth should be slow enough to preserve the importance of recruitment.

[PREFERRED] Characters can learn basic secondary competence from experienced crew.

Example:
an inexperienced sailor repeatedly assisting the carpenter may gain basic repair familiarity.

---

# 26. Mentorship

Experienced characters may teach others.

Mentorship requires:
- compatible skill,
- available time,
- relationship tolerance,
- learner capability,
- repeated exposure.

This creates long-term value in experienced crew beyond raw task speed.

---

# 27. Encounters at Sea

Possible encounters:
- merchant vessel,
- fishing vessel,
- military/private vessel where historically appropriate,
- damaged ship,
- abandoned/debris field,
- survivors,
- island,
- known/unknown harbor,
- floating wreckage,
- unusual natural phenomenon.

Encounters should have independent context and purpose.

Another ship is not automatically:
- enemy,
- merchant,
- quest dispenser.

Possible actions:
- signal,
- avoid,
- approach,
- trade,
- exchange news,
- ask directions,
- seek medical aid,
- provide aid,
- recruit/transfer people,
- investigate.

[RESEARCH] Flags, signaling, political relationships, and maritime law must be period/location appropriate.

---

# 28. Other Expeditions

Other travelers may have their own missions.

Track lightweight:
- origin,
- destination,
- purpose,
- condition,
- leadership,
- relationship to player,
- known history.

[PREFERRED] The player can later hear what happened to ships encountered earlier.

This helps make the ocean feel connected rather than procedurally empty.

---

# 29. Discovery

Discovery should be grounded in the captain's perspective, not framed as places being nonexistent until Europeans see them.

Use language such as:
- charted by expedition,
- sighted,
- recorded,
- encountered,
- identified,
- learned from local/other navigators.

[REQUIRED] Avoid "discovering" inhabited places as though their inhabitants did not exist.

---

# 30. Ocean Visual Experience

The ocean must support long-duration visual variety.

Variables:
- sun angle,
- cloud cover,
- sea color,
- wave character,
- swell,
- wind,
- foam,
- fog,
- rain,
- visibility,
- reflections.

Visual moods:
- glassy dawn,
- brilliant midday,
- hazy heat,
- golden evening,
- moonlit night,
- fog,
- rain,
- threatening storm,
- post-storm calm.

[REQUIRED] Weather changes lighting on ship and characters, not only sky/water.

[REQUIRED] Ocean state should visually communicate gameplay conditions where possible.

---

# 31. Shipboard Visual Storytelling

The ship should change during voyage.

Possible visible state:
- patched sail,
- replaced rope,
- damaged railing,
- wet deck,
- cargo depletion,
- improvised repair,
- medical area in use,
- drying clothing,
- storm-secured cargo,
- accumulated grime,
- cleaned/restored areas.

[PREFERRED] Major repairs remain visible for some time.

The vessel should look lived in rather than reset after every event.

---

# 32. Camera and Readability

Camera must support:
- general isometric overview,
- direct character control,
- task inspection,
- interior cutaway,
- storm readability,
- navigation/map transition.

[REQUIRED] Critical crew and task icons remain readable during severe weather.

[DON'T] let dramatic camera shake make command gameplay unusable.

[PREFERRED] Use restrained ship/camera motion to communicate sea state while preserving control.

---

# 33. Sound Design

The ship's soundscape should become familiar enough that unusual sounds communicate danger.

Routine:
- hull creaks,
- water against hull,
- rigging,
- canvas,
- footsteps,
- work calls,
- gulls near land,
- wind,
- occasional tools.

Interior:
- muffled sea,
- timber stress,
- cargo movement,
- voices overhead.

Storm:
- wind roar,
- canvas strain,
- waves,
- timber impacts,
- shouted commands,
- loose cargo,
- pumps.

[REQUIRED] Sound state changes with location, weather, deck/interior, damage, and time.

[PREFERRED] A new leak, loose cargo, or damaged rigging may first be noticed acoustically by attentive crew/player.

---

# 34. Music

Music should not continuously overwhelm ship ambience.

Use music for:
- departure,
- major discovery,
- severe crisis,
- aftermath,
- land sighting,
- death/mourning,
- rare emotional events.

[PREFERRED] Long routine sailing frequently uses environmental sound without score.

---

# 35. Alert and Information Design

The player needs awareness without constant popups.

Severity levels:
- informational,
- attention,
- urgent,
- critical.

Examples:
Informational: watch changed.
Attention: water consumption above plan.
Urgent: navigator confidence falling.
Critical: major leak.

[REQUIRED] Critical alerts identify cause/location/person where known.

[REQUIRED] Player can click an alert to focus relevant location/person/system.

---

# 36. Captain's Log / Chronicle

Automatically record meaningful events:
- departure,
- route changes,
- weather,
- sightings,
- injuries,
- illness,
- deaths,
- disputes,
- repairs,
- discoveries/encounters,
- resource emergencies,
- leadership changes.

Allow important player annotations if practical.

The chronicle serves:
- narrative memory,
- player planning,
- debugging,
- post-campaign history.

---

# 37. Death at Sea

Death should matter.

Consequences:
- skill loss,
- relationship reactions,
- task coverage changes,
- leadership impact,
- burial/body decision where historically/contextually appropriate,
- chronicle entry.

[RESEARCH] Death and burial practices must respect historical/cultural context.

[REQUIRED] Dead characters cannot reappear through later state transitions.

---

# 38. Captain Incapacitation / Death

If captain becomes incapacitated:
- authority may temporarily pass,
- player control may shift according to established succession rules.

If captain dies:
- select plausible successor based on expedition state,
- original captain's personal skills/relationships/inventory context are lost or transformed appropriately,
- leadership crisis may occur.

[REQUIRED] This is a campaign continuation system, not a free respawn.

---

# 39. Shipwreck Transition

Shipwreck must be modeled as a transition.

Possible triggers:
- grounding,
- catastrophic storm damage,
- collision,
- reef/coast impact,
- unrecoverable flooding.

Transition process:
1. determine immediate casualties,
2. determine location,
3. determine accessible escape options,
4. determine salvage,
5. transfer survivor states,
6. preserve injuries/relationships/history,
7. create shore/island survival state if plausible.

[REQUIRED] Do not reset survivors to default condition.

[REQUIRED] Inventory survival is based on location, accessibility, damage, and salvage—not arbitrary percentage alone.

---

# 40. Survival After Wreck

Immediate loop:
- count survivors,
- treat urgent injuries,
- locate fresh water,
- establish shelter,
- recover food/tools,
- salvage ship,
- assess location,
- decide next objective.

Possible objectives:
- repair/refloat vessel,
- build small craft,
- signal passing ship,
- explore,
- reach known settlement,
- establish temporary/permanent settlement.

This is a continuation of the voyage story.

---

# 41. Long-Voyage Variety Framework

To avoid repetition, vary four layers:

## Environmental
weather, visibility, sea state, temperature abstraction, land proximity.

## Operational
maintenance, navigation, provisioning, repair, watches.

## Human
relationships, sickness, fatigue, mentorship, conflict, personal events.

## External
ships, islands, ports, debris, information, wildlife/natural observations.

[REQUIRED] Do not solve voyage variety by simply increasing random-event frequency.

---

# 42. Event Causality

Prefer events generated from state.

Bad:
"Random event: sailor injured."

Better:
high sea state + unsecured cargo + exhausted sailor + urgent task -> cargo shifts -> sailor injured.

[REQUIRED] Major negative outcomes should expose enough causal information that the player can learn.

---

# 43. Reproducibility and Testing

Simulation randomness should be seedable/reproducible for testing.

Tests should cover:
- provision consumption over 30/60/90 days,
- fatigue and watch rotation,
- damage/repair persistence,
- navigation uncertainty,
- weather transition,
- task interruption,
- death persistence,
- save/load during storm,
- shipwreck state transfer,
- cargo loss/salvage,
- accelerated-time alerts,
- relationship/social-crisis triggers.

---

# 44. Simulation LOD

At sea most relevant crew are local, but performance rules still apply.

Full detail:
- visible/active crew,
- urgent tasks,
- social interactions.

Reduced update:
- sleeping crew,
- stable routine tasks,
- distant background activity.

[REQUIRED] LOD may reduce update frequency, never change authoritative identity or invent outcomes inconsistent with the simulation.

---

# 45. Voyage Vertical Slice

Initial vertical slice:

- one historically plausible ship placeholder pending final research,
- 8–12 crew,
- departure from port,
- standing watch schedule,
- food/water consumption,
- fatigue/rest,
- basic navigation uncertainty,
- wind-dependent travel,
- ship task assignments,
- one maintenance problem,
- one medical problem,
- one social disagreement,
- one escalating storm,
- hull leak,
- sail/rigging damage,
- repair/pumping prioritization,
- post-storm recovery,
- landfall OR shipwreck branch.

This is sufficient to prove the voyage before expanding content.

---

# 46. Four Voyage Validation Scenarios

## Scene A — First Calm Morning at Sea
Tests:
routine life, work animations, watch schedule, ocean rendering, ship soundscape.

## Scene B — Fog and Navigation Uncertainty
Tests:
visibility, navigation UI, confidence system, lookout behavior, atmospheric audio.

## Scene C — Full Storm Emergency
Tests:
weather, ship motion, alerts, task prioritization, damage cascade, crew fatigue/injury, readability.

## Scene D — Aftermath / Damaged Ship at Dawn
Tests:
persistent visual damage, injured crew, repair state, resource consequences, emotional atmosphere, chronicle.

[REQUIRED] Prove these before scaling voyage content.

---

# 47. Visual Reference Board

Reference categories only; do not copy specific works/assets.

## A. Early-16th-Century Carracks / Naos
Research:
- museum reconstructions,
- archaeological scholarship,
- maritime museums,
- scholarly ship plans,
- replica vessels with documented caveats.

Extract:
- human scale,
- deck arrangement,
- rigging density,
- hull proportions,
- material wear.

## B. Maritime Paintings
Research:
- old-master sea paintings,
- sailing ships in calm/storm,
- harbor departure scenes.

Extract:
- light,
- atmospheric depth,
- sea/sky relationship,
- scale,
- emotional composition.

Do not assume later-period ship technology is valid for 1500–1520.

## C. Working Sailing Ships
Research:
- traditional sailing vessel deck work,
- rope handling,
- sail handling,
- ship maintenance.

Extract:
- body mechanics,
- teamwork,
- rope/canvas behavior,
- spatial choreography.

Validate period-specific equipment separately.

## D. Storm at Sea
Extract:
- visibility reduction,
- wave direction,
- wet materials,
- sail strain,
- human posture,
- lighting.

## E. Below Deck
Extract:
- cramped scale,
- timber structure,
- storage density,
- low-light atmosphere.

[RESEARCH] Final layout must derive from appropriate vessel evidence.

---

# 48. Performance Priorities

Protect in this order:

1. authoritative simulation,
2. responsive commands/UI,
3. persistent characters,
4. readable weather/task state,
5. core ship animation,
6. ocean/weather visuals,
7. secondary atmospheric details.

On lower hardware reduce:
- distant ocean detail,
- particles,
- secondary animations,
- decorative crew,
- reflection quality,

before reducing core simulation correctness.

---

# 49. Acceptance Criteria

Voyage vertical slice is complete when:

- [ ] ship moves according to simplified wind/heading model,
- [ ] route and estimated position are distinct from true position,
- [ ] crew can receive standing and urgent tasks,
- [ ] watches/rest affect fatigue,
- [ ] food/water are consumed,
- [ ] health conditions persist,
- [ ] ship has multiple damageable systems,
- [ ] leaks create water/pumping pressure,
- [ ] repair consumes time/materials,
- [ ] storm preparation affects outcomes,
- [ ] severe weather changes visuals/audio/gameplay,
- [ ] accelerated time stops/alerts appropriately,
- [ ] social concerns can emerge causally,
- [ ] chronicle records meaningful events,
- [ ] save/load works during normal travel and crisis,
- [ ] actual port recruits remain the same characters,
- [ ] landfall preserves state,
- [ ] shipwreck preserves survivors/injuries/relationships/salvage,
- [ ] dead characters remain dead,
- [ ] player can understand major failure causes.

---

# 50. Agent Implementation Order

1. Greybox ship and semantic work locations.
2. Implement simplified sailing/wind model.
3. Implement ship system state.
4. Connect shared character/task simulation.
5. Add standing watch assignments.
6. Add fatigue/rest.
7. Add food/water consumption.
8. Add navigation true/estimated position.
9. Add accelerated time and alert thresholds.
10. Add hull leak/pumping.
11. Add repair task/material use.
12. Add sail/rigging damage.
13. Add weather variables.
14. Build storm escalation.
15. Add health/medical crisis.
16. Add one causal social conflict.
17. Add chronicle.
18. Add visual ship-state persistence.
19. Build landfall transition.
20. Build shipwreck transition.
21. Validate four voyage scenes.
22. Profile and expand only after core systems pass tests.

---

# 51. Agent Guardrails

[REQUIRED] Read all authoritative project documents first.

[REQUIRED] Audit repository before introducing frameworks/plugins.

[REQUIRED] Reuse the shared character, task, inventory, event, relationship, save, and chronicle systems.

[REQUIRED] Keep simulation separate from rendering.

[REQUIRED] Preserve character IDs across every phase transition.

[APPROVAL REQUIRED] Final hero ship type.

[APPROVAL REQUIRED] Major change to navigation abstraction.

[APPROVAL REQUIRED] Major change to direct-control/delegation model.

[APPROVAL REQUIRED] Full naval-physics implementation.

[DON'T] introduce combat-heavy naval gameplay unless separately approved.

[DON'T] turn the voyage into roguelike event cards.

[DON'T] silently resolve crises during accelerated time without appropriate notification.

---

# 52. Final Design Test

Ask:

> During ten minutes of calm sailing, can I understand what my crew are doing, why the ship is functioning, and what risks are quietly accumulating without being forced to micromanage them?

Then:

> When a storm arrives, do the people, supplies, skills, repairs, watch schedules, and decisions I made earlier determine what happens?

Then:

> After the storm, does the ship and crew visibly and mechanically carry the history of what just happened?

And finally:

> If the ship sinks, does the story continue with the actual survivors rather than resetting into a generic survival mode?

If all four answers are yes, the voyage is serving the core identity of **The Expedition**.

The voyage should not feel like the space between the port and colony.

**The voyage is the expedition.**
