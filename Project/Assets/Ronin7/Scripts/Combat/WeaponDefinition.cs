using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>
    /// Designer-tunable melee weapon stats. Damage scales with how fast the blade is moving,
    /// so a light touch does nothing and a committed swing hurts.
    /// </summary>
    [CreateAssetMenu(menuName = "Space Samurai/Weapon Definition", fileName = "WeaponDefinition")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Swing → Damage")]
        [Tooltip("Blade speed (m/s) below which a contact does no damage.")]
        public float minSwingSpeed = 1.5f;
        [Tooltip("Blade speed (m/s) at which damage reaches its maximum.")]
        public float referenceSwingSpeed = 6f;
        public float minDamage = 8f;
        public float maxDamage = 40f;

        [Header("Feel")]
        [Tooltip("Seconds before the same blade can deal damage again (prevents multi-hits per swing).")]
        public float hitCooldown = 0.25f;

        public float DamageForSpeed(float speed)
        {
            if (speed < minSwingSpeed) return 0f;
            float t = Mathf.InverseLerp(minSwingSpeed, referenceSwingSpeed, speed);
            return Mathf.Lerp(minDamage, maxDamage, t);
        }
    }
}
