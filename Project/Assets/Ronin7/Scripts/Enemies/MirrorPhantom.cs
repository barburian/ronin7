using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Ronin7.Combat;

namespace Ronin7.Enemies
{
    /// <summary>
    /// Mirror Maze mechanic: drives a SEQUENCE of phantom "self" copies that fight
    /// one at a time. Each phantom is a separate GameObject with Health; when the current
    /// dies, the next activates. The final phantom applies a scale reduction and its death
    /// fires onSequenceCleared exactly once.
    /// </summary>
    public class MirrorPhantom : MonoBehaviour
    {
        [SerializeField] private List<Health> phantoms = new();
        [Tooltip("Scale applied to the final phantom's transform when it activates.")]
        [SerializeField] private float finalChildScale = 0.6f;

        public UnityEvent onSequenceCleared = new UnityEvent();

        public int ActiveIndex { get; private set; }

        public bool IsFinalChildActive =>
            phantoms != null && phantoms.Count > 0 && ActiveIndex == phantoms.Count - 1;

        private bool sequenceCleared;

        private void OnEnable() => Begin();

        public void Begin()
        {
            if (phantoms == null || phantoms.Count == 0) return;

            sequenceCleared = false;
            ActiveIndex = 0;

            // Deactivate all except index 0.
            for (int i = 0; i < phantoms.Count; i++)
            {
                if (phantoms[i] != null && phantoms[i].gameObject != null)
                {
                    phantoms[i].gameObject.SetActive(i == 0);
                }
            }

            // Subscribe to the first phantom's death.
            if (phantoms[0] != null)
            {
                phantoms[0].Died += OnPhantomDied;
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromAll();
        }

        private void OnPhantomDied()
        {
            // Unsubscribe from and dissolve (deactivate) the phantom that just died.
            if (ActiveIndex < phantoms.Count && phantoms[ActiveIndex] != null)
            {
                phantoms[ActiveIndex].Died -= OnPhantomDied;
                if (phantoms[ActiveIndex].gameObject != null)
                {
                    phantoms[ActiveIndex].gameObject.SetActive(false);
                }
            }

            // Advance to the next phantom.
            ActiveIndex++;

            // If there's a next phantom, activate it.
            if (ActiveIndex < phantoms.Count && phantoms[ActiveIndex] != null)
            {
                Health nextPhantom = phantoms[ActiveIndex];
                nextPhantom.gameObject.SetActive(true);

                // If this is the final phantom, apply the scale reduction.
                if (IsFinalChildActive)
                {
                    nextPhantom.transform.localScale *= finalChildScale;
                }

                // Subscribe to the next phantom's death.
                nextPhantom.Died += OnPhantomDied;
            }
            else if (!sequenceCleared)
            {
                // All phantoms are dead; fire the one-time event.
                sequenceCleared = true;
                onSequenceCleared?.Invoke();
            }
        }

        private void UnsubscribeFromAll()
        {
            if (phantoms == null) return;
            foreach (var h in phantoms)
            {
                if (h != null) h.Died -= OnPhantomDied;
            }
        }
    }
}
