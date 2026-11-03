using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;
using Ronin7.Enemies;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Editor
{
    /// <summary>P8-03: weighted no-repeat pattern picker, held-peek combos, blade-only unparryable-aware parry.</summary>
    public class AttackPatternTests
    {
        private GameObject body;
        [TearDown] public void TearDown() { if (body != null) Object.DestroyImmediate(body); }

        private static AttackPattern Pat(string name, int weight, params Strike[] s) =>
            new AttackPattern { name = name, weight = weight, strikes = new List<Strike>(s) };

        private static EnemyWeaponDefinition LoadOrFail(string weapon)
        {
            var d = EnemyWeaponBuilder.Load(weapon);
            if (d == null) Assert.Fail("EW_" + weapon + " missing - run Compose Enemy Weapons (additive).");
            return d;
        }

        private Enemy MakeBody(out Transform mainSocket)
        {
            body = new GameObject("body");
            body.SetActive(false); // spawn order: equip before Awake
            var armR = new GameObject("Rig_ArmR").transform; armR.SetParent(body.transform, false);
            var armL = new GameObject("Rig_ArmL").transform; armL.SetParent(body.transform, false);
            mainSocket = new GameObject(EnemyWeaponDefinition.MainSocket).transform; mainSocket.SetParent(armR, false);
            mainSocket.localPosition = new Vector3(0f, -0.6f, 0f); // the hand, well away from the shoulder pivot
            new GameObject(EnemyWeaponDefinition.OffSocket).transform.SetParent(armL, false);
            var enemy = body.AddComponent<Enemy>();
            var so = new SerializedObject(enemy);
            so.FindProperty("weapon").objectReferenceValue = armR;
            so.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        [Test]
        public void Pattern_NoImmediateRepeat()
        {
            var pats = new List<AttackPattern> { Pat("A", 2, Strike.Chop), Pat("B", 1, Strike.Chop), Pat("C", 1, Strike.Chop) };
            var rng = new System.Random(7);
            var seen = new int[3];
            int last = -1;
            for (int i = 0; i < 1000; i++)
            {
                int p = StrikeSequencer.Pick(pats, last, rng);
                Assert.AreNotEqual(last, p, "pattern repeated at pick " + i);
                seen[p]++;
                last = p;
            }
            foreach (int n in seen) Assert.Greater(n, 0);
            // No-repeat with weights 2:1:1 lands near A 50%, B/C 25% each.
            Assert.That(seen[0] / 1000f, Is.InRange(0.4f, 0.6f));
            Assert.That(seen[1] / (float)seen[2], Is.InRange(0.75f, 1.33f));
        }

        [Test]
        public void Pattern_SinglePattern_Repeats()
        {
            var seq = new StrikeSequencer(new List<AttackPattern>(LoadOrFail("Crossbow").patterns), new System.Random(1));
            for (int i = 0; i < 5; i++) Assert.IsTrue(seq.Consume().Ranged, "crossbow keeps firing its one pattern");
            Assert.AreEqual(0, StrikeSequencer.Pick(new List<AttackPattern> { Pat("Only", 1, Strike.Chop) }, 0, new System.Random(1)));
        }

        [Test]
        public void Combo_TimingsInOrder()
        {
            var dagger = LoadOrFail("Dagger");
            var flurry = dagger.patterns.Find(p => p.name == "Flurry");
            Assert.IsNotNull(flurry, "EW_Dagger has no Flurry");
            Assert.AreEqual(3, flurry.strikes.Count);
            var seq = new StrikeSequencer(new List<AttackPattern>(dagger.patterns), new System.Random(3));
            for (int guard = 0; seq.Current.name != "Flurry"; guard++)
            {
                Assert.Less(guard, 50, "Flurry never picked");
                seq.Consume(); // Cross slash is one strike
            }
            for (int i = 0; i < 3; i++)
            {
                Strike peek = seq.Peek;
                Assert.AreEqual(peek.windup, seq.Peek.windup, "held peek must not advance");
                Assert.AreEqual(i, seq.StrikeIndex);
                Strike s = seq.Consume();
                Assert.AreEqual(flurry.strikes[i].windup, s.windup, "flurry strike " + i);
                Assert.AreEqual(flurry.strikes[i].direction, s.direction, "flurry strike " + i);
                Assert.AreEqual(i < 2, seq.ComboPending, "combo pending after strike " + i);
            }
            Assert.AreNotEqual("Flurry", seq.Current.name, "no repeat once the combo ends");

            // Blaster Burst fires 3 shots.
            var blaster = LoadOrFail("Blaster");
            Assert.AreEqual(3, blaster.patterns.Find(p => p.name == "Burst").strikes.FindAll(s => s.Ranged).Count);
        }

        [Test]
        public void Heavy_NotParryable()
        {
            var axe = LoadOrFail("HeavyAxe");
            foreach (var p in axe.patterns)
                foreach (var s in p.strikes)
                {
                    Assert.IsTrue(s.unparryable, p.name);
                    Assert.IsFalse(s.Parryable, p.name + ": a blade parry must not interrupt a DODGE strike");
                }
            Assert.IsTrue(Strike.Chop.Parryable, "old assets (unparryable=false) stay parryable");
        }

        [Test]
        public void ParryCapsule_IsBladeOnly()
        {
            var enemy = MakeBody(out var socket);
            var def = ScriptableObject.CreateInstance<EnemyWeaponDefinition>();
            def.edgeBase = new Vector3(0f, 0f, 0.2f); def.tip = new Vector3(0f, 0f, 0.9f);
            enemy.Equip(def);
            Assert.That(Vector3.Distance(enemy.ParryCapsuleStart, socket.TransformPoint(def.edgeBase)), Is.LessThan(1e-4f),
                "capsule starts at the weapon's edgeBase, not the shoulder");
        }

        [Test]
        public void ShieldBash_MergedFromOffHand()
        {
            var baton = LoadOrFail("Baton");
            var shield = LoadOrFail("Shield");
            int batonCount = baton.patterns.Count, shieldCount = shield.patterns.Count;
            var bash = shield.patterns.Find(p => p.name == "Shield bash");
            Assert.IsNotNull(bash, "EW_Shield has no Shield bash");
            Assert.AreEqual(StrikeDirection.Thrust, bash.strikes[0].direction);
            Assert.IsTrue(bash.strikes[0].unparryable);

            var enemy = MakeBody(out _);
            enemy.Equip(baton, shield);
            Assert.AreEqual(batonCount + shieldCount, enemy.Strikes.Patterns.Count);
            Assert.IsTrue(enemy.Strikes.Patterns.Contains(bash));
            Assert.AreEqual(batonCount, baton.patterns.Count, "shared main-hand asset mutated");

            // Dagger+Dagger: the same asset in both hands is not doubled.
            var dagger = LoadOrFail("Dagger");
            enemy.Equip(dagger, dagger);
            Assert.AreEqual(dagger.patterns.Count, enemy.Strikes.Patterns.Count);
        }
    }
}
