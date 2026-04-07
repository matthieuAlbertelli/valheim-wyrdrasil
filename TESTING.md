# Wyrdrasil.Registry — In-Game Testing Guide

This document is a practical test checklist for **Wyrdrasil.Registry**.
It is intentionally separate from the main `README.md`, which should stay focused on project overview, architecture, and setup.

---

## Purpose

Use this guide to validate that the current in-game vertical slice works correctly inside Valheim.

At the current stage, the main goals are to verify:

- Registry Mode activation and HUD behavior
- Tavern zone creation
- Navigation waypoint creation and linking
- Innkeeper slot creation
- Seat designation from world furniture
- Test Viking spawning
- Randomized Viking visual identity
- NPC registration
- Role assignment
- Seat assignment
- Route traversal through waypoint graphs
- Chair interaction and seating behavior
- Deletion flows
- Basic regression checks after changes

---

## Preconditions

Before testing in game, make sure the following are true:

- The plugin compiles successfully
- The DLL is copied into the Valheim `BepInEx/plugins` folder
- The plugin loads without startup exceptions
- Valheim starts normally with BepInEx enabled
- You are testing in a local world where you can easily place and inspect objects
- You have at least one tavern-like building or test structure available

Recommended test setup:

- a simple wooden building
- at least one chair, stool, or bench
- enough open space around the building to observe movement
- a short route outside and inside the building for waypoint testing

---

## Core Testing Flow

The best order is:

1. Enter Registry Mode
2. Create a Tavern zone
3. Create navigation waypoints
4. Link waypoints into a path
5. Create an innkeeper slot
6. Designate one or more seats
7. Spawn test Vikings
8. Verify visual randomization
9. Register a spawned Viking
10. Assign Innkeeper role
11. Register another Viking
12. Assign a designated seat
13. Observe movement and chair interaction
14. Test deletions and fallback cases

---

## 1. Registry Mode

### What to test

- Toggle Registry Mode on
- Toggle Registry Mode off
- Confirm the HUD appears only while Registry Mode is active
- Confirm zone markers / slot markers / waypoint markers only appear in Registry Mode

### Expected result

When Registry Mode is enabled:

- the Registry HUD appears
- interactive registry visuals become visible
- the game remains responsive

When Registry Mode is disabled:

- registry visuals disappear
- the HUD disappears
- normal gameplay view is restored

### Regression signs

- HUD does not appear
- HUD remains visible after disabling mode
- markers stay visible outside Registry Mode
- activation causes errors in the log

---

## 2. Tavern Zone Creation

### What to test

- Select the action to create a Tavern zone
- Place a zone in the world
- Place multiple Tavern zones
- Verify deletion of a zone later in the flow

### Expected result

- a Tavern zone is created at the targeted placement point
- zone markers are visible in Registry Mode
- zone count in the HUD increases
- logs confirm the created zone with its identifier and position

### Regression signs

- zone is not created
- zone is created at the wrong position
- zone marker does not appear
- deletion leaves ghost visuals behind

---

## 3. Navigation Waypoints

### What to test

- Create several navigation waypoints
- Create them both outside and inside the tavern
- Make sure they form a sensible walking route

Recommended minimal test path:

- one waypoint outside the tavern
- one at the entrance
- one inside near the innkeeper position
- one near a designated seat

### Expected result

- each waypoint gets a unique ID
- waypoints are visible in Registry Mode
- waypoint count in the HUD increases
- logs confirm creation

### Regression signs

- no marker appears
- IDs behave strangely
- duplicate waypoints overlap unexpectedly
- waypoint visuals remain after deletion

---

## 4. Waypoint Linking

### What to test

- Select a start waypoint
- Select a second waypoint to create a link
- Continue until a full path exists
- Try linking already linked waypoints
- Try canceling a link selection by selecting the same waypoint again

### Expected result

- links are created only between valid waypoint pairs
- the pending start waypoint is shown in the HUD
- link lines are visible in Registry Mode
- the system does not create duplicate links

### Regression signs

- links do not appear visually
- link selection gets stuck
- duplicate lines stack on top of each other
- pathfinding later ignores created links

---

## 5. Innkeeper Slot

### What to test

- Create an innkeeper slot inside the Tavern zone
- Create it near the intended serving area

### Expected result
n
- the slot is created successfully
- slot markers appear in Registry Mode
- logs confirm slot creation

### Regression signs

- slot cannot be created inside a valid tavern
- slot appears outside expected placement
- slot count or visuals become inconsistent

---

## 6. Seat Designation

### What to test

- Aim at a chair, stool, or bench and designate it as a registered seat
- Designate multiple seats
- Try designating the same furniture twice
- Try designating furniture outside the Tavern zone

### Expected result

- valid seat furniture becomes a registered seat
- duplicate registration is rejected
- seats outside Tavern zones are rejected
- seat markers appear in Registry Mode

### Regression signs

- arbitrary furniture is accepted when it should not be
- seat approach points are invalid
- registered seat visuals are wrong or missing

---

## 7. Spawn Test Viking

### What to test

- Spawn multiple test Vikings
- Spawn them in different locations
- Watch for runtime errors during spawn

### Expected result

- Vikings spawn in front of the player
- they remain in the world
- no local player destruction occurs
- logs confirm spawn position, seed, role, sex, hair, beard, etc.

### Regression signs

- spawned NPC appears briefly then disappears
- local player gets destroyed or replaced
- repeated `Destroying old local player` logs appear
- spawn produces exceptions

---

## 8. Visual Identity Randomization

