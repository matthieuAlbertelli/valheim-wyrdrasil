# Wyrdrasil Persistence and Restore Flow

This document explains the **current intended save/load flow** after the modular persistence refactors.

It exists to reduce regressions when persistence code moves between modules.

---

## Goals

The persistence flow should keep these responsibilities separate:

- **Core** defines the common persistence contracts.
- **Gameplay modules** own their own save section and participant.
- **Registry** orchestrates world save/load and runs Registry-specific restore hooks.
- **Restore hooks** rebuild runtime-only relationships that should not live inside serialized data.

---

## Current contracts

### Core

Shared contracts live in `Wyrdrasil.Core.Persistence`:

- `IWorldPersistenceParticipant`
- `IWorldPersistenceRestoreHook`
- `WorldPersistenceCoordinator`
- `WorldSaveEnvelope`
- `ModuleSaveSectionData`

### Module participants

Current module-owned persistence participants are expected to be:

- `SettlementsPersistenceParticipant`
- `SoulsPersistenceParticipant`
- `RoutinesPersistenceParticipant`
- `ConstructionPersistenceParticipant`

These participants should own:

- capture of module data
- restore of serialized module data
- deferred resolution retry logic specific to that module
- reset on world change

They should **not** own Registry gameplay reconstruction that depends on cross-module orchestration.

### Registry restore hooks

Registry restore hooks should own:

- resident post-restore normalization
- runtime rebinding that crosses module boundaries
- schedule refresh after restore
- Registry-specific integrity repair that happens after module restore

Current direction:

- `RegistryPersistenceService` orchestrates participants and hooks
- `RegistryResidentPersistenceRestoreHook` performs resident-specific restore reconciliation

---

## Save flow

### 1. Registry initiates the save

`RegistryPersistenceService.SaveWorldState()` drives the save.

### 2. Registry delegates capture to Core coordinator

`WorldPersistenceCoordinator.Capture(...)` iterates all registered `IWorldPersistenceParticipant` instances.

### 3. Each participant produces its own section

Each module participant contributes one save section to the `WorldSaveEnvelope`.

Expected ownership:

- Settlements -> settlement data
- Souls -> souls/resident identity data
- Routines -> schedule/runtime routine data
- Construction -> construction data

### 4. Registry serializes the final envelope

Registry writes the XML file, but it should **not** repack module internals manually.

---

## Restore flow

### 1. Registry reads the world save envelope

`RegistryPersistenceService` deserializes the world file into `WorldSaveEnvelope`.

### 2. Core coordinator dispatches restore to participants

`WorldPersistenceCoordinator.Restore(...)` routes each section to the owning participant.

### 3. Module participants restore their serialized state

Each participant restores only the state owned by its module.

At this stage, runtime-only relationships may still be incomplete.

### 4. Registry runs restore hooks

After module restore, Registry runs `IWorldPersistenceRestoreHook.OnAfterRestore()`.

This is where cross-module runtime reconstruction should happen, such as:

- restoring resident assignments against runtime settlement objects
- refreshing resident runtime presence
- refreshing schedules or clearing invalid occupations
- rebuilding post-load derived state that should not be serialized directly

### 5. Deferred resolutions may continue after initial restore

When a participant resolves something later, `RegistryPersistenceService.Update()` may call:

- `participant.RetryDeferredResolutions()`
- then `restoreHook.OnAfterDeferredResolutions()`

This is the correct place for follow-up integrity repair after delayed restoration.

---

## Design rules

### Rule 1

If data is **owned by a gameplay module**, the serialized section should be owned by that module participant.

### Rule 2

If logic is **cross-module and runtime-only**, it belongs in a restore hook or Registry orchestration layer.

### Rule 3

`RegistryPersistenceService` should stay an orchestrator.

It should not gradually regrow direct knowledge of:

- beds
- seats
- craft stations
- resident identity internals
- routine scheduling internals
- construction placement internals

### Rule 4

Adding a new persistent gameplay capability should usually mean:

1. update the owning module's save data model
2. update the owning module participant
3. update restore hooks only if runtime reconciliation is needed afterward

### Rule 5

Prefer module APIs over service leakage during restore.

If a restore hook needs behavior from `Settlements`, `Souls`, `Routines`, or `Construction`, it should prefer the module runtime/authoring API when possible.

---

## What to watch for during hardening

Warning signs that persistence architecture is regressing:

- a new `*PersistenceParticipant` appears inside `Wyrdrasil.Registry` for data owned by another module
- `RegistryPersistenceService` starts taking concrete services from feature modules again
- restore hooks begin to serialize or deserialize module internals themselves
- module bootstraps stop exposing their persistence participant explicitly
- save/load fixes are implemented in gameplay services instead of participant/hook boundaries

---

## Practical checklist before keeping a persistence refactor

- build the whole solution
- save a world with authored content
- reload the world
- verify assignments, routines, residents, and construction state
- run `Validate-ModuleBoundaries.ps1`
- run `Validate-PersistenceContracts.ps1`
- update this document if ownership changed
