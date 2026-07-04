using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Pure yaw-only gaze math for <see cref="NpcGazeGlance"/>: how far (in degrees, around the
    /// world-up axis) an NPC's torso should turn toward a nearby target. Flattens to the XZ plane,
    /// clamps to a maximum turn so it never looks like a full retarget, and fades to zero as the
    /// target leaves proximity range so glances only happen at conversational distance.
    /// </summary>
    public static class NpcGazeSolver
    {
        public static float SolveGazeYawDegrees(Vector3 npcPosition, Vector3 npcForward, Vector3 targetPosition,
            float proximityRadius, float maxYawDegrees)
        {
            if (proximityRadius <= 0f) return 0f;

            Vector3 toTarget = targetPosition - npcPosition;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist < 0.001f || dist >= proximityRadius) return 0f;

            Vector3 flatForward = npcForward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f) return 0f;

            float fade = 1f - Mathf.Clamp01(dist / proximityRadius);
            float rawAngle = Vector3.SignedAngle(flatForward, toTarget, Vector3.up);
            float clamped = Mathf.Clamp(rawAngle, -maxYawDegrees, maxYawDegrees);
            return clamped * fade;
        }
    }

    /// <summary>
    /// Turns an ambient NPC's torso (the Rig_Body bone baked in by the NpcAutoRigger, see
    /// <see cref="NpcWalkAnimator"/>) toward the player when they're within <see cref="proximityRadius"/>,
    /// for a subtle "notices you" read with no AI/behavior changes. Purely cosmetic and additive:
    /// disables itself when the character has no rig. Zero per-frame allocations.
    /// </summary>
    public class NpcGazeGlance : MonoBehaviour
    {
        private const string BodyBoneName = "Rig_Body";

        [SerializeField] private float proximityRadius = 4f;
        [SerializeField] private float maxYawDegrees = 35f;
        [SerializeField] private float turnSpeedDegPerSec = 120f;

        private Transform body;
        private Quaternion restRotation;
        private Camera mainCamera;
        private float currentYaw;

        private void Awake()
        {
            body = FindDeep(transform, BodyBoneName);
            if (body == null)
            {
                enabled = false; // unrigged character (placeholder/non-humanoid): nothing to turn
                return;
            }

            restRotation = body.localRotation;
        }

        private void Update()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            float targetYaw = NpcGazeSolver.SolveGazeYawDegrees(
                transform.position, transform.forward, mainCamera.transform.position,
                proximityRadius, maxYawDegrees);
            currentYaw = Mathf.MoveTowards(currentYaw, targetYaw, turnSpeedDegPerSec * Time.deltaTime);
            body.localRotation = restRotation * Quaternion.AngleAxis(currentYaw, Vector3.up);
        }

        // Same recursive-search idiom as NpcWalkAnimator.FindDeep, duplicated privately so this
        // component has no dependency on it.
        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = FindDeep(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
