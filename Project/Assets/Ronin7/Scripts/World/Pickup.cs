using System;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>A collectible relic. Grabbing it collects it (reuses the grab haptic).</summary>
    [RequireComponent(typeof(Grabbable))]
    public class Pickup : MonoBehaviour
    {
        public event Action<Pickup> Collected;

        private bool collected;

        private void Awake() => GetComponent<Grabbable>().Grabbed += OnGrabbed;

        private void OnGrabbed(Transform hand)
        {
            if (collected) return;
            collected = true;
            Collected?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
