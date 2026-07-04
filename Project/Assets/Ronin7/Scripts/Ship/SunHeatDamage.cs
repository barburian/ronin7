using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Sun-navigation Pillar 2 follow-through: applies <see cref="SunGravityWell"/>'s hull heat to the
    /// player ship each frame, scaled by dt (heat is already a per-second rate — see the well's own
    /// class doc). Presence in the scene IS the switch: this component does nothing unless both
    /// <see cref="well"/> and <see cref="target"/> are assigned, so dropping it into a scene turns sun
    /// heat on with no extra flag (mirrors <see cref="AsteroidHazard"/>'s cooldown-damage shape, minus
    /// the cooldown — heat is a continuous rate, not a discrete contact tick).
    /// </summary>
    [DisallowMultipleComponent]
    public class SunHeatDamage : MonoBehaviour
    {
        [Tooltip("The sun's SunGravityWell.")]
        [SerializeField] private SunGravityWell well;
        [Tooltip("The player ship Health (rig). Heat damage is applied here.")]
        [SerializeField] private Health target;

        private void Update()
        {
            if (well == null || target == null || !target.IsAlive) return;

            float heat = well.HeatAt(Vector3.zero) * Time.deltaTime;
            if (heat <= 0f) return;

            Vector3 point = target.transform.position;
            Vector3 dir = (point - well.transform.position).normalized;
            target.ApplyDamage(new DamageInfo(heat, point, dir, well.gameObject, DamageType.Collision));
        }
    }
}
