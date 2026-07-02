using System;
using System.Collections.Generic;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Selects the mission objective (waypoint) based on campaign milestone completion.
    /// At Start, walks the entries in order and picks the LAST entry whose requiredCompletedScene
    /// is empty or completed, then calls ObjectiveArrowController.SetObjective with that target.
    /// </summary>
    public class CampaignObjectiveSelector : MonoBehaviour
    {
        [System.Serializable]
        public class ObjectiveEntry
        {
            [Tooltip("Campaign scene that must be completed to unlock this objective. Empty = always available.")]
            public string requiredCompletedScene = "";
            [Tooltip("The transform to aim the arrow at when this objective is active.")]
            public Transform objective;
        }

        [SerializeField] private List<ObjectiveEntry> entries = new();
        [SerializeField] private ObjectiveArrowController arrow;

        private void Start()
        {
            if (arrow == null)
            {
                Debug.LogError("[CampaignObjectiveSelector] No ObjectiveArrowController assigned.", this);
                return;
            }

            // Walk entries and pick the last one that is unlocked
            Transform selected = null;
            foreach (var entry in entries)
            {
                bool isUnlocked = string.IsNullOrEmpty(entry.requiredCompletedScene) ||
                                  CampaignState.IsCompleted(entry.requiredCompletedScene);
                if (isUnlocked)
                {
                    selected = entry.objective;
                }
            }

            if (selected != null)
            {
                arrow.SetObjective(selected);
            }
        }
    }
}
