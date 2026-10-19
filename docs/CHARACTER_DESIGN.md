# THE EXPEDITION — CHARACTER DESIGN & SIMULATION SPECIFICATION

**Status:** Authoritative character-system design brief
**Purpose:** Defines who characters are, how they are generated, what they know, how they behave, how they change, and how the same individual persists through Port → Voyage → Disaster → Settlement.

## 0. Character North Star
People are the primary emotional system of **The Expedition**.

> **A person encountered in port is the same person who works aboard ship, survives or dies during disaster, reaches land, and helps build the settlement.**

[REQUIRED] Persistent characters use stable unique IDs.
[REQUIRED] Phase transitions never replace persistent people with generic equivalents.
[REQUIRED] Death, injuries, relationships, knowledge, equipment, experience, promises, and important history persist.
[DON'T] Model characters as disposable RPG stat containers.

## 1. Character Lifecycle
**Generated/Existing → Encountered → Observed → Investigated/Known → Recruited/Rejected/Independent → Expedition Member → Changed by Experience → Survivor/Departure/Death → Founder/Resident/External Character → Chronicle**

A character can remain meaningful even if never recruited.

## 2. Core Character Record
Each persistent character supports:
- Identity: ID, name, age, origin, languages, occupation/background, social context, visual identity reference.
- Capability: skills, experience, physical attributes, learned knowledge.
- Psychology: traits, temperament, risk tolerance, preferences, fears, ambitions, goals.
- Physical state: health, injuries, illnesses, hunger, thirst, fatigue, stress, survival status.
- Social state: relationships, obligations, promises, conflicts, affiliations.
- Material state: personal inventory, equipment, clothing identity.
- Employment: employer, contract, wages, duties, expedition membership.
- Knowledge: facts, rumors, observations, beliefs and provenance.
- History: major events, achievements, failures, injuries, promises and expedition milestones.
- Current state: location, task, queue, schedule, availability and movement.

## 3. Truth, Knowledge, and Belief
Maintain three independent layers:
1. **Actual Truth** — authoritative simulation state.
2. **Player Knowledge** — what the captain/player has learned.
3. **Character Belief** — what an NPC believes.

[REQUIRED] These may disagree. UI exposes player knowledge, not hidden truth.

## 4. Knowledge Provenance
Important knowledge stores source, time learned, confidence, observation/report type and contradictions.

Example:
`Mateo: Carpentry — Experienced | Source: former employer + direct observation | Confidence: High`

## 5. Skills
Prototype scale:
0 no meaningful training; 1 basic familiarity; 2 routine competence; 3 experienced; 4 highly skilled; 5 exceptional specialist.

Categories may include maritime, survival, medical, practical and social skills. Physical attributes such as strength, endurance, coordination and perception remain separate from learned skill.

## 6. Task Outcomes
Contribution depends on:
`skill + physical condition + fatigue + tools + assistance + environment + difficulty + time + traits + controlled randomness`

No single stat determines a complex task.

## 7. Traits
Prototype target: 2–3 meaningful traits per persistent character. Traits affect behavior, not merely flat modifiers.

Examples: cautious, impulsive, proud, loyal, resourceful, patient, stubborn, sociable, suspicious.

Generation should respect age, background, occupation, experience and compatibility.

## 8. Motivation and Goals
Possible motivations: money, adventure, advancement, obligation, escape, family, professional ambition, faith/personal conviction, fear, desire to return.

Goals may include earning money, reaching a destination, protecting someone, learning a trade, returning home or fulfilling a promise.

[REQUIRED] Persuasion cannot override every core motive.

## 9. Needs
Prototype: hunger, thirst, fatigue, health, stress.
Needs respond to activity/environment rather than identical flat decay.

## 10. Health
Support injury, illness, treatment, recovery and persistent impairment. Injury affects relevant capability. Visual state should reflect health through the Character Art specification.

## 11. Stress
Responds to danger, exhaustion, hunger, conflict, loss, uncertainty, leadership and recovery.
[DON'T] Make stress a generic insanity meter.

## 12. Relationships
Relationships are asymmetric and may include trust, respect, affection, hostility, obligation and fear. Store meaningful causes/history.

Example: `Mateo → Ilario: strong trust; Ilario pulled Mateo from flooded hold, Day 18.`

## 13. Shared Experiences
Rescue, betrayal, successful work, hardship, insults, promises, mentorship, ration decisions and losses can create persistent social memories.

## 14. Group Dynamics
Groups may emerge around friendship, language, occupation, family, leadership loyalty, grievance and shared experience. Shared origin does not automatically create a faction.

## 15. Schedules
Port: work/eat/socialize/sleep/travel.
Voyage: watch/work/eat/rest/socialize/emergency response.
Settlement: assigned work/meals/rest/personal/community activity.

Schedules are interruptible by urgent needs/events.

## 16. Tasks and Autonomy
Characters support current task, queued tasks, standing duty, interruption, cancellation and rest.

Autonomy priority:
1. immediate safety,
2. assigned task,
3. needs,
4. standing schedule,
5. social/personal behavior.

Characters are not mindless robots and may refuse impossible, dangerous or unacceptable orders for understandable reasons.

## 17. Direct Control
Direct control never erases skill, injury, fatigue, equipment or task requirements. Player dexterity cannot turn an untrained character into an expert navigator or surgeon.

## 18. Learning and Mentorship
Skills improve slowly through practice, successful work, crisis experience and mentorship. Growth must preserve the importance of recruitment. Mentorship requires skill, time, relationship and repeated exposure.

## 19. Languages
Competence may be none, limited, functional or fluent. Language affects recruitment, social interaction, information reliability and diplomacy. Historical languages require scenario research.

## 20. Recruitment
Candidates evaluate the captain using pay, contract, destination, ship condition, sponsor, reputation, existing crew, obligations and risk. Hiring is not a charisma check.

## 21. Contracts
May include wages, payment timing, duties, duration, destination, reward/shares where historically appropriate, and promises. Broken terms affect trust.

## 22. Character Generation
Pipeline:
1. historical context,
2. origin/background,
3. age,
4. occupation/experience,
5. skills,
6. physical attributes,
7. traits,
8. motivations,
9. relationships,
10. languages,
11. equipment/clothing,
12. visual identity,
13. current goals/schedule.

Generated people must be internally coherent.

## 23. Historical Plausibility
Names, origins, languages, occupations, clothing, equipment and social context require research.
[DON'T] Use generic medieval-European tables.
[DON'T] Use culture as personality shorthand.

## 24. Age and Occupation
Age influences plausible experience, condition, appearance and history without being a universal debuff. Occupation informs likely skills, tools, contacts, clothing and routines without hard-locking future roles.

## 25. Character Uniqueness
Two people with the same skill rating differ through attributes, traits, motives, relationships, knowledge, history, health, equipment, confidence, goals and appearance.

## 26. Memory and Chronicle
Persist meaningful memories rather than every moment. Major events feed the global Chronicle using persistent character IDs.

## 27. Death
On death: stop tasks, preserve record, resolve inventory/body context, trigger relationship reactions, update staffing and Chronicle. Never delete the historical character record.

## 28. Captain Death and Succession
The captain is a normal persistent character with authority. A successor receives authority—not copied identity, skills, relationships or history. Final succession selection is [APPROVAL REQUIRED].

## 29. Phase Continuity
Port → Voyage: same IDs, contracts, relationships, inventory and knowledge.
Voyage → Shipwreck: same survivors, injuries, equipment, relationships and memories.
Voyage → Settlement: same survivors become founders.

[REQUIRED] Automated transition tests.

## 30. Simulation LOD
Tier A active/visible/full; Tier B nearby/offscreen reduced; Tier C distant/abstracted.
LOD changes frequency/detail, never identity or fundamental truth.

## 31. Dialogue
Dialogue derives from identity, knowledge, relationship, current state, goals and event context.
[DON'T] Let generated dialogue invent authoritative facts.

## 32. Behavior Legibility
Player and diagnostics should be able to answer: What is this person doing? Why? Why did they stop/refuse? Why are they upset?

## 33. UI
Follow `UI_UX_Design_System.md`. Prioritize name → current state → relevant capability → relationship/context → deeper detail. Raw truth values remain debug-only where appropriate.

## 34. Performance
Avoid all-to-all relationship checks, per-frame need updates, global task searches and unbounded memories. Prefer scheduled updates, event-driven relationships, ID maps and bounded meaningful history.

## 35. Automated Tests
Test unique IDs, valid/coherent generation, needs, fatigue/rest, injury effects, relationship persistence, knowledge provenance, refusal causes, death, save/load, phase transitions, mentorship and LOD invariants.

## 36. Character Vertical Slice
Create 8–12 persistent people demonstrating distinct identities, skills, traits, motives, relationships, schedules, uncertain knowledge, recruitment, tasks, fatigue, injury, conflict, mentorship and persistence across phases.

## 37. Acceptance Criteria
- stable IDs
- coherent generation
- truth/knowledge/belief separation
- skills and attributes matter appropriately
- traits affect behavior
- needs respond to activity
- health affects capability
- relationships have causes
- purposeful schedules/tasks
- recruitment respects motives
- learning/mentorship works
- history/death persist
- exact people survive phase transitions
- save/load preserves state
- behavior is explainable

## 38. Agent Guardrails
Reuse shared task/inventory/save/event architecture. Historical identity content requires research. Do not create stereotype-driven personality, interchangeable workers, hidden-truth UI, or opaque AI when explainable rules suffice.

## 39. Final Design Test
> If I remove this person's skill numbers, can I still describe who they are?

> If I meet them forty hours later, do their history, injuries, relationships and behavior make them recognizably the same person?

> If they die, does the expedition materially and emotionally change?

**People are not resources carried by the expedition. People are the expedition.**
