using System.IO;
using Ronin7.World;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Bakes a procedural 6-bone walk rig into the single-mesh Tripo character prefabs under
    /// Characters3D/{Named,Enemies,Diversity}/ so <see cref="NpcWalkAnimator"/> can swing their
    /// legs/arms while they move (they previously slid around as one static mesh). Per prefab:
    /// transforms the mesh into root space, computes bone weights via <see cref="NpcRigSolver"/>,
    /// saves the skinned mesh as &lt;prefab-dir&gt;/Rigged/&lt;Name&gt;_Skinned.asset, and replaces
    /// the old MeshRenderer subtree with a SkinnedMeshRenderer + Rig_Root bone hierarchy. Idempotent:
    /// prefabs that already contain a SkinnedMeshRenderer are skipped, and multi-renderer prefabs
    /// (greybox placeholders) are left alone. Prefab GUIDs are preserved, so every scene instance
    /// picks the rig up automatically. Non-humanoid meshes (drones) degrade to a body-weighted slide.
    /// </summary>
    public static class NpcAutoRigger
    {
        private const string CharRoot = "Assets/Ronin7/Art/Generated/Characters3D";
        private const string NamedFolder = CharRoot + "/Named";
        private const string EnemiesFolder = CharRoot + "/Enemies";
        private const string DiversityFolder = CharRoot + "/Diversity";

        [MenuItem("Tools/Space Samurai/Art/Rig NPC Characters For Walk", priority = 210)]
        public static void RigAllNamedCharacters() => RigFolder(NamedFolder, "Named");

        [MenuItem("Tools/Space Samurai/Art/Rig Enemy Characters", priority = 211)]
        public static void RigAllEnemies() => RigFolder(EnemiesFolder, "Enemies");

        [MenuItem("Tools/Space Samurai/Art/Rig Filler Crowd", priority = 212)]
        public static void RigAllFiller() => RigFolder(DiversityFolder, "Filler");

        /// <summary>Rigs every single-mesh Tripo prefab found (recursively) under <paramref name="folder"/>.</summary>
        public static void RigFolder(string folder, string label)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            int rigged = 0, skipped = 0, failed = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                switch (RigPrefab(path))
                {
                    case RigResult.Rigged: rigged++; break;
                    case RigResult.Skipped: skipped++; break;
                    default: failed++; break;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NpcAutoRigger] {label}: {rigged} rigged, {skipped} skipped (already rigged / not a single-mesh character), {failed} failed.");
        }

        private enum RigResult { Rigged, Skipped, Failed }

        private static RigResult RigPrefab(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var existingSmr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (existingSmr != null)
                {
                    // Already rigged: upgrade in place if the rig predates the jaw bone. The baked
                    // _Skinned mesh already stores root-space vertices, so weights can be re-solved
                    // without the (long-deleted) static source mesh.
                    if (FindDeep(root.transform, NpcWalkAnimator.JawBoneName) != null)
                        return RigResult.Skipped; // current rig — nothing to do
                    return UpgradeRigWithJaw(root, existingSmr, path);
                }

                var filters = root.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length != 1)
                    return RigResult.Skipped; // greybox placeholder or empty stub — not a Tripo single mesh

                MeshFilter mf = filters[0];
                var mr = mf.GetComponent<MeshRenderer>();
                Mesh source = mf.sharedMesh;
                if (mr == null || source == null)
                    return RigResult.Skipped;

                EnsureMeshReadable(source);

                // Bake the mesh into ROOT space so the import node rotations/scales (tripo_node,
                // 'scene') are flattened away and the rig can live in plain character space
                // (+Y up, +Z the NPC's facing that the movement scripts rotate).
                Matrix4x4 toRoot = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var baked = Object.Instantiate(source);
                baked.name = Path.GetFileNameWithoutExtension(path) + "_Skinned";

                Vector3[] verts = source.vertices;
                for (int i = 0; i < verts.Length; i++) verts[i] = toRoot.MultiplyPoint3x4(verts[i]);
                baked.vertices = verts;

                Vector3[] normals = source.normals;
                if (normals != null && normals.Length == verts.Length)
                {
                    for (int i = 0; i < normals.Length; i++)
                        normals[i] = toRoot.MultiplyVector(normals[i]).normalized;
                    baked.normals = normals;
                }

                Vector4[] tangents = source.tangents;
                if (tangents != null && tangents.Length == verts.Length)
                {
                    for (int i = 0; i < tangents.Length; i++)
                    {
                        Vector3 t = toRoot.MultiplyVector(tangents[i]).normalized;
                        tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
                    }
                    baked.tangents = tangents;
                }

                baked.RecalculateBounds();
                NpcRigLayout layout = NpcRigSolver.SolveLayout(baked.bounds);
                baked.boneWeights = NpcRigSolver.Solve(verts, layout);

                // Bone hierarchy: legs under the rig root (planted), arms under the body so they
                // follow the torso bob. Order must match NpcRigSolver bone indices.
                Transform rigRoot = NewChild(root.transform, NpcWalkAnimator.RootBoneName, Vector3.zero);
                Transform body = NewChild(rigRoot, NpcWalkAnimator.BodyBoneName, layout.BodyJoint);
                Transform legL = NewChild(rigRoot, NpcWalkAnimator.LegLBoneName, layout.HipL);
                Transform legR = NewChild(rigRoot, NpcWalkAnimator.LegRBoneName, layout.HipR);
                Transform armL = NewChild(body, NpcWalkAnimator.ArmLBoneName, layout.ShoulderL);
                Transform armR = NewChild(body, NpcWalkAnimator.ArmRBoneName, layout.ShoulderR);
                // Jaw under the body so it follows the torso bob/talk nod. NewChild takes the
                // parent-LOCAL position, so joints under `body` subtract its own offset.
                Transform jaw = NewChild(body, NpcWalkAnimator.JawBoneName, layout.JawJoint - layout.BodyJoint);
                Transform[] bones = { body, legL, legR, armL, armR, jaw };

                var bindposes = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    bindposes[i] = bones[i].worldToLocalMatrix * root.transform.localToWorldMatrix;
                baked.bindposes = bindposes;

                SaveMeshAsset(baked, path);

                var visualGo = new GameObject("Visual_Skinned");
                visualGo.transform.SetParent(root.transform, false);
                var smr = visualGo.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = baked;
                smr.bones = bones;
                smr.rootBone = rigRoot;
                smr.sharedMaterials = mr.sharedMaterials;
                smr.updateWhenOffscreen = false;
                var b = baked.bounds;
                b.Expand(0.4f); // headroom for limb swing so culling never clips a mid-stride pose
                smr.localBounds = b;

                // Drop the old static-visual subtree (the direct child of root that holds the mesh).
                Transform oldTop = mf.transform;
                while (oldTop.parent != null && oldTop.parent != root.transform) oldTop = oldTop.parent;
                if (oldTop == root.transform)
                {
                    Object.DestroyImmediate(mr);
                    Object.DestroyImmediate(mf);
                }
                else
                {
                    Object.DestroyImmediate(oldTop.gameObject);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return RigResult.Rigged;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NpcAutoRigger] Failed to rig '{path}': {ex.Message}");
                return RigResult.Failed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Upgrades a pre-jaw 5-bone rig in place: re-solves the baked mesh's weights with the
        /// 6-bone solver (the _Skinned asset's vertices are already in root space), adds the
        /// Rig_Jaw bone under Rig_Body, and appends its bindpose. The existing five bones keep
        /// their transforms, so old bindposes/weights stay valid for non-jaw vertices.
        /// </summary>
        private static RigResult UpgradeRigWithJaw(GameObject root, SkinnedMeshRenderer smr, string path)
        {
            Mesh mesh = smr.sharedMesh;
            Transform body = FindDeep(root.transform, NpcWalkAnimator.BodyBoneName);
            if (mesh == null || body == null || smr.bones == null || smr.bones.Length != NpcRigSolver.BoneCount - 1)
                return RigResult.Skipped; // not the 5-bone auto-rig this upgrade targets

            NpcRigLayout layout = NpcRigSolver.SolveLayout(mesh.bounds);

            Transform jaw = NewChild(body, NpcWalkAnimator.JawBoneName, layout.JawJoint - layout.BodyJoint);
            var bones = new Transform[NpcRigSolver.BoneCount];
            for (int i = 0; i < smr.bones.Length; i++) bones[i] = smr.bones[i];
            bones[NpcRigSolver.BoneJaw] = jaw;

            // Bindposes before weights: a weight referencing bone 5 must never exist while the
            // mesh still carries only 5 bindposes.
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                bindposes[i] = bones[i].worldToLocalMatrix * root.transform.localToWorldMatrix;
            mesh.bindposes = bindposes;
            mesh.boneWeights = NpcRigSolver.Solve(mesh.vertices, layout);
            smr.bones = bones;

            EditorUtility.SetDirty(mesh);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return RigResult.Rigged;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = FindDeep(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void EnsureMeshReadable(Mesh mesh)
        {
            if (mesh.isReadable) return;
            string meshPath = AssetDatabase.GetAssetPath(mesh);
            if (AssetImporter.GetAtPath(meshPath) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        private static Transform NewChild(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private static void SaveMeshAsset(Mesh mesh, string prefabPath)
        {
            // Store the baked skinned mesh in a Rigged/ subfolder next to the source prefab, so the
            // same rigger works for Named/, Enemies/, and Diversity/<Type>/ alike.
            string dir = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            string riggedFolder = dir + "/Rigged";
            if (!AssetDatabase.IsValidFolder(riggedFolder))
                AssetDatabase.CreateFolder(dir, "Rigged");
            string assetPath = $"{riggedFolder}/{Path.GetFileNameWithoutExtension(prefabPath)}_Skinned.asset";
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(mesh, assetPath);
        }
    }
}
