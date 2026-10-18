# THE EXPEDITION
## Agent Handoff: Game Vision, Technical Direction, and First Playable Milestone

**Document purpose:** This is the project’s working source of truth for an AI coding agent or development team. Use it to plan, implement, test, and document the first playable version of *The Expedition*. It is intentionally implementation-oriented, but it is not permission to build the entire game at once.

**Project status:** Pre-production / prototype planning. The engine and repository have not been confirmed in this document. Inspect the user's existing repository and tooling before selecting or introducing technology.

---

# 1. Instructions to the Implementing Agent

You are the technical lead and implementation agent for *The Expedition*, a single-player isometric historical survival, expedition, and settlement game set around 1500–1520. Your job is to turn the project vision into a maintainable, testable playable prototype through small, verified increments.

## 1.1 Start here

Before changing code:
1. Inspect the repository, current project files, README, existing conventions, engine/runtime version, build process, and tests.
2. Summarize what already exists and identify the smallest useful first milestone.
3. Propose an implementation plan with small tickets, dependencies, acceptance criteria, and likely risks.
4. Preserve working features. Do not replace the project structure or introduce a new engine/framework without explaining why and getting approval when the change is consequential.
5. If no repository or codebase is supplied, produce a proposed architecture and a minimal project scaffold plan before implementing a large system.

## 1.2 Working rules

- **Build a vertical slice, not a broad collection of unfinished systems.** The first goal is a complete short journey from recruitment through voyage crisis to landfall or shipwreck survival.
- **Simulation before presentation.** Keep gameplay rules independent of rendering and UI wherever the chosen engine permits.
- **Persistent people are the heart of the game.** Characters must be the same entities across port, voyage, disaster, and settlement phases. Never replace them with phase-specific copies.
- **Use data-driven definitions** for skills, traits, items, tasks, ships, events, and buildings where practical.
- **Make systems explainable.** Important outcomes should have observable causes and be recorded in the event history.
- **Prefer readable, conventional code** over clever abstractions. Use clear names, focused modules, explicit state transitions, and documented public interfaces.
- **Test rules, not just screens.** Add automated tests for resource use, task eligibility, persistence, state transitions, and failure cases.
- **Avoid speculative scope.** Do not implement later-phase features just because they are described in the vision document.
- **Do not invent historical certainty.** Treat historical claims as research questions when accuracy matters. Depict Indigenous societies as distinct communities with their own agency, interests, and goals—not as generic hostile NPCs or empty land.
- **Never silently discard campaign state.** Save/load and phase transitions must preserve identity, health, inventory, relationships, knowledge, and history.

## 1.3 Required response after each work unit

At the end of each ticket or implementation session, report:
1. What changed and why.
2. Files/modules changed.
3. Commands/tests run and their actual results.
4. Known defects, assumptions, and unimplemented items.
5. How to run or inspect the result.
6. The next recommended small ticket.

Do not claim tests passed unless they were actually run. If a command cannot be run in the environment, say so and provide the exact command for the user.

---

# 2. High-Level Vision

*The Expedition* is a single-player isometric historical survival, expedition, and colony-building game. It begins in a busy European port at the cusp of the Age of Discovery, around 1500–1520. The player is a persistent captain who accepts a sponsor's charter, recruits a limited crew, provisions and inspects a ship, plans a route, and sets sail toward the Americas. The expedition can succeed, suffer disaster, become stranded, or transform into a survival story. If the crew reaches land, the game continues into settlement establishment and management.

The desired opening atmosphere is a crowded, cinematic port: people working, trading, arguing, traveling, and pursuing their own business. The player should feel like one person making consequential decisions in a living world—not an omniscient manager selecting units from a menu.

## 2.1 Core identity

**The same people persist across the entire campaign, and their identities, relationships, skills, injuries, knowledge, choices, and losses shape every phase.**

The port, voyage, shipwreck, and settlement are not separate games. They are different situations acting on the same persistent campaign simulation.

## 2.2 Design pillars

