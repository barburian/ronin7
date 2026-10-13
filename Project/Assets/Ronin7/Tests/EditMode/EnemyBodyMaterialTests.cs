using NUnit.Framework;
using Ronin7.Enemies;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// BUG-17: the windup telegraph tints <c>_BaseColor</c>/<c>_EmissionColor</c> via RendererTint. The
    /// glTFast import material reads <c>baseColorFactor</c> instead, so the baker must swap every
    /// roguelike enemy body onto Ronin7/GrittyCyber or the telegraph is invisible.
    /// </summary>
    public class EnemyBodyMaterialTests
    {
        private const string EnemyFolder = "Assets/Ronin7/Prefabs/Roguelike/Enemies";

        [Test]
        public void EveryRoguelikeEnemy_BodyRenderer_UsesGrittyCyber()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyFolder });
            Assert.IsNotEmpty(guids, "no baked enemy prefabs found");

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Enemy>();
                Assert.IsNotNull(enemy, $"{path}: no Enemy");
                var body = new SerializedObject(enemy).FindProperty("bodyRenderer").objectReferenceValue as Renderer;
                Assert.IsNotNull(body, $"{path}: bodyRenderer not wired");

                foreach (var mat in body.sharedMaterials)
                {
                    Assert.IsNotNull(mat, $"{path}: empty material slot");
                    Assert.AreEqual("Ronin7/GrittyCyber", mat.shader.name, path);
                    Assert.IsTrue(mat.HasProperty("_BaseColor"), path);
                    Assert.IsTrue(mat.HasProperty("_EmissionColor"), path);
                    // Full metal = no diffuse; with no reflection probe on Quest the body renders black.
                    Assert.LessOrEqual(mat.GetFloat("_Metallic"), 0.2f, $"{path}: _Metallic too high, body renders black on Quest");
                    Assert.Greater(mat.GetColor("_BaseColor").maxColorComponent, 0.5f, $"{path}: _BaseColor near black");
                }
            }
        }
    }
}
