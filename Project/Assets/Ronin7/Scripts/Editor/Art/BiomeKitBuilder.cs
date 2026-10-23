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
        public Color DecalColor;      // HDR emissive tint of the decal atlas (black = not emissive)
        public Color DecalBase = new Color(0.2f, 0.8f, 1f, 1f); // base colour multiplied over the atlas
        public Func<int, byte[]> DecalAtlas; // seed -> 1024x1024 PNG, 4x4 cells of 256 px
        /// <summary>false: the AI base maps double as emission maps (Program, black-backed). true: the floor is not
        /// emissive and the trim's emission map is a derived mask of its bright amber pixels (Rust lamp slits).</summary>
        public bool MaskedEmission;
        /// <summary>MaskedEmission only: base-map pixel -> emission mask. Trim defaults to the Rust lamp mask; a null
        /// floor mask leaves the floor non-emissive.</summary>
        public Func<Color, Color> TrimMask, FloorMask;
        public float Smoothness = 0.6f, Metallic = 0.5f;
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
            DecalAtlas = DecalAtlasPng,
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

        private const string RustRoot = "Assets/Ronin7/Art/Biomes/Rust";
        private const string AmberNeonMat = "Assets/Ronin7/Art/Materials/Mat_CyberNeon_Amber.mat";

        /// <summary>Rust: Ash-World foundry (kit table, Rust column). Same slots / pivots / contracts as Program.</summary>
        public static readonly BiomeKit Rust = new BiomeKit
        {
            Name = "Rust",
            Root = RustRoot,
            SlotMaterials = new[]
            {
                RustRoot + "/Materials/Rust_GrateFloor.mat",
                RustRoot + "/Materials/Rust_BulkheadTrim.mat",
                AmberNeonMat,
                RustRoot + "/Materials/Rust_Decals.mat",
            },
            MaskedEmission = true,
            EmissionTint = new Color(1f, 0.85f, 0.7f) * 2.5f,
            DecalColor = Color.black,
            DecalBase = Color.white,
            DecalAtlas = RustDecalAtlasPng,
            Smoothness = 0.35f, Metallic = 0.6f,
            Pieces = new[]
            {
                new KitPiece { Name = "Floor_A", Size = new Vector3(2, 0.1f, 2), MaxTris = 200, Build = m => FloorPlain(m) },
                new KitPiece { Name = "Floor_B", Size = new Vector3(2, 0.1f, 2), MaxTris = 200, Build = m => FloorInset(m) },
                new KitPiece { Name = "Floor_C", Size = new Vector3(2, 0.1f, 2), MaxTris = 200, Build = m => FloorQuads(m) },
                new KitPiece { Name = "Wall_Straight", Size = new Vector3(4, 4.5f, 0.5f), MaxTris = 1500, Occluder = true,
                    Build = m => BulkheadWall(m, false) },
                new KitPiece { Name = "Wall_Corner", Size = new Vector3(4, 4.5f, 4), MaxTris = 1500, Occluder = true,
                    Colliders = new[] { B(-2, 0, -2, 2, 4.5f, -1.5f), B(-2, 0, -2, -1.5f, 4.5f, 2) },
                    Build = m => Corner(m, k => BulkheadWall(k, false)) },
                new KitPiece { Name = "Wall_Door", Size = new Vector3(4, 4.5f, 0.5f), MaxTris = 1500, Occluder = true,
                    Colliders = new[] { B(-2, 0, -0.25f, -1, 4.5f, 0.25f), B(1, 0, -0.25f, 2, 4.5f, 0.25f), B(-1, 3, -0.25f, 1, 4.5f, 0.25f) },
                    Build = m => BulkheadWall(m, true) },
                new KitPiece { Name = "Pillar", Size = new Vector3(0.8f, 4.5f, 0.8f), MaxTris = 600, Occluder = true, Build = IBeam },
                new KitPiece { Name = "RoofRing_Segment", Size = new Vector3(4, 0.2f, 1.5f), MaxTris = 400, Build = SkylightSegment },
                new KitPiece { Name = "Gantry_Beam", Size = new Vector3(4, 0.6f, 0.4f), MaxTris = 200, Build = GantryBeam },
                new KitPiece { Name = "CraneHoist", Size = new Vector3(0.8f, 2.6f, 0.6f), MaxTris = 300, Build = CraneHoist },
                new KitPiece { Name = "SodiumCage", Size = new Vector3(0.24f, 0.4f, 0.31f), MaxTris = 200,
                    Mount = new Vector3(0, 0, 0.45f), Build = SodiumCage },
                // Animated piece (mesh only for now): the rotor needs its own transform before it can spin
                // (gate it with LightBudget.ShouldAnimate).
                new KitPiece { Name = "VentFan", Size = new Vector3(1.2f, 1.2f, 0.25f), MaxTris = 400, Build = VentFan },
                // HERO: the user's image->3D broken mech once Raw/.../selected.glb exists; until then a kneeling block
                // placeholder (Build). Either way probe-lit, max dimension 2.5 m, pivot bottom-centre, same prefab path.
                new KitPiece { Name = "Hero_BrokenMech", Size = new Vector3(2.1f, 2.5f, 2.3f), MaxTris = 12000, HeroDiameter = 2.5f,
                    SourceModel = RustRoot + "/Raw/Rust_Hero_BrokenMech_AI_Assets/selected.glb", Build = BrokenMechPlaceholder },
            },
        };

        private const string GardenRoot = "Assets/Ronin7/Art/Biomes/Garden";
        private const string GreenNeonMat = "Assets/Ronin7/Art/Materials/Mat_CyberNeon_Green.mat";
        /// <summary>Garden atlas column 0 (u 0-0.25, full height) = the AI vine card; columns 1-3 = floor decals.</summary>
        private static readonly Rect VineUV = new Rect(0, 0, 0.25f, 1);

        /// <summary>Garden: Silent Garden bio-dome (kit table, Garden column). Slot 4 is the alpha-clip foliage material;
        /// its atlas also carries the floor decals (petals / puddles / lichen), so the kit stays at 4 materials.</summary>
        public static readonly BiomeKit Garden = new BiomeKit
        {
            Name = "Garden",
            Root = GardenRoot,
            SlotMaterials = new[]
            {
                GardenRoot + "/Materials/Garden_MossStone.mat",
                GardenRoot + "/Materials/Garden_LatticeTrim.mat",
                GreenNeonMat,
                GardenRoot + "/Materials/Garden_Foliage.mat",
            },
            MaskedEmission = true,
            FloorMask = LichenMask,
            TrimMask = PodMask,
            EmissionTint = new Color(0.6f, 1.2f, 0.7f) * 0.8f,
            DecalColor = Color.black,
            DecalBase = Color.white,
            DecalAtlas = GardenAtlasPng,
            Smoothness = 0.3f, Metallic = 0f,
            Pieces = new[]
            {
                new KitPiece { Name = "Floor_A", Size = new Vector3(2, 0.1f, 2), MaxTris = 300, Build = m => Slabs(m, 0) },
                new KitPiece { Name = "Floor_B", Size = new Vector3(2, 0.1f, 2), MaxTris = 300, Build = m => Slabs(m, 1) },
                new KitPiece { Name = "Floor_C", Size = new Vector3(2, 0.1f, 2), MaxTris = 300, Build = m => Slabs(m, 2) },
                new KitPiece { Name = "Wall_Straight", Size = new Vector3(4, 4.5f, 0.5f), MaxTris = 1200, Occluder = true,
                    Build = m => LatticeWall(m, false) },
                new KitPiece { Name = "Wall_Corner", Size = new Vector3(4, 4.5f, 4), MaxTris = 1200, Occluder = true,
                    Colliders = new[] { B(-2, 0, -2, 2, 4.5f, -1.5f), B(-2, 0, -2, -1.5f, 4.5f, 2) },
                    Build = m => Corner(m, k => LatticeWall(k, false)) },
                new KitPiece { Name = "Wall_Door", Size = new Vector3(4, 4.5f, 0.5f), MaxTris = 1200, Occluder = true,
                    Colliders = new[] { B(-2, 0, -0.25f, -1, 4.5f, 0.25f), B(1, 0, -0.25f, 2, 4.5f, 0.25f), B(-1, 3, -0.25f, 1, 4.5f, 0.25f) },
                    Build = m => LatticeWall(m, true) },
                new KitPiece { Name = "Pillar", Size = new Vector3(2, 4.2f, 0.4f), MaxTris = 1500, Build = TrellisArch },
                new KitPiece { Name = "RoofRing_Segment", Size = new Vector3(4, 1.26f, 1.7f), MaxTris = 400, Build = DomeRibs },
                new KitPiece { Name = "PodCluster", Size = new Vector3(0.37f, 0.75f, 0.21f), MaxTris = 300,
                    Mount = new Vector3(0, 0, 0.4f), Build = PodCluster },
                // Animated piece: vine cards, vertex colour R = sway weight (top 0 -> tip 1) for the later vertex sway
                // (gate it with LightBudget.ShouldAnimate). Pivot = top centre.
                new KitPiece { Name = "VineCards", Size = new Vector3(1.15f, 2.6f, 0.1f), MaxTris = 100, Build = VineCards },
                // HERO: the user's image->3D bioluminescent tree once Raw/.../selected.glb exists; until then a low-poly
                // placeholder (Build). Probe-lit, max dimension 3.5 m, pivot bottom-centre, same prefab path.
                new KitPiece { Name = "Hero_BioTree", Size = new Vector3(2.8f, 3.5f, 2.8f), MaxTris = 12000, HeroDiameter = 3.5f,
                    SourceModel = GardenRoot + "/Raw/Garden_Hero_BioTree_AI_Assets/selected.glb", Build = BioTreePlaceholder },
            },
        };

        [MenuItem("Tools/Space Samurai/Art/Build Biome Kit/Garden", priority = 20)]
        public static void BuildGarden()
        {
            // Green glow pods: a CyberNeon variant (same shader as the cyan strips).
            if (AssetDatabase.LoadAssetAtPath<Material>(GreenNeonMat) == null) AssetDatabase.CopyAsset(NeonMat, GreenNeonMat);
            var green = AssetDatabase.LoadAssetAtPath<Material>(GreenNeonMat);
            green.SetColor("_BaseColor", new Color(0.02f, 0.07f, 0.03f));
            green.SetColor("_EmissionColor", new Color(0.25f, 1.6f, 0.45f));
            green.SetColor("_FresnelColor", new Color(0.15f, 0.9f, 0.3f));
            EditorUtility.SetDirty(green);
            Build(Garden);
        }

        [MenuItem("Tools/Space Samurai/Art/Build Biome Kit/Program", priority = 20)]
        public static void BuildProgram() => Build(Program);

        [MenuItem("Tools/Space Samurai/Art/Build Biome Kit/Rust", priority = 20)]
        public static void BuildRust()
        {
            // Amber sodium glow: a CyberNeon variant (same shader as the cyan strips).
            if (AssetDatabase.LoadAssetAtPath<Material>(AmberNeonMat) == null) AssetDatabase.CopyAsset(NeonMat, AmberNeonMat);
            var amber = AssetDatabase.LoadAssetAtPath<Material>(AmberNeonMat);
            amber.SetColor("_BaseColor", new Color(0.07f, 0.035f, 0f));
            amber.SetColor("_EmissionColor", new Color(7.5f, 3.4f, 0.4f));
            amber.SetColor("_FresnelColor", new Color(3.5f, 1.6f, 0.2f));
            EditorUtility.SetDirty(amber);
            Build(Rust);
        }

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
                if (piece.SourceModel != null && (piece.Build == null || AssetDatabase.LoadAssetAtPath<GameObject>(piece.SourceModel) != null))
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
        private static void WallCorner(KitMesh m) => Corner(m, k => RackWall(k, false));

        private static void Corner(KitMesh m, Action<KitMesh> wall)
        {
            m.Xf = Matrix4x4.Translate(V(0, 0, -1.75f));
            wall(m);
            m.Xf = Matrix4x4.TRS(V(-1.75f, 0, 0), Quaternion.Euler(0, 90, 0), Vector3.one);
            wall(m);
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

        // ---------------- Rust pieces ----------------

        private static readonly Vector2 BulkTile = new Vector2(4, 4.5f); // one bulkhead base map per 4 m wall (2 rows of lamp slits)

        /// <summary>4 x 4.5 m riveted bulkhead, z in [-0.25, 0.25], face +Z: plinth, ribs, mid band, cornice, two pipe runs.</summary>
        private static void BulkheadWall(KitMesh m, bool door)
        {
            const float W = 2, H = 4.5f, Z0 = -0.25f, ZB = -0.05f;
            var t = KitSlot.Trim;
            if (door)
            {
                m.Box(V(-W, 0, Z0), V(-1, H, ZB), t, BulkTile);
                m.Box(V(1, 0, Z0), V(W, H, ZB), t, BulkTile);
                m.Box(V(-1, 3, Z0), V(1, H, ZB), t, BulkTile);
                m.Box(V(-1.2f, 0, ZB), V(-1, 3.2f, 0.15f), t, BulkTile);                    // jambs + lintel
                m.Box(V(1, 0, ZB), V(1.2f, 3.2f, 0.15f), t, BulkTile);
                m.Box(V(-1, 3, ZB), V(1, 3.2f, 0.15f), t, BulkTile);
                m.Box(V(-1, 3.05f, 0.15f), V(1, 3.1f, 0.18f), KitSlot.Neon, BulkTile);       // amber lintel strip
                foreach (float a in new[] { -W, 1.2f }) m.Box(V(a, 0, ZB), V(a + 0.8f, 0.25f, 0.1f), t, BulkTile);
            }
            else
            {
                m.Box(V(-W, 0, Z0), V(W, H, ZB), t, BulkTile);
                m.Box(V(-W, 0, ZB), V(W, 0.25f, 0.1f), t, BulkTile);                         // plinth
            }
            m.Box(V(-W, 4.1f, Z0), V(W, H, 0.25f), t, BulkTile);                              // cornice
            foreach (float a in new[] { -2f, 1.85f })                                         // edge ribs
                m.Box(V(a, 0.25f, ZB), V(a + 0.15f, 4.1f, 0.15f), t, BulkTile);
            if (!door)
            {
                m.Box(V(-0.075f, 0.25f, ZB), V(0.075f, 4.1f, 0.15f), t, BulkTile);           // centre rib
                m.Box(V(-W, 2.2f, ZB), V(W, 2.3f, 0.05f), t, BulkTile);                       // mid band
            }
            // pipe runs (cheap octagonal cylinders) proud of the ribs, above the door head
            Pipe(m, 3.55f, 0.15f, 0.08f, -W, W);
            Pipe(m, 3.8f, 0.17f, 0.05f, -W, W);
        }

        /// <summary>Octagonal pipe along X at height y, depth z.</summary>
        private static void Pipe(KitMesh m, float y, float z, float r, float x0, float x1)
        {
            var keep = m.Xf;
            m.Xf = keep * Matrix4x4.TRS(V(0, y, z), Quaternion.Euler(0, 0, -90), Vector3.one);
            m.Prism(Chamfered(r, r * 0.3f), x0, x1, KitSlot.Trim, 1);
            m.Xf = keep;
        }

        /// <summary>0.6 m I-beam column on base/top plates with two chains hanging on its arena face.</summary>
        private static void IBeam(KitMesh m)
        {
            var t = KitSlot.Trim;
            m.Box(V(-0.4f, 0, -0.4f), V(0.4f, 0.1f, 0.4f), t, BulkTile);
            m.Box(V(-0.4f, 4.4f, -0.4f), V(0.4f, 4.5f, 0.4f), t, BulkTile);
            m.Box(V(-0.3f, 0.1f, -0.3f), V(0.3f, 4.4f, -0.24f), t, BulkTile);
            m.Box(V(-0.3f, 0.1f, 0.24f), V(0.3f, 4.4f, 0.3f), t, BulkTile);
            m.Box(V(-0.04f, 0.1f, -0.24f), V(0.04f, 4.4f, 0.24f), t, BulkTile);
            foreach (var (x, links) in new[] { (-0.15f, 14), (0.18f, 9) })
                for (int i = 0; i < links; i++)
                {
                    float y = 4.3f - i * 0.1f, hw = i % 2 == 0 ? 0.035f : 0.01f;
                    m.Box(V(x - hw, y - 0.12f, 0.3f), V(x + hw, y, i % 2 == 0 ? 0.32f : 0.36f), t, BulkTile);
                }
        }

        /// <summary>Skylight-frame segment on a wall top (y 4.5-4.7), overhanging 1.25 m toward +Z; open sky between the ribs.</summary>
        private static void SkylightSegment(KitMesh m)
        {
            const float y0 = 4.5f, y1 = 4.7f;
            var t = KitSlot.Trim;
            m.Box(V(-2, y0, -0.25f), V(2, y1, -0.05f), t, BulkTile);
            m.Box(V(-2, y0 + 0.05f, 1.1f), V(2, y1, 1.25f), t, BulkTile);
            foreach (float x in new[] { -2f, -0.06f, 1.88f })
                m.Box(V(x, y0, -0.05f), V(x + 0.12f, y1, 1.1f), t, BulkTile);
            m.Box(V(-2, y1 - 0.04f, 0.55f), V(2, y1, 0.6f), t, BulkTile);                    // mullion
        }

        /// <summary>4 m box girder along X (pivot centre, top at y=0, hangs 0.6 m) for the crane bay; scale X to span.</summary>
        private static void GantryBeam(KitMesh m)
        {
            var t = KitSlot.Trim;
            m.Box(V(-2, -0.08f, -0.2f), V(2, 0, 0.2f), t, BulkTile);
            m.Box(V(-2, -0.6f, -0.2f), V(2, -0.52f, 0.2f), t, BulkTile);
            m.Box(V(-2, -0.52f, -0.04f), V(2, -0.08f, 0.04f), t, BulkTile);
            for (float x = -1.75f; x < 2; x += 0.5f)                                         // web stiffeners
                m.Box(V(x - 0.02f, -0.52f, -0.18f), V(x + 0.02f, -0.08f, 0.18f), t, BulkTile);
        }

        /// <summary>Crane trolley + cables + hook block; pivot = top centre (sits under the gantry beam), hangs 2.6 m.</summary>
        private static void CraneHoist(KitMesh m)
        {
            var t = KitSlot.Trim;
            m.Box(V(-0.4f, -0.4f, -0.3f), V(0.4f, 0, 0.3f), t, BulkTile);                    // trolley
            m.Box(V(-0.15f, -0.48f, -0.1f), V(0.15f, -0.4f, 0.1f), KitSlot.Neon, BulkTile);   // amber warning lamp
            foreach (float x in new[] { -0.08f, 0.08f }) m.Box(V(x - 0.015f, -2.1f, -0.015f), V(x + 0.015f, -0.48f, 0.015f), t, BulkTile);
            m.Box(V(-0.25f, -2.4f, -0.15f), V(0.25f, -2.1f, 0.15f), t, BulkTile);           // block
            m.Box(V(-0.04f, -2.6f, -0.04f), V(0.04f, -2.4f, 0.04f), t, BulkTile);           // hook shank
            m.Box(V(-0.04f, -2.6f, -0.04f), V(0.14f, -2.54f, 0.04f), t, BulkTile);          // hook lip
        }

        /// <summary>Amber sodium cage lamp; pivot = wall-mount back centre, light along +Z (LightMount at 0.45 m).</summary>
        private static void SodiumCage(KitMesh m)
        {
            var t = KitSlot.Trim;
            m.Box(V(-0.12f, -0.2f, 0), V(0.12f, 0.2f, 0.03f), t, BulkTile);                   // back plate
            m.Box(V(-0.03f, -0.03f, 0.03f), V(0.03f, 0.03f, 0.1f), t, BulkTile);              // arm
            m.Box(V(-0.11f, 0.15f, 0.09f), V(0.11f, 0.18f, 0.31f), t, BulkTile);              // caps
            m.Box(V(-0.11f, -0.18f, 0.09f), V(0.11f, -0.15f, 0.31f), t, BulkTile);
            foreach (float x in new[] { -0.1f, 0.1f })
                foreach (float z in new[] { 0.1f, 0.3f })
                    m.Box(V(x - 0.01f, -0.15f, z - 0.01f), V(x + 0.01f, 0.15f, z + 0.01f), t, BulkTile);
            m.Xf = Matrix4x4.Translate(V(0, 0, 0.2f));
            m.Prism(Chamfered(0.06f, 0.02f), -0.15f, 0.15f, KitSlot.Neon, 1);                  // the sodium tube
            m.Xf = Matrix4x4.identity;
        }

        /// <summary>1.2 m wall vent: housing frame, amber furnace glow behind, 5 pitched blades on a hub. Pivot = back centre.</summary>
        private static void VentFan(KitMesh m)
        {
            var t = KitSlot.Trim;
            m.Box(V(-0.5f, -0.5f, 0), V(0.5f, 0.5f, 0.02f), KitSlot.Neon, BulkTile);
            m.Box(V(-0.6f, -0.6f, 0), V(0.6f, -0.5f, 0.25f), t, BulkTile);
            m.Box(V(-0.6f, 0.5f, 0), V(0.6f, 0.6f, 0.25f), t, BulkTile);
            m.Box(V(-0.6f, -0.5f, 0), V(-0.5f, 0.5f, 0.25f), t, BulkTile);
            m.Box(V(0.5f, -0.5f, 0), V(0.6f, 0.5f, 0.25f), t, BulkTile);
            m.Box(V(-0.5f, -0.02f, 0.2f), V(0.5f, 0.02f, 0.23f), t, BulkTile);              // guard bars
            m.Box(V(-0.02f, -0.5f, 0.2f), V(0.02f, 0.5f, 0.23f), t, BulkTile);
            m.Xf = Matrix4x4.TRS(V(0, 0, 0.02f), Quaternion.Euler(90, 0, 0), Vector3.one);
            m.Prism(Chamfered(0.08f, 0.025f), 0, 0.15f, t, 1);                                // hub (along +Z)
            for (int i = 0; i < 5; i++)
            {
                m.Xf = Matrix4x4.TRS(V(0, 0, 0.1f), Quaternion.Euler(0, 0, 72 * i) * Quaternion.Euler(0, 25, 0), Vector3.one);
                m.Box(V(-0.07f, 0.06f, -0.008f), V(0.07f, 0.46f, 0.008f), t, BulkTile);
            }
            m.Xf = Matrix4x4.identity;
        }

        /// <summary>Hero placeholder until the AI mesh lands: a kneeling block mech, 2.5 m tall, one material (Trim).</summary>
        private static void BrokenMechPlaceholder(KitMesh m)
        {
            var t = KitSlot.Trim;
            m.Box(V(-0.5f, 0.9f, -0.35f), V(0.5f, 1.3f, 0.35f), t, BulkTile);                 // hips
            m.Box(V(0.2f, 0.6f, 0), V(0.6f, 1f, 0.8f), t, BulkTile);                          // right thigh (forward)
            m.Box(V(0.22f, 0.1f, 0.6f), V(0.58f, 0.7f, 1f), t, BulkTile);                     // right shin
            m.Box(V(0.2f, 0, 0.6f), V(0.6f, 0.15f, 1.3f), t, BulkTile);                       // right foot
            m.Box(V(-0.6f, 0.25f, -0.1f), V(-0.2f, 1f, 0.3f), t, BulkTile);                   // left thigh (down)
            m.Box(V(-0.62f, 0, 0), V(-0.18f, 0.3f, 0.4f), t, BulkTile);                       // left knee on the floor
            m.Box(V(-0.58f, 0, -1f), V(-0.22f, 0.28f, 0), t, BulkTile);                       // left shin back along the floor
            m.Box(V(-0.7f, 1.3f, -0.5f), V(0.7f, 2.1f, 0.4f), t, BulkTile);                   // torso
            m.Box(V(-0.25f, 2.1f, -0.1f), V(0.25f, 2.5f, 0.35f), t, BulkTile);                // head
            m.Box(V(-1.05f, 1.8f, -0.4f), V(-0.7f, 2.2f, 0.3f), t, BulkTile);                 // shoulders
            m.Box(V(0.7f, 1.8f, -0.4f), V(1.05f, 2.2f, 0.3f), t, BulkTile);
            m.Box(V(-1f, 0.25f, -0.2f), V(-0.75f, 1.8f, 0.1f), t, BulkTile);                  // left arm, hanging
            m.Box(V(-1.05f, 0, -0.25f), V(-0.7f, 0.25f, 0.25f), t, BulkTile);                 // left fist on the floor
            m.Box(V(0.75f, 1.1f, -0.2f), V(1f, 1.8f, 0.1f), t, BulkTile);                     // right upper arm
            m.Box(V(0.7f, 0.9f, 0), V(1f, 1.15f, 0.9f), t, BulkTile);                         // right forearm on the knee
        }

        // ---------------- Garden pieces ----------------

        private static readonly Vector2 LatticeTile = new Vector2(2, 2);

        /// <summary>2 x 2 m tile of slightly uneven stone slabs (tops 0 / -1.5 / -3 cm) on a base, three seeded patterns.</summary>
        private static void Slabs(KitMesh m, int pattern)
        {
            var rng = new System.Random(17 + pattern);
            m.Box(V(-1, -0.1f, -1), V(1, -0.04f, 1), KitSlot.Floor, FloorTile);
            float[] cuts = pattern == 0 ? new[] { -1f, 0f, 1f } : pattern == 1 ? new[] { -1f, -0.3f, 0.45f, 1f } : new[] { -1f, 0.2f, 1f };
            for (int i = 0; i + 1 < cuts.Length; i++)
            {
                // each row splits into two slabs at a seeded x; 1 cm joints; the first slab stays at full height (bounds)
                float x = -0.4f + 0.8f * (float)rng.NextDouble();
                foreach (var (x0, x1) in new[] { (-1f, x), (x, 1f) })
                {
                    float top = i == 0 && x0 < -0.99f ? 0 : -0.015f * rng.Next(3);
                    m.Box(V(x0 + 0.01f, -0.04f, cuts[i] + 0.01f), V(x1 - 0.01f, top, cuts[i + 1] - 0.01f), KitSlot.Floor, FloorTile,
                        new Vector2(pattern * 0.33f, i * 0.5f));
                }
            }
        }

        /// <summary>Hanging vine card (foliage slot): double-sided pane from <paramref name="top"/> down
        /// <paramref name="len"/> m, vertex colour R = sway weight 0 at the top -> 1 at the tip.</summary>
        private static void Vine(KitMesh m, Vector3 top, float width, float len)
        {
            m.SwayWeight = p => Mathf.InverseLerp(top.y, top.y - len, p.y);
            m.Pane(V(top.x - width / 2, top.y - len, top.z), V(top.x + width / 2, top.y, top.z), KitSlot.Decal, VineUV);
            m.SwayWeight = null;
        }

        /// <summary>4 x 4.5 m overgrown lattice wall, z in [-0.25, 0.25], face +Z: lattice panel (trim), stone plinth /
        /// cornice / posts (floor stone), vine cards hanging from the cornice.</summary>
        private static void LatticeWall(KitMesh m, bool door)
        {
            const float W = 2, H = 4.5f, Z0 = -0.25f, ZB = -0.05f;
            var t = KitSlot.Trim; var st = KitSlot.Floor;
            if (door)
            {
                m.Box(V(-W, 0, Z0), V(-1, H, ZB), t, LatticeTile);
                m.Box(V(1, 0, Z0), V(W, H, ZB), t, LatticeTile);
                m.Box(V(-1, 3, Z0), V(1, H, ZB), t, LatticeTile);
                m.Box(V(-1.2f, 0, ZB), V(-1, 3.2f, 0.15f), st, FloorTile);                  // stone jambs + lintel
                m.Box(V(1, 0, ZB), V(1.2f, 3.2f, 0.15f), st, FloorTile);
                m.Box(V(-1.2f, 3, ZB), V(1.2f, 3.25f, 0.15f), st, FloorTile);
            }
            else
            {
                m.Box(V(-W, 0, Z0), V(W, H, ZB), t, LatticeTile);
                m.Box(V(-W, 0, ZB), V(W, 0.3f, 0.1f), st, FloorTile);                       // plinth
                m.Box(V(-0.075f, 0.3f, ZB), V(0.075f, 4.1f, 0.12f), st, FloorTile);        // centre post
            }
            m.Box(V(-W, 4.1f, Z0), V(W, H, 0.25f), st, FloorTile);                          // cornice
            foreach (float a in new[] { -2f, 1.85f })                                         // edge posts
                m.Box(V(a, 0, ZB), V(a + 0.15f, 4.1f, 0.12f), st, FloorTile);
            if (door) { Vine(m, V(-1.55f, 4.1f, 0.2f), 0.5f, 2.2f); Vine(m, V(1.5f, 4.1f, 0.2f), 0.45f, 1.6f); }
            else { Vine(m, V(-1.2f, 4.1f, 0.2f), 0.5f, 2.3f); Vine(m, V(0.5f, 4.1f, 0.2f), 0.45f, 1.5f); Vine(m, V(1.3f, 4.1f, 0.2f), 0.5f, 2.6f); }
        }

        /// <summary>Trellis arch, 2 m wide x 4.2 m, depth 0.3 (stone feet 0.4): two posts + an 8-segment semicircle,
        /// two vine cards. Arena face local +Z; pivot floor-centre.</summary>
        private static void TrellisArch(KitMesh m)
        {
            var t = KitSlot.Trim;
            foreach (float s in new[] { -1f, 1f })
            {
                m.Box(V(s > 0 ? 0.84f : -1, 0, -0.2f), V(s > 0 ? 1 : -0.84f, 0.3f, 0.2f), KitSlot.Floor, FloorTile); // foot
                m.Box(V(s > 0 ? 0.86f : -0.98f, 0.3f, -0.15f), V(s > 0 ? 0.98f : -0.86f, 3.2f, 0.15f), t, LatticeTile);
                for (int k = 0; k < 5; k++)                                                   // lattice rungs on the post face
                    m.Box(V(s * 0.92f - 0.08f, 0.7f + k * 0.55f, 0.15f), V(s * 0.92f + 0.08f, 0.74f + k * 0.55f, 0.18f), t, LatticeTile);
            }
            const int Seg = 8; const float R = 0.92f;
            for (int i = 0; i < Seg; i++)
            {
                float a = Mathf.PI * (i + 0.5f) / Seg, len = 2 * R * Mathf.Sin(Mathf.PI / Seg / 2) + 0.02f;
                m.Xf = Matrix4x4.TRS(V(R * Mathf.Cos(a), 3.2f + R * Mathf.Sin(a), 0), Quaternion.Euler(0, 0, a * Mathf.Rad2Deg), Vector3.one);
                m.Box(V(-0.06f, -len / 2, -0.15f), V(0.06f, len / 2, 0.15f), t, LatticeTile);
            }
            m.Xf = Matrix4x4.identity;
            Vine(m, V(-0.45f, 3.95f, 0.17f), 0.4f, 1.6f);
            Vine(m, V(0.35f, 4.05f, -0.17f), 0.4f, 2f);
        }

        /// <summary>Glass-dome ribs on a wall top (y 4.5): stone ring beam + 3 ribs curving up 1.2 m over 1.5 m inward and
        /// a purlin at their tips. Ribs only (no glass, so no transparent pass); open sky between.</summary>
        private static void DomeRibs(KitMesh m)
        {
            m.Box(V(-2, 4.5f, -0.25f), V(2, 4.65f, 0.25f), KitSlot.Floor, FloorTile);
            const int Seg = 4;
            foreach (float x in new[] { -1.95f, 0f, 1.95f })
                for (int i = 0; i < Seg; i++)
                {
                    // quarter arc: z = 1.45 sin(a), y = 4.5 + 1.2 (1 - cos a), a in [0, 90] deg
                    float a0 = Mathf.PI / 2 * i / Seg, a1 = Mathf.PI / 2 * (i + 1) / Seg;
                    var p0 = V(x, 4.5f + 1.2f * (1 - Mathf.Cos(a0)), 1.45f * Mathf.Sin(a0) - 0.05f);
                    var p1 = V(x, 4.5f + 1.2f * (1 - Mathf.Cos(a1)), 1.45f * Mathf.Sin(a1) - 0.05f);
                    var d = p1 - p0;
                    m.Xf = Matrix4x4.TRS((p0 + p1) / 2, Quaternion.LookRotation(d), Vector3.one);
                    m.Box(V(-0.05f, -0.05f, -d.magnitude / 2 - 0.02f), V(0.05f, 0.05f, d.magnitude / 2), KitSlot.Trim, LatticeTile);
                }
            m.Xf = Matrix4x4.identity;
            m.Box(V(-2, 5.62f, 1.3f), V(2, 5.7f, 1.4f), KitSlot.Trim, LatticeTile);              // purlin
        }

        /// <summary>Green glow pod cluster on a stone bracket; pivot = wall-mount back centre, LightMount at 0.4 m.</summary>
        private static void PodCluster(KitMesh m)
        {
            m.Box(V(-0.12f, -0.15f, 0), V(0.12f, 0.15f, 0.05f), KitSlot.Floor, FloorTile);
            foreach (var (x, y, z, r, len) in new[] { (0f, -0.05f, 0.14f, 0.07f, 0.2f), (-0.1f, 0.05f, 0.12f, 0.05f, 0.16f),
                         (0.11f, 0f, 0.15f, 0.06f, 0.18f), (0.04f, 0.12f, 0.1f, 0.045f, 0.12f) })
            {
                m.Xf = Matrix4x4.Translate(V(x, y, z));
                m.Prism(Chamfered(r, r * 0.4f), -len / 2, len / 2, KitSlot.Neon, 1);
            }
            m.Xf = Matrix4x4.identity;
            Vine(m, V(-0.08f, 0.25f, 0.21f), 0.24f, 0.75f);
        }

        /// <summary>Three hanging vine cards (2.6 m max), pivot top centre.</summary>
        private static void VineCards(KitMesh m)
        {
            Vine(m, V(-0.3f, 0, -0.05f), 0.55f, 2.2f);
            Vine(m, V(0.05f, 0, 0.05f), 0.6f, 2.6f);
            Vine(m, V(0.3f, 0, 0f), 0.55f, 1.8f);
        }

        /// <summary>Hero placeholder until the AI mesh lands: low-poly tree, 3.5 m, one material (Trim: ivy lattice).
        /// Tapered octagonal trunk, 4 boughs, 5 octagonal canopy blobs.</summary>
        private static void BioTreePlaceholder(KitMesh m)
        {
            var t = KitSlot.Trim;
            float[] rs = { 0.3f, 0.22f, 0.17f, 0.13f };
            for (int i = 0; i < rs.Length; i++) m.Prism(Chamfered(rs[i], rs[i] * 0.4f), i * 0.55f, (i + 1) * 0.55f + 0.02f, t, 1);
            for (int i = 0; i < 4; i++)
            {
                m.Xf = Matrix4x4.TRS(V(0, 1.7f, 0), Quaternion.Euler(0, 90 * i + 20, 0) * Quaternion.Euler(50, 0, 0), Vector3.one);
                m.Box(V(-0.07f, 0, -0.07f), V(0.07f, 1.1f, 0.07f), t, LatticeTile);
            }
            foreach (var (x, y, z, r) in new[] { (0f, 2.6f, 0f, 0.9f), (0.75f, 2.35f, 0.3f, 0.65f), (-0.7f, 2.4f, -0.25f, 0.7f),
                         (0.2f, 2.3f, -0.75f, 0.6f), (-0.25f, 2.3f, 0.75f, 0.6f) })
            {
                m.Xf = Matrix4x4.Translate(V(x, 0, z));
                m.Prism(Chamfered(r, r * 0.4f), y - r * 0.5f, Mathf.Min(3.5f, y + r), t, 1.5f);
            }
            m.Xf = Matrix4x4.identity;
        }

        /// <summary>Lichen emission mask (floor): bright pale-green dots only, black elsewhere.</summary>
        private static Color LichenMask(Color c) =>
            c.g > 0.75f && c.g - c.b > 0.2f && c.g - c.r > 0.12f ? new Color(c.r, c.g, c.b, 1) : Color.black;

        /// <summary>Pod emission mask (trim): the saturated near-white green pods; leaves and lattice highlights stay black.</summary>
        private static Color PodMask(Color c) =>
            c.g > 0.92f && c.g - c.r > 0.1f && c.g - c.b > 0.1f ? new Color(c.r, c.g, c.b, 1) : Color.black;

        /// <summary>1024² Garden atlas: column 0 = the AI vine card (Textures/Garden_VineCard.png, its centre 256 px
        /// strip); columns 1-3 cycle petals, puddles and lichen patches. Colour in the atlas; alpha-clipped.</summary>
        private static byte[] GardenAtlasPng(int seed)
        {
            const int S = 1024, C = 256;
            var px = new Color32[S * S];
            var vine = new Texture2D(2, 2);
            vine.LoadImage(File.ReadAllBytes(GardenRoot + "/Textures/Garden_VineCard.png"));
            var vp = vine.GetPixels32();
            int vw = vine.width, vh = vine.height, vx0 = vw / 2 - C / 2;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < C; x++)
                    px[y * S + x] = vp[(y * vh / S) * vw + vx0 + x];
            UnityEngine.Object.DestroyImmediate(vine);

            var rng = new System.Random(seed);
            for (int cell = 0; cell < 16; cell++)
            {
                if (cell % 4 == 0) continue; // vine column
                int ox = (cell % 4) * C, oy = (cell / 4) * C, kind = cell % 3;
                int count = kind == 1 ? 2 : 14; // puddles: two big pools; petals / lichen: scattered blobs
                var blobs = new Vector3[count];
                for (int i = 0; i < count; i++)
                    blobs[i] = new Vector3(60 + rng.Next(136), 60 + rng.Next(136), kind == 0 ? 7 + rng.Next(6) : kind == 1 ? 30 + rng.Next(30) : 5 + rng.Next(14));
                for (int y = 16; y < C - 16; y++)
                for (int x = 16; x < C - 16; x++)
                {
                    Color32? c = null;
                    float n = Mathf.Sin(x * 0.11f + y * 0.07f) * 3;
                    foreach (var b in blobs)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), b) + n;
                        if (d < b.z)
                            c = kind == 0 ? (d < b.z * 0.35f ? new Color32(250, 230, 140, 255) : new Color32(235, 180, 205, 255)) // petals
                              : kind == 1 ? new Color32(38, 52, 50, 255)                                                          // puddle
                              : new Color32(120, 190, 110, 255);                                                                  // lichen
                    }
                    if (c.HasValue) px[(oy + y) * S + ox + x] = c.Value;
                }
            }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            var png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            return png;
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
                if (!kit.MaskedEmission) MakeEmissive(mat, mat.GetTexture("_BaseMap"), kit.EmissionTint);
                else if (slot == KitSlot.Trim || kit.FloorMask != null)
                    MakeEmissive(mat, ExtractTexture(mat.GetTexture("_BaseMap"), $"{kit.Root}/Textures/{kit.Name}_{slot}EmissionMask.png", true,
                        slot == KitSlot.Trim ? kit.TrimMask ?? LampMask : kit.FloorMask), kit.EmissionTint);
                else
                {
                    mat.DisableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
                mat.SetFloat("_Smoothness", kit.Smoothness);
                mat.SetFloat("_Metallic", kit.Metallic);
                EditorUtility.SetDirty(mat);
            }

            string texPath = $"{kit.Root}/Textures/{kit.Name}_DecalAtlas.png";
            File.WriteAllBytes(texPath, kit.DecalAtlas(kit.Name.Length * 7919));
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
            decal.SetColor("_BaseColor", kit.DecalBase);
            decal.SetFloat("_AlphaClip", 1);
            decal.SetFloat("_Cutoff", 0.5f);
            decal.EnableKeyword("_ALPHATEST_ON");
            decal.SetOverrideTag("RenderType", "TransparentCutout");
            decal.renderQueue = (int)RenderQueue.AlphaTest;
            decal.SetFloat("_Smoothness", 0.3f);
            if (kit.DecalColor.maxColorComponent > 0) MakeEmissive(decal, atlas, kit.DecalColor);
            EditorUtility.SetDirty(decal);
        }

        private static void MakeEmissive(Material mat, Texture map, Color hdr)
        {
            mat.SetTexture("_EmissionMap", map);
            mat.SetColor("_EmissionColor", hdr);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }

        /// <summary>Emission mask: keeps only the bright, saturated amber pixels (sodium lamp slits), black elsewhere.</summary>
        private static Color LampMask(Color c)
        {
            float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            // Lamp cores are near-white (255,254,228), their rims saturated amber; rust highlights stay below r 0.9 / lum 0.5.
            return c.r > 0.9f && lum > 0.5f && c.r >= c.g && c.g >= c.b && c.r - c.b > 0.1f ? new Color(c.r, c.g, c.b, 1) : Color.black;
        }

        /// <summary>1024x1024 Rust atlas, 4x4 cells cycling scorch marks, rust drips and hazard stripes
        /// (colour lives in the atlas; alpha-clipped, not emissive).</summary>
        private static byte[] RustDecalAtlasPng(int seed)
        {
            const int S = 1024, C = 256;
            var px = new Color32[S * S];
            var rng = new System.Random(seed);
            for (int cell = 0; cell < 16; cell++)
            {
                int ox = (cell % 4) * C, oy = (cell / 4) * C, kind = cell % 3;
                float phase = (float)rng.NextDouble() * 6.3f, lobes = 3 + rng.Next(3);
                var drips = new int[8];
                for (int i = 0; i < drips.Length; i++) drips[i] = 60 + rng.Next(170);
                for (int y = 16; y < C - 16; y++)
                for (int x = 16; x < C - 16; x++)
                {
                    float dx = x - C / 2f, dy = y - C / 2f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32? c = null;
                    if (kind == 0) // scorch: lobed dark blot with a brown rim
                    {
                        float edge = 80 + 22 * Mathf.Sin(Mathf.Atan2(dy, dx) * lobes + phase) + 6 * Mathf.Sin(x * 0.09f + y * 0.07f);
                        if (r < edge) c = r < edge - 14 ? new Color32(18, 15, 13, 255) : new Color32(70, 38, 20, 255);
                    }
                    else if (kind == 1) // rust drips: 8 streaks hanging from the top of the cell, tapering
                    {
                        int col = Mathf.Min((x - 16) / 28, 7), lx = (x - 16) % 28, fromTop = C - 16 - y;
                        float w = 9 * (1 - fromTop / (float)drips[col]);
                        if (fromTop < drips[col] && Mathf.Abs(lx - 14) < w) c = new Color32(125, 58, 22, 255);
                    }
                    else if (Mathf.Abs(dx) < 110 && Mathf.Abs(dy) < 40) // hazard stripes
                        c = ((x + y) / 22) % 2 == 0 ? new Color32(235, 165, 25, 255) : new Color32(22, 20, 18, 255);
                    if (c.HasValue) px[(oy + y) * S + ox + x] = c.Value;
                }
            }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            var png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            return png;
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
        private readonly List<Color> _col = new List<Color>();
        private bool _swayed;
        /// <summary>When set, new vertices get colour R = SwayWeight(local point) (vine cards); the mesh then carries colours.</summary>
        public Func<Vector3, float> SwayWeight;
        private readonly SortedDictionary<KitSlot, List<int>> _tris = new SortedDictionary<KitSlot, List<int>>();

        private int Vert(Vector3 p, Vector3 n, Vector2 uv)
        {
            _v.Add(Xf.MultiplyPoint3x4(p)); _n.Add(Xf.MultiplyVector(n).normalized); _uv.Add(uv);
            _col.Add(new Color(SwayWeight?.Invoke(p) ?? 0, 0, 0, 1));
            _swayed |= SwayWeight != null;
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
            if (_swayed) mesh.SetColors(_col);
            slots = new KitSlot[_tris.Count];
            _tris.Keys.CopyTo(slots, 0);
            mesh.subMeshCount = slots.Length;
            for (int i = 0; i < slots.Length; i++) mesh.SetTriangles(_tris[slots[i]], i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }
    }
}
