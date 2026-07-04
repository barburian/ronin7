using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>
    /// Turns combat events into "juice": a brief slow-mo + strong rumble on a successful
    /// deflect, rumble on taking a hit, and a light tick when the blade connects. Lives in
    /// Player so it can use the Haptics helper (Combat must not depend on Player).
    /// </summary>
    public class CombatFeedbackController : MonoBehaviour
    {
        [Header("Deflect slow-mo")]
        [SerializeField] private float deflectTimeScale = 0.25f;
        [SerializeField] private float deflectRealSeconds = 0.15f;

        private float defaultFixedDelta;
        private float slowMoEndUnscaled = -1f;

        private void Awake() => defaultFixedDelta = Time.fixedDeltaTime;

        private void OnEnable()
        {
            EventBus.Subscribe<SwordDeflected>(OnDeflect);
            EventBus.Subscribe<PlayerHit>(OnPlayerHit);
            EventBus.Subscribe<SwordImpact>(OnImpact);
            EventBus.Subscribe<PostureBroken>(OnPostureBroken);
            EventBus.Subscribe<PostureNearBreak>(OnPostureNearBreak);
            EventBus.Subscribe<GameModeChanged>(OnModeChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SwordDeflected>(OnDeflect);
            EventBus.Unsubscribe<PlayerHit>(OnPlayerHit);
            EventBus.Unsubscribe<SwordImpact>(OnImpact);
            EventBus.Unsubscribe<PostureBroken>(OnPostureBroken);
            EventBus.Unsubscribe<PostureNearBreak>(OnPostureNearBreak);
            EventBus.Unsubscribe<GameModeChanged>(OnModeChanged);
            RestoreTime();
        }

        // Quest dashboard / focus loss can pause us mid-slowmo; without this, resume leaves
        // Time.timeScale stuck at 0.25.
        private void OnApplicationPause(bool paused)
        {
            if (paused) RestoreTime();
        }

        // Cancel any in-flight slowmo when the game mode changes — a scene transition
        // (e.g. Phase4 → Phase7) mid-effect would otherwise carry 0.25× into the next scene.
        private void OnModeChanged(GameModeChanged e) => RestoreTime();

        private void Update()
        {
            if (slowMoEndUnscaled > 0f && Time.unscaledTime >= slowMoEndUnscaled)
                RestoreTime();
        }

        private void OnDeflect(SwordDeflected e)
        {
            PulseBoth(0.8f, 0.12f);
            Time.timeScale = deflectTimeScale;
            Time.fixedDeltaTime = defaultFixedDelta * deflectTimeScale;
            slowMoEndUnscaled = Time.unscaledTime + deflectRealSeconds;
        }

        private void OnPlayerHit(PlayerHit e) => PulseBoth(0.6f, 0.18f);

        private void OnImpact(SwordImpact e) => PulseBoth(0.35f, 0.05f);

        // Strongest/longest pulse of the set — a posture break is the biggest combat payoff, so it
        // should read as distinctly bigger than the deflect pulse. No slow-mo here (unlike OnDeflect):
        // the break's own stagger/bonus-damage feedback already lands, timescale is not this event's job.
        private void OnPostureBroken(PostureBroken e) => PulseBoth(1.0f, 0.2f);

        // Short, weak tick — "one more hit and this enemy staggers". Clearly below the break's
        // 1.0f/0.2f pulse so the player never confuses the warning for the payoff itself.
        private void OnPostureNearBreak(PostureNearBreak e) => PulseBoth(0.4f, 0.06f);

        private void RestoreTime()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = defaultFixedDelta;
            slowMoEndUnscaled = -1f;
        }

        private static void PulseBoth(float amplitude, float duration)
        {
            Haptics.Pulse(XRNode.LeftHand, amplitude, duration);
            Haptics.Pulse(XRNode.RightHand, amplitude, duration);
        }
    }
}
