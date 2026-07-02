using UnityEngine;

namespace Ronin7.Audio
{
    /// <summary>
    /// Fixed-capacity pool of 3D one-shot <see cref="AudioSource"/>s. Replaces
    /// <see cref="AudioSource.PlayClipAtPoint"/>, which allocates a temporary GameObject +
    /// AudioSource and <c>Destroy</c>s them per call — real GC churn on Quest when
    /// bolt-impact bursts fire 5–15× per second.
    ///
    /// Behaviour mirrors <c>PlayClipAtPoint</c>: spatialBlend = 1 (3D), positioned at the
    /// requested point, plays the clip once at the given volume. A slot is considered "in
    /// use" until <c>Time.unscaledTime &gt;= freeAt[i]</c> (claim time + clip length). If
    /// the pool is exhausted, the oldest-claimed slot is stolen — acceptable for short
    /// sword/bolt cues, where clipping the tail of an older one-shot is inaudible.
    /// </summary>
    public class OneShotPool : MonoBehaviour
    {
        [Tooltip("Number of pre-allocated 3D AudioSources. Sized for the worst-case " +
                 "simultaneous one-shots (bolt-impact bursts + sword cues + UI ticks).")]
        [SerializeField, Range(4, 64)] private int capacity = 16;

        private AudioSource[] sources;
        private float[] freeAtUnscaled;

        private void Awake()
        {
            sources = new AudioSource[capacity];
            freeAtUnscaled = new float[capacity];
            for (int i = 0; i < capacity; i++)
            {
                var go = new GameObject($"OneShot_{i:00}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 1f; // 3D, matches PlayClipAtPoint
                sources[i] = src;
            }
        }

        /// <summary>
        /// Play <paramref name="clip"/> at <paramref name="point"/> at <paramref name="volume"/>.
        /// No-op if the clip is null or volume is zero. Returns immediately; the source
        /// becomes claimable again once the clip's playback length elapses.
        /// </summary>
        public void Play(AudioClip clip, Vector3 point, float volume)
        {
            if (clip == null || volume <= 0f || sources == null) return;

            int slot = ClaimSlot();
            var src = sources[slot];
            src.transform.position = point;
            src.clip = clip;
            src.volume = volume;
            src.pitch = 1f;
            src.Play();
            freeAtUnscaled[slot] = Time.unscaledTime + clip.length;
        }

        /// <summary>
        /// Return the index of the first free slot, or — if none are free — the slot whose
        /// claim is oldest (smallest <c>freeAtUnscaled</c>). Picking the oldest produces the
        /// least-audible interruption when the pool is saturated.
        /// </summary>
        private int ClaimSlot()
        {
            float now = Time.unscaledTime;
            int oldest = 0;
            float oldestTime = freeAtUnscaled[0];
            for (int i = 0; i < capacity; i++)
            {
                if (freeAtUnscaled[i] <= now) return i;
                if (freeAtUnscaled[i] < oldestTime)
                {
                    oldestTime = freeAtUnscaled[i];
                    oldest = i;
                }
            }
            return oldest;
        }
    }
}
