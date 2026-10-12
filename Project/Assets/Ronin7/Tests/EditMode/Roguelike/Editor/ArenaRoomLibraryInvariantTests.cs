using NUnit.Framework;
using Ronin7.World;
using UnityEditor;

namespace Ronin7.Tests.EditMode.Roguelike.Editor
{
    /// <summary>
    /// Invariant over the SHIPPED <see cref="ArenaRoomLibrary"/> asset: the runtime-built room needs a
    /// serialized URP material, because Unity's default primitive material is unavailable in player
    /// builds (Quest renders magenta). Rebuild via "Tools/Space Samurai/Roguelike/Build Spawn Table +
    /// Room Library" if this fails.
    /// </summary>
    public class ArenaRoomLibraryInvariantTests
    {
        private const string RoomLibraryPath = "Assets/Ronin7/Data/Roguelike/ArenaRoomLibrary.asset";

        [Test]
        public void ShippedLibrary_HasUrpSurfaceMaterial()
        {
            var library = AssetDatabase.LoadAssetAtPath<ArenaRoomLibrary>(RoomLibraryPath);
            Assert.IsNotNull(library, $"{RoomLibraryPath} missing — run the Roguelike data builder menu.");
            Assert.IsNotNull(library.surfaceMaterial, "surfaceMaterial unassigned — room renders magenta on device.");
            StringAssert.StartsWith("Universal Render Pipeline/", library.surfaceMaterial.shader.name);
        }
    }
}
