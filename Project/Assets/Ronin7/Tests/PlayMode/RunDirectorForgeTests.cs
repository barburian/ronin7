using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Flow;
using Ronin7.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// BUG-02: a zero-enemy Forge/Treasure node publishes <see cref="RoomCleared"/> from
    /// RunArenaController.Start(), before the arena-load hook restores the carried HP onto the fresh
    /// rig. Reproduces that order (clear first, then the onLoaded hook) without a real scene load and
    /// asserts the reward is computed from the carried HP, not the prefab's full HP.
    /// </summary>
    public class RunDirectorForgeTests
    {
        private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        private GameObject directorGo;
        private GameObject rigGo;
        private BoonCatalog catalog;
        private string saveDir;

        [SetUp]
        public void SetUp()
        {
            saveDir = Path.Combine(Path.GetTempPath(), "ronin7_forge_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDir);
            SaveSystem.DirectoryOverride = saveDir;
        }

        [TearDown]
        public void TearDown()
        {
            if (directorGo != null) Object.Destroy(directorGo);
            if (rigGo != null) Object.Destroy(rigGo);
            if (catalog != null) Object.DestroyImmediate(catalog);
            RunState.Reset();
            EventBus.Clear();
            SaveSystem.DirectoryOverride = null;
            if (Directory.Exists(saveDir)) Directory.Delete(saveDir, true);
        }

        /// <summary>P2-04: walks seed 1's branching map to the first node of <paramref name="expectedKind"/>.</summary>
        private IEnumerator ClearZeroEnemyNodeBeforeRigRestore(RoomKind expectedKind, float carriedHp)
        {
            RunState.Begin(1);
            int nodeIndex = -1;
            for (int id = 0; id < RunState.Map.Count && nodeIndex < 0; id++)
                if (RunState.Map[id].Kind == expectedKind) nodeIndex = id;
            Assert.GreaterOrEqual(nodeIndex, 0, $"seed 1 has no {expectedKind} node");
            var walk = RunState.Graph.PathTo(nodeIndex);
            for (int i = 1; i < walk.Count; i++) Assert.IsTrue(RunState.Advance(walk[i]));
            Assert.AreEqual(expectedKind, RunState.CurrentNode.Kind);
            RunState.NotePlayerHealth(carriedHp);

            catalog = ScriptableObject.CreateInstance<BoonCatalog>();
            catalog.boons = new BoonDefinition[0];
            directorGo = new GameObject("RunDirector");
            directorGo.SetActive(false);
            var director = directorGo.AddComponent<RunDirector>();
            typeof(RunDirector).GetField("boonCatalog", Private).SetValue(director, catalog);
            directorGo.SetActive(true);

            // A freshly loaded rig: prefab HP is full, as RunArena's rig is on every load.
            rigGo = new GameObject("Rig");
            rigGo.AddComponent<VRRig>();
            var health = rigGo.AddComponent<Health>();
            health.Configure(100f);
            yield return null;

            // Arena load in flight: the zero-enemy room clears from Start() before onLoaded runs.
            typeof(RunDirector).GetField("awaitingRigRestore", Private).SetValue(director, true);
            EventBus.Publish(new RoomCleared(nodeIndex));
            Assert.AreEqual(nodeIndex, RunState.NodeIndex, "The clear must be parked until the rig is restored.");

            // The parked clear replays once the rig is restored: reward, then the P2-04 path choice.
            typeof(RunDirector).GetMethod("OnArenaLoaded", Private).Invoke(director, null);
            Assert.AreEqual(nodeIndex, RunState.NodeIndex, "No advance until a path is picked.");
            Assert.IsNotNull(typeof(RunDirector).GetField("pathChoices", Private).GetValue(director), "The parked clear must replay once the rig is restored.");

            // Picking a path requests the next load; no GameFlowManager exists in this harness.
            int next = RunState.NextChoices[0];
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("No GameFlowManager"));
            EventBus.Publish(new PathChosen(next));
            Assert.AreEqual(next, RunState.NodeIndex);
        }

        [UnityTest]
        public IEnumerator Forge_HealsFromCarriedHp_NotPrefabFullHp()
        {
            yield return ClearZeroEnemyNodeBeforeRigRestore(RoomKind.Forge, 40f);
            Assert.That(RunState.PlayerHealth, Is.EqualTo(76f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Treasure_KeepsCarriedHp()
        {
            yield return ClearZeroEnemyNodeBeforeRigRestore(RoomKind.Treasure, 40f);
            Assert.That(RunState.PlayerHealth, Is.EqualTo(40f).Within(0.01f));
        }
    }
}
