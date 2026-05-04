# Wyrdrasil Architecture Notes

This document captures the **current intended module boundaries** after the refactor and hardening passes.

It is not a lore document and it is not a player-facing document.
It exists to help future refactors stay coherent.

---

## Current module roles

### Wyrdrasil.Core
Owns:
- shared persistence contracts
- shared tool data types
- cross-module value objects
- generic services that do not belong to one gameplay module

Should not know:
- Registry implementation details
- Settlements implementation details
- Souls implementation details
- Routines implementation details
- Construction implementation details

### Wyrdrasil.Registry
Owns:
- in-world editing workflow
- Registry HUD and mode routing
- action selection and action execution
- runtime orchestration across modules
- restore orchestration specific to Registry gameplay flows

Should prefer consuming:
- `*.Authoring` APIs
- `*.Runtime` APIs
- module bootstraps

Should avoid depending directly on:
- `Wyrdrasil.Settlements.Services.*`
- `Wyrdrasil.Souls.Services.*`
- `Wyrdrasil.Routines.Services.*`

Direct service references are only tolerated when they are still needed for composition or when no module facade exists yet.

### Wyrdrasil.Settlements
Owns:
- zones
- slots
- beds
- seats
- craft stations
- waypoints
- settlement runtime queries
- settlement authoring queries
- settlement persistence participant

Public surfaces:
- `ISettlementsAuthoringApi`
- `ISettlementsRuntimeApi`
- `SettlementsModuleBootstrap`

### Wyrdrasil.Souls
Owns:
- resident identity
- resident catalog
- resident spawn/runtime binding
- soul persistence participant

Public surfaces:
- `ISoulsAuthoringApi`
- `ISoulsRuntimeApi`
- `SoulsModuleBootstrap`

### Wyrdrasil.Routines
Owns:
- world clock
- schedule evaluation
- occupation resolution
- routine runtime state
- routines persistence participant

Public surfaces:
- `IRoutinesRuntimeApi`
- `RoutinesModuleBootstrap`

### Wyrdrasil.Construction
Owns:
- blueprint capture
- project runtime
- placement preview
- progress and completion
- construction persistence participant

Public surfaces:
- `IConstructionAuthoringApi`
- `IConstructionRuntimeApi`
- `IConstructionTestingApi`
- `ConstructionModuleBootstrap`

---

## Composition roots

Current composition roots are expected to be:

- `Plugin` for BepInEx hosting only
- `RegistryModuleBootstrap` for Registry module composition
- module bootstraps inside each owned module:
  - `SettlementsModuleBootstrap`
  - `SoulsModuleBootstrap`
  - `RoutinesModuleBootstrap`
  - `ConstructionModuleBootstrap`

### Rule
If a module can compose its own runtime, persistence participant, and public APIs internally, that composition should live **inside that module**, not in Registry.

---

## Registry runtime layering

The intended layering inside `Wyrdrasil.Registry` is:

1. **Plugin host**
2. **Registry module bootstrap/runtime**
3. **Tool controller and interaction mode routing**
4. **Action registry and executable actions**
5. **Runtime services and restore hooks**
6. **Module facades from Settlements / Souls / Routines / Construction**

The normal action path should be:

`Tool selection -> Action registry -> IRegistryExecutableAction -> module APIs/services -> feedback/HUD`

The old `RegistryContext` and legacy action pipeline should not be reintroduced.

---

## Persistence rules

Each gameplay module should own its own save section and participant when possible.

Current direction:
- `Settlements` owns settlement persistence
- `Souls` owns soul persistence
- `Routines` owns routines persistence
- `Construction` owns construction persistence
- `Registry` orchestrates world save/load and any Registry-specific post-restore hooks

### Restore hooks
Restore hooks are the right place for:
- re-linking runtime-only state
- refreshing resident presence after restore
- schedule refresh / occupation cleanup after restore
- Registry-specific integrity checks

Restore hooks are **not** the right place for embedding module persistence logic that belongs inside the module itself.

---

## Current tolerated debt

The architecture is cleaner than before, but some debt is still tolerated:

- a few `Registry` services still reference concrete services from other modules
- bootstrap composition bundles still exist for internal wiring convenience
- some public APIs are still broader than ideal because they are carrying migration debt

These are acceptable only when they are clearly transitional.

---

## Future direction

The next safe improvements should prefer:

1. reducing remaining direct `*.Services` dependencies in Registry
2. shrinking composition bundles behind narrower facades
3. making more internal services actually `internal`
4. adding tests for persistence round-trips and restore invariants
5. keeping new features on top of the module APIs rather than bypassing them

---

## Practical rule of thumb

Before adding a new dependency, ask:

- Does this belong in the module that owns the data?
- Can this be expressed through an existing Runtime or Authoring API?
- Am I adding a real capability, or leaking a service just because it is convenient?

If the answer is convenience only, the dependency should probably not be added.
