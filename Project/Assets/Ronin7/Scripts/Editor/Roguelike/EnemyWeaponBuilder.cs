using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// P8-02 (D-17): builds the enemy weapon prefabs + <see cref="EnemyWeaponDefinition"/>s, procedural from primitives
    /// (like EnemySwordVisual) on a 3-material shared palette, no colliders, lights or shadows. The heavy, shield and
    /// staff are stand-ins for the user's image-to-3D meshes: drop the mesh prefab into the definition's
    /// <c>prefab</c> field (name its renderers "Blade"/"Shield" to keep the tint and guard hooks) and a re-run leaves it.
    /// Prefabs are authored in the socket frame: origin = hand, +Z forward, +Y up at the arm's rest.
    /// </summary>
    public static class EnemyWeaponBuilder
    {
        internal const string DefFolder = "Assets/Ronin7/Data/Roguelike/Weapons";
        private const string PrefabFolder = "Assets/Ronin7/Prefabs/Roguelike/Weapons";
        private const string MatFolder = "Assets/Ronin7/Art/Materials/EnemyWeapons";
        private const string SpawnTablePath = "Assets/Ronin7/Data/Roguelike/EnemySpawnTable.asset";

        private enum M { Steel, Grip, Brass }
        private static readonly Color[] Palette = { new Color(0.92f, 0.95f, 1f), new Color(0.12f, 0.1f, 0.18f), new Color(0.78f, 0.55f, 0.18f) };

        private struct Part
        {
            public string name; public PrimitiveType type; public M mat; public Vector3 pos, scale, euler;
            public Part(string n, PrimitiveType t, M m, Vector3 p, Vector3 s, Vector3 e = default) { name = n; type = t; mat = m; pos = p; scale = s; euler = e; }
        }

        // P8-02c: the Aim pose swings the arm -75 deg about X, which carries weapon +Z to world up and -Y to world forward.
        // Ranged kits are authored the natural way (barrel +Z, up +Y) then turned 90 deg about X so the barrel lands on -Y.
        private static Vector3 Aimed(Vector3 v) => new Vector3(v.x, -v.z, v.y);
        private static Part[] Aimed(params Part[] ps)
        {
            for (int i = 0; i < ps.Length; i++)
                ps[i] = new Part(ps[i].name, ps[i].type, ps[i].mat, Aimed(ps[i].pos), ps[i].scale, (Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(ps[i].euler)).eulerAngles);
            return ps;
        }

        private static readonly Vector3 Roll = new Vector3(90f, 0f, 0f); // cylinder long axis Y -> +Z

        // name, tip, reach multiplier, ranged, parts (null = natural/meshless)
        private static readonly (string name, Vector3 tip, float reach, bool ranged, Part[] parts)[] Specs =
        {
            ("Sword", new Vector3(0f, 0f, 0.875f), 1f, false, new[] {
                new Part("Handle", PrimitiveType.Cube, M.Grip, Vector3.zero, new Vector3(0.035f, 0.035f, 0.22f)),
                new Part("Guard", PrimitiveType.Cylinder, M.Brass, new Vector3(0f, 0f, 0.12f), new Vector3(0.08f, 0.01f, 0.08f), Roll),
                new Part("Blade", PrimitiveType.Cube, M.Steel, new Vector3(0f, 0f, 0.5f), new Vector3(0.045f, 0.012f, 0.75f)) }),
            ("Spear", new Vector3(0f, 0f, 1.47f), 1.25f, false, new[] {
                new Part("Shaft", PrimitiveType.Cylinder, M.Grip, new Vector3(0f, 0f, 0.45f), new Vector3(0.03f, 0.8f, 0.03f), Roll),
                new Part("Blade", PrimitiveType.Cube, M.Steel, new Vector3(0f, 0f, 1.36f), new Vector3(0.05f, 0.012f, 0.22f)) }),
            ("HeavyAxe", new Vector3(0f, 0.2f, 0.7f), 1f, false, new[] { // stand-in: user image-to-3D mesh later
                new Part("Haft", PrimitiveType.Cylinder, M.Grip, new Vector3(0f, 0f, 0.3f), new Vector3(0.04f, 0.45f, 0.04f), Roll),
                new Part("Blade", PrimitiveType.Cube, M.Steel, new Vector3(0f, 0.09f, 0.68f), new Vector3(0.06f, 0.24f, 0.16f)) }),
            ("Dagger", new Vector3(0f, 0f, 0.34f), 1f, false, new[] {
                new Part("Grip", PrimitiveType.Cube, M.Grip, Vector3.zero, new Vector3(0.03f, 0.03f, 0.12f)),
                new Part("Blade", PrimitiveType.Cube, M.Steel, new Vector3(0f, 0f, 0.2f), new Vector3(0.035f, 0.01f, 0.28f)) }),
            ("Baton", new Vector3(0f, 0f, 0.52f), 1f, false, new[] {
                new Part("Blade", PrimitiveType.Cylinder, M.Steel, new Vector3(0f, 0f, 0.22f), new Vector3(0.035f, 0.3f, 0.035f), Roll) }),
            ("Shield", Vector3.zero, 1f, false, new[] { // stand-in: user image-to-3D mesh later. Socket_Shield faces it out-forward, sized to a 0.25 m forearm.
                new Part("Shield", PrimitiveType.Cube, M.Steel, new Vector3(0f, 0f, 0.015f), new Vector3(0.32f, 0.42f, 0.03f)) }),
            ("Staff", new Vector3(0f, 0f, 1.2f), 1f, true, new[] { // stand-in: user image-to-3D mesh later
                new Part("Shaft", PrimitiveType.Cylinder, M.Grip, new Vector3(0f, 0f, 0.35f), new Vector3(0.03f, 0.75f, 0.03f), Roll),
                new Part("Blade", PrimitiveType.Sphere, M.Brass, new Vector3(0f, 0f, 1.15f), new Vector3(0.1f, 0.1f, 0.1f)) }),
            ("Bow", Aimed(new Vector3(0f, 0f, 0.7f)), 1f, true, Aimed(new[] { // limbs vertical, string toward the archer, arrow along the aim line
                new Part("Grip", PrimitiveType.Cube, M.Grip, Vector3.zero, new Vector3(0.035f, 0.12f, 0.04f)),
                new Part("LimbU", PrimitiveType.Cylinder, M.Grip, new Vector3(0f, 0.26f, -0.06f), new Vector3(0.025f, 0.267f, 0.025f), new Vector3(-13f, 0f, 0f)),
                new Part("LimbL", PrimitiveType.Cylinder, M.Grip, new Vector3(0f, -0.26f, -0.06f), new Vector3(0.025f, 0.267f, 0.025f), new Vector3(13f, 0f, 0f)),
                new Part("String", PrimitiveType.Cylinder, M.Steel, new Vector3(0f, 0f, -0.12f), new Vector3(0.006f, 0.52f, 0.006f)),
                new Part("Blade", PrimitiveType.Cylinder, M.Brass, new Vector3(0f, 0f, 0.35f), new Vector3(0.012f, 0.35f, 0.012f), Roll) })),
            ("Crossbow", Aimed(new Vector3(0f, 0.03f, 0.7f)), 1f, true, Aimed(new[] {
                new Part("Stock", PrimitiveType.Cube, M.Grip, new Vector3(0f, 0f, 0.25f), new Vector3(0.045f, 0.055f, 0.55f)),
                new Part("Prod", PrimitiveType.Cube, M.Steel, new Vector3(0f, 0.01f, 0.5f), new Vector3(0.5f, 0.025f, 0.04f)),
                new Part("String", PrimitiveType.Cube, M.Steel, new Vector3(0f, 0.01f, 0.45f), new Vector3(0.48f, 0.008f, 0.008f)),
                new Part("Blade", PrimitiveType.Cube, M.Brass, new Vector3(0f, 0.04f, 0.45f), new Vector3(0.015f, 0.015f, 0.5f)) })),
            ("Blaster", Aimed(new Vector3(0f, 0.02f, 0.55f)), 1f, true, Aimed(new[] {
                new Part("Body", PrimitiveType.Cube, M.Grip, new Vector3(0f, 0f, 0.12f), new Vector3(0.07f, 0.1f, 0.26f)),
                new Part("Handle", PrimitiveType.Cube, M.Grip, new Vector3(0f, -0.07f, 0.05f), new Vector3(0.045f, 0.11f, 0.055f), new Vector3(-12f, 0f, 0f)),
                new Part("Blade", PrimitiveType.Cylinder, M.Steel, new Vector3(0f, 0.02f, 0.38f), new Vector3(0.04f, 0.16f, 0.04f), Roll),
                new Part("Cell", PrimitiveType.Cube, M.Brass, new Vector3(0f, 0.065f, 0.15f), new Vector3(0.05f, 0.03f, 0.14f)) })),
            ("Claws", new Vector3(0f, 0f, 0.25f), 1f, false, null),
        };

        /// <summary>Fallback kit per trash body (main, off), used when the table has no roll list. Natural bodies get claws.</summary>
        internal static (string main, string off) KitFor(string prefabName) => prefabName switch
        {
            "Ash-World_Scavenger" => ("Spear", null),
            "Coil_Syndicate_Ganger" => ("Dagger", "Dagger"),
            "Dominion_Trooper" => ("Baton", "Shield"),
            "Program_Operative_Grunt" => ("Sword", null),
            "Humanoid_Automaton" => ("HeavyAxe", null),
            "Iron_Dojo_Warden-Cadre" => ("Staff", null),
            _ => ("Claws", null),
        };

        /// <summary>P8-02b: the loadouts an armed body can roll: every weapon, in an order tests rely on.</summary>
        private static readonly (string main, string off)[] RollKits =
        {
            ("Sword", null), ("Spear", null), ("HeavyAxe", null), ("Dagger", "Dagger"), ("Baton", "Shield"), ("Spear", "Shield"), ("Staff", null),
            ("Bow", null), ("Crossbow", null), ("Blaster", null),
        };

        private static AttackPattern Pat(string name, int weight, params Strike[] strikes) =>
            new AttackPattern { name = name, weight = weight, strikes = new System.Collections.Generic.List<Strike>(strikes) };

        private static Strike Shot(float windup, float recovery, float speed, float gravity, float radius) => new Strike
        {
            direction = StrikeDirection.Aim, windup = windup, active = 1f, recovery = recovery,
            boltSpeed = speed, boltGravity = gravity, boltRadius = radius,
        };

        // P8-03 melee strike: windup multiplier on the body's telegraph; dodge = unparryable (a blade parry won't stop it).
        private static Strike Hit(StrikeDirection d, float windup, bool dodge = false) => new Strike
        {
            direction = d, windup = windup, active = 1f, recovery = 1f, unparryable = dodge,
        };

        /// <summary>P8-03: each weapon's moveset (the approved table), rewritten on every compose. Ranged weapons carry
        /// their bolt data. An off-hand's patterns (Shield bash) are merged into the main hand's at Equip.</summary>
        internal static AttackPattern[] PatternsFor(string weapon) => weapon switch
        {
            "Sword" => new[] { Pat("Overhead", 2, Hit(StrikeDirection.Overhead, 1f)),
                               Pat("L-R", 1, Hit(StrikeDirection.Left, 0.8f), Hit(StrikeDirection.Right, 0.8f)) },
            "Spear" => new[] { Pat("Thrust", 2, Hit(StrikeDirection.Thrust, 1f)),
                               Pat("Double thrust", 1, Hit(StrikeDirection.Thrust, 0.7f), Hit(StrikeDirection.Thrust, 0.7f)),
                               Pat("Low sweep", 1, Hit(StrikeDirection.Low, 1.3f, dodge: true)) },
            "HeavyAxe" => new[] { Pat("Overhead slam", 2, Hit(StrikeDirection.Overhead, 1.6f, dodge: true)),
                                  Pat("Side sweep", 1, Hit(StrikeDirection.Left, 1.4f, dodge: true)) },
            "Dagger" => new[] { Pat("Flurry", 2, Hit(StrikeDirection.Right, 0.5f), Hit(StrikeDirection.Left, 0.5f), Hit(StrikeDirection.Thrust, 0.5f)),
                                Pat("Cross slash", 1, Hit(StrikeDirection.Left, 1f)) },
            "Baton" => new[] { Pat("Baton strike", 2, Hit(StrikeDirection.Overhead, 1f)) },
            "Shield" => new[] { Pat("Shield bash", 1, Hit(StrikeDirection.Thrust, 0.7f, dodge: true)) },
            "Claws" => new[] { Pat("Swipe", 2, Hit(StrikeDirection.Right, 1f)),
                               Pat("Double swipe", 1, Hit(StrikeDirection.Left, 0.8f), Hit(StrikeDirection.Right, 0.8f)) },
            "Bow" => new[] { Pat("Aimed", 2, Shot(1.5f, 1f, 8f, 6f, 0.1f)), Pat("Snap", 1, Shot(0.8f, 1f, 10f, 3f, 0.1f)) },
            "Crossbow" => new[] { Pat("Bolt", 1, Shot(0.6f, 2.5f, 10f, 0f, 0.06f)) },
            // Burst shots 1-2 recover at 0.3x so the 3 bolts read as one burst, not three separate attacks.
            "Blaster" => new[] { Pat("Burst", 2, Shot(0.8f, 0.3f, 6f, 0f, 0.1f), Shot(0.15f, 0.3f, 6f, 0f, 0.1f), Shot(0.15f, 1f, 6f, 0f, 0.1f)),
                                 Pat("Charged", 1, Shot(1.4f, 1f, 3f, 0f, 0.25f)) },
            "Staff" => new[] { Pat("Bolt cast", 2, Shot(1.2f, 1f, 4f, 0f, 0.12f)), Pat("Jab", 1, Hit(StrikeDirection.Thrust, 1f)) },
            _ => new[] { new AttackPattern() },
        };

        internal static void AssignKits(EnemySpawnTable table)
        {
            table.kits = System.Array.ConvertAll(RollKits, k => new EnemySpawnTable.Kit { main = Load(k.main), off = Load(k.off) });
            EditorUtility.SetDirty(table);
        }

        internal static EnemyWeaponDefinition Load(string weapon) =>
            weapon == null ? null : AssetDatabase.LoadAssetAtPath<EnemyWeaponDefinition>(DefFolder + "/EW_" + weapon + ".asset");

        internal static void Assign(EnemySpawnTable.Entry e)
        {
            var (main, off) = KitFor(e.prefab.name);
            e.mainHand = Load(main);
            e.offHand = Load(off);
            e.fixedKit = e.prefab.name == "Humanoid_Automaton" || e.prefab.name == "Iron_Dojo_Warden-Cadre"; // sculpted weapon must match
        }

        /// <summary>P8-02 (additive, idempotent): weapons, socketed bodies (Sword/slab stripped) and the spawn-table kits.</summary>
        [MenuItem("Tools/Space Samurai/Roguelike/Compose Enemy Weapons (additive)")]
        public static void ComposeAll()
        {
            BuildWeapons();
            EnemyPrefabBaker.ComposeBakedBodies();
            foreach (var boss in new[] { "The-Warden", "Maelgorn" }) // armed bosses keep the P8-01 baked katana
            {
                string path = "Assets/Ronin7/Prefabs/Roguelike/Enemies/" + boss + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try { PaintKatana(root.transform); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>(SpawnTablePath);
            if (table != null)
            {
                foreach (var e in table.trash) Assign(e);
                AssignKits(table);
                EditorUtility.SetDirty(table);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[EnemyWeaponBuilder] Composed enemy weapons.");
        }

        internal static void BuildWeapons()
        {
            foreach (var f in new[] { DefFolder, PrefabFolder, MatFolder }) EnsureFolder(f);
            var mats = new Material[Palette.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                string path = MatFolder + "/EnemyWeapon_" + (M)i + ".mat";
                mats[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mats[i] == null) { mats[i] = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mats[i], path); }
                mats[i].SetColor("_BaseColor", Palette[i]);
                mats[i].enableInstancing = true;
                EditorUtility.SetDirty(mats[i]);
            }

            foreach (var spec in Specs)
            {
                string defPath = DefFolder + "/EW_" + spec.name + ".asset";
                var def = AssetDatabase.LoadAssetAtPath<EnemyWeaponDefinition>(defPath);
                if (def == null) { def = ScriptableObject.CreateInstance<EnemyWeaponDefinition>(); AssetDatabase.CreateAsset(def, defPath); }
                def.tip = spec.tip;
                def.edgeBase = new Vector3(spec.tip.x, spec.tip.y, Mathf.Min(spec.tip.z, 0.12f));
                def.reach = spec.reach;
                def.patterns = new System.Collections.Generic.List<AttackPattern>(PatternsFor(spec.name));
                def.ranged = def.patterns.Exists(p => p.strikes.Exists(k => k.Ranged));
                def.forearm = spec.name == "Shield";
                if (spec.parts != null)
                {
                    string prefabPath = PrefabFolder + "/EW_" + spec.name + ".prefab";
                    var root = new GameObject("EW_" + spec.name);
                    foreach (var p in spec.parts) MakePart(root.transform, p, mats[(int)p.mat]);
                    var built = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Object.DestroyImmediate(root);
                    // A user mesh swapped into def.prefab survives a re-run.
                    if (def.prefab == null || AssetDatabase.GetAssetPath(def.prefab) == prefabPath) def.prefab = built;
                }
                else def.prefab = null;
                EditorUtility.SetDirty(def);
            }
        }

        /// <summary>The baked EnemySwordVisual katana got a runtime (unsaved) material, so the saved boss prefabs
        /// referenced none (magenta). Point Handle/Guard/Blade at the palette.</summary>
        internal static void PaintKatana(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                int m = r.name == "Handle" ? (int)M.Grip : r.name == "Guard" ? (int)M.Brass : r.name == "Blade" ? (int)M.Steel : -1;
                if (m >= 0) r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/EnemyWeapon_" + (M)m + ".mat");
            }
        }

        private static void MakePart(Transform parent, Part p, Material mat)
        {
            var go = GameObject.CreatePrimitive(p.type);
            go.name = p.name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = p.pos;
            go.transform.localRotation = Quaternion.Euler(p.euler);
            go.transform.localScale = p.scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            EnsureFolder(folder.Substring(0, slash));
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }
    }
}
