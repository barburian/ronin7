using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.Audio.Vfx
{
    /// <summary>
    /// Fixed-capacity pool of reusable <see cref="ParticleSystem"/> instances — one ring per effect
    /// prefab. The particle equivalent of <see cref="OneShotPool"/>: a slot is "in use" until
    /// <c>Time.unscaledTime &gt;= freeAt[i]</c> (claim time + the system's particle lifetime); when a
    /// ring is saturated the oldest-claimed slot is stolen (clipping the tail of an older burst is
    /// invisible). This replaces per-hit <c>Instantiate</c>/<c>Destroy</c>, which would churn GC
    /// during a busy fight on Quest.
    ///
    /// A plain class (not a MonoBehaviour) so it can be unit-tested without the play loop; the owner
    /// passes the parent transform the pooled instances are kept under. Bursts are injected via
    /// <see cref="ParticleSystem.Emit(int)"/> so the runtime count can scale with tier/swing speed
    /// without per-prefab emission config; the prefabs loop with emission disabled so they stay alive
    /// and simulate the manually-emitted particles.
    /// </summary>
    public class VfxPool
    {
        private readonly Transform parent;
        private readonly int capacity;
        private readonly Dictionary<ParticleSystem, Ring> rings = new();

        /// <summary>Total ParticleSystems Instantiated so far. Test hook: stays ≤ capacity × distinct prefabs.</summary>
        public int Instantiations { get; private set; }

        private class Ring
        {
            public ParticleSystem[] systems;
            public float[] freeAtUnscaled;
        }

        public VfxPool(Transform parent, int capacity = 16)
        {
            this.parent = parent;
            this.capacity = Mathf.Max(1, capacity);
        }

        /// <summary>
        /// Emit <paramref name="count"/> particles from a pooled instance of <paramref name="prefab"/>
        /// at <paramref name="point"/>. No-op if the prefab is null or count ≤ 0. The instance becomes
        /// claimable again once its longest particle lifetime elapses.
        /// </summary>
        public void Play(ParticleSystem prefab, Vector3 point, int count)
        {
            if (prefab == null || count <= 0) return;

            var ring = GetOrCreateRing(prefab);
            int slot = ClaimSlot(ring);
            var ps = ring.systems[slot];
            ps.transform.position = point;
            ps.Emit(count);

            float life = prefab.main.startLifetime.constantMax;
            ring.freeAtUnscaled[slot] = Time.unscaledTime + Mathf.Max(0.05f, life);
        }

        /// <summary>The ring for <paramref name="prefab"/>, allocating its <see cref="capacity"/>
        /// instances on first use (the only time this pool Instantiates).</summary>
        private Ring GetOrCreateRing(ParticleSystem prefab)
        {
            if (rings.TryGetValue(prefab, out var ring)) return ring;

            ring = new Ring
            {
                systems = new ParticleSystem[capacity],
                freeAtUnscaled = new float[capacity],
            };
            for (int i = 0; i < capacity; i++)
            {
                var ps = Object.Instantiate(prefab, parent);
                ps.name = $"{prefab.name}_{i:00}";
                ps.Play(); // idle (emission disabled); manual Emit injects the bursts
                Instantiations++;
                ring.systems[i] = ps;
            }
            rings[prefab] = ring;
            return ring;
        }

        /// <summary>First free slot, else the oldest-claimed one (mirrors <see cref="OneShotPool"/>).</summary>
        private int ClaimSlot(Ring ring)
        {
            float now = Time.unscaledTime;
            int oldest = 0;
            float oldestTime = ring.freeAtUnscaled[0];
            for (int i = 0; i < capacity; i++)
            {
                if (ring.freeAtUnscaled[i] <= now) return i;
                if (ring.freeAtUnscaled[i] < oldestTime)
                {
                    oldestTime = ring.freeAtUnscaled[i];
                    oldest = i;
                }
            }
            return oldest;
        }
    }
}