### What to test

Spawn a series of Vikings and inspect variation in:

- male / female body model
- hair style
- beard presence / beard style
- hair / beard color
- armor / clothing visuals

### Expected result

Across multiple spawned NPCs, you should observe variation.

Specifically:

- some NPCs are female
- some NPCs are male
- hairstyles vary
- beards vary for male NPCs
- hair color is not always white
- equipment loadouts vary according to catalog generation

### Regression signs

- everyone looks identical
- all hair remains white
- no women appear
- all NPCs spawn naked
- default player gear leaks in (for example torch-only visuals)

---

## 9. NPC Registration

### What to test

- Aim at a spawned Viking and register it
- Register multiple different Vikings
- Try registering the same NPC twice
- Try registering the local player

### Expected result

- spawned Viking becomes a registered resident
- duplicate registration is rejected
- local player cannot be registered
- resident count in the HUD updates
- registered NPC markers appear in Registry Mode

### Regression signs

- wrong character gets registered
- duplicate registration succeeds
- local player is accepted incorrectly

---

## 10. Assign Innkeeper Role

### What to test

- Aim at a registered Viking
- assign the Innkeeper role
- verify behavior when no free innkeeper slot exists

### Expected result

- the resident receives role `Innkeeper`
- the resident is assigned to the innkeeper slot
- navigation starts toward the assigned position
- logs confirm assignment

### Regression signs

- role changes but movement never starts
- resident moves to the wrong place
- duplicate slot assignments occur

---

## 11. Assign Seat

### What to test

- register another Viking
- assign a designated seat to that resident
- repeat with multiple seats if available

### Expected result

- the resident receives the seat assignment
- movement begins toward the seat approach point
- if a waypoint route exists, it is used
- if no route exists, the direct fallback path is used

### Regression signs

- seat assignment succeeds but no movement happens
- resident goes to wrong seat
- seat assignment is allowed for invalid target

---

## 12. Route Traversal

### What to test

Observe an assigned resident moving along a linked waypoint graph.

Check:

- movement from spawn area to first waypoint
- movement across intermediate waypoints
- movement into tavern interior
- movement toward slot or seat target

### Expected result

- path progression is stable
- no constant oscillation
- no endless circling around a node
- waypoint chain is followed in plausible order
- direct fallback occurs only when graph routing is unavailable

### Regression signs

- violent oscillation
- waypoint skipping that breaks the route
- infinite loops
- NPC freezes between two nodes

---

## 13. Chair Interaction / Seating

### What to test

- assign a resident to a designated seat
- observe final approach to the chair
- confirm actual chair interaction
- confirm the seated state is stable

### Expected result

- NPC reaches the seat approach point
- chair interaction occurs
- NPC becomes attached to the chair
- seated animation / seated transform remains stable

### Regression signs

- NPC reaches chair but never sits
- NPC sits then immediately detaches
- NPC clips into chair or rotates incorrectly
- seated state breaks route controller logic

---

## 14. Deletion Flows

### What to test

- delete a waypoint
- delete a linked waypoint
- delete a designated seat
- delete an innkeeper slot
- delete a zone containing sub-elements

### Expected result

- visual markers disappear
- internal assignments are cleared when needed
- no ghost lines / ghost markers remain
- dependent residents lose invalid assignments safely

### Regression signs

- deleted content leaves visuals behind
- dependent resident keeps impossible assignment
- internal state counts become inconsistent

---

## 15. Diagnostics Action

### What to test

Use the diagnostic action on a target NPC AI.

### Expected result

- useful AI-related reflection output is logged
- no crash occurs
- diagnostics can help inspect the runtime composition of the target

### Regression signs

- diagnostics crash the session
- no useful information appears
- unrelated targets produce invalid output

---

## Recommended Regression Checklist After Any Change

After modifying spawn, visuals, routes, chairs, or identity systems, rerun this minimum regression pass:

1. Enable Registry Mode
2. Create Tavern zone
3. Create and link 3 to 5 waypoints
4. Create innkeeper slot
5. Designate 1 to 3 seats
6. Spawn 5 test Vikings
7. Confirm male/female variation and non-white hair colors
8. Register one Viking and assign Innkeeper role
9. Register another Viking and assign seat
10. Confirm route traversal and successful seating
11. Delete one seat and confirm assignment cleanup
12. Delete one waypoint and confirm visuals/links cleanup

---

## Log Signals Worth Watching

Good signs:

- plugin loaded successfully
- registry mode enabled / disabled
- created Tavern zone
- created navigation waypoint
- connected waypoint A <-> B
- designated seat
- spawned player-derived registry viking
- assigned innkeeper role
- assigned designated seat
- route applied with waypoint count
- seat interaction success

Bad signs:

- local player destroyed
- cannot spawn test NPC
- customization failed
- missing prefab / missing component
- no connected waypoint route found when a valid graph should exist
- chair interaction repeatedly failing
- route never completes

---

## Current Practical Goal of the Vertical Slice

The current ideal demo scenario is:

- activate Registry Mode
- create a Tavern zone
- place an innkeeper slot
- designate multiple seats
- create a waypoint route entering the tavern
- spawn randomized Vikings
- register one as innkeeper
- register another as a seated villager
- watch them move to their assigned places and sit correctly

If this scenario works reliably, the current vertical slice is in good shape.

---

## Next-Step Testing Focus

Once the current slice is stable, the next testing target should be:

- persistent identity for registered residents
- identity replay after respawn / runtime reconstruction
- later, persistence into saved world data

