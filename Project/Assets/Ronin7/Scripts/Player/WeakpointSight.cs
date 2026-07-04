using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Player
{
    /// <summary>
    /// Ch7 ("Forgotten Names") permanent ability: the pacified blade's shadow-AI imparts its
    /// accumulated combat-read to Echo, who overlays enemy weak points on Cipher's optic feed. Self-
    /// disables in <see cref="Awake"/> unless <c>CampaignState.HasAbility(AbilityId.WeakpointSight)</c>
    /// — mirrors the ability-gated pattern <c>EchoPresence</c>/<c>Ronin7.World.Story.AbilityGranter</c>
    /// already use — so it is harmless to place on the rig in every scene, locked or not.
    ///
    /// Toggled by <see cref="toggleAction"/> (wired at build time to a dedicated "Toggle Weakpoint
    /// Sight" action bound to the left controller's primary/X button with a Hold(0.6s) interaction —
    /// left X also carries Crouch as a Tap, so a quick tap crouches and a held press toggles this
    /// ability; read via <c>WasPerformedThisFrame</c> so the Hold interaction is respected). While active: drives the rig's
    /// <see cref="PlayerCombatModifiers.WeakpointMultiplier"/> and shows marker spheres over nearby
    /// enemies, found via <see cref="Health"/> (this assembly doesn't reference Ronin7.Enemies).
    /// Publishes <see cref="AbilityActivated"/> on toggle-ON so <c>EchoPresence</c> reacts, per that
    /// event's documented contract. <see cref="OnDisable"/> restores the multiplier to 1 and hides
    /// markers as a failsafe (scene teardown, chapter transition).
    ///
    /// INPUT SHARING (resolved): Left X hosts both Crouch (Tap) and this ability (Hold 0.6s) as
    /// separate interactions on the same control, mirroring the project's existing tap/hold overloads
    /// (Dash+RecenterCockpit on right A, Hack+Recenter on right B). Tap => crouch, hold => weakpoint.
    ///
    /// Marker geometry is a small pooled set of tinted sphere primitives (greybox, per VR-comfort
    /// rules: no camera motion, visual-only feedback), reused across toggles instead of
    /// instantiate/destroy per press. Re-scanned on an interval rather than every frame to stay cheap.
    /// </summary>
    public class WeakpointSight : MonoBehaviour
    {
        private const int MarkerPoolSize = 8;
        private const float ScanInterval = 0.5f;

        [SerializeField] private InputActionReference toggleAction;
        [SerializeField] private PlayerCombatModifiers combatModifiers;
        [SerializeField] private float activeDamageMultiplier = 2f;
        [SerializeField] private float markerRadius = 15f;
        [SerializeField] private float markerChestHeight = 1.3f;
        [SerializeField] private Color markerColor = new Color(1f, 0.25f, 0.15f, 1f);

        private WeakpointSightState state;
        private readonly List<Transform> markerPool = new List<Transform>();
        private readonly List<Health> targetsInRange = new List<Health>();
        private readonly List<Health> candidateHealth = new List<Health>();
        private readonly List<Vector3> candidatePositions = new List<Vector3>();
        private float nextScanTime;

        private void Awake()
        {
            if (!CampaignState.HasAbility(AbilityId.WeakpointSight))
            {
                enabled = false;
                return;
            }

            if (combatModifiers == null) combatModifiers = GetComponent<PlayerCombatModifiers>();
            state = new WeakpointSightState(activeDamageMultiplier);
            BuildMarkerPool();
        }

        private void OnEnable() => toggleAction?.action?.Enable();

        private void OnDisable()
        {
            toggleAction?.action?.Disable();
            state?.Deactivate();
            ApplyMultiplier();
            HideAllMarkers();
        }

        private void Update()
        {
            var action = toggleAction != null ? toggleAction.action : null;
            if (action != null && action.WasPerformedThisFrame())
            {
                bool active = state.Toggle();
                ApplyMultiplier();
                if (active)
                {
                    nextScanTime = 0f; // force an immediate scan this frame
                    EventBus.Publish(new AbilityActivated(AbilityId.WeakpointSight));
                }
                else
                {
                    HideAllMarkers();
                }
            }

            if (state.IsActive && Time.time >= nextScanTime)
            {
                nextScanTime = Time.time + ScanInterval;
                RefreshMarkers();
            }
        }

        private void ApplyMultiplier()
        {
            if (combatModifiers != null) combatModifiers.WeakpointMultiplier = state != null ? state.DamageMultiplier : 1f;
        }

        private void BuildMarkerPool()
        {
            for (int i = 0; i < MarkerPoolSize; i++)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "WeakpointMarker";
                marker.transform.SetParent(transform, false);
                marker.transform.localScale = Vector3.one * 0.18f;
                var col = marker.GetComponent<Collider>();
                if (col != null) Destroy(col);
                RendererTint.Apply(marker.GetComponent<Renderer>(), markerColor);
                marker.SetActive(false);
                markerPool.Add(marker.transform);
            }
        }

        private void RefreshMarkers()
        {
            var active = Health.Active;
            candidateHealth.Clear();
            candidatePositions.Clear();
            for (int i = 0; i < active.Count; i++)
            {
                var h = active[i];
                if (h == null || !h.IsAlive) continue;
                if (h.transform.root == transform.root) continue; // skip the player's own Health
                candidateHealth.Add(h);
                candidatePositions.Add(h.transform.position);
            }

            var inRange = WeakpointSightMarkers.SelectInRange(transform.position, candidatePositions, markerRadius);
            targetsInRange.Clear();
            for (int i = 0; i < inRange.Count && i < markerPool.Count; i++)
                targetsInRange.Add(candidateHealth[inRange[i]]);

            for (int i = 0; i < markerPool.Count; i++)
            {
                if (i < targetsInRange.Count)
                {
                    markerPool[i].position = targetsInRange[i].transform.position + Vector3.up * markerChestHeight;
                    markerPool[i].gameObject.SetActive(true);
                }
                else
                {
                    markerPool[i].gameObject.SetActive(false);
                }
            }
        }

        private void HideAllMarkers()
        {
            foreach (var m in markerPool)
                if (m != null) m.gameObject.SetActive(false);
        }
    }
}
