using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>P4-04 S1 Riposte (pure): after a perfect parry, the next hit on THAT attacker within
    /// <see cref="Window"/> s deals x<see cref="DamageScale"/> damage and +100% posture. One use per parry.</summary>
    public class RiposteLogic
    {
        public const float Window = 1.2f, DamageScale = 1.75f, PostureScale = 2f;

        private GameObject target;
        private float until = -1f;

        public void Arm(GameObject attacker, float now)
        {
            target = attacker;
            until = now + Window;
        }

        /// <summary>Damage scale for a hit on <paramref name="victim"/> at <paramref name="now"/>; consumes the
        /// riposte when it applies. Hits on anyone else neither benefit nor consume it.</summary>
        public float Consume(GameObject victim, float now, out float postureScale)
        {
            postureScale = 1f;
            if (target == null || victim != target || now > until) return 1f;
            target = null;
            postureScale = PostureScale;
            return DamageScale;
        }
    }

    /// <summary>
    /// P4-04 S1 Riposte (passive, boon-granted via <c>RunState.GrantAbility(AbilityId.Riposte)</c>). Listens
    /// for <see cref="PerfectParry"/> and plugs <see cref="RiposteLogic"/> into
    /// <see cref="PlayerCombatModifiers.RiposteBonus"/> (applied inside the boon group). Self-disables unless
    /// granted, like the other ability controllers.
    /// </summary>
    public class RiposteController : MonoBehaviour
    {
        private readonly RiposteLogic logic = new RiposteLogic();
        private PlayerCombatModifiers mods;
        private PlayerCombatModifiers.HitBonus bonus;

        private void Awake()
        {
            bonus = Bonus;
            if (!AbilityAccess.Has(AbilityId.Riposte)) enabled = false;
        }

        private void OnEnable() => EventBus.Subscribe<PerfectParry>(OnPerfectParry);

        private void OnDisable()
        {
            EventBus.Unsubscribe<PerfectParry>(OnPerfectParry);
            if (mods != null && mods.RiposteBonus == bonus) mods.RiposteBonus = null;
        }

        private void OnPerfectParry(PerfectParry e)
        {
            if (mods == null)
            {
                mods = GetComponentInParent<PlayerCombatModifiers>();
                if (mods == null) return;
            }
            mods.RiposteBonus = bonus; // lazily: RunDirector may add the modifiers after Awake
            logic.Arm(e.Attacker, Time.time);
        }

        private float Bonus(GameObject victim, out float postureScale)
        {
            float scale = logic.Consume(victim, Time.time, out postureScale);
            if (scale > 1f) Haptics.Pulse(XRNode.RightHand, 0.9f, 0.08f);
            return scale;
        }
    }
}
