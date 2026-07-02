using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Swaps built-in Unity primitive meshes (Sphere/Cube/Capsule/Cylinder) with their
    /// faceted LowPolyMeshes equivalents in the open scene. Provides an audit (dry run) and
    /// a facetize operation; both are fully reversible via Undo. The single biggest visual-
    /// continuity lever: built-in primitives appear "too smooth" next to faceted geometry.
    /// </summary>
    public static class SceneContinuityFix
    {
        /// <summary>
        /// Detects whether a mesh is a Unity built-in primitive (Sphere/Cube/Capsule/Cylinder).
        /// Built-in primitives live in "Library/unity default resources" and have names matching
        /// one of the supported types. Returns false for Quad/Plane (no faceted equivalent).
        /// </summary>
        public static bool IsBuiltInPrimitive(Mesh m)
        {
            if (m == null)
                return false;

            string assetPath = AssetDatabase.GetAssetPath(m);
            if (assetPath != "Library/unity default resources")
                return false;

            // Only Sphere, Cube, Capsule, Cylinder have faceted equivalents.
            // Quad and Plane are excluded.
            return m.name == "Sphere"
                || m.name == "Cube"
                || m.name == "Capsule"
                || m.name == "Cylinder";
        }

        /// <summary>
        /// Maps a built-in primitive mesh to its faceted LowPolyMeshes equivalent.
        /// Returns null if the mesh is not a recognized built-in primitive.
        /// </summary>
        public static Mesh FacetedFor(Mesh builtIn)
        {
            if (builtIn == null)
                return null;

            PrimitiveType? type = null;
            switch (builtIn.name)
            {
                case "Sphere":
                    type = PrimitiveType.Sphere;
                    break;
                case "Cube":
                    type = PrimitiveType.Cube;
                    break;
                case "Capsule":
                    type = PrimitiveType.Capsule;
                    break;
                case "Cylinder":
                    type = PrimitiveType.Cylinder;
                    break;
            }

            if (type == null)
                return null;

            return LowPolyMeshes.ForType(type.Value);
        }

        [MenuItem("Tools/Space Samurai/Art/Continuity — Audit Open Scene (dry run)", priority = 20)]
        private static void AuditOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();

            int totalCount = 0;
            System.Collections.Generic.Dictionary<string, int> countByType =
                new System.Collections.Generic.Dictionary<string, int>();

            foreach (GameObject root in roots)
            {
                MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
                foreach (MeshFilter mf in filters)
                {
                    if (mf.sharedMesh != null && IsBuiltInPrimitive(mf.sharedMesh))
                    {
                        if (FacetedFor(mf.sharedMesh) != null)
                        {
                            totalCount++;
                            string name = mf.sharedMesh.name;
                            if (!countByType.ContainsKey(name))
                                countByType[name] = 0;
                            countByType[name]++;
                        }
                    }
                }
            }

            // Build the summary string.
            System.Text.StringBuilder summary = new System.Text.StringBuilder();
            summary.Append("[SceneContinuityFix] DRY RUN: ");
            summary.Append(totalCount);
            summary.Append(" built-in primitive meshes (");

            bool first = true;
            foreach (var kvp in countByType)
            {
                if (!first)
                    summary.Append(", ");
                summary.Append(kvp.Key);
                summary.Append("×");
                summary.Append(kvp.Value);
                first = false;
            }

            summary.Append(") would be faceted. Run 'Facetize Open Scene' to apply.");
            Debug.Log(summary.ToString());
        }

        [MenuItem("Tools/Space Samurai/Art/Continuity — Facetize Open Scene", priority = 21)]
        private static void FacetizeOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();

            Undo.SetCurrentGroupName("Facetize Scene");
            int undoGroup = Undo.GetCurrentGroup();

            int swappedCount = 0;

            foreach (GameObject root in roots)
            {
                MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
                foreach (MeshFilter mf in filters)
                {
                    if (mf.sharedMesh != null && IsBuiltInPrimitive(mf.sharedMesh))
                    {
                        Mesh faceted = FacetedFor(mf.sharedMesh);
                        if (faceted != null)
                        {
                            Undo.RecordObject(mf, "Facetize Scene");
                            mf.sharedMesh = faceted;
                            swappedCount++;
                        }
                    }
                }
            }

            if (swappedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[SceneContinuityFix] Facetized {swappedCount} mesh(es).");
        }
    }
}
