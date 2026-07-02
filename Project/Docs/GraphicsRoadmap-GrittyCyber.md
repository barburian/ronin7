# Ronin7 — "Gritty Cyber-Fantasy" Graphics Roadmap

Target style: a hybrid of **Sairento VR** (cyberpunk neon, high-contrast, kinetic VFX) and
**Blade & Sorcery** (gritty realistic PBR, grounded materials, visceral combat). VR constraints are
non-negotiable: **90 FPS floor (11.11 ms)**, **Single-Pass-Instanced** stereo, **no camera shake**,
fake/HDR bloom over heavy screen-space post, low shader instruction counts, minimal VFX overdraw.

This document is the standing direction. The first hero proof-of-concept (below) is already built and
validated in-editor.

---

## 1. Rendering pipeline — keep the current config (it is already VR-correct)

The project ships two URP assets driven per platform; the roadmap **validates and documents** them
rather than changing them:

| | PC (`Settings/PC_RPAsset`) | Quest (`Settings/Mobile_RPAsset`) |
|---|---|---|
| Rendering path | Forward | Forward |
| MSAA | 4× | 4× |
| HDR | On | On |
| Render scale | 1.0 | 0.8 |
| Shadows | 4 cascade / 2048 / soft | 1 cascade / 1024 / hard |
| SSAO | On (`PC_Renderer`) | Off |
| Depth/Opaque tex | — | Off (bandwidth) |

- **Stay on Forward.** Forward+ and Deferred are net-negative for this scene density in stereo.
- **Fake bloom only.** All glow is HDR emission routed through the existing Bloom threshold
  (`ArtDirectionSpec.BloomThreshold = 0.9`). No new full-screen passes. `Flow/GraphicsDirector.cs`
  already scales bloom per tier at runtime (Low 0.35× / High 0.9×) and gates blade lights + particle
  scale, and `Flow/QualityBootstrap.cs` sets the 72 Hz target, fixed-foveation 2, and a 25 m shadow clamp.
- **Grade is owned by `Editor/Art/NeonPostFx.cs`** (ACES tonemap + bloom + cool filter + split-toning
  cyan-shadows/magenta-highlights). Extend that, do not add parallel volumes.

Every new shader must mirror the SPI boilerplate already in `Art/Shaders/CyberNeon.shader`
(`UNITY_VERTEX_INPUT_INSTANCE_ID` / `UNITY_VERTEX_OUTPUT_STEREO` /
`UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX`).

---

## 2. Shader architecture — one new lit+emissive family, everything else reused

The gap in the old stack: glow and lit-PBR lived in **two separate materials** (a `SamuraiToon` cel
base + a `CyberNeon`/Plasma accent). The new shaders fuse **real PBR + animated neon in a single pass**.

| Role | Shader | Status |
|---|---|---|
| Pure neon accents (signs, holo, seams) | `Ronin7/CyberNeon` | reuse |
| Stylized cel body (world / non-hero) | `Ronin7/SamuraiToon` | reuse |
| **Gritty PBR metal/leather + animated neon edge (hero)** | **`Ronin7/GrittyCyber`** | **new** |
| **Cyber-Sword (metal blade + animated edge)** | **`Ronin7/CyberSword`** | **new** |
| Silhouette outline | `Ronin7/SamuraiOutline` | reuse (material slot [1]) |

**`Ronin7/GrittyCyber`** (`Art/Shaders/GrittyCyber.shader`) — hand-authored URP **Lit**:
real PBR via `UniversalFragmentPBR` (`_BaseMap`/`_BaseColor`, `_MetallicGlossMap`+`_Metallic`/`_Smoothness`,
`_BumpMap`, `_OcclusionMap`), **plus** an emissive edge = `_EmissionMap` mask **or** fresnel rim, scrolled
by `_EdgeSpeed` and optionally pulsed (`_EdgePulse`), tinted by HDR `_EmissionColor`. Passes:
`UniversalForward` + `ShadowCaster` + `DepthNormals` (so PC SSAO sees it). Dynamic GI via SH (hero rigs
aren't lightmapped). Non-emissive parts cost nothing extra — `_EmissionColor` defaults to black.

**Design rule for believability:** metallic is opt-in. Parts default to **non-metal** (skin, cloth,
leather at low smoothness); only an explicit armor allowlist (plate/pauldron/vambrace/greave/helmet/
guard/kashira/…) becomes metal. This keeps skin and leather from looking chromed.

---

## 3. The Cyber-Sword shader (deliverable)

`Ronin7/CyberSword` (`Art/Shaders/CyberSword.shader`) — a `GrittyCyber` sibling tuned for a thin,
high-gloss blade. Lit metallic steel (`_Metallic 1`, `_Smoothness ~0.9`) with a glowing energy band
that runs **along** the blade length (UV.y), scrolling with `_EdgeSpeed`, confined to the cutting edge
by `_EdgeWidth`, reinforced by a sharpened fresnel rim and an optional pulse. Emission is HDR
(default `(0,4,6)` and the material uses `NeonPalette.Cyan = (0.15,0.85,1)×2.5`) so it pushes through
Bloom instead of a screen-space glow. Same three passes (Forward/ShadowCaster/DepthNormals), SPI-correct.

It **pairs with existing systems** — no new VFX code:
- Weapon trail: `Editor/Art/ArtPrefabBuilder.Plasma.cs::AddBladeTrail()` (TrailRenderer on the blade tip).
- Sparks / deflect / hit / death bursts: `Audio/Vfx/CombatVfxController.cs` → `Audio/Vfx/VfxPool.cs`
  driven by `Combat/BladeDamager.cs`'s `SwordImpact(point, speed, victim)` event (speed already
  EMA-smoothed; scale spark counts by it).
