using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// A15.8, P9-02d: the Whip prototype on the handle's Grabbable. While held, runs the <see cref="VerletChain"/> on real
    /// time, draws it with one LineRenderer and, while the chain moves, sphere-checks its points for the body hit: 4 x the wielder's
    /// damage multiplier <see cref="DamageType.Status"/> (R7), at most once per enemy per <see cref="ReHitCooldown"/> (R6). It is
    /// feedback only (R5): it builds no Overdrive or combo and does not reset them. Holstered it does nothing and shows the coil (A15.14).
    /// The chain has no collider and no BladeDamager, so it cannot parry or reflect, and it ignores walls.
    /// Crack, tip bend and lasso are P9-02e.
    /// </summary>
    public class WhipController : MonoBehaviour
    {
        internal const float BodyHitDamage = 4f; // A15.8
        /// <summary>The fastest chain point must beat this (m/s) to deal body hits; falling below it ends the swing.</summary>
        internal const float SwingSpeed = 2.5f;
        internal const float BodyCheckRadius = 0.12f;
        /// <summary>R6: seconds, real time, before the same enemy takes another body hit.</summary>
        internal const float ReHitCooldown = 0.5f;
        private const int CooldownCapacity = 64;
        private const int OverlapBufferSize = 16;

        [SerializeField] private Transform chainRing;
        [SerializeField] private LineRenderer chainLine;
        [SerializeField] private GameObject coil;

        [Header("A15.11 cuts, in this order, on a failed proof")]
        [SerializeField, Range(6, VerletChain.DefaultPoints)] private int chainPoints = VerletChain.DefaultPoints;
        [Tooltip("1 = body checks every frame, 2 = every other frame.")]
        [SerializeField, Range(1, 2)] private int checkEveryNthFrame = 1;
        [SerializeField] private bool bodyDamage = true;

        private static readonly Collider[] overlaps = new Collider[OverlapBufferSize];
        private readonly Dictionary<Health, float> lastHit = new Dictionary<Health, float>(CooldownCapacity); // R6: cleared on grab/release
        private BladeDamager guard; // the handle's blade: Grabber disables it during tracking loss
        private float clock; // real seconds while held, summed from the dt Step is given
        private Grabbable grabbable;
        private VerletChain chain;
        private PlayerCombatModifiers mods;
        private bool held;
        private bool needsReset;
        private int lastChecks;

        internal VerletChain Chain => chain;
        internal bool Held => held;
        internal int StruckCount => lastHit.Count;
        internal int LastChecks => lastChecks;

        private void OnEnable()
        {
            grabbable = GetComponent<Grabbable>();
            if (grabbable != null)
            {
                grabbable.Grabbed += OnGrabbed;
                grabbable.Released += OnReleased;
            }
            SetHeld(grabbable != null && grabbable.IsHeld);
        }

        private void OnDisable()
        {
            if (grabbable == null) return;
            grabbable.Grabbed -= OnGrabbed;
            grabbable.Released -= OnReleased;
        }

        private void OnGrabbed(Transform hand) => SetHeld(true);
        private void OnReleased() => SetHeld(false);

        internal void SetHeld(bool value)
        {
            if (chain == null)
            {
                chain = new VerletChain(chainPoints);
                chainLine.positionCount = chain.Count;
            }
            held = value;
            chainLine.enabled = value;
            coil.SetActive(!value);
            lastHit.Clear();
            needsReset = value; // next tick, once the grab has put the handle in the hand: no swing spike
            lastChecks = 0;
        }

        private void LateUpdate()
        {
            if (!held) return;
#if RONIN7_DEV
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            Step(Time.unscaledDeltaTime); // A15.8: real time, Overdrive changes the time scale
#if RONIN7_DEV
            WeaponPerfLog.AddWhip(System.Diagnostics.Stopwatch.GetTimestamp() - start, lastChecks, chain.Count);
#endif
        }

        // ponytail: the ring pose is read in LateUpdate, not at before-render, so the first line point can trail a
        // late-latched hand by a frame. Refresh point 0 from Application.onBeforeRender if it shows in the headset.
        internal void Step(float realDt)
        {
            // ponytail: the rig root is the floor (one flat arena, transform.root = the rig while held). A raycast when that stops being true.
            float floorY = transform.root.position.y;
            if (needsReset)
            {
                chain.Reset(chainRing.position, chainRing.forward, floorY);
                needsReset = false;
            }
            chain.Tick(chainRing.position, realDt, floorY);
            chainLine.SetPositions(chain.Positions);

            clock += realDt;
            lastChecks = 0;
            if (chain.MaxSpeed < SwingSpeed) return;
            float speed = chain.MaxSpeed;
            if (guard == null) guard = GetComponentInChildren<BladeDamager>(true);
            if (guard != null && !guard.enabled) return; // tracking loss: the hand pose jumps, so no phantom hits
            if (!bodyDamage || Time.timeScale <= 0f) return; // paused: real time still runs, combat does not
            if (checkEveryNthFrame > 1 && Time.frameCount % checkEveryNthFrame != 0) return;

            Vector3[] points = chain.Positions;
            for (int i = 1; i < points.Length; i++) // not the pin: that is the hand
            {
                lastChecks++;
                int n = Physics.OverlapSphereNonAlloc(points[i], BodyCheckRadius, overlaps, Layers.BladeMask, QueryTriggerInteraction.Collide);
                for (int c = 0; c < n; c++) TryBodyHit(overlaps[c], points[i], chain.Velocity(i), speed);
            }
        }

        // Shared damage path, but mods.DamageMultiplier and not DamageMultiplierFor: that one spends Riposte and Quick Draw,
        // which a 4-damage Status tick must not eat. Status also stays out of flinch, posture and boon procs.
        internal bool TryBodyHit(Collider other, Vector3 point, Vector3 velocity, float speed)
        {
            // TagManager has no gameplay layers, so the overlap returns every collider: floor, walls and the rig land here too.
            var health = other.GetComponentInParent<Health>();
            if (health == null || !health.IsAlive) return false;
            Transform rig = transform.root;
            if (health.transform.root == rig) return false;
            // BladeDamager.TryRegisterHit's pattern (it is internal to Combat). Never grows past the prealloc: worst case one early re-hit.
            if (lastHit.TryGetValue(health, out float last) && clock - last < ReHitCooldown) return false;
            if (lastHit.Count >= CooldownCapacity) lastHit.Clear();
            lastHit[health] = clock;

            if (mods == null) mods = rig.GetComponent<PlayerCombatModifiers>(); // lazily: RunDirector may add it after the grab
            float damage = BodyHitDamage * (mods != null ? mods.DamageMultiplier : 1f); // R7
            Vector3 dir = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.up;
            health.ApplyDamage(new DamageInfo(damage, point, dir, rig.gameObject, DamageType.Status, speed));
            EventBus.Publish(new SwordImpact(point, speed, health.gameObject, velocity, true));
            return true;
        }
    }
}
