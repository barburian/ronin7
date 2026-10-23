using System.Collections.Generic;
using System.IO;
using System.Text;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using Ronin7.Player;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Ronin7.Editor
{
    /// <summary>
    /// P5-00: repeatable in-editor frame-budget measurement. Play mode, entered from Phase6_Boot with a
    /// run started (RunArena loaded). <see cref="Run"/> waits until <c>minEnemies</c> live enemies exist,
    /// freezes time on that tick, raises Camera.main to 1.6 m (no HMD pose in the editor), then for each
    /// tier (Mobile_RPAsset = quality 0, PC_RPAsset = 1) samples draws / SetPass / tris / particles /
    /// texture memory from the Game view, renders Camera.main through the full URP post stack to a PNG,
    /// and checks mean luminance + magenta ratio. GC is sampled first with time running.
    /// Results append to Project/Logs/PerfProbe/report.md (PNGs beside it); poll for the "done" line.
    /// Editor numbers are a proxy: device frame time (OVR Metrics) is still the G6 truth.
    /// G6 worst case needs the dev hooks: <see cref="SetDevDefine"/>(true), recompile, then on the boss
    /// node call DevRunTools.SpawnG6WorstCase; always SetDevDefine(false) afterwards.
    /// </summary>
    public static class PerfBudgetProbe
    {
        private const int WarmFrames = 5, GcFrames = 30, StatFrames = 10;
        private const float EyeHeight = 1.6f;

        public static string ReportDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "PerfProbe"));
        public static string ReportPath => Path.Combine(ReportDir, "report.md");

        [MenuItem("Tools/Ronin7/Perf/Measure Budget (play mode)")]
        private static void RunMenu() => Run("menu", 1);

        /// <summary>Starts the async measurement. Returns at once; the report gains a "done: label" line.</summary>
        public static void Run(string label, int minEnemies)
        {
            if (!Application.isPlaying) { Debug.LogError("[PerfBudgetProbe] Enter play mode first."); return; }
            Directory.CreateDirectory(ReportDir);
            new Session(label, minEnemies).Start();
        }

        /// <summary>Adds/removes RONIN7_DEV on the Standalone target (editor play mode compiles against it).
        /// Triggers a recompile. Restore it to false before committing; BuildMenu never writes it.</summary>
        public static void SetDevDefine(bool on)
        {
            var target = NamedBuildTarget.Standalone;
            var defines = new List<string>(PlayerSettings.GetScriptingDefineSymbols(target).Split(';'));
            defines.RemoveAll(d => d == "RONIN7_DEV" || d.Length == 0);
            if (on) defines.Add("RONIN7_DEV");
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
        }

        private sealed class Session
        {
            private readonly string label;
            private readonly int minEnemies;
            private readonly StringBuilder report = new StringBuilder();
            // Unity 6.5 splits draw calls by path; the old single "Draw Calls Count" counter reads 0.
            private static readonly string[] DrawCounters =
            {
                "Standard Draw Calls Count", "Standard Indirect Draw Calls Count", "Standard Instanced Draw Calls Count",
                "SRP Batcher Draw Calls Count", "BRG Draw Calls Count", "BRG Indirect Draw Calls Count",
                "Null Geometry Draw Calls Count", "Null Geometry Indirect Draw Calls Count",
            };
            private readonly List<ProfilerRecorder> drawParts = new List<ProfilerRecorder>();
            private ProfilerRecorder setPass, tris, gc, texMem;
            private int stage, tier, frames, lastFrame = -1;
            private long gcTotal, gcMax;
            private readonly PerfBudget.Sample[] samples = new PerfBudget.Sample[2];

            public Session(string label, int minEnemies) { this.label = label; this.minEnemies = minEnemies; }

            public void Start()
            {
                foreach (string n in DrawCounters)
                    drawParts.Add(ProfilerRecorder.StartNew(ProfilerCategory.Render, n));
                setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                texMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
                EditorApplication.update += Tick;
            }

            private void Tick()
            {
                if (!Application.isPlaying) { Finish("aborted: left play mode"); return; }
                if (Time.frameCount == lastFrame) return; // one step per player frame
                lastFrame = Time.frameCount;

                switch (stage)
                {
                    case 0: // wait for the room's enemies and the load fade to clear, then sample GC with time running
                        if (LiveEnemies() < minEnemies || Fading()) return;
                        MakePlayerInvulnerable(); // no HMD: the idle player dies in ~15 s and the run unloads mid-sample
                        Time.timeScale = 1f;
                        stage = 1; frames = 0;
                        return;
                    case 1:
                        if (frames++ < WarmFrames) return;
                        gcTotal += gc.LastValue; gcMax = System.Math.Max(gcMax, gc.LastValue);
                        if (frames < WarmFrames + GcFrames) return;
                        Time.timeScale = 0f; // freeze the spawn tick for the render stats
                        RaiseCamera();
                        stage = 2; tier = 0; ApplyTier(tier); frames = 0;
                        return;
                    case 2:
                        if (frames++ < WarmFrames) { ResetMax(); return; }
                        AccumulateMax();
                        if (frames < WarmFrames + StatFrames) return;
                        samples[tier].Particles = LiveParticles();
                        samples[tier].GcBytesPerFrame = gcTotal / GcFrames;
                        samples[tier].GcMaxBytes = gcMax;
                        samples[tier].TextureMb = texMem.LastValue / (1024f * 1024f);
                        CaptureFrame(tier);
                        if (tier == 0) { tier = 1; ApplyTier(tier); frames = 0; return; }
                        WriteReport();
                        Finish("done: " + label);
                        return;
                }
            }

            private long mDraws, mSetPass, mTris;
            private void ResetMax() { mDraws = mSetPass = mTris = 0; }
            private void AccumulateMax()
            {
                long d = 0;
                foreach (var r in drawParts) d += r.LastValue;
                mDraws = System.Math.Max(mDraws, d);
                mSetPass = System.Math.Max(mSetPass, setPass.LastValue);
                mTris = System.Math.Max(mTris, tris.LastValue);
                samples[tier].Draws = mDraws; samples[tier].SetPass = mSetPass; samples[tier].Triangles = mTris;
            }

            private static void ApplyTier(int t)
            {
                QualitySettings.SetQualityLevel(t, true);
                // Mobile tier = the Quest 3 path: Low knobs, post on (GraphicsDirector.PostEnabledFor(Quest3)).
                if (GraphicsDirector.Instance != null)
                    GraphicsDirector.Instance.SetQuality(t == 0 ? GraphicsQuality.Low : GraphicsQuality.High);
            }

            private static void RaiseCamera()
            {
                var cam = Camera.main;
                if (cam == null) return;
                // PoseFollower copies the (absent) HMD pose every frame and would snap it back to y=0.
                if (cam.TryGetComponent(out PoseFollower follower)) follower.enabled = false;
                var p = cam.transform.localPosition;
                cam.transform.localPosition = new Vector3(p.x, EyeHeight, p.z);

                // The hands + sword are always in view on device (budget: <= 6 draws), so pose the
                // controllers in a guard stance in front of the raised camera instead of on the floor.
                if (cam.transform.parent == null) return;
                foreach (var f in cam.transform.parent.GetComponentsInChildren<PoseFollower>())
                {
                    if (f.transform == cam.transform) continue;
                    bool left = f.name.StartsWith("Left");
                    f.enabled = false;
                    f.transform.localPosition = new Vector3(left ? -0.22f : 0.22f, EyeHeight - 0.22f, p.z + 0.45f);
                    f.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
                }
            }

            private static bool Fading()
            {
                var cam = Camera.main;
                if (cam == null) return true;
                foreach (var f in cam.GetComponentsInChildren<ScreenFader>())
                    foreach (var r in f.GetComponentsInChildren<Renderer>())
                        if (r.enabled) return true;
                return false;
            }

            private static void MakePlayerInvulnerable()
            {
                if (VRRig.Instance != null && VRRig.Instance.TryGetComponent(out Health h)) h.IncomingDamageMultiplier = 0f;
            }

            private static int LiveEnemies()
            {
                int n = 0;
                foreach (Health h in Health.Active)
                    if (h != null && h.IsAlive && h.GetComponent<Enemy>() != null) n++;
                return n;
            }

            private static int LiveParticles()
            {
                int n = 0;
                foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude))
                    n += ps.particleCount;
                return n;
            }

            private void CaptureFrame(int t)
            {
                var cam = Camera.main;
                if (cam == null) return;
                const int w = 960, h = 540;
                var rt = RenderTexture.GetTemporary(w, h, 24);
                var prevTarget = cam.targetTexture;
                var prevActive = RenderTexture.active;
                cam.targetTexture = rt;
                cam.Render(); // full URP pipeline incl. post
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);

                PerfBudget.ImageStats(tex.GetPixels32(), out samples[t].MeanLuminance, out samples[t].MagentaRatio);
                File.WriteAllBytes(Path.Combine(ReportDir, $"{Sanitize(label)}_{TierName(t)}.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            private void WriteReport()
            {
                report.AppendLine($"## {label} ({System.DateTime.Now:yyyy-MM-dd HH:mm}, {LiveEnemies()} enemies)");
                report.AppendLine("| Tier | Draws | SetPass | Tris | Particles | GC B/frame (max) | Tex MB | Luma | Magenta | Quest 3 budget |");
                report.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
                for (int t = 0; t < 2; t++)
                {
                    var s = samples[t];
                    string verdict = t == 0 ? PerfBudget.Verdict(s, PerfBudget.Quest3) : PerfBudget.Verdict(s, PerfBudget.Pcvr);
                    report.AppendLine($"| {TierName(t)} | {s.Draws} | {s.SetPass} | {s.Triangles} | {s.Particles} | {s.GcBytesPerFrame} ({s.GcMaxBytes}) | {s.TextureMb:0} | {s.MeanLuminance:0.000} | {s.MagentaRatio:0.0000} | {verdict} |");
                }
                File.AppendAllText(ReportPath, report.ToString());
            }

            private void Finish(string line)
            {
                EditorApplication.update -= Tick;
                foreach (var r in drawParts) r.Dispose();
                setPass.Dispose(); tris.Dispose(); gc.Dispose(); texMem.Dispose();
                File.AppendAllText(ReportPath, line + "\n\n");
                Debug.Log("[PerfBudgetProbe] " + line + " -> " + ReportPath);
            }

            private static string TierName(int t) => t == 0 ? "Mobile" : "PC";
            private static string Sanitize(string s) => string.Join("_", s.Split(Path.GetInvalidFileNameChars()));
        }
    }
}
