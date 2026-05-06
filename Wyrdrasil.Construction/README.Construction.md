# Wyrdrasil.Construction

`Wyrdrasil.Construction` manages blueprint capture, chantier creation, PNJ-driven work contribution, safe piece placement, Registry ghosts, and integrity repair.

## Construction model

The blueprint remains the intended building. The world represents the current state.

A construction project now tracks every blueprint piece individually:

- `Pending`: planned but not yet evaluated as buildable.
- `Buildable`: safe to build from the current structural frontier.
- `Reserved`: reserved for a future worker-specific task.
- `Built`: placed in the world and linked to a world object name.
- `Missing`: was built before, but is now absent from the world and must be rebuilt.
- `Blocked`: not safe to build yet.

## Safe build order

Construction is no longer linear by capture order. The project asks for the next safe piece:

1. Identify already built pieces.
2. Build grounded/root pieces first.
3. Expand only to pieces connected to the built frontier.
4. Probe candidate stability through `ConstructionPieceStabilityProbeService`.
5. Build only `Grounded`, `Strong`, or `Acceptable` candidates.
6. Block the project if no pending piece is safe.

This creates a foundation for Valheim-native stability probing without coupling the chantier runtime to hammer internals.

## Registry ghost view

When Registry mode is active, active projects reveal ghosts for pieces that are not built yet.

Current color semantics:

- Blue: grounded/root candidate.
- Green: strongly supported candidate.
- Yellow-green: acceptable frontier candidate.
- Orange: missing piece queued for repair.
- Red: blocked/unsupported.
- Grey: pending or not evaluated yet.

Built pieces are not ghosted, to avoid visually duplicating real construction.

## Damage and repair

Completed projects are intentionally retained. They are no longer pruned automatically, because Wyrdrasil needs them for long-term repair logic.

`ConstructionProjectIntegrityService` periodically checks built piece links. If a built piece disappears from the world:

1. The piece becomes `Missing`.
2. Project state becomes `Damaged`.
3. The piece returns to the construction queue.
4. Registry mode shows its ghost again.
5. Assigned workers can rebuild it when it becomes safe.

## Current limitation

`ConstructionPieceStabilityProbeService` is conservative and native-aware, but does not yet call Valheim's exact hammer ghost support-color pipeline directly. The API boundary is now present so a future `WearNTear`/hammer-preview probe can replace the heuristic without changing project progress, PNJ work, or Registry ghost code.
