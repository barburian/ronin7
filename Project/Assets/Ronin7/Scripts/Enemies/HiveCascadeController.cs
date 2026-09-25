using System.Collections.Generic;
using UnityEngine;
using Ronin7.Combat;

namespace Ronin7.Enemies
{
    /// <summary>
    /// "The Fracture Protocol" mechanic: drives a GROUP of clone enemies through hive-consensus
    /// collapse. A colony grown as one mind in fifty thousand bodies is fracturing into individuals, so
    /// each clone drifts between three states instead of attacking in lockstep:
    ///   • Attacking  — normal: its <see cref="MeleeAttacker"/> is enabled and it engages the player.
    ///   • Frozen     — overwhelmed by competing impulses: MeleeAttacker disabled, tinted cold; it stands inert.
    ///   • Conflicted — turning on its own: MeleeAttacker disabled, tinted hot; it disengages the player,
    ///                  consumed by internal conflict. (Literal sibling friendly-fire is represented as
    ///                  disengagement — we toggle attack/tint only, never reach into MeleeAttacker's target,
    ///                  keeping this augment non-invasive to the shared melee FSM.)
    ///
    /// Each member runs an independent timer so the group desynchronizes over time — the visible signature
    /// of a failing collective. No per-frame allocation. Composes with (does not replace) <see cref="MirrorPhantom"/>:
    /// MirrorPhantom owns the "every face is your face" dissolve-on-death sequence; this owns the cascade behaviour.
    ///
    /// Set <see cref="autoCascade"/> false to freeze transitions and drive state manually (deterministic tests).
    /// </summary>
    public class HiveCascadeController : MonoBehaviour
    {
        public enum CascadeState { Attacking, Frozen, Conflicted }

        [SerializeField] private List<MeleeAttacker> members = new();

        [Header("State durations (seconds, randomized per cycle within range)")]
        [SerializeField] private Vector2 attackingDuration = new Vector2(3.5f, 6f);
        [SerializeField] private Vector2 frozenDuration = new Vector2(1.2f, 2.5f);
        [SerializeField] private Vector2 conflictedDuration = new Vector2(1.5f, 3f);

        [Header("Transition odds out of Attacking")]
        [Tooltip("When an Attacking member's timer expires, chance it goes Frozen vs Conflicted.")]
        [Range(0f, 1f)]
        [SerializeField] private float freezeChance = 0.55f;

        [Header("Tints")]
        [SerializeField] private Color frozenTint = new Color(0.45f, 0.6f, 0.95f);
        [SerializeField] private Color conflictedTint = new Color(0.95f, 0.45f, 0.3f);

        [Tooltip("When false, members hold their current state (no random transitions) so tests can drive them.")]
        [SerializeField] private bool autoCascade = true;

        private CascadeState[] state;
        private float[] timer;
        private Renderer[] renderers;
        private Health[] healths;
        private MaterialPropertyBlock mpb;
        private Color[] baseColor;
        private bool initialized;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public int MemberCount => members != null ? members.Count : 0;

        private void OnEnable()
        {
            Initialize();
        }

        private void Initialize()
        {
            int n = members != null ? members.Count : 0;
            state = new CascadeState[n];
            timer = new float[n];
            renderers = new Renderer[n];
            healths = new Health[n];
            baseColor = new Color[n];
            mpb = new MaterialPropertyBlock();

            for (int i = 0; i < n; i++)
            {
                var m = members[i];
                state[i] = CascadeState.Attacking;
                // Stagger initial timers so the group never transitions in unison.
                timer[i] = Random.Range(attackingDuration.x, attackingDuration.y) * Random.value;
                if (m != null)
                {
                    m.enabled = true;
                    healths[i] = m.GetComponent<Health>();
                    // includeInactive: members are typically spawned (inactive) after this controller initializes.
                    var r = m.GetComponentInChildren<Renderer>(true);
                    renderers[i] = r;
                    baseColor[i] = r != null && HasColor(r) ? ReadColor(r) : Color.white;
                }
            }
            initialized = true;
        }

        private void Update()
        {
            if (!autoCascade || !initialized) return;

            float dt = Time.deltaTime;
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (m == null) continue;

                var h = healths[i];
                if (h != null && !h.IsAlive) continue;

                timer[i] -= dt;
                if (timer[i] > 0f) continue;

                AdvanceState(i);
            }
        }

        private void AdvanceState(int i)
        {
            switch (state[i])
            {
                case CascadeState.Attacking:
                    // A clone overwhelmed by individuation either freezes or turns inward.
                    if (Random.value < freezeChance)
                        ApplyState(i, CascadeState.Frozen, Random.Range(frozenDuration.x, frozenDuration.y));
                    else
                        ApplyState(i, CascadeState.Conflicted, Random.Range(conflictedDuration.x, conflictedDuration.y));
                    break;
                default:
                    // Frozen/Conflicted both resolve back to attacking — protocol reasserts, for now.
                    ApplyState(i, CascadeState.Attacking, Random.Range(attackingDuration.x, attackingDuration.y));
                    break;
            }
        }

        /// <summary>Force a member into a state. Public for builders/tests. Duration &lt;= 0 picks a default for the state.</summary>
        public void SetMemberState(int i, CascadeState next)
        {
            if (!initialized) Initialize();
            if (members == null || i < 0 || i >= members.Count) return;
            float d = next switch
            {
                CascadeState.Frozen => frozenDuration.y,
                CascadeState.Conflicted => conflictedDuration.y,
                _ => attackingDuration.y,
            };
            ApplyState(i, next, d);
        }

        public CascadeState GetMemberState(int i)
        {
            if (state == null || i < 0 || i >= state.Length) return CascadeState.Attacking;
            return state[i];
        }

        private void ApplyState(int i, CascadeState next, float duration)
        {
            state[i] = next;
            timer[i] = duration;

            var m = members[i];
            if (m == null) return;

            // Only an Attacking clone engages the player.
            m.enabled = next == CascadeState.Attacking;

            var r = renderers != null ? renderers[i] : null;
            if (r == null) return;
            Color c = next switch
            {
                CascadeState.Frozen => frozenTint,
                CascadeState.Conflicted => conflictedTint,
                _ => baseColor[i],
            };
            r.GetPropertyBlock(mpb);
            if (HasColor(r)) mpb.SetColor(r.sharedMaterial.HasProperty(BaseColorId) ? BaseColorId : ColorId, c);
            r.SetPropertyBlock(mpb);
        }

        private static bool HasColor(Renderer r)
        {
            var mat = r.sharedMaterial;
            return mat != null && (mat.HasProperty(BaseColorId) || mat.HasProperty(ColorId));
        }

        private static Color ReadColor(Renderer r)
        {
            var mat = r.sharedMaterial;
            if (mat == null) return Color.white;
            if (mat.HasProperty(BaseColorId)) return mat.GetColor(BaseColorId);
            if (mat.HasProperty(ColorId)) return mat.GetColor(ColorId);
            return Color.white;
        }
    }
}
