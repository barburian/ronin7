using System.Collections.Generic;
using Ronin7.Combat;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// P3-04 Support drone (Dominion_Scan-Drone, Linchpin). Never attacks. Holds one beam target (nearest living
    /// non-support enemy, found via <see cref="Health.Active"/>, re-picked at most once per
    /// <see cref="RetargetInterval"/>) whose <see cref="Health.IncomingDamageMultiplier"/> is halved while
    /// beamed. Flees the player inside <see cref="FleeRange"/>. Its death staggers the beamed ally (Linchpin).
    /// </summary>
    public class SupportDrone : Enemy
    {
        public const float BuffScale = 0.5f, FleeRange = 4f, RetargetInterval = 1f, BackOffset = 2f;

        [Tooltip("Optional beam (2 points, world-space).")]
        [SerializeField] private LineRenderer beam;

        private Health buffed;
        private float nextRetargetTime;

        internal Health Buffed => buffed;

        /// <summary>Nearest living enemy (not a SupportDrone, not <paramref name="self"/>) in the registry,
        /// preferring one no other drone is already beaming (falls back to the nearest if all are beamed).</summary>
        internal static Health PickNearest(IReadOnlyList<Health> all, Health self, Vector3 from)
        {
            Health best = PickNearest(all, self, from, true);
            return best != null ? best : PickNearest(all, self, from, false);
        }

        private static Health PickNearest(IReadOnlyList<Health> all, Health self, Vector3 from, bool skipBeamed)
        {
            Health best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                Health h = all[i];
                if (h == null || h == self || !h.IsAlive || !h.TryGetComponent(out Enemy _) || h.TryGetComponent(out SupportDrone _)) continue;
                if (skipBeamed && IsBeamed(all, h)) continue;
                float d = (h.transform.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = h; }
            }
            return best;
        }

        private static bool IsBeamed(IReadOnlyList<Health> all, Health target)
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].TryGetComponent(out SupportDrone d) && d.buffed == target) return true;
            return false;
        }

        /// <summary>Re-targets when the beam target is gone, at most once per <see cref="RetargetInterval"/>.
        /// Returns true if a new target was picked.</summary>
        internal bool TryRetarget(float now)
        {
            if (buffed != null && buffed.IsAlive) return false;
            if (now < nextRetargetTime) return false;
            nextRetargetTime = now + RetargetInterval;
            ReleaseBuff(false);
            buffed = PickNearest(Health.Active, health, transform.position);
            if (buffed != null) buffed.IncomingDamageMultiplier *= BuffScale;
            return buffed != null;
        }

        internal void ReleaseBuff(bool stagger)
        {
            if (buffed == null) return;
            buffed.IncomingDamageMultiplier /= BuffScale;
            if (stagger && buffed.IsAlive && buffed.TryGetComponent(out MeleeAttacker m)) m.ForceStagger();
            buffed = null;
        }

        protected override void Awake()
        {
            base.Awake();
            if (beam != null) { beam.useWorldSpace = true; beam.positionCount = 2; beam.enabled = false; }
        }

        protected override bool IsInLandHitRange() => false;
        protected override void TryDetectParry() { }

        protected override void OnChaseTick()
        {
            TryRetarget(Time.time);
            Vector3 toPlayer = target.transform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            Vector3 step = Vector3.zero;
            if (dist < FleeRange)
                step = -toPlayer / Mathf.Max(dist, 1e-3f);
            else if (buffed != null)
            {
                // hover behind the ally, relative to the player
                Vector3 back = buffed.transform.position - target.transform.position;
                back.y = 0f;
                Vector3 want = buffed.transform.position + back.normalized * BackOffset - transform.position;
                want.y = 0f;
                if (want.sqrMagnitude > 0.25f) step = want.normalized;
            }
            Hover(step);
        }

        protected override void Update()
        {
            base.Update();
            if (beam == null) return;
            bool on = buffed != null && buffed.IsAlive && state != State.Dead;
            beam.enabled = on;
            if (!on) return;
            beam.SetPosition(0, transform.position + Vector3.up * 0.8f);
            beam.SetPosition(1, buffed.transform.position + Vector3.up);
        }

        protected override void OnDied()
        {
            ReleaseBuff(true);
            if (beam != null) beam.enabled = false;
            base.OnDied();
        }
    }
}
