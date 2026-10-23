using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Flow
{
    /// <summary>
    /// P5-08: decorative boot-room wall rack of the P4-08 loadout blades. Locked blades are dimmed via
    /// <see cref="RendererTint"/> (MPB, no material clones). Unlock state is read like the Shrine's LOADOUT tab
    /// (<see cref="MetaProgression.IsWeaponUnlocked"/>); nothing is saved.
    /// </summary>
    public sealed class LoadoutRackDisplay : MonoBehaviour
    {
        [SerializeField] private string[] weaponIds;     // parallel to blades
        [SerializeField] private GameObject[] blades;
        private static readonly Color LockedTint = new Color(0.12f, 0.12f, 0.15f);

        private void Start()
        {
            MetaProgression.EnsureLoaded();
            for (int i = 0; i < blades.Length && i < weaponIds.Length; i++)
            {
                if (blades[i] == null || MetaProgression.IsWeaponUnlocked(weaponIds[i])) continue;
                foreach (var r in blades[i].GetComponentsInChildren<Renderer>()) RendererTint.Apply(r, LockedTint);
            }
        }
    }
}
