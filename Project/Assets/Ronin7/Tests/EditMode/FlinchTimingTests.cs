using System.Reflection;
using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class FlinchTimingTests
    {
        [Test] public void IdleHit_Flinches()
            => Assert.IsTrue(FlinchTiming.ShouldFlinch(true, false, false, false, true, 1f, 0f));

        [Test] public void Dead_NoFlinch()
            => Assert.IsFalse(FlinchTiming.ShouldFlinch(false, false, false, false, true, 1f, 0f));

        [Test] public void AlreadyStaggered_NoFlinch()
            => Assert.IsFalse(FlinchTiming.ShouldFlinch(true, true, false, false, true, 1f, 0f));

        [Test] public void ActiveStrike_NoFlinch()
            => Assert.IsFalse(FlinchTiming.ShouldFlinch(true, false, true, false, true, 1f, 0f));

        [Test] public void Windup_CancelsWhenAllowed()
            => Assert.IsTrue(FlinchTiming.ShouldFlinch(true, false, false, true, true, 1f, 0f));

        [Test] public void Windup_IgnoredWhenCancelDisabled()
            => Assert.IsFalse(FlinchTiming.ShouldFlinch(true, false, false, true, false, 1f, 0f));

        // BUG-11: super-armor definitions keep their windup when hit; trash still cancels.
        static string HitDuringWindup(bool superArmor)
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.superArmor = superArmor;
            var go = new GameObject("flinch-test");
            go.SetActive(false);
            var enemy = go.AddComponent<Enemy>();
            typeof(Enemy).GetField("definition", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(enemy, def);
            go.SetActive(true);
            // EditMode doesn't run Awake on non-ExecuteAlways behaviours; call them in lifecycle order.
            const BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(Ronin7.Combat.Health).GetMethod("Awake", bf).Invoke(go.GetComponent<Ronin7.Combat.Health>(), null);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("bladeTip not wired"));
            typeof(Enemy).GetMethod("Awake", bf).Invoke(enemy, null);
            var f = typeof(MeleeAttacker).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
            f.SetValue(enemy, System.Enum.Parse(f.FieldType, "Windup"));
            go.GetComponent<Ronin7.Combat.Health>().ApplyDamage(new DamageInfo(1f, Vector3.zero, Vector3.forward, null));
            string result = f.GetValue(enemy).ToString();
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(def);
            return result;
        }

        [Test] public void SuperArmor_WindupHit_DoesNotCancel()
            => Assert.AreEqual("Windup", HitDuringWindup(true));

        [Test] public void Trash_WindupHit_StillCancels()
            => Assert.AreEqual("Stagger", HitDuringWindup(false));

        [Test] public void ImmunityWindow_BlocksThenExpires()
        {
            Assert.IsFalse(FlinchTiming.ShouldFlinch(true, false, false, false, true, 1.3f, 1.4f));
            Assert.IsTrue(FlinchTiming.ShouldFlinch(true, false, false, false, true, 1.4f, 1.4f));
        }

        [Test] public void TiltEnvelope_ZeroAtEnds_PeakMid()
        {
            Assert.AreEqual(0f, FlinchTiming.TiltEnvelope(0f), 1e-5f);
            Assert.AreEqual(1f, FlinchTiming.TiltEnvelope(0.5f), 1e-5f);
            Assert.AreEqual(0f, FlinchTiming.TiltEnvelope(1f), 1e-5f);
            Assert.AreEqual(0f, FlinchTiming.TiltEnvelope(2f), 1e-5f);
        }
    }
}
