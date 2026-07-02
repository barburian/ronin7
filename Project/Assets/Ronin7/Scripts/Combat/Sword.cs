using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>
    /// Marks the held weapon and carries its tuning. The actual hit detection lives on the
    /// blade child (<see cref="BladeDamager"/>), which reads this definition.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class Sword : MonoBehaviour
    {
        public WeaponDefinition Definition;
    }
}
