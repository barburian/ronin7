using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class UnbrokenWardTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            CampaignState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            CampaignState.Reset();
        }

        // ---- Pure decision (mirrors DuelYield.ShouldYield / ChapterOutro.ShouldPublish). ----

        [Test]
        public void ShouldIntercept_NotYetUsed_ReturnsTrue()
        {
            Assert.IsTrue(UnbrokenWard.ShouldIntercept(alreadyUsed: false));
        }

        [Test]
        public void ShouldIntercept_AlreadyUsed_ReturnsFalse()
        {
            Assert.IsFalse(UnbrokenWard.ShouldIntercept(alreadyUsed: true));
        }

        // ---- Integration via the real Health hook. EditMode does NOT auto-run MonoBehaviour lifecycle
        // methods on AddComponent, so drive Awake/OnEnable/OnDisable explicitly via reflection (mirroring
        // Unity's own order: Awake, then OnEnable only if the component stayed enabled). This exercises
        // the real registration + TryIntercept path, not a stubbed reimplementation. ----

        private static void Life(MonoBehaviour c, string method) =>
            c.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(c, null);

        private static UnbrokenWard StartWard(GameObject go)
        {
            var ward = go.AddComponent<UnbrokenWard>();
            Life(ward, "Awake");
            if (ward.enabled) Life(ward, "OnEnable"); // Unity skips OnEnable when Awake self-disabled it
            return ward;
        }

        private static DamageInfo LethalHit() =>
            new DamageInfo(9999f, Vector3.zero, Vector3.forward, null, DamageType.Melee);

        [Test]
        public void AbilityLocked_ComponentDisablesItself_NeverRegistersInterceptor()
        {
            _go = new GameObject("UnbrokenWardTestTarget");
            var health = _go.AddComponent<Health>();
            var ward = StartWard(_go);

            Assert.IsFalse(ward.enabled);
            Assert.IsNull(health.DeathInterceptor);
        }

        [Test]
        public void AbilityUnlocked_SurvivesFirstLethalBlow_DiesOnSecond()
        {
            CampaignState.UnlockAbility(AbilityId.Unbroken);

            _go = new GameObject("UnbrokenWardTestTarget");
            var health = _go.AddComponent<Health>();
            health.Configure(100f); // Configure sets Current (Awake isn't auto-run in EditMode)
            StartWard(_go);

            int diedCount = 0;
            health.Died += () => diedCount++;

            health.ApplyDamage(LethalHit());
            Assert.AreEqual(0, diedCount);
            Assert.AreEqual(1f, health.Current);
            Assert.IsTrue(health.IsAlive);

            health.ApplyDamage(LethalHit());
            Assert.AreEqual(1, diedCount);
            Assert.AreEqual(0f, health.Current);
            Assert.IsFalse(health.IsAlive);
        }

        [Test]
        public void AbilityUnlocked_PublishesAbilityActivated_OnceOnFirstIntercept()
        {
            CampaignState.UnlockAbility(AbilityId.Unbroken);
            EventBus.Clear();

            _go = new GameObject("UnbrokenWardTestTarget");
            var health = _go.AddComponent<Health>();
            health.Configure(100f);
            StartWard(_go);

            int activatedCount = 0;
            EventBus.Subscribe<AbilityActivated>(e =>
            {
                if (e.Id == AbilityId.Unbroken) activatedCount++;
            });

            health.ApplyDamage(LethalHit()); // ward spends here
            health.ApplyDamage(LethalHit()); // ward already spent; no second publish

            Assert.AreEqual(1, activatedCount);
            EventBus.Clear();
        }

        [Test]
        public void OnDisable_ClearsInterceptor_WhenStillOwnedByThisWard()
        {
            CampaignState.UnlockAbility(AbilityId.Unbroken);

            _go = new GameObject("UnbrokenWardTestTarget");
            var health = _go.AddComponent<Health>();
            var ward = StartWard(_go);

            Assert.IsNotNull(health.DeathInterceptor);

            Life(ward, "OnDisable");

            Assert.IsNull(health.DeathInterceptor);
        }

        [Test]
        public void OnDisable_DoesNotClobber_InterceptorTakenOverByAnotherOwner()
        {
            CampaignState.UnlockAbility(AbilityId.Unbroken);

            _go = new GameObject("UnbrokenWardTestTarget");
            var health = _go.AddComponent<Health>();
            var ward = StartWard(_go);

            System.Func<bool> otherOwner = () => false;
            health.DeathInterceptor = otherOwner; // a different system took over the hook

            Life(ward, "OnDisable"); // this ward's stale unregister must not clobber it

            Assert.AreSame(otherOwner, health.DeathInterceptor);
        }

        [Test]
        public void OnDisable_HealthAlreadyDestroyed_DoesNotThrow()
        {
            CampaignState.UnlockAbility(AbilityId.Unbroken);

            _go = new GameObject("UnbrokenWardTestTarget");
            var health = _go.AddComponent<Health>();
            var ward = StartWard(_go);

            Object.DestroyImmediate(health);

            Assert.DoesNotThrow(() => Life(ward, "OnDisable"));
        }
    }
}
