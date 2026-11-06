using NUnit.Framework;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>Playtest: the Warden is clawed (Claws kit, no held katana); the eye material is a shipped asset.</summary>
    public class ClawedBossTests
    {
        private const string Dir = "Assets/Ronin7/Prefabs/Roguelike/Enemies/";

        [Test]
        public void Warden_HasNoBakedKatana_AndClawsKit()
        {
            var warden = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "The-Warden.prefab");
            foreach (var t in warden.GetComponentsInChildren<Transform>(true))
                Assert.AreNotEqual("Sword", t.name, "no katana on the clawed Warden");
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>("Assets/Ronin7/Data/Roguelike/EnemySpawnTable.asset");
            EnemySpawnTable.BossEntry e = null;
            foreach (var b in table.bosses) if (b.prefab != null && b.prefab.name == "The-Warden") e = b;
            Assert.NotNull(e);
            Assert.NotNull(e.mainHand, "Claws composed at spawn");
            Assert.AreEqual("Swipe", e.mainHand.patterns[0].name);
            Assert.IsNull(e.mainHand.prefab, "claws are meshless");
            Assert.Greater(e.mainHand.tip.magnitude, 0.1f, "parry tip off the hand");
        }

        [Test]
        public void MaelgornKeepsKatana()
        {
            var m = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "Maelgorn.prefab");
            bool found = false;
            foreach (var t in m.GetComponentsInChildren<Transform>(true)) found |= t.name == "Sword";
            Assert.IsTrue(found);
        }

        [Test]
        public void EyeMaterial_IsAShippedAsset()
        {
            var mat = Resources.Load<Material>(DodgeEyes.MaterialResource);
            Assert.NotNull(mat);
            Assert.IsTrue(mat.shader.name.Contains("Unlit"));
            Assert.Greater(mat.GetColor("_BaseColor").r, 1f, "HDR red");
            StringAssert.DoesNotContain("Shader.Find", System.IO.File.ReadAllText("Assets/Ronin7/Scripts/Enemies/DodgeEyes.cs").Replace("Shader.Find can be stripped", ""));
        }
    }
}
