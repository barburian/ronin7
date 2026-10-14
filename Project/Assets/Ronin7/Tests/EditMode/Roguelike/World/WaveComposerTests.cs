using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.World
{
    /// <summary>
    /// Covers <see cref="WaveComposer"/>'s pure contract: placement spacing/bounds/player-distance
    /// guarantees (including the forced-fallback path), eligibility filtering by depth/eliteOnly/
    /// null-prefab/null-definition/zero-weight (A5.1), boss selection by sector including the
    /// null-prefab exclusion, determinism for a fixed seed, and empty/degenerate tables.
    /// </summary>
    public class WaveComposerTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GameObject _dummyPrefab;
        private EnemyDefinition _dummyDefinition;

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
            _dummyPrefab = null;
            _dummyDefinition = null;
        }

        // A5.1 made a non-null prefab/definition part of eligibility, so the default entry factory
        // now hands out shared dummies — tests that specifically exercise the null-prefab/definition
        // exclusion construct their own Entry with the field left null instead of calling this.
        private GameObject DummyPrefab()
        {
            if (_dummyPrefab == null)
            {
                _dummyPrefab = new GameObject("DummyEnemyPrefab");
                _created.Add(_dummyPrefab);
            }
            return _dummyPrefab;
        }

        private EnemyDefinition DummyDefinition()
        {
            if (_dummyDefinition == null)
            {
                _dummyDefinition = ScriptableObject.CreateInstance<EnemyDefinition>();
                _created.Add(_dummyDefinition);
            }
            return _dummyDefinition;
        }

        private EnemySpawnTable.Entry MakeEntry(int weight = 10, int minDepth = 0, bool eliteOnly = false)
            => new EnemySpawnTable.Entry
            {
                prefab = DummyPrefab(),
                definition = DummyDefinition(),
                weight = weight,
                minDepth = minDepth,
                eliteOnly = eliteOnly,
            };

        private EnemySpawnTable MakeTable(EnemySpawnTable.Entry[] trash, EnemySpawnTable.BossEntry[] bosses = null)
        {
            var table = ScriptableObject.CreateInstance<EnemySpawnTable>();
            table.trash = trash;
            table.bosses = bosses;
            _created.Add(table);
            return table;
        }

        // ---- Positions: spacing / bounds / player distance ----

        [Test]
        public void Positions_ZeroOrNegativeCount_ReturnsEmptyList()
        {
            var rng = new RunRng(1);
            Assert.AreEqual(0, WaveComposer.Positions(0, 12f, Vector3.zero, ref rng).Count);

            var rng2 = new RunRng(1);
            Assert.AreEqual(0, WaveComposer.Positions(-3, 12f, Vector3.zero, ref rng2).Count);
        }

        [Test]
        public void Positions_ReturnsExactlyRequestedCount()
        {
            var rng = new RunRng(123);
            var positions = WaveComposer.Positions(6, 12f, Vector3.zero, ref rng);
            Assert.AreEqual(6, positions.Count);
        }

        [Test]
        public void Positions_AllWithinArenaBoundsWithMargin()
        {
            var rng = new RunRng(7);
            float halfExtent = 12f;
            // The real guarantee is the wall-margin-reduced bound (WaveComposer's private
            // WallMargin = 1f), not the raw halfExtent — asserting against halfExtent alone would
            // still pass with an enemy standing inside the wall.
            float bound = halfExtent - 1f;
            var positions = WaveComposer.Positions(8, halfExtent, Vector3.zero, ref rng);

            foreach (var p in positions)
            {
                Assert.LessOrEqual(Mathf.Abs(p.x), bound + 0.001f, "x escaped the wall-margin bound");
                Assert.LessOrEqual(Mathf.Abs(p.z), bound + 0.001f, "z escaped the wall-margin bound");
            }
        }

        [TestCase(4f)]
        [TestCase(5f)]
        [TestCase(6f)]
        [TestCase(8f)]
        [TestCase(12f)]
        public void Positions_NonCentredPlayerSpawn_StaysWithinBoundsAndOnFloor(float halfExtent)
        {
            // A5.4/A5.8: a small halfExtent (e.g. a designer typing 4) reaches the fallback ring on
            // nearly every point, so this exercises that path directly with a player spawn well off
            // the arena centre — every returned point must still land inside the wall-margin bound
            // and on the floor (y == 0), never above/below it or outside the room.
            var rng = new RunRng(2050);
            var playerSpawn = new Vector3(halfExtent * 0.4f, 1.6f, -halfExtent * 0.3f);
            float bound = Mathf.Max(0.1f, halfExtent - 1f);

            var positions = WaveComposer.Positions(10, halfExtent, playerSpawn, ref rng);

            Assert.AreEqual(10, positions.Count);
            foreach (var p in positions)
            {
                Assert.LessOrEqual(Mathf.Abs(p.x), bound + 0.001f, "x escaped the wall-margin bound");
                Assert.LessOrEqual(Mathf.Abs(p.z), bound + 0.001f, "z escaped the wall-margin bound");
                Assert.AreEqual(0f, p.y, "enemies must spawn on the floor, never at playerSpawn's head height");
            }
        }

        [Test]
        public void Positions_AllAtLeastMinSeparationApart()
        {
            var rng = new RunRng(99);
            var positions = WaveComposer.Positions(6, 12f, Vector3.zero, ref rng);

            for (int i = 0; i < positions.Count; i++)
                for (int j = i + 1; j < positions.Count; j++)
                    Assert.GreaterOrEqual(Vector3.Distance(positions[i], positions[j]), 1.5f - 0.001f,
                        $"points {i} and {j} are too close");
        }

        [Test]
        public void Positions_AllAtLeastMinPlayerDistance()
        {
            var rng = new RunRng(2026);
            var playerSpawn = new Vector3(1f, 0f, -1f);
            var positions = WaveComposer.Positions(5, 12f, playerSpawn, ref rng);

            foreach (var p in positions)
                Assert.GreaterOrEqual(Vector3.Distance(p, playerSpawn), 4f - 0.001f);
        }

        [Test]
        public void Positions_SameSeed_ProducesIdenticalPositions()
        {
            var rngA = new RunRng(555);
            var rngB = new RunRng(555);

            var a = WaveComposer.Positions(5, 12f, Vector3.zero, ref rngA);
            var b = WaveComposer.Positions(5, 12f, Vector3.zero, ref rngB);

            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
                Assert.AreEqual(a[i], b[i]);
        }

        [Test]
        public void Positions_TinyArenaForcesRejectionFallback_StillReturnsExactCountWithoutHanging()
        {
            // A 2m half-extent room can't legally fit 10 points 1.5m apart AND 4m from the player —
            // rejection sampling will exhaust its attempts and every point falls back to the ring.
            // The contract under test is "never loops forever, always returns `count`" — not that the
            // fallback ring is itself collision-free in a room this small.
            var rng = new RunRng(42);
            List<Vector3> positions = null;

            Assert.DoesNotThrow(() => positions = WaveComposer.Positions(10, 2f, Vector3.zero, ref rng));
            Assert.AreEqual(10, positions.Count);
        }

        // ---- Compose: Forge/Treasure ----

        [Test]
        public void Compose_ForgeNode_ReturnsEmptyList()
        {
            var table = MakeTable(new[] { MakeEntry() });
            var node = new RunNode(3, 0, 3, RoomKind.Forge);
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Compose_TreasureNode_ReturnsEmptyList()
        {
            var table = MakeTable(new[] { MakeEntry() });
            var node = new RunNode(1, 0, 1, RoomKind.Treasure);
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Compose_NullTable_ReturnsEmptyList()
        {
            var node = new RunNode(0, 0, 0, RoomKind.Combat);
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(null, node, 12f, Vector3.zero, ref rng);

            Assert.AreEqual(0, result.Count);
        }

        // ---- Compose: Boss selection ----

        [Test]
        public void Compose_BossNode_ReturnsSingleRequest_MatchingSector()
        {
            var bosses = new[]
            {
                new EnemySpawnTable.BossEntry { prefab = DummyPrefab(), sector = 0 },
                new EnemySpawnTable.BossEntry { prefab = DummyPrefab(), sector = 1 },
                new EnemySpawnTable.BossEntry { prefab = DummyPrefab(), sector = 2 },
            };
            var table = MakeTable(new EnemySpawnTable.Entry[0], bosses);
            var node = new RunNode(9, 1, 4, RoomKind.Boss);
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].IsBoss);
            Assert.AreEqual(1, result[0].EntryIndex);
        }

        [Test]
        public void Compose_BossNode_NoSectorMatch_FallsBackToLastBossEntry()
        {
            var bosses = new[]
            {
                new EnemySpawnTable.BossEntry { prefab = DummyPrefab(), sector = 0 },
                new EnemySpawnTable.BossEntry { prefab = DummyPrefab(), sector = 1 },
            };
            var table = MakeTable(new EnemySpawnTable.Entry[0], bosses);
            var node = new RunNode(14, 2, 4, RoomKind.Boss); // sector 2 has no matching boss entry
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].EntryIndex); // last entry
        }

        [Test]
        public void Compose_BossNode_EmptyBossArray_ReturnsEmptyList()
        {
            var table = MakeTable(new EnemySpawnTable.Entry[0], new EnemySpawnTable.BossEntry[0]);
            var node = new RunNode(4, 0, 4, RoomKind.Boss);
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Compose_BossNode_NullPrefabEntries_ExcludedFromSectorMatchAndFallback()
        {
            // A5.1: a Boss node with only null-prefab entries must NOT silently "clear" with a
            // bogus request — it must find nothing at all, so the caller can log loudly and still
            // clear rather than winning the run with no boss fight.
            var bosses = new[]
            {
                new EnemySpawnTable.BossEntry { prefab = null, sector = 0 },
                new EnemySpawnTable.BossEntry { prefab = null, sector = 1 },
            };
            var table = MakeTable(new EnemySpawnTable.Entry[0], bosses);
            var node = new RunNode(14, 2, 4, RoomKind.Boss); // no sector match either
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Compose_BossNode_NullPrefabAtMatchingSector_FallsBackToUsableEntry()
        {
            var bosses = new[]
            {
                new EnemySpawnTable.BossEntry { prefab = null, sector = 1 }, // matches sector but unusable
                new EnemySpawnTable.BossEntry { prefab = DummyPrefab(), sector = 0 },
            };
            var table = MakeTable(new EnemySpawnTable.Entry[0], bosses);
            var node = new RunNode(9, 1, 4, RoomKind.Boss);
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].EntryIndex); // skips the null-prefab sector match, uses index 1
        }

        // ---- Compose: trash eligibility ----

        [Test]
        public void Compose_CombatNode_ExcludesEntriesAboveMinDepth()
        {
            var lowDepth = MakeEntry(minDepth: 0);
            var highDepth = MakeEntry(minDepth: 10);
            var table = MakeTable(new[] { lowDepth, highDepth });
            var node = new RunNode(2, 0, 2, RoomKind.Combat);
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.IsTrue(result.Count > 0);
            foreach (var req in result)
                Assert.AreEqual(0, req.EntryIndex); // only index 0 (lowDepth) is eligible
        }

        [Test]
        public void Compose_CombatNode_ExcludesEliteOnlyEntries()
        {
            var normal = MakeEntry();
            var eliteOnly = MakeEntry(eliteOnly: true);
            var table = MakeTable(new[] { normal, eliteOnly });
            var node = new RunNode(2, 0, 2, RoomKind.Combat);
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.IsTrue(result.Count > 0);
            foreach (var req in result)
                Assert.AreEqual(0, req.EntryIndex); // only index 0 (normal) is eligible
        }

        [Test]
        public void Compose_EliteNode_IncludesEliteOnlyEntries()
        {
            var eliteOnly = MakeEntry(eliteOnly: true);
            var table = MakeTable(new[] { eliteOnly });
            var node = new RunNode(6, 1, 1, RoomKind.Elite);
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.IsTrue(result.Count > 0);
            foreach (var req in result)
                Assert.AreEqual(0, req.EntryIndex);
        }

        // ---- P1-09 (BUG-09): elite rooms lead with an elite plus escorts ----

        private EnemySpawnTable.Entry MakeEntryHp(float maxHealth, int minDepth = 0, bool eliteOnly = false)
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = maxHealth;
            _created.Add(def);
            var e = MakeEntry(minDepth: minDepth, eliteOnly: eliteOnly);
            e.definition = def;
            return e;
        }

        [TestCase(0, 0, 3)]  // sector 0: no eliteOnly eligible -> strongest trash leads, 2 escorts
        [TestCase(3, 0, 3)]
        [TestCase(7, 1, 3)]  // sector 1: 1 elite + 2 escorts
        [TestCase(12, 2, 4)] // sector 2: 1 elite + 3 escorts
        public void Elite_FirstRequestIsElite(int depth, int sector, int expectedTotal)
        {
            var table = MakeTable(new[]
            {
                MakeEntryHp(60f),
                MakeEntryHp(100f),                               // strongest non-elite-only
                MakeEntryHp(150f, minDepth: 6, eliteOnly: true), // eligible from sector 1 on
            });
            var rng = new RunRng(77);

            var result = WaveComposer.Compose(table, new RunNode(depth, sector, 0, RoomKind.Elite), 12f, Vector3.zero, ref rng);

            Assert.AreEqual(expectedTotal, result.Count, "elite + escort count");
            Assert.IsTrue(result[0].IsElite, "first request must be the elite");
            for (int i = 1; i < result.Count; i++)
                Assert.IsFalse(result[i].IsElite, $"escort {i} must not be flagged elite");
            int expectedLead = depth >= 6 ? 2 : 1;
            Assert.AreEqual(expectedLead, result[0].EntryIndex, "lead entry");
            for (int i = 1; i < result.Count; i++)
                Assert.AreNotEqual(2, result[i].EntryIndex, "escorts are trash, never an elite-only pick");
        }

        [Test]
        public void EliteThreat_GreaterOrEqualCombatThreat_SameDepth()
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>("Assets/Ronin7/Data/Roguelike/EnemySpawnTable.asset");
            if (table == null || table.trash == null || table.trash.Length == 0)
                Assert.Ignore("Shipped spawn table not built yet.");

            for (int depth = 0; depth <= 14; depth++)
            {
                int sector = depth / RunMapGenerator.NodesPerSector;
                float eliteSum = 0f, combatSum = 0f;
                const int Seeds = 60;
                for (uint seed = 1; seed <= Seeds; seed++)
                {
                    var rngE = new RunRng(seed);
                    var rngC = new RunRng(seed);
                    eliteSum += Threat(table, WaveComposer.Compose(table, new RunNode(depth, sector, 0, RoomKind.Elite), 12f, Vector3.zero, ref rngE), depth, RoomKind.Elite);
                    combatSum += Threat(table, WaveComposer.Compose(table, new RunNode(depth, sector, 0, RoomKind.Combat), 12f, Vector3.zero, ref rngC), depth, RoomKind.Combat);
                }
                // Mean over seeds: a single Combat roll can land 4 heavies; the design claim is on average.
                Assert.GreaterOrEqual(eliteSum / Seeds, combatSum / Seeds,
                    $"depth {depth} (sector {sector}): mean elite threat {eliteSum / Seeds:F1} < mean combat threat {combatSum / Seeds:F1}");
            }
        }

        private static float Threat(EnemySpawnTable table, List<SpawnRequest> requests, int depth, RoomKind kind)
        {
            float sum = 0f;
            foreach (var r in requests)
                sum += table.trash[r.EntryIndex].definition.maxHealth * RunScaling.HealthMultiplier(depth, kind);
            return sum;
        }

        [Test]
        public void Compose_EliteNode_SameSeed_Deterministic()
        {
            var table = MakeTable(new[] { MakeEntryHp(60f), MakeEntryHp(100f, eliteOnly: true) });
            var node = new RunNode(12, 2, 0, RoomKind.Elite);
            var rngA = new RunRng(9);
            var rngB = new RunRng(9);

            var a = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rngA);
            var b = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rngB);

            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].EntryIndex, b[i].EntryIndex);
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(a[i].IsElite, b[i].IsElite);
            }
        }

        [Test]
        public void Compose_CombatNode_ExcludesNullPrefabEntries()
        {
            // A5.1: null prefab is nothing usable to spawn — must be excluded from eligibility, not
            // just fail loudly at instantiate time.
            var nullPrefab = new EnemySpawnTable.Entry { prefab = null, definition = DummyDefinition(), weight = 10 };
            var usable = MakeEntry();
            var table = MakeTable(new[] { nullPrefab, usable });
            var node = new RunNode(2, 0, 2, RoomKind.Combat);
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.IsTrue(result.Count > 0);
            foreach (var req in result)
                Assert.AreEqual(1, req.EntryIndex); // only index 1 (usable) is eligible
        }

        [Test]
        public void Compose_CombatNode_ExcludesNullDefinitionEntries()
        {
            var nullDefinition = new EnemySpawnTable.Entry { prefab = DummyPrefab(), definition = null, weight = 10 };
            var usable = MakeEntry();
            var table = MakeTable(new[] { nullDefinition, usable });
            var node = new RunNode(2, 0, 2, RoomKind.Combat);
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.IsTrue(result.Count > 0);
            foreach (var req in result)
                Assert.AreEqual(1, req.EntryIndex); // only index 1 (usable) is eligible
        }

        [Test]
        public void Compose_CombatNode_ExcludesZeroWeightEntries()
        {
            // A5.1: previously a zero-weight entry was spawned EXCLUSIVELY when it was the only
            // eligible one — the exact opposite of what weight 0 should mean ("never").
            var zeroWeight = MakeEntry(weight: 0);
            var table = MakeTable(new[] { zeroWeight });
            var node = new RunNode(2, 0, 2, RoomKind.Combat);
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.AreEqual(0, result.Count, "A zero-weight-only table must be treated as no eligible entries.");
        }

        [Test]
        public void Compose_NoEligibleTrashEntries_ReturnsEmptyList()
        {
            var tooDeep = MakeEntry(minDepth: 99);
            var table = MakeTable(new[] { tooDeep });
            var node = new RunNode(0, 0, 0, RoomKind.Combat);
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Compose_EmptyTrashArray_ReturnsEmptyList()
        {
            var table = MakeTable(new EnemySpawnTable.Entry[0]);
            var node = new RunNode(0, 0, 0, RoomKind.Combat);
            var rng = new RunRng(1);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Compose_NullTrashArray_DoesNotThrow()
        {
            var table = MakeTable(null);
            var node = new RunNode(0, 0, 0, RoomKind.Combat);
            var rng = new RunRng(1);

            List<SpawnRequest> result = null;
            Assert.DoesNotThrow(() => result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng));
            Assert.AreEqual(0, result.Count);
        }

        // ---- Compose: determinism ----

        [Test]
        public void Compose_SameSeed_ProducesIdenticalRequests()
        {
            var table = MakeTable(new[] { MakeEntry(), MakeEntry() });
            var node = new RunNode(5, 1, 0, RoomKind.Combat);
            var rngA = new RunRng(2026);
            var rngB = new RunRng(2026);

            var a = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rngA);
            var b = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rngB);

            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].EntryIndex, b[i].EntryIndex);
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(a[i].IsBoss, b[i].IsBoss);
            }
        }

        [Test]
        public void Compose_CombatNode_RequestPositionsRespectSpacingAndPlayerDistance()
        {
            var table = MakeTable(new[] { MakeEntry(), MakeEntry() });
            var node = new RunNode(8, 1, 3, RoomKind.Combat);
            var rng = new RunRng(321);
            var playerSpawn = new Vector3(0.5f, 0f, 0.5f);

            var result = WaveComposer.Compose(table, node, 12f, playerSpawn, ref rng);

            Assert.IsTrue(result.Count > 0);
            for (int i = 0; i < result.Count; i++)
            {
                Assert.GreaterOrEqual(Vector3.Distance(result[i].Position, playerSpawn), 4f - 0.001f);
                for (int j = i + 1; j < result.Count; j++)
                    Assert.GreaterOrEqual(Vector3.Distance(result[i].Position, result[j].Position), 1.5f - 0.001f);
            }
        }

        // ---- P2-07: explicit waves ----

        [Test]
        public void SplitWaves_SumsToN_EachWaveWithinCap_Deterministic()
        {
            for (int n = 0; n <= 40; n++)
            {
                var a = WaveComposer.SplitWaves(n, 3);
                var b = WaveComposer.SplitWaves(n, 3);
                int sum = 0;
                foreach (int w in a) { Assert.That(w, Is.InRange(1, 3), $"n={n}"); sum += w; }
                Assert.AreEqual(n, sum, $"n={n}");
                CollectionAssert.AreEqual(a, b, $"n={n}");
            }
        }

        [Test]
        public void SplitWaves_Balanced_NoTinyStragglerWave()
        {
            CollectionAssert.AreEqual(new[] { 3, 2, 2 }, WaveComposer.SplitWaves(7, 3));
            CollectionAssert.AreEqual(new[] { 2 }, WaveComposer.SplitWaves(2, 3));
            Assert.AreEqual(0, WaveComposer.SplitWaves(0, 3).Count);
        }

        [Test]
        public void Waves_AliveLeOneTrigger_NeverExceedsConcurrencyCap()
        {
            foreach (RoomKind kind in (RoomKind[])System.Enum.GetValues(typeof(RoomKind)))
            {
                for (int depth = 0; depth <= 14; depth++)
                {
                    var rng = new RunRng((uint)(depth * 7 + (int)kind + 1));
                    var waves = WaveComposer.SplitWaves(RunScaling.EnemyCount(depth, kind, ref rng), 3);
                    int next = 0, alive = 0, max = 0, killed = 0, total = 0;
                    foreach (int w in waves) total += w;
                    while (next < waves.Count || alive > 0)
                    {
                        if (WaveComposer.NextWaveDue(alive, waves.Count - next)) { alive += waves[next++]; max = Mathf.Max(max, alive); continue; }
                        alive--; killed++;
                    }
                    Assert.LessOrEqual(max, RunScaling.MaxConcurrentEnemies, $"depth {depth} kind {kind}");
                    Assert.AreEqual(total, killed, $"depth {depth} kind {kind}");
                }
            }
        }

        // ---- P2-06: front-arc placement ----

        [Test]
        public void FrontArc_WithinCone_WithinRadius_InBounds_Y0()
        {
            var head = new Vector2(1f, -2f);
            var fwd = new Vector2(1f, 1f).normalized;
            for (uint seed = 1; seed <= 200; seed++)
            {
                var rng = new RunRng(seed);
                Assert.IsTrue(WaveComposer.FrontArcPosition(head, fwd, 4f, 7f, 70f, 12f, ref rng, out Vector3 p), $"seed {seed}");
                Vector2 d = new Vector2(p.x, p.z) - head;
                Assert.GreaterOrEqual(d.magnitude, 4f - 1e-3f);
                Assert.LessOrEqual(d.magnitude, 7f + 1e-3f);
                Assert.LessOrEqual(Vector2.Angle(fwd, d), 70f + 1e-2f);
                Assert.LessOrEqual(Mathf.Abs(p.x), 11f + 1e-3f);
                Assert.LessOrEqual(Mathf.Abs(p.z), 11f + 1e-3f);
                Assert.AreEqual(0f, p.y);
            }
        }

        [Test]
        public void FrontArc_Deterministic()
        {
            var a = new RunRng(77); var b = new RunRng(77);
            WaveComposer.FrontArcPosition(Vector2.zero, Vector2.up, 4f, 7f, 70f, 12f, ref a, out Vector3 pa);
            WaveComposer.FrontArcPosition(Vector2.zero, Vector2.up, 4f, 7f, 70f, 12f, ref b, out Vector3 pb);
            Assert.AreEqual(pa, pb);
        }

        [Test]
        public void FrontArc_NoPointFits_ReturnsFalse()
        {
            // Facing straight at a wall with the head against it: every arc point is out of bounds.
            var rng = new RunRng(5);
            Assert.IsFalse(WaveComposer.FrontArcPosition(new Vector2(0f, 4.9f), Vector2.up, 4f, 7f, 70f, 5f, ref rng, out _));
        }

        // ---- P2-08 onboarding ----

        [Test]
        public void EnemyCount_FirstRun_Node0_IsTwo()
        {
            var scav = ScriptableObject.CreateInstance<EnemyDefinition>();
            scav.name = "RL_AshScavenger";
            _created.Add(scav);
            var table = MakeTable(new[]
            {
                MakeEntry(),
                new EnemySpawnTable.Entry { prefab = DummyPrefab(), definition = scav, weight = 1 },
            });
            var node = new RunNode(0, 0, 0, RoomKind.Combat);
            var rng = new RunRng(9);

            var result = WaveComposer.Compose(table, node, 12f, Vector3.zero, ref rng, tutorial: true);

            Assert.AreEqual(2, result.Count);
            foreach (var r in result) Assert.AreEqual(1, r.EntryIndex);
        }

        [Test]
        public void EnemyCount_NotFirstRun_Node0_IsNormal()
        {
            var table = MakeTable(new[] { MakeEntry() });
            var rng = new RunRng(9);
            var result = WaveComposer.Compose(table, new RunNode(0, 0, 0, RoomKind.Combat), 12f, Vector3.zero, ref rng);
            Assert.GreaterOrEqual(result.Count, 3);
        }
    }
}