- Per-instance tint without breaking SRP batching: `Core/RendererTint.cs::Apply(Renderer, Color)`.

---

## 4. VFX — reuse now, one documented gap

Reuse the Quest-budgeted prefab set built by `Editor/Art/VfxPrefabBuilder.cs` (≤30 particles, ≤0.4 s,
no collision/lights/shadows/trails) and the `VfxPool` ring buffer. **Gap for a later pass (not built
here):** projected **impact decals** (blood / structural scoring). Recommended as a pooled world-space
quad-decal system modeled on `Ship/ProjectilePool.cs` (Queue/List, `useUnscaledTime`, hard Quest cap),
hooked to `SwordImpact` / `EntityDamaged` — **not** URP's Decal Renderer Feature (it adds a render pass).

---

## 5. Hero proof-of-concept — built & validated

Applied the treatment to the two hero characters + the sword via the Unity MCP, then ran a full
self-validation gate before sign-off.

- **New armored Ronin-7.** The protagonist already existed unmasked as `Soren`
  (`Generated/Soren.prefab`; "Ronin-7" is his designation, "Soren" his name). A NEW *masked armored
  operative* form was authored as a data-driven spec `Data/CharacterSpecs/Decorative/Ronin7.json`
  (kabuto helmet + glowing visor slit, pauldrons, layered chestplate with a cyan chest-seam, vambraces,
  greaves, tattered coat tail) and baked by the existing `ArtPrefabBuilder.BuildCharacterFromSpec`
  pipeline → `Prefabs/Art/Generated/Ronin7.prefab` (20 parts). Hero cyan accents at `EmissionHero 2.5×`.
- **Kessler** (`Generated/Kessler.prefab`) re-skinned to grounded worn leather/cloth (matte, non-metal
  skin preserved) with dim **amber** temple/eye augments at `EmissionDim 1.0×`.
- **Sword** (`Prefabs/Art/Sword_Katana.prefab`): blade → `CyberSword` (cyan energy edge); guard/pommel →
  `GrittyCyber` metal; handle → leather. Outlines preserved on every renderer (material slot [1] intact).
- New materials live in `Art/Materials/HeroVariants/` (asset-backed, keyed by color+profile+emission so
  they're rebuild-safe and never mutate the shared cross-character variants).

### Validation gate (all green)
| Gate | Check | Result |
|---|---|---|
| A | Both shaders compile (`IsSupported`, no errors), 3 passes each | ✅ |
| B | Isolated renders of Ronin-7 / Kessler / Cyber-Sword | ✅ PBR metal + HDR neon, no error shading |
| C | Console clean after build/apply/render | ✅ (no project errors) |
| D | EditMode suite | ✅ **413/413** (baseline 400) |

---

## 6. Next steps (not yet done)
- In-headset tuning of edge `_EdgeSpeed`/`_EdgePulse`/emission strength and bloom on Quest vs PCVR.
- Author real albedo/normal/metallic-smoothness textures for hero armor (POC uses flat tints + uniform
  per-profile metallic; the shaders already sample all maps).
- Build the pooled impact-decal system (§4).
- Optionally promote `GrittyCyber` to enemy elites; keep the wider world on `SamuraiToon`/`CyberNeon`.

## Files
- Shaders: `Project/Assets/Ronin7/Art/Shaders/{GrittyCyber,CyberSword}.shader`
- Spec + prefab: `Data/CharacterSpecs/Decorative/Ronin7.json` → `Prefabs/Art/Generated/Ronin7.prefab`
- Materials: `Project/Assets/Ronin7/Art/Materials/HeroVariants/`
- Reused: `CyberNeon.shader`, `SamuraiToon.shader`, `NeonPostFx.cs`, `GraphicsDirector.cs`,
  `QualityBootstrap.cs`, `RendererTint.cs`, `CombatVfxController.cs`, `VfxPool.cs`,
  `ArtPrefabBuilder.Plasma.cs`, `ArtDirectionSpec.cs`, `NeonPalette.cs`
