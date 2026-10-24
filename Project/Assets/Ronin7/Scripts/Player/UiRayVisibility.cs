using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace Ronin7.Player
{
    /// <summary>
    /// BUG-45: hides the UI ray's laser while any <see cref="Grabber"/> on this rig holds something
    /// (the sword), shows it otherwise (Phase6_Boot has no sword, so it stays visible there). Only the
    /// visuals toggle — the XRRayInteractor stays enabled, because the boon panel is clicked with the
    /// grip (Select) and relies on the ray still hovering UI while the sword is held.
    /// </summary>
    public class UiRayVisibility : MonoBehaviour
    {
        private Grabber[] grabbers;
        private XRInteractorLineVisual visual;
        private LineRenderer line;
        private bool lastShown = true;

        private void Awake()
        {
            visual = GetComponent<XRInteractorLineVisual>();
            line = GetComponent<LineRenderer>();
        }

        private void Start() => grabbers = transform.root.GetComponentsInChildren<Grabber>(true);

        private void Update()
        {
            bool held = false;
            for (int i = 0; i < grabbers.Length && !held; i++)
                held = grabbers[i] != null && grabbers[i].IsHolding;
            if (!held == lastShown) return;
            lastShown = !held;
            if (visual != null) visual.enabled = lastShown;
            if (line != null) line.enabled = lastShown;
        }
    }
}
