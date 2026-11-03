using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Editor
{
    /// <summary>P8-05: HeavyBreak follows the shield, the shield wears the body's armour material, and its weak spot
    /// (a bright flank strip) is where a light hit still breaks the guard. Plus the no-magenta prefab audit.</summary>
    public class ShieldTests
    {
        private const string SpawnTablePath = "Assets/Ronin7/Data/Roguelike/EnemySpawnTable.asset";
        private const string Weapons = "Assets/Ronin7/Data/Roguelike/Weapons/EW_";

        private GameObject go;
        private EnemyDefinition copy;
        private GameSettings savedSettings;

        [SetUp] public void SetUp() => savedSettings = GameSettings.Active;

        [TearDown]
        public void TearDown()
        {
            GameSettings.Active = savedSettings;
            if (go != null) Object.DestroyImmediate(go);
            if (copy != null) Object.DestroyImmediate(copy);
        }

        private static EnemyWeaponDefinition W(string n) => AssetDatabase.LoadAssetAtPath<EnemyWeaponDefinition>(Weapons + n + ".asset");

        private static EnemySpawnTable.Entry Entry(string prefab)
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>(SpawnTablePath);
            if (table == null) Assert.Ignore("Spawn table not built.");
            foreach (var e in table.trash) if (e.prefab.name == prefab) return e;
            Assert.Fail(prefab + " not in the spawn table");
            return null;
        }

        // The spawner's order (A3.1): instantiate inactive -> Configure(per-spawn copy) -> Equip -> activate.
        private Enemy Spawn(string prefab, string main, string off)
        {
            var e = Entry(prefab);
            go = Object.Instantiate(e.prefab);
            copy = Object.Instantiate(e.definition);
            var enemy = go.GetComponent<Enemy>();
            enemy.Configure(copy);
            enemy.Equip(W(main), off != null ? W(off) : null);
            go.SetActive(true);
            // Edit mode skips Awake: run it by hand, as WeaknessRulesTests.Spawn does.
            const System.Reflection.BindingFlags Bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(Health).GetMethod("Awake", Bf).Invoke(go.GetComponent<Health>(), null);
            var posture = go.GetComponent<PostureMeter>();
            if (posture != null)
            {
                typeof(PostureMeter).GetMethod("Awake", Bf).Invoke(posture, null);
                typeof(PostureMeter).GetMethod("OnEnable", Bf).Invoke(posture, null);
            }
            typeof(Enemy).GetMethod("Awake", Bf).Invoke(enemy, null);
            return enemy;
        }

        private Renderer Find(string name)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (r.name == name) return r;
            return null;
        }

        private static Color BaseColor(Renderer r)
        {
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            return mpb.GetColor("_BaseColor");
        }

        [Test]
        public void HeavyBreak_FollowsShield()
        {
            var trooper = Entry("Dominion_Trooper").definition;
            Assert.AreNotEqual(WeaknessKind.HeavyBreak, trooper.weakness, "the Trooper body must not carry HeavyBreak itself");

            Assert.AreEqual(WeaknessKind.HeavyBreak, Spawn("Dominion_Trooper", "Baton", "Shield").Weakness);
            Assert.IsNotNull(go.GetComponent<Health>().DamageFilter, "the shield guard needs the weakness filter");
            TearDown();

            Assert.AreEqual(WeaknessKind.HeavyBreak, Spawn("Ash-World_Scavenger", "Spear", "Shield").Weakness, "any shield-kit body");
            TearDown();

            Assert.AreEqual(WeaknessKind.None, Spawn("Dominion_Trooper", "Sword", null).Weakness, "no shield, no HeavyBreak");
            Assert.AreNotEqual(WeaknessKind.HeavyBreak, trooper.weakness, "Equip must not touch the shared asset");
        }

        [Test]
        public void Shield_UsesWearerArmourFamily()
        {
            var enemy = Spawn("Dominion_Trooper", "Baton", "Shield");
            var shield = Find("Shield");
            Assert.IsNotNull(shield);
            Assert.IsNotNull(enemy.BodyRenderer);
            Assert.AreSame(enemy.BodyRenderer.sharedMaterial, shield.sharedMaterial, "shield must share the body's armour material");
            Assert.IsTrue(AssetDatabase.Contains(shield.sharedMaterial), "a shared asset, not a runtime instance");
            Assert.IsFalse(shield.HasPropertyBlock(), "no MPB tint on the shield (keeps it SRP-batched with the body)");
        }

        [Test]
        public void Shield_HasWeakSpot_HookedToWeakness()
        {
            var enemy = Spawn("Dominion_Trooper", "Baton", "Shield");
            var spot = Find("WeakSpot");
            Assert.IsNotNull(spot, "shield needs a WeakSpot renderer");
            Assert.IsTrue(spot.enabled);
            var health = go.GetComponent<Health>();
            float before = health.Current;
            health.ApplyDamage(new DamageInfo(10f, spot.bounds.center, Vector3.back, null, DamageType.Melee, 0.5f));
            Assert.AreEqual(10f * WeaknessRules.ShieldExploit, before - health.Current, 1e-3f, "a light hit on the weak spot is exploited");
            TearDown();

            enemy = Spawn("Dominion_Trooper", "Baton", "Shield");
            spot = Find("WeakSpot");
            health = go.GetComponent<Health>();
            Vector3 centre = go.transform.position + go.transform.forward * 0.3f + Vector3.up * 1.3f;
            Assume.That(WeaknessRules.OnWeakSpot(spot.bounds, centre), Is.False);
            before = health.Current;
            health.ApplyDamage(new DamageInfo(10f, centre, Vector3.back, null, DamageType.Melee, 0.5f));
            Assert.AreEqual(10f * WeaknessRules.ShieldResist, before - health.Current, 1e-3f, "a light centre hit is guarded");
        }

        [Test]
        public void WeakSpot_ReduceFlashes_NoPulse()
        {
            var s = GameSettings.Active; s.ReduceFlashes = true; GameSettings.Active = s;
            var enemy = Spawn("Dominion_Trooper", "Baton", "Shield");
            var spot = Find("WeakSpot");
            Color rest = BaseColor(spot);
            Vector3 scale = spot.transform.localScale;
            Assert.Greater(rest.maxColorComponent, 1f, "the weak spot is a bright HDR strip (reads without colour)");
            var tick = typeof(Enemy).GetMethod("TickWeakMarks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            enemy.PulseWeakPoint(1f);
            var health = go.GetComponent<Health>();
            health.ApplyDamage(new DamageInfo(1f, go.transform.position + go.transform.forward * 0.3f + Vector3.up * 1.3f,
                Vector3.back, null, DamageType.Melee, 0.5f)); // resisted: would flicker the guard
            for (int i = 0; i < 5; i++)
            {
                tick.Invoke(enemy, null);
                Assert.AreEqual(rest, BaseColor(spot), "weak spot brightness must stay steady with Reduce Flashes");
                Assert.AreEqual(scale, spot.transform.localScale);
            }
        }

        [Test]
        public void NoMagentaMaterials_OnEnemyPrefabs()
        {
            var bad = new System.Collections.Generic.List<string>();
            foreach (var folder in new[] { "Assets/Ronin7/Prefabs/Roguelike/Enemies", "Assets/Ronin7/Prefabs/Roguelike/Weapons" })
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                        foreach (var m in r.sharedMaterials)
                        {
                            string sh = m != null && m.shader != null ? m.shader.name : null;
                            bool ok = sh != null && m.shader.isSupported
                                && (sh.StartsWith("Universal Render Pipeline/") || sh.StartsWith("Ronin7/") || sh.StartsWith("Shader Graphs/"));
                            if (!ok) bad.Add($"{prefab.name}/{r.name}: {(m == null ? "null material" : m.name + " (" + sh + ")")}");
                        }
                }
            Assert.IsEmpty(bad, "magenta (missing/non-URP) materials - run Compose Enemy Weapons (additive):\n" + string.Join("\n", bad));
        }
    }
}
