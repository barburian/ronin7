using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Player;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Art
{
    /// <summary>P5-04 / BUG-28: the hero blade keeps the CyberSword shader (and the P5-03 trail driver).</summary>
    public class CyberSwordTests
    {
        [Test]
        public void SwordKatanaPrefab_BladeUsesCyberSword()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Ronin7/Prefabs/Art/Sword_Katana.prefab");
            Assert.IsNotNull(prefab);
            var blade = prefab.GetComponentInChildren<BladeDamager>(true);
            Assert.IsNotNull(blade);
            Assert.AreEqual("Ronin7/CyberSword", blade.GetComponent<Renderer>().sharedMaterial.shader.name);
            Assert.IsNotNull(prefab.GetComponentInChildren<BladeTrailDriver>(true), "P5-03 trail driver must survive");
        }
    }
}
