using System;
using NUnit.Framework;
using Ronin7.Audio;
using Ronin7.Player;
using Ronin7.World;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P0-05 (BUG-05): RunArena once shipped without the combat-feedback components or the
    /// settings panel. Guards the saved scene's contents so a rebuild cannot silently drop them.</summary>
    public class RunArenaSceneTests
    {
        private const string ScenePath = "Assets/Ronin7/Scenes/RunArena.unity";

        [Test]
        public void ContainsRequiredFeedbackComponents()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (Type type in new[]
                {
                    typeof(CombatFeedbackController), typeof(HeartbeatHaptics), typeof(BladeClashFeedback),
                    typeof(SettingsPanel), typeof(SettingsMenuToggle), typeof(RunArenaController),
                })
                {
                    Assert.IsTrue(SceneHas(scene, type), $"RunArena is missing {type.Name}.");
                }

                var toggle = FindIn<SettingsMenuToggle>(scene);
                var listens = new UnityEditor.SerializedObject(toggle).FindProperty("listenForMenuButton").boolValue;
                Assert.IsFalse(listens, "In RunArena the Menu button pauses; the settings toggle must not also listen to it.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool SceneHas(Scene scene, Type type)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren(type, true) != null) return true;
            return false;
        }

        private static T FindIn<T>(Scene scene) where T : Object
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }
    }
}
