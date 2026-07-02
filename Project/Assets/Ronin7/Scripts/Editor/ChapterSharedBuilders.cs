using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using Ronin7.Player;
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
            TintShared(floor.GetComponent<Renderer>(), floorColor);

            var ceil = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceil.name = name + "_Ceiling";
            ceil.transform.SetParent(parent, false);
            ceil.transform.localPosition = new Vector3(center.x, RoomH, center.z);
            ceil.transform.localScale = new Vector3(size.x, 0.2f, size.z);
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
            TintShared(panelL.GetComponent<Renderer>(), doorColor);

            var panelR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panelR.name = "PanelR";
            panelR.transform.SetParent(root.transform, false);
            panelR.transform.localPosition = rightClosed;
            panelR.transform.localScale = panelScale;
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
    }
}
