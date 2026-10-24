using Ronin7.Core;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>Fire-and-forget controller haptics via the stable UnityEngine.XR device API.
    /// The amplitude is scaled by the player's Haptic Intensity setting (P6-02); 0 sends nothing.</summary>
    public static class Haptics
    {
        // Device lookup per node is cached (InputDevice is a struct) and re-fetched only once it goes invalid.
        private static InputDevice left, right;
        private static bool leftImpulse, rightImpulse;

        public static void Pulse(XRNode node, float amplitude, float duration)
        {
            amplitude = ComfortOptions.HapticAmplitude(amplitude, GameSettings.Active.HapticIntensity);
            if (!ComfortOptions.HapticAudible(amplitude)) return;

            if (node == XRNode.LeftHand) { if (Send(node, ref left, ref leftImpulse, amplitude, duration)) return; }
            else if (node == XRNode.RightHand) { if (Send(node, ref right, ref rightImpulse, amplitude, duration)) return; }
            else
            {
                var d = InputDevices.GetDeviceAtXRNode(node);
                if (d.isValid && d.TryGetHapticCapabilities(out var c) && c.supportsImpulse) d.SendHapticImpulse(0u, amplitude, duration);
            }
        }

        private static bool Send(XRNode node, ref InputDevice device, ref bool impulse, float amplitude, float duration)
        {
            if (!device.isValid)
            {
                device = InputDevices.GetDeviceAtXRNode(node);
                impulse = device.isValid && device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse;
            }
            if (impulse) device.SendHapticImpulse(0u, amplitude, duration);
            return true;
        }
    }
}
