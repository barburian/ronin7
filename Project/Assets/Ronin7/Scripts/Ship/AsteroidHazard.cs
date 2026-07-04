using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// The single scene-wide manager that turns the asteroid belt into a real hazard: each frame it
    /// applies <see cref="DamageType.Collision"/> chip damage to BOTH the player ship and any enemy
    /// ships that are touching a live <see cref="Asteroid"/>, with a short per-victim cooldown so a
    /// brush against a rock is a punch rather than instant death.
    ///
    /// FRAME OF REFERENCE: contacts are resolved entirely in WORLD space, which the moving-universe
    /// design makes correct for free — the player hull sits at the world origin, and asteroids (under
    /// the universe transform) render at their swept world positions, so an asteroid "near the player"
    /// is literally near the origin. Enemy ships also render at world positions, so the same
    /// world-space distance test works for them.
    ///
    /// PERFORMANCE: there is exactly one overlap-free pass per frame. We iterate
    /// <see cref="Asteroid.Active"/> (a few dozen) against the player once, and against the small
    /// <see cref="EnemyShip.Active"/> list — pure squared-distance maths, no physics queries and no
    /// per-frame allocations. Enemy cooldowns live in a dictionary that is only mutated on hit and
    /// pruned cheaply when an enemy dies.
    /// </summary>
    [DisallowMultipleComponent]
    public class AsteroidHazard : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("The player ship Health (rig). Collision damage + the comfort-flash are applied here.")]
        [SerializeField] private Health playerHealth;
        [Tooltip("World-space radius of the player's hull damage volume (the 'Ship Hull' SphereCollider).")]
        [SerializeField] private float playerHullRadius = 1.2f;

        [Header("Tuning")]
        [Tooltip("Collision damage per contact tick, applied to player and enemies alike.")]
        [SerializeField] private float collisionDamage = 15f;
        [Tooltip("Seconds the player is immune after an asteroid hit (i-frames).")]
        [SerializeField] private float playerCooldown = 1.0f;
        [Tooltip("Seconds an enemy ship is immune after an asteroid hit.")]
        [SerializeField] private float enemyCooldown = 1.0f;
        [Tooltip("Comfort-vignette punch on a player hit (0 = none). Eases back out via the vignette itself.")]
        [SerializeField, Range(0f, 1f)] private float hitVignette = 0.6f;

        // Per-enemy next-allowed-hit time. Mutated only on hit; pruned against EnemyShip.Active.
        private readonly Dictionary<EnemyShip, float> enemyNextHit = new();
        // Scratch list reused to prune dead enemies out of the dictionary without per-frame allocation.
        private static readonly List<EnemyShip> _pruneScratch = new();
        // Scratch set reused so the alive-membership check is O(1) instead of an O(n) list scan, without per-frame allocation.
        private static readonly HashSet<EnemyShip> _aliveSet = new();

        private float playerNextHit;
        private ComfortVignette vignette;

        /// <summary>Editor/runtime wiring entry point (mirrors how the scene builder wires other managers).</summary>
        public void Configure(Health player, float hullRadius)
        {
            playerHealth = player;
            playerHullRadius = hullRadius;
        }

        /// <summary>Pure sphere-overlap check shared by <see cref="ScanPlayer"/> and <see cref="ScanEnemies"/>:
        /// true when two spheres (given by center + radius) are touching or overlapping.</summary>
        public static bool InRange(Vector3 centerA, float radiusA, Vector3 centerB, float radiusB)
        {
            float reach = radiusA + radiusB;
            return (centerA - centerB).sqrMagnitude <= reach * reach;
        }

        private void Update()
        {
            float now = Time.time;
            ScanPlayer(now);
            ScanEnemies(now);
        }

        /// <summary>Player hull sits at (near) the world origin; test it against every live asteroid.</summary>
        private void ScanPlayer(float now)
        {
            if (playerHealth == null || !playerHealth.IsAlive || now < playerNextHit) return;

            Vector3 hullCenter = playerHealth.transform.position; // rig root ≈ world origin
            var list = Asteroid.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a == null || !a.IsAlive) continue;

                if (!InRange(hullCenter, playerHullRadius, a.WorldCenter, a.WorldRadius)) continue;

                Vector3 point = a.WorldCenter;
                Vector3 dir = (hullCenter - point).normalized;
                playerHealth.ApplyDamage(new DamageInfo(collisionDamage, point, dir, a.gameObject, DamageType.Collision));
                playerNextHit = now + playerCooldown;
                PunchVignette();
                break; // one contact tick per cooldown window — don't stack multiple rocks in one frame
            }
        }

        /// <summary>Each live enemy vs every live asteroid, world-space squared-distance. Small N×M.</summary>
        private void ScanEnemies(float now)
        {
            var enemies = EnemyShip.Active;
            var rocks = Asteroid.Active;
            if (enemies.Count == 0 || rocks.Count == 0)
            {
                if (enemyNextHit.Count > 0) PruneEnemies(enemies);
                return;
            }

            for (int e = 0; e < enemies.Count; e++)
            {
                var enemy = enemies[e];
                if (enemy == null || !enemy.IsAlive) continue;

                if (enemyNextHit.TryGetValue(enemy, out float next) && now < next) continue;

                Vector3 enemyPos = enemy.transform.position;
                // Treat the enemy hull as a coarse radius; the body is ~a few metres, so a small
                // padding keeps grazing contacts feeling fair without a per-enemy collider lookup.
                const float enemyRadius = 4f;
                for (int r = 0; r < rocks.Count; r++)
                {
                    var a = rocks[r];
                    if (a == null || !a.IsAlive) continue;

                    if (!InRange(enemyPos, enemyRadius, a.WorldCenter, a.WorldRadius)) continue;

                    Vector3 point = a.WorldCenter;
                    Vector3 dir = (enemyPos - point).normalized;
                    var eh = enemy.GetComponent<Health>();
                    if (eh != null)
                        eh.ApplyDamage(new DamageInfo(collisionDamage, point, dir, a.gameObject, DamageType.Collision));
                    enemyNextHit[enemy] = now + enemyCooldown;
                    break; // one tick per enemy per cooldown window
                }
            }

            PruneEnemies(enemies);
        }

        /// <summary>Drop cooldown entries for enemies no longer alive so the dictionary can't grow unbounded.</summary>
        private void PruneEnemies(List<EnemyShip> alive)
        {
            if (enemyNextHit.Count == 0) return;
            _aliveSet.Clear();
            for (int i = 0; i < alive.Count; i++) _aliveSet.Add(alive[i]);
            _pruneScratch.Clear();
            foreach (var kv in enemyNextHit)
                if (kv.Key == null || !_aliveSet.Contains(kv.Key)) _pruneScratch.Add(kv.Key);
            for (int i = 0; i < _pruneScratch.Count; i++) enemyNextHit.Remove(_pruneScratch[i]);
        }

        private void PunchVignette()
        {
            if (hitVignette <= 0f) return;
            if (vignette == null) vignette = VignetteRig.Ensure(Camera.main);
            if (vignette != null) vignette.SetIntensity(hitVignette);
        }
    }
}
