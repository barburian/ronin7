using Ronin7.Core;
using Ronin7.Combat;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// P3-04 Teleporter (Dream_Ghost_Manifestation). In Chase it picks a flank point
    /// <see cref="FlankDistance"/> m to the player's side, announces it for <see cref="Lead"/> s
    /// (<see cref="EnemyBlinkTelegraph"/> for the positional cue, optional floor glyph), then sets its
    /// transform there with no fade (an enemy teleporting is not camera motion), calls MarkBlinked
    /// (Blink weakness window) and attacks. Alternates sides each blink.
    /// </summary>
    public class BlinkEnemy : Enemy
    {
        public const float FlankDistance = 3f, Lead = 0.5f, Cooldown = 4f;

        [Tooltip("Optional floor glyph, moved to the destination during the lead time.")]
        [SerializeField] private Transform glyph;

        private bool pending;
        private float blinkAt, nextBlinkTime;
        private Vector3 destination;
        private int side = 1;

        /// <summary>Pure: a point <paramref name="distance"/> m to the player's <paramref name="side"/> (+1 right,
        /// -1 left) of its flattened forward, always inside +-(halfExtent - 0.5). If that side is out of bounds
        /// it flips to the other side, then clamps.</summary>
        public static Vector3 BlinkTarget(Vector3 playerPos, Vector3 playerFwd, int side, float distance, float halfExtent)
        {
            playerFwd.y = 0f;
            if (playerFwd.sqrMagnitude < 1e-6f) playerFwd = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, playerFwd.normalized);
            float s = side >= 0 ? 1f : -1f;
            Vector3 p = playerPos + right * (s * distance);
            if (ClampToArena(p, halfExtent) != p) p = playerPos - right * (s * distance);
            return ClampToArena(p, halfExtent);
        }

        /// <summary>Pure (P3-07): a point <paramref name="distance"/> m behind the player (opposite its flattened
        /// forward), clamped inside +-(halfExtent - 0.5).</summary>
        public static Vector3 BehindTarget(Vector3 playerPos, Vector3 playerFwd, float distance, float halfExtent)
        {
            playerFwd.y = 0f;
            if (playerFwd.sqrMagnitude < 1e-6f) playerFwd = Vector3.forward;
            return ClampToArena(playerPos - playerFwd.normalized * distance, halfExtent);
        }

        protected override void Awake()
        {
            base.Awake();
            if (glyph != null) glyph.gameObject.SetActive(false);
        }

        // A stagger/kill during the lead would otherwise leave the glyph up and fire a stale blink later.
        private void CancelBlink()
        {
            pending = false;
            if (glyph != null) glyph.gameObject.SetActive(false);
        }

        protected override void BeforeStateTick()
        {
            base.BeforeStateTick();
            if (pending && state != State.Chase) CancelBlink();
        }

        protected override void OnDied()
        {
            CancelBlink();
            base.OnDied();
        }

        protected override void OnChaseTick()
        {
            // P3-07: only a Blink-weakness definition blinks (Vane's phase 1 is a plain duelist).
            if (Definition.weakness != WeaknessKind.Blink) { base.OnChaseTick(); return; }
            if (pending)
            {
                if (Time.time < blinkAt) return;
                pending = false;
                if (glyph != null) glyph.gameObject.SetActive(false);
                Displace(destination - transform.position, 0f);
                MarkBlinked();
                nextBlinkTime = Time.time + Cooldown;
                BeginAttack();
                return;
            }
            if (Time.time >= nextBlinkTime)
            {
                Camera cam = Camera.main;
                Vector3 fwd = cam != null ? cam.transform.forward : target.transform.forward;
                side = -side;
                destination = Definition.blinkBehind
                    ? BehindTarget(target.transform.position, fwd, FlankDistance, ArenaHalfExtent)
                    : BlinkTarget(target.transform.position, fwd, side, FlankDistance, ArenaHalfExtent);
                pending = true;
                blinkAt = Time.time + Lead;
                if (glyph != null) { glyph.position = new Vector3(destination.x, glyph.position.y, destination.z); glyph.gameObject.SetActive(true); }
                EventBus.Publish(new EnemyBlinkTelegraph(destination, gameObject));
                return;
            }
            base.OnChaseTick();
        }
    }
}
