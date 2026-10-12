using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// A press-and-hold button for destructive choices (ABANDON RUN): it fires <see cref="Confirmed"/>
    /// only after being held for <see cref="holdSeconds"/>, filling <see cref="fill"/> as feedback.
    /// Uses unscaled time, so it works while the game is paused (timeScale 0).
    /// </summary>
    public class HoldToConfirmButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float holdSeconds = 1f;
        public Image fill;

        public event Action Confirmed;

        private bool pressed;
        private float held;

        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;
        public void OnPointerExit(PointerEventData eventData) => pressed = false;

        private void OnDisable()
        {
            pressed = false;
            held = 0f;
            if (fill != null) fill.fillAmount = 0f;
        }

        private void Update()
        {
            held = AdvanceHold(held, Time.unscaledDeltaTime, pressed);
            if (fill != null) fill.fillAmount = Mathf.Clamp01(held / holdSeconds);
            if (!HoldComplete(held, holdSeconds)) return;
            pressed = false;
            held = 0f;
            Confirmed?.Invoke();
        }

        /// <summary>Accumulates while pressed; releasing resets to 0. Pure. P0-04 test seam.</summary>
        internal static float AdvanceHold(float held, float dt, bool pressed) => pressed ? held + dt : 0f;

        internal static bool HoldComplete(float held, float holdSeconds) => held >= holdSeconds;
    }
}
