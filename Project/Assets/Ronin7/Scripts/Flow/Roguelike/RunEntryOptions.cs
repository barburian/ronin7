using Ronin7.Core;

namespace Ronin7.Flow
{
    /// <summary>What the run entrance offers. Pure decision (EditMode-testable): with no saved
    /// snapshot there is one control that starts a new run; with one, RESUME RUN + ABANDON.</summary>
    public static class RunEntryOptions
    {
        public const string NewRunLabel = "ENTER THE FRACTURE";
        public const string ResumeLabel = "RESUME RUN";
        public const string AbandonLabel = "ABANDON RUN";
        public const string ConfirmAbandonLabel = "CONFIRM ABANDON?";

        /// <summary>BUG-27: ABANDON is two-click. A first click only arms it (relabels to
        /// <see cref="ConfirmAbandonLabel"/>); the second click abandons.</summary>
        public static bool AbandonConfirmed(bool armed) => armed;

        public static bool ShowAbandon(bool hasSnapshot) => hasSnapshot;
        public static string PrimaryLabel(bool hasSnapshot) => hasSnapshot ? ResumeLabel : NewRunLabel;

        /// <summary>True if a valid saved run exists (a stale/old-version file reads as none).</summary>
        public static bool HasSavedRun() => RunSnapshot.Read() != null;
    }
}
