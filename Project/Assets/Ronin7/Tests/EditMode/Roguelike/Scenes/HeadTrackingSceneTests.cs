using NUnit.Framework;
using Ronin7.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ronin7.Tests.EditMode
{
    /// <summary>v0.6.0 shipped Phase6_Boot with the head camera's PoseFollower disabled and the camera pinned at
    /// 1.6 m (left over from an in-editor render setup and saved by a lightmap bake): on the headset the world
    /// followed the head and nothing could be selected. Guards the saved roguelike scenes.</summary>
    public class HeadTrackingSceneTests
    {
        [TestCase("Assets/Ronin7/Scenes/Phase6_Boot.unity")]
        [TestCase("Assets/Ronin7/Scenes/RunArena.unity")]
        public void MainCameraIsHeadTracked(string scenePath)
        {
            Scene existing = SceneManager.GetSceneByPath(scenePath);
            bool wasLoaded = existing.isLoaded;
            Scene scene = wasLoaded ? existing : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                Camera cam = null;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var c in root.GetComponentsInChildren<Camera>(true))
                        if (c.CompareTag("MainCamera")) cam = c;
                Assert.IsNotNull(cam, $"{scenePath} has no MainCamera.");

                var follower = cam.GetComponent<PoseFollower>();
                Assert.IsNotNull(follower, $"{scenePath}: the head camera has no PoseFollower.");
                Assert.IsTrue(follower.enabled, $"{scenePath}: the head camera's PoseFollower is disabled (world would follow the head).");
                Assert.AreEqual(Vector3.zero, cam.transform.localPosition,
                    $"{scenePath}: the head camera is offset locally; tracking supplies the pose.");

                foreach (var root in scene.GetRootGameObjects())
                    foreach (var f in root.GetComponentsInChildren<PoseFollower>(true))
                        Assert.IsTrue(f.enabled, $"{scenePath}: PoseFollower on {f.name} is disabled.");
            }
            finally
            {
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
