using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.EditorTools;
using Ronin7.Player;
using Ronin7.Ship;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P9-02c (A15.2, A15.6, A15.7, A15.14, R3): Polearm thrust, Shield absorb/reflect, held-only blades,
    /// holster mirroring and the shipped asset numbers.</summary>
    public class PolearmShieldTests
    {
        private static WeaponDefinition PolearmDef()
        {
            var d = ScriptableObject.CreateInstance<WeaponDefinition>();
            d.id = WeaponId.PolearmShield;
            d.minSwingSpeed = 1.5f; d.referenceSwingSpeed = 7f; d.minDamage = 6f; d.maxDamage = 20f;
            d.canParry = false; d.thrustDamage = 30f;
            return d;
        }

        // --- thrust (A15.6, R3) ---

        [TestCase(2f)]
        [TestCase(5f)]
        public void Thrust_ForwardAtLeast2_IsFlat30(float along)
        {
            var d = PolearmDef();
            try { Assert.AreEqual(30f, BladeDamager.BaseDamage(d, along, 1f, along), 1e-4f); }
            finally { Object.DestroyImmediate(d); }
        }

        [Test]
        public void Thrust_Below2_FallsBackToSwingDamage()
        {
            var d = PolearmDef();
            try
            {
                Assert.IsFalse(BladeDamager.IsThrust(30f, 1.9f));
                Assert.AreEqual(d.DamageForSpeed(3f, 1f), BladeDamager.BaseDamage(d, 3f, 1f, 1.9f), 1e-4f);
            }
            finally { Object.DestroyImmediate(d); }
        }

        [Test]
        public void Thrust_BackwardPush_IsNotAThrust()
        {
            Assert.IsFalse(BladeDamager.IsThrust(30f, -4f), "only a forward push counts, not |dot|");
            Assert.IsTrue(BladeDamager.IsThrust(30f, 2f));
        }

        [Test]
        public void Thrust_ZeroThrustDamage_NeverThrusts_KatanaSwingUnchanged()
        {
            var k = ScriptableObject.CreateInstance<WeaponDefinition>(); // katana numbers: 8-40, 1.5..6
            try
            {
                Assert.IsFalse(BladeDamager.IsThrust(0f, 10f));
                Assert.AreEqual(k.DamageForSpeed(4f, 1f), BladeDamager.BaseDamage(k, 4f, 1f, 10f), 1e-4f);
            }
            finally { Object.DestroyImmediate(k); }
        }

        [Test]
        public void Thrust_HeadStillCounts_ButDirectionBonusIsOff()
        {
            var body = new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(0.6f, 1.8f, 0.6f));
            Vector3 forward = Vector3.forward * 5f, axis = Vector3.forward;
            Assert.IsTrue(BladeDamager.IsPrecisionHit(1f, body, forward, axis), "a katana thrust keeps the direction bonus");
            Assert.IsFalse(BladeDamager.IsPrecisionHit(1f, body, forward, axis, directionBonus: false), "thrust weapon: no direction bonus");
            Assert.IsTrue(BladeDamager.IsPrecisionHit(1.7f, body, forward, axis, directionBonus: false), "head still counts");
        }

        [Test]
        public void Thrust_Numbers_BodyHeadSightAndCap()
        {
            const float thrust = 30f;
            Assert.AreEqual(30f, thrust * BladeDamager.PrecisionMultiplier(false, false, 0f), 1e-4f, "body");
            Assert.AreEqual(37.5f, thrust * BladeDamager.PrecisionMultiplier(true, false, 0f), 1e-4f, "head");
            Assert.AreEqual(45f, thrust * BladeDamager.PrecisionMultiplier(true, false, 0.25f), 1e-4f, "Precision boon +0.25");
            Assert.AreEqual(60f, thrust * BladeDamager.PrecisionMultiplier(true, true, 0f), 1e-4f, "Sight x2");
            Assert.AreEqual(60f, thrust * BladeDamager.PrecisionMultiplier(true, false, 5f), 1e-4f, "capped at 60");
            Assert.AreEqual(45f, BladeDamager.ApplyWielderMultiplier(30f, 1.5f), 1e-4f, "damage boons come on top");
        }

        // --- swing vs thrust speed point (A15.6) ---

        private static FieldInfo F(string n) => typeof(BladeDamager).GetField(n, BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void SwingSpeed_WristRotationAboutGrip_IsHigh_NoThrust_HandSpeedStaysLow()
        {
            // Grip fixed at the origin; the damage-zone centre (0.85 m out) sweeps 0.85 * 8 rad/s = 6.8 m/s sideways.
            Vector3 c0 = new Vector3(0f, 0f, 0.85f), c1 = c0 + Vector3.right * (6.8f * 0.02f);
            float swing = BladeDamager.SmoothSpeed(c0, c1, 0.02f, 0f, 0f);
            Assert.AreEqual(6.8f, swing, 1e-3f);
            Vector3 handVel = Vector3.zero; // the grip does not translate
            Assert.IsFalse(BladeDamager.IsThrust(30f, Vector3.Dot(handVel, Vector3.forward)));
            Assert.AreEqual(swing, BladeDamager.HitSpeed(false, swing, handVel), 1e-5f);
        }

        [Test]
        public void Thrust_ForwardTranslation_ReportsHandSpeed_AndHeavyBreakResistsIt()
        {
            Vector3 handVel = Vector3.forward * 3f;
            float swing = 9f; // would be exploited if reported
            Assert.IsTrue(BladeDamager.IsThrust(30f, Vector3.Dot(handVel, Vector3.forward)));
            float reported = BladeDamager.HitSpeed(true, swing, handVel);
            Assert.AreEqual(3f, reported, 1e-5f);
            var ctx = new WeaknessContext(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, 5f),
                reported, DamageType.Melee, true, false, false, false, false, false, false, 1, 0, 99f);
            Assert.AreEqual(WeaknessResult.Resisted, WeaknessRules.Evaluate(WeaknessKind.HeavyBreak, ctx, 5.5f));
        }

        [Test]
        public void TryHit_Thrust_UsesAxisAndReportsHandSpeed()
        {
            var def = PolearmDef();
            var bladeGo = new GameObject("Blade", typeof(BoxCollider), typeof(BladeDamager));
            var victimGo = new GameObject("Victim", typeof(Health), typeof(BoxCollider));
            try
            {
                var box = bladeGo.GetComponent<BoxCollider>();
                box.center = new Vector3(0f, 0f, 0.85f); box.size = new Vector3(0.1f, 0.1f, 1.1f);
                var blade = bladeGo.GetComponent<BladeDamager>();
                blade.SetDefinition(def);
                F("box").SetValue(blade, box);
                F("bladeVel").SetValue(blade, Vector3.forward * 3f);
                F("speed").SetValue(blade, 9f);
                victimGo.transform.position = new Vector3(0f, 0f, 1.2f);
                var health = victimGo.GetComponent<Health>();
                health.Configure(100f);
                DamageInfo got = default;
                health.Damaged += d => got = d;
                typeof(BladeDamager).GetMethod("TryHit", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(blade, new object[] { victimGo.GetComponent<BoxCollider>(), new Vector3(0f, 0f, 1.2f) });
                Assert.AreEqual(30f, 100f - health.Current, 1e-3f, "flat thrust damage along the pole axis");
                Assert.AreEqual(3f, got.Speed, 1e-4f, "hand speed, not the 9 m/s swing speed");
            }
            finally { Object.DestroyImmediate(def); Object.DestroyImmediate(bladeGo); Object.DestroyImmediate(victimGo); }
        }

        [Test]
        public void TryHit_Shield_DealsNothing()
        {
            var def = ScriptableObject.CreateInstance<WeaponDefinition>();
            def.maxDamage = 0f; def.minDamage = 0f; def.canParry = true;
            var bladeGo = new GameObject("Face", typeof(BoxCollider), typeof(BladeDamager));
            var victimGo = new GameObject("Victim", typeof(Health), typeof(BoxCollider));
            try
            {
                var blade = bladeGo.GetComponent<BladeDamager>();
                blade.SetDefinition(def);
                F("speed").SetValue(blade, 9f);
                var health = victimGo.GetComponent<Health>();
                health.Configure(100f);
                typeof(BladeDamager).GetMethod("TryHit", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(blade, new object[] { victimGo.GetComponent<BoxCollider>(), Vector3.zero });
                Assert.AreEqual(100f, health.Current, 1e-4f);
            }
            finally { Object.DestroyImmediate(def); Object.DestroyImmediate(bladeGo); Object.DestroyImmediate(victimGo); }
        }

        // --- bolt vs gear grip, release path ---

        [Test]
        public void Bolt_TouchingGearGrip_FliesOn_WielderUnharmed()
        {
            var rig = new GameObject("Rig", typeof(Health));
            var boltGo = new GameObject("Bolt", typeof(Projectile));
            try
            {
                Assert.IsTrue(XRRigBuilder.RogueEnsurePolearmShield(rig.transform));
                var health = rig.GetComponent<Health>();
                health.Configure(10f);
                var polearm = rig.GetComponent<PolearmShieldGear>().polearm;
                polearm.gameObject.SetActive(true); // in a run the equipped gear is active
                var grip = polearm.transform.Find("GripCollider").GetComponent<Collider>();
                var bolt = boltGo.GetComponent<Projectile>();
                typeof(Projectile).GetField("damage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bolt, 3f);
                bool consumed = (bool)typeof(Projectile).GetMethod("TryHit", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(bolt, new object[] { grip, Vector3.zero, Vector3.forward });
                Assert.IsFalse(consumed);
                Assert.AreEqual(10f, health.Current, 1e-4f);
            }
            finally { Object.DestroyImmediate(boltGo); Object.DestroyImmediate(rig); }
        }

        [Test]
        public void Grabber_Release_LeavesHeldOnlyDamagerOff()
        {
            var go = new GameObject("Gear", typeof(Rigidbody));
            var grab = go.AddComponent<Grabbable>();
            var bladeGo = new GameObject("Blade", typeof(BoxCollider));
            bladeGo.transform.SetParent(go.transform, false);
            var damager = bladeGo.AddComponent<BladeDamager>();
            var held = bladeGo.AddComponent<HeldOnlyBlade>();
            var hand = new GameObject("Hand", typeof(HandVelocityTracker));
            var grabber = hand.AddComponent<Grabber>();
            try
            {
                typeof(Grabbable).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(grab, null);
                typeof(HeldOnlyBlade).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(held, null);
                grabber.GrabDirect(grab);
                Assert.IsTrue(damager.enabled);
                typeof(Grabber).GetMethod("ReleaseHeld", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(grabber, null);
                Assert.IsFalse(damager.enabled, "a stowed polearm must not sweep");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(hand); }
        }

        // --- shield (A15.7) ---

        [TestCase(0f, true)]
        [TestCase(2.49f, true)]
        [TestCase(2.5f, false)]
        [TestCase(6f, false)]
        public void Shield_AbsorbsBelowReflectSpeed_ReflectsAbove(float speed, bool absorbs)
            => Assert.AreEqual(absorbs, Projectile.ShieldAbsorbs(true, speed));

        [Test]
        public void Shield_Katana_NeverAbsorbs() => Assert.IsFalse(Projectile.ShieldAbsorbs(false, 0f));

        [TestCase(0f, 10f, true, TestName = "Bolt_SlowShield_AbsorbedNoDamage")]
        [TestCase(40f, 7f, true, TestName = "Bolt_SlowKatana_StillHurtsParent")]
        public void Bolt_TryHit_ShieldVersusKatana(float maxDamage, float expectedHealth, bool consumed)
        {
            var def = ScriptableObject.CreateInstance<WeaponDefinition>();
            var parentGo = new GameObject("Wielder", typeof(Health));
            var boltGo = new GameObject("Bolt", typeof(Projectile));
            var bladeGo = new GameObject("Blade", typeof(BoxCollider), typeof(BladeDamager));
            try
            {
                var health = parentGo.GetComponent<Health>();
                health.Configure(10f);
                bladeGo.transform.SetParent(parentGo.transform);
                def.maxDamage = maxDamage;
                var blade = bladeGo.GetComponent<BladeDamager>();
                blade.SetDefinition(def);
                Assert.AreEqual(maxDamage <= 0f, blade.IsShield);

                var bolt = boltGo.GetComponent<Projectile>();
                typeof(Projectile).GetField("damage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bolt, 3f);
                var tryHit = typeof(Projectile).GetMethod("TryHit", BindingFlags.Instance | BindingFlags.NonPublic);
                bool result = (bool)tryHit.Invoke(bolt, new object[] { bladeGo.GetComponent<BoxCollider>(), Vector3.zero, Vector3.forward });

                Assert.AreEqual(consumed, result);
                Assert.AreEqual(expectedHealth, health.Current, 1e-4f);
            }
            finally { Object.DestroyImmediate(def); Object.DestroyImmediate(boltGo); Object.DestroyImmediate(parentGo); }
        }

        // --- held-only (a holstered polearm or shield is inert) ---

        [Test]
        public void HeldOnlyBlade_InertUntilHeld()
        {
            var go = new GameObject("Gear", typeof(Rigidbody));
            var grab = go.AddComponent<Grabbable>();
            var bladeGo = new GameObject("Blade", typeof(BoxCollider));
            bladeGo.transform.SetParent(go.transform, false);
            bladeGo.AddComponent<BladeDamager>();
            var held = bladeGo.AddComponent<HeldOnlyBlade>();
            var hand = new GameObject("Hand");
            try
            {
                // Awake / OnEnable do not run in edit mode: run them by hand.
                typeof(Grabbable).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(grab, null);
                typeof(HeldOnlyBlade).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(held, null);
                var col = bladeGo.GetComponent<BoxCollider>();
                var damager = bladeGo.GetComponent<BladeDamager>();
                Assert.IsFalse(col.enabled); Assert.IsFalse(damager.enabled);

                grab.Grab(hand.transform);
                Assert.IsTrue(col.enabled); Assert.IsTrue(damager.enabled);

                grab.Release(Vector3.zero, Vector3.zero);
                Assert.IsFalse(col.enabled); Assert.IsFalse(damager.enabled);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(hand); }
        }

        // --- holster rotation mirroring (A15.14) ---

        [Test]
        public void MirrorX_FlipsXOfEveryAxis()
        {
            var q = Quaternion.Euler(-135f, 10f, 0f);
            var m = KatanaHolster.MirrorX(q);
            Vector3 f = q * Vector3.forward, mf = m * Vector3.forward;
            Assert.AreEqual(-f.x, mf.x, 1e-4f); Assert.AreEqual(f.y, mf.y, 1e-4f); Assert.AreEqual(f.z, mf.z, 1e-4f);
            Vector3 u = q * Vector3.up, mu = m * Vector3.up;
            Assert.AreEqual(-u.x, mu.x, 1e-4f); Assert.AreEqual(u.y, mu.y, 1e-4f); Assert.AreEqual(u.z, mu.z, 1e-4f);
        }

        [Test]
        public void ApplySide_MirrorsDiagonalPole_Idempotent_AndLeavesKatanaHipAlone()
        {
            var pole = new GameObject("Pole").transform;
            var hip = new GameObject("Hip").transform;
            try
            {
                var rot = Quaternion.Euler(-135f, 10f, 0f);
                pole.localPosition = new Vector3(0.15f, 0.9f, 0.05f); pole.localRotation = rot;
                hip.localPosition = new Vector3(0.15f, 0.9f, 0.05f);

                KatanaHolster.ApplySide(pole, false); KatanaHolster.ApplySide(hip, false);
                Assert.AreEqual(0f, Quaternion.Angle(rot, pole.localRotation), 1e-3f, "already right-handed: untouched");

                KatanaHolster.ApplySide(pole, true); KatanaHolster.ApplySide(pole, true); KatanaHolster.ApplySide(hip, true);
                Assert.AreEqual(-0.15f, pole.localPosition.x, 1e-5f);
                Vector3 f = rot * Vector3.forward, mf = pole.localRotation * Vector3.forward;
                Assert.AreEqual(-f.x, mf.x, 1e-4f); Assert.AreEqual(f.y, mf.y, 1e-4f); Assert.AreEqual(f.z, mf.z, 1e-4f);
                Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, hip.localRotation), 1e-3f, "katana hip rotation stays identity");

                KatanaHolster.ApplySide(pole, false);
                Assert.AreEqual(0.15f, pole.localPosition.x, 1e-5f);
                Assert.AreEqual(0f, Quaternion.Angle(rot, pole.localRotation), 1e-3f);
            }
            finally { Object.DestroyImmediate(pole.gameObject); Object.DestroyImmediate(hip.gameObject); }
        }

        [Test]
        public void Equip_AppliesCurrentMirrorToNewAnchor_AndEquipHomeRestores()
        {
            var rig = new GameObject("Rig");
            try
            {
                var homeAnchor = new GameObject("Hip").transform; homeAnchor.SetParent(rig.transform, false);
                homeAnchor.localPosition = new Vector3(0.15f, 0.9f, 0.05f);
                var home = new GameObject("Sword", typeof(Rigidbody)).AddComponent<Grabbable>();
                var holster = rig.AddComponent<KatanaHolster>();
                var so = new SerializedObject(holster);
                so.FindProperty("sword").objectReferenceValue = home;
                so.FindProperty("hipAnchor").objectReferenceValue = homeAnchor;
                so.ApplyModifiedPropertiesWithoutUndo();

                var poleAnchor = new GameObject("PoleAnchor").transform; poleAnchor.SetParent(rig.transform, false);
                poleAnchor.localPosition = new Vector3(0.15f, 0.9f, 0.05f); poleAnchor.localRotation = Quaternion.Euler(-135f, 10f, 0f);
                var pole = new GameObject("Pole", typeof(Rigidbody)).AddComponent<Grabbable>();

                holster.SetMirrored(true); // left-handed
                holster.Equip(pole, poleAnchor);
                Assert.AreSame(pole, holster.Sword);
                Assert.AreSame(poleAnchor, pole.transform.parent);
                Assert.AreEqual(-0.15f, poleAnchor.localPosition.x, 1e-5f);
                Assert.IsTrue(holster.IsSheathed);

                holster.EquipHome();
                Assert.AreSame(home, holster.Sword);
                Assert.AreSame(homeAnchor, home.transform.parent);
                Assert.IsTrue(holster.IsSheathed);
            }
            finally { Object.DestroyImmediate(rig); }
        }

        // --- shipped assets and geometry ---

        [Test]
        public void PolearmAsset_MatchesA156()
        {
            var d = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Ronin7/Data/Polearm.asset");
            Assert.IsNotNull(d);
            Assert.AreEqual(WeaponId.PolearmShield, d.id);
            Assert.AreEqual(1.5f, d.minSwingSpeed, 1e-4f);
            Assert.AreEqual(7f, d.referenceSwingSpeed, 1e-4f);
            Assert.AreEqual(6f, d.minDamage, 1e-4f);
            Assert.AreEqual(20f, d.maxDamage, 1e-4f);
            Assert.AreEqual(0.3f, d.hitCooldown, 1e-4f);
            Assert.IsFalse(d.canParry);
            Assert.AreEqual(30f, d.thrustDamage, 1e-4f);
            Assert.AreEqual(0f, d.bladeLength, 1e-4f);
        }

        [Test]
        public void ShieldAsset_ZeroDamageParries_IdIsNotALoadout()
        {
            var d = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Ronin7/Data/PlayerShield.asset");
            Assert.IsNotNull(d);
            Assert.AreEqual(0f, d.maxDamage); Assert.AreEqual(0f, d.minDamage);
            Assert.IsTrue(d.canParry);
            Assert.AreEqual(0.25f, d.parryStagger, 1e-4f);
            Assert.IsFalse(WeaponId.OnRoster(d.id), d.id);
        }

        [Test]
        public void RigGear_PolearmReach_AndOnlyGripsAreExtraColliders()
        {
            var rig = new GameObject("Rig");
            try
            {
                Assert.IsTrue(XRRigBuilder.RogueEnsurePolearmShield(rig.transform));
                Assert.IsFalse(XRRigBuilder.RogueEnsurePolearmShield(rig.transform), "idempotent");
                var gear = rig.GetComponent<PolearmShieldGear>();
                Assert.IsTrue(gear.Complete);
                Assert.IsFalse(gear.polearm.gameObject.activeSelf); Assert.IsFalse(gear.shield.gameObject.activeSelf);

                var blade = gear.polearm.transform.Find("Blade");
                var box = blade.GetComponent<BoxCollider>();
                Assert.AreEqual(Vector3.zero, blade.localPosition, "pivot at the grip: bladeVel is hand velocity (swing speed is read at the box centre)");
                Assert.IsTrue(box.isTrigger);
                Assert.AreEqual(0.3f, box.center.z - box.size.z * 0.5f, 1e-4f, "starts 0.3 m ahead of the grip");
                Assert.AreEqual(1.4f, box.center.z + box.size.z * 0.5f, 1e-4f, "about 1.4 m reach");
                Assert.AreEqual(1.1f, box.size.z, 1e-4f, "front 1.1 m only");
                Assert.AreEqual("Assets/Ronin7/Data/Polearm.asset", AssetDatabase.GetAssetPath(gear.polearm.GetComponent<Sword>().Definition));

                Assert.AreEqual(2, gear.polearm.GetComponentsInChildren<Collider>(true).Length, "grip + blade only");
                Assert.IsNotNull(gear.polearm.transform.Find("GripCollider"));
                Assert.AreEqual(0, gear.polearm.GetComponentsInChildren<MeshCollider>(true).Length);
                Assert.AreEqual(0, gear.shield.GetComponentsInChildren<MeshCollider>(true).Length);
                Assert.IsNull(gear.shield.GetComponent<Sword>(), "the shield keeps its own definition");
                Assert.IsNotNull(gear.polearm.GetComponentInChildren<HeldOnlyBlade>(true));
                var face = gear.shield.GetComponentInChildren<HeldOnlyBlade>(true).GetComponent<BladeDamager>();
                Assert.IsNotNull(face);
            }
            finally { Object.DestroyImmediate(rig); }
        }
    }
}
