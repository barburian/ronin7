using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>
    /// Sits on the player rig root. Holds the multiplier <see cref="BladeDamager"/> applies to the
    /// wielder's swing damage. Defaults to 1 (no effect) so every existing scene/test that doesn't
    /// place this component — or an ability that hasn't unlocked yet — is unaffected. Ch7's
    /// weakpoint-sight (<c>Ronin7.Player.WeakpointSight</c>) is the first ability to drive this; the
    /// coupling runs Player -> writes -> this component -> Combat reads, since Combat doesn't
    /// reference Player.
    /// </summary>
    public class PlayerCombatModifiers : MonoBehaviour
    {
        public float DamageMultiplier { get; set; } = 1f;
    }
}
