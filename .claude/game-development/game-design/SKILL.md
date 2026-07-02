---
name: game-design
description: "Game design principles. GDD structure, balancing, player psychology, progression."
risk: unknown
source: community
date_added: "2026-02-27"
---

# Game Design Principles

> Design thinking for engaging games.

---

## 1. Core Loop Design

### The 30-Second Test

```
Every game needs a fun 30-second loop:
1. ACTION → Player does something
2. FEEDBACK → Game responds
3. REWARD → Player feels good
4. REPEAT
```

### Loop Examples

| Genre | Core Loop |
|-------|-----------|
| Platformer | Run → Jump → Land → Collect |
| Shooter | Aim → Shoot → Kill → Loot |
| Puzzle | Observe → Think → Solve → Advance |
| RPG | Explore → Fight → Level → Gear |

---

## 2. Game Design Document (GDD)

### Essential Sections

| Section | Content |
|---------|---------|
| **Pitch** | One-sentence description |
| **Core Loop** | 30-second gameplay |
| **Mechanics** | How systems work |
| **Progression** | How player advances |
| **Art Style** | Visual direction |
| **Audio** | Sound direction |

### Principles

- Keep it living (update regularly)
- Visuals help communicate
- Less is more (start small)

---

## 3. Player Psychology

### Motivation Types

| Type | Driven By |
|------|-----------|
| **Achiever** | Goals, completion |
| **Explorer** | Discovery, secrets |
| **Socializer** | Interaction, community |
| **Killer** | Competition, dominance |

### Reward Schedules

| Schedule | Effect | Use |
|----------|--------|-----|
| **Fixed** | Predictable | Milestone rewards |
| **Variable** | Addictive | Loot drops |
| **Ratio** | Effort-based | Grind games |

---

## 4. Difficulty Balancing

### Flow State

```
Too Hard → Frustration → Quit
Too Easy → Boredom → Quit
Just Right → Flow → Engagement
```

### Balancing Strategies

| Strategy | How |
|----------|-----|
| **Dynamic** | Adjust to player skill |
| **Selection** | Let player choose |
| **Accessibility** | Options for all |

---

## 5. Progression Design

### Progression Types

| Type | Example |
|------|---------|
| **Skill** | Player gets better |
| **Power** | Character gets stronger |
| **Content** | New areas unlock |
| **Story** | Narrative advances |

### Pacing Principles

- Early wins (hook quickly)
- Gradually increase challenge
- Rest beats between intensity
- Meaningful choices

---

## 6. Anti-Patterns

| ❌ Don't | ✅ Do |
|----------|-------|
| Design in isolation | Playtest constantly |
| Polish before fun | Prototype first |
| Force one way to play | Allow player expression |
| Punish excessively | Reward progress |

---

> **Remember:** Fun is discovered through iteration, not designed on paper.

---

## Ronin7 specifics

- **Structure:** linear campaign across episodes `Galaxy1_EP01..EP06`, each split into multiple
  scenes (Ship / Hideout / Planet / Docking / Core / EngineRoom / ArchiveRing / etc.). Narrative
  backbone is `story ouput/00_STORY_BIBLE.md`.
- **Hub caveat:** `Galaxy1_EP01_Ship` doubles as the hub *and* the EP01 combat intro — idling there
  is a game-over state, not a safe menu.
- **Encounters:** `Ship/SpaceEncounterManager.cs` builds waves via `ComposeWave` (pool-safe clamp +
  data-driven elite variety) — tune encounters there, it's unit-tested.
- **Melee model:** `Combat/BladeDamager.cs` uses EMA-smoothed swing speed (damage saturates ~6 m/s)
  so frame spikes can't inflate hits — balance against `speedSmoothing`.
- **Sun-nav mechanic:** `Ship/SunNavigation/` (`SunCompass`, `SunGravityWell`, `SunGlare`) is a
  navigation/strategy layer — additive and off by default; see `Project/Docs/SunNavigation-Design.md`.

## When to Use
This skill is applicable to execute the workflow or actions described in the overview.

## Limitations
- Use this skill only when the task clearly matches the scope described above.
- Do not treat the output as a substitute for environment-specific validation, testing, or expert review.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
