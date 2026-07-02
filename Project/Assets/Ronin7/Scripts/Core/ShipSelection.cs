using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Persists the player's chosen ship hull across the scene load from the ship-select scene
    /// into the gameplay scene. PlayerPrefs-backed (key "ss.shipHull") so it also survives app restarts.
    /// </summary>
    public static class ShipSelection
    {
        // Ship-select collapsed to a single signature ship; this stays >0 only as the clamp bound for
        // any legacy saved index. ShipHullSelector re-clamps to the wired array (length 1) at runtime.
        public const int Count = 4;            // legacy clamp bound (selection removed)

        private const string KeyShipHull = "ss.shipHull";

        /// <summary>Index of the selected hull, always clamped to 0..Count-1.</summary>
        public static int SelectedHullIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeyShipHull, 0), 0, Count - 1);
            set
            {
                int clamped = Mathf.Clamp(value, 0, Count - 1);
                PlayerPrefs.SetInt(KeyShipHull, clamped);
                PlayerPrefs.Save();
            }
        }
    }
}
