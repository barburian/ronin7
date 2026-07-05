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
    /// kill/perfect-parry/posture-break/combo-peak/kill-streak-peak/near-miss-streak-peak/parry-streak-peak/
    /// flawless-encounter counts toward the campaign stats.
    ///
    /// Player-death exclusion mirrors <see cref="GameFlowManager.OnEntityDied"/>'s own test:
    /// <c>EntityDied</c> fires for both the on-foot player rig and enemies through the same Health
    /// component, so a death only counts as an enemy kill when it is NOT the VRRig instance.
    ///
    /// COMBO / KILL-STREAK / PARRY-STREAK ARE COMPUTED HERE, not consumed from the buff mechanics'
    /// events: <c>ComboMomentumController</c>, <c>AdrenalineFlow</c> and <c>ParryFlowController</c> are
    /// per-scene opt-in BUFFS (damage/heal) that are absent from the hub and the whole Ch2–Ch6 arc
    /// (AdrenalineFlow from every scene), so subscribing to their ComboChained/KillStreakAdvanced/
    /// ParryStreakAdvanced left BEST COMBO / BEST KILL STREAK / BEST PARRY STREAK frozen at 0 on the
    /// hub stats board for most of the campaign. The tracker derives those stats from the primitive
    /// events every combat scene publishes (<see cref="SwordImpact"/>, <see cref="EntityDied"/>,
    /// <see cref="PerfectParry"/>/<see cref="PlayerHit"/>), using the same locked-in chain schemes and
    /// default windows as the mechanics, so the stats track everywhere while the buffs stay
    /// scene-gated. BEST NEAR MISS STREAK is the exception: a near miss only exists where
    /// <c>TrickBoostController</c>'s asteroid scan runs, so <see cref="NearMissStreakAdvanced"/>
    /// remains its only possible source.
    /// </summary>
    public class CampaignStatsTracker : MonoBehaviour
    {
        // Windows/caps mirror the buff mechanics' locked-in defaults (ComboMomentumController,
        // AdrenalineFlow, ParryFlowController) so a Ch7+ scene's visible buff and the stat recorded
        // here can't disagree.
        private const float ComboWindowSeconds = 1.2f;
        private const int MaxCombo = 4;
        private const float KillWindowSeconds = 4f;
        private const int MaxKillStreak = 6;
        private const int MaxParryStreak = 5;
        private const float ParryIdleResetSeconds = 6f;

        private int comboCount;
        private int comboLastTargetId;
        private float comboLastHitTime;
        private int killStreak;
        private float lastKillTime;
        private int parryStreak;
        private float lastParryUnscaledTime = float.NegativeInfinity;

        private void OnEnable()
        {
            EventBus.Subscribe<EntityDied>(OnEntityDied);
            EventBus.Subscribe<PerfectParry>(OnPerfectParry);
            EventBus.Subscribe<PostureBroken>(OnPostureBroken);
            EventBus.Subscribe<SwordImpact>(OnSwordImpact);
            EventBus.Subscribe<PlayerHit>(OnPlayerHit);
            EventBus.Subscribe<NearMissStreakAdvanced>(OnNearMissStreakAdvanced);
            EventBus.Subscribe<FlawlessEncounterCleared>(OnFlawlessEncounterCleared);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EntityDied>(OnEntityDied);
            EventBus.Unsubscribe<PerfectParry>(OnPerfectParry);
            EventBus.Unsubscribe<PostureBroken>(OnPostureBroken);
            EventBus.Unsubscribe<SwordImpact>(OnSwordImpact);
            EventBus.Unsubscribe<PlayerHit>(OnPlayerHit);
            EventBus.Unsubscribe<NearMissStreakAdvanced>(OnNearMissStreakAdvanced);
            EventBus.Unsubscribe<FlawlessEncounterCleared>(OnFlawlessEncounterCleared);
        }

        private void OnEntityDied(EntityDied evt)
        {
            if (VRRig.Instance != null && evt.Entity == VRRig.Instance.gameObject) return;
            // Practice targets don't count as defeats — a player grinding the dojo dummy to zero
            // outside a drill would otherwise inflate the career stat. String-based GetComponent
            // keeps Flow decoupled from the Ronin7.Enemies assembly (rare event, cost is fine).
            if (evt.Entity != null && evt.Entity.GetComponent("TrainingDummy") != null) return;
            // NPC-vs-NPC kills are not player feats: a gang-war brawler downing its rival, hive
            // friendly fire, or an enemy killing a protected NPC must not advance career defeats or
            // the kill streak (the player can just stand and watch Ch10's tier-4 brawl). The killer
            // is the lethal DamageInfo's Source; combatant sources carry MeleeAttacker (enemies,
            // dummies) or FactionCombatant. Same string-GetComponent decoupling idiom as above.
            if (evt.Killer != null &&
                (evt.Killer.GetComponent("MeleeAttacker") != null || evt.Killer.GetComponent("FactionCombatant") != null))
                return;
            CampaignStats.RecordEnemyDefeated();

            // Kill streak (same guards as the defeat stat: no player death, no practice dummies).
            killStreak = NextKillStreak(killStreak, lastKillTime, Time.time, KillWindowSeconds, MaxKillStreak);
            lastKillTime = Time.time;
            CampaignStats.RecordKillStreak(killStreak);
        }

        private void OnPerfectParry(PerfectParry evt)
        {
            CampaignStats.RecordPerfectParry();

            // Idle decay is lazy: only the NEXT parry needs to know whether the streak survived,
            // so an expiry check here replaces ParryFlowController's proactive Update() reset.
            // Unscaled time for the same reason as the mechanic: slow-mo must not stretch the window.
            if (Time.unscaledTime - lastParryUnscaledTime >= ParryIdleResetSeconds) parryStreak = 0;
            parryStreak = ParryTiming.NextStreak(parryStreak, evt.Quality, MaxParryStreak);
            lastParryUnscaledTime = Time.unscaledTime;
            CampaignStats.RecordParryStreak(parryStreak);
        }

        private void OnPlayerHit(PlayerHit _) => parryStreak = 0; // getting hit ends the flow state

        private void OnPostureBroken(PostureBroken _) => CampaignStats.RecordPostureBreak();

        private void OnSwordImpact(SwordImpact evt)
        {
            // Practice targets don't build the career combo either (mirrors OnEntityDied's dummy
            // exclusion — alternating swings between two dojo dummies would otherwise farm BEST COMBO).
            if (evt.Victim != null && evt.Victim.GetComponent("TrainingDummy") != null) return;

            int targetId = evt.Victim != null ? evt.Victim.GetEntityId().GetHashCode() : 0;
            (comboCount, comboLastTargetId) =
                NextCombo(comboCount, comboLastTargetId, comboLastHitTime, targetId, Time.time, ComboWindowSeconds, MaxCombo);
            comboLastHitTime = Time.time;
            CampaignStats.RecordCombo(comboCount);
        }

        private void OnNearMissStreakAdvanced(NearMissStreakAdvanced evt) => CampaignStats.RecordNearMissStreak(evt.Count);

        private void OnFlawlessEncounterCleared(FlawlessEncounterCleared _) => CampaignStats.RecordFlawlessEncounter();

        /// <summary>
        /// Pure combo-chain transition, mirroring <c>ComboMomentumController.RegisterHit</c>'s
        /// locked-in scheme exactly: a fresh/expired hit starts a chain at 1, a different target
        /// within the window extends (capped), the same target punishes the chain back to 0.
        /// Internal + unit-tested.
        /// </summary>
        internal static (int count, int lastTargetId) NextCombo(
            int count, int lastTargetId, float lastHitTime, int targetId, float now, float window, int maxCombo)
        {
            bool freshOrExpired = count == 0 || now - lastHitTime >= window;
            if (freshOrExpired) return (1, targetId);
            if (targetId == lastTargetId) return (0, targetId);
            return (Mathf.Min(count + 1, maxCombo), targetId);
        }

        /// <summary>
        /// Pure kill-streak transition, mirroring <c>AdrenalineFlow.RegisterKill</c>'s semantics
        /// exactly: a kill within the window extends the streak (capped), otherwise the streak
        /// restarts at 1. Internal + unit-tested.
        /// </summary>
        internal static int NextKillStreak(int count, float lastKillTime, float now, float window, int maxStreak)
        {
            bool withinWindow = count > 0 && now - lastKillTime <= window;
            return withinWindow ? Mathf.Min(count + 1, maxStreak) : 1;
        }
    }
}
