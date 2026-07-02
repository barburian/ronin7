using NUnit.Framework;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class ZeroGCombatVolumeTests
    {
        /// <summary>
        /// Smoke test: ZeroGCombatVolume can be instantiated without errors.
        /// Full ref-counting behavior requires PlayMode and trigger setup.
        /// </summary>
        [Test]
        public void ZeroGCombatVolume_CanBeInstantiated()
        {
            var go = new GameObject("TestVolume");
            var volume = go.AddComponent<ZeroGCombatVolume>();
            Assert.IsNotNull(volume);
            Object.DestroyImmediate(go);
        }
    }
}
