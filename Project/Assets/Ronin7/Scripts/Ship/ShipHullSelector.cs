using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Sits on the Cockpit GameObject. On Start, instantiates the player's chosen exterior hull
    /// (picked in the ship-select scene, persisted in ShipSelection) as a child at local identity so
    /// it wraps the cockpit and follows the cockpit transform (incl. CockpitRecenter) automatically.
    /// The 4 hull prefab references are wired at scene-build time by Galaxy1Builder.
    /// </summary>
    public class ShipHullSelector : MonoBehaviour
    {
        [SerializeField] private GameObject[] hullPrefabs; // index order matches ShipSelection / ShipHullPrefabPaths

        private void Start()
        {
            if (hullPrefabs == null || hullPrefabs.Length == 0) return;

            int index = Mathf.Clamp(ShipSelection.SelectedHullIndex, 0, hullPrefabs.Length - 1);
            var prefab = hullPrefabs[index];
            if (prefab == null) return;

            var hull = Instantiate(prefab, transform);
            hull.transform.localPosition = Vector3.zero;
            hull.transform.localRotation = Quaternion.identity;
            hull.transform.localScale = Vector3.one;
        }
    }
}
