using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>
    /// Opt-in feedback for G4 "Blade Clash" (see <see cref="Combat.BladeClash"/>): a stronger
    /// both-hands haptic pulse than a normal deflect (<see cref="CombatFeedbackController"/>'s
    /// 0.8 amplitude / 0.12s), since a genuine mutual clash is rarer and more dramatic than an
    /// ordinary parry. Kept as a separate, optional component rather than folded into
    /// CombatFeedbackController so scenes/rigs can adopt it independently, mirroring how other
    /// additive-but-off-by-default systems (e.g. sun-nav) are structured in this project.
    ///
    /// No dedicated clash audio: BladeClash is always published alongside SwordDeflected from the
    /// same deflect, and AudioDirector already plays its swordDeflect one-shot for that event —
    /// playing it a second time here would double/phase the same clip at the same point rather
    /// than intensify it, and there's no separate clash clip asset. Haptics alone carry the
    /// "notch up" distinction.
    /// </summary>
    public class BladeClashFeedback : MonoBehaviour
    {
        private void OnEnable() => EventBus.Subscribe<BladeClash>(OnClash);

        private void OnDisable() => EventBus.Unsubscribe<BladeClash>(OnClash);

        private void OnClash(BladeClash e)
        {
            Haptics.Pulse(XRNode.LeftHand, 1.0f, 0.15f);
            Haptics.Pulse(XRNode.RightHand, 1.0f, 0.15f);
        }
    }
}
