using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Ship;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// PlayMode harness for the Projectile. Test A confirms a moving bolt damages a target it
    /// flies into; Test B is the initial-overlap harness — a bolt fired at the exact position of a
    /// target. Pre-fix, Test B fails (Physics.SphereCast ignores colliders already overlapping at
    /// the cast origin, so the bolt passes through). Post-fix, the leading OverlapSphere catches it.
    /// </summary>
    public class ProjectileHitTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            _spawned.Clear();
        }

        private GameObject CreatePool(out ProjectilePool pool)
        {
            var go = new GameObject("Pool");
            pool = go.AddComponent<ProjectilePool>();
            _spawned.Add(go);
            return go;
        }

        private GameObject CreateTarget(Vector3 position, float radius, out Health health)
        {
            var go = new GameObject("Target");
            go.transform.position = position;
            var col = go.AddComponent<SphereCollider>();
            col.radius = radius;
            col.isTrigger = false; // non-trigger so SphereCast (which ignores triggers here) can hit it
            health = go.AddComponent<Health>();
            health.Configure(100f);
            _spawned.Add(go);
            return go;
        }

        [UnityTest]
        public IEnumerator Projectile_DirectFlight_DamagesTarget()
        {
            CreatePool(out var pool);
            // Allow Awake to run on the freshly-added ProjectilePool so its bolts spawn.
            yield return null;

            CreateTarget(new Vector3(0f, 0f, 10f), 1f, out var health);

            pool.Fire(
                owner: null,
                worldPosition: Vector3.zero,
                worldRotation: Quaternion.identity,
                speed: 50f,
                damage: 10f,
                lifetime: 1f,
                radius: 0.2f,
                color: Color.white,
                parent: null);

            // 50 u/s reaches z=10 in 0.2s; 0.5s is generous.
            yield return new WaitForSeconds(0.5f);

            Assert.Less(health.Current, 100f, "Direct flight should damage the target.");
        }

        [UnityTest]
        public IEnumerator Projectile_ParentedToFrame_TravelsWithThatFrame()
        {
            // Contract: a bolt launched with a non-null parent lives in that frame, so when the
            // frame moves the bolt is carried with it. This is what keeps player bolts committed to
            // a world path instead of following the player's view as the universe rotates on steer.
            CreatePool(out var pool);
            yield return null;

            var universe = new GameObject("Universe").transform; // stands in for ShipController.Universe
            _spawned.Add(universe.gameObject);

            // Fire off the rotation pivot with zero speed so the bolt doesn't translate itself —
            // any motion we observe is purely the frame carrying it.
            var bolt = pool.Fire(
                owner: null,
                worldPosition: new Vector3(3f, 0f, 0f),
                worldRotation: Quaternion.identity,
                speed: 0f,
                damage: 10f,
                lifetime: 5f,
                radius: 0.2f,
                color: Color.white,
                parent: universe);
            Assert.NotNull(bolt, "Pool should serve a bolt.");
            yield return null;

            // Rotate the frame 90° about Y; the bolt's world position must rotate with it.
            universe.Rotate(0f, 90f, 0f);
            yield return null;

            Vector3 expected = new Vector3(0f, 0f, -3f); // (3,0,0) rotated +90° about Y
            Assert.Less((bolt.transform.position - expected).magnitude, 0.01f,
                "A parented bolt should be carried by its frame when the frame rotates.");
        }

        [UnityTest]
        public IEnumerator Projectile_InitialOverlap_DamagesTarget()
        {
            CreatePool(out var pool);
            yield return null;

            // Target at origin — the same position the bolt is launched from. Bolt radius 0.5
            // plus target radius 1.0 means the bolt is fully inside the target on launch.
            CreateTarget(Vector3.zero, 1f, out var health);

            pool.Fire(
                owner: null,
                worldPosition: Vector3.zero,
                worldRotation: Quaternion.identity,
                speed: 5f,
                damage: 10f,
                lifetime: 1f,
                radius: 0.5f,
                color: Color.white,
                parent: null);

            // A couple of Update ticks for the leading OverlapSphere to detect the overlap.
            yield return null;
            yield return null;

            Assert.Less(health.Current, 100f,
                "Bolt launched inside the target should still register a hit (initial-overlap fix).");
        }
    }
}