1. **People create the story.** Emergent narrative comes from individual motives, capabilities, relationships, and experiences.
2. **Preparation reduces risk but never eliminates it.** Recruitment, supplies, maintenance, route planning, and promises matter.
3. **Disasters create new problems, not automatic endings.** A shipwreck can lead to salvage, camp life, rescue attempts, a new vessel, or a different settlement.
4. **The world remembers.** Promises, injuries, deaths, discoveries, broken agreements, and leadership decisions remain part of the campaign history.

## 2.3 Tone and presentation

- **Art direction:** Stylized realism. Readable silhouettes, expressive faces, grounded materials, and clear gameplay readability.
- **Perspective:** Direct player control in the world; isometric camera.
- **Gameplay:** Hybrid. The player can directly control the captain or selected crew member and can also pause, issue orders, and delegate routine tasks.
- **Pacing:** Long voyage, with routine time accelerated and critical situations slowed down for decisions.
- **Emotional focus:** People—their loyalties, ambitions, fears, competence, conflicts, and losses.
- **Player role:** A persistent captain, not a disembodied colony-management cursor.
- **Motivation:** A sponsor, royal authority, or private patron offers a charter with obligations, limits, and a deadline.

---

# 3. Campaign Structure

## 3.1 Main loop

1. **Accept commission:** Learn the sponsor's terms, restrictions, deadline, and success conditions.
2. **Recruit and prepare:** Meet candidates, investigate their abilities and motives, negotiate terms, buy supplies, inspect the ship, and plan the route.
3. **Undertake the voyage:** Navigate, maintain the ship, manage crew needs, explore, respond to weather and crises, and make consequential decisions.
4. **Reach land or suffer disaster:** Landfall, alternate port, rescue, or shipwreck/survival branch depending on events.
5. **Establish a foothold:** Secure water, food, shelter, salvage, and labor; then build toward a viable settlement.
6. **Fulfill or redefine the mission:** Meet the charter, negotiate a changed objective, continue surviving, or fail for understandable reasons.

## 3.2 Campaign state transitions

The campaign should use explicit, testable state transitions rather than scattered booleans.

Suggested high-level states:
- `SPONSOR_COMMISSION`
- `PORT_PREPARATION`
- `VOYAGE`
- `SHIPWRECK_SURVIVAL`
- `ALTERNATE_PORT`
- `LANDFALL`
- `SETTLEMENT_SURVIVAL`
- `CHARTER_FULFILLED`
- `CAMPAIGN_FAILED`

Not every transition is always available. For example, rescue requires a plausible rescuer, ship, or settlement; a remote wreck with no survivors can be unrecoverable. Branches should preserve all relevant campaign state.

## 3.3 Failure and succession

Failure should be causal and legible. The game should usually continue after a shipwreck if there is a plausible path forward. Preserve surviving characters, their conditions, relationships, remaining inventory, salvage, and knowledge.

The captain can die. If so, leadership may pass to a credible successor—such as an appointed officer or a crew member accepted by enough survivors. If there is no clear successor, a leadership crisis may emerge. The original captain's personal skills, carried items, and relationships are not magically transferred to the successor.

## 3.4 Charter success

Success should be based on a defined charter rather than maximizing buildings or eliminating all threats. Example target: maintain a viable settlement through two winters, sustain a minimum population, secure dependable food and water, provide shelter and basic health support, and meet a sponsor obligation. Exact goals should be data-driven and scenario-specific.

---

# 4. Persistent Character Simulation

This is the most important shared system in the game.

## 4.1 Character record

Each character needs a unique persistent ID and a record covering:
- **Identity:** name, age, appearance, origin, languages.
- **Capabilities:** skills, experience, physical attributes, learned knowledge.
- **Disposition:** temperament, habits, risk tolerance, preferences, and 2–3 meaningful traits in the prototype.
- **Needs:** hunger, thirst, fatigue, health, and stress in the first prototype. Later needs may include pain, comfort, belonging, and morale-related factors.
- **Health:** injuries, illnesses, conditions, treatment history, and survival status.
- **Relationships:** trust, respect, affection/hostility, obligations, and shared experiences. Relationships may be asymmetric.
- **Motivations:** personal goals, duties, fears, ambitions, and non-negotiable concerns.
- **Inventory/equipment:** carried items, equipped tools, and personal possessions.
- **Employment/contract:** role, wages, promised provisions, duties, shares, and obligations.
- **Knowledge:** facts, rumors, discoveries, references, and how each item became known.
- **History:** recruitment, promises, injuries, achievements, major decisions, and important events.
- **Current state:** location, task, movement, availability, and survival status.

