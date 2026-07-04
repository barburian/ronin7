# Orphan Materials Report

**Date:** 2026-07-04
**Author:** Team Epsilon (automated read-only audit)
**Status:** Analysis only. No assets were moved, renamed, or deleted.

## Method

1. Inventoried every `.mat` file under `Project/Assets` via `Glob`/`find` (703 files) and read each
   asset's GUID from its adjacent `.meta` file.
2. Collected every `guid: <32-hex>` occurrence across all `.unity`, `.prefab`, `.asset`,
   `.controller`, and `.mat` files under `Project/Assets` (872 candidate referencing files) into one
   deduplicated set (690 unique GUIDs referenced by *something*), using a NUL-delimited file list to
   correctly handle the handful of paths with spaces (`TextMesh Pro/Resources/Fonts & Materials/...`).
3. Diffed the 703 material GUIDs against that reference set. A material is an **orphan** if its GUID
   never appears in any of those file types outside of its own `.meta`.
4. Cross-checked orphan candidates against `Project/Assets/Ronin7/Scripts/**/*.cs` for
   `AssetDatabase.LoadAssetAtPath` / hardcoded-path usage (materials can be "used" by editor tooling
   without ever being serialized into a checked-in scene/prefab, so a GUID-orphan is not automatically
   a dead asset) and for `Resources.Load` usage (materials loaded by name at runtime would show as
   GUID-orphans too, and must not be treated as safe to remove).

**Important caveat on what "orphan" means here:** this method only detects references that are
*serialized by GUID*. It does **not** detect materials assigned by hardcoded string path from editor
scripts (`AssetDatabase.LoadAssetAtPath("Assets/.../Foo.mat")`), which several of the base/template
materials below use. Those are flagged separately in Section 4 — they are "GUID-orphans" but are
still live inputs to the art-generation tooling, not dead weight.

## Summary Counts

| Metric | Count |
|---|---|
| Total `.mat` files under `Project/Assets` | 703 |
| Referenced by GUID (scene/prefab/asset/controller/mat) | 26 |
| **Orphaned (no GUID reference found)** | **677** |
| Orphan total disk size | ~937 KB (959,461 bytes) — materials are small text/YAML assets |
| All materials total disk size | ~983 KB (1,006,157 bytes) |

### Discrepancy vs. the handoff's "~288 orphans"

This audit finds **677** GUID-orphans, well above the ~288 figure in
`Project/Docs/IMPROVEMENT-SUMMARY.md`. The most likely explanation, based on code and memory notes
found during this audit: the "Variants"/"HeroVariants" material folders (650 of the 677 orphans) are
output of the **old primitive-block character/prop generator**. The project's character pipeline has
since moved to the Tripo image→3D method (see project memory `character-creation-method.md`), whose
generated meshes carry **embedded** materials (serialized with `type: 3`, i.e. a sub-asset of the
model import) rather than references to the standalone `.mat` files in `Art/Materials/`. Every
`Named/*.prefab` character checked during this audit (e.g. `Aldric_Knight-1.prefab`) uses an embedded
model material, not a Variants/HeroVariants `.mat` file. If the ~288 figure was measured before this
migration, or measured only a subset of the Variants folder, that would account for the gap. This is
inference, not confirmed history — flagging it for a human to reconcile rather than asserting it as fact.

## Dynamic-Load / Runtime-Risk Check

- `grep -rn "Resources.Load" Project/Assets/Ronin7/Scripts --include="*.cs"` returns exactly one
  call site: `CombatVfxController.cs:71` — `Resources.Load<GameObject>($"Vfx/{name}")`, which loads
  **prefabs** from `Assets/Ronin7/Resources/Vfx/`, not materials. No game script loads any material
  by name/`Resources.Load`.
- None of the 677 orphaned materials live under a `Resources/` folder **except two**:
  `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat` and
  `... - Outline.mat`. These are stock TextMeshPro package assets. TMP's `TMP_Text` material-preset
  picker resolves alternate presets for a font asset (e.g. "LiberationSans SDF - Outline") by a
  **naming-convention search through all `Resources` folders** at both edit- and run-time, not by a
  GUID serialized anywhere. That means they are legitimately orphaned by this GUID method **and
  legitimately load-bearing**. **Flag: do not quarantine or delete these two.**
- No other orphan sits in a `Resources/` folder, and no orphan name appears in any
  `Resources.Load`/`Resources.LoadAll` call anywhere in `Scripts/`.

## Orphan List by Folder

### `Assets/Ronin7/Art/Materials/Variants/` — 625 orphans (regenerable)

Randomized per-instance color/roughness/emissive swatches, named `<Family>_r<R>g<G>b<B>[_e<R>g<G>b<B>].mat`.
Generated (and re-generatable) by `ArtGenerationMenu.cs` / `ArtPrefabBuilder.cs`
(`AssignBaseMapToFamily`, `WriteAndAssignDetail`, `AssignEmissionToFamily`). Family breakdown:

| Family | Count |
|---|---|
| SamuraiToon_EnemyFoot | 301 |
| SamuraiToon_Npc | 299 |
| SamuraiToon_ShipHull | 11 |
| SamuraiToon_Cockpit | 7 |
| SamuraiToon_Sword | 4 |
| SamuraiToon_Hand | 3 |

Full list: see **Appendix A**.

### `Assets/Ronin7/Art/Materials/HeroVariants/` — 25 orphans (no code reference found)

Same swatch-generator naming style (`Gritty_<R>_<G>_<B>[_s|_m][_e<R>_<G>_<B>].mat`) plus one named
asset (`CyberSword_Blade.mat`). Unlike the base `Variants/` folder, **no script under
`Project/Assets/Ronin7/Scripts` references the `HeroVariants` folder or any of these 25 filenames** —
these are the highest-confidence "truly dead" candidates in the whole set, though the no-auto-delete
policy still applies. Full list: see **Appendix B**.

### `Assets/Ronin7/Art/Materials/Placeholders/` — 0 orphans

All 18 materials in this folder ARE GUID-referenced (via prefabs built by
`PlaceholderCharacterBuilder.cs`, whose own comment notes it was deliberately designed to avoid
producing an orphaned Placeholders folder). Listed here only to confirm the method isn't
systematically over-flagging — no action needed.

### Loose top-level `Assets/Ronin7/Art/Materials/*.mat` — 25 orphans, mixed status

These are hand-authored "template" materials. Most are still live inputs to editor build tooling via
hardcoded `AssetDatabase.LoadAssetAtPath` calls (not GUID references), so despite being GUID-orphans
they are **not** dead. Two have no known consumer at all.

