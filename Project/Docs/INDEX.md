# Ronin7 — Documentation Index

A map of the project's design, engineering, and narrative docs. Paths are relative to the repo root
(`D:\ronin7`). This is a discoverability index only — nothing is moved or renamed.

## Engineering & design

| Doc | What it covers |
|---|---|
| `Project/Docs/IMPROVEMENT-SUMMARY.md` | Audit/improvement pass: what shipped per phase, corrected audit claims, hygiene findings, the **handoff checklist**, and the EditMode test gate (639 tests, 0 skips; PlayMode 70/70). Also tracks the ongoing chapter-build studio pipeline. Read before touching combat, pooling, materials, or sun-nav. |
| `Project/Docs/SunNavigation-Design.md` | Design spec for the sun-based navigation mechanic (`SunCompass`/`SunGravityWell`/`SunGlare`) — gravity wells, glare, boost/heat interaction, and the integration hand-off. |
| `Project/Docs/GraphicsRoadmap-GrittyCyber.md` | "Gritty Cyber-Fantasy" graphics direction — VR pipeline settings, the lit-PBR + animated-neon shader family (`GrittyCyber`/`CyberSword`), VFX reuse, and the validated two-hero proof-of-concept. Read before adding shaders or re-skinning hero assets. |
| `Project/Docs/Ch01-Scene-Construction.md` | Scene-construction spec for Chapter 1's `Galaxy1_Ch1_Hub` — per-beat environment/background, NPC travel routes (Kessler's `NpcWalker` legs), the 17-step `MissionDirector` spine, lighting/VO/perf tables, all grounded in `Chapter1Builder.cs`. Read before patching the Ch1 hub scene. |
| `Project/Assets/Ronin7/SETUP_Phase0.md` | Initial project setup guide. |
| `Project/Assets/Ronin7/DISABLED-ENCOUNTERS.md` | Tracking doc for encounters currently disabled. |
| `Project/Assets/Ronin7/Docs/Phase5_Flight_Debug.md` | Flight debugging notes. |
| `Project/Assets/Ronin7/Docs/Phase6_Loop.md` | Core-loop notes. |
| `Project/Assets/Ronin7/Docs/Phase7_Hitbox_Debug.md` | Hitbox debugging notes. |

## Narrative

The campaign story lives in `story ouput/` (note: the folder name is misspelled — left as-is to
avoid breaking references). It also has an in-project copy under `Project/Assets/Ronin7/Docs/`.

| Doc | What it covers |
|---|---|
| `story ouput/00_STORY_BIBLE.md` | Narrative backbone / canon. |
| `story ouput/STORY_TIMELINE.md` | Chronology across chapters. |
| `story ouput/GAME_STORY_SO_FAR.md` | Running summary of story state. |
| `story ouput/00b_SWORD_AI_RECON.md` | The sword-AI character concept. |
| `story ouput/17_THE_HUB_Kesslers_Ship.md` | Hub location (Kessler's ship) writeup. |
| `story ouput/Ch01..Ch16_*.md` | Chapter prose + matching `*_Dialogue_Script.md` files. |
| `story ouput/README.md` | Narrative folder's own readme. |
| `Project/Assets/Ronin7/Docs/Ronin7_StoryBible.md` | In-project story bible (mirror of the narrative canon). |

> Note: the story bible exists in two places (`story ouput/00_STORY_BIBLE.md` and
> `Project/Assets/Ronin7/Docs/Ronin7_StoryBible.md`). Confirm which is authoritative before editing.
