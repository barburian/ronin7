using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Pink-on-Quest guard: every material a player build can reach (enabled build scenes plus
    /// everything under a runtime Resources folder) must use a shader URP can actually draw. A
    /// Built-in Standard/Legacy shader renders magenta on device even though the editor often hides it.
    /// </summary>
    public class BuildMaterialShaderTests
    {
        // Editor-visualisation materials ProBuilder ships in its package Resources folder. They are
        // only drawn by ProBuilder's editor tooling (collider/trigger volume preview), never by the
        // game, so a Built-in shader on them cannot show up pink in a player build.
        private static readonly HashSet<string> Allowlist = new HashSet<string>
        {
            "Packages/com.unity.probuilder/Content/Resources/Materials/Collider.mat",
            "Packages/com.unity.probuilder/Content/Resources/Materials/Trigger.mat",
        };

        // LightMode tags only the Built-in pipeline renders; URP skips these passes (→ magenta).
        private static readonly HashSet<string> BuiltinOnlyLightModes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) // Unity reports some upper-cased (FORWARDBASE)
        {
            "ForwardBase", "ForwardAdd", "Deferred", "PrepassBase", "PrepassFinal",
            "Vertex", "VertexLMRGBM", "VertexLM",
        };

        [Test]
        public void EveryBuildReachableMaterial_UsesUrpCompatibleShader()
        {
            var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
                .Concat(AssetDatabase.GetAllAssetPaths().Where(p => p.Contains("/Resources/") && !p.Contains("/Editor/")))
                .ToArray();

            var failures = new List<string>();
            foreach (var path in AssetDatabase.GetDependencies(roots, true))
            {
                if (Allowlist.Contains(path)) continue;
                // .mat files plus materials embedded in model imports (glTF/FBX sub-assets).
                IEnumerable<Material> mats;
                if (path.EndsWith(".mat")) mats = new[] { AssetDatabase.LoadAssetAtPath<Material>(path) };
                else if (path.EndsWith(".glb") || path.EndsWith(".gltf") || path.EndsWith(".fbx") || path.EndsWith(".FBX"))
                    mats = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>();
                else continue;

                foreach (var m in mats)
                {
                    if (m == null) continue;
                    string problem = ShaderProblem(m.shader);
                    if (problem != null) failures.Add($"{path} ({m.name}): {problem}");
                }
            }

            Assert.IsEmpty(failures, "Materials that will render magenta on device:\n" + string.Join("\n", failures));
        }

        /// <summary>Null when URP can draw <paramref name="s"/>; otherwise why not.</summary>
        private static string ShaderProblem(Shader s)
        {
            if (s == null) return "missing shader";
            if (s.name == "Hidden/InternalErrorShader") return "InternalErrorShader";
            if (ShaderUtil.ShaderHasError(s)) return $"'{s.name}' has compile errors";
            if (!s.isSupported) return $"'{s.name}' is not supported";
            if (s.name.StartsWith("Universal Render Pipeline/")) return null;

            var pipelineTag = new ShaderTagId("RenderPipeline");
            var shaderData = ShaderUtil.GetShaderData(s);
            for (int i = 0; i < shaderData.SubshaderCount; i++)
            {
                string rp = s.FindSubshaderTagValue(i, pipelineTag).name;
                if (rp == "UniversalPipeline") return null; // Ronin7/* and Shader Graph (glTFast) land here
                if (!string.IsNullOrEmpty(rp)) return $"'{s.name}' targets {rp}";
            }

            // Untagged shader (UI/Default, TextMeshPro, Sprites, Skybox...): fine unless it only has
            // Built-in lighting passes, which is exactly what Standard and Legacy Shaders/* look like.
            var lightMode = new ShaderTagId("LightMode");
            for (int p = 0; p < s.passCount; p++)
                if (BuiltinOnlyLightModes.Contains(s.FindPassTagValue(p, lightMode).name))
                    return $"'{s.name}' is a Built-in pipeline shader";
            return null;
        }
    }
}
