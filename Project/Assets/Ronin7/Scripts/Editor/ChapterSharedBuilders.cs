using System.Collections.Generic;
using Ronin7.Audio;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using Ronin7.Player;
using Ronin7.Ship;
using Ronin7.World;
using Ronin7.World.Story;
using Ronin7.Editor.Art;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Chapter-agnostic scene-building helpers shared by every chapter builder (geometry, doors, NPCs,
    /// dialogue, mission-step authoring). Lives in the same <see cref="XRRigBuilder"/> partial class, so
    /// every builder file keeps calling these exactly as before.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const float RoomH = 3.6f;

        // Named-cast prefabs baked from Data/CharacterSpecs by "Build Characters from Specs Folder".
        // These are FEET-pivot (parts authored from y≈0 up), so place them at floor height (y=0) —
        // unlike ArtPrefabBuilder.KesslerPrefabPath, whose root is a body capsule placed at y=1.
        // Used across chapter builders.
        private const string GeneratedCharFolder = "Assets/Ronin7/Prefabs/Art/Generated";
        private const string ReshPrefabPath    = GeneratedCharFolder + "/Resh.prefab";
        private const string IrisPrefabPath    = GeneratedCharFolder + "/Iris.prefab";
        private const string KhallPrefabPath   = GeneratedCharFolder + "/Khall.prefab";
        private const string DrHerisPrefabPath = GeneratedCharFolder + "/DrHeris.prefab";
        private const string ChildPrefabPath   = GeneratedCharFolder + "/Cassie04.prefab";

        /// <summary>Drops a capsule-frame position (pivot at y≈1) to the floor for feet-pivot prefabs.</summary>
        private static Vector3 AtFloor(Vector3 p) => new Vector3(p.x, 0f, p.z);

        private static void BuildWall(Transform parent, string name, Vector3 localPos, Vector3 localScale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = localPos;
            wall.transform.localScale = localScale;
            wall.GetComponent<MeshFilter>().sharedMesh = LowPolyMeshes.ForType(PrimitiveType.Cube);
            TintShared(wall.GetComponent<Renderer>(), new Color(0.18f, 0.20f, 0.24f));
            // Keep the collider so player can't walk through.
        }

        /// <summary>Floor (with collider) + ceiling (no collider) pair for a rectangular area. <paramref name="size"/>.y is ignored.</summary>
        private static void BuildFloorCeiling(Transform parent, string name, Vector3 center, Vector3 size, Color floorColor, Color ceilColor)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name + "_Floor";
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector3(center.x, -0.1f, center.z);
            floor.transform.localScale = new Vector3(size.x, 0.2f, size.z);
            floor.GetComponent<MeshFilter>().sharedMesh = LowPolyMeshes.ForType(PrimitiveType.Cube);
            TintShared(floor.GetComponent<Renderer>(), floorColor);

            var ceil = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceil.name = name + "_Ceiling";
            ceil.transform.SetParent(parent, false);
            ceil.transform.localPosition = new Vector3(center.x, RoomH, center.z);
            ceil.transform.localScale = new Vector3(size.x, 0.2f, size.z);
            ceil.GetComponent<MeshFilter>().sharedMesh = LowPolyMeshes.ForType(PrimitiveType.Cube);
            TintShared(ceil.GetComponent<Renderer>(), ceilColor);
            Object.DestroyImmediate(ceil.GetComponent<Collider>());
        }

        /// <summary>A wall with a centred door gap: two side segments plus a lintel above the gap.</summary>
        private static void BuildDoorwayWall(Transform parent, string name, Vector3 center, float length, bool alongX, float doorWidth)
        {
            const float t = 0.2f, doorH = 2.4f;
            const float h = RoomH;
            float segLen = (length - doorWidth) / 2f;
            float off = doorWidth / 2f + segLen / 2f;
            float lintelY = doorH + (h - doorH) / 2f;
            if (alongX)
            {
                BuildWall(parent, name + "_A", center + new Vector3(-off, 0f, 0f), new Vector3(segLen, h, t));
                BuildWall(parent, name + "_B", center + new Vector3(off, 0f, 0f), new Vector3(segLen, h, t));
                BuildWall(parent, name + "_Lintel", new Vector3(center.x, lintelY, center.z), new Vector3(doorWidth, h - doorH, t));
            }
            else
            {
                BuildWall(parent, name + "_A", center + new Vector3(0f, 0f, -off), new Vector3(t, h, segLen));
                BuildWall(parent, name + "_B", center + new Vector3(0f, 0f, off), new Vector3(t, h, segLen));
                BuildWall(parent, name + "_Lintel", new Vector3(center.x, lintelY, center.z), new Vector3(t, h - doorH, doorWidth));
            }
        }

        /// <summary>A wall running along z (at fixed <paramref name="x"/>) with one or more centred door gaps.</summary>
        private static void BuildCorridorWall(Transform parent, string name, float x, float zStart, float zEnd, float[] doorCenters, float doorWidth)
        {
            const float t = 0.2f, doorH = 2.4f;
            const float h = RoomH;
            float lintelY = doorH + (h - doorH) / 2f;
            float cursor = zStart;
            int i = 0;
            foreach (float dc in doorCenters)
            {
                float gapStart = dc - doorWidth / 2f;
                if (gapStart > cursor)
                {
                    float segLen = gapStart - cursor;
                    BuildWall(parent, $"{name}_seg{i}", new Vector3(x, h / 2f, cursor + segLen / 2f), new Vector3(t, h, segLen));
                }
                BuildWall(parent, $"{name}_lintel{i}", new Vector3(x, lintelY, dc), new Vector3(t, h - doorH, doorWidth));
                cursor = dc + doorWidth / 2f;
                i++;
            }
            if (zEnd > cursor)
            {
                float segLen = zEnd - cursor;
                BuildWall(parent, $"{name}_segEnd", new Vector3(x, h / 2f, cursor + segLen / 2f), new Vector3(t, h, segLen));
            }
        }

        /// <summary>
        /// A two-panel sliding door filling a wall's door gap. Returns the controller GameObject carrying
        /// the <see cref="ProximityDoor"/>; activating it "unlocks" the door. When
        /// <paramref name="startLocked"/> is true the controller starts inactive so the door stays shut.
        /// <paramref name="alongX"/> = the wall runs along x (door faces ±z); otherwise it runs along z.
        /// </summary>
        private static GameObject BuildSlidingDoor(Transform parent, string name, Vector3 doorCenter, float doorWidth, bool alongX, bool startLocked)
        {
            const float doorH = 2.4f, panelT = 0.15f;
            var doorColor = new Color(0.32f, 0.46f, 0.6f);

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = doorCenter;

            Vector3 panelScale = alongX
                ? new Vector3(doorWidth / 2f, doorH, panelT)
                : new Vector3(panelT, doorH, doorWidth / 2f);
            Vector3 leftClosed = alongX
                ? new Vector3(-doorWidth / 4f, doorH / 2f, 0f)
                : new Vector3(0f, doorH / 2f, -doorWidth / 4f);
            Vector3 rightClosed = alongX
                ? new Vector3(doorWidth / 4f, doorH / 2f, 0f)
                : new Vector3(0f, doorH / 2f, doorWidth / 4f);
            Vector3 openOffset = alongX
                ? new Vector3(-doorWidth / 2f, 0f, 0f)
                : new Vector3(0f, 0f, -doorWidth / 2f);

            var panelL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panelL.name = "PanelL";
            panelL.transform.SetParent(root.transform, false);
            panelL.transform.localPosition = leftClosed;
            panelL.transform.localScale = panelScale;
            panelL.GetComponent<MeshFilter>().sharedMesh = LowPolyMeshes.ForType(PrimitiveType.Cube);
            TintShared(panelL.GetComponent<Renderer>(), doorColor);

            var panelR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panelR.name = "PanelR";
            panelR.transform.SetParent(root.transform, false);
            panelR.transform.localPosition = rightClosed;
            panelR.transform.localScale = panelScale;
            panelR.GetComponent<MeshFilter>().sharedMesh = LowPolyMeshes.ForType(PrimitiveType.Cube);
            TintShared(panelR.GetComponent<Renderer>(), doorColor);

            var controller = new GameObject("Controller");
            controller.transform.SetParent(root.transform, false);
            var door = controller.AddComponent<ProximityDoor>();
            var so = new SerializedObject(door);
            SetObjectRef(so, "leftPanel", panelL.transform);
            SetObjectRef(so, "rightPanel", panelR.transform);
            so.FindProperty("openOffset").vector3Value = openOffset;
            so.ApplyModifiedPropertiesWithoutUndo();

            controller.SetActive(!startLocked);
            return controller;
        }

        /// <summary>Wire audio to a ProximityDoor controller: add a 3D AudioSource and assign open/close clips.</summary>
        private static void WireDoorAudio(GameObject doorController, AudioClip slideClip)
        {
            if (doorController == null || slideClip == null) return;
            var audioSource = doorController.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D spatial
            audioSource.playOnAwake = false;

            var doorComp = doorController.GetComponent<ProximityDoor>();
            if (doorComp != null)
            {
                var doorSo = new SerializedObject(doorComp);
                doorSo.FindProperty("openClip").objectReferenceValue = slideClip;
                doorSo.FindProperty("closeClip").objectReferenceValue = slideClip;
                SetObjectRef(doorSo, "audioSource", audioSource);
                doorSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>A cheap realtime point light used as a mood accent (URP additional light).</summary>
        private static void BuildAccentPointLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None; // keep it cheap on Quest
        }

        /// <summary>Builds a root-level looping ambience bed: AudioSource + <see cref="ProximityAmbienceLayer"/>
        /// at a world position. Wire the clip afterward with a single scene-wide
        /// <see cref="Ronin7.Editor.Art.ProceduralAudioClipBuilder.AssignGeneratedClips"/> call — it infers
        /// the theme (HangarHum/GardenWind/DreadDrone) from this object's own <paramref name="name"/>, so
        /// name accordingly (e.g. containing "garden"/"wind" or "dread"/"throne"/"vault" picks those themes;
        /// anything else defaults to HangarHum).</summary>
        private static GameObject BuildAmbienceLayer(string name, Vector3 position, float innerRadius, float outerRadius, float maxVolume)
        {
            var existing = GameObject.Find(name);
            if (existing != null) return existing; // idempotent

            var go = new GameObject(name);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 0f; // ProximityAmbienceLayer drives its own distance-based volume
            var layer = go.AddComponent<ProximityAmbienceLayer>();
            var so = new SerializedObject(layer);
            SetObjectRef(so, "source", source);
            so.FindProperty("innerRadius").floatValue = innerRadius;
            so.FindProperty("outerRadius").floatValue = outerRadius;
            so.FindProperty("maxVolume").floatValue = maxVolume;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>Adds a <see cref="ConsoleFlickerLight"/> to an existing root-level accent light found
        /// by <paramref name="lightName"/> (as built by <see cref="BuildAccentPointLight"/>). No-op if the
        /// light isn't found. <paramref name="seed"/> should differ across lights in the same scene so
        /// they don't flicker in lockstep.</summary>
        private static void AddConsoleFlicker(string lightName, float seed)
        {
            var light = GameObject.Find(lightName)?.GetComponent<Light>();
            if (light == null) return;
            if (light.gameObject.GetComponent<ConsoleFlickerLight>() != null) return; // idempotent
            var flicker = light.gameObject.AddComponent<ConsoleFlickerLight>();
            var so = new SerializedObject(flicker);
            SetObjectRef(so, "targetLight", light);
            so.FindProperty("minIntensity").floatValue = light.intensity * 0.75f;
            so.FindProperty("maxIntensity").floatValue = light.intensity * 1.2f;
            so.FindProperty("seed").floatValue = seed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Adds an <see cref="AmbientLightPulse"/> to an existing root-level accent light found
        /// by <paramref name="lightName"/> (as built by <see cref="BuildAccentPointLight"/>). No-op if the
        /// light isn't found.</summary>
        private static void AddAmbientPulse(string lightName, float periodSeconds = 6f)
        {
            var light = GameObject.Find(lightName)?.GetComponent<Light>();
            if (light == null) return;
            if (light.gameObject.GetComponent<AmbientLightPulse>() != null) return; // idempotent
            var pulse = light.gameObject.AddComponent<AmbientLightPulse>();
            var so = new SerializedObject(pulse);
            SetObjectRef(so, "targetLight", light);
            so.FindProperty("periodSeconds").floatValue = periodSeconds;
            so.FindProperty("minIntensity").floatValue = light.intensity * 0.6f;
            so.FindProperty("maxIntensity").floatValue = light.intensity * 1.15f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildProp(Transform parent, string name, Vector3 pos, Vector3 scale, Color color) =>
            BuildProp(parent, name, pos, Quaternion.identity, scale, color);

        /// <summary>Overload taking an explicit local rotation — used by the room-detail archetypes below
        /// for offset/rotated crate scatter.</summary>
        private static void BuildProp(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = LowPolyMeshes.ForType(PrimitiveType.Cube);
            TintShared(go.GetComponent<Renderer>(), color);
        }

        /// <summary>Cylinder-primitive counterpart of <see cref="BuildProp"/>, used by the cable-run
        /// archetype below. Keeps its collider, matching every other room-detail prop.</summary>
        private static void BuildCylinderProp(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = LowPolyMeshes.ForType(PrimitiveType.Cylinder);
            TintShared(go.GetComponent<Renderer>(), color);
        }

        /// <summary>Scatters a few cheap, shared-material detail props (crates, a console with an emissive
        /// screen, a ceiling pipe) inside a room to make it feel lived-in. Kept light for
        /// Quest. <paramref name="center"/> is the room floor-center; <paramref name="halfExtents"/> is the
        /// half-size (x,z) of the usable floor; <paramref name="accent"/> tints the props.
        /// On top of that fixed base template, 1-2 <see cref="PickRoomDetailArchetypes"/>-selected extra
        /// props are added so every room doesn't read as the identical 3-prop cluster (deterministic per
        /// <paramref name="name"/>, so rebuilding a chapter reproduces the same layout).</summary>
        private static void BuildRoomDetails(Transform parent, string name, Vector3 center, Vector2 halfExtents, Color accent)
        {
            float hx = halfExtents.x, hz = halfExtents.y;

            // Crate cluster tucked into the -x/-z corner. Door gaps sit on the x-walls for side rooms and
            // around x=0 / +z for the medbay & command rooms, so this corner stays clear of doorways.
            float cornerX = center.x - hx + 0.85f;
            float cornerZ = center.z - hz + 0.85f;
            var crateLo  = new Color(accent.r * 0.8f, accent.g * 0.8f, accent.b * 0.8f);
            var crateHi  = new Color(accent.r * 1.1f, accent.g * 1.1f, accent.b * 1.1f);
            var crateMid = new Color(accent.r * 0.9f, accent.g * 0.9f, accent.b * 0.9f);
            BuildProp(parent, name + "_Crate0", new Vector3(cornerX, 0.45f, cornerZ), new Vector3(0.9f, 0.9f, 0.9f), crateLo);
            BuildProp(parent, name + "_Crate1", new Vector3(cornerX + 0.1f, 1.2f, cornerZ + 0.1f), new Vector3(0.6f, 0.6f, 0.6f), crateHi);
            BuildProp(parent, name + "_Crate2", new Vector3(cornerX + 0.75f, 0.3f, cornerZ), new Vector3(0.6f, 0.6f, 0.6f), crateMid);

            // Wall console against the -z wall on the +x side, with a bright emissive-reading screen.
            float consoleX = center.x + hx - 1.0f;
            float consoleZ = center.z - hz + 0.7f;
            BuildProp(parent, name + "_Console", new Vector3(consoleX, 0.5f, consoleZ), new Vector3(1.2f, 1.0f, 0.6f),
                new Color(accent.r * 0.7f, accent.g * 0.7f, accent.b * 0.7f));
            BuildProp(parent, name + "_ConsoleScreen", new Vector3(consoleX, 0.6f, consoleZ + 0.33f), new Vector3(0.8f, 0.5f, 0.05f),
                new Color(0.2f, 0.8f, 1f)); // bright cyan reads as a lit screen against the dark interior

            // Ceiling pipe running along x near the -z wall (up at the ceiling, so it clears doorways).
            BuildProp(parent, name + "_Pipe", new Vector3(center.x, RoomH - 0.2f, center.z - hz + 0.4f),
                new Vector3(2f * hx - 0.8f, 0.18f, 0.18f), new Color(0.3f, 0.32f, 0.36f));

            foreach (int archetype in PickRoomDetailArchetypes(name))
                BuildRoomDetailArchetype(archetype, parent, name, center, halfExtents, accent);
        }

        // ---- Room-detail variety: deterministic archetype selection + the archetypes themselves. ----
        // Breaks up the identical base template above (survey's last-ranked immersion gap) without
        // touching it, by adding 1-3 extra renderers per room picked from a stable per-room-name hash.

        /// <summary>FNV-1a string hash. Deterministic across processes/sessions — unlike
        /// <see cref="string.GetHashCode"/>, which .NET randomizes per-process for security — so
        /// per-room prop selection stays reproducible across editor sessions and chapter rebuilds.</summary>
        internal static uint StableHash(string s)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in s)
                {
                    hash ^= c;
                    hash *= 16777619;
                }
                return hash;
            }
        }

        /// <summary>Picks 1-2 archetype indices (0=StackedCrates, 1=CableRun, 2=FloorGrate, 3=SignagePanel)
        /// for <paramref name="roomName"/>, deterministically from its <see cref="StableHash"/>. A second
        /// archetype is only added when the pair's combined renderer cost stays within the ~4-added-renderer
        /// budget (StackedCrates=3, CableRun=2, FloorGrate=1, SignagePanel=1).</summary>
        internal static int[] PickRoomDetailArchetypes(string roomName)
        {
            uint hash = StableHash(roomName);
            int primary = (int)(hash % 4);
            if (hash % 3 != 0) return new[] { primary };

            int second = (int)((hash / 3) % 4);
            if (second == primary) second = (second + 1) % 4;

            return RoomDetailArchetypeRendererCount(primary) + RoomDetailArchetypeRendererCount(second) <= 4
                ? new[] { primary, second }
                : new[] { primary };
        }

        /// <summary>Marker child name used to test whether <paramref name="archetype"/> was already built
        /// for a room — <see cref="ChapterRoomDetailsVarietyWirer"/> uses this for idempotence.</summary>
        internal static string RoomDetailArchetypeMarkerSuffix(int archetype) => archetype switch
        {
            0 => "_StackA",
            1 => "_CableA",
            2 => "_Grate",
            3 => "_Signage",
            _ => "_Unknown",
        };

        /// <summary>Renderer/GameObject count each archetype adds — BuildStackedCratesExtra (3),
        /// BuildCableRunExtra (2), BuildFloorGrateExtra (1), BuildSignagePanelExtra (1). Used both for the
        /// pairing budget above and by <see cref="ChapterRoomDetailsVarietyWirer"/> to report an accurate
        /// added-prop count.</summary>
        internal static int RoomDetailArchetypeRendererCount(int archetype) => archetype switch
        {
            0 => 3,
            1 => 2,
            2 => 1,
            3 => 1,
            _ => 0,
        };

        private static void BuildRoomDetailArchetype(int archetype, Transform parent, string name, Vector3 center, Vector2 halfExtents, Color accent)
        {
            switch (archetype)
            {
                case 0: BuildStackedCratesExtra(parent, name, center, halfExtents, accent); break;
                case 1: BuildCableRunExtra(parent, name, center, halfExtents, accent); break;
                case 2: BuildFloorGrateExtra(parent, name, center, halfExtents, accent); break;
                case 3: BuildSignagePanelExtra(parent, name, center, halfExtents, accent); break;
            }
        }

        /// <summary>Archetype 0: a second, smaller crate stack in the corner diagonally opposite the base
        /// cluster (base crates always sit -x/-z; door gaps are centred on a wall span, so every corner —
        /// including this one — stays clear). 3 offset, Y-rotated cubes for a "dumped in a hurry" look.</summary>
        private static void BuildStackedCratesExtra(Transform parent, string name, Vector3 center, Vector2 halfExtents, Color accent)
        {
            float hx = halfExtents.x, hz = halfExtents.y;
            float cx = center.x + hx - 0.85f;
            float cz = center.z + hz - 0.85f;
            var lo = new Color(accent.r * 0.75f, accent.g * 0.75f, accent.b * 0.75f);
            var hi = new Color(accent.r * 1.05f, accent.g * 1.05f, accent.b * 1.05f);
            BuildProp(parent, name + "_StackA", new Vector3(cx, 0.4f, cz), Quaternion.Euler(0f, 12f, 0f), new Vector3(0.8f, 0.8f, 0.8f), lo);
            BuildProp(parent, name + "_StackB", new Vector3(cx - 0.15f, 1.0f, cz + 0.1f), Quaternion.Euler(0f, -18f, 0f), new Vector3(0.55f, 0.5f, 0.55f), hi);
            BuildProp(parent, name + "_StackC", new Vector3(cx + 0.55f, 0.3f, cz - 0.15f), Quaternion.Euler(0f, 25f, 0f), new Vector3(0.5f, 0.6f, 0.5f), lo);
        }

        /// <summary>Archetype 1: a cable run — 2 thin cylinder segments hugging the -x wall near the
        /// ceiling (the ceiling pipe always runs along x near the -z wall, so this reads as a distinct
        /// conduit on a different wall) with a slight vertical offset between segments for a "sagging
        /// cable" look.</summary>
        private static void BuildCableRunExtra(Transform parent, string name, Vector3 center, Vector2 halfExtents, Color accent)
        {
            float hx = halfExtents.x, hz = halfExtents.y;
            float wallX = center.x - hx + 0.12f;
            float y = RoomH - 0.35f;
            float half = Mathf.Min(hz * 0.7f, hz - 0.3f);
            var cableColor = new Color(0.12f, 0.12f, 0.14f);
            var rot = Quaternion.Euler(90f, 0f, 0f); // cylinder's local +Y (length) axis -> world +Z
            BuildCylinderProp(parent, name + "_CableA", new Vector3(wallX, y, center.z - half * 0.5f), rot,
                new Vector3(0.05f, half * 0.5f, 0.05f), cableColor);
            BuildCylinderProp(parent, name + "_CableB", new Vector3(wallX, y - 0.08f, center.z + half * 0.5f), rot,
                new Vector3(0.05f, half * 0.5f, 0.05f), cableColor);
        }

        /// <summary>Archetype 2: a flat floor vent/grate panel against the +z wall (opposite the crate
        /// corner and console), darker than the room accent so it reads as recessed metal grating. Nearly
        /// flush with the floor (0.05m thick) so it never trips up locomotion.</summary>
        private static void BuildFloorGrateExtra(Transform parent, string name, Vector3 center, Vector2 halfExtents, Color accent)
        {
            float hx = halfExtents.x, hz = halfExtents.y;
            float gx = center.x - hx * 0.2f;
            float gz = center.z + hz - 0.6f;
            var grateColor = new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f);
            BuildProp(parent, name + "_Grate", new Vector3(gx, 0.03f, gz), new Vector3(1.1f, 0.05f, 0.7f), grateColor);
        }

        /// <summary>Archetype 3: a small bright wall placard near the +z wall (where side-room doors tend
        /// to land) at head height — same "bright reads-as-lit" trick as the console screen, so it reads
        /// as way-finding signage against the dark interior.</summary>
        private static void BuildSignagePanelExtra(Transform parent, string name, Vector3 center, Vector2 halfExtents, Color accent)
        {
            float hx = halfExtents.x, hz = halfExtents.y;
            float sx = center.x - hx * 0.4f;
            float sz = center.z + hz - 0.1f;
            BuildProp(parent, name + "_Signage", new Vector3(sx, 2.0f, sz), new Vector3(0.55f, 0.3f, 0.05f),
                new Color(1f, 0.7f, 0.15f));
        }

        // ---- Shared scene-building helpers called by the chapter builders. ----

        /// <summary>Replaces the command room's back wall (z=42) with a bridge windshield: a structural
        /// frame around a large transparent canopy, backed by a big emissive galaxy backdrop quad set
        /// further out so the player reads it as the view of Galaxy 1 outside the ship.</summary>
        private static void BuildCommandWindshield(Transform parent, Material galaxyMat)
        {
            var rootGo = new GameObject("Command_Windshield");
            var root = rootGo.transform;
            root.SetParent(parent, false);

            // Structural frame (keeps colliders so the player can't walk out): side pillars, top header,
            // bottom sill. This frames a window opening of roughly x[-8,8], y[0.6, RoomH-0.5].
            BuildWall(root, "Windshield_Frame_PillarW", new Vector3(-8f, RoomH/2f, 42f), new Vector3(0.4f, RoomH, 0.3f));
            BuildWall(root, "Windshield_Frame_PillarE", new Vector3(8f, RoomH/2f, 42f), new Vector3(0.4f, RoomH, 0.3f));
            BuildWall(root, "Windshield_Frame_Header", new Vector3(0f, RoomH - 0.25f, 42f), new Vector3(18f, 0.5f, 0.3f));
            BuildWall(root, "Windshield_Frame_Sill", new Vector3(0f, 0.3f, 42f), new Vector3(18f, 0.6f, 0.3f));
            // Vertical mullion dividers across the opening for a canopy look.
            BuildWall(root, "Windshield_Frame_MullionL", new Vector3(-2.7f, RoomH/2f, 42f), new Vector3(0.15f, RoomH, 0.25f));
            BuildWall(root, "Windshield_Frame_MullionR", new Vector3(2.7f, RoomH/2f, 42f), new Vector3(0.15f, RoomH, 0.25f));

            // Invisible barrier sealing the FULL opening (x[-9,9]) so the player can't walk out into
            // z>42 (the floor ends at z=42). We keep its collider but strip the renderer — a visible
            // opaque pane would hide the galaxy, and we want a clear "open canopy" view through to it.
            var glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glass.name = "Windshield_Glass";
            glass.transform.SetParent(root, false);
            glass.transform.localPosition = new Vector3(0f, RoomH/2f, 42f);
            glass.transform.localScale = new Vector3(18f, RoomH, 0.05f);
            Object.DestroyImmediate(glass.GetComponent<MeshRenderer>());

            // Galaxy backdrop: a large quad set further out (z=45), sized larger than the opening so its
            // edges hide behind the frame and it reads as distant. Purely visual (no collider).
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "GalaxyBackdrop";
            backdrop.transform.SetParent(root, false);
            backdrop.transform.localPosition = new Vector3(0f, RoomH/2f, 45f);
            backdrop.transform.localScale = new Vector3(22f, 6f, 0.1f);
            // A Unity Quad's front face normal points -Z; the player stands on the -Z side looking +Z,
            // so identity rotation already faces the lit side at the player. (No 180° flip — that would
            // turn the back-culled face toward the player and show nothing.)
            Object.DestroyImmediate(backdrop.GetComponent<Collider>());
            var backdropRenderer = backdrop.GetComponent<Renderer>();
            if (galaxyMat != null)
                backdropRenderer.sharedMaterial = galaxyMat;
            else
                TintShared(backdropRenderer, new Color(0.12f, 0.10f, 0.28f)); // deep-space fallback tint
        }

        /// <summary>
        /// A side room off the corridor: 3 outer walls + floor/ceiling + a back-wall label and a couple
        /// of props. The corridor-facing wall (with its door gap) is built by <see cref="BuildCorridorWall"/>.
        /// </summary>
        private static void BuildRoomShell(Transform parent, string name, float corridorX, bool west, float doorZ,
            float halfW, float depth, Color floorColor, Color ceilColor, string label, Color accent)
        {
            float sign = west ? -1f : 1f;
            float backX = sign * (corridorX + depth);
            float centerX = sign * (corridorX + depth / 2f);

            BuildFloorCeiling(parent, name, new Vector3(centerX, 0f, doorZ), new Vector3(depth, 0f, halfW * 2f), floorColor, ceilColor);
            BuildWall(parent, name + "_Back", new Vector3(backX, RoomH/2f, doorZ), new Vector3(0.2f, RoomH, halfW * 2f));
            BuildWall(parent, name + "_SideA", new Vector3(centerX, RoomH/2f, doorZ - halfW), new Vector3(depth, RoomH, 0.2f));
            BuildWall(parent, name + "_SideB", new Vector3(centerX, RoomH/2f, doorZ + halfW), new Vector3(depth, RoomH, 0.2f));

            // Label on the back wall, facing the room interior / corridor.
            var labelGo = new GameObject(name + "_Label");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.localPosition = new Vector3(backX - sign * 0.15f, 2.3f, doorZ);
            labelGo.transform.localRotation = Quaternion.Euler(0f, west ? -90f : 90f, 0f);
            labelGo.transform.localScale = Vector3.one * 0.05f;
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = label;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 48;
            tm.color = new Color(0.8f, 0.9f, 1f);

            // A couple of generic props tinted with the room accent.
            BuildProp(parent, name + "_Prop1", new Vector3(backX - sign * 0.6f, 0.5f, doorZ - halfW * 0.5f), new Vector3(0.8f, 1f, 0.8f), accent);
            BuildProp(parent, name + "_Prop2", new Vector3(backX - sign * 0.6f, 0.35f, doorZ + halfW * 0.5f), new Vector3(0.8f, 0.7f, 0.8f), accent);

            // Set-dressing to make the room feel lived-in. Floor center/half-extents match BuildFloorCeiling above.
            BuildRoomDetails(parent, name, new Vector3(centerX, 0f, doorZ), new Vector2(depth / 2f, halfW), accent);
        }

        /// <summary>Builds a small emissive "maintenance drone" primitive (no collider — purely cosmetic)
        /// parented under <paramref name="parent"/>. The caller attaches a mover (FloatingArrow / PlanetOrbit).
        /// Returns the drone GameObject.</summary>
        private static GameObject BuildShipDrone(Transform parent, string name, Vector3 localPos, Color glow)
        {
            var drone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            drone.name = name;
            drone.transform.SetParent(parent, false);
            drone.transform.localPosition = localPos;
            drone.transform.localScale = Vector3.one * 0.25f;
            TintShared(drone.GetComponent<Renderer>(), glow);
            Object.DestroyImmediate(drone.GetComponent<Collider>());

            // Tiny antenna nub for silhouette (also cosmetic, no collider).
            var antenna = GameObject.CreatePrimitive(PrimitiveType.Cube);
            antenna.name = "Antenna";
            antenna.transform.SetParent(drone.transform, false);
            antenna.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            antenna.transform.localScale = new Vector3(0.12f, 0.6f, 0.12f);
            TintShared(antenna.GetComponent<Renderer>(), glow);
            Object.DestroyImmediate(antenna.GetComponent<Collider>());

            return drone;
        }

        /// <summary>
        /// Scatters decorative character prefabs (baked by "Build Characters from Specs Folder" into
        /// Prefabs/Art/Generated/) at the given <paramref name="positions"/> for crowd flavour. Purely
        /// visual — no StoryNpc, dialogue, arrow, or combat. Which prefabs count as "decorative" is read
        /// from the Data/CharacterSpecs/Decorative specs; the named story cast is skipped so the crowd
        /// never duplicates them. Degrades gracefully (logs) if fewer prefabs are baked than positions
        /// requested. Returns the count actually placed.
        /// </summary>
        private static int PlaceDecorativeCrowd(Vector3[] positions)
        {
            const string GeneratedFolder = "Assets/Ronin7/Prefabs/Art/Generated";
            const string DecorativeSpecs = "Assets/Ronin7/Data/CharacterSpecs/Decorative";
            var skip = new HashSet<string> { "Kessler", "Khall", "Iris" }; // named cast placed by story scenes

            string osFolder = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), DecorativeSpecs);
            if (!System.IO.Directory.Exists(osFolder))
            {
                Debug.LogWarning($"[Space Samurai] No decorative specs at {DecorativeSpecs}; skipping crowd.");
                return 0;
            }

            int placed = 0;
            foreach (string file in System.IO.Directory.GetFiles(osFolder, "*.json", System.IO.SearchOption.TopDirectoryOnly))
            {
                if (placed >= positions.Length) break;

                ArtPrefabBuilder.CharacterSpec spec;
                try { spec = JsonUtility.FromJson<ArtPrefabBuilder.CharacterSpec>(System.IO.File.ReadAllText(file)); }
                catch { continue; }
                if (spec == null || string.IsNullOrEmpty(spec.name) || skip.Contains(spec.name)) continue;

                // The baker names the prefab from the sanitized spec name, not the JSON filename.
                string prefabPath = $"{GeneratedFolder}/{SanitizeAssetName(spec.name)}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) continue; // not baked yet

                var p = positions[placed];
                InstantiateNpc(prefabPath, new Vector3(p.x, 0f, p.z), $"Decorative_{spec.name}");
                placed++;
            }

            if (placed < positions.Length)
                Debug.LogWarning($"[Space Samurai] Placed {placed}/{positions.Length} decorative NPCs " +
                                 "— run 'Tools/Space Samurai/Art/Build Characters from Specs Folder' first to bake them all.");
            return placed;
        }

        // Mirrors the private Sanitize in ArtPrefabBuilder.CharacterGen so derived prefab paths match
        // the baker's output names exactly (letters/digits kept, everything else collapsed to '_').
        private static string SanitizeAssetName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "GeneratedCharacter";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            string r = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(r) ? "GeneratedCharacter" : r;
        }

        /// <summary>Instantiate an NPC prefab or fallback to a capsule placeholder.</summary>
        private static GameObject InstantiateNpc(string prefabPath, Vector3 position, string name)
        {
            var prefab = string.IsNullOrEmpty(prefabPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = name;
                instance.transform.position = position;
                return instance;
            }

            // Fallback: capsule placeholder.
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = name + "_Placeholder";
            capsule.transform.position = position;
            return capsule;
        }

        /// <summary>Builds a melee Dominion trooper using the DominionTrooper prefab, wired like BuildEnemy.</summary>
        private static Enemy BuildDominionEnemy(Vector3 position, Health playerHealth, EnemyDefinition def)
        {
            var root = new GameObject("DominionTrooper");
            root.transform.position = position;
            root.AddComponent<Health>();

            GameObject bodyGo;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtPrefabBuilder.DominionTrooperPrefabPath);
            if (prefab != null)
            {
                bodyGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            }
            else
            {
                bodyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                bodyGo.name = "Body";
                bodyGo.transform.SetParent(root.transform, false);
                bodyGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                bodyGo.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            }

            var bodyRenderer = bodyGo.GetComponent<Renderer>();
            var armR = bodyGo.transform.Find("ArmR");
            var bladeTip = bodyGo.transform.Find("ArmR/Sword/Blade/BladeTip");
            if (armR == null || bladeTip == null)
            {
                var armRGo = new GameObject("ArmR");
                armRGo.transform.SetParent(bodyGo.transform, false);
                armRGo.transform.localPosition = new Vector3(0.28f, 0.95f, 0f);
                var swordGo = new GameObject("Sword");
                swordGo.transform.SetParent(armRGo.transform, false);
                var bladeGo = new GameObject("Blade");
                bladeGo.transform.SetParent(swordGo.transform, false);
                var tipGo = new GameObject("BladeTip");
                tipGo.transform.SetParent(bladeGo.transform, false);
                tipGo.transform.localPosition = new Vector3(0f, 0f, 0.5f);
                armR = armRGo.transform;
                bladeTip = tipGo.transform;
            }

            var enemy = root.AddComponent<Enemy>();
            var so = new SerializedObject(enemy);
            SetObjectRef(so, "definition", def);
            SetObjectRef(so, "weapon", armR);
            SetObjectRef(so, "bladeTip", bladeTip);
            SetObjectRef(so, "bodyRenderer", bodyRenderer);
            SetObjectRef(so, "target", playerHealth);
            so.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        /// <summary>Build a dialogue player GameObject with a TextMesh + Panel + AudioSource, populated
        /// from an explicit line array. Voice clips are wired only when <paramref name="clipSetId"/> is
        /// non-null (the clip-name pattern is keyed by the set id, with the given <paramref name="clipPrefix"/>).
        /// When <paramref name="advanceRef"/> is provided, each line waits for Y (Left Hand/Talk) to advance.</summary>
        private static DialoguePlayer BuildDialoguePlayer(string name, Vector3 position, DialogueLine[] lines,
            InputActionReference advanceRef = null, string clipSetId = null, string clipPrefix = "ep01")
        {
            var go = new GameObject(name);
            go.transform.position = position;

            // TextMesh child for the dialogue text.
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 1.5f, 0.5f);
            textGo.transform.localScale = Vector3.one * 0.01f;
            var textMesh = textGo.AddComponent<TextMesh>();
            textMesh.text = "";
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 64;
            textMesh.color = new Color(0.9f, 0.85f, 0.7f);

            // Panel background (a thin quad behind the text).
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(go.transform, false);
            panelGo.transform.localPosition = new Vector3(0f, 1.5f, 0.45f);
            var panelVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panelVisual.name = "Background";
            panelVisual.transform.SetParent(panelGo.transform, false);
            panelVisual.transform.localScale = new Vector3(3f, 0.8f, 0.1f);
            TintShared(panelVisual.GetComponent<Renderer>(), new Color(0.05f, 0.05f, 0.08f, 0.9f));
            Object.DestroyImmediate(panelVisual.GetComponent<Collider>());

            // AudioSource for clip playback.
            var audioSource = go.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            // DialoguePlayer component.
            var dialoguePlayer = go.AddComponent<DialoguePlayer>();
            var dpSo = new SerializedObject(dialoguePlayer);

            // Populate the lines array and wire voice clips.
            var linesProp = dpSo.FindProperty("lines");
            linesProp.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                var el = linesProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("speaker").stringValue = lines[i].speaker;
                el.FindPropertyRelative("text").stringValue = lines[i].text;
                el.FindPropertyRelative("seconds").floatValue = lines[i].seconds;

                // Attempt to load voice clip (only when a clip set id is supplied).
                if (!string.IsNullOrEmpty(clipSetId))
                {
                    // Build clip name with configurable prefix: {clipPrefix}_{setId}_{index:00}_{sanitizedSpeaker}
                    string sanitized = SanitizeSpeaker(lines[i].speaker);
                    string clipName = $"{clipPrefix}_{clipSetId}_{i:00}_{sanitized}";
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Ronin7/Audio/Voice/{clipName}.wav");
                    if (clip == null)
                        clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Ronin7/Audio/Voice/{clipName}.mp3");
                    if (clip == null)
                        Debug.LogWarning($"[Dialogue] Missing voice clip: {clipName}");
                    else
                        el.FindPropertyRelative("clip").objectReferenceValue = clip;
                }
            }

            SetObjectRef(dpSo, "textMesh", textMesh);
            SetObjectRef(dpSo, "panelRoot", panelGo);
            SetObjectRef(dpSo, "audioSource", audioSource);
            if (advanceRef != null) SetObjectRef(dpSo, "advanceAction", advanceRef);
            dpSo.ApplyModifiedPropertiesWithoutUndo();

            return dialoguePlayer;
        }

        /// <summary>Sanitize a speaker name for clip naming: lowercase, strip non-alphanumeric.</summary>
        private static string SanitizeSpeaker(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return "unknown";
            var sb = new System.Text.StringBuilder();
            foreach (char c in speaker)
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        /// <summary>
        /// Builds an UNLIT (self-luminous) URP material for decorative visuals that must read uniformly
        /// bright regardless of scene lighting (glow props, starfields, holo panes).
        /// </summary>
        private static Material MakeUnlitMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.color = color;
            return mat;
        }

        /// <summary>
        /// Builds a faint transparent "glass" material (URP Unlit, alpha-blended) for canopy/pane
        /// meshes. Configured for the transparent surface path so the low-alpha tint reads through
        /// clearly. No collider is attached to glass meshes.
        /// </summary>
        private static Material MakeGlassMaterial(Color color, float alpha)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            color.a = alpha;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.color = color;
            mat.SetFloat("_Surface", 1f);   // 0 = Opaque, 1 = Transparent
            mat.SetFloat("_Blend", 0f);     // 0 = Alpha blend
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        /// <summary>A worldspace "box" panel with a single button + a StoryTransition. Created active;
        /// the caller hides it and wires the button's onClick.</summary>
        private static GameObject BuildTransitionBox(string name, Vector3 position, string label,
            out Button button, out StoryTransition transition)
        {
            var canvasGo = new GameObject(name);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600f, 200f);
            rt.localScale = Vector3.one * 0.001f;
            rt.position = position;
            rt.rotation = Quaternion.Euler(0f, 180f, 0f); // face -z, toward the approaching player

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.09f, 0.85f);

            transition = canvasGo.AddComponent<StoryTransition>();
            button = MenuMakeButton(rt, label, Vector2.zero);
            return canvasGo;
        }

        /// <summary>A stylised cyan data projection (the hacked-terminal hologram).</summary>
        private static GameObject BuildHologram(Transform parent, Vector3 localPosition)
        {
            var root = new GameObject("Hologram");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            var cyan = new Color(0.4f, 0.95f, 1f);
            var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Projection";
            column.transform.SetParent(root.transform, false);
            column.transform.localScale = new Vector3(0.5f, 0.6f, 0.5f);
            TintShared(column.GetComponent<Renderer>(), cyan);
            Object.DestroyImmediate(column.GetComponent<Collider>());

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Node";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            head.transform.localScale = Vector3.one * 0.4f;
            TintShared(head.GetComponent<Renderer>(), cyan);
            Object.DestroyImmediate(head.GetComponent<Collider>());

            root.AddComponent<FloatingArrow>(); // reuse the slow spin/bob for a holographic shimmer
            return root;
        }

        /// <summary>Builds an EnemyWaveSpawner with a trigger point, wave sting SFX, audio source, and
        /// the given waves (one enemy-health list + optional bark per wave).</summary>
        private static EnemyWaveSpawner BuildWaveSpawner(string name, Vector3 triggerPos, float triggerRadius,
            List<List<Health>> waves, DialoguePlayer[] barks)
        {
            var spawnerGo = new GameObject(name);
            var spawner = spawnerGo.AddComponent<EnemyWaveSpawner>();
            var triggerGo = new GameObject(name + "_Trigger");
            triggerGo.transform.position = triggerPos;

            var so = new SerializedObject(spawner);
            SetObjectRef(so, "triggerPoint", triggerGo.transform);
            so.FindProperty("triggerRadius").floatValue = triggerRadius;
            var waveSting = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/WaveAlarm.wav");
            if (waveSting != null)
                so.FindProperty("waveSting").objectReferenceValue = waveSting;

            var wavesProp = so.FindProperty("waves");
            wavesProp.arraySize = waves.Count;
            for (int w = 0; w < waves.Count; w++)
            {
                var wave = wavesProp.GetArrayElementAtIndex(w);
                var enemiesProp = wave.FindPropertyRelative("enemies");
                enemiesProp.arraySize = waves[w].Count;
                for (int i = 0; i < waves[w].Count; i++)
                    enemiesProp.GetArrayElementAtIndex(i).objectReferenceValue = waves[w][i];
                if (barks != null && w < barks.Length && barks[w] != null)
                    wave.FindPropertyRelative("bark").objectReferenceValue = barks[w];
            }

            var audio = spawnerGo.AddComponent<AudioSource>();
            audio.spatialBlend = 0f;
            audio.playOnAwake = false;
            SetObjectRef(so, "audioSource", audio);
            so.ApplyModifiedPropertiesWithoutUndo();
            return spawner;
        }

        // ---- Mission-step authoring helpers (SerializedObject wiring). ----

        private static void AuthorDialogueStep(SerializedProperty steps, int i, string label, DialoguePlayer dialogue)
        {
            var s = steps.GetArrayElementAtIndex(i);
            s.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s.FindPropertyRelative("label").stringValue = label;
            s.FindPropertyRelative("dialogue").objectReferenceValue = dialogue;
        }

        private static void AuthorPromptStep(SerializedProperty steps, int i, string label, GameObject promptObject)
        {
            var s = steps.GetArrayElementAtIndex(i);
            s.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s.FindPropertyRelative("label").stringValue = label;
            s.FindPropertyRelative("promptObject").objectReferenceValue = promptObject;
        }

        private static void AuthorReachStep(SerializedProperty steps, int i, string label, Transform reachPoint, float radius)
        {
            var s = steps.GetArrayElementAtIndex(i);
            s.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s.FindPropertyRelative("label").stringValue = label;
            s.FindPropertyRelative("reachPoint").objectReferenceValue = reachPoint;
            s.FindPropertyRelative("reachRadius").floatValue = radius;
        }

        private static void AuthorTriggerStep(SerializedProperty steps, int i, string label, params GameObject[] objs)
        {
            var s = steps.GetArrayElementAtIndex(i);
            s.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s.FindPropertyRelative("label").stringValue = label;
            var t = s.FindPropertyRelative("triggerObjects");
            t.arraySize = objs.Length;
            for (int k = 0; k < objs.Length; k++)
                t.GetArrayElementAtIndex(k).objectReferenceValue = objs[k];
        }

        private static void AuthorDefeatStep(SerializedProperty steps, int i, string label, List<Object> enemyHealths)
        {
            var s = steps.GetArrayElementAtIndex(i);
            s.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatEnemies;
            s.FindPropertyRelative("label").stringValue = label;
            var en = s.FindPropertyRelative("enemies");
            en.arraySize = enemyHealths.Count;
            for (int k = 0; k < enemyHealths.Count; k++)
                en.GetArrayElementAtIndex(k).objectReferenceValue = enemyHealths[k];
        }

        /// <summary>
        /// Attaches every permanent player ability shipped so far onto the rig root, each self-gating in
        /// its own Awake on <c>CampaignState.HasAbility</c> so it is inert until unlocked. Chapter
        /// builders from Ch7 on call this instead of hand-adding ability components, so a later chapter
        /// can't silently drop an ability the player already earned (the abilities live on the rig in
        /// EVERY scene from their unlock chapter onward; the unlock itself stays per-chapter via
        /// <c>AbilityGranter</c>). Grow this list as each ability ships:
        /// weakpoint-sight (Ch7) → Overdrive (Ch9) → Phase-step (Ch10) → Unbroken (Ch11) → Mirror (Ch12).
        /// </summary>
        private static void AttachPlayerAbilities(GameObject rig, Object[] refs)
        {
            var combatMods = rig.GetComponent<PlayerCombatModifiers>();
            if (combatMods == null) combatMods = rig.AddComponent<PlayerCombatModifiers>();

            // Ch7 — weakpoint-sight (X-button Hold toggle; 2x damage + weakpoint markers).
            if (rig.GetComponent<WeakpointSight>() == null)
            {
                var weakpointSight = rig.AddComponent<WeakpointSight>();
                var wpSo = new SerializedObject(weakpointSight);
                SetObjectRef(wpSo, "toggleAction", FindRef(refs, "Left Hand", "Toggle Weakpoint Sight"));
                SetObjectRef(wpSo, "combatModifiers", combatMods);
                wpSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Ch9 — Overdrive (right-A Hold(0.4s) activation once the sword-hit meter is full; time-slow
            // burst). Wrist meter anchors to the right hand (the same hand that activates it).
            if (rig.GetComponent<OverdriveController>() == null)
            {
                var overdrive = rig.AddComponent<OverdriveController>();
                var odSo = new SerializedObject(overdrive);
                SetObjectRef(odSo, "activateAction", FindRef(refs, "Right Hand", "Activate Overdrive"));
                var vrRig = rig.GetComponent<VRRig>();
                if (vrRig != null) SetObjectRef(odSo, "anchor", vrRig.RightHand);
                odSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Ch10 — Phase-step (right-secondaryButton Tap activation; shares the button with Recenter's
            // Hold(0.35s) — tap blinks, hold recenters). Direction is the head's horizontal forward.
            if (rig.GetComponent<PhaseStepController>() == null)
            {
                var phaseStep = rig.AddComponent<PhaseStepController>();
                var psSo = new SerializedObject(phaseStep);
                SetObjectRef(psSo, "activateAction", FindRef(refs, "Right Hand", "Phase Step"));
                var vrRig = rig.GetComponent<VRRig>();
                if (vrRig != null) SetObjectRef(psSo, "headTransform", vrRig.Head);
                psSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Ch11 — Unbroken (passive: no input action, no toggle). Registers itself as the rig's
            // Health.DeathInterceptor on enable; survives one otherwise-lethal blow per life.
            if (rig.GetComponent<UnbrokenWard>() == null)
            {
                rig.AddComponent<UnbrokenWard>();
                // No SerializedObject wiring needed: UnbrokenWard resolves its Health via
                // GetComponent<Health>() on the same rig GameObject when its own serialized field is
                // left unset (see UnbrokenWard.Awake).
            }

            // Ch12 — Mirror (right-secondaryButton Hold(0.4s) activation; shares the button with
            // Phase-step's Tap and the dead Recenter binding — see MirrorSummonController's class
            // summary). Summons a ghost-tinted AllyCombatant double for a limited duration. Direction is
            // the head's horizontal forward, same idiom as Phase-step.
            if (rig.GetComponent<MirrorSummonController>() == null)
            {
                var mirror = rig.AddComponent<MirrorSummonController>();
                var mirrorSo = new SerializedObject(mirror);
                SetObjectRef(mirrorSo, "activateAction", FindRef(refs, "Right Hand", "Summon Mirror"));
                var vrRig = rig.GetComponent<VRRig>();
                if (vrRig != null) SetObjectRef(mirrorSo, "headTransform", vrRig.Head);
                mirrorSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
