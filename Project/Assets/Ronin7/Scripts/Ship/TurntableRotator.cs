using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>Slowly spins a transform about its local Y axis — used for the ship-select preview turntables.</summary>
    public class TurntableRotator : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 20f;

        void Update()
        {
            transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }
    }
}
