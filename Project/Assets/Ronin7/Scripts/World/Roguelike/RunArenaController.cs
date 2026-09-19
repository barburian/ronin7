using System;
using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Scene component on the RunArena scene root. In <see cref="Start"/>: builds the room shell
    /// and instantiates the node's full enemy wave (all inactive, A5.5), then drip-activates up to
    /// <see cref="RunScaling.MaxConcurrentEnemies"/> at once — one more per death — so the arena is
    /// never assembled or expanded mid-gameplay. Tracks alive enemies purely via each activated
    /// enemy's <see cref="Health.Died"/> (subscribe on activation, never a per-frame scan). Raises
    /// <see cref="Cleared"/> and publishes <see cref="RoomCleared"/> once the node's total is
    /// exhausted and no enemy remains alive; a Forge/Treasure node (zero enemies) clears immediately.
    /// </summary>
    public class RunArenaController : MonoBehaviour
    {
        [SerializeField] private ArenaRoomLibrary roomLibrary;
        [SerializeField] private EnemySpawnTable spawnTable;
        [SerializeField] private float arenaHalfExtent = ArenaGeometryBuilder.DefaultHalfExtent;
        [SerializeField] private Transform playerSpawn;

        public event Action Cleared;
        public bool IsCleared { get; private set; }

        /// <summary>One instantiated-but-not-yet-activated enemy, paired with its scaled runtime
        /// <see cref="EnemyDefinition"/> clone so it can be destroyed alongside the enemy on death
        /// (A5.7). A plain list, not a <c>Dictionary&lt;Health,...&gt;</c> keyed for later removal —
        /// A5.6 found that Unity's fake-null equality breaks that removal pattern; here entries are
        /// only ever taken from the front, never looked up by key.</summary>
        private readonly struct QueuedEnemy
        {
            public readonly Health Health;
            public readonly EnemyDefinition Definition;
            public QueuedEnemy(Health health, EnemyDefinition definition) { Health = health; Definition = definition; }
        }

        private readonly List<QueuedEnemy> queued = new();
        private int aliveCount;
        private int currentNodeIndex;

        private void Start()
        {
            if (roomLibrary == null)
            {
                Debug.LogError("[RunArenaController] No ArenaRoomLibrary assigned.", this);
                return;
            }
            if (spawnTable == null)
            {
                Debug.LogError("[RunArenaController] No EnemySpawnTable assigned.", this);
                return;
            }
            if (!RunState.InRun)
            {
                Debug.LogError("[RunArenaController] Started outside an active run (RunState.InRun is false).", this);
                return;
            }

            RunNode node = RunState.CurrentNode;
            currentNodeIndex = node.Index;
            RunRng rng = RunRng.ForNode(RunState.Seed, node.Index);

            ArenaGeometryBuilder.Build(transform, arenaHalfExtent, roomLibrary.ForSector(node.Sector), ref rng);

            // A5.3: geometry above is built in the controller's own local space; playerSpawn (and the
            // exclusion centre below) are world positions, so convert once here — this only works
            // correctly while the arena root sits at world identity, which every callsite (the
            // RunArena scene root) guarantees.
            Vector3 exclusionCentreLocal = transform.InverseTransformPoint(ResolveExclusionCentre());

            List<SpawnRequest> requests = WaveComposer.Compose(spawnTable, node, arenaHalfExtent, exclusionCentreLocal, ref rng);
            foreach (SpawnRequest request in requests)
                SpawnEnemy(request, node);

            if (queued.Count == 0)
            {
                // A5.1: only Forge/Treasure may legitimately produce zero enemies. A Combat/Elite/Boss
                // node reaching here (whether WaveComposer composed nothing, or every spawn attempt
                // failed) would otherwise clear silently — a Boss node "winning" the run with no
                // fight, or a Combat/Elite node with a broken table doing the same. Clearing is still
                // the safe failure (a soft-lock is worse), but it must not be silent.
                if (node.Kind == RoomKind.Combat || node.Kind == RoomKind.Elite || node.Kind == RoomKind.Boss)
                    Debug.LogError($"[RunArenaController] Node {node.Index} ({node.Kind}) composed/spawned zero enemies — clearing rather than soft-locking the run.", this);
                MarkCleared();
                return;
            }

            ActivateFront(ReleaseCount(0, queued.Count, RunScaling.MaxConcurrentEnemies));
        }

        /// <summary>A5.7: the no-spawn exclusion centre should track the live player rig, not a
        /// stale scene transform — otherwise an enemy can materialise inside wherever the player
        /// actually is at fade-in, the worst possible VR moment. <see cref="Camera.main"/> is the
        /// live head position and needs no Player-assembly reference; <c>playerSpawn</c> is the
        /// fallback, then the arena root.</summary>
        private Vector3 ResolveExclusionCentre()
        {
            if (Camera.main != null)
            {
                Vector3 pos = Camera.main.transform.position;
                pos.y = 0f;
                return pos;
            }
            if (playerSpawn != null)
            {
                Debug.LogWarning("[RunArenaController] Camera.main is null — falling back to playerSpawn for the enemy exclusion centre.", this);
                return playerSpawn.position;
            }
            Debug.LogWarning("[RunArenaController] Camera.main and playerSpawn are both null — using the arena root for the enemy exclusion centre.", this);
            return transform.position;
        }

        private void SpawnEnemy(SpawnRequest request, RunNode node)
        {
            ResolveEntry(request, out GameObject prefab, out EnemyDefinition definitionAsset);

            if (prefab == null)
            {
                Debug.LogError($"[RunArenaController] Spawn table entry {request.EntryIndex} (boss={request.IsBoss}) has no prefab.", this);
                return;
            }

            // A3.1 spawn order — do not reorder: Instantiate (prefab root baked inactive by Module 5,
            // so Awake does not fire yet) -> Configure(scaledRuntimeCopy) -> position/parent ->
            // SetActive(true). Activation (the last step) is deferred to ActivateFront so at most
            // RunScaling.MaxConcurrentEnemies are ever live at once (A5.5); the first three steps
            // happen here so every queued instance sits fully configured and positioned while still
            // inactive under the black fade.
            GameObject instance = Instantiate(prefab);
            instance.transform.SetParent(transform, false);
            instance.transform.localPosition = request.Position; // arena-local (A5.3)

            EnemyDefinition scaled = BuildScaledDefinition(definitionAsset, node);
            if (scaled != null)
            {
                Enemy enemy = instance.GetComponent<Enemy>();
                if (enemy != null) enemy.Configure(scaled); // A3.1 — no reflection
            }

            Health health = instance.GetComponent<Health>();
            if (health == null)
            {
                // A5.1: an instance with no Health can never die, so it would otherwise sit in the
                // scene forever, chasing and damaging the player, uncounted and unkillable.
                Debug.LogError($"[RunArenaController] Spawned '{prefab.name}' has no Health component — destroying it.", this);
                if (scaled != null) Destroy(scaled);
                Destroy(instance);
                return;
            }

            queued.Add(new QueuedEnemy(health, scaled));
        }

        private void ResolveEntry(SpawnRequest request, out GameObject prefab, out EnemyDefinition definitionAsset)
        {
            prefab = null;
            definitionAsset = null;

            if (request.IsBoss)
            {
                if (spawnTable.bosses != null && request.EntryIndex >= 0 && request.EntryIndex < spawnTable.bosses.Length)
                {
                    EnemySpawnTable.BossEntry entry = spawnTable.bosses[request.EntryIndex];
                    prefab = entry?.prefab;
                    definitionAsset = entry?.definition;
                }
            }
            else
            {
                if (spawnTable.trash != null && request.EntryIndex >= 0 && request.EntryIndex < spawnTable.trash.Length)
                {
                    EnemySpawnTable.Entry entry = spawnTable.trash[request.EntryIndex];
                    prefab = entry?.prefab;
                    definitionAsset = entry?.definition;
                }
            }
        }

        /// <summary>Builds a scaled runtime copy of the enemy's definition by RunScaling's
        /// depth/kind multipliers — never mutates the shared asset on disk. A3.1/A5.7: applying it is
        /// left to <see cref="Enemy.Configure"/> + the enemy's own (deferred) <c>Awake</c>, which also
        /// configures Health/PostureMeter from it — calling <c>Health.Configure</c> here too would be
        /// redundant and would hide the definition-swap-timing bug A3.1 fixed.</summary>
        private static EnemyDefinition BuildScaledDefinition(EnemyDefinition source, RunNode node)
        {
            if (source == null) return null;

            float healthMul = RunScaling.HealthMultiplier(node.Index, node.Kind);
            float damageMul = RunScaling.DamageMultiplier(node.Index, node.Kind);

            EnemyDefinition scaled = Instantiate(source);
            scaled.maxHealth = source.maxHealth * healthMul;
            scaled.damage = source.damage * damageMul;
            return scaled;
        }

        /// <summary>Activates up to <paramref name="count"/> queued enemies (front of the queue
        /// first — spawn order), subscribing each one's <see cref="Health.Died"/> at activation time
        /// rather than instantiate time (A5.5), so the alive set and the queue stay disjoint.</summary>
        private void ActivateFront(int count)
        {
            for (int i = 0; i < count && queued.Count > 0; i++)
            {
                QueuedEnemy entry = queued[0];
                queued.RemoveAt(0);

                entry.Health.gameObject.SetActive(true); // Awake runs now, reading the Configure'd definition
                Health health = entry.Health;
                EnemyDefinition definition = entry.Definition;
                health.Died += () => OnEnemyDied(health, definition);
                aliveCount++;
            }
        }

        /// <summary>A5.5/A5.6: the concurrency-cap drip release. <see cref="Health.Died"/> fires
        /// synchronously inside <see cref="Health.ApplyDamage"/>, so deaths are never actually
        /// simultaneous — this handler always completes before the next one starts, which is what
        /// makes reading <see cref="aliveCount"/> fresh (via <see cref="ReleaseCount"/>) correct.</summary>
        private void OnEnemyDied(Health health, EnemyDefinition definition)
        {
            aliveCount--;
            if (definition != null) Destroy(definition); // A5.7 — no orphaned runtime clone

            ActivateFront(ReleaseCount(aliveCount, queued.Count, RunScaling.MaxConcurrentEnemies));

            if (aliveCount == 0 && queued.Count == 0) MarkCleared();
        }

        /// <summary>Pure seam (A3.3/A5.5/A5.8): how many queued enemies to activate right now, given
        /// how many are currently alive and how many remain queued, capped at
        /// <paramref name="maxConcurrent"/>. Never negative, never exceeds
        /// <paramref name="queuedRemaining"/>, and 0 once <paramref name="aliveNow"/> has already
        /// reached the cap.</summary>
        internal static int ReleaseCount(int aliveNow, int queuedRemaining, int maxConcurrent)
        {
            int room = maxConcurrent - aliveNow;
            if (room <= 0) return 0;
            return Mathf.Min(room, Mathf.Max(0, queuedRemaining));
        }

        private void MarkCleared()
        {
            if (IsCleared) return;
            IsCleared = true;
            Cleared?.Invoke();
            EventBus.Publish(new RoomCleared(currentNodeIndex));
        }
    }
}
