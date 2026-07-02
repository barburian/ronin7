using Ronin7.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// MonoBehaviour for the main-menu LOAD GAME section: shows each slot's summary and disables empty slots.
    /// Wired by the boot-scene builder.
    /// </summary>
    public class SaveSlotMenuView : MonoBehaviour
    {
        [SerializeField] private Button[] slotButtons = new Button[3];
        [SerializeField] private Text[] slotLabels = new Text[3];

        private void OnEnable()
        {
            Refresh();
        }

        /// <summary>Update slot labels and enable/disable buttons based on slot occupancy.</summary>
        public void Refresh()
        {
            for (int i = 0; i < Mathf.Min(slotButtons.Length, slotLabels.Length); i++)
            {
                int slot = i + 1;
                if (slotLabels[i] != null)
                    slotLabels[i].text = $"SLOT {slot} — {SaveSystem.SlotSummary(slot).ToUpperInvariant()}";
                if (slotButtons[i] != null)
                    slotButtons[i].interactable = SaveSystem.SlotExists(slot);
            }
        }
    }
}
