using UnityEditor;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// EP16 "The Silence Falls" on-foot finale builder.
    /// Unlike EP15, EP16 has no space finale scene—all scenes are on-foot combat/narrative.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Galaxy 2/Build All EP16 Scenes", priority = 166)]
        public static void BuildAllEp16Scenes()
        {
            Debug.Log("[Space Samurai] Building all EP16 scenes in order: Docking Trench, Monastery, Blade Garden, Corvette Assault, The Duel, Silent Garden, Galaxy 2...");
            BuildEp16DockingTrench();
            BuildEp16Monastery();
            BuildEp16BladeGarden();
            BuildEp16CorvetteAssault();
            BuildEp16TheDuel();
            BuildEp16SilentGarden();
            BuildGalaxy2Scene();
            Debug.Log("[Space Samurai] All EP16 scenes built successfully! Galaxy 2 hub rebuilt to register EP16 completions.");
        }
    }
}
