using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>
    /// Designer-tunable data for a single boon. <see cref="id"/> is one of the <c>Ronin7.Core.BoonId</c>
    /// constants — <see cref="BoonCatalog"/> looks definitions up by it, and <c>Ronin7.Core.RunState</c>
    /// persists held boons as these same id strings so a run can track counts without referencing
    /// ScriptableObjects.
    /// </summary>
    [CreateAssetMenu(menuName = "Ronin 7/Boon Definition", fileName = "BoonDefinition")]
    public class BoonDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("A Ronin7.Core.BoonId constant. Must be stable — it's what RunState/BoonInventory key on.")]
        public string id;
        public string displayName;
        [TextArea]
        public string description;
        public BoonRarity rarity;

        [Header("Effect")]
        public BoonEffectKind effect;
        [Tooltip("Interpreted per-effect — see BoonEffectKind's doc comments.")]
        public float magnitude;
        [Tooltip("Only read when effect == GrantAbility. A Ronin7.Core.AbilityId constant.")]
        public string abilityId;
        [Tooltip("How many times this boon can be held at once. GrantAbility/ReviveOnce should use 1.")]
        public int maxStacks = 3;
    }
}