| File | Bytes | Status |
|---|---|---|
| Galaxy1_Sky_Blackveil.mat | 1,016 | Editor tool: `Galaxy1Builder.cs` loads `Galaxy1_Sky_{themeName}.mat` by hardcoded path pattern to set a chapter's skybox at scene-build time. Not yet wired into any checked-in scene's `RenderSettings`. |
| Galaxy1_Sky_Desert.mat | 998 | Same as above. |
| Galaxy1_Sky_EP01Water.mat | 1,006 | Same as above. |
| Galaxy1_Sky_Frost.mat | 997 | Same as above. |
| Galaxy1_Sky_Jungle.mat | 1,004 | Same as above. |
| Galaxy1_Sky_Lava.mat | 1,004 | Same as above. |
| Galaxy1_Sky_Velorum.mat | 997 | Same as above. |
| Galaxy1_Sky_Water.mat | 1,007 | Same as above. |
| Mat_Armor_Metal.mat | 3,831 | **No code or scene reference found anywhere.** Dead. |
| Mat_DojoTech_Skin.mat | 1,206 | **No code or scene reference found anywhere.** Dead. |
| PlasmaTrail_Additive.mat | 2,608 | Editor tool: `ArtPrefabBuilder.Plasma.cs` (`PlasmaTrailMatPath` const). |
| SamuraiOutline.mat | 835 | Editor tool: `ArtPrefabBuilder.cs` (`OutlineMatPath`) and `Galaxy1Builder.cs` (loaded at scene build). |
| SamuraiToon_Asteroid.mat | 1,325 | Editor tool: `ArtPrefabBuilder.cs` (`AsteroidMatPath`), `ArtGenerationMenu.cs` detail-texture family. |
| SamuraiToon_Base.mat | 1,070 | Used by EditMode test `SamuraiToonShaderTests.cs` (`BaseMaterialPath`). |
| SamuraiToon_Cockpit.mat | 1,322 | Editor tool: `ArtPrefabBuilder.cs` (`CockpitMatPath`). |
| SamuraiToon_EnemyFoot.mat | 1,405 | Editor tool: `ArtPrefabBuilder.cs` (`EnemyFootMatPath`), base for the 301 Variants swatches. |
| SamuraiToon_EnemyShip.mat | 1,401 | Editor tool: `ArtPrefabBuilder.cs` (`EnemyShipMatPath`). |
| SamuraiToon_Hand.mat | 1,319 | Editor tool: `ArtPrefabBuilder.cs` (`HandMatPath`). |
| SamuraiToon_Npc.mat | 1,399 | Editor tool: `ArtPrefabBuilder.cs` (`NpcBodyMatPath`), base for the 299 Variants swatches. |
| SamuraiToon_PlasmaBlade_r255g51b140.mat | 1,309 | Editor tool: `ArtPrefabBuilder.Plasma.cs` (`PlasmaBladeMatPrefix`). |
| SamuraiToon_PlasmaBlade_r38g217b255.mat | 1,312 | Editor tool: `ArtPrefabBuilder.Plasma.cs` (`PlasmaBladeMatPrefix`). |
| SamuraiToon_ShipHull.mat | 1,323 | Editor tool: `ArtPrefabBuilder.cs` (`ShipHullMatPath`). |
| SamuraiToon_Sword.mat | 1,310 | Editor tool: `ArtPrefabBuilder.cs` (`SwordMatPath`). |
| SamuraiToon_Wormhole.mat | 1,076 | Editor tool: `ArtPrefabBuilder.cs` (`WormholeMatPath`). |
| SpaceBlackSkybox.mat | 969 | Editor tool: `XRRigBuilder.cs` (`SpaceSkyboxPath`). |

### `Assets/TextMesh Pro/Resources/Fonts & Materials/` — 2 orphans (DO NOT TOUCH)

| File | Bytes | Status |
|---|---|---|
| LiberationSans SDF - Drop Shadow.mat | 2,975 | TMP built-in material preset, resolved by name at runtime — see risk check above. |
| LiberationSans SDF - Outline.mat | 2,889 | Same. |

## Recommended Actions

Per handoff policy, **no automatic deletion**. Ranked by confidence that removal is safe:

1. **Quarantine candidates (highest confidence dead — 25 files, `HeroVariants/`):** no script, scene,
   prefab, or asset references the folder or any filename in it. Safe to move to a
   `_Quarantine/Materials/HeroVariants/` staging folder for a human to confirm before deletion.
2. **Quarantine candidates (2 files, top-level):** `Mat_Armor_Metal.mat`, `Mat_DojoTech_Skin.mat` —
   no code or scene reference found anywhere. Same treatment as above.
3. **Keep, but understand they're stale-by-design (625 files, `Variants/`):** generator output from
   the pre-Tripo character pipeline. Deleting is reversible (regenerate via
   `Tools/Space Samurai/Art/...` menu items in `ArtGenerationMenu.cs`), but since 300 of these hang
   off `SamuraiToon_EnemyFoot`/`SamuraiToon_Npc` (the enemy/NPC base materials that ARE still used by
   editor tooling) a human should confirm the new Tripo-based enemy pipeline truly no longer consumes
   swatch variants before bulk-quarantining.
4. **Keep as live tooling inputs (23 top-level template materials):** all `SamuraiToon_*`,
   `SamuraiOutline`, `PlasmaTrail_Additive`, `SpaceBlackSkybox`, and `Galaxy1_Sky_*` materials are
   referenced by hardcoded path from `ArtPrefabBuilder.cs`, `ArtGenerationMenu.cs`,
   `ArtPrefabBuilder.Plasma.cs`, `XRRigBuilder.cs`, `Galaxy1Builder.cs`, or an EditMode test. Do not
   move or delete — they are inputs to regeneration, not orphans in the "unused" sense.
5. **Never touch (2 files):** the two TextMesh Pro `Resources/` materials. They are dynamically
   resolved by TMP's naming convention and would break font rendering (drop-shadow/outline text
   styles) if removed.
6. **Reconcile the count:** flag to whoever owns `IMPROVEMENT-SUMMARY.md` that the true current
   orphan count is 677, not ~288, and update that doc once the human review above is complete.

## Appendix A — `Variants/` full list (625 files)

