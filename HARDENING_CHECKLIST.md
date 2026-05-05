# Wyrdrasil Hardening Checklist

This checklist exists to stabilize the refactored architecture before another large wave of feature work.

---

## 1. Build validation

Run a full solution build and confirm that all projects compile:

- Wyrdrasil.Core
- Wyrdrasil.Settlements
- Wyrdrasil.Souls
- Wyrdrasil.Routines
- Wyrdrasil.Construction
- Wyrdrasil.Registry

Record:
- build success/failure
- warnings introduced by the latest iteration
- any project that now depends on a new assembly reference

---

## 2. Plugin load validation

Confirm in Valheim/BepInEx:

- plugin loads successfully
- no startup exception in BepInEx log
- Registry mode can be toggled on/off
- HUD renders correctly
- no missing type/assembly errors at runtime

---

## 3. Manual gameplay smoke tests

### Registry mode
- toggle on
- toggle off
- confirm visuals appear/disappear correctly

### Settlements authoring
- create Tavern zone
- create Bedroom zone
- create waypoint
- connect waypoints
- create innkeeper slot
- designate seat
- designate bed
- designate craft station

### Souls / residents
- spawn test Viking
- register NPC
- assign innkeeper role
- assign seat
- assign bed
- respawn/despawn assigned resident

### Routines
- simulate noon
- simulate night
- clear time simulation
- verify resident route traversal still works
- verify seating / work occupation still works

### Construction
- capture blueprint
- spawn project
- assign target resident to project
- assign target craft station to project
- verify construction link visuals

---

## 4. Save / load validation

On a world containing authored data:

- save the world
- quit and reload
- verify zones are restored
- verify slots, seats, beds, craft stations, and waypoints are restored
- verify residents are restored
- verify resident assignments are restored
- verify routines recover correctly after load
- verify construction state is restored if present

Watch specifically for:
- missing visuals until mode toggle
- residents restored without assignments
- assignments restored but runtime binding missing
- duplicate markers after reload

---

## 5. Architecture validation

Run the architecture validation script:

- `Validate-ModuleBoundaries.ps1`
- `Validate-PersistenceContracts.ps1`

Check that new violations are either:
- intentional and documented
- or removed before merge

---

## 6. Safe merge criteria

A consolidation iteration is considered safe to keep when:

- full solution build passes
- no new startup exception appears
- Registry mode still works
- save/load smoke tests pass
- persistence contract validation passes
- no unexplained architecture-boundary violations are introduced

---

## 7. Do not regress

Avoid reintroducing:

- `RegistryContext`
- parallel legacy action pipelines
- direct service leakage from modules when a facade already exists
- giant composition logic inside `Plugin`
- restore logic embedded in the wrong module
