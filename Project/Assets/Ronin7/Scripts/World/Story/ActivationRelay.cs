using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Generic Trigger-step adapter: MissionDirector Trigger steps only SetActive(true) their target
    /// objects, so a Trigger step that needs to call arbitrary methods (e.g. an
    /// <see cref="Ronin7.World.EnemyWaveSpawner"/>'s <c>Begin()</c>) instead activates a GameObject
    /// carrying this component, which relays that activation into whatever persistent listeners are
    /// wired on <see cref="onEnabled"/> — the same "inactive until a Trigger step activates it" idiom
    /// <see cref="MemoryDiveEntryTrigger"/> uses, generalized to any method instead of one hardcoded
    /// call. The public <see cref="OnEnabled"/> accessor mirrors <c>ChapterOutro.OnActivated</c> /
    /// <c>HeatMeter.OnThresholdEvent</c> so builder code can wire persistent listeners onto it.
    /// </summary>
    public class ActivationRelay : MonoBehaviour
    {
        // Initialized inline (matches ChapterOutro.onActivated / HeatMeter.onThreshold) so a freshly
        // AddComponent'd relay is immediately safe to wire persistent listeners onto.
        [SerializeField] private UnityEvent onEnabled = new UnityEvent();
        public UnityEvent OnEnabled => onEnabled;

        private void OnEnable() => onEnabled?.Invoke();
    }
}
