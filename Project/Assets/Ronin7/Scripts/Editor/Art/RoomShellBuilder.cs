using System.Collections.Generic;
using System.Diagnostics;
using Ronin7.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// P5-05 step b: generates a biome's baked room-shell scenes from its kit prefabs (<see cref="BiomeKitBuilder.Program"/>,
    /// <see cref="BiomeKitBuilder.Rust"/>), 3 layouts x the 8 m / 11 m footprints, and wires them into
    /// <see cref="ArenaRoomLibrary"/> + Build Settings. Shell contract: <c>RunArenaController.LoadShell</c>.
    /// Re-running regenerates the scenes it owns (Rooms/&lt;Biome&gt; only); never touches shipped scenes.
    /// </summary>
    public static class RoomShellBuilder
    {
        private const string LibraryPath = "Assets/Ronin7/Data/Roguelike/ArenaRoomLibrary.asset";
        public static readonly int[] Footprints = { 8, 11 };
        public const int LayoutCount = 3;
        private const float WallH = 4.5f, WallT = 0.5f, KitWallW = 4f, BarY = 3.6f;
        private const float LightmapScale = 0.6f;
        private static readonly Color Cyan = new Color(0.25f, 0.85f, 1f);
        private static readonly Color Sodium = new Color(1f, 0.6f, 0.22f);

        public static string Dir(BiomeKit kit) => $"Assets/Ronin7/Scenes/Rooms/{kit.Name}";
        public static string SceneName(BiomeKit kit, int layout, int half) => $"Room_{kit.Name}_{layout:00}_{half}m";
        public static string ScenePath(BiomeKit kit, int layout, int half) => $"{Dir(kit)}/{SceneName(kit, layout, half)}.unity";

        [MenuItem("Tools/Space Samurai/Art/Build Room Shells/Program", priority = 21)]
        public static void BuildProgramMenu() => BuildAll(BiomeKitBuilder.Program, "program");

        [MenuItem("Tools/Space Samurai/Art/Build Room Shells/Rust", priority = 21)]
        public static void BuildRustMenu() => BuildAll(BiomeKitBuilder.Rust, "rust");

        [MenuItem("Tools/Space Samurai/Art/Build Room Shells/Garden", priority = 21)]
        public static void BuildGardenMenu() => BuildAll(BiomeKitBuilder.Garden, "garden");

        private const float AlcoveDepth = 4f;
        public static int Segments(int half) => Mathf.CeilToInt(2f * half / KitWallW);
        private static float SegT(float h, int n, int i) => -h + 2 * h / n * (i + 0.5f);

        /// <summary>Hero alcove (side, wall segment) behind the rim, outside the combat footprint, or null
        /// (Program: the eye core floats above the arena). The side's Bound collider still closes the opening.</summary>
        public static (int side, int seg)? Alcove(BiomeKit kit, int layout, int half)
        {
            if (kit == BiomeKitBuilder.Program) return null;
            int n = Segments(half);
            bool garden = kit == BiomeKitBuilder.Garden;
            return layout switch
            {
                1 => garden ? (3, n / 2) : (0, n / 2),
                2 => garden ? (2, n - 1) : (3, 1),   // garden 02: tree alcove behind the north-east rim
                _ => (0, n / 2 - 1),
            };
        }

        /// <summary>Hero prop pose: centred in its alcove facing the arena, or (Program) the floating eye core.</summary>
        public static (Vector3 pos, float yaw) HeroPose(BiomeKit kit, int layout, int half)
        {
            float h = half;
            if (Alcove(kit, layout, half) is (int s, int i))
                return (SidePoint(s, h + AlcoveDepth / 2, SegT(h, Segments(half), i), 0), SideYaw(s));
            if (layout == 2) return (new Vector3(0, 5.75f, 0), 0);
            if (layout == 1) return (SidePoint(0, h - 0.5f, 0, 5.25f), SideYaw(0));
            return (SidePoint(2, h - 0.5f, 0, 5.25f), SideYaw(2));
        }

        public static void BuildAll(BiomeKit kit, string biomeId)
        {
            for (int l = 1; l <= LayoutCount; l++)
                foreach (int h in Footprints)
                    Debug.Log("[RoomShellBuilder] " + BuildOne(kit, l, h, bake: true));
            Wire(kit, biomeId);
        }

        /// <summary>Builds (and optionally bakes) one shell scene alone, then restores the open scenes.
        /// Refuses while any open scene is dirty (the bake must run with only the shell loaded).</summary>
        public static string BuildOne(BiomeKit kit, int layout, int half, bool bake)
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) return "ABORT: an open scene has unsaved changes";
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try { return Build(kit, layout, half, bake); }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static string Build(BiomeKit kit, int layout, int half, bool bake)
        {
            kitPrefabs = kit;
            bool rust = kit == BiomeKitBuilder.Rust, garden = kit == BiomeKitBuilder.Garden;
            var alcove = Alcove(kit, layout, half);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float h = half;
            var rng = new System.Random(layout * 1000 + half);

            // Floor: contract collider on a root named "Floor"; tiles (A/B/C, seeded) as children.
            var floor = new GameObject("Floor");
            var fc = floor.AddComponent<BoxCollider>();
            fc.center = new Vector3(0, -0.05f, 0); fc.size = new Vector3(2 * h, 0.1f, 2 * h);
            string[] tiles = { "Floor_A", "Floor_B", "Floor_C" };
            for (float x = -h + 1; x < h; x += 2)
                for (float z = -h + 1; z < h; z += 2)
                    Place(tiles[rng.Next(3)], floor.transform, new Vector3(x, 0, z), 90 * rng.Next(4));

            var shell = new GameObject("Shell").transform;
            // Contract walls: one box per side, inner face exactly on the boundary.
            for (int s = 0; s < 4; s++)
            {
                var b = new GameObject("Bound_" + s);
                b.transform.SetParent(shell, false);
                b.transform.localRotation = SideRot(s);
                b.transform.localPosition = SideRot(s) * new Vector3(0, 0, -h - WallT / 2);
                var bc = b.AddComponent<BoxCollider>();
                bc.center = new Vector3(0, WallH / 2, 0); bc.size = new Vector3(2 * h + 2 * WallT, WallH, WallT);
            }

            // Walls + roof ring: n pieces per side, scaled in X to fit the footprint exactly (22 m = 6 x 3.67 m).
            int n = Segments(half);
            float w = 2 * h / n;
            // True when p sits at the rim in front of the hero alcove's opening (fixtures / pillars skip it).
            bool Blocked(Vector3 p)
            {
                if (alcove is not (int sa, int ia)) return false;
                var lp = Quaternion.Inverse(SideRot(sa)) * p;
                return Mathf.Abs(lp.x - SegT(h, n, ia)) < w / 2 + 0.6f && -lp.z > h - 1.5f;
            }
            var lights = new GameObject("BakedLights").transform;
            var probes = new List<Vector3>();
            for (int s = 0; s < 4; s++)
                for (int i = 0; i < n; i++)
                {
                    float t = -h + w * (i + 0.5f);
                    bool door = layout == 3 && s >= 2 && i % 2 == 1; // 03: E/W rack aisles with door recesses
                    Vector3 pos = SidePoint(s, h + WallT / 2, t, 0);
                    if (alcove == (s, i))
                    {
                        BuildAlcove(shell, lights, probes, s, h, t, w, garden ? GardenGreen : rust ? Sodium : Cyan);
                        continue;
                    }
                    Place(door ? "Wall_Door" : "Wall_Straight", shell, pos, SideYaw(s), w / KitWallW);
                    Place("RoofRing_Segment", shell, pos, SideYaw(s), w / KitWallW);
                    if (!door) continue;
                    Place("Wall_Straight", shell, SidePoint(s, h + 1.75f, t, 0), SideYaw(s), w / KitWallW);
                    Place("Floor_A", shell, SidePoint(s, h + 1f, t, 0), SideYaw(s));
                }

            // Pillars: always the 4 corners (hides the wall seams); layout extras on the E/W rim only
            // (the north wall carries the runtime exit gates).
            var pillars = new List<Vector3> { new(-h, 0, -h), new(h, 0, -h), new(-h, 0, h), new(h, 0, h) };
            if (garden) // trellis arches: corners span the diagonal (pivot 0.65 m in), cloister 01 adds rim arches E/W/S
            {
                for (int k = 0; k < 4; k++) pillars[k] -= new Vector3(Mathf.Sign(pillars[k].x), 0, Mathf.Sign(pillars[k].z)) * 0.65f;
                if (layout == 1) foreach (int s in new[] { 0, 2, 3 }) foreach (float a in new[] { -0.6f * h, 0.6f * h }) pillars.Add(SidePoint(s, h, a, 0));
            }
            else if (rust && layout == 1) // foundry hall: I-beam colonnade on the E/W + S rim
                foreach (float a in new[] { -h / 3, h / 3 }) { pillars.Add(new(-h, 0, a)); pillars.Add(new(h, 0, a)); pillars.Add(new(a, 0, -h)); }
            else if (layout == 1) { pillars.Add(new(-h, 0, 0)); pillars.Add(new(h, 0, 0)); }
            if (layout == 2) foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -0.5f, 0.5f }) pillars.Add(new(sx * h, 0, sz * h));
            foreach (var p in pillars)
                if (!Blocked(p)) Place("Pillar", shell, p, garden ? Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg : 0);

            // Fixtures (S + E/W, never N): Program light bars / Rust sodium cages, one baked point light at each LightMount.
            var bars = new List<(int side, float t)> { (0, -0.3f * h), (0, 0.3f * h), (2, -0.3f * h), (2, 0.3f * h), (3, -0.3f * h), (3, 0.3f * h) };
            if (half >= 11) bars.Add((0, 0));
            foreach (var (side, t) in bars)
            {
                if (Blocked(SidePoint(side, h, t, 0))) continue;
                var bar = Place(garden ? "PodCluster" : rust ? "SodiumCage" : "LightBar", shell, SidePoint(side, h, t, BarY), SideYaw(side));
                var mount = bar.transform.Find("LightMount");
                var go = new GameObject("BarLight");
                go.transform.SetParent(lights, false);
                go.transform.position = mount.position;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point; l.color = garden ? GardenGreen : rust ? Sodium : Cyan; l.range = 7f; l.intensity = 3f;
                l.lightmapBakeType = LightmapBakeType.Baked; l.shadows = LightShadows.Soft;
            }
            var sun = new GameObject("Sun");
            sun.transform.SetParent(lights, false);
            sun.transform.rotation = Quaternion.Euler(55, -35, 0);
            var sl = sun.AddComponent<Light>();
            // Garden: soft cool-white daylight through the open dome ribs.
            sl.type = LightType.Directional;
            sl.color = garden ? new Color(0.85f, 0.92f, 1f) : rust ? new Color(1f, 0.62f, 0.38f) : new Color(0.7f, 0.8f, 1f);
            sl.intensity = garden ? 0.7f : rust ? 0.5f : 0.6f;
            sl.lightmapBakeType = LightmapBakeType.Baked; sl.shadows = LightShadows.Soft;

            // HoloGlyphs (animated later, P5 "layer 4"), 0.1 m proud of the wall face at eye height.
            var glyphs = layout == 3
                ? new[] { (0, -0.6f * h), (0, 0.6f * h) }
                : layout == 2 ? new[] { (2, -0.65f * h), (2, 0.65f * h), (3, -0.65f * h), (3, 0.65f * h) }
                : new[] { (2, -0.65f * h), (3, -0.65f * h) };
            // Hero: in its alcove behind the rim (Rust / Garden) or floating above the arena (Program); never on the combat floor.
            var (heroPos, heroYaw) = HeroPose(kit, layout, half);
            Place(System.Array.Find(kit.Pieces, p => p.SourceModel != null).Name, shell, heroPos, heroYaw);
            if (garden)
            {
                // Vine cards (Garden's animated piece) take the glyph slots, hanging from the cornice.
                foreach (var (side, t) in glyphs)
                    if (!Blocked(SidePoint(side, h, t, 0))) Place("VineCards", shell, SidePoint(side, h - 0.1f, t, 4.1f), SideYaw(side));
            }
            else if (rust)
            {
                // Vent fans (Rust's animated piece) take the glyph slots, flush on the wall face.
                foreach (var (side, t) in glyphs)
                    if (!Blocked(SidePoint(side, h, t, 0))) Place("VentFan", shell, SidePoint(side, h, t, 1.8f), SideYaw(side));
                // 02 crane bay: a gantry girder across the room resting on the E/W skylight frames, hoist near the east rim.
                if (layout == 2)
                {
                    float z = 0.3f * h;
                    for (int i = 0; i < n; i++)
                        Place("Gantry_Beam", shell, new Vector3(-h + w * (i + 0.5f), 5.3f, z), 0, w / KitWallW);
                    Place("CraneHoist", shell, new Vector3(h - 1.2f, 4.7f, z), 0);
                }
            }
            else
            {
                foreach (var (side, t) in glyphs) Place("HoloGlyph", shell, SidePoint(side, h - 0.1f, t, 1.8f), SideYaw(side));
            }

            BuildDecals(shell, h, 8 + 2 * layout - 2, rng);

            // Probes: 2 m grid at 0.3 / 1.5 / 3.0 m, inset 0.5 m from the walls; one baked box reflection probe.
            int cells = Mathf.CeilToInt((2 * h - 1) / 2f);
            foreach (float y in new[] { 0.3f, 1.5f, 3f })
                for (int ix = 0; ix <= cells; ix++)
                    for (int iz = 0; iz <= cells; iz++)
                        probes.Add(new Vector3(-h + 0.5f + ix * (2 * h - 1) / cells, y, -h + 0.5f + iz * (2 * h - 1) / cells));
            new GameObject("LightProbes").AddComponent<LightProbeGroup>().probePositions = probes.ToArray();
            var rp = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Baked; rp.boxProjection = true; rp.resolution = 128;
            rp.center = new Vector3(0, WallH / 2, 0); rp.size = new Vector3(2 * h, WallH, 2 * h);

            Lightmapping.lightingSettings = LightingAsset(kit);
            string path = ScenePath(kit, layout, half);
            EnsureFolder(Dir(kit));
            EditorSceneManager.SaveScene(scene, path);

            string bakeInfo = "not baked";
            if (bake)
            {
                var sw = Stopwatch.StartNew();
                bool ok = Lightmapping.Bake();
                if (!ok)
                {
                    Debug.LogWarning("[RoomShellBuilder] GPU bake failed, retrying with the CPU lightmapper.");
                    Lightmapping.lightingSettings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
                    ok = Lightmapping.Bake();
                    Lightmapping.lightingSettings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
                }
                bakeInfo = $"bake {(ok ? "ok" : "FAILED")} in {sw.Elapsed.TotalSeconds:F0}s, {LightmapSettings.lightmaps.Length} lightmap(s)";
                EditorSceneManager.SaveScene(scene, path);
            }

            int renderers = 0, tris = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                renderers++;
                for (int sm = 0; sm < mf.sharedMesh.subMeshCount; sm++) tris += (int)mf.sharedMesh.GetIndexCount(sm) / 3;
            }
            return $"{SceneName(kit, layout, half)}: {renderers} renderers, {tris} tris, {probes.Count} probes, {bakeInfo}";
        }

        /// <summary>Puts the kit's 6 shells into biome <paramref name="biomeId"/>'s roomScenes and enables them in Build Settings.</summary>
        public static void Wire(BiomeKit kit, string biomeId)
        {
            var names = new List<string>();
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int l = 1; l <= LayoutCount; l++)
                foreach (int h in Footprints)
                {
                    names.Add(SceneName(kit, l, h));
                    string p = ScenePath(kit, l, h);
                    int idx = scenes.FindIndex(s => s.path == p);
                    if (idx < 0) scenes.Add(new EditorBuildSettingsScene(p, true));
                    else scenes[idx].enabled = true;
                }
            EditorBuildSettings.scenes = scenes.ToArray();

            var lib = AssetDatabase.LoadAssetAtPath<ArenaRoomLibrary>(LibraryPath);
            foreach (var b in lib.biomes)
                if (b.id == biomeId) b.roomScenes = names.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
        }

        // ---------------- helpers ----------------

        private static readonly Color GardenGreen = new Color(0.45f, 1f, 0.55f);

        /// <summary>Open wall bay -> a closed alcove AlcoveDepth deep behind it (back wall + 2 side walls + floor + roof
        /// ribs), with a baked accent light and light probes for the probe-lit hero standing in it.</summary>
        private static void BuildAlcove(Transform shell, Transform lights, List<Vector3> probes, int s, float h, float t, float w, Color tint)
        {
            const float d = AlcoveDepth;
            var back = SidePoint(s, h + d + WallT / 2, t, 0);
            Place("Wall_Straight", shell, back, SideYaw(s), w / KitWallW);
            Place("RoofRing_Segment", shell, back, SideYaw(s), w / KitWallW);
            foreach (float sgn in new[] { -1f, 1f })
                Place("Wall_Straight", shell, SidePoint(s, h + d / 2, t + sgn * (w / 2 + WallT / 2), 0), SideYaw(s) - sgn * 90, d / KitWallW);
            for (float o = 1; o < d; o += 2)
                foreach (float sgn in new[] { -1f, 1f })
                    Place("Floor_A", shell, SidePoint(s, h + o, t + sgn * w / 4, 0), SideYaw(s), w / KitWallW);
            var go = new GameObject("AlcoveLight");
            go.transform.SetParent(lights, false);
            go.transform.position = SidePoint(s, h + 1f, t, 3.4f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = tint; l.range = 6f; l.intensity = 2.5f;
            l.lightmapBakeType = LightmapBakeType.Baked; l.shadows = LightShadows.Soft;
            foreach (float y in new[] { 0.3f, 1.5f, 3f })
                foreach (float o in new[] { 0.6f, d - 0.6f })
                    probes.Add(SidePoint(s, h + o, t, y));
        }

        // Sides: 0 = S (faces +Z), 1 = N, 2 = E (faces -X), 3 = W. The kit's arena-facing side is local +Z.
        private static float SideYaw(int s) => s switch { 0 => 0f, 1 => 180f, 2 => 270f, _ => 90f };
        private static Quaternion SideRot(int s) => Quaternion.Euler(0, SideYaw(s), 0);

        /// <summary>World point <paramref name="outward"/> m from the centre toward side s, <paramref name="t"/> m along it.</summary>
        private static Vector3 SidePoint(int s, float outward, float t, float y) =>
            SideRot(s) * new Vector3(t, 0, -outward) + Vector3.up * y;

        private static BiomeKit kitPrefabs; // the kit Place() instantiates from (set per Build)

        private static GameObject Place(string piece, Transform parent, Vector3 pos, float yaw, float scaleX = 1f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{kitPrefabs.PrefabDir}/{kitPrefabs.Name}_{piece}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = new Vector3(scaleX, 1, 1);
            // The kit's auto-unwrapped UV2 charts pack ~3x denser than the 14 texel/m setting (back faces,
            // rack detail), which blows the <= 2 x 1024 lightmap budget at 11 m; shrink every kit instance.
            var so = new SerializedObject(go.GetComponent<MeshRenderer>());
            so.FindProperty("m_ScaleInLightmap").floatValue = LightmapScale;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>One combined, inline (scene-serialized) quad mesh: count decals from the 4x4 atlas at +1 cm.
        /// Light-probe lit, no GI contribution, 1 draw.</summary>
        private static void BuildDecals(Transform parent, float h, int count, System.Random rng)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var idx = new List<int>();
            for (int i = 0; i < count; i++)
            {
                float size = 1f + (float)rng.NextDouble();
                var c = new Vector3((float)(rng.NextDouble() * 2 - 1) * (h - 2), 0.01f + i * 0.0005f, (float)(rng.NextDouble() * 2 - 1) * (h - 2));
                var rot = Quaternion.Euler(0, 90 * rng.Next(4), 0);
                int cell = rng.Next(16);
                var o = new Vector2(cell % 4, cell / 4) * 0.25f;
                int b = v.Count;
                for (int k = 0; k < 4; k++)
                {
                    float x = k == 1 || k == 2 ? 0.5f : -0.5f, z = k >= 2 ? 0.5f : -0.5f;
                    v.Add(c + rot * new Vector3(x, 0, z) * size);
                    uv.Add(o + new Vector2(x + 0.5f, z + 0.5f) * 0.25f);
                }
                idx.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            var mesh = new Mesh { name = kitPrefabs.Name + "_Decals_Room" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject("Decals");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(kitPrefabs.SlotMaterials[(int)KitSlot.Decal]);
            mr.receiveGI = ReceiveGI.LightProbes;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        private static LightingSettings LightingAsset(BiomeKit kit)
        {
            string path = $"{Dir(kit)}/{kit.Name}_Lighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (ls != null) return FixedAtlasSize(ls);
            EnsureFolder(Dir(kit));
            ls = new LightingSettings
            {
                name = kit.Name + "_Lighting",
                bakedGI = true,
                realtimeGI = false,
                mixedBakeMode = MixedLightingMode.IndirectOnly,
                lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                lightmapResolution = 14,
                lightmapMaxSize = 1024,
                lightmapPadding = 2,
                directionalityMode = LightmapsMode.NonDirectional,
                lightmapCompression = LightmapCompression.NormalQuality,
                directSampleCount = 32,
                indirectSampleCount = 256,
                environmentSampleCount = 128,
            };
            AssetDatabase.CreateAsset(ls, path);
            return FixedAtlasSize(ls);
        }

        /// <summary>Forces full 1024 atlases (fixed size, no xatlas repack of underfilled maps): otherwise the packer splits an underfilled map into 3 x 512 (seen on the
        /// Rust 8 m shells), and every extra lightmap is another static-batch split.</summary>
        private static LightingSettings FixedAtlasSize(LightingSettings ls)
        {
            var so = new SerializedObject(ls);
            so.FindProperty("m_LightmapSizeFixed").boolValue = true;
            so.FindProperty("m_XAtlasRepackUnderutilizedLightmaps").boolValue = false; // P5-05: the 3 x 512 split on Rust 8 m
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
