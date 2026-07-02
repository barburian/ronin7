using System.Collections.Generic;
using Ronin7.Combat;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// A solid, destructible chunk of rock in an asteroid belt. Lives under the moving
    /// <c>universe</c> transform like the planets and enemy ships, so the player rig (pinned at the
    /// world origin) sweeps THROUGH the belt as the universe scrolls past.
    ///
    /// Destructibility is free: the asteroid carries a non-trigger <see cref="SphereCollider"/> on
    /// the <see cref="Layers.Hittable"/> layer, so the existing <see cref="Projectile"/> sweep
    /// (player and enemy bolts alike) hits it via <c>GetComponentInParent&lt;IDamageable&gt;()</c>
    /// → the <see cref="Health"/> on this object. When health hits zero we simply disable the
    /// GameObject (no pooling — belts are static and small), which also removes its collider from
    /// physics so it stops dealing contact damage.
    ///
    /// Contact damage to the player and to enemy ships is resolved centrally by
    /// <see cref="AsteroidHazard"/>, which scans the static <see cref="Active"/> registry each frame.
    /// Asteroids keep no per-contact timers themselves — the hazard manager owns those.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Asteroid : MonoBehaviour
    {
        /// <summary>All live asteroids, so <see cref="AsteroidHazard"/> can do allocation-free
        /// squared-distance checks against the player and each enemy without physics queries.</summary>
        public static readonly List<Asteroid> Active = new();

        [Tooltip("Degrees/sec of slow tumble, for a little life. Kept cheap (one Rotate per frame).")]
        [SerializeField] private float spinSpeed = 12f;

        private Health health;
        private SphereCollider sphere;
        private Vector3 spinAxis;

        /// <summary>Current collision radius in WORLD units (collider radius × largest world scale).
        /// Read by <see cref="AsteroidHazard"/> for its contact checks.</summary>
        public float WorldRadius { get; private set; }

        /// <summary>This asteroid's centre in WORLD space (where the player hull, at the origin, can touch it).</summary>
        public Vector3 WorldCenter => sphere != null ? transform.TransformPoint(sphere.center) : transform.position;

        public bool IsAlive => health != null && health.IsAlive;

        private void Awake()
        {
            health = GetComponent<Health>();
            sphere = GetComponent<SphereCollider>();
            if (sphere == null) sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = false; // solid so the bolt SphereCast (ignores triggers) and overlap both register

            RecomputeWorldRadius();

            // Deterministic-ish spin axis from instance id so each rock tumbles a touch differently.
            var rng = new System.Random(GetEntityId().GetHashCode());
            spinAxis = new Vector3(
                (float)rng.NextDouble() - 0.5f,
                (float)rng.NextDouble() - 0.5f,
                (float)rng.NextDouble() - 0.5f).normalized;
            if (spinAxis.sqrMagnitude < 0.001f) spinAxis = Vector3.up;
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
            if (sphere != null) sphere.enabled = true;
            if (health != null) health.Died += OnDied;
        }

        private void OnDisable()
        {
            Active.Remove(this);
            if (health != null) health.Died -= OnDied;
        }

        private void Update()
        {
            // Cheap drift so the field isn't dead-still. Local rotation, no allocations.
            transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.Self);
        }

        /// <summary>Largest absolute axis scale, matching <see cref="Projectile.MaxAbsScale"/> so the
        /// contact radius tracks the rendered size under any parent scaling.</summary>
        private void RecomputeWorldRadius()
        {
            Vector3 s = transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
            WorldRadius = sphere.radius * maxScale;
        }

        private void OnDied()
        {
            // Break apart: drop out of the registry, kill the collider, and disable. No pooling —
            // disabling is enough; OnDisable already removes us from Active and unsubscribes.
            if (sphere != null) sphere.enabled = false;
            gameObject.SetActive(false);
        }
    }
}
