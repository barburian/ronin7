using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// P9-02c: the Polearm and Shield objects and their hip anchors, carried on the rig. Both start inactive;
    /// RunDirector.ApplyLoadout points the main / off KatanaHolsters at them for a polearm_shield run.
    /// P9-02d: also the Whip handle and its hip anchor (main holster only; the off hand stays free).
    /// </summary>
    public class PolearmShieldGear : MonoBehaviour
    {
        public Grabbable polearm;
        public Transform polearmAnchor;
        public Grabbable shield;
        public Transform shieldAnchor;
        public Grabbable whip;
        public Transform whipAnchor;

        public bool Complete => polearm != null && polearmAnchor != null && shield != null && shieldAnchor != null;
        public bool WhipComplete => whip != null && whipAnchor != null;
    }
}
