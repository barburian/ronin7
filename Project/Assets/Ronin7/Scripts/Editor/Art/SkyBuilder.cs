using System.Collections.Generic;
using Ronin7.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// P5-06: imports the AI equirect skies in <c>Art/Sky</c> as Lat-Long cubemaps (PC 2048 / Android 1024
    /// ASTC 8x8, no mips), makes a <c>Skybox/Cubemap</c> material per sky, builds the shared far-vista mesh
    /// (gas giant + orbital ring + fleet silhouette, one unlit draw) and wires both into every biome of the
    /// ArenaRoomLibrary. Idempotent.
    /// </summary>
    public static class SkyBuilder
    {
        public const string SkyFolder = "Assets/Ronin7/Art/Sky";
        public const string LibraryPath = "Assets/Ronin7/Data/Roguelike/ArenaRoomLibrary.asset";
        public const string VistaMeshPath = SkyFolder + "/Vista_Far.asset";
        public const string VistaMaterialPath = SkyFolder + "/Vista_Unlit.mat";
        public const string VistaPrefabPath = SkyFolder + "/Vista_Far.prefab";
        public const int VistaMaxTris = 2000;

        // biome id -> sky name; exposure keeps the sky a background behind the arena.
        private static readonly (string biome, string sky, float exposure, float rotation)[] Skies =
        {
            ("rust", "Rust", 0.55f, 0f),
            ("program", "Program", 0.7f, 180f),
            ("garden", "Garden", 0.6f, 0f),
            (null, "BootNebula", 0.8f, 0f),
        };

        public static string TexturePath(string sky) => $"{SkyFolder}/Sky_{sky}.png";
        public static string MaterialPath(string sky) => $"{SkyFolder}/Sky_{sky}.mat";

        [MenuItem("Tools/Space Samurai/Art/Build Skies", priority = 21)]
        public static void BuildAll()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ArenaRoomLibrary>(LibraryPath);
            GameObject vista = BuildVista();
            foreach (var s in Skies)
            {
                Material mat = BuildSky(s.sky, s.exposure, s.rotation);
                if (s.biome == null || lib == null) continue;
                foreach (var b in lib.biomes)
                    if (b.id == s.biome) { b.skybox = mat; b.vista = vista; }
            }
            if (lib != null) EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[SkyBuilder] Skies + vista built and wired into " + LibraryPath);
        }

        private static Material BuildSky(string sky, float exposure, float rotation)
        {
            string texPath = TexturePath(sky);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.textureShape = TextureImporterShape.TextureCube;
            imp.generateCubemap = TextureImporterGenerateCubemap.Cylindrical; // Latitude-Longitude layout
            imp.mipmapEnabled = false;
            imp.maxTextureSize = 2048;
            imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = 1024,
                format = TextureImporterFormat.ASTC_8x8,
            });
            imp.SaveAndReimport();

            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(texPath);
            string matPath = MaterialPath(sky);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Cubemap"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = Shader.Find("Skybox/Cubemap");
            mat.SetTexture("_Tex", cube);
            mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f)); // neutral
            mat.SetFloat("_Exposure", exposure);
            mat.SetFloat("_Rotation", rotation);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------- vista ----------------

        private static GameObject BuildVista()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(VistaMeshPath);
            if (mesh == null) { mesh = new Mesh { name = "Vista_Far" }; AssetDatabase.CreateAsset(mesh, VistaMeshPath); }
            FillVista(mesh);
            EditorUtility.SetDirty(mesh);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(VistaMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Ronin7/VistaUnlit"));
                AssetDatabase.CreateAsset(mat, VistaMaterialPath);
            }
            mat.shader = Shader.Find("Ronin7/VistaUnlit"); // vertex colour x tint, no fog, Cull Off
            mat.SetColor("_BaseColor", Color.white); // per-biome tint arrives via RendererTint (MPB)
            EditorUtility.SetDirty(mat);

            var go = new GameObject("Vista");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.allowOcclusionWhenDynamic = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, VistaPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>World-space layout (the shell root sits at identity, player faces +Z): a ringed gas giant
        /// high front-left, a fleet of slivers high front-right. Elevations >= 25 deg clear the 4.5 m walls.
        /// Vertex colours carry fake lighting from <see cref="LightDir"/>; the VistaUnlit shader multiplies them (no fog).</summary>
        private static readonly Vector3 LightDir = new Vector3(1f, 0.35f, -0.25f).normalized;

        public static void FillVista(Mesh mesh)
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();

            // Gas giant: LowPoly_PlanetSphere (320 tris, radius 0.5) scaled to 70 m diameter.
            Vector3 planet = Dir(-35f, 32f) * 200f;
            Mesh sphere = LowPolyMeshes.PlanetSphere();
            Vector3[] sv = sphere.vertices; int[] st = sphere.triangles;
            int b0 = v.Count;
            foreach (var p in sv)
            {
                Vector3 n = p.normalized;
                float band = 0.85f + 0.15f * Mathf.Sin(n.y * 18f); // faint latitude bands
                v.Add(planet + p * 70f);
                c.Add(Shade(n, band));
            }
            foreach (int i in st) t.Add(b0 + i);

            // Orbital ring: flat annulus around the giant, seen almost edge-on, 64 segments (128 tris).
            Quaternion tilt = Quaternion.LookRotation(planet.normalized) * Quaternion.Euler(-12f, 0f, 18f); // near edge-on, opened 12 deg
            const int seg = 64;
            int r0 = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float shade = 0.35f + 0.2f * Mathf.Abs(Mathf.Sin(a * 3f));
                v.Add(planet + tilt * (dir * 50f)); c.Add(Gray(shade * 0.8f));
                v.Add(planet + tilt * (dir * 66f)); c.Add(Gray(shade));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = r0 + i * 2;
                t.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }

            // Fleet: 6 slivers (wedge = 6 tris each) heading -X, high front-right.
            Vector3 fleet = Dir(40f, 38f) * 190f;
            Vector3[] offs = { Vector3.zero, new Vector3(-24f, 7f, 10f), new Vector3(-24f, -8f, -10f),
                               new Vector3(-48f, 13f, 22f), new Vector3(-50f, -4f, -20f), new Vector3(-72f, 5f, 3f) };
            float[] len = { 26f, 16f, 16f, 12f, 12f, 10f };
            for (int i = 0; i < offs.Length; i++) AddSliver(v, c, t, fleet + offs[i], len[i]);

            mesh.Clear();
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }

        private static void AddSliver(List<Vector3> v, List<Color> c, List<int> t, Vector3 at, float len)
        {
            float w = len * 0.18f, h = len * 0.08f;
            int b = v.Count;
            v.Add(at + new Vector3(len * 0.5f, 0f, 0f));     // nose (-> +X)
            v.Add(at + new Vector3(-len * 0.5f, h, 0f));     // dorsal
            v.Add(at + new Vector3(-len * 0.5f, -h, w));     // port
            v.Add(at + new Vector3(-len * 0.5f, -h, -w));    // starboard
            c.Add(Gray(0.6f)); c.Add(Gray(0.35f)); c.Add(Gray(0.15f)); c.Add(Gray(0.15f));
            t.AddRange(new[] { b, b + 1, b + 2, b, b + 3, b + 1, b, b + 2, b + 3, b + 1, b + 3, b + 2 });
        }

        private static Vector3 Dir(float azimuthDeg, float elevationDeg) =>
            Quaternion.Euler(-elevationDeg, azimuthDeg, 0f) * Vector3.forward;

        private static Color Shade(Vector3 n, float k) => Gray(Mathf.Lerp(0.02f, 0.8f, Mathf.Pow(Mathf.Clamp01(Vector3.Dot(n, LightDir) * 0.8f + 0.2f), 2.2f)) * k); // linear-space terminator
        private static Color Gray(float g) => new Color(g, g, g, 1f);
    }
}
