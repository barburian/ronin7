using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>Fire-and-forget controller haptics via the stable UnityEngine.XR device API.</summary>
    public static class Haptics
    {
        public static void Pulse(XRNode node, float amplitude, float duration)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return;
            if (device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                device.SendHapticImpulse(0u, amplitude, duration);
        }
    }
}
