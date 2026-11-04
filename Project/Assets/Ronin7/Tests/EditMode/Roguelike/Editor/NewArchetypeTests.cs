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
    /// unparryable ground-slam AoE). Part 2: Leaper (telegraphed gap-close leap, strike, retreat) and Banner Commander
    /// (non-stacking windup buff in radius, weak in melee).</summary>
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

        // ---- P8-06 part 2: Leaper + Banner Commander ----

        static Transform PlayerOf(Enemy e) => ((Health)typeof(MeleeAttacker).GetField("target", Bf).GetValue(e)).transform;

        LeaperEnemy Leaper(float playerDist)
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 55f; def.attackRange = 1.6f; def.weakness = WeaknessKind.Recovery;
            var l = Spawn<LeaperEnemy>(def);
            PlayerOf(l).position = l.transform.position + l.transform.forward * playerDist;
            Enter(l, "Chase");
            return l;
        }

        [Test]
        public void Leaper_LeapsOnlyFrom6To8m()
        {
            foreach (float d in new[] { 6f, 7f, 8f })
                Assert.AreEqual(LeaperEnemy.Move.Leap, LeaperEnemy.Decide(d, 1.6f, true, true), d + " m");
            foreach (float d in new[] { 1f, 3f, 5.9f, 8.1f, 15f })
                Assert.AreNotEqual(LeaperEnemy.Move.Leap, LeaperEnemy.Decide(d, 1.6f, true, true), d + " m");
            Assert.AreNotEqual(LeaperEnemy.Move.Leap, LeaperEnemy.Decide(7f, 1.6f, false, true), "cooldown");

            var near = Leaper(4f); Tick(near);
            Assert.AreEqual(LeaperEnemy.Phase.None, near.LeapPhase, "4 m: too close to leap");
            var far = Leaper(12f); Tick(far);
            Assert.AreEqual(LeaperEnemy.Phase.None, far.LeapPhase, "12 m: walks in first");
            var mid = Leaper(7f); Tick(mid);
            Assert.AreEqual(LeaperEnemy.Phase.Telegraph, mid.LeapPhase, "7 m: leaps");
        }

        [Test]
        public void Leaper_LeapIsTelegraphed()
        {
            int cues = 0; bool heavy = false;
            System.Action<AttackWindupStarted> onCue = e => { cues++; heavy = e.Heavy; };
            EventBus.Subscribe(onCue);
            var l = Leaper(7f);
            Vector3 start = l.transform.position;
            Tick(l);
            Assert.AreEqual(LeaperEnemy.Phase.Telegraph, l.LeapPhase);
            Assert.AreEqual(1, cues, "the crouch plays a sound cue (reads without colour)");
            Assert.IsTrue(heavy, "the heavy growl, not the plain windup tick");
            Assert.GreaterOrEqual(LeaperEnemy.LeapTelegraph, 0.6f, "telegraph >= 0.6 s");
            Assert.AreEqual(LeaperEnemy.LeapTelegraph, l.PhaseEnd - Time.time, 1e-3f);
            Tick(l);
            Assert.AreEqual(start, l.transform.position, "holds still while it telegraphs");
            Assert.That(LeaperEnemy.LeapAirtime, Is.InRange(0.5f, 0.7f), "airtime is dodgeable");
        }

        [Test]
        public void Leaper_LandsAtStrikeRangeNotOnPlayer()
        {
            var l = Leaper(7f);
            Transform player = PlayerOf(l);
            Vector3 start = l.transform.position;
            Tick(l);                                      // telegraph
            l.PhaseEnd = float.NegativeInfinity; Tick(l); // take off
            Assert.AreEqual(LeaperEnemy.Phase.Air, l.LeapPhase);
            l.PhaseEnd = float.NegativeInfinity; Tick(l); // land
            Assert.AreEqual(LeaperEnemy.Phase.None, l.LeapPhase);
            Vector3 gap = player.position - l.transform.position; gap.y = 0f;
            Assert.AreEqual(1.6f, gap.magnitude, 0.01f, "lands at strike range");
            Assert.Greater(Vector3.Dot(gap, player.position - start), 0f, "lands on its own side of the player");
            Assert.AreEqual(start.y, l.transform.position.y, 1e-4f, "back on the ground");
            Assert.AreEqual("Windup", StateOf(l), "then strikes");

            // The whole arc stays in front of the player: never on or over their head.
            Vector3 from = new Vector3(0f, 0f, -7f), to = LeaperEnemy.LeapLanding(from, Vector3.zero, 1.6f);
            Assert.AreEqual(0f, Vector3.Distance(new Vector3(0f, 0f, -1.6f), to), 1e-4f);
            for (float t = 0f; t <= 1f; t += 0.05f)
            {
                Vector3 p = LeaperEnemy.LeapArc(from, to, t, LeaperEnemy.LeapApex);
                Assert.GreaterOrEqual(new Vector2(p.x, p.z).magnitude, 1.6f - 1e-3f, "t=" + t);
                Assert.LessOrEqual(p.y, LeaperEnemy.LeapApex + 1e-4f);
            }
        }

        [Test]
        public void Leaper_RetreatsAfterStrike()
        {
            var l = Leaper(1.6f);
            Transform player = PlayerOf(l);
            Enter(l, "Active");
            typeof(MeleeAttacker).GetMethod("OnActiveEnd", Bf).Invoke(l, null);
            Assert.IsTrue(l.RetreatPending);
            Enter(l, "Chase");
            Tick(l);
            Assert.IsTrue(l.Retreating, "skitters back after the strike");
            Assert.IsFalse(l.RetreatPending);
            Vector3 to = l.RetreatTarget - player.position; to.y = 0f;
            Assert.That(to.magnitude, Is.InRange(LeaperEnemy.LeapMin, LeaperEnemy.LeapMax), "back out to leap range");
            Assert.Less(Vector3.Dot(l.RetreatTarget - l.transform.position, player.position - l.transform.position), 0f, "away from the player");
        }

        BannerCommander Commander(Vector3 at)
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 45f; def.weakness = WeaknessKind.Recovery;
            var c = Spawn<BannerCommander>(def);
            c.transform.position = at;
            return c;
        }

        Enemy Ally(Vector3 at)
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 50f;
            var e = Spawn<Enemy>(def);
            e.transform.position = at;
            return e;
        }

        static void Kill(Enemy e) => e.GetComponent<Health>().ApplyDamage(
            new DamageInfo(999f, e.transform.position, Vector3.forward, null, DamageType.Melee, 6f));

        [Test]
        public void Banner_BuffsAlliesInRadius()
        {
            var c = Commander(Vector3.zero);
            var inside = Ally(new Vector3(BannerCommander.Radius - 0.5f, 0f, 0f));
            var outside = Ally(new Vector3(0f, 0f, BannerCommander.Radius + 1f));
            c.RallyTick();
            Assert.Less(BannerCommander.RallyWindupScale, 1f);
            Assert.AreEqual(BannerCommander.RallyWindupScale, inside.WindupScale, "ally in radius winds up faster");
            Assert.AreEqual(1f, outside.WindupScale, "ally outside radius is untouched");
            Assert.AreEqual(1f, c.WindupScale, "the commander does not buff itself");

            inside.transform.position = new Vector3(BannerCommander.Radius + 2f, 0f, 0f);
            c.RallyTick();
            Assert.AreEqual(1f, inside.WindupScale, "leaving the radius ends the buff");
        }

        [Test]
        public void Banner_BuffDoesNotStack()
        {
            var a = Commander(Vector3.zero);
            var b = Commander(new Vector3(1f, 0f, 0f));
            var ally = Ally(new Vector3(0.5f, 0f, 2f));
            a.RallyTick(); b.RallyTick(); a.RallyTick();
            Assert.AreEqual(BannerCommander.RallyWindupScale, ally.WindupScale, "two commanders = one buff, never x0.64");
            Assert.AreSame(a, ally.RalliedBy);
            Kill(a);
            b.RallyTick();
            Assert.AreSame(b, ally.RalliedBy, "the surviving commander takes over");
            Assert.AreEqual(BannerCommander.RallyWindupScale, ally.WindupScale);
        }

        [Test]
        public void Banner_BuffEndsOnDeath()
        {
            var c = Commander(Vector3.zero);
            var ally = Ally(new Vector3(2f, 0f, 0f));
            c.RallyTick();
            Assert.AreEqual(BannerCommander.RallyWindupScale, ally.WindupScale);
            Kill(c);
            Assert.IsNull(ally.RalliedBy, "released the moment it dies, no tick needed");
            Assert.AreEqual(1f, ally.WindupScale);
        }

        [Test]
        public void Banner_WeakInMelee()
        {
            var t = LoadTable();
            var e = EntryFor(t, "BannerCommander");
            var d = e.definition;
            Assert.LessOrEqual(d.maxHealth, 50f, "low HP");
            Assert.Less(d.maxHealth, EntryFor(t, "Duelist").definition.maxHealth);
            Assert.LessOrEqual(d.damage, 8f, "weak jab");
            Assert.GreaterOrEqual(d.telegraphTime, 1f, "slow jab");
            Assert.IsTrue(e.fixedKit);
            Assert.AreEqual("EW_Banner", e.mainHand.name);
            foreach (var p in e.mainHand.patterns) foreach (var s in p.strikes) Assert.GreaterOrEqual(s.windup, 1.2f, p.name);
            Assert.AreEqual(1, e.maxPerRoom, "one commander per room");
            Assert.IsInstanceOf<BannerCommander>(e.prefab.GetComponent<Enemy>());
            Assert.IsNotNull(System.Array.Find(e.mainHand.prefab.GetComponentsInChildren<Renderer>(true), r => r.name == BannerCommander.FlagName), "the banner has a flag");

            var c = Commander(Vector3.zero);
            Assert.AreEqual(1.7f, c.HoldRange, 1e-4f, "alone it closes to melee");
            Ally(new Vector3(1f, 0f, 0f));
            c.RallyTick();
            Assert.AreEqual(BannerCommander.HangBack, c.HoldRange, "with allies it hangs back");
            Assert.Greater(BannerCommander.HangBack, d.attackRange);

            var l = EntryFor(t, "Leaper");
            Assert.IsInstanceOf<LeaperEnemy>(l.prefab.GetComponent<Enemy>());
            Assert.IsTrue(l.definition.naturalWeapon, "the leaper uses its claws");
        }

        [Test]
        public void NewArchetypes_InSpawnTable_NotInTutorialRoom()
        {
            var t = LoadTable();
            foreach (var rl in new[] { "Duelist", "Juggernaut", "Leaper", "BannerCommander" })
            {
                var e = EntryFor(t, rl);
                Assert.Greater(e.minDepth, 0, rl + " must not appear in the depth-0 tutorial room");
                Assert.Greater(e.weight, 0, rl);
                Assert.IsFalse(e.eliteOnly, rl);
                Assert.AreNotEqual(WeaknessKind.None, e.definition.weakness, rl);
            }
            Assert.AreEqual(1, EntryFor(t, "Juggernaut").maxPerRoom, "one juggernaut per room");
            Assert.AreEqual(1, EntryFor(t, "BannerCommander").maxPerRoom, "one commander per room");
            Assert.IsTrue(EntryFor(t, "Duelist").meleeOnly);
        }

        [Test]
        public void NewArchetypes_DistinctBodies()
        {
            var t = LoadTable();
            var fresh = new[] { EntryFor(t, "Duelist"), EntryFor(t, "Juggernaut"), EntryFor(t, "Leaper"), EntryFor(t, "BannerCommander") };
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
