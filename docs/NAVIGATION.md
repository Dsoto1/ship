# THE EXPEDITION — NAVIGATION & CHART SYSTEM SPECIFICATION

**Status:** Authoritative subsystem design brief  
**Primary inspiration:** the deliberate chart-work, plotting, information management, and time-compression rhythm associated with games such as *Silent Hunter*, adapted to an early-16th-century sailing expedition rather than submarine warfare.

**Companion documents:** `Voyage_and_Ship_Experience_and_Simulation.md` and all core project specifications.

# 0. Purpose

Navigation should make the player feel like a captain working with incomplete information.

The map is not an omniscient videogame map.

It is a **working chart** assembled from inherited knowledge, observations, estimates, reports, and the expedition's own experience.

Core loop:

**Observe -> Estimate -> Plot -> Sail -> Accelerate -> Interrupt -> Reassess -> Correct**

# 1. Core Principles

[REQUIRED] Simulation maintains true position separately from player knowledge.

[REQUIRED] The player normally sees estimated position, not true position.

[REQUIRED] Navigation error can accumulate.

[REQUIRED] Skilled navigators improve estimates and warnings but do not provide magical certainty.

[REQUIRED] Time compression makes routine long-distance sailing practical.

[REQUIRED] Significant conditions can interrupt acceleration.

[REQUIRED] The player can annotate and work with the chart.

