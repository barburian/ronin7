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
    /// <see cref="RunScaling.MaxConcurrentEnemies"/> at once — in waves of &lt;= 3 (P2-07), the next
    /// when &lt;= 1 is alive — so the arena is never assembled or expanded mid-gameplay. Tracks alive enemies purely via each activated
    /// enemy's <see cref="Health.Died"/> (subscribe on activation, never a per-frame scan). Raises
    /// <see cref="Cleared"/> and publishes <see cref="RoomCleared"/> once the node's total is
    /// exhausted and no enemy remains alive; a Forge/Treasure node (zero enemies) clears immediately.
    /// </summary>
    public class RunArenaController : MonoBehaviour
    {
        [SerializeField] private ArenaRoomLibrary roomLibrary;
        [SerializeField] private EnemySpawnTable spawnTable;
        /// <summary>Debug override (m); applies only when &gt; 0. Otherwise <see cref="RunScaling.ArenaHalfExtent"/> by node kind (P2-05).</summary>
        [SerializeField] private float arenaHalfExtent;
        private float halfExtent;
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
            /// <summary>P3-06: elite carries the Volatile affix (death ring + blast).</summary>
            public readonly bool Volatile;
            /// <summary>P3-07: the boss's later-phase runtime clones (null for everything else).</summary>
            public readonly EnemyDefinition[] Phases;
            public QueuedEnemy(Health health, EnemyDefinition definition, bool isVolatile = false, EnemyDefinition[] phases = null)
            { Health = health; Definition = definition; Volatile = isVolatile; Phases = phases; }
        }

        // P3-07: the boss, its adds (instantiated inactive in Start like every other enemy — A5.5: never
        // Instantiate inside the blade-contact frame that triggers the phase change) and its north-wall bar.
        private Health boss;
        private readonly List<QueuedEnemy> reservedAdds = new();
        private float[] bossPhaseHealth; // scaled HP per phase, in order
        private int bossPhase;
        private Transform bossBarFill;
        private float bossBarShown = -1f;
        private const float BossBarWidth = 6f, BossBarHeight = 0.2f, BossBarY = 3.7f;

        private readonly List<QueuedEnemy> queued = new();
        /// <summary>Activated, not-yet-dead enemies, for the 1 Hz out-of-bounds sweep (BUG-04).</summary>
        private readonly List<Health> alive = new();
        private int aliveCount;
        private const int WaveSize = 3;
        private List<int> waves = new();
        private int nextWave;

        /// <summary>Margin (m) past the arena wall, and the floor depth, beyond which an enemy is lost.</summary>
        private const float OutOfArenaMargin = 0.5f;
        private const float OutOfArenaFloorY = -2f;
        private int currentNodeIndex;

        // P2-06: dequeued-but-not-yet-active enemies (telegraph showing). Scaled time (Time.time) on
        // purpose: TimeScaleArbiter slow-mo (hit-stop, Overdrive) and pause stretch/suspend the delay
        // together with the enemy AI and animation it gates, so a spawn never "lands" mid-freeze.
        private struct PendingSpawn { public QueuedEnemy Entry; public float StartTime; public int Disc; }
        private readonly List<PendingSpawn> pending = new();
        private const float SpawnDelay = 1.2f;
        private const int DiscPoolSize = 4; // <= 3 telegraphs at once: a wave is <= 3 and the next waits on pending
        private Renderer[] discs;
        private int placedCount;
        private uint runSeed;

        // P2-04 exit gates: node ids behind each raised arch (null = no gates up) and the gate the
        // head was in last frame, so only stepping/teleporting INTO an arch picks it.
        private int[] gateChoices;
        private GameObject gatesRoot;
        private int lastGate = -1;
        private readonly List<RoomKind> gateKinds = new(3);

        private void OnEnable()
        {
            EventBus.Subscribe<PathChoiceOffered>(OnPathChoiceOffered);
            EventBus.Subscribe<BossPhaseChanged>(OnBossPhaseChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PathChoiceOffered>(OnPathChoiceOffered);
            EventBus.Unsubscribe<BossPhaseChanged>(OnBossPhaseChanged);
        }

        /// <summary>P3-07: the first phase change releases the boss's adds through the normal wave path.</summary>
        private void OnBossPhaseChanged(BossPhaseChanged e)
        {
            if (boss == null || e.Boss != boss.gameObject) return;
            bossPhase = e.Phase;
            SpawnAdds();
        }

        /// <summary>P3-07: moves the pre-instantiated adds into the queue as extra waves (<= WaveSize each) and releases it
        /// via <see cref="ActivateNextWave"/> → <see cref="ActivateFront"/>, so the
        /// <see cref="RunScaling.MaxConcurrentEnemies"/> cap, telegraphs and clear logic all apply.</summary>
        private void SpawnAdds()
        {
            if (reservedAdds.Count == 0) return;
            queued.AddRange(reservedAdds);
            waves.AddRange(WaveComposer.SplitWaves(reservedAdds.Count, WaveSize)); // waves <= WaveSize so none strand behind the release cap
            reservedAdds.Clear();
            if (WaveDue(aliveCount, pending.Count, waves.Count - nextWave)) ActivateNextWave();
            EventBus.Publish(new RoomProgress(Remaining));
        }

        /// <summary>P2-04: raise one arch per choice on the north wall (after the reward is resolved).</summary>
        private void OnPathChoiceOffered(PathChoiceOffered offer)
        {
            if (offer.Choices == null || offer.Choices.Length == 0 || RunState.Map == null) return;
            if (gatesRoot != null) Destroy(gatesRoot);
            gateKinds.Clear();
            foreach (int id in offer.Choices) gateKinds.Add(RunState.Map[id].Kind);
            if (halfExtent <= 0f) halfExtent = arenaHalfExtent > 0f ? arenaHalfExtent : RunScaling.ArenaHalfExtent(RunState.CurrentNode.Kind);
            gatesRoot = ArenaGeometryBuilder.BuildExitGates(transform, gateKinds, halfExtent,
                roomLibrary != null ? roomLibrary.surfaceMaterial : null);
            gateChoices = offer.Choices;
            GetHeadLocal(out Vector2 head, out _);
            lastGate = ArenaGeometryBuilder.GateIndexAt(head, gateChoices.Length, halfExtent);
        }

        /// <summary>Head-in-arch poll (no allocation). Fires <see cref="PathChosen"/> once.</summary>
        private void PollGates()
        {
            GetHeadLocal(out Vector2 head, out _);
            int gate = ArenaGeometryBuilder.GateIndexAt(head, gateChoices.Length, halfExtent);
            if (gate >= 0 && gate != lastGate)
            {
                int id = gateChoices[gate];
                gateChoices = null;
                EventBus.Publish(new PathChosen(id));
                return;
            }
            lastGate = gate;
        }

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
            runSeed = RunState.Seed;
            RunRng rng = RunRng.ForNode(RunState.Seed, node.Index);

            // Audio (and anything else) reacts to the node's sector/kind via the EventBus rather than a
            // hard reference into this assembly — see RunNodeEntered's doc comment.
            EventBus.Publish(new RunNodeEntered(node.Sector, node.Kind));

            halfExtent = arenaHalfExtent > 0f ? arenaHalfExtent : RunScaling.ArenaHalfExtent(node.Kind);
            ArenaGeometryBuilder.Build(transform, halfExtent, roomLibrary.ForSector(node.Sector), roomLibrary.surfaceMaterial, ref rng);

            // A5.3: geometry above is built in the controller's own local space; playerSpawn (and the
            // exclusion centre below) are world positions, so convert once here — this only works
            // correctly while the arena root sits at world identity, which every callsite (the
            // RunArena scene root) guarantees.
            Vector3 exclusionCentreLocal = transform.InverseTransformPoint(ResolveExclusionCentre());

            // P2-08: first run only (IsLoaded guard: never touches a save that was not loaded this session).
            bool tutorial = MetaProgression.IsLoaded && !MetaProgression.TutorialDone && node.Depth == 0 && node.Kind == RoomKind.Combat;
            if (tutorial) TutorialPrompt.Create(transform, halfExtent);
            List<SpawnRequest> requests = WaveComposer.Compose(spawnTable, node, halfExtent, exclusionCentreLocal, ref rng, tutorial);
            foreach (SpawnRequest request in requests)
            {
                SpawnEnemy(request, node);
                if (request.IsBoss) ReserveBossAdds(request, node);
            }

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

            waves = WaveComposer.SplitWaves(queued.Count, WaveSize);
            if (node.Kind == RoomKind.Elite) BuildBlastPool(); // pre-build the Volatile rings, not in the kill frame
            ActivateNextWave();
            if (boss != null) BuildBossBar();
            EventBus.Publish(new RoomProgress(Remaining));
            // Scaled time: a paused run (timeScale 0) suspends the sweep along with everything else.
            InvokeRepeating(nameof(CheckBounds), 1f, 1f);
        }

        /// <summary>
        /// BUG-04: an enemy knocked through a wall or below the floor can never reach the player and
        /// never dies, so the room never clears — a soft-lock. Kill it through the normal damage path
        /// so the usual death/drip/clear bookkeeping runs.
        /// </summary>
        private void CheckBounds()
        {
            // Backwards: a kill removes its entry (OnEnemyDied) and may append newly activated ones.
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                if (i >= alive.Count) continue;
                Health h = alive[i];
                if (h == null || !h.IsAlive) continue;
                Vector3 local = transform.InverseTransformPoint(h.transform.position);
                if (!OutOfArena(local, halfExtent)) continue;
                Debug.LogWarning($"[RunArenaController] '{h.name}' left the arena at {local}; killing it so the room can clear.", this);
                h.ApplyDamage(new DamageInfo(h.Max + h.Current + 1f, h.transform.position, Vector3.down, null));
            }
        }

        /// <summary>True if an arena-local position is past a wall (plus margin) or below the floor.
        /// Pure. BUG-04 test seam.</summary>
        internal static bool OutOfArena(Vector3 local, float halfExtent)
        {
            float limit = halfExtent + OutOfArenaMargin;
            return Mathf.Abs(local.x) > limit || Mathf.Abs(local.z) > limit || local.y < OutOfArenaFloorY;
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

            EnemyDefinition scaled = BuildScaledDefinition(definitionAsset, node, request.IsElite, request.IsBoss, runSeed, out bool isVolatile);
            Enemy enemy = instance.GetComponent<Enemy>();
            if (scaled != null && enemy != null) enemy.Configure(scaled, halfExtent); // A3.1 (P3-04: halfExtent bounds Displace) — no reflection

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

            if (enemy != null) enemy.SeedTelegraph(RunRng.ForNode(runSeed ^ 0x7E1E6Au, node.Index * 31 + queued.Count).NextUInt()); // P3-07 feints
            EnemyDefinition[] phases = request.IsBoss && scaled != null && enemy != null ? BuildBossPhases(request, node, scaled, enemy) : null;
            if (request.IsBoss) boss = health;
            queued.Add(new QueuedEnemy(health, scaled, isVolatile, phases));
        }

        /// <summary>P3-07: clones and scales the boss entry's later phases (see <see cref="PhaseHealths"/>),
        /// re-scales the first phase to its share and arms <see cref="Enemy.ConfigurePhases"/>. Null when
        /// the entry has no usable phases (single-phase boss, behaviour unchanged).</summary>
        private EnemyDefinition[] BuildBossPhases(SpawnRequest request, RunNode node, EnemyDefinition first, Enemy enemy)
        {
            EnemySpawnTable.BossEntry entry = spawnTable.bosses[request.EntryIndex];
            if (entry.phases == null) return null;
            var sources = new List<EnemyDefinition>(entry.phases.Length + 1) { entry.definition };
            foreach (EnemyDefinition p in entry.phases) if (p != null) sources.Add(p);
            if (sources.Count == 1) return null;

            var weights = new float[sources.Count];
            for (int i = 0; i < weights.Length; i++) weights[i] = sources[i].maxHealth;
            bossPhaseHealth = PhaseHealths(first.maxHealth, weights); // first.maxHealth = BossTargetHealth (P1-10)
            first.maxHealth = bossPhaseHealth[0];

            var clones = new EnemyDefinition[sources.Count - 1];
            for (int i = 0; i < clones.Length; i++)
            {
                clones[i] = Instantiate(sources[i + 1]);
                clones[i].maxHealth = bossPhaseHealth[i + 1];
                clones[i].damage = sources[i + 1].damage * RunScaling.BossDamageMultiplier(node.Sector);
            }
            enemy.ConfigurePhases(clones);
            return clones;
        }

        /// <summary>Pure (P3-07) share rule: phase i gets <paramref name="total"/> x weight_i / sum(weights),
        /// where each weight is that phase asset's own maxHealth (so the authored per-phase numbers only set
        /// proportions — the total is always RunScaling.BossTargetHealth). Any non-positive weight → equal shares.</summary>
        internal static float[] PhaseHealths(float total, float[] weights)
        {
            float sum = 0f;
            bool valid = weights.Length > 0;
            foreach (float w in weights) { if (!(w > 0f)) valid = false; sum += w; }
            var hp = new float[weights.Length];
            for (int i = 0; i < hp.Length; i++) hp[i] = valid ? total * weights[i] / sum : total / hp.Length;
            return hp;
        }

        /// <summary>P3-07: instantiates the boss entry's adds now (inactive, under the fade) and holds them
        /// out of the queue until the first <see cref="BossPhaseChanged"/>.</summary>
        private void ReserveBossAdds(SpawnRequest bossRequest, RunNode node)
        {
            // Only a multi-phase boss that just spawned (a single-phase boss never changes phase, so no adds).
            if (bossPhaseHealth == null || queued.Count == 0 || queued[queued.Count - 1].Health != boss || reservedAdds.Count > 0) return;
            EnemySpawnTable.BossEntry entry = spawnTable.bosses[bossRequest.EntryIndex];
            if (entry.addEntry < 0 || spawnTable.trash == null || entry.addEntry >= spawnTable.trash.Length) return;
            int before = queued.Count;
            for (int i = 0; i < entry.addCount; i++)
                SpawnEnemy(new SpawnRequest(entry.addEntry, bossRequest.Position, isBoss: false), node);
            reservedAdds.AddRange(queued.GetRange(before, queued.Count - before));
            queued.RemoveRange(before, queued.Count - before);
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
        private static EnemyDefinition BuildScaledDefinition(EnemyDefinition source, RunNode node,
            bool isElite, bool isBoss, uint seed, out bool isVolatile)
        {
            isVolatile = false;
            if (source == null) return null;

            ScaledStats(source.maxHealth, source.damage, node.Depth, node.Kind, node.Sector, out float health, out float damage, isBoss);

            EnemyDefinition scaled = Instantiate(source);
            scaled.maxHealth = health;
            scaled.damage = damage;

            if (isElite)
            {
                // P3-06: affixes mutate the clone only; own RNG stream so map/spawn goldens never move.
                RunRng affixRng = EliteAffixes.Stream(seed, node.Index);
                foreach (EliteAffix a in EliteAffixes.Pick(node.Sector, ref affixRng))
                {
                    EliteAffixes.Apply(scaled, a);
                    isVolatile |= a == EliteAffix.Volatile;
                }
            }
            return scaled;
        }

        /// <summary>Pure seam for <see cref="BuildScaledDefinition"/> (EditMode never runs Awake).
        /// P1-10: Boss nodes use the per-sector target bands (total HP, not a multiplier on base);
        /// every other kind uses the depth multipliers. Posture follows via Enemy.Awake reading
        /// definition.PostureMax = scaled maxHealth x postureMaxFraction.</summary>
        internal static void ScaledStats(float baseHealth, float baseDamage, int depth, RoomKind kind, int sector,
            out float health, out float damage, bool isBoss = true)
        {
            if (kind == RoomKind.Boss && isBoss) // P3-07: boss adds on a Boss node take the depth multipliers instead
            {
                health = RunScaling.BossTargetHealth(sector);
                damage = baseDamage * RunScaling.BossDamageMultiplier(sector);
                return;
            }
            health = baseHealth * RunScaling.HealthMultiplier(depth, kind);
            damage = baseDamage * RunScaling.DamageMultiplier(depth, kind);
        }

        private int Remaining => aliveCount + pending.Count + queued.Count;

        /// <summary>P2-07: the single wave-activation point. Takes the next wave's size, clamped by the
        /// A5.5 cap via <see cref="ReleaseCount"/>. Pending (telegraphing) enemies count toward the cap.</summary>
        private void ActivateNextWave()
        {
            int size = waves[nextWave++];
            ActivateFront(Mathf.Min(size, ReleaseCount(aliveCount + pending.Count, queued.Count, RunScaling.MaxConcurrentEnemies)));
        }

        /// <summary>P2-06: moves up to <paramref name="count"/> queued enemies (spawn order) into the
        /// pending list: each is re-placed in the player's front arc (substream per enemy, so seeded and
        /// deterministic; falls back to its pre-rolled point) and gets a floor-disc + audio telegraph.
        /// <see cref="Update"/> activates it <see cref="SpawnDelay"/> later. The initial wave takes this
        /// same path, so the first enemies also telegraph (during the fade-in).</summary>
        private void ActivateFront(int count)
        {
            for (int i = 0; i < count && queued.Count > 0; i++)
            {
                QueuedEnemy entry = queued[0];
                queued.RemoveAt(0);

                Transform t = entry.Health.transform;
                RunRng rng = RunRng.ForNode(runSeed, currentNodeIndex * 31 + placedCount++);
                GetHeadLocal(out Vector2 head, out Vector2 fwd);
                if (WaveComposer.FrontArcPosition(head, fwd, 4f, 7f, 70f, halfExtent, ref rng, out Vector3 p))
                    t.localPosition = p;
                Vector3 local = t.localPosition;
                local.y = 0f;
                t.localPosition = local;

                int disc = ShowTelegraph(local);
                EventBus.Publish(new EnemySpawning(transform.TransformPoint(local)));
                pending.Add(new PendingSpawn { Entry = entry, StartTime = Time.time, Disc = disc });
            }
        }

        private void GetHeadLocal(out Vector2 head, out Vector2 fwd)
        {
            head = Vector2.zero;
            fwd = Vector2.up;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 hp = transform.InverseTransformPoint(cam.transform.position);
            Vector3 hf = transform.InverseTransformDirection(cam.transform.forward);
            head = new Vector2(hp.x, hp.z);
            if (hf.x * hf.x + hf.z * hf.z > 1e-4f) fwd = new Vector2(hf.x, hf.z);
        }

        private int ShowTelegraph(Vector3 local)
        {
            if (discs == null)
            {
                discs = new Renderer[DiscPoolSize];
                for (int d = 0; d < DiscPoolSize; d++)
                {
                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    go.name = "SpawnTelegraph";
                    Destroy(go.GetComponent<Collider>());
                    go.transform.SetParent(transform, false);
                    Renderer r = go.GetComponent<Renderer>();
                    r.sharedMaterial = VrUiMaterials.SharedUnlit;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    RendererTint.Apply(r, new Color(2.5f, 0.35f, 0.1f, 1f)); // HDR orange-red: reads as danger, not a pickup
                    go.SetActive(false);
                    discs[d] = r;
                }
            }
            for (int d = 0; d < discs.Length; d++)
            {
                if (discs[d].gameObject.activeSelf) continue;
                discs[d].transform.localPosition = new Vector3(local.x, 0.02f, local.z);
                discs[d].transform.localScale = new Vector3(0.3f, 0.01f, 0.3f);
                discs[d].gameObject.SetActive(true);
                return d;
            }
            return -1; // pool exhausted (not reachable by the cap maths): spawn untelegraphed rather than fail
        }

        // P3-06 Volatile: tiny fixed pool of floor rings (pre-created on first use, reused; the
        // same SharedUnlit material as the spawn telegraph, tinted via MPB). Blast = Health.Active scan.
        private struct Blast { public Renderer Ring; public Vector3 Centre; public float Start, Damage; public bool Live; }
        private const int BlastPoolSize = 3;
        private Blast[] blasts;

        private void BuildBlastPool()
        {
            blasts = new Blast[BlastPoolSize];
            for (int i = 0; i < BlastPoolSize; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "VolatileRing";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                Renderer r = go.GetComponent<Renderer>();
                r.sharedMaterial = VrUiMaterials.SharedUnlit;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                RendererTint.Apply(r, new Color(3f, 1.1f, 0.1f, 1f)); // HDR orange
                go.SetActive(false);
                blasts[i].Ring = r;
            }
        }

        private void StartBlast(Vector3 centre, float damage)
        {
            if (blasts == null) BuildBlastPool();
            for (int i = 0; i < blasts.Length; i++)
            {
                if (blasts[i].Live) continue;
                blasts[i].Live = true;
                blasts[i].Centre = centre;
                blasts[i].Start = Time.time;
                blasts[i].Damage = damage;
                Transform t = blasts[i].Ring.transform;
                t.position = new Vector3(centre.x, transform.position.y + 0.03f, centre.z);
                t.localScale = new Vector3(0.3f, 0.01f, 0.3f);
                t.gameObject.SetActive(true);
                return;
            }
            // pool exhausted (cap of 4 alive makes this near-unreachable): skip the blast rather than allocate
        }

        private void UpdateBlasts(float now)
        {
            for (int i = 0; i < blasts.Length; i++)
            {
                if (!blasts[i].Live) continue;
                float k = (now - blasts[i].Start) / EliteAffixes.RingSeconds;
                if (k < 1f)
                {
                    float d = EliteAffixes.RingDiameter(k);
                    blasts[i].Ring.transform.localScale = new Vector3(d, 0.01f, d);
                    continue;
                }
                blasts[i].Live = false;
                blasts[i].Ring.gameObject.SetActive(false);
                Vector3 c = blasts[i].Centre;
                float blastDamage = blasts[i].Damage; // locals: ApplyDamage can kill a Volatile and reuse this slot
                for (int h = Health.Active.Count - 1; h >= 0; h--) // backwards: ApplyDamage may disable a Health
                {
                    if (h >= Health.Active.Count) continue;
                    Health hp = Health.Active[h];
                    if (hp == null || !hp.IsAlive || !EliteAffixes.InBlast(c, hp.transform.position)) continue;
                    hp.ApplyDamage(new DamageInfo(blastDamage, hp.transform.position, Vector3.up, null));
                }
            }
        }

        private void Update()
        {
            if (gateChoices != null) PollGates();
            if (bossBarFill != null) UpdateBossBar();
            float now = Time.time;
            if (blasts != null) UpdateBlasts(now);
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingSpawn ps = pending[i];
                if (!ReleaseSchedule(now, ps.StartTime, SpawnDelay))
                {
                    if (ps.Disc >= 0)
                    {
                        float k = Mathf.Clamp01((now - ps.StartTime) / SpawnDelay);
                        float d = Mathf.Lerp(0.3f, 1.6f, k);
                        discs[ps.Disc].transform.localScale = new Vector3(d, 0.01f, d);
                    }
                    continue;
                }
                pending.RemoveAt(i);
                if (ps.Disc >= 0) discs[ps.Disc].gameObject.SetActive(false);
                Activate(ps.Entry);
            }
        }

        /// <summary>Activates one enemy, subscribing its <see cref="Health.Died"/> at activation time
        /// (A5.5) so the alive set and the queue/pending sets stay disjoint.</summary>
        private void Activate(QueuedEnemy entry)
        {
            entry.Health.gameObject.SetActive(true); // Awake runs now, reading the Configure'd definition
            Health health = entry.Health;
            EnemyDefinition definition = entry.Definition;
            bool isVolatile = entry.Volatile;
            EnemyDefinition[] phases = entry.Phases;
            health.Died += () => OnEnemyDied(health, definition, isVolatile, phases);
            alive.Add(health);
            aliveCount++;
            if (EnemyHealthBar.Wants(health, boss)) EnemyHealthBar.Attach(health, transform); // P3-08 (bosses use the north-wall bar)
        }

        /// <summary>P3-07: diegetic boss bar on the north wall — a dark backing quad, a fill quad scaled from the
        /// left edge by <see cref="HealthBarFill"/> of the whole fight (all phases), and thin markers at the
        /// phase thresholds. Runtime-built like the spawn discs: shared unlit material, colour via MPB
        /// (<see cref="RendererTint"/>), no Canvas, no new Material.</summary>
        private void BuildBossBar()
        {
            var root = new GameObject("BossBar").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, BossBarY, halfExtent - 0.25f); // just inside the north wall, above the exit arches
            BarQuad(root, "Back", Vector3.zero, new Vector2(BossBarWidth + 0.08f, BossBarHeight + 0.08f), new Color(0.05f, 0.02f, 0.02f, 1f));
            bossBarFill = BarQuad(root, "Fill", new Vector3(0f, 0f, -0.01f), new Vector2(BossBarWidth, BossBarHeight), new Color(2.2f, 0.25f, 0.15f, 1f));
            if (bossPhaseHealth == null) return;
            float total = 0f, acc = 0f;
            foreach (float h in bossPhaseHealth) total += h;
            for (int i = 0; i < bossPhaseHealth.Length - 1; i++)
            {
                acc += bossPhaseHealth[i];
                float x = (0.5f - HealthBarFill(acc, total)) * BossBarWidth; // phase i ends when the bar drains to here
                BarQuad(root, "PhaseMark", new Vector3(x, 0f, -0.02f), new Vector2(0.04f, BossBarHeight + 0.16f), new Color(1.6f, 1.6f, 1.6f, 1f));
            }
        }

        private static Transform BarQuad(Transform parent, string name, Vector3 local, Vector2 size, Color c)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad); // faces -Z: readable from inside the room
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = VrUiMaterials.SharedUnlit;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            RendererTint.Apply(r, c);
            return go.transform;
        }

        private void UpdateBossBar()
        {
            float remaining = boss.Current, total = boss.Max;
            if (bossPhaseHealth != null)
            {
                total = 0f;
                foreach (float h in bossPhaseHealth) total += h;
                for (int i = bossPhase + 1; i < bossPhaseHealth.Length; i++) remaining += bossPhaseHealth[i];
            }
            float fill = boss.gameObject.activeSelf ? HealthBarFill(remaining, total) : 1f; // still telegraphing: Health not configured yet
            if (Mathf.Approximately(fill, bossBarShown)) return;
            bossBarShown = fill;
            bossBarFill.localScale = new Vector3(BossBarWidth * fill, BossBarHeight, 1f);
            bossBarFill.localPosition = new Vector3((fill - 1f) * 0.5f * BossBarWidth, 0f, -0.01f); // drains toward the left edge
        }

        /// <summary>Pure (P3-07): bar fill fraction, clamped to 0..1 (0 when max is not positive).</summary>
        internal static float HealthBarFill(float current, float max) => max > 0f ? Mathf.Clamp01(current / max) : 0f;

        /// <summary>Pure seam (P2-06): true once <paramref name="delay"/> seconds have passed since <paramref name="startTime"/>.</summary>
        internal static bool ReleaseSchedule(float now, float startTime, float delay) => now >= startTime + delay;

        /// <summary>Pure seam (P2-06): the next wave is due on the P2-07 rule counting scheduled-but-inactive enemies as present.</summary>
        internal static bool WaveDue(int alive, int pendingCount, int wavesRemaining) =>
            WaveComposer.NextWaveDue(alive + pendingCount, wavesRemaining);

        /// <summary>A5.5/A5.6: the concurrency-cap drip release. <see cref="Health.Died"/> fires
        /// synchronously inside <see cref="Health.ApplyDamage"/>, so deaths are never actually
        /// simultaneous — this handler always completes before the next one starts, which is what
        /// makes reading <see cref="aliveCount"/> fresh (via <see cref="ReleaseCount"/>) correct.</summary>
        private void OnEnemyDied(Health health, EnemyDefinition definition, bool isVolatile, EnemyDefinition[] phases)
        {
            aliveCount--;
            alive.Remove(health);
            if (isVolatile) StartBlast(health.transform.position, definition != null ? definition.damage : 0f);
            if (definition != null) Destroy(definition); // A5.7 — no orphaned runtime clone
            if (phases != null) foreach (EnemyDefinition p in phases) if (p != null) Destroy(p); // P3-07: every phase clone too
            if (health == boss && bossBarFill != null) bossBarFill.parent.gameObject.SetActive(false);

            if (WaveDue(aliveCount, pending.Count, waves.Count - nextWave)) ActivateNextWave();

            EventBus.Publish(new RoomProgress(Remaining));
            if (Remaining == 0) MarkCleared();
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
