using Ronin7.Core;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Read-only hub HUD panel mirroring <see cref="CampaignStats"/> onto a TextMesh. Refreshes at
    /// most once per second and is zero-alloc when the underlying stats haven't changed since the
    /// last refresh (cached-value idiom, see HeatMeter/EvacuationTimer).
    /// </summary>
    public class CampaignStatsDisplay : MonoBehaviour
    {
        [SerializeField] private TextMesh target;

        private float nextRefreshTime;
        private string lastDisplayed;

        private void Update()
        {
            if (Time.time < nextRefreshTime) return;
            nextRefreshTime = Time.time + 1f;

            string text = Format(CampaignStats.EnemiesDefeated, CampaignStats.PerfectParries,
                CampaignStats.PostureBreaks, CampaignStats.BestCombo);
            if (text == lastDisplayed) return;

            lastDisplayed = text;
            if (target != null) target.text = text;
        }

        /// <summary>Pure formatter — "DEFEATED n / PERFECT PARRIES n / GUARD BREAKS n / BEST COMBO n".</summary>
        public static string Format(int enemiesDefeated, int perfectParries, int postureBreaks, int bestCombo) =>
            $"DEFEATED {enemiesDefeated} / PERFECT PARRIES {perfectParries} / GUARD BREAKS {postureBreaks} / BEST COMBO {bestCombo}";
    }
}