[DON'T] show GPS coordinates, perfect live heading tracks, or omniscient hazard icons unless explicitly justified as debug tools.

# 2. Information Layers

## Charted / Known
Information considered sufficiently reliable by expedition sources.

## Reported
Unverified information from sailors, documents, rumors, other vessels.

## Observed
Information directly recorded by the expedition.

## Estimated
Current navigational inference.

## Planned
Captain's intended route, waypoints, contingencies.

## Historical Track
The expedition's reconstructed believed track.

[REQUIRED] Believed track may differ from true track.

# 3. Position Model

Maintain:

```
truePosition
estimatedPosition
uncertaintyRegion
lastReliableObservation
estimatedHeading
estimatedSpeed
confidence
```

True position is simulation-only except debug mode.

Uncertainty should grow based on:
- elapsed time,
- poor observations,
- current uncertainty,
- weather,
- current/wind uncertainty,
- navigator skill,
- instrument condition.

# 4. Uncertainty Visualization

Possible visualization:
- faded circle/ellipse/region,
- soft boundary,
- confidence shading,
- annotated estimate.

Example:

```
Estimated Position
Latitude: reasonably constrained
East/West position: highly uncertain
Last strong observation: 17h ago
Confidence: Low
```

[REQUIRED] Avoid false numerical precision.

# 5. Chart Interaction

Player should be able to:
- pan/zoom,
- inspect chart marks,
- place waypoint,
- draw route leg,
- measure approximate distance,
- set intended heading,
- mark sighting,
- add note,
- mark suspected hazard,
- mark reported land,
- compare planned vs believed track,
- ask navigator for assessment,
- estimate supplies/time for route.

[PREFERRED] Interaction feels tactile but remains fast and readable.

# 6. Plotting Route

Route consists of legs.

Each leg may contain:
- target point/region,
- intended heading,
- expected distance,
- expected duration,
- assumptions,
- notes.

Player can revise route at any time.

# 7. Standing Navigation Orders

Examples:
- maintain heading,
- follow plotted leg,
- wake captain if land sighted,
- wake captain if confidence falls below threshold,
- wake captain if weather changes,
- wake captain if vessel sighted,
- wake captain near estimated waypoint.

These orders enable time compression without removing captain agency.

# 8. Time Compression

Example levels:
- x1 normal,
- moderate,
- high,
- very high when safe.

[APPROVAL REQUIRED] Exact multipliers.

Acceleration automatically reduces/stops for configured critical events.

[REQUIRED] Simulation remains authoritative at accelerated time.

# 9. Interrupt Conditions

Possible:
- land sighted,
- vessel sighted,
- hazard,
- weather deterioration,
- wind shift,
- course deviation,
- navigation confidence low,
- observation opportunity,
- damage,
- injury/illness,
- resource threshold,
- social crisis.

Player can configure some categories.

# 10. Observations

Navigation information may come from:

- compass heading,
- estimated speed/time,
- celestial observations appropriate to date,
- coastline/landmarks,
- soundings,
- birds,
- floating vegetation/debris,
- currents,
- water characteristics,
- weather,
- other navigators,
- charts/documents.

[RESEARCH] Exact historical techniques must be validated for circa 1500–1520 and the expedition's culture/origin.

# 11. Celestial Navigation

Do not assume later longitude-solving capability.

[RESEARCH] Represent what navigators of the chosen date could plausibly estimate.

Possible gameplay:
- order observation,
- requires visibility,
- requires qualified person/instrument,
- consumes time,
- produces information with uncertainty.

[REQUIRED] No modern longitude certainty.

# 12. Dead Reckoning

Between strong observations, estimate movement using:
- heading,
- speed estimate,
- time,
- current/wind assumptions.

Error accumulates.

This creates the classic:
"We should be here" problem.

# 13. Speed Estimation

[RESEARCH] Period practices.

Gameplay should represent imperfect speed knowledge.

Navigator quality and conditions influence estimate.

# 14. Soundings

Near coasts/shallow areas, sounding can provide:
- depth,
- trend,
- bottom information where appropriate.

This can help:
- detect approach,
- compare chart,
- avoid grounding.

Requires crew task and conditions.

# 15. Land Signs

Potential clues:
- birds,
- vegetation,
- drift material,
- water changes,
- clouds/weather patterns,
- fishing activity,
- smell only where plausible.

[REQUIRED] These are clues, not guaranteed land detectors.

# 16. Navigator Role

Navigator provides:
- estimates,
- confidence,
- warnings,
- route advice,
- interpretation of observations.

Skill influences:
- error,
- confidence calibration,
- recognition of bad data,
- warning time,
- observation quality.

Traits matter.

An overconfident navigator may communicate certainty differently from a cautious one even with similar skill.

# 17. Multiple Opinions

If multiple knowledgeable crew exist, player may ask for independent assessments.

Disagreement is possible.

This turns people into part of navigation gameplay.

# 18. Instruments

Potentially relevant instruments require research.

Candidates for investigation:
- magnetic compass,
- mariner's astrolabe,
- quadrant,
- cross-staff depending on exact chronology/context,
- sounding lead,
- charts/portolans,
- tables/manuals.

[RESEARCH] Do not include an instrument simply because it is associated generally with the Age of Sail.

# 19. Instrument Condition

Instruments/items can be:
- intact,
- damaged,
- lost,
- poorly calibrated/used.

Condition affects information quality.

# 20. Chart Sources

Charts can come from:
- sponsor,
- captain,
- navigator,
- purchase,
- other ship,
- captured/copied information where scenario permits,
- expedition observations.

Each chart/source can have:
- region,
- reliability,
- age,
- annotations,
- provenance.

# 21. Reported Geography

Charts may include:
- uncertain island,
- suspected shoal,
- reported harbor,
- incomplete coastline.

Use distinct visual language from confirmed observations.

# 22. Updating the Chart

When expedition confirms something:
- mark observation,
- update coastline/feature,
- record date/source,
- improve local confidence.

The chart becomes a record of the voyage.

# 23. Chart UI Style

Follow Art Bible:
historical working-material aesthetic + modern usability.

Possible visual elements:
- parchment/paper,
- ink lines,
- handwritten-style annotations used sparingly,
- compass rose,
- route marks,
- pins/marks.

[REQUIRED] Typography and interaction remain highly readable.

[DON'T] sacrifice usability for fake antique decoration.

# 24. Captain's Table Presentation

[PREFERRED] Chart mode can visually evoke the captain working at a table/chart rather than an abstract fullscreen strategy map.

Possible:
- chart,
- compass/dividers as visual motif,
- notes,
- navigator report.

This is presentation, not mandatory diegetic manipulation of every tool.

# 25. Map-to-World Transition

Player should move smoothly between:
- chart,
- ship overview,
- direct character control.

Alerts can focus relevant world location after leaving chart.

# 26. Weather Overlay

Chart can show only weather information the expedition knows/infers.

Examples:
- current local wind,
- observed storm direction,
- reported weather.

[DON'T] show omniscient satellite-style global weather.

# 27. Resource Projection

Route planning can estimate:
- travel time range,
- food use,
- water use,
- expected fatigue burden.

[REQUIRED] Present estimates/ranges, not guaranteed outcomes.

# 28. Risk Assessment

Navigator/officers may identify:
- known hazard,
- uncertain charting,
- weather concern,
- water shortage risk,
- ship-condition risk.

Advice is character knowledge, not omniscient UI truth.

# 29. Route Deviation

Actual ship movement can diverge from plan due to:
- wind,
- current,
- steering,
- storm,
- damage,
- human error.

Player may not immediately know exact deviation.

# 30. Landfall

Approaching destination should require increasing attention.

Possible flow:
1. estimated proximity,
2. reduced time compression,
3. lookouts intensified,
4. soundings/observations,
5. land sighting,
6. identify coast/landmark,
7. revise position,
8. choose safe approach.

[REQUIRED] Do not simply snap to destination when route progress reaches 100%.

# 31. Wrong Landfall

A navigationally plausible wrong landfall can occur.

This is not automatically failure.

Player must:
- identify location,
- reassess supplies,
- correct route,
- seek information,
- potentially exploit opportunity.

# 32. Other Vessels and Information

Encountered ships may provide:
- position claims,
- weather reports,
- coast information,
- harbor news,
- charts.

Their information can be wrong/outdated.

Store provenance.

# 33. Historical Knowledge

The game distinguishes:
- what modern developers know,
- what historical world knows,
- what expedition knows,
- what individual character believes.

[REQUIRED] Never leak developer omniscience into normal player navigation.

# 34. Difficulty Options

Potential accessibility settings:
- larger uncertainty hints,
- stronger navigator recommendations,
- automatic routine plotting,
- more frequent auto-pause,
- simplified chart tools.

[REQUIRED] Accessibility should reduce workload without necessarily granting impossible information.

# 35. Debug Navigation Mode

Developer tools may show:
- true position,
- true track,
- current vectors,
- error values,
- observation calculations.

[REQUIRED] Clearly separate debug information from player UI.

# 36. Navigation Event Log

Record:
- observations,
- position estimates,
- route changes,
- sightings,
- chart updates,
- navigator warnings,
- wrong-landfall corrections.

Useful for player chronicle and debugging.

# 37. Navigation Failure Should Be Legible

When player becomes lost, they should understand likely causes:
- no observation for X time,
- storm deviation,
- damaged instrument,
- inexperienced navigator,
- uncertain current.

Avoid:
"You are randomly lost."

# 38. Silent-Hunter-Style Rhythm

Desired play rhythm:

1. open chart,
2. review situation,
3. plot route,
4. issue standing orders,
5. accelerate,
6. alert interrupts,
7. return to ship/world,
8. solve problem,
9. gather new information,
10. update chart,
11. replot,
12. accelerate.

[REQUIRED] This rhythm must be enjoyable before adding large amounts of ocean content.

# 39. Navigation Vertical Slice

Implement:
- true vs estimated position,
- one chart,
- route legs,
- heading,
- basic wind/current deviation,
- one navigator,
- uncertainty growth,
- one observation method,
- land sighting,
- time compression,
- auto-pause,
- wrong-position possibility,
- chart update.

# 40. Four Validation Scenarios

## A — Leaving Known Coast
High confidence gradually decreases.

## B — Three Days of Routine Sailing
Plot, accelerate, consume resources, update estimates.

## C — Storm / Lost Confidence
Storm disrupts course and observations; uncertainty expands.

## D — Landfall
Player must determine whether sighted coast matches expectation.

# 41. Acceptance Criteria

- [ ] true and estimated position are separate,
- [ ] player cannot normally see true position,
- [ ] uncertainty grows causally,
- [ ] navigator skill affects estimate,
- [ ] route can be plotted,
- [ ] time acceleration works,
- [ ] critical alerts interrupt acceleration,
- [ ] observations reduce uncertainty,
- [ ] weather can block observations,
- [ ] actual movement can deviate from planned track,
- [ ] chart distinguishes known/reported/observed information,
- [ ] sightings can be annotated,
- [ ] resource projections are estimates,
- [ ] landfall requires identification/approach,
- [ ] save/load preserves navigation state,
- [ ] debug truth is separate from player UI.

# 42. Agent Implementation Order

1. true-position simulation,
2. estimated-position state,
3. chart rendering,
4. route-leg plotting,
5. ship heading integration,
6. uncertainty growth,
7. navigator skill,
8. observation action,
9. time compression,
10. interrupt framework,
11. chart annotations,
12. weather observation blocking,
13. land sighting,
14. wrong-landfall handling,
15. provenance/logging,
16. polish captain-table presentation.

# 43. Agent Guardrails

[REQUIRED] Navigation mechanics must be testable headlessly.

[REQUIRED] Use deterministic seeded tests.

[RESEARCH] Historical instruments and capabilities.

[APPROVAL REQUIRED] Exact level of manual plotting complexity.

[APPROVAL REQUIRED] Exact time-compression multipliers.

[DON'T] introduce modern GPS-like certainty.

[DON'T] require real-world professional navigation knowledge from the player.

[DON'T] copy *Silent Hunter* UI/assets; borrow the information-management rhythm, not protected presentation.

# 44. Final Design Test

Ask:

> When I open the chart, am I looking at what my expedition believes rather than what the game engine knows?

Ask:

> Can a skilled navigator make me safer without making navigation automatic?

Ask:

> Can I confidently plot a plan, accelerate time, be interrupted by new information, and meaningfully revise that plan?

Ask:

> Can a storm make me genuinely uncertain where I am without the game feeling like it cheated?

If yes, navigation is working.

**The chart should not tell the player where they are. It should show the best case the expedition can make for where they believe they are.**
