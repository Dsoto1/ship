# THE EXPEDITION — CONTINUOUS ECONOMY, LABOR & COMMUNITY LOOP

**Status:** Authoritative cross-phase gameplay specification  
**Scope:** Recruitment → Port Preparation → Voyage → Shipwreck/Landfall → Camp → Settlement → Established Community

## 0. Core Thesis

The economic game begins when the captain accepts the expedition, not when a settlement is founded.

The player leaves Europe carrying a finite collection of **people, skills, knowledge, tools, food, water, medicine, repair materials, trade goods, money, relationships, promises, information and time**.

> **PORT — Choose.**  
> **VOYAGE — Preserve.**  
> **LANDFALL — Transform.**  
> **SETTLEMENT — Sustain.**  
> **ESTABLISHED COMMUNITY — Prosper and endure.**

The campaign is the attempt to transform finite expedition resources into renewable systems capable of sustaining human life.

## 1. Central Architecture

[REQUIRED] Do not create independent port, voyage and settlement economies.

Build shared systems for characters, skills, tasks, work, tools, inventory, containers, resources, consumption, production, hauling, storage, health, fatigue, knowledge, relationships, priorities, time, events and history.

The phase changes available tasks, workplaces, environmental constraints, production opportunities, risk, scale and UI—not economic truth.

## 2. People Are the Economy

Every meaningful unit of early labor comes from an actual persistent person. A task is performed by **Mateo Alvarez**, not `Builder #3`.

Losing a specialist means losing labor, expertise, knowledge, relationships, teaching potential and future production capacity.

## 3. Universal Loop

**Assess → Prioritize → Assign → Perform → Move → Produce/Repair → Consume → Observe → Adapt**

At larger scale:

**Plan → Allocate Labor → Secure Inputs → Produce → Store/Distribute → Consume → Maintain → Prepare → Expand**

## 4. Port — Choose the Future Community

Recruitment is the first settlement decision. Candidates are multi-skilled people, not rigid classes.

Example:

**Mateo Alvarez — Ship Carpenter**
- Carpentry: Experienced
- Ship Repair: Experienced
- Sailing: Basic
- Fishing: Basic
- Leadership: uncertain
- Cooking: unknown

He may repair the ship, build shelter, establish a workshop and later teach apprentices. His context changes; his identity does not.

## 5. Berths and Recruitment

Limited berths, money and provisions create the first labor puzzle. Voyage coverage competes with future landfall capability.

Each recruit adds labor, skills and knowledge, but also food/water consumption, berth use, wages, medical risk and social complexity.

There is no universally perfect roster.

Candidate information remains imperfect: claimed, rumored, referenced, observed or confirmed.

## 6. Cargo Is the Second Founding Decision

Every item competes for finite cargo capacity.

**Voyage Survival ↔ Landfall Capability ↔ Settlement Investment**

Examples:
- water vs carpentry tools,
- repair material vs seed,
- medicine vs trade goods,
- provisions vs agricultural equipment.

[REQUIRED] Cargo choices survive phase transitions. Do not reset resources at landfall.

## 7. Same Goods, Continuous State

A barrel bought in port may be loaded aboard, damaged at sea, salvaged after wreck and reused at settlement.

At minimum quantity, condition and provenance persist.

Important containers and items may retain lightweight history; do not track provenance for every nail.

## 8. Finite vs Renewable

Initially finite:
- provisions,
- stored water,
- medicine,
- tools,
- repair material,
- money,
- trade goods,
- imported seed,
- specialist knowledge embodied in people.

Potentially renewable:
- water,
- fish,
- crops,
- timber,
- some materials,
- local trade,
- trained labor,
- accumulated knowledge.

Settlement success is converting finite stores into reliable renewable systems before the finite base collapses.

## 9. The Ship Is a Moving Settlement

The ship is simultaneously home, workplace, warehouse, transportation, food/water store, repair site, social environment and survival system.

At sea, **watches replace workplaces**.

Example:
- Helm — Tomás
- Rigging — Diego + João
- Lookout — Ilario
- Cook — Martín
- Maintenance — Mateo
- Rest — 3 crew

Every reassignment creates opportunity cost.

## 10. Shared Task Model

A task should conceptually support:
`taskId, type, location, priority, inputs, desiredSkills, minimumCapability, tools, assignedCharacters, estimatedDuration, progress, interruptConditions, outputs, risks, state`.