Do not implement every field as a fully simulated system immediately. Design the data model to grow, then implement only fields required by the current milestone.

## 4.2 Separate truth, belief, and player knowledge

The game must distinguish:
1. **Actual truth:** what the character can really do, wants, fears, or knows.
2. **Character belief:** what the character thinks is true; this can be mistaken.
3. **Captain/player knowledge:** what has been observed, reported, tested, or inferred.

This distinction enables fair uncertainty. For example, a candidate may claim to be a skilled carpenter, a former employer may give a biased reference, and a practical task may reveal the candidate's true competence. Knowledge should track provenance where practical: firsthand observation, rumor, reference, document, or test.

## 4.3 Skill scale

Use a consistent five-level scale:
- `0`: no meaningful training
- `1`: basic familiarity
- `2`: routine competence
- `3`: experienced
- `4`: highly skilled
- `5`: exceptional specialist

Skill is distinct from physical attributes. Task outcomes can depend on skill, condition, tools, environmental difficulty, and time.

### Initial skill groups
- **Maritime:** sailing, rigging, navigation, seamanship, ship repair.
- **Survival:** fishing, foraging, hunting, tracking.
- **Medical:** first aid, treatment, sanitation, surgery.
- **Practical:** carpentry, cooking, animal handling, crafting.
- **Social:** leadership, negotiation, persuasion, languages.
- **Physical:** strength, endurance, coordination, perception.

## 4.4 Traits should change behavior

Traits must not be only numerical modifiers. Examples:
- **Cautious:** avoids unnecessary risk and may refuse dangerous orders.
- **Resourceful:** seeks alternatives when supplies or tools are missing.
- **Proud:** reacts poorly to public humiliation.
- **Loyal:** more willing to help a trusted leader or friend.
- **Impulsive:** may act before waiting for an order.

Trait generation should be constrained by background, occupation, and history so that character combinations remain plausible. For the first prototype, use a small hand-authored trait set.

## 4.5 Needs and tasks

Needs should respond to activity, environment, and condition—not simply decay at identical rates at all times. In the prototype, hunger, thirst, fatigue, health, and stress should influence task availability and performance.

Characters have tasks with requirements, duration, interruption conditions, and consequences. Skilled characters generally work faster or more reliably. Unskilled characters may still help but can be slower or risk mistakes. The player can intervene directly for precision or emergencies.

## 4.6 Relationships and morale

Avoid a single morale threshold that causes random mutiny. Social crises should arise from understandable conditions such as hunger, exhaustion, broken promises, unequal treatment, interpersonal conflict, loss of a leader, or fear that the captain is hiding information. Characters should voice concerns where feasible. The player can respond through supplies, negotiation, changed duties, leadership, or concessions. Not every disagreement must be resolvable.

---

# 5. Port Preparation and Recruitment

The port should be compact, active, and explorable—not a giant static recruitment menu.

## 5.1 Suggested districts
- **Quayside:** sailors, dockworkers, shipwrights, cargo handlers, merchants.
- **Market:** food, tools, clothing, livestock, and supplies.
- **Tavern and inns:** travelers, rumors, introductions, private conversations.
- **Workshops:** carpenters, smiths, rope makers, coopers, tailors.
- **Customs house / sponsor's office:** charter, cargo restrictions, fees, official records, departure deadline.

NPCs have schedules, jobs, and goals; some may leave if the player waits too long.

## 5.2 Recruitment sequence
1. Encounter a candidate.
2. Investigate by observing, conversing, checking references, requesting demonstrations, or assigning a test task.
3. Negotiate wages, provisions, duties, shares, and obligations.
4. Recruit if both sides accept the terms.

