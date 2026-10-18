using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>P4-04 S5 Blade Throw and Recall (pure): letting go of the grip while the Weapon Art trigger is
    /// held and the hand moves at least <see cref="MinReleaseSpeed"/> m/s throws the blade; otherwise it
    /// resheathes as usual. Recall flies it back to the hand over <see cref="RecallSeconds"/>.</summary>
    public static class ThrowLogic
    {
        public const float MinReleaseSpeed = 4f, RecallSeconds = 0.35f, FlightSeconds = 0.8f;
        public const float ThrowSpeedScale = 1.5f, MinThrowSpeed = 8f, SpinDegPerSec = 900f;

        public static bool ShouldThrow(bool triggerHeld, float handSpeed) => triggerHeld && handSpeed >= MinReleaseSpeed;

        public static Vector3 ThrowVelocity(Vector3 handVelocity) =>
            handVelocity.normalized * Mathf.Max(handVelocity.magnitude * ThrowSpeedScale, MinThrowSpeed);

        public static Vector3 RecallPosition(Vector3 from, Vector3 hand, float elapsed) =>
            Vector3.Lerp(from, hand, Mathf.Clamp01(elapsed / RecallSeconds));

        public static bool RecallDone(float elapsed) => elapsed >= RecallSeconds;
    }

    /// <summary>
    /// P4-04 S5 Blade Throw and Recall (Weapon Art <see cref="ArtId.BladeThrow"/>, the alternative to S4). The
    /// thrown blade flies kinematically for 0.8 s (spinning, stopping on level geometry), cutting through
    /// enemies with its own <see cref="BladeDamager"/> (which keeps the wielder as Source), then hangs. Squeeze
    /// the right grip on an empty hand to recall it into the hand. Holds <see cref="KatanaHolster.SuppressResheathe"/>
    /// while armed or away.
    /// </summary>
    public class BladeThrowController : MonoBehaviour
    {
        private enum Phase { Idle, Flying, Hanging, Recalling }

        private Phase phase;
        private KatanaHolster holster;
        private Grabbable sword;
        private Rigidbody swordBody;
        private Grabber rightGrabber;
        private HandVelocityTracker rightVelocity;
        private Vector3 flightVelocity, recallFrom;
        private float phaseStart;
        private static readonly RaycastHit[] stepHits = new RaycastHit[4];

        private void OnEnable() => ArtSlots.Register(ArtId.BladeThrow, OnPress);

        private void OnDisable()
        {
            ArtSlots.Unregister(ArtId.BladeThrow);
            if (sword != null) sword.Released -= OnReleased;
            sword = null;
            phase = Phase.Idle;
            if (holster != null) holster.SuppressResheathe = false; // its safety net brings the blade home
        }

        private static void OnPress() { } // the throw itself happens on grip release while the trigger is held

        private bool Resolve()
        {
            if (holster == null) holster = GetComponentInParent<KatanaHolster>();
            if (sword == null && holster != null && holster.Sword != null)
            {
                sword = holster.Sword;
                swordBody = sword.GetComponent<Rigidbody>();
                sword.Released += OnReleased;
            }
            var rig = VRRig.Instance;
            if (rightGrabber == null && rig != null && rig.RightHand != null)
            {
                rightGrabber = rig.RightHand.GetComponent<Grabber>();
                rightVelocity = rig.RightHand.GetComponent<HandVelocityTracker>();
            }
            return sword != null && rightGrabber != null;
        }

        private void Update()
        {
            if (!Resolve()) return;
            float now = Time.time;
            switch (phase)
            {
                case Phase.Idle:
                    holster.SuppressResheathe = ArtSlots.IsEquipped(ArtSlot.Weapon, ArtId.BladeThrow) && rightGrabber.IsHolding
                        && ThrowLogic.ShouldThrow(ArtSlots.WeaponHeld, SkillRig.RealVelocity(rightVelocity).magnitude);
                    break;
                case Phase.Flying:
                    if (sword.IsHeld) { EndThrow(); break; } // caught by a hand
                    if (TryStartRecall(now)) break;
                    Fly(now);
                    break;
                case Phase.Hanging:
                    if (sword.IsHeld) { EndThrow(); break; }
                    TryStartRecall(now);
                    break;
                case Phase.Recalling:
                    Recall(now);
                    break;
            }
        }

        private void OnReleased()
        {
            if (phase != Phase.Idle || !holster.SuppressResheathe) return;
            sword.transform.SetParent(null, true);
            if (swordBody != null) swordBody.isKinematic = true;
            flightVelocity = ThrowLogic.ThrowVelocity(SkillRig.RealVelocity(rightVelocity));
            phase = Phase.Flying;
            phaseStart = Time.time;
            EventBus.Publish(new AbilityActivated(ArtId.BladeThrow));
        }

        private void Fly(float now)
        {
            Vector3 step = flightVelocity * Time.deltaTime;
            Vector3 pos = sword.transform.position;
            float dist = step.magnitude;
            if (dist > 1e-5f)
            {
                int n = Physics.RaycastNonAlloc(pos, step / dist, stepHits, dist, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var c = stepHits[i].collider;
                    // Level geometry stops it; bodies (enemies, the rig) and the blade itself do not.
                    if (c.GetComponentInParent<Health>() != null || c.transform.root == sword.transform.root
                        || c.transform.root == transform.root) continue;
                    sword.transform.position = stepHits[i].point - step / dist * 0.05f;
                    phase = Phase.Hanging;
                    return;
                }
            }
            sword.transform.position = pos + step;
            sword.transform.Rotate(Vector3.right, ThrowLogic.SpinDegPerSec * Time.deltaTime, Space.Self);
            if (now - phaseStart >= ThrowLogic.FlightSeconds) phase = Phase.Hanging;
        }

        private bool TryStartRecall(float now)
        {
            if (!rightGrabber.GripHeld || rightGrabber.IsHolding) return false;
            recallFrom = sword.transform.position;
            phase = Phase.Recalling;
            phaseStart = now;
            Haptics.Pulse(XRNode.RightHand, 0.5f, 0.05f);
            return true;
        }

        private void Recall(float now)
        {
            if (sword.IsHeld) { EndThrow(); return; }
            float t = now - phaseStart;
            Transform hand = rightGrabber.transform;
            sword.transform.position = ThrowLogic.RecallPosition(recallFrom, hand.position, t);
            if (!ThrowLogic.RecallDone(t)) return;
            if (rightGrabber.GripHeld) rightGrabber.GrabDirect(sword); // let go mid-recall = it resheathes instead
            EndThrow();
            Haptics.Pulse(XRNode.RightHand, 0.8f, 0.08f);
        }

        private void EndThrow()
        {
            phase = Phase.Idle;
            holster.SuppressResheathe = false;
        }
    }
}
