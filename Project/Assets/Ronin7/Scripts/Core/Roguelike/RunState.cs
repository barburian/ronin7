using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Static, in-session state for the run currently in progress (mirrors <see cref="CampaignState"/>'s
    /// static-class pattern). Session-scoped: <see cref="End"/> clears it when a run finishes (win or
    /// permadeath), so it never leaks into the next run or back into the hub.
    /// </summary>
    public static class RunState
    {
        private static RunMap graph;
        private static ReadOnlyCollection<RunNode> mapView;
        private static int nodeIndex;
        private static readonly List<int> path = new();
        private static float maxHealthBonus;
        private static uint seed;
        private static int echoesEarned;
        private static int rerollTokens;
        private static int revivesSpent;
        private static bool unbrokenUsed;
        private static float playerHealth = -1f;
        private static readonly Dictionary<string, int> boonCounts = new();
        private static readonly HashSet<string> grantedAbilities = new();

        public static bool InRun { get; private set; }
        public static uint Seed => seed;
        /// <summary>P2-04: the current node's id in <see cref="Graph"/> (not its depth — see <see cref="Depth"/>).</summary>
        public static int NodeIndex => nodeIndex;

        /// <summary>P2-04: rows cleared before this node, 0..14 (sector x 5 + row). Scaling, summary,
        /// meta best-depth and the HUD read this, never the id.</summary>
        public static int Depth => CurrentNode.Depth;

        /// <summary>The node at <see cref="NodeIndex"/>. Returns <c>default</c> (a plausible-looking
        /// <c>{0, 0, 0, Combat}</c> node) when called outside a run — callers must check
        /// <see cref="InRun"/> first rather than trusting this value on its own.</summary>
        public static RunNode CurrentNode => graph != null && nodeIndex < graph.Nodes.Length ? graph.Nodes[nodeIndex] : default;

        /// <summary>A2.6: wrapped with <see cref="System.Array.AsReadOnly{T}"/> on <see cref="Begin"/>
        /// so a caller can't downcast this back to the live <c>RunNode[]</c> and rewrite the run.
        /// Indexed by node id.</summary>
        public static IReadOnlyList<RunNode> Map => mapView;

        /// <summary>P2-04: the branching graph (null outside a run). Treat as read-only.</summary>
        public static RunMap Graph => graph;

        /// <summary>P2-04: ids reachable from the current node (empty on the final Boss / outside a run).</summary>
        public static IReadOnlyList<int> NextChoices => graph != null ? graph.Next[nodeIndex] : System.Array.Empty<int>();

        /// <summary>P2-04: node ids visited this run, start first, current last.</summary>
        public static IReadOnlyList<int> Path => path;

        /// <summary>P2-04 events (Blood Pact): flat max-HP change for the rest of the run, applied on
        /// top of boons at every node load.</summary>
        public static float MaxHealthBonus => maxHealthBonus;
        public static void AddMaxHealthBonus(float amount) => maxHealthBonus += amount;
        public static int EchoesEarned => echoesEarned;
        public static int RerollTokens => rerollTokens;
        public static int RevivesSpent => revivesSpent;

        /// <summary>P1-07: Unbroken is once per run in the roguelike; durable across node reloads.</summary>
        public static bool UnbrokenUsed => unbrokenUsed;
        public static void NoteUnbrokenUsed() => unbrokenUsed = true;

        /// <summary>Durable player HP, carried across the fresh scene load every node performs
        /// (A4.2). Negative means "unset" — spawn at full HP, which is also the state at run start
        /// and after <see cref="ClearPlayerHealth"/>.</summary>
        public static float PlayerHealth => playerHealth;

        public static IReadOnlyCollection<string> Boons => boonCounts.Keys;

        /// <summary>Records that a run-picked Epic boon grants this ability for the run only (A7.7:
        /// unlike a story unlock this must never touch <see cref="CampaignState"/> — a run pick must
        /// not permanently unlock a campaign ability, and a completed-campaign player must not find the
        /// rarest boon tier a dead pick). Cleared with the run. Read via
        /// <see cref="Ronin7.Core.AbilityAccess.Has"/>, never CampaignState directly.</summary>
        public static void GrantAbility(string abilityId)
        {
            if (!string.IsNullOrEmpty(abilityId)) grantedAbilities.Add(abilityId);
        }

        public static bool HasGrantedAbility(string abilityId) =>
            !string.IsNullOrEmpty(abilityId) && grantedAbilities.Contains(abilityId);

        /// <summary>Starts a new run: generates the map from the seed and resets all run-scoped counters.</summary>
        public static void Begin(uint runSeed)
        {
            seed = runSeed;
            graph = RunMapGenerator.GenerateGraph(runSeed);
            mapView = System.Array.AsReadOnly(graph.Nodes);
            nodeIndex = graph.StartId;
            path.Clear();
            path.Add(nodeIndex);
            maxHealthBonus = 0f;
            InRun = true;
            echoesEarned = 0;
            rerollTokens = 0;
            revivesSpent = 0;
            unbrokenUsed = false;
            playerHealth = -1f;
            boonCounts.Clear();
            grantedAbilities.Clear();
        }

        /// <summary>P2-04: moves along the edge to <paramref name="nextId"/>. Returns false (and does not
        /// move) outside a run or when <paramref name="nextId"/> is not a successor of the current node.</summary>
        public static bool Advance(int nextId)
        {
            if (!InRun || graph == null || !graph.HasEdge(nodeIndex, nextId)) return false;
            nodeIndex = nextId;
            path.Add(nextId);
            return true;
        }

        /// <summary>Boons may stack — repeated ids increment their count rather than being deduped.</summary>
        public static void AddBoon(string boonId)
        {
            if (string.IsNullOrEmpty(boonId)) return;
            boonCounts.TryGetValue(boonId, out int count);
            boonCounts[boonId] = count + 1;
        }

        public static int BoonCount(string boonId)
        {
            if (string.IsNullOrEmpty(boonId)) return 0;
            return boonCounts.TryGetValue(boonId, out int count) ? count : 0;
        }

        /// <summary>Clamps negatives to 0 rather than letting EchoesEarned go below zero.</summary>
        public static void AddEchoes(int amount)
        {
            echoesEarned = Mathf.Max(0, echoesEarned + amount);
        }

        /// <summary>Clamps negatives to 0, mirroring AddEchoes.</summary>
        public static void AddRerollToken(int amount)
        {
            rerollTokens = Mathf.Max(0, rerollTokens + amount);
        }

        /// <summary>P2-04 shop/shrine: spend this run's unbanked Echoes. False (no change) if short.</summary>
        public static bool TrySpendEchoes(int amount)
        {
            if (amount < 0 || echoesEarned < amount) return false;
            echoesEarned -= amount;
            return true;
        }

        public static bool TrySpendRerollToken()
        {
            if (rerollTokens <= 0) return false;
            rerollTokens--;
            return true;
        }

        /// <summary>Records that a revive was consumed. Durable across the arena scene reload that
        /// rebuilds <c>BoonInventory</c> every node — without this, a transient spent-revive counter
        /// would resurrect a used Second Wind on the next node (infinite revives).</summary>
        public static void NoteReviveSpent() => revivesSpent++;

        /// <summary>Records the player's current HP immediately before leaving a node (before the
        /// fade/load starts, while the rig still exists) — see Roguelike-Design.md A4.2. Without this
        /// the rig rebuilds at full HP on every node's fresh scene load, which guts attrition and
        /// makes permadeath nearly unreachable.</summary>
        public static void NotePlayerHealth(float current) => playerHealth = current;

        /// <summary>Back to "unset" (spawn at full HP).</summary>
        public static void ClearPlayerHealth() => playerHealth = -1f;

        /// <summary>Captures everything needed to rebuild this run exactly (the map comes back from
        /// the seed). <see cref="RunSnapshot.runKills"/> is left for the caller, which owns that count.</summary>
        public static RunSnapshot ToSnapshot()
        {
            var snapshot = new RunSnapshot
            {
                version = RunSnapshot.CurrentVersion,
                seed = seed,
                nodeIndex = nodeIndex,
                depth = Depth,
                maxHealthBonus = maxHealthBonus,
                echoesEarned = echoesEarned,
                rerollTokens = rerollTokens,
                revivesSpent = revivesSpent,
                unbrokenUsed = unbrokenUsed,
                playerHealth = playerHealth,
            };
            foreach (KeyValuePair<string, int> boon in boonCounts)
                for (int i = 0; i < boon.Value; i++) snapshot.boons.Add(boon.Key);
            snapshot.grantedAbilities.AddRange(grantedAbilities);
            snapshot.path.AddRange(path);
            return snapshot;
        }

        /// <summary>Rebuilds the run from <paramref name="snapshot"/>. False (and no run) if it is
        /// null, from another <see cref="RunSnapshot.CurrentVersion"/>, or its path is not a walk along
        /// the seed's map ending at <see cref="RunSnapshot.nodeIndex"/>.</summary>
        public static bool Restore(RunSnapshot snapshot)
        {
            if (snapshot == null || snapshot.version != RunSnapshot.CurrentVersion) return false;

            Begin(snapshot.seed);
            if (!ReplayPath(snapshot.path) || nodeIndex != snapshot.nodeIndex)
            {
                Reset();
                return false;
            }
            maxHealthBonus = snapshot.maxHealthBonus;
            echoesEarned = snapshot.echoesEarned;
            rerollTokens = snapshot.rerollTokens;
            revivesSpent = snapshot.revivesSpent;
            unbrokenUsed = snapshot.unbrokenUsed;
            playerHealth = snapshot.playerHealth;
            foreach (string boonId in snapshot.boons) AddBoon(boonId);
            foreach (string abilityId in snapshot.grantedAbilities) GrantAbility(abilityId);
            return true;
        }

        /// <summary>Clears run-scoped state at the end of a run (win or permadeath). Callers bank
        /// EchoesEarned into <see cref="MetaProgression"/> before calling this.</summary>
        public static void End() => ClearRun();

        /// <summary>Hard reset for tests/menu. Same effect as End(); kept as a separate entry point
        /// per the CampaignState convention.</summary>
        public static void Reset() => ClearRun();

        private static bool ReplayPath(List<int> saved)
        {
            if (saved == null || saved.Count == 0 || saved[0] != graph.StartId) return false;
            for (int i = 1; i < saved.Count; i++)
                if (!Advance(saved[i])) return false;
            return true;
        }

        private static void ClearRun()
        {
            InRun = false;
            seed = 0;
            graph = null;
            mapView = null;
            nodeIndex = 0;
            path.Clear();
            maxHealthBonus = 0f;
            echoesEarned = 0;
            rerollTokens = 0;
            revivesSpent = 0;
            unbrokenUsed = false;
            playerHealth = -1f;
            boonCounts.Clear();
            grantedAbilities.Clear();
        }

        /// <summary>Pure arithmetic for the Forge reward (§1: "heal 60% of missing HP"). A4.3 test
        /// seam — testable without a rig.</summary>
        public static float HealAmountForForge(float current, float max) => Mathf.Max(0f, max - current) * 0.6f;

        /// <summary>Pure arithmetic for carrying stored HP across a node hop when max HP changes
        /// (e.g. an Ironskin pickup raises max mid-run). Callers only pass a <paramref name="stored"/>
        /// that already passed the <c>PlayerHealth &gt;= 0</c> "is it set" check, so this only needs
        /// to guard the upper bound. A4.3 test seam.</summary>
        public static float CarryOverHealth(float stored, float newMax) => Mathf.Min(stored, newMax);

        // Editor sessions with "Enter Play Mode (no domain reload)" keep static state across Play
        // cycles, which would leak a stale run into the next Play session (mirrors EventBus/Health's
        // reset).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() => Reset();
    }
}
