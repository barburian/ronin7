using System.Collections.Generic;
using System.IO;
using Ronin7.Combat;
using Ronin7.Ship;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// One-shot builder for every subject prefab the art pass needs (ship enemy, on-foot enemy,
    /// cockpit, hands, sword). Constructs each prefab from the flat-shaded faceted meshes in
    /// <see cref="LowPolyMeshes"/> assigned to the SamuraiToon_* materials authored as YAML next
    /// to it, then writes the prefab via PrefabUtility.SaveAsPrefabAsset. Silhouette comes from
    /// composing parts; facets + the toon shader + pixel detail maps carry the look.
    ///
    /// Run after pulling: Tools/Space Samurai/Art/Build All Art Prefabs.
    /// Then re-run the existing scene builders (Tools/Space Samurai/Build Phase X ...) so scenes
    /// pick up the new prefabs via ArtPrefabRegistry.
    /// </summary>
    public static partial class ArtPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Ronin7/Prefabs/Art";

        private const string EnemyShipPrefabPath = PrefabFolder + "/EnemyShip.prefab";
        private const string EnemyFootPrefabPath = PrefabFolder + "/EnemyFoot.prefab";
        private const string CockpitPrefabPath   = PrefabFolder + "/Cockpit.prefab";
        private const string HandLeftPrefabPath  = PrefabFolder + "/Hand_L.prefab";
        private const string HandRightPrefabPath = PrefabFolder + "/Hand_R.prefab";
        private const string SwordPrefabPath     = PrefabFolder + "/Sword_Katana.prefab";
        private const string AsteroidPrefabPath  = PrefabFolder + "/Asteroid.prefab";
        private const string WormholePrefabPath  = PrefabFolder + "/Wormhole.prefab";
        public const string DominionTrooperPrefabPath = PrefabFolder + "/DominionTrooper.prefab";
        public const string KesslerPrefabPath        = PrefabFolder + "/Kessler.prefab";
        public const string Ronin9PrefabPath         = PrefabFolder + "/Ronin9.prefab";
        public const string KhallHologramPrefabPath  = PrefabFolder + "/KhallHologram.prefab";

        // Public ship-hull contract. Collapsed to a SINGLE signature ship (ship-select removed); the
        // array shape is kept so ShipHullSelector/Galaxy builders that iterate it still work unchanged —
        // they now wire a 1-element array and always instantiate the one hull.
        public static readonly string[] ShipHullPrefabPaths = {
            "Assets/Ronin7/Prefabs/Art/ShipHull_Samurai.prefab",
        };
        public static readonly string[] ShipHullNames = { "SAMURAI" };

        private const string MatFolder            = "Assets/Ronin7/Art/Materials";
        private const string EnemyShipMatPath     = MatFolder + "/SamuraiToon_EnemyShip.mat";
        private const string EnemyFootMatPath     = MatFolder + "/SamuraiToon_EnemyFoot.mat";
        internal const string NpcBodyMatPath      = MatFolder + "/SamuraiToon_Npc.mat";
        private const string CockpitMatPath       = MatFolder + "/SamuraiToon_Cockpit.mat";
        private const string HandMatPath          = MatFolder + "/SamuraiToon_Hand.mat";
        private const string SwordMatPath         = MatFolder + "/SamuraiToon_Sword.mat";
        private const string AsteroidMatPath      = MatFolder + "/SamuraiToon_Asteroid.mat";
        private const string WormholeMatPath      = MatFolder + "/SamuraiToon_Wormhole.mat";
        private const string ShipHullMatPath      = MatFolder + "/SamuraiToon_ShipHull.mat";
        private const string OutlineMatPath       = MatFolder + "/SamuraiOutline.mat";
        private const string BoltMatPath          = MatFolder + "/SamuraiToon_Bolt.mat";
        private const string BoltPrefabPath       = PrefabFolder + "/Bolt.prefab";

        private const string PlanetPrefabPath     = PrefabFolder + "/Planet_VariantA.prefab";

        // Shared data assets — referenced so prefab Awake() doesn't warn about missing defs and
        // create a throwaway ScriptableObject instance. XRRigBuilder authors these on first scene
        // build; if they don't exist yet we silently skip the bake and Awake's fallback kicks in.
        private const string EnemyShipDefPath     = "Assets/Ronin7/Data/Interceptor.asset";

        [MenuItem("Tools/Space Samurai/Art/Build All Art Prefabs", priority = 50)]
        public static void BuildAll()
        {
            EnsureFolder(PrefabFolder);
            RestyleBaseMaterials();

            int built = 0;
            built += BuildEnemyShipPrefab() ? 1 : 0;
            built += BuildEnemyFootPrefab() ? 1 : 0;
            built += BuildCockpitPrefab() ? 1 : 0;
            built += BuildHandPrefab(isLeft: true) ? 1 : 0;
            built += BuildHandPrefab(isLeft: false) ? 1 : 0;
            built += BuildSwordPrefab() ? 1 : 0;
            built += BuildAsteroidPrefab() ? 1 : 0;
            built += BuildWormholePrefab() ? 1 : 0;
            built += BuildProjectilePrefab() ? 1 : 0;
            built += BuildPlanetPrefab() ? 1 : 0;

            var hullMat = EnsureShipHullMaterial();
            built += BuildShipHull_Samurai(hullMat) ? 1 : 0;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArtPrefabBuilder] Built {built}/11 art prefabs into {PrefabFolder}/. " +
                      "Re-run Tools/Space Samurai/Build Phase X Scene to bake into scenes.");
        }

        [MenuItem("Tools/Space Samurai/Art/Build All Art Prefabs (and rebuild scenes)", priority = 51)]
        public static void BuildAllAndRebuild()
        {
            BuildAll();
            Debug.Log("[ArtPrefabBuilder] Prefabs built. Now manually run each Tools/Space Samurai/Build Phase X Scene to apply.");
        }

        [MenuItem("Tools/Space Samurai/Art/Build EP01 Characters", priority = 53)]
        public static void BuildEp01Characters()
        {
            EnsureFolder(PrefabFolder);
            int built = 0;
            built += BuildDominionTrooper() ? 1 : 0;
            built += BuildKesslerNpc() ? 1 : 0;
            built += BuildRonin9() ? 1 : 0;
            built += BuildKhallHologram() ? 1 : 0;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArtPrefabBuilder] Built {built}/4 EP01 characters.");
        }

        // ---------------- Ship Enemy ----------------

        internal static bool BuildEnemyShipPrefab()
        {
            var mat = LoadMaterial(EnemyShipMatPath);
            if (mat == null) return false;

            var root = new GameObject("EnemyShip");
            try
            {
                root.AddComponent<Health>();

                // Hull: a wider, slightly tapered body. Single cube so a single BoxCollider
                // covers it for bolt hits (gameplay-critical: hit detection must match greybox).
                var body = MakeChild(root.transform, "Body", PrimitiveType.Cube, mat,
                    pos: Vector3.zero, scale: new Vector3(6f, 2.5f, 9f), keepCollider: true);
                var bodyRenderer = body.GetComponent<Renderer>();

                // Asymmetric kabuto-style nose plate (samurai motif: stretched forward beak).
                MakeChild(root.transform, "NoseBeak", PrimitiveType.Cube, mat,
                    pos: new Vector3(0f, 0.2f, 6f), scale: new Vector3(2f, 1.2f, 4.5f));
                // Kabuto "horns" — two angled wedges flanking the nose.
                var hornL = MakeChild(root.transform, "HornL", PrimitiveType.Cube, mat,
                    pos: new Vector3(-1.3f, 1.4f, 4.6f), scale: new Vector3(0.4f, 1.5f, 1.8f));
                hornL.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
                var hornR = MakeChild(root.transform, "HornR", PrimitiveType.Cube, mat,
                    pos: new Vector3(1.3f, 1.4f, 4.6f), scale: new Vector3(0.4f, 1.5f, 1.8f));
                hornR.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);

                // Wings — angular swept plates.
                var wingL = MakeChild(root.transform, "WingL", PrimitiveType.Cube, mat,
                    pos: new Vector3(-4.5f, 0f, -1f), scale: new Vector3(4f, 0.3f, 5f));
                wingL.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
                var wingR = MakeChild(root.transform, "WingR", PrimitiveType.Cube, mat,
                    pos: new Vector3(4.5f, 0f, -1f), scale: new Vector3(4f, 0.3f, 5f));
                wingR.transform.localRotation = Quaternion.Euler(0f, -18f, 0f);

                // Wing-tip "blade" verticals — readable silhouette.
                MakeChild(root.transform, "WingFinL", PrimitiveType.Cube, mat,
                    pos: new Vector3(-6.5f, 0.6f, -1.8f), scale: new Vector3(0.25f, 1.4f, 2.2f));
                MakeChild(root.transform, "WingFinR", PrimitiveType.Cube, mat,
                    pos: new Vector3(6.5f, 0.6f, -1.8f), scale: new Vector3(0.25f, 1.4f, 2.2f));

                // Cockpit bump (dark accent for read).
                MakeChild(root.transform, "Canopy", PrimitiveType.Sphere, mat,
                    pos: new Vector3(0f, 1.2f, 1.5f), scale: new Vector3(1.6f, 0.8f, 2f));

                // Engine glow blocks at the rear (just geometry — color comes from material).
                MakeChild(root.transform, "EngineL", PrimitiveType.Cube, mat,
                    pos: new Vector3(-1.6f, 0f, -4.6f), scale: new Vector3(1f, 1.2f, 1.2f));
                MakeChild(root.transform, "EngineR", PrimitiveType.Cube, mat,
                    pos: new Vector3(1.6f, 0f, -4.6f), scale: new Vector3(1f, 1.2f, 1.2f));

                // Muzzle marker for bolt-spawn position (nose tip).
                var muzzle = new GameObject("Muzzle");
                muzzle.transform.SetParent(root.transform, false);
                muzzle.transform.localPosition = new Vector3(0f, 0.2f, 8.5f);

                // Generous catch radius so glancing player bolts still register. Sits as a child
                // collider — IDamageable resolves via GetComponentInParent so it just routes hits to
                // the EnemyShip's Health like the body box does.
                var hitbox = new GameObject("Hitbox");
                hitbox.transform.SetParent(root.transform, false);
                var hitSphere = hitbox.AddComponent<SphereCollider>();
                hitSphere.radius = 7f;
                hitSphere.isTrigger = false;

                var ship = root.AddComponent<EnemyShip>();
                var so = new SerializedObject(ship);
                SetRef(so, "muzzle", muzzle.transform);
                SetRef(so, "bodyRenderer", bodyRenderer);
                // Bake the EnemyShipDefinition reference INTO the prefab so Awake() doesn't fire
                // its "no definition" warning and spin up a throwaway ScriptableObject before the
                // spawner's Configure() call lands.
                var shipDef = AssetDatabase.LoadAssetAtPath<EnemyShipDefinition>(EnemyShipDefPath);
                if (shipDef != null) SetRef(so, "definition", shipDef);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, EnemyShipPrefabPath);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ---------------- On-foot Enemy ----------------

        private static bool BuildEnemyFootPrefab()
        {
            var mat = LoadMaterial(EnemyFootMatPath);
            if (mat == null) return false;

            // The XRRigBuilder.BuildEnemy contract: prefab root must have a Renderer
            // (so GetComponent<Renderer>() on the instance returns the body).
            var root = new GameObject("EnemyFoot");
            try
            {
                // Body capsule on root so root.GetComponent<Renderer>() resolves.
                root.AddComponent<MeshFilter>().sharedMesh = LowPolyMeshes.Capsule8();
                root.AddComponent<MeshRenderer>().sharedMaterial = mat;
                root.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
                root.transform.localPosition = new Vector3(0f, 0.9f, 0f);

                // Body hurtbox so the player's blade can land a hit. BladeDamager.OnTriggerEnter
                // needs a collider to overlap and a Health in its parents; without this the enemy
                // is purely visual and takes no sword damage. (Parry works without it — the enemy
                // polls for the blade — and enemy-on-player damage is a direct ApplyDamage call.)
                // Default capsule collider, non-trigger, matches the greybox/TrainingDummy body.
                root.AddComponent<CapsuleCollider>();

                // Kabuto helmet (sphere + brim disc).
                MakeChild(root.transform, "Helmet", PrimitiveType.Sphere, mat,
                    pos: new Vector3(0f, 0.78f, 0f), scale: new Vector3(1.05f, 1.0f, 1.05f),
                    localRot: Quaternion.identity);
                MakeChild(root.transform, "HelmetBrim", PrimitiveType.Cylinder, mat,
                    pos: new Vector3(0f, 0.62f, 0.05f), scale: new Vector3(1.35f, 0.05f, 1.35f));

                // Sode — shoulder plates.
                var sodeL = MakeChild(root.transform, "SodeL", PrimitiveType.Cube, mat,
                    pos: new Vector3(-0.55f, 0.35f, 0f), scale: new Vector3(0.45f, 0.4f, 0.65f));
                sodeL.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
                var sodeR = MakeChild(root.transform, "SodeR", PrimitiveType.Cube, mat,
                    pos: new Vector3(0.55f, 0.35f, 0f), scale: new Vector3(0.45f, 0.4f, 0.65f));
                sodeR.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);

                // Dō — chest plate.
                MakeChild(root.transform, "DoChest", PrimitiveType.Cube, mat,
                    pos: new Vector3(0f, 0.15f, 0.32f), scale: new Vector3(0.7f, 0.7f, 0.25f));

                // Kusazuri — hanging skirt plates.
                MakeChild(root.transform, "KusazuriFront", PrimitiveType.Cube, mat,
                    pos: new Vector3(0f, -0.4f, 0.35f), scale: new Vector3(0.6f, 0.5f, 0.15f));
                MakeChild(root.transform, "KusazuriBack", PrimitiveType.Cube, mat,
                    pos: new Vector3(0f, -0.4f, -0.35f), scale: new Vector3(0.6f, 0.5f, 0.15f));

                // Right arm + held katana — the ArmR/Sword/Blade/BladeTip rig Enemy.cs drives.
                // Indigo hard-cube limbs, 8 cm thick, to match this enemy's blocky armor.
                AttachHeldKatanaRig(root.transform, mat,
                    limbColor: new Color(0.12f, 0.1f, 0.18f), roundedLimbs: false, limbThickness: 0.08f);

                PrefabUtility.SaveAsPrefabAsset(root, EnemyFootPrefabPath);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ---------------- Cockpit ----------------

        internal static bool BuildCockpitPrefab()
        {
            var mat = LoadMaterial(CockpitMatPath);
            if (mat == null) return false;

            // Sleek modern fighter: dark steel panels + cyan holographic readouts. Mirrors the greybox
            // layout in XRRigBuilder.BuildCockpit so seated head/hand pose targets keep lining up with
            // the console/canopy. The forward gun/sight box (x∈[-0.55,0.55], y∈[0.6,1.4], z>0.85) is
            // kept clear: the console front face sits AT z=0.85, holo screens tilt back to z<0.85, and
            // the canopy frame is slim and outboard/above the view.
            var steel     = NeonPalette.BaseSteel; // structural panels
            var darkSteel = NeonPalette.BaseDark;  // recessed/holo backing
            var cyan      = NeonPalette.Cyan;       // bright holo (HDR emission)
            var cyanDim   = NeonPalette.CyanDim;    // subtler accent glow

            var root = new GameObject("Cockpit");
            try
            {
                // Floor — smooth beveled steel pad (seen only in the open Phase7/preview view; the
                // galaxy cabins lay their own floor over it).
                MakeRoundedTinted(root.transform, "CockpitFloor", mat, darkSteel,
                    new Vector3(0f, 0f, 0f), new Vector3(1.6f, 0.1f, 1.6f));

                // Low curved console across the dash — beveled steel, front face hugging z=0.85.
                MakeRoundedTinted(root.transform, "Console", mat, steel,
                    new Vector3(0f, 0.72f, 0.74f), new Vector3(1.5f, 0.5f, 0.22f));

                // Cyan HUD glow line along the console's top front edge (sits at z<0.85, below the
                // forward bolt lane). The dashboard's signature holographic strip.
                MakeChildEmissive(root.transform, "HoloStrip", PrimitiveType.Cube, mat, darkSteel, cyan,
                    new Vector3(0f, 0.96f, 0.8f), new Vector3(1.0f, 0.02f, 0.06f));

                // Holographic instrument readouts replacing the brass dials: thin glowing screens
                // tilted up toward the seated player (they tilt BACK, so they stay z<0.85).
                MakeChildEmissive(root.transform, "HoloPanelL", PrimitiveType.Cube, mat, darkSteel, cyanDim,
                    new Vector3(-0.45f, 0.95f, 0.78f), new Vector3(0.28f, 0.16f, 0.015f),
                    Quaternion.Euler(35f, 0f, 0f));
                MakeChildEmissive(root.transform, "HoloNav", PrimitiveType.Cube, mat, darkSteel, cyan,
                    new Vector3(0f, 0.98f, 0.78f), new Vector3(0.34f, 0.2f, 0.015f),
                    Quaternion.Euler(30f, 0f, 0f));
                MakeChildEmissive(root.transform, "HoloPanelR", PrimitiveType.Cube, mat, darkSteel, cyanDim,
                    new Vector3(0.45f, 0.95f, 0.78f), new Vector3(0.28f, 0.16f, 0.015f),
                    Quaternion.Euler(35f, 0f, 0f));

                // Armrest consoles (replace the side rails) — beveled steel with a cyan edge strip,
                // well outboard of the sightline at |x|=0.72 where the hands rest.
                MakeRoundedTinted(root.transform, "ArmrestL", mat, steel,
                    new Vector3(-0.72f, 0.82f, 0.3f), new Vector3(0.16f, 0.14f, 1.0f));
                MakeRoundedTinted(root.transform, "ArmrestR", mat, steel,
                    new Vector3(0.72f, 0.82f, 0.3f), new Vector3(0.16f, 0.14f, 1.0f));
                MakeChildEmissive(root.transform, "ArmStripL", PrimitiveType.Cube, mat, darkSteel, cyanDim,
                    new Vector3(-0.72f, 0.9f, 0.3f), new Vector3(0.13f, 0.012f, 0.9f));
                MakeChildEmissive(root.transform, "ArmStripR", PrimitiveType.Cube, mat, darkSteel, cyanDim,
                    new Vector3(0.72f, 0.9f, 0.3f), new Vector3(0.13f, 0.012f, 0.9f));

                // Canopy frame (CanopyBrow/CanopyGlow + StrutL/R) intentionally omitted: it sat close and
                // central (z~0.6-0.8) in front of the player and obstructed the view. The cabin supplies
                // the real canopy in galaxy scenes, so the cockpit needs no near-field frame.

                PrefabUtility.SaveAsPrefabAsset(root, CockpitPrefabPath);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ---------------- Ship Hull Variants ----------------

        [MenuItem("Tools/Space Samurai/Art/Build Ship Hull Variants", priority = 52)]
        public static void BuildShipHullVariants()
        {
            EnsureFolder(PrefabFolder);
            var mat = EnsureShipHullMaterial();

            int built = BuildShipHull_Samurai(mat) ? 1 : 0;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArtPrefabBuilder] Built {built}/1 signature ship hull into {PrefabFolder}/.");
        }

        /// <summary>
        /// Loads (creating if missing) the shared gunmetal hull material. Created in code so a fresh
        /// checkout without the on-disk .mat still produces non-pink hulls. Per-part color comes from
        /// MakeChildTinted variants; this
        /// is just the base.
        /// </summary>
        private static Material EnsureShipHullMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(ShipHullMatPath);
            if (existing != null) return existing;

            var m = new Material(Shader.Find("Ronin7/SamuraiToon"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", NeonPalette.BaseSteel);
            if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 0.3f);
            if (m.HasProperty("_RimPower")) m.SetFloat("_RimPower", 4f);
            EnsureAssetFolder(MatFolder);
            AssetDatabase.CreateAsset(m, ShipHullMatPath);
            return AssetDatabase.LoadAssetAtPath<Material>(ShipHullMatPath);
        }

        /// <summary>
        /// Loads (creating if missing) the shared NON-HOSTILE character body material — the
        /// ally/civilian counterpart to SamuraiToon_EnemyFoot. Cloned from the EnemyFoot base so it
        /// inherits the same toon shader + pixel detail map, but its emissive seam/visor accent is
        /// driven CYAN (player/ally faction) instead of Dominion magenta (see
        /// BuildEp01ProceduralTextures, which assigns the cyan emission to this family). Batch NPCs
        /// (CharacterBatchBuilder) and the named ally prefabs (Kessler/Ronin9) tint from THIS base via
        /// TintInstance, so their per-character variants inherit the cyan glow rather than the magenta
        /// that the EnemyFoot family carries. Idempotent.
        /// </summary>
        internal static Material EnsureNpcMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(NpcBodyMatPath);
            if (existing != null) return existing;

            EnsureAssetFolder(MatFolder);
            var src = AssetDatabase.LoadAssetAtPath<Material>(EnemyFootMatPath);
            var m = src != null ? new Material(src) : new Material(Shader.Find("Ronin7/SamuraiToon"));
            m.name = "SamuraiToon_Npc";
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", NeonPalette.BaseDark);
            AssetDatabase.CreateAsset(m, NpcBodyMatPath);
            return AssetDatabase.LoadAssetAtPath<Material>(NpcBodyMatPath) ?? m;
        }

        /// <summary>
        /// Loads (creating if missing) the shared projectile bolt material. Idempotent: load if present,
        /// else create with SamuraiToon shader configured for self-luminosity via _ShadowThreshold = -1
        /// so the per-shot RendererTint colour reads at full brightness.
        /// </summary>
        private static Material EnsureBoltMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(BoltMatPath);
            if (existing != null) return existing;
            var m = new Material(Shader.Find("Ronin7/SamuraiToon"));
            m.name = "SamuraiToon_Bolt";
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);   // per-shot RendererTint overrides this
            if (m.HasProperty("_ShadowThreshold")) m.SetFloat("_ShadowThreshold", -1f); // self-luminous (whole surface in lit step)
            EnsureAssetFolder(MatFolder);
            AssetDatabase.CreateAsset(m, BoltMatPath);
            return AssetDatabase.LoadAssetAtPath<Material>(BoltMatPath) ?? m;
        }

        /// <summary>
        /// Techno-noir base-tint pass: drops the shared SamuraiToon family materials from their old
        /// mid-grey/white base colours into the cool-dark <see cref="NeonPalette"/> range so emissive
        /// accents and bloom read against shadow. Idempotent — it re-sets the colour every run (unlike
        /// <see cref="EnsureShipHullMaterial"/>, which only initialises on first creation). The
        /// TintInstance variant system and the 262 batch NPCs pick up the darker bodies (and any
        /// base-material-direct parts) on the next rebuild.
        /// </summary>
        private static void RestyleBaseMaterials()
        {
            void Darken(string path, Color baseColor)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || !m.HasProperty("_BaseColor")) return;
                m.SetColor("_BaseColor", baseColor);
                EditorUtility.SetDirty(m);
            }

            Darken(EnemyFootMatPath, NeonPalette.BaseDark);   // Dominion foot enemies (magenta enemy glow)
            EnsureNpcMaterial();                              // ally/civilian body family (cyan trim)
            Darken(NpcBodyMatPath,   NeonPalette.BaseDark);   // ally/civilian NPC bodies
            Darken(EnemyShipMatPath, NeonPalette.BaseDark);   // Dominion interceptor hull
            Darken(CockpitMatPath,   NeonPalette.BaseDark);   // cockpit shell behind the lit dials
            Darken(ShipHullMatPath,  NeonPalette.BaseSteel);  // player ship hull (slightly lighter steel)

            // Player hands are a hero prop: keep them readable (steel, not near-black) and give them a
            // dim cyan rim so they edge-glow to match the player's plasma faction colour. Selective —
            // not a project-wide rim bump (rim is an extra cost on Quest).
            var hand = AssetDatabase.LoadAssetAtPath<Material>(HandMatPath);
            if (hand != null)
            {
                if (hand.HasProperty("_BaseColor")) hand.SetColor("_BaseColor", NeonPalette.BaseSteel);
                if (hand.HasProperty("_RimColor")) hand.SetColor("_RimColor", new Color(0.3f, 0.8f, 1f, 1f));
                if (hand.HasProperty("_RimStrength")) hand.SetFloat("_RimStrength", 0.45f);
                EditorUtility.SetDirty(hand);
            }
        }

        // Spatial contract (cockpit-local frame; hull is a child of Cockpit at local identity):
        // +Z = forward, floor y=0, head ≈ y1.6, canopy top ≈ y1.7. Twin guns fire +Z from
        // (±0.5, 1.0, 0.8); crosshair at (0,1,8.8). The forward sightline/gun-bolt box
        // x∈[-0.55,0.55], y∈[0.6,1.4], z>0.85 MUST stay empty. Hull mass goes on the SIDES
        // (|x|≳0.85), BELOW (y<0.15), and REAR (z<-0.2). Windshield is a peripheral A-frame only.

        private static bool BuildShipHull_Samurai(Material mat)
        {
            if (mat == null) return false;

            // Single signature ship — a sleek modern fighter. Dark steel body with cyan engine/nav
            // glow; mass kept on the sides/belly/rear so the forward sight box stays clear.
            var steel     = NeonPalette.BaseSteel;
            var darkSteel = NeonPalette.BaseDark;
            var cyan      = NeonPalette.Cyan;
            var cyanDim   = NeonPalette.CyanDim;

            var root = new GameObject("ShipHull_Samurai");
            try
            {
                // Blended belly fuselage under the player — a smooth beveled wedge widening to the rear.
                MakeRoundedTinted(root.transform, "BellyFront", mat, steel,
                    new Vector3(0f, 0.0f, 0.45f), new Vector3(0.9f, 0.18f, 1.4f));
                MakeRoundedTinted(root.transform, "BellyRear", mat, steel,
                    new Vector3(0f, 0.0f, -0.55f), new Vector3(1.4f, 0.2f, 1.2f));

                // Leading-edge side fairings sweeping fore-aft, well outboard of the sightline, each
                // capped with a cyan glow line.
                MakeRoundedTinted(root.transform, "FairingL", mat, steel,
                    new Vector3(-0.95f, 0.65f, 0.1f), new Vector3(0.22f, 0.45f, 1.7f));
                MakeRoundedTinted(root.transform, "FairingR", mat, steel,
                    new Vector3(0.95f, 0.65f, 0.1f), new Vector3(0.22f, 0.45f, 1.7f));
                MakeChildEmissive(root.transform, "FairingGlowL", PrimitiveType.Cube, mat, darkSteel, cyanDim,
                    new Vector3(-0.95f, 0.9f, 0.3f), new Vector3(0.12f, 0.02f, 1.0f));
                MakeChildEmissive(root.transform, "FairingGlowR", PrimitiveType.Cube, mat, darkSteel, cyanDim,
                    new Vector3(0.95f, 0.9f, 0.3f), new Vector3(0.12f, 0.02f, 1.0f));

                // Low swept wings, outboard and aft, with a cyan nav light at each tip.
                var wingL = MakeRoundedTinted(root.transform, "WingL", mat, steel,
                    new Vector3(-1.25f, 0.45f, -0.2f), new Vector3(1.0f, 0.06f, 0.9f));
                wingL.transform.localRotation = Quaternion.Euler(0f, 18f, 8f);
                var wingR = MakeRoundedTinted(root.transform, "WingR", mat, steel,
                    new Vector3(1.25f, 0.45f, -0.2f), new Vector3(1.0f, 0.06f, 0.9f));
                wingR.transform.localRotation = Quaternion.Euler(0f, -18f, -8f);
                MakeChildEmissive(root.transform, "NavLightL", PrimitiveType.Cube, mat, darkSteel, cyan,
                    new Vector3(-1.7f, 0.5f, -0.35f), new Vector3(0.08f, 0.05f, 0.18f));
                MakeChildEmissive(root.transform, "NavLightR", PrimitiveType.Cube, mat, darkSteel, cyan,
                    new Vector3(1.7f, 0.5f, -0.35f), new Vector3(0.08f, 0.05f, 0.18f));

                // Twin engine nacelles at the rear, each with a bright cyan thruster disc.
                MakeRoundedTinted(root.transform, "NacelleL", mat, steel,
                    new Vector3(-0.5f, 0.45f, -1.0f), new Vector3(0.3f, 0.3f, 0.7f));
                MakeRoundedTinted(root.transform, "NacelleR", mat, steel,
                    new Vector3(0.5f, 0.45f, -1.0f), new Vector3(0.3f, 0.3f, 0.7f));
                MakeChildEmissive(root.transform, "ThrusterL", PrimitiveType.Cylinder, mat, darkSteel, cyan,
                    new Vector3(-0.5f, 0.45f, -1.35f), new Vector3(0.26f, 0.04f, 0.26f),
                    Quaternion.Euler(90f, 0f, 0f));
                MakeChildEmissive(root.transform, "ThrusterR", PrimitiveType.Cylinder, mat, darkSteel, cyan,
                    new Vector3(0.5f, 0.45f, -1.35f), new Vector3(0.26f, 0.04f, 0.26f),
                    Quaternion.Euler(90f, 0f, 0f));

                // Dorsal fin behind the player with a cyan tip strip.
                MakeRoundedTinted(root.transform, "DorsalFin", mat, steel,
                    new Vector3(0f, 0.8f, -0.9f), new Vector3(0.06f, 0.7f, 0.6f));
                MakeChildEmissive(root.transform, "FinGlow", PrimitiveType.Cube, mat, darkSteel, cyanDim,
                    new Vector3(0f, 1.15f, -0.9f), new Vector3(0.05f, 0.02f, 0.5f));

                // Windshield A-frame (WindStrutL/R + WindBrow) intentionally omitted: it sat close and
                // central (x±0.34, z~0.75) in front of the player and obstructed the view. The cabin
                // supplies the real canopy in galaxy scenes, so the hull needs no near-field frame.

                PrefabUtility.SaveAsPrefabAsset(root, ShipHullPrefabPaths[0]);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ---------------- Hands ----------------

        internal static bool BuildHandPrefab(bool isLeft)
        {
            var mat = LoadMaterial(HandMatPath);
            if (mat == null) return false;

            string path = isLeft ? HandLeftPrefabPath : HandRightPrefabPath;
            // The greybox factory in XRRigBuilder returns a wrapper GO with a sphere palm and a
            // forward cube. Prefab root mirrors that — wrapper named "Visual".
            var root = new GameObject(isLeft ? "Hand_L" : "Hand_R");
            try
            {
                // Tekko glove cloth wrap — slightly off-white wrap with darker accent.
                var wrap = new Color(0.95f, 0.92f, 0.88f);
                var skin = new Color(0.93f, 0.78f, 0.62f);
                var cord = new Color(0.55f, 0.18f, 0.18f);

                // Palm — flattened sphere reads more like a hand than a sphere.
                MakeChildTinted(root.transform, "Palm", PrimitiveType.Sphere, mat, wrap,
                    new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.045f, 0.085f));

                // Knuckle ridge — small rounded ridge on top.
                MakeRoundedTinted(root.transform, "KnuckleRidge", mat, wrap,
                    new Vector3(0f, 0.022f, 0.025f), new Vector3(0.065f, 0.012f, 0.045f));

                // Four finger stubs (skin tone at tip, wrap on base — single segment each).
                float baseZ = 0.055f;
                for (int i = 0; i < 4; i++)
                {
                    float x = (-1.5f + i) * 0.018f; // spread across knuckles
                    MakeRoundedTinted(root.transform, $"FingerWrap_{i}", mat, wrap,
                        new Vector3(x, 0.005f, baseZ), new Vector3(0.015f, 0.018f, 0.025f));
                    MakeRoundedTinted(root.transform, $"FingerTip_{i}", mat, skin,
                        new Vector3(x, 0.005f, baseZ + 0.025f), new Vector3(0.014f, 0.016f, 0.025f));
                }
                // Thumb (mirrored by hand) — offset to the outer side of the palm.
                float thumbX = isLeft ? 0.045f : -0.045f;
                float thumbRot = isLeft ? -35f : 35f;
                var thumb = MakeRoundedTinted(root.transform, "Thumb", mat, wrap,
                    new Vector3(thumbX, 0.005f, 0.02f), new Vector3(0.018f, 0.018f, 0.045f));
                thumb.transform.localRotation = Quaternion.Euler(0f, thumbRot, 0f);
                var thumbTip = MakeRoundedTinted(root.transform, "ThumbTip", mat, skin,
                    new Vector3(thumbX * 1.4f, 0.005f, 0.04f), new Vector3(0.016f, 0.016f, 0.022f));
                thumbTip.transform.localRotation = Quaternion.Euler(0f, thumbRot, 0f);

                // Cord-lacing accent on the wrist.
                MakeRoundedTinted(root.transform, "WristCord", mat, cord,
                    new Vector3(0f, 0f, -0.02f), new Vector3(0.08f, 0.05f, 0.012f));

                AddOutline(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ---------------- Sword ----------------

        internal static bool BuildSwordPrefab()
        {
            var mat = LoadMaterial(SwordMatPath);
            if (mat == null) return false;

            var bladeColor = new Color(0.92f, 0.95f, 1f);
            var tsubaColor = new Color(0.78f, 0.55f, 0.18f); // brass
            var itoColor   = new Color(0.12f, 0.1f, 0.18f);  // indigo wrap
            var sameColor  = new Color(0.95f, 0.92f, 0.88f); // pommel cap

            // Wrapper matches XRRigBuilder.BuildSword's swordGreybox factory: a parent GO with
            // Handle, Guard (tsuba), and Blade children. The Blade child carries the trigger
            // BoxCollider + BladeDamager so gameplay-critical hitbox geometry is preserved.
            var root = new GameObject("SwordVisual");
            try
            {
                // Tsuka (handle, wrapped indigo) — same local pos/scale as greybox Handle, rounded.
                MakeRoundedTinted(root.transform, "Handle", mat, itoColor,
                    new Vector3(0f, 0f, 0.06f), new Vector3(0.035f, 0.035f, 0.12f));

                // Tsuba (guard, brass) — same local pos as greybox Guard; cylinder on its side
                // reads as a disc rather than the original flat slab.
                var tsuba = MakeChildTinted(root.transform, "Guard", PrimitiveType.Cylinder, mat, tsubaColor,
                    new Vector3(0f, 0f, 0.13f), new Vector3(0.07f, 0.012f, 0.07f));
                tsuba.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                // Kashira (pommel cap) — small sphere at the butt of the handle.
                MakeChildTinted(root.transform, "Kashira", PrimitiveType.Sphere, mat, sameColor,
                    new Vector3(0f, 0f, -0.01f), new Vector3(0.04f, 0.04f, 0.04f));

                // Blade — gameplay-critical hitbox. Local pos/scale match the greybox factory so
                // BladeDamager + trigger BoxCollider sweep the same volume.
                var blade = MakeChildTinted(root.transform, "Blade", PrimitiveType.Cube, mat, bladeColor,
                    new Vector3(0f, 0f, 0.45f), new Vector3(0.04f, 0.012f, 0.62f));
                // Add the trigger BoxCollider (auto-fits the unit cube bounds) and BladeDamager
                // so the prefab is gameplay-ready without code outside this builder.
                var bladeBox = blade.GetComponent<BoxCollider>();
                if (bladeBox == null) bladeBox = blade.AddComponent<BoxCollider>();
                bladeBox.isTrigger = true;
                if (blade.GetComponent<BladeDamager>() == null) blade.AddComponent<BladeDamager>();

                // Plasma look: emissive cyan blade + neon tip trail + tier-gated light (player weapon).
                ApplyPlasmaBlade(blade, PlasmaPlayer);

                AddOutline(root);
                PrefabUtility.SaveAsPrefabAsset(root, SwordPrefabPath);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ---------------- Asteroid (Phase 4 hazard) ----------------

        /// <summary>
        /// A craggy rock built from a few overlapping primitives (silhouette comes from composition,
        /// not mesh detail). The prefab ROOT carries the only collider: a solid (non-trigger)
        /// SphereCollider sized to roughly enclose the cluster, because XRRigBuilder.BuildAsteroid
        /// adds Health + Asteroid to this root and Asteroid.WorldRadius derives from this collider's
        /// radius × scale. Child visual primitives have their auto-colliders stripped so no stray
        /// hittable child colliders exist. No Health/Asteroid here — BuildAsteroid adds those.
        /// </summary>
        private static bool BuildAsteroidPrefab()
        {
            var mat = LoadMaterial(AsteroidMatPath);
            if (mat == null) return false;

            // Empty root so its SphereCollider is the single, predictable hit volume (a primitive
            // root would carry its own mesh + auto-collider and skew the silhouette/scale chain).
            var root = new GameObject("Asteroid");
            try
            {
                // Main mass + overlapping lumps give an irregular craggy read. All children are
                // collider-stripped (MakeChild keepCollider:false) — the root collider is the only one.
                MakeChild(root.transform, "Mass", PrimitiveType.Sphere, mat,
                    pos: Vector3.zero, scale: new Vector3(1.0f, 0.92f, 1.05f));
                MakeChild(root.transform, "LumpA", PrimitiveType.Sphere, mat,
                    pos: new Vector3(0.42f, 0.28f, -0.18f), scale: new Vector3(0.6f, 0.6f, 0.58f));
                MakeChild(root.transform, "LumpB", PrimitiveType.Sphere, mat,
                    pos: new Vector3(-0.38f, -0.22f, 0.3f), scale: new Vector3(0.55f, 0.5f, 0.62f));
                MakeChild(root.transform, "LumpC", PrimitiveType.Sphere, mat,
                    pos: new Vector3(0.05f, -0.4f, -0.35f), scale: new Vector3(0.5f, 0.48f, 0.5f));
                // A couple of angular crags (cubes) break the round silhouette.
                var cragA = MakeChild(root.transform, "CragA", PrimitiveType.Cube, mat,
                    pos: new Vector3(-0.3f, 0.4f, -0.3f), scale: new Vector3(0.5f, 0.45f, 0.5f));
                cragA.transform.localRotation = Quaternion.Euler(25f, 40f, 15f);
                var cragB = MakeChild(root.transform, "CragB", PrimitiveType.Cube, mat,
                    pos: new Vector3(0.36f, -0.1f, 0.4f), scale: new Vector3(0.42f, 0.42f, 0.42f));
                cragB.transform.localRotation = Quaternion.Euler(-18f, 30f, -22f);

                // Root collider: radius 0.62 (in prefab-local space) encloses the ~unit cluster
                // including the lumps that poke out to ~0.5..0.72. BuildAsteroid scales the whole
                // instance, so WorldRadius = 0.62 × instanceScale tracks the rendered size.
                var col = root.AddComponent<SphereCollider>();
                col.center = Vector3.zero;
                col.radius = 0.62f;
                col.isTrigger = false;

                PrefabUtility.SaveAsPrefabAsset(root, AsteroidPrefabPath);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ---------------- Wormhole (Phase 4 hazard/portal) ----------------

        /// <summary>
        /// A glowing portal: a ring of small cubes orbiting the center plus an inner disc, using the
        /// bright SamuraiToon_Wormhole material (its _ShadowThreshold is pushed to -1 so the whole
        /// surface stays in the lit step and reads as self-luminous). SamuraiToon is stereo-safe
        /// (carries the single-pass-instanced macros) — no custom shader authored. No collider:
        /// Wormhole proximity is distance-based, not physics. XRRigBuilder.BuildWormhole instantiates
        /// this as a CHILD of the Wormhole root and tints it per-end (blue / orange).
        /// </summary>
        private static bool BuildWormholePrefab()
        {
            var mat = LoadMaterial(WormholeMatPath);
            if (mat == null) return false;

            // Sized to read like the greybox flattened sphere (~40 unit diameter, thin on Z).
            // Built at radius ~20 so the per-end tint and scale match the old placeholder footprint.
            const float ringRadius = 18f;
            const int segments = 16;

            var root = new GameObject("Wormhole");
            try
            {
                // Ring of cubes approximating a torus — each oriented to face outward along the ring.
                for (int i = 0; i < segments; i++)
                {
                    float ang = (i / (float)segments) * Mathf.PI * 2f;
                    var pos = new Vector3(Mathf.Cos(ang) * ringRadius, Mathf.Sin(ang) * ringRadius, 0f);
                    var seg = MakeChild(root.transform, $"RingSeg_{i}", PrimitiveType.Cube, mat,
                        pos: pos, scale: new Vector3(2.4f, 7f, 2.4f));
                    // Rotate so the long (Y) axis runs tangent to the ring, giving a clean band.
                    seg.transform.localRotation = Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg + 90f);
                }

                // Inner disc — a flattened sphere filling the ring, so the portal reads as a glowing
                // membrane rather than an empty hoop. Slightly recessed on +Z is unnecessary (flat).
                MakeChild(root.transform, "Membrane", PrimitiveType.Sphere, mat,
                    pos: Vector3.zero, scale: new Vector3(ringRadius * 2f, ringRadius * 2f, 2f));

                PrefabUtility.SaveAsPrefabAsset(root, WormholePrefabPath);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ---------------- Projectile Bolt ----------------

        [MenuItem("Tools/Space Samurai/Art/Regenerate Projectile Bolt", priority = 53)]
        public static void RegenerateProjectileBolt()
        {
            EnsureFolder(PrefabFolder);
            int built = BuildProjectilePrefab() ? 1 : 0;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArtPrefabBuilder] Built {built}/1 projectile bolt into {PrefabFolder}/. " +
                      "Assign it to the ProjectilePool.projectilePrefab field in combat scenes.");
        }

        /// <summary>
        /// Faceted, self-luminous energy bolt prefab. Replaces ProjectilePool's runtime grey-box sphere.
        /// Unit-diameter icosphere (Projectile.Launch scales it by radius*2); SamuraiToon self-luminous so
        /// the per-shot RendererTint colour reads at full brightness and blooms.
        /// </summary>
        private static bool BuildProjectilePrefab()
        {
            var mat = EnsureBoltMaterial();
            if (mat == null) return false;
            var root = new GameObject("Bolt");
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = LowPolyMeshes.Icosphere();
                var mr = root.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                root.AddComponent<Projectile>(); // RequireComponent auto-adds Rigidbody + SphereCollider
                PrefabUtility.SaveAsPrefabAsset(root, BoltPrefabPath);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ---------------- Planet ----------------

        /// <summary>
        /// Swaps the hand-authored planet prefab's built-in smooth sphere for the faceted
        /// PlanetSphere mesh. Load-edit-save (not rebuild-from-scratch) so the prefab's GUID and
        /// fileIDs survive — scenes reference this prefab directly.
        /// </summary>
        internal static bool BuildPlanetPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlanetPrefabPath) == null)
            {
                Debug.LogWarning($"[ArtPrefabBuilder] Planet prefab missing at {PlanetPrefabPath}; skipped.");
                return false;
            }
            var contents = PrefabUtility.LoadPrefabContents(PlanetPrefabPath);
            try
            {
                var filter = contents.GetComponent<MeshFilter>();
                if (filter == null) filter = contents.AddComponent<MeshFilter>();
                filter.sharedMesh = LowPolyMeshes.PlanetSphere();
                PrefabUtility.SaveAsPrefabAsset(contents, PlanetPrefabPath);
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        // ---------------- EP01 Characters ----------------

        private static bool BuildDominionTrooper()
        {
            var mat = LoadMaterial(EnemyFootMatPath);
            if (mat == null) return false;

            // Human armored melee soldier — cold steel + crimson faction accent. Keeps the same
            // XRRigBuilder.BuildEnemy contract as EnemyFootPrefab: body capsule on root (so
            // root.GetComponent<Renderer>() resolves), CapsuleCollider hurtbox, ArmR/Sword/Blade/BladeTip
            // hierarchy for parry detection and Enemy AI windup/strike animation.
            var root = new GameObject("DominionTrooper");
            try
            {
                // Body capsule on root so root.GetComponent<Renderer>() resolves.
                root.AddComponent<MeshFilter>().sharedMesh = LowPolyMeshes.Capsule8();
                root.AddComponent<MeshRenderer>().sharedMaterial = mat;
                root.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
                root.transform.localPosition = new Vector3(0f, 0.9f, 0f);

                // Body hurtbox — CapsuleCollider for blade hits.
                root.AddComponent<CapsuleCollider>();

                // Military helmet — sleeker than kabuto, more angular.
                var steel = new Color(0.6f, 0.65f, 0.72f);
                var crimson = new Color(0.55f, 0.1f, 0.1f);
                MakeChildTinted(root.transform, "Helmet", PrimitiveType.Sphere, mat,
                    steel, new Vector3(0f, 0.78f, 0f), new Vector3(1.05f, 1.0f, 1.05f),
                    Quaternion.identity);
                // Helmet crest accent.
                MakeRoundedTinted(root.transform, "HelmetCrest", mat,
                    crimson, new Vector3(0f, 1.0f, 0f), new Vector3(0.08f, 0.4f, 0.08f));

                // Chest plate — rigid military look, softened with filleted edges.
                MakeRoundedTinted(root.transform, "ChestPlate", mat,
                    steel, new Vector3(0f, 0.15f, 0.35f), new Vector3(0.7f, 0.7f, 0.35f));
                // Chest accent stripe (crimson).
                MakeRoundedTinted(root.transform, "ChestStripe", mat,
                    crimson, new Vector3(0f, 0.15f, 0.38f), new Vector3(0.08f, 0.5f, 0.05f));

                // Shoulder pads — rounded plates.
                var sodeL = MakeRoundedTinted(root.transform, "SodeL", mat,
                    steel, new Vector3(-0.55f, 0.35f, 0.05f), new Vector3(0.5f, 0.45f, 0.45f));
                sodeL.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
                var sodeR = MakeRoundedTinted(root.transform, "SodeR", mat,
                    steel, new Vector3(0.55f, 0.35f, 0.05f), new Vector3(0.5f, 0.45f, 0.45f));
                sodeR.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);

                // Arm + held katana — same ArmR/Sword/Blade/BladeTip rig Enemy.cs drives.
                // Steel rounded limbs, 10 cm thick, to match the trooper's heavier armor.
                AttachHeldKatanaRig(root.transform, mat,
                    limbColor: steel, roundedLimbs: true, limbThickness: 0.1f);

                AddOutline(root);
                PrefabUtility.SaveAsPrefabAsset(root, DominionTrooperPrefabPath);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static bool BuildKesslerNpc()
        {
            // Ally NPC → cyan-accent NPC body family, not the Dominion magenta EnemyFoot family.
            var mat = EnsureNpcMaterial();
            if (mat == null) return false;

            // Human ally NPC — no weapon, no combat setup. Body capsule on root (for Renderer
            // resolve), simple head + shoulders, civilian/officer palette. Friendly worn look.
            var root = new GameObject("Kessler");
            try
            {
                // Body capsule on root so root.GetComponent<Renderer>() resolves (NPC chat system
                // may reference it).
                root.AddComponent<MeshFilter>().sharedMesh = LowPolyMeshes.Capsule8();
                root.AddComponent<MeshRenderer>().sharedMaterial = mat;
                root.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
                root.transform.localPosition = new Vector3(0f, 0.9f, 0f);

                // NO CapsuleCollider — he's a talker, not a combatant.

                var worn = new Color(0.85f, 0.78f, 0.62f); // weathered cloth
                var brass = new Color(0.78f, 0.55f, 0.18f); // officer badge
                var steel = new Color(0.6f, 0.65f, 0.72f);

                // Head — friendly, slightly larger than enemy.
                MakeChildTinted(root.transform, "Head", PrimitiveType.Sphere, mat,
                    worn, new Vector3(0f, 0.85f, 0f), new Vector3(1.15f, 1.1f, 1.15f),
                    Quaternion.identity);

                // Officer rank insignia on chest — brass plate.
                MakeRoundedTinted(root.transform, "RankBadge", mat,
                    brass, new Vector3(0f, 0.2f, 0.32f), new Vector3(0.15f, 0.12f, 0.05f));

                // Shoulder straps (worn cloth).
                var shoulderL = MakeRoundedTinted(root.transform, "ShoulderL", mat,
                    worn, new Vector3(-0.5f, 0.4f, 0.05f), new Vector3(0.35f, 0.35f, 0.35f));
                shoulderL.transform.localRotation = Quaternion.Euler(0f, 0f, 10f);
                var shoulderR = MakeRoundedTinted(root.transform, "ShoulderR", mat,
                    worn, new Vector3(0.5f, 0.4f, 0.05f), new Vector3(0.35f, 0.35f, 0.35f));
                shoulderR.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);

                // Worn belt (steel buckle, cloth body).
                MakeRoundedTinted(root.transform, "Belt", mat,
                    worn, new Vector3(0f, -0.05f, 0.3f), new Vector3(0.6f, 0.15f, 0.2f));
                MakeRoundedTinted(root.transform, "BeltBuckle", mat,
                    steel, new Vector3(0f, -0.08f, 0.36f), new Vector3(0.18f, 0.08f, 0.05f));

                AddOutline(root);
                PrefabUtility.SaveAsPrefabAsset(root, KesslerPrefabPath);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static bool BuildRonin9()
        {
            // Combat ally NPC → cyan-accent NPC body family, not the Dominion magenta EnemyFoot family.
            var mat = EnsureNpcMaterial();
            if (mat == null) return false;

            // Female combat ally — weathered, scarred, hardened. Slightly slimmer/shorter frame than
            // Kessler (taller male). Rust-grey armor palette with dark prosthetic right arm. No weapon.
            // Body capsule on root (for Renderer resolve), weathered silhouette.
            var root = new GameObject("Ronin9");
            try
            {
                // Body capsule on root so root.GetComponent<Renderer>() resolves.
                root.AddComponent<MeshFilter>().sharedMesh = LowPolyMeshes.Capsule8();
                root.AddComponent<MeshRenderer>().sharedMaterial = mat;
                root.transform.localScale = new Vector3(0.45f, 0.85f, 0.45f); // slightly shorter/slimmer
                root.transform.localPosition = new Vector3(0f, 0.9f, 0f);

                // NO CapsuleCollider — she's a talker, not a combatant.

                var greyRust = new Color(0.72f, 0.68f, 0.62f);  // weathered scar-grey cloth
                var darkMetal = new Color(0.15f, 0.15f, 0.18f); // gun-metal prosthetic (dark)
                var steel = new Color(0.6f, 0.65f, 0.72f);      // accent trim

                // Head — slightly smaller than Kessler, weathered expression.
                MakeChildTinted(root.transform, "Head", PrimitiveType.Sphere, mat,
                    greyRust, new Vector3(0f, 0.8f, 0f), new Vector3(1.05f, 1.0f, 1.05f),
                    Quaternion.identity);

                // Left shoulder — normal cloth.
                var shoulderL = MakeRoundedTinted(root.transform, "ShoulderL", mat,
                    greyRust, new Vector3(-0.45f, 0.35f, 0.05f), new Vector3(0.3f, 0.3f, 0.3f));
                shoulderL.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);

                // Right shoulder — armored, dark prosthetic arm attachment point.
                var shoulderR = MakeRoundedTinted(root.transform, "ShoulderR", mat,
                    greyRust, new Vector3(0.45f, 0.35f, 0.05f), new Vector3(0.3f, 0.3f, 0.3f));
                shoulderR.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);

                // Right prosthetic arm (upper) — dark gun-metal, single piece showing mechanical nature.
                MakeChildTinted(root.transform, "ArmR_Upper", PrimitiveType.Cube, mat,
                    darkMetal, new Vector3(0.5f, 0.25f, 0.05f), new Vector3(0.12f, 0.28f, 0.15f));

                // Right prosthetic arm (lower) — dark metal forearm continuing the mechanical look.
                MakeChildTinted(root.transform, "ArmR_Lower", PrimitiveType.Cube, mat,
                    darkMetal, new Vector3(0.52f, -0.15f, 0.05f), new Vector3(0.1f, 0.25f, 0.15f));

                // Left arm (cloth) — functional, weathered.
                MakeRoundedTinted(root.transform, "ArmL", mat,
                    greyRust, new Vector3(-0.5f, 0.2f, 0.05f), new Vector3(0.12f, 0.4f, 0.15f));

                // Weathered belt accent.
                MakeRoundedTinted(root.transform, "Belt", mat,
                    greyRust, new Vector3(0f, -0.05f, 0.25f), new Vector3(0.5f, 0.12f, 0.18f));

                // Steel belt buckle.
                MakeRoundedTinted(root.transform, "BeltBuckle", mat,
                    steel, new Vector3(0f, -0.08f, 0.3f), new Vector3(0.14f, 0.06f, 0.05f));

                AddOutline(root);
                PrefabUtility.SaveAsPrefabAsset(root, Ronin9PrefabPath);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static bool BuildKhallHologram()
        {
            var mat = LoadMaterial(EnemyFootMatPath);
            if (mat == null) return false;

            // Translucent cyan comms bust — head + shoulders only, floating, no legs. Reads as
            // a hologram via bright cyan tint and minimal silhouette (just upper body floating).
            // Uses SamuraiToon tinted cyan rather than a custom transparent shader.
            var root = new GameObject("KhallHologram");
            try
            {
                var cyan = new Color(0.2f, 0.85f, 0.95f); // bright cyan hologram tint

                // Head — sphere, cyan.
                MakeChildTinted(root.transform, "Head", PrimitiveType.Sphere, mat,
                    cyan, new Vector3(0f, 0.6f, 0f), new Vector3(1.1f, 1.15f, 1.1f),
                    Quaternion.identity);

                // Shoulder plates — rounded, cyan, reading as upper body.
                var shoulderL = MakeRoundedTinted(root.transform, "ShoulderL", mat,
                    cyan, new Vector3(-0.45f, 0.15f, 0f), new Vector3(0.4f, 0.35f, 0.5f));
                shoulderL.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
                var shoulderR = MakeRoundedTinted(root.transform, "ShoulderR", mat,
                    cyan, new Vector3(0.45f, 0.15f, 0f), new Vector3(0.4f, 0.35f, 0.5f));
                shoulderR.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);

                // Central collar/neck (thin cyan disc).
                MakeChildTinted(root.transform, "Collar", PrimitiveType.Cylinder, mat,
                    cyan, new Vector3(0f, 0.35f, 0f), new Vector3(0.35f, 0.08f, 0.35f),
                    Quaternion.Euler(90f, 0f, 0f));

                // Base ring — thin disc under the bust, cyan. Reads as the hologram "sits" on
                // a projection point (no floor, just a floating ring marker).
                MakeChildTinted(root.transform, "BaseRing", PrimitiveType.Cylinder, mat,
                    cyan, new Vector3(0f, -0.05f, 0f), new Vector3(0.9f, 0.04f, 0.9f),
                    Quaternion.Euler(90f, 0f, 0f));

                // Self-luminous projection: push cyan HDR emission + a bright fresnel rim onto each
                // part's (cyan) variant material so the bust reads as a glowing hologram rather than
                // a solid cyan prop. No outline — an ink edge would fight the glow. These cyan
                // variants are unique to Khall, so tweaking them here doesn't leak to other prefabs.
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    var m = r.sharedMaterial;
                    if (m == null) continue;
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", new Color(0.15f, 0.85f, 1f) * 1.4f);
                    if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 1.3f);
                    if (m.HasProperty("_RimColor")) m.SetColor("_RimColor", new Color(0.5f, 0.95f, 1f));
                    EditorUtility.SetDirty(m);
                }

                PrefabUtility.SaveAsPrefabAsset(root, KhallHologramPrefabPath);
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ---------------- held-katana rig (shared) ----------------

        /// <summary>
        /// Builds the right-arm + held-katana sub-tree that Enemy.cs drives: an ArmR pivot (at the
        /// right shoulder, tilted 15° forward) carrying UpperArm/Forearm/Hand and a Sword
        /// (Handle/Guard/Blade) whose Blade carries an empty BladeTip marker at its +Z tip. Enemy.cs
        /// rotates ArmR for the windup/strike and samples BladeTip's world position each frame for
        /// parry detection, so these names and this local frame are a GAMEPLAY CONTRACT — do not
        /// rename or move. The katana color family (blade/brass/indigo/pommel) is fixed; only the
        /// limb color, rounded-vs-hard limbs, and limb thickness vary between wielders.
        /// Extracted verbatim from BuildEnemyFootPrefab / BuildDominionTrooper (which shared it).
        /// </summary>
        private static void AttachHeldKatanaRig(Transform root, Material mat,
            Color limbColor, bool roundedLimbs, float limbThickness,
            Vector3? armOrigin = null, float rigScale = 1f,
            bool includeArm = true, Vector3? armEuler = null)
        {
            var bladeColor = new Color(0.92f, 0.95f, 1f);
            var tsubaColor = new Color(0.78f, 0.55f, 0.18f); // brass
            var itoColor   = new Color(0.12f, 0.1f, 0.18f);  // indigo wrap
            var sameColor  = new Color(0.95f, 0.92f, 0.88f); // pommel/skin

            // armOrigin/rigScale/armEuler let full-size figures (generated combat characters at identity
            // root scale) place the rig at their own hand, shrink it to a natural blade length, and aim
            // it; the enemy/trooper callers omit them and get the original (0.28,0.35,0) @ scale 1 frame.
            var armR = new GameObject("ArmR");
            armR.transform.SetParent(root, false);
            armR.transform.localPosition = armOrigin ?? new Vector3(0.28f, 0.35f, 0f);
            armR.transform.localRotation = Quaternion.Euler(armEuler ?? new Vector3(15f, 0f, 0f));
            armR.transform.localScale = Vector3.one * rigScale;

            // The drawn arm (upper/fore/hand) is for wielders whose ONLY arm is this rig (the enemy
            // capsule). Generated figures already have their own arms from the spec, so they pass
            // includeArm:false and get just the katana gripped at the figure's existing hand — adding
            // the limb here would give them a redundant third arm. gripZ is the sword's seat: at the
            // forearm tip when the arm is drawn, at ArmR's origin (the hand) when it isn't.
            float gripZ = includeArm ? 0.62f : 0f;
            if (includeArm)
            {
                float t = limbThickness;
                if (roundedLimbs)
                {
                    MakeRoundedTinted(armR.transform, "UpperArm", mat, limbColor,
                        new Vector3(0f, 0f, 0.16f), new Vector3(t, t, 0.32f));
                    MakeRoundedTinted(armR.transform, "Forearm", mat, limbColor,
                        new Vector3(0f, 0f, 0.46f), new Vector3(t, t, 0.28f));
                }
                else
                {
                    MakeChildTinted(armR.transform, "UpperArm", PrimitiveType.Cube, mat, limbColor,
                        new Vector3(0f, 0f, 0.16f), new Vector3(t, t, 0.32f));
                    MakeChildTinted(armR.transform, "Forearm", PrimitiveType.Cube, mat, limbColor,
                        new Vector3(0f, 0f, 0.46f), new Vector3(t, t, 0.28f));
                }
                MakeChildTinted(armR.transform, "Hand", PrimitiveType.Sphere, mat, sameColor,
                    new Vector3(0f, 0f, gripZ), new Vector3(0.08f, 0.08f, 0.08f));
            }

            // Sword parents as a SIBLING under ArmR (not under Hand) so it doesn't inherit the hand's
            // 0.08 scale and shrink to a tanto.
            var sword = new GameObject("Sword");
            sword.transform.SetParent(armR.transform, false);
            sword.transform.localPosition = new Vector3(0f, 0f, gripZ);
            sword.transform.localRotation = Quaternion.identity;

            // Handle (tsuka) — indigo wrap. Rounded or hard to match the limbs.
            if (roundedLimbs)
                MakeRoundedTinted(sword.transform, "Handle", mat, itoColor,
                    new Vector3(0f, 0f, 0.08f), new Vector3(0.06f, 0.045f, 0.32f));
            else
                MakeChildTinted(sword.transform, "Handle", PrimitiveType.Cube, mat, itoColor,
                    new Vector3(0f, 0f, 0.08f), new Vector3(0.06f, 0.045f, 0.32f));
            // Guard (tsuba) — brass disc; cylinder rolled 90° so its flat face is perpendicular to +Z.
            MakeChildTinted(sword.transform, "Guard", PrimitiveType.Cylinder, mat, tsubaColor,
                new Vector3(0f, 0f, 0.18f), new Vector3(0.08f, 0.015f, 0.08f),
                Quaternion.Euler(90f, 0f, 0f));
            // Blade — extends along +Z.
            var blade = MakeChildTinted(sword.transform, "Blade", PrimitiveType.Cube, mat, bladeColor,
                new Vector3(0f, 0f, 0.70f), new Vector3(0.05f, 0.0144f, 1.0f));

            var bladeTip = new GameObject("BladeTip");
            bladeTip.transform.SetParent(blade.transform, false);
            bladeTip.transform.localPosition = new Vector3(0f, 0f, 0.5f); // +Z end of the unit cube
            bladeTip.transform.localRotation = Quaternion.identity;

            // Plasma look: emissive magenta blade + neon tip trail + tier-gated light (Dominion wielders).
            ApplyPlasmaBlade(blade, PlasmaEnemy);
        }

        // ---------------- helpers ----------------

        private static GameObject MakeChild(Transform parent, string name, PrimitiveType type,
            Material mat, Vector3 pos, Vector3 scale, bool keepCollider = false, Quaternion? localRot = null)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = LowPolyMeshes.ForType(type);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            // keepCollider used to mean "don't strip CreatePrimitive's collider"; with custom
            // meshes it means "add the analytic collider CreatePrimitive would have attached".
            if (keepCollider) AddAnalyticCollider(go, type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (localRot.HasValue) go.transform.localRotation = localRot.Value;
            return go;
        }

        private static void AddAnalyticCollider(GameObject go, PrimitiveType type)
        {
            switch (type)
            {
                case PrimitiveType.Sphere:
                    go.AddComponent<SphereCollider>().radius = 0.5f;
                    break;
                case PrimitiveType.Cylinder:
                case PrimitiveType.Capsule:
                    var cap = go.AddComponent<CapsuleCollider>();
                    cap.radius = 0.5f;
                    cap.height = 2f;
                    break;
                default:
                    go.AddComponent<BoxCollider>(); // center 0, size 1 — matches the unit cube
                    break;
            }
        }

        private static GameObject MakeChildTinted(Transform parent, string name, PrimitiveType type,
            Material baseMat, Color tint, Vector3 pos, Vector3 scale, Quaternion? localRot = null)
        {
            var go = MakeChild(parent, name, type, baseMat, pos, scale, false, localRot);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = TintInstance(baseMat, tint);
            return go;
        }

        /// <summary>
        /// <see cref="MakeChildTinted"/> with an extra HDR emission so the part reads as a glowing
        /// holographic accent (Bloom turns it into light). Use for cockpit dash strips/readouts.
        /// </summary>
        private static GameObject MakeChildEmissive(Transform parent, string name, PrimitiveType type,
            Material baseMat, Color baseTint, Color emission, Vector3 pos, Vector3 scale, Quaternion? localRot = null)
        {
            var go = MakeChild(parent, name, type, baseMat, pos, scale, false, localRot);
            go.GetComponent<Renderer>().sharedMaterial = TintEmissiveInstance(baseMat, baseTint, emission);
            return go;
        }

        // ---------------- chamfered geometry + outlines ----------------

        /// <summary>
        /// Chamfer-cube counterpart to <see cref="MakeChildTinted"/>: a flat-shaded beveled plate
        /// with the same pivot/extents as a Cube, tinted via the on-disk variant material. No
        /// collider (visual only; gameplay colliders live on the prefab root, untouched by this).
        /// </summary>
        private static GameObject MakeRoundedTinted(Transform parent, string name, Material baseMat,
            Color tint, Vector3 pos, Vector3 scale, Quaternion? localRot = null)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = LowPolyMeshes.ChamferCube();
            go.AddComponent<MeshRenderer>().sharedMaterial = TintInstance(baseMat, tint);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (localRot.HasValue) go.transform.localRotation = localRot.Value;
            return go;
        }

        /// <summary>
        /// Loads (creating if missing) the shared inverted-hull outline material. Mirrors
        /// <see cref="EnsureShipHullMaterial"/> so a fresh checkout still resolves a real material.
        /// </summary>
        private static Material EnsureOutlineMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(OutlineMatPath);
            if (existing != null) return existing;
            var shader = Shader.Find("Ronin7/SamuraiOutline");
            if (shader == null) { Debug.LogError("[ArtPrefabBuilder] SamuraiOutline shader missing."); return null; }
            var m = new Material(shader) { name = "SamuraiOutline" };
            if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", new Color(0.02f, 0.02f, 0.03f));
            if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", 0.012f);
            EnsureAssetFolder(MatFolder);
            AssetDatabase.CreateAsset(m, OutlineMatPath);
            return AssetDatabase.LoadAssetAtPath<Material>(OutlineMatPath) ?? m;
        }

        /// <summary>
        /// Appends the inverted-hull outline material as element [1] on every MeshRenderer under
        /// <paramref name="root"/> (the usage the SamuraiOutline shader header documents). Idempotent:
        /// renderers that already carry 2 materials are left alone so re-runs don't stack outlines.
        /// </summary>
        private static void AddOutline(GameObject root)
        {
            var outline = EnsureOutlineMaterial();
            if (outline == null) return;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                if (mats.Length >= 2) continue;
                var grown = new Material[mats.Length + 1];
                System.Array.Copy(mats, grown, mats.Length);
                grown[mats.Length] = outline;
                r.sharedMaterials = grown;
            }
        }

        private const string VariantFolder = MatFolder + "/Variants";

        /// <summary>
        /// Returns (creating if needed) a tinted variant material as an ASSET on disk. Saving the
        /// tinted Material as a real asset is what keeps the shader binding intact when the prefab
        /// is reimported — `new Material(src)` instances assigned to a prefab renderer get inlined
        /// into the prefab YAML and frequently come back pink (FallbackError) on next Unity launch.
        /// </summary>
        private static Material TintInstance(Material src, Color color)
        {
            EnsureAssetFolder(VariantFolder);
            string variantName = $"{src.name}_{ColorTag(color)}";
            string path = $"{VariantFolder}/{variantName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (existing.shader != src.shader) existing.shader = src.shader;
                if (existing.HasProperty("_BaseColor")) existing.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            var m = new Material(src) { name = variantName };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, path);
            // Re-load through AssetDatabase so the renderer holds the imported-asset reference,
            // not the in-memory object we just passed to CreateAsset. Subtle but important: the
            // pre-import reference can become stale after the import completes, and saving it into
            // a prefab can leave the prefab pointing at a ghost material.
            return AssetDatabase.LoadAssetAtPath<Material>(path) ?? m;
        }

        /// <summary>
        /// Like <see cref="TintInstance"/> but also bakes an HDR <c>_EmissionColor</c> so the part
        /// glows (URP Bloom turns it into light). The variant is keyed on BOTH the base tint and the
        /// emission, so an emissive part never shares (and silently lights up) a plain base-colour
        /// variant. Used for the holographic cockpit dash/console accents.
        /// </summary>
        private static Material TintEmissiveInstance(Material src, Color baseColor, Color emission)
        {
            EnsureAssetFolder(VariantFolder);
            string variantName = $"{src.name}_{ColorTag(baseColor)}_e{ColorTag(emission)}";
            string path = $"{VariantFolder}/{variantName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (existing.shader != src.shader) existing.shader = src.shader;
                if (existing.HasProperty("_BaseColor")) existing.SetColor("_BaseColor", baseColor);
                if (existing.HasProperty("_EmissionColor")) existing.SetColor("_EmissionColor", emission);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            var m = new Material(src) { name = variantName };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
            AssetDatabase.CreateAsset(m, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path) ?? m;
        }

        private static string ColorTag(Color c) =>
            $"r{Mathf.RoundToInt(c.r * 255)}g{Mathf.RoundToInt(c.g * 255)}b{Mathf.RoundToInt(c.b * 255)}";

        private static Material LoadMaterial(string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) Debug.LogError($"[ArtPrefabBuilder] Material missing: {path}");
            return m;
        }

        private static void SetRef(SerializedObject so, string prop, UnityEngine.Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void EnsureFolder(string assetPath)
        {
            string abs = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
        }

        /// <summary>
        /// Registers a folder with the Unity AssetDatabase so CreateAsset can write into it.
        /// Walks the path from project root and calls AssetDatabase.CreateFolder for each segment
        /// that isn't already a known asset folder — a plain filesystem Directory.Create is NOT
        /// enough; CreateAsset silently no-ops if the parent isn't a registered AssetDatabase folder.
        /// </summary>
        private static void EnsureAssetFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;
            string[] segments = assetFolderPath.Split('/');
            string built = segments[0]; // "Assets"
            for (int i = 1; i < segments.Length; i++)
            {
                string next = built + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(built, segments[i]);
                built = next;
            }
        }
    }
}
