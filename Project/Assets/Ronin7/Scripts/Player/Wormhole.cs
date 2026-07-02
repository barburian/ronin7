using System.Collections;
using Ronin7.Core;
using Ronin7.Ship;
using UnityEngine;

// Lives in the Player assembly (not Ship) because it drives ScreenFader, which is a Player-side
// component — and Player already references Ship, so Ship cannot reference Player back (cycle).
// This mirrors how LandingApproach stays in Ship by only publishing an EventBus event for the fade.
namespace Ronin7.Player
{
    /// <summary>
    /// A paired wormhole that instantly relocates the player's virtual ship to its <see cref="exit"/>
    /// elsewhere in the SAME scene (no scene load), with a comfort fade-to-black across the jump.
    ///
    /// Geometry mirrors <see cref="LandingApproach"/>: the rig never moves — the world (under
    /// <see cref="universe"/>) moves as the inverse of the virtual ship pose, so a wormhole at
    /// universe-local position P sits a distance |P - shipPos| from the player. We capture this
    /// wormhole's fixed universe-local position at Start (it's a child of universe and won't move
    /// within it) and each frame measure it against <see cref="ShipController.ShipPosition"/>.
    ///
    /// Arrival can't bounce straight back: the jumper drops the player <see cref="exitOffset"/> units
    /// PAST the exit (along the ship's forward), which is set larger than the exit's
    /// <see cref="triggerRadius"/>, so it spawns outside the exit's trigger; a short re-entry cooldown
    /// armed on BOTH ends is the belt-and-braces backup. A shared static guard stops either wormhole
    /// double-firing while a jump is mid-fade.
    /// </summary>
    public class Wormhole : MonoBehaviour
    {
        [Header("Pairing")]
        [Tooltip("The paired destination wormhole. Entering THIS one drops the player just past the exit.")]
        [SerializeField] private Wormhole exit;

        [Header("Refs")]
        [Tooltip("The moving-world root this wormhole lives under (same one the ShipController drives). " +
                 "Falls back to ShipController.Instance.Universe if left empty.")]
        [SerializeField] private Transform universe;
        [Tooltip("The virtual ship to relocate. Falls back to ShipController.Instance if left empty.")]
        [SerializeField] private ShipController ship;

        [Header("Tuning")]
        [Tooltip("How close (universe units) the ship must be to trigger the jump.")]
        [SerializeField] private float triggerRadius = 25f;
        [Tooltip("How far PAST the exit (universe units, along the ship's forward) the player emerges. " +
                 "MUST be larger than the exit's triggerRadius so arrival lands outside its trigger.")]
        [SerializeField] private float exitOffset = 60f;
        [Tooltip("Seconds after a jump during which this wormhole won't re-trigger (armed on both ends).")]
        [SerializeField] private float reentryCooldown = 1.5f;

        // Captured at Start: this wormhole's fixed position in universe-local space (the frame
        // ShipPosition lives in). InverseTransformPoint is robust to any nesting under universe.
        private Vector3 universePos;

        // Time.time until which this wormhole is dormant (set by ArmCooldown on both ends of a jump).
        private float cooldownUntil;

        // One jump at a time across ALL wormholes: stops the exit firing mid-fade and stops two
        // overlapping wormholes both starting jumps in the same frame.
        private static bool jumpInProgress;

        /// <summary>Suppress triggering for <paramref name="seconds"/> (armed on both ends of a jump
        /// so arriving at the exit doesn't immediately bounce the player back).</summary>
        public void ArmCooldown(float seconds) => cooldownUntil = Time.time + seconds;

        private void Start()
        {
            // A fresh scene load means no jump is in flight; clear the static in case a previous
            // jump was cut short by a scene change mid-fade (the static survives "no domain reload").
            jumpInProgress = false;

            if (universe == null && ShipController.Instance != null) universe = ShipController.Instance.Universe;
            if (ship == null) ship = ShipController.Instance;

            if (exit == null) Debug.LogError("[Wormhole] No exit assigned — this wormhole can't jump.", this);
            if (universe == null) Debug.LogError("[Wormhole] No universe transform.", this);

            universePos = universe != null
                ? universe.InverseTransformPoint(transform.position)
                : transform.position;
        }

        private void Update()
        {
            // A gentle idle spin so the greybox reads as a portal (art is Phase 4).
            transform.Rotate(0f, 30f * Time.deltaTime, 0f, Space.Self);

            if (jumpInProgress || Time.time < cooldownUntil) return;
            if (ship == null || exit == null) return;

            if (Vector3.Distance(ship.ShipPosition, universePos) <= triggerRadius)
                StartCoroutine(Jump());
        }

        private IEnumerator Jump()
        {
            jumpInProgress = true;

            var fader = ScreenFader.Ensure();
            if (fader != null) yield return fader.FadeOut();

            // Emerge just past the exit, flying the way the player is facing, so the landing point
            // sits outside the exit's trigger radius (exitOffset > exit.triggerRadius).
            Vector3 landingPos = exit.universePos + ship.ShipRotation * Vector3.forward * exitOffset;
            ship.Teleport(landingPos);

            // Belt-and-braces: keep BOTH ends dormant briefly so the fresh arrival can't re-trigger.
            ArmCooldown(reentryCooldown);
            exit.ArmCooldown(reentryCooldown);

            EventBus.Publish(new WormholeTraversed(landingPos));

            if (fader != null) yield return fader.FadeIn();

            jumpInProgress = false;
        }
    }
}
