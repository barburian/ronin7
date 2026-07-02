using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.World
{
    /// <summary>
    /// EP21 pollen dreamscape mechanic: models scene "lucidity" (coherence) as the player
    /// is exposed to mind-altering pollen. Coherence is a [0,1] float where 1 = fully lucid
    /// and 0 = fully submerged. When coherence drops below a threshold, the "dreamscape engages"
    /// and fires a one-time event. The scene can be cleared (coherence restored to 1).
    /// </summary>
    public class PollenHazeController : MonoBehaviour
    {
        [SerializeField] private float hazeRiseRate = 0.1f;
        [SerializeField] private float hazeThreshold = 0.5f;
        [Tooltip("When true, Update() calls Tick(Time.deltaTime). Disable for deterministic tests.")]
        [SerializeField] private bool autoAdvance = true;

        public UnityEvent onHazeEngaged = new UnityEvent();
        public UnityEvent onCleared = new UnityEvent();

        private float coherence = 1f;
        private bool isEngaged;

        /// <summary>Scene coherence in [0,1]: 1 = fully lucid, 0 = fully submerged.</summary>
        public float Coherence => coherence;

        /// <summary>1 - Coherence; convenience read for "how much haze" is present.</summary>
        public float Haze => 1f - coherence;

        /// <summary>True after onHazeEngaged fires, until Clear() restores lucidity.</summary>
        public bool IsEngaged => isEngaged;

        private void Update()
        {
            if (autoAdvance)
            {
                Tick(Time.deltaTime);
            }
        }

        /// <summary>
        /// Reduce coherence by hazeRiseRate * dt. If not yet engaged and coherence
        /// drops below hazeThreshold, engage the dreamscape (set IsEngaged, fire onHazeEngaged once).
        /// </summary>
        public void Tick(float dt)
        {
            SetCoherence(coherence - hazeRiseRate * dt);
        }

        /// <summary>
        /// Set coherence to a specific value. Clamps to [0,1] and handles the
        /// engagement threshold check. Idempotent with respect to firing onHazeEngaged.
        /// </summary>
        public void SetCoherence(float v)
        {
            coherence = Mathf.Clamp01(v);

            // Check if we should engage the dreamscape (coherence now below threshold, not yet engaged)
            if (!isEngaged && coherence < hazeThreshold)
            {
                isEngaged = true;
                onHazeEngaged?.Invoke();
            }
        }

        /// <summary>
        /// Restore coherence to 1 and clear the dreamscape. If it was engaged,
        /// sets IsEngaged to false and fires onCleared exactly once.
        /// </summary>
        public void Clear()
        {
            bool wasEngaged = isEngaged;
            coherence = 1f;
            isEngaged = false;

            if (wasEngaged)
            {
                onCleared?.Invoke();
            }
        }
    }
}
