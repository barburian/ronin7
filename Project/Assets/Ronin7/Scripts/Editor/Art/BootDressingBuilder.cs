using System.Collections.Generic;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// P5-08: dresses the Phase6_Boot menu room with the Program biome kit, additively: the old primitive dressing is
    /// disabled (never deleted), the kit, holo map table and loadout rack go under "Boot Zone Dressing/Program Kit"
    /// (rebuilt on each run), scene-embedded materials become assets in Art/Materials/Boot, and the room bakes with
    /// Boot_Lighting (recipe P5-05). XRRigBuilder.BuildBootDressing calls <see cref="Apply"/>, so a rebuild matches;
    /// the menu below also bakes. North (+Z, beyond the menu) stays open to the Sky_BootNebula vista.
    /// </summary>
    public static class BootDressingBuilder
    {
        public const string BootScenePath = "Assets/Ronin7/Scenes/Phase6_Boot.unity";
        public const string MatDir = "Assets/Ronin7/Art/Materials/Boot";
        public const string LightingPath = "Assets/Ronin7/Scenes/Boot_Lighting.lighting";
        private const string KitDir = "Assets/Ronin7/Art/Biomes/Program/Prefabs/Program_";
        private const string KitRoot = "Program Kit";
        private const float LightmapScale = 0.4f; // RoomShellBuilder uses 0.6; the kit UV2 packs ~3x dense and the boot room must stay <= 2 maps
        private static readonly Color Cyan = new Color(0.25f, 0.85f, 1f);

        /// <summary>Primitive dressing the kit replaces (children of "Boot Zone Dressing"). Disabled, not deleted.</summary>
        public static readonly string[] Replaced = { "Distant Planet", "Boot Deck", "Deck Rim", "Pillar", "Pillar Strip", "Holo Ring" };

        public static readonly (string id, string prefab)[] Blades =
        {
            (WeaponId.Katana, "Sword_Katana"), (WeaponId.Nodachi, "Sword_Nodachi"),
            (WeaponId.Tanto, "Sword_Tanto"), (WeaponId.TwinWakizashi, "Sword_Wakizashi"),
        };

        [MenuItem("Tools/Space Samurai/Art/Dress And Bake Boot Menu")]
        public static void DressAndBakeMenu()
        {
            if (EditorSceneManager.sceneCount != 1 || EditorSceneManager.GetActiveScene().path != BootScenePath
                || EditorSceneManager.GetActiveScene().isDirty)
            {
                Debug.LogError("[BootDressing] Open ONLY Phase6_Boot, saved (Lightmapping.Bake bakes every open scene).");
                return;
            }
            var scene = EditorSceneManager.GetActiveScene();
            Apply(scene);
            EditorSceneManager.SaveScene(scene);
            bool ok = Lightmapping.Bake();
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[BootDressing] dressed; bake {(ok ? "ok" : "FAILED")}, {LightmapSettings.lightmaps.Length} lightmap(s)");
        }

        /// <summary>Idempotent: disables the replaced primitives, rebuilds the kit root, moves embedded materials, sets the lighting asset.</summary>
        public static void Apply(UnityEngine.SceneManagement.Scene scene)
        {
            GameObject dressing = null;
            foreach (var go in scene.GetRootGameObjects()) if (go.name == "Boot Zone Dressing") dressing = go;
            if (dressing == null) { Debug.LogError("[BootDressing] no 'Boot Zone Dressing' root"); return; }

            foreach (Transform child in dressing.transform)
                if (System.Array.IndexOf(Replaced, child.name) >= 0) child.gameObject.SetActive(false);
            var old = dressing.transform.Find(KitRoot);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var kit = new GameObject(KitRoot).transform;
            kit.SetParent(dressing.transform, false);

            // Floor: 4 x 4 hex tiles, 8 x 8 m, top at y = 0.
            for (int ix = 0; ix < 4; ix++)
                for (int iz = 0; iz < 4; iz++)
                    Place("Floor_" + "ABC"[(ix + 2 * iz) % 3], kit, new Vector3(-3 + 2 * ix, 0, -3 + 2 * iz), 0);
            // Monolith pillars at the corners.
            foreach (var p in new[] { new Vector3(-3.5f, 0, 3.5f), new Vector3(3.5f, 0, 3.5f), new Vector3(-3.5f, 0, -3.5f), new Vector3(3.5f, 0, -3.5f) })
                Place("Pillar", kit, p, 0);
            // Floor-edge light bars (S + E + W, north open), one baked point light each.
            var lights = new GameObject("Lights").transform;
            lights.SetParent(kit, false);
            foreach (var (pos, yaw) in new[] { (new Vector3(-1.5f, 0.03f, -3.9f), 0f), (new Vector3(1.5f, 0.03f, -3.9f), 0f),
                         (new Vector3(-3.9f, 0.03f, -1.5f), 90f), (new Vector3(-3.9f, 0.03f, 1.5f), 90f),
                         (new Vector3(3.9f, 0.03f, -1.5f), 270f), (new Vector3(3.9f, 0.03f, 1.5f), 270f) })
            {
                var bar = Place("LightBar", kit, pos, yaw);
                var mount = bar.transform.Find("LightMount");
                PointLight(lights, (mount != null ? mount.position : pos) + Vector3.up * 0.3f);
            }
            // Holo glyphs flanking the title; eye core floating above it.
            Place("HoloGlyph", kit, new Vector3(-2.4f, 0.3f, 4.2f), 180);
            Place("HoloGlyph", kit, new Vector3(2.4f, 0.3f, 4.2f), 180);
            var eye = Place("Hero_EyeCore", kit, new Vector3(0, 5.4f, 12f), 180);
            eye.transform.localScale = Vector3.one * 1.8f;

            BuildRack(kit, lights);
            BuildMapTable(kit, lights);

            // Probes: 2 m grid at 0.3 / 1.5 / 3.0 m over the deck; one baked box reflection probe (128).
            var probes = new List<Vector3>();
            foreach (float y in new[] { 0.3f, 1.5f, 3f })
                for (int ix = 0; ix <= 4; ix++)
                    for (int iz = 0; iz <= 4; iz++)
                        probes.Add(new Vector3(-3.5f + 1.75f * ix, y, -3.5f + 1.75f * iz));
            var lp = new GameObject("LightProbes").AddComponent<LightProbeGroup>();
            lp.transform.SetParent(kit, false);
            lp.probePositions = probes.ToArray();
            var rp = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            rp.transform.SetParent(kit, false);
            rp.mode = ReflectionProbeMode.Baked; rp.boxProjection = true; rp.resolution = 128;
            rp.center = new Vector3(0, 2.25f, 0); rp.size = new Vector3(8, 4.5f, 8);

            // The scene's sun: baked (Quest: no realtime shadows; hands read the probes). No gameplay-signal light exists here.
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
                if (l.type == LightType.Directional && l.gameObject.scene == scene) { l.lightmapBakeType = LightmapBakeType.Baked; l.shadows = LightShadows.Soft; }

            // Starfield: 800 -> 200 particles (Quest 3 particle budget); the nebula sky carries the stars now.
            var stars = dressing.transform.Find("Starfield");
            if (stars != null && stars.TryGetComponent(out ParticleSystem ps))
            {
                var main = ps.main; main.maxParticles = 200;
                ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 200) });
            }

            MoveEmbeddedMaterials(scene);
            Lightmapping.lightingSettings = LightingAsset();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>West wall segment with the four loadout blades on light-bar shelves, ~1.0-1.9 m high.</summary>
        private static void BuildRack(Transform kit, Transform lights)
        {
            var rack = new GameObject("Loadout Rack").transform;
            rack.SetParent(kit, false);
            Place("Wall_Straight", rack, new Vector3(-3.0f, 0, 0.6f), 90);
            var display = rack.gameObject.AddComponent<LoadoutRackDisplay>();
            var blades = new GameObject[Blades.Length];
            for (int i = 0; i < Blades.Length; i++)
            {
                float y = 1.0f + 0.3f * i;
                Place("LightBar", rack, new Vector3(-2.72f, y - 0.1f, 0.6f), 90);
                var src = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Ronin7/Prefabs/Art/{Blades[i].prefab}.prefab");
                if (src == null) { Debug.LogWarning("[BootDressing] missing " + Blades[i].prefab); continue; }
                var blade = (GameObject)PrefabUtility.InstantiatePrefab(src, kit.gameObject.scene);
                PrefabUtility.UnpackPrefabInstance(blade, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                StripToVisual(blade);
                blade.name = "Rack_" + Blades[i].prefab;
                blade.transform.SetParent(rack, false);
                // Lay the blade's longest axis along world Z, flat against the wall.
                blade.transform.localRotation = LongAxisToZ(blade) * Quaternion.Euler(0, 0, 90); // roll: flat of the blade to the room
                blade.transform.localPosition = new Vector3(-2.65f, y, 0.6f);
                var b = Bounds(blade);
                blade.transform.position += new Vector3(0, 0, 0.6f - b.center.z);
                blades[i] = blade;
            }
            var so = new SerializedObject(display);
            var ids = so.FindProperty("weaponIds"); var arr = so.FindProperty("blades");
            ids.arraySize = arr.arraySize = Blades.Length;
            for (int i = 0; i < Blades.Length; i++)
            {
                ids.GetArrayElementAtIndex(i).stringValue = Blades[i].id;
                arr.GetArrayElementAtIndex(i).objectReferenceValue = blades[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PointLight(lights, new Vector3(-2.3f, 2.3f, 0.6f));
        }

        /// <summary>Pedestal (scaled pillar) + hex-tile top + the 1-draw marker mesh + a TMP caption (Rajdhani = TMP default).</summary>
        private static void BuildMapTable(Transform kit, Transform lights)
        {
            var table = new GameObject("Holo Map Table").transform;
            table.SetParent(kit, false);
            table.SetLocalPositionAndRotation(new Vector3(2.0f, 0, 0.8f), Quaternion.Euler(0, 90, 0));
            Place("Pillar", table, Vector3.zero, 0).transform.localScale = new Vector3(0.8f, 0.2f, 0.8f);
            Place("Floor_A", table, new Vector3(0, 0.95f, 0), 0).transform.localScale = new Vector3(0.5f, 1, 0.5f);

            var markers = new GameObject("Map Markers", typeof(MeshFilter), typeof(MeshRenderer));
            markers.transform.SetParent(table, false);
            markers.transform.localPosition = new Vector3(0, 0.955f, 0);
            var mr = markers.GetComponent<MeshRenderer>();
            mr.sharedMaterial = HoloMat();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var capGo = new GameObject("Map Caption");
            capGo.transform.SetParent(table, false);
            capGo.transform.localPosition = new Vector3(0, 1.25f, 0.4f);
            var cap = capGo.AddComponent<TMPro.TextMeshPro>();
            cap.text = "NO RUNS YET";
            cap.fontSize = 0.7f;
            cap.alignment = TMPro.TextAlignmentOptions.Center;
            cap.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            cap.rectTransform.sizeDelta = new Vector2(1.8f, 0.3f);
            cap.color = new Color(0.6f, 0.95f, 1f);

            var holo = table.gameObject.AddComponent<HoloMapTable>();
            var so = new SerializedObject(holo);
            so.FindProperty("markers").objectReferenceValue = markers.GetComponent<MeshFilter>();
            so.FindProperty("caption").objectReferenceValue = cap;
            so.ApplyModifiedPropertiesWithoutUndo();
            PointLight(lights, table.position + new Vector3(0, 2.2f, 0));
        }

        private static Material HoloMat()
        {
            string path = MatDir + "/Boot_HoloMap.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            EnsureFolder(MatDir);
            m = new Material(Shader.Find("Ronin7/VistaUnlit")) { name = "Boot_HoloMap" };
            m.SetColor("_BaseColor", new Color(2f, 2f, 2f)); // HDR: clears the 0.9 bloom threshold
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static GameObject Place(string piece, Transform parent, Vector3 pos, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitDir + piece + ".prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var so = new SerializedObject(r);
                so.FindProperty("m_ScaleInLightmap").floatValue = LightmapScale;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return go;
        }

        private static void PointLight(Transform parent, Vector3 pos)
        {
            var go = new GameObject("BakedPoint");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = Cyan; l.range = 5f; l.intensity = 1.5f;
            l.lightmapBakeType = LightmapBakeType.Baked; l.shadows = LightShadows.Soft;
        }

        /// <summary>Keeps Transform/MeshFilter/MeshRenderer only: no grab, damage, trail or physics on a wall prop.</summary>
        private static void StripToVisual(GameObject root)
        {
            // Scripts first: they RequireComponent the colliders / trails / lights.
            foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
            foreach (var c in root.GetComponentsInChildren<Component>(true))
                if (c != null && !(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer))
                    Object.DestroyImmediate(c);
        }

        private static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        private static Quaternion LongAxisToZ(GameObject go)
        {
            go.transform.rotation = Quaternion.identity;
            var s = Bounds(go).size;
            if (s.y >= s.x && s.y >= s.z) return Quaternion.Euler(90, 0, 0);
            if (s.x >= s.z) return Quaternion.Euler(0, 90, 0);
            return Quaternion.identity;
        }

        /// <summary>Turns every scene-embedded material on renderers / TMP into an asset in <see cref="MatDir"/>
        /// (same object, so no visual change). An existing asset of that name is updated in place (GUID kept).</summary>
        public static int MoveEmbeddedMaterials(UnityEngine.SceneManagement.Scene scene)
        {
            EnsureFolder(MatDir);
            var done = new Dictionary<Material, Material>();
            int moved = 0;
            Material Fix(Material m, string owner)
            {
                if (m == null || AssetDatabase.Contains(m)) return m;
                if (done.TryGetValue(m, out var a)) return a;
                string path = $"{MatDir}/Boot_{owner.Replace(" ", "")}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null) { EditorUtility.CopySerialized(m, existing); existing.name = System.IO.Path.GetFileNameWithoutExtension(path); a = existing; }
                else { m.name = System.IO.Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(m, path); a = m; }
                done[m] = a; moved++;
                return a;
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    if (t.fontSharedMaterial != null && !AssetDatabase.Contains(t.fontSharedMaterial)) t.fontSharedMaterial = Fix(t.fontSharedMaterial, t.name);
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.GetComponent<TMPro.TMP_Text>() != null) continue;
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null && !AssetDatabase.Contains(mats[i])) { mats[i] = Fix(mats[i], r.name); changed = true; }
                    if (changed) r.sharedMaterials = mats;
                }
            }
            AssetDatabase.SaveAssets();
            return moved;
        }

        private static LightingSettings LightingAsset()
        {
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (ls == null)
            {
                ls = new LightingSettings
                {
                    name = "Boot_Lighting", bakedGI = true, realtimeGI = false,
                    mixedBakeMode = MixedLightingMode.IndirectOnly,
                    lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                    lightmapResolution = 14, lightmapMaxSize = 1024, lightmapPadding = 2,
                    directionalityMode = LightmapsMode.NonDirectional,
                    lightmapCompression = LightmapCompression.NormalQuality,
                    directSampleCount = 32, indirectSampleCount = 256, environmentSampleCount = 128,
                };
                AssetDatabase.CreateAsset(ls, LightingPath);
            }
            var so = new SerializedObject(ls);
            so.FindProperty("m_LightmapSizeFixed").boolValue = true;
            so.FindProperty("m_XAtlasRepackUnderutilizedLightmaps").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ls;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
