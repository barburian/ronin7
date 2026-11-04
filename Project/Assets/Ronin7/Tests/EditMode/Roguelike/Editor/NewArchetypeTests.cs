using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.EditorTools;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.EditMode.Roguelike.Editor
{
    /// <summary>P8-06 part 1: Duelist (weapon guard + riposte, melee kits only) and Juggernaut (windup hyper-armour,
    /// unparryable ground-slam AoE).</summary>
    public class NewArchetypeTests
    {
        const BindingFlags Bf = BindingFlags.NonPublic | BindingFlags.Instance;
        const string TablePath = "Assets/Ronin7/Data/Roguelike/EnemySpawnTable.asset";
        readonly System.Collections.Generic.List<GameObject> spawned = new();

        [SetUp] public void SetUp() { Health.Active.Clear(); EventBus.Clear(); }

        [TearDown] public void TearDown()
        {
            foreach (var g in spawned) if (g != null) Object.DestroyImmediate(g);
            spawned.Clear();
            Health.Active.Clear();
            EventBus.Clear();
        }

        T Spawn<T>(EnemyDefinition def) where T : Enemy
        {
            var go = new GameObject(typeof(T).Name);
            spawned.Add(go);
            go.SetActive(false);
            var enemy = go.AddComponent<T>();
            enemy.Configure(def);
            go.SetActive(true);
            var h = go.GetComponent<Health>();
            typeof(Health).GetMethod("Awake", Bf).Invoke(h, null);
            typeof(Health).GetMethod("OnEnable", Bf).Invoke(h, null);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("bladeTip not wired"));
            typeof(Enemy).GetMethod("Awake", Bf).Invoke(enemy, null);
            var player = new GameObject("Player");
            spawned.Add(player);
            player.transform.position = go.transform.position + go.transform.forward * 1.2f;
            typeof(MeleeAttacker).GetField("target", Bf).SetValue(enemy, player.AddComponent<Health>());
            return enemy;
        }

        static string StateOf(Enemy e) => typeof(MeleeAttacker).GetField("state", Bf).GetValue(e).ToString();
        static void Enter(Enemy e, string state)
        {
            var enumType = typeof(MeleeAttacker).GetNestedType("State", BindingFlags.NonPublic);
            typeof(MeleeAttacker).GetMethod("Enter", Bf).Invoke(e, new[] { System.Enum.Parse(enumType, state) });
        }
        static void Tick(Enemy e) => typeof(MeleeAttacker).GetMethod("Update", Bf).Invoke(e, null);

        static EnemySpawnTable LoadTable()
        {
            var t = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>(TablePath);
            Assert.IsNotNull(t, TablePath);
            return t;
        }

        static EnemySpawnTable.Entry EntryFor(EnemySpawnTable t, string rl)
        {
            var e = System.Array.Find(t.trash, x => x.definition != null && x.definition.name == "RL_" + rl);
            Assert.IsNotNull(e, "no trash entry for RL_" + rl + " - run 'Bake Archetype Bodies (additive)'");
            return e;
        }

        static DamageInfo Hit(Enemy e, Vector3 at) => new DamageInfo(20f, at, -e.transform.forward, null, DamageType.Melee, 6f);

        [Test]
        public void Duelist_GuardDeflectsHit_ThenRipostes()
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 100f; def.weakness = WeaknessKind.Recovery;
            var d = Spawn<DuelistEnemy>(def);
            var h = d.GetComponent<Health>();
            int blocked = 0;
            System.Action<HitResisted> onBlock = _ => blocked++;
            EventBus.Subscribe(onBlock);
            Enter(d, "Chase");
            Assert.IsTrue(d.Guarding, "guard is up between attacks");

            h.ApplyDamage(Hit(d, d.transform.position + d.transform.forward * 0.4f + Vector3.up));
            Assert.AreEqual(100f, h.Current, "a frontal swing into the guard deals no damage");
            Assert.AreEqual(1, blocked, "the block plays the resisted clang (sound reads without colour)");
            Assert.IsTrue(d.RipostePending);
            Assert.AreEqual("Chase", StateOf(d), "a block never staggers the duelist");

            d.RiposteAt = float.NegativeInfinity; // the short beat has passed
            Tick(d);
            Assert.AreEqual("Windup", StateOf(d), "riposte swing starts after the beat");
            Assert.IsFalse(d.RipostePending);
            Assert.IsFalse(d.Guarding, "guard drops while it attacks");

            h.ApplyDamage(Hit(d, d.transform.position + d.transform.forward * 0.4f + Vector3.up));
            Assert.Less(h.Current, 100f, "the riposte windup is open");
        }

        [Test]
        public void Duelist_GuardIsFrontalOnly()
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 100f; def.weakness = WeaknessKind.Recovery;
            var d = Spawn<DuelistEnemy>(def);
            Enter(d, "Chase");
            var h = d.GetComponent<Health>();
            h.ApplyDamage(Hit(d, d.transform.position - d.transform.forward * 0.4f + Vector3.up));
            Assert.Less(h.Current, 100f, "a hit from behind gets past the blade");
            Assert.IsFalse(d.RipostePending);
        }

        [Test]
        public void Duelist_NeverRollsRangedKit()
        {
            var t = LoadTable();
            var e = EntryFor(t, "Duelist");
            Assert.IsTrue(System.Array.Exists(t.kits, k => k.main != null && k.main.ranged), "table has ranged kits to exclude");
            Assert.IsInstanceOf<DuelistEnemy>(e.prefab.GetComponent<Enemy>());
            var rng = new RunRng(42);
            for (int i = 0; i < 500; i++)
            {
                var k = t.RollKit(e, ref rng, 0);
                Assert.IsNotNull(k.main, "the duelist always holds a weapon");
                Assert.IsFalse(k.main.ranged, "rolled " + k.main.name);
            }
        }

        [Test]
        public void Juggernaut_HyperArmour_NoStaggerInWindup()
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 200f; def.hyperArmourWindup = true; // weakness None: no resist roll, so only hyper-armour can stop the flinch
            var j = Spawn<Enemy>(def);
            var h = j.GetComponent<Health>();
            Enter(j, "Windup");
            h.ApplyDamage(new DamageInfo(20f, j.transform.position + Vector3.up, Vector3.forward, null, DamageType.Melee, 6f));
            Assert.Less(h.Current, 200f, "hyper-armour still takes damage");
            Assert.AreEqual("Windup", StateOf(j), "a hit does not interrupt the windup");

            var plain = ScriptableObject.CreateInstance<EnemyDefinition>();
            plain.maxHealth = 200f;
            var p = Spawn<Enemy>(plain);
            Enter(p, "Windup");
            p.GetComponent<Health>().ApplyDamage(new DamageInfo(20f, p.transform.position + Vector3.up, Vector3.forward, null, DamageType.Melee, 6f));
            Assert.AreEqual("Stagger", StateOf(p), "control: without hyper-armour the hit flinches the windup");

            var shipped = EntryFor(LoadTable(), "Juggernaut");
            Assert.IsTrue(shipped.definition.hyperArmourWindup);
        }

        [Test]
        public void Juggernaut_GroundSlam_IsUnparryable()
        {
            var e = EntryFor(LoadTable(), "Juggernaut");
            Assert.IsTrue(e.fixedKit);
            Assert.AreEqual("EW_HeavyAxe", e.mainHand.name);
            Assert.Greater(e.definition.groundSlamRadius, e.definition.attackRange + 0.4f, "slam reaches past a normal swing");

            var def = Object.Instantiate(e.definition);
            var body = new GameObject("jugg");
            spawned.Add(body);
            body.SetActive(false);
            new GameObject("Rig_ArmR").transform.SetParent(body.transform, false);
            var enemy = body.AddComponent<Enemy>();
            var so = new SerializedObject(enemy);
            so.FindProperty("weapon").objectReferenceValue = body.transform.GetChild(0);
            so.ApplyModifiedPropertiesWithoutUndo();
            enemy.Configure(def);
            enemy.Equip(e.mainHand, e.offHand);
            var slam = enemy.Strikes.Patterns.Find(p => p.name == Enemy.GroundSlamName);
            Assert.IsNotNull(slam, "the juggernaut adds a Ground slam pattern");
            foreach (var s in slam.strikes)
            {
                Assert.IsFalse(s.Parryable, "ground slam must be dodged");
                Assert.AreEqual(def.groundSlamRadius, s.aoeRadius);
                Assert.AreEqual(def.groundSlamRadius, Enemy.LandHitRadius(s, def.attackRange));
            }
            Assert.IsNull(e.mainHand.patterns.Find(p => p.name == Enemy.GroundSlamName), "the shared axe asset is untouched");
        }

        [Test]
        public void NewArchetypes_InSpawnTable_NotInTutorialRoom()
        {
            var t = LoadTable();
            foreach (var rl in new[] { "Duelist", "Juggernaut" })
            {
                var e = EntryFor(t, rl);
                Assert.Greater(e.minDepth, 0, rl + " must not appear in the depth-0 tutorial room");
                Assert.Greater(e.weight, 0, rl);
                Assert.IsFalse(e.eliteOnly, rl);
                Assert.AreNotEqual(WeaknessKind.None, e.definition.weakness, rl);
            }
            Assert.AreEqual(1, EntryFor(t, "Juggernaut").maxPerRoom, "one juggernaut per room");
            Assert.IsTrue(EntryFor(t, "Duelist").meleeOnly);
        }

        [Test]
        public void NewArchetypes_DistinctBodies()
        {
            var t = LoadTable();
            var fresh = new[] { EntryFor(t, "Duelist"), EntryFor(t, "Juggernaut") };
            foreach (var n in fresh)
            {
                var smr = n.prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.IsNotNull(smr, n.prefab.name);
                foreach (var o in t.trash)
                {
                    if (o == n) continue;
                    Assert.AreNotEqual(o.prefab, n.prefab, n.prefab.name);
                    var other = o.prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    Assert.AreNotEqual(other.sharedMesh, smr.sharedMesh, n.prefab.name + " shares a mesh with " + o.prefab.name);
                }
            }
            // BUG-40: the juggernaut reads as huge next to the 1.8 m roster.
            var j = (GameObject)PrefabUtility.InstantiatePrefab(fresh[1].prefab);
            try
            {
                j.SetActive(true);
                Assert.AreEqual(1.8f * EnemyPrefabBaker.JuggernautScale, EnemyPrefabBaker.ArtBounds(j).size.y, 0.05f);
            }
            finally { Object.DestroyImmediate(j); }
        }
    }
}
