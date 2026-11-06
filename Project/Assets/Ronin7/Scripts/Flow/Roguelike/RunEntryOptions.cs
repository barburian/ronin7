using Ronin7.Core;

namespace Ronin7.Flow
{
    /// <summary>What the run entrance offers. Pure decision (EditMode-testable): NEW RUN is always
    /// there (with a saved run it first asks to abandon it); CONTINUE RUN appears only with a saved run.</summary>
    public static class RunEntryOptions
    {
        public const string NewRunLabel = "NEW RUN";
        public const string ContinueLabel = "CONTINUE RUN";
        public const string ConfirmAbandonLabel = "ABANDON SAVED RUN?";

        /// <summary>Seconds after arming during which NEW RUN ignores clicks, so a double trigger-pull cannot confirm.</summary>
        public const float ConfirmDelay = 0.6f;

        public enum NewRunStep { OpenWeaponSelect, ArmAbandon, AbandonThenWeaponSelect }

        /// <summary>NEW RUN click: no saved run goes straight to WEAPON SELECT (A15.13); a saved run needs a
        /// second, deliberate click (<paramref name="armed"/> and <paramref name="sinceArmed"/> past
        /// <see cref="ConfirmDelay"/>) before it is abandoned. A too-early click while armed does nothing new.</summary>
        public static NewRunStep NewRunClick(bool hasSnapshot, bool armed, float sinceArmed)
        {
            if (!hasSnapshot) return NewRunStep.OpenWeaponSelect;
            return armed && sinceArmed >= ConfirmDelay ? NewRunStep.AbandonThenWeaponSelect : NewRunStep.ArmAbandon;
        }

        public static bool ShowContinue(bool hasSnapshot) => hasSnapshot;

        public static string NewRunButtonLabel(bool armed) => armed ? ConfirmAbandonLabel : NewRunLabel;

        /// <summary>True if a valid saved run exists (a stale/old-version file reads as none).</summary>
        public static bool HasSavedRun() => RunSnapshot.Read() != null;
    }
}
