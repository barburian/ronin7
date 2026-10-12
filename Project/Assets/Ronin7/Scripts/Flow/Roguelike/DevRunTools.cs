#if RONIN7_DEV
using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Flow
{
    /// <summary>
    /// P0-11 developer shortcuts for the G5 device smoke run. Compiled only with the RONIN7_DEV
    /// scripting define (set by the build script's -dev flag), so release builds never contain it.
    /// During a run: hold the right stick click 2 s to kill every enemy in the room; hold the left
    /// stick click 2 s to grant a random boon (it takes effect from the next node, like any pick).
    /// </summary>
    public class DevRunTools : MonoBehaviour
    {
        private const float HoldSeconds = 2f;
        private const int MaxKillPasses = 32; // the drip activates queued enemies as others die

        private InputAction killAllAction;
        private InputAction grantBoonAction;
        private float killHeld, boonHeld;
        private bool killFired, boonFired;
        private readonly List<Health> scratch = new List<Health>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject(nameof(DevRunTools));
            DontDestroyOnLoad(go);
            go.AddComponent<DevRunTools>();
            Debug.Log("[DevRunTools] RONIN7_DEV: hold right stick 2 s = clear room, hold left stick 2 s = random boon.");
        }

        private void OnEnable()
        {
            killAllAction = new InputAction("DevKillAll", InputActionType.Button, "<XRController>{RightHand}/{Primary2DAxisClick}");
            grantBoonAction = new InputAction("DevGrantBoon", InputActionType.Button, "<XRController>{LeftHand}/{Primary2DAxisClick}");
            killAllAction.Enable();
            grantBoonAction.Enable();
        }

        private void OnDisable()
        {
            killAllAction?.Dispose();
            grantBoonAction?.Dispose();
            killAllAction = grantBoonAction = null;
        }

        private void Update()
        {
            bool live = RunState.InRun && Time.timeScale > 0f;
            if (Held(live && killAllAction.IsPressed(), ref killHeld, ref killFired)) KillAllEnemies();
            if (Held(live && grantBoonAction.IsPressed(), ref boonHeld, ref boonFired))
                RunDirector.Instance?.DevGrantRandomBoon();
        }

        /// <summary>True once per hold, when it reaches <see cref="HoldSeconds"/>.</summary>
        private static bool Held(bool pressed, ref float held, ref bool fired)
        {
            held = HoldToConfirmButton.AdvanceHold(held, Time.unscaledDeltaTime, pressed);
            if (!pressed) fired = false;
            if (fired || !HoldToConfirmButton.HoldComplete(held, HoldSeconds)) return false;
            fired = true;
            return true;
        }

        private void KillAllEnemies()
        {
            Health player = VRRig.Instance != null ? VRRig.Instance.GetComponent<Health>() : null;
            int killed = 0;
            for (int pass = 0; pass < MaxKillPasses; pass++)
            {
                scratch.Clear();
                scratch.AddRange(Health.Active); // deaths mutate Active; iterate a copy
                int before = killed;
                foreach (Health h in scratch)
                {
                    if (h == null || h == player || !h.IsAlive) continue;
                    h.ApplyDamage(new DamageInfo(h.Max + h.Current + 1f, h.transform.position, Vector3.down, null));
                    killed++;
                }
                if (killed == before) break;
            }
            Debug.Log($"[DevRunTools] Killed {killed} enemies.");
        }
    }
}
#endif
