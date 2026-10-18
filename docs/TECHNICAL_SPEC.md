# THE EXPEDITION — TECHNICAL SPECIFICATION

**Document:** `The_Expedition_Technical_Spec.md`  
**Status:** Authoritative implementation specification  
**Companion documents:** `The_Expedition_Agent_Handoff.md`, `The_Expedition_Art_Bible.md`  
**Target era:** approximately 1500–1520  
**Game type:** single-player isometric historical survival, expedition, and settlement simulation

---

## 0. PURPOSE AND AGENT OPERATING INSTRUCTIONS

This document translates the game design into implementation rules. It does **not** authorize an agent to redesign the game, replace the technology stack, or silently resolve open design questions.

### Required reading order

1. `The_Expedition_Agent_Handoff.md` — product vision, gameplay, priorities, milestones.
2. `The_Expedition_Art_Bible.md` — visual requirements and research constraints.
3. `The_Expedition_Technical_Spec.md` — architecture and implementation rules.

### Priority labels

- **[REQUIRED]** Must be followed unless technically impossible; document the reason if blocked.
- **[PREFERRED]** Default approach; alternatives require a concrete benefit.
- **[RESEARCH]** Verify before implementing; do not invent historical or technical facts.
- **[APPROVAL REQUIRED]** Do not make a permanent project-wide decision without owner approval.
- **[DON'T]** Explicitly prohibited.

### First action for any new coding agent

**[REQUIRED] Do not begin feature implementation immediately.** First perform a repository audit and report:

- engine/framework/runtime and versions;
- repository structure and major modules;
- current build/run/test commands;
- current playable functionality;
- persistence/save implementation, if any;
- rendering and UI stack;
- existing data models and content formats;
- automated tests and CI;
- known warnings/errors;
- third-party dependencies/licenses that materially affect development;
- conflicts between current architecture and these specifications;
- recommended next 3–8 implementation tickets.

Do not replace an existing viable architecture merely because another stack is preferred.

---

# 1. TECHNICAL NORTH STAR

The Expedition is one **continuous persistent simulation** presented through several gameplay contexts: port preparation, ocean voyage, disaster/survival, landfall, and settlement.

**[REQUIRED] The same people and campaign state must persist across these contexts.**

A recruited character is not converted into a generic sailor during the voyage or a generic worker after landfall. Their identity, skills, injuries, health, equipment, relationships, promises, knowledge, history, and survival status continue with them.

The central architecture should therefore be:

```text
Persistent Campaign State
│
├── Character Simulation
├── Inventory / Resource Simulation
├── Time / Task Simulation
├── Relationship / Knowledge Simulation
├── Ship Simulation
├── Environment / Weather Simulation
├── Event / Consequence System
├── Objectives / Charter State
└── Chronicle / History
        │
        ▼
Gameplay Context / Presentation
├── Port
├── Voyage
├── Shipwreck / Survival
└── Settlement
        │
        ▼
Rendering / UI / Audio / Input
```

**[REQUIRED] Simulation truth must not depend on UI widgets, animation state, or scene objects.**

---

# 2. ARCHITECTURAL PRINCIPLES

## 2.1 Simulation separate from presentation

**[REQUIRED]** Core rules should be executable without rendering wherever practical.

Examples:

- hunger progression must not require a character model to exist;
- a repair task must be resolvable without playing an animation;
- a storm must be able to damage a ship in a headless test;
- save/load must reconstruct simulation state independent of scene initialization order.

**[PREFERRED]** Use plain data/state objects and dedicated systems/services for simulation logic rather than embedding all logic in visual scene components.

## 2.2 Data-driven content

**[REQUIRED]** Definitions that designers will tune repeatedly should be externalized from core logic where the chosen stack reasonably supports it.

Candidates include:

- skills;
- traits;
- occupations;
- items;
- recipes;
- tasks;
- injuries/conditions;
- ships and ship components;
- weather types;
- buildings;
- candidate-generation templates;
- events;
- sponsor charters;
- localization strings.

Definitions should have stable IDs. Display names are not identifiers.

## 2.3 Composition over giant inheritance trees

**[PREFERRED]** Model capabilities through components/data and systems rather than deep class hierarchies such as `Human > Sailor > Navigator > MasterNavigator`.

A character can simultaneously be an experienced sailor, competent carpenter, poor negotiator, injured, multilingual, indebted to another character, and assigned to pumping the bilge. The model must support combinations naturally.

## 2.4 Deterministic and inspectable simulation

**[REQUIRED]** Randomized simulation must support seeded/reproducible execution for testing and debugging.

When a meaningful outcome occurs, the system should be able to explain the causal inputs: relevant skill, condition, tools, environment, task difficulty, modifiers, and random result where applicable.

**[DON'T]** Hide core outcomes behind opaque randomness that cannot be reproduced.

## 2.5 Stable identifiers

**[REQUIRED]** Persistent entities receive stable unique IDs that survive save/load and scene/context transitions.

Never use display name, array position, scene node path, memory address, or UI index as persistent identity.

---

# 3. ENGINE AND PLATFORM POLICY

## 3.1 Engine choice

**[APPROVAL REQUIRED]** Selecting a new engine, migrating engines, or replacing the current primary framework.

Unity and Godot are both plausible candidates if no viable implementation exists. This specification intentionally does not choose between them before repository inspection.

**[DON'T]** Build a custom engine for this project unless the owner explicitly authorizes it after a demonstrated requirement.

## 3.2 Initial platform

**[PREFERRED]** Desktop-first development with mouse/keyboard controls. Keep architecture compatible with controller support where inexpensive, but controller/mobile parity is not a P0 requirement.

**[APPROVAL REQUIRED]** Committing to a platform-specific architecture that would materially constrain future desktop distribution.

## 3.3 Performance targets

Exact hardware targets are unresolved.

**[PREFERRED]** Develop toward a stable 60 FPS presentation on reasonable contemporary desktop hardware while ensuring simulation cost does not scale linearly with every off-screen NPC at full fidelity.

**[APPROVAL REQUIRED]** Final minimum/recommended hardware specification and hard CPU/GPU/memory budgets.

---

# 4. PERSISTENT CAMPAIGN STATE

The campaign state is the authoritative record of the expedition.

A conceptual model:

```text
CampaignState
- campaignId
- schemaVersion
- seed
- currentTime
- currentPhase / context
- captainCharacterId
- characters
- relationships
- inventories / item instances
- shipState(s)
- world / discovered locations
- weather / environment state
- sponsorCharter
- objectives
- finances
- knowledge / rumors / discoveries
- settlementState
- eventState
- chronicle
- settings required for simulation
```

This is conceptual, not a required class layout.

## 4.1 Campaign phases

At minimum support these conceptual states:

```text
Sponsor / Commission
Port Preparation
Voyage
Landfall
Settlement
Charter Fulfilled
```

Possible branches include:

```text
Voyage → Shipwreck → Survival → Escape/Repair → Voyage
Voyage → Shipwreck → Survival → Settlement
Voyage → Alternate Port → Voyage
Captain Death → Succession → Continue
```

**[REQUIRED]** Phase changes transfer state; they do not regenerate the party from templates.

---

# 5. CHARACTER MODEL

Characters are the most important persistent entities in the game.

## 5.1 Required character domains

Each character must be capable of representing:

```text
Identity
Capabilities / skills
Physical attributes
Traits / disposition
Needs
Health / injuries / illnesses
Relationships
Motivations / obligations
Inventory / equipment
Employment / contract
Knowledge / beliefs
History
Current assignment / task
Location
Availability
Survival status
```

Not every field needs full gameplay depth in P0.

## 5.2 Truth, knowledge, and belief

**[REQUIRED]** Architecturally distinguish:

1. **Actual truth** — the simulation's real value.
2. **Player knowledge** — what the captain/player has discovered.
3. **Character belief** — what an NPC believes, which may be incomplete or wrong.

Example:

```text
Actual navigation skill: 4
Player knowledge: "Experienced" with medium confidence
Candidate claims: "I served as navigator on three crossings"
Reference report: "Competent coastal pilot"
```

Do not expose hidden truth simply because it exists in memory.

## 5.3 Skills

Initial skill scale:

```text
0 — No meaningful training
1 — Basic familiarity
2 — Routine competent
3 — Experienced
4 — Highly skilled
5 — Exceptional specialist
```

Initial categories:

```text
Maritime
- Sailing
- Rigging
- Navigation
- Seamanship
- Ship Repair

Survival
- Fishing
- Foraging
- Hunting
- Tracking

Medical
- First Aid
- Treatment
- Sanitation
- Surgery

Practical
- Carpentry
- Cooking
- Animal Handling
- Crafting

Social
- Leadership
- Negotiation
- Persuasion
- Languages

Physical
- Strength
- Endurance
- Coordination
- Perception
```

**[PREFERRED]** Keep skill IDs independent of category/display hierarchy so categories can change without breaking saves.

## 5.4 Physical attributes versus skills

**[REQUIRED]** Training and physical capability are separate concepts.

A strong untrained person may assist with hauling but is not automatically a skilled rigger. A master carpenter weakened by illness retains knowledge but may work slowly or be unable to perform heavy labor.

## 5.5 Traits

Prototype target: roughly 2–3 meaningful traits per generated character.

Traits should alter behavior or decision tendencies, not exist solely as passive percentage bonuses.

Examples:

- cautious;
- resourceful;
- proud;
- loyal;
- impulsive.

**[PREFERRED]** Trait generation should be constrained by occupation/history/personality rules to avoid incoherent combinations.

## 5.6 Needs

P0 needs:

- hunger;
- thirst;
- fatigue;
- health;
- stress.

Later systems may add pain, comfort, belonging, morale dimensions, etc.

**[REQUIRED]** Needs should respond to activity, environment, condition, access to resources, and rest. Avoid identical flat decay curves for every character regardless of context.

## 5.7 Health

Health must support more than a single HP bar.

Conceptual data:

```text
overall condition
injuries
illnesses
symptoms
severity
mobility/work restrictions
treatment status
treatment history
recovery progression
```

P0 may implement a smaller subset, but the data model should not make future conditions impossible.

## 5.8 Death and survival state

**[REQUIRED]** Death is persistent. A dead character cannot:

- receive tasks;
- consume routine provisions;
- appear alive after a phase transition;
- re-enter recruitment pools;
- be silently regenerated after load.

History and relationship references to the dead remain valid.

---

# 6. RELATIONSHIP SYSTEM

Relationships are persistent directed state between characters.

**[REQUIRED]** Support asymmetric relationships.

Character A may respect B while B distrusts A.

Conceptual dimensions may include:

- trust;
- respect;
- affection/hostility;
- obligation/debt;
- fear;
- shared-history references.

**[PREFERRED]** Store major relationship-changing events or tags in addition to numeric values so UI and events can explain *why* a relationship exists.

Example:

```text
A trusts B +18
Reasons:
- B treated A during fever
- B shared water during shortage
- B later broke a promise
```

Exact scoring is tunable data, not hardcoded design truth.

---

# 7. TASK AND WORK SYSTEM

The task system is one of the project's foundational systems and should be shared across phases.

Examples:

- inspect hull;
- load cargo;
- repair rigging;
- pump bilge;
- cook meal;
- stand watch;
- treat injury;
- fish;
- rest;
- gather wood;
- construct shelter;
- farm;
- craft.

## 7.1 Task definition

A task should be capable of describing:

```text
stable task type ID
actor(s)
target
location
required/beneficial skills
required tools/resources
minimum conditions
estimated/base duration
progress
priority
interruptibility
failure/cancel conditions
resource consumption
outcome/effects
risk
```

## 7.2 Task outcome model

**[REQUIRED]** Outcomes may depend on:

```text
skill
physical condition
fatigue/needs
traits where relevant
tools/equipment
assistance
work environment
difficulty
time invested
randomness
```

Do not reduce every task to `skill + random number`.

## 7.3 Task queues and delegation

**[REQUIRED]** Characters can receive queued/assigned work.

**[PREFERRED]** Support priority and interruption so emergencies can supersede routine work.

Example:

```text
Cook evening meal
↓ interrupted by
Pump rapidly flooding bilge
↓ resume or cancel previous work after crisis
```

## 7.4 Direct control

The captain is directly controllable, and design allows temporary direct control of crew members for precise or urgent work.

**[REQUIRED]** Direct control and delegated simulation must use the same underlying character/task state where practical. Do not create a second incompatible ruleset for player-controlled characters.

---

# 8. TIME SYSTEM

Time is shared across the campaign.

Required conceptual modes:

- normal;
- paused planning;
- accelerated routine time.

**[PREFERRED]** Automatically slow or pause on critical events if user settings permit.

## 8.1 Simulation stepping

**[REQUIRED]** Core simulation should not assume rendering frame rate equals simulation rate.

Use an appropriate fixed/logical simulation update or event scheduling approach based on the engine.

**[PREFERRED]** Expensive systems should update at appropriate frequencies rather than every rendered frame.

Examples:

- movement: frequent;
- active repair task: regular;
- hunger: coarse interval;
- distant NPC schedule: coarse interval;
- long-term crop growth: much coarser interval.

## 8.2 Long voyage pacing

The voyage is intentionally long in narrative/game time, not necessarily in real-world waiting time.

**[REQUIRED]** Routine periods must be acceleratable without breaking simulation correctness.

---

# 9. INVENTORY, ITEMS, AND RESOURCES

Separate **item definitions** from **item instances** where instance state matters.

Definition examples:

```text
Fresh Water Barrel
Hammer
Sailcloth
Salted Fish
Medicine
Rope
```

Instance state may include:

```text
quantity
condition
durability
freshness
owner/container
location
weight
volume
custom modifiers
```

## 9.1 Cargo constraints

Ship provisioning must eventually support:

- weight;
- volume/capacity;
- safe stowage constraints where relevant;
- consumption;
- damage/loss/spoilage.

**[REQUIRED]** Cargo state survives voyage events and shipwreck transitions accurately.

## 9.2 Containers and ownership

**[PREFERRED]** Use a generalized container/location concept that can represent character inventory, ship hold, chest, workshop storage, settlement storehouse, and salvage pile without bespoke inventory code for each.

---

# 10. SHIP SIMULATION

P1 ship systems:

## Hull

State may include condition, damage regions, leak rate, and repair state.

## Rigging and sails

State may include condition, sail configuration, damage, and wind exposure.

## Navigation

Maintain distinction between actual world position and estimated/known position where gameplay requires uncertainty.

## Cargo

Track capacity and state of carried goods.

## Bilge / pumps

Water accumulation and pumping effectiveness create labor pressure.

## Crew operations

Watches, assignments, fatigue, and skill coverage affect ship performance.

**[DON'T]** Attempt a full fluid-dynamics or professional sailing simulator for the first playable.

**[REQUIRED]** Sailing must still make wind, heading, weather, ship condition, and crew competence meaningful.

---

# 11. NAVIGATION AND WORLD KNOWLEDGE

Navigation is not a modern GPS interface.

The simulation may distinguish:

- actual position;
- captain/crew estimated position;
- charted coastline/routes;
- reported but unverified locations;
- discovered locations;
- confidence/uncertainty.

**[REQUIRED]** Navigation skill and environmental conditions influence information quality, not merely ship speed.

Storms, cloud, visibility, missing observations, and poor navigation may increase uncertainty.

**[PREFERRED]** Knowledge entries retain provenance when useful: chart, rumor, observation, traveler report, sponsor information, etc.

---

# 12. WEATHER AND ENVIRONMENT

Weather is simulation input, not only visual dressing.

It can influence:

- wind;
- sailing speed/handling;
- sail stress;
- hull damage risk;
- visibility;
- navigation confidence;
- temperature/exposure;
- work efficiency;
- injury risk;
- character stress/fatigue;
- settlement production.

**[PREFERRED]** Weather systems should expose gameplay-relevant state to both simulation and rendering rather than maintaining unrelated visual and gameplay weather.

Storms should create interacting pressures, not a single damage roll.

---

# 13. EVENT AND CONSEQUENCE SYSTEM

Events should emerge from state whenever possible.

Examples:

- argument caused by rationing and distrust;
- illness spreading due to sanitation problems;
- candidate leaving because departure was delayed;
- rigging failure after ignored damage;
- crew member refusing an order due to relationship/motivation state.

## 13.1 Event structure

A reusable event system should be able to represent:

```text
stable event ID
eligibility conditions
trigger/cause
participants
context
choices, if interactive
AI/default resolution where appropriate
state changes
follow-up hooks
chronicle output
cooldown/repetition rules
```

**[DON'T]** Make the game primarily a deck of unrelated random popups.

Random events may exist, but system state should make them plausible and contextual.

---

# 14. CREW CONFLICT AND MUTINY

**[REQUIRED]** Avoid a single hidden "mutiny meter" that randomly fires at a threshold.

Conflict should derive from character and campaign state such as:

- hunger/thirst;
- exhaustion;
- broken promises;
- unequal treatment;
- fear;
- leadership failure;
- interpersonal hostility;
- destination disagreement;
- loss of confidence after disaster.

The system should allow concerns to become visible before extreme outcomes when plausible.

---

# 15. PORT NPC AND RECRUITMENT SYSTEM

## 15.1 Port simulation

P1 target: compact port with roughly 20–30 active NPCs and schedules sufficient to make the place feel inhabited.

**[PREFERRED]** NPCs should have lightweight schedules/goals rather than standing permanently at recruitment markers.

Potential districts:

- Quayside;
- Market;
- Tavern/Inns;
- Workshops;
- Customs House/Sponsor Office.

## 15.2 Recruitment pipeline

```text
Encounter
→ Investigate
→ Negotiate
→ Recruit / Refuse / Counteroffer
```

Candidate decisions can consider:

- wages;
- provisions;
- duties;
- destination/risk;
- ship condition;
- captain reputation;
- personal obligations;
- relationships with existing crew;
- contract terms.

**[REQUIRED]** Persuasion cannot override every refusal reason.

## 15.3 Information discovery

Player may learn about candidates through:

- observing work;
- conversation;
- references;
- demonstrations/tests;
- documents;
- performance under real pressure.

Information should carry confidence/provenance where appropriate.

---

# 16. SHIPWRECK AND FAILURE CONTINUATION

Major failure should often create a new playable state rather than an immediate game-over screen.

## 16.1 Shipwreck transition

On shipwreck, determine and persist:

- survivors;
- deaths/missing characters;
- injuries;
- item/cargo loss;
- damaged items;
- salvageable items;
- location;
- weather/time;
- relationship consequences;
- known information;
- active obligations/objectives.

Then transition into survival state.

**[REQUIRED]** Never reconstruct the survivor group from a generic roster.

## 16.2 Survival loop

Initial sequence:

1. assess survivors;
2. secure water;
3. secure food;
4. secure shelter;
5. establish camp;
6. salvage/explore;
7. choose a continuation strategy.

Possible strategies include repair/rebuild, construct smaller transport, signal rescue, seek another settlement/port, or establish a settlement where viable.

Not every disaster must be survivable.

---

# 17. SETTLEMENT SYSTEM

The settlement layer reuses the same people, items, skills, tasks, health, relationships, and history.

Initial progression:

```text
Landfall
→ Survival
→ Establishment
→ Stability
```

P1 structures:

- shelter;
- storehouse;
- water source/station where geographically viable;
- garden/field;
- workshop;
- infirmary;
- dock/landing.

**[REQUIRED]** Construction consumes physical resources and labor, not an abstract universal building currency.

Character skill, tools, condition, fatigue, and environment influence work.

## 17.1 Placement and viability

Building placement may depend on terrain/resource conditions.

Example: a water structure cannot create fresh water where no viable source exists merely because the player paid a cost.

## 17.2 Scale control

**[DON'T]** Build a full city-builder economy before the core expedition loop works.

P1 should prove that surviving people can establish and maintain a small settlement.

---

# 18. CAPTAIN, SUCCESSION, AND PLAYER CONTROL

The player is a persistent captain character rather than an omniscient disembodied manager.

The captain has normal character state: skills, needs, injuries, relationships, inventory, etc.

## 18.1 Captain death

Captain death does not automatically end the campaign.

Possible succession candidates include appointed officers or accepted crew members.

**[REQUIRED]** Succession transfers player authority, not the deceased captain's personal skills, inventory, relationships, or identity.

A leadership crisis may occur when no credible successor exists.

Exact succession rules are **[APPROVAL REQUIRED]** before final implementation.

---

# 19. AI AND AUTONOMY

NPC AI should serve simulation readability and character believability rather than attempt unconstrained human-level planning.

## 19.1 AI layers

A practical architecture may separate:

1. immediate movement/interaction;
2. assigned-task execution;
3. personal needs/self-preservation;
4. schedule/routine;
5. social/event decision logic.

**[PREFERRED]** Use explicit utility/rules/state machines appropriate to the problem rather than a single enormous behavior tree.

## 19.2 Player orders versus autonomy

Characters should generally obey valid assignments but may fail, hesitate, refuse, or interrupt work for understandable reasons such as incapacity, emergency, extreme need, or severe conflict.

These exceptions must be visible/explainable to the player.

---

# 20. MOVEMENT, PATHFINDING, AND INTERACTION

P0 requires:

- isometric navigation;
- collision;
- selection;
- interaction ranges;
- reachable targets;
- character movement to assigned tasks.

**[PREFERRED]** Navigation architecture should tolerate dynamic blockers and changing settlement layouts.

**[DON'T]** Tie simulation identity to navigation agents or rendered character objects.

---

# 21. SIMULATION LEVELS OF DETAIL

Hundreds of future NPCs cannot all run maximum-fidelity simulation every frame.

Use simulation tiers appropriate to distance/relevance.

Conceptual example:

### Tier A — Active/visible
Full movement, active tasks, frequent state updates.

### Tier B — Nearby/off-screen
Task and needs simulation at reduced frequency; simplified movement.

### Tier C — Distant/world
Schedule/event abstraction; no detailed pathfinding or animation.

**[REQUIRED]** Tier transitions must preserve meaningful state and must not create resources, heal injuries, teleport impossibly, or resurrect characters.

---

# 22. SAVE / LOAD / VERSIONING

Save/load is a foundational feature, not post-launch polish.

## 22.1 Required properties

**[REQUIRED]** Saves must preserve at minimum:

- character IDs/state;
- relationships;
- inventories/items;
- time;
- current context/phase;
- ship state;
- active tasks where feasible;
- injuries/illnesses;
- objectives/charter;
- discovered knowledge;
- settlement state;
- relevant event state;
- random seed/state needed for reproducibility where practical;
- chronicle/history.

## 22.2 Schema version

Every save contains a schema/version identifier.

**[PREFERRED]** Build migrations for compatible development changes rather than silently corrupting or discarding old saves.

During very early prototypes, breaking saves may occasionally be acceptable, but the agent must clearly report it before/when making the change.

## 22.3 Atomicity

**[PREFERRED]** Save writes should avoid leaving the only save corrupted if interrupted. Use temporary/atomic replacement or engine-appropriate equivalent.

## 22.4 Save tests

At minimum test:

```text
create campaign
advance state
save
reload
compare important state
continue simulation
```

Also test saving during or around crises as architecture matures.

---

# 23. CHRONICLE, LOGGING, AND EXPLAINABILITY

The campaign chronicle is both a player feature and debugging aid.

Record significant events such as:

- recruitment;
- contract/promises;
- injuries;
- illness;
- deaths;
- relationship-changing incidents;
- ship damage;
- route changes;
- discoveries;
- shipwreck;
- settlement founding;
- charter milestones.

Entries should reference stable entity IDs internally while resolving player-facing names/text for presentation.

## 23.1 Developer diagnostics

**[REQUIRED]** Provide enough diagnostic logging to answer questions such as:

- Why did this character stop working?
- Why did this task fail?
- Why is this character exhausted?
- What caused this leak?
- Why did this candidate refuse?
- What changed this relationship?

Do not spam release logs with every simulation tick.

---

# 24. RANDOMNESS

Use centralized or clearly managed random sources.

**[REQUIRED]** Tests must be able to set seeds.

**[PREFERRED]** Consider separate deterministic streams/domains for world generation, characters, weather, and task outcomes if the chosen implementation benefits from it. This prevents unrelated UI or content changes from unpredictably altering every future random result.

**[DON'T]** Call arbitrary global randomness throughout core simulation code with no way to reproduce outcomes.

---

# 25. DATA VALIDATION

Content definitions must be validated at load/build/test time where possible.

Examples:

- duplicate IDs;
- missing item references;
- invalid task requirements;
- impossible skill ranges;
- missing localization keys;
- building recipes referencing nonexistent resources;
- event conditions referencing unknown traits.

**[REQUIRED]** Invalid content should fail loudly in development rather than silently produce corrupted campaigns.

---

# 26. TESTING STRATEGY

Testing is mandatory because the game's complexity comes from interacting systems.

## 26.1 Unit tests

Examples:

- provision consumption;
- fatigue/recovery;
- skill calculations;
- cargo capacity;
- relationship updates;
- task eligibility;
- injury restrictions.

## 26.2 Simulation tests

Headless/fast simulations should test scenarios such as:

- 90-day voyage provision consumption;
- crew starvation/dehydration behavior;
- watch rotations and fatigue;
- hull leak + pump + repair interactions;
- storm damage;
- candidate generation validity;
- settlement food production;
- succession after captain death.

## 26.3 Persistence tests

- save/load equivalence;
- stable IDs;
- dead characters remain dead;
- active injuries persist;
- shipwreck transition persists survivors/items;
- context transitions do not duplicate entities.

## 26.4 Property/invariant tests

Where supported, test invariants:

```text
No two persistent entities share an ID.
Inventory quantities cannot become negative.
Dead characters cannot be assigned active work.
An item cannot exist in two exclusive containers simultaneously.
A character cannot be both dead and actively moving.
Cargo loss cannot produce more cargo than existed before.
```

## 26.5 Integration/play tests

Automated tests do not replace actual playtesting. Each milestone should include a short reproducible manual scenario and expected results.

---

# 27. CODING STANDARDS FOR AGENTS

## 27.1 Readability

**[REQUIRED]** Prefer clear names and straightforward control flow over clever abstractions.

Code should be understandable by a future developer or agent without reconstructing hidden intent.

## 27.2 Scope discipline

When implementing a ticket:

1. identify the minimum systems touched;
2. preserve existing working behavior;
3. avoid unrelated refactors;
4. add/update tests;
5. run relevant tests/build;
6. summarize changed files and behavior;
7. identify follow-up work separately.

**[DON'T]** Perform opportunistic project-wide rewrites while implementing a small feature.

## 27.3 Comments

Comments should explain **why**, invariants, historical/technical constraints, or non-obvious decisions. Do not comment obvious syntax.

## 27.4 Dependencies

**[REQUIRED]** Before adding a dependency, check whether the existing stack already solves the problem.

**[APPROVAL REQUIRED]** Add a major framework/service/dependency that materially affects architecture, licensing, build size, cost, or deployment.

## 27.5 Secrets

**[REQUIRED]** Never commit API keys, credentials, private tokens, or secrets.

---

# 28. AGENT CHANGE PROTOCOL

Before coding a meaningful feature, the agent should state:

```text
Goal
Relevant existing systems/files
Assumptions
Implementation approach
Tests/validation
Any approval-required decision
```

After coding, report:

```text
What changed
Files/modules changed
Tests run and results
Manual validation performed
Known limitations
New technical debt, if any
Recommended next ticket
```

If tests fail because of the change, fix them before declaring completion unless explicitly blocked.

If pre-existing tests fail, distinguish those failures clearly from new regressions.

---

# 29. SOURCE OF TRUTH AND CONFLICT RULES

Use these priorities:

1. Explicit current owner instruction.
2. Approved project documents.
3. Existing intentional implementation where documents are silent.
4. Agent recommendation.

If documents conflict:

- do not silently choose;
- identify the conflict;
- prefer the more specific document for its domain only when the intent is clear;
- request approval for consequential ambiguity.

Examples:

- gameplay behavior → Handoff generally leads;
- visual implementation → Art Bible generally leads;
- architecture/code rules → Technical Spec generally leads.

Gameplay readability overrides decorative art choices.

---

# 30. HISTORICAL DATA IN TECHNICAL SYSTEMS

Historical facts should not be buried as arbitrary hardcoded assumptions.

**[RESEARCH]** Verify period-specific ships, tools, clothing, foods, crops, diseases, geography, peoples, languages, political entities, trade goods, and technologies before final content implementation.

Where uncertainty exists, record source/confidence in content-production notes when practical.

**[REQUIRED]** The Americas are not modeled as empty land awaiting European ownership. Indigenous communities are autonomous actors with their own settlements, economies, diplomacy, territorial interests, relationships, and goals.

**[APPROVAL REQUIRED]** Final destination region and specific Indigenous societies represented. These decisions materially affect maps, ecology, architecture, languages, crops, diplomacy, and content systems.

---

# 31. UI / SIMULATION BOUNDARY

UI observes and commands the simulation; it does not become the simulation.

Examples:

- clicking "Assign Repair" creates/requests a repair task;
- the progress bar displays task progress but does not own it;
- roster cards display character state but do not store authoritative health;
- closing a panel cannot erase an active task.

**[REQUIRED]** UI must not contain hidden duplicate authoritative state for characters, inventory, tasks, or ship systems.

---

# 32. EVENTUAL MODDABILITY / CONTENT AUTHORING

Full mod support is not a P0/P1 requirement.

However:

**[PREFERRED]** Stable IDs, data-driven definitions, validation, and separation of content from logic should make internal content authoring easy and avoid blocking future mod support.

**[DON'T]** Build a full mod SDK before the core game works.

---

# 33. LOCALIZATION READINESS

Localization is not necessarily required for the prototype, but architecture should avoid making it unnecessarily difficult.

**[PREFERRED]** Player-facing content uses localization keys or a centralized text/content layer rather than being scattered through simulation code.

Names and historically relevant multilingual text require separate consideration from UI localization.

---

# 34. SECURITY AND EXTERNAL SERVICES

The core single-player simulation should not require a live service to function unless a future feature explicitly needs one.

**[PREFERRED]** Keep saves local/offline-capable.

**[APPROVAL REQUIRED]** Introducing accounts, telemetry services, cloud saves, paid APIs, generative-AI runtime services, or always-online dependencies.

---

# 35. P0 — FOUNDATION IMPLEMENTATION ORDER

Unless repository conditions strongly justify another order, use this sequence:

## P0.0 Repository audit

Deliver architecture assessment and implementation plan before major changes.

## P0.1 Persistent IDs and base campaign state

Create minimal persistent campaign and character identity model.

**Exit criteria:** create characters with stable unique IDs; campaign state can reference them reliably.

## P0.2 Character state

Implement minimum skills, needs, health/status, traits, and inventory references.

**Exit criteria:** character state can change independently of rendering.

## P0.3 World clock and simulation stepping

Implement pause/normal/accelerated time architecture.

**Exit criteria:** simulation remains stable across time speeds.

## P0.4 Task system

Implement assignable timed tasks with requirements, progress, completion, cancellation/interruption.

**Exit criteria:** characters can execute tasks based on state and skill.

## P0.5 Inventory/resource containers

Implement item definitions, quantities/instances as needed, containers, transfer, consumption.

**Exit criteria:** provisions can be stored, moved, and consumed without invalid quantities.

## P0.6 Needs and rest

Implement hunger, thirst, fatigue, health/stress baseline interactions.

**Exit criteria:** time/activity changes needs; food/water/rest affect them.

## P0.7 Save/load

Persist the above state.

**Exit criteria:** save → quit/reinitialize → load → continue with equivalent state.

## P0.8 Chronicle and diagnostics

Record meaningful changes and provide debugging visibility.

## P0.9 Isometric interaction layer

Movement, selection, collision, interaction ranges, task targets.

**P0 completion gate:**

Characters can move, perform tasks, consume resources, become tired/hungry/thirsty, sustain persistent state, save/load correctly, and be simulated independently of the renderer.

---

# 36. P1 — VERTICAL SLICE IMPLEMENTATION ORDER

## P1.1 Port scene

Compact port, districts, 20–30 lightweight NPCs, schedules, interactable candidates.

## P1.2 Recruitment and knowledge

Candidate truth versus known information, investigation, negotiation, recruitment/refusal.

## P1.3 Provisioning and charter

Funds, berth limit, cargo constraints, departure deadline, sponsor objective.

## P1.4 Ship context

Deck/interior spaces sufficient for crew tasks and direct movement.

## P1.5 Sailing and navigation

Route, wind/heading, travel, estimated position, time acceleration.

## P1.6 Ship systems

Hull/leaks, rigging, bilge, repair, cargo.

## P1.7 Crew survival

Food/water/fatigue, injury/medical crisis, watches/tasks.

## P1.8 Storm scenario

One systemic storm capable of damaging ship and injuring/exhausting crew.

## P1.9 Shipwreck transition

Persistent survivors, injuries, relationships, and salvage transferred to survival context.

## P1.10 Landfall / camp

Water, food, shelter, basic gathering/construction.

**P1 completion gate:**

A player can accept the scenario, recruit a crew, provision a ship, depart, survive a meaningful voyage crisis, and either reach land or shipwreck and continue playing with the actual surviving people and resources.

---

# 37. FIRST PLAYABLE — “THE LAST BERTH”

Use this as the primary vertical-slice validation scenario.

## Setup

- sponsor charter;
- departure at dawn / meaningful deadline;
- limited funds;
- limited berths;
- approximately 25 port NPCs;
- approximately 6 plausible recruits;
- candidates differ in skills, motives, reputation, demands, and hidden qualities.

## Required player actions

- walk through/interact with port;
- investigate candidates;
- negotiate/recruit;
- buy/stow provisions;
- inspect ship;
- depart;
- assign crew tasks;
- respond to storm/crisis;
- reach land or wreck;
- establish immediate survival state.

## Technical definition of done

**[REQUIRED]**

- persistent character IDs;
- candidate hidden truth and discovered information;
- recruitment tradeoffs;
- cargo/resource constraints;
- time progression;
- task delegation;
- ship movement/travel;
- resource consumption;
- ship damage;
- fatigue/health consequences;
- systemic crisis;
- landfall or shipwreck transition;
- save/load at meaningful points;
- chronicle entries;
- understandable causal feedback.

---

# 38. REQUIRED AUTOMATED TEST SCENARIOS

By the vertical slice, maintain automated coverage for at least these behaviors where technically practical:

1. Generate hundreds of characters; IDs are unique and values valid.
2. Advance 90 voyage days; provision consumption remains valid.
3. A character without water deteriorates according to defined rules.
4. Rest reduces fatigue under valid conditions.
5. Dead character cannot accept or execute tasks.
6. Damaged hull accumulates water according to leak state.
7. Pumping reduces bilge water when pump/crew conditions permit.
8. Repair consumes required resources and improves valid component state.
9. Save/load preserves a storm/crisis state.
10. Shipwreck transition preserves the correct survivors.
11. Lost cargo does not reappear after transition/load.
12. Relationships persist across context changes.
13. Captain death does not copy the captain's skills onto successor.
14. Item/container invariants remain valid after transfers.
15. Time acceleration does not materially change deterministic results compared with equivalent normal-time simulation, except where explicitly designed.

---

# 39. PERFORMANCE STRATEGY

Optimize based on profiling, not speculation, but design away obvious scaling failures.

**[REQUIRED]** Avoid:

- every NPC running pathfinding every frame;
- every need updating every render frame;
- repeated full-world searches for common lookups;
- excessive allocation in hot simulation loops;
- UI rebuilding entire large lists unnecessarily;
- saving giant redundant rendered/derived state.

**[PREFERRED]** Use:

- stable ID lookup maps/registries;
- scheduled/coarse updates;
- simulation LOD;
- event-driven notifications where appropriate;
- pooling only when profiling demonstrates value;
- cached derived data with clear invalidation where worthwhile.

Profile before complex optimization.

---

# 40. ART / TECH INTEGRATION RULES

The Art Bible governs visual direction.

Technical implementation must support:

- recognizable characters at isometric gameplay scale;
- portraits distinct from world-model detail if used;
- weather-driven lighting/visual state;
- material wetness/damage where feasible;
- readable interactables;
- visual ship deterioration;
- character condition cues;
- scalable asset LOD/detail.

**[DON'T]** Solve readability solely by permanent glowing outlines around everything.

**[APPROVAL REQUIRED]** Final rendering pipeline, material complexity, texture-density budgets, character polygon budgets, and target LOD specifications after engine/platform audit.

---

# 41. APPROVAL-REQUIRED DECISIONS

The agent must not silently finalize these:

- engine migration or replacement;
- core renderer/pipeline change;
- target hardware specification;
- multiplayer/networking introduction;
- live-service dependency;
- paid runtime API/service;
- major new framework/dependency;
- final destination region in the Americas;
- specific Indigenous societies represented;
- hero ship type if historically/content consequential;
- major change to persistent-captain premise;
- removing failure-continuation design;
- replacing persistent individual characters with abstract workforce units;
- final captain succession rules;
- major change to illustrated historical realism.

---

# 42. PROHIBITED SHORTCUTS

**[DON'T]**

- Treat characters as disposable phase-specific units.
- Reset injuries/relationships between port, voyage, and settlement.
- Use display names as database IDs.
- Put authoritative game state in UI controls.
- Make save/load an afterthought.
- Use unseeded opaque randomness throughout simulation.
- Build hundreds of NPCs before simulation LOD exists.
- Implement full city-building before the expedition vertical slice works.
- Build professional-grade sailing physics before proving meaningful simplified sailing.
- Resolve historical uncertainty with generic fantasy/medieval assumptions.
- Represent the Americas as empty territory.
- Turn Indigenous communities into a single generic faction.
- Add major dependencies simply because they are familiar.
- Rewrite working systems without measured benefit.
- Declare a feature complete without validation.

---

# 43. DEFINITION OF A GOOD SYSTEM

A system is not complete merely because it appears on screen.

For core systems, completion means:

```text
Correct state model
+ clear interfaces
+ persistence
+ understandable player feedback
+ automated tests where practical
+ manual validation
+ acceptable performance
+ no violation of project invariants
```

A beautiful repair animation with no persistent ship damage model is not a completed repair system.

A recruitment UI with randomly generated numbers visible to the player is not the intended recruitment system.

A settlement that spawns anonymous workers is not the intended settlement system.

---

# 44. AGENT KICKOFF PROMPT

The following can be given directly to a coding agent with the three project documents:

> You are working on **The Expedition**, a single-player isometric historical survival, expedition, and settlement game set around 1500–1520.
>
> Read these documents in order before making architectural changes:
>
> 1. `The_Expedition_Agent_Handoff.md`
> 2. `The_Expedition_Art_Bible.md`
> 3. `The_Expedition_Technical_Spec.md`
>
> Treat them as authoritative project specifications. Explicit current owner instructions override them.
>
> Your first task is **not to implement the entire design**. Audit the repository and report the existing engine/framework, architecture, implemented systems, build/test process, technical risks, specification conflicts, and the next 3–8 smallest high-value implementation tickets.
>
> Do not migrate engines, introduce major dependencies, resolve `[APPROVAL REQUIRED]` questions, or perform broad rewrites without approval.
>
> Preserve the project's central invariant: **the same individual people persist across port, voyage, disaster, landfall, and settlement, carrying their skills, health, injuries, relationships, inventory, knowledge, and history with them.**
>
> Keep simulation state separate from rendering/UI, favor data-driven and testable systems, use stable persistent IDs, make randomness reproducible, and build save/load early.
>
> For every implementation ticket, state the goal, affected systems, assumptions, implementation approach, and validation plan before coding. After coding, report changed files, tests run, results, limitations, and recommended next work.
>
> If a specification is ambiguous or conflicts with the repository, flag it rather than silently inventing a permanent solution.

---

# 45. CURRENT OPEN TECHNICAL QUESTIONS

These should be answered after repository audit, not guessed in advance:

1. What engine/framework currently exists, if any?
2. What is the target desktop OS/distribution plan?
3. What rendering pipeline best supports the Art Bible within performance goals?
4. What data format is best for definitions in the chosen engine?
5. How should save serialization/version migration be implemented in that stack?
6. What navigation solution best supports ship, port, and settlement contexts?
7. How large should active settlements become before simulation LOD is mandatory?
8. How many simultaneously visible characters are expected?
9. What exact day/tick granularity produces the right voyage pacing?
10. How much direct-control action gameplay is desired versus pause/delegation?
11. What region is the departure port based on?
12. What region of the Americas is the primary destination?
13. Which ship type(s) are appropriate for the approved route/date/sponsor?
14. Which historical communities, languages, crops, goods, and ecologies must be modeled for that region?

---

# 46. FINAL IMPLEMENTATION PRINCIPLE

When forced to choose between a technically impressive feature and preserving the continuity of the expedition, preserve continuity.

The defining technical achievement of **The Expedition** is not rendering a large ocean or simulating hundreds of villagers. It is allowing the player to meet a person in a crowded port, learn who they are, hire them, depend on them at sea, watch them change through hardship, perhaps survive a wreck with them, and later see that exact person help build—or fail to reach—the settlement at the end of the journey.

Every major system should reinforce that continuity.
