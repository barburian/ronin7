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
        private Grabbable homeSword;
        private Transform homeAnchor;
        private bool? mirrored; // last SetMirrored request (true = anchor on the -x side); null = never asked

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
                sword.Released -= Resheathe; // Equip may have subscribed already
                sword.Released += Resheathe;
            }
        }

        /// <summary>P9-02c: points this holster at another object and anchor (the Polearm / Shield loadout).
        /// Moves the Released subscription and cached Rigidbody, applies the current mirror state to the new
        /// anchor and sheathes the object. The first call remembers the scene's own sword for <see cref="EquipHome"/>.
        /// The caller owns which objects are active.</summary>
        public void Equip(Grabbable gear, Transform anchor)
        {
            if (gear == null || anchor == null) return;
            if (homeSword == null) { homeSword = sword; homeAnchor = hipAnchor; }
            if (sword != null) sword.Released -= Resheathe;
            sword = gear;
            hipAnchor = anchor;
            swordBody = gear.GetComponent<Rigidbody>();
            if (mirrored.HasValue) ApplySide(anchor, mirrored.Value);
            Resheathe();
            sword.Released += Resheathe;
        }

        /// <summary>P9-02c: back to the sword and anchor the scene shipped with. No-op if never re-pointed.</summary>
        public void EquipHome()
        {
            if (homeSword != null) Equip(homeSword, homeAnchor);
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
            this.mirrored = mirrored;
            ApplySide(hipAnchor, mirrored);
        }

        /// <summary>Puts <paramref name="anchor"/> on the -x (<paramref name="negativeSide"/>) or +x hip. Crossing sides
        /// also mirrors its rotation (a diagonal pole or flat shield must flip with it); the side it is on now is
        /// read from the position, so repeated calls are idempotent. An identity rotation (the katana hip) is unchanged.</summary>
        internal static void ApplySide(Transform anchor, bool negativeSide)
        {
            if (anchor == null) return;
            var p = anchor.localPosition;
            bool wasNegative = p.x < 0f;
            p.x = negativeSide ? -Mathf.Abs(p.x) : Mathf.Abs(p.x);
            anchor.localPosition = p;
            if (wasNegative != negativeSide) anchor.localRotation = MirrorX(anchor.localRotation);
        }

        /// <summary>Reflection of a rotation through the x = 0 plane: (x, -y, -z, w).</summary>
        internal static Quaternion MirrorX(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);

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
