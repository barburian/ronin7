using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>P4-04 S7 Chain Hook (pure): aim the left hand at an enemy within <see cref="AimRange"/> m, then
    /// yank the hand toward the body at least <see cref="MinYankSpeed"/> m/s to pull the ENEMY up to
    /// <see cref="PullDistance"/> m toward the player over <see cref="PullSeconds"/> s, never closer than
    /// <see cref="Standoff"/> m. The player is never moved.</summary>
    public static class ChainLogic
    {
        public const float AimRange = 8f, AimRadius = 0.2f, MinYankSpeed = 2.5f;
        public const float PullDistance = 3f, PullSeconds = 0.25f, Standoff = 1f;

        /// <summary>Speed of the hand toward the body (horizontal) is at least <see cref="MinYankSpeed"/>.
        /// <paramref name="bodyForward"/> stands in when the hand is right at the body.</summary>
        public static bool IsYank(Vector3 handVelocity, Vector3 hand, Vector3 body, Vector3 bodyForward)
        {
            Vector3 toBody = body - hand;
            toBody.y = 0f;
            if (toBody.sqrMagnitude < 0.01f) { toBody = -bodyForward; toBody.y = 0f; }
            if (toBody.sqrMagnitude < 1e-6f) return false;
            return Vector3.Dot(handVelocity, toBody.normalized) >= MinYankSpeed;
        }

        /// <summary>Horizontal pull for the enemy at <paramref name="enemy"/> toward <paramref name="player"/>.</summary>
        public static Vector3 PullDelta(Vector3 player, Vector3 enemy)
        {
            Vector3 d = player - enemy;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < 1e-4f) return Vector3.zero;
            return d / dist * Mathf.Min(PullDistance, Mathf.Max(0f, dist - Standoff));
        }
    }

    /// <summary>
    /// P4-04 S7 Chain Hook (Off-hand Art <see cref="ArtId.ChainHook"/>). Press: one sphere cast (r 0.2, 8 m)
    /// along the left hand latches the nearest enemy and shows a 2-point <see cref="LineRenderer"/> (builder
    /// wires it with the shared additive VFX material; no line is drawn when it is unset). While the trigger
    /// stays held, a yank pulls the enemy via <see cref="Enemy.Displace"/>, then staggers it when the slide ends
    /// (a stagger cancels a timed slide). Releasing the trigger drops the chain.
    /// </summary>
    public class ChainHookController : MonoBehaviour
    {
        [SerializeField] private LineRenderer chainLine;
        [Tooltip("Not in the roadmap table: stops a stagger-lock by spamming the chain. Tune on device.")]
        [SerializeField] private float cooldownSeconds = 5f;

        private static readonly RaycastHit[] hits = new RaycastHit[8];
        private PlayerCombatModifiers mods;
        private HandVelocityTracker leftVelocity;
        private Enemy latched, pulling;
        private float staggerAt = -1f, readyAt = float.NegativeInfinity;

        private void OnEnable() => ArtSlots.Register(ArtId.ChainHook, Aim);

        private void OnDisable()
        {
            ArtSlots.Unregister(ArtId.ChainHook);
            Drop();
            pulling = null;
        }

        private void Aim()
        {
            var rig = VRRig.Instance;
            if (rig == null || rig.LeftHand == null || Time.time < readyAt) return;
            Transform hand = rig.LeftHand;
            int n = Physics.SphereCastNonAlloc(hand.position, ChainLogic.AimRadius, hand.forward, hits, ChainLogic.AimRange,
                ~0, QueryTriggerInteraction.Ignore);
            Enemy best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var e = hits[i].collider.GetComponentInParent<Enemy>();
                if (e == null || hits[i].distance >= bestDist) continue;
                var h = e.GetComponent<Health>();
                if (h == null || !h.IsAlive) continue;
                best = e;
                bestDist = hits[i].distance;
            }
            if (best == null) return;
            latched = best;
            if (chainLine != null) chainLine.enabled = true;
            Haptics.Pulse(XRNode.LeftHand, 0.4f, 0.05f);
        }

        private void Update()
        {
            if (pulling != null && staggerAt >= 0f && Time.time >= staggerAt)
            {
                if (pulling.TryGetComponent(out Health ph) && ph.IsAlive) pulling.ForceStagger();
                pulling = null;
                staggerAt = -1f;
            }
            if (latched == null) return;
            var rig = VRRig.Instance;
            var health = latched.GetComponent<Health>();
            if (rig == null || !ArtSlots.OffhandHeld || !ArtSlots.IsEquipped(ArtSlot.Offhand, ArtId.ChainHook)
                || health == null || !health.IsAlive) { Drop(); return; }

            Transform hand = rig.LeftHand;
            Vector3 target = latched.transform.position + Vector3.up;
            if (chainLine != null) { chainLine.SetPosition(0, hand.position); chainLine.SetPosition(1, target); }

            if (leftVelocity == null) leftVelocity = hand.GetComponent<HandVelocityTracker>();
            Transform head = rig.Head != null ? rig.Head : transform;
            if (!ChainLogic.IsYank(SkillRig.RealVelocity(leftVelocity), hand.position, head.position, head.forward)) return;

            latched.Displace(ChainLogic.PullDelta(head.position, latched.transform.position), ChainLogic.PullSeconds);
            pulling = latched;
            staggerAt = Time.time + ChainLogic.PullSeconds;
            if (mods == null) mods = GetComponentInParent<PlayerCombatModifiers>();
            readyAt = Time.time + cooldownSeconds * SkillRig.CooldownScale(mods);
            Haptics.Pulse(XRNode.LeftHand, 1f, 0.12f);
            EventBus.Publish(new AbilityActivated(ArtId.ChainHook));
            Drop();
        }

        private void Drop()
        {
            latched = null;
            if (chainLine != null) chainLine.enabled = false;
        }
    }
}