| File | Bytes |
|---|---|
| SamuraiToon_Cockpit_r140g26b26.mat | 1332 |
| SamuraiToon_Cockpit_r18g20b26.mat | 1332 |
| SamuraiToon_Cockpit_r18g20b26_er38g217b255.mat | 1351 |
| SamuraiToon_Cockpit_r18g20b26_er96g542b638.mat | 1357 |
| SamuraiToon_Cockpit_r199g140b46.mat | 1335 |
| SamuraiToon_Cockpit_r26g31b41.mat | 1332 |
| SamuraiToon_Cockpit_r46g31b20.mat | 1333 |
| SamuraiToon_EnemyFoot_r0g0b139.mat | 1409 |
| SamuraiToon_EnemyFoot_r0g0b140.mat | 1408 |
| SamuraiToon_EnemyFoot_r0g0b66.mat | 1407 |
| SamuraiToon_EnemyFoot_r0g105b148.mat | 1414 |
| SamuraiToon_EnemyFoot_r0g107b133.mat | 1413 |
| SamuraiToon_EnemyFoot_r0g107b140.mat | 1413 |
| SamuraiToon_EnemyFoot_r0g112b143.mat | 1413 |
| SamuraiToon_EnemyFoot_r0g112b148.mat | 1413 |
| SamuraiToon_EnemyFoot_r0g117b143.mat | 1413 |
| SamuraiToon_EnemyFoot_r0g122b148.mat | 1413 |
| SamuraiToon_EnemyFoot_r0g128b153.mat | 1411 |
| SamuraiToon_EnemyFoot_r0g128b158.mat | 1412 |
| SamuraiToon_EnemyFoot_r0g139b139.mat | 1415 |
| SamuraiToon_EnemyFoot_r0g178b178.mat | 1411 |
| SamuraiToon_EnemyFoot_r0g204b178.mat | 1411 |
| SamuraiToon_EnemyFoot_r0g255b102.mat | 1409 |
| SamuraiToon_EnemyFoot_r0g255b136.mat | 1411 |
| SamuraiToon_EnemyFoot_r0g46b66.mat | 1411 |
| SamuraiToon_EnemyFoot_r0g46b71.mat | 1411 |
| SamuraiToon_EnemyFoot_r0g51b76.mat | 1409 |
| SamuraiToon_EnemyFoot_r0g51b82.mat | 1410 |
| SamuraiToon_EnemyFoot_r0g56b87.mat | 1411 |
| SamuraiToon_EnemyFoot_r0g56b89.mat | 1411 |
| SamuraiToon_EnemyFoot_r0g64b97.mat | 1411 |
| SamuraiToon_EnemyFoot_r0g76b112.mat | 1411 |
| SamuraiToon_EnemyFoot_r0g76b115.mat | 1411 |
| SamuraiToon_EnemyFoot_r0g84b117.mat | 1412 |
| SamuraiToon_EnemyFoot_r0g89b122.mat | 1412 |
| SamuraiToon_EnemyFoot_r0g89b128.mat | 1411 |
| SamuraiToon_EnemyFoot_r0g97b128.mat | 1411 |
| SamuraiToon_EnemyFoot_r0g97b133.mat | 1412 |
| SamuraiToon_EnemyFoot_r102g0b0.mat | 1407 |
| SamuraiToon_EnemyFoot_r102g102b102.mat | 1415 |
| SamuraiToon_EnemyFoot_r102g102b120.mat | 1417 |
| SamuraiToon_EnemyFoot_r102g107b112.mat | 1417 |
| SamuraiToon_EnemyFoot_r102g20b153.mat | 1415 |
| SamuraiToon_EnemyFoot_r102g76b51.mat | 1413 |
| SamuraiToon_EnemyFoot_r107g117b133.mat | 1418 |
| SamuraiToon_EnemyFoot_r107g120b128.mat | 1417 |
| SamuraiToon_EnemyFoot_r107g59b41.mat | 1416 |
| SamuraiToon_EnemyFoot_r107g84b59.mat | 1416 |
| SamuraiToon_EnemyFoot_r10g10b10.mat | 1415 |
| SamuraiToon_EnemyFoot_r10g10b13.mat | 1415 |
| SamuraiToon_EnemyFoot_r10g10b20.mat | 1418 |
| SamuraiToon_EnemyFoot_r10g15b20.mat | 1415 |
| SamuraiToon_EnemyFoot_r10g26b96.mat | 1418 |
| SamuraiToon_EnemyFoot_r10g8b8.mat | 1413 |
| SamuraiToon_EnemyFoot_r110g107b102.mat | 1417 |
| SamuraiToon_EnemyFoot_r112g120b140.mat | 1418 |
| SamuraiToon_EnemyFoot_r117g94b66.mat | 1416 |
| SamuraiToon_EnemyFoot_r120g85b55.mat | 1419 |
| SamuraiToon_EnemyFoot_r120g97b68.mat | 1417 |
| SamuraiToon_EnemyFoot_r122g133b143.mat | 1418 |
| SamuraiToon_EnemyFoot_r122g59b16.mat | 1419 |
| SamuraiToon_EnemyFoot_r122g59b31.mat | 1416 |
| SamuraiToon_EnemyFoot_r122g92b61.mat | 1416 |
| SamuraiToon_EnemyFoot_r122g99b70.mat | 1417 |
| SamuraiToon_EnemyFoot_r122g99b71.mat | 1416 |
| SamuraiToon_EnemyFoot_r125g102b74.mat | 1416 |
| SamuraiToon_EnemyFoot_r128g0b32.mat | 1414 |
| SamuraiToon_EnemyFoot_r128g0b33.mat | 1411 |
| SamuraiToon_EnemyFoot_r128g105b75.mat | 1417 |
| SamuraiToon_EnemyFoot_r128g105b76.mat | 1415 |
| SamuraiToon_EnemyFoot_r128g107b92.mat | 1416 |
| SamuraiToon_EnemyFoot_r133g31b0.mat | 1412 |
| SamuraiToon_EnemyFoot_r139g0b0.mat | 1409 |
| SamuraiToon_EnemyFoot_r139g105b20.mat | 1420 |
| SamuraiToon_EnemyFoot_r139g115b85.mat | 1420 |
| SamuraiToon_EnemyFoot_r139g69b19.mat | 1419 |
| SamuraiToon_EnemyFoot_r13g10b10.mat | 1415 |
| SamuraiToon_EnemyFoot_r13g10b15.mat | 1415 |
| SamuraiToon_EnemyFoot_r13g112b112.mat | 1420 |
| SamuraiToon_EnemyFoot_r13g13b15.mat | 1415 |
| SamuraiToon_EnemyFoot_r13g13b18.mat | 1415 |
| SamuraiToon_EnemyFoot_r140g140b140.mat | 1421 |
| SamuraiToon_EnemyFoot_r140g140b148.mat | 1418 |
| SamuraiToon_EnemyFoot_r140g26b26.mat | 1414 |
| SamuraiToon_EnemyFoot_r140g36b0.mat | 1412 |
| SamuraiToon_EnemyFoot_r140g74b31.mat | 1416 |
| SamuraiToon_EnemyFoot_r140g94b61.mat | 1416 |
| SamuraiToon_EnemyFoot_r143g143b158.mat | 1418 |
| SamuraiToon_EnemyFoot_r143g166b191.mat | 1418 |
| SamuraiToon_EnemyFoot_r148g158b173.mat | 1418 |
| SamuraiToon_EnemyFoot_r153g10b20.mat | 1415 |
| SamuraiToon_EnemyFoot_r153g133b89.mat | 1416 |
| SamuraiToon_EnemyFoot_r153g166b173.mat | 1417 |
| SamuraiToon_EnemyFoot_r153g166b184.mat | 1417 |
| SamuraiToon_EnemyFoot_r156g78b22.mat | 1419 |
| SamuraiToon_EnemyFoot_r158g10b20.mat | 1416 |
| SamuraiToon_EnemyFoot_r158g122b84.mat | 1417 |
| SamuraiToon_EnemyFoot_r158g150b184.mat | 1418 |
| SamuraiToon_EnemyFoot_r15g10b10.mat | 1415 |
| SamuraiToon_EnemyFoot_r15g13b23.mat | 1415 |
| SamuraiToon_EnemyFoot_r15g15b18.mat | 1415 |
| SamuraiToon_EnemyFoot_r160g58b12.mat | 1419 |
| SamuraiToon_EnemyFoot_r166g140b102.mat | 1417 |
| SamuraiToon_EnemyFoot_r166g158b140.mat | 1418 |
| SamuraiToon_EnemyFoot_r166g184b209.mat | 1418 |
| SamuraiToon_EnemyFoot_r169g169b169.mat | 1421 |
| SamuraiToon_EnemyFoot_r173g153b140.mat | 1417 |
| SamuraiToon_EnemyFoot_r178g13b13.mat | 1415 |
| SamuraiToon_EnemyFoot_r178g158b122.mat | 1417 |
| SamuraiToon_EnemyFoot_r178g166b230.mat | 1416 |
| SamuraiToon_EnemyFoot_r178g186b191.mat | 1417 |
| SamuraiToon_EnemyFoot_r179g74b0.mat | 1413 |
| SamuraiToon_EnemyFoot_r17g17b17.mat | 1418 |
| SamuraiToon_EnemyFoot_r180g180b190.mat | 1421 |
| SamuraiToon_EnemyFoot_r183g65b14.mat | 1419 |
| SamuraiToon_EnemyFoot_r184g135b41.mat | 1417 |
| SamuraiToon_EnemyFoot_r184g144b42.mat | 1420 |
| SamuraiToon_EnemyFoot_r184g15b26.mat | 1415 |
| SamuraiToon_EnemyFoot_r184g166b120.mat | 1418 |
| SamuraiToon_EnemyFoot_r184g173b158.mat | 1418 |
| SamuraiToon_EnemyFoot_r184g184b191.mat | 1418 |
| SamuraiToon_EnemyFoot_r184g184b199.mat | 1418 |
| SamuraiToon_EnemyFoot_r184g189b178.mat | 1417 |
| SamuraiToon_EnemyFoot_r184g71b8.mat | 1415 |
| SamuraiToon_EnemyFoot_r18g13b10.mat | 1415 |
| SamuraiToon_EnemyFoot_r18g15b28.mat | 1415 |
| SamuraiToon_EnemyFoot_r18g18b18.mat | 1415 |
| SamuraiToon_EnemyFoot_r191g153b46.mat | 1416 |
| SamuraiToon_EnemyFoot_r191g191b191.mat | 1418 |
| SamuraiToon_EnemyFoot_r191g191b217.mat | 1418 |
| SamuraiToon_EnemyFoot_r191g196b204.mat | 1417 |
| SamuraiToon_EnemyFoot_r191g204b242.mat | 1417 |
| SamuraiToon_EnemyFoot_r192g192b192.mat | 1421 |
| SamuraiToon_EnemyFoot_r192g72b7.mat | 1418 |
| SamuraiToon_EnemyFoot_r194g194b166.mat | 1418 |
| SamuraiToon_EnemyFoot_r194g194b194.mat | 1418 |
| SamuraiToon_EnemyFoot_r199g135b66.mat | 1417 |
| SamuraiToon_EnemyFoot_r199g140b46.mat | 1417 |
| SamuraiToon_EnemyFoot_r199g158b51.mat | 1416 |
| SamuraiToon_EnemyFoot_r199g166b54.mat | 1417 |
| SamuraiToon_EnemyFoot_r199g184b219.mat | 1418 |
| SamuraiToon_EnemyFoot_r199g194b209.mat | 1418 |
| SamuraiToon_EnemyFoot_r199g196b191.mat | 1418 |
| SamuraiToon_EnemyFoot_r199g209b219.mat | 1418 |
| SamuraiToon_EnemyFoot_r200g200b204.mat | 1419 |
| SamuraiToon_EnemyFoot_r201g79b26.mat | 1418 |
| SamuraiToon_EnemyFoot_r204g160b32.mat | 1418 |
| SamuraiToon_EnemyFoot_r204g166b0.mat | 1412 |
| SamuraiToon_EnemyFoot_r204g201b153.mat | 1416 |
| SamuraiToon_EnemyFoot_r204g85b0.mat | 1412 |
| SamuraiToon_EnemyFoot_r205g127b50.mat | 1420 |
| SamuraiToon_EnemyFoot_r207g217b224.mat | 1418 |
| SamuraiToon_EnemyFoot_r209g153b20.mat | 1416 |
| SamuraiToon_EnemyFoot_r209g196b184.mat | 1418 |
| SamuraiToon_EnemyFoot_r209g209b204.mat | 1417 |
| SamuraiToon_EnemyFoot_r209g217b242.mat | 1418 |
| SamuraiToon_EnemyFoot_r211g211b211.mat | 1421 |
| SamuraiToon_EnemyFoot_r212g147b90.mat | 1420 |
| SamuraiToon_EnemyFoot_r212g175b55.mat | 1420 |
| SamuraiToon_EnemyFoot_r212g176b56.mat | 1417 |
| SamuraiToon_EnemyFoot_r212g196b161.mat | 1418 |
| SamuraiToon_EnemyFoot_r217g171b31.mat | 1417 |
| SamuraiToon_EnemyFoot_r217g178b51.mat | 1415 |
| SamuraiToon_EnemyFoot_r217g184b61.mat | 1417 |
| SamuraiToon_EnemyFoot_r217g199b133.mat | 1418 |
| SamuraiToon_EnemyFoot_r217g199b158.mat | 1418 |
| SamuraiToon_EnemyFoot_r217g209b232.mat | 1418 |
| SamuraiToon_EnemyFoot_r218g165b32.mat | 1420 |
| SamuraiToon_EnemyFoot_r219g212b158.mat | 1418 |
| SamuraiToon_EnemyFoot_r21g21b21.mat | 1418 |
| SamuraiToon_EnemyFoot_r220g165b30.mat | 1420 |
| SamuraiToon_EnemyFoot_r222g222b230.mat | 1417 |
| SamuraiToon_EnemyFoot_r224g209b122.mat | 1418 |
| SamuraiToon_EnemyFoot_r224g224b230.mat | 1417 |
| SamuraiToon_EnemyFoot_r224g224b235.mat | 1418 |
| SamuraiToon_EnemyFoot_r224g89b41.mat | 1416 |
| SamuraiToon_EnemyFoot_r230g122b5.mat | 1415 |
| SamuraiToon_EnemyFoot_r230g173b71.mat | 1416 |
| SamuraiToon_EnemyFoot_r230g199b81.mat | 1419 |
| SamuraiToon_EnemyFoot_r230g204b178.mat | 1415 |
| SamuraiToon_EnemyFoot_r230g209b191.mat | 1417 |
| SamuraiToon_EnemyFoot_r230g220b200.mat | 1421 |
| SamuraiToon_EnemyFoot_r230g230b230.mat | 1415 |
| SamuraiToon_EnemyFoot_r230g230b250.mat | 1420 |
| SamuraiToon_EnemyFoot_r230g26b26.mat | 1413 |
| SamuraiToon_EnemyFoot_r232g201b122.mat | 1418 |
| SamuraiToon_EnemyFoot_r232g204b112.mat | 1417 |
| SamuraiToon_EnemyFoot_r232g209b97.mat | 1417 |
| SamuraiToon_EnemyFoot_r232g232b232.mat | 1418 |
| SamuraiToon_EnemyFoot_r232g240b255.mat | 1415 |
| SamuraiToon_EnemyFoot_r235g228b210.mat | 1421 |
| SamuraiToon_EnemyFoot_r235g235b230.mat | 1417 |
| SamuraiToon_EnemyFoot_r235g242b255.mat | 1415 |
| SamuraiToon_EnemyFoot_r237g215b137.mat | 1421 |
| SamuraiToon_EnemyFoot_r237g219b138.mat | 1418 |
| SamuraiToon_EnemyFoot_r237g228b176.mat | 1420 |
| SamuraiToon_EnemyFoot_r237g230b217.mat | 1417 |
| SamuraiToon_EnemyFoot_r237g235b227.mat | 1418 |
| SamuraiToon_EnemyFoot_r240g232b217.mat | 1418 |
| SamuraiToon_EnemyFoot_r240g235b216.mat | 1421 |
| SamuraiToon_EnemyFoot_r240g240b255.mat | 1415 |
| SamuraiToon_EnemyFoot_r242g15b15.mat | 1416 |
| SamuraiToon_EnemyFoot_r242g199b38.mat | 1417 |
| SamuraiToon_EnemyFoot_r242g199b56.mat | 1417 |
| SamuraiToon_EnemyFoot_r242g213b184.mat | 1421 |
| SamuraiToon_EnemyFoot_r242g230b153.mat | 1416 |
| SamuraiToon_EnemyFoot_r242g235b224.mat | 1418 |
| SamuraiToon_EnemyFoot_r242g240b247.mat | 1418 |
| SamuraiToon_EnemyFoot_r242g242b242.mat | 1418 |
| SamuraiToon_EnemyFoot_r242g242b245.mat | 1418 |
| SamuraiToon_EnemyFoot_r245g235b224.mat | 1418 |
| SamuraiToon_EnemyFoot_r245g245b252.mat | 1418 |
| SamuraiToon_EnemyFoot_r247g242b252.mat | 1418 |
| SamuraiToon_EnemyFoot_r247g242b255.mat | 1415 |
| SamuraiToon_EnemyFoot_r249g247b244.mat | 1420 |
| SamuraiToon_EnemyFoot_r249g248b245.mat | 1421 |
| SamuraiToon_EnemyFoot_r250g249b246.mat | 1420 |
| SamuraiToon_EnemyFoot_r250g249b247.mat | 1421 |
| SamuraiToon_EnemyFoot_r252g252b249.mat | 1420 |
| SamuraiToon_EnemyFoot_r252g252b250.mat | 1420 |
| SamuraiToon_EnemyFoot_r255g13b13.mat | 1413 |
| SamuraiToon_EnemyFoot_r255g140b8.mat | 1413 |
| SamuraiToon_EnemyFoot_r255g178b26.mat | 1412 |
| SamuraiToon_EnemyFoot_r255g179b0.mat | 1411 |
| SamuraiToon_EnemyFoot_r255g181b18.mat | 1414 |
| SamuraiToon_EnemyFoot_r255g210b50.mat | 1416 |
| SamuraiToon_EnemyFoot_r255g214b0.mat | 1410 |
| SamuraiToon_EnemyFoot_r255g215b0.mat | 1411 |
| SamuraiToon_EnemyFoot_r255g245b230.mat | 1414 |
| SamuraiToon_EnemyFoot_r255g247b235.mat | 1415 |
| SamuraiToon_EnemyFoot_r255g248b220.mat | 1417 |
| SamuraiToon_EnemyFoot_r255g250b204.mat | 1414 |
| SamuraiToon_EnemyFoot_r255g250b205.mat | 1416 |
| SamuraiToon_EnemyFoot_r255g252b240.mat | 1417 |
| SamuraiToon_EnemyFoot_r255g254b240.mat | 1417 |
| SamuraiToon_EnemyFoot_r255g254b241.mat | 1417 |
| SamuraiToon_EnemyFoot_r255g255b240.mat | 1413 |
| SamuraiToon_EnemyFoot_r255g255b242.mat | 1412 |
| SamuraiToon_EnemyFoot_r255g255b255.mat | 1409 |
| SamuraiToon_EnemyFoot_r25g25b112.mat | 1419 |
| SamuraiToon_EnemyFoot_r26g18b13.mat | 1414 |
| SamuraiToon_EnemyFoot_r26g18b8.mat | 1417 |
| SamuraiToon_EnemyFoot_r26g23b38.mat | 1414 |
| SamuraiToon_EnemyFoot_r26g255b102.mat | 1412 |
| SamuraiToon_EnemyFoot_r26g26b26.mat | 1418 |
| SamuraiToon_EnemyFoot_r26g26b31.mat | 1413 |
| SamuraiToon_EnemyFoot_r26g74b128.mat | 1418 |
| SamuraiToon_EnemyFoot_r26g84b140.mat | 1419 |
| SamuraiToon_EnemyFoot_r28g28b117.mat | 1416 |
| SamuraiToon_EnemyFoot_r28g28b28.mat | 1415 |
| SamuraiToon_EnemyFoot_r31g10b61.mat | 1415 |
| SamuraiToon_EnemyFoot_r31g26b23.mat | 1414 |
| SamuraiToon_EnemyFoot_r31g26b46.mat | 1414 |
| SamuraiToon_EnemyFoot_r31g46b74.mat | 1415 |
| SamuraiToon_EnemyFoot_r36g20b8.mat | 1414 |
| SamuraiToon_EnemyFoot_r38g26b97.mat | 1414 |
| SamuraiToon_EnemyFoot_r38g31b23.mat | 1415 |
| SamuraiToon_EnemyFoot_r38g38b46.mat | 1415 |
| SamuraiToon_EnemyFoot_r3g3b204.mat | 1413 |
| SamuraiToon_EnemyFoot_r3g6b10.mat | 1415 |
| SamuraiToon_EnemyFoot_r40g50b60.mat | 1418 |
| SamuraiToon_EnemyFoot_r41g31b26.mat | 1414 |
| SamuraiToon_EnemyFoot_r42g42b42.mat | 1418 |
| SamuraiToon_EnemyFoot_r42g53b64.mat | 1418 |
| SamuraiToon_EnemyFoot_r42g54b64.mat | 1418 |
| SamuraiToon_EnemyFoot_r43g158b143.mat | 1417 |
| SamuraiToon_EnemyFoot_r43g26b13.mat | 1414 |
| SamuraiToon_EnemyFoot_r46g41b33.mat | 1415 |
| SamuraiToon_EnemyFoot_r46g43b43.mat | 1415 |
| SamuraiToon_EnemyFoot_r46g43b56.mat | 1415 |
| SamuraiToon_EnemyFoot_r46g46b46.mat | 1415 |
| SamuraiToon_EnemyFoot_r46g51b56.mat | 1414 |
| SamuraiToon_EnemyFoot_r46g51b59.mat | 1414 |
| SamuraiToon_EnemyFoot_r51g217b242.mat | 1413 |
| SamuraiToon_EnemyFoot_r51g38b31.mat | 1414 |
| SamuraiToon_EnemyFoot_r51g51b51.mat | 1412 |
| SamuraiToon_EnemyFoot_r51g56b64.mat | 1414 |
| SamuraiToon_EnemyFoot_r54g69b79.mat | 1417 |
| SamuraiToon_EnemyFoot_r56g31b184.mat | 1416 |
| SamuraiToon_EnemyFoot_r56g33b26.mat | 1414 |
| SamuraiToon_EnemyFoot_r56g43b36.mat | 1415 |
| SamuraiToon_EnemyFoot_r56g56b56.mat | 1415 |
| SamuraiToon_EnemyFoot_r58g58b58.mat | 1418 |
| SamuraiToon_EnemyFoot_r59g32b18.mat | 1418 |
| SamuraiToon_EnemyFoot_r5g5b5.mat | 1412 |
| SamuraiToon_EnemyFoot_r5g5b8.mat | 1412 |
| SamuraiToon_EnemyFoot_r61g40b21.mat | 1418 |
| SamuraiToon_EnemyFoot_r64g48b26.mat | 1414 |
| SamuraiToon_EnemyFoot_r64g69b79.mat | 1415 |
| SamuraiToon_EnemyFoot_r64g76b87.mat | 1418 |
| SamuraiToon_EnemyFoot_r66g66b71.mat | 1415 |
| SamuraiToon_EnemyFoot_r71g28b28.mat | 1415 |
| SamuraiToon_EnemyFoot_r74g15b128.mat | 1415 |
| SamuraiToon_EnemyFoot_r74g46b26.mat | 1416 |
| SamuraiToon_EnemyFoot_r74g95b106.mat | 1418 |
| SamuraiToon_EnemyFoot_r75g0b130.mat | 1413 |
| SamuraiToon_EnemyFoot_r76g153b76.mat | 1413 |
| SamuraiToon_EnemyFoot_r82g56b166.mat | 1416 |
| SamuraiToon_EnemyFoot_r87g66b43.mat | 1415 |
| SamuraiToon_EnemyFoot_r87g66b46.mat | 1415 |
| SamuraiToon_EnemyFoot_r89g89b89.mat | 1415 |
| SamuraiToon_EnemyFoot_r89g97b115.mat | 1416 |
| SamuraiToon_EnemyFoot_r8g8b8.mat | 1412 |
| SamuraiToon_EnemyFoot_r92g46b46.mat | 1416 |
| SamuraiToon_EnemyFoot_r92g61b31.mat | 1415 |
| SamuraiToon_EnemyFoot_r92g69b46.mat | 1415 |
| SamuraiToon_EnemyFoot_r97g74b51.mat | 1414 |
| SamuraiToon_EnemyFoot_r97g79b74.mat | 1415 |
| SamuraiToon_Hand_r140g46b46.mat | 1331 |
| SamuraiToon_Hand_r237g199b158.mat | 1333 |
| SamuraiToon_Hand_r242g235b224.mat | 1333 |
| SamuraiToon_Npc_r0g0b139.mat | 1403 |
| SamuraiToon_Npc_r0g0b140.mat | 1402 |
| SamuraiToon_Npc_r0g0b66.mat | 1401 |
| SamuraiToon_Npc_r0g105b148.mat | 1408 |
| SamuraiToon_Npc_r0g107b133.mat | 1407 |
| SamuraiToon_Npc_r0g107b140.mat | 1407 |
| SamuraiToon_Npc_r0g112b143.mat | 1407 |
| SamuraiToon_Npc_r0g112b148.mat | 1407 |
| SamuraiToon_Npc_r0g117b143.mat | 1407 |
| SamuraiToon_Npc_r0g122b148.mat | 1407 |
| SamuraiToon_Npc_r0g128b153.mat | 1405 |
| SamuraiToon_Npc_r0g128b158.mat | 1406 |
| SamuraiToon_Npc_r0g139b139.mat | 1409 |
| SamuraiToon_Npc_r0g178b178.mat | 1405 |
| SamuraiToon_Npc_r0g191b230.mat | 1406 |
| SamuraiToon_Npc_r0g204b178.mat | 1405 |
| SamuraiToon_Npc_r0g217b242.mat | 1407 |
| SamuraiToon_Npc_r0g255b102.mat | 1403 |
| SamuraiToon_Npc_r0g255b136.mat | 1405 |
| SamuraiToon_Npc_r0g46b66.mat | 1405 |
| SamuraiToon_Npc_r0g46b71.mat | 1405 |
| SamuraiToon_Npc_r0g51b76.mat | 1403 |
| SamuraiToon_Npc_r0g51b82.mat | 1404 |
| SamuraiToon_Npc_r0g56b87.mat | 1405 |
| SamuraiToon_Npc_r0g56b89.mat | 1405 |
| SamuraiToon_Npc_r0g64b97.mat | 1405 |
| SamuraiToon_Npc_r0g76b112.mat | 1405 |
| SamuraiToon_Npc_r0g76b115.mat | 1405 |
| SamuraiToon_Npc_r0g84b117.mat | 1406 |
| SamuraiToon_Npc_r0g89b122.mat | 1406 |
| SamuraiToon_Npc_r0g89b128.mat | 1405 |
| SamuraiToon_Npc_r0g97b128.mat | 1405 |
| SamuraiToon_Npc_r0g97b133.mat | 1406 |
| SamuraiToon_Npc_r102g0b0.mat | 1401 |
| SamuraiToon_Npc_r102g102b102.mat | 1409 |
| SamuraiToon_Npc_r102g102b120.mat | 1411 |
| SamuraiToon_Npc_r102g107b112.mat | 1411 |
| SamuraiToon_Npc_r102g20b153.mat | 1409 |
| SamuraiToon_Npc_r102g76b51.mat | 1407 |
| SamuraiToon_Npc_r107g117b133.mat | 1412 |
| SamuraiToon_Npc_r107g120b128.mat | 1411 |
| SamuraiToon_Npc_r107g59b41.mat | 1410 |
| SamuraiToon_Npc_r107g84b59.mat | 1410 |
| SamuraiToon_Npc_r10g10b10.mat | 1409 |
| SamuraiToon_Npc_r10g10b13.mat | 1409 |
| SamuraiToon_Npc_r10g10b20.mat | 1412 |
| SamuraiToon_Npc_r10g15b20.mat | 1409 |
| SamuraiToon_Npc_r10g26b96.mat | 1412 |
| SamuraiToon_Npc_r10g8b8.mat | 1407 |
| SamuraiToon_Npc_r110g107b102.mat | 1411 |
| SamuraiToon_Npc_r117g94b66.mat | 1410 |
| SamuraiToon_Npc_r120g85b55.mat | 1413 |
| SamuraiToon_Npc_r120g97b68.mat | 1411 |
| SamuraiToon_Npc_r122g133b143.mat | 1412 |
| SamuraiToon_Npc_r122g59b16.mat | 1413 |
| SamuraiToon_Npc_r122g59b31.mat | 1410 |
| SamuraiToon_Npc_r122g92b61.mat | 1410 |
| SamuraiToon_Npc_r122g99b70.mat | 1411 |
| SamuraiToon_Npc_r122g99b71.mat | 1410 |
| SamuraiToon_Npc_r125g102b74.mat | 1410 |
| SamuraiToon_Npc_r128g0b32.mat | 1408 |
| SamuraiToon_Npc_r128g0b33.mat | 1405 |
| SamuraiToon_Npc_r128g105b75.mat | 1411 |
| SamuraiToon_Npc_r128g105b76.mat | 1409 |
| SamuraiToon_Npc_r128g107b92.mat | 1410 |
| SamuraiToon_Npc_r133g31b0.mat | 1406 |
| SamuraiToon_Npc_r139g0b0.mat | 1403 |
| SamuraiToon_Npc_r139g105b20.mat | 1414 |
| SamuraiToon_Npc_r139g115b85.mat | 1414 |
| SamuraiToon_Npc_r139g69b19.mat | 1413 |
| SamuraiToon_Npc_r13g10b10.mat | 1409 |
| SamuraiToon_Npc_r13g10b15.mat | 1409 |
| SamuraiToon_Npc_r13g112b112.mat | 1414 |
| SamuraiToon_Npc_r13g13b15.mat | 1409 |
| SamuraiToon_Npc_r13g13b18.mat | 1409 |
| SamuraiToon_Npc_r140g140b140.mat | 1415 |
| SamuraiToon_Npc_r140g140b148.mat | 1412 |
| SamuraiToon_Npc_r140g36b0.mat | 1406 |
| SamuraiToon_Npc_r140g74b31.mat | 1410 |
| SamuraiToon_Npc_r140g94b61.mat | 1410 |
| SamuraiToon_Npc_r143g143b158.mat | 1412 |
| SamuraiToon_Npc_r143g166b191.mat | 1412 |
| SamuraiToon_Npc_r148g158b173.mat | 1412 |
| SamuraiToon_Npc_r153g10b20.mat | 1409 |
| SamuraiToon_Npc_r153g133b89.mat | 1410 |
| SamuraiToon_Npc_r153g166b184.mat | 1411 |
| SamuraiToon_Npc_r156g78b22.mat | 1413 |
| SamuraiToon_Npc_r158g10b20.mat | 1410 |
| SamuraiToon_Npc_r158g122b84.mat | 1411 |
| SamuraiToon_Npc_r158g150b184.mat | 1412 |
| SamuraiToon_Npc_r15g10b10.mat | 1409 |
| SamuraiToon_Npc_r15g13b23.mat | 1409 |
| SamuraiToon_Npc_r15g15b18.mat | 1409 |
| SamuraiToon_Npc_r160g58b12.mat | 1413 |
| SamuraiToon_Npc_r166g140b102.mat | 1411 |
| SamuraiToon_Npc_r166g158b140.mat | 1412 |
| SamuraiToon_Npc_r166g184b209.mat | 1412 |
| SamuraiToon_Npc_r169g169b169.mat | 1415 |
| SamuraiToon_Npc_r173g153b140.mat | 1411 |
| SamuraiToon_Npc_r178g158b122.mat | 1411 |
| SamuraiToon_Npc_r178g166b230.mat | 1410 |
| SamuraiToon_Npc_r178g186b191.mat | 1411 |
| SamuraiToon_Npc_r179g74b0.mat | 1407 |
| SamuraiToon_Npc_r17g17b17.mat | 1412 |
| SamuraiToon_Npc_r180g180b190.mat | 1415 |
| SamuraiToon_Npc_r183g65b14.mat | 1413 |
| SamuraiToon_Npc_r184g135b41.mat | 1411 |
| SamuraiToon_Npc_r184g144b42.mat | 1414 |
| SamuraiToon_Npc_r184g15b26.mat | 1409 |
| SamuraiToon_Npc_r184g166b120.mat | 1412 |
| SamuraiToon_Npc_r184g173b158.mat | 1412 |
| SamuraiToon_Npc_r184g184b191.mat | 1412 |
| SamuraiToon_Npc_r184g184b199.mat | 1412 |
| SamuraiToon_Npc_r184g189b178.mat | 1411 |
| SamuraiToon_Npc_r184g71b8.mat | 1409 |
| SamuraiToon_Npc_r18g13b10.mat | 1409 |
| SamuraiToon_Npc_r18g15b28.mat | 1409 |
| SamuraiToon_Npc_r18g18b18.mat | 1409 |
| SamuraiToon_Npc_r18g18b23.mat | 1409 |
| SamuraiToon_Npc_r191g153b46.mat | 1410 |
| SamuraiToon_Npc_r191g191b191.mat | 1412 |
| SamuraiToon_Npc_r191g191b217.mat | 1412 |
| SamuraiToon_Npc_r191g196b204.mat | 1411 |
| SamuraiToon_Npc_r191g204b242.mat | 1411 |
| SamuraiToon_Npc_r192g192b192.mat | 1415 |
| SamuraiToon_Npc_r192g72b7.mat | 1412 |
| SamuraiToon_Npc_r194g194b166.mat | 1412 |
| SamuraiToon_Npc_r194g194b194.mat | 1412 |
| SamuraiToon_Npc_r199g135b66.mat | 1411 |
| SamuraiToon_Npc_r199g140b46.mat | 1411 |
| SamuraiToon_Npc_r199g158b51.mat | 1410 |
| SamuraiToon_Npc_r199g166b54.mat | 1411 |
| SamuraiToon_Npc_r199g184b219.mat | 1412 |
| SamuraiToon_Npc_r199g194b209.mat | 1412 |
| SamuraiToon_Npc_r199g196b191.mat | 1412 |
| SamuraiToon_Npc_r199g209b219.mat | 1412 |
| SamuraiToon_Npc_r200g200b204.mat | 1413 |
| SamuraiToon_Npc_r201g79b26.mat | 1412 |
| SamuraiToon_Npc_r204g160b32.mat | 1412 |
| SamuraiToon_Npc_r204g166b0.mat | 1406 |
| SamuraiToon_Npc_r204g201b153.mat | 1410 |
| SamuraiToon_Npc_r204g85b0.mat | 1406 |
| SamuraiToon_Npc_r205g127b50.mat | 1414 |
| SamuraiToon_Npc_r207g217b224.mat | 1412 |
| SamuraiToon_Npc_r209g153b20.mat | 1410 |
| SamuraiToon_Npc_r209g196b184.mat | 1412 |
| SamuraiToon_Npc_r209g209b204.mat | 1411 |
| SamuraiToon_Npc_r209g217b242.mat | 1412 |
| SamuraiToon_Npc_r20g20b26.mat | 1408 |
| SamuraiToon_Npc_r211g211b211.mat | 1415 |
| SamuraiToon_Npc_r212g147b90.mat | 1414 |
| SamuraiToon_Npc_r212g175b55.mat | 1414 |
| SamuraiToon_Npc_r212g176b56.mat | 1411 |
| SamuraiToon_Npc_r212g196b161.mat | 1412 |
| SamuraiToon_Npc_r217g171b31.mat | 1411 |
| SamuraiToon_Npc_r217g178b51.mat | 1409 |
| SamuraiToon_Npc_r217g184b61.mat | 1411 |
| SamuraiToon_Npc_r217g199b133.mat | 1412 |
| SamuraiToon_Npc_r217g199b158.mat | 1412 |
| SamuraiToon_Npc_r217g209b232.mat | 1412 |
| SamuraiToon_Npc_r218g165b32.mat | 1414 |
| SamuraiToon_Npc_r219g212b158.mat | 1412 |
| SamuraiToon_Npc_r21g21b21.mat | 1412 |
| SamuraiToon_Npc_r220g165b30.mat | 1414 |
| SamuraiToon_Npc_r222g222b230.mat | 1411 |
| SamuraiToon_Npc_r224g209b122.mat | 1412 |
| SamuraiToon_Npc_r224g224b230.mat | 1411 |
| SamuraiToon_Npc_r224g224b235.mat | 1412 |
| SamuraiToon_Npc_r224g89b41.mat | 1410 |
| SamuraiToon_Npc_r230g122b5.mat | 1409 |
| SamuraiToon_Npc_r230g173b71.mat | 1410 |
| SamuraiToon_Npc_r230g199b81.mat | 1413 |
| SamuraiToon_Npc_r230g204b178.mat | 1409 |
| SamuraiToon_Npc_r230g209b191.mat | 1411 |
| SamuraiToon_Npc_r230g220b200.mat | 1415 |
| SamuraiToon_Npc_r230g230b230.mat | 1409 |
| SamuraiToon_Npc_r230g230b250.mat | 1414 |
| SamuraiToon_Npc_r232g201b122.mat | 1412 |
| SamuraiToon_Npc_r232g204b112.mat | 1411 |
| SamuraiToon_Npc_r232g209b97.mat | 1411 |
| SamuraiToon_Npc_r232g232b232.mat | 1412 |
| SamuraiToon_Npc_r232g240b255.mat | 1409 |
| SamuraiToon_Npc_r235g228b210.mat | 1415 |
| SamuraiToon_Npc_r235g242b255.mat | 1409 |
| SamuraiToon_Npc_r237g215b137.mat | 1415 |
| SamuraiToon_Npc_r237g219b138.mat | 1412 |
| SamuraiToon_Npc_r237g228b176.mat | 1414 |
| SamuraiToon_Npc_r237g230b217.mat | 1411 |
| SamuraiToon_Npc_r237g235b227.mat | 1412 |
| SamuraiToon_Npc_r23g26b31.mat | 1408 |
| SamuraiToon_Npc_r240g232b217.mat | 1412 |
| SamuraiToon_Npc_r240g235b216.mat | 1415 |
| SamuraiToon_Npc_r240g240b255.mat | 1409 |
| SamuraiToon_Npc_r242g15b15.mat | 1410 |
| SamuraiToon_Npc_r242g199b38.mat | 1411 |
| SamuraiToon_Npc_r242g199b56.mat | 1411 |
| SamuraiToon_Npc_r242g213b184.mat | 1415 |
| SamuraiToon_Npc_r242g230b153.mat | 1410 |
| SamuraiToon_Npc_r242g240b247.mat | 1412 |
| SamuraiToon_Npc_r242g242b242.mat | 1412 |
| SamuraiToon_Npc_r242g242b245.mat | 1412 |
| SamuraiToon_Npc_r245g235b224.mat | 1412 |
| SamuraiToon_Npc_r245g245b252.mat | 1412 |
| SamuraiToon_Npc_r247g242b252.mat | 1412 |
| SamuraiToon_Npc_r247g242b255.mat | 1409 |
| SamuraiToon_Npc_r249g247b244.mat | 1414 |
| SamuraiToon_Npc_r249g248b245.mat | 1415 |
| SamuraiToon_Npc_r250g249b246.mat | 1414 |
| SamuraiToon_Npc_r250g249b247.mat | 1415 |
| SamuraiToon_Npc_r252g252b249.mat | 1414 |
| SamuraiToon_Npc_r252g252b250.mat | 1414 |
| SamuraiToon_Npc_r255g140b8.mat | 1407 |
| SamuraiToon_Npc_r255g178b26.mat | 1406 |
| SamuraiToon_Npc_r255g179b0.mat | 1405 |
| SamuraiToon_Npc_r255g181b18.mat | 1408 |
| SamuraiToon_Npc_r255g210b50.mat | 1410 |
| SamuraiToon_Npc_r255g214b0.mat | 1404 |
| SamuraiToon_Npc_r255g215b0.mat | 1405 |
| SamuraiToon_Npc_r255g245b230.mat | 1408 |
| SamuraiToon_Npc_r255g247b235.mat | 1409 |
| SamuraiToon_Npc_r255g248b220.mat | 1411 |
| SamuraiToon_Npc_r255g250b204.mat | 1408 |
| SamuraiToon_Npc_r255g250b205.mat | 1410 |
| SamuraiToon_Npc_r255g252b240.mat | 1411 |
| SamuraiToon_Npc_r255g254b240.mat | 1411 |
| SamuraiToon_Npc_r255g254b241.mat | 1411 |
| SamuraiToon_Npc_r255g255b240.mat | 1407 |
| SamuraiToon_Npc_r255g255b242.mat | 1406 |
| SamuraiToon_Npc_r255g255b255.mat | 1403 |
| SamuraiToon_Npc_r25g25b112.mat | 1413 |
| SamuraiToon_Npc_r26g18b13.mat | 1408 |
| SamuraiToon_Npc_r26g18b8.mat | 1411 |
| SamuraiToon_Npc_r26g23b38.mat | 1408 |
| SamuraiToon_Npc_r26g255b102.mat | 1406 |
| SamuraiToon_Npc_r26g26b26.mat | 1412 |
| SamuraiToon_Npc_r26g28b33.mat | 1408 |
| SamuraiToon_Npc_r26g74b128.mat | 1412 |
| SamuraiToon_Npc_r26g84b140.mat | 1413 |
| SamuraiToon_Npc_r28g28b117.mat | 1410 |
| SamuraiToon_Npc_r28g28b28.mat | 1409 |
| SamuraiToon_Npc_r28g31b36.mat | 1409 |
| SamuraiToon_Npc_r31g10b61.mat | 1409 |
| SamuraiToon_Npc_r31g26b23.mat | 1408 |
| SamuraiToon_Npc_r31g26b46.mat | 1408 |
| SamuraiToon_Npc_r31g46b74.mat | 1409 |
| SamuraiToon_Npc_r33g38b46.mat | 1409 |
| SamuraiToon_Npc_r36g20b8.mat | 1408 |
| SamuraiToon_Npc_r38g26b97.mat | 1408 |
| SamuraiToon_Npc_r38g31b23.mat | 1409 |
| SamuraiToon_Npc_r38g38b46.mat | 1409 |
| SamuraiToon_Npc_r3g3b204.mat | 1407 |
| SamuraiToon_Npc_r3g6b10.mat | 1409 |
| SamuraiToon_Npc_r40g50b60.mat | 1412 |
| SamuraiToon_Npc_r41g31b26.mat | 1408 |
| SamuraiToon_Npc_r41g33b28.mat | 1409 |
| SamuraiToon_Npc_r42g42b42.mat | 1412 |
| SamuraiToon_Npc_r42g53b64.mat | 1412 |
| SamuraiToon_Npc_r42g54b64.mat | 1412 |
| SamuraiToon_Npc_r43g158b143.mat | 1411 |
| SamuraiToon_Npc_r43g26b13.mat | 1408 |
| SamuraiToon_Npc_r46g41b33.mat | 1409 |
| SamuraiToon_Npc_r46g43b43.mat | 1409 |
| SamuraiToon_Npc_r46g43b56.mat | 1409 |
| SamuraiToon_Npc_r46g46b46.mat | 1409 |
| SamuraiToon_Npc_r46g51b56.mat | 1408 |
| SamuraiToon_Npc_r46g51b59.mat | 1408 |
| SamuraiToon_Npc_r51g38b31.mat | 1408 |
| SamuraiToon_Npc_r51g51b51.mat | 1406 |
| SamuraiToon_Npc_r51g56b64.mat | 1408 |
| SamuraiToon_Npc_r54g69b79.mat | 1411 |
| SamuraiToon_Npc_r56g31b184.mat | 1410 |
| SamuraiToon_Npc_r56g33b26.mat | 1408 |
| SamuraiToon_Npc_r56g43b36.mat | 1409 |
| SamuraiToon_Npc_r56g56b56.mat | 1409 |
| SamuraiToon_Npc_r58g58b58.mat | 1412 |
| SamuraiToon_Npc_r59g32b18.mat | 1412 |
| SamuraiToon_Npc_r5g5b5.mat | 1406 |
| SamuraiToon_Npc_r5g5b8.mat | 1406 |
| SamuraiToon_Npc_r61g40b21.mat | 1412 |
| SamuraiToon_Npc_r64g48b26.mat | 1408 |
| SamuraiToon_Npc_r64g76b87.mat | 1412 |
| SamuraiToon_Npc_r66g66b71.mat | 1409 |
| SamuraiToon_Npc_r71g28b28.mat | 1409 |
| SamuraiToon_Npc_r74g15b128.mat | 1409 |
| SamuraiToon_Npc_r74g46b26.mat | 1410 |
| SamuraiToon_Npc_r74g95b106.mat | 1412 |
| SamuraiToon_Npc_r75g0b130.mat | 1407 |
| SamuraiToon_Npc_r76g153b76.mat | 1407 |
| SamuraiToon_Npc_r82g56b166.mat | 1410 |
| SamuraiToon_Npc_r87g66b43.mat | 1409 |
| SamuraiToon_Npc_r87g66b46.mat | 1409 |
| SamuraiToon_Npc_r89g89b89.mat | 1409 |
| SamuraiToon_Npc_r89g97b115.mat | 1410 |
| SamuraiToon_Npc_r8g8b8.mat | 1406 |
| SamuraiToon_Npc_r92g46b46.mat | 1410 |
| SamuraiToon_Npc_r92g61b31.mat | 1409 |
| SamuraiToon_Npc_r92g69b46.mat | 1409 |
| SamuraiToon_Npc_r97g74b51.mat | 1408 |
| SamuraiToon_Npc_r97g79b74.mat | 1409 |
| SamuraiToon_ShipHull_r115g64b38.mat | 1335 |
| SamuraiToon_ShipHull_r128g102b64.mat | 1334 |
| SamuraiToon_ShipHull_r140g26b26.mat | 1333 |
| SamuraiToon_ShipHull_r153g166b184.mat | 1336 |
| SamuraiToon_ShipHull_r18g20b26_er38g217b255.mat | 1352 |
| SamuraiToon_ShipHull_r18g20b26_er96g542b638.mat | 1358 |
| SamuraiToon_ShipHull_r199g140b46.mat | 1336 |
| SamuraiToon_ShipHull_r235g235b242.mat | 1337 |
| SamuraiToon_ShipHull_r26g31b41.mat | 1333 |
| SamuraiToon_ShipHull_r31g26b46.mat | 1333 |
| SamuraiToon_ShipHull_r46g31b20.mat | 1334 |
| SamuraiToon_Sword_r199g140b46.mat | 1331 |
| SamuraiToon_Sword_r235g242b255.mat | 1329 |
| SamuraiToon_Sword_r242g235b224.mat | 1332 |
| SamuraiToon_Sword_r31g26b46.mat | 1328 |

