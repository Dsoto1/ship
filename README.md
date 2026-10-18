# The Expedition

A single-player isometric historical survival, expedition and settlement game set around
1500–1520. A persistent captain recruits a crew in a European port, sails for the Americas,
and survives what follows: a storm, a shipwreck, landfall and settlement. The same crew is
carried through every phase.

**Status:** P0 (simulation foundation). Characters, tasks, time, needs, provisions, inventories
and save/load are implemented and tested headless. Nothing is playable yet; the first Godot
scene is next.

## Build and test

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build
dotnet test
```

## Docs

- [`docs/GAME_VISION.md`](docs/GAME_VISION.md) — vision, design pillars, roadmap (source of truth)
- [`docs/TECHNICAL_DESIGN.md`](docs/TECHNICAL_DESIGN.md) — proposed architecture and invariants
- [`docs/MILESTONES.md`](docs/MILESTONES.md) — ticket plan and status
- [`docs/DECISIONS.md`](docs/DECISIONS.md) — decision log

## Tech

Godot 4 (.NET) for presentation, with a Godot-independent C# simulation library tested with
xUnit. Requires the .NET 8 SDK; the Godot 4.x .NET editor is needed only for scene work.
