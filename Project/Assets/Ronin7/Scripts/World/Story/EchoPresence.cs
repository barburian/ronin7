using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Echo — the katana's shadow-AI — comments ambiently on combat, heard (read) only by the player.
    /// Silent until "ch3_complete" (see <see cref="EchoLines"/>), then widens as later chapter flags
    /// land. Listens for enemy kills, low player health, and ability activations; stays quiet while
    /// <see cref="missionDialogue"/> is playing so lines never overlap a scripted conversation.
    /// Displays its own small head-locked subtitle (lighter than <see cref="DialoguePlayer"/>'s) rather
    /// than reusing that component. Additive and safe with no inspector wiring at all.
    /// </summary>
    public class EchoPresence : MonoBehaviour
    {
        [Tooltip("Optional: the scene's mission DialoguePlayer. Echo stays silent while it's playing.")]
        [SerializeField] private DialoguePlayer missionDialogue;
        [SerializeField] private float cooldownSeconds = 20f;
        [SerializeField] private float displaySeconds = 2.5f;
        [Tooltip("Fraction of max health (Current/Max) below which a hit triggers a player_hurt callout.")]
        [SerializeField] private float lowHealthFraction = 0.35f;

        [Header("Head-locked subtitle")]
        [SerializeField] private float followDistance = 1.2f;
        [SerializeField] private float followVerticalOffset = 0.3f;
        [SerializeField] private float followLerp = 12f;

        private EchoCalloutSelector selector;
        private TextMesh textMesh;
        private float hideAtTime = -1f;

        private void Awake()
        {
            selector = new EchoCalloutSelector(EchoLines.PoolsFor(CampaignState.HasFlag), cooldownSeconds);

            var textGo = new GameObject("EchoSubtitle");
            textGo.transform.SetParent(transform, false);
            textGo.transform.localScale = Vector3.one * 0.01f;
            textMesh = textGo.AddComponent<TextMesh>();
            textMesh.text = "";
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 48;
            textMesh.color = new Color(0.55f, 0.85f, 1f); // cool cyan, distinct from DialoguePlayer's white
            // Runtime-built TextMesh gets no font auto-assigned (edit-time-only convenience) — without
            // an explicit font + its material the subtitle renders nothing.
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textMesh.font = font;
            textGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EntityDied>(OnEntityDied);
            EventBus.Subscribe<EntityDamaged>(OnEntityDamaged);
            EventBus.Subscribe<AbilityActivated>(OnAbilityActivated);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EntityDied>(OnEntityDied);
            EventBus.Unsubscribe<EntityDamaged>(OnEntityDamaged);
            EventBus.Unsubscribe<AbilityActivated>(OnAbilityActivated);
        }

        private void OnEntityDied(EntityDied evt)
        {
            if (IsPlayer(evt.Entity)) return; // Echo comments on enemies going down, not the player's own death
            TrySpeak(EchoLines.EnemyKilled);
        }

        private void OnEntityDamaged(EntityDamaged evt)
        {
            if (!IsPlayer(evt.Entity)) return;
            if (evt.Max <= 0f || evt.Current / evt.Max >= lowHealthFraction) return;
            TrySpeak(EchoLines.PlayerHurt);
        }

        private void OnAbilityActivated(AbilityActivated evt) => TrySpeak(EchoLines.AbilityActivated);

        // Same player-identification idiom as ExtractionZone.IsPlayer / FactionCombatant.RetargetNearestEnemy:
        // the on-foot player rig carries a CharacterController, enemies never do.
        private static bool IsPlayer(GameObject entity) =>
            entity != null && entity.GetComponent<CharacterController>() != null;

        private void TrySpeak(string eventKind)
        {
            if (missionDialogue != null && missionDialogue.IsPlaying) return;
            // Ch9 overdrive audit: callout cooldown/display run on unscaled time so an Echo line called
            // out during a time-slowed burst still shows for its real 2.5s, not 3x that.
            if (!selector.TryPick(eventKind, Time.unscaledTime, out string line)) return;

            textMesh.text = line;
            hideAtTime = Time.unscaledTime + displaySeconds;
        }

        private void Update()
        {
            if (hideAtTime < 0f) return;
            if (Time.unscaledTime >= hideAtTime)
            {
                textMesh.text = "";
                hideAtTime = -1f;
            }
        }

        // Head-locked subtitle: while a line is showing, float it in front of the camera and ease
        // toward that pose each frame (same idiom as DialoguePlayer's LateUpdate follow).
        private void LateUpdate()
        {
            if (hideAtTime < 0f) return;

            var cam = Camera.main;
            if (cam == null) return;

            var camT = cam.transform;
            Vector3 targetPos = camT.TransformPoint(new Vector3(0f, followVerticalOffset, followDistance));
            Quaternion targetRot = Quaternion.LookRotation(targetPos - camT.position, camT.up);

            var t = textMesh.transform;
            // Ch9 overdrive audit: unscaled so the subtitle keeps pace with real head motion during a
            // time-slowed burst instead of visibly lagging/detaching from the view.
            float k = 1f - Mathf.Exp(-followLerp * Time.unscaledDeltaTime);
            t.position = Vector3.Lerp(t.position, targetPos, k);
            t.rotation = Quaternion.Slerp(t.rotation, targetRot, k);
        }
    }
}
