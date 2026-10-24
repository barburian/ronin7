using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>P6-03: scales a world-space panel by <see cref="GameSettings.UiScale"/> (1..1.5x) around its own
    /// pivot. Captures the authored scale on Awake, so add it AFTER the panel's scale is set. Event-driven
    /// (SettingsChanged), nothing per frame.</summary>
    public class UiScaleFollower : MonoBehaviour
    {
        private Vector3 baseScale;
        private bool captured;

        private void Awake() { baseScale = transform.localScale; captured = true; }

        private void OnEnable()
        {
            if (!captured) { baseScale = transform.localScale; captured = true; }
            EventBus.Subscribe<SettingsChanged>(OnChanged);
            Apply(GameSettings.Active.UiScale);
        }

        private void OnDisable() => EventBus.Unsubscribe<SettingsChanged>(OnChanged);

        private void OnChanged(SettingsChanged e) => Apply(e.Settings.UiScale);

        private void Apply(float uiScale) => transform.localScale = baseScale * AccessibilityOptions.ClampUiScale(uiScale);
    }
}
