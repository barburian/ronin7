using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Repair pass for the pre-cache TintShared era: scenes built before the shared tint-material
    /// cache (8e7c3b5) embed one cloned material PER RENDERER — Ch02 shipped with 170 embedded
    /// materials, the hub with 198 — and every duplicate breaks SRP batching for its renderer
    /// (unnecessary set-pass switches on Quest). This dedupes each scene's embedded (non-asset)
    /// materials by FULL property equality — shader + every color/float/texture property — so only
    /// true duplicates merge and visuals cannot change, then re-points renderers to one
    /// representative per group. Orphaned embedded materials fall out of the scene file on save.
    /// Additive and idempotent; never touches materials that live as assets on disk.
    /// </summary>
    public static class TintMaterialDeduper
    {
        [MenuItem("Tools/Space Samurai/Dedupe Embedded Tint Materials (all build scenes)", priority = 22)]
        public static void DedupeAllScenes()
        {
            var report = new StringBuilder("[TintDedupe] ");
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled) continue;
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                int merged = DedupeOpenScene();
                if (merged > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                report.Append($"{scene.name}:{merged} ");
            }
            Debug.Log(report.ToString());
        }

        /// <summary>Returns how many renderer material slots were re-pointed to a representative.</summary>
        internal static int DedupeOpenScene()
        {
            var byKey = new Dictionary<string, Material>();
            int merged = 0;

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m))) continue; // on-disk asset: leave alone

                    string key = MaterialKey(m);
                    if (byKey.TryGetValue(key, out var rep))
                    {
                        if (rep != m)
                        {
                            mats[i] = rep;
                            changed = true;
                            merged++;
                        }
                    }
                    else
                    {
                        byKey[key] = m;
                    }
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                }
            }
            return merged;
        }

        /// <summary>Full-property identity key: two materials merge ONLY when every shader property
        /// (colors, floats/ranges, texture refs + scale/offset), the shader itself, keywords and
        /// render queue all match — visual equality by construction.</summary>
        internal static string MaterialKey(Material m)
        {
            var sb = new StringBuilder(m.shader != null ? m.shader.name : "null");
            sb.Append('|').Append(m.renderQueue);
            var keywords = m.shaderKeywords;
            System.Array.Sort(keywords);
            sb.Append('|').Append(string.Join(",", keywords));

            var shader = m.shader;
            int count = shader != null ? shader.GetPropertyCount() : 0;
            for (int i = 0; i < count; i++)
            {
                string name = shader.GetPropertyName(i);
                switch (shader.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        sb.Append('|').Append(name).Append(':').Append(m.GetColor(name).ToString("F4"));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        sb.Append('|').Append(name).Append(':').Append(m.GetFloat(name).ToString("F5"));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        sb.Append('|').Append(name).Append(':').Append(m.GetVector(name).ToString("F4"));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        var tex = m.GetTexture(name);
                        sb.Append('|').Append(name).Append(':')
                          .Append(tex != null ? tex.GetEntityId().ToString() : "none")
                          .Append(m.GetTextureScale(name).ToString("F4"))
                          .Append(m.GetTextureOffset(name).ToString("F4"));
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
