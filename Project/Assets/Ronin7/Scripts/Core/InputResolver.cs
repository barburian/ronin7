using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Core
{
    /// <summary>
    /// Consolidated resolver for <see cref="InputActionReference"/> wiring. Replaces the four
    /// near-identical <c>Resolve</c> copies that lived across the Ship namespace.
    ///
    /// Strategy (mirrors the most-complete copy from <c>ShipController</c>):
    /// take the action from the serialized reference, enable its whole owning
    /// <see cref="InputActionAsset"/>, and return the asset's canonical instance so enabled ==
    /// bound == read. If the reference is broken/null, optionally fall back to locating the
    /// action by map+name on the reference's asset. The <paramref name="ownerTag"/> is used as
    /// the log prefix (e.g. <c>[Ship]</c>, <c>[ShipWeapon]</c>, <c>[Landing]</c>).
    /// </summary>
    public static class InputResolver
    {
        public static InputAction Resolve(
            InputActionReference reference,
            string mapName,
            string actionName,
            string ownerTag)
        {
            InputAction action = reference != null ? reference.action : null;

            if (action != null)
            {
                // Enable the whole asset (all maps): cheap, and guarantees the map containing this
                // action is active. Reading the asset's own action instance avoids the mixed-ref
                // "enabled one instance, read another" trap.
                var asset = action.actionMap != null ? action.actionMap.asset : null;
                if (asset != null)
                {
                    var canonical = asset.FindAction(action.id);
                    if (canonical != null) action = canonical;
                    asset.Enable();
                }
                else
                {
                    action.Enable();
                }
                return action;
            }

            // Fallback: reference didn't resolve (e.g. null/broken serialization). Try to find the
            // action by map + name on the reference's asset if we have one. Skipped when the
            // caller has no map/action name to fall back to.
            if (!string.IsNullOrEmpty(mapName) && !string.IsNullOrEmpty(actionName))
            {
                var refAsset = reference != null ? reference.asset : null;
                if (refAsset != null)
                {
                    var byName = refAsset.FindActionMap(mapName)?.FindAction(actionName);
                    if (byName != null)
                    {
                        refAsset.Enable();
                        Debug.LogWarning($"[{ownerTag}] '{mapName}/{actionName}' reference was unresolved; " +
                                         $"recovered by name from the input asset.");
                        return byName;
                    }
                }
            }

            Debug.LogError($"[{ownerTag}] Could not resolve input action '{mapName}/{actionName}'. " +
                           $"Assign the InputActionReference on the component.");
            return null;
        }
    }
}
