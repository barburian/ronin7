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
        [SerializeField] private Grabbable sword;
        [SerializeField] private Transform hipAnchor;

        private Rigidbody swordBody;

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

        private void LateUpdate()
        {
            // Safety net: if the sword somehow ended up loose without a Released event.
            if (sword != null && hipAnchor != null && !sword.IsHeld && sword.transform.parent != hipAnchor)
            {
                Resheathe();
            }
        }

        private void Resheathe()
        {
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
