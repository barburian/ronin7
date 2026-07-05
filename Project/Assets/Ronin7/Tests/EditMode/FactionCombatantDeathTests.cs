using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards the FactionCombatant death fix: before it, a brawler had NO death handling at all —
    /// a corpse kept retargeting, chasing and attacking (observed live in the Ch10 gang-war QA:
    /// six dead units all still targeting the player). Death must stop the component and clear
    /// its target; a dead unit driven directly through TickCombat must do nothing.
    /// </summary>
    public class FactionCombatantDeathTests
    {
        private GameObject _a, _b;

        [SetUp]
        public void SetUp() => EventBus.Clear();

        [TearDown]
        public void TearDown()
        {
            if (_a != null) Object.DestroyImmediate(_a);
            if (_b != null) Object.DestroyImmediate(_b);
            Health.Active.Clear(); // EditMode never ran OnEnable/OnDisable — reset the registry by hand
            EventBus.Clear();
        }

        /// <summary>EditMode AddComponent runs no lifecycle methods: initialize Health via its public
        /// Configure, register it in Health.Active by hand, and reflection-invoke the combatant's
        /// Awake so the Died subscription and ownHealth cache exist (SpaceEncounterManager-tests idiom).</summary>
        private static FactionCombatant Make(out GameObject go, Vector3 pos, int faction, string name)
        {
            go = new GameObject(name);
            go.transform.position = pos;
            var h = go.AddComponent<Health>();
            h.Configure(45f);
            Health.Active.Add(h);
            var c = go.AddComponent<FactionCombatant>();
            c.SetFaction(faction);
            typeof(FactionCombatant).GetMethod("Awake",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(c, null);
            return c;
        }

        [Test]
        public void LethalDamage_DisablesCombatant_AndClearsTarget()
        {
            var a = Make(out _a, Vector3.zero, 0, "BrawlerA");
            var b = Make(out _b, new Vector3(1f, 0f, 0f), 1, "BrawlerB");

            // Let A acquire B as a rival-faction target.
            a.TickCombat(1.1f); // past retargetInterval
            Assert.IsNotNull(a.CurrentTarget, "Sanity: A must target its rival before dying.");

            _a.GetComponent<Health>().ApplyDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, _b));

            Assert.IsFalse(a.enabled, "A dead brawler must stop acting entirely.");
            Assert.IsNull(a.CurrentTarget, "Death must clear the target lock.");
        }

        [Test]
        public void DeadCombatant_TickedDirectly_DealsNoDamage()
        {
            var a = Make(out _a, Vector3.zero, 0, "BrawlerA");
            var b = Make(out _b, new Vector3(1f, 0f, 0f), 1, "BrawlerB");
            var bHealth = _b.GetComponent<Health>();

            _a.GetComponent<Health>().ApplyDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, _b));

            float before = bHealth.Current;
            for (int i = 0; i < 10; i++) a.TickCombat(1f); // way past retarget+attack intervals
            Assert.AreEqual(before, bHealth.Current, 1e-4f,
                "A corpse driven through TickCombat must never damage anyone.");
            Assert.IsNull(a.CurrentTarget);
        }
    }
}