Candidates evaluate the captain too. They may refuse because of low wages, distrust, the destination, an unsafe ship, family obligations, conflicts with existing crew, or other motives. Persuasion cannot override every concern.

## 5.3 Preparation trade-offs
The player has limited money, berths, provisions, and cargo capacity. Do not use rigid class seats; candidates should have overlapping capabilities and backups matter.

The UI should communicate:
- Known skills and confidence in those assessments.
- Wages and contract terms.
- Known health concerns.
- Relationships or conflicts with the current roster.
- Provisions consumed by the roster.
- Critical skill gaps.
- Cargo volume/weight and stowage limits.

Show meaningful trade-offs; do not automatically solve the roster for the player.

## 5.4 Departure deadline
The deadline introduces urgency. Candidates may take other work, stock may sell out, fees may increase, weather windows may close, and the sponsor may become impatient. These outcomes should be telegraphed enough that the player can make informed decisions.

## 5.5 Initial supplies
Food, fresh water, medicine, tools, spare materials, trade goods, and personal equipment. Cargo must respect volume/weight and safe stowage limits. Skills can partially substitute for supplies—a carpenter can repair some damage, and a medical specialist can improve treatment—but cannot fully replace missing materials or medicine.

---

# 6. Voyage Simulation

The voyage is intended to be long, but it must not force the player to watch empty ocean for hours. Routine operations should run at accelerated speed; meaningful decisions and crises should slow down.

## 6.1 Initial ship systems
- **Hull:** condition, leak rate, inspection, patching, reinforcement.
- **Rigging and sails:** condition, wind exposure, sail configuration, reefing/reduction, repair.
- **Navigation:** estimated position, heading, chart confidence, route plotting, bearings, revisions.
- **Cargo:** capacity, condition, placement, loading/unloading, securing.
- **Bilge and pumps:** water accumulation, pump effectiveness, crew assignments.
- **Crew operations:** watch, fatigue, skill coverage, duty rotations, rest, intervention.

Do not build a full naval physics simulator initially. Implement a simplified sailing model where wind, speed, handling, weather, and crew competence have meaningful effects.

## 6.2 Navigation and uncertainty
The map should distinguish:
- Known routes and coastline.
- Reported but unverified locations.
- Uncertain hazards and conditions.
- Unexplored areas.

Navigation skill improves estimates. Storms, fog, and poor visibility reduce confidence. Detours may discover a harbor, island, or trading opportunity but consume time and supplies.

## 6.3 Time controls
Support normal time, accelerated routine time, and paused planning. Consider automatic slowdown when a critical event starts. The system must ensure that important events remain understandable and actionable at each speed.

## 6.4 Crew task examples
- Maintain rigging.
- Pump bilge.
- Cook.
- Treat injuries.
- Maintain sanitation.
- Repair hull or equipment.
- Observe weather.
- Stand watch.
- Rest.

Tasks need clear requirements, duration, interruption conditions, and consequences. The player may directly control a crew member for an urgent or precision task.

## 6.5 Weather and cascading crises
Weather can affect ship systems, navigation, and crew simultaneously. A storm might damage rigging, increase leaks, reduce navigation reliability, injure crew, and exhaust the repair team—creating a second crisis from the first. Give plausible warning signs. Not every danger is preventable, but outcomes should be causal and learnable rather than arbitrary.

## 6.6 Other vessels and discoveries
Later content may include other ships, travelers on independent missions, ports, islands, and discoveries. These are not required for the first vertical slice beyond simple placeholders or one optional encounter if it adds little complexity.

---

# 7. Shipwreck and Survival Continuation

A shipwreck is a transition to a new survival situation, not merely a game-over screen.

The player first assesses survivors, injuries, supplies, water, shelter, and salvage. The next goals may include:
- Secure fresh water and food.
- Establish temporary shelter and camp organization.
- Search wreckage and nearby terrain for salvage.
- Craft or repair equipment.
- Build a boat or raft if plausible.
- Signal passing ships or seek rescue.
- Find another settlement or port.
- Establish a permanent settlement if departure is not viable.

