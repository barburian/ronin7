using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>The full set of boons a run can offer, authored as one asset for the offer picker to draw from.</summary>
    [CreateAssetMenu(menuName = "Ronin 7/Boon Catalog", fileName = "BoonCatalog")]
    public class BoonCatalog : ScriptableObject
    {
        public BoonDefinition[] boons;

        /// <summary>Linear lookup by <see cref="BoonDefinition.id"/>. Null id or no match returns null.</summary>
        public BoonDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id) || boons == null) return null;
            foreach (var boon in boons)
            {
                if (boon != null && boon.id == id) return boon;
            }
            return null;
        }
    }
}
