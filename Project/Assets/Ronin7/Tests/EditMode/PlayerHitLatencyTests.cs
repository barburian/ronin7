using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P2-10 / BUG-29: an enemy chop must damage the player (and fire PlayerHit, which drives
    /// haptics/audio/vignette) when the strike pose lands, not at the end of Active.</summary>
    public class PlayerHitLatencyTests
    {
        private class TestAttacker : MeleeAttacker
        {
            // Long Active so the editor's Time.deltaTime can never carry the timer to Active's end.
            public const float Active = 10f;
            public int parryProbes;
            protected override float TelegraphTime => 1f;
            protected override float ActiveTime => Active;
            protected override float RecoverTime => 1f;
            protected override float StaggerTime => 1f;
            protected override float AttackDamage => 10f;
            protected override void TryDetectParry() => parryProbes++;
            protected override bool IsInLandHitRange() => true;
            public void StartActive(Health player, float elapsed) { target = player; Enter(State.Active); timer = elapsed; }
            public bool InActive => state == State.Active;
        }

        private GameObject enemyGo, playerGo;
        private TestAttacker attacker;
        private Health player;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            playerGo = new GameObject("Player");
            player = playerGo.AddComponent<Health>();
            player.Configure(100f);
            enemyGo = new GameObject("Enemy");
            enemyGo.AddComponent<Health>().Configure(100f);
            var weapon = new GameObject("Weapon").transform;
            weapon.SetParent(enemyGo.transform);
            attacker = enemyGo.AddComponent<TestAttacker>();
            typeof(MeleeAttacker).GetField("weapon", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.SetValue(attacker, weapon);
            Call("Awake"); // Awake doesn't fire for AddComponent outside play mode
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(playerGo);
        }

        private void Call(string method) =>
            typeof(MeleeAttacker).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(attacker, null);

        private static float StrikeLandsAt => TestAttacker.Active * 0.35f; // StrikePoseProgress hits 1 here

        [Test]
        public void PlayerHit_FeedbackFiresSameFrameAsDamage()
        {
            int damagedFrame = -1, hitFrame = -1, damagedCount = 0, order = 0, damagedOrder = 0, hitOrder = 0;
            player.Damaged += _ => { damagedFrame = Time.frameCount; damagedCount++; damagedOrder = ++order; };
            System.Action<PlayerHit> onHit = _ => { hitFrame = Time.frameCount; hitOrder = ++order; };
            EventBus.Subscribe(onHit);

            Assert.AreEqual(1f, MeleeAttacker.StrikePoseProgress(StrikeLandsAt + 0.01f, TestAttacker.Active), 1e-4f);
            attacker.StartActive(player, StrikeLandsAt + 0.01f); // strike pose has fully landed
            Call("Update");

            Assert.AreEqual(1, damagedCount, "damage must land when the strike pose lands, not at Active's end");
            Assert.AreEqual(damagedFrame, hitFrame, "PlayerHit feedback must fire the same frame as the damage");
            Assert.Less(damagedOrder, hitOrder);
            Assert.IsTrue(attacker.InActive, "Active (follow-through) still runs to its full length");

            Call("Update");
            Assert.AreEqual(1, damagedCount, "one hit per swing");
        }

        [Test]
        public void Strike_BeforeContact_NoDamageYet()
        {
            int damagedCount = 0;
            player.Damaged += _ => damagedCount++;
            attacker.StartActive(player, 0f);
            Call("Update");
            Assert.AreEqual(0, damagedCount);
        }

        [Test]
        public void Strike_AfterHitLands_ParryWindowClosed()
        {
            attacker.StartActive(player, StrikeLandsAt + 0.01f);
            Call("Update"); // hit lands
            Call("FixedUpdate");
            Assert.AreEqual(0, attacker.parryProbes, "a blow that already landed cannot be deflected");
        }
    }
}
