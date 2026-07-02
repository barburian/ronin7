using System.IO;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Editor.Art;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.EditMode.Art
{
    /// <summary>
    /// Tests for the description → character pipeline. The text-response parsing
    /// (ExtractInlineText / StripJsonFence) runs with no network. The build tests exercise
    /// BuildCharacterFromSpec on hand-authored specs (no Gemini call), then delete the prefabs they
    /// create so the project is left clean.
    /// </summary>
    public class CharacterGenTests
    {
        // ---------------- text response parsing (no network) ----------------

        [Test]
        public void ExtractInlineText_ReturnsUnescapedTextPart()
        {
            // The text part is itself JSON, so its quotes are escaped in the envelope. Build the
            // envelope with the same escaper the client uses, then assert we recover the original.
            string inner = "{\"name\":\"Bot\",\"parts\":[{\"name\":\"Body\"}]}";
            string envelope =
                "{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":" +
                GeminiClient.JsonEscape(inner) + "}]}}]}";

            string text = GeminiClient.ExtractInlineText(envelope);
            Assert.AreEqual(inner, text);
        }

        [Test]
        public void ExtractInlineText_NoTextPart_Throws()
        {
            string json = "{\"error\":{\"code\":400,\"message\":\"bad request\"}}";
            Assert.Throws<System.InvalidOperationException>(() => GeminiClient.ExtractInlineText(json));
        }

        [Test]
        public void StripJsonFence_RemovesJsonFence()
        {
            Assert.AreEqual("{\"a\":1}", GeminiClient.StripJsonFence("```json\n{\"a\":1}\n```"));
            Assert.AreEqual("{\"a\":1}", GeminiClient.StripJsonFence("```\n{\"a\":1}\n```"));
        }

        [Test]
        public void StripJsonFence_LeavesUnfencedTextUntouched()
        {
            Assert.AreEqual("{\"a\":1}", GeminiClient.StripJsonFence("{\"a\":1}"));
        }

        [Test]
        public void TextCachePathFor_DiffersBySeed_AndIsJson()
        {
            string a = GeminiClient.TextCachePathFor("hello", 1);
            string b = GeminiClient.TextCachePathFor("hello", 2);
            Assert.AreNotEqual(a, b, "Seed must participate in the text cache key.");
            StringAssert.EndsWith(".json", a);
        }

        // ---------------- spec → prefab (creates + cleans up assets) ----------------

        private const string EnemyFootMatPath = "Assets/Ronin7/Art/Materials/SamuraiToon_EnemyFoot.mat";
        private const string GeneratedFolder  = "Assets/Ronin7/Prefabs/Art/Generated";

        private static ArtPrefabBuilder.CharacterSpec ThreePartSpec(string name)
        {
            return new ArtPrefabBuilder.CharacterSpec
            {
                name = name,
                parts = new[]
                {
                    new ArtPrefabBuilder.CharPartSpec { name = "Torso", shape = "rounded",  pos = new[] {0f,1f,0f},   scale = new[] {0.6f,0.7f,0.4f}, color = new[] {0.3f,0.6f,0.3f} },
                    new ArtPrefabBuilder.CharPartSpec { name = "Head",  shape = "sphere",   pos = new[] {0f,1.6f,0f}, scale = new[] {0.5f,0.5f,0.5f}, color = new[] {0.9f,0.8f,0.7f} },
                    new ArtPrefabBuilder.CharPartSpec { name = "Pole",  shape = "cylinder", pos = new[] {0.4f,0.8f,0f},scale = new[] {0.1f,0.6f,0.1f}, color = new[] {0.4f,0.3f,0.2f} },
                }
            };
        }

        private void DeleteGenerated(string sanitizedName)
        {
            string path = $"{GeneratedFolder}/{sanitizedName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        [Test]
        public void BuildCharacterFromSpec_Decorative_ProducesPrefabWithRenderers()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(EnemyFootMatPath) == null)
                Assert.Ignore("SamuraiToon_EnemyFoot.mat missing; skipping asset-building test.");

            const string name = "TestDeco_DELETEME";
            try
            {
                var prefab = ArtPrefabBuilder.BuildCharacterFromSpec(
                    ThreePartSpec(name), ArtPrefabBuilder.CharacterRole.Decorative);

                Assert.IsNotNull(prefab, "BuildCharacterFromSpec returned null.");
                var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                Assert.GreaterOrEqual(renderers.Length, 3,
                    "Expected at least one MeshRenderer per part (3).");
                // Decorative root must NOT carry combat components.
                Assert.IsNull(prefab.GetComponent<Health>(), "Decorative character must not have Health.");
            }
            finally { DeleteGenerated(name); }
        }

        [Test]
        public void BuildCharacterFromSpec_Combat_HasRigAndCombatComponents()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(EnemyFootMatPath) == null)
                Assert.Ignore("SamuraiToon_EnemyFoot.mat missing; skipping asset-building test.");

            const string name = "TestCombat_DELETEME";
            try
            {
                var prefab = ArtPrefabBuilder.BuildCharacterFromSpec(
                    ThreePartSpec(name), ArtPrefabBuilder.CharacterRole.Combat);

                Assert.IsNotNull(prefab, "BuildCharacterFromSpec returned null.");
                Assert.IsNotNull(prefab.GetComponent<Health>(), "Combat character needs Health.");
                Assert.IsNotNull(prefab.GetComponent<CapsuleCollider>(), "Combat character needs a hurtbox.");

                bool hasArmR = false, hasBladeTip = false;
                foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "ArmR") hasArmR = true;
                    if (t.name == "BladeTip") hasBladeTip = true;
                }
                Assert.IsTrue(hasArmR, "Combat rig must include the ArmR pivot Enemy.cs drives.");
                Assert.IsTrue(hasBladeTip, "Combat rig must include the BladeTip parry marker.");
            }
            finally { DeleteGenerated(name); }
        }

        [Test]
        public void BuildCharacterFromSpec_EmptyParts_ReturnsNull()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[CharacterGen\] spec has no parts"));
            var prefab = ArtPrefabBuilder.BuildCharacterFromSpec(
                new ArtPrefabBuilder.CharacterSpec { name = "Empty", parts = new ArtPrefabBuilder.CharPartSpec[0] },
                ArtPrefabBuilder.CharacterRole.Decorative);
            Assert.IsNull(prefab);
        }

        [Test]
        public void GenerateCharacterMenuItem_Exists()
        {
            var menuType = typeof(UnityEditor.Menu);
            var method = menuType.GetMethod("MenuItemExists",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, "UnityEditor.Menu.MenuItemExists not found via reflection.");
            bool exists = (bool)method.Invoke(null,
                new object[] { "Tools/Space Samurai/Art/Generate Character from Description" });
            Assert.IsTrue(exists, "Generate Character menu item not registered.");
        }
    }
}