The branch must preserve actual survivors, health, fatigue, injuries, remaining/salvaged inventory, relationships, regional knowledge, equipment, and obligations. Branch options should reflect geography, resources, and survivors' capabilities. Do not promise rescue if no plausible rescuer can exist.

---

# 8. Settlement and Colony Management

The settlement phase transitions toward RTS/settlement management while keeping the same persistent people and campaign history.

## 8.1 Progression
1. **Landfall:** find water, food, shelter, survivors, and salvage; establish a camp.
2. **Survival:** build durable shelter, food production, sanitation, storage, and prepare for seasonal shortages and disease.
3. **Establishment:** add workshops, farmland, defenses, trade, docks, and additional settlers when plausible opportunities arise.
4. **Stability:** sustain the population, meet charter goals, manage external relationships, and survive the required period.

## 8.2 Initial buildings
- Shelter.
- Storehouse.
- Well or water station, only where viable.
- Garden or field.
- Workshop.
- Infirmary.
- Dock or landing.

Buildings should occupy physical locations and require real materials and labor, not only currency. Assign characters to work; skill, tools, health, and fatigue influence outcomes. Crew relationships and conflicts persist.

Keep the first settlement compact. Do not build a complex civilization simulator before the survival loop is proven.

## 8.3 External world and historical care
Later systems may include trade, negotiation for access, neighboring communities, territorial disputes, other European expeditions, and sponsor demands. Indigenous communities must be distinct, politically and culturally specific where research supports it, and represented as agents with their own goals—not as interchangeable enemies or a generic obstacle. Avoid framing the Americas as empty or awaiting discovery by Europeans.

---

# 9. Technical Architecture Principles

The exact engine should be selected only after repository and team constraints are understood. Unity or Godot may be appropriate; a custom engine is not recommended for this project unless there is an exceptional, well-supported reason.

## 9.1 Logical layers
Separate these responsibilities, using idiomatic patterns for the chosen engine:

1. **Campaign state:** phase, objectives, clock, persistent character IDs, inventory, relationships, history, and outcome.
2. **Character simulation:** needs, health, skills, traits, behavior, tasks, relationships, survival status.
3. **World simulation:** time, weather, ship systems, supplies, resources, events, settlement production.
4. **Phase presentation:** port, voyage, shipwreck, and settlement scenes/views.
5. **Input and commands:** direct control, selection, orders, pause/speed controls, interactions.
6. **UI:** status, character information, recruitment, cargo, map, task queue, journal, alerts.
7. **Persistence:** versioned save/load and safe migrations as the model changes.
8. **Diagnostics/testing:** simulation logs, deterministic test scenarios, assertions, debug tools.

## 9.2 Architecture rules
- Keep gameplay rules independent of visual effects and scene layout wherever practical.
- Use explicit interfaces for task execution, time advancement, state transitions, and save/load.
- Keep data definitions separate from runtime state.
- Use stable IDs for characters, items, tasks, events, and other persistent entities.
- Use reproducible random seeds in tests and log important random inputs/outcomes.
- Do not let UI widgets directly mutate arbitrary simulation internals; route actions through clear commands or services.
- Avoid overengineering. Introduce abstractions when they support a real requirement, testability, or expected extension.
- Add save/load from the earliest viable milestone, not at the end.
- Use simulation tiers later if world scale requires it: detailed updates near the player and simplified updates for distant entities.

## 9.3 Save/load requirements
Save files must preserve enough state to resume the campaign consistently, including the current phase, clock, random state where appropriate, character records, task state, ship condition, cargo, weather/event state, relationships, objectives, and history. Include a schema/version field. Failed or partial loads should fail safely and explain the issue rather than silently resetting the campaign.

## 9.4 Testing principles
Build headless or non-rendering simulation tests as early as the engine allows. At minimum, test:
- Provision consumption over a long voyage.
- Fatigue accumulation and recovery.
- Task eligibility and interruption.
- A dead character cannot work, move as an active survivor, or reappear after loading.
- Save/load preserves a storm-damaged ship and crew state.
- Character generation produces unique IDs and valid values.
- Shipwreck transition preserves survivors and correctly accounts for lost/salvaged items.
- Phase transitions cannot duplicate characters or inventory.
- Invalid state transitions are rejected and logged.

