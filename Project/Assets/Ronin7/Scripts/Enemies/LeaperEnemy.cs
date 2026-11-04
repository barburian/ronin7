using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// P8-06 Leaper. Waits at 6-8 m, then telegraphs a leap: it crouches (claws raised, torso hunched forward) and
    /// growls (the heavy windup cue) for <see cref="LeapTelegraph"/>, then jumps a scripted parabola that lands at
    /// strike range in FRONT of the player (never on or over them), strikes, and skitters back out to leap range.
    /// Rushed inside 6 m it backs off on a cooldown; within strike range it fights. No physics, no root motion:
    /// the arc is a lerp plus a parabola on the spawn-height latch. Natural claws (skips the kit roll).
    /// </summary>
    public class LeaperEnemy : Enemy
    {
        public const float LeapMin = 6f, LeapMax = 8f, LeapTelegraph = 0.7f, LeapAirtime = 0.6f, LeapApex = 1.1f;
        public const float LeapCooldown = 1.5f, RetreatSeconds = 1.1f, RetreatCooldown = 3f;
        private const float LeapMid = (LeapMin + LeapMax) * 0.5f, CrouchIn = 0.25f, LeapCuePitch = 0.75f;
        // Crouch: claws raised overhead and out (MeleeAttacker pose space: x swing, negative = overhead; z roll out)
        // with the torso hunched forward (negative body draw = forward lean). Reads by silhouette, no colour.
        private static readonly Vector3 CrouchArms = new Vector3(-150f, 0f, 40f);
        private const float CrouchDraw = -2.5f; // ~37 deg forward with RL_Leaper lean 15

        internal enum Move { Base, Leap, Retreat }
        internal enum Phase { None, Telegraph, Air }
        internal Phase LeapPhase { get; private set; }
        internal float PhaseEnd { get; set; }
        internal float NextLeap { get; set; }
        internal bool RetreatPending { get; private set; }
        internal Vector3 RetreatTarget { get; private set; }
        internal bool Retreating => Time.time < retreatUntil;
        private float nextRetreat, retreatUntil, leapY;
        private Vector3 leapFrom, leapTo;

        protected internal override float HoldRange => LeapMid; // the base walk stops in leap range

        /// <summary>Pure: what to do at <paramref name="dist"/> m from the player (Base = walk in / strike in range).</summary>
        internal static Move Decide(float dist, float attackRange, bool leapReady, bool retreatReady)
        {
            if (dist >= LeapMin && dist <= LeapMax) return leapReady ? Move.Leap : Move.Base;
            if (dist < LeapMin && dist > attackRange && retreatReady) return Move.Retreat;
            return Move.Base;
        }

        /// <summary>Pure: the landing point, <paramref name="strikeRange"/> from the player on the leaper's side.</summary>
        public static Vector3 LeapLanding(Vector3 from, Vector3 player, float strikeRange)
        {
            Vector3 d = from - player; d.y = 0f;
            Vector3 dir = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
            Vector3 p = player + dir * strikeRange;
            p.y = from.y;
            return p;
        }

        /// <summary>Pure: the scripted leap arc at <paramref name="t"/> 0..1 (straight line plus a parabola).</summary>
        public static Vector3 LeapArc(Vector3 from, Vector3 to, float t, float apex)
            => Vector3.Lerp(from, to, t) + Vector3.up * (4f * apex * t * (1f - t));

        protected override void OnChaseTick()
        {
            if (LeapPhase != Phase.None) { TickLeap(); return; }
            if (Retreating) return; // skittering back: Enemy.Update moves it
            Vector3 to = target.transform.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            if (RetreatPending && dist < LeapMin) { Retreat(to, dist); return; }
            RetreatPending = false;
            switch (Decide(dist, Definition.attackRange, Time.time >= NextLeap, Time.time >= nextRetreat))
            {
                case Move.Leap: StartLeap(); return;
                case Move.Retreat: Retreat(to, dist); return;
            }
            base.OnChaseTick();
        }

        private void StartLeap()
        {
            LeapPhase = Phase.Telegraph;
            PhaseEnd = Time.time + LeapTelegraph;
            leapY = transform.position.y;
            // The growl: the heavy windup cue (spatial sound + hand haptic tick), so the leap reads without colour.
            EventBus.Publish(new AttackWindupStarted(transform.position + Vector3.up, gameObject, LeapCuePitch, true));
        }

        private void TickLeap()
        {
            float now = Time.time;
            if (LeapPhase == Phase.Telegraph)
            {
                Crouch(Mathf.SmoothStep(0f, 1f, (LeapTelegraph - (PhaseEnd - now)) / CrouchIn));
                if (now < PhaseEnd) return;
                leapFrom = transform.position;
                leapTo = LeapLanding(leapFrom, target.transform.position, Definition.attackRange); // locked at take-off: sidestep it
                LeapPhase = Phase.Air;
                PhaseEnd = now + LeapAirtime;
                return;
            }
            float t = Mathf.Clamp01(1f - (PhaseEnd - now) / LeapAirtime);
            transform.position = LeapArc(leapFrom, leapTo, t, LeapApex);
            Crouch(1f - t); // springs open in the air, lands at rest so the strike windup starts clean
            if (t < 1f) return;
            EndLeap();
            BeginAttack(); // the strike: its own telegraph, parryable like any claw swipe
        }

        private void Crouch(float k)
        {
            PoseWeapon(restEuler, CrouchArms, k);
            PoseBody(0f, CrouchDraw, k);
        }

        /// <summary>Lands (back on the spawn-height latch) and starts the cooldown; also the abort path.</summary>
        private void EndLeap()
        {
            if (LeapPhase == Phase.None) return;
            Vector3 p = transform.position; p.y = leapY; transform.position = p;
            LeapPhase = Phase.None;
            NextLeap = Time.time + LeapCooldown;
        }

        private void Retreat(Vector3 to, float dist)
        {
            RetreatPending = false;
            Vector3 away = dist > 1e-3f ? -to / dist : -transform.forward;
            RetreatTarget = target.transform.position + away * LeapMid;
            Displace(RetreatTarget - transform.position, RetreatSeconds); // arena-clamped; a stagger cancels it
            retreatUntil = Time.time + RetreatSeconds;
            nextRetreat = Time.time + RetreatCooldown;
        }

        protected override void OnActiveEnd()
        {
            bool combo = ComboPending;
            base.OnActiveEnd();
            if (!combo) RetreatPending = true; // after the last swipe: back out to leap range
        }

        protected override void BeforeStateTick()
        {
            base.BeforeStateTick();
            if (LeapPhase != Phase.None && state != State.Chase) EndLeap(); // staggered mid-leap: drop to the ground
        }

        protected override void OnDied()
        {
            EndLeap();
            base.OnDied();
        }

        /// <summary>Editor capture hook: hold the full leap crouch.</summary>
        public void HoldLeapCrouch() => Crouch(1f);

        public override void Respawn(Vector3 position)
        {
            LeapPhase = Phase.None;
            base.Respawn(position);
            RetreatPending = false;
            NextLeap = nextRetreat = retreatUntil = 0f;
        }
    }
}
