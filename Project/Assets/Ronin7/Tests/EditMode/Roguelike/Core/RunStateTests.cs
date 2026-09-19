using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    public class RunStateTests
    {
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp() => RunState.Reset();

        [TearDown]
        public void TearDown()
        {
            RunState.Reset();
            CampaignState.Reset();
            foreach (var obj in _created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        private BoonDefinition MakeBoon(string id, BoonEffectKind effect, float magnitude, int maxStacks = 3)
        {
            var boon = ScriptableObject.CreateInstance<BoonDefinition>();
            boon.id = id;
            boon.effect = effect;
            boon.magnitude = magnitude;
            boon.maxStacks = maxStacks;
            _created.Add(boon);
            return boon;
        }

        /// <summary>Mirrors RunDirector.ApplyBoonInventoryToRig's rebuild loop (A4.1/A4.3): replay
        /// RunState.Boons/BoonCount against a (here, small local) catalog lookup into a fresh
        /// BoonInventory, syncing RevivesSpent last — exactly what happens on every arena scene load.</summary>
        private static BoonInventory RebuildInventory(params BoonDefinition[] catalog)
        {
            var inventory = new BoonInventory();
            foreach (string id in RunState.Boons)
            {
                BoonDefinition def = null;
                foreach (var candidate in catalog)
                {
                    if (candidate.id == id) { def = candidate; break; }
                }
                if (def == null) continue;
                int stacks = RunState.BoonCount(id);
                for (int i = 0; i < stacks; i++) inventory.Add(def);
            }
            inventory.SetRevivesSpent(RunState.RevivesSpent);
            return inventory;
        }

        [Test]
        public void InitialState_NotInRun()
        {
            Assert.IsFalse(RunState.InRun);
            Assert.AreEqual(0, RunState.NodeIndex);
            Assert.IsNull(RunState.Map);
            Assert.AreEqual(0, RunState.EchoesEarned);
            Assert.AreEqual(0, RunState.RerollTokens);
            Assert.AreEqual(0, RunState.RevivesSpent);
            Assert.AreEqual(0, RunState.Boons.Count);
        }

        [Test]
        public void Begin_SetsInRunAndGeneratesFullMap()
        {
            RunState.Begin(123);

            Assert.IsTrue(RunState.InRun);
            Assert.AreEqual(123u, RunState.Seed);
            Assert.AreEqual(0, RunState.NodeIndex);
            Assert.AreEqual(RunMapGenerator.TotalNodes, RunState.Map.Count);
            Assert.AreEqual(RunState.Map[0].Index, RunState.CurrentNode.Index);
        }

        [Test]
        public void Begin_MapMatchesRunMapGenerator_ForSameSeed()
        {
            RunNode[] expected = RunMapGenerator.Generate(777);

            RunState.Begin(777);

            Assert.AreEqual(expected.Length, RunState.Map.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Kind, RunState.Map[i].Kind);
            }
        }

        [Test]
        public void Advance_WalksThroughEveryNode_ThenReturnsFalsePastTheEnd()
        {
            RunState.Begin(1);

            for (int step = 0; step < RunMapGenerator.TotalNodes - 1; step++)
            {
                bool advanced = RunState.Advance();
                Assert.IsTrue(advanced, $"expected step {step} to advance");
                Assert.AreEqual(step + 1, RunState.NodeIndex);
            }

            // Now on the final node (Boss). Further Advance calls must return false and not move,
            // repeatedly and without throwing (Advance past the end).
            Assert.IsFalse(RunState.Advance());
            Assert.AreEqual(RunMapGenerator.TotalNodes - 1, RunState.NodeIndex);
            Assert.IsFalse(RunState.Advance());
            Assert.AreEqual(RunMapGenerator.TotalNodes - 1, RunState.NodeIndex);
        }

        [Test]
        public void Advance_BeforeBegin_ReturnsFalse_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Assert.IsFalse(RunState.Advance()));
        }

        [Test]
        public void AddBoon_ThenBoonCount_Stacks()
        {
            RunState.Begin(1);

            RunState.AddBoon(BoonId.KeenEdge);
            RunState.AddBoon(BoonId.KeenEdge);
            RunState.AddBoon(BoonId.Ironskin);

            Assert.AreEqual(2, RunState.BoonCount(BoonId.KeenEdge));
            Assert.AreEqual(1, RunState.BoonCount(BoonId.Ironskin));
            Assert.AreEqual(2, RunState.Boons.Count);
        }

        [Test]
        public void AddBoon_NullOrEmpty_DoesNotThrow_NotCounted()
        {
            RunState.Begin(1);

            Assert.DoesNotThrow(() => RunState.AddBoon(null));
            Assert.DoesNotThrow(() => RunState.AddBoon(""));
            Assert.AreEqual(0, RunState.Boons.Count);
        }

        [Test]
        public void BoonCount_UnknownId_ReturnsZero()
        {
            RunState.Begin(1);

            Assert.AreEqual(0, RunState.BoonCount(BoonId.SecondWind));
        }

        [Test]
        public void AddEchoes_Accumulates()
        {
            RunState.Begin(1);

            RunState.AddEchoes(10);
            RunState.AddEchoes(5);

            Assert.AreEqual(15, RunState.EchoesEarned);
        }

        [Test]
        public void AddEchoes_ClampsBelowZeroToZero()
        {
            RunState.Begin(1);

            RunState.AddEchoes(5);
            RunState.AddEchoes(-100);

            Assert.AreEqual(0, RunState.EchoesEarned);
        }

        [Test]
        public void RerollTokens_AddThenSpend()
        {
            RunState.Begin(1);

            RunState.AddRerollToken(2);
            Assert.AreEqual(2, RunState.RerollTokens);

            Assert.IsTrue(RunState.TrySpendRerollToken());
            Assert.AreEqual(1, RunState.RerollTokens);

            Assert.IsTrue(RunState.TrySpendRerollToken());
            Assert.AreEqual(0, RunState.RerollTokens);

            Assert.IsFalse(RunState.TrySpendRerollToken());
            Assert.AreEqual(0, RunState.RerollTokens);
        }

        [Test]
        public void AddRerollToken_ClampsBelowZeroToZero()
        {
            RunState.Begin(1);

            RunState.AddRerollToken(-5);

            Assert.AreEqual(0, RunState.RerollTokens);
        }

        [Test]
        public void End_ClearsRunScopedState()
        {
            RunState.Begin(1);
            RunState.AddBoon(BoonId.KeenEdge);
            RunState.AddEchoes(10);
            RunState.AddRerollToken(1);
            RunState.NoteReviveSpent();
            RunState.Advance();

            RunState.End();

            Assert.IsFalse(RunState.InRun);
            Assert.AreEqual(0, RunState.NodeIndex);
            Assert.IsNull(RunState.Map);
            Assert.AreEqual(0, RunState.EchoesEarned);
            Assert.AreEqual(0, RunState.RerollTokens);
            Assert.AreEqual(0, RunState.RevivesSpent);
            Assert.AreEqual(0, RunState.Boons.Count);
        }

        [Test]
        public void Reset_ClearsRunScopedState()
        {
            RunState.Begin(1);
            RunState.AddBoon(BoonId.KeenEdge);
            RunState.NoteReviveSpent();

            RunState.Reset();

            Assert.IsFalse(RunState.InRun);
            Assert.AreEqual(0, RunState.RevivesSpent);
            Assert.AreEqual(0, RunState.Boons.Count);
        }

        [Test]
        public void NoteReviveSpent_IncrementsRevivesSpent()
        {
            RunState.Begin(1);

            RunState.NoteReviveSpent();
            Assert.AreEqual(1, RunState.RevivesSpent);

            RunState.NoteReviveSpent();
            Assert.AreEqual(2, RunState.RevivesSpent);
        }

        [Test]
        public void Begin_ClearsRevivesSpentFromAPreviousRun()
        {
            RunState.Begin(1);
            RunState.NoteReviveSpent();
            Assert.AreEqual(1, RunState.RevivesSpent);

            RunState.Begin(2);

            Assert.AreEqual(0, RunState.RevivesSpent);
        }

        // ---- A4.3 required tests ----

        [Test]
        public void Boons_RebuildingInventoryFromRunState_ReproducesIdenticalAggregates()
        {
            RunState.Begin(10);
            var keenEdge = MakeBoon(BoonId.KeenEdge, BoonEffectKind.BladeDamageMultiplier, 0.15f);
            var ironskin = MakeBoon(BoonId.Ironskin, BoonEffectKind.MaxHealthAdd, 20f);
            RunState.AddBoon(BoonId.KeenEdge);
            RunState.AddBoon(BoonId.KeenEdge);
            RunState.AddBoon(BoonId.Ironskin);

            var first = RebuildInventory(keenEdge, ironskin);
            var second = RebuildInventory(keenEdge, ironskin); // simulates the next arena load's rebuild

            Assert.AreEqual(first.BladeDamageMultiplier, second.BladeDamageMultiplier);
            Assert.AreEqual(first.MaxHealthAdd, second.MaxHealthAdd);
        }

        [Test]
        public void RevivesSpent_StaysSpentAcrossARebuild()
        {
            RunState.Begin(11);
            var secondWind = MakeBoon(BoonId.SecondWind, BoonEffectKind.ReviveOnce, 0f, maxStacks: 1);
            RunState.AddBoon(BoonId.SecondWind);

            var first = RebuildInventory(secondWind);
            Assert.IsTrue(first.HasRevive);
            Assert.IsTrue(first.ConsumeRevive()); // spends it — calls RunState.NoteReviveSpent()
            Assert.IsFalse(first.HasRevive);

            var rebuilt = RebuildInventory(secondWind); // simulates the next arena load
            Assert.IsFalse(rebuilt.HasRevive, "a spent revive must stay spent across a rebuild");
        }

        [Test]
        public void MaxHealthAdd_RaisesMaxWithoutFullHealing_AndCarriesStoredHpAcrossANodeHop()
        {
            RunState.Begin(12);
            var go = new GameObject("rig");
            _created.Add(go);
            var health = go.AddComponent<Health>();
            health.Configure(100f);
            health.SetCurrent(40f); // simulate attrition from a fight

            RunState.NotePlayerHealth(health.Current); // captured before leaving the node (A4.2/A7.3)

            // Next node's load: Configure must come first — a fresh MaxHealthAdd raises the ceiling
            // and resets Current to the new Max...
            health.Configure(health.Max + 20f); // e.g. an Ironskin pickup
            Assert.AreEqual(120f, health.Current);

            // ...then the stored HP is restored, clamped to the new max — NOT a full heal.
            health.SetCurrent(RunState.CarryOverHealth(RunState.PlayerHealth, health.Max));
            Assert.AreEqual(40f, health.Current);

            // A shrinking max (or an oversized stored value) must clamp, never overshoot.
            Assert.AreEqual(50f, RunState.CarryOverHealth(90f, 50f));
        }

        [TestCase(40f, 100f, 36f)]
        [TestCase(100f, 100f, 0f)]
        [TestCase(0f, 100f, 60f)]
        public void HealAmountForForge_Is60PercentOfMissingHp(float current, float max, float expected)
        {
            Assert.AreEqual(expected, RunState.HealAmountForForge(current, max), 0.001f);
        }

        [Test]
        public void HealAmountForForge_AppliedThroughHealth_NeverOvershootsMax()
        {
            var go = new GameObject("rig");
            _created.Add(go);
            var health = go.AddComponent<Health>();
            health.Configure(100f);
            health.SetCurrent(40f);

            health.Heal(RunState.HealAmountForForge(health.Current, health.Max));

            Assert.AreEqual(76f, health.Current); // 40 + 60% * (100 - 40) = 40 + 36
            Assert.LessOrEqual(health.Current, health.Max);
        }

        /// <summary>A4.3's original wording asked for "every GrantAbility boon results in
        /// CampaignState.HasAbility(id)" — superseded by A7.7, which found that contract wrong (a run
        /// pick must never permanently unlock a campaign ability). This asserts the corrected
        /// behaviour: the ability is visible through AbilityAccess.Has for the run, but the campaign
        /// save is never touched.</summary>
        [Test]
        public void GrantAbility_MakesAbilityAccessHasTrue_WithoutTouchingCampaignState()
        {
            RunState.Begin(13);

            RunState.GrantAbility(AbilityId.Unbroken);

            Assert.IsTrue(AbilityAccess.Has(AbilityId.Unbroken));
            Assert.IsFalse(CampaignState.HasAbility(AbilityId.Unbroken));
        }

        [Test]
        public void HasGrantedAbility_ClearedByEnd()
        {
            RunState.Begin(14);
            RunState.GrantAbility(AbilityId.Mirror);
            Assert.IsTrue(RunState.HasGrantedAbility(AbilityId.Mirror));

            RunState.End();

            Assert.IsFalse(RunState.HasGrantedAbility(AbilityId.Mirror));
        }

        [Test]
        public void GrantAbility_NullOrEmpty_DoesNotThrow_NotGranted()
        {
            RunState.Begin(15);

            Assert.DoesNotThrow(() => RunState.GrantAbility(null));
            Assert.DoesNotThrow(() => RunState.GrantAbility(""));
            Assert.IsFalse(RunState.HasGrantedAbility(null));
            Assert.IsFalse(RunState.HasGrantedAbility(""));
        }
    }
}
