using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Cockpit-mounted waypoint arrow that points toward a target planet. Like <see cref="ShipCrosshair"/>,
    /// it lives in the stable cockpit and does not move with the head, providing a comfortable
    /// fixed-cockpit HUD experience. The arrow direction is recomputed each frame by converting
    /// the universe-space vector to ship-local coordinates, so it always aims correctly even as
    /// the ship rotates. Hides when the player approaches the target (within <see cref="arriveRange"/>).
    /// </summary>
    public class PlanetTargetMarker : MonoBehaviour
    {
        [SerializeField] private Transform arrow;
        [SerializeField] private Transform target;
        [SerializeField] private Transform universe;
        [SerializeField] private float fadeStartRange = 600f;
        [SerializeField] private float arriveRange = 120f;
        [SerializeField] private Renderer arrowRenderer;
        [SerializeField] private Color aheadColor = new Color(0.3f, 0.8f, 1f);
        [SerializeField] private Color behindColor = new Color(1f, 0.55f, 0.1f);
        [SerializeField] private float minAlpha = 0.45f;

        private Transform universeRoot;
        private MaterialPropertyBlock mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Start()
        {
            mpb = new MaterialPropertyBlock();

            universeRoot = universe != null ? universe : ShipController.Instance?.Universe;
            if (target == null || universeRoot == null)
            {
                Debug.LogWarning("[PlanetTargetMarker] Missing target or universe; disabling.", gameObject);
                enabled = false;
                return;
            }
        }

        private void LateUpdate()
        {
            if (ShipController.Instance == null || arrow == null || target == null || universeRoot == null)
                return;

            // Re-sample every frame: targets MOVE (orbiting planets, enemy ships), so a position
            // captured once at Start/SetTarget would point at where the target used to be.
            Vector3 targetUniversePos = universeRoot.InverseTransformPoint(target.position);
            Vector3 toTargetUniverse = targetUniversePos - ShipController.Instance.ShipPosition;
            float dist = toTargetUniverse.magnitude;

            // Convert universe-space direction into ship-local frame.
            Vector3 localDir = Quaternion.Inverse(ShipController.Instance.ShipRotation) * toTargetUniverse;

            // When the target is behind the player, the arrow would point backward with no cue,
            // so instead point it the way to turn (toward the on-screen direction of the target).
            bool behind = localDir.z < 0f;
            if (behind)
            {
                Vector3 turnDir = new Vector3(localDir.x, localDir.y, 0f);
                if (turnDir.sqrMagnitude < 0.0001f)
                    turnDir = Vector3.down; // target directly behind: pick an arbitrary turn cue.
                arrow.localRotation = Quaternion.LookRotation(turnDir.normalized);
            }
            else if (localDir.sqrMagnitude > 0.0001f)
            {
                arrow.localRotation = Quaternion.LookRotation(localDir.normalized);
            }

            // Hide when arrived. Toggle the RENDERER, not the GameObject: the marker (and the
            // ObjectiveArrowController) live on the arrow object itself, so SetActive(false) here
            // would disable this very script and the arrow could never come back for the session.
            bool show = dist > arriveRange;
            if (arrowRenderer != null && arrowRenderer.enabled != show)
                arrowRenderer.enabled = show;

            // Fade alpha based on distance.
            if (arrowRenderer != null && show)
            {
                float alpha = Mathf.Clamp((dist - arriveRange) / (fadeStartRange - arriveRange), minAlpha, 1f);
                Color c = behind ? behindColor : aheadColor;
                c.a = alpha;
                arrowRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, c);
                mpb.SetColor(ColorId, c);
                arrowRenderer.SetPropertyBlock(mpb);
            }
        }

        /// <summary>Update the target; its position is sampled live every frame.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
