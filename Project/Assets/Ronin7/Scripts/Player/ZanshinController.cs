using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>P4-04 S3 Zanshin (pure): a perfect parry slows time to <see cref="Scale"/> for
    /// <see cref="RealSeconds"/> real seconds, at most once per <see cref="Cooldown"/> s (AbilityHaste scales it).</summary>
    public class ZanshinLogic
    {
        public const float Scale = 0.4f, RealSeconds = 0.6f, Cooldown = 5f;

        private float readyAt = float.NegativeInfinity;
        private float endAt = -1f;

        public bool Active => endAt >= 0f;

        public bool TryTrigger(float now, float cooldownScale)
        {
            if (now < readyAt) return false;
            readyAt = now + Cooldown * cooldownScale;
            endAt = now + RealSeconds;
            return true;
        }

        public bool Expired(float now) => Active && now >= endAt;

        public void End() => endAt = -1f;
    }

    /// <summary>
    /// P4-04 S3 Zanshin (passive, boon-granted via <c>AbilityId.Zanshin</c>). Requests
    /// <see cref="TimeScaleChannel.Zanshin"/> through the arbiter (MIN with DeflectSlowMo / WeaknessTeach /
    /// Pause, never a direct timeScale write) and releases it on expiry, disable, app pause and mode change.
    /// </summary>
    public class ZanshinController : MonoBehaviour
    {
        private readonly ZanshinLogic logic = new ZanshinLogic();
        private PlayerCombatModifiers mods;

        private void Awake()
        {
            if (!AbilityAccess.Has(AbilityId.Zanshin)) enabled = false;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PerfectParry>(OnPerfectParry);
            EventBus.Subscribe<GameModeChanged>(OnModeChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PerfectParry>(OnPerfectParry);
            EventBus.Unsubscribe<GameModeChanged>(OnModeChanged);
            Release();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Release();
        }

        private void OnModeChanged(GameModeChanged _) => Release();

        private void Update()
        {
            if (logic.Expired(Time.unscaledTime)) Release();
        }

        internal void OnPerfectParry(PerfectParry _)
        {
            if (mods == null) mods = GetComponentInParent<PlayerCombatModifiers>();
            if (!logic.TryTrigger(Time.unscaledTime, SkillRig.CooldownScale(mods))) return;
            TimeScaleArbiter.SetRequest(TimeScaleChannel.Zanshin, ZanshinLogic.Scale);
        }

        private void Release()
        {
            logic.End();
            TimeScaleArbiter.ClearRequest(TimeScaleChannel.Zanshin);
        }
    }
}
