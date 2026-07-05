namespace Ronin7.Core
{
    /// <summary>
    /// Chapter-restructure name mapping (save-migration v1→v2): each campaign chapter's mission
    /// entry moved from its planet scene to its ship-prologue scene ("Ch02_Auction" →
    /// "Ch02_Prologue"). Pure string rule so <see cref="SaveSystem"/>'s migration and its tests need
    /// no scene tables: a chapter planet scene is any "ChNN_*" name; everything else (hub, boot,
    /// annexes, already-migrated prologues) passes through unchanged.
    /// </summary>
    public static class PrologueSceneNames
    {
        public static string Rename(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return scene;
            int underscore = scene.IndexOf('_');
            if (underscore <= 2 || underscore == scene.Length - 1) return scene;
            if (scene[0] != 'C' || scene[1] != 'h') return scene;
            for (int i = 2; i < underscore; i++)
                if (!char.IsDigit(scene[i])) return scene;
            string suffix = scene.Substring(underscore + 1);
            if (suffix == "Prologue") return scene; // already migrated
            return scene.Substring(0, underscore) + "_Prologue";
        }
    }
}
