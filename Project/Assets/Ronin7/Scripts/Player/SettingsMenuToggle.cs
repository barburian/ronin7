using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Player
{
    /// <summary>
    /// Shows/hides the worldspace settings panel on the controller Menu/Options button. The panel
    /// starts hidden and, each time it opens, is re-placed in front of the player's head so it can't
    /// drift off into a corner of the play space.
    ///
    /// This MUST live on a separate, always-active object — never on the panel it toggles, or it
    /// would disable itself when the panel hides and could never reopen.
    /// </summary>
    public class SettingsMenuToggle : MonoBehaviour
    {
        [Tooltip("The settings panel root toggled on/off (e.g. the Settings Canvas).")]
        [SerializeField] private GameObject panel;

        [Tooltip("Optional. If unset, the left controller Menu button is used.")]
        [SerializeField] private InputActionReference toggleAction;

        [Tooltip("Metres in front of the head the panel appears when opened.")]
        [SerializeField] private float distance = 1.2f;

        private InputAction action;
        private InputAction owned; // created here only when no reference is assigned

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnEnable()
        {
            action = toggleAction != null ? toggleAction.action : null;
            if (action == null)
            {
                // No asset action assigned: bind the left controller's Menu button directly.
                owned = new InputAction("ToggleSettings", InputActionType.Button,
                    "<XRController>{LeftHand}/menuButton");
                action = owned;
            }
            action.performed += OnToggle;
            action.Enable();
        }

        private void OnDisable()
        {
            if (action != null) action.performed -= OnToggle;
            owned?.Disable();
            owned?.Dispose();
            owned = null;
            action = null;
        }

        private void OnToggle(InputAction.CallbackContext _)
        {
            if (panel == null) return;
            bool show = !panel.activeSelf;
            panel.SetActive(show);
            if (show) PlaceInFront();
        }

        /// <summary>Position the panel a fixed distance ahead of the head at eye height, facing the player.</summary>
        private void PlaceInFront()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();

            Vector3 pos = cam.transform.position + fwd * distance;
            panel.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd));
        }
    }
}
