using Ronin7.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// P6-03: head-locked lower subtitle band for VO. Listens for <see cref="SubtitleLine"/> (published by
    /// DialoguePlayer at line start, so P6-04's Kessler and boss lines feed it with no extra wiring) and shows
    /// it while <see cref="GameSettings.Subtitles"/> is on. The canvas is built in Awake, so a scene only needs
    /// this component on an empty object. Body text is 28 px at 0.0013 scale 1.5 m out (about 1.4 deg, no smaller
    /// than the 28 px at 0.001 / 1.3 m menu minimum). Rigidly head-locked on purpose: a laggy caption
    /// swims in VR; it is small, low and only on screen while someone speaks.
    /// </summary>
    public class SubtitleDisplay : MonoBehaviour
    {
        public const float Distance = 1.5f, Drop = 0.45f, CanvasScale = 0.0013f;
        public static readonly Vector2 BandSize = new Vector2(1100f, 130f);

        private Canvas canvas;
        private TextMeshProUGUI label;
        private float hideAt;
        private bool shown;

        private void Awake()
        {
            var go = new GameObject("Subtitle Canvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = BandSize;
            rt.localScale = Vector3.one * CanvasScale;
            go.AddComponent<UiScaleFollower>();

            go.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.78f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rt, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(24f, 8f); lrt.offsetMax = new Vector2(-24f, -8f);
            label = UiKit.Text(labelGo, string.Empty, UiKit.MinBody, Color.white, TextAlignmentOptions.Center);
            label.overflowMode = TextOverflowModes.Ellipsis;
            canvas.enabled = false;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<SubtitleLine>(OnLine);
            EventBus.Subscribe<SettingsChanged>(OnSettings);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SubtitleLine>(OnLine);
            EventBus.Unsubscribe<SettingsChanged>(OnSettings);
        }

        private void OnLine(SubtitleLine line)
        {
            string text = SubtitleLine.Format(line.Speaker, line.Text);
            if (!GameSettings.Active.Subtitles || text.Length == 0) return;
            label.text = text;
            hideAt = Time.unscaledTime + Mathf.Max(0.5f, line.Seconds);
            shown = true;
            canvas.enabled = true;
            LateUpdate();
        }

        private void OnSettings(SettingsChanged e) { if (!e.Settings.Subtitles) Hide(); }

        private void Hide() { shown = false; canvas.enabled = false; }

        private void LateUpdate()
        {
            if (!shown) return;
            if (Time.unscaledTime >= hideAt) { Hide(); return; }
            var cam = Camera.main;
            if (cam == null) return;
            var t = cam.transform;
            canvas.transform.SetPositionAndRotation(t.position + t.forward * Distance - t.up * Drop, t.rotation);
        }
    }
}
