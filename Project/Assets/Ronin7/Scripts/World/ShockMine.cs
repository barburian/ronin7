using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// One-shot buried mine. Requires a trigger collider on the same GameObject (SphereCollider isTrigger).
    /// Ignores triggers until armed (armDelay), then detonates once on player contact, dealing damage
    /// and optionally enabling a flash VFX object.
    /// </summary>
    public class ShockMine : MonoBehaviour
    {
        [SerializeField] private float damage = 15f;
        [SerializeField] private GameObject flashObject;
        [SerializeField] private float armDelay = 1f;

        private float timeCreated;
        private bool detonated;

        public bool Detonated => detonated;

        private void Awake()
        {
            timeCreated = Time.time;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Not yet armed
            if (Time.time - timeCreated < armDelay) return;

            // Already detonated
            if (detonated) return;

            // Find Health on the root
            Health health = other.GetComponentInParent<Health>();
            if (health == null) return;

            // Only damage the player (identified by CharacterController component)
            if (health.GetComponent<CharacterController>() == null) return;

            // Apply damage
            Vector3 hitPoint = other.ClosestPointOnBounds(transform.position);
            Vector3 direction = (hitPoint - transform.position).normalized;
            health.ApplyDamage(new DamageInfo(damage, hitPoint, direction, gameObject));

            // Detonate: enable flash, mark detonated, disable collider
            detonated = true;
            if (flashObject != null) flashObject.SetActive(true);
            GetComponent<Collider>().enabled = false;
        }
    }
}
