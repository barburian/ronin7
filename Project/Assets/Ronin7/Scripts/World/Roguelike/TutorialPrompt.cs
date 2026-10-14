using Ronin7.Combat;
using Ronin7.Core;
using TMPro;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// P2-08: first-run onboarding. A big static north-wall label (world-space, no motion, comfort-safe)
    /// stepping DRAW -> SWING -> PARRY -> CHOOSE off bus events. Never blocks: later events skip earlier
    /// steps, and clearing the room jumps straight to CHOOSE. The first RoomCleared also marks the
    /// tutorial done in meta.json.
    /// </summary>
    public sealed class TutorialPrompt : MonoBehaviour
    {
        internal enum Step { Draw, Swing, Parry, Choose, Done }
        internal enum Trigger { Grabbed, Impact, Deflected, Cleared, BoonChosen }

        internal static readonly string[] Texts =
        {
            "DRAW YOUR BLADE\n<size=50%>(grip at hip)</size>",
            "SWING TO CUT",
            "MEET THEIR STRIKE TO PARRY",
            "POINT AND PULL TRIGGER TO CHOOSE",
            "",
        };

        /// <summary>Pure step logic: a trigger completes its own step and every earlier one, never rewinds.
        /// Cleared enters CHOOSE (the parry is optional), BoonChosen finishes.</summary>
        internal static Step Advance(Step current, Trigger t)
        {
            Step target = t switch
            {
                Trigger.Grabbed => Step.Swing,
                Trigger.Impact => Step.Parry,
                Trigger.Deflected => Step.Choose,
                Trigger.Cleared => Step.Choose,
                _ => Step.Done,
            };
            return target > current ? target : current;
        }

        private Step step;
        private TextMeshPro label;

        public static TutorialPrompt Create(Transform arena, float halfExtent)
        {
            var go = new GameObject("Tutorial Prompt");
            go.transform.SetParent(arena, false);
            // North wall, inside the wall slab (wall thickness ~0.3 m), eye-ish height; identity rotation reads from the south.
            go.transform.localPosition = new Vector3(0f, 2.8f, halfExtent - 0.5f);
            var p = go.AddComponent<TutorialPrompt>();
            p.label = go.AddComponent<TextMeshPro>();
            p.label.rectTransform.sizeDelta = new Vector2(16f, 3.5f);
            p.label.alignment = TextAlignmentOptions.Center;
            p.label.fontStyle = FontStyles.Bold;
            p.label.fontSize = 11f; // ponytail: first-guess size (~0.8 m caps, player stands ~10 m away), tune in-headset
            p.label.color = new Color(1f, 0.9f, 0.5f);
            p.label.text = Texts[0];
            return p;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ObjectGrabbed>(OnGrabbed);
            EventBus.Subscribe<SwordImpact>(OnImpact);
            EventBus.Subscribe<SwordDeflected>(OnDeflected);
            EventBus.Subscribe<RoomCleared>(OnCleared);
            EventBus.Subscribe<BoonChosen>(OnBoon);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ObjectGrabbed>(OnGrabbed);
            EventBus.Unsubscribe<SwordImpact>(OnImpact);
            EventBus.Unsubscribe<SwordDeflected>(OnDeflected);
            EventBus.Unsubscribe<RoomCleared>(OnCleared);
            EventBus.Unsubscribe<BoonChosen>(OnBoon);
        }

        private void OnGrabbed(ObjectGrabbed _) => Apply(Trigger.Grabbed);
        private void OnImpact(SwordImpact _) => Apply(Trigger.Impact);
        private void OnDeflected(SwordDeflected _) => Apply(Trigger.Deflected);
        private void OnBoon(BoonChosen _) => Apply(Trigger.BoonChosen);

        private void OnCleared(RoomCleared _)
        {
            MetaProgression.CompleteTutorial();
            Apply(Trigger.Cleared);
        }

        private void Apply(Trigger t)
        {
            Step next = Advance(step, t);
            if (next == step) return;
            step = next;
            if (label != null) label.text = Texts[(int)step];
            if (step == Step.Done) Destroy(gameObject);
        }
    }
}
