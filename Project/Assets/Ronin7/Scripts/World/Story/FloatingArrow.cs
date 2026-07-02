using UnityEngine;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Bobs and spins a marker (e.g. the arrow above an un-talked NPC) so it reads as an
    /// interactable target. Purely cosmetic — toggled off via <see cref="StoryNpc.MarkTalked"/>.
    /// </summary>
    public class FloatingArrow : MonoBehaviour
    {
        [SerializeField] private float bobAmplitude = 0.15f;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float spinDegreesPerSecond = 90f;

        private Vector3 baseLocalPos;

        private void Awake()
        {
            baseLocalPos = transform.localPosition;
        }

        private void Update()
        {
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.localPosition = baseLocalPos + new Vector3(0f, bob, 0f);
            transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
