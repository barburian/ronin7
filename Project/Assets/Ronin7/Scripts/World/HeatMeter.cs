using Ronin7.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.World
{
    /// <summary>
    /// Chapter 4's ambient manhunt pressure meter: wraps <see cref="HeatLogic"/>, ticks decay every
    /// frame, and fires <see cref="onThreshold"/>[i] each time normalized heat crosses
    /// <c>thresholds[i]</c> going up — wired by the builder to Coil hunter-wave spawners.
    ///
    /// Displays a small wrist-anchored quad (green to red via <see cref="RendererTint"/>, no material
    /// clone) plus a percent readout, following a serialized left-hand <see cref="anchor"/> transform
    /// set by the builder rather than found at runtime.
    /// </summary>
    public class HeatMeter : MonoBehaviour
    {
        [Header("Tuning")]
        // Tuning contract: one scan volume (detectionPerSecond ~0.2-0.25) must OUTPACE decay, or the
        // whole heat->hunter-wave mechanic is unreachable. 0.6 gain x 0.2 det = +0.12/s raw against
        // 0.04/s decay -> net +0.08/s inside a volume; threshold 0.5 crossed in ~6s of exposure.
        [SerializeField] private float gainPerDetection = 0.6f;
        [SerializeField] private float decayPerSecond = 0.04f;
        [SerializeField] private float[] thresholds = { 0.5f, 1f };

        [Header("Wrist UI")]
        [Tooltip("Left-hand transform the meter quad follows. Assigned by the builder.")]
        [SerializeField] private Transform anchor;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.02f, 0.06f);

        [Header("Events")]
        [Tooltip("Fires when normalized heat crosses thresholds[i] going up. Sized to match thresholds' default length.")]
        [SerializeField] private UnityEvent[] onThreshold = { new UnityEvent(), new UnityEvent() };

        private HeatLogic logic;
        private Renderer quadRenderer;
        private TextMesh percentText;

        public float Value => logic != null ? logic.Value : 0f;

        /// <summary>Build-time hook: returns the persistent-listener target for thresholds[index].</summary>
        public UnityEvent OnThresholdEvent(int index) =>
            onThreshold != null && index >= 0 && index < onThreshold.Length ? onThreshold[index] : null;

        private void Awake()
        {
            logic = new HeatLogic(gainPerDetection, decayPerSecond, thresholds);
            BuildWristUi();
        }

        private void Update()
        {
            logic.Tick(Time.deltaTime);
            while (logic.ConsumeThresholdCrossing(out int index))
            {
                if (onThreshold != null && index < onThreshold.Length)
                    onThreshold[index]?.Invoke();
            }
            RefreshUi();
        }

        private void LateUpdate()
        {
            if (anchor == null || quadRenderer == null) return;
            var t = quadRenderer.transform;
            t.SetPositionAndRotation(anchor.TransformPoint(localOffset), anchor.rotation);
        }

        /// <summary>Scan-drone/patrol exposure call site.</summary>
        public void ReportDetection(float amount) => logic?.AddDetection(amount);

        private void BuildWristUi()
        {
            var quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadGo.name = "HeatQuad";
            quadGo.transform.SetParent(transform, false);
            quadGo.transform.localScale = Vector3.one * 0.05f;
            Destroy(quadGo.GetComponent<Collider>());
            quadRenderer = quadGo.GetComponent<Renderer>();

            var textGo = new GameObject("HeatPercent");
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
            if (quadRenderer != null) RendererTint.Apply(quadRenderer, Color.Lerp(Color.green, Color.red, logic.Value));
            if (percentText != null) percentText.text = Mathf.RoundToInt(logic.Value * 100f) + "%";
        }
    }
}
