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

        [Header("Directional hit feedback")]
        [Tooltip("HMD transform. Falls back to Camera.main on the first hit.")]
        [SerializeField] private Transform head;
        [Tooltip("Haptic weight kept on the hand away from the hit, so both still feel it.")]
        [SerializeField, Range(0f, 1f)] private float farHandMin = 0.3f;
        [SerializeField] private Color hitFlashColor = new Color(0.8f, 0f, 0f, 0.6f);
        [SerializeField, Range(0f, 1f)] private float hitFlashStrength = 0.35f;
        [SerializeField] private float hitFlashSeconds = 0.25f;

        private float slowMoEndUnscaled = -1f;
        private Ronin7.Ship.ComfortVignette vignette;

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
            TimeScaleArbiter.SetRequest(TimeScaleChannel.DeflectSlowMo, deflectTimeScale);
            slowMoEndUnscaled = Time.unscaledTime + deflectRealSeconds;
        }

        private void OnPlayerHit(PlayerHit e)
        {
            if (head == null && Camera.main != null) head = Camera.main.transform; // once; Camera.main is cached by Unity
            if (head == null) { PulseBoth(0.6f, 0.18f); return; }

            Vector2 w = HitSideWeights(head.position, head.right, e.Source, farHandMin);
            Haptics.Pulse(XRNode.LeftHand, 0.6f * w.x, 0.18f);
            Haptics.Pulse(XRNode.RightHand, 0.6f * w.y, 0.18f);

            if (vignette == null) vignette = Ronin7.Ship.VignetteRig.Ensure(head.GetComponent<Camera>());
            if (vignette != null) vignette.Flash(hitFlashColor, hitFlashStrength, hitFlashSeconds);
        }

        /// <summary>(left, right) haptic weights for a hit from <paramref name="source"/>. The near hand gets
        /// 1, the far hand fades to <paramref name="farMin"/> as the hit goes fully lateral; dead ahead,
        /// behind, or a degenerate direction gives (1, 1).</summary>
        public static Vector2 HitSideWeights(Vector3 headPos, Vector3 headRight, Vector3 source, float farMin)
        {
            Vector3 dir = source - headPos;
            if (dir.sqrMagnitude < 1e-6f) return Vector2.one;
            float side = Vector3.Dot(dir.normalized, headRight.normalized); // -1 left .. +1 right
            float left = side > 0f ? Mathf.Lerp(1f, farMin, side) : 1f;
            float right = side < 0f ? Mathf.Lerp(1f, farMin, -side) : 1f;
            return new Vector2(left, right);
        }

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
            TimeScaleArbiter.ClearRequest(TimeScaleChannel.DeflectSlowMo);
            slowMoEndUnscaled = -1f;
        }

        private static void PulseBoth(float amplitude, float duration)
        {
            Haptics.Pulse(XRNode.LeftHand, amplitude, duration);
            Haptics.Pulse(XRNode.RightHand, amplitude, duration);
        }
    }
}
