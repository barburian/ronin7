using System.IO;
using NUnit.Framework;
using Ronin7.Editor;
using UnityEditor;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P0-12: build output naming and the -dev switch.</summary>
    public class BuildMenuTests
    {
        [Test]
        public void OutputPath_PerTarget()
        {
            Assert.AreEqual(Path.Combine("R", "Builds", "Quest", "Ronin7_0.1.0.apk"),
                BuildMenu.OutputPath("R", BuildTarget.Android, "0.1.0", dev: false));
            Assert.AreEqual(Path.Combine("R", "Builds", "Windows", "Ronin7_0.1.0-dev.exe"),
                BuildMenu.OutputPath("R", BuildTarget.StandaloneWindows64, "0.1.0", dev: true));
        }

        [Test]
        public void IsDev_OnlyWithTheFlag()
        {
            Assert.IsTrue(BuildMenu.IsDev(new[] { "Unity.exe", "-batchmode", "-dev" }));
            Assert.IsFalse(BuildMenu.IsDev(new[] { "Unity.exe", "-batchmode", "-development" }));
            Assert.IsFalse(BuildMenu.IsDev(null));
        }
    }
}
