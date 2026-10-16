using NUnit.Framework;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Editor
{
    /// <summary>P3-04 Part B: the shipped RL_ definitions, spawn table and prefabs match the roster spec.</summary>
    public class ArchetypeRosterTests
    {
        private const string Folder = "Assets/Ronin7/Data/Roguelike/Enemies/RL_";

        // (rlName, hp, weakness) straight from the P3-04 table; Swarmer is the only None.
        private static readonly object[][] Roster =
        {
            new object[] { "AshScavenger", 30f, WeaknessKind.None },
            new object[] { "CoilGanger", 70f, WeaknessKind.Recovery },
            new object[] { "DominionTrooper", 80f, WeaknessKind.HeavyBreak },
            new object[] { "ProgramGrunt", 70f, WeaknessKind.Alternating },
            new object[] { "HunterDrone", 35f, WeaknessKind.Reflect },
            new object[] { "ScanDrone", 25f, WeaknessKind.Linchpin },
            new object[] { "HumanoidAutomaton", 160f, WeaknessKind.Backstab },
            new object[] { "IronWarden", 120f, WeaknessKind.ParryGate },
            new object[] { "GraveGuardian", 120f, WeaknessKind.CounterHit },
            new object[] { "RedactionConstruct", 180f, WeaknessKind.PostureOnly },
            new object[] { "DreamGhost", 90f, WeaknessKind.Blink },
            new object[] { "Warden", 450f, WeaknessKind.Backstab },
            new object[] { "Warden_P2", 450f, WeaknessKind.Backstab },
            new object[] { "VaneWraith", 650f, WeaknessKind.Alternating },
            new object[] { "VaneWraith_P2", 650f, WeaknessKind.Blink },
            new object[] { "Maelgorn", 300f, WeaknessKind.HeavyBreak },
            new object[] { "Maelgorn_P2", 300f, WeaknessKind.CounterHit },
            new object[] { "Maelgorn_P3", 300f, WeaknessKind.ParryGate },
        };

        private static EnemyDefinition L(string n) => AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Folder + n + ".asset");

        [TestCaseSource(nameof(Roster))]
        public void Definition_MatchesSpec(string rlName, float hp, WeaknessKind weakness)
        {
            var d = L(rlName);
            Assert.IsNotNull(d, rlName);
            Assert.AreEqual(hp, d.maxHealth, rlName + " HP");
            Assert.AreEqual(weakness, d.weakness, rlName + " weakness");
            Assert.GreaterOrEqual(d.telegraphTime, 0.55f, rlName + " telegraph");
        }

        [Test]
        public void ArchetypeNumbers_MatchSpec()
        {
            var swarmer = L("AshScavenger");
            Assert.AreEqual(2f, swarmer.moveSpeed);
            Assert.AreEqual(0.6f, swarmer.telegraphTime);
            Assert.AreEqual(0.35f, swarmer.activeTime);
            Assert.AreEqual(0.6f, swarmer.attackCooldown);
            Assert.AreEqual(1f, L("CoilGanger").telegraphTime);
            Assert.AreEqual(0.5f, L("CoilGanger").resistMultiplier);
            Assert.AreEqual(2.5f, L("CoilGanger").exploitMultiplier);
            Assert.AreEqual(5.5f, L("DominionTrooper").heavySwingSpeed);
            Assert.AreEqual(0.15f, L("DominionTrooper").resistMultiplier);
            Assert.AreEqual(1.5f, L("DominionTrooper").exploitMultiplier);
            Assert.AreEqual(0.2f, L("ProgramGrunt").resistMultiplier);
            Assert.AreEqual(0f, L("GraveGuardian").resistMultiplier); // CounterHit: Resisted Idle/Chase (x0)
            Assert.AreEqual(0f, L("Maelgorn_P2").resistMultiplier);
            Assert.AreEqual(1.5f, L("ProgramGrunt").exploitMultiplier);
            var heavy = L("HumanoidAutomaton");
            Assert.AreEqual(18f, heavy.damage);
            Assert.AreEqual(1.2f, heavy.telegraphTime);
            Assert.IsTrue(heavy.superArmor);
            Assert.AreEqual(0.3f, heavy.resistMultiplier);
            Assert.AreEqual(2.5f, heavy.exploitMultiplier);
            var parry = L("IronWarden");
            Assert.AreEqual(2, parry.chainHits);
            Assert.AreEqual(2f, parry.staggerTime);
            Assert.AreEqual(0.1f, parry.resistMultiplier);
            Assert.AreEqual(2f, parry.exploitMultiplier);
            Assert.AreEqual(0.25f, L("RedactionConstruct").postureBreakBonusFraction);
            Assert.AreEqual(1.75f, L("DreamGhost").exploitMultiplier);
            Assert.AreEqual(1f, L("Warden").telegraphTime);
            Assert.AreEqual(2, L("Maelgorn_P3").chainHits);
            Assert.IsTrue(L("Maelgorn_P3").superArmor);
        }

        [Test]
        public void EveryRlArchetype_ExceptSwarmer_HasWeakness()
        {
            var guids = AssetDatabase.FindAssets("t:EnemyDefinition", new[] { "Assets/Ronin7/Data/Roguelike/Enemies" });
            Assert.AreEqual(18, guids.Length);
            foreach (var guid in guids)
            {
                var d = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (d.name == "RL_AshScavenger") Assert.AreEqual(WeaknessKind.None, d.weakness);
                else Assert.AreNotEqual(WeaknessKind.None, d.weakness, d.name);
            }
        }

        [Test]
        public void SpawnTable_WeightsDepthsAndCaps_MatchSpec()
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>("Assets/Ronin7/Data/Roguelike/EnemySpawnTable.asset");
            Assert.IsNotNull(table);
            void Check(string rl, int weight, int minDepth, int max)
            {
                var e = System.Array.Find(table.trash, t => t.definition.name == "RL_" + rl);
                Assert.IsNotNull(e, rl);
                Assert.AreEqual(weight, e.weight, rl + " weight");
                Assert.AreEqual(minDepth, e.minDepth, rl + " minDepth");
                Assert.AreEqual(max, e.maxPerRoom, rl + " maxPerRoom");
                Assert.IsFalse(e.eliteOnly, rl);
            }
            Check("AshScavenger", 18, 0, 0);
            Check("CoilGanger", 12, 0, 0);
            Check("DominionTrooper", 12, 0, 0);
            Check("ProgramGrunt", 10, 2, 0);
            Check("HunterDrone", 10, 2, 0);
            Check("ScanDrone", 6, 4, 1);
            foreach (var n in new[] { "HumanoidAutomaton", "IronWarden", "GraveGuardian", "RedactionConstruct", "DreamGhost" })
                Assert.IsTrue(System.Array.Find(table.trash, t => t.definition.name == "RL_" + n).eliteOnly, n);
        }

        [Test]
        public void ArchetypePrefabs_UseTheirSubclass()
        {
            const string F = "Assets/Ronin7/Prefabs/Roguelike/Enemies/";
            T P<T>(string n) where T : Enemy => AssetDatabase.LoadAssetAtPath<GameObject>(F + n + ".prefab").GetComponent<T>();
            Assert.IsNotNull(P<ChargerEnemy>("Coil_Syndicate_Ganger"));
            Assert.IsNotNull(P<Ronin7.Ship.RangedEnemy>("Hunter_Drone"));
            Assert.IsNotNull(P<SupportDrone>("Dominion_Scan-Drone"));
            Assert.IsNotNull(P<BlinkEnemy>("Dream_Ghost_Manifestation"));
            Assert.IsNotNull(P<BlinkEnemy>("Vane_Wraith-6")); // P3-07: blinks in phase 2 only
        }

        [Test]
        public void BossEntries_WirePhasesAndAdds()
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>("Assets/Ronin7/Data/Roguelike/EnemySpawnTable.asset");
            EnemySpawnTable.BossEntry B(int sector) => System.Array.Find(table.bosses, b => b.sector == sector);
            string[] Names(EnemySpawnTable.BossEntry b) => System.Array.ConvertAll(b.phases, p => p.name);
            string AddName(EnemySpawnTable.BossEntry b) => table.trash[b.addEntry].definition.name;

            CollectionAssert.AreEqual(new[] { "RL_Warden_P2" }, Names(B(0)));
            Assert.AreEqual("RL_ScanDrone", AddName(B(0))); Assert.AreEqual(2, B(0).addCount);
            CollectionAssert.AreEqual(new[] { "RL_VaneWraith_P2" }, Names(B(1)));
            Assert.AreEqual(-1, B(1).addEntry);
            CollectionAssert.AreEqual(new[] { "RL_Maelgorn_P2", "RL_Maelgorn_P3" }, Names(B(2)));
            Assert.AreEqual("RL_AshScavenger", AddName(B(2))); Assert.AreEqual(2, B(2).addCount);

            var vane = L("VaneWraith");
            Assert.AreEqual(0.6f, vane.telegraphTime); Assert.AreEqual(1f, vane.telegraphMaxTime);
            Assert.IsTrue(L("VaneWraith_P2").blinkBehind);
        }
    }
}
