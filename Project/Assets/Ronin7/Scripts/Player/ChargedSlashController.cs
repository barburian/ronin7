using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>P4-04 S4 Charged Slash (pure): hold the Weapon Art trigger <see cref="HoldSeconds"/>, then release
    /// it during a swing of at least <see cref="MinSwingSpeed"/> m/s to fire. <see cref="Cooldown"/> s after a
    /// fire (AbilityHaste scales it). An early or slow release wastes the charge but costs no cooldown.</summary>
    public class ChargeLogic
    {
        public const float HoldSeconds = 0.8f, MinSwingSpeed = 5f, Cooldown = 6f;
        public const float DamageFraction = 0.6f, Radius = 0.4f, Range = 6f;

        private float holdStart = -1f;
        private float readyAt = float.NegativeInfinity;

        public bool Charging => holdStart >= 0f;

        public float Charge01(float now) => Charging ? Mathf.Clamp01((now - holdStart) / HoldSeconds) : 0f;

        public void Begin(float now)
        {
            if (!Charging && now >= readyAt) holdStart = now;
        }

        /// <summary>Trigger released: true when the slash fires.</summary>
        public bool Release(float now, float swingSpeed, float cooldownScale)
        {
            if (!Charging) return false;
            bool full = now - holdStart >= HoldSeconds;
            holdStart = -1f;
            if (!full || swingSpeed < MinSwingSpeed) return false;
            readyAt = now + Cooldown * cooldownScale;
            return true;
        }

        /// <summary>Cast direction: where the player looks, laid into the swing plane (blade axis x velocity).</summary>
        public static Vector3 SlashDirection(Vector3 headForward, Vector3 bladeAxis, Vector3 bladeVelocity)
        {
            Vector3 normal = Vector3.Cross(bladeAxis, bladeVelocity);
            Vector3 d = normal.sqrMagnitude > 1e-6f ? Vector3.ProjectOnPlane(headForward, normal.normalized) : headForward;
            return d.sqrMagnitude > 1e-6f ? d.normalized : headForward.normalized;
        }
    }

    /// <summary>
    /// P4-04 S4 Charged Slash (Weapon Art <see cref="ArtId.ChargedSlash"/>). Press arms the charge (haptic ramp
    /// on the sword hand); releasing during a fast swing fires one <c>SphereCastNonAlloc</c> (r 0.4, 6 m)
    /// dealing 60% of the swing's damage to each enemy along it. No VFX yet (P5 crescent mesh).
    /// </summary>
    public class ChargedSlashController : MonoBehaviour
    {
        private const int MaxHits = 8;
        private const float RampPulseInterval = 0.05f;

        private readonly ChargeLogic logic = new ChargeLogic();
        private static readonly RaycastHit[] hits = new RaycastHit[MaxHits];
        private readonly Health[] struck = new Health[MaxHits];
        private PlayerCombatModifiers mods;
        private KatanaHolster holster;
        private BladeDamager blade;
        private float nextPulse;

        private void OnEnable() => ArtSlots.Register(ArtId.ChargedSlash, OnPress);
        private void OnDisable() => ArtSlots.Unregister(ArtId.ChargedSlash);

        private void OnPress() => logic.Begin(Time.unscaledTime);

        private void Update()
        {
            if (!logic.Charging) return;
            float now = Time.unscaledTime;
            if (ArtSlots.WeaponHeld && ArtSlots.IsEquipped(ArtSlot.Weapon, ArtId.ChargedSlash))
            {
                if (now >= nextPulse)
                {
                    nextPulse = now + RampPulseInterval;
                    Haptics.Pulse(XRNode.RightHand, 0.1f + 0.5f * logic.Charge01(now), RampPulseInterval);
                }
                return;
            }
            if (!Resolve()) { logic.Release(now, 0f, 1f); return; }
            if (logic.Release(now, blade.Speed, SkillRig.CooldownScale(mods))) Fire();
        }

        private bool Resolve()
        {
            if (mods == null) mods = GetComponentInParent<PlayerCombatModifiers>();
            if (holster == null) holster = GetComponentInParent<KatanaHolster>();
            if (blade == null && holster != null && holster.Sword != null)
                blade = holster.Sword.GetComponentInChildren<BladeDamager>(true);
            return blade != null;
        }

        private void Fire()
        {
            blade.WorldEndpoints(out Vector3 bladeBase, out Vector3 tip);
            Transform head = VRRig.Instance != null && VRRig.Instance.Head != null ? VRRig.Instance.Head : transform;
            Vector3 dir = ChargeLogic.SlashDirection(head.forward, tip - bladeBase, blade.Velocity);
            float dmg = ChargeLogic.DamageFraction * blade.SwingDamageNow();
            Haptics.Pulse(XRNode.RightHand, 1f, 0.15f);
            if (dmg <= 0f) return;

            int n = Physics.SphereCastNonAlloc(tip, ChargeLogic.Radius, dir, hits, ChargeLogic.Range, ~0, QueryTriggerInteraction.Collide);
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i].collider.GetComponentInParent<Health>();
                if (h == null || !h.IsAlive || h.transform.root == transform.root || Contains(count, h)) continue;
                struck[count++] = h;
                Vector3 point = hits[i].distance > 0f ? hits[i].point : h.transform.position;
                h.ApplyDamage(new DamageInfo(dmg, point, dir, transform.root.gameObject, DamageType.Melee, blade.Speed));
                EventBus.Publish(new SwordImpact(point, blade.Speed, h.gameObject, blade.Velocity));
            }
            System.Array.Clear(struck, 0, count);
        }

        private bool Contains(int count, Health h)
        {
            for (int i = 0; i < count; i++) if (struck[i] == h) return true;
            return false;
        }
    }
}
