using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Read-only diagnostic that emits structured JSON describing every visible renderer in the open scene.
    /// Logs to Unity console for external visual continuity scoring against the spec.
    /// </summary>
    public static class ContinuityAudit
    {
        [System.Serializable]
        private class Entry
        {
            public string path;
            public string mesh;
            public bool builtInPrimitive;
            public string material;
            public float[] baseColor;
            public float[] emissionColor;
            public float emissionMag;
            public float sizeM;
        }

        [System.Serializable]
        private class Report
        {
            public int count;
            public Entry[] entries;
        }

        [MenuItem("Tools/Space Samurai/Art/Continuity — Audit Scene JSON", priority = 22)]
        private static void AuditSceneJson()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();

            List<Entry> entries = new List<Entry>();

            foreach (GameObject root in roots)
            {
                MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);

                foreach (MeshRenderer renderer in renderers)
                {
                    Entry entry = new Entry();

                    // Hierarchy path
                    entry.path = GetHierarchyPath(renderer.gameObject);

                    // Mesh info
                    MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        entry.mesh = meshFilter.sharedMesh.name;
                        entry.builtInPrimitive = SceneContinuityFix.IsBuiltInPrimitive(meshFilter.sharedMesh);
                    }
                    else
                    {
                        entry.mesh = "(none)";
                        entry.builtInPrimitive = false;
                    }

                    // Material and color info
                    if (renderer.sharedMaterial != null)
                    {
                        entry.material = renderer.sharedMaterial.name;

                        // Base color: try _BaseColor first, then _Color
                        if (renderer.sharedMaterial.HasProperty("_BaseColor"))
                        {
                            entry.baseColor = ColorToArray(renderer.sharedMaterial.GetColor("_BaseColor"));
                        }
                        else if (renderer.sharedMaterial.HasProperty("_Color"))
                        {
                            entry.baseColor = ColorToArray(renderer.sharedMaterial.GetColor("_Color"));
                        }
                        else
                        {
                            entry.baseColor = new float[] { 0, 0, 0 };
                        }

                        // Emission color
                        if (renderer.sharedMaterial.HasProperty("_EmissionColor"))
                        {
                            entry.emissionColor = ColorToArray(renderer.sharedMaterial.GetColor("_EmissionColor"));
                        }
                        else
                        {
                            entry.emissionColor = new float[] { 0, 0, 0 };
                        }
                    }
                    else
                    {
                        entry.material = "(none)";
                        entry.baseColor = new float[] { 0, 0, 0 };
                        entry.emissionColor = new float[] { 0, 0, 0 };
                    }

                    // Emission magnitude
                    entry.emissionMag = Mathf.Max(entry.emissionColor[0], entry.emissionColor[1], entry.emissionColor[2]);

                    // Size in meters
                    entry.sizeM = Mathf.Max(
                        renderer.bounds.size.x,
                        renderer.bounds.size.y,
                        renderer.bounds.size.z
                    );

                    entries.Add(entry);
                }
            }

            // Create report and serialize
            Report report = new Report
            {
                count = entries.Count,
                entries = entries.ToArray()
            };

            string json = JsonUtility.ToJson(report, true);
            Debug.Log("[ContinuityAudit] " + json);
        }

        private static string GetHierarchyPath(GameObject go)
        {
            List<string> path = new List<string>();
            Transform current = go.transform;

            while (current != null)
            {
                path.Insert(0, current.gameObject.name);
                current = current.parent;
            }

            return string.Join("/", path);
        }

        private static float[] ColorToArray(Color c)
        {
            return new float[] { c.r, c.g, c.b };
        }
    }
}
