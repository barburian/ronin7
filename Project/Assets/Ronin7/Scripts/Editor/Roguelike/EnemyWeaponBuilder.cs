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

        private const string GenFolder = "Assets/Ronin7/Art/Generated/AI/Weapons";

        /// <summary>How a Tripo mesh (<c>GenFolder/name.prefab</c>, 1 m on its longest axis, arbitrary pose) sits in the weapon frame.
        /// fwd/up/anchor are in the generated prefab's own (Y-up, root-rotation applied) frame: the anchor (the grip) lands on
        /// <c>at</c>, fwd on +Z, up on +Y, scaled to <c>scale</c>; <c>aimed</c> kits are then turned like <see cref="Aimed(Vector3)"/>.
        /// tip/edge are in the pre-scale natural frame relative to the anchor (x right, y up, z fwd). node = renderer name hook.</summary>
        private struct MeshFit { public Vector3 fwd, up, anchor, at, tip, edge; public float scale; public bool aimed; public string node; }

        // Orientation/anchors measured from the meshes (PCA of the haft; blade side = the flared, tapering side). Verified by the lineup capture.
        private static readonly System.Collections.Generic.Dictionary<string, MeshFit> Fits = new System.Collections.Generic.Dictionary<string, MeshFit>
        {
            ["HeavyAxe"] = new MeshFit { fwd = new Vector3(-0.784f, 0.474f, -0.402f), up = new Vector3(0.491f, 0.077f, -0.868f), anchor = new Vector3(0.287f, -0.197f, 0.172f),
                scale = 1.08f, tip = new Vector3(0f, 0.18f, 0.82f), edge = new Vector3(0.04f, 0.23f, 0.52f), node = "Blade" }, // 1.2 m; grip 0.27 m above the butt; edge = blade's outer line, toe to top corner
            ["Shield"] = new MeshFit { fwd = Vector3.forward, up = Vector3.up, anchor = Vector3.zero, at = new Vector3(0f, 0f, 0.05f), scale = 0.55f, node = "Shield" }, // 0.41 x 0.55 m, convex face out (+Z)
            ["Staff"] = new MeshFit { fwd = Vector3.up, up = Vector3.forward, anchor = new Vector3(0f, -0.222f, 0f), scale = 1.8f,
                tip = new Vector3(0f, 0f, 0.70f), edge = new Vector3(0f, 0f, 0.067f), node = "Blade" }, // 1.8 m; hand 0.5 m above the butt, lantern forward
            ["Bow"] = new MeshFit { fwd = Vector3.left, up = Vector3.up, anchor = new Vector3(-0.07f, 0f, 0f), scale = 1.3f, aimed = true,
                tip = new Vector3(0f, 0f, 0.03f), node = "Blade" }, // 1.3 m, shoots out the belly side (-X in mesh), hand on the riser
            ["Crossbow"] = new MeshFit { fwd = Vector3.forward, up = Vector3.up, anchor = new Vector3(0f, -0.04f, -0.15f), scale = 0.75f, aimed = true,
                tip = new Vector3(0f, 0.075f, 0.64f), node = "Blade" }, // 0.75 m, bolt head forward
            ["Blaster"] = new MeshFit { fwd = Vector3.left, up = Vector3.up, anchor = new Vector3(0.22f, -0.08f, 0f), scale = 0.6f, aimed = true,
                tip = new Vector3(0f, 0.18f, 0.70f), node = "Blade" }, // 0.6 m, muzzle at -X in mesh, hand on the pistol grip
        };

        private static Quaternion FitRot(MeshFit f) =>
            (f.aimed ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity) * Quaternion.Inverse(Quaternion.LookRotation(f.fwd, f.up));
        private static Vector3 FitLocal(MeshFit f, Vector3 natural) => f.scale * (f.aimed ? Aimed(natural) : natural);

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
            ("HeavyAxe", new Vector3(0f, 0.2f, 0.76f), 1f, false, new[] { // stand-in: user image-to-3D mesh later
                new Part("Haft", PrimitiveType.Cylinder, M.Grip, new Vector3(0f, 0f, 0.3f), new Vector3(0.04f, 0.45f, 0.04f), Roll),
                new Part("Blade", PrimitiveType.Cube, M.Steel, new Vector3(0f, 0.09f, 0.68f), new Vector3(0.06f, 0.24f, 0.16f)) }),
            ("Dagger", new Vector3(0f, 0f, 0.34f), 1f, false, new[] {
                new Part("Grip", PrimitiveType.Cube, M.Grip, Vector3.zero, new Vector3(0.03f, 0.03f, 0.12f)),
                new Part("Blade", PrimitiveType.Cube, M.Steel, new Vector3(0f, 0f, 0.2f), new Vector3(0.035f, 0.01f, 0.28f)) }),
            ("Baton", new Vector3(0f, 0f, 0.52f), 1f, false, new[] {
                new Part("Blade", PrimitiveType.Cylinder, M.Steel, new Vector3(0f, 0f, 0.22f), new Vector3(0.035f, 0.3f, 0.035f), Roll) }),
            ("Shield", Vector3.zero, 1f, false, new[] { // stand-in: user image-to-3D mesh later. Socket_Shield faces it out-forward, sized to a 0.25 m forearm.
                new Part("Shield", PrimitiveType.Cube, M.Steel, new Vector3(0f, 0f, 0.015f), new Vector3(0.32f, 0.42f, 0.03f)),
                // P8-05 weak spot: a bright seam on the outer (-X, rear-left) flank of the face. Enemy.Equip lights it.
                new Part(Enemy.WeakSpotName, PrimitiveType.Cube, M.Brass, new Vector3(-0.145f, 0f, 0.034f), new Vector3(0.03f, 0.38f, 0.012f)) }),
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
            // P8-06 Banner Commander (fixed kit, never rolled): an upright pole from the hand (~2.8 m tall in the rest pose)
            // with a white (Steel: reads by value in greyscale) flag flying out to the bearer's right, facing the player full-on. Finial = tip ("Blade": tint hook).
            ("Banner", new Vector3(0f, 2f, 0f), 1f, false, new[] {
                new Part("Pole", PrimitiveType.Cylinder, M.Grip, new Vector3(0f, 0.85f, 0f), new Vector3(0.035f, 1.15f, 0.035f)),
                new Part(BannerCommander.FlagName, PrimitiveType.Cube, M.Steel, new Vector3(0.3f, 1.62f, 0f), new Vector3(0.56f, 0.46f, 0.015f)),
                new Part("Blade", PrimitiveType.Sphere, M.Steel, new Vector3(0f, 2f, 0f), new Vector3(0.09f, 0.09f, 0.09f)) }),
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
            "Banner" => new[] { Pat("Banner jab", 1, Hit(StrikeDirection.Thrust, 1.3f)) }, // P8-06: weak, slow
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

        /// <param name="only">P8-06: build just this weapon (the additive archetype bake), leaving the others untouched.</param>
        internal static void BuildWeapons(string only = null)
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
                if (only != null && spec.name != only) continue;
                string defPath = DefFolder + "/EW_" + spec.name + ".asset";
                var def = AssetDatabase.LoadAssetAtPath<EnemyWeaponDefinition>(defPath);
                if (def == null) { def = ScriptableObject.CreateInstance<EnemyWeaponDefinition>(); AssetDatabase.CreateAsset(def, defPath); }
                def.tip = spec.tip;
                def.edgeBase = spec.name == "HeavyAxe" ? new Vector3(0f, 0.2f, 0.58f) // along the head's top (striking) edge, not floating beside the haft
                    : spec.name == "Banner" ? new Vector3(0f, 0.3f, 0f) // along the pole, above the fist
                    : new Vector3(spec.tip.x, spec.tip.y, Mathf.Min(spec.tip.z, 0.12f));
                var gen = Fits.TryGetValue(spec.name, out var fit) ? AssetDatabase.LoadAssetAtPath<GameObject>(GenFolder + "/" + spec.name + ".prefab") : null;
                if (gen != null)
                {
                    def.tip = FitLocal(fit, fit.tip);
                    def.edgeBase = fit.aimed ? def.tip : FitLocal(fit, fit.edge); // ranged: the edge runs from the weapon root
                    if (spec.name == "Shield") def.tip = def.edgeBase = Vector3.zero;
                }
                def.reach = spec.reach;
                def.patterns = new System.Collections.Generic.List<AttackPattern>(PatternsFor(spec.name));
                def.ranged = def.patterns.Exists(p => p.strikes.Exists(k => k.Ranged));
                def.forearm = spec.name == "Shield";
                if (spec.parts != null)
                {
                    string prefabPath = PrefabFolder + "/EW_" + spec.name + ".prefab";
                    var root = new GameObject("EW_" + spec.name);
                    if (gen != null) MakeMesh(root.transform, gen, fit, mats[(int)M.Brass]);
                    else foreach (var p in spec.parts) MakePart(root.transform, p, mats[(int)p.mat]);
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

        /// <summary>P8-05: saved URP Unlit palette material for baked weak-point marks (Enemy swaps to SharedUnlit at runtime).</summary>
        internal static Material MarkMaterial => AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/EnemyWeapon_" + M.Brass + ".mat");

        /// <summary>The Tripo prefab as a nested instance, posed by <paramref name="f"/>; its renderer takes the hook name
        /// (Blade/Shield). The shield also gets its P8-05 WeakSpot strip laid over the mesh's brass band.</summary>
        private static void MakeMesh(Transform parent, GameObject gen, MeshFit f, Material strip)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(gen, parent);
            inst.name = "Mesh";
            var t = inst.transform;
            t.localRotation = FitRot(f) * gen.transform.localRotation; // the node's own pose is the prefab root's rotation
            t.localScale = Vector3.one * f.scale;
            t.localPosition = f.at - f.scale * (FitRot(f) * f.anchor);
            var r = inst.GetComponentInChildren<MeshRenderer>(true);
            r.gameObject.name = f.node;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // Band: full width, ~0.1 of the height at mid-face, face top at z 0.144 (mesh units).
            if (f.node == "Shield") MakePart(parent, new Part(Enemy.WeakSpotName, PrimitiveType.Cube, M.Brass, new Vector3(0f, 0f, 0.144f * f.scale + f.at.z + 0.004f), new Vector3(0.34f, 0.05f, 0.01f)), strip);
        }

        /// <summary>The Tripo textures are 2048 px (DXT1 + normal): cap them for Quest (512 ASTC) and PC (1024).</summary>
        internal static void TrimTextures()
        {
            foreach (var name in Fits.Keys)
            {
                var g = AssetDatabase.LoadAssetAtPath<GameObject>(GenFolder + "/" + name + ".prefab");
                if (g == null) continue;
                var mat = g.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                foreach (var id in new[] { "_BaseMap", "_BumpMap" })
                {
                    var tex = mat.GetTexture(id);
                    if (tex == null || !(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) is TextureImporter ti)) continue;
                    ti.maxTextureSize = id == "_BaseMap" ? 1024 : 512;
                    var a = ti.GetPlatformTextureSettings("Android");
                    a.overridden = true; a.maxTextureSize = 512; a.format = TextureImporterFormat.ASTC_6x6;
                    ti.SetPlatformTextureSettings(a);
                    ti.SaveAndReimport();
                }
            }
        }

        /// <summary>Wires the six Tripo weapon meshes into their EW_* kits (additive: only these weapons, not the bodies or spawn table).</summary>
        [MenuItem("Tools/Space Samurai/Roguelike/Apply Tripo Weapon Meshes (additive)")]
        public static void ApplyTripoMeshes()
        {
            TrimTextures();
            foreach (var n in Fits.Keys) BuildWeapons(n);
            AssetDatabase.SaveAssets();
            Debug.Log("[EnemyWeaponBuilder] Tripo weapon meshes applied.");
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
