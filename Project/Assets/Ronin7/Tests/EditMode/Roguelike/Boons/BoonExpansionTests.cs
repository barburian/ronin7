using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Boons
{
    /// <summary>P4-05: tags / Duos / pack locks in the picker, Status damage, procs, Last Stand, Reversal,
    /// Riposte stacks and the v1 MetaSave pack field.</summary>
    public class BoonExpansionTests
    {
        private const BindingFlags Bf = BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created)
            {
                if (o is GameObject go && go.TryGetComponent<RunBoonProcs>(out var p)) Invoke(p, "OnDisable");
                if (o != null) Object.DestroyImmediate(o);
            }
            created.Clear();
            MetaProgression.ResetForTests();
        }

        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Bf).Invoke(target, null);

        private BoonDefinition Boon(string id, BoonRarity rarity, BoonTag tags = BoonTag.None, string[] requires = null)
        {
            var b = ScriptableObject.CreateInstance<BoonDefinition>();
            b.id = id; b.rarity = rarity; b.maxStacks = 3; b.tags = tags; b.requiresTags = requires;
            created.Add(b);
            return b;
        }

        private static bool EverOffered(List<BoonDefinition> pool, System.Func<string, int> held, string id, RoomKind kind)
        {
            for (uint seed = 1; seed <= 200; seed++)
            {
                var rng = new RunRng(seed);
                foreach (var b in BoonOfferPicker.Pick(pool, held, kind, ref rng)) if (b.id == id) return true;
            }
            return false;
        }

        // ---- picker ----

        [Test]
        public void DuoIneligibleWithoutTags()
        {
            var parry = Boon("p", BoonRarity.Common, BoonTag.Parry);
            var flow = Boon("f", BoonRarity.Common, BoonTag.Flow);
            var duo = Boon("duo", BoonRarity.Duo, BoonTag.Parry | BoonTag.Flow, new[] { "Parry", "Flow" });
            var pool = new List<BoonDefinition> { parry, flow, duo, Boon("x", BoonRarity.Rare), Boon("y", BoonRarity.Epic) };

            Assert.IsFalse(EverOffered(pool, id => id == "p" ? 1 : 0, "duo", RoomKind.Boss), "one of two tags owned");
            Assert.IsTrue(EverOffered(pool, id => id == "p" || id == "f" ? 1 : 0, "duo", RoomKind.Boss), "both tags owned");
        }

        [Test]
        public void TagAffinity_RaisesWeight()
        {
            var parry = Boon("p", BoonRarity.Common, BoonTag.Parry);
            var plain = Boon("q", BoonRarity.Common);
            Assert.AreEqual(3, BoonOfferPicker.TagWeight(parry, BoonTag.Parry));
            Assert.AreEqual(2, BoonOfferPicker.TagWeight(plain, BoonTag.Parry));

            // Statistical: owning a Parry boon, a single-pick Common offer favours the other Parry Common ~3:2.
            var owned = Boon("owned", BoonRarity.Common, BoonTag.Parry);
            owned.maxStacks = 1;
            var pool = new List<BoonDefinition> { owned, parry, plain };
            int affine = 0, other = 0;
            for (uint seed = 1; seed <= 2000; seed++)
            {
                var rng = new RunRng(seed);
                var pick = BoonOfferPicker.Pick(pool, id => id == "owned" ? 1 : 0, RoomKind.Combat, ref rng, 1);
                if (pick.Count == 0) continue;
                if (pick[0] == parry) affine++; else if (pick[0] == plain) other++;
            }
            Assert.Greater(affine, other * 1.25f, $"affinity {affine} vs plain {other}");
        }

        [Test]
        public void LockedPack_NeverOffered_UntilUnlocked()
        {
            MetaProgression.ResetForTests();
            var quickDraw = Boon("qd", BoonRarity.Rare, BoonTag.Executioner);
            var pool = new List<BoonDefinition> { quickDraw, Boon("c", BoonRarity.Common), Boon("e", BoonRarity.Epic) };
            Assert.IsFalse(EverOffered(pool, _ => 0, "qd", RoomKind.Elite));
            MetaProgression.UnlockPack(BoonPackId.Executioner);
            Assert.IsTrue(EverOffered(pool, _ => 0, "qd", RoomKind.Elite));
        }

        [Test]
        public void MetaSaveV1_WithoutUnlockedPacks_LoadsDefaults()
        {
            var data = JsonUtility.FromJson<MetaSaveData>("{\"version\":1,\"metaEchoes\":5}");
            MetaProgression.ApplyFrom(data);
            Assert.AreEqual(1, data.version);
            Assert.IsNotNull(data.unlockedPacks);
            Assert.IsFalse(MetaProgression.IsPackUnlocked(BoonPackId.Bleed));
        }

        // ---- damage rules ----

        [Test]
        public void FlinchTiming_StatusNeverFlinches()
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            created.Add(def);
            var go = new GameObject("status-flinch");
            created.Add(go);
            go.SetActive(false);
            var enemy = go.AddComponent<Enemy>();
            typeof(Enemy).GetField("definition", Bf).SetValue(enemy, def);
            if (!go.TryGetComponent<PostureMeter>(out var posture)) posture = go.AddComponent<PostureMeter>();
            go.SetActive(true);
            Invoke(go.GetComponent<Health>(), "Awake");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("bladeTip not wired"));
            Invoke(enemy, "Awake");
            Invoke(posture, "Awake");
            Invoke(posture, "OnEnable");
            var state = typeof(MeleeAttacker).GetField("state", Bf);
            state.SetValue(enemy, System.Enum.Parse(state.FieldType, "Windup"));

            go.GetComponent<Health>().ApplyDamage(new DamageInfo(5f, Vector3.zero, Vector3.forward, null, DamageType.Status));

            Assert.AreEqual("Windup", state.GetValue(enemy).ToString());
            Assert.AreEqual(0f, posture.Current);
            Invoke(posture, "OnDisable");
        }

        private (RunBoonProcs procs, PlayerCombatModifiers mods, Health victim) ProcRig()
        {
            var rig = new GameObject("rig");
            created.Add(rig);
            rig.SetActive(false);
            rig.AddComponent<Health>();
            var mods = rig.AddComponent<PlayerCombatModifiers>();
            var procs = rig.AddComponent<RunBoonProcs>();
            Invoke(procs, "Awake");
            Invoke(procs, "OnEnable");

            var foe = new GameObject("foe");
            created.Add(foe);
            foe.SetActive(false); // no Enemy Awake/OnEnable: only the component's presence matters here
            foe.AddComponent<Enemy>();
            var h = foe.GetComponent<Health>();
            Invoke(h, "Awake");
            return (procs, mods, h);
        }

        private static void Hit(RunBoonProcs procs, Health victim, DamageType type)
        {
            var info = new DamageInfo(10f, Vector3.zero, Vector3.forward, procs.gameObject, type);
            EventBus.Publish(new EntityDamaged(victim.gameObject, info, victim.Current, victim.Max));
        }

        [Test]
        public void StatusDamage_DoesNotReproc()
        {
            var (procs, mods, victim) = ProcRig();
            mods.SetBoon(BoonEffectKind.Bleed, 0.1f);
            Hit(procs, victim, DamageType.Status);
            Assert.IsFalse(procs.IsBleeding(victim), "a Status tick must not start a bleed");
            Hit(procs, victim, DamageType.Melee);
            Assert.IsTrue(procs.IsBleeding(victim), "a melee hit bleeds");
        }

        [Test]
        public void ProcRateScale_DoublesBleedAndStaticStep()
        {
            Assert.AreEqual(0f, RunBoonProcs.BleedTotal(10f, 0f, 1f, false, 0));
            Assert.AreEqual(1.5f, RunBoonProcs.BleedTotal(10f, 0.1f, 1f, false, 0), 1e-5f);
            Assert.AreEqual(3f, RunBoonProcs.BleedTotal(10f, 0.1f, 2f, false, 0), 1e-5f);
            Assert.AreEqual(3f, RunBoonProcs.BleedTotal(10f, 0.1f, 1f, true, 3), 1e-5f, "Crimson Tide at combo 3");

            float m = 0f; bool fire;
            for (int i = 0; i < 3; i++) { m = RunBoonProcs.StaticMeterStep(m, 1f, out fire); Assert.IsFalse(fire); }
            m = RunBoonProcs.StaticMeterStep(m, 1f, out fire);
            Assert.IsTrue(fire); Assert.AreEqual(0f, m);
            m = RunBoonProcs.StaticMeterStep(0f, 2f, out fire); Assert.IsFalse(fire);
            RunBoonProcs.StaticMeterStep(m, 2f, out fire); Assert.IsTrue(fire, "imbued: every 2nd hit");

            Assert.AreEqual(1, RunBoonProcs.ChainJumps(0f));
            Assert.AreEqual(3, RunBoonProcs.ChainJumps(2f));
            Assert.AreEqual(7f, RunBoonProcs.ChainRange(2f), 1e-5f);

            var mods = new GameObject("m").AddComponent<PlayerCombatModifiers>();
            created.Add(mods.gameObject);
            Assert.AreEqual(1f, mods.ProcRateScale);
            mods.Imbued = ImbueElement.Storm;
            Assert.AreEqual(2f, mods.ProcRateScale);
        }

        [Test]
        public void LastStand_TogglesAt30Percent()
        {
            Assert.IsTrue(PlayerCombatModifiers.LastStandActiveAt(29f, 100f));
            Assert.IsFalse(PlayerCombatModifiers.LastStandActiveAt(30.5f, 100f));
            Assert.IsFalse(PlayerCombatModifiers.LastStandActiveAt(0f, 100f), "dead");
        }

        // ---- skills ----

        [Test]
        public void Reversal_SameTargetReversedSwing_AdvancesCombo()
        {
            var s = ComboMomentumController.RegisterHit(default, 7, 0f, 1f, 10);
            Assert.AreEqual(2, ComboMomentumController.RegisterHit(s, 7, 0.2f, 1f, 10, true).Count);
            Assert.AreEqual(0, ComboMomentumController.RegisterHit(s, 7, 0.2f, 1f, 10, false).Count);
            Assert.IsTrue(ComboMomentumController.IsReversal(Vector3.right, Vector3.left));
            Assert.IsFalse(ComboMomentumController.IsReversal(Vector3.right, Vector3.right));
            Assert.IsFalse(ComboMomentumController.IsReversal(Vector3.zero, Vector3.left));
        }

        [Test]
        public void Riposte_ScaleFor_ExtraStacksAddQuarter()
        {
            Assert.AreEqual(1.75f, RiposteLogic.ScaleFor(0f), 1e-5f);
            Assert.AreEqual(1.75f, RiposteLogic.ScaleFor(0.25f), 1e-5f);
            Assert.AreEqual(2f, RiposteLogic.ScaleFor(0.5f), 1e-5f);
        }
    }
}
