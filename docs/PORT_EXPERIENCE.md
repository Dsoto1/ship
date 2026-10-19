# THE EXPEDITION — PORT EXPERIENCE & SIMULATION SPECIFICATION

**Status:** Authoritative system-specific design brief  
**Companion documents:** `The_Expedition_Agent_Handoff.md`, `The_Expedition_Art_Bible.md`, `The_Expedition_Technical_Spec.md`

## 0. Purpose

This document defines the first European port as a playable place, simulation space, recruitment environment, and emotional prologue to the expedition.

The port is **not** a static recruitment menu with decorative NPCs. It must feel as though it existed before the captain arrived and will continue after the ship departs.

Its primary design purpose is to make the player care about:
1. the people they recruit,
2. the ship they will depend upon,
3. the provisions and equipment they personally chose,
4. the risks they accepted before departure.

The voyage should later place all four at risk.

---

# 1. Experience North Star

[REQUIRED] The port should feel crowded, purposeful, noisy, imperfect, and alive.

[REQUIRED] Density of meaningful activity is more important than map size.

[REQUIRED] NPC movement must usually have an understandable purpose. Avoid crowds whose primary behavior is random wandering.

[REQUIRED] The player's expedition ship must physically exist in the harbor during preparation and act as a recurring visual landmark.

[PREFERRED] Streets and buildings repeatedly hide and reveal the harbor and ship, making the vessel a constant reminder of the coming voyage.

[REQUIRED] The player should discover potential recruits through the world: observation, conversation, introductions, rumors, employers, references, and chance encounters.

[REQUIRED] The port must visually and behaviorally change as departure approaches.

