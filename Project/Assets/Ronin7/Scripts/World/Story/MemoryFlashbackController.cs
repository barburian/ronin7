using System.Collections;
using UnityEngine;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Scene treatment for "memory-space" chapter sequences. Applies fog, ambient lighting, and an
    /// optional heartbeat loop to establish a dreamlike atmosphere.
    /// </summary>
    public class MemoryFlashbackController : MonoBehaviour
    {
        [SerializeField] private Color fogColor = new Color(0.35f, 0.37f, 0.42f);
        [SerializeField] private float fogDensity = 0.045f;
        [SerializeField] private Color ambientColor = new Color(0.30f, 0.30f, 0.34f);
        [SerializeField] private AudioClip heartbeatLoop;
        [SerializeField] private float heartbeatVolume = 0.35f;

        private AudioSource heartbeatSource;

        private void Awake()
        {
            ApplyTreatment();

            if (heartbeatLoop != null)
            {
                if (heartbeatSource == null)
                {
                    heartbeatSource = gameObject.AddComponent<AudioSource>();
                }
                heartbeatSource.clip = heartbeatLoop;
                heartbeatSource.loop = true;
                heartbeatSource.spatialBlend = 0f; // 2D ambient sound
                heartbeatSource.volume = heartbeatVolume;
                heartbeatSource.playOnAwake = true;
                heartbeatSource.Play();
            }
        }

        /// <summary>
        /// Apply the memory-space visual treatment: enable fog (ExponentialSquared), set fog color
        /// and density, and configure ambient lighting to be flat with the assigned color.
        /// </summary>
        public void ApplyTreatment()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
        }

        /// <summary>
        /// Create a transparent URP Unlit material with a pale blue-white tint and ~0.35 alpha,
        /// suitable for rendering ghost NPCs during memory echoes.
        /// </summary>
        public static Material MakeGhostMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            Color ghostColor = new Color(0.75f, 0.85f, 1.0f, 0.35f);

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ghostColor);
            if (mat.HasProperty("_Color")) mat.color = ghostColor;

            mat.SetFloat("_Surface", 1f);   // 0 = Opaque, 1 = Transparent
            mat.SetFloat("_Blend", 0f);     // 0 = Alpha blend
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            return mat;
        }
    }

    /// <summary>
    /// A trigger-driven "ghost replay": when the player crosses this trigger, ghost echo NPCs
    /// activate and replay a short scripted movement while a DialoguePlayer set plays dialogue.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MemoryEchoVignette : MonoBehaviour
    {
        [SerializeField] private GameObject echoRoot;
        [SerializeField] private Transform moveFrom;
        [SerializeField] private Transform moveTo;
        [SerializeField] private float duration = 6f;
        [SerializeField] private DialoguePlayer dialogue;
        [SerializeField] private bool oneShot = true;

        public bool HasPlayed { get; private set; }

        private Coroutine playCoroutine;

        private void Awake()
        {
            // Force collider to be a trigger.
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            // Ensure echo is inactive until triggered.
            if (echoRoot != null)
            {
                echoRoot.SetActive(false);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
            {
                TriggerVignette();
            }
        }

        /// <summary>
        /// Detect whether the collider belongs to the player rig (via CharacterController).
        /// </summary>
        private static bool IsPlayer(Collider other) =>
            other.GetComponentInParent<CharacterController>() != null;

        /// <summary>
        /// Activate the echo sequence: show the ghost NPC, lerp its position if movement is defined,
        /// and play dialogue. After completion, optionally deactivate and disable this component.
        /// </summary>
        public void TriggerVignette()
        {
            if (HasPlayed && oneShot)
            {
                return;
            }

            HasPlayed = true;

            if (playCoroutine != null)
            {
                StopCoroutine(playCoroutine);
            }
            playCoroutine = StartCoroutine(PlayVignetteRoutine());
        }

        private IEnumerator PlayVignetteRoutine()
        {
            if (echoRoot != null)
            {
                echoRoot.SetActive(true);
            }

            // Start dialogue if wired.
            if (dialogue != null)
            {
                dialogue.Play();
            }

            // Lerp movement if both moveFrom and moveTo are set.
            if (moveFrom != null && moveTo != null && echoRoot != null)
            {
                Vector3 startPos = moveFrom.localPosition;
                Vector3 endPos = moveTo.localPosition;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    echoRoot.transform.localPosition = Vector3.Lerp(startPos, endPos, t);
                    yield return null;
                }

                // Snap to end position to ensure precision.
                echoRoot.transform.localPosition = endPos;
            }
            else
            {
                // No movement: just wait for duration.
                yield return new WaitForSeconds(duration);
            }

            // Deactivate echo and this component if oneShot.
            if (oneShot)
            {
                if (echoRoot != null)
                {
                    echoRoot.SetActive(false);
                }
                enabled = false;
            }
        }
    }
}
