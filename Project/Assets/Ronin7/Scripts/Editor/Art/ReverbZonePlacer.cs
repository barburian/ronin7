using Ronin7.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Auto-places <see cref="AudioReverbZone"/> components on the zones already marked out by
    /// <see cref="ZoneBounds"/> in the currently open scene, classified via
    /// <see cref="ReverbPresetSelector"/>. Operates on the open scene only (never iterates all 14
    /// scenes), marks it dirty, and does not save. Re-running is idempotent: each zone's reverb
    /// child is found by name and updated in place rather than duplicated, and zones that
    /// reclassify to Off have their previously-placed child removed.
    /// ZoneDefinition/ZoneController were considered too, but neither is placed with a scene
    /// position of its own in any of the 14 chapter scenes today (only ZoneBounds is), so that
    /// path only acts on ZoneBounds. <see cref="InteriorVolume"/> markers get their own menu entry
    /// (below) that runs the same place/update/remove logic against their footprints instead.
    /// </summary>
    public static class ReverbZonePlacer
    {
        /// <summary>Name of the child GameObject this tool creates under each ZoneBounds/InteriorVolume
        /// — also the marker "Remove Reverb Zones" uses to find only zones this tool created.</summary>
        public const string GeneratedChildName = "AutoReverbZone";

        [MenuItem("Tools/Space Samurai/Art/Place Reverb Zones (Open Scene)")]
        public static void PlaceReverbZones()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();

            Undo.SetCurrentGroupName("Place Reverb Zones");
            int undoGroup = Undo.GetCurrentGroup();

            int placed = 0, updated = 0, removed = 0, skippedOff = 0;

            foreach (GameObject root in roots)
            {
                foreach (ZoneBounds zone in root.GetComponentsInChildren<ZoneBounds>(true))
                {
                    float diameter = zone.radius * 2f;
                    var bounds = new Bounds(zone.center, new Vector3(diameter, diameter, diameter));
                    PlaceOrUpdateReverbZone(zone.transform, zone.center, bounds, ref placed, ref updated, ref removed, ref skippedOff);
                }
            }

            if (placed + updated + removed > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[ReverbZonePlacer] '{scene.name}': placed {placed}, updated {updated}, " +
                      $"removed {removed} (reclassified Off), skipped {skippedOff} (open/off) zone(s). " +
                      "Scene marked dirty — save manually.");
        }

        /// <summary>
        /// Same place/update/remove logic as <see cref="PlaceReverbZones"/>, run against every
        /// <see cref="InteriorVolume"/> marker's <see cref="InteriorVolume.WorldBounds"/> instead of
        /// ZoneBounds. Separate menu entry/method because InteriorVolume markers are purely authored
        /// footprints (center offset + size), not ZoneBounds' circle-clamp radius.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Art/Place Reverb Zones — Interior Volumes (Open Scene)")]
        public static void PlaceReverbZonesForInteriorVolumes()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();

            Undo.SetCurrentGroupName("Place Reverb Zones (Interior Volumes)");
            int undoGroup = Undo.GetCurrentGroup();

            int placed = 0, updated = 0, removed = 0, skippedOff = 0;

            foreach (GameObject root in roots)
            {
                foreach (InteriorVolume volume in root.GetComponentsInChildren<InteriorVolume>(true))
                {
                    Bounds bounds = volume.WorldBounds;
                    PlaceOrUpdateReverbZone(volume.transform, bounds.center, bounds, ref placed, ref updated, ref removed, ref skippedOff);
                }
            }

            if (placed + updated + removed > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[ReverbZonePlacer] '{scene.name}' (InteriorVolume): placed {placed}, updated {updated}, " +
                      $"removed {removed} (reclassified Off), skipped {skippedOff} (open/off) zone(s). " +
                      "Scene marked dirty — save manually.");
        }

        /// <summary>Shared place/update/remove step for one zone owner: classifies
        /// <paramref name="bounds"/>, then creates/updates/removes its <see cref="GeneratedChildName"/>
        /// child accordingly. Used by both the ZoneBounds and InteriorVolume placement paths so their
        /// idempotency logic can't drift apart.</summary>
        private static void PlaceOrUpdateReverbZone(Transform owner, Vector3 position, Bounds bounds,
            ref int placed, ref int updated, ref int removed, ref int skippedOff)
        {
            var (preset, minDistance, maxDistance) = ReverbPresetSelector.Classify(bounds);
            Transform existing = owner.Find(GeneratedChildName);

            if (preset == AudioReverbPreset.Off)
            {
                if (existing != null)
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                    removed++;
                }
                skippedOff++;
                return;
            }

            GameObject zoneGo;
            if (existing != null)
            {
                zoneGo = existing.gameObject;
                updated++;
            }
            else
            {
                zoneGo = new GameObject(GeneratedChildName);
                Undo.RegisterCreatedObjectUndo(zoneGo, "Place Reverb Zones");
                zoneGo.transform.SetParent(owner, false);
                placed++;
            }

            zoneGo.transform.position = position;

            var reverb = zoneGo.GetComponent<AudioReverbZone>();
            if (reverb == null) reverb = Undo.AddComponent<AudioReverbZone>(zoneGo);
            Undo.RecordObject(reverb, "Place Reverb Zones");
            reverb.reverbPreset = preset;
            reverb.minDistance = minDistance;
            reverb.maxDistance = maxDistance;
        }

        /// <summary>Name of the marker child this tool creates on each enclosed room's floor.</summary>
        public const string GeneratedVolumeChildName = "AutoInteriorVolume";

        /// <summary>
        /// Bridges the gap between chapter geometry and <see cref="PlaceReverbZonesForInteriorVolumes"/>:
        /// every enclosed room built via <c>BuildFloorCeiling</c> (and its per-chapter Y-aware variants,
        /// e.g. Chapter16's <c>Ch16BuildTier</c>) leaves a same-named "X_Floor" + "X_Ceiling" cube pair
        /// under a common parent — the only reliable, chapter-agnostic signal that a room is actually
        /// enclosed (an open platform built with "floor only" has no matching "_Ceiling" and is
        /// correctly skipped). For each such pair found in the open scene, creates/updates an
        /// <see cref="InteriorVolume"/> marker as a child of the floor sized to the floor's horizontal
        /// footprint and the floor-to-ceiling height. Idempotent like its sibling menu items — safe to
        /// re-run after geometry edits.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Art/Auto-Tag Interior Volumes From Floor+Ceiling Pairs (Open Scene)")]
        public static void AutoTagInteriorVolumes()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();

            Undo.SetCurrentGroupName("Auto-Tag Interior Volumes");
            int undoGroup = Undo.GetCurrentGroup();
            int placed = 0, updated = 0;

            foreach (GameObject root in roots)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    const string suffix = "_Floor";
                    if (!t.name.EndsWith(suffix) || t.parent == null) continue;

                    string prefix = t.name.Substring(0, t.name.Length - suffix.Length);
                    Transform ceiling = t.parent.Find(prefix + "_Ceiling");
                    if (ceiling == null) continue; // open platform, not an enclosed room -- no reverb

                    float height = Mathf.Max(ceiling.localPosition.y - t.localPosition.y, 0.1f);
                    Vector3 size = new Vector3(t.localScale.x, height, t.localScale.z);

                    Transform existing = t.Find(GeneratedVolumeChildName);
                    GameObject volumeGo;
                    if (existing != null) { volumeGo = existing.gameObject; updated++; }
                    else
                    {
                        volumeGo = new GameObject(GeneratedVolumeChildName);
                        Undo.RegisterCreatedObjectUndo(volumeGo, "Auto-Tag Interior Volumes");
                        volumeGo.transform.SetParent(t, false);
                        placed++;
                    }

                    var volume = volumeGo.GetComponent<InteriorVolume>();
                    if (volume == null) volume = Undo.AddComponent<InteriorVolume>(volumeGo);
                    Undo.RecordObject(volume, "Auto-Tag Interior Volumes");
                    volume.centerOffset = new Vector3(0f, height * 0.5f, 0f);
                    volume.size = size;
                }
            }

            if (placed + updated > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[ReverbZonePlacer] '{scene.name}': tagged {placed} new interior volume(s), " +
                      $"updated {updated}. Scene marked dirty — save manually.");
        }

        [MenuItem("Tools/Space Samurai/Art/Remove Reverb Zones (Open Scene)")]
        public static void RemoveReverbZones()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();

            Undo.SetCurrentGroupName("Remove Reverb Zones");
            int undoGroup = Undo.GetCurrentGroup();
            int removed = 0;

            foreach (GameObject root in roots)
            {
                foreach (ZoneBounds zone in root.GetComponentsInChildren<ZoneBounds>(true))
                    removed += RemoveGeneratedChild(zone.transform);
                foreach (InteriorVolume volume in root.GetComponentsInChildren<InteriorVolume>(true))
                    removed += RemoveGeneratedChild(volume.transform);
            }

            if (removed > 0) EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[ReverbZonePlacer] '{scene.name}': removed {removed} reverb zone(s). Scene marked dirty — save manually.");
        }

        private static int RemoveGeneratedChild(Transform owner)
        {
            Transform existing = owner.Find(GeneratedChildName);
            if (existing == null) return 0;
            Undo.DestroyObjectImmediate(existing.gameObject);
            return 1;
        }
    }
}
