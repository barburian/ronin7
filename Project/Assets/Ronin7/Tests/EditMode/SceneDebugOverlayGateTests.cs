using System;
using NUnit.Framework;

namespace Ronin7.Tests.EditMode
{
    /// <summary>BUG-31: the scene-name debug label must only exist in RONIN7_DEV builds.</summary>
    public class SceneDebugOverlayGateTests
    {
        [Test]
        public void SceneDebugOverlay_AbsentInRelease()
        {
            var type = Type.GetType("Ronin7.Flow.SceneDebugOverlay, Ronin7.Flow");
#if RONIN7_DEV
            Assert.IsNotNull(type, "SceneDebugOverlay must compile when RONIN7_DEV is defined");
#else
            Assert.IsNull(type, "SceneDebugOverlay must not compile without RONIN7_DEV");
#endif
        }
    }
}
