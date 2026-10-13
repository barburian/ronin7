using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P1-03 / BUG-24 (partial): player lookup scans the Health.Active registry, not the scene.</summary>
    public class MeleeAttackerFindPlayerTests
    {
        private readonly List<GameObject> _gos = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _gos) if (g != null) Object.DestroyImmediate(g);
            _gos.Clear();
        }

        private Health Make(bool withController)
        {
            var go = new GameObject("h");
            _gos.Add(go);
            if (withController) go.AddComponent<CharacterController>();
            return go.AddComponent<Health>();
        }

        [Test]
        public void FindPlayer_UsesActiveRegistry()
        {
            var enemy = Make(false);
            var player = Make(true);
            var list = new List<Health> { enemy, player };
            Assert.AreSame(player, MeleeAttacker.FindPlayer(list));
        }

        [Test]
        public void FindPlayer_NoControllerOrEmpty_ReturnsNull()
        {
            Assert.IsNull(MeleeAttacker.FindPlayer(new List<Health> { Make(false) }));
            Assert.IsNull(MeleeAttacker.FindPlayer(new List<Health>()));
        }
    }
}
