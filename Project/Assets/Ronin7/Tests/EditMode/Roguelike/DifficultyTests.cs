using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>R13: Easy / Normal / Hard. Pure factors, stacking with Heat (A15.19), defaults, persistence,
    /// snapshot resume, Whip gating, the select-screen selector.</summary>
    public class DifficultyTests
    {
        private string dir;
        private readonly List<UnityEngine.Object> created = new();

        [SetUp]
        public void SetUp()
        {
            RunState.Reset();
            MetaProgression.ResetForTests();
            dir = Path.Combine(Path.GetTempPath(), "ss_diff_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            SaveSystem.DirectoryOverride = dir;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
            RunState.Reset();
            MetaProgression.ResetForTests();
            SaveSystem.DirectoryOverride = null;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        private void WriteMeta(string json) => File.WriteAllText(Path.Combine(dir, "meta.json"), json);

        // --- pure factors ---

        [Test]
        public void Factors_PerMode()
        {
            Assert.AreEqual(0.75f, Difficulty.DamageFactor(DifficultyMode.Easy), 1e-5f);
            Assert.AreEqual(1f, Difficulty.DamageFactor(DifficultyMode.Normal), 1e-5f);
            Assert.AreEqual(1f, Difficulty.DamageFactor(DifficultyMode.Hard), 1e-5f);
            Assert.AreEqual(1.2f, Difficulty.Telegraph(1f, DifficultyMode.Easy), 1e-5f);
            Assert.AreEqual(1f, Difficulty.Telegraph(1f, DifficultyMode.Normal), 1e-5f);
            Assert.AreEqual(0.85f, Difficulty.Telegraph(1f, DifficultyMode.Hard), 1e-5f);
            Assert.AreEqual(-1, Difficulty.EnemyDelta(DifficultyMode.Easy));
            Assert.AreEqual(0, Difficulty.EnemyDelta(DifficultyMode.Normal));
            Assert.AreEqual(1, Difficulty.EnemyDelta(DifficultyMode.Hard));
        }

        [Test]
        public void Telegraph_HardKeepsTheFloor_AndStacksWithHeat()
        {
            Assert.AreEqual(0.6f, Difficulty.Telegraph(0.65f, DifficultyMode.Hard), 1e-5f); // 0.5525 floored
            Assert.AreEqual(0.5f, Difficulty.Telegraph(0.5f, DifficultyMode.Hard), 1e-5f);  // already under: left alone
            // MeleeAttacker order: mode first, then Heat. Hard 1.0 -> 0.85 -> Quick Telegraph rank 2 (x0.81).
            Assert.AreEqual(0.85f * 0.81f, Heat.Telegraph(Difficulty.Telegraph(1f, DifficultyMode.Hard), 2), 1e-5f);
            for (float t = 0.6f; t < 2f; t += 0.05f)
                Assert.GreaterOrEqual(Heat.Telegraph(Difficulty.Telegraph(t, DifficultyMode.Hard), 2), 0.6f - 1e-5f);
            // Easy lengthens before Heat shortens: 1.2 x 0.81.
            Assert.AreEqual(1.2f * 0.81f, Heat.Telegraph(Difficulty.Telegraph(1f, DifficultyMode.Easy), 2), 1e-5f);
        }

        [Test]
        public void ScaledStats_EasyDamage_StacksWithHeat_HealthUntouched()
        {
            RunArenaController.ScaledStats(100f, 10f, 0, RoomKind.Combat, 0, out float hpN, out float dmgN);
            RunArenaController.ScaledStats(100f, 10f, 0, RoomKind.Boss, 0, out _, out float bossN);
            MetaProgression.SelectDifficulty(DifficultyMode.Easy);
            RunState.Begin(5);
            RunArenaController.ScaledStats(100f, 10f, 0, RoomKind.Combat, 0, out float hpE, out float dmgE);
            RunArenaController.ScaledStats(100f, 10f, 0, RoomKind.Boss, 0, out _, out float bossE);
            Assert.AreEqual(hpN, hpE, 1e-4f);
            Assert.AreEqual(dmgN * 0.75f, dmgE, 1e-4f);
            Assert.AreEqual(bossN * 0.75f, bossE, 1e-4f, "bosses too");

            // Heat Sharpened Program rank 2 (+30%) multiplies with Easy.
            RunState.Reset();
            MetaProgression.ApplyFrom(new MetaSaveData { metaRunsWon = 1, heatRanks = new List<int> { 2 }, difficulty = 0 });
            RunState.Begin(5);
            RunArenaController.ScaledStats(100f, 10f, 0, RoomKind.Combat, 0, out _, out float both);
            Assert.AreEqual(dmgN * 1.3f * 0.75f, both, 1e-3f);
        }

        // --- enemy counts ---

        private EnemySpawnTable Table()
        {
            var prefab = new GameObject("Trash");
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            var boss = ScriptableObject.CreateInstance<EnemyDefinition>();
            var table = ScriptableObject.CreateInstance<EnemySpawnTable>();
            created.AddRange(new UnityEngine.Object[] { prefab, def, boss, table });
            table.trash = new[] { new EnemySpawnTable.Entry { prefab = prefab, definition = def, weight = 10 } };
            table.bosses = new[] { new EnemySpawnTable.BossEntry { prefab = prefab, definition = boss, sector = 0 } };
            return table;
        }

        private static int Count(EnemySpawnTable t, RoomKind kind, int delta, int swarm = 0)
        {
            var rng = new RunRng(42);
            return WaveComposer.Compose(t, new RunNode(9, 1, 9, kind), 8f, Vector3.zero, ref rng, false, swarm, delta).Count;
        }

        [Test]
        public void CombatRoom_EasyOneFewer_HardOneMore_StacksWithSwarm_NeverEmpty()
        {
            var t = Table();
            int normal = Count(t, RoomKind.Combat, 0);
            Assert.AreEqual(normal - 1, Count(t, RoomKind.Combat, Difficulty.EnemyDelta(DifficultyMode.Easy)));
            Assert.AreEqual(normal + 1, Count(t, RoomKind.Combat, Difficulty.EnemyDelta(DifficultyMode.Hard)));
            Assert.AreEqual(normal + 2, Count(t, RoomKind.Combat, 1, swarm: 1), "Hard + Swarm rank 1");
            Assert.AreEqual(1, Count(t, RoomKind.Combat, -100), "minimum one enemy");
        }

        [Test]
        public void BossAndEliteRooms_AreNotChangedByMode()
        {
            var t = Table();
            Assert.AreEqual(1, Count(t, RoomKind.Boss, -1));
            Assert.AreEqual(1, Count(t, RoomKind.Boss, 1));
            Assert.AreEqual(Count(t, RoomKind.Elite, 0), Count(t, RoomKind.Elite, 1));
            Assert.AreEqual(Count(t, RoomKind.Elite, 0), Count(t, RoomKind.Elite, -1));
        }

        // --- defaults and persistence ---

        [Test]
        public void Default_NewPlayerEasy_OldWithRunsNormal_OldNoRunsEasy()
        {
            MetaProgression.Load(); // no file
            Assert.AreEqual(DifficultyMode.Easy, MetaProgression.SelectedDifficulty);

            MetaProgression.ResetForTests();
            WriteMeta("{\"version\":1,\"metaRunsCompleted\":3,\"metaRunsWon\":1}");
            MetaProgression.Load();
            Assert.AreEqual(DifficultyMode.Normal, MetaProgression.SelectedDifficulty, "existing save with runs is not moved");

            MetaProgression.ResetForTests();
            WriteMeta("{\"version\":1,\"metaEchoes\":50}");
            MetaProgression.Load();
            Assert.AreEqual(DifficultyMode.Easy, MetaProgression.SelectedDifficulty);
        }

        [Test]
        public void Selection_Persists_AndFirstRunDoesNotFlipTheDefault()
        {
            MetaProgression.Load();
            MetaProgression.SelectDifficulty(DifficultyMode.Hard);
            MetaProgression.Save();
            MetaProgression.ResetForTests();
            MetaProgression.Load();
            Assert.AreEqual(DifficultyMode.Hard, MetaProgression.SelectedDifficulty);

            MetaProgression.ResetForTests();
            File.Delete(Path.Combine(dir, "meta.json"));
            MetaProgression.Load();
            MetaProgression.NoteRunEnded(3, false);
            Assert.AreEqual(DifficultyMode.Easy, MetaProgression.SelectedDifficulty, "a new player who never chose stays on Easy after run 1");
            MetaProgression.Save();
            MetaProgression.ResetForTests();
            MetaProgression.Load();
            Assert.AreEqual(DifficultyMode.Easy, MetaProgression.SelectedDifficulty);
        }

        // --- run lock, snapshot, daily ---

        [Test]
        public void Run_LocksModeAtBegin_DailyIsNormal()
        {
            MetaProgression.SelectDifficulty(DifficultyMode.Hard);
            RunState.Begin(9);
            Assert.AreEqual(DifficultyMode.Hard, RunState.Difficulty);
            MetaProgression.SelectDifficulty(DifficultyMode.Easy);
            Assert.AreEqual(DifficultyMode.Hard, RunState.Difficulty, "changing the pick mid-run does nothing");
            RunState.Begin(Heat.DailySeed(new DateTime(2026, 11, 6)));
            Assert.AreEqual(DifficultyMode.Normal, RunState.Difficulty, "Daily Fracture ignores the mode");
        }

        [Test]
        public void Snapshot_RoundTrips_AndResumeUsesTheSnapshotMode()
        {
            MetaProgression.SelectDifficulty(DifficultyMode.Hard);
            RunState.Begin(11);
            RunSnapshot snap = RunState.ToSnapshot();
            Assert.AreEqual(3, snap.version, "version stays 3");
            Assert.AreEqual((int)DifficultyMode.Hard, snap.difficulty);
            RunState.Reset();
            MetaProgression.SelectDifficulty(DifficultyMode.Easy);
            Assert.IsTrue(RunState.Restore(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(snap))));
            Assert.AreEqual(DifficultyMode.Hard, RunState.Difficulty);
        }

        [Test]
        public void OldV3Snapshot_ResumesAsNormal()
        {
            MetaProgression.SelectDifficulty(DifficultyMode.Hard);
            RunState.Begin(11);
            string json = JsonUtility.ToJson(RunState.ToSnapshot());
            Assert.IsTrue(json.Contains(",\"difficulty\":2"));
            RunState.Reset();
            var old = JsonUtility.FromJson<RunSnapshot>(json.Replace(",\"difficulty\":2", ""));
            Assert.AreEqual((int)DifficultyMode.Normal, old.difficulty);
            Assert.IsTrue(RunState.Restore(old));
            Assert.AreEqual(DifficultyMode.Normal, RunState.Difficulty);
        }

        // --- Whip / wins ---

        [Test]
        public void Easy_WinCountsForShrine_ButNotWhipOrBest()
        {
            MetaProgression.NoteRunEnded(15, true, 0, DifficultyMode.Easy);
            Assert.AreEqual(1, MetaProgression.RunsWon);
            Assert.IsFalse(MetaProgression.IsWeaponUnlocked(WeaponId.Whip));
            Assert.AreEqual(-1, MetaProgression.BestHits);
            MetaProgression.NoteRunEnded(15, true, 2, DifficultyMode.Normal);
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.Whip));
            Assert.AreEqual(2, MetaProgression.BestHits);
        }

        [Test]
        public void Hard_WinUnlocksWhipAndRecordsBest()
        {
            MetaProgression.NoteRunEnded(15, true, 3, DifficultyMode.Hard);
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.Whip));
            Assert.AreEqual(3, MetaProgression.BestHits);
        }

        // --- run log ---

        [Test]
        public void RunLogEntry_CarriesDifficulty_OldLinesAreEmpty()
        {
            var e = new RunLogEntry { difficulty = DifficultyMode.Hard.ToString() };
            Assert.AreEqual("Hard", JsonUtility.FromJson<RunLogEntry>(JsonUtility.ToJson(e)).difficulty);
            Assert.IsTrue(string.IsNullOrEmpty(JsonUtility.FromJson<RunLogEntry>("{\"seed\":1}").difficulty));
        }

        // --- selector ---

        [Test]
        public void Selector_ShowsAndPersistsThePick()
        {
            MetaProgression.Load();
            var cam = new GameObject("Cam", typeof(Camera));
            created.Add(cam);
            var panel = WeaponSelectPanelFactory.Build(cam.GetComponent<Camera>(), requireEventSystem: false);
            Assert.IsNotNull(panel);
            Assert.AreEqual(3, panel.modeButtons.Length);
            Assert.AreEqual("[ EASY ]", panel.modeButtons[0].GetComponentInChildren<TMPro.TMP_Text>().text, "new player defaults to Easy");
            Assert.AreEqual("NORMAL", panel.modeButtons[1].GetComponentInChildren<TMPro.TMP_Text>().text);
            panel.modeButtons[2].onClick.Invoke();
            Assert.AreEqual(DifficultyMode.Hard, MetaProgression.SelectedDifficulty);
            Assert.AreEqual("[ HARD ]", panel.modeButtons[2].GetComponentInChildren<TMPro.TMP_Text>().text);
            Assert.AreEqual(Difficulty.Blurb(DifficultyMode.Hard), panel.modeText.text);
            UnityEngine.Object.DestroyImmediate(panel.gameObject);
        }
    }
}
