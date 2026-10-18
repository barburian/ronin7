#if RONIN7_DEV
using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Player;
using Ronin7.Ship;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Flow
{
    /// <summary>
    /// P0-11 developer shortcuts for the G5 device smoke run. Compiled only with the RONIN7_DEV
    /// scripting define (set by the build script's -dev flag), so release builds never contain it.
    /// During a run: hold the right stick click 2 s to kill every enemy in the room; hold the left
    /// stick click 2 s to grant a random boon (it takes effect from the next node, like any pick).
    /// Outside a run (hub/menu): hold BOTH stick clicks 2 s to reset the tutorial in memory (BUG-32).
    /// In a run, BOTH stick clicks 2 s = P4-00e G6 worst case: off a Boss node it jumps to the next
    /// boss; on the Boss node it forces boss phase 2 (adds release), a Volatile ring and 8 bolts.
    /// P4-04: keyboard 1-5 equips an Art (1 Charged Slash, 2 Blade Throw, 3 Kiai Palm, 4 Chain Hook,
    /// 5 Element Imbue), 0 clears both slots; <see cref="DevEquipArt"/> does the same from the editor/MCP.
    /// </summary>
    public class DevRunTools : MonoBehaviour
    {
        private const float HoldSeconds = 2f;
        private const int MaxKillPasses = 32; // the drip activates queued enemies as others die

        private InputAction killAllAction;
        private InputAction grantBoonAction;
        private float killHeld, boonHeld, tutHeld;
        private bool killFired, boonFired, tutFired, g6Fired;
        private float g6Held;
        private readonly List<Health> scratch = new List<Health>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject(nameof(DevRunTools));
            DontDestroyOnLoad(go);
            go.AddComponent<DevRunTools>();
            Debug.Log("[DevRunTools] RONIN7_DEV: hold right stick 2 s = clear room, hold left stick 2 s = random boon, hold both 2 s = G6 worst case.");
            MetaProgression.DevUnlockAllPacks = true; // P4-05: until the P4-10 Shrine sells packs
            Debug.Log("[DevRunTools] RONIN7_DEV: all archetype packs (Bleed/Storm/Executioner) unlocked for this session.");
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
            bool kill = killAllAction.IsPressed(), boon = grantBoonAction.IsPressed();
            if (Held(live && kill && !boon, ref killHeld, ref killFired)) KillAllEnemies();
            if (Held(live && boon && !kill, ref boonHeld, ref boonFired))
                RunDirector.Instance?.DevGrantRandomBoon();
            if (Held(live && kill && boon, ref g6Held, ref g6Fired)) SpawnG6WorstCase();
            PollArtKeys();
            bool both = !RunState.InRun && killAllAction.IsPressed() && grantBoonAction.IsPressed();
            if (Held(both, ref tutHeld, ref tutFired))
            {
                MetaProgression.ResetTutorialInMemory();
                Debug.Log("[DevRunTools] Tutorial reset in memory: start a run to see the prompts.");
            }
        }

        private static readonly string[] DevArts =
            { ArtId.ChargedSlash, ArtId.BladeThrow, ArtId.KiaiPalm, ArtId.ChainHook, ArtId.ElementImbue };

        private static void PollArtKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit0Key.wasPressedThisFrame) { ArtSlots.Equip(ArtSlot.Weapon, ""); ArtSlots.Equip(ArtSlot.Offhand, ""); }
            if (kb.digit1Key.wasPressedThisFrame) DevEquipArt(DevArts[0]);
            if (kb.digit2Key.wasPressedThisFrame) DevEquipArt(DevArts[1]);
            if (kb.digit3Key.wasPressedThisFrame) DevEquipArt(DevArts[2]);
            if (kb.digit4Key.wasPressedThisFrame) DevEquipArt(DevArts[3]);
            if (kb.digit5Key.wasPressedThisFrame) DevEquipArt(DevArts[4]);
        }

        /// <summary>P4-04: equip an Art into its slot (S4/S5 Weapon, S6-S8 Off-hand). Element Imbue also grants
        /// both elements when none is owned yet (P4-05 boons will own them for real).</summary>
        public static void DevEquipArt(string id)
        {
            bool weapon = id == ArtId.ChargedSlash || id == ArtId.BladeThrow;
            ArtSlots.Equip(weapon ? ArtSlot.Weapon : ArtSlot.Offhand, id);
            if (id == ArtId.ElementImbue && VRRig.Instance != null
                && VRRig.Instance.TryGetComponent(out PlayerCombatModifiers mods) && mods.OwnedElements == ImbueElement.None)
                mods.OwnedElements = ImbueElement.Bleed | ImbueElement.Storm;
            Debug.Log($"[DevRunTools] Equipped Art '{id}' ({(weapon ? "Weapon" : "Off-hand")}).");
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

        /// <summary>P4-00e G6 worst case (boss P2 + adds + Volatile ring + 8 bolts + hit VFX), reusing the
        /// real paths: the boss's phase interceptor releases the adds, the arena's Volatile ring pool,
        /// and the shared <see cref="ProjectilePool"/>. Public so it can be driven from the editor.</summary>
        public void SpawnG6WorstCase()
        {
            if (RunDirector.Instance != null && RunDirector.Instance.DevJumpToBoss()) return; // trigger again on the boss node
            Health player = VRRig.Instance != null ? VRRig.Instance.GetComponent<Health>() : null;
            Health boss = null;
            foreach (Health h in Health.Active)
                if (h != null && h != player && h.IsAlive && h.DeathInterceptor != null && h.GetComponent<Enemy>() != null) { boss = h; break; }
            if (boss == null) { Debug.LogWarning("[DevRunTools] G6: no live multi-phase boss (wait for it to spawn)."); return; }

            // Lethal blow -> Enemy.AdvancePhase -> BossPhaseChanged -> the arena releases the adds.
            // Max x 100: the boss's weakness DamageFilter scales a resisted hit down, so Current + 1 is not lethal.
            boss.ApplyDamage(new DamageInfo(boss.Max * 100f, boss.transform.position + Vector3.up, Vector3.down, player != null ? player.gameObject : null));
            Vector3 c = boss.transform.position;
            if (boss.transform.parent != null) boss.transform.parent.SendMessage("DevStartVolatileRing", c, SendMessageOptions.DontRequireReceiver);

            var pool = FindAnyObjectByType<ProjectilePool>();
            Camera cam = Camera.main;
            if (pool == null || cam == null) { Debug.LogWarning("[DevRunTools] G6: no ProjectilePool/Camera; bolts skipped."); return; }
            Vector3 head = cam.transform.position;
            for (int i = 0; i < 8; i++)
            {
                // Slow, long-lived ring of bolts at 6 m so all 8 are in flight together for ~10 s.
                Vector3 from = head + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * 6f;
                pool.Fire(boss.gameObject, from, Quaternion.LookRotation(head - from), 0.6f, 1f, 12f, 0.12f, new Color(2f, 1.4f, 0.2f), null);
            }
            Debug.Log($"[DevRunTools] G6 worst case: boss '{boss.name}' forced to phase 2, Volatile ring + 8 bolts.");
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
