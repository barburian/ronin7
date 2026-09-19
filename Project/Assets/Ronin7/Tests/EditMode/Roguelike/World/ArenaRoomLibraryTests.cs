using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.World
{
    /// <summary>Covers <see cref="ArenaRoomLibrary.ForSector"/>'s wrap/degenerate behaviour.</summary>
    public class ArenaRoomLibraryTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private ArenaRoomLibrary MakeLibrary(params string[] biomeIds)
        {
            var library = ScriptableObject.CreateInstance<ArenaRoomLibrary>();
            var biomes = new ArenaRoomLibrary.Biome[biomeIds.Length];
            for (int i = 0; i < biomeIds.Length; i++)
                biomes[i] = new ArenaRoomLibrary.Biome { id = biomeIds[i] };
            library.biomes = biomes;
            _created.Add(library);
            return library;
        }

        [Test]
        public void ForSector_OneBiomePerSector_ReturnsMatchingBiome()
        {
            var library = MakeLibrary("rust", "program", "garden");

            Assert.AreEqual("rust", library.ForSector(0).id);
            Assert.AreEqual("program", library.ForSector(1).id);
            Assert.AreEqual("garden", library.ForSector(2).id);
        }

        [Test]
        public void ForSector_FewerBiomesThanSectors_Wraps()
        {
            var library = MakeLibrary("rust", "program");

            Assert.AreEqual("rust", library.ForSector(0).id);
            Assert.AreEqual("program", library.ForSector(1).id);
            Assert.AreEqual("rust", library.ForSector(2).id); // wraps back to index 0
        }

        [Test]
        public void ForSector_SingleBiome_AlwaysReturnsIt()
        {
            var library = MakeLibrary("only");

            Assert.AreEqual("only", library.ForSector(0).id);
            Assert.AreEqual("only", library.ForSector(1).id);
            Assert.AreEqual("only", library.ForSector(2).id);
        }

        [Test]
        public void ForSector_EmptyBiomesArray_ReturnsNull()
        {
            var library = ScriptableObject.CreateInstance<ArenaRoomLibrary>();
            library.biomes = new ArenaRoomLibrary.Biome[0];
            _created.Add(library);

            Assert.IsNull(library.ForSector(0));
        }

        [Test]
        public void ForSector_NullBiomesArray_ReturnsNull()
        {
            var library = ScriptableObject.CreateInstance<ArenaRoomLibrary>();
            library.biomes = null;
            _created.Add(library);

            Assert.IsNull(library.ForSector(0));
        }

        [Test]
        public void ForSector_NegativeSector_WrapsCorrectly()
        {
            var library = MakeLibrary("rust", "program", "garden");

            // -1 should behave like wrapping one step back from 0, landing on the last biome.
            Assert.AreEqual("garden", library.ForSector(-1).id);
        }
    }
}
