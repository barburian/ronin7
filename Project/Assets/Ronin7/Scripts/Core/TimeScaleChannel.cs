namespace Ronin7.Core
{
    /// <summary>
    /// Stable owner keys for a <see cref="TimeScaleArbiter"/> request. Add a new entry whenever a new
    /// effect wants to slow time instead of writing <c>Time.timeScale</c>/<c>Time.fixedDeltaTime</c>
    /// directly — see the arbiter's class doc for why that direct-write pattern corrupts overlapping
    /// effects.
    /// </summary>
    public enum TimeScaleChannel
    {
        /// <summary><c>Ronin7.Player.OverdriveController</c>'s Ch9 hyper-reflex burst.</summary>
        Overdrive,

        /// <summary><c>Ronin7.Player.CombatFeedbackController</c>'s brief deflect slow-mo.</summary>
        DeflectSlowMo,

        /// <summary><c>Ronin7.Flow.RunPauseController</c>: scale 0 while the run is paused (focus loss,
        /// headset off, Menu button). Appended — this enum is append-only.</summary>
        Pause,
    }
}
