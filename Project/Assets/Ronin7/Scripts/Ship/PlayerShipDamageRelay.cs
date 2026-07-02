using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Bridges the player ship's generic <see cref="Health"/> to the space-combat event vocabulary:
    /// when the ship's Health takes damage it republishes a <see cref="PlayerShipDamaged"/> on the
    /// <see cref="EventBus"/> so HUD/comfort/audio systems can react to ship hits specifically
    /// (rather than every <see cref="EntityDamaged"/> in the scene). Kept tiny and decoupled — the
    /// same pattern the melee enemies use to translate generic damage into gameplay events.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerShipDamageRelay : MonoBehaviour
    {
        private Health health;

        private void Awake() => health = GetComponent<Health>();
        private void OnEnable()  { if (health != null) { health.Damaged += OnDamaged; health.Died += OnDied; } }
        private void OnDisable() { if (health != null) { health.Damaged -= OnDamaged; health.Died -= OnDied; } }

        private void OnDamaged(DamageInfo info)
        {
            // Only surface incoming weapon fire as a "ship hit" (ignore e.g. self/melee sources).
            if (info.Type != DamageType.Projectile && info.Type != DamageType.Collision) return;
            EventBus.Publish(new PlayerShipDamaged(info.Amount, info.Point, health.Current, health.Max));
        }

        private void OnDied()
        {
            // The dogfight's lose condition. Republished as space-combat vocabulary so flow/HUD/audio
            // can react to defeat without referencing the ship directly.
            EventBus.Publish(new PlayerShipDestroyed(gameObject, transform.position));
        }
    }
}
