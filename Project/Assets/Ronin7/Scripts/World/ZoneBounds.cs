using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Keeps the player inside the bounded zone by clamping the rig to a horizontal circle.
    /// Enforces the "limited space to search" rule.
    /// Also a fall failsafe: floors are authored geometry with open edges in places, and the
    /// rig's CharacterController has real gravity — if the player slips off any unfenced edge,
    /// they would fall forever. When the rig drops below <see cref="fallResetY"/> it is
    /// teleported back to the last grounded position.
    /// </summary>
    public class ZoneBounds : MonoBehaviour
    {
        public Vector3 center = Vector3.zero;
        public float radius = 8f;
        [Tooltip("Below this Y the rig has fallen off the level and is teleported back to the last grounded position.")]
        public float fallResetY = -3f;

        private CharacterController controller;
        private Vector3 lastSafePosition;
        private bool hasSafePosition;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void LateUpdate()
        {
            Vector3 p = transform.position;
            float dx = p.x - center.x;
            float dz = p.z - center.z;
            float sqr = dx * dx + dz * dz;
            if (sqr > radius * radius)
            {
                float scale = radius / Mathf.Sqrt(sqr);
                p = new Vector3(center.x + dx * scale, p.y, center.z + dz * scale);
                transform.position = p;
            }

            if (p.y < fallResetY)
            {
                // Fell off the level. CharacterController overrides direct transform writes, so
                // toggle it around the teleport.
                Vector3 target = hasSafePosition ? lastSafePosition : new Vector3(center.x, 0f, center.z);
                if (controller != null) controller.enabled = false;
                transform.position = target;
                if (controller != null) controller.enabled = true;
            }
            else if (controller != null ? controller.isGrounded : p.y >= -0.5f)
            {
                lastSafePosition = p;
                hasSafePosition = true;
            }
        }
    }
}
