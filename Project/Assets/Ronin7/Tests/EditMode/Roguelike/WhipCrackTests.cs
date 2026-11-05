using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.EditorTools;
using Ronin7.Flow;
using Ronin7.Ship;
using Ronin7.Player;
using Ronin7.World;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P9-02e (A15.8, R2, R5): the tip crack, its windup cancel, the tip bend, Parry boons hidden on a Whip run, the tutorial.</summary>
    public class WhipCrackTests
    {
        private const BindingFlags NonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Flick = 12f; // m/s, a deliberate flick

        private GameObject rig;
        private WhipController whip;
        private readonly List<Object> made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            MetaProgression.ResetForTests();
            RunState.Reset();
            rig = new GameObject("Rig");
            XRRigBuilder.RogueEnsureWhip(rig.transform);
            whip = rig.GetComponent<PolearmShieldGear>().whip.GetComponent<WhipController>();
            whip.SetHeld(true);
            whip.Step(1f / 90f);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            MetaProgression.DevUnlockAllWeapons = false;
            MetaProgression.ResetForTests();
            RunState.Reset();
            Object.DestroyImmediate(rig);
            foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        // The chain laid out along +x at head height, tip at x = 4: the tip is index 9, the zone is indices 8 and 9.
        private void PlaceChain()
        {
            var p = whip.Chain.Positions;
            for (int i = 0; i < p.Length; i++) p[i] = new Vector3(i * VerletChain.TotalLength / (p.Length - 1), 1f, 0f);
            Physics.SyncTransforms();
        }

        private Vector3 PointOf(int i) => whip.Chain.Positions[i];

        private Health Victim(Vector3 at, float size = 0.2f, float hp = 100f)
        {
            var go = new GameObject("Victim", typeof(BoxCollider), typeof(Health));
            made.Add(go);
            go.transform.position = at;
            go.GetComponent<BoxCollider>().size = Vector3.one * size;
            Physics.SyncTransforms();
            var h = go.GetComponent<Health>();
            h.Configure(hp);
            return h;
        }

        private void StepFor(float seconds)
        {
            int n = Mathf.CeilToInt(seconds * 90f);
            for (int i = 0; i < n; i++) whip.Step(1f / 90f);
        }

        private static Vector3 Along => Vector3.right * Flick;

        // --- crack ---

        [Test]
        public void Constants_AreA15()
        {
            Assert.AreEqual(25f, WhipController.CrackDamage);
            Assert.AreEqual(0.5f, WhipController.CrackZone);
            Assert.AreEqual(0.6f, WhipController.CrackCooldown);
            Assert.Greater(WhipController.CrackSpeed, WhipController.SwingSpeed);
        }

        [Test]
        public void Threshold_BelowDoesNothing_AtItCracks()
        {
            PlaceChain();
            var h = Victim(PointOf(9));
            Assert.IsFalse(whip.TryCrackScan(Vector3.right * (WhipController.CrackSpeed - 0.1f)));
            Assert.AreEqual(100f, h.Current, 1e-4f);
            Assert.IsTrue(whip.TryCrackScan(Vector3.right * WhipController.CrackSpeed));
            Assert.AreEqual(75f, h.Current, 1e-4f);
        }

        [Test]
        public void Threshold_ADeliberateFlickReachesIt_SteadyDragDoesNot()
        {
            // A quick 1.2 m stroke (18 m/s hand) and a dead stop: the chain straightens and the tip snaps past the threshold
            // (measured 10.7 m/s about 30 frames after the stop; a 0.1 m/frame stroke only reached 3.6).
            float flickTip = 0f;
            rig.transform.position = Vector3.zero;
            whip.SetHeld(false); whip.SetHeld(true); whip.Step(1f / 90f);
            for (int f = 0; f < 46; f++)
            {
                if (f < 6) rig.transform.position += Vector3.right * 0.2f;
                whip.Step(1f / 90f);
                flickTip = Mathf.Max(flickTip, whip.Chain.Velocity(whip.Chain.Count - 1).magnitude);
            }
            Assert.GreaterOrEqual(flickTip, WhipController.CrackSpeed, "a stroke and a stop reaches the crack speed");

            rig.transform.position = Vector3.zero;
            whip.SetHeld(false); whip.SetHeld(true); whip.Step(1f / 90f);
            float drag = 0f;
            for (int f = 0; f < 200; f++) { rig.transform.position += Vector3.right * 0.04f; whip.Step(1f / 90f); drag = Mathf.Max(drag, whip.Chain.Velocity(whip.Chain.Count - 1).magnitude); }
            Assert.Less(drag, WhipController.CrackSpeed, "a steady 3.6 m/s drag never cracks (measured 3.3)");
        }

        [Test]
        public void Zone_LastHalfMetreHits_EarlierDoesNot()
        {
            PlaceChain();
            var h = Victim(PointOf(7)); // 0.89 m from the tip
            Assert.IsFalse(whip.TryCrackScan(Along));
            Assert.AreEqual(100f, h.Current, 1e-4f);
            h.transform.position = PointOf(8); // 0.44 m
            Physics.SyncTransforms();
            Assert.IsTrue(whip.TryCrackScan(Along));
            Assert.AreEqual(75f, h.Current, 1e-4f);
        }

        [Test]
        public void Crack_Is25Flat_TimesTheMultiplier_Melee_RealTipSpeed_FromTheRig()
        {
            PlaceChain();
            var h = Victim(PointOf(9));
            var mods = rig.AddComponent<PlayerCombatModifiers>();
            mods.OverdriveMultiplier = 2f;
            DamageInfo got = default;
            Action<EntityDamaged> on = e => got = e.Info;
            EventBus.Subscribe(on);
            try { Assert.IsTrue(whip.TryCrackScan(Vector3.right * 13f)); }
            finally { EventBus.Unsubscribe(on); }
            Assert.AreEqual(50f, h.Current, 1e-4f, "25 x 2");
            Assert.AreEqual(50f, got.Amount, 1e-4f);
            Assert.AreEqual(DamageType.Melee, got.Type);
            Assert.AreEqual(13f, got.Speed, 1e-3f, "the real tip speed");
            Assert.AreSame(rig, got.Source);
        }

        [Test]
        public void Crack_SpendsRiposte_AndResetsThePostureScale()
        {
            PlaceChain();
            Victim(PointOf(9));
            var mods = rig.AddComponent<PlayerCombatModifiers>();
            int asked = 0;
            mods.RiposteBonus = (GameObject v, out float p) => { asked++; p = 0.5f; return 1f; };
            float during = -1f;
            Action<EntityDamaged> on = e => during = mods.HitPostureScale;
            EventBus.Subscribe(on);
            try { whip.TryCrackScan(Along); }
            finally { EventBus.Unsubscribe(on); }
            Assert.AreEqual(1, asked, "a real strike spends it");
            Assert.AreEqual(0.5f, during, 1e-4f, "set while the damage applies");
            Assert.AreEqual(1f, mods.HitPostureScale, "reset after the hit");
        }

        [Test]
        public void Cooldown_IsGlobal_RealTime_StartsOnlyWhenACrackLands()
        {
            PlaceChain();
            Assert.IsFalse(whip.TryCrackScan(Along), "nothing there: no crack, no cooldown");
            var a = Victim(PointOf(9));
            Assert.IsTrue(whip.TryCrackScan(Along));
            Assert.AreEqual(75f, a.Current, 1e-4f, "first");
            a.transform.position = new Vector3(0f, -50f, 0f);
            var b = Victim(PointOf(9));
            Assert.IsFalse(whip.TryCrackScan(Along), "global: another target waits too");
            Assert.AreEqual(100f, b.Current, 1e-4f);

            Time.timeScale = 0.3f; // Overdrive does not slow it
            b.transform.position = new Vector3(0f, -50f, 0f); // the stepping below must not crack it by itself
            Physics.SyncTransforms();
            StepFor(WhipController.CrackCooldown - 0.1f);
            PlaceChain();
            b.transform.position = PointOf(9);
            Physics.SyncTransforms();
            Assert.IsFalse(whip.TryCrackScan(Along), "still cooling");
            b.transform.position = new Vector3(0f, -50f, 0f);
            Physics.SyncTransforms();
            StepFor(0.2f);
            PlaceChain();
            b.transform.position = PointOf(9);
            Physics.SyncTransforms();
            Assert.IsTrue(whip.TryCrackScan(Along), "0.6 s of real time has passed");
            Assert.AreEqual(75f, b.Current, 1e-4f, "a=" + a.Current + " b=" + b.Current);
        }

        [Test]
        public void OneNearestTarget_PerCrack()
        {
            PlaceChain();
            var far = Victim(PointOf(8));
            var near = Victim(PointOf(9) + Vector3.right * 0.02f);
            Assert.IsTrue(whip.TryCrackScan(Along));
            Assert.AreEqual(75f, near.Current, 1e-4f);
            Assert.AreEqual(100f, far.Current, 1e-4f);
        }

        [Test]
        public void NoBodyHit_OnTheCrackedEnemy_RightAfter()
        {
            PlaceChain();
            var h = Victim(PointOf(9));
            whip.TryCrackScan(Along);
            Assert.IsFalse(whip.TryBodyHit(h.GetComponent<Collider>(), PointOf(9), Vector3.right, 10f), "R6 cooldown starts on the crack");
            Assert.AreEqual(75f, h.Current, 1e-4f);
        }

        [Test]
        public void Crack_SkipsTheRig()
        {
            PlaceChain();
            var own = rig.AddComponent<Health>();
            own.Configure(50f);
            rig.AddComponent<BoxCollider>().size = Vector3.one * 20f;
            Physics.SyncTransforms();
            Assert.IsFalse(whip.TryCrackScan(Along));
            Assert.AreEqual(50f, own.Current, 1e-4f);
        }

        [Test]
        public void Crack_WarmedFrame_AllocatesNothing()
        {
            PlaceChain();
            Victim(PointOf(9), 0.2f, 1000000f);
            Assert.IsTrue(whip.TryCrackScan(Along));
            StepFor(0.7f);
            PlaceChain();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool cracked = whip.TryCrackScan(Along);
            long used = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(cracked);
            Assert.AreEqual(0L, used);
        }

        [Test]
        public void Crack_BuildsOverdriveAndCombo_UnlikeTheBody()
        {
            PlaceChain();
            var h = Victim(PointOf(9));
            SwordImpact seen = default;
            Action<SwordImpact> on = e => seen = e;
            EventBus.Subscribe(on);
            try { whip.TryCrackScan(Along); }
            finally { EventBus.Unsubscribe(on); }
            Assert.IsFalse(seen.FeedbackOnly, "R5: the crack credits");
            Assert.AreSame(h.gameObject, seen.Victim);
            Assert.AreEqual(Flick, seen.Speed, 1e-3f);

            var go = new GameObject("Credit", typeof(OverdriveController), typeof(ComboMomentumController));
            made.Add(go);
            var od = go.GetComponent<OverdriveController>();
            var combo = go.GetComponent<ComboMomentumController>();
            typeof(OverdriveController).GetField("logic", NonPublic).SetValue(od, new OverdriveLogic(0.1f, 0.1f, 0.5f));
            typeof(OverdriveController).GetMethod("OnSwordImpact", NonPublic).Invoke(od, new object[] { seen });
            typeof(ComboMomentumController).GetMethod("OnSwordImpact", NonPublic).Invoke(combo, new object[] { seen });
            Assert.Greater(od.Charge, 0f);
            Assert.AreEqual(1, ((ComboState)typeof(ComboMomentumController).GetField("state", NonPublic).GetValue(combo)).Count);
        }

        [Test]
        public void Step_CracksOnItsOwn_EvenWithBodyDamageCut()
        {
            var so = new UnityEditor.SerializedObject(whip);
            so.FindProperty("bodyDamage").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            // A stroke that stops dead, with a big victim around everything.
            rig.transform.position = Vector3.zero;
            whip.SetHeld(false); whip.SetHeld(true); whip.Step(1f / 90f);
            var h = Victim(new Vector3(1.6f, 0.8f, 0f), 6f, 1000f);
            for (int f = 0; f < 46; f++)
            {
                if (f < 6) rig.transform.position += Vector3.right * 0.2f;
                Physics.SyncTransforms();
                whip.Step(1f / 90f);
            }
            Assert.AreEqual(0, whip.LastChecks, "body checks are off");
            Assert.Less(h.Current, 1000f, "the crack landed regardless");
            Assert.AreEqual(0f, (1000f - h.Current) % 25f, 1e-3f);
        }

        // --- enemies ---

        private Enemy MakeEnemy(Action<EnemyDefinition> setup, string state)
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            setup(def);
            var go = new GameObject("crack-enemy", typeof(BoxCollider));
            go.SetActive(false);
            var enemy = go.AddComponent<Enemy>();
            made.Add(go); made.Add(def);
            typeof(Enemy).GetField("definition", NonPublic).SetValue(enemy, def);
            go.SetActive(true);
            go.transform.position = new Vector3(4f, 1f, 0f);
            go.GetComponent<BoxCollider>().size = Vector3.one * 0.6f;
            typeof(Health).GetMethod("Awake", NonPublic).Invoke(go.GetComponent<Health>(), null);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("bladeTip not wired"));
            typeof(Enemy).GetMethod("Awake", NonPublic).Invoke(enemy, null);
            var f = typeof(MeleeAttacker).GetField("state", NonPublic);
            f.SetValue(enemy, Enum.Parse(f.FieldType, state));
            Physics.SyncTransforms();
            return enemy;
        }

        private static string StateOf(Enemy e) => typeof(MeleeAttacker).GetField("state", NonPublic).GetValue(e).ToString();

        // The enemy's centre is the chain tip (x = 4).
        private string CrackIn(string state, Action<EnemyDefinition> setup)
        {
            PlaceChain();
            var e = MakeEnemy(setup, state);
            Assert.IsTrue(whip.TryCrackScan(Along));
            return StateOf(e);
        }

        [Test] public void Windup_NormalEnemy_IsCancelled() => Assert.AreEqual("Stagger", CrackIn("Windup", d => { }));

        [Test]
        public void Windup_ResistedHit_IsStillCancelled_ByTheCrack_NotByTheHitItself()
        {
            Action<EnemyDefinition> resisted = d => d.weakness = WeaknessKind.Recovery; // an attacking Recovery enemy resists
            var e = MakeEnemy(resisted, "Windup");
            e.GetComponent<Health>().ApplyDamage(new DamageInfo(1f, Vector3.zero, Vector3.forward, null));
            Assert.AreEqual("Windup", StateOf(e), "premise: a Resisted hit alone does not cancel");
            Object.DestroyImmediate(e.gameObject);
            Assert.AreEqual("Stagger", CrackIn("Windup", resisted));
        }

        [Test] public void Windup_SuperArmor_Holds() => Assert.AreEqual("Windup", CrackIn("Windup", d => d.superArmor = true));
        [Test] public void Windup_HyperArmourWindup_Holds() => Assert.AreEqual("Windup", CrackIn("Windup", d => d.hyperArmourWindup = true));
        [Test] public void Active_IsNeverCancelled() => Assert.AreEqual("Active", CrackIn("Active", d => { }));

        [Test]
        public void Windup_DoesNotDoubleEnterStagger()
        {
            PlaceChain();
            var e = MakeEnemy(d => { }, "Windup");
            var immune = typeof(MeleeAttacker).GetField("flinchImmuneUntil", NonPublic);
            Assert.IsTrue(whip.TryCrackScan(Along));
            Assert.AreEqual("Stagger", StateOf(e));
            float until = (float)immune.GetValue(e);
            Assert.IsFalse(e.CancelWindup(Vector3.forward), "already staggered: nothing to cancel");
            Assert.AreEqual(until, (float)immune.GetValue(e), "the immunity timer is not re-armed");
        }

        [Test]
        public void HeavyBreak_Exploited_AtTipSpeed_AndStaggers()
        {
            PlaceChain();
            var e = MakeEnemy(d => { d.weakness = WeaknessKind.HeavyBreak; d.heavySwingSpeed = 5.5f; }, "Idle");
            whip.Chain.Positions[9] += Vector3.forward * 0.1f; // in front of it (it faces +z), not "behind"
            Physics.SyncTransforms();
            int exploited = 0;
            Action<WeaknessExploited> on = _ => exploited++;
            EventBus.Subscribe(on);
            try { Assert.IsTrue(whip.TryCrackScan(Along)); }
            finally { EventBus.Unsubscribe(on); }
            Assert.AreEqual(1, exploited, "a crack counts as heavy");
            Assert.AreEqual("Stagger", StateOf(e));
        }

        // --- bend ---

        [Test]
        public void Bend_IsBoundedTo10Degrees_KeepsLengthAndVelocity()
        {
            var c = new VerletChain();
            c.Reset(new Vector3(0f, 3f, 0f), Vector3.forward, 0f);
            int tip = c.Count - 1;
            Vector3 root = c.Positions[tip - 1];
            Vector3 link = c.Positions[tip] - root;
            Vector3 vBefore = c.Velocity(tip);
            c.BendTip(root + Vector3.right * 5f);
            Vector3 after = c.Positions[tip] - root;
            Assert.AreEqual(VerletChain.MaxTipBendDegrees, Vector3.Angle(link, after), 0.01f);
            Assert.AreEqual(link.magnitude, after.magnitude, 1e-4f);
            Assert.AreEqual(vBefore, c.Velocity(tip));

            // Already nearly on target: it only turns as far as needed.
            Vector3 near = Quaternion.AngleAxis(3f, Vector3.right) * after;
            c.BendTip(root + near * 5f);
            Assert.AreEqual(3f, Vector3.Angle(c.Positions[tip] - root, after), 0.05f);
            Assert.AreEqual(10f, VerletChain.MaxTipBendDegrees);
        }

        [Test]
        public void Bend_Degenerate_IsIgnored_AndChainStaysStable()
        {
            var c = new VerletChain();
            c.Reset(new Vector3(0f, 3f, 0f), Vector3.forward, 0f);
            c.BendTip(c.Positions[c.Count - 2]); // zero-length aim
            for (int f = 0; f < 90 * 3; f++)
            {
                c.Tick(new Vector3(Mathf.Sin(f / 15f), 3f, 0f), 1f / 90f, 0f);
                if (f % 20 == 0) c.BendTip(new Vector3(Mathf.Cos(f), 1f, Mathf.Sin(f)) * 3f);
                float len = 0f;
                for (int i = 1; i < c.Count; i++)
                {
                    var p = c.Positions[i];
                    Assert.IsFalse(float.IsNaN(p.x + p.y + p.z) || float.IsInfinity(p.x + p.y + p.z));
                    len += Vector3.Distance(p, c.Positions[i - 1]);
                }
                Assert.LessOrEqual(len, VerletChain.TotalLength + 0.3f, "frame " + f);
                Assert.LessOrEqual(c.MaxSpeed, VerletChain.MaxPointSpeed * 3f);
            }
        }

        [Test]
        public void Crack_BendsTheTipTowardTheTarget()
        {
            PlaceChain();
            Victim(PointOf(9) + Vector3.up * 0.05f + Vector3.forward * 0.3f, 0.6f);
            Vector3 root = PointOf(8);
            Vector3 before = PointOf(9) - root;
            Assert.IsTrue(whip.TryCrackScan(Along));
            float turned = Vector3.Angle(before, PointOf(9) - root);
            Assert.Greater(turned, 0.1f);
            Assert.LessOrEqual(turned, VerletChain.MaxTipBendDegrees + 0.01f);
        }

        // --- boons ---

        private BoonDefinition Boon(string id, BoonRarity rarity, BoonTag tags, params string[] requires)
        {
            var b = ScriptableObject.CreateInstance<BoonDefinition>();
            b.id = id; b.rarity = rarity; b.maxStacks = 1; b.tags = tags; b.requiresTags = requires;
            made.Add(b);
            return b;
        }

        private static void StartRun(string loadout)
        {
            MetaProgression.DevUnlockAllWeapons = true;
            Assert.IsTrue(MetaProgression.TrySelectLoadout(loadout));
            RunState.Begin(1u);
            Assert.AreEqual(loadout, RunState.LoadoutId);
        }

        private static List<BoonDefinition> Offers(IReadOnlyList<BoonDefinition> pool, Func<string, int> held)
        {
            var seen = new HashSet<BoonDefinition>();
            for (uint s = 1; s <= 40; s++)
                foreach (var kind in new[] { RoomKind.Combat, RoomKind.Treasure, RoomKind.Shop })
                {
                    var rng = new RunRng(s);
                    foreach (var b in BoonOfferPicker.Pick(pool, held, kind, ref rng, 3)) seen.Add(b);
                }
            return new List<BoonDefinition>(seen);
        }

        private List<BoonDefinition> Pool() => new List<BoonDefinition>
        {
            Boon("plain", BoonRarity.Common, BoonTag.None),
            Boon("plain2", BoonRarity.Common, BoonTag.Flow),
            Boon("parry_c", BoonRarity.Common, BoonTag.Parry),
            Boon("parry_r", BoonRarity.Rare, BoonTag.Parry | BoonTag.Flow),
            Boon("parry_e", BoonRarity.Epic, BoonTag.Parry),
        };

        [Test]
        public void ParryBoons_AreHiddenOnAWhipRun_EveryPath()
        {
            StartRun(WeaponId.Whip);
            var pool = Pool();
            foreach (var b in Offers(pool, _ => 0)) Assert.AreEqual(BoonTag.None, b.tags & BoonTag.Parry, b.id);
            var rng = new RunRng(3);
            for (int i = 0; i < 20; i++)
            {
                var one = BoonOfferPicker.PickOfRarity(pool, _ => 0, BoonRarity.Common, ref rng);
                Assert.IsNotNull(one);
                Assert.AreEqual(BoonTag.None, one.tags & BoonTag.Parry);
            }
            Assert.IsNull(BoonOfferPicker.PickOfRarity(pool, _ => 0, BoonRarity.Epic, ref rng), "the only Epic was a Parry boon");
        }

        [Test]
        public void ParryBoons_AreStillOffered_OnKatana()
        {
            StartRun(WeaponId.Katana);
            var ids = new HashSet<string>();
            foreach (var b in Offers(Pool(), _ => 0)) ids.Add(b.id);
            Assert.IsTrue(ids.Contains("parry_c") && ids.Contains("parry_r") && ids.Contains("parry_e"), string.Join(",", ids));
        }

        [Test]
        public void ParryDuo_IsHiddenOnWhip_OfferedOnKatana()
        {
            var held = new Dictionary<string, int> { { "parry_c", 1 }, { "gate", 1 } };
            int Stacks(string id) => held.TryGetValue(id, out int n) ? n : 0;
            var pool = new List<BoonDefinition>
            {
                Boon("parry_c", BoonRarity.Common, BoonTag.Parry),
                Boon("gate", BoonRarity.Common, BoonTag.Ability),
                Boon("duo", BoonRarity.Duo, BoonTag.Parry | BoonTag.Ability, "Parry", "Ability"),
                Boon("duo_plain", BoonRarity.Duo, BoonTag.Flow, "Ability"),
            };
            var rng = new RunRng(1);
            StartRun(WeaponId.Katana);
            var ids = new HashSet<string>();
            for (int i = 0; i < 30; i++) ids.Add(BoonOfferPicker.PickOfRarity(pool, Stacks, BoonRarity.Duo, ref rng)?.id ?? "none");
            Assert.IsTrue(ids.Contains("duo"), string.Join(",", ids));

            StartRun(WeaponId.Whip);
            ids.Clear();
            for (int i = 0; i < 30; i++) ids.Add(BoonOfferPicker.PickOfRarity(pool, Stacks, BoonRarity.Duo, ref rng)?.id ?? "none");
            Assert.IsFalse(ids.Contains("duo"), "the Parry Duo is hidden");
            Assert.IsTrue(ids.Contains("duo_plain"), "a Duo without Parry is not");
        }

        [Test]
        public void TheRealCatalog_NoParryTaggedBoonSurvivesOnWhip()
        {
            StartRun(WeaponId.Whip);
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<BoonCatalog>("Assets/Ronin7/Data/Boons/BoonCatalog.asset");
            Assert.IsNotNull(catalog);
            var offers = Offers(catalog.boons, _ => 0);
            Assert.Greater(offers.Count, 0);
            foreach (var b in offers) Assert.AreEqual(BoonTag.None, b.tags & BoonTag.Parry, b.id);
        }

        // --- tutorial ---

        [Test]
        public void Tutorial_OnWhip_NeverShowsParry_AndCannotSoftLock()
        {
            var s = TutorialPrompt.Advance(TutorialPrompt.Step.Draw, TutorialPrompt.Trigger.Grabbed, false);
            Assert.AreEqual(TutorialPrompt.Step.Swing, s);
            s = TutorialPrompt.Advance(s, TutorialPrompt.Trigger.Impact, false);
            Assert.AreEqual(TutorialPrompt.Step.Swing, s, "a hit does not move a Whip run to the parry prompt");
            Assert.AreEqual(TutorialPrompt.Step.Swing, TutorialPrompt.Advance(TutorialPrompt.Step.Draw, TutorialPrompt.Trigger.Impact, false));
            s = TutorialPrompt.Advance(s, TutorialPrompt.Trigger.Cleared, false);
            Assert.AreEqual(TutorialPrompt.Step.Choose, s, "clearing the room still moves on");
            s = TutorialPrompt.Advance(s, TutorialPrompt.Trigger.BoonChosen, false);
            Assert.AreEqual(TutorialPrompt.Step.Done, s);
            Assert.AreEqual(TutorialPrompt.Step.Parry, TutorialPrompt.Advance(TutorialPrompt.Step.Swing, TutorialPrompt.Trigger.Impact), "Katana unchanged");
        }
    }
}