---

# 10. Milestones and Acceptance Criteria

## P0 — Simulation Foundation

Implement in this order unless repository inspection reveals a clear dependency change:
1. Project boot/build and a documented run path.
2. Isometric camera and basic movement/collision/interaction if the chosen engine requires it for the first playable scene.
3. Persistent character model: IDs, skills, needs, health, traits, inventory, relationships, status.
4. World clock and task simulation: durations, interruptions, rest, needs.
5. Resource and inventory system: item quantities, equipment, consumption, cargo capacity.
6. Save/load and event history.
7. Data-driven definitions for initial characters, skills, items, tasks, ships, and buildings.

**P0 exit criteria:** Characters can be created, moved or assigned tasks, consume resources, become tired or injured, and retain state through save/load. Core simulation rules can be tested without relying on rendered presentation.

## P1 — First Playable Vertical Slice

1. **Port:** one compact district or connected small districts and 20–30 scheduled NPCs.
2. **Recruitment:** at least six candidates with different skills, motives, reputations, and hidden qualities; investigation and negotiation reveal information.
3. **Preparation:** roster, cargo, provisions, ship inspection, charter, and departure deadline.
4. **Voyage:** route selection, wind-influenced progress, estimated position, time controls, and supply consumption.
5. **Ship damage:** at least hull/leak and rigging damage with repair tasks and material requirements.
6. **Crew survival:** hunger/thirst/fatigue/health, work/rest assignments, one storm crisis, and one medical crisis.
7. **Continuation:** a shipwreck branch that transfers surviving characters and salvage to a beach/camp.
8. **Landfall/settlement:** basic shelter, water, food gathering, construction, and a short survival objective.
9. **Chronicle:** important recruitment, promise, damage, injury, death, route, and settlement events are recorded.

**P1 exit criteria:** A player can recruit a crew, provision and depart, experience a meaningful crisis caused by interacting systems, reach land or suffer a shipwreck, and continue with the actual surviving crew and resources. State persists through save/load. Success and failure are understandable.

## P2 — Depth and Variety

- More traits, dialogue, relationships, and information provenance.
- Additional ship types, provisioning strategies, weather, navigation hazards, ports, and islands.
- Crew conflicts, leadership succession, and sponsor reputation/contract renegotiation.
- Seasonal agriculture and production.
- Improved character animation, readable feedback, and quality-of-life UI.
- Broader automated test coverage and balance tools.

## P3 — Expansion

- Hundreds of detailed NPCs and schedules.
- Autonomous rival expeditions.
- Complex diplomacy and regional politics.
- Procedural world generation.
- Advanced sailing/weather simulation.
- Deep story arcs and multiple regions/ships/scenarios.

Do not begin P2 or P3 systems until P1 exit criteria are met, unless the user explicitly reprioritizes the roadmap.

---

# 11. First Playable Scenario: “The Last Berth”

Use this as the scope anchor for the first vertical slice.

## Setup
- The captain has a sponsor charter.
- The ship is scheduled to depart at dawn.
- Funds are limited and there are empty berths.
- The port contains approximately 25 NPCs and six plausible recruit candidates.
- Candidates have different skills, references, motives, and hidden qualities.

## Player experience
1. Explore the port and meet candidates.
2. Investigate skills, motives, and reputation using conversation, references, observation, or tests.
3. Negotiate terms and recruit a crew.
4. Buy provisions and tools within cargo and budget limits.
5. Inspect the ship and decide whether to depart on time or accept a cost for delay.
6. Sail a short route with routine time acceleration and meaningful navigation choices.
7. Encounter a storm that tests crew, ship, and resource systems.
8. Reach land or enter a shipwreck-survival branch.
9. Establish a basic camp and meet a short survival objective—or reach a clearly explained unrecoverable failure.

