# THE EXPEDITION — UI / UX DESIGN SYSTEM

**Status:** Authoritative global interface and interaction specification  
**Scope:** Port, Voyage, Navigation/Chart, Landfall, Settlement, menus, management screens, alerts, character interaction, and shared controls.

**Companion documents:**  
- `The_Expedition_Agent_Handoff.md`
- `The_Expedition_Art_Bible.md`
- `The_Expedition_Technical_Spec.md`
- `Port_Experience_and_Simulation.md`
- `Voyage_and_Ship_Experience_and_Simulation.md`
- `Navigation_and_Chart_System.md`
- `Settlement_Experience_and_Simulation.md`

---

# 0. Purpose

The Expedition contains a large amount of simulation information: people, skills, uncertain knowledge, relationships, inventory, ship systems, navigation, tasks, health, time, resources, construction, diplomacy, sponsor obligations, and history.

The interface must make that depth understandable **without covering the game world in permanent panels**.

The central UX principle is:

> **The world is the primary interface. UI appears because the player needs information or intends to make a decision—not merely because information exists.**

The interface should feel appropriate to an early-16th-century expedition while retaining modern usability.

Historical materials provide atmosphere.

Modern information architecture provides clarity.

---

# 1. Core UI Principles

[REQUIRED] The playable world receives the majority of screen space during ordinary play.

[REQUIRED] Persistent HUD contains only information that routinely requires immediate awareness.

[REQUIRED] Detailed information is contextual, collapsible, or intentionally opened.

[REQUIRED] The same visual and interaction language spans Port, Voyage, Navigation, and Settlement.

[REQUIRED] UI must represent uncertainty honestly rather than exposing hidden simulation truth.

[REQUIRED] Important information must be understandable through hierarchy, not simply additional text.

[REQUIRED] Common actions should require few clicks.

[REQUIRED] The UI must remain usable while paused and during time acceleration.

[REQUIRED] Mouse/keyboard interaction is the primary initial target unless repository/platform requirements say otherwise.

[PREFERRED] Important actions can often be initiated either through the world or an appropriate management screen.

