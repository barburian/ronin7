using UnityEngine;
using Ronin7.Core;

namespace Ronin7.Player
{
    /// <summary>
    /// Keeps the sword sheathed at the hip whenever it is not held: snaps it back to the hip
    /// anchor on release (from any distance) so the player can never lose it in the world.
    /// </summary>
    public class KatanaHolster : MonoBehaviour
    {
        /// <summary>P4-08: child of the rig that carries the Twin Wakizashi's left-blade holster (and is its hip anchor).</summary>
        public const string OffhandAnchorName = "OffhandHolster";

        [SerializeField] private Grabbable sword;
        [SerializeField] private Transform hipAnchor;

        private Rigidbody swordBody;

        /// <summary>P4-04 S5 Blade Throw: while true the holster leaves a released sword alone (no snap back on
        /// Released, no safety-net resheathe). Set by <see cref="BladeThrowController"/> while armed or thrown.</summary>
        public bool SuppressResheathe { get; set; }

        public Grabbable Sword => sword;

        /// <summary>True while the sword rests on the hip anchor.</summary>
        public bool IsSheathed => sword != null && !sword.IsHeld && sword.transform.parent == hipAnchor;

        private void Awake()
        {
            if (sword != null && hipAnchor != null)
            {
                swordBody = sword.GetComponent<Rigidbody>();
                Resheathe();
                sword.Released += Resheathe;
            }
        }

        private void OnDestroy()
        {
            if (sword != null)
            {
                sword.Released -= Resheathe;
            }
        }

        /// <summary>P2-09 left-handed: put the hip anchor on the other hip. The builder places it at
        /// +x (right hip) for a right-handed player; left-handed mirrors it to -x. Idempotent.</summary>
        public void SetMirrored(bool mirrored)
        {
            if (hipAnchor == null) return;
            var p = hipAnchor.localPosition;
            p.x = mirrored ? -Mathf.Abs(p.x) : Mathf.Abs(p.x);
            hipAnchor.localPosition = p;
        }

        private void LateUpdate()
        {
            // Safety net: if the sword somehow ended up loose without a Released event.
            if (!SuppressResheathe && sword != null && hipAnchor != null && !sword.IsHeld && sword.transform.parent != hipAnchor)
            {
                Resheathe();
            }
        }

        private void Resheathe()
        {
            if (SuppressResheathe) return;
            sword.transform.SetParent(hipAnchor);
            sword.transform.localPosition = Vector3.zero;
            sword.transform.localRotation = Quaternion.identity;
            if (swordBody != null)
            {
                swordBody.isKinematic = true; // gravity must not pull it off the hip
            }
        }
    }
}
