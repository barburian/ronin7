# Art Direction Spec

**Single source of truth for visual axes:** scale grammar, emission quantization, post-FX grade.

**C# source:** `Assets/Ronin7/Scripts/Editor/Art/ArtDirectionSpec.cs`

**Palette colors:** centralized in `NeonPalette` — referenced, not re-declared here.

---

## World Scale

**Law: 1 Unity unit = 1 meter.** Never break this in VR.

| Object Class | Size (m) | Notes |
|---|---|---|
| Character height | 1.8 | humanoid total |
| VR hand | 0.18 | bounding size |
| Katana blade | 0.95 | melee weapon |
| Enemy/player ship | 9.0 | hull length |
| Projectile bolt | 0.15 | radius |
| Cockpit reach | 2.0 | interior zone radius |

### Asteroid Diameters

| Tier | Diameter (m) |
|---|---|
| Small | 2 |
| Medium | 5 |
| Large | 12 |

**Scale tolerance:** ±10% counts as conforming.

---

## Emission Quantization

Only two allowed strengths:

| Mode | Strength | Use |
|---|---|---|
| Dim | 1.0 | civilian / secondary elements |
| Hero | 2.5 | hero / primary elements |

Snap arbitrary emission values to the nearer of these two.

---

## Post-FX Grade (Canonical)

All scenes grade toward this canonical post-FX state. Deviations must stay within envelope bounds.

| Parameter | Canonical | Min | Max |
|---|---|---|---|
| Bloom intensity | 0.9 | — | — |
| Bloom threshold | 0.9 | — | — |
| Bloom scatter | 0.7 | — | — |
| Contrast | 18 | 12 | 24 |
| Saturation | 14 | 8 | 20 |
| Vignette | 0.2 | 0.12 | 0.30 |
| Cool filter | (0.86, 0.93, 1.0) | — | — |
| Split shadows | (0.00, 0.30, 0.38) | — | — |
| Split highlights | (0.55, 0.42, 0.32) | — | — |
| Split balance | 0.0 | — | — |

---

## Key Constraints

- **Meshes:** Every solid uses a `LowPolyMeshes` faceted mesh — never raw `CreatePrimitive()`.
- **Faction:** Read from emission hue, not body color.
- **Palette:** All accent colors centralized in `NeonPalette` (Cyan, CyanDim, Magenta, Amber, Violet).