## Definition of done
- The player can move and interact in the port.
- Candidates have persistent identities and capabilities; the player does not know everything immediately.
- Recruitment and provisioning create meaningful trade-offs.
- The ship advances, consumes resources, and can sustain damage.
- Crew can perform tasks, become fatigued or injured, and require care/rest.
- At least one crisis emerges from interacting systems rather than a purely scripted cutscene.
- Landfall and shipwreck outcomes preserve correct campaign state.
- Save/load works through a representative crisis.
- The game records important events and gives understandable feedback about outcomes.

---

# 12. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| NPC simulation becomes expensive | Start with 20–30 NPCs; use schedules and event-driven updates; add simulation tiers only when needed. |
| Voyage becomes repetitive | Accelerate routine time; ensure route, weather, supplies, ship condition, and people create meaningful decisions. |
| Random events feel unfair | Provide plausible warning signs, causal rules, event logs, and reproducible test seeds. |
| Character generation feels implausible | Use constrained trait/background combinations and validate generated data. |
| Phases feel disconnected | Share persistent IDs and state; test every transition for continuity. |
| Settlement scope explodes | Begin with a compact survival camp and a small set of buildings/objectives. |
| Failures are confusing | Explain causal chains in UI, journal, and debug logs. |
| Agent overbuilds or rewrites architecture | Require ticket-sized changes, acceptance criteria, tests, and explicit approval for major technology changes. |
| Historical portrayal becomes simplistic | Research regional/time-specific contexts and represent Indigenous communities as distinct actors with agency. |

---

# 13. Suggested Repository Structure

Adapt this to the chosen engine and existing repository. Do not impose this layout blindly.

```text
/docs
  GAME_VISION.md
  TECHNICAL_DESIGN.md
  MILESTONES.md
  DECISIONS.md
/src or engine project folders
  /campaign
  /characters
  /tasks
  /inventory
  /world
  /ship
  /phases
  /ui
  /persistence
/tests
  /unit
  /simulation
  /integration
/data
  /characters
  /skills
  /items
  /tasks
  /events
  /ships
  /buildings
```

Keep the project's actual conventions if they already exist. `DECISIONS.md` should record consequential decisions, alternatives considered, and why an option was selected.

---

# 14. Initial Agent Work Plan

Do not start by implementing every feature described above. Follow this sequence:

### Ticket 0 — Repository and feasibility audit
- Inspect project files and current state.
- Identify engine/runtime, build commands, test framework, style conventions, and known blockers.
- Report current capabilities versus the P0/P1 plan.
- Recommend the next three small tickets.
- **Acceptance:** no code changes unless needed to run existing tests or the user explicitly asks for implementation in this first pass.

### Ticket 1 — Core data model and invariants
- Define the minimal campaign, character, inventory, task, ship, event-history, and phase-state models needed for the first slice.
- Define invariants: unique IDs, valid skill ranges, valid survival states, no work by dead/unavailable characters, no duplicated inventory across transitions.
- Add unit tests for validation and ID uniqueness.
- **Acceptance:** models are documented, testable, and do not depend on UI objects.

### Ticket 2 — Simulation clock and task execution
- Add deterministic time advancement, task duration, task eligibility, completion/interruption, and basic need changes.
- Add tests for rest, fatigue, task interruption, and resource-consuming tasks.
- **Acceptance:** test scenarios can advance time and inspect results without a rendered scene.

### Ticket 3 — Inventory, supplies, and save/load
- Add item definitions, quantities, carrying/cargo constraints, consumption, and versioned persistence.
- **Acceptance:** a test campaign can be saved, loaded, and compared for state equivalence.

### Ticket 4 — Small playable test scene
- Implement movement/interactions and a debug-visible view of a few characters and tasks using the existing engine.
- **Acceptance:** player can interact with characters and assign a task; the simulation remains the authority for state.

### Ticket 5 — Recruitment prototype
- Create six candidates with hidden attributes, evidence sources, negotiation terms, and accept/refuse rules.
- **Acceptance:** player can learn candidate information through actions and recruit based on mutually acceptable terms.

