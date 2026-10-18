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

        [Header("P4-05 archetype")]
        public BoonTag tags;
        [Tooltip("Duo only: BoonTag names that must ALL be owned before this boon is offered.")]
        public string[] requiresTags;
        public bool hasSecondary;
        public BoonEffectKind secondaryEffect;
        [Tooltip("Same per-stack meaning as magnitude, for secondaryEffect. Curses carry a negative one.")]
        public float secondaryMagnitude;
        [Tooltip("Filled in P5.")]
        public Sprite icon;

        /// <summary><see cref="requiresTags"/> as a mask. False when any name is not a single <see cref="BoonTag"/>
        /// (a mis-authored Duo is then never offerable rather than always offerable).</summary>
        public bool TryGetRequiredTags(out BoonTag mask)
        {
            mask = BoonTag.None;
            if (requiresTags == null) return true;
            foreach (var name in requiresTags)
            {
                if (!System.Enum.TryParse(name, false, out BoonTag tag) || tag == BoonTag.None
                    || ((int)tag & ((int)tag - 1)) != 0) return false;
                mask |= tag;
            }
            return true;
        }
    }

    /// <summary>P4-05 archetype tags. APPEND-ONLY flags (assets serialize the mask).</summary>
    [System.Flags]
    public enum BoonTag
    {
        None = 0,
        Parry = 1,
        Flow = 2,
        Bleed = 4,
        Storm = 8,
        Executioner = 16,
        Ability = 32,
        Bulwark = 64,
        Curse = 128,
    }
}
