using UnityEngine;

namespace Ronin7.Flow
{
    /// <summary>
    /// Hub-side launcher for the roguelike mode. Sits next to <see cref="MissionLauncher"/> on the
    /// hub console — same idiom (a thin interactable-facing wrapper), invoked by a console
    /// button/prompt. Unlike <see cref="MissionLauncher"/> there is no "next incomplete" lookup:
    /// every run starts fresh from node 0 with a new seed.
    /// </summary>
    public class RunLauncher : MonoBehaviour
    {
        /// <summary>Start a new roguelike run with a random seed.</summary>
        public void LaunchRun()
        {
            if (RunDirector.Instance == null)
            {
                Debug.LogError("[RunLauncher] No RunDirector in the scene.");
                return;
            }
            RunDirector.Instance.StartRun();
        }
    }
}
