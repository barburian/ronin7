using NUnit.Framework;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>Rig_ArmR is both the walk-swing bone and MeleeAttacker.weapon: the walk animator must
    /// leave it alone while the attacker is posing it, and ease back afterwards.</summary>
    public class NpcWalkAnimatorAttackArmTests
    {
        private class TestAttacker : MeleeAttacker
        {
            protected override float TelegraphTime => 1f;
            protected override float ActiveTime => 1f;
            protected override float RecoverTime => 1f;
            protected override float StaggerTime => 1f;
            protected override float AttackDamage => 0f;
            protected override void TryDetectParry() { }
            protected override bool IsInLandHitRange() => false;
            public void SetWeapon(Transform w) { weapon = w; }
            public void Windup() { BeginAttack(); PoseWeapon(restEuler, windupEuler, 1f); }
            public void GoIdle() { Enter(State.Idle); }
            public Quaternion WindupPose => Quaternion.AngleAxis(RigArmAngle(windupEuler.x, restEuler.x, strikeEuler.x, 60f), Vector3.right) * RigArmBase;
        }

        private GameObject root;
        private Transform armR;
        private TestAttacker attacker;
        private NpcWalkAnimator walk;
        private Quaternion rest;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Npc");
            root.AddComponent<Ronin7.Combat.Health>();
            var rig = new GameObject(NpcWalkAnimator.RootBoneName).transform; rig.SetParent(root.transform);
            foreach (var n in new[] { NpcWalkAnimator.LegLBoneName, NpcWalkAnimator.LegRBoneName })
                new GameObject(n).transform.SetParent(rig);
            armR = new GameObject(NpcWalkAnimator.ArmRBoneName).transform; armR.SetParent(rig);
            attacker = root.AddComponent<TestAttacker>();
            attacker.SetWeapon(armR);
            walk = root.AddComponent<NpcWalkAnimator>();
            // Awake doesn't fire for AddComponent outside play mode; run it so refs/rest poses resolve.
            foreach (var mb in new MonoBehaviour[] { attacker, walk })
                mb.GetType().GetMethod("Awake", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.FlattenHierarchy)?.Invoke(mb, null);
            rest = attacker.RigArmBase; // arms-down rest the walk animator captured as armRRest
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void Windup_ArmKeepsAttackPose_AfterWalkTick()
        {
            attacker.Windup();
            // Move so the walk swing would be non-zero if it were applied.
            for (int i = 0; i < 20; i++) { root.transform.position += Vector3.forward * 0.05f; walk.Tick(0.02f); }
            Assert.AreEqual(0f, Quaternion.Angle(attacker.WindupPose, armR.localRotation), 0.01f);
        }

        [Test]
        public void AfterAttack_ArmEasesBackToWalkSwing_WithoutSnap()
        {
            attacker.Windup();
            walk.Tick(0.02f);
            Quaternion pose = armR.localRotation;
            attacker.GoIdle();
            walk.Tick(0.02f); // first free frame: blend ~0.13, so it must not jump to rest
            Assert.Less(Quaternion.Angle(pose, armR.localRotation), 30f);
            for (int i = 0; i < 20; i++) walk.Tick(0.02f);
            Assert.AreEqual(0f, Quaternion.Angle(rest, armR.localRotation), 0.01f);
        }

        [Test]
        public void RigArm_RestsLoweredFromTPose_AndMirrors()
        {
            walk.Tick(0.02f);
            // right arm (+X in the T-pose) rolls down about -Z by 75 degrees
            Assert.AreEqual(75f, Quaternion.Angle(Quaternion.identity, armR.localRotation), 0.01f);
            Assert.Less((armR.localRotation * Vector3.right).y, -0.9f);
        }

        // BUG-47: arm elevations measured on the baked rigs (degrees above the shoulder's horizontal, mean of L/R).
        [TestCase(-32.5f)] // Dominion_Trooper
        [TestCase(-43.5f)] // Ash-World_Scavenger (lowest trash rig)
        [TestCase(17.5f)]  // Program_Operative_Grunt
        [TestCase(1.5f)]   // Spectral_Grave-Guardian
        public void ArmsDownFor_TPosedTrashRig_KeepsFullRoll(float elevation)
        {
            Assert.AreEqual(75f, NpcWalkAnimator.ArmsDownFor(elevation, 75f));
        }

        [TestCase(-61.5f)] // The-Warden
        [TestCase(-66.5f)] // Vane_Wraith-6
        [TestCase(-73f)]   // Maelgorn
        public void ArmsDownFor_HangingBossRig_NoRoll(float elevation)
        {
            Assert.AreEqual(0f, NpcWalkAnimator.ArmsDownFor(elevation, 75f));
        }

        [Test]
        public void ArmElevation_MirrorsLeftAndRight()
        {
            Assert.AreEqual(-32.2f, NpcWalkAnimator.ArmElevation(new Vector3(0.146f, -0.092f, 0.06f), true), 0.5f);
            Assert.AreEqual(-32.2f, NpcWalkAnimator.ArmElevation(new Vector3(-0.146f, -0.092f, 0.06f), false), 0.5f);
            Assert.AreEqual(-90f, NpcWalkAnimator.ArmElevation(Vector3.down, true), 0.01f);
        }
    }
}
