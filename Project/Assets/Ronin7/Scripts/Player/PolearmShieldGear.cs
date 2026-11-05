using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// P9-02c: the Polearm and Shield objects and their hip anchors, carried on the rig. Both start inactive;
    /// RunDirector.ApplyLoadout points the main / off KatanaHolsters at them for a polearm_shield run.
    /// </summary>
    public class PolearmShieldGear : MonoBehaviour
    {
        public Grabbable polearm;
        public Transform polearmAnchor;
        public Grabbable shield;
        public Transform shieldAnchor;

        public bool Complete => polearm != null && polearmAnchor != null && shield != null && shieldAnchor != null;
    }
}
