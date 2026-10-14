using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    /// PlayMode lifecycle harness for <see cref="RunArenaController"/> (A5.8): the concurrency-cap
    /// drip (5 queued, exactly 4 active, kill one, the 5th activates, all dead clears the room and
    /// publishes <see cref="RoomCleared"/> exactly once) and the null-prefab-table soft-lock
    /// guard (A5.1 — the node must still clear rather than trap the player).
    /// </summary>
    public class RunArenaLifecycleTests
    {
        private readonly List<GameObject> _spawned = new();
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.Destroy(go);
            _spawned.Clear();

            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();

            RunState.Reset();
            EventBus.Clear();
        }

        // ---- Test fixture builders ----

        private void AddMainCamera()
        {
            var camGo = new GameObject("Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            _spawned.Add(camGo);
        }

        private EnemyDefinition MakeDefinition()
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 50f;
            def.damage = 5f;
            _created.Add(def);
            return def;
        }

        /// <summary>A minimal but real spawnable-enemy source: Health + Enemy with weapon/bladeTip
        /// wired via reflection (both are private/protected serialized fields with no public setter —
        /// mirrors this suite's established KatanaHolsterTests/PostureMeterTests convention for
        /// injecting refs before Awake runs). Created inactive, mirroring A3.1's "prefab root baked
        /// inactive" contract so Awake only fires once <see cref="RunArenaController"/> activates a
        /// clone of it.</summary>
        private GameObject BuildEnemyPrefabSource(string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            _spawned.Add(go);

            go.AddComponent<Health>();

            var weapon = new GameObject("Weapon").transform;
            weapon.SetParent(go.transform, false);
            var bladeTip = new GameObject("BladeTip").transform;
            bladeTip.SetParent(weapon, false);

            var enemy = go.AddComponent<Enemy>();
            typeof(MeleeAttacker).GetField("weapon", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(enemy, weapon);
            typeof(Enemy).GetField("bladeTip", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(enemy, bladeTip);

            return go;
        }

        private ArenaRoomLibrary MakeEmptyRoomLibrary()
        {
            var library = ScriptableObject.CreateInstance<ArenaRoomLibrary>();
            library.biomes = new ArenaRoomLibrary.Biome[0]; // ForSector -> null -> greybox, no props, no rng draws
            var surface = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default")); // ArenaGeometryBuilder logs an error without one
            library.surfaceMaterial = surface;
            _created.Add(surface);
            _created.Add(library);
            return library;
        }

        private RunArenaController CreateController(ArenaRoomLibrary roomLibrary, EnemySpawnTable spawnTable)
        {
            var arenaGo = new GameObject("RunArena");
            arenaGo.SetActive(false); // inject fields before Awake/Start run
            _spawned.Add(arenaGo);

            var controller = arenaGo.AddComponent<RunArenaController>();
            var type = typeof(RunArenaController);
            type.GetField("roomLibrary", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, roomLibrary);
            type.GetField("spawnTable", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, spawnTable);

            arenaGo.SetActive(true); // Awake runs now; Start runs next frame
            return controller;
        }

        // ---- Tests ----

        [UnityTest]
        public IEnumerator FiveQueued_FourActivate_KillOne_FifthActivates_AllDeadClearsRoom()
        {
            AddMainCamera();

            // P2-04: pick seed 1's first sector-2 Combat node whose own RunRng substream makes
            // RunScaling.EnemyCount return exactly 5 (3 base + 2 per-sector bonus + 0). The empty-biome
            // room library consumes zero rng draws before that, so the arena composes exactly 5.
            RunState.Begin(1);
            int target = -1;
            for (int id = 0; id < RunState.Map.Count && target < 0; id++)
            {
                RunNode n = RunState.Map[id];
                if (n.Sector != 2 || n.Kind != RoomKind.Combat) continue;
                RunRng probe = RunRng.ForNode(1, id);
                if (RunScaling.EnemyCount(n.Depth, n.Kind, ref probe) == 5) target = id;
            }
            Assert.GreaterOrEqual(target, 0, "seed 1 has no 5-enemy sector-2 Combat node");
            var walk = RunState.Graph.PathTo(target);
            for (int i = 1; i < walk.Count; i++) RunState.Advance(walk[i]);
            Assert.AreEqual(target, RunState.NodeIndex);
            Assert.AreEqual(RoomKind.Combat, RunState.CurrentNode.Kind);

            var table = ScriptableObject.CreateInstance<EnemySpawnTable>();
            _created.Add(table);
            table.trash = new[]
            {
                new EnemySpawnTable.Entry
                {
                    prefab = BuildEnemyPrefabSource("EnemyPrefab"),
                    definition = MakeDefinition(),
                    weight = 10,
                    minDepth = 0,
                    eliteOnly = false,
                },
            };

            int roomClearedCount = 0;
            System.Action<RoomCleared> onCleared = _ => roomClearedCount++;
            EventBus.Subscribe(onCleared);

            RunArenaController controller = CreateController(MakeEmptyRoomLibrary(), table);
            yield return null; // Start() runs

            Health[] all = controller.GetComponentsInChildren<Health>(true);
            Assert.AreEqual(5, all.Length, "Expected all 5 enemies instantiated (queued + active).");

            int ActiveCount() => controller.GetComponentsInChildren<Health>(false).Length;
            Assert.AreEqual(4, ActiveCount(), "Exactly 4 should be active up front — the concurrency cap.");
            Assert.IsFalse(controller.IsCleared);

            // Kill one of the four active enemies.
            Health firstVictim = System.Array.Find(all, h => h.gameObject.activeSelf);
            firstVictim.ApplyDamage(new DamageInfo(9999f, Vector3.zero, Vector3.forward, null));

            Assert.AreEqual(5, controller.GetComponentsInChildren<Health>(false).Length,
                "The 5th (previously queued) enemy must activate immediately on the first death.");
            Assert.IsFalse(controller.IsCleared, "Four enemies are still alive.");

            // Kill everything else.
            foreach (Health h in all)
            {
                if (h.IsAlive) h.ApplyDamage(new DamageInfo(9999f, Vector3.zero, Vector3.forward, null));
            }

            Assert.IsTrue(controller.IsCleared, "All 5 enemies are dead — the room must be cleared.");
            Assert.AreEqual(1, roomClearedCount, "RoomCleared must publish exactly once.");

            EventBus.Unsubscribe(onCleared);
        }

        [UnityTest]
        public IEnumerator CombatNode_AllNullPrefabTrashEntries_StillClearsInsteadOfSoftLocking()
        {
            AddMainCamera();

            // Seed 2026, node 0 is Combat (A2.5 golden vector: "C C E F B ..." — index 0 is 'C').
            RunState.Begin(2026);
            Assert.AreEqual(RoomKind.Combat, RunState.CurrentNode.Kind);

            var table = ScriptableObject.CreateInstance<EnemySpawnTable>();
            _created.Add(table);
            table.trash = new[]
            {
                new EnemySpawnTable.Entry { prefab = null, definition = MakeDefinition(), weight = 10 },
                new EnemySpawnTable.Entry { prefab = null, definition = MakeDefinition(), weight = 10 },
            };

            int roomClearedCount = 0;
            System.Action<RoomCleared> onCleared = _ => roomClearedCount++;
            EventBus.Subscribe(onCleared);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("composed/spawned zero enemies"));
            RunArenaController controller = CreateController(MakeEmptyRoomLibrary(), table);
            yield return null; // Start() runs

            Assert.IsTrue(controller.IsCleared, "A Combat node with only null-prefab entries must still clear, not soft-lock.");
            Assert.AreEqual(1, roomClearedCount);

            EventBus.Unsubscribe(onCleared);
        }
    }
}
