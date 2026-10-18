using System;
using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P4-09 Fracture Pressure (heat) + Daily Fracture.</summary>
    public class HeatTests
    {
        private readonly List<UnityEngine.Object> created = new();

        [SetUp]
        public void SetUp()
        {
            MetaProgression.ResetForTests();
            RunState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
            MetaProgression.ResetForTests();
            RunState.Reset();
        }

        /// <summary>Starts a run whose boot-menu pick is <paramref name="ranks"/> of <paramref name="r"/> (heat unlocked).</summary>
        private static void BeginRunWith(HeatRank r, int ranks)
        {
            var heat = new List<int>(new int[Heat.Count]);
            heat[(int)r] = ranks;
            MetaProgression.ApplyFrom(new MetaSaveData { metaRunsWon = 1, heatRanks = heat });
            RunState.Begin(12345u);
        }

        [Test]
        public void Heat_LockedBeforeFirstWin()
        {
            MetaProgression.ApplyFrom(new MetaSaveData { metaRunsWon = 0, heatRanks = new List<int> { 3, 3 } });

            Assert.IsFalse(MetaProgression.HeatUnlocked);
            Assert.IsFalse(MetaProgression.TrySetHeatRank(HeatRank.SharpenedProgram, 1));
            Assert.AreEqual(0, MetaProgression.HeatRankOf(HeatRank.SharpenedProgram));
            RunState.Begin(7u);
            Assert.AreEqual(0, RunState.HeatTotal);
        }

        [Test]
        public void MetaSave_V1WithoutHeatRanks_LoadsDefaults()
        {
            var data = JsonUtility.FromJson<MetaSaveData>("{\"version\":1,\"metaRunsWon\":2}");
            MetaProgression.ApplyFrom(data);

            Assert.AreEqual(1, MetaSaveData.CurrentVersion);
            Assert.IsTrue(MetaProgression.HeatUnlocked);
            for (int i = 0; i < Heat.Count; i++) Assert.AreEqual(0, MetaProgression.HeatRankOf((HeatRank)i));
        }

        [Test]
        public void MetaSave_HeatRanks_RoundTripAndClampToMax()
        {
            MetaProgression.ApplyFrom(new MetaSaveData { metaRunsWon = 1, heatRanks = new List<int> { 9, 2, 1 } });
            var written = new MetaSaveData();
            MetaProgression.WriteTo(written);
            MetaProgression.ResetForTests();
            MetaProgression.ApplyFrom(JsonUtility.FromJson<MetaSaveData>(JsonUtility.ToJson(written)));

            Assert.AreEqual(3, MetaProgression.HeatRankOf(HeatRank.SharpenedProgram)); // clamped to max 3
            Assert.AreEqual(2, MetaProgression.HeatRankOf(HeatRank.HardenedShells));
            Assert.AreEqual(1, MetaProgression.HeatRankOf(HeatRank.QuickTelegraph));
            Assert.AreEqual(0, MetaProgression.HeatRankOf(HeatRank.EscortedBosses));
        }

        [Test]
        public void Heat_MaxRanks_MatchRoadmap()
        {
            int[] expected = { 3, 3, 2, 2, 1, 1, 1, 1 };
            for (int i = 0; i < Heat.Count; i++) Assert.AreEqual(expected[i], Heat.Max((HeatRank)i), ((HeatRank)i).ToString());
        }

        [Test]
        public void Heat_TelegraphFloor_0_6()
        {
            Assert.AreEqual(1f, Heat.Telegraph(1f, 0), 1e-5f);
            Assert.AreEqual(0.9f, Heat.Telegraph(1f, 1), 1e-5f);
            Assert.AreEqual(0.81f, Heat.Telegraph(1f, 2), 1e-5f);
            Assert.AreEqual(0.6f, Heat.Telegraph(0.65f, 2), 1e-5f); // 0.5265 floored
            Assert.AreEqual(0.5f, Heat.Telegraph(0.5f, 2), 1e-5f);  // already under the floor: heat never lengthens it
            for (float t = 0.6f; t < 2f; t += 0.05f) Assert.GreaterOrEqual(Heat.Telegraph(t, 2), 0.6f - 1e-5f);
        }

        [Test]
        public void Heat_Swarm_NeverExceedsMaxConcurrent()
        {
            var prefab = new GameObject("Trash");
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            var table = ScriptableObject.CreateInstance<EnemySpawnTable>();
            created.AddRange(new UnityEngine.Object[] { prefab, def, table });
            table.trash = new[] { new EnemySpawnTable.Entry { prefab = prefab, definition = def, weight = 10 } };
            var node = new RunNode(9, 1, 9, RoomKind.Combat);

            var rng0 = new RunRng(42);
            int baseCount = WaveComposer.Compose(table, node, 8f, Vector3.zero, ref rng0).Count;
            var rng2 = new RunRng(42);
            int swarmCount = WaveComposer.Compose(table, node, 8f, Vector3.zero, ref rng2, false, swarmRanks: 2).Count;
            Assert.AreEqual(baseCount + 2, swarmCount, "Swarm Protocol adds +1 total per rank");

            // Drip release over the bigger room: never more than the cap live at once.
            int alive = 0, queued = swarmCount;
            while (queued > 0)
            {
                int release = RunArenaController.ReleaseCount(alive, queued, RunScaling.MaxConcurrentEnemies);
                alive += release;
                queued -= release;
                Assert.LessOrEqual(alive, RunScaling.MaxConcurrentEnemies);
                alive--; // one dies
            }
            Assert.AreEqual(4, RunScaling.MaxConcurrentEnemies);
        }

        [Test]
        public void Heat_EchoMultiplier_Linear()
        {
            Assert.AreEqual(1f, RunDirector.HeatEchoMultiplier(0), 1e-5f);
            for (int n = 1; n <= 14; n++)
                Assert.AreEqual(1f + 0.1f * n, RunDirector.HeatEchoMultiplier(n), 1e-5f);
            Assert.AreEqual(14, RunDirector.ApplyHeatEchoes(10, 4));
            Assert.AreEqual(10, RunDirector.ApplyHeatEchoes(10, 0));
        }

        [Test]
        public void Heat_RunTotal_DrivesEchoes()
        {
            BeginRunWith(HeatRank.HardenedShells, 3);
            Assert.AreEqual(3, RunState.HeatTotal);
            Assert.AreEqual(13, RunDirector.ApplyHeatEchoes(10, RunState.HeatTotal));
        }

        [Test]
        public void Heat_SharpenedAndHardened_ScaleEveryEnemy()
        {
            RunArenaController.ScaledStats(100f, 10f, 0, RoomKind.Combat, 0, out float hp0, out float dmg0);
            RunArenaController.ScaledStats(100f, 10f, 0, RoomKind.Boss, 0, out float bossHp0, out float bossDmg0);

            BeginRunWith(HeatRank.SharpenedProgram, 2);
            RunArenaController.ScaledStats(100f, 10f, 0, RoomKind.Combat, 0, out float hp, out float dmg);
            Assert.AreEqual(hp0, hp, 1e-3f);
            Assert.AreEqual(dmg0 * 1.3f, dmg, 1e-3f);

            BeginRunWith(HeatRank.HardenedShells, 3);
            RunArenaController.ScaledStats(100f, 10f, 0, RoomKind.Boss, 0, out float bossHp, out float bossDmg);
            Assert.AreEqual(bossHp0 * 1.45f, bossHp, 1e-2f);
            Assert.AreEqual(bossDmg0, bossDmg, 1e-3f);
        }

        [Test]
        public void Heat_RustedForge_Mend30Percent()
        {
            Assert.AreEqual(60f, RunState.HealAmountForForge(0f, 100f), 1e-3f);
            BeginRunWith(HeatRank.RustedForge, 1);
            Assert.AreEqual(30f, RunState.HealAmountForForge(0f, 100f), 1e-3f);
        }

        [Test]
        public void Heat_NoSecondChances_DisablesRevive()
        {
            var boon = ScriptableObject.CreateInstance<BoonDefinition>();
            created.Add(boon);
            boon.id = "boon_second_wind";
            boon.effect = BoonEffectKind.ReviveOnce;
            boon.maxStacks = 1;
            var inv = new BoonInventory();
            inv.Add(boon);
            Assert.IsTrue(inv.HasRevive);

            BeginRunWith(HeatRank.NoSecondChances, 1);
            Assert.IsFalse(inv.HasRevive);
            Assert.IsFalse(inv.ConsumeRevive());
        }

        [Test]
        public void Heat_RunState_ClearedWithRun()
        {
            BeginRunWith(HeatRank.UnforgivingGuard, 1);
            Assert.AreEqual(1, RunState.HeatRankOf(HeatRank.UnforgivingGuard));
            RunState.End();
            Assert.AreEqual(0, RunState.HeatRankOf(HeatRank.UnforgivingGuard));
            Assert.AreEqual(0, RunState.HeatTotal);
        }

        [Test]
        public void DailySeed_StableForDate()
        {
            uint a = Heat.DailySeed(new DateTime(2026, 10, 18, 0, 0, 1, DateTimeKind.Utc));
            uint b = Heat.DailySeed(new DateTime(2026, 10, 18, 23, 59, 59, DateTimeKind.Utc));
            uint next = Heat.DailySeed(new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc));

            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, next);
            Assert.IsTrue(Heat.IsDailySeed(a));
            Assert.IsFalse(Heat.IsDailySeed((uint)int.MaxValue)); // random seeds are < int.MaxValue
            Assert.AreEqual(RunMapGenerator.GenerateGraph(a).Nodes.Length, RunMapGenerator.GenerateGraph(b).Nodes.Length);
        }

        [Test]
        public void DailyFracture_UsesFixedHeatSet_NotMetaPick()
        {
            BeginRunWith(HeatRank.RustedForge, 1); // meta pick ignored on a daily
            RunState.End();
            RunState.Begin(Heat.DailySeed(new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc)));

            int total = 0;
            for (int i = 0; i < Heat.Count; i++)
            {
                Assert.AreEqual(Heat.DailyRank((HeatRank)i), RunState.HeatRankOf((HeatRank)i));
                total += Heat.DailyRank((HeatRank)i);
            }
            Assert.AreEqual(total, RunState.HeatTotal);
            Assert.AreEqual(0, RunState.HeatRankOf(HeatRank.RustedForge));
            Assert.Greater(total, 0);
        }

        [Test]
        public void HeatSelector_CyclesAndWraps()
        {
            Assert.AreEqual(1, MetaShopPanel.NextHeatRank(0, 3));
            Assert.AreEqual(3, MetaShopPanel.NextHeatRank(2, 3));
            Assert.AreEqual(0, MetaShopPanel.NextHeatRank(3, 3));
            Assert.AreEqual("FRACTURE PRESSURE  ECHOES x1.4", MetaShopPanel.HeatTitleText(4));
        }
    }
}
