using UnityEngine;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Chapter-agnostic "walkable memory dive": activates a self-contained geometry island
    /// (<see cref="diveRoot"/>), teleports the player rig to <see cref="diveEntryPoint"/> on entry, and
    /// can reset the rig back to that entry point (wired to a <see cref="RedactionSentinel"/>'s
    /// onSpotted callback: being seen resets you to the dive entry, it does not fail the mission) or
    /// deactivate the dive on exit. First used by Ch03 (the Kethel-7 playback); reusable by any later
    /// chapter's memory-space beat.
    ///
    /// <see cref="flashback"/> is optional: when wired, its fog/ambient treatment is (re)applied on
    /// every <see cref="EnterDive"/> so the dive's mood always reasserts itself even if something else
    /// touched RenderSettings in between, and the pre-dive fog/ambient values are captured and
    /// restored on <see cref="ExitDive"/> — the "color floods back" beat. When it is not wired,
    /// RenderSettings are never touched (which also keeps EditMode tests hermetic).
    ///
    /// The rig teleport target is a serialized <see cref="rigRoot"/>, wired by the scene builder from
    /// the player rig's own root transform (the same transform ContinuousLocomotion/ZoneBounds move) —
    /// Ronin7.World does not reference Ronin7.Player, so this can't resolve VRRig.Instance itself.
    /// Comfort-safe: an instant position/rotation set, no lerp, no camera motion.
    /// </summary>
    public class MemoryDiveController : MonoBehaviour
    {
        [Tooltip("The memory-space geometry root. Inactive until EnterDive() activates it.")]
        [SerializeField] private GameObject diveRoot;
        [Tooltip("Respawn/reset anchor inside the dive.")]
        [SerializeField] private Transform diveEntryPoint;
        [Tooltip("Optional: where the rig returns on ExitDive (e.g. the playback seat in the real " +
                 "scene). Without it the rig would be left standing in the deactivated memory-space " +
                 "coordinates, over nothing.")]
        [SerializeField] private Transform diveExitPoint;
        [Tooltip("The player rig root to teleport (its XR Origin transform). Wired by the scene builder.")]
        [SerializeField] private Transform rigRoot;
        [Tooltip("Optional: reapplies MemoryFlashbackController's fog/ambient treatment on EnterDive.")]
        [SerializeField] private MemoryFlashbackController flashback;

        // Pre-dive RenderSettings snapshot, captured only when flashback is wired.
        private bool hasPreDiveSettings;
        private bool preDiveFog;
        private FogMode preDiveFogMode;
        private Color preDiveFogColor;
        private float preDiveFogDensity;
        private UnityEngine.Rendering.AmbientMode preDiveAmbientMode;
        private Color preDiveAmbientLight;

        /// <summary>True while the dive geometry is active.</summary>
        public bool IsActive => diveRoot != null && diveRoot.activeSelf;

        /// <summary>Activates the dive geometry, (re)applies the flashback treatment if wired
        /// (snapshotting the current fog/ambient first), and teleports the rig to the entry point.</summary>
        public void EnterDive()
        {
            // Snapshot BEFORE activating diveRoot: if the flashback component lives under the dive
            // geometry, its Awake() applies the treatment during SetActive(true) — capturing after
            // that would snapshot the already-greyed settings and ExitDive could never restore.
            if (flashback != null && !hasPreDiveSettings)
            {
                hasPreDiveSettings = true;
                preDiveFog = RenderSettings.fog;
                preDiveFogMode = RenderSettings.fogMode;
                preDiveFogColor = RenderSettings.fogColor;
                preDiveFogDensity = RenderSettings.fogDensity;
                preDiveAmbientMode = RenderSettings.ambientMode;
                preDiveAmbientLight = RenderSettings.ambientLight;
            }
            if (diveRoot != null) diveRoot.SetActive(true);
            if (flashback != null) flashback.ApplyTreatment();
            TeleportRig(diveEntryPoint);
        }

        /// <summary>Teleports the rig back to the entry point without touching dive/flashback state.</summary>
        public void ResetToEntry() => TeleportRig(diveEntryPoint);

        /// <summary>Deactivates the dive geometry, teleports the rig to <see cref="diveExitPoint"/>
        /// (when wired), and restores the pre-dive fog/ambient (if one was captured) — the color
        /// flooding back into the real scene.</summary>
        public void ExitDive()
        {
            if (diveRoot != null) diveRoot.SetActive(false);
            TeleportRig(diveExitPoint);
            if (hasPreDiveSettings)
            {
                hasPreDiveSettings = false;
                RenderSettings.fog = preDiveFog;
                RenderSettings.fogMode = preDiveFogMode;
                RenderSettings.fogColor = preDiveFogColor;
                RenderSettings.fogDensity = preDiveFogDensity;
                RenderSettings.ambientMode = preDiveAmbientMode;
                RenderSettings.ambientLight = preDiveAmbientLight;
            }
        }

        private void TeleportRig(Transform target)
        {
            if (rigRoot == null || target == null) return;

            // CharacterController overrides direct transform writes while enabled (same idiom as
            // ZoneBounds' fall-reset teleport) — toggle it around the position set.
            var controller = rigRoot.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            rigRoot.SetPositionAndRotation(target.position, target.rotation);
            if (controller != null) controller.enabled = true;
        }
    }
}
