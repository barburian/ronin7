using System.Collections.Generic;
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
    /// Chapter-agnostic scene-building helpers shared by every chapter/episode builder (geometry,
    /// doors, NPCs, dialogue, mission-step authoring). Moved out of <c>Ep01Builder</c> /
    /// <c>Ep03Builder</c> / <c>Chapter1Builder</c> so new chapter builders don't have to depend on a
    /// specific episode's file. Lives in the same <see cref="XRRigBuilder"/> partial class, so every
    /// builder file keeps calling these exactly as before.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const float RoomH = 3.6f;

        // Moved from Ep01Builder (deleted) — still needed by XRRigBuilder's boot-scene build list and
        // by Galaxy1Builder's landable/completion checks.
        private const string Ep01ShipScenePath = SceneFolder + "/Galaxy1_EP01_Ship.unity";
        private const string Galaxy1Ep01PlanetScenePath = SceneFolder + "/Galaxy1_EP01_Planet.unity";
        private static readonly string Galaxy1Ep01PlanetSceneName = System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep01PlanetScenePath);
        private const string Galaxy1Ep01HideoutScenePath = SceneFolder + "/Galaxy1_EP01_Hideout.unity";

        // Named-cast prefabs baked from Data/CharacterSpecs by "Build Characters from Specs Folder".
        // These are FEET-pivot (parts authored from y≈0 up), so place them at floor height (y=0) —
        // unlike ArtPrefabBuilder.KesslerPrefabPath, whose root is a body capsule placed at y=1.
        // Moved from Ep01Builder (deleted) — still used by several later episode builders.
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

        private static void BuildProp(Transform parent, string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = LowPolyMeshes.ForType(PrimitiveType.Cube);
            TintShared(go.GetComponent<Renderer>(), color);
        }

        /// <summary>Scatters a few cheap, shared-material detail props (crates, a console with an emissive
        /// screen, a ceiling pipe) inside a room to make it feel lived-in. Kept light for
        /// Quest. <paramref name="center"/> is the room floor-center; <paramref name="halfExtents"/> is the
        /// half-size (x,z) of the usable floor; <paramref name="accent"/> tints the props.</summary>
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
        }

        // ---- Moved from Ep01Builder (deleted) — still called by Chapter1Builder / Galaxy1Builder. ----

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

        /// <summary>Build a dialogue player GameObject with a TextMesh + Panel + AudioSource.
        /// Loads lines from Ep01Lines.Get(setId) and attempts to wire voice clips from
        /// Assets/Ronin7/Audio/Voice/{clipName}.wav/.mp3. When <paramref name="advanceRef"/>
        /// is provided, each line waits for Y (Left Hand/Talk) to advance.</summary>
        private static DialoguePlayer BuildDialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep01Lines.Get(setId), advanceRef, setId);
        }

        /// <summary>Overload taking an explicit line array — used by builders that author lines inline
        /// (e.g. the Corsair greeting). Voice clips are wired only when <paramref name="clipSetId"/> is
        /// non-null (the clip-name pattern is keyed by the set id). The <paramref name="clipPrefix"/>
        /// defaults to "ep01" but can be overridden for other episodes (e.g. "ep02").</summary>
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
                    string sanitized = Ep01Lines.Sanitize(lines[i].speaker);
                    string clipName = $"{clipPrefix}_{clipSetId}_{i:00}_{sanitized}";
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Ronin7/Audio/Voice/{clipName}.wav");
                    if (clip == null)
                        clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Ronin7/Audio/Voice/{clipName}.mp3");
                    if (clip == null)
                        Debug.LogWarning($"[Ep01] Missing voice clip: {clipName}");
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

        // ---- Mission-step authoring helpers (mirror Ep01Builder's SerializedObject wiring). ----

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

        // Moved from Ep03Builder (deleted) — ScenePath consts still needed by Galaxy1Builder's
        // EnsureScenesInBuild call; Galaxy1Ep03HaulerSceneName still used by Galaxy1Builder's
        // gating/landable-station checks.
        private const string Galaxy1Ep03HaulerScenePath = SceneFolder + "/Galaxy1_EP03_Hauler.unity";
        private const string Galaxy1Ep03EngineRoomScenePath = SceneFolder + "/Galaxy1_EP03_EngineRoom.unity";
        private const string Galaxy1Ep03LotusScenePath = SceneFolder + "/Galaxy1_EP03_LotusStation.unity";
        private const string Galaxy1Ep03SanctuaryScenePath = SceneFolder + "/Galaxy1_EP03_Sanctuary.unity";

        private static readonly string Galaxy1Ep03HaulerSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep03HaulerScenePath);

        /// <summary>Builds a parented, initially-inactive pair of red alarm point lights (EP02 Core pattern).
        /// Moved from Ep03Builder (deleted) — still used by Ep04's (now also moved) archive-heart alarm,
        /// and by Ep05Builder, Ep05BuilderHub, Ep06BuilderVault.</summary>
        private static GameObject BuildEp03AlarmLights(Vector3 pos1, Vector3 pos2)
        {
            var alarmLightsGo = new GameObject("AlarmLights");
            foreach (var (pos, idx) in new[] { (pos1, 1), (pos2, 2) })
            {
                var lightGo = new GameObject($"AlarmLight{idx}");
                lightGo.transform.SetParent(alarmLightsGo.transform, false);
                lightGo.transform.position = pos;
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.3f, 0.2f);
                light.intensity = 2.0f;
                light.range = 8f;
                light.shadows = LightShadows.None;
            }
            alarmLightsGo.SetActive(false); // Activated by Trigger step.
            return alarmLightsGo;
        }

        // Moved from Ep04Builder (deleted) — BuildEp04JungleMoon/BuildEp04ArchiveRing/BuildEp04Ledger are
        // still called by Galaxy1Builder.BuildAllGalaxy1Scenes, and the scene-path/name consts below are
        // still used by Galaxy1Builder's EnsureScenesInBuild/gating/landable-station logic.
        private const string Galaxy1Ep04JungleMoonScenePath = SceneFolder + "/Galaxy1_EP04_JungleMoon.unity";
        private const string Galaxy1Ep04ArchiveRingScenePath = SceneFolder + "/Galaxy1_EP04_ArchiveRing.unity";
        private const string Galaxy1Ep04LedgerScenePath = SceneFolder + "/Galaxy1_EP04_Ledger.unity";

        private static readonly string Galaxy1Ep04JungleMoonSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep04JungleMoonScenePath);
        private static readonly string Galaxy1Ep04ArchiveRingSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep04ArchiveRingScenePath);
        private static readonly string Galaxy1Ep04LedgerSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep04LedgerScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP04 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep04" and loads lines from Ep04Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp04DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep04Lines.Get(setId), advanceRef, setId, clipPrefix: "ep04");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP04 Jungle Moon", priority = 76)]
        public static void BuildEp04JungleMoon()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Jungle moon exterior: warm-green daylight, lush foliage, decaying ruins.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.9f, 0.7f);
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.25f, 0.15f);

            // Jungle canopy fog: green exponential, low density.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.2f, 0.35f, 0.2f);
            RenderSettings.fogDensity = 0.03f;

            // Clearing accent lights.
            BuildAccentPointLight("ClearingLight1", new Vector3(-3f, 2.4f, 14f),
                new Color(0.8f, 1f, 0.6f), intensity: 1.6f, range: 14f);
            BuildAccentPointLight("ClearingLight2", new Vector3(3f, 2.4f, 14f),
                new Color(0.8f, 1f, 0.6f), intensity: 1.6f, range: 14f);

            // ---- Jungle moon exterior: ramp vigil area (z ~0-4) -> wreckage clearing (z ~8-16) ->
            // back to ship bridge nook (z ~2-8, side). Large ground plane (jungle-green, no ceiling).
            var exteriorGo = new GameObject("JungleExterior");
            var exterior = exteriorGo.transform;

            // Ground plane: large flat landscape (60x60, flattened cube).
            var groundPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            groundPlane.name = "GroundPlane";
            groundPlane.transform.SetParent(exterior, false);
            groundPlane.transform.localPosition = new Vector3(0f, -0.5f, 30f);
            groundPlane.transform.localScale = new Vector3(60f, 1f, 60f);
            TintShared(groundPlane.GetComponent<Renderer>(), new Color(0.15f, 0.28f, 0.12f));
            var groundCollider = groundPlane.GetComponent<Collider>();
            if (groundCollider != null) groundCollider.isTrigger = false;

            // Trees and jungle props scattered via BuildJungleProps.
            BuildJungleProps(exterior, 25f);

            // Corsair hint: grey BuildProp boxes forming a grounded ship hull + ramp.
            // Kept behind the player spawn (origin) so the rig never starts inside the hull collider.
            var shipBodyColor = new Color(0.5f, 0.5f, 0.52f);
            BuildProp(exterior, "ShipHull_Main", new Vector3(0f, 1.5f, -8f), new Vector3(6f, 3f, 10f), shipBodyColor);
            BuildProp(exterior, "ShipHull_Bridge", new Vector3(0f, 2.5f, -14f), new Vector3(4f, 2f, 4f), shipBodyColor);
            BuildProp(exterior, "RampBox", new Vector3(0f, 0.2f, -2.5f), new Vector3(3f, 0.4f, 3f), new Color(0.6f, 0.55f, 0.5f));

            // Landing pad prop: rust-metal color.
            var padColor = new Color(0.65f, 0.45f, 0.35f);
            BuildProp(exterior, "LandingPad", new Vector3(-8f, 0f, 20f), new Vector3(5f, 0.3f, 5f), padColor);

            // Wreckage props scattered in the clearing.
            BuildProp(exterior, "Wreckage1", new Vector3(5f, 0.5f, 12f), new Vector3(2f, 1.5f, 3f), padColor);
            BuildProp(exterior, "Wreckage2", new Vector3(-6f, 0.4f, 15f), new Vector3(3f, 1f, 2f), padColor);
            BuildProp(exterior, "Wreckage3", new Vector3(4f, 0.3f, 20f), new Vector3(1.5f, 0.8f, 2.5f), padColor);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 60f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Kessler near the ramp, beside the player spawn.
            var kesslerPos = new Vector3(1.5f, 1f, -1f);
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler_JungleMoon");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kesslerSo = new SerializedObject(kesslerNpc);
                kesslerSo.FindProperty("displayName").stringValue = "Kessler";
                kesslerSo.FindProperty("remote").boolValue = false;
                kesslerSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Alarm lights (attack ambiance, activated when scouts emerge). ----
            var alarmLightsGo = BuildEp03AlarmLights(new Vector3(-2f, 2.4f, 13f), new Vector3(2f, 2.4f, 15f));

            // ---- Dialogue Players ----
            var jungleVigilDialogue = BuildEp04DialoguePlayer("Dialogue_JungleVigil", kesslerPos, "jungle_vigil", talkRef);
            var jungleFightBarksDialogue = BuildEp04DialoguePlayer("Dialogue_JungleFightBarks", new Vector3(0f, 1.5f, 14f), "jungle_fight_barks");
            var jungleConfessionDialogue = BuildEp04DialoguePlayer("Dialogue_JungleConfession", new Vector3(0f, 1f, -1f), "jungle_confession", talkRef);

            // ---- Enemies: 4 Rustfang scouts (rust-orange tint). ----
            var rustOrange = new Color(0.75f, 0.4f, 0.15f);
            var scoutPositions = new Vector3[]
            {
                new Vector3(-2f, 0f, 11f),
                new Vector3(2f, 0f, 12f),
                new Vector3(-1f, 0f, 14f),
                new Vector3(1f, 0f, 16f)
            };
            var scoutHealths = new List<Health>();
            foreach (var pos in scoutPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, rustOrange);
                enemy.gameObject.SetActive(false);
                scoutHealths.Add(enemy.GetComponent<Health>());
            }

            var scoutWaveSpawner = BuildWaveSpawner("ScoutWaveSpawner", new Vector3(0f, 1f, 11f), 3f,
                new List<List<Health>> { scoutHealths }, new[] { jungleFightBarksDialogue });

            // Reach triggers.
            var clearingReachGo = new GameObject("ClearingReachPoint");
            clearingReachGo.transform.position = new Vector3(0f, 1f, 14f);
            var nookReachGo = new GameObject("NookReachPoint");
            nookReachGo.transform.position = new Vector3(0f, 1f, -1f);

            // Transition box: "LAUNCH FOR THE VEILED REACHES" wired to ReturnToSpace.
            var launchBoxGo = BuildTransitionBox("LaunchToVeiledReachesBox", new Vector3(0f, 1.2f, 1.5f), "LAUNCH FOR THE VEILED REACHES",
                out var launchBtn, out var launchTransition);
            UnityEventTools.AddPersistentListener(launchBtn.onClick,
                new UnityEngine.Events.UnityAction(launchTransition.ReturnToSpace));
            launchBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 7;

            // Step 0: Dialogue jungle_vigil (Kessler's ramp vigil).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Ramp Vigil";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = jungleVigilDialogue;

            // Step 1: ReachTrigger — wreckage clearing.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s1.FindPropertyRelative("label").stringValue = "ReachTrigger: Clearing";
            s1.FindPropertyRelative("reachPoint").objectReferenceValue = clearingReachGo.transform;
            s1.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 2: Trigger — alarm lights (scouts emerge).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s2.FindPropertyRelative("label").stringValue = "Trigger: Scout Alarm";
            var t2 = s2.FindPropertyRelative("triggerObjects");
            t2.arraySize = 1;
            t2.GetArrayElementAtIndex(0).objectReferenceValue = alarmLightsGo;

            // Step 3: DefeatWaves — 4 Rustfang scouts.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s3.FindPropertyRelative("label").stringValue = "DefeatWaves: 4 Rustfang Scouts";
            s3.FindPropertyRelative("waveSpawner").objectReferenceValue = scoutWaveSpawner;

            // Step 4: ReachTrigger — ship bridge nook.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s4.FindPropertyRelative("label").stringValue = "ReachTrigger: Bridge Nook";
            s4.FindPropertyRelative("reachPoint").objectReferenceValue = nookReachGo.transform;
            s4.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 5: Dialogue jungle_confession (Kessler's confession).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s5.FindPropertyRelative("label").stringValue = "Dialogue: The Confession";
            s5.FindPropertyRelative("dialogue").objectReferenceValue = jungleConfessionDialogue;

            // Step 6: Prompt — launch to the Veiled Reaches.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s6.FindPropertyRelative("label").stringValue = "Prompt: Launch to Veiled Reaches";
            s6.FindPropertyRelative("promptObject").objectReferenceValue = launchBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep04JungleMoonScenePath);
            EnsureScenesInBuild(Galaxy1Ep04JungleMoonScenePath);

            Debug.Log($"[Space Samurai] EP04 Jungle Moon scene built at {Galaxy1Ep04JungleMoonScenePath}. " +
                      "Layout: exterior ground plane with jungle props, ramp vigil area (Kessler) → wreckage clearing (4 Rustfang scouts) → ship bridge nook. " +
                      "Green exponential fog + warm-green daylight. " +
                      "7 steps: jungle_vigil → reach clearing → trigger alarms → defeat 4 scouts + barks → reach nook → jungle_confession → launch to Veiled Reaches.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP04 Archive Ring", priority = 77)]
        public static void BuildEp04ArchiveRing()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Archive ring interior: amber solar-flare, cold-grey mood, ancient tech.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.8f, 0.75f, 0.65f);
            light.intensity = 0.9f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.12f, 0.1f);

            // Amber solar-flare fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.28f, 0.18f);
            RenderSettings.fogDensity = 0.025f;

            // Amber accent lights.
            BuildAccentPointLight("RingLight1", new Vector3(-3f, 2.6f, 12f),
                new Color(1f, 0.75f, 0.4f), intensity: 1.7f, range: 14f);
            BuildAccentPointLight("RingLight2", new Vector3(3f, 2.6f, 20f),
                new Color(1f, 0.7f, 0.35f), intensity: 1.6f, range: 14f);
            BuildAccentPointLight("NaveLight", new Vector3(0f, 2.6f, 28f),
                new Color(0.9f, 0.75f, 0.5f), intensity: 1.8f, range: 16f);

            // ---- Archive ring: airlock (z 0-6) -> outer corridor (z 6-18) -> central nave (z 18-38) ->
            // heart-gate alcove (z 38-44).
            var interiorGo = new GameObject("ArchiveInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.2f, 0.2f, 0.22f);
            var ceilColor = new Color(0.12f, 0.12f, 0.14f);

            // Airlock: x[-3,3], z[0,6].
            BuildFloorCeiling(interior, "Airlock", new Vector3(0f, 0f, 3f), new Vector3(6f, 0f, 6f), floorColor, ceilColor);
            BuildWall(interior, "Airlock_WallW", new Vector3(-3f, 1.5f, 3f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "Airlock_WallE", new Vector3(3f, 1.5f, 3f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "Airlock_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(6f, 3f, 0.2f));
            BuildDoorwayWall(interior, "Airlock_WallBack", new Vector3(0f, 1.5f, 6f), 6f, true, 2.4f);

            // Outer corridor: x[-3.5,3.5], z[6,18].
            BuildFloorCeiling(interior, "Corridor", new Vector3(0f, 0f, 12f), new Vector3(7f, 0f, 12f), floorColor, ceilColor);
            BuildCorridorWall(interior, "Corridor_WallW", -3.5f, 6f, 18f, new float[0], 2.4f);
            BuildCorridorWall(interior, "Corridor_WallE", 3.5f, 6f, 18f, new float[0], 2.4f);

            // Central nave: x[-6,6], z[18,38], with raised catwalk floor strips (y~0.4).
            BuildFloorCeiling(interior, "NaveFloor", new Vector3(0f, 0f, 28f), new Vector3(12f, 0f, 20f),
                new Color(0.15f, 0.15f, 0.17f), ceilColor);
            BuildWall(interior, "Nave_WallW", new Vector3(-6f, 1.5f, 28f), new Vector3(0.2f, 3f, 20f));
            BuildWall(interior, "Nave_WallE", new Vector3(6f, 1.5f, 28f), new Vector3(0.2f, 3f, 20f));

            // Catwalk strips (raised platforms where Sentries stand).
            var catwalkColor = new Color(0.25f, 0.25f, 0.27f);
            BuildProp(interior, "CatwalkW", new Vector3(-4f, 0.4f, 22f), new Vector3(1.5f, 0.2f, 8f), catwalkColor);
            BuildProp(interior, "CatwalkCenter", new Vector3(0f, 0.4f, 28f), new Vector3(2f, 0.2f, 8f), catwalkColor);
            BuildProp(interior, "CatwalkE", new Vector3(4f, 0.4f, 32f), new Vector3(1.5f, 0.2f, 8f), catwalkColor);

            // Heart-gate alcove: x[-4,4], z[38,44].
            BuildFloorCeiling(interior, "HeartAlcove", new Vector3(0f, 0f, 41f), new Vector3(8f, 0f, 6f), floorColor, ceilColor);
            BuildWall(interior, "HeartAlcove_WallW", new Vector3(-4f, 1.5f, 41f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "HeartAlcove_WallE", new Vector3(4f, 1.5f, 41f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "HeartAlcove_WallBack", new Vector3(0f, 1.5f, 44f), new Vector3(8f, 3f, 0.2f));

            // Sliding door "RingDoor" at z=6.
            var doorSlideClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ronin7/Audio/DoorSlide.wav");
            var ringDoor = BuildSlidingDoor(interior, "RingDoor", new Vector3(0f, 0f, 6f), 2.4f, true, startLocked: false);
            WireDoorAudio(ringDoor, doorSlideClip);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 60f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- Dialogue Players ----
            var archiveGhostDialogue = BuildEp04DialoguePlayer("Dialogue_ArchiveGhost", new Vector3(0f, 1.5f, 2f), "archive_ghost");
            var archiveSentryBarksDialogue = BuildEp04DialoguePlayer("Dialogue_ArchiveSentryBarks", new Vector3(0f, 1.5f, 24f), "archive_sentry_barks");
            var archiveWelcomeDialogue = BuildEp04DialoguePlayer("Dialogue_ArchiveWelcome", new Vector3(0f, 1.5f, 41f), "archive_welcome", talkRef);

            // ---- Heart-of-the-archive dialogue (folded in from the merged Archive Heart scene): Khall's video
            // log + the blade-truth reveal play in the nave; the encrypted-files/alarm/escape beats in the alcove. ----
            var heartKhallLogDialogue = BuildEp04DialoguePlayer("Dialogue_KhallLog", new Vector3(0f, 1.5f, 30f), "heart_khall_log");
            var heartBladeTruthDialogue = BuildEp04DialoguePlayer("Dialogue_BladeTruth", new Vector3(0f, 1.5f, 30f), "heart_blade_truth", talkRef);
            var heartEnforcerChallengeDialogue = BuildEp04DialoguePlayer("Dialogue_EnforcerChallenge", new Vector3(0f, 1.5f, 30f), "heart_enforcer_challenge");
            var heartEnforcerAfterDialogue = BuildEp04DialoguePlayer("Dialogue_EnforcerAfter", new Vector3(0f, 1.5f, 32f), "heart_enforcer_after");
            var heartFilesDialogue = BuildEp04DialoguePlayer("Dialogue_Files", new Vector3(0f, 1f, 41f), "heart_files", talkRef);
            var heartAlarmDialogue = BuildEp04DialoguePlayer("Dialogue_HeartAlarm", new Vector3(0f, 1.5f, 41f), "heart_alarm");
            var heartEscapeDialogue = BuildEp04DialoguePlayer("Dialogue_EscapeRun", new Vector3(0f, 1.5f, 41f), "heart_escape");

            // ---- Enemies: 2 Recon Sentries (pale-grey tint) — trimmed from 3 in the consolidation pass so the
            // merged scene's nave skirmish stays light ahead of the Enforcer duel. ----
            var sentryGrey = new Color(0.72f, 0.75f, 0.8f);
            var sentryPositions = new Vector3[]
            {
                new Vector3(-4f, 0.6f, 22f),
                new Vector3(4f, 0.6f, 30f)
            };
            var sentryHealths = new List<Health>();
            foreach (var pos in sentryPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, sentryGrey);
                enemy.gameObject.SetActive(false);
                sentryHealths.Add(enemy.GetComponent<Health>());
            }

            var sentryWaveSpawner = BuildWaveSpawner("SentryWaveSpawner", new Vector3(0f, 1f, 20f), 3f,
                new List<List<Health>> { sentryHealths }, new[] { archiveSentryBarksDialogue });

            // ---- Enemy: Dominion Enforcer duel (silver, 3.5x health) — folded in from the merged Archive Heart;
            // the episode's climactic guardian fight, staged in the nave. ----
            var enforcerSilver = new Color(0.8f, 0.82f, 0.88f);
            var enforcer = BuildDominionEnemy(new Vector3(0f, 0f, 30f), playerHealth, enemyDef);
            var enforcerRenderer = enforcer.GetComponent<Renderer>();
            if (enforcerRenderer != null) TintShared(enforcerRenderer, enforcerSilver);
            var enforcerHealth = enforcer.GetComponent<Health>();
            if (enforcerHealth != null)
            {
                var ehSo = new SerializedObject(enforcerHealth);
                ehSo.FindProperty("maxHealth").floatValue = ehSo.FindProperty("maxHealth").floatValue * 3.5f;
                ehSo.ApplyModifiedPropertiesWithoutUndo();
            }
            enforcer.gameObject.SetActive(false);

            var enforcerWaveSpawner = BuildWaveSpawner("EnforcerWaveSpawner", new Vector3(0f, 1f, 28f), 3f,
                new List<List<Health>> { new List<Health> { enforcerHealth } }, new[] { heartEnforcerChallengeDialogue });

            // ---- Heart props (folded in): Khall's video screen on the nave back wall, the records terminal in
            // the alcove, and red alarm lights for the escape beat. ----
            var videoQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            videoQuad.name = "VideoScreen";
            videoQuad.transform.SetParent(interior, false);
            videoQuad.transform.localPosition = new Vector3(0f, 1.8f, 37.8f);
            videoQuad.transform.localScale = new Vector3(5f, 3f, 1f);
            TintShared(videoQuad.GetComponent<Renderer>(), new Color(0.2f, 0.15f, 0.1f, 0.7f));
            var videoCollider = videoQuad.GetComponent<Collider>();
            if (videoCollider != null) Object.DestroyImmediate(videoCollider);

            BuildProp(interior, "RecordsTerminal", new Vector3(0f, 0.7f, 43f), new Vector3(1.4f, 1.4f, 0.6f), new Color(0.1f, 0.12f, 0.15f));

            var heartAlarmLightsGo = BuildEp03AlarmLights(new Vector3(-3f, 2.4f, 40f), new Vector3(3f, 2.4f, 42f));

            // Reach triggers.
            var naveReachGo = new GameObject("NaveReachPoint");
            naveReachGo.transform.position = new Vector3(0f, 1f, 24f);
            var terminalReachGo = new GameObject("TerminalReachPoint");
            terminalReachGo.transform.position = new Vector3(0f, 1f, 41f);

            // Transition box: "ESCAPE TO THE CORSAIR" (Archive Heart merged into this scene — the reveal +
            // Enforcer duel play here now, so the ring chains straight to the Ledger finale scene).
            var heartBoxGo = BuildTransitionBox("EscapeToCorsairBox", new Vector3(0f, 1.2f, 43.2f), "ESCAPE TO THE CORSAIR",
                out var heartBtn, out var heartTransition);
            var htSo = new SerializedObject(heartTransition);
            htSo.FindProperty("onFootScene").stringValue = Galaxy1Ep04LedgerSceneName;
            htSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(heartBtn.onClick,
                new UnityEngine.Events.UnityAction(heartTransition.LoadOnFootScene));
            heartBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 15;

            // Step 0: Dialogue archive_ghost (the ghost signal, auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Ghost Signal";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = archiveGhostDialogue;

            // Step 1: Trigger — open RingDoor.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s1.FindPropertyRelative("label").stringValue = "Trigger: Open Ring Door";
            var t1 = s1.FindPropertyRelative("triggerObjects");
            t1.arraySize = 1;
            t1.GetArrayElementAtIndex(0).objectReferenceValue = ringDoor;

            // Step 2: ReachTrigger — central nave.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s2.FindPropertyRelative("label").stringValue = "ReachTrigger: Central Nave";
            s2.FindPropertyRelative("reachPoint").objectReferenceValue = naveReachGo.transform;
            s2.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 3: DefeatWaves — 2 Recon Sentries (trimmed skirmish).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s3.FindPropertyRelative("label").stringValue = "DefeatWaves: 2 Recon Sentries";
            s3.FindPropertyRelative("waveSpawner").objectReferenceValue = sentryWaveSpawner;

            // Step 4: Dialogue heart_khall_log (Khall's video log, auto) — folded from Archive Heart.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Khall's Video Log";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = heartKhallLogDialogue;

            // Step 5: Dialogue heart_blade_truth (the blade's truth — THE reveal, talk-gated).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s5.FindPropertyRelative("label").stringValue = "Dialogue: The Blade's Truth";
            s5.FindPropertyRelative("dialogue").objectReferenceValue = heartBladeTruthDialogue;

            // Step 6: DefeatWaves — Enforcer duel (silver, 3.5x health), the climax.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s6.FindPropertyRelative("label").stringValue = "DefeatWaves: Enforcer Duel";
            s6.FindPropertyRelative("waveSpawner").objectReferenceValue = enforcerWaveSpawner;

            // Step 7: Dialogue heart_enforcer_after (after the duel, auto).
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s7.FindPropertyRelative("label").stringValue = "Dialogue: After the Duel";
            s7.FindPropertyRelative("dialogue").objectReferenceValue = heartEnforcerAfterDialogue;

            // Step 8: ReachTrigger — records terminal in the heart alcove.
            var s8 = stepsProp.GetArrayElementAtIndex(8);
            s8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s8.FindPropertyRelative("label").stringValue = "ReachTrigger: Records Terminal";
            s8.FindPropertyRelative("reachPoint").objectReferenceValue = terminalReachGo.transform;
            s8.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 9: Dialogue archive_welcome (entry to the inner records granted, talk-gated).
            var s9 = stepsProp.GetArrayElementAtIndex(9);
            s9.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s9.FindPropertyRelative("label").stringValue = "Dialogue: Welcome Home Elegy";
            s9.FindPropertyRelative("dialogue").objectReferenceValue = archiveWelcomeDialogue;

            // Step 10: Dialogue heart_files (the encrypted files, talk-gated).
            var s10 = stepsProp.GetArrayElementAtIndex(10);
            s10.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s10.FindPropertyRelative("label").stringValue = "Dialogue: The Encrypted Files";
            s10.FindPropertyRelative("dialogue").objectReferenceValue = heartFilesDialogue;

            // Step 11: Trigger — heart alarm lights.
            var s11 = stepsProp.GetArrayElementAtIndex(11);
            s11.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s11.FindPropertyRelative("label").stringValue = "Trigger: Heart Alarm";
            var t11 = s11.FindPropertyRelative("triggerObjects");
            t11.arraySize = 1;
            t11.GetArrayElementAtIndex(0).objectReferenceValue = heartAlarmLightsGo;

            // Step 12: Dialogue heart_alarm (alarms wail, auto).
            var s12 = stepsProp.GetArrayElementAtIndex(12);
            s12.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s12.FindPropertyRelative("label").stringValue = "Dialogue: Heart Alarms";
            s12.FindPropertyRelative("dialogue").objectReferenceValue = heartAlarmDialogue;

            // Step 13: Dialogue heart_escape (dogfight over comms, auto).
            var s13 = stepsProp.GetArrayElementAtIndex(13);
            s13.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s13.FindPropertyRelative("label").stringValue = "Dialogue: Escape Run";
            s13.FindPropertyRelative("dialogue").objectReferenceValue = heartEscapeDialogue;

            // Step 14: Prompt — escape to the Corsair (chains to the EP04 Ledger finale).
            var s14 = stepsProp.GetArrayElementAtIndex(14);
            s14.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s14.FindPropertyRelative("label").stringValue = "Prompt: Escape to the Corsair";
            s14.FindPropertyRelative("promptObject").objectReferenceValue = heartBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep04ArchiveRingScenePath);
            EnsureScenesInBuild(Galaxy1Ep04ArchiveRingScenePath);

            Debug.Log($"[Space Samurai] EP04 Archive Ring scene built at {Galaxy1Ep04ArchiveRingScenePath} " +
                      "(Archive Heart merged in). Layout: airlock → outer corridor → central nave (catwalk strips, " +
                      "2 Sentries + the Enforcer duel, Khall video screen) → heart-gate alcove (records terminal). " +
                      "Amber solar-flare fog + cold-grey accents. " +
                      "15 steps: archive_ghost → open RingDoor → reach nave → defeat 2 Sentries → heart_khall_log → " +
                      "heart_blade_truth → Enforcer duel → heart_enforcer_after → reach terminal → archive_welcome → " +
                      "heart_files → heart alarms → heart_alarm → heart_escape → ESCAPE TO THE CORSAIR (chains to Ledger).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP04 Ledger", priority = 79)]
        public static void BuildEp04Ledger()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Corsair salvage bay: warm interior light, intimate space.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.9f, 0.85f, 0.75f);
            light.intensity = 0.95f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -20f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.12f, 0.1f);

            // Interior light accents.
            BuildAccentPointLight("TableLight", new Vector3(0f, 2.4f, 7f),
                new Color(1f, 0.9f, 0.7f), intensity: 1.6f, range: 12f);

            // ---- Corsair salvage bay: one small room z 0-14, x[-4,4].
            var interiorGo = new GameObject("SalvageBay");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.22f, 0.2f, 0.18f);
            var ceilColor = new Color(0.12f, 0.11f, 0.1f);

            // Bay: x[-4,4], z[0,14].
            BuildFloorCeiling(interior, "Bay", new Vector3(0f, 0f, 7f), new Vector3(8f, 0f, 14f), floorColor, ceilColor);
            BuildWall(interior, "Bay_WallW", new Vector3(-4f, 1.5f, 7f), new Vector3(0.2f, 3f, 14f));
            BuildWall(interior, "Bay_WallE", new Vector3(4f, 1.5f, 7f), new Vector3(0.2f, 3f, 14f));
            BuildWall(interior, "Bay_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(8f, 3f, 0.2f));
            BuildWall(interior, "Bay_WallBack", new Vector3(0f, 1.5f, 14f), new Vector3(8f, 3f, 0.2f));

            // Chart table prop in the center (z~7).
            BuildProp(interior, "ChartTable", new Vector3(0f, 0.7f, 7f), new Vector3(2.5f, 0.8f, 2f), new Color(0.3f, 0.28f, 0.25f));

            // Hyperspace window quads on side walls (blue-tinted, semi-transparent, no colliders).
            var windowColor = new Color(0.25f, 0.45f, 0.9f, 0.6f);
            var windowW = GameObject.CreatePrimitive(PrimitiveType.Quad);
            windowW.name = "WindowW";
            windowW.transform.SetParent(interior, false);
            windowW.transform.localPosition = new Vector3(-4f, 1.5f, 7f);
            windowW.transform.localScale = new Vector3(0.2f, 2f, 3f);
            TintShared(windowW.GetComponent<Renderer>(), windowColor);
            var wcW = windowW.GetComponent<Collider>();
            if (wcW != null) Object.DestroyImmediate(wcW);

            var windowE = GameObject.CreatePrimitive(PrimitiveType.Quad);
            windowE.name = "WindowE";
            windowE.transform.SetParent(interior, false);
            windowE.transform.localPosition = new Vector3(4f, 1.5f, 7f);
            windowE.transform.localScale = new Vector3(0.2f, 2f, 3f);
            TintShared(windowE.GetComponent<Renderer>(), windowColor);
            var wcE = windowE.GetComponent<Collider>();
            if (wcE != null) Object.DestroyImmediate(wcE);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 30f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Kessler by the chart table.
            var kesslerPos = new Vector3(-1f, 1f, 7f);
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler_Ledger");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kesslerSo = new SerializedObject(kesslerNpc);
                kesslerSo.FindProperty("displayName").stringValue = "Kessler";
                kesslerSo.FindProperty("remote").boolValue = false;
                kesslerSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Dialogue Players ----
            var ledgerCountDialogue = BuildEp04DialoguePlayer("Dialogue_LedgerCount", kesslerPos, "ledger_count");
            var ledgerRevelationDialogue = BuildEp04DialoguePlayer("Dialogue_LedgerRevelation", kesslerPos, "ledger_revelation", talkRef);

            // Reach trigger.
            var tableReachGo = new GameObject("TableReachPoint");
            tableReachGo.transform.position = new Vector3(0f, 1f, 7f);

            // Transition box: "LAUNCH TO SPACE" wired to ReturnToSpace.
            var launchBoxGo = BuildTransitionBox("LaunchToSpaceBox", new Vector3(0f, 1.2f, 12f), "LAUNCH TO SPACE",
                out var launchBtn, out var launchTransition);
            UnityEventTools.AddPersistentListener(launchBtn.onClick,
                new UnityEngine.Events.UnityAction(launchTransition.ReturnToSpace));
            launchBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 4;

            // Step 0: Dialogue ledger_count (the ledger count, auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: The Ledger";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = ledgerCountDialogue;

            // Step 1: ReachTrigger — chart table.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s1.FindPropertyRelative("label").stringValue = "ReachTrigger: Chart Table";
            s1.FindPropertyRelative("reachPoint").objectReferenceValue = tableReachGo.transform;
            s1.FindPropertyRelative("reachRadius").floatValue = 2.5f;

            // Step 2: Dialogue ledger_revelation (the final revelation).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s2.FindPropertyRelative("label").stringValue = "Dialogue: The Final Revelation";
            s2.FindPropertyRelative("dialogue").objectReferenceValue = ledgerRevelationDialogue;

            // Step 3: Prompt — launch to space.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s3.FindPropertyRelative("label").stringValue = "Prompt: Launch to Space";
            s3.FindPropertyRelative("promptObject").objectReferenceValue = launchBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep04LedgerScenePath);
            EnsureScenesInBuild(Galaxy1Ep04LedgerScenePath);

            Debug.Log($"[Space Samurai] EP04 Ledger scene built at {Galaxy1Ep04LedgerScenePath}. " +
                      "Layout: Corsair salvage bay interior (chart table center, Kessler, hyperspace window quads on sides). " +
                      "4 steps: ledger_count auto → reach chart table → ledger_revelation → LAUNCH TO SPACE (ReturnToSpace, marks EP04 complete).");
        }

        // ---- Moved from Ep05Builder.cs (deleted) — BuildEp05MarketTier/BuildEp05PressureLocks are
        // called directly by Galaxy1Builder.BuildAllGalaxy1Scenes; the scene-path consts and
        // BuildEp05DialoguePlayer helper are used by Galaxy1Builder too (Ep05Lines space-briefing dialogue). ----
        private const string Galaxy1Ep05MarketTierScenePath = SceneFolder + "/Galaxy1_EP05_MarketTier.unity";
        private const string Galaxy1Ep05PressureLocksScenePath = SceneFolder + "/Galaxy1_EP05_PressureLocks.unity";
        private const string Galaxy1Ep05RotundaScenePath = SceneFolder + "/Galaxy1_EP05_Rotunda.unity";
        private const string Galaxy1Ep05CommandHubScenePath = SceneFolder + "/Galaxy1_EP05_CommandHub.unity";

        private static readonly string Galaxy1Ep05MarketTierSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep05MarketTierScenePath);
        private static readonly string Galaxy1Ep05PressureLocksSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep05PressureLocksScenePath);
        private static readonly string Galaxy1Ep05RotundaSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep05RotundaScenePath);
        private static readonly string Galaxy1Ep05CommandHubSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep05CommandHubScenePath);

        /// <summary>Shorthand for building a DialoguePlayer with EP05 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep05" and loads lines from Ep05Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp05DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep05Lines.Get(setId), advanceRef, setId, clipPrefix: "ep05");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP05 Market Tier", priority = 80)]
        public static void BuildEp05MarketTier()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Market tier interior: warm rust-amber palette, salvage station ambiance.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.75f, 0.65f);
            light.intensity = 1.0f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.2f, 0.15f);

            // Rust-vapor fog: amber exponential, low density.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.4f, 0.28f, 0.18f);
            RenderSettings.fogDensity = 0.025f;

            // Market accent lights (warm rust tones).
            BuildAccentPointLight("MarketLight1", new Vector3(-3f, 2.6f, 10f),
                new Color(1f, 0.7f, 0.4f), intensity: 1.5f, range: 12f);
            BuildAccentPointLight("MarketLight2", new Vector3(3f, 2.6f, 16f),
                new Color(1f, 0.75f, 0.5f), intensity: 1.4f, range: 12f);

            // ---- Market tier: market hall (z 0-10) -> cargo warrens corridor (z 10-20) ->
            // sealed refuge room (z 20-26).
            var interiorGo = new GameObject("MarketInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.22f, 0.18f, 0.15f);
            var ceilColor = new Color(0.12f, 0.1f, 0.08f);

            // Market hall: x[-5,5], z[0,10].
            BuildFloorCeiling(interior, "MarketHall", new Vector3(0f, 0f, 5f), new Vector3(10f, 0f, 10f), floorColor, ceilColor);
            BuildWall(interior, "MarketHall_WallW", new Vector3(-5f, 1.5f, 5f), new Vector3(0.2f, 3f, 10f));
            BuildWall(interior, "MarketHall_WallE", new Vector3(5f, 1.5f, 5f), new Vector3(0.2f, 3f, 10f));
            BuildWall(interior, "MarketHall_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(10f, 3f, 0.2f));

            // Market stall props (boxes/crates).
            var stallColor = new Color(0.35f, 0.3f, 0.25f);
            BuildProp(interior, "Stall1", new Vector3(-3f, 0.5f, 3f), new Vector3(1.5f, 1f, 1.5f), stallColor);
            BuildProp(interior, "Stall2", new Vector3(3f, 0.5f, 4f), new Vector3(1.5f, 1.2f, 1.5f), stallColor);
            BuildProp(interior, "Stall3", new Vector3(-2f, 0.4f, 7f), new Vector3(1f, 0.8f, 1f), stallColor);

            // Cargo warrens corridor: x[-4,4], z[10,20] with stacked container props.
            BuildFloorCeiling(interior, "WarrensCorridor", new Vector3(0f, 0f, 15f), new Vector3(8f, 0f, 10f), floorColor, ceilColor);
            BuildCorridorWall(interior, "WarrensCorridor_WallW", -4f, 10f, 20f, new float[0], 2.4f);
            BuildCorridorWall(interior, "WarrensCorridor_WallE", 4f, 10f, 20f, new float[0], 2.4f);

            // Container props stacked in warrens.
            var containerColor = new Color(0.45f, 0.35f, 0.25f);
            BuildProp(interior, "Container1", new Vector3(-2.5f, 0.8f, 12f), new Vector3(1.2f, 1.6f, 1.2f), containerColor);
            BuildProp(interior, "Container2", new Vector3(2.5f, 0.8f, 14f), new Vector3(1.2f, 1.6f, 1.2f), containerColor);
            BuildProp(interior, "Container3", new Vector3(-1f, 1.6f, 16f), new Vector3(1f, 1.4f, 1f), containerColor);

            // Sealed refuge room: x[-3,3], z[20,26].
            BuildFloorCeiling(interior, "RefugeRoom", new Vector3(0f, 0f, 23f), new Vector3(6f, 0f, 6f), floorColor, ceilColor);
            BuildWall(interior, "RefugeRoom_WallW", new Vector3(-3f, 1.5f, 23f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "RefugeRoom_WallE", new Vector3(3f, 1.5f, 23f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "RefugeRoom_WallBack", new Vector3(0f, 1.5f, 26f), new Vector3(6f, 3f, 0.2f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 50f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Ronin-9 near a market stall.
            var ronin9Pos = new Vector3(-2f, 1f, 4f);
            var ronin9Go = InstantiateNpc(ArtPrefabBuilder.Ronin9PrefabPath, ronin9Pos, "Ronin9");
            if (ronin9Go != null)
            {
                var ronin9Npc = ronin9Go.AddComponent<StoryNpc>();
                var r9So = new SerializedObject(ronin9Npc);
                r9So.FindProperty("displayName").stringValue = "Ronin-9";
                r9So.FindProperty("remote").boolValue = false;
                r9So.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Alarm lights (activated when enforcers emerge). ----
            var alarmLightsGo = BuildEp03AlarmLights(new Vector3(-2f, 2.4f, 14f), new Vector3(2f, 2.4f, 16f));

            // ---- Dialogue Players ----
            var marketRecognitionDialogue = BuildEp05DialoguePlayer("Dialogue_MarketRecognition", ronin9Pos, "market_recognition", talkRef);
            var warrensEnforcerBarksDialogue = BuildEp05DialoguePlayer("Dialogue_WarrensEnforcerBarks", new Vector3(0f, 1.5f, 15f), "warrens_enforcer_barks");
            var warrensAfterDialogue = BuildEp05DialoguePlayer("Dialogue_WarrensAfter", new Vector3(0f, 1f, 18f), "warrens_after");
            var kethelExplainedDialogue = BuildEp05DialoguePlayer("Dialogue_KethelExplained", new Vector3(0f, 1f, 23f), "kethel_explained", talkRef);

            // ---- Enemies: 3 Enforcers (rust-brown tint). ----
            var enforcerBrown = new Color(0.65f, 0.45f, 0.3f);
            var enforcerPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 12f),
                new Vector3(1.5f, 0f, 13f),
                new Vector3(0f, 0f, 15f)
            };
            var enforcerHealths = new List<Health>();
            foreach (var pos in enforcerPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, enforcerBrown);
                enemy.gameObject.SetActive(false);
                enforcerHealths.Add(enemy.GetComponent<Health>());
            }

            var enforcerWaveSpawner = BuildWaveSpawner("EnforcerWaveSpawner", new Vector3(0f, 1f, 13f), 3f,
                new List<List<Health>> { enforcerHealths }, new[] { warrensEnforcerBarksDialogue });

            // Reach triggers.
            var warrensEntranceReachGo = new GameObject("WarrensEntranceReachPoint");
            warrensEntranceReachGo.transform.position = new Vector3(0f, 1f, 10f);
            var refugeRoomReachGo = new GameObject("RefugeRoomReachPoint");
            refugeRoomReachGo.transform.position = new Vector3(0f, 1f, 23f);

            // Transition box: "TO THE PRESSURE LOCKS".
            var pressureBoxGo = BuildTransitionBox("ToPressureLocksBox", new Vector3(0f, 1.2f, 24.5f), "TO THE PRESSURE LOCKS",
                out var pressureBtn, out var pressureTransition);
            var ptSo = new SerializedObject(pressureTransition);
            ptSo.FindProperty("onFootScene").stringValue = Galaxy1Ep05PressureLocksSceneName;
            ptSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(pressureBtn.onClick,
                new UnityEngine.Events.UnityAction(pressureTransition.LoadOnFootScene));
            pressureBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 8;

            // Step 0: Dialogue market_recognition (Ronin-9 recognition, talk-gated).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Market Recognition";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = marketRecognitionDialogue;

            // Step 1: ReachTrigger — warrens entrance.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s1.FindPropertyRelative("label").stringValue = "ReachTrigger: Warrens Entrance";
            s1.FindPropertyRelative("reachPoint").objectReferenceValue = warrensEntranceReachGo.transform;
            s1.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 2: Trigger — alarm lights.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s2.FindPropertyRelative("label").stringValue = "Trigger: Enforcer Alarm";
            var t2 = s2.FindPropertyRelative("triggerObjects");
            t2.arraySize = 1;
            t2.GetArrayElementAtIndex(0).objectReferenceValue = alarmLightsGo;

            // Step 3: DefeatWaves — 3 Enforcers.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s3.FindPropertyRelative("label").stringValue = "DefeatWaves: 3 Cargo Enforcers";
            s3.FindPropertyRelative("waveSpawner").objectReferenceValue = enforcerWaveSpawner;

            // Step 4: Dialogue warrens_after.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: After the Fight";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = warrensAfterDialogue;

            // Step 5: ReachTrigger — refuge room.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s5.FindPropertyRelative("label").stringValue = "ReachTrigger: Refuge Room";
            s5.FindPropertyRelative("reachPoint").objectReferenceValue = refugeRoomReachGo.transform;
            s5.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 6: Dialogue kethel_explained (talk-gated).
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s6.FindPropertyRelative("label").stringValue = "Dialogue: Kethel-7 Explained";
            s6.FindPropertyRelative("dialogue").objectReferenceValue = kethelExplainedDialogue;

            // Step 7: Prompt — transition to pressure locks.
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s7.FindPropertyRelative("label").stringValue = "Prompt: To the Pressure Locks";
            s7.FindPropertyRelative("promptObject").objectReferenceValue = pressureBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep05MarketTierScenePath);
            EnsureScenesInBuild(Galaxy1Ep05MarketTierScenePath);

            Debug.Log($"[Space Samurai] EP05 Market Tier scene built at {Galaxy1Ep05MarketTierScenePath}. " +
                      "Layout: market hall with vendor stalls → cargo warrens corridor (stacked containers, 3 Enforcers) → sealed refuge room. " +
                      "Warm rust-amber fog, emergency strobe accents. " +
                      "8 steps: market_recognition (Ronin-9 talk) → reach warrens → trigger alarms → defeat 3 Enforcers + barks → warrens_after → reach refuge → " +
                      "kethel_explained (talk) → transition to Pressure Locks.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP05 Pressure Locks", priority = 81)]
        public static void BuildEp05PressureLocks()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Pressure locks interior: cold blue-white palette, industrial/technical mood.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.8f, 0.85f, 0.95f);
            light.intensity = 0.95f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.18f, 0.25f);

            // Frost-grey fog: blue-tinted.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.25f, 0.3f, 0.38f);
            RenderSettings.fogDensity = 0.02f;

            // Pressure lock accent lights (cool blue).
            BuildAccentPointLight("LockLight1", new Vector3(-2.5f, 2.6f, 12f),
                new Color(0.6f, 0.8f, 1f), intensity: 1.4f, range: 12f);
            BuildAccentPointLight("LockLight2", new Vector3(2.5f, 2.6f, 20f),
                new Color(0.65f, 0.85f, 1f), intensity: 1.3f, range: 12f);

            // ---- Pressure locks: entry corridor (z 0-8) -> lock maze corridor (z 8-22) ->
            // salvage bay (z 22-30).
            var interiorGo = new GameObject("PressureLockInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.2f, 0.22f, 0.28f);
            var ceilColor = new Color(0.1f, 0.12f, 0.16f);

            // Entry corridor: x[-3,3], z[0,8].
            BuildFloorCeiling(interior, "EntryLock", new Vector3(0f, 0f, 4f), new Vector3(6f, 0f, 8f), floorColor, ceilColor);
            BuildCorridorWall(interior, "EntryLock_WallW", -3f, 0f, 8f, new float[0], 2.4f);
            BuildCorridorWall(interior, "EntryLock_WallE", 3f, 0f, 8f, new float[0], 2.4f);
            BuildWall(interior, "EntryLock_WallFront", new Vector3(0f, 1.5f, 0f), new Vector3(6f, 3f, 0.2f));

            // Lock maze corridor: x[-4,4], z[8,22] with narrow passages and rotating-bulkhead props.
            BuildFloorCeiling(interior, "LockMaze", new Vector3(0f, 0f, 15f), new Vector3(8f, 0f, 14f), floorColor, ceilColor);
            BuildCorridorWall(interior, "LockMaze_WallW", -4f, 8f, 22f, new float[0], 2.4f);
            BuildCorridorWall(interior, "LockMaze_WallE", 4f, 8f, 22f, new float[0], 2.4f);

            // Rotating bulkhead props (grey-metal color).
            var bulkheadColor = new Color(0.4f, 0.42f, 0.45f);
            BuildProp(interior, "Bulkhead1", new Vector3(-1.5f, 0.8f, 11f), new Vector3(1f, 1.8f, 0.3f), bulkheadColor);
            BuildProp(interior, "Bulkhead2", new Vector3(1.5f, 0.8f, 16f), new Vector3(1f, 1.8f, 0.3f), bulkheadColor);
            BuildProp(interior, "Bulkhead3", new Vector3(-2f, 0.6f, 19f), new Vector3(0.8f, 1.6f, 0.3f), bulkheadColor);

            // Salvage bay: x[-5,5], z[22,30].
            BuildFloorCeiling(interior, "SalvageBay", new Vector3(0f, 0f, 26f), new Vector3(10f, 0f, 8f), floorColor, ceilColor);
            BuildWall(interior, "SalvageBay_WallW", new Vector3(-5f, 1.5f, 26f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "SalvageBay_WallE", new Vector3(5f, 1.5f, 26f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "SalvageBay_WallBack", new Vector3(0f, 1.5f, 30f), new Vector3(10f, 3f, 0.2f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 50f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- Alarm lights. ----
            var alarmLightsGo = BuildEp03AlarmLights(new Vector3(-2f, 2.4f, 14f), new Vector3(2f, 2.4f, 18f));

            // ---- Dialogue Players ----
            var lockAlertDialogue = BuildEp05DialoguePlayer("Dialogue_LockAlert", new Vector3(0f, 1.5f, 5f), "lock_alert");
            var lockHunterBarksDialogue = BuildEp05DialoguePlayer("Dialogue_LockHunterBarks", new Vector3(0f, 1.5f, 15f), "lock_hunter_barks");
            var purgeTruthDialogue = BuildEp05DialoguePlayer("Dialogue_PurgeTruth", new Vector3(0f, 1f, 15f), "purge_truth", talkRef);
            var defectiveGenerationDialogue = BuildEp05DialoguePlayer("Dialogue_DefectiveGeneration", new Vector3(0f, 1f, 26f), "defective_generation", talkRef);

            // ---- Folded in from the merged Observation Deck: Kessler reaches the salvage bay; the "Khall is
            // coming" wounded beat + Kessler's talk play here (the deck's Silencer fight is dropped). ----
            var kesslerPos = new Vector3(-1f, 1f, 28f);
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler_SalvageBay");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kSo = new SerializedObject(kesslerNpc);
                kSo.FindProperty("displayName").stringValue = "Kessler";
                kSo.FindProperty("remote").boolValue = false;
                kSo.ApplyModifiedPropertiesWithoutUndo();
            }
            var deckWoundedDialogue = BuildEp05DialoguePlayer("Dialogue_DeckWounded", new Vector3(0f, 1f, 27f), "deck_wounded");
            var medBayKesslerDialogue = BuildEp05DialoguePlayer("Dialogue_MedBayKessler", kesslerPos, "medbay_kessler", talkRef);

            // ---- Enemies: 2 Hunters (gunmetal tint, 1.5x health). ----
            var hunterGunmetal = new Color(0.55f, 0.57f, 0.6f);
            var hunterPositions = new Vector3[]
            {
                new Vector3(-1.5f, 0f, 13f),
                new Vector3(1.5f, 0f, 17f)
            };
            var hunterHealths = new List<Health>();
            foreach (var pos in hunterPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, hunterGunmetal);
                var hunterHealth = enemy.GetComponent<Health>();
                if (hunterHealth != null)
                {
                    var hSo = new SerializedObject(hunterHealth);
                    hSo.FindProperty("maxHealth").floatValue = hSo.FindProperty("maxHealth").floatValue * 1.5f;
                    hSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                hunterHealths.Add(hunterHealth);
            }

            var hunterWaveSpawner = BuildWaveSpawner("HunterWaveSpawner", new Vector3(0f, 1f, 15f), 3f,
                new List<List<Health>> { hunterHealths }, new[] { lockHunterBarksDialogue });

            // Reach triggers.
            var salvageBayReachGo = new GameObject("SalvageBayReachPoint");
            salvageBayReachGo.transform.position = new Vector3(0f, 1f, 26f);

            // Transition box: "TO THE CENTRAL ROTUNDA" (Observation Deck merged in — its wounded + Kessler
            // beats now play in this salvage bay, so Pressure Locks chains straight to the Rotunda).
            var observationBoxGo = BuildTransitionBox("ToCentralRotundaBox", new Vector3(0f, 1.2f, 29.5f), "TO THE CENTRAL ROTUNDA",
                out var observationBtn, out var observationTransition);
            var otSo = new SerializedObject(observationTransition);
            otSo.FindProperty("onFootScene").stringValue = Galaxy1Ep05RotundaSceneName;
            otSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(observationBtn.onClick,
                new UnityEngine.Events.UnityAction(observationTransition.LoadOnFootScene));
            observationBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 9;

            // Step 0: Dialogue lock_alert (auto-play).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Pressure Lock Alert";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = lockAlertDialogue;

            // Step 1: Trigger — alarm lights.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s1.FindPropertyRelative("label").stringValue = "Trigger: Hunter Alarm";
            var t1 = s1.FindPropertyRelative("triggerObjects");
            t1.arraySize = 1;
            t1.GetArrayElementAtIndex(0).objectReferenceValue = alarmLightsGo;

            // Step 2: DefeatWaves — 2 Hunters.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: 2 Hunter Operatives";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = hunterWaveSpawner;

            // Step 3: Dialogue purge_truth (talk-gated).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: The Purge Truth";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = purgeTruthDialogue;

            // Step 4: ReachTrigger — salvage bay.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s4.FindPropertyRelative("label").stringValue = "ReachTrigger: Salvage Bay";
            s4.FindPropertyRelative("reachPoint").objectReferenceValue = salvageBayReachGo.transform;
            s4.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 5: Dialogue defective_generation (talk-gated).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s5.FindPropertyRelative("label").stringValue = "Dialogue: Defective Generation";
            s5.FindPropertyRelative("dialogue").objectReferenceValue = defectiveGenerationDialogue;

            // Step 6: Dialogue deck_wounded (auto) — folded from the merged Observation Deck.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s6.FindPropertyRelative("label").stringValue = "Dialogue: Wounded, Khall is Coming";
            s6.FindPropertyRelative("dialogue").objectReferenceValue = deckWoundedDialogue;

            // Step 7: Dialogue medbay_kessler (talk-gated at Kessler) — folded from the merged Observation Deck.
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s7.FindPropertyRelative("label").stringValue = "Dialogue: Kessler Arrives";
            s7.FindPropertyRelative("dialogue").objectReferenceValue = medBayKesslerDialogue;

            // Step 8: Prompt — transition to central rotunda.
            var s8 = stepsProp.GetArrayElementAtIndex(8);
            s8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s8.FindPropertyRelative("label").stringValue = "Prompt: To the Central Rotunda";
            s8.FindPropertyRelative("promptObject").objectReferenceValue = observationBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep05PressureLocksScenePath);
            EnsureScenesInBuild(Galaxy1Ep05PressureLocksScenePath);

            Debug.Log($"[Space Samurai] EP05 Pressure Locks scene built at {Galaxy1Ep05PressureLocksScenePath}. " +
                      "Layout: entry corridor → lock maze (rotating bulkheads, 2 Hunters) → salvage bay. " +
                      "Cold blue-white fog, industrial accents. " +
                      "9 steps: lock_alert auto → trigger alarms → defeat 2 Hunters (1.5x health) + barks → purge_truth (talk) → " +
                      "reach salvage bay → defective_generation (talk) → deck_wounded → medbay_kessler (talk, both folded " +
                      "from the merged Observation Deck) → transition to Central Rotunda.");
        }

        // ---- Moved from Ep05BuilderHub.cs (deleted) — BuildEp05Rotunda/BuildEp05CommandHub are
        // called directly by Galaxy1Builder.BuildAllGalaxy1Scenes. ----
        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP05 Rotunda", priority = 83)]
        public static void BuildEp05Rotunda()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Rust Collective rotunda: cool grey + amber accents, industrial crane and reactor-cooling props,
            // echoing steel palette, neutron-star accretion glow accent.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.75f, 0.78f);
            light.intensity = 0.85f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.15f, 0.17f);

            // Cool grey exponential fog with hint of rust.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.18f, 0.18f, 0.2f);
            RenderSettings.fogDensity = 0.02f;

            // Amber accretion-glow accent lights.
            BuildAccentPointLight("AmberLight1", new Vector3(-4f, 3f, 12f),
                new Color(1f, 0.65f, 0.3f), intensity: 1.5f, range: 16f);
            BuildAccentPointLight("AmberLight2", new Vector3(4f, 3f, 16f),
                new Color(0.95f, 0.6f, 0.25f), intensity: 1.4f, range: 15f);

            // ---- Rotunda: central circular-ish chamber (z 0-20) with crane/reactor props ->
            // bridge room (z 20-28) with console props and viewport.
            var rotundaGo = new GameObject("RotundaInterior");
            var rotunda = rotundaGo.transform;
            var steelColor = new Color(0.25f, 0.25f, 0.27f);
            var darkSteelColor = new Color(0.15f, 0.15f, 0.17f);

            // Central chamber: x[-8,8], z[0,20], large open space.
            BuildFloorCeiling(rotunda, "CentralChamber", new Vector3(0f, 0f, 10f), new Vector3(16f, 0f, 20f), steelColor, darkSteelColor);
            BuildWall(rotunda, "Chamber_WallW", new Vector3(-8f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));
            BuildWall(rotunda, "Chamber_WallE", new Vector3(8f, 1.5f, 10f), new Vector3(0.2f, 3f, 20f));

            // Crane prop (overhead structure, approximated).
            BuildProp(rotunda, "CraneArm", new Vector3(0f, 2.5f, 8f), new Vector3(10f, 0.4f, 1.5f), steelColor);
            BuildProp(rotunda, "CraneBase", new Vector3(-6f, 0.5f, 8f), new Vector3(1f, 2f, 1f), steelColor);

            // Reactor cooling pipes (exposed-girder flavor).
            BuildProp(rotunda, "CoolingPipe1", new Vector3(3f, 1.2f, 15f), new Vector3(0.3f, 0.3f, 6f), darkSteelColor);
            BuildProp(rotunda, "CoolingPipe2", new Vector3(-3f, 1.2f, 12f), new Vector3(0.3f, 0.3f, 4f), darkSteelColor);

            // Bridge room: x[-6,6], z[20,28].
            BuildFloorCeiling(rotunda, "BridgeRoom", new Vector3(0f, 0f, 24f), new Vector3(12f, 0f, 8f), steelColor, darkSteelColor);
            BuildWall(rotunda, "Bridge_WallW", new Vector3(-6f, 1.5f, 24f), new Vector3(0.2f, 3f, 8f));
            BuildWall(rotunda, "Bridge_WallE", new Vector3(6f, 1.5f, 24f), new Vector3(0.2f, 3f, 8f));
            BuildWall(rotunda, "Bridge_WallBack", new Vector3(0f, 1.5f, 28f), new Vector3(12f, 3f, 0.2f));

            // Console props in bridge room.
            BuildProp(rotunda, "Console1", new Vector3(-2f, 0.8f, 24f), new Vector3(1.2f, 1.2f, 0.5f), new Color(0.1f, 0.12f, 0.15f));
            BuildProp(rotunda, "Console2", new Vector3(2f, 0.8f, 24f), new Vector3(1.2f, 1.2f, 0.5f), new Color(0.1f, 0.12f, 0.15f));

            // Viewport emissive quad (no collider).
            var viewportQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            viewportQuad.name = "Viewport";
            viewportQuad.transform.SetParent(rotunda, false);
            viewportQuad.transform.localPosition = new Vector3(0f, 1.5f, 20f);
            viewportQuad.transform.localScale = new Vector3(6f, 3f, 1f);
            TintShared(viewportQuad.GetComponent<Renderer>(), new Color(1f, 0.4f, 0.15f, 0.7f));
            var vpCollider = viewportQuad.GetComponent<Collider>();
            if (vpCollider != null) Object.DestroyImmediate(vpCollider);

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 50f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- Alarm lights (two sets). ----
            var alarmLights1Go = BuildEp03AlarmLights(new Vector3(-4f, 2.8f, 10f), new Vector3(4f, 2.8f, 12f));
            var alarmLights2Go = BuildEp03AlarmLights(new Vector3(-3f, 2.8f, 24f), new Vector3(3f, 2.8f, 26f));

            // ---- Dialogue Players ----
            var rotundaAlertDialogue = BuildEp05DialoguePlayer("Dialogue_RotundaAlert", new Vector3(0f, 1.5f, 5f), "rotunda_alert");
            var rotundaStrikeBarksDialogue = BuildEp05DialoguePlayer("Dialogue_RotundaStrikeBarks", new Vector3(0f, 1.5f, 12f), "rotunda_strike_barks");
            var rotundaChoiceDialogue = BuildEp05DialoguePlayer("Dialogue_RotundaChoice", new Vector3(0f, 1.5f, 10f), "rotunda_choice", talkRef);
            var bridgeBoardersBarksDialogue = BuildEp05DialoguePlayer("Dialogue_BridgeBoardersBarks", new Vector3(0f, 1.5f, 24f), "bridge_boarders_barks");
            var bridgeOthersDialogue = BuildEp05DialoguePlayer("Dialogue_BridgeOthers", new Vector3(0f, 1.5f, 24f), "bridge_others", talkRef);

            // ---- Enemies: Rotunda Wave 1 (5 soldiers) ----
            // 4 standard soldiers (grey) + 1 Dominion Officer (darker tint, 2x health).
            var soldierGrey = new Color(0.65f, 0.65f, 0.68f);
            var officerDark = new Color(0.35f, 0.35f, 0.38f);

            var soldierPositions = new Vector3[]
            {
                new Vector3(-4f, 0f, 8f),
                new Vector3(4f, 0f, 10f),
                new Vector3(-2f, 0f, 14f),
                new Vector3(2f, 0f, 16f)
            };

            var wave1Healths = new List<Health>();
            foreach (var pos in soldierPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, soldierGrey);
                enemy.gameObject.SetActive(false);
                wave1Healths.Add(enemy.GetComponent<Health>());
            }

            // Officer with 2x health.
            var officer = BuildDominionEnemy(new Vector3(0f, 0f, 12f), playerHealth, enemyDef);
            var officerRenderer = officer.GetComponent<Renderer>();
            if (officerRenderer != null) TintShared(officerRenderer, officerDark);
            var officerHealth = officer.GetComponent<Health>();
            if (officerHealth != null)
            {
                var ohSo = new SerializedObject(officerHealth);
                ohSo.FindProperty("maxHealth").floatValue = ohSo.FindProperty("maxHealth").floatValue * 2f;
                ohSo.ApplyModifiedPropertiesWithoutUndo();
            }
            officer.gameObject.SetActive(false);
            wave1Healths.Add(officerHealth);

            var wave1Spawner = BuildWaveSpawner("Wave1Spawner", new Vector3(0f, 1f, 10f), 3f,
                new List<List<Health>> { wave1Healths }, new[] { rotundaStrikeBarksDialogue });

            // ---- Enemies: Rotunda Wave 2 (2 boarders) ----
            var boarderGrey = new Color(0.6f, 0.6f, 0.63f);
            var boarderPositions = new Vector3[]
            {
                new Vector3(-3f, 0f, 24f),
                new Vector3(3f, 0f, 26f)
            };

            var wave2Healths = new List<Health>();
            foreach (var pos in boarderPositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, boarderGrey);
                enemy.gameObject.SetActive(false);
                wave2Healths.Add(enemy.GetComponent<Health>());
            }

            var wave2Spawner = BuildWaveSpawner("Wave2Spawner", new Vector3(0f, 1f, 24f), 3f,
                new List<List<Health>> { wave2Healths }, new[] { bridgeBoardersBarksDialogue });

            // Reach triggers.
            var bridgeDoorwayReachGo = new GameObject("BridgeDoorwayReachPoint");
            bridgeDoorwayReachGo.transform.position = new Vector3(0f, 1f, 20f);

            // Transition box: "TO THE COMMAND HUB" wired to LoadOnFootScene (copy Ep04 ArchiveRing pattern).
            var hubBoxGo = BuildTransitionBox("ToCommandHubBox", new Vector3(0f, 1.2f, 26f), "TO THE COMMAND HUB",
                out var hubBtn, out var hubTransition);
            var htSo = new SerializedObject(hubTransition);
            htSo.FindProperty("onFootScene").stringValue = Galaxy1Ep05CommandHubSceneName;
            htSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(hubBtn.onClick,
                new UnityEngine.Events.UnityAction(hubTransition.LoadOnFootScene));
            hubBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 9;

            // Step 0: Dialogue rotunda_alert (Khall over comms, auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Rotunda Alert";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = rotundaAlertDialogue;

            // Step 1: Trigger — alarm lights (first pair).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s1.FindPropertyRelative("label").stringValue = "Trigger: Rotunda Alarms";
            var t1 = s1.FindPropertyRelative("triggerObjects");
            t1.arraySize = 1;
            t1.GetArrayElementAtIndex(0).objectReferenceValue = alarmLights1Go;

            // Step 2: DefeatWaves — 5 soldiers (4 + 1 officer).
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s2.FindPropertyRelative("label").stringValue = "DefeatWaves: 4 Soldiers + 1 Officer";
            s2.FindPropertyRelative("waveSpawner").objectReferenceValue = wave1Spawner;

            // Step 3: Dialogue rotunda_choice (talk-gated).
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s3.FindPropertyRelative("label").stringValue = "Dialogue: Rotunda Choice";
            s3.FindPropertyRelative("dialogue").objectReferenceValue = rotundaChoiceDialogue;

            // Step 4: ReachTrigger — bridge doorway.
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s4.FindPropertyRelative("label").stringValue = "ReachTrigger: Bridge Doorway";
            s4.FindPropertyRelative("reachPoint").objectReferenceValue = bridgeDoorwayReachGo.transform;
            s4.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 5: Trigger — alarm lights (second pair, at bridge).
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s5.FindPropertyRelative("label").stringValue = "Trigger: Bridge Alarms";
            var t5 = s5.FindPropertyRelative("triggerObjects");
            t5.arraySize = 1;
            t5.GetArrayElementAtIndex(0).objectReferenceValue = alarmLights2Go;

            // Step 6: DefeatWaves — 2 boarders.
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s6.FindPropertyRelative("label").stringValue = "DefeatWaves: 2 Boarders";
            s6.FindPropertyRelative("waveSpawner").objectReferenceValue = wave2Spawner;

            // Step 7: Dialogue bridge_others (talk-gated).
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s7.FindPropertyRelative("label").stringValue = "Dialogue: Bridge Others";
            s7.FindPropertyRelative("dialogue").objectReferenceValue = bridgeOthersDialogue;

            // Step 8: Prompt — transition to Command Hub.
            var s8 = stepsProp.GetArrayElementAtIndex(8);
            s8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s8.FindPropertyRelative("label").stringValue = "Prompt: To the Command Hub";
            s8.FindPropertyRelative("promptObject").objectReferenceValue = hubBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep05RotundaScenePath);
            EnsureScenesInBuild(Galaxy1Ep05RotundaScenePath);

            Debug.Log($"[Space Samurai] EP05 Rotunda scene built at {Galaxy1Ep05RotundaScenePath}. " +
                      "Layout: central chamber (crane, reactor-cooling props, cool grey + amber accretion glow) → bridge room (consoles, viewport). " +
                      "9 steps: rotunda_alert auto → trigger alarms → defeat 5 soldiers (4+Officer 2x) + barks → rotunda_choice → reach bridge doorway → " +
                      "trigger bridge alarms → defeat 2 boarders + barks → bridge_others → transition to Command Hub.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP05 Command Hub", priority = 84)]
        public static void BuildEp05CommandHub()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Command hub: dark, intense orange accretion-glare, life-support core pulsing,
            // holo-panel emissive props, catwalk over void.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.7f, 0.73f);
            light.intensity = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.12f, 0.14f);

            // Dark void fog with orange-tinted accretion glow.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.25f, 0.15f, 0.08f);
            RenderSettings.fogDensity = 0.015f;

            // Intense orange accretion-glare accent lights (outer hull zone).
            BuildAccentPointLight("AccretionGlare1", new Vector3(-5f, 2.6f, 12f),
                new Color(1f, 0.5f, 0.2f), intensity: 1.8f, range: 14f);
            BuildAccentPointLight("AccretionGlare2", new Vector3(5f, 2.6f, 18f),
                new Color(0.95f, 0.45f, 0.15f), intensity: 1.7f, range: 13f);

            // ---- Command hub: archive alcove (z 0-6) -> outer-hull walkway (z 6-20) ->
            // hub arena (z 20-36).
            var hubGo = new GameObject("CommandHubInterior");
            var hub = hubGo.transform;
            var darkSteel = new Color(0.18f, 0.18f, 0.2f);
            var voidBlack = new Color(0.08f, 0.08f, 0.1f);

            // Archive alcove: x[-4,4], z[0,6].
            BuildFloorCeiling(hub, "ArchiveAlcove", new Vector3(0f, 0f, 3f), new Vector3(8f, 0f, 6f), darkSteel, voidBlack);
            BuildWall(hub, "Alcove_WallW", new Vector3(-4f, 1.5f, 3f), new Vector3(0.2f, 3f, 6f));
            BuildWall(hub, "Alcove_WallE", new Vector3(4f, 1.5f, 3f), new Vector3(0.2f, 3f, 6f));
            BuildWall(hub, "Alcove_WallBack", new Vector3(0f, 1.5f, 6f), new Vector3(8f, 3f, 0.2f));

            // Terminal prop in archive alcove.
            BuildProp(hub, "ArchiveTerminal", new Vector3(0f, 0.7f, 3f), new Vector3(1.4f, 1.4f, 0.6f), new Color(0.1f, 0.12f, 0.15f));

            // Emissive screen quad (no collider) on back wall of alcove.
            var screenQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screenQuad.name = "ArchiveScreen";
            screenQuad.transform.SetParent(hub, false);
            screenQuad.transform.localPosition = new Vector3(0f, 1.5f, 6f);
            screenQuad.transform.localScale = new Vector3(4f, 2.5f, 1f);
            TintShared(screenQuad.GetComponent<Renderer>(), new Color(0.15f, 0.25f, 0.4f, 0.7f));
            var sqCollider = screenQuad.GetComponent<Collider>();
            if (sqCollider != null) Object.DestroyImmediate(sqCollider);

            // Outer-hull walkway: x[-6,6], z[6,20], dark narrow catwalk strips over void.
            BuildFloorCeiling(hub, "OuterWalkway", new Vector3(0f, 0f, 13f), new Vector3(12f, 0f, 14f),
                new Color(0.15f, 0.15f, 0.17f), voidBlack);
            BuildWall(hub, "Hull_WallW", new Vector3(-6f, 1.5f, 13f), new Vector3(0.2f, 3f, 14f));
            BuildWall(hub, "Hull_WallE", new Vector3(6f, 1.5f, 13f), new Vector3(0.2f, 3f, 14f));

            // Catwalk strips (raised platforms, dark).
            var catwalkDark = new Color(0.22f, 0.22f, 0.24f);
            BuildProp(hub, "CatwalkLeft", new Vector3(-4f, 0.4f, 10f), new Vector3(1.5f, 0.2f, 6f), catwalkDark);
            BuildProp(hub, "CatwalkCenter", new Vector3(0f, 0.4f, 15f), new Vector3(2f, 0.2f, 6f), catwalkDark);

            // Hub arena: x[-7,7], z[20,36], command center with holo-panels and life-support core.
            BuildFloorCeiling(hub, "HubArena", new Vector3(0f, 0f, 28f), new Vector3(14f, 0f, 16f), darkSteel, voidBlack);
            BuildWall(hub, "Arena_WallW", new Vector3(-7f, 1.5f, 28f), new Vector3(0.2f, 3f, 16f));
            BuildWall(hub, "Arena_WallE", new Vector3(7f, 1.5f, 28f), new Vector3(0.2f, 3f, 16f));

            // Holo-panel emissive props (floating panels, no colliders).
            var panelColor = new Color(0.3f, 0.5f, 0.8f, 0.8f);
            var panel1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel1.name = "HoloPanel1";
            panel1.transform.SetParent(hub, false);
            panel1.transform.localPosition = new Vector3(-3f, 1.2f, 24f);
            panel1.transform.localScale = new Vector3(2f, 1.5f, 0.2f);
            TintShared(panel1.GetComponent<Renderer>(), panelColor);
            var p1col = panel1.GetComponent<Collider>();
            if (p1col != null) Object.DestroyImmediate(p1col);

            var panel2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel2.name = "HoloPanel2";
            panel2.transform.SetParent(hub, false);
            panel2.transform.localPosition = new Vector3(3f, 1.2f, 30f);
            panel2.transform.localScale = new Vector3(2f, 1.5f, 0.2f);
            TintShared(panel2.GetComponent<Renderer>(), panelColor);
            var p2col = panel2.GetComponent<Collider>();
            if (p2col != null) Object.DestroyImmediate(p2col);

            // Life-support core prop (pulsing emissive center).
            BuildProp(hub, "LifeSupportCore", new Vector3(0f, 1f, 28f), new Vector3(1.5f, 2f, 1.5f),
                new Color(0.2f, 0.4f, 0.6f));

            // Game root.
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // Player rig + katana + bounds.
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 60f;
            BuildSword(new Vector3(0f, 1f, 0f), weapon);

            var talkRef = FindRef(refs, "Left Hand", "Talk");

            // ---- NPCs ----
            // Ronin-9 near the archive alcove.
            var ronin9Pos = new Vector3(-1.5f, 1f, 3f);
            var ronin9Go = InstantiateNpc(ArtPrefabBuilder.Ronin9PrefabPath, ronin9Pos, "Ronin9");
            if (ronin9Go != null)
            {
                var ronin9Npc = ronin9Go.AddComponent<StoryNpc>();
                var r9So = new SerializedObject(ronin9Npc);
                r9So.FindProperty("displayName").stringValue = "Ronin-9";
                r9So.FindProperty("remote").boolValue = false;
                r9So.ApplyModifiedPropertiesWithoutUndo();
            }

            // Kessler near archive alcove (other side).
            var kesslerPos = new Vector3(1.5f, 1f, 3f);
            var kesslerGo = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler");
            if (kesslerGo != null)
            {
                var kesslerNpc = kesslerGo.AddComponent<StoryNpc>();
                var kSo = new SerializedObject(kesslerNpc);
                kSo.FindProperty("displayName").stringValue = "Kessler";
                kSo.FindProperty("remote").boolValue = false;
                kSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Khall hologram near hub center (inactive, visual anchor).
            var khallPos = new Vector3(0f, 1f, 28f);
            var khallHoloGo = InstantiateNpc(ArtPrefabBuilder.KhallHologramPrefabPath, khallPos, "KhallHologram");
            if (khallHoloGo != null)
            {
                khallHoloGo.SetActive(false);
            }

            // ---- Alarm lights (during outer hull phase). ----
            var hullAlarmLightsGo = BuildEp03AlarmLights(new Vector3(-4f, 2.8f, 10f), new Vector3(4f, 2.8f, 16f));

            // ---- Dialogue Players ----
            var archiveReadingDialogue = BuildEp05DialoguePlayer("Dialogue_ArchiveReading", ronin9Pos, "archive_reading", talkRef);
            var hullEliteBarksDialogue = BuildEp05DialoguePlayer("Dialogue_HullEliteBarks", new Vector3(0f, 1.5f, 13f), "hull_elite_barks");
            var hullReinforcementsDialogue = BuildEp05DialoguePlayer("Dialogue_HullReinforcements", new Vector3(0f, 1.5f, 13f), "hull_reinforcements", talkRef);
            var khallDuelOpenDialogue = BuildEp05DialoguePlayer("Dialogue_KhallDuelOpen", new Vector3(0f, 1.5f, 28f), "khall_duel_open");
            var khallDuelBarksDialogue = BuildEp05DialoguePlayer("Dialogue_KhallDuelBarks", new Vector3(0f, 1.5f, 28f), "khall_duel_barks");
            var khallDuelAfterDialogue = BuildEp05DialoguePlayer("Dialogue_KhallDuelAfter", new Vector3(0f, 1.5f, 28f), "khall_duel_after", talkRef);
            var spareChoiceDialogue = BuildEp05DialoguePlayer("Dialogue_SpareChoice", new Vector3(0f, 1.5f, 28f), "spare_choice", talkRef);
            var escapeThreatDialogue = BuildEp05DialoguePlayer("Dialogue_EscapeThreat", new Vector3(0f, 1.5f, 28f), "escape_threat");

            // ---- Enemies: Outer Hull Wave (3 Elite Operatives, jet-black, 2x health each) ----
            var eliteBlack = new Color(0.15f, 0.15f, 0.18f);
            var elitePositions = new Vector3[]
            {
                new Vector3(-3f, 0f, 8f),
                new Vector3(0f, 0f, 12f),
                new Vector3(3f, 0f, 16f)
            };

            var hullWaveHealths = new List<Health>();
            foreach (var pos in elitePositions)
            {
                var enemy = BuildDominionEnemy(pos, playerHealth, enemyDef);
                var renderer = enemy.GetComponent<Renderer>();
                if (renderer != null) TintShared(renderer, eliteBlack);
                var enemyHealth = enemy.GetComponent<Health>();
                if (enemyHealth != null)
                {
                    var ehSo = new SerializedObject(enemyHealth);
                    ehSo.FindProperty("maxHealth").floatValue = ehSo.FindProperty("maxHealth").floatValue * 2f;
                    ehSo.ApplyModifiedPropertiesWithoutUndo();
                }
                enemy.gameObject.SetActive(false);
                hullWaveHealths.Add(enemyHealth);
            }

            var hullWaveSpawner = BuildWaveSpawner("HullWaveSpawner", new Vector3(0f, 1f, 12f), 3f,
                new List<List<Health>> { hullWaveHealths }, new[] { hullEliteBarksDialogue });

            // ---- Enemy: Khall Duel (dark-crimson/black tint, 5x health, single wave) ----
            var khallCrimson = new Color(0.4f, 0.15f, 0.15f);
            var khall = BuildDominionEnemy(new Vector3(0f, 0f, 28f), playerHealth, enemyDef);
            var khallRenderer = khall.GetComponent<Renderer>();
            if (khallRenderer != null) TintShared(khallRenderer, khallCrimson);
            var khallHealth = khall.GetComponent<Health>();
            if (khallHealth != null)
            {
                var khSo = new SerializedObject(khallHealth);
                khSo.FindProperty("maxHealth").floatValue = khSo.FindProperty("maxHealth").floatValue * 5f;
                khSo.ApplyModifiedPropertiesWithoutUndo();
            }
            khall.gameObject.SetActive(false);

            var khallWaveSpawner = BuildWaveSpawner("KhallWaveSpawner", new Vector3(0f, 1f, 28f), 3f,
                new List<List<Health>> { new List<Health> { khallHealth } }, new[] { khallDuelBarksDialogue });

            // Reach triggers.
            var outerhullReachGo = new GameObject("OuterHullReachPoint");
            outerhullReachGo.transform.position = new Vector3(0f, 1f, 13f);

            var hubReachGo = new GameObject("HubReachPoint");
            hubReachGo.transform.position = new Vector3(0f, 1f, 28f);

            // Transition box: "LAUNCH TO SPACE" wired to LoadOnFootScene (transition to EP06 Approach).
            var launchBoxGo = BuildTransitionBox("LaunchToSpaceBox", new Vector3(0f, 1.2f, 34f), "LAUNCH TO SPACE",
                out var launchBtn, out var launchTransition);
            var ltSo = new SerializedObject(launchTransition);
            ltSo.FindProperty("onFootScene").stringValue = "Galaxy1_EP06_Approach";
            ltSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(launchBtn.onClick,
                new UnityEngine.Events.UnityAction(launchTransition.LoadOnFootScene));
            launchBoxGo.SetActive(false);

            // ---- Mission Director ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 12;

            // Step 0: Dialogue archive_reading (talk-gated).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Archive Reading";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = archiveReadingDialogue;

            // Step 1: ReachTrigger — outer hull walkway.
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s1.FindPropertyRelative("label").stringValue = "ReachTrigger: Outer Hull Walkway";
            s1.FindPropertyRelative("reachPoint").objectReferenceValue = outerhullReachGo.transform;
            s1.FindPropertyRelative("reachRadius").floatValue = 3f;

            // Step 2: Trigger — alarm lights.
            var s2 = stepsProp.GetArrayElementAtIndex(2);
            s2.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Trigger;
            s2.FindPropertyRelative("label").stringValue = "Trigger: Hull Alarms";
            var t2 = s2.FindPropertyRelative("triggerObjects");
            t2.arraySize = 1;
            t2.GetArrayElementAtIndex(0).objectReferenceValue = hullAlarmLightsGo;

            // Step 3: DefeatWaves — 3 Elite Operatives.
            var s3 = stepsProp.GetArrayElementAtIndex(3);
            s3.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s3.FindPropertyRelative("label").stringValue = "DefeatWaves: 3 Elite Operatives (2x health)";
            s3.FindPropertyRelative("waveSpawner").objectReferenceValue = hullWaveSpawner;

            // Step 4: Dialogue hull_reinforcements (talk-gated).
            var s4 = stepsProp.GetArrayElementAtIndex(4);
            s4.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s4.FindPropertyRelative("label").stringValue = "Dialogue: Hull Reinforcements";
            s4.FindPropertyRelative("dialogue").objectReferenceValue = hullReinforcementsDialogue;

            // Step 5: ReachTrigger — command hub center.
            var s5 = stepsProp.GetArrayElementAtIndex(5);
            s5.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.ReachTrigger;
            s5.FindPropertyRelative("label").stringValue = "ReachTrigger: Command Hub";
            s5.FindPropertyRelative("reachPoint").objectReferenceValue = hubReachGo.transform;
            s5.FindPropertyRelative("reachRadius").floatValue = 4f;

            // Step 6: Dialogue khall_duel_open (auto).
            var s6 = stepsProp.GetArrayElementAtIndex(6);
            s6.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s6.FindPropertyRelative("label").stringValue = "Dialogue: Khall Duel Open";
            s6.FindPropertyRelative("dialogue").objectReferenceValue = khallDuelOpenDialogue;

            // Step 7: DefeatWaves — Khall duel (5x health, single wave).
            var s7 = stepsProp.GetArrayElementAtIndex(7);
            s7.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.DefeatWaves;
            s7.FindPropertyRelative("label").stringValue = "DefeatWaves: Khall Duel (5x health)";
            s7.FindPropertyRelative("waveSpawner").objectReferenceValue = khallWaveSpawner;

            // Step 8: Dialogue khall_duel_after (talk-gated).
            var s8 = stepsProp.GetArrayElementAtIndex(8);
            s8.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s8.FindPropertyRelative("label").stringValue = "Dialogue: After the Duel";
            s8.FindPropertyRelative("dialogue").objectReferenceValue = khallDuelAfterDialogue;

            // Step 9: Dialogue spare_choice (talk-gated).
            var s9 = stepsProp.GetArrayElementAtIndex(9);
            s9.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s9.FindPropertyRelative("label").stringValue = "Dialogue: Spare Choice";
            s9.FindPropertyRelative("dialogue").objectReferenceValue = spareChoiceDialogue;

            // Step 10: Dialogue escape_threat (auto).
            var s10 = stepsProp.GetArrayElementAtIndex(10);
            s10.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s10.FindPropertyRelative("label").stringValue = "Dialogue: Escape Threat";
            s10.FindPropertyRelative("dialogue").objectReferenceValue = escapeThreatDialogue;

            // Step 11: Prompt — launch to space (LoadOnFootScene, chains into EP06 Approach).
            var s11 = stepsProp.GetArrayElementAtIndex(11);
            s11.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s11.FindPropertyRelative("label").stringValue = "Prompt: Launch to Space";
            s11.FindPropertyRelative("promptObject").objectReferenceValue = launchBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep05CommandHubScenePath);
            EnsureScenesInBuild(Galaxy1Ep05CommandHubScenePath);

            Debug.Log($"[Space Samurai] EP05 Command Hub scene built at {Galaxy1Ep05CommandHubScenePath}. " +
                      "Layout: archive alcove (terminal, emissive screen, Ronin-9 + Kessler staged) → outer-hull walkway (dark catwalk, intense orange accretion-glare) → " +
                      "hub arena (holo-panels, life-support core, Khall hologram anchor). " +
                      "12 steps: archive_reading → reach outer hull → trigger alarms → defeat 3 Elite (2x) + barks → hull_reinforcements → reach hub → " +
                      "khall_duel_open auto → defeat Khall duel (5x, crimson-black) + barks → khall_duel_after → spare_choice → escape_threat → " +
                      "LAUNCH TO SPACE (LoadOnFootScene to Galaxy1_EP06_Approach, chains to EP06).");
        }

        // ---- Moved from Ep06Builder.cs (deleted) — BuildEp06DialoguePlayer is still needed by
        // BuildEp06Approach/BuildEp06Escape (moved below from Ep06BuilderSpace.cs). ----
        /// <summary>Shorthand for building a DialoguePlayer with EP06 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep06" and loads lines from Ep06Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp06DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep06Lines.Get(setId), advanceRef, setId, clipPrefix: "ep06");
        }

        // ---- Moved from Ep06BuilderSpace.cs (deleted) — BuildEp06Approach/BuildEp06Escape are
        // called directly by EnemyWarningBuilder.BuildAllSpaceCombatScenes. ----
        private const string Galaxy1Ep06ApproachScenePath = SceneFolder + "/Galaxy1_EP06_Approach.unity";
        private const string Galaxy1Ep06EscapeScenePath = SceneFolder + "/Galaxy1_EP06_Escape.unity";

        private static readonly string Galaxy1Ep06ApproachSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep06ApproachScenePath);
        private static readonly string Galaxy1Ep06EscapeSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep06EscapeScenePath);

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP06 Approach", priority = 87)]
        public static void BuildEp06Approach()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space is a black void: kill fog, drop ambient to a faint cool fill.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.025f, 0.035f);
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 40f, 0f);

            var gameGo = new GameObject("Game");
            var gs = gameGo.AddComponent<GameState>();
            var gsSo = new SerializedObject(gs);
            var sm = gsSo.FindProperty("startMode");
            if (sm != null) sm.enumValueIndex = (int)GameMode.SpaceFlight;
            gsSo.ApplyModifiedPropertiesWithoutUndo();

            // Seated flight rig: stationary at the origin, NO locomotion.
            var rig = BuildRig(refs, addLocomotion: false);

            // Far clip to cover Frosthold planet (icy white-blue sphere at a distance).
            var vrRig = rig.GetComponent<VRRig>();
            var cam = vrRig != null && vrRig.Head != null ? vrRig.Head.GetComponent<Camera>() : null;
            if (cam != null) cam.farClipPlane = 6000f;

            // Player ship Health for damage relay.
            if (rig.GetComponent<Health>() == null) rig.AddComponent<Health>();
            rig.AddComponent<PlayerShipDamageRelay>();

            // Hull collider at origin for damage.
            var hull = new GameObject("Ship Hull (Damage Volume)");
            hull.transform.SetParent(rig.transform, false);
            hull.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var hullCol = hull.AddComponent<SphereCollider>();
            hullCol.radius = 1.2f;
            hullCol.isTrigger = false;

            var cockpit = new GameObject("Cockpit").transform;
            cockpit.SetParent(rig.transform, false);
            cockpit.localPosition = Vector3.zero;

            // Sleek shared cockpit + runtime exterior hull (replaces the old inline canopy/HUD box).
            BuildPlayerShipVisual(cockpit);

            EnemyWarningBuilder.AddTo(cockpit, new Vector3(0.85f, 1.4f, -1.3f));

            // Starfield dome (shared helper).
            BuildStarfield(null, 5000f, 1500);

            // Universe root: the ship "flies" by moving this root past the stationary cockpit.
            var universe = new GameObject("Universe").transform;

            // Frosthold planet: icy white-blue sphere distant from the approach.
            var planetGo = AddUnlitVisual(universe, "Frosthold", new Vector3(2000f, -500f, 3000f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.7f, 0.85f, 1f));
            var planetCollider = planetGo.GetComponent<Collider>();
            if (planetCollider != null) planetCollider.isTrigger = true;

            // Flight controller, wired identically to the Galaxy1 cockpit (universe + sticks).
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var scSo = new SerializedObject(shipCtrl);
            SetObjectRef(scSo, "universe", universe);
            SetObjectRef(scSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(scSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            scSo.ApplyModifiedPropertiesWithoutUndo();

            // Shared bolt pool (player + enemies draw from this one bounded pool).
            var poolGo = new GameObject("Projectile Pool");
            var pool = poolGo.AddComponent<ProjectilePool>();

            // Player guns mounted on the cockpit (Galaxy1 pattern: twin muzzles flank the canopy).
            var gunsGo = new GameObject("Ship Guns");
            gunsGo.transform.SetParent(cockpit, false);
            var muzzleL = new GameObject("Muzzle L").transform;
            muzzleL.SetParent(gunsGo.transform, false);
            muzzleL.localPosition = new Vector3(-0.5f, 1.0f, 0.8f);
            var muzzleR = new GameObject("Muzzle R").transform;
            muzzleR.SetParent(gunsGo.transform, false);
            muzzleR.localPosition = new Vector3(0.5f, 1.0f, 0.8f);

            var guns = gunsGo.AddComponent<ShipWeaponController>();
            var gunsSo = new SerializedObject(guns);
            SetObjectRef(gunsSo, "pool", pool);
            SetObjectRef(gunsSo, "definition", shipWeapon);
            SetObjectRef(gunsSo, "fireAction", FindRef(refs, "Right Hand", "Activate"));
            SetObjectRef(gunsSo, "ownerRoot", rig);
            SetObjectRefList(gunsSo, "muzzles", new List<Object> { muzzleL, muzzleR });
            gunsSo.ApplyModifiedPropertiesWithoutUndo();

            // Holographic gunsight reticle, wired to the player guns.
            BuildCockpitCrosshair(cockpit, guns);

            // ---- Dialogue Players ----
            // Opening cockpit dialogue: approach_transmission (Kessler's intel on Frosthold).
            var approachDialogue = BuildEp06DialoguePlayer("Dialogue_ApproachTransmission", new Vector3(0f, 1.62f, 0.8f),
                "approach_transmission");
            var approachDpGo = approachDialogue.gameObject;
            approachDpGo.transform.SetParent(cockpit, false);
            approachDpGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            approachDpGo.transform.localRotation = Quaternion.identity;

            // ---- Kessler co-pilot (if present from prior scenes) ----
            // Kessler is already on the ship from EP05; no need to instantiate again.
            // The cockpit dialogue will be driven by the approach_transmission set.

            // ---- Enemy Encounter: Dominion Pursuit (3 ships) ----
            // Copy EP04/05 pursuit pattern: guardTarget = null, spawn around player.
            var pursuitGo = new GameObject("Dominion Pursuit");
            var pursuit = pursuitGo.AddComponent<GuardEncounter>();
            var pursuitSo = new SerializedObject(pursuit);
            SetObjectRef(pursuitSo, "player", shipCtrl);
            SetObjectRef(pursuitSo, "universe", universe);
            SetObjectRef(pursuitSo, "pool", pool);
            SetObjectRef(pursuitSo, "definition", enemyShipDef);
            // Pursuit mode: guardTarget = null, spawn around player
            pursuitSo.FindProperty("shipCount").intValue = 3;
            pursuitSo.FindProperty("spawnRadius").floatValue = 260f;
            pursuitSo.FindProperty("initialDelay").floatValue = 6f;
            // Scene is only reachable via the EP05 chain, so the pursuit is always active.
            pursuitSo.FindProperty("requiredCompletedScene").stringValue = "";
            pursuitSo.FindProperty("clearedFlag").stringValue = "ep06_pursuit_cleared";

            // Build spawn dialogue for this encounter
            var orbitalUltimatum = BuildEp06DialoguePlayer("Dialogue_OrbitalUltimatum", new Vector3(0f, 1.62f, 0.8f),
                "orbital_ultimatum");
            var orbitalUltimGo = orbitalUltimatum.gameObject;
            orbitalUltimGo.transform.SetParent(cockpit, false);
            orbitalUltimGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            orbitalUltimGo.transform.localRotation = Quaternion.identity;
            var orbitalDpSo = new SerializedObject(orbitalUltimatum);
            orbitalDpSo.FindProperty("playOnStart").boolValue = false;
            orbitalDpSo.ApplyModifiedPropertiesWithoutUndo();
            SetObjectRef(pursuitSo, "spawnDialogue", orbitalUltimatum);
            pursuitSo.ApplyModifiedPropertiesWithoutUndo();

            // Landing prompt for transition to on-foot medical compound.
            var prompt = BuildLandingPrompt(cockpit);
            var landingGo = new GameObject("Landing Approach");
            var landing = landingGo.AddComponent<LandingApproach>();
            var lso = new SerializedObject(landing);
            SetObjectRef(lso, "ship", shipCtrl);
            SetObjectRef(lso, "universe", universe);
            SetObjectRef(lso, "landAction", FindRef(refs, "Right Hand", "Select"));
            SetObjectRef(lso, "promptText", prompt);
            lso.FindProperty("maxLandingSpeed").floatValue = 12f;
            var landables = lso.FindProperty("landables");
            if (landables != null)
            {
                landables.arraySize = 1;
                // Single landable: Frosthold Medical Compound (the on-foot scene).
                var frostHoldEntry = landables.GetArrayElementAtIndex(0);
                frostHoldEntry.FindPropertyRelative("target").objectReferenceValue = planetGo.transform;
                frostHoldEntry.FindPropertyRelative("approachRadius").floatValue = 140f;
                frostHoldEntry.FindPropertyRelative("destinationScene").stringValue = "Galaxy1_EP06_MedicalCompound";
            }
            lso.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep06ApproachScenePath);
            EnsureScenesInBuild(Galaxy1Ep06ApproachScenePath);

            Debug.Log($"[Space Samurai] EP06 Approach scene built at {Galaxy1Ep06ApproachScenePath}. " +
                      "Space over Frosthold (icy white-blue planet). " +
                      "Cockpit with canopy + HUD, Kessler co-pilot, 3-ship Dominion pursuit with orbital_ultimatum comms. " +
                      "Landing to Galaxy1_EP06_MedicalCompound. Reached via EP05 CommandHub launch.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP06 Escape", priority = 88)]
        public static void BuildEp06Escape()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: same dark void as Approach.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.025f, 0.035f);
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 40f, 0f);

            var gameGo = new GameObject("Game");
            var gs = gameGo.AddComponent<GameState>();
            var gsSo = new SerializedObject(gs);
            var sm = gsSo.FindProperty("startMode");
            if (sm != null) sm.enumValueIndex = (int)GameMode.SpaceFlight;
            gsSo.ApplyModifiedPropertiesWithoutUndo();

            // Seated flight rig: NO locomotion.
            var rig = BuildRig(refs, addLocomotion: false);

            var vrRig = rig.GetComponent<VRRig>();
            var cam = vrRig != null && vrRig.Head != null ? vrRig.Head.GetComponent<Camera>() : null;
            if (cam != null) cam.farClipPlane = 6000f;

            if (rig.GetComponent<Health>() == null) rig.AddComponent<Health>();
            rig.AddComponent<PlayerShipDamageRelay>();

            var hull = new GameObject("Ship Hull (Damage Volume)");
            hull.transform.SetParent(rig.transform, false);
            hull.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var hullCol = hull.AddComponent<SphereCollider>();
            hullCol.radius = 1.2f;
            hullCol.isTrigger = false;

            var cockpit = new GameObject("Cockpit").transform;
            cockpit.SetParent(rig.transform, false);
            cockpit.localPosition = Vector3.zero;

            // Sleek shared cockpit + runtime exterior hull (replaces the old inline canopy/HUD box).
            // No-combat escape scene, so no gun crosshair.
            BuildPlayerShipVisual(cockpit);

            EnemyWarningBuilder.AddTo(cockpit, new Vector3(0.85f, 1.4f, -1.3f));

            // Starfield dome.
            BuildStarfield(null, 5000f, 1500);

            // Universe root: the ship "flies" by moving this root past the stationary cockpit.
            var universe = new GameObject("Universe").transform;

            // Frosthold behind the escape.
            var planetGo = AddUnlitVisual(universe, "Frosthold", new Vector3(-2000f, -500f, -3000f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.7f, 0.85f, 1f));
            var planetCollider = planetGo.GetComponent<Collider>();
            if (planetCollider != null) planetCollider.isTrigger = true;

            // Flight controller (no combat in this scene, so no guns/pool needed).
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var scSo = new SerializedObject(shipCtrl);
            SetObjectRef(scSo, "universe", universe);
            SetObjectRef(scSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(scSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            scSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Player ----
            // Escape jammer dialogue: the failsafe is activated and returns player to galaxy map.
            var escapeDialogue = BuildEp06DialoguePlayer("Dialogue_EscapeJammer", new Vector3(0f, 1.62f, 0.8f),
                "escape_jammer");
            var escapeDpGo = escapeDialogue.gameObject;
            escapeDpGo.transform.SetParent(cockpit, false);
            escapeDpGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            escapeDpGo.transform.localRotation = Quaternion.identity;

            // Transition box: "CONTINUE TO BLACKVEIL YARDS" wired to LoadOnFootScene (chains to EP07 Approach).
            var returnBoxGo = BuildTransitionBox("ReturnToSpaceBox", new Vector3(0f, 1.2f, 0.8f),
                "CONTINUE TO BLACKVEIL YARDS", out var returnBtn, out var returnTransition);
            var rtSo = new SerializedObject(returnTransition);
            rtSo.FindProperty("onFootScene").stringValue = "Galaxy1_EP07_Approach";
            rtSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.LoadOnFootScene));
            returnBoxGo.SetActive(false);

            // Mission Director for the escape sequence.
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 2;

            // Step 0: Dialogue escape_jammer (jammer activation, auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Escape Jammer";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = escapeDialogue;

            // Step 1: Prompt — return to galaxy map (ReturnToSpace, marks EP06 complete).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Return to Galaxy Map";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = returnBoxGo;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep06EscapeScenePath);
            EnsureScenesInBuild(Galaxy1Ep06EscapeScenePath);

            Debug.Log($"[Space Samurai] EP06 Escape scene built at {Galaxy1Ep06EscapeScenePath}. " +
                      "Space leaving Frosthold (planet receding). Cockpit with canopy + HUD. " +
                      "2 steps: escape_jammer auto → CONTINUE TO BLACKVEIL YARDS (LoadOnFootScene to Galaxy1_EP07_Approach, chains to EP07).");
        }

        // ---- Moved from Ep07Builder.cs (deleted) — BuildEp07DialoguePlayer is still needed by
        // BuildEp07Approach/BuildEp07Escape (moved below from Ep07BuilderSpace.cs). ----
        /// <summary>Shorthand for building a DialoguePlayer with EP07 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep07" and loads lines from Ep07Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp07DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep07Lines.Get(setId), advanceRef, setId, clipPrefix: "ep07");
        }

        // ---- Moved from Ep07BuilderSpace.cs (deleted) — BuildEp07Approach/BuildEp07Escape are
        // called directly by EnemyWarningBuilder.BuildAllSpaceCombatScenes. ----
        private const string Galaxy1Ep07ApproachScenePath = SceneFolder + "/Galaxy1_EP07_Approach.unity";
        private const string Galaxy1Ep07EscapeScenePath = SceneFolder + "/Galaxy1_EP07_Escape.unity";

        private static readonly string Galaxy1Ep07ApproachSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep07ApproachScenePath);
        private static readonly string Galaxy1Ep07EscapeSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep07EscapeScenePath);

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP07 Approach", priority = 97)]
        public static void BuildEp07Approach()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space is a black void: kill fog, drop ambient to a faint cool fill.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.025f, 0.035f);
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 40f, 0f);

            var gameGo = new GameObject("Game");
            var gs = gameGo.AddComponent<GameState>();
            var gsSo = new SerializedObject(gs);
            var sm = gsSo.FindProperty("startMode");
            if (sm != null) sm.enumValueIndex = (int)GameMode.SpaceFlight;
            gsSo.ApplyModifiedPropertiesWithoutUndo();

            // Seated flight rig: stationary at the origin, NO locomotion.
            var rig = BuildRig(refs, addLocomotion: false);

            // Far clip to cover Blackveil moon and distant sun.
            var vrRig = rig.GetComponent<VRRig>();
            var cam = vrRig != null && vrRig.Head != null ? vrRig.Head.GetComponent<Camera>() : null;
            if (cam != null) cam.farClipPlane = 6000f;

            // Player ship Health for damage relay.
            if (rig.GetComponent<Health>() == null) rig.AddComponent<Health>();
            rig.AddComponent<PlayerShipDamageRelay>();

            // Hull collider at origin for damage.
            var hull = new GameObject("Ship Hull (Damage Volume)");
            hull.transform.SetParent(rig.transform, false);
            hull.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var hullCol = hull.AddComponent<SphereCollider>();
            hullCol.radius = 1.2f;
            hullCol.isTrigger = false;

            var cockpit = new GameObject("Cockpit").transform;
            cockpit.SetParent(rig.transform, false);
            cockpit.localPosition = Vector3.zero;

            // Sleek shared cockpit + runtime exterior hull (replaces the old inline canopy/HUD box).
            BuildPlayerShipVisual(cockpit);

            EnemyWarningBuilder.AddTo(cockpit, new Vector3(0.85f, 1.4f, -1.3f));

            // Starfield dome.
            BuildStarfield(null, 5000f, 1500);

            // Universe root: the ship "flies" by moving this root past the stationary cockpit.
            var universe = new GameObject("Universe").transform;

            // Blackveil moon: dead grey-brown industrial body with scattered debris.
            var blackveilGo = AddUnlitVisual(universe, "Blackveil", new Vector3(2000f, -500f, 3000f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.35f, 0.3f, 0.25f));
            var blackveilCollider = blackveilGo.GetComponent<Collider>();
            if (blackveilCollider != null) blackveilCollider.isTrigger = true;

            // Debris field: scattered grey hull-plate chunks around Blackveil.
            for (int i = 0; i < 5; i++)
            {
                float angle = (i / 5f) * Mathf.PI * 2f;
                float distance = 1200f + i * 200f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * distance, -200f + i * 100f, Mathf.Sin(angle) * distance);
                var debrisGo = AddUnlitVisual(universe, $"Hull Plate {i}", pos,
                    Vector3.one * (150f + i * 50f), PrimitiveType.Cube, new Color(0.4f, 0.38f, 0.35f));
                var debrisCollider = debrisGo.GetComponent<Collider>();
                if (debrisCollider != null) debrisCollider.isTrigger = true;
            }

            // Flight controller, wired identically to the Galaxy1 cockpit (universe + sticks).
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var scSo = new SerializedObject(shipCtrl);
            SetObjectRef(scSo, "universe", universe);
            SetObjectRef(scSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(scSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            scSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Dialogue Players ----
            // Opening cockpit dialogue: approach_viewport (Cipher's realization over Blackveil).
            var approachDialogue = BuildEp07DialoguePlayer("Dialogue_ApproachViewport", new Vector3(0f, 1.62f, 0.8f),
                "approach_viewport");
            var approachDpGo = approachDialogue.gameObject;
            approachDpGo.transform.SetParent(cockpit, false);
            approachDpGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            approachDpGo.transform.localRotation = Quaternion.identity;

            // Landing prompt for transition to on-foot market hub.
            var prompt = BuildLandingPrompt(cockpit);
            var landingGo = new GameObject("Landing Approach");
            var landing = landingGo.AddComponent<LandingApproach>();
            var lso = new SerializedObject(landing);
            SetObjectRef(lso, "ship", shipCtrl);
            SetObjectRef(lso, "universe", universe);
            SetObjectRef(lso, "landAction", FindRef(refs, "Right Hand", "Select"));
            SetObjectRef(lso, "promptText", prompt);
            lso.FindProperty("maxLandingSpeed").floatValue = 12f;
            var landables = lso.FindProperty("landables");
            if (landables != null)
            {
                landables.arraySize = 1;
                // Single landable: Blackveil Market Hub (the on-foot scene).
                var blackveilEntry = landables.GetArrayElementAtIndex(0);
                blackveilEntry.FindPropertyRelative("target").objectReferenceValue = blackveilGo.transform;
                blackveilEntry.FindPropertyRelative("approachRadius").floatValue = 140f;
                blackveilEntry.FindPropertyRelative("destinationScene").stringValue = "Galaxy1_EP07_MarketHub";
            }
            lso.ApplyModifiedPropertiesWithoutUndo();

            // Mission Director for the approach sequence.
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();
            var mdSo = new SerializedObject(missionDirector);
            var stepsProp = mdSo.FindProperty("steps");
            stepsProp.arraySize = 2;

            // Step 0: Dialogue approach_viewport (auto).
            var s0 = stepsProp.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Dialogue;
            s0.FindPropertyRelative("label").stringValue = "Dialogue: Approach Viewport";
            s0.FindPropertyRelative("dialogue").objectReferenceValue = approachDialogue;

            // Step 1: Prompt — land on Blackveil (LoadOnFootScene, chains to EP07 MarketHub).
            var s1 = stepsProp.GetArrayElementAtIndex(1);
            s1.FindPropertyRelative("kind").enumValueIndex = (int)MissionStepKind.Prompt;
            s1.FindPropertyRelative("label").stringValue = "Prompt: Land on Blackveil";
            s1.FindPropertyRelative("promptObject").objectReferenceValue = prompt.gameObject;

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep07ApproachScenePath);
            EnsureScenesInBuild(Galaxy1Ep07ApproachScenePath);

            Debug.Log($"[Space Samurai] EP07 Approach scene built at {Galaxy1Ep07ApproachScenePath}. " +
                      "Space over Blackveil (dead industrial moon, grey-brown, debris field). " +
                      "Cockpit with canopy + HUD. Opening dialogue approach_viewport (Cipher's realization). " +
                      "Landing to Galaxy1_EP07_MarketHub. Reached via EP06 Escape chaining.");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP07 Escape", priority = 98)]
        public static void BuildEp07Escape()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: same dark void as Approach.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.025f, 0.035f);
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 40f, 0f);

            var gameGo = new GameObject("Game");
            var gs = gameGo.AddComponent<GameState>();
            var gsSo = new SerializedObject(gs);
            var sm = gsSo.FindProperty("startMode");
            if (sm != null) sm.enumValueIndex = (int)GameMode.SpaceFlight;
            gsSo.ApplyModifiedPropertiesWithoutUndo();

            // Seated flight rig: NO locomotion.
            var rig = BuildRig(refs, addLocomotion: false);

            var vrRig = rig.GetComponent<VRRig>();
            var cam = vrRig != null && vrRig.Head != null ? vrRig.Head.GetComponent<Camera>() : null;
            if (cam != null) cam.farClipPlane = 6000f;

            if (rig.GetComponent<Health>() == null) rig.AddComponent<Health>();
            rig.AddComponent<PlayerShipDamageRelay>();

            var hull = new GameObject("Ship Hull (Damage Volume)");
            hull.transform.SetParent(rig.transform, false);
            hull.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var hullCol = hull.AddComponent<SphereCollider>();
            hullCol.radius = 1.2f;
            hullCol.isTrigger = false;

            var cockpit = new GameObject("Cockpit").transform;
            cockpit.SetParent(rig.transform, false);
            cockpit.localPosition = Vector3.zero;

            // Sleek shared cockpit + runtime exterior hull (replaces the old inline canopy/HUD box).
            BuildPlayerShipVisual(cockpit);

            EnemyWarningBuilder.AddTo(cockpit, new Vector3(0.85f, 1.4f, -1.3f));

            // Starfield dome.
            BuildStarfield(null, 5000f, 1500);

            // Universe root: the ship "flies" by moving this root past the stationary cockpit.
            var universe = new GameObject("Universe").transform;

            // Blackveil receding behind the escape.
            var blackveilGo = AddUnlitVisual(universe, "Blackveil", new Vector3(-2000f, -500f, -3000f),
                Vector3.one * 600f, PrimitiveType.Sphere, new Color(0.35f, 0.3f, 0.25f));
            var blackveilCollider = blackveilGo.GetComponent<Collider>();
            if (blackveilCollider != null) blackveilCollider.isTrigger = true;

            // Debris field: scattered grey hull-plate chunks around Blackveil (receding).
            for (int i = 0; i < 5; i++)
            {
                float angle = (i / 5f) * Mathf.PI * 2f;
                float distance = 1200f + i * 200f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * distance * -1f, -200f + i * 100f, Mathf.Sin(angle) * distance * -1f);
                var debrisGo = AddUnlitVisual(universe, $"Hull Plate {i}", pos,
                    Vector3.one * (150f + i * 50f), PrimitiveType.Cube, new Color(0.4f, 0.38f, 0.35f));
                var debrisCollider = debrisGo.GetComponent<Collider>();
                if (debrisCollider != null) debrisCollider.isTrigger = true;
            }

            // Flight controller.
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var scSo = new SerializedObject(shipCtrl);
            SetObjectRef(scSo, "universe", universe);
            SetObjectRef(scSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(scSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            scSo.ApplyModifiedPropertiesWithoutUndo();

            // Shared bolt pool (player + enemies draw from this one bounded pool).
            var poolGo = new GameObject("Projectile Pool");
            var pool = poolGo.AddComponent<ProjectilePool>();

            // Player guns mounted on the cockpit.
            var gunsGo = new GameObject("Ship Guns");
            gunsGo.transform.SetParent(cockpit, false);
            var muzzleL = new GameObject("Muzzle L").transform;
            muzzleL.SetParent(gunsGo.transform, false);
            muzzleL.localPosition = new Vector3(-0.5f, 1.0f, 0.8f);
            var muzzleR = new GameObject("Muzzle R").transform;
            muzzleR.SetParent(gunsGo.transform, false);
            muzzleR.localPosition = new Vector3(0.5f, 1.0f, 0.8f);

            var guns = gunsGo.AddComponent<ShipWeaponController>();
            var gunsSo = new SerializedObject(guns);
            SetObjectRef(gunsSo, "pool", pool);
            SetObjectRef(gunsSo, "definition", shipWeapon);
            SetObjectRef(gunsSo, "fireAction", FindRef(refs, "Right Hand", "Activate"));
            SetObjectRef(gunsSo, "ownerRoot", rig);
            SetObjectRefList(gunsSo, "muzzles", new List<Object> { muzzleL, muzzleR });
            gunsSo.ApplyModifiedPropertiesWithoutUndo();

            // Holographic gunsight reticle, wired to the player guns.
            BuildCockpitCrosshair(cockpit, guns);

            // ---- Dialogue Players ----
            // Manifest transfer dialogue (auto): aboard Corsair, transferring the operatives' manifest.
            var manifestDialogue = BuildEp07DialoguePlayer("Dialogue_ManifestTransfer", new Vector3(0f, 1.62f, 0.8f),
                "manifest_transfer");
            var manifestDpGo = manifestDialogue.gameObject;
            manifestDpGo.transform.SetParent(cockpit, false);
            manifestDpGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            manifestDpGo.transform.localRotation = Quaternion.identity;

            // ---- Enemy Encounter: Dominion Interceptor Pursuit ----
            // Single hunter-class interceptor (pursuit mode: guardTarget = null, spawn around player).
            var interceptorGo = new GameObject("Dominion Interceptor");
            var interceptor = interceptorGo.AddComponent<GuardEncounter>();
            var interceptorSo = new SerializedObject(interceptor);
            SetObjectRef(interceptorSo, "player", shipCtrl);
            SetObjectRef(interceptorSo, "universe", universe);
            SetObjectRef(interceptorSo, "pool", pool);
            SetObjectRef(interceptorSo, "definition", enemyShipDef);
            // Pursuit mode: guardTarget = null, spawn around player
            interceptorSo.FindProperty("shipCount").intValue = 1;
            interceptorSo.FindProperty("spawnRadius").floatValue = 260f;
            // Generous delay so the manifest-transfer dialogue can finish before the fight starts.
            interceptorSo.FindProperty("initialDelay").floatValue = 20f;
            // Gated by EP07 completion (never suppresses or requires prior scene).
            interceptorSo.FindProperty("requiredCompletedScene").stringValue = "";
            interceptorSo.FindProperty("clearedFlag").stringValue = "ep07_interceptor_cleared";

            // Build spawn dialogue for this encounter.
            var interceptorPursuitDialogue = BuildEp07DialoguePlayer("Dialogue_InterceptorPursuit", new Vector3(0f, 1.62f, 0.8f),
                "interceptor_pursuit");
            var interceptorPursuitGo = interceptorPursuitDialogue.gameObject;
            interceptorPursuitGo.transform.SetParent(cockpit, false);
            interceptorPursuitGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            interceptorPursuitGo.transform.localRotation = Quaternion.identity;
            var interceptorDpSo = new SerializedObject(interceptorPursuitDialogue);
            interceptorDpSo.FindProperty("playOnStart").boolValue = false;
            interceptorDpSo.ApplyModifiedPropertiesWithoutUndo();
            SetObjectRef(interceptorSo, "spawnDialogue", interceptorPursuitDialogue);
            interceptorSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-encounter dialogue: compassion anomalies (Tessa's revelation about prior hesitations).
            // Played by the EncounterClearedActivator once the interceptor is destroyed, not on start.
            var compassionDialogue = BuildEp07DialoguePlayer("Dialogue_CompassionAnomalies", new Vector3(0f, 1.62f, 0.8f),
                "compassion_anomalies");
            var compassionDpGo = compassionDialogue.gameObject;
            compassionDpGo.transform.SetParent(cockpit, false);
            compassionDpGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            compassionDpGo.transform.localRotation = Quaternion.identity;
            var compassionDpSo = new SerializedObject(compassionDialogue);
            compassionDpSo.FindProperty("playOnStart").boolValue = false;
            compassionDpSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "CONTINUE — EPISODE 8" wired to LoadOnFootScene (chains to EP08 CargoHold).
            // Hidden until the interceptor encounter is cleared.
            var returnBoxGo = BuildTransitionBox("ContinueToEp08Box", new Vector3(0f, 1.2f, 0.8f),
                "CONTINUE — EPISODE 8", out var returnBtn, out var returnTransition);
            var rtSo = new SerializedObject(returnTransition);
            rtSo.FindProperty("onFootScene").stringValue = "Galaxy1_EP08_CargoHold";
            rtSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.LoadOnFootScene));
            returnBoxGo.SetActive(false);

            // Gate the ending on the fight: when the GuardEncounter publishes SpaceEncounterCleared,
            // play the compassion_anomalies dialogue and reveal the return prompt. No MissionDirector
            // here — its completion would publish ZoneCompleted and end the scene early.
            var clearedGateGo = new GameObject("EncounterClearedGate");
            var clearedGate = clearedGateGo.AddComponent<EncounterClearedActivator>();
            var gateSo = new SerializedObject(clearedGate);
            var activateProp = gateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = returnBoxGo;
            gateSo.FindProperty("playOnCleared").objectReferenceValue = compassionDialogue;
            gateSo.FindProperty("clearedFlag").stringValue = "ep07_interceptor_cleared";
            gateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep07EscapeScenePath);
            EnsureScenesInBuild(Galaxy1Ep07EscapeScenePath);

            Debug.Log($"[Space Samurai] EP07 Escape scene built at {Galaxy1Ep07EscapeScenePath}. " +
                      "Space leaving Blackveil (industrial moon receding, debris field). Cockpit with canopy + HUD. " +
                      "Flow: manifest_transfer auto → interceptor pursuit (GuardEncounter, 20s delay) → on cleared, " +
                      "EncounterClearedActivator plays compassion_anomalies + reveals CONTINUE TO APEX VAULT " +
                      "(LoadOnFootScene to Galaxy1_EP08_CargoHold, chains to EP08).");
        }

        // ---- Moved from Ep08Builder.cs (deleted) — BuildEp08DialoguePlayer is still needed by
        // BuildEp08OrbitBreak/BuildEp08NebulaEdge (moved below from Ep08BuilderSpace.cs). ----
        /// <summary>Shorthand for building a DialoguePlayer with EP08 conventions. Calls BuildDialoguePlayer
        /// with clipPrefix="ep08" and loads lines from Ep08Lines.Get(setId).</summary>
        private static DialoguePlayer BuildEp08DialoguePlayer(string name, Vector3 position, string setId,
            InputActionReference advanceRef = null)
        {
            return BuildDialoguePlayer(name, position, Ep08Lines.Get(setId), advanceRef, setId, clipPrefix: "ep08");
        }

        // ---- Moved from Ep08BuilderSpace.cs (deleted) — BuildEp08OrbitBreak/BuildEp08NebulaEdge are
        // called directly by EnemyWarningBuilder.BuildAllSpaceCombatScenes. ----
        private const string Galaxy1Ep08OrbitBreakScenePath = SceneFolder + "/Galaxy1_EP08_OrbitBreak.unity";
        private const string Galaxy1Ep08NebulaEdgeScenePath = SceneFolder + "/Galaxy1_EP08_NebulaEdge.unity";

        private static readonly string Galaxy1Ep08OrbitBreakSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep08OrbitBreakScenePath);
        private static readonly string Galaxy1Ep08NebulaEdgeSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep08NebulaEdgeScenePath);

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP08 Orbit Break", priority = 103)]
        public static void BuildEp08OrbitBreak()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space styling: black void.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.025f, 0.035f);
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 40f, 0f);

            var gameGo = new GameObject("Game");
            var gs = gameGo.AddComponent<GameState>();
            var gsSo = new SerializedObject(gs);
            var sm = gsSo.FindProperty("startMode");
            if (sm != null) sm.enumValueIndex = (int)GameMode.SpaceFlight;
            gsSo.ApplyModifiedPropertiesWithoutUndo();

            // Seated flight rig: NO locomotion.
            var rig = BuildRig(refs, addLocomotion: false);

            var vrRig = rig.GetComponent<VRRig>();
            var cam = vrRig != null && vrRig.Head != null ? vrRig.Head.GetComponent<Camera>() : null;
            if (cam != null) cam.farClipPlane = 6000f;

            if (rig.GetComponent<Health>() == null) rig.AddComponent<Health>();
            rig.AddComponent<PlayerShipDamageRelay>();

            var hull = new GameObject("Ship Hull (Damage Volume)");
            hull.transform.SetParent(rig.transform, false);
            hull.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var hullCol = hull.AddComponent<SphereCollider>();
            hullCol.radius = 1.2f;
            hullCol.isTrigger = false;

            var cockpit = new GameObject("Cockpit").transform;
            cockpit.SetParent(rig.transform, false);
            cockpit.localPosition = Vector3.zero;

            // Sleek shared cockpit + runtime exterior hull (replaces the old inline canopy/HUD box).
            BuildPlayerShipVisual(cockpit);

            EnemyWarningBuilder.AddTo(cockpit, new Vector3(0.85f, 1.4f, -1.3f));

            // Starfield dome.
            BuildStarfield(null, 5000f, 1500);

            // Universe root: the ship "flies" by moving this root past the stationary cockpit.
            var universe = new GameObject("Universe").transform;

            // Vel Keth as big red sphere below/behind the corsair.
            var velKethGo = AddUnlitVisual(universe, "Vel Keth", new Vector3(-1500f, -800f, -2500f),
                Vector3.one * 700f, PrimitiveType.Sphere, new Color(0.75f, 0.40f, 0.35f)); // red planet
            var velKethCollider = velKethGo.GetComponent<Collider>();
            if (velKethCollider != null) velKethCollider.isTrigger = true;

            // 2 large frigate hull visuals (unlit block clusters).
            var frigateColor = new Color(0.45f, 0.42f, 0.40f);
            var frigate1 = AddUnlitVisual(universe, "Frigate Hulk 1", new Vector3(-2000f, 200f, -3000f),
                new Vector3(300f, 150f, 400f), PrimitiveType.Cube, frigateColor);
            var f1Collider = frigate1.GetComponent<Collider>();
            if (f1Collider != null) f1Collider.isTrigger = true;

            var frigate2 = AddUnlitVisual(universe, "Frigate Hulk 2", new Vector3(2500f, -300f, -2800f),
                new Vector3(280f, 140f, 380f), PrimitiveType.Cube, frigateColor);
            var f2Collider = frigate2.GetComponent<Collider>();
            if (f2Collider != null) f2Collider.isTrigger = true;

            // Flight controller.
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var scSo = new SerializedObject(shipCtrl);
            SetObjectRef(scSo, "universe", universe);
            SetObjectRef(scSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(scSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            scSo.ApplyModifiedPropertiesWithoutUndo();

            // Shared bolt pool.
            var poolGo = new GameObject("Projectile Pool");
            var pool = poolGo.AddComponent<ProjectilePool>();

            // Player guns.
            var gunsGo = new GameObject("Ship Guns");
            gunsGo.transform.SetParent(cockpit, false);
            var muzzleL = new GameObject("Muzzle L").transform;
            muzzleL.SetParent(gunsGo.transform, false);
            muzzleL.localPosition = new Vector3(-0.5f, 1.0f, 0.8f);
            var muzzleR = new GameObject("Muzzle R").transform;
            muzzleR.SetParent(gunsGo.transform, false);
            muzzleR.localPosition = new Vector3(0.5f, 1.0f, 0.8f);

            var guns = gunsGo.AddComponent<ShipWeaponController>();
            var gunsSo = new SerializedObject(guns);
            SetObjectRef(gunsSo, "pool", pool);
            SetObjectRef(gunsSo, "definition", shipWeapon);
            SetObjectRef(gunsSo, "fireAction", FindRef(refs, "Right Hand", "Activate"));
            SetObjectRef(gunsSo, "ownerRoot", rig);
            SetObjectRefList(gunsSo, "muzzles", new List<Object> { muzzleL, muzzleR });
            gunsSo.ApplyModifiedPropertiesWithoutUndo();

            // Holographic gunsight reticle, wired to the player guns.
            BuildCockpitCrosshair(cockpit, guns);

            // ---- Dialogue Players ----
            var frigateBracketDialogue = BuildEp08DialoguePlayer("Dialogue_FrigateBracket", new Vector3(0f, 1.62f, 0.8f),
                "frigate_bracket");
            var frigateBracketGo = frigateBracketDialogue.gameObject;
            frigateBracketGo.transform.SetParent(cockpit, false);
            frigateBracketGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            frigateBracketGo.transform.localRotation = Quaternion.identity;

            // ---- Enemy Encounter: 3-ship frigate bracket ----
            var frigateEncounterGo = new GameObject("FrigateBracketEncounter");
            var frigateEncounter = frigateEncounterGo.AddComponent<GuardEncounter>();
            var frigateEncounterSo = new SerializedObject(frigateEncounter);
            SetObjectRef(frigateEncounterSo, "player", shipCtrl);
            SetObjectRef(frigateEncounterSo, "universe", universe);
            SetObjectRef(frigateEncounterSo, "pool", pool);
            SetObjectRef(frigateEncounterSo, "definition", enemyShipDef);
            frigateEncounterSo.FindProperty("shipCount").intValue = 3;
            frigateEncounterSo.FindProperty("spawnRadius").floatValue = 280f;
            frigateEncounterSo.FindProperty("initialDelay").floatValue = 5f;
            frigateEncounterSo.FindProperty("requiredCompletedScene").stringValue = "";
            frigateEncounterSo.FindProperty("clearedFlag").stringValue = "ep08_bracket_cleared";
            SetObjectRef(frigateEncounterSo, "spawnDialogue", frigateBracketDialogue);
            frigateEncounterSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "PUNCH THROUGH".
            var nebulaBoxGo = BuildTransitionBox("ToNebulaBox", new Vector3(0f, 1.2f, 0.8f), "PUNCH THROUGH",
                out var nebulaBtn, out var nebulaTransition);
            var ntSo = new SerializedObject(nebulaTransition);
            ntSo.FindProperty("onFootScene").stringValue = Galaxy1Ep08NebulaEdgeSceneName;
            ntSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(nebulaBtn.onClick,
                new UnityEngine.Events.UnityAction(nebulaTransition.LoadOnFootScene));
            nebulaBoxGo.SetActive(false);

            // Gate the ending on the fight: EncounterClearedActivator.
            var frigateGateGo = new GameObject("FrigateClearedGate");
            var frigateGate = frigateGateGo.AddComponent<EncounterClearedActivator>();
            var frigateSo = new SerializedObject(frigateGate);
            var activateProp = frigateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = nebulaBoxGo;
            frigateSo.FindProperty("clearedFlag").stringValue = "ep08_bracket_cleared";
            frigateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep08OrbitBreakScenePath);
            EnsureScenesInBuild(Galaxy1Ep08OrbitBreakScenePath);

            Debug.Log($"[Space Samurai] EP08 Orbit Break scene built at {Galaxy1Ep08OrbitBreakScenePath}. " +
                      "Space over Vel Keth system (red planet below/behind, 2 frigate hulk visuals). " +
                      "Cockpit with canopy + HUD. " +
                      "Flow: frigate_bracket spawn (GuardEncounter, 3 ships, 5s delay) → on cleared, " +
                      "EncounterClearedActivator reveals PUNCH THROUGH (LoadOnFootScene, chains to EP08 Nebula Edge).");
        }

        [MenuItem("Tools/Space Samurai/Galaxy 1/Build EP08 Nebula Edge", priority = 104)]
        public static void BuildEp08NebulaEdge()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Nebula fringe: tinted fog + colored accent visuals.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.35f, 0.25f, 0.45f); // nebula purple-blue tint
            RenderSettings.fogDensity = 0.015f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.05f, 0.12f); // dim purple tones
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            light.color = new Color(0.6f, 0.5f, 0.8f); // purple-tinted key
            lightGo.transform.rotation = Quaternion.Euler(30f, 50f, 0f);

            var gameGo = new GameObject("Game");
            var gs = gameGo.AddComponent<GameState>();
            var gsSo = new SerializedObject(gs);
            var sm = gsSo.FindProperty("startMode");
            if (sm != null) sm.enumValueIndex = (int)GameMode.SpaceFlight;
            gsSo.ApplyModifiedPropertiesWithoutUndo();

            // Seated flight rig: NO locomotion.
            var rig = BuildRig(refs, addLocomotion: false);

            var vrRig = rig.GetComponent<VRRig>();
            var cam = vrRig != null && vrRig.Head != null ? vrRig.Head.GetComponent<Camera>() : null;
            if (cam != null) cam.farClipPlane = 6000f;

            if (rig.GetComponent<Health>() == null) rig.AddComponent<Health>();
            rig.AddComponent<PlayerShipDamageRelay>();

            var hull = new GameObject("Ship Hull (Damage Volume)");
            hull.transform.SetParent(rig.transform, false);
            hull.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var hullCol = hull.AddComponent<SphereCollider>();
            hullCol.radius = 1.2f;
            hullCol.isTrigger = false;

            var cockpit = new GameObject("Cockpit").transform;
            cockpit.SetParent(rig.transform, false);
            cockpit.localPosition = Vector3.zero;

            // Sleek shared cockpit + runtime exterior hull (replaces the old inline canopy/HUD box).
            BuildPlayerShipVisual(cockpit);

            EnemyWarningBuilder.AddTo(cockpit, new Vector3(0.85f, 1.4f, -1.3f));

            // Starfield dome.
            BuildStarfield(null, 5000f, 1500);

            // Universe root.
            var universe = new GameObject("Universe").transform;

            // Nebula fringe accent visuals (tinted geometric shapes).
            var nebulaTint1 = new Color(0.45f, 0.25f, 0.65f, 0.4f);
            var nebulaTint2 = new Color(0.55f, 0.35f, 0.75f, 0.35f);
            var nebulaVis1 = AddUnlitVisual(universe, "NebulaAccent1", new Vector3(1500f, 300f, 1200f),
                new Vector3(500f, 300f, 600f), PrimitiveType.Cube, nebulaTint1);
            var nv1Col = nebulaVis1.GetComponent<Collider>();
            if (nv1Col != null) nv1Col.isTrigger = true;

            var nebulaVis2 = AddUnlitVisual(universe, "NebulaAccent2", new Vector3(-1800f, -400f, 1400f),
                new Vector3(400f, 250f, 500f), PrimitiveType.Cube, nebulaTint2);
            var nv2Col = nebulaVis2.GetComponent<Collider>();
            if (nv2Col != null) nv2Col.isTrigger = true;

            // Flight controller.
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var scSo = new SerializedObject(shipCtrl);
            SetObjectRef(scSo, "universe", universe);
            SetObjectRef(scSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(scSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            scSo.ApplyModifiedPropertiesWithoutUndo();

            // Shared bolt pool.
            var poolGo = new GameObject("Projectile Pool");
            var pool = poolGo.AddComponent<ProjectilePool>();

            // Player guns.
            var gunsGo = new GameObject("Ship Guns");
            gunsGo.transform.SetParent(cockpit, false);
            var muzzleL = new GameObject("Muzzle L").transform;
            muzzleL.SetParent(gunsGo.transform, false);
            muzzleL.localPosition = new Vector3(-0.5f, 1.0f, 0.8f);
            var muzzleR = new GameObject("Muzzle R").transform;
            muzzleR.SetParent(gunsGo.transform, false);
            muzzleR.localPosition = new Vector3(0.5f, 1.0f, 0.8f);

            var guns = gunsGo.AddComponent<ShipWeaponController>();
            var gunsSo = new SerializedObject(guns);
            SetObjectRef(gunsSo, "pool", pool);
            SetObjectRef(gunsSo, "definition", shipWeapon);
            SetObjectRef(gunsSo, "fireAction", FindRef(refs, "Right Hand", "Activate"));
            SetObjectRef(gunsSo, "ownerRoot", rig);
            SetObjectRefList(gunsSo, "muzzles", new List<Object> { muzzleL, muzzleR });
            gunsSo.ApplyModifiedPropertiesWithoutUndo();

            // Holographic gunsight reticle, wired to the player guns.
            BuildCockpitCrosshair(cockpit, guns);

            // ---- Dialogue Players ----
            // Covenant epilogue (auto) at scene start.
            var covenantEpilogueDialogue = BuildEp08DialoguePlayer("Dialogue_CovenantEpilogue", new Vector3(0f, 1.62f, 0.8f),
                "covenant_epilogue");
            var covenantGo = covenantEpilogueDialogue.gameObject;
            covenantGo.transform.SetParent(cockpit, false);
            covenantGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            covenantGo.transform.localRotation = Quaternion.identity;
            var covenantSo = new SerializedObject(covenantEpilogueDialogue);
            covenantSo.FindProperty("playOnStart").boolValue = true;
            covenantSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Enemy Encounter: 1-ship final interceptor ----
            var interceptorGo = new GameObject("FinalInterceptor");
            var interceptor = interceptorGo.AddComponent<GuardEncounter>();
            var interceptorSo = new SerializedObject(interceptor);
            SetObjectRef(interceptorSo, "player", shipCtrl);
            SetObjectRef(interceptorSo, "universe", universe);
            SetObjectRef(interceptorSo, "pool", pool);
            SetObjectRef(interceptorSo, "definition", enemyShipDef);
            interceptorSo.FindProperty("shipCount").intValue = 1;
            interceptorSo.FindProperty("spawnRadius").floatValue = 250f;
            interceptorSo.FindProperty("initialDelay").floatValue = 30f; // 30s delay for epilogue
            interceptorSo.FindProperty("requiredCompletedScene").stringValue = "";
            interceptorSo.FindProperty("clearedFlag").stringValue = "ep08_interceptor_cleared";

            // Build spawn dialogue for this encounter.
            var finalPursuitDialogue = BuildEp08DialoguePlayer("Dialogue_FinalPursuit", new Vector3(0f, 1.62f, 0.8f),
                "final_pursuit");
            var finalPursuitGo = finalPursuitDialogue.gameObject;
            finalPursuitGo.transform.SetParent(cockpit, false);
            finalPursuitGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            finalPursuitGo.transform.localRotation = Quaternion.identity;
            var finalPursuitSo = new SerializedObject(finalPursuitDialogue);
            finalPursuitSo.FindProperty("playOnStart").boolValue = false;
            finalPursuitSo.ApplyModifiedPropertiesWithoutUndo();
            SetObjectRef(interceptorSo, "spawnDialogue", finalPursuitDialogue);
            interceptorSo.ApplyModifiedPropertiesWithoutUndo();

            // Transition box: "JUMP — RETURN TO GALAXY MAP".
            var returnMapBoxGo = BuildTransitionBox("ReturnToMapBox", new Vector3(0f, 1.2f, 0.8f), "JUMP — RETURN TO GALAXY MAP",
                out var returnMapBtn, out var returnMapTransition);
            UnityEventTools.AddPersistentListener(returnMapBtn.onClick,
                new UnityEngine.Events.UnityAction(returnMapTransition.ReturnToSpace));
            returnMapBoxGo.SetActive(false);

            // Gate the ending on the fight: EncounterClearedActivator with extraFlag "galaxy1_complete".
            var interceptorGateGo = new GameObject("InterceptorClearedGate");
            var interceptorGate = interceptorGateGo.AddComponent<EncounterClearedActivator>();
            var interceptorGateSo = new SerializedObject(interceptorGate);
            var activateProp = interceptorGateSo.FindProperty("activateOnCleared");
            activateProp.arraySize = 1;
            activateProp.GetArrayElementAtIndex(0).objectReferenceValue = returnMapBoxGo;
            interceptorGateSo.FindProperty("clearedFlag").stringValue = "ep08_interceptor_cleared";
            // IMPORTANT: set extraFlag = "galaxy1_complete" if the property exists.
            var extraFlagProp = interceptorGateSo.FindProperty("extraFlag");
            if (extraFlagProp != null)
            {
                extraFlagProp.stringValue = "galaxy1_complete";
            }
            interceptorGateSo.ApplyModifiedPropertiesWithoutUndo();

            // Ensure XR UI infrastructure.
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // Save the scene.
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1Ep08NebulaEdgeScenePath);
            EnsureScenesInBuild(Galaxy1Ep08NebulaEdgeScenePath);

            Debug.Log($"[Space Samurai] EP08 Nebula Edge scene built at {Galaxy1Ep08NebulaEdgeScenePath}. " +
                      "Nebula fringe (purple-blue tinted fog, accent visuals). Cockpit with canopy + HUD. " +
                      "Flow: covenant_epilogue (auto on start) → final_pursuit (GuardEncounter, 1 ship, 30s delay) → " +
                      "on cleared, EncounterClearedActivator plays nothing + sets extraFlag 'galaxy1_complete' + " +
                      "reveals JUMP — RETURN TO GALAXY MAP (ReturnToSpace, marks galaxy1 complete).");
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
