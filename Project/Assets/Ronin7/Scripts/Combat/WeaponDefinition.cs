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

        [Header("Blade (A13.5)")]
        [Tooltip("World length (m) of the blade, which is also the swept hit reach. 0 = leave the scene's blade as built (Katana).")]
        public float bladeLength = 0f;

        [Header("Parry / Thrust (A15.4)")]
        [Tooltip("Off = this weapon never parries: enemy strikes and bolts pass it and only hurt if they reach the body.")]
        public bool canParry = true;
        [Tooltip("Scales the stagger of a normal (non-perfect) parry. A perfect parry always gets full stagger.")]
        public float parryStagger = 1f;
        [Tooltip("Flat thrust damage when pushed forward along the pole at 2 m/s or more (A15.6). 0 = no thrust.")]
        public float thrustDamage = 0f;

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
