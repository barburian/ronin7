using NUnit.Framework;
using Ronin7.EditorTools;
using Ronin7.Enemies;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Editor
{
    /// <summary>A data rebuild must not wipe hand-tuned RL_ values: re-deriving every clone from its story
    /// source plus the builder's overrides has to reproduce the committed asset field-for-field.</summary>
    public class RlDefinitionOverridesTests
    {
        [Test]
        public void Builder_Overrides_ReproduceCommittedRlDefinitions()
        {
            int checkedCount = 0;
            foreach (var (source, rlName) in RoguelikeDataBuilder.DefinitionSources())
            {
                var src = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Ronin7/Data/" + source + ".asset");
                var committed = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    "Assets/Ronin7/Data/Roguelike/Enemies/RL_" + rlName + ".asset");
                Assert.IsNotNull(src, source);
                Assert.IsNotNull(committed, rlName);

                var rebuilt = Object.Instantiate(src);
                try
                {
                    RoguelikeDataBuilder.Overrides(rlName)(rebuilt);
                    Assert.AreEqual(JsonUtility.ToJson(committed), JsonUtility.ToJson(rebuilt), "RL_" + rlName);
                }
                finally { Object.DestroyImmediate(rebuilt); }
                checkedCount++;
            }
            Assert.AreEqual(18, checkedCount); // 14 + 4 boss phases (P3-04)
        }
    }
}