### Ticket 6 — Voyage-to-crisis vertical slice
- Add the minimal ship, supplies, route/time progression, storm, repair tasks, and health consequences.
- **Acceptance:** storm outcomes are causal, testable, and recorded in the chronicle.

### Ticket 7 — Shipwreck or landfall continuation
- Transfer surviving people and valid salvage to the next phase; provide water/food/shelter actions and a short objective.
- **Acceptance:** no character or item duplication/loss beyond explicitly modeled damage/loss; save/load works after transition.

The agent may split tickets further. It should not combine them into one large implementation task if that makes review or testing difficult.

---

# 15. Definition of Good Implementation

Every change should aim for:
- Clear, human-readable code and names.
- Small modules with focused responsibilities.
- Explicit data ownership and state transitions.
- Predictable, testable behavior.
- Useful errors and logs instead of silent failure.
- Data-driven content when it reduces code duplication.
- Accessibility and UI clarity where applicable.
- Sensible performance without premature optimization.
- A working build at each meaningful milestone.
- Documentation updated when architecture or behavior changes.

Avoid:
- A giant monolithic script that owns all gameplay systems.
- UI-only fake state that does not survive phase changes.
- Re-generating characters when entering a new phase.
- Unbounded feature expansion before the vertical slice works.
- Complex frameworks or dependency injection solely for fashion.
- Randomness that cannot be reproduced during debugging.
- Silent data resets or hidden changes to project conventions.

---

# 16. Copy-and-Paste Agent Kickoff Prompt

Use the following prompt when giving this document and the project repository to an AI coding agent:

> You are the technical lead and implementation agent for **The Expedition**. Read the attached `The_Expedition_Agent_Handoff.md` completely before making changes. First inspect the existing repository and identify the current engine, architecture, run/build/test commands, and existing implementation. Treat the handoff as the product vision and roadmap, but prioritize a small, testable vertical slice over implementing every listed feature.
>
> **Do not immediately rewrite or scaffold the entire project.** Start with a repository audit and a concise gap analysis against P0 and P1. Then propose the next three small implementation tickets, each with scope, dependencies, files likely affected, acceptance criteria, tests, and risks. Preserve existing working behavior. Do not change engine, framework, or core architecture without explaining the need and requesting approval for consequential changes.
>
> The core invariant is that the same persistent characters and campaign state continue through port preparation, voyage, shipwreck/landfall, and settlement. Keep simulation rules independent from presentation where practical, use stable IDs, data-driven definitions, reproducible tests, versioned save/load, and explicit state transitions. Implement only the agreed ticket. Run relevant tests and report actual results; never claim success for tests that were not run. End with changed files, known limitations, run instructions, and the next recommended ticket.
>
> Begin with **Ticket 0 — Repository and feasibility audit**. Do not modify code in this first pass unless a minimal change is necessary to run existing checks or I explicitly approve implementation.

---

# 17. Open Decisions to Resolve Before Major Implementation

These are intentionally not silently decided in this document:
1. **Engine and target platforms:** inspect current repository and the developer's experience before recommending Godot, Unity, or another established engine.
2. **Historical starting port and destination region:** select based on research, desired narrative, and feasible scope.
3. **Exact charter and deadline:** define the first scenario's measurable win/fail conditions.
4. **Crew and ship scale:** tune to readable simulation and performance; do not assume a historically huge crew is required.
5. **Time scale:** establish how much in-game time passes per real-time minute at normal and accelerated speeds.
6. **Control scheme:** finalize direct movement, crew selection, task orders, pause, and speed controls after a basic interaction prototype.
7. **Historical research standard:** identify reliable sources and review expectations for geography, ships, daily life, and Indigenous societies in the selected region and period.
8. **Save format and platform requirements:** determine after repository and engine inspection.

The agent should surface these decisions at the point they affect implementation, not block all early progress by trying to solve every future question up front.

---

**North-star test:** If a crew member recruited in the port is injured in a storm, distrusts the captain after a broken promise, survives a shipwreck, and later helps build a settlement, the game should preserve that same person's identity and let those experiences affect what happens next.
