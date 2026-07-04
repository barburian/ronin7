using System.Text.RegularExpressions;
using NUnit.Framework;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards <see cref="InputResolver.Resolve"/>: the happy path (valid reference resolves to the
    /// asset's canonical action and enables the asset), the by-name recovery fallback, and the
    /// severity fix where an unassigned OPTIONAL binding (no map/action name to fall back to, as
    /// used by CockpitRecenter/LandingApproach) logs a Warning instead of an Error.
    /// </summary>
    public class InputResolverTests
    {
        [Test]
        public void Resolve_ValidReference_EnablesAssetAndReturnsCanonicalAction()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionReference reference = null;
            try
            {
                var map = asset.AddActionMap("TestMap");
                var action = map.AddAction("TestAction", InputActionType.Button);
                reference = InputActionReference.Create(action);

                InputAction resolved = InputResolver.Resolve(reference, "TestMap", "TestAction", "Test");

                Assert.AreSame(asset.FindAction("TestMap/TestAction"), resolved);
                Assert.IsTrue(asset.enabled);
            }
            finally
            {
                asset.Disable();
                if (reference != null) Object.DestroyImmediate(reference);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Resolve_NullReferenceWithNonEmptyMapAndActionName_LogsErrorReturnsNull()
        {
            LogAssert.Expect(LogType.Error, new Regex(".*Could not resolve input action 'SomeMap/SomeAction'.*"));

            InputAction resolved = InputResolver.Resolve(null, "SomeMap", "SomeAction", "Test");

            Assert.IsNull(resolved);
        }

        [Test]
        public void Resolve_NullReferenceWithEmptyMapAndActionName_LogsWarningNotErrorReturnsNull()
        {
            // CockpitRecenter/LandingApproach pass empty map/action names for optional bindings —
            // an unassigned optional binding is expected configuration, not an error.
            LogAssert.Expect(LogType.Warning, new Regex(".*Could not resolve input action '/'.*"));

            InputAction resolved = InputResolver.Resolve(null, "", "", "Test");

            Assert.IsNull(resolved);
        }

        [Test]
        public void Resolve_ReferenceUnresolvedButAssetHasMatchingNamedAction_RecoversByNameAndLogsWarning()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionReference reference = null;
            try
            {
                var map = asset.AddActionMap("TestMap");
                var originalAction = map.AddAction("TestAction", InputActionType.Button);
                reference = InputActionReference.Create(originalAction);

                // Break the ID-based resolution the same way a stale/broken serialized reference
                // would: remove the action from its map. reference.action now resolves to null
                // (the cached instance's actionMap is cleared and the ID lookup no longer finds
                // it), while reference.asset still points at the still-alive asset — which is
                // exactly what makes InputResolver's by-name fallback reachable.
                originalAction.RemoveAction();

                // Re-add an action under the same map/name so the by-name fallback has something
                // to find (mirrors a reimported .inputactions asset where the GUID churned but the
                // map/action names stayed the same).
                var recoveredAction = map.AddAction("TestAction", InputActionType.Button);

                LogAssert.Expect(LogType.Warning, new Regex(".*recovered by name.*"));

                InputAction resolved = InputResolver.Resolve(reference, "TestMap", "TestAction", "Test");

                Assert.AreSame(recoveredAction, resolved);
                Assert.IsTrue(asset.enabled);
            }
            finally
            {
                asset.Disable();
                if (reference != null) Object.DestroyImmediate(reference);
                Object.DestroyImmediate(asset);
            }
        }
    }
}
