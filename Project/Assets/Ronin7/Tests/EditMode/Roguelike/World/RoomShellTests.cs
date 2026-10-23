using NUnit.Framework;
using Ronin7.Core;
using Ronin7.World;
using UnityEditor;

namespace Ronin7.Tests.EditMode.Roguelike.World
{
    /// <summary>P5-05: room-shell scene pick (footprint + determinism), greybox fallback, Build Settings.</summary>
    public class RoomShellTests
    {
        private static readonly string[] Scenes =
            { "Room_Program_01_8m", "Room_Program_02_8m", "Room_Program_01_11m", "Room_Program_02_11m" };

        [Test]
        public void Biome_EmptyRoomScenes_UsesGreybox()
        {
            var biome = new ArenaRoomLibrary.Biome();
            RunRng rng = RunRng.ForNode(1u, 1);
            Assert.IsNotNull(biome.roomScenes);
            Assert.IsNull(ArenaRoomLibrary.PickRoomScene(biome.roomScenes, 8f, ref rng));
            Assert.IsNull(ArenaRoomLibrary.PickRoomScene(null, 8f, ref rng));
        }

        [Test]
        public void PickRoomScene_MatchesFootprint()
        {
            for (uint seed = 1; seed < 20; seed++)
            {
                RunRng a = RunRng.ForNode(seed, 3), b = RunRng.ForNode(seed, 3);
                StringAssert.EndsWith("_8m", ArenaRoomLibrary.PickRoomScene(Scenes, 8f, ref a));
                StringAssert.EndsWith("_11m", ArenaRoomLibrary.PickRoomScene(Scenes, 11f, ref b));
            }
        }

        [Test]
        public void PickRoomScene_NoMatchingFootprint_ReturnsNull()
        {
            RunRng rng = RunRng.ForNode(1u, 1);
            Assert.IsNull(ArenaRoomLibrary.PickRoomScene(Scenes, 5f, ref rng));
            Assert.IsNull(ArenaRoomLibrary.PickRoomScene(Scenes, 9f, ref rng));
        }

        [Test]
        public void PickRoomScene_IsDeterministicPerSeedAndNode()
        {
            bool sawBoth = false;
            string first = null;
            for (uint seed = 1; seed < 40; seed++)
            {
                RunRng a = RunRng.ForNode(seed, 5), b = RunRng.ForNode(seed, 5);
                string pa = ArenaRoomLibrary.PickRoomScene(Scenes, 8f, ref a);
                Assert.AreEqual(pa, ArenaRoomLibrary.PickRoomScene(Scenes, 8f, ref b));
                first ??= pa;
                sawBoth |= pa != first;
            }
            Assert.IsTrue(sawBoth, "pick never varies across seeds");
        }

        [TestCase("program", "Program")]
        [TestCase("rust", "Rust")]
        public void BiomeShells_NamesEncodeShellFootprints(string id, string folder)
        {
            var library = AssetDatabase.LoadAssetAtPath<ArenaRoomLibrary>("Assets/Ronin7/Data/Roguelike/ArenaRoomLibrary.asset");
            var biome = System.Array.Find(library.biomes, b => b.id == id);
            Assert.AreEqual(6, biome.roomScenes.Length);
            foreach (string name in biome.roomScenes)
            {
                var m = System.Text.RegularExpressions.Regex.Match(name, @"_(\d+)m$");
                Assert.IsTrue(m.Success, name);
                int half = int.Parse(m.Groups[1].Value);
                Assert.IsTrue(half == 8 || half == 11, $"{name}: only 8 m / 11 m footprints get shells");
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>($"Assets/Ronin7/Scenes/Rooms/{folder}/{name}.unity"), name);
            }
        }

        [Test]
        public void AllRoomScenes_InBuildSettings()
        {
            var library = AssetDatabase.LoadAssetAtPath<ArenaRoomLibrary>("Assets/Ronin7/Data/Roguelike/ArenaRoomLibrary.asset");
            Assert.IsNotNull(library);
            if (library.biomes == null) return;
            foreach (var biome in library.biomes)
            {
                if (biome?.roomScenes == null) continue;
                foreach (string name in biome.roomScenes)
                {
                    bool found = false;
                    foreach (var s in EditorBuildSettings.scenes)
                        found |= s.enabled && System.IO.Path.GetFileNameWithoutExtension(s.path) == name;
                    Assert.IsTrue(found, $"room scene '{name}' (biome {biome.id}) is not enabled in Build Settings - lightmaps would strip.");
                }
            }
        }
    }
}
