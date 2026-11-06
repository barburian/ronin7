using UnityEngine;

namespace Ronin7.Flow
{
    /// <summary>A15.13/A15.16: the model shown on each WEAPON SELECT card, in card order (Katana, Double Katana,
    /// Polearm + Shield, Whip). Loaded from Resources so it ships in the Quest build. P9-04 swaps in the Tripo
    /// meshes by replacing an entry here: upright, +Y up, facing -Z; the panel centres and scales it to fit.</summary>
    public sealed class WeaponCardModels : ScriptableObject
    {
        public const string ResourcePath = "WeaponCardModels";
        [SerializeField] private GameObject[] models;

        public GameObject Get(int card) => models != null && card >= 0 && card < models.Length ? models[card] : null;
    }
}
