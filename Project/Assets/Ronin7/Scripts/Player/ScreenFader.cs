using System.Collections;
using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// Full-screen black fade for VR-comfort scene transitions. Fading to black before a
    /// teleport / scene load (and back after) is the standard nausea mitigation: the player
    /// never sees the world pop or reposition.
    ///
    /// Built entirely from code (no scene/prefab assets), mirroring <c>Ship/ComfortVignette</c>:
    /// a solid quad sits just in front of the eyes on a transparent unlit overlay material, and
    /// its alpha animates 0 (clear) -> 1 (opaque). It auto-attaches to the head camera so it
    /// survives across scene loads when carried by a persistent rig, and a fresh one can be
    /// spawned per scene by the flow manager via <see cref="Ensure"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScreenFader : MonoBehaviour
    {
        [Tooltip("Distance in front of the eyes the fade quad sits (metres). Closer than the vignette so it always draws on top.")]
        [SerializeField] private float distance = 0.4f;
        [Tooltip("Default fade duration (seconds) when none is supplied.")]
        [SerializeField] private float defaultDuration = 0.4f;
        [SerializeField] private Color color = Color.black;

        private MeshRenderer quad;
        private MaterialPropertyBlock mpb;
        private float alpha; // current 0..1 opacity
        private Coroutine fade;

        /// <summary>True once the screen is fully black (safe to swap scenes behind it).</summary>
        public bool IsOpaque => alpha >= 0.999f;

        /// <summary>
        /// Find the head camera and ensure a single <see cref="ScreenFader"/> child on it.
        /// Returns null if no camera exists yet (caller should retry once the rig is live).
        /// </summary>
        public static ScreenFader Ensure()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var fader = cam.GetComponentInChildren<ScreenFader>();
            if (fader == null)
            {
                var go = new GameObject("Screen Fader");
                go.transform.SetParent(cam.transform, false);
                fader = go.AddComponent<ScreenFader>();
            }
            return fader;
        }

        private void Awake()
        {
            BuildQuad();
            mpb = new MaterialPropertyBlock();
            Apply();
        }

        private void LateUpdate()
        {
            // Keep the quad pinned in front of the eyes regardless of head motion.
            transform.localPosition = new Vector3(0f, 0f, distance);
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>Fade to black. Yields until fully opaque.</summary>
        public IEnumerator FadeOut(float duration = -1f) => FadeTo(1f, duration);

        /// <summary>Fade back to clear. Yields until fully transparent.</summary>
        public IEnumerator FadeIn(float duration = -1f) => FadeTo(0f, duration);

        /// <summary>Snap to fully black with no animation (e.g. on a hard boot).</summary>
        public void SetBlackInstant()
        {
            if (fade != null) { StopCoroutine(fade); fade = null; }
            alpha = 1f;
            Apply();
        }

        private IEnumerator FadeTo(float to, float duration)
        {
            if (duration < 0f) duration = defaultDuration;
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeRoutine(to, duration));
            yield return fade;
        }

        private IEnumerator FadeRoutine(float to, float duration)
        {
            float from = alpha;
            if (duration <= 0f)
            {
                alpha = to;
                Apply();
                fade = null;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // robust if anything pauses time during a load
                alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                Apply();
                yield return null;
            }
            alpha = to;
            Apply();
            fade = null;
        }

        private void Apply()
        {
            if (quad == null) return;
            quad.enabled = alpha > 0.001f;
            quad.GetPropertyBlock(mpb);
            var c = color; c.a = alpha;
            mpb.SetColor("_BaseColor", c); // URP
            mpb.SetColor("_Color", c);     // legacy fallback
            quad.SetPropertyBlock(mpb);
        }

        /// <summary>A large flat quad facing the eyes, sized to overflow any HMD FOV.</summary>
        private void BuildQuad()
        {
            var go = new GameObject("FaderQuad");
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            quad = go.AddComponent<MeshRenderer>();
            quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quad.receiveShadows = false;
            quad.material = Ronin7.Core.VrUiMaterials.CreateTransparentUnlit();

            const float r = 6f; // half-extent; far larger than any FOV at the quad's distance
            var verts = new[]
            {
                new Vector3(-r, -r, 0f), new Vector3(r, -r, 0f),
                new Vector3(-r,  r, 0f), new Vector3(r,  r, 0f),
            };
            var tris = new[] { 0, 2, 1, 2, 3, 1 };

            var mesh = new Mesh { name = "ScreenFaderQuad" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
        }

    }
}
