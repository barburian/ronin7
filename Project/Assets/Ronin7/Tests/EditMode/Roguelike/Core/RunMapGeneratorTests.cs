using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    public class RunMapGeneratorTests
    {
        [Test]
        public void Generate_AlwaysReturnsTotalNodesLength()
        {
            RunNode[] map = RunMapGenerator.Generate(42);

            Assert.AreEqual(RunMapGenerator.TotalNodes, map.Length);
            Assert.AreEqual(15, map.Length);
        }

        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(1000u)]
        [TestCase(uint.MaxValue)]
        public void Generate_FixedSlots_AlwaysMatchContract(uint seed)
        {
            RunNode[] map = RunMapGenerator.Generate(seed);

            for (int sector = 0; sector < RunMapGenerator.SectorCount; sector++)
            {
                int baseIndex = sector * RunMapGenerator.NodesPerSector;

                Assert.AreEqual(RoomKind.Combat, map[baseIndex + 0].Kind, $"sector {sector} idx0");
                Assert.AreEqual(RoomKind.Forge, map[baseIndex + 3].Kind, $"sector {sector} idx3");
                Assert.AreEqual(RoomKind.Boss, map[baseIndex + 4].Kind, $"sector {sector} idx4");
            }
        }

        [Test]
        public void Generate_IndexAndSectorFields_AreConsistent()
        {
            RunNode[] map = RunMapGenerator.Generate(7);

            for (int i = 0; i < map.Length; i++)
            {
                Assert.AreEqual(i, map[i].Index);
                Assert.AreEqual(i / RunMapGenerator.NodesPerSector, map[i].Sector);
                Assert.AreEqual(i % RunMapGenerator.NodesPerSector, map[i].IndexInSector);
            }
        }

        [Test]
        public void Generate_NeverRollsTwoTreasuresInOneSector()
        {
            for (uint seed = 1; seed <= 500; seed++)
            {
                RunNode[] map = RunMapGenerator.Generate(seed);

                for (int sector = 0; sector < RunMapGenerator.SectorCount; sector++)
                {
                    int baseIndex = sector * RunMapGenerator.NodesPerSector;
                    int treasureCount = 0;
                    if (map[baseIndex + 1].Kind == RoomKind.Treasure) treasureCount++;
                    if (map[baseIndex + 2].Kind == RoomKind.Treasure) treasureCount++;

                    Assert.LessOrEqual(treasureCount, 1, $"seed {seed} sector {sector} rolled two Treasures");
                }
            }
        }

        [Test]
        public void Generate_MidSlots_OnlyEverContainWeightedKinds()
        {
            for (uint seed = 1; seed <= 200; seed++)
            {
                RunNode[] map = RunMapGenerator.Generate(seed);

                for (int sector = 0; sector < RunMapGenerator.SectorCount; sector++)
                {
                    int baseIndex = sector * RunMapGenerator.NodesPerSector;
                    for (int mid = 1; mid <= 2; mid++)
                    {
                        RoomKind kind = map[baseIndex + mid].Kind;
                        Assert.IsTrue(kind == RoomKind.Combat || kind == RoomKind.Elite || kind == RoomKind.Treasure,
                            $"seed {seed} sector {sector} idx{mid} had unexpected kind {kind}");
                    }
                }
            }
        }

        [Test]
        public void Generate_SameSeed_ProducesByteIdenticalMap()
        {
            RunNode[] a = RunMapGenerator.Generate(2026);
            RunNode[] b = RunMapGenerator.Generate(2026);

            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].Index, b[i].Index);
                Assert.AreEqual(a[i].Sector, b[i].Sector);
                Assert.AreEqual(a[i].IndexInSector, b[i].IndexInSector);
                Assert.AreEqual(a[i].Kind, b[i].Kind);
            }
        }

        [Test]
        public void Generate_DifferentSeeds_CanProduceDifferentMaps()
        {
            // Compare seed 1 against a spread of other seeds rather than a single fixed pair, so an
            // unlucky coincidental match on one pair can't make this test flaky.
            RunNode[] baseline = RunMapGenerator.Generate(1);

            bool anyDifferentKind = false;
            for (uint seed = 2; seed <= 20 && !anyDifferentKind; seed++)
            {
                RunNode[] other = RunMapGenerator.Generate(seed);
                for (int i = 0; i < baseline.Length; i++)
                {
                    if (baseline[i].Kind != other[i].Kind)
                    {
                        anyDifferentKind = true;
                        break;
                    }
                }
            }

            Assert.IsTrue(anyDifferentKind);
        }
    }
}
