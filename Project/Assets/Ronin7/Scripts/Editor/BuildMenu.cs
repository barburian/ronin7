using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ronin7.Editor
{
    /// <summary>
    /// P0-12: one-click and batchmode player builds for the two shipping targets.
    ///   Menu:      Tools/Ronin7/Build/Quest (APK) and Tools/Ronin7/Build/Windows (PCVR) (+ Dev variants)
    ///   Batchmode: Unity.exe -batchmode -quit -projectPath Project -executeMethod Ronin7.Editor.BuildMenu.BuildWindows [-dev]
    /// Builds the enabled scenes of EditorBuildSettings into &lt;repo&gt;/Builds/&lt;target&gt;/Ronin7_&lt;version&gt;.&lt;ext&gt;
    /// and writes the build summary beside it as report.txt. In batchmode a failed build exits with
    /// code 1 so CI and scripts see it. -dev makes a Development build with the RONIN7_DEV define
    /// (the dev tools of P0-11); the define is passed per build and never written to Player Settings.
    /// Keep Android minSdk at 32, and never call CleanBuildCache (both are project gotchas).
    /// </summary>
    public static class BuildMenu
    {
        private const string DevDefine = "RONIN7_DEV";

        [MenuItem("Tools/Ronin7/Build/Quest (APK)", priority = 1)]
        private static void BuildQuestMenu() => Build(BuildTarget.Android, dev: false);

        [MenuItem("Tools/Ronin7/Build/Quest (APK, Dev)", priority = 2)]
        private static void BuildQuestDevMenu() => Build(BuildTarget.Android, dev: true);

        [MenuItem("Tools/Ronin7/Build/Windows (PCVR)", priority = 20)]
        private static void BuildWindowsMenu() => Build(BuildTarget.StandaloneWindows64, dev: false);

        [MenuItem("Tools/Ronin7/Build/Windows (PCVR, Dev)", priority = 21)]
        private static void BuildWindowsDevMenu() => Build(BuildTarget.StandaloneWindows64, dev: true);

        /// <summary>-executeMethod entry point. Add -dev on the command line for a Development build.</summary>
        public static void BuildQuest() => Build(BuildTarget.Android, IsDev(Environment.GetCommandLineArgs()));

        /// <summary>-executeMethod entry point. Add -dev on the command line for a Development build.</summary>
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, IsDev(Environment.GetCommandLineArgs()));

        internal static bool IsDev(string[] args) => args != null && args.Any(a => a == "-dev");

        internal static string TargetFolderName(BuildTarget target) =>
            target == BuildTarget.Android ? "Quest" : "Windows";

        /// <summary>&lt;repoRoot&gt;/Builds/&lt;Quest|Windows&gt;/Ronin7_&lt;version&gt;[-dev].&lt;apk|exe&gt;. Pure. P0-12 test seam.</summary>
        internal static string OutputPath(string repoRoot, BuildTarget target, string version, bool dev)
        {
            string ext = target == BuildTarget.Android ? "apk" : "exe";
            string file = $"Ronin7_{version}{(dev ? "-dev" : "")}.{ext}";
            return Path.Combine(repoRoot, "Builds", TargetFolderName(target), file);
        }

        /// <summary>The repository root: the folder above the Unity project (Application.dataPath is &lt;repo&gt;/Project/Assets).</summary>
        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        public static BuildReport Build(BuildTarget target, bool dev)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Fail(target, "No scenes are enabled in EditorBuildSettings.");
                return null;
            }

            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (EditorUserBuildSettings.activeBuildTarget != target &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
            {
                Fail(target, $"Could not switch the active build target to {target} (is the module installed?).");
                return null;
            }
            if (target == BuildTarget.Android)
            {
                EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
                EditorUserBuildSettings.buildAppBundle = false; // sideloadable APK for adb install / App Lab upload
            }

            string output = OutputPath(RepoRoot, target, PlayerSettings.bundleVersion, dev);
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                targetGroup = group,
                options = dev ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = dev ? new[] { DevDefine } : null,
            };

            Debug.Log($"[BuildMenu] Building {target}{(dev ? " (dev)" : "")} -> {output} with {scenes.Length} scene(s): {string.Join(", ", scenes)}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            WriteReport(report, target);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Fail(target, $"Build {report.summary.result} with {report.summary.totalErrors} error(s).");
                return report;
            }
            Debug.Log($"[BuildMenu] {target} build succeeded: {output} ({report.summary.totalSize / (1024f * 1024f):0.0} MB, {report.summary.totalTime}).");
            return report;
        }

        private static void WriteReport(BuildReport report, BuildTarget target)
        {
            var s = report.summary;
            string path = Path.Combine(RepoRoot, "Builds", TargetFolderName(target), "report.txt");
            string text =
                $"result: {s.result}\n" +
                $"platform: {s.platform}\n" +
                $"output: {s.outputPath}\n" +
                $"version: {PlayerSettings.bundleVersion} (Android code {PlayerSettings.Android.bundleVersionCode})\n" +
                $"options: {s.options}\n" +
                $"size_bytes: {s.totalSize}\n" +
                $"time: {s.totalTime}\n" +
                $"errors: {s.totalErrors}\n" +
                $"warnings: {s.totalWarnings}\n" +
                $"started_utc: {s.buildStartedAt:o}\n";
            File.WriteAllText(path, text);
        }

        private static void Fail(BuildTarget target, string message)
        {
            Debug.LogError($"[BuildMenu] {target}: {message}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