The same foundation powers hull repair, cargo loading, cooking, fishing, shelter construction, timber cutting, treatment and harvesting.

## 11. Physical Work and Logistics

Resources should move physically where practical.

Port:
**merchant/warehouse → porter → dock staging → ship**

Voyage:
**hold → worker → repair/worksite**

Settlement:
**tree → log → hauling → stockpile → construction**

Hauling requires people/transport, time, access, path and capacity. Storage is not globally accessible.

## 12. Storage

Storage affects travel time, spoilage, damage, weather exposure and emergency access.

Examples:
- grain beside a leak is vulnerable,
- food outdoors spoils faster,
- tools near a workshop reduce hauling,
- medical stores need accessibility.

## 13. Tools Are Productive Capital

Tools change feasibility, speed, quality and risk. A skilled worker without tools may be severely constrained.

Meaningful tools may wear, break, require repair or be lost. Avoid durability micromanagement for trivial objects.

## 14. Knowledge Is Productive Capital

Navigation, carpentry, medicine, language, agriculture, geography and seasonal/environmental knowledge can exist in people, be recorded, taught, exchanged and lost.

A specialist's survival matters because knowledge is economically productive.

## 15. Apprenticeship and Redundancy

Assisting skilled workers can gradually teach competence.

Mateo Carpentry 4 + Ilario Carpentry 0:
- Ilario assists voyage repairs,
- gains familiarity,
- performs simple landfall construction,
- later becomes a competent backup.

Learning is gradual and cannot erase recruitment choices.

A robust expedition deliberately creates redundancy.

## 16. Food Continuity

**Port — Purchase → Voyage — Store/Consume/Supplement → Landfall — Consume/Forage/Fish/Hunt/Trade → Settlement — Produce/Preserve/Store/Consume**

The satisfying transition is when reliable local food production begins exceeding ordinary consumption.

## 17. Water Continuity

**Port — Acquire → Voyage — Store/Ration/Lose/Replenish → Landfall — Search/Transport/Treat → Settlement — Secure reliable source/infrastructure**

Water is both a resource and logistics problem.

## 18. Projection and Rationing

Use estimated projections such as:

**Fresh water: ~18–22 days at current use**

Avoid false precision.

Policies such as normal, reduced and emergency rationing change consumption but also affect health, fatigue, morale and trust.

## 19. Health, Rest and Maintenance Are Economic

Illness and injury reduce labor. Caregiving consumes labor. Rest competes with work. Overwork increases mistakes, injury and future inefficiency.

Preventive maintenance competes with immediate production but reduces future crisis risk.

## 20. Causal Crises

Prefer cascades:

**contaminated water → illness → fewer workers → harvest delay → falling food reserve → rationing → conflict → paused construction**

The player should understand why the crisis happened.

Weather similarly changes work, safety, storage, crops, fishing, sailing and health.

## 21. Shipwreck Transforms the Economy

Shipwreck does not spawn a generic survival inventory.

Preserve exact survivors, injuries, relationships, knowledge, equipment, recoverable/lost/damaged cargo and wreck condition.

The wreck may become a salvage source for timber, rope, sailcloth, containers, tools, fittings and provisions. Salvage requires labor, time, access and risk.

## 22. Successful Landfall Also Has a Cost

An intact ship may preserve most people and cargo, but it remains a strategic asset requiring crew, maintenance and materials.

The player chooses how much labor remains maritime versus moving ashore.

## 23. Landfall — Transform

Immediate questions:
1. Who survived?
2. Who needs treatment?
3. What remains?
4. Where is reliable water?
5. Where can people sleep safely?
6. What can be eaten?
7. What can be salvaged?
8. What environmental risks are known?
9. What remains unknown?

First 72 hours emphasize triage, water, shelter, fire/cooking, secure storage, sanitation, food and reconnaissance.

## 24. Camp Before Town

**Landing Site → Emergency Camp → Survival Camp → Outpost → Settlement → Stable Community**

Do not expose a giant building catalog immediately.

Emergency: fire, lean-to, cache, latrine, medical shelter.

Survival: shelters, storehouse, water infrastructure where viable, cook area, workshop, infirmary, landing/dock.

Outpost: proper homes, expanded storage, specialized work, preservation, agriculture, trade area, improved dock.

