using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Player;
using Ronin7.Ship;
using UnityEngine;

namespace Ronin7.Flow
{
    /// <summary>
    /// Bridges combat EventBus events into <see cref="CampaignStats"/>. Presence-in-scene is the
    /// switch (mirrors the <c>Ronin7.Ship.SunHeatDamage</c>/<c>AsteroidHazard</c> precedent) — drop one
    /// instance anywhere persistent (e.g. alongside <see cref="GameFlowManager"/>) and every subsequent
    /// kill/perfect-parry/posture-break/combo-peak/kill-streak-peak/near-miss-streak-peak/parry-streak-peak
    /// counts toward the campaign stats.
    ///
    /// Player-death exclusion mirrors <see cref="GameFlowManager.OnEntityDied"/>'s own test:
    /// <c>EntityDied</c> fires for both the on-foot player rig and enemies through the same Health
    /// component, so a death only counts as an enemy kill when it is NOT the VRRig instance.
    /// </summary>
    public class CampaignStatsTracker : MonoBehaviour
    {
        private void OnEnable()
        {
            EventBus.Subscribe<EntityDied>(OnEntityDied);
            EventBus.Subscribe<PerfectParry>(OnPerfectParry);
            EventBus.Subscribe<PostureBroken>(OnPostureBroken);
            EventBus.Subscribe<ComboChained>(OnComboChained);
            EventBus.Subscribe<KillStreakAdvanced>(OnKillStreakAdvanced);
            EventBus.Subscribe<NearMissStreakAdvanced>(OnNearMissStreakAdvanced);
            EventBus.Subscribe<ParryStreakAdvanced>(OnParryStreakAdvanced);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EntityDied>(OnEntityDied);
            EventBus.Unsubscribe<PerfectParry>(OnPerfectParry);
            EventBus.Unsubscribe<PostureBroken>(OnPostureBroken);
            EventBus.Unsubscribe<ComboChained>(OnComboChained);
            EventBus.Unsubscribe<KillStreakAdvanced>(OnKillStreakAdvanced);
            EventBus.Unsubscribe<NearMissStreakAdvanced>(OnNearMissStreakAdvanced);
            EventBus.Unsubscribe<ParryStreakAdvanced>(OnParryStreakAdvanced);
        }

        private void OnEntityDied(EntityDied evt)
        {
            if (VRRig.Instance != null && evt.Entity == VRRig.Instance.gameObject) return;
            // Practice targets don't count as defeats — a player grinding the dojo dummy to zero
            // outside a drill would otherwise inflate the career stat. String-based GetComponent
            // keeps Flow decoupled from the Ronin7.Enemies assembly (rare event, cost is fine).
            if (evt.Entity != null && evt.Entity.GetComponent("TrainingDummy") != null) return;
            CampaignStats.RecordEnemyDefeated();
        }

        private void OnPerfectParry(PerfectParry _) => CampaignStats.RecordPerfectParry();

        private void OnPostureBroken(PostureBroken _) => CampaignStats.RecordPostureBreak();

        private void OnComboChained(ComboChained evt) => CampaignStats.RecordCombo(evt.Count);

        private void OnKillStreakAdvanced(KillStreakAdvanced evt) => CampaignStats.RecordKillStreak(evt.Count);

        private void OnNearMissStreakAdvanced(NearMissStreakAdvanced evt) => CampaignStats.RecordNearMissStreak(evt.Count);

        private void OnParryStreakAdvanced(ParryStreakAdvanced evt) => CampaignStats.RecordParryStreak(evt.Count);
    }
}
