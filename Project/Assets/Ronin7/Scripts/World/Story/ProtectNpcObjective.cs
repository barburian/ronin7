using System;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Minimal escort/protect primitive: fails the objective if <see cref="protectedHealth"/> dies while
    /// this component is enabled. Built for Ch02's Mira escape leg; reusable wherever a chapter needs to
    /// gate an escort NPC's survival (e.g. Ch06).
    ///
    /// Failure mechanism: <c>GameFlowManager</c> already ends the run whenever <c>EntityDied</c> is
    /// published for the player rig (see <c>GameFlowManager.OnEntityDied</c>, which compares
    /// <c>evt.Entity</c> against <c>VRRig.Instance.gameObject</c>). Rather than invent a second game-over
    /// path — or have this World-assembly component reach into Ronin7.Player for VRRig, which it does not
    /// reference — losing the protected NPC republishes that same EntityDied event for a player GameObject
    /// wired in by the scene builder (which already holds the rig reference from BuildRig). This reuses
    /// the existing game-over flow with zero changes to GameFlowManager/Health/VRRig.
    /// </summary>
    public class ProtectNpcObjective : MonoBehaviour
    {
        [SerializeField] private Health protectedHealth;
        [Tooltip("The player rig GameObject to report as died (matches VRRig.Instance.gameObject) so " +
                 "GameFlowManager's existing EntityDied game-over handling fires. Wired by the scene builder.")]
        [SerializeField] private GameObject playerEntity;

        /// <summary>Fires once, the moment the protected NPC dies.</summary>
        public event Action Failed;

        private void OnEnable()
        {
            if (protectedHealth != null) protectedHealth.Died += OnProtectedDied;
        }

        private void OnDisable()
        {
            if (protectedHealth != null) protectedHealth.Died -= OnProtectedDied;
        }

        private void OnProtectedDied()
        {
            Failed?.Invoke();
            if (playerEntity != null) EventBus.Publish(new EntityDied(playerEntity));
        }
    }
}