[DON'T] make every system permanently visible.

[DON'T] use parchment textures indiscriminately.

[DON'T] turn historical flavor into poor readability.

[DON'T] expose developer/debug state in production UI.

---

# 2. Three Information Layers

## Layer A — Persistent HUD

Only information routinely needed at a glance.

Candidate elements:
- date/time,
- simulation speed,
- pause state,
- critical alerts,
- selected character/object,
- concise current objective,
- phase-specific critical status.

Target:
**small, quiet, immediately legible.**

## Layer B — Contextual UI

Appears because of current interaction.

Examples:
- character inspection,
- merchant,
- recruit negotiation,
- worksite,
- cargo stack,
- ship component,
- building,
- conversation,
- wounded character.

Target:
**show only information relevant to this interaction.**

## Layer C — Management UI

Opened deliberately.

Examples:
- Crew,
- Expedition,
- Cargo,
- Ship,
- Chart,
- Chronicle,
- Charter,
- Settlement,
- Relationships/knowledge where appropriate.

Target:
**dense information is acceptable because the player intentionally entered a management context.**

---

# 3. Three-Second Rule

Every primary gameplay state should answer within approximately three seconds:

1. What phase/context am I in?
2. Is something urgent?
3. What am I currently doing?
4. What should I probably care about next?

[REQUIRED] Do not require reading every numeric value to understand the current situation.

---

# 4. World-First Rule

When all optional panels are closed:

> **The game should look like a beautiful isometric historical world—not a dashboard laid over a game.**

This is an acceptance criterion.

---

# 5. Visual Language

The interface follows **Illustrated Historical Realism**.

Use historical inspiration from:
- charts,
- manifests,
- contracts,
- ledgers,
- letters,
- journals,
- ship diagrams,
- seals,
- period materials.

But abstract them into a consistent modern UI system.

Example:
A cargo manifest can visually resemble a working document while still supporting:
- sorting,
- filtering,
- tooltips,
- comparison,
- clear buttons,
- keyboard navigation.

[DON'T] simulate illegible handwriting for primary information.

---

# 6. Panel Design

Panels should use:
- restrained opacity,
- clear hierarchy,
- limited decorative framing,
- consistent spacing,
- readable typography,
- contextual icons,
- obvious close/back behavior.

[PREFERRED] Semi-transparent overlays may reveal enough world context to preserve spatial orientation.

[DON'T] stack multiple large panels unless the workflow explicitly requires comparison.

---

# 7. Typography

Typography must prioritize readability.

Establish roles:
- display/title,
- section heading,
- body,
- metadata,
- numeric/status,
- annotation/flavor.

[REQUIRED] Primary gameplay text uses a highly readable typeface.

[PREFERRED] Historical/display typography is reserved for titles, seals, major headings, or authored documents.

[DON'T] use decorative historical fonts for dense management data.

---

# 8. Color

Color communicates state but is never the only communication channel.

Potential semantic categories:
- neutral/information,
- positive/stable,
- caution,
- urgent,
- critical,
- unavailable/disabled,
- uncertain/unverified.

[REQUIRED] Pair color with icon, text, pattern, or shape.

[REQUIRED] Maintain accessibility for common color-vision deficiencies.

[DON'T] make the interface look like a modern neon sci-fi HUD.

---

# 9. Icons

Icons should be:
- simple,
- readable at small size,
- consistent stroke/shape language,
- supported by tooltips where ambiguity exists.

Use icons for frequent concepts:
- food,
- water,
- health,
- fatigue,
- cargo,
- tools,
- ship damage,
- weather,
- warning,
- time,
- task.

[DON'T] invent an icon for every rare concept.

---

# 10. Spacing and Density

Management screens may be information-dense.

Exploration HUD should not be.

[REQUIRED] Establish reusable spacing tokens in implementation rather than arbitrary per-panel values.

[REQUIRED] Interactive targets require consistent minimum hit area.

---

# 11. Interaction States

All interactive UI controls require:
- default,
- hover,
- pressed,
- focused,
- selected where applicable,
- disabled,
- warning/critical where applicable.

World interactables require clear contextual feedback without permanent glowing outlines.

Possible:
- cursor change,
- subtle highlight,
- small contextual label,
- selection ring,
- edge emphasis.

---

# 12. Persistent Top-Level Navigation

Primary management categories should be reachable consistently.

Recommended conceptual categories:

**People | Expedition | Cargo | Ship | Chart | Chronicle**

During Settlement:

**People | Settlement | Stores | Region | Charter | Chronicle**

Exact presentation may be tabs, compact buttons, keyboard shortcuts, or contextual navigation.

[APPROVAL REQUIRED] Final visual arrangement after usability testing.

---

# 13. Port HUD

Default port exploration should surface approximately:

- Day / time,
- departure deadline,
- time controls,
- current selected character,
- urgent expedition warning,
- current objective/lead.

Example concept:

```
DAY 2 · 14:20                 DEPARTURE 18h
                                   [▶] [≫]

                 WORLD

Captain Álvaro
Current lead: Find a navigator

                         ⚠ Crew 3/4 minimum
```

Detailed Crew/Cargo/Chronicle information remains available but not permanently expanded.

---

# 14. Voyage HUD

Default voyage HUD prioritizes:
- time/speed,
- weather/wind,
- critical ship condition,
- navigation confidence,
- urgent crew/resource alerts.

Concept:

```
DAY 16 · 06:40               [Pause] [2×] [10×] [60×]

Wind ↙ Moderate               Navigation: FAIR
Sea: Moderate                 Last observation: 9h

                    SHIP

⚠ Water usage above projection
```

[REQUIRED] Do not display true position unless debug mode.

---

# 15. Settlement HUD

Default settlement HUD prioritizes:
- time,
- population condition,
- critical food/water status,
- urgent work/crisis,
- current objective.

Concept:

```
DAY 83 · MORNING             [Pause] [2×] [10×]

Population 14                Food: 18d
Able 10                      Water: Stable
Injured 3

                 SETTLEMENT

Priority: Finish storehouse before rain
```

---

# 16. Time Controls

Time is a global system.

Controls should remain in a consistent location.

States:
- Pause,
- Normal,
- accelerated levels.

[REQUIRED] Current speed is unmistakable.

[REQUIRED] Critical events can interrupt/slow acceleration according to settings.

[REQUIRED] Hover/tooltips explain acceleration.

[PREFERRED] Keyboard shortcuts for common speeds.

[DON'T] allow the player to accidentally remain at extreme acceleration after a critical interruption.

---

# 17. Alert System

Severity:

### Information
Routine update.

### Attention
Potential issue worth reviewing.

### Urgent
Action likely needed.

### Critical
Immediate threat.

Each alert may contain:
- icon,
- concise cause,
- affected person/system/location,
- timestamp,
- focus action.

Example:

**CRITICAL — Hull leak worsening**  
Lower hold · Carpenter Mateo assigned  
`Focus`

[REQUIRED] Clicking an alert should focus or open the relevant context where possible.

---

# 18. Notifications vs Interruptions

Not every event should pause the game.

Examples:

Notification:
- task completed,
- routine watch change,
- merchant stock update.

Potential interruption:
- serious injury,
- fire,
- severe leak,
- land sighted,
- critical resource threshold,
- social confrontation.

[REQUIRED] Allow configurable auto-pause categories where practical.

---

# 19. Selected Character HUD

When a character is selected, show concise immediate state:

- name,
- current task,
- health concern if significant,
- fatigue/need warning if significant,
- task progress,
- interaction actions.

Do not immediately expose the entire character record.

---

# 20. Character Inspection

Expanded character view may include:

## Identity
name, age, origin, role/employment, languages.

## Current State
task, location, health, fatigue, needs.

## Known Capabilities
skills and confidence/provenance.

## Traits
only known/observed traits as appropriate.

## Relationships
player-known relationship information.

## Equipment
personal inventory.

## History
meaningful events.

## Contract
wages, obligations, terms.

[REQUIRED] Separate player-known information from simulation truth.

---

# 21. Skill Presentation

Do not default to exposing raw hidden skill numbers.

Example:

**Carpentry — Experienced**  
Confidence: High  
*Observed completing hull repairs; confirmed by employer.*

Unknown:

**Navigation — Claimed experienced**  
Confidence: Low  
*Source: candidate.*

Debug UI may expose numeric values.

---

# 22. Uncertainty Presentation

Use vocabulary consistently:

- Unknown,
- Rumored,
- Claimed,
- Suspected,
- Observed,
- Confirmed.

Confidence:
- Poor,
- Low,
- Fair,
- High,
- Very High where justified.

[REQUIRED] Never present uncertain data with false precision.

---

# 23. Knowledge Provenance

Where useful, show:

**Source:**  
**Observed:**  
**Last updated:**  
**Confidence:**

This applies to:
- candidate skills,
- geography,
- weather reports,
- rumors,
- navigation,
- community information.

---

# 24. Recruitment UX

Recruitment should support world discovery first.

Candidate panel shows:
- identity,
- known background,
- known/claimed skills,
- requested compensation,
- contract expectations,
- known concerns,
- confidence/provenance,
- relationships relevant to decision.

Actions:
- Ask,
- Investigate,
- Negotiate,
- Offer,
- Delay,
- Decline.

[DON'T] turn candidates into perfectly known RPG stat cards.

---

# 25. Negotiation UX

Negotiation should communicate:
- candidate request,
- captain offer,
- concerns,
- concessions,
- unresolved conditions.

[REQUIRED] Failure/refusal provides understandable reason when the captain could reasonably know it.

---

# 26. Crew Management

Crew screen answers:
- Who do I have?
- What can they do?
- What are they doing?
- Who is unavailable?
- Where are skill gaps?
- Who is exhausted/injured?
- Who has unresolved issues?

Support:
- sorting,
- filtering,
- assignment,
- comparison.

[DON'T] automatically solve crew composition unless accessibility/assistance option is enabled.

---

# 27. Task Assignment

Task UX should show:
- task,
- location,
- estimated duration,
- desired skills,
- tools/materials,
- urgency,
- assigned characters,
- relevant risk.

Candidate assignment view can communicate:

**Mateo**
Experienced carpenter  
Fatigue: Moderate  
Estimated completion: ~2h  
Risk: Low

**Tomás**
Basic carpentry  
Fatigue: Low  
Estimated completion: ~5h  
Risk: Moderate

These are estimates based on known information.

---

# 28. Task Queues

Characters may have:
- current task,
- queued task,
- standing duty,
- rest requirement.

UI should make queue order obvious and support reorder/cancel where appropriate.

---

# 29. World Task Feedback

World objects can show contextual task indicators when selected/hovered.

Avoid filling the entire world with permanent floating icons.

Use visibility filters:
- all tasks,
- urgent,
- selected character,
- selected worksite.

---

# 30. Cargo / Inventory

Cargo UI should answer:
- what do we have?
- how much?
- where is it?
- what condition?
- how heavy/bulky?
- what is reserved?
- how long might it last?

Categories may include:
- provisions,
- water,
- medicine,
- tools,
- repair materials,
- trade goods,
- personal equipment.

---

# 31. Provision Projection

Show projections as estimates:

**Fresh water: ~42–51 days**  
Current consumption: 18 L/day  
Confidence: High

Not:

`Water remaining: 46.738 days`

[REQUIRED] Projection changes when crew/rations/losses change.

---

# 32. Ship Management

Ship screen should be spatially understandable.

Prefer a ship diagram/cutaway with selectable systems:
- Hull,
- Rigging/Sails,
- Steering,
- Cargo,
- Bilge/Pumps,
- Provisions,
- Crew Operations.

System state should expose cause.

Example:

**Lower Hull — Damaged**  
Leak: Moderate and increasing  
Temporary patch: 61% integrity  
Assigned: Mateo + Ilario  
Materials reserved: 2 oak plank

---

# 33. Navigation / Chart UX

`Navigation_and_Chart_System.md` is authoritative for mechanics.

UI principle:

> **The chart shows what the expedition believes, not what the engine knows.**

Chart tools:
- pan,
- zoom,
- route leg,
- waypoint,
- measure,
- annotation,
- sighting,
- suspected hazard,
- navigator assessment,
- route/resource estimate.

[REQUIRED] Known, reported, observed, estimated, and planned information have distinguishable visual treatment.

---

# 34. Captain's Chart Presentation

Historical working-chart atmosphere is encouraged.

Possible visual frame:
- chart surface,
- navigator notes,
- compass motif,
- route marks,
- ink annotations.

But controls should behave like modern strategy-game tools.

[DON'T] require the player to physically drag decorative dividers around for routine actions unless testing proves that fun.

---

# 35. Chronicle

The Chronicle is important but should not permanently occupy a large portion of exploration view.

Default:
- collapsed indicator,
- recent important event toast,
- unread count.

Expanded Chronicle supports:
- chronological list,
- filters,
- character,
- ship,
- navigation,
- settlement,
- relationship,
- mission.

Example:

`Day 18, 03:12 — Mateo and Ilario completed the emergency hull patch during the storm.`

[PREFERRED] Clicking names/events focuses relevant records.

---

# 36. Objectives / Charter

Separate:

### Immediate
"Find another qualified sailor."

### Expedition
"Depart by dawn, Day 3."

### Charter
"Establish a viable settlement and maintain it through two winters."

Do not display all objective text permanently.

---

# 37. Settlement Management

Settlement UI should answer:
- Who is available?
- What is urgent?
- What is being built?
- Are food/water stable?
- What resources constrain us?
- What seasonal risk is approaching?

Avoid city-builder resource-bar overload.

---

# 38. Construction UX

Construction flow:

1. choose project,
2. inspect requirements,
3. choose viable site,
4. place,
5. assign priority/labor,
6. construction proceeds physically.

Placement preview communicates:
- valid/invalid,
- slope/terrain,
- material access,
- water/flood concern where known,
- footprint.

[REQUIRED] Do not reveal environmental facts the expedition has not discovered.

---

# 39. Regional Map

Regional map shares information-language with ocean chart:
- known,
- reported,
- observed,
- estimated.

It should not reveal settlements/resources before discovery or credible information.

---

# 40. Conversation UI

Conversations should preserve world context where practical.

Avoid huge cinematic dialogue boxes unless narrative importance warrants them.

Show:
- speaker,
- concise choices,
- relevant known context.

[PREFERRED] Character portrait can provide additional identity/detail.

---

# 41. Tooltips

Tooltips should answer:
- What is this?
- Why does it matter?
- What affects it?

Advanced detail may be expandable.

Example:

**Fatigue — High**  
Reduces work speed and increases mistakes.  
Mateo has worked 9h with limited rest.

---

# 42. Progressive Disclosure

New players see essential information first.

Advanced players can inspect:
- calculations,
- sources,
- history,
- detailed modifiers.

[REQUIRED] Do not front-load every simulation variable.

---

# 43. Confirmation Rules

Confirm destructive/high-cost actions:
- abandon cargo,
- dismiss crew under consequential conditions,
- cancel nearly completed expensive project,
- sail before minimum readiness where allowed,
- destroy structure,
- abandon settlement.

Do not confirm trivial reversible actions.

---

# 44. Error Messages

Errors explain what happened and how to resolve it.

Bad:
`Cannot do that.`

Better:
`Mateo cannot begin hull repair: no repair tools are available.`

---

# 45. Disabled Controls

Where useful, disabled actions explain why via tooltip.

Avoid hiding important actions merely because requirements are unmet.

---

# 46. Saving / Loading

Save UX communicates:
- save success,
- save time,
- campaign/day,
- location/context.

[REQUIRED] Never imply save succeeded before persistence confirms.

Autosave behavior must be visible/configurable as appropriate.

---

# 47. Pause Behavior

Management screens should generally permit pause.

[REQUIRED] Opening complex chart/management screens should not punish a player who needs time to read unless a deliberate real-time rule is approved.

---

# 48. Keyboard / Mouse

Recommended:
- right-click world movement if established by current controls,
- left-click select/interact,
- Escape close/back,
- Space pause,
- number/function keys for time speed where appropriate,
- keyboard shortcuts for major management screens.

[APPROVAL REQUIRED] Final mapping after existing implementation audit.

---

# 49. Camera UX

UI must not fight camera controls.

When focusing an alert:
- preserve prior camera location where practical,
- provide return/back behavior,
- avoid disorienting hard jumps.

---

# 50. Resolution / Scaling

[REQUIRED] UI scales cleanly across supported resolutions.

[REQUIRED] Do not position critical controls using fragile fixed pixels if responsive layout is available.

[REQUIRED] Test common aspect ratios including 16:9 and wider displays.

[APPROVAL REQUIRED] Minimum supported resolution.

---

# 51. Accessibility

Plan for:
- UI scale,
- text size,
- color-blind-safe communication,
- reduced motion,
- configurable auto-pause,
- subtitle/caption support where needed,
- remappable controls if platform allows,
- high-contrast interaction feedback,
- simplified navigation assistance.

Accessibility options should reduce interaction burden without exposing impossible simulation truth unless explicitly selected as an assist.

---

# 52. Motion

UI motion should be restrained.

Use transitions to communicate:
- opening,
- closing,
- state change,
- alert arrival,
- selection.

[DON'T] add bouncy/mobile-app animation to historical management screens.

[PREFERRED] 100–250 ms class transitions for ordinary interface changes, tuned in implementation.

---

# 53. Audio Feedback

Subtle UI sounds may communicate:
- selection,
- confirmation,
- warning,
- page/chart movement,
- task completion.

Use material-inspired sound carefully:
paper, wood, pen, buckle, bell.

[DON'T] make every click produce loud thematic Foley.

---

# 54. Modal Policy

Modals interrupt the world and should be rare.

Appropriate:
- major irreversible decision,
- negotiation requiring focus,
- charter/significant narrative decision,
- critical confirmation.

Inappropriate:
- routine inventory changes,
- ordinary task completion,
- every new Chronicle event.

---

# 55. Information Persistence

Closing a panel never destroys authoritative state.

Examples:
- task continues,
- route remains plotted,
- negotiation state persists where appropriate,
- selected policies remain.

UI is an observer/controller of simulation, not simulation owner.

---

# 56. Debug UI

Debug interface may expose:
- IDs,
- raw skills,
- true position,
- AI state,
- task queues,
- relationship numbers,
- simulation ticks.

[REQUIRED] Debug presentation is visually and architecturally separate from player UI.

---

# 57. Performance

Avoid:
- rebuilding large UI trees every simulation tick,
- updating hidden panels unnecessarily,
- querying entire world from rendering code,
- excessive floating labels.

Prefer:
- event-driven updates,
- cached view models where architecture supports them,
- virtualization for long lists,
- filtered subscriptions.

---

# 58. Port Screenshot Remediation

The current port prototype demonstrates several immediate UX issues:

1. top controls consume excessive horizontal attention;
2. Crew/Hold panel is permanently expanded;
3. expedition-status panel is too large for persistent display;
4. Chronicle occupies a large bottom-center portion of the world;
5. instructional/location text competes with primary controls;
6. world interaction and management information lack clear hierarchy.

Target remediation:

### Persistent
- date/time,
- speed,
- concise departure status,
- critical warning,
- selected character.

### Collapsed/Contextual
- Crew,
- Hold/Cargo,
- Chronicle,
- Chandler list,
- detailed expedition status,
- controls/help.

[REQUIRED] Preserve access to all existing functionality while reducing default screen obstruction.

---

# 59. Visual Validation Screens

Create UI mockups/implemented states for:

## A — Port Exploration
No panels open.

## B — Recruit Inspection
Candidate + uncertain information.

## C — Voyage Storm
Multiple alerts without losing world readability.

## D — Captain's Chart
Route plotting + uncertainty.

## E — Ship Management
Damage and crew assignments.

## F — Settlement
Construction + resource concern.

## G — Chronicle
Expanded historical record.

These become visual consistency tests.

---

# 60. UX Scenario Tests

Test complete workflows:

### Recruit
hear lead -> locate candidate -> inspect -> investigate -> negotiate -> hire.

### Provision
open cargo -> identify shortage -> buy/load -> projection updates.

### Storm
alert -> focus damage -> assign repair -> inspect fatigue -> change priorities -> recover.

### Navigation
open chart -> assess -> plot -> accelerate -> interrupt -> update -> replot.

### Settlement
identify water problem -> inspect knowledge -> assign exploration -> discover source -> create water task.

Measure:
- clicks,
- panel transitions,
- confusion,
- accidental actions,
- world visibility.

---

# 61. UI State Priority

When multiple systems want attention:

1. life-threatening/critical,
2. immediate mission blocker,
3. selected context,
4. routine management,
5. informational/history.

Lower-priority UI should yield visually to higher priority.

---

# 62. Avoid Number Soup

Prefer:

**Water — ~8 days at current use**

over:

`Water 160 / Consumption 19.73 / Capacity 220 / Ratio 0.727`

Detailed numbers may remain available in expanded views.

---

# 63. Avoid Status-Bar Soup

Not every character need five permanently visible bars.

Surface needs when:
- selected,
- abnormal,
- relevant to task,
- requested by player.

---

# 64. Human-Centered Presentation

Because people are the emotional core, character names and faces should generally outrank abstract resource IDs.

Prefer:
`Mateo is exhausted`

over:
`Worker efficiency -32%`

The detailed modifier may be available beneath it.

---

# 65. Historical Flavor Rule

Historical framing should answer:

> "What would the captain conceptually be looking at?"

Examples:
- cargo → manifest,
- voyage → chart/log,
- charter → contract/letter,
- crew → roster,
- history → journal,
- ship → diagram/inspection record.

Then build modern UX on top.

---

# 66. Do / Don't

## DO
- keep the world visible,
- use progressive disclosure,
- prioritize people,
- explain causes,
- show uncertainty,
- use consistent navigation,
- make critical states obvious,
- keep routine actions fast.

## DON'T
- cover screen because data exists,
- make every panel parchment,
- expose hidden truth,
- use decorative fonts for data,
- use color alone,
- require excessive confirmations,
- use modal popups for routine events,
- display every stat at all times.

---

# 67. Agent Implementation Strategy

Before redesigning UI, agent must audit:

1. current UI architecture,
2. shared component system,
3. styling/theme mechanism,
4. responsive layout approach,
5. input handling,
6. state ownership,
7. update frequency,
8. accessibility support,
9. current phase-specific panels,
10. debug/player UI separation.

Then propose:
- design tokens,
- shared primitives,
- persistent HUD shell,
- contextual panel system,
- management navigation,
- alert framework,
- responsive behavior.

[REQUIRED] Refactor incrementally.

[DON'T] rewrite all UI simultaneously without a migration plan.

---

# 68. Recommended Implementation Order

1. Establish design tokens.
2. Establish shared typography/icon rules.
3. Build persistent HUD shell.
4. Collapse existing Port panels into contextual/management UI.
5. Implement alert system.
6. Implement selected-character card.
7. Implement reusable management panel shell.
8. Crew screen.
9. Cargo screen.
10. Chronicle.
11. Voyage HUD.
12. Ship management.
13. Chart UI.
14. Settlement HUD.
15. Construction UI.
16. Accessibility/scaling pass.
17. Workflow testing.
18. Performance profiling.

---

# 69. Acceptance Criteria

- [ ] world occupies majority of ordinary gameplay screen,
- [ ] persistent HUD is restrained,
- [ ] time controls are consistent,
- [ ] urgent conditions are immediately visible,
- [ ] optional panels collapse,
- [ ] character information respects player knowledge,
- [ ] uncertainty has consistent visual language,
- [ ] tasks are understandable and assignable,
- [ ] Chronicle no longer permanently obscures world,
- [ ] Port/Voyage/Settlement feel like one interface family,
- [ ] Chart does not expose true position,
- [ ] ship damage UI exposes causes,
- [ ] settlement management avoids resource-bar overload,
- [ ] color is not sole status indicator,
- [ ] UI scales at supported resolutions,
- [ ] keyboard/mouse workflows are efficient,
- [ ] debug information is separate,
- [ ] save feedback is truthful,
- [ ] common workflows pass usability testing.

---

# 70. Final Design Tests

Ask:

> Can the player understand the current situation in roughly three seconds without reading every number?

Ask:

> If every optional panel is closed, does this look like a beautiful game rather than a dashboard?

Ask:

> When the player needs depth, can they reach it quickly without hunting through menus?

Ask:

> Does the UI show what the expedition knows rather than what the simulation secretly knows?

Ask:

> Does the interface make individual people more important, or does it reduce them to rows of numbers?

If the answers are correct, the UI is serving the game.

**The Expedition is a deep simulation, but it should feel deep because the world behaves deeply—not because the screen is permanently full of data.**