Settlement: specialized production, roads, communal structures, larger storage, administration and justified defenses.

## 25. Construction

Construction consumes actual location, materials, tools, labor, skill and time. Quality can depend on lead skill, materials, weather, urgency and assistance.

[DON'T] Spend abstract currency to instantly spawn buildings.

## 26. Organic Paths

Repeated movement may create desire paths:
beach → water, camp → forest, storehouse → dock, homes → workshop.

Later, improve these into formal roads. The settlement's circulation becomes a record of its development.

## 27. Short Production Chains

Avoid factory-game sprawl.

Examples:
- Tree → Log → Timber → Construction
- Fish → Prepare/Preserve → Store → Meal
- Crop → Harvest → Store → Cook → Meal
- Fiber/material → Process → Cloth → Clothing/repair

The important questions are **who works, where, with what, and what is blocking them?**

## 28. Bottlenecks

Output can be limited by labor, skill, tool, input, storage, transport, weather, health, knowledge or access.

UI communicates cause:
**Storehouse stalled — timber unavailable**
rather than simply `Production 0%`.

## 29. Labor Tension

There should often be more desirable work than available people. Strategy comes from deciding what **not** to do.

Priorities may exist at task, workplace, project and emergency levels: Critical / High / Normal / Low / Paused.

Priority cannot override impossibility or severe human needs.

## 30. Delegation Progression

- Expedition: individuals.
- Camp: individuals + tasks.
- Outpost: workplaces + key individuals.
- Settlement: priorities + workplaces + key people.
- Mature community: policies + leadership + exception management.

The player can always drill down to a named person.

## 31. Workplaces

A workplace has physical location, tasks, tools, inputs, outputs, assigned people and storage links.

Experienced characters can lead workplaces, affecting organization, teaching, quality, throughput and safety.

This creates meaningful late-game roles for veteran characters.

## 32. Households and Housing

As the expedition becomes community, people may form households with shared dwelling, possessions, consumption and relationships. Exact family depth is [APPROVAL REQUIRED].

Housing affects rest, exposure, health, privacy, belonging and storage. Avoid one opaque "housing happiness" statistic.

## 33. Community Condition

Prefer explainable state:

**Community Morale — Strained**
- recent deaths
- poor food variety
- overcrowding
+ respected captain
+ successful harvest

The player should know why.

## 34. Governance and Policies

As population grows, control may shift from captain orders toward foremen, advisors/council and policies. This exists to reduce micromanagement, not become a separate grand-strategy game.

Potential policies:
- ration level,
- work hours,
- emergency labor,
- reserve targets,
- exploration priority,
- trade restrictions.

## 35. Seasonal Macro Loop

Adapt to the approved geography rather than assuming European seasons.

General rhythm:

**Favorable period → build/explore/produce → preparation → harvest/preserve/repair/stockpile → difficult period → consume/maintain/survive → recovery**

Forecast quality depends on expedition knowledge, not omniscient calendars.

## 36. Knowledge of Land

Initially uncertain:
- soil,
- flood risk,
- water reliability,
- seasonal weather,
- edible plants,
- hazards,
- routes.

Knowledge grows through observation, exploration, failure, records, trade and local expertise.

Wrong decisions can teach rather than being meaningless random punishment.

## 37. Exploration Is Economic Investment

Exploration consumes labor, food, time, equipment and risk.

It may return water sources, food/materials, routes, geographic knowledge, communities, hazards and trade opportunities.

Exploration therefore competes directly with immediate production.

## 38. External Communities

Indigenous societies and other settlements are autonomous communities with people, goals, economy, territory, knowledge, relationships and politics.

They are not resource nodes.

Trade and diplomacy can exchange goods, route/environmental knowledge, agricultural techniques, language and seasonal information.

Representation requires specific historical research for the approved region/community.

## 39. Population Growth Means People

New arrivals should be actual persistent people: sponsor settlers, specialists, merchants, another expedition, survivors, families or other historically plausible arrivals.

Avoid anonymous `Population +5`.

Natural births/children require a campaign timescale that makes them meaningful and are [APPROVAL REQUIRED].

## 40. Internal Distribution

As scale grows, the captain should not assign every meal.

Storehouses, households, kitchens and appropriate distribution systems increasingly automate routine transfers while the player manages capacity, policy and exceptions.

## 41. Efficiency vs Resilience

Efficiency:
one expert performs all critical work.

Resilience:
multiple people possess adequate capability.

The game should reward backups, multiple food/water sources, spare tools/materials and reserve labor.

Running at 100% utilization should make the expedition fragile.

## 42. Economic Feedback

Examples:

**Fishing output low**
- two fishers injured
- rough sea
- boat damaged

**Water reserve falling**
- population increased
- hot weather
- secondary source contaminated

Prefer conditions, causes, projections and bottlenecks over number soup.

## 43. Departure Readiness

Advisory summaries may show:

**Voyage**
- Sailing coverage: Good
- Navigation: Strong
- Repair: Good
- Medical: Weak
- Water: Fair

**Landfall**
- Construction: Good
- Food acquisition: Fair
- Agriculture: Poor
- Trade goods: Strong

[DON'T] Provide a single `87% Ready` optimizer score.

## 44. Self-Sufficiency

Self-sufficiency is multidimensional:
water, food, shelter, health, tools/repair, storage, labor redundancy, social stability, trade/communication.

Milestones may include:
- reliable local water,
- first permanent storehouse,
- durable homes,
- successful harvest,
- locally repaired/replaced critical tool,
- trained apprentice,
- stable trade relationship,
- first difficult season survived.

## 45. Buildings and People Carry History

Example:

**Old Storehouse**
- Built Day 73
- Lead Carpenter: Mateo Alvarez
- Assisted by Ilario Costa and Tomás Vega
- Timber partly salvaged from expedition ship
- Survived storm Day 119
- Repaired Day 121

Character records similarly preserve recruitment role, voyage duties, learned skills and settlement leadership.

## 46. Failure Continuation

Where plausible:
- shortage → ration/trade/relocate,
- ship damage → wreck,
- crop failure → emergency fishing/trade,
- settlement collapse → abandonment/relocation.

Not every disaster is survivable, but avoid arbitrary game-over thresholds.

If relocation occurs, pack actual goods, move actual survivors and continue campaign history.

## 47. Phase Teaching

Port teaches people, uncertainty and finite cargo.

Voyage teaches assignment, scarcity, consumption, maintenance, rest, storage and crisis.

Landfall adds renewable resources, construction, exploration and local logistics.

Settlement adds production, workplaces, households, seasonal planning and delegation.

## 48. Visual Connection

Economic activity should be visible where practical.

If construction occurs, people/materials are present.
If cargo unloads, goods move.
If food is produced, someone fishes, farms, cooks, carries or stores.

Decorative animations must not falsely imply production that is not occurring.

## 49. Technical Model

Prefer shared definitions such as:
- `ResourceDefinition`
- `ItemInstance`
- `Container`
- `TaskDefinition`
- `TaskInstance`
- `WorkplaceDefinition`
- `WorkplaceInstance`
- `ProcessDefinition`
- `CharacterAssignment`
- `KnowledgeRecord`
- `EconomicEvent`

A process may define inputs, outputs, tools, skill weights, labor, duration, workplace/location, environmental constraints, quality, byproducts and risk.

Containers include personal inventory, crate/barrel, ship hold, dock staging, stockpile, storehouse, household and workshop storage.

Tasks reserve inputs where needed and release reservations on completion/cancellation/invalidation.

## 50. Abstraction and Performance

Do not simulate every grain or nail physically.

Physically represent meaningful containers, major cargo, work bundles and visible stockpiles; aggregate tiny interchangeable goods internally.

Near/important simulation may show detailed hauling. Distant simulation may batch transfers at lower frequency, but camera position must not change economic truth.

## 51. Save/Load

Persist tasks, reservations, inventories, containers, workplace state, production progress, crop/season state, policies and economic history.

Mid-storm and mid-construction saves must restore correctly.

## 52. Automated Tests

Minimum:
- port cargo persists to ship,
- consumed goods never reappear,
- wreck loss persists,
- salvage becomes camp inventory,
- dead worker cannot produce,
- injuries affect work,
- tool absence blocks/reduces tasks,
- reservation prevents double spending,
- hauling transfers between valid containers,
- consumption scales with people/policy,
- apprenticeship gains bounded experience,
- workplace output respects constraints,
- save/load preserves progress,
- LOD cannot create resources,
- settlement cannot consume nonexistent ship cargo.

## 53. Cross-Phase Vertical Slice

**Port:** recruit 6–8 people and buy finite supplies.

**Voyage:** simulate several days, consume supplies, maintain ship, train one assistant, damage/lose a resource.

**Crisis:** storm causes injury and cargo/ship consequence.

**Landfall:** unload exact surviving people/items.

**Camp:** secure water, shelter, food and storage.

**Early Settlement:** construct one permanent building and establish one renewable food source.

Every person/item/resource must trace correctly through the chain.

## 54. Scenario — The Carpenter

1. Recruit Mateo for ship repair.
2. Ilario assists him during voyage.
3. Ilario gains basic carpentry.
4. Storm injures Mateo.
5. Landfall occurs.
6. Ilario performs basic shelter work.
7. Mateo recovers and leads storehouse construction.
8. Months later Ilario can lead simple repairs.

This should emerge from systems, not scripted quest logic.

## 55. Scenario — Lost Water Barrel

1. Buy four barrels.
2. Load four aboard.
3. One leaks.
4. One is lost.
5. Two reach land.
6. Camp begins with those two.
7. Water search becomes urgent.
8. Reliable source is found.
9. Barrels become transport/storage equipment.

No reset occurs.

## 56. Scenario — Successful vs Wrecked Arrival

**Successful:** intact ship, most cargo, maritime labor choice, ship supports fishing/trade/exploration.

**Wrecked:** simulation-derived losses, triage, salvage, sailcloth becomes shelter, ship timber becomes construction material.

Both are valid continuations with radically different economic starts.

## 57. Implementation Order

### A — Shared Foundation
resources/items → containers/transfers → tasks/processes → assignments → consumption → tools → reservations → hauling → save/load → economic logging.

### B — Port
recruit capability → purchase/load cargo → ship hold → readiness summary.

### C — Voyage
watches/duties → consumption → maintenance → fishing → fatigue/rest → cargo damage/loss → apprenticeship.

### D — Transition
shipwreck/landfall transfer → salvage → camp storage → first-72-hour tasks.

### E — Settlement
construction → stockpiles → renewable food/water → workplaces → logistics → seasonal pressure → delegation → households/community.

### F — Mature Community
trade → migration → policies → leadership delegation → advanced production → regional economy.

## 58. Guardrails

[DON'T] Create a separate settlement inventory.
[DON'T] Reset supplies on landfall.
[DON'T] Replace crew with anonymous workers.
[DON'T] Teleport resources globally.
[DON'T] build enormous production chains early.
[DON'T] create dozens of meaningless resources.
[DON'T] allow infinite labor without rest.
[DON'T] make every crisis a random event card.
[DON'T] turn communities into vending machines.
[DON'T] let delegation erase named people.
[DON'T] use one opaque happiness number.

## 59. Acceptance Criteria

- recruitment affects future labor
- berth/cargo constraints matter
- people/goods persist
- voyage and settlement share labor/task foundations
- watches function as maritime workplaces
- hauling/storage/tools/knowledge matter
- apprenticeship creates redundancy
- health/rest affect labor
- crises emerge causally
- wreck transforms rather than resets economy
- landfall uses actual survivors/cargo
- construction consumes real labor/material
- production chains remain legible
- scarcity creates priorities
- delegation increases with scale
- characters remain inspectable
- external communities remain autonomous
- environmental/seasonal planning matters
- settlement can become self-sufficient
- buildings/people retain history
- failure can branch
- save/load preserves economy
- simulation remains testable/headless where practical

## 60. Final Design Tests

> Does a recruitment decision made in the first hour still matter after landfall?

> Can I trace settlement people, tools and supplies back through the voyage?

> Is the ship already teaching the labor/logistics rules used on land?

> Does a specialist's death matter because of what that person actually knew and did?

> Does self-sufficiency come from renewable systems rather than arbitrary unlock tiers?

> Does delegation grow without losing named individuals?

> Can disaster create a different playable economic problem instead of merely displaying Game Over?

## 61. Final Principle

> **The Expedition begins as a finite collection of people, knowledge, tools and supplies. The campaign is the story of whether those finite resources can survive the crossing, adapt to disaster, and become a self-sustaining human community.**

This principle governs recruitment, voyage, landfall, settlement, economy, labor, logistics, progression and failure.