[DON'T] Do not make the port a huge open-world city.

[DON'T] Do not reduce recruitment to a single "available crew" list.

[DON'T] Populate the port with hundreds of expensive fully simulated characters merely to create density.

---

# 2. Emotional Arc

The port experience should have a recognizable progression.

### Arrival
The captain is one person inside a city that does not care much about the expedition.

### Investigation
The player learns where useful people, supplies, workshops, officials, and information can be found.

### Commitment
Contracts are signed. Money is spent. Candidates become crew. Cargo accumulates.

### Recognition
Word of the expedition spreads. Merchants recognize the captain. Families appear. Sponsor representatives inspect progress. The ship becomes visibly prepared.

### Departure
The expedition becomes a public event. The player sees the actual people and material they assembled leave with them.

[REQUIRED] Departure should feel emotionally different depending on who was recruited and what occurred during preparation.

---

# 3. Port Layout

Use a compact interconnected layout with five functional districts.

## 3.1 Quayside
Primary functions:
- expedition ship,
- cargo handling,
- fishermen,
- arriving/departing vessels,
- sailors,
- dockworkers,
- harbor officials,
- ship inspection.

Visual language:
- piers,
- ropes,
- cranes/hoists appropriate to research,
- barrels,
- carts,
- nets,
- fish,
- warehouses,
- wet timber,
- rigging forest,
- water reflections.

## 3.2 Market
Primary functions:
- provisions,
- tools,
- clothing,
- livestock where appropriate,
- trade goods,
- rumor exchange.

The market should visually change as stock is purchased and time advances.

## 3.3 Tavern / Inns
Primary functions:
- travelers,
- sailors,
- introductions,
- private negotiation,
- rumors,
- lodging,
- candidate discovery.

Interior sound should strongly contrast with the harbor outside.

## 3.4 Workshops
Include contextually appropriate:
- carpenter/shipwright,
- smith,
- cooper,
- rope maker,
- tailor/sail repair,
- related trades validated for the chosen location.

Workshops should show work occurring rather than function as storefront menus.

## 3.5 Customs / Sponsor District
Primary functions:
- charter,
- sponsor representative,
- official records,
- cargo restrictions,
- fees,
- departure requirements,
- deadline information.

[PREFERRED] Place this district somewhat above or away from the waterfront so the player can look back across the harbor.

---

# 4. Spatial Composition

[REQUIRED] Build the port around a series of visual reveals.

Examples:
- narrow street -> market square,
- alley -> sudden harbor vista,
- workshop yard -> glimpse of expedition mast,
- customs steps -> broad overview of harbor,
- tavern doorway -> noisy waterfront.

[PREFERRED] The expedition ship should appear in distant compositions whenever geographically reasonable.

[REQUIRED] Major districts must be distinguishable by silhouette, activity, props, sound, and movement patterns—not just UI labels.

---

# 5. The Expedition Ship as a Persistent Landmark

The ship is effectively a major character.

It should remain physically present throughout port preparation.

## Suggested preparation states

### State 0 — Awaiting Expedition
Sparse deck, maintenance activity, incomplete provisioning.

### State 1 — Inspection Underway
Crew or craftsmen inspect hull, rigging, sails, storage, and equipment.

### State 2 — Initial Loading
Water, food, tools, spare materials, and player purchases begin arriving.

### State 3 — Expedition Taking Shape
More crew present. Cargo organized. Personal belongings appear. Repairs reflect player choices.

### State 4 — Departure Ready
Cargo secured, crew assembled, rigging prepared, final checks underway.

### State 5 — Departure Morning
Gangplank activity, farewells, sponsor/customs presence where appropriate, last cargo and crew arrivals.

[REQUIRED] Player purchases should produce visible consequences where practical.

Examples:
- barrels physically appear,
- repair work occurs,
- livestock appears if purchased,
- cargo stacks change,
- damaged equipment disappears after repair,
- crew bring personal belongings aboard.

[REQUIRED] Do not require a unique bespoke model for every item. Use representative cargo groupings while keeping authoritative quantities in simulation data.

---

# 6. Population Simulation

Use three simulation tiers.

## Tier A — Persistent Characters

Examples:
- recruitable crew,
- merchants,
- officials,
- innkeepers,
- craftsmen,
- recurring contacts,
- sponsor representatives.

Features:
- persistent ID,
- schedule,
- relationships,
- knowledge,
- traits,
- occupation,
- relevant needs/state,
- memory/history,
- dialogue state,
- employment/recruitment status.

These characters can survive beyond the port if recruited.

## Tier B — Local Workers

Examples:
- dockworkers,
- fishermen,
- porters,
- sailors,
- vendors,
- apprentices.

Features:
- lightweight identity,
- schedule/activity loop,
- contextual animations,
- limited interaction,
- local persistence as needed.

## Tier C — Atmospheric Population

Examples:
- distant dock workers,
- shipboard silhouettes,
- people behind windows,
- distant boats,
- background market traffic.

Features:
- visual/audio atmosphere,
- no expensive full character simulation,
- may despawn or abstract when irrelevant.

[REQUIRED] Use simulation tiers to create the impression of a substantially larger population than the number of fully simulated entities.

---

# 7. Activity-Driven NPC Behavior

NPCs should move because they are performing activities.

Example dockworker:
warehouse -> acquire crate -> carry to quay -> transfer cargo -> short rest/conversation -> repeat.

Example fisherman:
boat -> unload catch -> market -> negotiate/sell -> return or leave.

Example carpenter:
workbench -> measure timber -> cut/shape -> inspect vessel -> speak to apprentice -> return.

Example recruitable navigator:
inn breakfast -> waterfront observation -> visit another vessel -> inspect expedition ship -> tavern/social contact -> lodging.

## Activity Node Model

World spaces should expose semantic activity nodes such as:
- `CarryPickup`
- `CarryDropoff`
- `WorkBench`
- `SawStation`
- `HammerStation`
- `RopeWork`
- `MarketStall`
- `ConversationPoint`
- `SitPoint`
- `LeanPoint`
- `DrinkPoint`
- `ShipInspection`
- `CargoInspection`
- `PrayerPoint`
- `RestPoint`
- `WatchWater`
- `BoardShip`
- `LeaveDistrict`

[REQUIRED] AI selects activities based on schedule, occupation, state, world conditions, and available nodes.

[REQUIRED] Nodes must support reservation/occupancy so multiple NPCs do not attempt to use the same physical point simultaneously.

---

# 8. Reusable Animation Library

Prioritize reusable work animation over excessive character polygon detail.

Initial animation set should target:
- carry crate,
- carry sack,
- carry barrel,
- two-person carry,
- push/pull cart,
- roll barrel,
- pull rope,
- tie/coil rope,
- climb ladder,
- hammer,
- saw,
- sweep,
- repair net,
- handle fish,
- load/unload ship,
- drink,
- eat,
- sit,
- lean,
- greet,
- point,
- inspect,
- argue,
- embrace/farewell,
- pray,
- rest,
- stumble,
- tend fire.

[REQUIRED] Animation must communicate purpose at normal isometric zoom.

[PREFERRED] Props align to reusable hand/interaction sockets rather than requiring bespoke animation for every object.

---

# 9. Recruitment Through the World

Recruitment loop:

**Encounter -> Investigate -> Evaluate -> Negotiate -> Recruit/Reject/Delay**

Candidates should not begin as fully known stat cards.

## Discovery channels
- direct conversation,
- observing work,
- introduction,
- tavern rumor,
- employer reference,
- previous captain/reference,
- official record,
- practical test,
- demonstration,
- other crew opinion.

Example:

> "There's a Portuguese pilot staying at the Crown. Came in aboard a Biscayan merchantman three nights ago."

Journal:
`Possible navigator — Crown Inn`

The player may arrive and discover the candidate is elsewhere. The innkeeper can provide a new lead.

This makes recruitment spatial and investigative.

---

# 10. Knowledge and Provenance

Use the project's established distinction:

**Actual Truth** — what is really true about the character.  
**Player Knowledge** — what the captain currently believes/knows.  
**Character Belief** — what another character believes, which may be incorrect.

Information should store provenance.

Example:

```
Navigation:
Claim: "Extensive Atlantic experience"
Source: Mateo Alvarez
Confidence: Low/Unverified
```

Later:

```
Navigation:
Assessment: Experienced
Source: Captain Almeida reference
Confidence: High
```

[REQUIRED] The UI should communicate uncertainty rather than secretly presenting exact hidden numbers.

[REQUIRED] Persuasion cannot override every motivation or constraint.

Candidates may reject the captain because of:
- wages,
- unsafe ship,
- destination,
- family obligation,
- reputation,
- departure timing,
- conflict with another crew member,
- contract terms,
- personal fear or ambition.

---

# 11. The Port Information Network

Information itself should move through the port.

Sources include:
- taverns,
- markets,
- sailors,
- craftsmen,
- merchants,
- officials,
- arriving ships,
- churches/community gathering spaces where historically appropriate,
- sponsor network.

Rumors may concern:
- candidate reputation,
- weather,
- route conditions,
- prices,
- arriving ships,
- disease,
- political conditions,
- another expedition,
- port closures,
- possible destinations.

[REQUIRED] Rumor does not equal truth.

[PREFERRED] Important information can be independently corroborated.

---

# 12. Ambient Micro-Events

The port should generate small stories that are not necessarily quests.

Examples:
- sailors argue,
- worker drops cargo,
- animal refuses to move,
- thief runs through market,
- sailor sings while working,
- child follows a procession,
- drunk insults someone,
- religious procession temporarily blocks street,
- person falls from dock,
- ship unexpectedly arrives,
- funeral passes,
- family says goodbye,
- minor workplace accident,
- customs dispute,
- cart wheel breaks,
- sudden rain sends vendors covering goods.

Most should last 10–30 seconds.

[REQUIRED] Most ambient events should not reward the player.

[REQUIRED] Events must respect location, time, weather, occupation, and historical plausibility.

[PREFERRED] Some events can create persistent consequences if the player becomes involved.

---

# 13. Time and Daily Rhythm

The port must change with time.

Example activity profile:

### Early Morning
fishermen arriving, bakers/vendors preparing, dock crews assembling, bells, carts.

### Morning
heavy loading/unloading, workshops active, markets opening, officials available.

### Midday
peak market/social density, merchants active, taverns filling.

### Afternoon
continued trade/work, candidate movement, ships preparing departure/arrival.

### Evening
workshops close, taverns intensify, fewer dock workers, more social encounters.

### Night
reduced traffic, watchmen, taverns, water/wind/rigging dominant.

[REQUIRED] Persistent NPC schedules should reflect occupation and personal circumstances.

[REQUIRED] Waiting has consequences. Candidates may leave, stock may sell, deadlines advance, weather windows change.

---

# 14. Weather Reactions

Weather is not merely a visual effect.

## Rain
- vendors cover goods,
- pedestrians seek cover,
- stone/timber wetness changes,
- workers alter tasks,
- cargo may need protection.

## Wind
- flags, clothing, smoke, rigging, and loose canvas react,
- ship crews secure equipment,
- harbor water changes.

## Fog
- visibility reduced,
- ship silhouettes disappear into depth,
- sound becomes more important,
- some harbor operations slow.

## Approaching Storm
- sky/light shifts,
- cargo is secured,
- crews move with urgency,
- harbor traffic changes.

[REQUIRED] Weather should alter visuals, sound, and at least some NPC behavior.

---

# 15. Sound Design

Sound is a major component of perceived population density.

Use spatial zones and layered emitters.

## Quayside
waves, hull creaks, gulls, rope strain, canvas, bells, shouted work calls, cargo impacts.

## Market
vendors, animals, carts, coins, crowd conversation, tools.

## Workshops
hammering, sawing, metalwork, rope work, apprentices.

## Tavern
conversation, chairs, cups, fire, singing/music when appropriate.

## Customs/Sponsor
quieter interior, papers, footsteps, restrained street noise.

[REQUIRED] Exterior sound should become muffled when entering enclosed interiors.

[REQUIRED] Soundscape changes with time and weather.

[PREFERRED] Important NPC activity can occasionally be discovered by sound before sight.

---

# 16. Expedition Recognition System

The captain's social presence in the port should grow as preparation advances.

Potential inputs:
- number of crew hired,
- money spent,
- sponsor prominence,
- ship size,
- public incidents,
- reputation,
- days remaining,
- unusual cargo or passengers.

Possible consequences:
- merchants greet captain,
- candidates approach,
- creditors notice,
- rumors spread,
- sponsor representative visits,
- families gather,
- workers refer to the expedition,
- prices/offers may react where justified.

[REQUIRED] Do not represent this only as a numeric fame bar. It should manifest through world behavior and dialogue.

---

# 17. Departure Sequence

Departure is the payoff for the entire port phase.

[REQUIRED] Use the actual recruited characters.

[REQUIRED] Use actual cargo/provision state.

[REQUIRED] Reflect relevant relationships and events.

Possible beats:
1. final deadline/check,
2. crew moves toward ship,
3. personal farewells,
4. last cargo arrives,
5. customs/sponsor resolution,
6. captain boards,
7. gangplank removed,
8. lines released,
9. vessel begins moving,
10. port gradually recedes.

[PREFERRED] Temporarily reduce management UI during the emotional portion of departure.

[PREFERRED] Allow procedural variations:
- family farewell,
- recruit with no one present,
- creditor confrontation,
- late crew member,
- sponsor messenger,
- unresolved argument,
- injured recruit struggling aboard.

[DON'T] Replace the whole sequence with a generic pre-rendered cinematic that ignores the player's actual expedition.

---

# 18. Performance Strategy

The port must feel busy without simulating every visible person at full fidelity.

Use:
- character simulation tiers,
- animation LOD,
- AI update staggering,
- distance-based behavior frequency,
- pooled props/characters where appropriate,
- occlusion/frustum culling,
- simplified distant navigation,
- background ship/worker impostors or simplified actors,
- audio virtualization,
- event budget limits.

[REQUIRED] Persistent recruitable characters must never lose identity because of LOD transitions.

[REQUIRED] Simulation truth must remain separate from visual representation.

[PREFERRED] Crowd density should degrade gracefully on lower hardware before core persistent-character behavior is reduced.

---

# 19. Vertical Slice Population Target

Initial target—not final production mandate:

- 20–30 meaningful persistent/local NPCs active across the playable port,
- ~6 serious recruitable candidates,
- additional lightweight/background population sufficient to imply a much busier harbor,
- 1 expedition ship,
- several background vessels,
- five compact districts,
- one complete day/night activity cycle,
- at least one rain/fog/weather transformation,
- 8–12 ambient micro-events,
- complete departure sequence.

[APPROVAL REQUIRED] Final NPC counts and performance budgets must be validated against the selected engine/platform and repository audit.

---

# 20. Visual Reference Board

These are **research categories**, not assets to copy.

## A. Renaissance / Early-16th-Century Harbor Paintings
Search targets:
- early 16th century European harbor painting,
- Renaissance port scene ships market,
- historical quay painting 1500s.

Extract:
- layered silhouettes,
- density,
- harbor composition,
- relationship between buildings and water,
- visual clutter,
- ship scale.

Do not copy:
- identifiable composition,
- individual figures,
- unverified fantasy/anachronistic details.

## B. Carrack / Nao Reconstructions
Search targets:
- carrack 1500 reconstruction,
- nao Victoria replica,
- early 16th century sailing ship reconstruction,
- Santa Maria reconstruction with scholarly caveats.

Extract:
- human-to-ship scale,
- deck organization,
- rigging density,
- cargo handling,
- mast/sail silhouettes,
- material wear.

[RESEARCH] Validate the project's final hero ship against reputable maritime-history sources before canonizing it.

## C. Dock Labor
Search:
- historical dock workers barrels crane,
- preindustrial harbor cargo handling,
- historical ship loading reconstruction.

Extract:
- two-person work,
- rope handling,
- carts,
- barrels/sacks/crates,
- staging areas,
- believable workflow.

## D. Historical Markets and Workshops
Search:
- Renaissance market painting,
- 16th century crafts workshop illustration,
- early modern European market reconstruction.

Extract:
- stall density,
- occupation-specific props,
- social mixing,
- narrow circulation,
- working rather than decorative clutter.

## E. Fog / Rain / Storm Harbors
Search:
- old master harbor fog,
- historic harbor rain painting,
- maritime storm harbor painting.

Extract:
- atmospheric depth,
- silhouettes,
- reflections,
- contrast changes,
- sky/water relationship.

## F. Shipyards
Search:
- historical wooden shipyard reconstruction,
- early modern shipbuilding illustration,
- Renaissance shipyard.

Extract:
- timber storage,
- scaffolding,
- tools,
- partially repaired/built vessels,
- worker choreography.

## G. Departure / Farewell Scenes
Search:
- historical ship departure painting,
- emigrant ship farewell painting,
- harbor family farewell art.

Extract:
- emotional staging,
- crowd focus,
- luggage,
- gestures,
- contrast between workers and family members.

Do not copy later-period clothing or ship technology merely because the emotional composition is useful.

## Reference Rule

[REQUIRED] Reference imagery is used to answer questions about **structure, material, atmosphere, scale, activity, and emotion**.

[DON'T] Treat reference images as direct generation targets.

[RESEARCH] Any historically consequential detail must be validated for the final chosen port, year, culture, and ship type.

---

# 21. Four Visual Validation Scenes

Before large-scale asset production, validate the port art direction using four representative scenes.

## Scene A — Dawn Quayside
Ship partially obscured by mist; fishermen and cargo workers starting day.

Tests:
lighting, water, ship scale, silhouettes, sound layering.

## Scene B — Midday Market
Highest visual density.

Tests:
crowd readability, props, color hierarchy, NPC activity, UI legibility.

## Scene C — Rainy Workshop Street
Craftsmen adapting to weather; wet materials.

Tests:
wetness, rain response, interiors/exteriors, weather AI.

## Scene D — Departure Morning
Actual recruited crew and prepared ship.

Tests:
emotional readability, persistent identities, crowd tiers, ship state, procedural farewell staging.

[REQUIRED] The art/technical team should prove these scenes before scaling asset production.

---

# 22. Acceptance Criteria

The port vertical slice is not complete merely because the player can walk around it.

It must demonstrate:

- [ ] five recognizable interconnected districts,
- [ ] expedition ship visible and persistent,
- [ ] ship visibly changes with preparation,
- [ ] recruitable candidates follow purposeful schedules,
- [ ] player can discover a candidate through a lead rather than a menu,
- [ ] candidate information includes uncertainty/provenance,
- [ ] at least three NPC activity loops visibly work,
- [ ] crowd tiers create apparent density,
- [ ] time of day changes activity and sound,
- [ ] weather changes visuals plus behavior,
- [ ] ambient events occur without player-centric rewards,
- [ ] purchased supplies affect authoritative cargo state,
- [ ] representative cargo appears physically,
- [ ] port recognition changes as expedition develops,
- [ ] departure uses actual crew/cargo/state,
- [ ] save/load preserves persistent NPC and expedition state,
- [ ] performance remains within project targets,
- [ ] no major historical detail was canonized from unverified generated content.

---

# 23. Agent Implementation Order

Do not attempt to build the finished port at once.

Recommended sequence:

1. Greybox five-district layout and harbor sightlines.
2. Place persistent expedition ship landmark.
3. Implement semantic activity-node system.
4. Implement one Tier-A candidate schedule.
5. Implement several Tier-B worker loops.
6. Add crowd Tier-C representation.
7. Connect character knowledge/provenance to recruitment.
8. Add port clock and schedule transitions.
9. Add ship preparation state visualization.
10. Add spatial audio zones.
11. Add one weather state and behavioral reactions.
12. Add ambient micro-event scheduler.
13. Add expedition-recognition reactions.
14. Build procedural departure sequence.
15. Validate four art scenes.
16. Profile and tune crowd/performance.
17. Expand content only after systems are proven.

---

# 24. Agent Guardrails

[REQUIRED] Read the main Handoff, Art Bible, and Technical Spec before implementing this system.

[REQUIRED] Audit the repository before selecting new frameworks/plugins.

[REQUIRED] Prefer reusable systems over scripted one-off scenes.

[REQUIRED] Keep simulation state authoritative and rendering replaceable.

[REQUIRED] Add automated tests for stateful logic where practical.

[APPROVAL REQUIRED] Major changes to port geography, historical location, hero ship type, crowd technology, or core recruitment loop.

[DON'T] Buy or introduce large asset packs that dictate a conflicting art style without approval.

[DON'T] optimize away persistent identity.

[DON'T] let procedural generation create historical canon without validation.

---

# 25. Final Design Test

Ask this question:

> If the graphics were temporarily replaced by simple shapes, would the port still feel alive because people have jobs, schedules, relationships, information, and reasons to move?

If the answer is no, the simulation is too dependent on decoration.

Then ask:

> If the simulation were frozen for a screenshot, would the scene still clearly communicate a crowded early-16th-century expedition port through composition, material, lighting, silhouettes, and activity?

If the answer is no, the visual presentation is too dependent on systems.

**The target is both.**

The port should be a functioning simulation that also produces beautiful historical scenes naturally.
