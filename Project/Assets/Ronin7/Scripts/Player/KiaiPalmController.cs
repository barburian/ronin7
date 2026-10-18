using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>P4-04 S6 Kiai Palm (pure): left trigger held and the empty left hand moving at least
    /// <see cref="MinSpeed"/> m/s releases a palm burst: <see cref="Posture"/> posture and a
    /// <see cref="Shove"/> m shove to every enemy within <see cref="Radius"/> m of the hand. <see cref="Cooldown"/> s
    /// (AbilityHaste scales it).</summary>
    public class KiaiLogic
    {
        public const float MinSpeed = 4f, Radius = 2.5f, Posture = 25f, Shove = 0.4f, Cooldown = 8f;

        private float readyAt = float.NegativeInfinity;

        public bool TryFire(float handSpeed, float now, float cooldownScale)
        {
            if (handSpeed < MinSpeed || now < readyAt) return false;
            readyAt = now + Cooldown * cooldownScale;
            return true;
        }

        public static bool InRadius(Vector3 hand, Vector3 target) => (target - hand).sqrMagnitude <= Radius * Radius;

        /// <summary>Horizontal push away from the hand, <see cref="Shove"/> m long.</summary>
        public static Vector3 ShoveDelta(Vector3 hand, Vector3 target)
        {
            Vector3 d = target - hand;
            d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d.normalized * Shove : Vector3.zero;
        }
    }

    /// <summary>
    /// P4-04 S6 Kiai Palm (Off-hand Art <see cref="ArtId.KiaiPalm"/>). Iterates <see cref="Health.Active"/>
    /// once per fire (never per frame), adds posture via <see cref="PostureMeter.AddPosture"/>, shoves through
    /// <see cref="Enemy.Displace"/> and staggers any enemy left near breaking (the break itself staggers).
    /// </summary>
    public class KiaiPalmController : MonoBehaviour
    {
        private const float NearBreakFraction = 0.8f;

        private readonly KiaiLogic logic = new KiaiLogic();
        private PlayerCombatModifiers mods;
        private HandVelocityTracker leftVelocity;

        private void OnEnable() => ArtSlots.Register(ArtId.KiaiPalm, TryPalm);
        private void OnDisable() => ArtSlots.Unregister(ArtId.KiaiPalm);

        private void Update()
        {
            if (ArtSlots.OffhandHeld && ArtSlots.IsEquipped(ArtSlot.Offhand, ArtId.KiaiPalm)) TryPalm();
        }

        private void TryPalm()
        {
            var rig = VRRig.Instance;
            if (rig == null || rig.LeftHand == null) return;
            if (leftVelocity == null) leftVelocity = rig.LeftHand.GetComponent<HandVelocityTracker>();
            if (mods == null) mods = GetComponentInParent<PlayerCombatModifiers>();
            if (!logic.TryFire(SkillRig.RealVelocity(leftVelocity).magnitude, Time.time, SkillRig.CooldownScale(mods))) return;

            Vector3 hand = rig.LeftHand.position;
            GameObject source = transform.root.gameObject;
            var all = Health.Active;
            for (int i = all.Count - 1; i >= 0; i--) // backwards: a break can kill and unregister
            {
                if (i >= all.Count) continue;
                var h = all[i];
                if (h == null || !h.IsAlive || h.transform.root == transform.root) continue;
                if (!KiaiLogic.InRadius(hand, h.transform.position) || !h.TryGetComponent(out Enemy enemy)) continue;
                enemy.Displace(KiaiLogic.ShoveDelta(hand, h.transform.position), 0f);
                if (h.TryGetComponent(out PostureMeter meter))
                {
                    meter.AddPosture(KiaiLogic.Posture, source);
                    if (h.IsAlive && meter.Fraction >= NearBreakFraction) enemy.ForceStagger();
                }
            }
            Haptics.Pulse(XRNode.LeftHand, 1f, 0.15f);
            EventBus.Publish(new AbilityActivated(ArtId.KiaiPalm));
        }
    }
}
