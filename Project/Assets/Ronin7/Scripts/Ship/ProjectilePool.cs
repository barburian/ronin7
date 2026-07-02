using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// A fixed-capacity pool of grey-box <see cref="Projectile"/> bolts. Both the player guns and
    /// every enemy ship share a single pool so the whole dogfight has ONE bounded allocation of
    /// projectiles — no per-shot Instantiate/Destroy, which is the Quest perf killer this exists
    /// to avoid. Bolts are built from primitives at startup and recycled on hit/expiry.
    ///
    /// The pool itself is frame-agnostic: the firer passes a parent transform to
    /// <see cref="Projectile.Launch"/> deciding whether a bolt lives in world/real space (player
    /// fire) or under the moving <c>universe</c> transform (enemy fire). See <see cref="Projectile"/>.
    /// </summary>
    public class ProjectilePool : MonoBehaviour
    {
        [Header("Pool")]
        [Tooltip("Number of bolts pre-spawned. Sized for the worst-case simultaneous bolts in flight " +
                 "across the player + all enemies. If exceeded, the oldest live bolt is recycled.")]
        [SerializeField, Range(8, 128)] private int capacity = 64;

        [Tooltip("Optional prefab. If null, the pool builds primitive sphere bolts at runtime so the " +
                 "scene needs no art to function (matches the grey-box builder style).")]
        [SerializeField] private Projectile projectilePrefab;

        private readonly Queue<Projectile> available = new();
        private readonly List<Projectile> liveOrder = new(); // FIFO so we can evict the oldest if starved
        private bool warnedStarvation; // one-shot guard so a starved pool warns once, not every frame

        private void Awake()
        {
            for (int i = 0; i < capacity; i++)
            {
                var p = CreateBolt(i);
                p.gameObject.SetActive(false);
                p.IsPooled = true;
                available.Enqueue(p);
            }
        }

        /// <summary>
        /// Fire a bolt. Returns null only if the pool genuinely cannot serve one (should not happen
        /// once <see cref="capacity"/> is sized sensibly — we evict the oldest live bolt first).
        /// <paramref name="parent"/> selects the frame: null = world/real space (player), the
        /// universe transform = moving-world space (enemies).
        /// </summary>
        public Projectile Fire(GameObject owner, Vector3 worldPosition, Quaternion worldRotation,
            float speed, float damage, float lifetime, float radius, Color color, Transform parent)
        {
            Projectile bolt = available.Count > 0 ? available.Dequeue() : EvictOldest();
            // Defensive: EvictOldest only returns null when no bolt is live, which can't happen while
            // `available` is empty (every bolt would then be live) — so this branch is effectively unreachable.
            if (bolt == null) return null;

            bolt.IsPooled = false;
            liveOrder.Add(bolt);
            bolt.Launch(this, owner, worldPosition, worldRotation, speed, damage, lifetime, radius, color, parent);
            return bolt;
        }

        /// <summary>Called by a <see cref="Projectile"/> when it expires or hits something.</summary>
        public void Return(Projectile bolt)
        {
            // O(1) double-return guard: a bolt already idle in `available` carries IsPooled == true, so a
            // second Return (e.g. expiry and a hit resolving in the same frame) is a no-op instead of an
            // O(n) Queue.Contains scan.
            if (bolt == null || bolt.IsPooled) return;
            liveOrder.Remove(bolt);
            bolt.IsPooled = true;
            available.Enqueue(bolt);
        }

        /// <summary>Recycle the oldest live bolt to make room when the pool is fully in flight.</summary>
        private Projectile EvictOldest()
        {
            if (liveOrder.Count == 0) return null;
            var oldest = liveOrder[0];
            liveOrder.RemoveAt(0);

            // Reaching here means every bolt is in flight, so we're about to pop a still-live bolt out of
            // the world mid-travel. Cosmetic, but a sign capacity is too low — warn once (not every shot)
            // so QA can see the pop. LogWarning, not Core.Log, so it survives into release builds.
            if (!oldest.IsPooled && !warnedStarvation)
            {
                warnedStarvation = true;
                Debug.LogWarning(
                    $"[ProjectilePool] Pool starved (capacity {capacity}): recycling a live bolt mid-flight. " +
                    "Raise capacity if these pops are visible.", this);
            }

            oldest.gameObject.SetActive(false);
            oldest.transform.SetParent(transform, false);
            return oldest;
        }

        private Projectile CreateBolt(int index)
        {
            if (projectilePrefab != null)
            {
                var inst = Instantiate(projectilePrefab, transform);
                inst.name = $"Bolt {index:00}";
                return inst;
            }

            // Grey-box bolt: a small unlit-ish sphere with a SphereCollider trigger + Rigidbody.
            // Projectile.Awake() configures the Rigidbody/collider; we only build the GameObject.
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Bolt {index:00}";
            go.transform.SetParent(transform, false);

            // Replace the auto SphereCollider's role is fine, but ensure exactly the components
            // Projectile requires exist (CreatePrimitive already adds a SphereCollider).
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) go.AddComponent<Rigidbody>();

            return go.AddComponent<Projectile>();
        }
    }
}
