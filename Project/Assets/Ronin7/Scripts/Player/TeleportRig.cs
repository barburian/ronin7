using Ronin7.World;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Ronin7.Player
{
    /// <summary>
    /// P2-09 teleport: builds the stock XRI teleport chain on the live rig at runtime (no scene
    /// rebuild): <see cref="LocomotionMediator"/> (+ its required <see cref="XRBodyTransformer"/>) and a
    /// <see cref="TeleportationProvider"/> on the XR Origin, plus a projectile-curve
    /// <see cref="XRRayInteractor"/> on the left (move-stick) controller. The ray's select is driven
    /// manually by <see cref="ContinuousLocomotion"/> from the move stick (push forward = aim,
    /// release = teleport), so no new input bindings are needed. Targets: the arena floor's
    /// TeleportationArea on <see cref="ArenaGeometryBuilder.TeleportLayerMask"/>.
    /// </summary>
    public static class TeleportRig
    {
        public const int LayerMask = ArenaGeometryBuilder.TeleportLayerMask;

        /// <summary>Returns the (inactive) teleport ray, or null when the scene has no rig hands.</summary>
        public static XRRayInteractor Create(GameObject originRoot, out TeleportationProvider provider)
        {
            provider = originRoot.GetComponent<TeleportationProvider>();
            if (provider == null)
            {
                if (originRoot.GetComponent<LocomotionMediator>() == null) originRoot.AddComponent<LocomotionMediator>();
                provider = originRoot.AddComponent<TeleportationProvider>();
            }

            var rig = VRRig.Instance;
            if (rig == null || rig.LeftHand == null) return null;

            var go = new GameObject("TeleportRay");
            go.SetActive(false); // shown only while aiming
            go.transform.SetParent(rig.LeftHand, false);

            // Reuse the scene UI ray's shipped line material (a runtime Shader.Find can be stripped).
            var line = go.AddComponent<LineRenderer>();
            var uiLine = rig.RightHand != null ? rig.RightHand.GetComponent<LineRenderer>() : null;
            line.sharedMaterial = uiLine != null ? uiLine.sharedMaterial : new Material(Shader.Find("Sprites/Default"));
            line.widthMultiplier = 0.01f;

            var ray = go.AddComponent<XRRayInteractor>();
            ray.lineType = XRRayInteractor.LineType.ProjectileCurve;
            ray.interactionLayers = LayerMask;
            ray.enableUIInteraction = false;
            ray.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            ray.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;
            go.AddComponent<XRInteractorLineVisual>();
            return ray;
        }
    }
}
