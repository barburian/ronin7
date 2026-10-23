using UnityEngine;

namespace Ronin7.Audio.Vfx
{
    /// <summary>
    /// P5-03 Unbroken proc: one pooled fresnel hemisphere (radius <see cref="Radius"/>, <see cref="Duration"/> s).
    /// The mesh is the back half of a sphere, so the dome is open toward where the player is looking and never
    /// covers the eyes; it follows the head position for its 0.4 s. The object is inactive when idle.
    /// </summary>
    public class UnbrokenShell
    {
        public const float Radius = 1.2f;
        public const float Duration = 0.4f;
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        private readonly GameObject go;
        private readonly Renderer rend;
        private MaterialPropertyBlock mpb;
        private Transform head;
        private float startedAt = -1f;

        public UnbrokenShell(Transform parent, GameObject prefab)
        {
            go = Object.Instantiate(prefab, parent);
            go.name = "UnbrokenShell";
            rend = go.GetComponent<Renderer>();
            go.SetActive(false);
        }

        public bool Visible => startedAt >= 0f;

        public void Play(Transform headTransform, float now)
        {
            head = headTransform;
            if (head == null) return;
            startedAt = now;
            Place(0f);
            go.SetActive(true);
        }

        /// <summary>Animates the shell; returns whether it is still visible.</summary>
        public bool Tick(float now)
        {
            if (startedAt < 0f) return false;
            float t = (now - startedAt) / Duration;
            if (t >= 1f || head == null) { go.SetActive(false); startedAt = -1f; return false; }
            Place(t);
            return true;
        }

        private void Place(float t)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            go.transform.SetPositionAndRotation(head.position, Quaternion.LookRotation(fwd.normalized));
            go.transform.localScale = Vector3.one * (Radius * Mathf.Lerp(0.7f, 1f, t));
            mpb ??= new MaterialPropertyBlock();
            mpb.SetFloat(FadeId, 1f - t);
            rend.SetPropertyBlock(mpb);
        }
    }
}
