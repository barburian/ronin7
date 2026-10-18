using System.Collections.Generic;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Pure, deterministic composition of one node's enemy wave: where to place bodies inside the
    /// arena, and which spawn-table entries to use. No Unity randomness, no <c>Time</c> — every
    /// decision flows through the caller's <see cref="RunRng"/> so the same seed reproduces the
    /// same room, forever.
    /// </summary>
    public static class WaveComposer
    {
        private const float MinSeparation = 1.5f;
        private const float MinPlayerDistance = 4f;
        private const float WallMargin = 1f;
        private const int MaxAttemptsPerPoint = 30;

        /// <summary>P2-07: splits <paramref name="n"/> enemies into balanced waves of at most
        /// <paramref name="cap"/> (larger waves first, e.g. 7 -> 3,2,2). Pure and deterministic.
        /// With the next wave due at alive &lt;= 1, concurrency peaks at cap + 1 (= 4 for cap 3).</summary>
        internal static List<int> SplitWaves(int n, int cap)
        {
            var waves = new List<int>();
            if (n <= 0 || cap <= 0) return waves;
            int count = (n + cap - 1) / cap;
            for (int i = 0; i < count; i++) waves.Add(n / count + (i < n % count ? 1 : 0));
            return waves;
        }

        /// <summary>P2-07: the next wave is due once at most one enemy is still alive.</summary>
        internal static bool NextWaveDue(int alive, int wavesRemaining) => wavesRemaining > 0 && alive <= 1;

        /// <summary>Yaw (degrees, Unity convention: 0 = +Z) from <paramref name="fromXZ"/> toward
        /// <paramref name="toXZ"/>; <paramref name="fallback"/> if they coincide.</summary>
        internal static float YawToward(Vector2 fromXZ, Vector2 toXZ, float fallback = 0f)
        {
            Vector2 d = toXZ - fromXZ;
            return d.sqrMagnitude < 1e-6f ? fallback : Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
        }

        /// <summary>P2-06: a spawn point in the player's front arc — radius in [minR, maxR], within
        /// +/- <paramref name="halfAngleDeg"/> of <paramref name="forwardXZ"/>, inside the wall margin, y = 0.
        /// Bounded rejection sampling; false if no sample fits (caller keeps its pre-rolled point).
        /// Arena-local x/z. Pure and deterministic via <paramref name="rng"/>.</summary>
        internal static bool FrontArcPosition(Vector2 headXZ, Vector2 forwardXZ, float minR, float maxR,
            float halfAngleDeg, float arenaHalfExtent, ref RunRng rng, out Vector3 position)
        {
            position = default;
            if (forwardXZ.sqrMagnitude < 1e-6f) return false;
            forwardXZ.Normalize();
            float bound = Mathf.Max(0.1f, arenaHalfExtent - WallMargin);
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float a = (rng.NextFloat() * 2f - 1f) * halfAngleDeg * Mathf.Deg2Rad;
                float r = Mathf.Lerp(minR, maxR, rng.NextFloat());
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                Vector2 dir = new Vector2(forwardXZ.x * c - forwardXZ.y * s, forwardXZ.x * s + forwardXZ.y * c);
                Vector2 p = headXZ + dir * r;
                if (Mathf.Abs(p.x) > bound || Mathf.Abs(p.y) > bound) continue;
                position = new Vector3(p.x, 0f, p.y);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Places <paramref name="count"/> points inside the arena (a <paramref name="arenaHalfExtent"/>
        /// square centred on the origin), each at least <see cref="MinSeparation"/> apart and at least
        /// <see cref="MinPlayerDistance"/> from <paramref name="playerSpawn"/>, with a wall margin.
        /// Bounded rejection sampling (hard-capped attempts per point) falls back to a deterministic
        /// even ring around the player spawn so this can never loop forever and always returns exactly
        /// <paramref name="count"/> positions.
        /// A5.3: returns arena-LOCAL coordinates — <paramref name="playerSpawn"/> must already be in
        /// the arena's local space (the caller converts once via <c>Transform.InverseTransformPoint</c>);
        /// this class does no scene/Transform lookups of its own.
        /// </summary>
        public static List<Vector3> Positions(int count, float arenaHalfExtent, Vector3 playerSpawn, ref RunRng rng)
        {
            var result = new List<Vector3>(Mathf.Max(0, count));
            if (count <= 0) return result;

            float bound = Mathf.Max(0.1f, arenaHalfExtent - WallMargin);

            for (int i = 0; i < count; i++)
            {
                bool placed = false;
                for (int attempt = 0; attempt < MaxAttemptsPerPoint; attempt++)
                {
                    float x = (rng.NextFloat() * 2f - 1f) * bound;
                    float z = (rng.NextFloat() * 2f - 1f) * bound;
                    var candidate = new Vector3(x, 0f, z);
                    if (IsValidPlacement(candidate, result, playerSpawn))
                    {
                        result.Add(candidate);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                    result.Add(FallbackRingPosition(i, count, bound, playerSpawn));
            }

            return result;
        }

        private static bool IsValidPlacement(Vector3 candidate, List<Vector3> placed, Vector3 playerSpawn)
        {
            if ((candidate - playerSpawn).sqrMagnitude < MinPlayerDistance * MinPlayerDistance) return false;
            for (int i = 0; i < placed.Count; i++)
                if ((candidate - placed[i]).sqrMagnitude < MinSeparation * MinSeparation) return false;
            return true;
        }

        /// <summary>A5.4: deterministic fallback ring around the player spawn's x/z. Radius is at
        /// least <see cref="MinPlayerDistance"/> but capped at <c>bound</c> so it can never land
        /// outside the arena or exactly on the wall plane (the old <c>Max(bound, MinPlayerDistance)</c>
        /// could do both). x/z are clamped to the bound and y is forced to 0 — an off-centre or
        /// head-height <paramref name="playerSpawn"/> must never strand an enemy outside the room or
        /// floating in the air, since <c>Enemy.Awake</c> latches <c>groundY</c> permanently.</summary>
        private static Vector3 FallbackRingPosition(int index, int count, float bound, Vector3 playerSpawn)
        {
            float radius = Mathf.Min(Mathf.Max(MinPlayerDistance, bound * 0.6f), bound);
            float angle = (Mathf.PI * 2f * index) / Mathf.Max(1, count);
            float x = Mathf.Clamp(playerSpawn.x + Mathf.Cos(angle) * radius, -bound, bound);
            float z = Mathf.Clamp(playerSpawn.z + Mathf.Sin(angle) * radius, -bound, bound);
            return new Vector3(x, 0f, z);
        }

        /// <summary>What to spawn for one node. Eligible trash entries are those with
        /// <c>minDepth &lt;= node.Depth</c>, and <c>eliteOnly</c> entries only when the node is
        /// Elite/Boss. Forge/Treasure/Shop/Event emit an empty list. Boss nodes emit exactly one
        /// <see cref="SpawnRequest"/> (<c>IsBoss = true</c>) picked from <c>bosses[]</c> matching
        /// <c>node.Sector</c>, falling back to the last boss entry if none match.</summary>
        public static List<SpawnRequest> Compose(
            EnemySpawnTable table, RunNode node, float arenaHalfExtent, Vector3 playerSpawn, ref RunRng rng, bool tutorial = false)
        {
            var result = new List<SpawnRequest>();
            if (table == null) return result;

            if (node.Kind == RoomKind.Forge || node.Kind == RoomKind.Treasure || node.Kind == RoomKind.Shop || node.Kind == RoomKind.Event)
                return result;

            if (node.Kind == RoomKind.Boss)
            {
                int bossIndex = SelectBossIndex(table.bosses, node.Sector);
                if (bossIndex < 0) return result;
                List<Vector3> bossPos = Positions(1, arenaHalfExtent, playerSpawn, ref rng);
                result.Add(new SpawnRequest(bossIndex, bossPos[0], isBoss: true));
                return result;
            }

            if (node.Kind == RoomKind.Elite)
                return ComposeElite(table, node, arenaHalfExtent, playerSpawn, ref rng);

            // P2-08: first run, start node = exactly 2 Scavengers (falls back to any eligible trash if the table has none).
            bool firstRoom = tutorial && node.Kind == RoomKind.Combat && node.Depth == 0;
            int count = firstRoom ? 2 : RunScaling.EnemyCount(node.Depth, node.Kind, ref rng);
            if (count <= 0) return result;

            List<int> eligible = EligibleTrashIndices(table.trash, node);
            if (firstRoom)
            {
                var scavengers = eligible.FindAll(i => IsScavenger(table.trash[i]));
                if (scavengers.Count > 0) eligible = scavengers;
            }
            if (eligible.Count == 0) return result;

            List<Vector3> positions = Positions(count, arenaHalfExtent, playerSpawn, ref rng);
            var weights = new int[eligible.Count];
            for (int i = 0; i < eligible.Count; i++)
                weights[i] = table.trash[eligible[i]].weight;

            var used = new int[table.trash.Length];
            for (int i = 0; i < count; i++)
            {
                int entryIndex = PickCapped(table.trash, eligible, weights, used, ref rng);
                if (entryIndex < 0) break; // every eligible entry is at its per-room cap
                result.Add(new SpawnRequest(entryIndex, positions[i], isBoss: false));
            }

            return result;
        }

        private static bool IsScavenger(EnemySpawnTable.Entry e) =>
            e.definition.name.Contains("Scavenger") || e.prefab.name.Contains("Scavenger");

        /// <summary>P1-09 (BUG-09): one elite first (an eliteOnly pick when eligible, else the strongest
        /// eligible trash by maxHealth), then escorts drawn from non-eliteOnly trash — 2 in sectors 0-1,
        /// 3 in sector 2. Falls back to all eligible entries if no plain trash is eligible.</summary>
        private static List<SpawnRequest> ComposeElite(
            EnemySpawnTable table, RunNode node, float arenaHalfExtent, Vector3 playerSpawn, ref RunRng rng)
        {
            var result = new List<SpawnRequest>();
            List<int> eligible = EligibleTrashIndices(table.trash, node);
            if (eligible.Count == 0) return result;

            var eliteOnly = new List<int>();
            var plain = new List<int>();
            foreach (int i in eligible) (table.trash[i].eliteOnly ? eliteOnly : plain).Add(i);

            int leadIndex;
            if (eliteOnly.Count > 0)
                leadIndex = eliteOnly[Mathf.Max(0, rng.PickWeighted(Weights(table.trash, eliteOnly)))];
            else
            {
                leadIndex = eligible[0];
                foreach (int i in eligible)
                    if (table.trash[i].definition.maxHealth > table.trash[leadIndex].definition.maxHealth) leadIndex = i;
            }

            List<int> escortPool = plain.Count > 0 ? plain : eligible;
            int[] escortWeights = Weights(table.trash, escortPool);
            int escorts = node.Sector >= 2 ? 3 : 2;

            List<Vector3> positions = Positions(1 + escorts, arenaHalfExtent, playerSpawn, ref rng);
            result.Add(new SpawnRequest(leadIndex, positions[0], isBoss: false, isElite: true));
            var used = new int[table.trash.Length];
            for (int i = 0; i < escorts; i++)
            {
                int entryIndex = PickCapped(table.trash, escortPool, escortWeights, used, ref rng);
                if (entryIndex < 0) break;
                result.Add(new SpawnRequest(entryIndex, positions[1 + i], isBoss: false));
            }
            return result;
        }

        /// <summary>P3-04: weighted pick that honours <c>Entry.maxPerRoom</c> (0 = unlimited): once an entry
        /// reaches its cap its weight is zeroed. Returns the trash index, or -1 when nothing is pickable.
        /// RNG use is identical to a plain pick while no cap is hit.</summary>
        private static int PickCapped(EnemySpawnTable.Entry[] trash, List<int> pool, int[] weights, int[] used, ref RunRng rng)
        {
            int pick = rng.PickWeighted(weights);
            if (pick < 0) return -1;
            int index = pool[pick];
            int cap = trash[index].maxPerRoom;
            if (cap > 0 && ++used[index] >= cap) weights[pick] = 0;
            return index;
        }

        private static int[] Weights(EnemySpawnTable.Entry[] trash, List<int> indices)
        {
            var w = new int[indices.Count];
            for (int i = 0; i < w.Length; i++) w[i] = trash[indices[i]].weight;
            return w;
        }

        /// <summary>A5.1: excludes entries with a null <c>prefab</c> or null <c>definition</c>
        /// (nothing usable to spawn) and entries with <c>weight &lt;= 0</c> — previously a zero-weight
        /// entry was spawned exclusively whenever it was the only eligible one, the exact opposite of
        /// what setting weight 0 means.</summary>
        private static List<int> EligibleTrashIndices(EnemySpawnTable.Entry[] trash, RunNode node)
        {
            var eligible = new List<int>();
            if (trash == null) return eligible;

            bool eliteAllowed = node.Kind == RoomKind.Elite || node.Kind == RoomKind.Boss;
            for (int i = 0; i < trash.Length; i++)
            {
                EnemySpawnTable.Entry entry = trash[i];
                if (entry == null) continue;
                if (entry.prefab == null || entry.definition == null) continue;
                if (entry.weight <= 0) continue;
                if (entry.minDepth > node.Depth) continue;
                if (entry.eliteOnly && !eliteAllowed) continue;
                eligible.Add(i);
            }
            return eligible;
        }

        /// <summary>A5.1: null-prefab bosses are excluded from both the sector match and the
        /// last-entry fallback — a null prefab in either path is what let a Boss node clear silently
        /// with no fight (winning the run with no boss).</summary>
        private static int SelectBossIndex(EnemySpawnTable.BossEntry[] bosses, int sector)
        {
            if (bosses == null || bosses.Length == 0) return -1;

            int lastUsable = -1;
            for (int i = 0; i < bosses.Length; i++)
            {
                EnemySpawnTable.BossEntry entry = bosses[i];
                if (entry == null || entry.prefab == null) continue;
                lastUsable = i;
                if (entry.sector == sector) return i;
            }
            return lastUsable; // fall back to the last usable boss entry, or -1 if none
        }
    }
}
