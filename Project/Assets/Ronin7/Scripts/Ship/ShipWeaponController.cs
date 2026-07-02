using Ronin7.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Ship
{
    /// <summary>
    /// Fixed forward guns mounted on the (stationary) cockpit. While the fire trigger is held the
    /// guns spit pooled <see cref="Projectile"/> bolts at a cadence set by a
    /// <see cref="ShipWeaponDefinition"/>.
    ///
    /// FRAME OF REFERENCE: the rig/cockpit never moves — it sits at the origin while the
    /// <c>universe</c> flies past (see <see cref="ShipController"/>). Bolts spawn at the muzzle
    /// near the origin, but we launch them parented to the <c>universe</c> transform (the same
    /// moving-world frame the enemy ships and enemy bolts live in). The launch world pose is
    /// preserved, so the aim point at the moment of firing is exactly the muzzle forward; from then
    /// on the bolt is carried by the world frame, so a fired bolt commits to a determined path in
    /// the game world and is NOT dragged around by the player subsequently steering/turning. (Firing
    /// in raw world space instead would leave bolts pinned to the player's view as the universe
    /// counter-rotates around the origin during steering.)
    ///
    /// Optional aim-assist: snap the muzzle to lead onto the nearest enemy ship within a cone so a
    /// seated player with gentle flight controls can still land hits. Kept minimal and readable.
    ///
    /// Input is read with the SAME asset-level <see cref="Resolve"/> strategy
    /// <see cref="ShipController"/> uses, so mixed/embedded serialized references can't leave us
    /// reading a different action instance than the one bound to live devices.
    /// </summary>
    public class ShipWeaponController : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Trigger action that fires the guns (e.g. Right Hand 'Activate'/'Select').")]
        [SerializeField] private InputActionReference fireAction;
        [SerializeField, Range(0.1f, 0.9f)] private float pressThreshold = 0.5f;

        [Header("Refs")]
        [Tooltip("Shared pool every shooter draws bolts from (player + enemies).")]
        [SerializeField] private ProjectilePool pool;
        [SerializeField] private ShipWeaponDefinition definition;
        [Tooltip("Muzzle transforms bolts launch from. If empty, this object's transform is used. " +
                 "Two muzzles = twin guns; they alternate so the cadence is per-gun, not per-pair.")]
        [SerializeField] private Transform[] muzzles;
        [Tooltip("The GameObject treated as the SHOOTER for friendly-fire exclusion. Set this to the " +
                 "player ship root (the object holding the ship Health + hull collider) so the player's " +
                 "own bolts can't strike their own hull. If null, this object is used.")]
        [SerializeField] private GameObject ownerRoot;

        [Header("Aim assist (optional, world space)")]
        [Tooltip("Snap bolt direction onto the nearest enemy ship inside this cone (deg). 0 disables assist.")]
        [SerializeField, Range(0f, 30f)] private float aimAssistConeAngle = 8f;
        [Tooltip("Max distance (world units) the aim assist will reach to find a lock.")]
        [SerializeField] private float aimAssistRange = 400f;

        private InputAction fireResolved;
        private float nextShotTime;
        private int muzzleCursor;

        private void OnEnable()
        {
            fireResolved = InputResolver.Resolve(fireAction, "Right Hand", "Activate", "ShipWeapon");
        }

        private void OnDisable()
        {
            fireResolved?.Disable();
            fireResolved = null;
        }

        private void Awake()
        {
            if (definition == null)
            {
                Debug.LogWarning("[ShipWeapon] No ShipWeaponDefinition assigned — using default stats.", this);
                definition = ScriptableObject.CreateInstance<ShipWeaponDefinition>();
            }
            if (pool == null) Debug.LogError("[ShipWeapon] No ProjectilePool assigned — guns cannot fire.", this);
        }

        private void Update()
        {
            if (pool == null || definition == null) return;

            bool firing = fireResolved != null && fireResolved.ReadValue<float>() >= pressThreshold;
            if (!firing) return;

            if (Time.time < nextShotTime) return;
            nextShotTime = Time.time + definition.SecondsPerShot;
            FireOnce();
        }

        private void FireOnce()
        {
            Transform muzzle = NextMuzzle();
            Vector3 pos = muzzle.position;
            Vector3 dir = muzzle.forward;

            // Optional aim assist: bend the launch direction toward the nearest enemy in the cone.
            if (aimAssistConeAngle > 0f && TryFindLock(pos, dir, out Vector3 lockPoint))
                dir = (lockPoint - pos).normalized;

            // Player fire lives in the moving-world (universe) frame, like enemy bolts, so a fired
            // bolt commits to a world path instead of following the player's view when they steer.
            // The launch world pose is preserved (worldPositionStays), so the aim is unchanged at
            // the instant of firing. Owner = the player ship root so the bolt skips its own hull.
            GameObject owner = ownerRoot != null ? ownerRoot : gameObject;
            Transform universeFrame = ShipController.Instance != null ? ShipController.Instance.Universe : null;
            pool.Fire(owner, pos, Quaternion.LookRotation(dir),
                definition.projectileSpeed, definition.damage, definition.projectileLifetime,
                definition.projectileRadius, definition.tracerColor, parent: universeFrame);

            OnFired(pos, dir);
            EventBus.Publish(new ShipWeaponFired(pos));
        }

        /// <summary>
        /// Public peek for HUD elements (the cockpit crosshair) so the lock indicator and the actual
        /// fire bend stay logically identical — they run the same cone scan against the same muzzle.
        /// Origin/forward use the first available muzzle so the visualization tracks the real bolt
        /// path. Returns false when aim assist is disabled, no muzzle is available, or no enemy is in
        /// cone/range.
        /// </summary>
        public bool TryGetAimLock(out Vector3 worldPoint)
        {
            worldPoint = default;
            if (aimAssistConeAngle <= 0f) return false;

            Transform m = transform;
            if (muzzles != null && muzzles.Length > 0)
            {
                for (int i = 0; i < muzzles.Length; i++)
                {
                    if (muzzles[i] != null) { m = muzzles[i]; break; }
                }
            }
            return TryFindLock(m.position, m.forward, out worldPoint);
        }

        /// <summary>
        /// Find the nearest live enemy ship whose direction lies within the assist cone of the
        /// muzzle forward. Works entirely in world space because enemy ships are rendered there.
        /// </summary>
        private bool TryFindLock(Vector3 origin, Vector3 forward, out Vector3 point)
        {
            point = default;
            float bestDot = Mathf.Cos(aimAssistConeAngle * Mathf.Deg2Rad);
            float bestDist = float.MaxValue;
            bool found = false;

            // EnemyShip count is small (conservative encounter sizes), so a per-shot scan is cheap.
            var ships = EnemyShip.Active;
            for (int i = 0; i < ships.Count; i++)
            {
                var ship = ships[i];
                if (ship == null || !ship.IsAlive) continue;
                Vector3 to = ship.transform.position - origin;
                float dist = to.magnitude;
                if (dist > aimAssistRange || dist < 0.01f) continue;
                float dot = Vector3.Dot(forward, to / dist);
                if (dot < bestDot) continue;          // outside the cone
                if (dist >= bestDist) continue;        // prefer the closest in-cone target
                bestDist = dist;
                point = ship.transform.position;
                found = true;
            }
            return found;
        }

        private Transform NextMuzzle()
        {
            if (muzzles == null || muzzles.Length == 0) return transform;
            Transform m = muzzles[muzzleCursor % muzzles.Length];
            muzzleCursor++;
            return m != null ? m : transform;
        }

        /// <summary>FX/SFX hook: muzzle flash / fire SFX. Overridable so art/audio extends without touching the loop.</summary>
        protected virtual void OnFired(Vector3 muzzlePos, Vector3 direction) { }
    }
}
