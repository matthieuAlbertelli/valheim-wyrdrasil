# Wyrdrasil

> *A living-world and roleplay framework for Valheim.*

Wyrdrasil is a modular framework for transforming **Valheim** into a more immersive, systemic, and roleplay-driven world.

The long-term goal is to bring **living villages**, **functional NPCs**, **daily routines**, **dialogue systems**, **quests**, and **emergent world interactions** into the game — in a way that feels coherent, extensible, and native to Valheim’s atmosphere.

---

## Vision

Wyrdrasil is not meant to be a single isolated mod.

It is designed as a **modular ecosystem** that can grow into a complete roleplay and living-world layer for Valheim.

The project aims to support systems such as:

- persistent villagers and named NPCs
- assignable roles and occupations
- functional buildings and social spaces
- routines and schedules
- dialogue trees and branching conversations
- quest logic and world-state progression
- village-level and settlement-level simulation
- immersive world editing tools

The guiding idea is simple:

> **A village should not just exist visually — it should live.**

---

## Current Status

Wyrdrasil is currently in **early development**.

The first implemented module is:

- **Wyrdrasil.Registry**

This module is intended to provide the foundational in-world tooling used to define and manage:

- registered NPCs
- functional zones
- activity slots
- village logic anchors

Its primary in-game tool is the **Registry of Souls** — a mystical world-editing artifact used to reveal and structure the invisible social fabric of a settlement.

---

## Planned Modules

Wyrdrasil is structured as a suite of independent but connected modules.

Planned modules include:

- **Wyrdrasil.Core**  
  Shared systems, common data structures, service registration, and internal APIs.

- **Wyrdrasil.Registry**  
  The in-world management and editing layer used to define places, roles, and linked entities.

- **Wyrdrasil.Souls**  
  NPC identity, registration, roles, and persistent soul-linked entities.

- **Wyrdrasil.Settlements**  
  Villages, buildings, functional zones, and settlement structure.

- **Wyrdrasil.Routines**  
  Daily schedules, social behavior, work cycles, and location-based activities.

- **Wyrdrasil.Dialogues**  
  Branching dialogue systems, conditions, responses, and interaction states.

- **Wyrdrasil.Quests**  
  Quest definitions, progression logic, triggers, and world-state consequences.

Additional modules may be added later depending on the direction of the framework.

---

## Design Principles

Wyrdrasil is being built around a few core principles:

### 1. **Modularity**
Each major system should be isolated enough to evolve independently without turning the project into an unmaintainable monolith.

### 2. **Diegetic Tooling**
Whenever possible, systems should be represented through in-game tools and interactions rather than detached admin-style interfaces.

### 3. **World Coherence**
A location should not be “special” because of hardcoded behavior alone — it should become meaningful through explicit world structure and relationships.

### 4. **Extensibility**
Everything should be designed with future systems in mind: dialogue, quests, factions, relationships, memory, and beyond.

### 5. **Valheim First**
Even ambitious systems should still feel like they belong inside Valheim’s tone and gameplay language.

---

## Repository Structure

This repository is intended to host the full Wyrdrasil suite.

Example structure:

    Wyrdrasil/
    ├── Wyrdrasil.Registry/
    ├── Wyrdrasil.Core/
    ├── Wyrdrasil.Souls/
    ├── Wyrdrasil.Settlements/
    ├── Wyrdrasil.Routines/
    ├── Wyrdrasil.Dialogues/
    └── Wyrdrasil.Quests/

At the moment, development is focused on the first playable vertical slice through **Wyrdrasil.Registry**.

---

## Development Notes

Wyrdrasil is currently developed as a **BepInEx plugin suite for Valheim**.

The project is still in active prototyping and foundational architecture work.

This means:

- systems may evolve rapidly
- APIs are not stable yet
- data structures are still being refined
- implementation details may change significantly during early development

---

## Long-Term Goal

The long-term ambition of Wyrdrasil is to make Valheim feel less like a static survival sandbox and more like a world that can support:

- villages with purpose
- characters with routines
- places with meaning
- stories that emerge naturally

Not by replacing Valheim —  
but by giving its world **memory, structure, and life**.

---

## License

TBD