## Appendix B — `HeroVariants/` full list (25 files)

| File | Bytes |
|---|---|
| CyberSword_Blade.mat | 1468 |
| Gritty_0_191_230_s_e96_542_638.mat | 1594 |
| Gritty_0_217_242_s_e96_542_638.mat | 1595 |
| Gritty_107_117_133_m.mat | 1579 |
| Gritty_110_107_102_s.mat | 1583 |
| Gritty_140_74_31_s.mat | 1581 |
| Gritty_158_122_84_s.mat | 1576 |
| Gritty_15_15_18_s.mat | 1582 |
| Gritty_184_166_120_s.mat | 1570 |
| Gritty_18_18_23_s.mat | 1581 |
| Gritty_199_140_46_m.mat | 1578 |
| Gritty_199_196_191_s_e255_140_26.mat | 1593 |
| Gritty_209_209_204_s_e255_140_26.mat | 1592 |
| Gritty_20_20_25_s.mat | 1582 |
| Gritty_23_25_31_s.mat | 1581 |
| Gritty_242_235_224_m.mat | 1567 |
| Gritty_25_28_33_m.mat | 1582 |
| Gritty_25_28_33_s.mat | 1580 |
| Gritty_28_31_36_m.mat | 1582 |
| Gritty_31_25_46_s.mat | 1580 |
| Gritty_33_38_46_m.mat | 1582 |
| Gritty_38_31_23_s.mat | 1581 |
| Gritty_41_33_28_s.mat | 1580 |
| Gritty_46_41_33_m.mat | 1582 |
| Gritty_46_41_33_s.mat | 1580 |
