using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// Exercises the two EP25 "The Sterile Reckoning" mechanics:
    ///  - ZeroGFloatController: drives MeleeAttacker members in a deterministic bob+sway float.
    ///  - SurgicalDefenseArray: a timed blade hazard that damages the player once per active sweep,
    ///    and can be permanently disabled.
    /// </summary>
    public class Ep25MechanicsPlayTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.Destroy(go);
            _spawned.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>A concrete MeleeAttacker (TrainingDummy) on a fresh GameObject at a known position.</summary>
        private TrainingDummy MakeDummy(Vector3 pos)
        {
            var go = new GameObject("Dummy");
            _spawned.Add(go);
            go.transform.position = pos;
            var dummy = go.AddComponent<TrainingDummy>(); // RequireComponent auto-adds Health
            return dummy;
        }

        /// <summary>A player-tagged Health: identified by a CharacterController (matches the hazard's player check).</summary>
        private Health MakePlayerHealth()
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            go.AddComponent<CharacterController>();
            var health = go.AddComponent<Health>();
            health.Configure(100f);
            return health;
        }

        // ---- ZeroGFloatController ----

        [Test]
        public void ZeroGFloat_OffsetIsZeroAtStart_AndBoundedByAmplitude()
        {
            var go = new GameObject("ZeroGFloat");
            _spawned.Add(go);
            var zg = go.AddComponent<ZeroGFloatController>();
            zg.RegisterMember(MakeDummy(new Vector3(0f, 0f, 5f)));

            Assert.AreEqual(1, zg.MemberCount, "Member should be registered.");

            // At clock 0, both sines are zero → zero offset.
            Assert.AreEqual(0f, zg.Offset(0).magnitude, 1e-4f, "Offset at clock 0 should be zero.");

            // Advance and confirm the offset stays within the configured bob amplitude (default 0.5).
            for (int i = 0; i < 20; i++)
            {
                zg.Tick(0.1f);
                Assert.LessOrEqual(Mathf.Abs(zg.Offset(0).y), 0.5f + 1e-3f, "Vertical bob must stay within bobAmplitude.");
            }

            // Out-of-range member index is safe.
            Assert.AreEqual(Vector3.zero, zg.Offset(5));
        }

        [Test]
        public void ZeroGFloat_TickMovesMemberOffItsBasePosition()
        {
            // A [Test] advances no frames, so the controller's own Update never auto-ticks — the clock
            // stays 0 until we call Tick, and the member sits exactly at its registered base position.
            var go = new GameObject("ZeroGFloat");
            _spawned.Add(go);
            var zg = go.AddComponent<ZeroGFloatController>();
            var startPos = new Vector3(0f, 0f, 5f);
            var dummy = MakeDummy(startPos); // unparented, so localPosition == startPos
            zg.RegisterMember(dummy);

            Assert.That((dummy.transform.localPosition - startPos).magnitude, Is.LessThan(1e-3f),
                "Before any Tick the member should rest at its base position.");

            // Advance to a clock where both sines are clearly non-zero.
            zg.Tick(0.5f);

            Vector3 expected = startPos + zg.Offset(0);
            Assert.That(zg.Clock, Is.GreaterThan(0f), "Clock should advance with Tick.");
            Assert.That((dummy.transform.localPosition - expected).magnitude, Is.LessThan(1e-3f),
                "Member should be repositioned to base + Offset.");
            Assert.That((dummy.transform.localPosition - startPos).magnitude, Is.GreaterThan(0.01f),
                "Member should have visibly floated off its base position.");
        }

        // ---- SurgicalDefenseArray ----

        [Test]
        public void SurgicalDefenseArray_DamagesPlayerOncePerActiveSweep()
        {
            var go = new GameObject("BladeArray");
            _spawned.Add(go);
            var arr = go.AddComponent<SurgicalDefenseArray>();
            var player = MakePlayerHealth();

            // Before any Tick the array is inactive.
            Assert.IsFalse(arr.IsActive, "Array should start inactive.");
            Assert.IsFalse(arr.TryDamageHealth(player), "Inactive array must not damage.");

            // Tick into the active window (default activeDuration 0.8 of a 2.5 cycle).
            arr.Tick(0.1f);
            Assert.IsTrue(arr.IsActive, "Array should be active early in the cycle.");

            float before = player.Current;
            Assert.IsTrue(arr.TryDamageHealth(player), "Active array should damage the player.");
            Assert.Less(player.Current, before, "Player health should drop.");

            // One-shot per sweep: a second hit in the same active window is refused.
            Assert.IsFalse(arr.TryDamageHealth(player), "Array must only damage once per active sweep.");

            // Advance past the active window into the safe gap; damage flag resets, no damage.
            arr.Tick(1.0f); // clock 1.1 of 2.5 → inactive
            Assert.IsFalse(arr.IsActive, "Array should be inactive in the gap.");
            float mid = player.Current;
            Assert.IsFalse(arr.TryDamageHealth(player), "Inactive array must not damage.");
            Assert.AreEqual(mid, player.Current, 1e-4f, "Health unchanged while inactive.");
        }

        [Test]
        public void SurgicalDefenseArray_DisableStopsAllSweeps()
        {
            var go = new GameObject("BladeArray");
            _spawned.Add(go);
            var arr = go.AddComponent<SurgicalDefenseArray>();
            var player = MakePlayerHealth();

            arr.Tick(0.1f);
            Assert.IsTrue(arr.IsActive);

            arr.Disable();
            Assert.IsTrue(arr.IsDisabled, "Array should report disabled.");
            Assert.IsFalse(arr.IsActive, "Disabled array is never active.");

            // Ticking a disabled array does nothing and it never damages.
            arr.Tick(0.1f);
            Assert.IsFalse(arr.IsActive);
            float before = player.Current;
            Assert.IsFalse(arr.TryDamageHealth(player), "Disabled array must not damage.");
            Assert.AreEqual(before, player.Current, 1e-4f, "Health unchanged after disable.");
        }

        [Test]
        public void SurgicalDefenseArray_IgnoresNonPlayerHealth()
        {
            var go = new GameObject("BladeArray");
            _spawned.Add(go);
            var arr = go.AddComponent<SurgicalDefenseArray>();

            // A Health WITHOUT a CharacterController is not the player.
            var npcGo = new GameObject("NPC");
            _spawned.Add(npcGo);
            var npcHealth = npcGo.AddComponent<Health>();
            npcHealth.Configure(100f);

            arr.Tick(0.1f);
            Assert.IsTrue(arr.IsActive);
            Assert.IsFalse(arr.TryDamageHealth(npcHealth), "Only the player (CharacterController) takes blade damage.");
            Assert.AreEqual(100f, npcHealth.Current, 1e-4f);
        }
    }
}
