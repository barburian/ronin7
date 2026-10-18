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
        [Tooltip("P4-08 loadout id (Ronin7.Core.WeaponId). Persisted in meta.json; never rename.")]
        public string id = "katana";

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

        public float DamageForSpeed(float speed) => DamageForSpeed(speed, 1f);

        /// <summary>P4-02 EdgeSensitivity: both speed thresholds scaled by <paramref name="thresholdScale"/>
        /// (&lt; 1 = max damage reached at a slower swing). The damage range itself is unchanged.</summary>
        public float DamageForSpeed(float speed, float thresholdScale)
        {
            float min = minSwingSpeed * thresholdScale;
            if (speed < min) return 0f;
            float t = Mathf.InverseLerp(min, referenceSwingSpeed * thresholdScale, speed);
            return Mathf.Lerp(minDamage, maxDamage, t);
        }

        /// <summary>P4-02 EdgeSensitivity bonus -> threshold scale, floored at x0.70.</summary>
        public static float EdgeThresholdScale(float edgeBonus) => 1f - Mathf.Clamp(edgeBonus, 0f, 0.3f);
    }
}
