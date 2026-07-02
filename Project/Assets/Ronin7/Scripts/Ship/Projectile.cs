using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// A pooled energy bolt. Travels in a straight line along its own +Z at a fixed speed,
    /// applies a single <see cref="DamageType.Projectile"/> hit to the first
    /// <see cref="IDamageable"/> it touches, and returns itself to its owning
    /// <see cref="ProjectilePool"/> on hit or when its lifetime expires. No per-shot
    /// instantiate/destroy — that allocation churn is exactly what we cannot afford on Quest.
    ///
    /// FRAME OF REFERENCE (important): a projectile simply integrates position along its local
    /// forward in <b>whatever space its transform lives in</b>. The pool/firer decides that:
    /// <list type="bullet">
    /// <item><b>Player bolts</b> are launched parented to nothing (world/real space) from the
    /// cockpit near the origin, so they fly out into rendered space and hit the rendered enemy
    /// ship colliders directly.</item>
    /// <item><b>Enemy bolts</b> are launched parented under the <c>universe</c> transform, so they
    /// share the moving-world frame with the enemy ships and travel toward the player's virtual
    /// position; their collider sweep still resolves correctly because Unity physics runs in world
    /// space and we move via <see cref="Rigidbody"/>.</item>
    /// </list>
    /// Movement uses a per-frame SphereCast sweep so fast bolts don't tunnel through thin targets.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class Projectile : MonoBehaviour
    {
        private ProjectilePool pool;
        private Rigidbody body;
        private SphereCollider sphere;

        private GameObject owner;          // the firer, so a bolt can't damage its own shooter
        private float damage;
        private float speed;               // units/sec along transform.forward
        private float lifeRemaining;
        private bool live;

        /// <summary>
        /// True while this bolt sits idle in its <see cref="ProjectilePool"/>'s available queue. The
        /// pool flips it (true on enqueue, false on launch) and reads it as an O(1) guard so a
        /// double-return — e.g. lifetime expiry and a contact resolving in the same frame — can't
        /// enqueue the same bolt twice. Owned by the pool; nothing else writes it.
        /// </summary>
        public bool IsPooled { get; internal set; }

        // Reused per-frame buffer for OverlapSphereNonAlloc so the initial-overlap check costs no GC.
        private static readonly Collider[] _overlapBuf = new Collider[8];

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;            // we drive motion explicitly; no physics forces
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;

            sphere = GetComponent<SphereCollider>();
            sphere.isTrigger = true;            // overlap detection; we resolve damage ourselves
        }

        /// <summary>
        /// Called by the pool the moment a bolt is fired. Positions/orients the bolt, stamps its
        /// payload, and arms it. <paramref name="parent"/> selects the frame the bolt lives in
        /// (null = world/real space for player fire; the universe transform for enemy fire).
        /// </summary>
        public void Launch(ProjectilePool owningPool, GameObject firer, Vector3 worldPosition,
            Quaternion worldRotation, float boltSpeed, float boltDamage, float lifetime,
            float radius, Color color, Transform parent)
        {
            pool = owningPool;
            owner = firer;
            speed = boltSpeed;
            damage = boltDamage;
            lifeRemaining = lifetime;

            transform.SetParent(parent, worldPositionStays: true);
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            transform.localScale = Vector3.one * (radius * 2f);
            sphere.radius = 0.5f; // unit sphere scaled by localScale → world radius == radius

            ApplyTint(color);
            gameObject.SetActive(true);
            live = true;
            OnLaunched();
        }

        private void Update()
        {
            if (!live) return;

            float dt = Time.deltaTime;
            lifeRemaining -= dt;
            if (lifeRemaining <= 0f)
            {
                Recycle();
                return;
            }

            float worldRadius = sphere.radius * MaxAbsScale();

            // Physics.SphereCast does NOT report colliders the sphere already overlaps at its origin,
            // so a bolt launched inside a target (point-blank / sudden swerve) would silently pass through.
            // Catch that case explicitly before the sweep.
            int overlapCount = Physics.OverlapSphereNonAlloc(transform.position, worldRadius, _overlapBuf, Layers.HittableMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
            {
                if (TryHit(_overlapBuf[i], transform.position, transform.forward)) return;
            }

            // Sweep from current position along forward by this frame's travel. A raycast catches
            // thin/fast hits that a pure trigger overlap could tunnel through between frames.
            Vector3 step = transform.forward * (speed * dt);
            float dist = step.magnitude;
            if (dist > 0f &&
                Physics.SphereCast(transform.position, worldRadius, transform.forward,
                    out RaycastHit hit, dist, Layers.HittableMask, QueryTriggerInteraction.Ignore))
            {
                if (TryHit(hit.collider, hit.point, transform.forward)) return;
            }

            transform.position += step;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!live) return;
            TryHit(other, transform.position, transform.forward);
        }

        /// <summary>
        /// Pure decision: should this bolt damage <paramref name="hitRoot"/>? False if the firer is
        /// shooting itself (or its own children), if the target is already dead, or if no target exists.
        /// </summary>
        public static bool ShouldDamage(GameObject owner, Transform hitRoot, bool targetAlive)
        {
            if (hitRoot == null) return false;
            if (owner != null && hitRoot.IsChildOf(owner.transform)) return false;
            return targetAlive;
        }

        /// <summary>
        /// Resolve a contact: ignore the firer (and its children), apply damage to the first
        /// <see cref="IDamageable"/> found up the hierarchy, then recycle. Returns true if the bolt
        /// was consumed (so the caller stops moving it this frame).
        /// </summary>
        private bool TryHit(Collider other, Vector3 point, Vector3 dir)
        {
            if (other == null) return false;

            var damageable = other.GetComponentInParent<IDamageable>();
            if (!ShouldDamage(owner, other.transform, damageable?.IsAlive ?? false)) return false;

            damageable.ApplyDamage(new DamageInfo(damage, point, dir, owner, DamageType.Projectile));
            OnHit(point, damageable);
            EventBus.Publish(new ProjectileImpact(point));
            Recycle();
            return true;
        }

        private void Recycle()
        {
            live = false;
            gameObject.SetActive(false);
            transform.SetParent(pool != null ? pool.transform : null, worldPositionStays: false);
            if (pool != null) pool.Return(this);
        }

        /// <summary>Largest absolute axis scale, so the SphereCast radius matches the rendered size under any parent scaling.</summary>
        private float MaxAbsScale()
        {
            Vector3 s = transform.lossyScale;
            return Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
        }

        private void ApplyTint(Color color)
        {
            var r = GetComponentInChildren<Renderer>();
            if (r == null) return;
            RendererTint.Apply(r, color);
        }

        // --- FX/SFX hooks: overridable so art/audio can extend without touching the core loop. ---

        /// <summary>Called once the bolt is positioned and armed. Hook muzzle flash / fire SFX here.</summary>
        protected virtual void OnLaunched() { }

        /// <summary>Called when the bolt strikes a damageable. Hook impact VFX / hit SFX here.</summary>
        protected virtual void OnHit(Vector3 point, IDamageable target) { }
    }
}
