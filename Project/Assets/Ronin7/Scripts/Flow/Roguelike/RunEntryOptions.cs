using Ronin7.Core;

namespace Ronin7.Flow
{
    /// <summary>What the run entrance offers. Pure decision (EditMode-testable): with no saved
    /// snapshot there is one control that starts a new run; with one, RESUME RUN + ABANDON.</summary>
    public static class RunEntryOptions
    {
        public const string NewRunLabel = "ENTER THE FRACTURE";
        public const string ResumeLabel = "RESUME RUN";

        public static bool ShowAbandon(bool hasSnapshot) => hasSnapshot;
        public static string PrimaryLabel(bool hasSnapshot) => hasSnapshot ? ResumeLabel : NewRunLabel;

        /// <summary>True if a valid saved run exists (a stale/old-version file reads as none).</summary>
        public static bool HasSavedRun() => RunSnapshot.Read() != null;
    }
}
