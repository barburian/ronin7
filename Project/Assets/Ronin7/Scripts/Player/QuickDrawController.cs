using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    public enum QuickDrawState { Armed, Drawing, Primed, Spent }

    /// <summary>P4-04 S2 Iaido quick-draw (pure): Armed (sheathed) -> Drawing (grabbed) -> Primed (hand reached
    /// <see cref="MinSpeed"/> within <see cref="DrawWindow"/> s) -> Spent (first hit, or
    /// <see cref="HitWindow"/> s after the draw). Re-arms only on <see cref="Resheathe"/>.</summary>
    public class QuickDrawLogic
    {
        public const float MinSpeed = 4.5f, DrawWindow = 0.25f, HitWindow = 0.6f, DamageScale = 2.5f;

        private float drawStart;

        public QuickDrawState State { get; private set; } = QuickDrawState.Armed;

        public void BeginDraw(float now)
        {
            if (State != QuickDrawState.Armed) return;
            State = QuickDrawState.Drawing;
            drawStart = now;
        }

        /// <summary>Returns true on the tick the draw becomes Primed.</summary>
        public bool Tick(float handSpeed, float now)
        {
            float t = now - drawStart;
            if (State == QuickDrawState.Drawing)
            {
                if (t <= DrawWindow && handSpeed >= MinSpeed) { State = QuickDrawState.Primed; return true; }
                if (t > DrawWindow) State = QuickDrawState.Spent;
            }
            else if (State == QuickDrawState.Primed && t > HitWindow) State = QuickDrawState.Spent;
            return false;
        }

        /// <summary>Damage scale for a landed hit; spends a primed draw.</summary>
        public float Consume(float now)
        {
            Tick(0f, now);
            if (State != QuickDrawState.Primed) return 1f;
            State = QuickDrawState.Spent;
            return DamageScale;
        }

        public void Resheathe() => State = QuickDrawState.Armed;
    }

    /// <summary>
    /// P4-04 S2 Iaido quick-draw (passive gesture, boon-granted via <c>AbilityId.QuickDraw</c>). Draw from the
    /// <see cref="KatanaHolster"/> fast and the first hit within 0.6 s does x2.5 (on top of the damage caps,
    /// see <see cref="PlayerCombatModifiers.QuickDrawBonus"/>). Draw speed = the right hand's real speed, not
    /// the blade's: the hip-to-hand snap on grab would read as a blade-speed spike.
    /// </summary>
    public class QuickDrawController : MonoBehaviour
    {
        private readonly QuickDrawLogic logic = new QuickDrawLogic();
        private PlayerCombatModifiers mods;
        private PlayerCombatModifiers.HitBonus bonus;
        private KatanaHolster holster;
        private HandVelocityTracker rightHand;

        private void Awake()
        {
            bonus = Bonus;
            if (!AbilityAccess.Has(AbilityId.QuickDraw)) enabled = false;
        }

        private void OnDisable()
        {
            if (mods != null && mods.QuickDrawBonus == bonus) mods.QuickDrawBonus = null;
        }

        private void Update()
        {
            if (!Resolve()) return;
            var sword = holster.Sword;
            float now = Time.unscaledTime;
            if (holster.IsSheathed) logic.Resheathe();
            else if (sword != null && sword.IsHeld) logic.BeginDraw(now);
            if (logic.Tick(SkillRig.RealVelocity(rightHand).magnitude, now))
                Haptics.Pulse(XRNode.RightHand, 0.6f, 0.05f);
        }

        private bool Resolve()
        {
            if (mods == null)
            {
                mods = GetComponentInParent<PlayerCombatModifiers>();
                if (mods != null) mods.QuickDrawBonus = bonus;
            }
            if (holster == null) holster = GetComponentInParent<KatanaHolster>();
            if (rightHand == null && VRRig.Instance != null && VRRig.Instance.RightHand != null)
                rightHand = VRRig.Instance.RightHand.GetComponent<HandVelocityTracker>();
            return mods != null && holster != null;
        }

        private float Bonus(GameObject victim, out float postureScale)
        {
            postureScale = 1f;
            return logic.Consume(Time.unscaledTime);
        }
    }
}
