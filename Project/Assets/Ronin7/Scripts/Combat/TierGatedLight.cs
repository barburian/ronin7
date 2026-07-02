using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>
    /// Disables its <see cref="Light"/> on the Low graphics tier. Authored onto the plasma-blade light
    /// at build time (every blade carries one), then gated at runtime so PCVR gets the local neon spill
    /// while Quest — where the URP additional-light budget is tight — drops it. Re-checks on every
    /// enable so a mid-session tier change or scene reload re-applies.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class TierGatedLight : MonoBehaviour
    {
        private void OnEnable()
        {
            var light = GetComponent<Light>();
            if (light != null) light.enabled = GraphicsRuntime.BladeLightsEnabled;
        }
    }
}
