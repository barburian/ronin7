using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Art
{
    /// <summary>P5-02: each hand prefab is one merged renderer (toon + outline = 2 draws).</summary>
    public class HandMergeTests
    {
        [TestCase("Hand_L")]
        [TestCase("Hand_R")]
        public void HandPrefab_IsSingleMergedRenderer(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Ronin7/Prefabs/Art/{name}.prefab");
            Assert.IsNotNull(prefab, name);
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            Assert.AreEqual(1, renderers.Length, "hand parts must be merged into one mesh");
            Assert.LessOrEqual(renderers[0].sharedMaterials.Length, 2, "toon + outline only");
            Assert.IsNotNull(renderers[0].GetComponent<MeshFilter>().sharedMesh.colors, "part colours live in vertex colours");
        }
    }
}
