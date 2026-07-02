using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// DEBUG AID: shows the current scene's name as a small label floating in the headset view,
    /// and prints "[Scene] '&lt;name&gt;' loaded" to the console/logcat on every scene load. Lets you
    /// see exactly which scene you're in the moment you hit a bug while playing.
    ///
    /// Self-bootstrapping (no scene wiring): a single persistent instance is spawned at play start
    /// via <see cref="RuntimeInitializeOnLoadMethod"/> (same trick as <c>EventBus</c>), so it works
    /// no matter which scene you launch into and survives every transition. The rig/camera is
    /// rebuilt per scene, so the label lives on this persistent object and *follows* the head camera
    /// each frame (a HUD) rather than being parented to it — being parented would destroy it on the
    /// next single-mode load. A screen-space-overlay canvas does not render in VR, hence world space.
    ///
    /// Uses raw <see cref="Debug.Log"/> (not <c>Ronin7.Core.Log</c>) on purpose: that logger
    /// is compile-stripped unless DEBUG_SS is defined, which it is not for the Quest/Standalone
    /// builds — so the stripped version would print nothing on the headset.
    ///
    /// To disable: comment out the [RuntimeInitializeOnLoadMethod] attribute below, or delete this
    /// file. Nothing else references it.
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneDebugOverlay : MonoBehaviour
    {
        // Lower-centre of view, ~0.6 m ahead — readable but out of the way of gameplay.
        private static readonly Vector3 Offset = new Vector3(0f, -0.18f, 0.6f);

        private Text label;
        private Canvas canvas;
        private Camera cam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<SceneDebugOverlay>() != null) return;
            var go = new GameObject("Scene Debug Overlay");
            go.AddComponent<SceneDebugOverlay>();
            DontDestroyOnLoad(go);
        }

        private void Awake() => BuildLabel();

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        // The boot scene's sceneLoaded already fired before we subscribed, so seed it here.
        private void Start() => Show(SceneManager.GetActiveScene().name);

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[Scene] '{scene.name}' loaded");
            Show(scene.name);
            cam = null; // force re-acquire of the freshly rebuilt head camera
        }

        private void Show(string sceneName)
        {
            if (label != null) label.text = sceneName;
        }

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            bool haveCam = cam != null;
            if (canvas != null && canvas.enabled != haveCam) canvas.enabled = haveCam;
            if (!haveCam) return; // hidden during the transition fade until the new camera exists

            var t = canvas.transform;
            t.SetPositionAndRotation(
                cam.transform.position + cam.transform.rotation * Offset,
                cam.transform.rotation);
        }

        private void BuildLabel()
        {
            var canvasGo = new GameObject("Scene Label Canvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(440f, 90f);
            rt.localScale = Vector3.one * 0.0009f;

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false; // never steal the VR UI pointer

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(rt, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            label = textGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 32;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.6f, 1f, 0.35f);
            label.raycastTarget = false;
            label.text = "";
        }
    }
}
