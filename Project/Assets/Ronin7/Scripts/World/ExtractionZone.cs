using System;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// The "return to ship" pad. Starts locked (red); once the zone objective is met it
    /// activates (green) and the player extracts by standing on it briefly.
    /// </summary>
    public class ExtractionZone : MonoBehaviour
    {
        [SerializeField] private Renderer pad;
        [SerializeField] private float dwellSeconds = 1.5f;

        public bool Active { get; private set; }
        public event Action Extracted;

        private bool playerInside;
        private float dwell;
        private bool done;

        private static readonly Color Locked = new Color(0.6f, 0.12f, 0.12f);
        private static readonly Color Ready = new Color(0.15f, 0.85f, 0.25f);

        private void Awake() => SetColor(Locked);

        public void Activate()
        {
            Active = true;
            SetColor(Ready);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other)) playerInside = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other)) { playerInside = false; dwell = 0f; }
        }

        private void Update()
        {
            if (done || !Active || !playerInside) return;
            dwell += Time.deltaTime;
            if (dwell >= dwellSeconds)
            {
                done = true;
                Extracted?.Invoke();
            }
        }

        private static bool IsPlayer(Collider other) =>
            other.GetComponentInParent<CharacterController>() != null;

        private void SetColor(Color c) { RendererTint.Apply(pad, c); }
    }
}
