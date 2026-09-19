using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Boons
{
    public class BoonCatalogTests
    {
        private readonly List<Object> _created = new List<Object>();

        private BoonDefinition MakeBoon(string id)
        {
            var boon = ScriptableObject.CreateInstance<BoonDefinition>();
            boon.id = id;
            _created.Add(boon);
            return boon;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        [Test]
        public void Find_ExistingId_ReturnsMatchingDefinition()
        {
            var a = MakeBoon("boon_a");
            var b = MakeBoon("boon_b");
            var catalog = ScriptableObject.CreateInstance<BoonCatalog>();
            catalog.boons = new[] { a, b };
            _created.Add(catalog);

            Assert.AreSame(b, catalog.Find("boon_b"));
        }

        [Test]
        public void Find_MissingId_ReturnsNull()
        {
            var catalog = ScriptableObject.CreateInstance<BoonCatalog>();
            catalog.boons = new[] { MakeBoon("boon_a") };
            _created.Add(catalog);

            Assert.IsNull(catalog.Find("boon_nonexistent"));
        }

        [Test]
        public void Find_NullOrEmptyId_ReturnsNull()
        {
            var catalog = ScriptableObject.CreateInstance<BoonCatalog>();
            catalog.boons = new[] { MakeBoon("boon_a") };
            _created.Add(catalog);

            Assert.IsNull(catalog.Find(null));
            Assert.IsNull(catalog.Find(""));
        }

        [Test]
        public void Find_NullBoonsArray_DoesNotThrow()
        {
            var catalog = ScriptableObject.CreateInstance<BoonCatalog>();
            catalog.boons = null;
            _created.Add(catalog);

            BoonDefinition result = null;
            Assert.DoesNotThrow(() => result = catalog.Find("boon_a"));
            Assert.IsNull(result);
        }

        [Test]
        public void Find_PoolContainsNullEntries_SkipsThemWithoutThrowing()
        {
            var catalog = ScriptableObject.CreateInstance<BoonCatalog>();
            catalog.boons = new[] { null, MakeBoon("boon_a") };
            _created.Add(catalog);

            BoonDefinition result = null;
            Assert.DoesNotThrow(() => result = catalog.Find("boon_a"));
            Assert.IsNotNull(result);
        }
    }
}
