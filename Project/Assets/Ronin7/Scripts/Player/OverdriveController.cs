using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Player
{
    /// <summary>
    /// Ch9 ("The Pit and the Deep") permanent ability: Vane/Wraith-6's freed blade-shadow, inherited by
    /// Echo on the kill, lets Echo spike Cipher into a hyper-reflex burst where the world appears frozen
    /// in time. Self-disables in <see cref="Awake"/> unless
    /// <c>CampaignState.HasAbility(AbilityId.Overdrive)</c> — mirrors <see cref="WeakpointSight"/> — so
    /// it is harmless to place on the rig in every scene, locked or not.
    ///
    /// CHARGE: builds from landed sword hits via <see cref="Ronin7.Combat.SwordImpact"/> (published only
    /// by <see cref="BladeDamager"/>, which lives on the player's own blade; enemies deal damage through
    /// <c>MeleeAttacker.LandHit</c> directly and never publish this event, so every impact this class
    /// sees is a hit Cipher landed). Drains only while active (see <see cref="OverdriveLogic"/>).
    ///
    /// INPUT: <see cref="activateAction"/> is wired at build time to a dedicated "Activate Overdrive"
    /// action on the right controller's primary/A button with a Hold(0.4s) interaction — A also carries
    /// Dash as a Tap on the same control (see <c>ContinuousLocomotion</c>'s dash read), so a quick tap
    /// dashes and a held press activates the burst once charge allows it. Read via
    /// <c>WasPerformedThisFrame</c> so the Hold interaction is respected.
    ///
    /// TIME-SCALE EFFECT: while active, time is slowed to <see cref="overdriveTimeScale"/> via a
    /// <see cref="TimeScaleArbiter"/> request on the <c>Overdrive</c> channel — the arbiter (not this
    /// class) owns the canonical baseline fixedDeltaTime and composes overlapping slow-mo effects (e.g.
    /// the deflect slow-mo) by taking the minimum scale, so this class never touches
    /// <c>Time.timeScale</c>/<c>Time.fixedDeltaTime</c> directly. The request is released on natural end
    /// (charge empties) AND, as failsafes, in <see cref="OnDisable"/> and <see cref="OnDestroy"/> —
    /// belt-and-suspenders, because a lingering active request surviving a scene teardown would be
    /// catastrophic (every subsequent scene running slow or frozen). Publishes
    /// <see cref="AbilityActivated"/> on activation so <c>EchoPresence</c> reacts, per that event's
    /// documented contract.
    ///
    /// VR-COMFORT (Ch9 unscaled-time audit): the charge/drain timer and the wrist meter both tick on
    /// <c>Time.unscaledDeltaTime</c>, so the burst lasts a fixed REAL duration (not 1/overdriveTimeScale
    /// as long, which is what scaled deltaTime would give once the effect is slowing the very clock that
    /// times it) and the meter animates smoothly instead of crawling during its own effect. See
    /// <c>EchoPresence</c> and <c>DialoguePlayer</c> for the other files converted in this same audit.
    ///
    /// WRIST UI: a small wrist-anchored quad + percent readout, mirroring
    /// <c>Ronin7.World.HeatMeter</c>'s "pure logic + component" shape (green->violet as charge fills,
    /// bright cyan flash while active) — built only after the ability-gate passes (mirrors
    /// <see cref="WeakpointSight"/>'s marker-pool gating), so a locked rig never spends the cost of it.
    /// </summary>
    public class OverdriveController : MonoBehaviour
    {
        [SerializeField] private InputActionReference activateAction;
        [SerializeField] private float chargePerHit = 0.2f;
        [SerializeField] private float drainPerSecond = 0.34f; // ~3s burst once fully charged
        [SerializeField] private float activationThreshold = 1f;
        [SerializeField] private float overdriveTimeScale = 0.35f;

        [Header("Wrist UI")]
        [Tooltip("Wrist transform the meter quad follows. Assigned by the builder.")]
        [SerializeField] private Transform anchor;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.02f, 0.06f);

        private OverdriveLogic logic;
        private bool timeScaleApplied;

        private Renderer quadRenderer;
        private TextMesh percentText;
        private int lastDisplayedPercent = -1;

        /// <summary>True while the time-dilation burst is running.</summary>
        public bool IsActive => logic != null && logic.IsActive;

        /// <summary>Current charge (0..1) for test/inspection use.</summary>
        public float Charge => logic != null ? logic.Charge : 0f;

        private void Awake()
        {
            if (!CampaignState.HasAbility(AbilityId.Overdrive))
            {
                enabled = false;
                return;
            }

            logic = new OverdriveLogic(chargePerHit, drainPerSecond, activationThreshold);
            BuildWristUi();
        }

        private void OnEnable()
        {
            activateAction?.action?.Enable();
            EventBus.Subscribe<SwordImpact>(OnSwordImpact);
        }

        private void OnDisable()
        {
            activateAction?.action?.Disable();
            EventBus.Unsubscribe<SwordImpact>(OnSwordImpact);
            // Failsafe #1: a lingering slow timeScale surviving this component going away (scene
            // teardown, chapter transition) would silently slow or freeze every scene after it.
            RestoreTimeScale();
            logic?.Deactivate();
        }

        private void OnDestroy()
        {
            // Failsafe #2: belt-and-suspenders alongside OnDisable (Unity always calls OnDisable before
            // OnDestroy, so this is normally a no-op — kept anyway per the Ch9 timeScale audit).
            RestoreTimeScale();
        }

        // Failsafe #3: Quest dashboard / focus loss can pause us mid-burst; without this, resume
        // leaves the arbiter's Overdrive request active indefinitely (same failure mode
        // CombatFeedbackController already guards against for the deflect channel).
        private void OnApplicationPause(bool paused)
        {
            if (paused) RestoreTimeScale();
        }

        private void OnSwordImpact(SwordImpact evt) => logic?.AddCharge();

        private void Update()
        {
            if (logic == null) return;

            var action = activateAction != null ? activateAction.action : null;
            if (!logic.IsActive && action != null && action.WasPerformedThisFrame())
            {
                if (logic.TryActivate())
                {
                    ApplyTimeScale();
                    EventBus.Publish(new AbilityActivated(AbilityId.Overdrive));
                }
            }

            if (logic.IsActive)
            {
                // Unscaled: the burst must hold a fixed REAL duration, not one stretched by the very
                // time-slow it's timing (Ch9 comfort/UI audit — see the class summary).
                if (logic.Tick(Time.unscaledDeltaTime))
                {
                    // Failsafe #3: the natural end-of-burst path (charge ran out on its own).
                    RestoreTimeScale();
                }
            }

            RefreshUi();
        }

        private void ApplyTimeScale()
        {
            if (timeScaleApplied) return;
            TimeScaleArbiter.SetRequest(TimeScaleChannel.Overdrive, overdriveTimeScale);
            timeScaleApplied = true;
        }

        private void RestoreTimeScale()
        {
            if (!timeScaleApplied) return;
            TimeScaleArbiter.ClearRequest(TimeScaleChannel.Overdrive);
            timeScaleApplied = false;
        }

        private void BuildWristUi()
        {
            var quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadGo.name = "OverdriveQuad";
            quadGo.transform.SetParent(transform, false);
            quadGo.transform.localScale = Vector3.one * 0.05f;
            var col = quadGo.GetComponent<Collider>();
            if (col != null) Destroy(col);
            quadRenderer = quadGo.GetComponent<Renderer>();

            var textGo = new GameObject("OverdrivePercent");
            textGo.transform.SetParent(quadGo.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            textGo.transform.localScale = Vector3.one * 0.2f;
            percentText = textGo.AddComponent<TextMesh>();
            percentText.anchor = TextAnchor.MiddleCenter;
            percentText.alignment = TextAlignment.Center;
            percentText.fontSize = 32;
            percentText.color = Color.black;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            percentText.font = font;
            textGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private void RefreshUi()
        {
            if (quadRenderer == null) return;

            if (anchor != null)
            {
                var t = quadRenderer.transform;
                t.SetPositionAndRotation(anchor.TransformPoint(localOffset), anchor.rotation);
            }

            // Deep blue (charging) -> bright cyan (ready/active) — distinct from HeatMeter's green->red
            // so the two read as different systems at a glance.
            Color tint = logic.IsActive
                ? new Color(0.4f, 0.95f, 1f)
                : Color.Lerp(new Color(0.15f, 0.2f, 0.4f), new Color(0.5f, 0.6f, 1f), logic.Charge);
            RendererTint.Apply(quadRenderer, tint);
            if (percentText != null)
            {
                int pct = Mathf.RoundToInt(logic.Charge * 100f);
                if (pct != lastDisplayedPercent)
                {
                    lastDisplayedPercent = pct;
                    percentText.text = pct + "%";
                }
            }
        }
    }
}
