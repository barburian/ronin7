using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.Audio.Vfx
{
    /// <summary>
    /// P5-03 Phase Step afterimage: a frozen copy of the hands and sword meshes drawn with the saved
    /// <c>MirrorGhost.mat</c> and faded over <see cref="FadeTime"/> via MaterialPropertyBlock alpha. The copies are
    /// pooled (created once, then only repositioned) and inactive when idle, so the ghost's SetPass is paid for 0.3 s only.
    /// <see cref="Capture"/> runs before the blink, <see cref="Show"/> once the rig has landed.
    /// </summary>
    public class Afterimage
    {
        public const int MaxParts = 12;
        public const float FadeTime = 0.3f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Tint = new Color(0.75f, 0.85f, 1f, 0.35f); // MirrorGhost.mat base colour
        private static readonly List<MeshRenderer> Scratch = new List<MeshRenderer>(32);

        private readonly Transform parent;
        private readonly Material ghost;
        private readonly MeshFilter[] filters = new MeshFilter[MaxParts];
        private readonly MeshRenderer[] renderers = new MeshRenderer[MaxParts];
        private int count;
        private float shownAt = -1f;
        private MaterialPropertyBlock mpb;

        public Afterimage(Transform parent, Material ghost) { this.parent = parent; this.ghost = ghost; }

        public bool Visible => shownAt >= 0f;
        public int PartCount => count;

        /// <summary>Snapshot the world pose and mesh of every enabled mesh renderer under <paramref name="roots"/> (kept hidden).</summary>
        public void Capture(IList<Transform> roots)
        {
            Hide();
            count = 0;
            if (ghost == null) return;
            foreach (var root in roots)
            {
                if (root == null) continue;
                root.GetComponentsInChildren(false, Scratch);
                foreach (var r in Scratch)
                {
                    if (count >= MaxParts) break;
                    if (!r.enabled || !r.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null) continue;
                    var sm = r.sharedMaterial;
                    if (sm != null && sm.shader != null && sm.shader.name.Contains("Outline")) continue; // skip the toon outline hull
                    var slot = Slot(count);
                    slot.sharedMesh = mf.sharedMesh;
                    slot.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                    slot.transform.localScale = r.transform.lossyScale;
                    count++;
                }
            }
            Scratch.Clear();
        }

        public void Show(float now)
        {
            if (count == 0) return;
            shownAt = now;
            for (int i = 0; i < count; i++) renderers[i].gameObject.SetActive(true);
            Paint(1f);
        }

        /// <summary>Fades the ghost; returns whether it is still visible.</summary>
        public bool Tick(float now)
        {
            if (shownAt < 0f) return false;
            float k = 1f - (now - shownAt) / FadeTime;
            if (k <= 0f) { Hide(); return false; }
            Paint(k);
            return true;
        }

        private void Hide()
        {
            shownAt = -1f;
            for (int i = 0; i < MaxParts; i++) if (renderers[i] != null) renderers[i].gameObject.SetActive(false);
        }

        private MeshFilter Slot(int i)
        {
            if (filters[i] == null)
            {
                var go = new GameObject($"Afterimage_{i:00}");
                go.transform.SetParent(parent, false);
                filters[i] = go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = ghost;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                renderers[i] = r;
                go.SetActive(false);
            }
            return filters[i];
        }

        private void Paint(float k)
        {
            mpb ??= new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, new Color(Tint.r, Tint.g, Tint.b, Tint.a * k));
            for (int i = 0; i < count; i++) renderers[i].SetPropertyBlock(mpb);
        }
    }
}
