using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Generates a tileable sci-fi wall-panel albedo via <see cref="GeminiClient"/> and assigns it to
    /// the flat untextured environment material in the OPEN scene, so the greybox interior reads as
    /// plated ship hull instead of blank grey. Reuses the cached Gemini client (no re-bill on the same
    /// seed/prompt). Reversible: clear the assigned <c>_BaseMap</c> and restore the base colour.
    ///
    /// Targets every renderer whose shared material is the generic <c>Universal Render Pipeline/Lit</c>
    /// the scene builders leave behind — one shared material instance, so a single assignment textures
    /// the whole interior. Does not save the scene; the operator/harness saves after verifying.
    /// </summary>
    public static class EnvTexturePass
    {
        private const string OutPath = "Assets/Ronin7/Art/Generated/EP01/WallPanel.png";
        private const string Prompt =
            "Seamless tileable texture of a dark gunmetal sci-fi spaceship wall panel: brushed steel " +
            "plates with rivets and recessed seams, subtle scratches and grime, faint cyan tech trim " +
            "lines, flat even lighting, orthographic top-down, no shadows, PBR albedo, high detail";

        [MenuItem("Tools/Space Samurai/Art/Continuity — Generate Wall Panel (Gemini)", priority = 23)]
        public static async void GenerateAndApply()
        {
            try
            {
                byte[] png = await GeminiClient.GenerateImageAsync(Prompt, seed: 7, width: 512, height: 512);

                string absDir = Path.GetDirectoryName(Path.Combine(Directory.GetCurrentDirectory(), OutPath));
                if (!Directory.Exists(absDir)) Directory.CreateDirectory(absDir);
                File.WriteAllBytes(OutPath, png);
                AssetDatabase.ImportAsset(OutPath, ImportAssetOptions.ForceUpdate);

                var imp = AssetImporter.GetAtPath(OutPath) as TextureImporter;
                if (imp != null)
                {
                    imp.wrapMode = TextureWrapMode.Repeat;
                    imp.SaveAndReimport();
                }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(OutPath);
                int applied = ApplyToWalls(tex);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"[EnvTexturePass] Wall panel generated ({png.Length} bytes) and applied to {applied} renderer(s).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnvTexturePass] Failed: {e.Message}");
            }
        }

        /// <summary>Assigns <paramref name="tex"/> as the base map of the shared URP/Lit env material.</summary>
        private static int ApplyToWalls(Texture2D tex)
        {
            int n = 0;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var m = mr.sharedMaterial;
                    if (m == null || m.shader == null) continue;
                    if (m.shader.name != "Universal Render Pipeline/Lit") continue;
                    if (m.HasProperty("_BaseMap"))
                    {
                        m.SetTexture("_BaseMap", tex);
                        m.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
                    }
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.85f, 0.87f, 0.92f));
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
                    EditorUtility.SetDirty(m);
                    n++;
                }
            }
            return n;
        }
    }
}
