using Ronin7.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Chapter 6's any-order three-tower kill-list gate: watches a fixed set of <see cref="Health"/>
    /// objectives (the three tower masters) and fires <see cref="onAllComplete"/> once every one of
    /// them has died, regardless of the order they fell in. Wraps <see cref="MultiObjectiveLogic"/> the
    /// same way <see cref="Ronin7.World.HeatMeter"/> wraps <see cref="Ronin7.World.HeatLogic"/>.
    ///
    /// Subscribes to each objective's <c>Died</c> event in <c>OnEnable</c> — the same idiom
    /// <c>MissionDirector.BeginDefeatEnemies</c> uses — and unsubscribes in <c>OnDisable</c>.
    /// The builder wires <see cref="onAllComplete"/> to <c>MissionDirector.AdvanceFromPrompt</c>, the
    /// same "gate a null-promptObject Prompt step" idiom <c>DuelYield.onAccepted</c> uses in Chapter 4.
    /// </summary>
    public class MultiObjectiveGate : MonoBehaviour
    {
        [SerializeField] private Health[] objectives;
        public UnityEvent onAllComplete = new UnityEvent();

        private MultiObjectiveLogic logic;
        private System.Action[] handlers;

        private void OnEnable()
        {
            int count = objectives != null ? objectives.Length : 0;
            logic = new MultiObjectiveLogic(count);
            if (count == 0) return;

            handlers = new System.Action[count];
            for (int i = 0; i < count; i++)
            {
                int index = i; // capture a per-iteration copy for the closure
                handlers[i] = () => OnObjectiveDied(index);
                if (objectives[i] != null) objectives[i].Died += handlers[i];
            }
        }

        private void OnDisable()
        {
            if (objectives == null || handlers == null) return;
            for (int i = 0; i < objectives.Length && i < handlers.Length; i++)
            {
                if (objectives[i] != null && handlers[i] != null) objectives[i].Died -= handlers[i];
            }
            handlers = null;
        }

        private void OnObjectiveDied(int index)
        {
            if (logic != null && logic.Complete(index))
                onAllComplete?.Invoke();
        }
    }
}
