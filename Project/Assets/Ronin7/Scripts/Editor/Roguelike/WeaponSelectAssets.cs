using System.IO;
using Ronin7.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ronin7.EditorTools
{
    /// <summary>P9-01 (A15.13) editor setup, both additive and idempotent: builds the WEAPON SELECT card models plus the
    /// Resources/WeaponCardModels asset that names them, and removes the boot rack's blades and LoadoutRackDisplay
    /// from Phase6_Boot without rebuilding the scene. Entries already set in the asset are never overwritten, so a
    /// P9-04 Tripo swap survives a re-run.</summary>
    public static class WeaponSelectAssets
    {
        private const string CardDir = "Assets/Ronin7/Prefabs/Art/WeaponCards";
        private const string AssetPath = "Assets/Ronin7/Resources/" + WeaponCardModels.ResourcePath + ".asset";
        private const string KatanaPrefab = "Assets/Ronin7/Prefabs/Art/Sword_Katana.prefab";
        private const string CrimsonMat = "Assets/Ronin7/Art/Materials/HeroVariants/CyberSword_Blade_Crimson.mat";
        private const string SteelMat = "Assets/Ronin7/Art/Materials/Mat_Armor_Metal.mat";
        private const string GlowMat = "Assets/Ronin7/Art/Materials/Mat_CyberNeon_Cyan.mat";

        [MenuItem("Tools/Space Samurai/Roguelike/Build Weapon Select Models (P9-01)", priority = 520)]
        public static void BuildModels()
        {
            if (!Directory.Exists(CardDir)) Directory.CreateDirectory(CardDir);
            var asset = AssetDatabase.LoadAssetAtPath<WeaponCardModels>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WeaponCardModels>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            var so = new SerializedObject(asset);
            var arr = so.FindProperty("models");
            if (arr.arraySize < 4) arr.arraySize = 4;
            var builders = new System.Func<GameObject>[] { KatanaCard, DoubleKatanaCard, PolearmCard, WhipCard };
            var names = new[] { "Card_Katana", "Card_DoubleKatana", "Card_Polearm", "Card_Whip" };
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    var el = arr.GetArrayElementAtIndex(i);
                    if (el.objectReferenceValue != null) continue;
                    var root = builders[i]();
                    root.name = names[i];
                    SceneManager.MoveGameObjectToScene(root, scene);
                    el.objectReferenceValue = PrefabUtility.SaveAsPrefabAsset(root, $"{CardDir}/{names[i]}.prefab");
                    Object.DestroyImmediate(root);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        // Cards are authored upright (+Y up, facing -Z); WeaponSelectPanelFactory scales and centres them.
        private static GameObject KatanaCard()
        {
            var root = new GameObject();
            Katana(root.transform, Vector3.zero, 0f, false);
            return root;
        }

        private static GameObject DoubleKatanaCard()
        {
            var root = new GameObject();
            Katana(root.transform, new Vector3(-0.1f, 0f, 0f), 10f, false);
            Katana(root.transform, new Vector3(0.1f, 0f, 0.02f), -10f, true); // A15.5: crimson through its own material
            return root;
        }

        /// <summary>One Sword_Katana standing blade-up (handle to BladeTip becomes +Y), tilted by roll degrees.</summary>
        private static void Katana(Transform parent, Vector3 pos, float roll, bool crimson)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(KatanaPrefab);
            var pivot = new GameObject("Katana").transform;
            pivot.SetParent(parent, false);
            var sword = (GameObject)PrefabUtility.InstantiatePrefab(src, pivot);
            Transform blade0 = sword.transform.Find("Blade"), handle = sword.transform.Find("Handle");
            if (blade0 != null && handle != null) // handle -> blade centre is the blade-up direction
                sword.transform.localRotation = Quaternion.FromToRotation(
                    blade0.GetComponent<Renderer>().bounds.center - handle.GetComponent<Renderer>().bounds.center, Vector3.up);
            pivot.localPosition = pos;
            pivot.localRotation = Quaternion.Euler(0f, 0f, roll);
            if (!crimson) return;
            var blade = sword.transform.Find("Blade");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CrimsonMat);
            if (blade == null || mat == null) return;
            var r = blade.GetComponent<Renderer>();
            var mats = r.sharedMaterials;
            mats[0] = mat;
            r.sharedMaterials = mats;
        }

        /// <summary>Placeholder until the P9-04 Tripo mesh: steel pole, diamond head, glowing shield disc.</summary>
        private static GameObject PolearmCard()
        {
            var root = new GameObject();
            Prim(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(0.04f, 0.9f, 0.04f), Quaternion.identity, SteelMat);
            Prim(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.05f, 0f), new Vector3(0.09f, 0.3f, 0.03f), Quaternion.identity, SteelMat);
            Prim(root.transform, PrimitiveType.Cylinder, new Vector3(0.3f, 0.1f, -0.03f), new Vector3(0.5f, 0.02f, 0.5f), Quaternion.Euler(90f, 0f, 0f), GlowMat);
            return root;
        }

        /// <summary>Placeholder coiled whip: two rings of beads facing the viewer on a handle.</summary>
        private static GameObject WhipCard()
        {
            var root = new GameObject();
            Prim(root.transform, PrimitiveType.Capsule, new Vector3(0f, -0.2f, 0f), new Vector3(0.05f, 0.18f, 0.05f), Quaternion.identity, SteelMat);
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 2f / 8f;
                    float rad = 0.16f - ring * 0.04f;
                    Prim(root.transform, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * rad, 0.2f + Mathf.Sin(a) * rad, ring * 0.05f), Vector3.one * 0.07f, Quaternion.identity, GlowMat);
                }
            return root;
        }

        private static void Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, string matPath)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }
    }
}
