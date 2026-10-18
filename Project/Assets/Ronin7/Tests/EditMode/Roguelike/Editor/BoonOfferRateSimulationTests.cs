using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Editor
{
    /// <summary>
    /// P4-06: offer-rate simulation over the SHIPPED catalog. Walks seeded runs through the real
    /// <see cref="RunMapGenerator"/> graph (random branch), and at every Combat / Elite / non-final Boss
    /// node calls the real <see cref="BoonOfferPicker.Pick"/> with the node's <see cref="RunRng.ForNode"/>
    /// stream, exactly like <c>RunDirector.BeginBoonOffer</c>: tag affinity, Duo gating, pack locks and
    /// ability prerequisites all come from the picker itself. Luck 0, no starting boons, no rerolls.
    /// Treasure grants its 1-card Pick (not a pick screen, not counted in pick rates); Shop / Event / Forge
    /// are skipped (they need currency or event choices to model).
    ///
    /// Player heuristic: take the highest rarity offered (Duo > Epic > Rare > Common), ties at random.
    /// A UniformRandom pass is reported alongside as a sanity check. Pick-when-offered numbers are a
    /// property of the heuristic, NOT of real players: the 5-40% target must be re-checked with
    /// runlog.jsonl playtest data (Tools/playtest/analyze_runlog.py). These tests only gate offer
    /// reachability (gating bugs), never the tuning target.
    /// </summary>
    public class BoonOfferRateSimulationTests
    {
        private const string CatalogPath = "Assets/Ronin7/Data/Boons/BoonCatalog.asset";
        private const int Runs = 2000;
        // Reachability gate, not the tuning target: a boon below 1% of runs is effectively dead (gating bug).
        // The table flags the softer < 5% line for a human to judge (e.g. Thunder Riposte's triple gate).
        private const float MinRunShare = 0.01f;

        public enum Heuristic { HighestRarity, UniformRandom }

        public sealed class Stat
        {
            public BoonDefinition Boon;
            public int RunsOffered, TimesOffered, TimesPicked;
        }

        [TearDown]
        public void TearDown()
        {
            RunState.Reset();
            MetaProgression.ResetForTests();
        }

        /// <summary>Per-boon stats over <paramref name="runs"/> seeded runs (seeds 1..runs).</summary>
        public static Dictionary<string, Stat> Simulate(BoonDefinition[] pool, int runs, bool packsUnlocked, Heuristic heuristic)
        {
            MetaProgression.ResetForTests();
            MetaProgression.DevUnlockAllPacks = packsUnlocked;
            var stats = new Dictionary<string, Stat>();
            foreach (var b in pool) if (b != null && !stats.ContainsKey(b.id)) stats[b.id] = new Stat { Boon = b };

            var offeredThisRun = new HashSet<string>();
            for (uint seed = 1; seed <= runs; seed++)
            {
                RunState.Begin(seed);
                offeredThisRun.Clear();
                var walk = new RunRng(seed ^ 0x9E3779B9u);
                var choice = new RunRng(seed ^ 0x85EBCA6Bu);
                var map = RunState.Graph;
                int node = map.StartId;
                while (true)
                {
                    RoomKind kind = map.Nodes[node].Kind;
                    bool final = map.Next[node].Length == 0;
                    var rng = RunRng.ForNode(seed, node);
                    if (!final && (kind == RoomKind.Combat || kind == RoomKind.Elite || kind == RoomKind.Boss))
                    {
                        var offer = BoonOfferPicker.Pick(pool, RunState.BoonCount, kind, ref rng, 3, 0f);
                        foreach (var b in offer)
                        {
                            stats[b.id].TimesOffered++;
                            if (offeredThisRun.Add(b.id)) stats[b.id].RunsOffered++;
                        }
                        if (offer.Count > 0)
                        {
                            var picked = Choose(offer, heuristic, ref choice);
                            stats[picked.id].TimesPicked++;
                            Grant(picked);
                        }
                    }
                    else if (kind == RoomKind.Treasure)
                    {
                        var take = BoonOfferPicker.Pick(pool, RunState.BoonCount, RoomKind.Treasure, ref rng, 1, 0f);
                        if (take.Count > 0) Grant(take[0]);
                    }
                    if (final) break;
                    var next = map.Next[node];
                    node = next[walk.NextInt(next.Length)];
                    RunState.Advance(node);
                }
            }
            RunState.Reset();
            MetaProgression.ResetForTests();
            return stats;
        }

        private static BoonDefinition Choose(List<BoonDefinition> offer, Heuristic heuristic, ref RunRng rng)
        {
            if (heuristic == Heuristic.UniformRandom) return offer[rng.NextInt(offer.Count)];
            BoonRarity best = BoonRarity.Common;
            foreach (var b in offer) if (b.rarity > best) best = b.rarity;
            var top = offer.FindAll(b => b.rarity == best);
            return top[rng.NextInt(top.Count)];
        }

        // Mirrors RunDirector.GrantBoon's run-state side (A7.7 GrantAbility).
        private static void Grant(BoonDefinition boon)
        {
            RunState.AddBoon(boon.id);
            if (boon.effect == BoonEffectKind.GrantAbility) RunState.GrantAbility(boon.abilityId);
        }

        public static string Table(Dictionary<string, Stat> stats, int runs, string title)
        {
            var rows = new List<Stat>(stats.Values);
            rows.Sort((a, b) => b.RunsOffered.CompareTo(a.RunsOffered));
            var sb = new StringBuilder().Append("[BoonOfferSim] ").Append(title).Append(" (").Append(runs).Append(" runs)\n")
                .Append("boon | rarity | offered % of runs | offers | picked | pick-when-offered %\n");
            foreach (var s in rows)
            {
                float runPct = 100f * s.RunsOffered / runs;
                float pickPct = s.TimesOffered > 0 ? 100f * s.TimesPicked / s.TimesOffered : 0f;
                sb.Append(s.Boon.id).Append(" | ").Append(s.Boon.rarity).Append(" | ").Append(runPct.ToString("F1"))
                  .Append(" | ").Append(s.TimesOffered).Append(" | ").Append(s.TimesPicked).Append(" | ").Append(pickPct.ToString("F1"))
                  .Append(runPct < 5f ? "  <5% OF RUNS" : "").Append(s.TimesOffered > 0 && pickPct > 40f ? "  >40% PICK" : "")
                  .Append(s.TimesOffered > 0 && pickPct < 5f ? "  <5% PICK" : "").Append('\n');
            }
            return sb.ToString();
        }

        private static BoonDefinition[] LoadCatalogOrSkip()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BoonCatalog>(CatalogPath);
            if (catalog == null || catalog.boons == null || catalog.boons.Length == 0)
                Assert.Ignore($"{CatalogPath} not built yet.");
            return catalog.boons;
        }

        [Test]
        public void AllPacksUnlocked_EveryBoonReachable()
        {
            var pool = LoadCatalogOrSkip();
            var stats = Simulate(pool, Runs, true, Heuristic.HighestRarity);
            Debug.Log(Table(stats, Runs, "all packs unlocked, HighestRarity"));
            Debug.Log(Table(Simulate(pool, Runs, true, Heuristic.UniformRandom), Runs, "all packs unlocked, UniformRandom"));

            var starved = new List<string>();
            foreach (var s in stats.Values) if (s.RunsOffered < Runs * MinRunShare) starved.Add($"{s.Boon.id} {100f * s.RunsOffered / Runs:F1}%");
            Assert.IsEmpty(starved, "offered in < 1% of simulated runs (gating or weight bug): " + string.Join(", ", starved));
        }

        [Test]
        public void AllPacksLocked_PackBoonsNeverOffered_OthersReachable()
        {
            var pool = LoadCatalogOrSkip();
            var stats = Simulate(pool, Runs, false, Heuristic.HighestRarity);
            Debug.Log(Table(stats, Runs, "all packs locked, HighestRarity"));

            var bad = new List<string>();
            const BoonTag packs = BoonTag.Bleed | BoonTag.Storm | BoonTag.Executioner;
            foreach (var s in stats.Values)
            {
                bool packBoon = (s.Boon.tags & packs) != 0;
                if (packBoon && s.TimesOffered > 0) bad.Add($"{s.Boon.id} offered while its pack is locked");
                if (!packBoon && s.RunsOffered < Runs * MinRunShare) bad.Add($"{s.Boon.id} {100f * s.RunsOffered / Runs:F1}% of runs");
            }
            Assert.IsEmpty(bad, string.Join(", ", bad));
        }
    }
}
