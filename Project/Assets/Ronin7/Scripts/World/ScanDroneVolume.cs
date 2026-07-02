using Ronin7.Core;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Chapter 4's ambient scan-drone/patrol detection source. Distance-checks a serialized
    /// <see cref="target"/> against <see cref="radius"/> every Update, like <see cref="ProximityDoor"/>'s
    /// proximity check, rather than a physics trigger. While the target is inside range, reports
    /// detection to <see cref="heat"/> and pulses <see cref="telegraphRenderer"/> so the player can read
    /// the exposure without any UI.
    /// </summary>
    public class ScanDroneVolume : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private HeatMeter heat;
        [SerializeField] private float radius = 6f;
        [SerializeField] private float detectionPerSecond = 0.25f;

        [Header("Telegraph")]
        [SerializeField] private Renderer telegraphRenderer;
        [SerializeField] private Color idleColor = new Color(0.6f, 0.65f, 0.7f);
        [SerializeField] private Color activeColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] private float pulseSpeed = 3f;

        private void Update()
        {
            if (target == null) return;

            float sqrDist = (transform.position - target.position).sqrMagnitude;
            bool inRange = sqrDist <= radius * radius;

            if (inRange && heat != null)
                heat.ReportDetection(detectionPerSecond * Time.deltaTime);

            if (telegraphRenderer != null)
            {
                Color c = inRange
                    ? Color.Lerp(idleColor, activeColor, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f)
                    : idleColor;
                RendererTint.Apply(telegraphRenderer, c);
            }
        }
    }
}
