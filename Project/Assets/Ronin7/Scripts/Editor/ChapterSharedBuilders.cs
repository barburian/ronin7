using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Enemies;
using Ronin7.World;
using Ronin7.World.Story;
using Ronin7.Editor.Art;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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
    }
}
