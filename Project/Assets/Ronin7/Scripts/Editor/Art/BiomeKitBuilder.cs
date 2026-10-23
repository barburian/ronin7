using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ronin7.Editor.Art
{
    /// <summary>Material slot a kit part is drawn with. Max 4 per room (roadmap P5-05).</summary>
    public enum KitSlot { Floor, Trim, Neon, Decal }

    /// <summary>One modular kit piece: a single multi-submesh mesh + BoxColliders, pivot floor-centre.</summary>
    public sealed class KitPiece
    {
        public string Name;
        public Vector3 Size;          // expected mesh bounds size (test tolerance 5 cm)
        public int MaxTris;
        public bool Occluder;         // walls / pillars get OccluderStatic
        public Bounds[] Colliders;    // null = one box over the mesh bounds
        public Vector3? Mount;        // optional "LightMount" child (baked light anchor for the room shells)
        /// <summary>Hero prop: imported model path (read every build, so a re-imported/retopo'd source is picked up).
        /// Gets its own material (the one exception to the 4-material cap) and is probe-lit, not lightmapped.</summary>
        public string SourceModel;
        public float HeroDiameter;    // hero: target max dimension (m)
        public Action<KitMesh> Build;
    }

    /// <summary>One biome's kit: where it lives, its 4 slot materials, and its pieces.
    /// Rust / Garden get added later as another entry like <see cref="BiomeKitBuilder.Program"/>.</summary>
    public sealed class BiomeKit
    {
        public string Name;
        public string Root;           // Assets/Ronin7/Art/Biomes/<Name>
        public string[] SlotMaterials; // indexed by KitSlot
        public Color EmissionTint;    // HDR, multiplies the AI base map used as emission map
        public Color DecalColor;      // HDR emissive tint of the decal atlas
        public KitPiece[] Pieces;
        public string MeshDir => Root + "/Meshes";
        public string PrefabDir => Root + "/Prefabs";
        public string PrefabPath(KitPiece p) => $"{PrefabDir}/{Name}_{p.Name}.prefab";
    }

    /// <summary>
    /// P5-05 biome kit builder (Amendment 13.6, hybrid): structural pieces built here to the kit-table
    /// triangle targets and skinned with the Unity AI materials already in the biome's Materials/ folder.
    /// Idempotent: mesh assets are refilled in place and prefabs overwritten, so GUIDs (and scene refs) survive.
    /// </summary>
    public static class BiomeKitBuilder
    {
        private const string NeonMat = "Assets/Ronin7/Art/Materials/Mat_CyberNeon_Cyan.mat";
        private const string ProgramRoot = "Assets/Ronin7/Art/Biomes/Program";

        public static readonly BiomeKit Program = new BiomeKit
        {
            Name = "Program",
            Root = ProgramRoot,
            SlotMaterials = new[]
            {
                ProgramRoot + "/Materials/Program_HexFloor.mat",
                ProgramRoot + "/Materials/Program_RackTrim.mat",
                NeonMat,
                ProgramRoot + "/Materials/Program_Decals.mat",
            },
            EmissionTint = new Color(0.9f, 1f, 1f) * 1.2f,
            DecalColor = new Color(0.15f, 0.85f, 1f) * 2f,
            Pieces = new[]
            {
                new KitPiece { Name = "Floor_A", Size = new Vector3(2, 0.1f, 2), MaxTris = 150, Build = m => FloorPlain(m) },
                new KitPiece { Name = "Floor_B", Size = new Vector3(2, 0.1f, 2), MaxTris = 150, Build = m => FloorInset(m) },
                new KitPiece { Name = "Floor_C", Size = new Vector3(2, 0.1f, 2), MaxTris = 150, Build = m => FloorQuads(m) },
                new KitPiece { Name = "Wall_Straight", Size = new Vector3(4, 4.5f, 0.5f), MaxTris = 1000, Occluder = true,
                    Build = m => RackWall(m, false) },
                new KitPiece { Name = "Wall_Corner", Size = new Vector3(4, 4.5f, 4), MaxTris = 1000, Occluder = true,
                    Colliders = new[] { B(-2, 0, -2, 2, 4.5f, -1.5f), B(-2, 0, -2, -1.5f, 4.5f, 2) },
                    Build = WallCorner },
                new KitPiece { Name = "Wall_Door", Size = new Vector3(4, 4.5f, 0.5f), MaxTris = 1000, Occluder = true,
                    Colliders = new[] { B(-2, 0, -0.25f, -1, 4.5f, 0.25f), B(1, 0, -0.25f, 2, 4.5f, 0.25f), B(-1, 3, -0.25f, 1, 4.5f, 0.25f) },
                    Build = m => RackWall(m, true) },
                new KitPiece { Name = "Pillar", Size = new Vector3(0.9f, 4.5f, 0.9f), MaxTris = 400, Occluder = true, Build = Pillar },
                new KitPiece { Name = "RoofRing_Segment", Size = new Vector3(4, 0.15f, 1.5f), MaxTris = 400, Build = RoofSegment },
                new KitPiece { Name = "LightBar", Size = new Vector3(1.5f, 0.06f, 0.11f), MaxTris = 100,
                    Mount = new Vector3(0, 0, 0.2f), Build = LightBar },
                new KitPiece { Name = "HoloGlyph", Size = new Vector3(1, 1.5f, 0.04f), MaxTris = 100, Build = HoloGlyph },
                // HERO: the user's Unity AI image->3D eye core (A13.6). Pivot bottom-centre, ~1.5 m. A pending retopo may
                // replace selected.glb in place; re-run this menu (+ the room shells) to pick it up. MaxTris drops with it.
                new KitPiece { Name = "Hero_EyeCore", Size = new Vector3(1.42f, 1.5f, 1.31f), MaxTris = 12000, HeroDiameter = 1.5f,
                    SourceModel = ProgramRoot + "/Raw/Program_Hero_EyeCore_AI_Assets/selected.glb" },
            },
        };

        [MenuItem("Tools/Space Samurai/Art/Build Biome Kit/Program", priority = 20)]
        public static void BuildProgram() => Build(Program);

        public static void Build(BiomeKit kit)
        {
            EnsureFolder(kit.MeshDir);
            EnsureFolder(kit.PrefabDir);
            EnsureFolder(kit.Root + "/Textures");
            PrepareMaterials(kit);
            var mats = new Material[4];
            for (int i = 0; i < 4; i++) mats[i] = AssetDatabase.LoadAssetAtPath<Material>(kit.SlotMaterials[i]);

            int totalTris = 0;
            foreach (var piece in kit.Pieces)
            {
                if (piece.SourceModel != null)
                {
                    var hero = SaveHeroMesh(kit, piece, out var heroMat);
                    totalTris += hero.triangles.Length / 3;
                    SavePrefab(kit, piece, hero, new[] { heroMat });
                    continue;
                }
                var km = new KitMesh();
                piece.Build(km);
                var mesh = SaveMesh($"{kit.MeshDir}/{kit.Name}_{piece.Name}.asset", km, out var slots);
                totalTris += mesh.triangles.Length / 3;
                SavePrefab(kit, piece, mesh, Array.ConvertAll(slots, s => mats[(int)s]));
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[BiomeKitBuilder] {kit.Name}: {kit.Pieces.Length} prefabs, {totalTris} tris total -> {kit.PrefabDir}");
        }

        // ---------------- pieces (1 unit = 1 m) ----------------

        private static readonly Vector2 FloorTile = new Vector2(2, 2);
        private static readonly Vector2 RackTile = new Vector2(2, 4.5f);

        private static void FloorPlain(KitMesh m) => m.Box(V(-1, -0.1f, -1), V(1, 0, 1), KitSlot.Floor, FloorTile);

        private static void FloorInset(KitMesh m)
        {
            var off = new Vector2(0.5f, 0);
            m.Box(V(-1, -0.1f, -1), V(1, 0, -0.85f), KitSlot.Floor, FloorTile, off);
            m.Box(V(-1, -0.1f, 0.85f), V(1, 0, 1), KitSlot.Floor, FloorTile, off);
            m.Box(V(-1, -0.1f, -0.85f), V(-0.85f, 0, 0.85f), KitSlot.Floor, FloorTile, off);
            m.Box(V(0.85f, -0.1f, -0.85f), V(1, 0, 0.85f), KitSlot.Floor, FloorTile, off);
            m.Box(V(-0.85f, -0.1f, -0.85f), V(0.85f, -0.02f, 0.85f), KitSlot.Floor, FloorTile, off); // 2 cm inset panel
        }

        private static void FloorQuads(KitMesh m)
        {
            var off = new Vector2(0.25f, 0.5f);
            m.Box(V(-1, -0.1f, -1), V(1, -0.03f, 1), KitSlot.Floor, FloorTile, off);
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -1 : 0.02f, z = (i & 2) == 0 ? -1 : 0.02f;
                m.Box(V(x, -0.03f, z), V(x + 0.98f, 0, z + 0.98f), KitSlot.Floor, FloorTile, off);
            }
        }

        /// <summary>4 m x 4.5 m server-rack wall, z in [-0.25, 0.25], rack face toward local +Z (the arena side).</summary>
        private static void RackWall(KitMesh m, bool door)
        {
            const float W = 2, H = 4.5f, Z0 = -0.25f, ZB = -0.05f, ZP = 0.22f, ZN = 0.24f;
            var t = KitSlot.Trim;
            if (door)
            {
                m.Box(V(-W, 0, Z0), V(-1, H, ZB), t, RackTile);
                m.Box(V(1, 0, Z0), V(W, H, ZB), t, RackTile);
                m.Box(V(-1, 3, Z0), V(1, H, ZB), t, RackTile);
                m.Box(V(-W, 0, ZB), V(-1.15f, 0.3f, ZP), t, RackTile);
                m.Box(V(1.15f, 0, ZB), V(W, 0.3f, ZP), t, RackTile);
                m.Box(V(-1.15f, 3, ZB), V(1.15f, 3.15f, ZP), t, RackTile);                 // header
                m.Box(V(-1, 3.05f, ZP), V(1, 3.09f, ZN), KitSlot.Neon, RackTile);         // header strip
            }
            else
            {
                m.Box(V(-W, 0, Z0), V(W, H, ZB), t, RackTile);
                m.Box(V(-W, 0, ZB), V(W, 0.3f, ZP), t, RackTile);
            }
            m.Box(V(-W, 4.1f, Z0), V(W, H, 0.25f), t, RackTile);                          // cornice
            m.Box(V(-W, 4.02f, ZP), V(W, 4.06f, ZN), KitSlot.Neon, RackTile);             // under-cornice strip

            float[] posts = door ? new[] { -2f, -1.85f, -1.15f, -1f, 1f, 1.15f, 1.85f, 2f }
                                 : new[] { -2f, -1.85f, -0.075f, 0.075f, 1.85f, 2f };
            float y0 = door ? 0 : 0.3f;
            for (int i = 0; i < posts.Length; i += 2)
            {
                float a = posts[i], b = posts[i + 1], c = (a + b) / 2;
                m.Box(V(a, y0, ZB), V(b, 4.1f, ZP), t, RackTile);
                m.Box(V(c - 0.02f, 0.5f, ZP), V(c + 0.02f, door ? 2.9f : 3.9f, ZN), KitSlot.Neon, RackTile);
            }
            // rack units: 6 slightly proud server blades per bay (skipping the door opening)
            for (int i = 1; i + 1 < posts.Length; i += 2)
            {
                float a = posts[i], b = posts[i + 1];
                if (door && a >= -1f && b <= 1f) continue;
                for (int k = 0; k < 6; k++)
                {
                    float u0 = 0.45f + k * 0.6f;
                    m.Box(V(a + 0.03f, u0, ZB), V(b - 0.03f, u0 + 0.5f, 0.05f), t, RackTile, new Vector2(0, k * 0.13f));
                }
            }
        }

        /// <summary>Inner 90-degree corner on a 4x4 m cell: one run along X (faces +Z), one along Z (faces +X).</summary>
        private static void WallCorner(KitMesh m)
        {
            m.Xf = Matrix4x4.Translate(V(0, 0, -1.75f));
            RackWall(m, false);
            m.Xf = Matrix4x4.TRS(V(-1.75f, 0, 0), Quaternion.Euler(0, 90, 0), Vector3.one);
            RackWall(m, false);
            m.Xf = Matrix4x4.identity;
        }

        private static void Pillar(KitMesh m)
        {
            m.Prism(Chamfered(0.45f, 0.12f), 0, 0.3f, KitSlot.Trim, 2);
            m.Prism(Chamfered(0.4f, 0.1f), 0.3f, 4.2f, KitSlot.Trim, 2);
            m.Prism(Chamfered(0.45f, 0.12f), 4.2f, 4.5f, KitSlot.Trim, 2);
            for (int i = 0; i < 4; i++) // glyph strip on each flat face
            {
                m.Xf = Matrix4x4.Rotate(Quaternion.Euler(0, 90 * i, 0));
                m.Box(V(-0.06f, 0.8f, 0.4f), V(0.06f, 3.8f, 0.415f), KitSlot.Neon, RackTile);
            }
            m.Xf = Matrix4x4.identity;
        }

        /// <summary>Holo-grid roof segment sitting on a wall top (y 4.5), overhanging 1.25 m inward; open sky beyond.</summary>
        private static void RoofSegment(KitMesh m)
        {
            const float y0 = 4.5f, y1 = 4.65f;
            m.Box(V(-2, y0, -0.25f), V(2, y1, -0.1f), KitSlot.Trim, RackTile);
            m.Box(V(-2, y0, 1.1f), V(2, y1, 1.25f), KitSlot.Trim, RackTile);
            foreach (float x in new[] { -2f, -0.05f, 1.9f })
                m.Box(V(x, y0, -0.1f), V(x + 0.1f, y1, 1.1f), KitSlot.Trim, RackTile);
            foreach (float z in new[] { 0.3f, 0.7f })
                m.Box(V(-2, y1 - 0.03f, z), V(2, y1, z + 0.02f), KitSlot.Neon, RackTile);
            foreach (float x in new[] { -1f, 1f })
                m.Box(V(x - 0.01f, y1 - 0.03f, -0.1f), V(x + 0.01f, y1, 1.1f), KitSlot.Neon, RackTile);
        }

        /// <summary>1.5 m cyan tube on two brackets; pivot = wall-mount centre, light points along +Z.</summary>
        private static void LightBar(KitMesh m)
        {
            m.Box(V(-0.62f, -0.03f, 0), V(-0.58f, 0.03f, 0.05f), KitSlot.Trim, RackTile);
            m.Box(V(0.58f, -0.03f, 0), V(0.62f, 0.03f, 0.05f), KitSlot.Trim, RackTile);
            m.Box(V(-0.75f, -0.03f, 0.05f), V(0.75f, 0.03f, 0.11f), KitSlot.Neon, RackTile);
        }

        /// <summary>1 x 1.5 m glyph frame (CyberNeon) with a double-sided glyph-atlas pane (animated later).</summary>
        private static void HoloGlyph(KitMesh m)
        {
            m.Box(V(-0.5f, 0, -0.02f), V(-0.46f, 1.5f, 0.02f), KitSlot.Neon, RackTile);
            m.Box(V(0.46f, 0, -0.02f), V(0.5f, 1.5f, 0.02f), KitSlot.Neon, RackTile);
            m.Box(V(-0.46f, 0, -0.02f), V(0.46f, 0.04f, 0.02f), KitSlot.Neon, RackTile);
            m.Box(V(-0.46f, 1.46f, -0.02f), V(0.46f, 1.5f, 0.02f), KitSlot.Neon, RackTile);
            m.Pane(V(-0.46f, 0.04f, 0), V(0.46f, 1.46f, 0), KitSlot.Decal, new Rect(0, 0, 0.5f, 0.75f));
        }

        // ---------------- assets ----------------

        private static Mesh SaveMesh(string path, KitMesh km, out KitSlot[] slots)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            km.Fill(mesh, out slots);
            mesh.name = Path.GetFileNameWithoutExtension(path);
            Unwrapping.GenerateSecondaryUVSet(mesh);
            if (isNew) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void SavePrefab(BiomeKit kit, KitPiece piece, Mesh mesh, Material[] mats)
        {
            var go = new GameObject($"{kit.Name}_{piece.Name}");
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = mats;
                bool hero = piece.SourceModel != null;
                mr.receiveGI = hero ? ReceiveGI.LightProbes : ReceiveGI.Lightmaps;
                foreach (var b in piece.Colliders ?? new[] { mesh.bounds })
                {
                    var bc = go.AddComponent<BoxCollider>();
                    bc.center = b.center; bc.size = b.size;
                }
                if (piece.Mount.HasValue)
                {
                    var mount = new GameObject("LightMount");
                    mount.transform.SetParent(go.transform, false);
                    mount.transform.localPosition = piece.Mount.Value;
                }
                // The hero is probe-lit so its mesh can be swapped / retopo'd without a rebake.
                var flags = hero ? StaticEditorFlags.BatchingStatic : StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic;
                if (piece.Occluder) flags |= StaticEditorFlags.OccluderStatic;
                foreach (var t in go.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
                PrefabUtility.SaveAsPrefabAsset(go, kit.PrefabPath(piece));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        /// <summary>Copies the source model's mesh (node transform baked in) into the kit's mesh asset, scaled to
        /// <see cref="KitPiece.HeroDiameter"/> with the pivot at bottom-centre; extracts its glTF textures to PNGs and
        /// builds a URP/Lit material from them.</summary>
        private static Mesh SaveHeroMesh(BiomeKit kit, KitPiece piece, out Material mat)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(piece.SourceModel);
            if (root == null) throw new FileNotFoundException("Hero source model missing", piece.SourceModel);
            var mf = root.GetComponentInChildren<MeshFilter>();
            var src = mf.sharedMesh;
            var xf = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var v = src.vertices; var n = src.normals; var tg = src.tangents;
            for (int i = 0; i < v.Length; i++) v[i] = xf.MultiplyPoint3x4(v[i]);
            var b = GeometryUtility.CalculateBounds(v, Matrix4x4.identity);
            float s = piece.HeroDiameter / Mathf.Max(b.size.x, b.size.y, b.size.z);
            var off = new Vector3(b.center.x, b.min.y, b.center.z);
            for (int i = 0; i < v.Length; i++) v[i] = (v[i] - off) * s;
            for (int i = 0; i < n.Length; i++) n[i] = xf.MultiplyVector(n[i]).normalized;
            for (int i = 0; i < tg.Length; i++) { var t = xf.MultiplyVector(tg[i]).normalized; tg[i] = new Vector4(t.x, t.y, t.z, tg[i].w); }

            string path = $"{kit.MeshDir}/{kit.Name}_{piece.Name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.indexFormat = src.indexFormat;
            mesh.vertices = v; mesh.normals = n; mesh.tangents = tg; mesh.uv = src.uv;
            mesh.triangles = src.triangles;
            mesh.RecalculateBounds();
            mesh.name = Path.GetFileNameWithoutExtension(path);
            if (isNew) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);

            // glTF material -> URP/Lit. glTF metallicRoughness packs metal in B, roughness in G; URP wants metal R, smoothness A.
            var g = mf.GetComponent<MeshRenderer>().sharedMaterial;
            Texture baseTex = null, normalTex = null, mrTex = null;
            foreach (var name in g.GetTexturePropertyNames())
            {
                var t = g.GetTexture(name);
                string k = name.ToLowerInvariant(); // glTFast: baseColorTexture / metallicRoughnessTexture / normalTexture
                if (t == null) continue;
                if (k.Contains("normal")) normalTex = t;
                else if (k.Contains("metallic")) mrTex = t;
                else if (k.Contains("basecolor") || k == "_maintex") baseTex = t;
            }
            string texBase = $"{kit.Root}/Textures/{kit.Name}_{piece.Name}";
            var albedo = ExtractTexture(baseTex, texBase + "_BaseColor.png", true, null);
            var normal = ExtractTexture(normalTex, texBase + "_Normal.png", false, null, TextureImporterType.NormalMap);
            var mr = ExtractTexture(mrTex, texBase + "_MetallicSmoothness.png", false, c => new Color(c.b, c.b, c.b, 1 - c.g));

            string matPath = $"{kit.Root}/Materials/{kit.Name}_{piece.Name}.mat";
            mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", normal);
            if (normal != null) mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", mr);
            if (mr != null) mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_Smoothness", 1f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mesh;
        }

        /// <summary>Blits a (possibly non-readable, embedded) texture to a PNG asset; optional per-pixel remap.</summary>
        private static Texture2D ExtractTexture(Texture src, string path, bool srgb, Func<Color, Color> remap,
            TextureImporterType type = TextureImporterType.Default)
        {
            if (src == null) return null;
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32,
                srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, !srgb);
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            if (remap != null)
            {
                var px = tex.GetPixels();
                for (int i = 0; i < px.Length; i++) px[i] = remap(px[i]);
                tex.SetPixels(px);
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = type;
            imp.sRGBTexture = srgb;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>AI base maps double as emission maps (bright seams/strips glow, black stays black); builds the decal atlas + material.</summary>
        private static void PrepareMaterials(BiomeKit kit)
        {
            foreach (var slot in new[] { KitSlot.Floor, KitSlot.Trim })
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(kit.SlotMaterials[(int)slot]);
                if (mat == null) throw new FileNotFoundException("Biome material missing", kit.SlotMaterials[(int)slot]);
                MakeEmissive(mat, mat.GetTexture("_BaseMap"), kit.EmissionTint);
                mat.SetFloat("_Smoothness", 0.6f);
                mat.SetFloat("_Metallic", 0.5f);
                EditorUtility.SetDirty(mat);
            }

            string texPath = $"{kit.Root}/Textures/{kit.Name}_DecalAtlas.png";
            File.WriteAllBytes(texPath, DecalAtlasPng(kit.Name.Length * 7919));
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            string decalPath = kit.SlotMaterials[(int)KitSlot.Decal];
            var decal = AssetDatabase.LoadAssetAtPath<Material>(decalPath);
            if (decal == null)
            {
                decal = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(decal, decalPath);
            }
            decal.SetTexture("_BaseMap", atlas);
            decal.SetColor("_BaseColor", new Color(0.2f, 0.8f, 1f, 1f));
            decal.SetFloat("_AlphaClip", 1);
            decal.SetFloat("_Cutoff", 0.5f);
            decal.EnableKeyword("_ALPHATEST_ON");
            decal.SetOverrideTag("RenderType", "TransparentCutout");
            decal.renderQueue = (int)RenderQueue.AlphaTest;
            decal.SetFloat("_Smoothness", 0.3f);
            MakeEmissive(decal, atlas, kit.DecalColor);
            EditorUtility.SetDirty(decal);
        }

        private static void MakeEmissive(Material mat, Texture map, Color hdr)
        {
            mat.SetTexture("_EmissionMap", map);
            mat.SetColor("_EmissionColor", hdr);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }

        /// <summary>1024² atlas, 4x4 cells: glyph cells (seeded stroke grids) alternate with scan-line cells. Cyan on clear.</summary>
        private static byte[] DecalAtlasPng(int seed)
        {
            const int S = 1024, C = 256;
            var px = new Color32[S * S];
            var on = new Color32(200, 255, 255, 255);
            var rng = new System.Random(seed);
            var masks = new int[32];
            for (int i = 0; i < masks.Length; i++) masks[i] = rng.Next() & 0x1FFFFFF;
            for (int cell = 0; cell < 16; cell++)
            {
                int ox = (cell % 4) * C, oy = (cell / 4) * C;
                bool glyph = ((cell % 4) + (cell / 4)) % 2 == 0;
                for (int y = 24; y < C - 24; y++)
                for (int x = 24; x < C - 24; x++)
                {
                    bool hit;
                    if (glyph)
                    {
                        // 5x5 stroke grid: a cell lights up its row/column bars by a per-glyph random mask
                        int gx = (x - 24) * 5 / (C - 48), gy = (y - 24) * 5 / (C - 48);
                        int lx = (x - 24) * 5 % (C - 48), ly = (y - 24) * 5 % (C - 48);
                        bool hBar = ly < 60, vBar = lx < 60;
                        int bit = gy * 5 + gx;
                        hit = (hBar && ((masks[cell] >> bit) & 1) != 0) || (vBar && ((masks[cell + 16] >> bit) & 1) != 0);
                    }
                    else hit = (y % 12) < 3 && ((x / 32 + y / 12 + cell) % 5 != 0);
                    if (hit) px[(oy + y) * S + ox + x] = on;
                }
            }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            var png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            return png;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        private static Bounds B(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            var b = new Bounds(); b.SetMinMax(V(x0, y0, z0), V(x1, y1, z1)); return b;
        }

        /// <summary>Square outline of half-size h with 45-degree chamfered corners (an octagon), in XZ.</summary>
        private static Vector2[] Chamfered(float h, float c) => new[]
        {
            new Vector2(h - c, -h), new Vector2(h, -h + c), new Vector2(h, h - c), new Vector2(h - c, h),
            new Vector2(-h + c, h), new Vector2(-h, h - c), new Vector2(-h, -h + c), new Vector2(-h + c, -h),
        };
    }

    /// <summary>Tiny mesh accumulator: flat-faced boxes/prisms/panes with world-ish planar UVs,
    /// one submesh per <see cref="KitSlot"/>. Winding is fixed up per triangle against the intended normal.</summary>
    public sealed class KitMesh
    {
        public Matrix4x4 Xf = Matrix4x4.identity;
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Vector3> _n = new List<Vector3>();
        private readonly List<Vector2> _uv = new List<Vector2>();
        private readonly SortedDictionary<KitSlot, List<int>> _tris = new SortedDictionary<KitSlot, List<int>>();

        private int Vert(Vector3 p, Vector3 n, Vector2 uv)
        {
            _v.Add(Xf.MultiplyPoint3x4(p)); _n.Add(Xf.MultiplyVector(n).normalized); _uv.Add(uv);
            return _v.Count - 1;
        }

        private void Tri(int a, int b, int c, KitSlot s)
        {
            if (!_tris.TryGetValue(s, out var list)) _tris[s] = list = new List<int>();
            var want = _n[a] + _n[b] + _n[c];
            if (Vector3.Dot(Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]), want) < 0) (b, c) = (c, b);
            list.Add(a); list.Add(b); list.Add(c);
        }

        private void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n, KitSlot s, Func<Vector3, Vector2> uv)
        {
            int a = Vert(p0, n, uv(p0)), b = Vert(p1, n, uv(p1)), c = Vert(p2, n, uv(p2)), d = Vert(p3, n, uv(p3));
            Tri(a, b, c, s); Tri(a, c, d, s);
        }

        /// <summary>Axis-aligned box (12 tris). UV = local planar projection / tile metres + offset.</summary>
        public void Box(Vector3 min, Vector3 max, KitSlot s, Vector2 tile, Vector2 off = default)
        {
            Vector3 c = (min + max) / 2, h = (max - min) / 2;
            foreach (var n in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                Vector3 u = n.y != 0 ? Vector3.right : n.x != 0 ? Vector3.forward : Vector3.right;
                Vector3 w = n.y != 0 ? Vector3.forward : Vector3.up;
                Vector3 fc = c + Vector3.Scale(n, h), U = Vector3.Scale(u, h), W = Vector3.Scale(w, h);
                Quad(fc - U - W, fc - U + W, fc + U + W, fc + U - W, n, s,
                    p => new Vector2(Vector3.Dot(p, u) / tile.x + off.x, Vector3.Dot(p, w) / tile.y + off.y));
            }
        }

        /// <summary>Vertical prism from a convex XZ outline, flat-shaded, with capped ends.</summary>
        public void Prism(Vector2[] o, float y0, float y1, KitSlot s, float tile)
        {
            float run = 0;
            for (int i = 0; i < o.Length; i++)
            {
                Vector2 a = o[i], b = o[(i + 1) % o.Length];
                var n = new Vector3(b.y - a.y, 0, a.x - b.x).normalized;
                if (Vector3.Dot(n, new Vector3(a.x + b.x, 0, a.y + b.y)) < 0) n = -n;
                float r0 = run, r1 = run += (b - a).magnitude;
                int p0 = Vert(new Vector3(a.x, y0, a.y), n, new Vector2(r0 / tile, y0 / tile));
                int p1 = Vert(new Vector3(a.x, y1, a.y), n, new Vector2(r0 / tile, y1 / tile));
                int p2 = Vert(new Vector3(b.x, y1, b.y), n, new Vector2(r1 / tile, y1 / tile));
                int p3 = Vert(new Vector3(b.x, y0, b.y), n, new Vector2(r1 / tile, y0 / tile));
                Tri(p0, p1, p2, s); Tri(p0, p2, p3, s);
            }
            foreach (var (y, n) in new[] { (y1, Vector3.up), (y0, Vector3.down) })
            {
                int first = _v.Count;
                foreach (var p in o) Vert(new Vector3(p.x, y, p.y), n, p / tile);
                for (int i = 1; i + 1 < o.Length; i++) Tri(first, first + i, first + i + 1, s);
            }
        }

        /// <summary>Double-sided vertical pane in the XY plane (z = min.z) mapping the given atlas rect.</summary>
        public void Pane(Vector3 min, Vector3 max, KitSlot s, Rect uv)
        {
            foreach (var n in new[] { Vector3.forward, Vector3.back })
                Quad(min, new Vector3(min.x, max.y, min.z), max, new Vector3(max.x, min.y, min.z), n, s,
                    p => new Vector2(uv.x + uv.width * Mathf.InverseLerp(min.x, max.x, p.x), uv.y + uv.height * Mathf.InverseLerp(min.y, max.y, p.y)));
        }

        public void Fill(Mesh mesh, out KitSlot[] slots)
        {
            mesh.Clear();
            mesh.indexFormat = _v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(_v); mesh.SetNormals(_n); mesh.SetUVs(0, _uv);
            slots = new KitSlot[_tris.Count];
            _tris.Keys.CopyTo(slots, 0);
            mesh.subMeshCount = slots.Length;
            for (int i = 0; i < slots.Length; i++) mesh.SetTriangles(_tris[slots[i]], i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }
    }
}
