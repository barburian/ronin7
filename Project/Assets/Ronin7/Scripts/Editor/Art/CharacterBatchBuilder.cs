using System.IO;
using Ronin7.World;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Bakes a whole folder tree of pre-authored character specs into prefabs in one pass — the
    /// offline, keyless counterpart to the Gemini-driven <see cref="CharacterGeneratorWindow"/>.
    /// Specs are plain JSON matching <see cref="ArtPrefabBuilder.CharacterSpec"/> and live under
    /// Data/CharacterSpecs/&lt;role&gt;/, so the folder a file sits in decides how it is built:
    ///
    ///   Decorative/ → BuildCharacterFromSpec(spec, Decorative)         (named cast, allies, NPC looks)
    ///   Combat/     → BuildCharacterFromSpec(spec, Combat)             (fightable enemy rig + katana)
    ///   Filler/     → Decorative build, then an <see cref="NpcWander"/> baked on so the prefab roams
    ///
    /// Every prefab lands in Prefabs/Art/Generated/ (the same place the single-character generator
    /// writes), named from the spec's own <c>name</c> field.
    /// </summary>
    public static class CharacterBatchBuilder
    {
        private const string SpecsRoot = "Assets/Ronin7/Data/CharacterSpecs";

        [MenuItem("Tools/Space Samurai/Art/Build Characters from Specs Folder")]
        public static void BuildAll()
        {
            int built = 0, failed = 0;

            built += BuildFolder($"{SpecsRoot}/Decorative", ArtPrefabBuilder.CharacterRole.Decorative, false, ref failed);
            built += BuildFolder($"{SpecsRoot}/Combat", ArtPrefabBuilder.CharacterRole.Combat, false, ref failed);
            built += BuildFolder($"{SpecsRoot}/Filler", ArtPrefabBuilder.CharacterRole.Decorative, true, ref failed);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CharacterBatchBuilder] Done — built {built}, failed {failed}.");
        }

        /// <summary>Builds every *.json spec in one folder. Returns the count built; adds to failed.</summary>
        private static int BuildFolder(string assetFolder, ArtPrefabBuilder.CharacterRole role, bool wander, ref int failed)
        {
            // assetFolder is project-relative ("Assets/..."); GetFiles needs the OS path.
            string osFolder = Path.Combine(Directory.GetCurrentDirectory(), assetFolder);
            if (!Directory.Exists(osFolder))
            {
                Debug.Log($"[CharacterBatchBuilder] No folder {assetFolder} — skipping.");
                return 0;
            }

            string[] files = Directory.GetFiles(osFolder, "*.json", SearchOption.TopDirectoryOnly);
            int built = 0;
            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var spec = JsonUtility.FromJson<ArtPrefabBuilder.CharacterSpec>(json);
                    var prefab = ArtPrefabBuilder.BuildCharacterFromSpec(spec, role);
                    if (prefab == null) { failed++; continue; }

                    if (wander) AttachWander(prefab);
                    built++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[CharacterBatchBuilder] Failed on {Path.GetFileName(file)}: {ex.Message}");
                    failed++;
                }
            }

            Debug.Log($"[CharacterBatchBuilder] {assetFolder}: built {built} of {files.Length}.");
            return built;
        }

        /// <summary>Re-opens the just-saved prefab and bakes an NpcWander onto its root.</summary>
        private static void AttachWander(GameObject prefab)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<NpcWander>() == null) root.AddComponent<NpcWander>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
