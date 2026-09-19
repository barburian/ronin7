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
        /// <c>minDepth &lt;= node.Index</c>, and <c>eliteOnly</c> entries only when the node is
        /// Elite/Boss. Forge/Treasure emit an empty list. Boss nodes emit exactly one
        /// <see cref="SpawnRequest"/> (<c>IsBoss = true</c>) picked from <c>bosses[]</c> matching
        /// <c>node.Sector</c>, falling back to the last boss entry if none match.</summary>
        public static List<SpawnRequest> Compose(
            EnemySpawnTable table, RunNode node, float arenaHalfExtent, Vector3 playerSpawn, ref RunRng rng)
        {
            var result = new List<SpawnRequest>();
            if (table == null) return result;

            if (node.Kind == RoomKind.Forge || node.Kind == RoomKind.Treasure)
                return result;

            if (node.Kind == RoomKind.Boss)
            {
                int bossIndex = SelectBossIndex(table.bosses, node.Sector);
                if (bossIndex < 0) return result;
                List<Vector3> bossPos = Positions(1, arenaHalfExtent, playerSpawn, ref rng);
                result.Add(new SpawnRequest(bossIndex, bossPos[0], isBoss: true));
                return result;
            }

            int count = RunScaling.EnemyCount(node.Index, node.Kind, ref rng);
            if (count <= 0) return result;

            List<int> eligible = EligibleTrashIndices(table.trash, node);
            if (eligible.Count == 0) return result;

            List<Vector3> positions = Positions(count, arenaHalfExtent, playerSpawn, ref rng);
            var weights = new int[eligible.Count];
            for (int i = 0; i < eligible.Count; i++)
                weights[i] = table.trash[eligible[i]].weight;

            for (int i = 0; i < count; i++)
            {
                int pick = rng.PickWeighted(weights);
                int entryIndex = eligible[pick >= 0 ? pick : 0];
                result.Add(new SpawnRequest(entryIndex, positions[i], isBoss: false));
            }

            return result;
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
                if (entry.minDepth > node.Index) continue;
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
