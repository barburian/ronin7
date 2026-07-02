using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Helper that attaches a runtime <see cref="ComfortVignette"/> to the head camera, shared by
    /// flight and on-foot locomotion. Consolidates the previously duplicated <c>EnsureVignette</c>
    /// methods in <c>ShipController</c> and <c>ContinuousLocomotion</c>.
    /// </summary>
    public static class VignetteRig
    {
        /// <summary>
        /// Returns the <see cref="ComfortVignette"/> on <paramref name="cam"/>'s hierarchy,
        /// creating one as a child if it doesn't exist. Returns null when <paramref name="cam"/>
        /// is null (e.g. no XR rig yet — vignette simply stays disabled).
        /// </summary>
        public static ComfortVignette Ensure(Camera cam)
        {
            if (cam == null) return null;
            var vignette = cam.GetComponentInChildren<ComfortVignette>();
            if (vignette == null)
            {
                var go = new GameObject("Comfort Vignette");
                go.transform.SetParent(cam.transform, false);
                vignette = go.AddComponent<ComfortVignette>();
            }
            return vignette;
        }
    }
}
