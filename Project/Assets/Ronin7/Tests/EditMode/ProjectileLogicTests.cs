using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Ship;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class ProjectileLogicTests
    {
        // ShouldDamage is the pure decision surface: damage only when there IS a target, that target
        // is alive, and it is not the firer (or one of the firer's children).

        private GameObject _owner;
        private GameObject _otherRoot;
        private GameObject _childOfOwner;
        private readonly List<GameObject> _pools = new(); // pool roots built per test, torn down below

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("Owner");
            _otherRoot = new GameObject("OtherRoot");
            _childOfOwner = new GameObject("ChildOfOwner");
            _childOfOwner.transform.SetParent(_owner.transform);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _pools) if (go != null) Object.DestroyImmediate(go);
            _pools.Clear();
            if (_owner != null) Object.DestroyImmediate(_owner);
            if (_otherRoot != null) Object.DestroyImmediate(_otherRoot);
            // _childOfOwner is destroyed with _owner.
        }

        [Test]
        public void ShouldDamage_NullOwner_AliveTarget_ReturnsTrue()
        {
            Assert.IsTrue(Projectile.ShouldDamage(null, _otherRoot.transform, true));
        }

        [Test]
        public void ShouldDamage_NullOwner_DeadTarget_ReturnsFalse()
        {
            Assert.IsFalse(Projectile.ShouldDamage(null, _otherRoot.transform, false));
        }

        [Test]
        public void ShouldDamage_OwnerNotAncestorOfHit_AliveTarget_ReturnsTrue()
        {
            Assert.IsTrue(Projectile.ShouldDamage(_owner, _otherRoot.transform, true));
        }

        [Test]
        public void ShouldDamage_HitIsChildOfOwner_ReturnsFalse()
        {
            Assert.IsFalse(Projectile.ShouldDamage(_owner, _childOfOwner.transform, true));
        }

        [Test]
        public void ShouldDamage_HitIsOwnerItself_ReturnsFalse()
        {
            // Transform.IsChildOf(self) returns true in Unity, so the owner can't shoot itself.
            Assert.IsFalse(Projectile.ShouldDamage(_owner, _owner.transform, true));
        }

        [Test]
        public void ShouldDamage_NullHitRoot_ReturnsFalse()
        {
            Assert.IsFalse(Projectile.ShouldDamage(_owner, null, true));
        }

        // --- Pool reuse: the O(1) IsPooled guard and capacity/recycle behaviour. ---

        [Test]
        public void Return_CalledTwice_DoesNotEnqueueBoltTwice()
        {
            var pool = MakePool(capacity: 3);
            var bolt = Fire(pool);
            Assert.IsFalse(bolt.IsPooled, "A just-fired bolt is live, not pooled.");

            pool.Return(bolt);
            Assert.IsTrue(bolt.IsPooled, "Return marks the bolt pooled.");
            int afterFirstReturn = Available(pool).Count;

            pool.Return(bolt); // double-return (e.g. expiry + hit same frame) must be a no-op
            Assert.AreEqual(afterFirstReturn, Available(pool).Count,
                "Returning an already-pooled bolt must not enqueue it a second time.");
        }

        [Test]
        public void Fire_ServesUpToCapacity_ThenRecyclesOldest()
        {
            var pool = MakePool(capacity: 3);

            var b0 = Fire(pool);
            var b1 = Fire(pool);
            var b2 = Fire(pool);

            Assert.AreNotSame(b0, b1);
            Assert.AreNotSame(b1, b2);
            Assert.AreNotSame(b0, b2);
            Assert.AreEqual(0, Available(pool).Count, "All capacity bolts are now in flight.");
            Assert.AreEqual(3, LiveOrder(pool).Count);

            // Beyond capacity the pool evicts the OLDEST live bolt (b0) and re-serves it.
            var b3 = Fire(pool);
            Assert.AreSame(b0, b3, "The 4th fire recycles the oldest live bolt.");
            Assert.AreEqual(3, LiveOrder(pool).Count, "Live count stays capped at capacity.");
        }

        // --- Helpers: EditMode does not run Awake on AddComponent, so we invoke it explicitly. ---

        private ProjectilePool MakePool(int capacity)
        {
            var go = new GameObject("ProjectilePool");
            _pools.Add(go);
            var pool = go.AddComponent<ProjectilePool>();
            PoolField("capacity").SetValue(pool, capacity);

            // Build the bolts (Pool.Awake) and arm each bolt's components (Projectile.Awake) so that
            // the subsequent Launch — which touches the SphereCollider — does not hit a null field.
            typeof(ProjectilePool).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(pool, null);
            var boltAwake = typeof(Projectile).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var bolt in Available(pool)) boltAwake.Invoke(bolt, null);
            return pool;
        }

        // Parent the bolts under the pool (not world space) so DestroyImmediate(pool) cleans them up.
        private Projectile Fire(ProjectilePool pool) =>
            pool.Fire(_owner, Vector3.zero, Quaternion.identity, 0f, 0f, 99f, 0.1f, Color.white, pool.transform);

        private static FieldInfo PoolField(string name) =>
            typeof(ProjectilePool).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

        private static Queue<Projectile> Available(ProjectilePool pool) =>
            (Queue<Projectile>)PoolField("available").GetValue(pool);

        private static List<Projectile> LiveOrder(ProjectilePool pool) =>
            (List<Projectile>)PoolField("liveOrder").GetValue(pool);
    }
}
