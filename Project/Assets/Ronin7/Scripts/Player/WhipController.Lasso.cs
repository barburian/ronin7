using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// A15.9 lasso, gesture only: spin and throw (<see cref="LassoGesture"/>) grabs the non-elite, non-boss enemy nearest the aim (<see cref="LassoOrbit"/>). A held
    /// <see cref="MeleeAttacker.ForceStagger(float)"/> stops its own movement (enemies only move in Chase, by transform); this writes its position
    /// and nothing else, so the stagger ending is the whole restore. Release of the grip or <see cref="LassoSeconds"/> flings it. Runs on real time and freezes
    /// while paused. Health is untouched: a captive still counts as a live enemy and the room clears through its death as usual.
    /// A miss costs nothing: no cooldown, only the next spin.
    /// </summary>
    public partial class WhipController
    {
        internal const float LassoSeconds = 4f, LassoCooldown = 8f; // A15.9
        internal const float CollisionDamage = 30f; // A15.9, to the enemy hit and to the captive
        /// <summary>Radius and height above the feet of the captive's one contact check per frame.</summary>
        internal const float LassoHitRadius = 0.7f, LassoHitHeight = 1f;
        /// <summary>A15.9 "once per enemy per swing": one orbit at <see cref="LassoOrbit.MaxAngularSpeed"/>, so a lap cannot hit one enemy every <see cref="ReHitCooldown"/>.</summary>
        internal const float LassoReHitSeconds = 1.6f;
        /// <summary>Stagger left on the body once it lands or is dropped.</summary>
        internal const float LandStaggerSeconds = 1f;
        private const float HoldStaggerSeconds = LassoSeconds + LassoOrbit.FlingSeconds + LandStaggerSeconds;
        private const float GrabHaptic = 0.8f, GrabHapticSeconds = 0.12f;
        private const float FlingHaptic = 1f, FlingHapticSeconds = 0.15f;

        private enum LassoPhase { None, Orbit, Fling }

        private readonly LassoGesture lassoGesture = new LassoGesture();
        private LassoPhase lassoPhase;
        private Enemy captive;
        private Health captiveHealth, rigHealth;
        private Transform lassoRig, lassoHead;
        private Vector3 flingStart, flingVelocity;
        private float lassoLeft, lassoCooldownLeft, flingTime, orbitAngle;

        internal bool LassoBusy => lassoPhase != LassoPhase.None;
        internal bool LassoOrbiting => lassoPhase == LassoPhase.Orbit;
        internal bool LassoFlinging => lassoPhase == LassoPhase.Fling;
        internal Enemy Captive => captive;
        internal float LassoCooldownLeft => lassoCooldownLeft;

        internal void StepUnheld(float realDt)
        {
            clock += realDt;
            TickLasso(realDt);
        }

        internal void TickLassoCooldown(float realDt)
        {
            if (Time.timeScale > 0f && lassoCooldownLeft > 0f) lassoCooldownLeft = Mathf.Max(0f, lassoCooldownLeft - realDt);
        }

        /// <summary>One real-time lasso frame. Frozen while paused: nothing drains, nothing moves.</summary>
        private void TickLasso(float realDt)
        {
            if (Time.timeScale <= 0f) { lassoGesture.Reset(); return; } // the hand moves during a pause: the old sample would read as a huge velocity
            if (realDt <= 0f) return;
            TickLassoCooldown(realDt);
            if (lassoPhase == LassoPhase.Fling) { TickFling(realDt); return; }

            Transform rig = lassoPhase == LassoPhase.None ? transform.root : lassoRig;
            Transform head = HeadOf(rig);
            if (guard == null) guard = GetComponentInChildren<BladeDamager>(true);
            bool tracked = held && head != null && (guard == null || guard.enabled); // tracking loss: the hand pose jumps
            bool thrown = false;
            if (tracked) thrown = lassoGesture.Sample(rig.InverseTransformPoint(chainRing.position), rig.InverseTransformPoint(head.position), rig.InverseTransformDirection(head.forward), realDt);
            else lassoGesture.Reset();

            if (lassoPhase == LassoPhase.Orbit) TickOrbit(realDt, rig, head);
            else if (thrown)
            {
                // Aim between the flick and the head: a real flick's direction is noisy, the eyes are on the target.
                TryBeginLasso(head.position, rig.TransformDirection(lassoGesture.ThrowDirection) + head.forward);
            }
        }

        private Transform HeadOf(Transform rig)
        {
            if (lassoHead != null) return lassoHead;
            VRRig vr = VRRig.Instance;
            if (vr == null) rig.TryGetComponent(out vr);
            lassoHead = vr != null ? vr.Head : null;
            return lassoHead;
        }

        /// <summary>Grabs the valid enemy nearest <paramref name="aim"/>. False (nothing changes) if none, busy, or cooling down.</summary>
        internal bool TryBeginLasso(Vector3 origin, Vector3 aim)
        {
            if (LassoBusy || lassoCooldownLeft > 0f) return false;
            Transform rig = transform.root;
            Enemy best = null;
            Health bestHealth = null;
            float bestAngle = float.MaxValue;
            for (int i = 0; i < Health.Active.Count; i++) // only on a throw, never per frame
            {
                Health h = Health.Active[i];
                if (h == null || !h.IsAlive || h.transform.root == rig || !h.TryGetComponent(out Enemy e) || e.IsEliteOrBoss) continue;
                if (!LassoOrbit.InAim(origin, aim, h.transform.position, out float angle) || angle >= bestAngle) continue;
                best = e;
                bestHealth = h;
                bestAngle = angle;
            }
            if (best == null) return false;

            captive = best;
            captiveHealth = bestHealth;
            lassoRig = rig;
            rigHealth = rig.GetComponent<Health>();
            Vector3 to = best.transform.position - origin;
            orbitAngle = Mathf.Atan2(to.x, to.z);
            lassoLeft = LassoSeconds;
            lassoPhase = LassoPhase.Orbit;
            lastHit.Clear();
            captive.ForceStagger(HoldStaggerSeconds);
            Pulse(GrabHaptic, GrabHapticSeconds);
            return true;
        }

        private void TickOrbit(float realDt, Transform rig, Transform head)
        {
            if (!HoldCaptive()) return;
            Vector3 headPos = head != null ? head.position : rig.position;
            Transform body = captive.transform;
            Vector3 from = body.position;
            float angular = LassoOrbit.AngularSpeed(rig.TransformDirection(lassoGesture.RotationRate), rig.TransformDirection(lassoGesture.Velocity), headPos, from);
            orbitAngle += angular * realDt;
            Vector3 target = LassoOrbit.RingPoint(headPos, orbitAngle, angular, captive.GroundY);
            Vector3 next = LassoOrbit.Step(from, target, headPos, captive.ArenaHalfExtent, realDt);
            body.position = next;
            Vector3 center = next + Vector3.up * LassoHitHeight;
            chain.PinTip(center);

            if (!Collide(center, (next - from) / realDt)) return;
            lassoLeft -= realDt;
            if (lassoLeft <= 0f) BeginFling();
        }

        private void BeginFling()
        {
            flingStart = captive.transform.position;
            flingVelocity = LassoOrbit.FlingVelocity(lassoRig.TransformDirection(lassoGesture.Velocity));
            flingTime = 0f;
            lassoPhase = LassoPhase.Fling;
            lassoCooldownLeft = LassoCooldown;
            lassoGesture.Reset();
            chain.UnpinTip();
            Pulse(FlingHaptic, FlingHapticSeconds);
        }

        private void TickFling(float realDt)
        {
            if (!HoldCaptive()) return;
            Transform head = HeadOf(lassoRig);
            Vector3 headPos = head != null ? head.position : lassoRig.position;
            Transform body = captive.transform;
            Vector3 from = body.position;
            flingTime += realDt;
            Vector3 next = LassoOrbit.Step(from, LassoOrbit.FlingPoint(flingStart, flingVelocity, captive.GroundY, flingTime), headPos, captive.ArenaHalfExtent, realDt);
            body.position = next;

            if (!Collide(next + Vector3.up * LassoHitHeight, (next - from) / realDt)) return;
            if (flingTime >= LassoOrbit.FlingSeconds) ReleaseCaptive();
        }

        /// <summary>Common per-frame check. False if the lasso just ended (death, destroyed, player dead). A posture break or a drone's
        /// death can shorten the held stagger, and the body must not act mid-air, so it is re-applied.</summary>
        private bool HoldCaptive()
        {
            if (captive == null || captiveHealth == null || !captiveHealth.IsAlive) { EndOnCaptiveDeath(); return false; }
            if (rigHealth != null && !rigHealth.IsAlive) { AbortLasso(); return false; }
            if (!captive.IsStaggered) captive.ForceStagger(HoldStaggerSeconds);
            return true;
        }

        /// <summary>A15.9: a captive's death ends it and starts the cooldown (a fling's already runs). The body drops to the floor.</summary>
        private void EndOnCaptiveDeath()
        {
            if (lassoPhase == LassoPhase.Orbit) lassoCooldownLeft = LassoCooldown;
            if (captive != null)
            {
                Vector3 p = captive.transform.position;
                p.y = captive.GroundY;
                captive.transform.position = p;
            }
            FinishLasso();
        }

        /// <summary>Not a fling: the player died or the Whip is going away (scene unload, weapon swap). Safe if the captive is already gone.</summary>
        private void AbortLasso()
        {
            if (!LassoBusy) return;
            if (lassoPhase == LassoPhase.Orbit) lassoCooldownLeft = LassoCooldown;
            if (captive != null && captiveHealth != null && captiveHealth.IsAlive) ReleaseCaptive();
            else EndOnCaptiveDeath();
        }

        /// <summary>The floor under the body and a short stagger that ends normally.</summary>
        private void ReleaseCaptive()
        {
            Vector3 p = captive.transform.position;
            p.y = captive.GroundY;
            captive.transform.position = p;
            captive.ForceStagger(LandStaggerSeconds);
            FinishLasso();
        }

        private void FinishLasso()
        {
            captive = null;
            captiveHealth = null;
            lassoPhase = LassoPhase.None;
            if (chain != null) chain.UnpinTip();
            lastHit.Clear();
            lassoGesture.Reset();
        }

        /// <summary>A15.9, unheld: release is the fling, at the hand's last velocity.</summary>
        private void OnLassoUnheld()
        {
            if (lassoPhase != LassoPhase.Orbit) return;
            if (captive == null || captiveHealth == null || !captiveHealth.IsAlive) EndOnCaptiveDeath();
            else BeginFling();
        }

        /// <summary>The captive's one overlap per frame. Every other enemy touched: it staggers, and both take <see cref="CollisionDamage"/>, once
        /// per enemy per <see cref="LassoReHitSeconds"/>. False if the lasso ended, which only a captive's death can do here.</summary>
        private bool Collide(Vector3 center, Vector3 velocity)
        {
            int n = Physics.OverlapSphereNonAlloc(center, LassoHitRadius, overlaps, Layers.BladeMask, QueryTriggerInteraction.Collide);
            float speed = velocity.magnitude;
            for (int c = 0; c < n; c++)
            {
                Collider other = overlaps[c];
                Health health = other.GetComponentInParent<Health>();
                if (health == null || health == captiveHealth || !health.IsAlive || health.transform.root == lassoRig) continue;
                if (!health.TryGetComponent(out MeleeAttacker melee)) continue; // an enemy, not a prop
                if (lastHit.TryGetValue(health, out float last) && clock - last < LassoReHitSeconds) continue;
                if (lastHit.Count >= CooldownCapacity) lastHit.Clear();
                lastHit[health] = clock;

                Vector3 point = other.ClosestPoint(center);
                Vector3 dir = point - center;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
                CollisionHit(health, point, dir, speed, velocity);
                // ForceStagger ignores super-armor and hyperArmourWindup: A15.9 says the enemy hit staggers, so it does.
                if (health.IsAlive) melee.ForceStagger();
                if (captiveHealth.IsAlive) CollisionHit(captiveHealth, point, -dir, speed, velocity);
                // One impact, for the enemy hit: it carries the Overdrive charge, combo count and haptic. The captive only takes damage.
                EventBus.Publish(new SwordImpact(point, speed, health.gameObject, velocity));
                if (!captiveHealth.IsAlive) { EndOnCaptiveDeath(); return false; }
            }
            return true;
        }

        // BladeDamager.TryHit's path, like the crack: boons apply and it spends Riposte / Quick Draw. Credit is the caller's (R5 only limits body hits).
        private void CollisionHit(Health health, Vector3 point, Vector3 dir, float speed, Vector3 velocity)
        {
            if (mods == null) mods = lassoRig.GetComponent<PlayerCombatModifiers>();
            float postureScale = 1f;
            float multiplier = mods != null ? mods.DamageMultiplierFor(health.gameObject, out postureScale) : 1f;
            if (mods != null) mods.HitPostureScale = postureScale;
            health.ApplyDamage(new DamageInfo(CollisionDamage * multiplier, point, dir, lassoRig.gameObject, DamageType.Collision, speed)); // weakness rules skip it: about 30 (A15.9)
            if (mods != null) mods.HitPostureScale = 1f;
        }

        private static void Pulse(float amplitude, float seconds) =>
            Haptics.Pulse(HandednessRig.MainHand(GameSettings.Active.LeftHanded), amplitude, seconds);
    }
}
