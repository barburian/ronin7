using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Cockpit-mounted reticle that shows the player where bolts will fly, with a second marker that
    /// appears on the aim-assist lock target.
    ///
    /// Comfort: the reticle is parented to the (stationary) cockpit, NOT to the head — head-locked
    /// HUDs are a known VR nausea trigger. Because the cockpit doesn't move (see
    /// <see cref="ShipController"/>'s "fixed cockpit, moving universe" model), a cockpit-locked
    /// reticle is rock-stable and reads exactly like a real gunsight.
    ///
    /// The lock marker is queried each <see cref="LateUpdate"/> from
    /// <see cref="ShipWeaponController.TryGetAimLock"/>, so the indicator and the actual bolt-bend
    /// are computed from the same code path — what the player sees is what they hit.
    /// </summary>
    public class ShipCrosshair : MonoBehaviour
    {
        [SerializeField] private ShipWeaponController weapon;
        [Tooltip("Cockpit-local reticle root; all child renderers get tinted on lock.")]
        [SerializeField] private GameObject reticleRoot;
        [Tooltip("World-positioned marker that snaps onto the aim-assist lock. Hidden when no lock.")]
        [SerializeField] private Renderer lockMarker;

        // HDR neon colours (>1) so the self-luminous reticle blooms: cyan when idle, amber on lock.
        [SerializeField] private Color idleColor = new Color(0.15f, 0.85f, 1f) * 2.5f;
        [SerializeField] private Color lockColor = new Color(1f, 0.55f, 0.1f) * 2.5f;

        private MaterialPropertyBlock mpb;
        private Renderer[] reticleRenderers;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            reticleRenderers = reticleRoot != null ? reticleRoot.GetComponentsInChildren<Renderer>() : System.Array.Empty<Renderer>();
            foreach (var r in reticleRenderers) ApplyColor(r, idleColor);
            if (lockMarker != null) lockMarker.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            Vector3 lockWorld = Vector3.zero;
            bool hasLock = weapon != null && weapon.TryGetAimLock(out lockWorld);
            Color c = hasLock ? lockColor : idleColor;
            foreach (var r in reticleRenderers) ApplyColor(r, c);

            if (lockMarker == null) return;
            lockMarker.gameObject.SetActive(hasLock);
            if (hasLock)
            {
                lockMarker.transform.position = lockWorld;
                ApplyColor(lockMarker, lockColor);
            }
        }

        private void ApplyColor(Renderer r, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, c);
            mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(mpb);
        }
    }
}
