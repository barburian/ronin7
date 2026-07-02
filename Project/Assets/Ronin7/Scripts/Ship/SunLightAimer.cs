using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Keeps the scene's directional key light shining FROM the sun visual TOWARD the player. The
    /// builder aims the light once at authoring time, but in the moving-universe model the world
    /// rotates under the stationary rig as the player flies — a world-fixed light direction makes
    /// sunlight drift off the sun. Re-aiming each LateUpdate keeps planet shading consistent with
    /// where the sun actually appears.
    /// </summary>
    public class SunLightAimer : MonoBehaviour
    {
        [SerializeField] private Light sunLight;
        [Tooltip("The sun visual sphere (a child of the universe root).")]
        [SerializeField] private Transform sunVisual;

        private void LateUpdate()
        {
            if (sunLight == null || sunVisual == null) return;

            // The player rig sits at the world origin; light rays travel sun → player.
            Vector3 dir = -sunVisual.position;
            if (dir.sqrMagnitude < 1e-4f) return;
            sunLight.transform.rotation = Quaternion.LookRotation(dir.normalized);
        }
    }
}
