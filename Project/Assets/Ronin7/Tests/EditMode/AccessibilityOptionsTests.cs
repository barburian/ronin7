using NUnit.Framework;
using Ronin7.Audio;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P6-03 accessibility: UI scale, assist multipliers, windup pulse, subtitles, Assisted tag.</summary>
    public class AccessibilityOptionsTests
    {
        private GameSettings savedActive, savedPrefs;

        [SetUp] public void Snapshot() { savedActive = GameSettings.Active; savedPrefs = SettingsService.Load(); RunTally.Reset(); }
        [TearDown] public void Restore() { GameSettings.Active = savedActive; SettingsService.Persist(savedPrefs); RunTally.Reset(); }

        [Test]
        public void Defaults_ChangeNothing()
        {
            var d = GameSettings.Default;
            Assert.AreEqual(1f, d.UiScale);
            Assert.AreEqual(1f, d.DamageTaken);
            Assert.AreEqual(1f, d.EnemyCooldownScale);
            Assert.IsTrue(d.Subtitles);
            Assert.IsFalse(AccessibilityOptions.IsAssisted(d));
        }

        [Test]
        public void Settings_Roundtrip()
        {
            var s = new GameSettings
            {
                SnapTurn = true, SnapTurnDegrees = 30f, ComfortVignette = false,
                SfxVolume = 0.3f, MusicVolume = 0.2f, AmbienceVolume = 0.1f, EyeHeightOffset = 0.07f,
                Quality = GraphicsQuality.Low, CombatBlood = false,
                Seated = true, LeftHanded = true, Locomotion = LocomotionMode.Teleport,
                HapticIntensity = 0.4f, ReduceFlashes = true, SlowMoFloor = 0.4f, VignetteStrength = 0.6f,
                UiScale = 1.3f, DamageTaken = 0.7f, EnemyCooldownScale = 1.25f, Subtitles = false, ReplayTutorial = true,
            };
            SettingsService.Persist(s);
            var l = SettingsService.Load();
            Assert.AreEqual(s.SnapTurn, l.SnapTurn);
            Assert.AreEqual(s.SnapTurnDegrees, l.SnapTurnDegrees, 1e-4f);
            Assert.AreEqual(s.ComfortVignette, l.ComfortVignette);
            Assert.AreEqual(s.SfxVolume, l.SfxVolume, 1e-4f);
            Assert.AreEqual(s.MusicVolume, l.MusicVolume, 1e-4f);
            Assert.AreEqual(s.AmbienceVolume, l.AmbienceVolume, 1e-4f);
            Assert.AreEqual(s.EyeHeightOffset, l.EyeHeightOffset, 1e-4f);
            Assert.AreEqual(s.Quality, l.Quality);
            Assert.AreEqual(s.CombatBlood, l.CombatBlood);
            Assert.AreEqual(s.Seated, l.Seated);
            Assert.AreEqual(s.LeftHanded, l.LeftHanded);
            Assert.AreEqual(s.Locomotion, l.Locomotion);
            Assert.AreEqual(s.HapticIntensity, l.HapticIntensity, 1e-4f);
            Assert.AreEqual(s.ReduceFlashes, l.ReduceFlashes);
            Assert.AreEqual(s.SlowMoFloor, l.SlowMoFloor, 1e-4f);
            Assert.AreEqual(s.VignetteStrength, l.VignetteStrength, 1e-4f);
            Assert.AreEqual(s.UiScale, l.UiScale, 1e-4f);
            Assert.AreEqual(s.DamageTaken, l.DamageTaken, 1e-4f);
            Assert.AreEqual(s.EnemyCooldownScale, l.EnemyCooldownScale, 1e-4f);
            Assert.AreEqual(s.Subtitles, l.Subtitles);
            Assert.AreEqual(s.ReplayTutorial, l.ReplayTutorial);
        }

        [Test]
        public void Settings_ReplayTutorial_Roundtrip()
        {
            Assert.IsFalse(GameSettings.Default.ReplayTutorial);
            PlayerPrefs.DeleteKey(SettingsService.KeyReplayTutorial);
            Assert.IsFalse(SettingsService.Load().ReplayTutorial); // absent on older installs
            var s = GameSettings.Default;
            s.ReplayTutorial = true;
            SettingsService.Persist(s);
            Assert.IsTrue(SettingsService.Load().ReplayTutorial);
            s.ReplayTutorial = false;
            SettingsService.Persist(s);
            Assert.IsFalse(SettingsService.Load().ReplayTutorial);
        }

        // loaded, tutorialDone, replay -> show. runsCompleted>0 already folds into tutorialDone (ResolveTutorialDone).
        [TestCase(true, false, false, true)]   // first run
        [TestCase(true, true, false, false)]   // veteran, toggle off
        [TestCase(true, true, true, true)]     // veteran, replay on
        [TestCase(false, false, true, false)]  // no save loaded: never
        public void ShouldShowTutorial_Decision(bool loaded, bool done, bool replay, bool expected) =>
            Assert.AreEqual(expected, MetaProgression.ShouldShowTutorial(loaded, done, replay));

        [TestCase(1.0f, 1.0f)]
        [TestCase(1.2f, 1.2f)]
        [TestCase(0.5f, 1.0f)]
        [TestCase(9f, 1.5f)]
        [TestCase(0f, 1.0f)]   // unset (default(GameSettings)) reads as 1x, never a zero-size panel
        [TestCase(float.NaN, 1.0f)]
        public void UiScale_Clamps(float v, float expected) => Assert.AreEqual(expected, AccessibilityOptions.ClampUiScale(v), 1e-5f);

        [TestCase(1.0f, 1.0f)]
        [TestCase(0.75f, 0.75f)]
        [TestCase(0.1f, 0.5f)]
        [TestCase(3f, 1.0f)]
        [TestCase(0f, 1.0f)]   // unset must never mean invulnerable
        [TestCase(-1f, 1.0f)]
        [TestCase(float.NaN, 1.0f)]
        public void AssistMultipliers_Clamp_DamageTaken(float v, float expected) => Assert.AreEqual(expected, AccessibilityOptions.ClampDamageTaken(v), 1e-5f);

        [TestCase(1.0f, 1.0f)]
        [TestCase(1.3f, 1.3f)]
        [TestCase(0.2f, 1.0f)]
        [TestCase(9f, 1.5f)]
        [TestCase(0f, 1.0f)]
        [TestCase(float.NaN, 1.0f)]
        public void AssistMultipliers_Clamp_Cooldown(float v, float expected) => Assert.AreEqual(expected, AccessibilityOptions.ClampCooldownScale(v), 1e-5f);

        [Test]
        public void AssistMultipliers_Apply()
        {
            Assert.AreEqual(8f, AccessibilityOptions.DamageTaken(16f, 0.5f), 1e-5f);
            Assert.AreEqual(16f, AccessibilityOptions.DamageTaken(16f, 1f), 1e-5f);
            Assert.AreEqual(1.2f, AccessibilityOptions.Cooldown(0.8f, 1.5f), 1e-5f);
            Assert.AreEqual(0.8f, AccessibilityOptions.Cooldown(0.8f, 1f), 1e-5f);
        }

        [Test]
        public void Pulse_Frequency_RisesFrom2To6()
        {
            Assert.AreEqual(2f, AccessibilityOptions.PulseHz(0f), 1e-5f);
            Assert.AreEqual(4f, AccessibilityOptions.PulseHz(0.5f), 1e-5f);
            Assert.AreEqual(6f, AccessibilityOptions.PulseHz(1f), 1e-5f);
            Assert.AreEqual(2f, AccessibilityOptions.PulseHz(-3f), 1e-5f);
            Assert.AreEqual(6f, AccessibilityOptions.PulseHz(3f), 1e-5f);
        }

        [Test]
        public void Pulse_Phase_AdvancesByHzTimesDt_AndWraps()
        {
            Assert.AreEqual(0.1f, AccessibilityOptions.AdvancePhase(0f, 0f, 0.05f, false), 1e-5f);   // 2 Hz * 0.05 s
            Assert.AreEqual(0.3f, AccessibilityOptions.AdvancePhase(0f, 1f, 0.05f, false), 1e-5f);   // 6 Hz * 0.05 s
            Assert.AreEqual(0.1f, AccessibilityOptions.AdvancePhase(0.9f, 0f, 0.1f, false), 1e-5f);  // wrapped 0.9 + 0.2
        }

        [Test]
        public void Pulse_ReduceFlashes_StaysUnderFlashThreshold()
        {
            // Constant 2 Hz regardless of progress (fewer than 3 flashes a second), sine-smooth.
            Assert.AreEqual(AccessibilityOptions.ReducedPulseHz, AccessibilityOptions.PulseHzFor(0f, true), 1e-5f);
            Assert.AreEqual(AccessibilityOptions.ReducedPulseHz, AccessibilityOptions.PulseHzFor(1f, true), 1e-5f);
            Assert.Less(AccessibilityOptions.ReducedPulseHz, 3f);
            Assert.AreEqual(6f, AccessibilityOptions.PulseHzFor(1f, false), 1e-5f);
        }

        [Test]
        public void Pulse_Scale_StaysInRange_AndNeverAboveOne()
        {
            float lo = 9f, hi = -9f, rlo = 9f, rhi = -9f;
            for (int i = 0; i <= 100; i++)
            {
                float p = i / 100f;
                float m = AccessibilityOptions.TelegraphPulse(p, false), r = AccessibilityOptions.TelegraphPulse(p, true);
                lo = Mathf.Min(lo, m); hi = Mathf.Max(hi, m); rlo = Mathf.Min(rlo, r); rhi = Mathf.Max(rhi, r);
            }
            Assert.AreEqual(1f, hi, 1e-3f);
            Assert.AreEqual(AccessibilityOptions.PulseMin, lo, 1e-3f);
            Assert.AreEqual(1f, rhi, 1e-3f);
            Assert.AreEqual(AccessibilityOptions.ReducedPulseMin, rlo, 1e-3f);
            Assert.Greater(rlo, lo); // reduced depth is shallower
            Assert.Greater(rhi - rlo, 0.2f); // still a readable modulation
        }

        [Test]
        public void Pulse_ReducedRim_StaysUnderCap()
        {
            // The pulse is applied AFTER the 1.2 cap, so its peak is the cap and it never blooms past it.
            var capped = ComfortOptions.Rim(new Color(2.5f, 1f, 0.6f), true);
            for (int i = 0; i <= 20; i++)
            {
                var c = capped * AccessibilityOptions.TelegraphPulse(i / 20f, true);
                Assert.LessOrEqual(Mathf.Max(c.r, Mathf.Max(c.g, c.b)), ComfortOptions.ReducedRimMax + 1e-4f);
            }
        }

        [Test]
        public void WeakPointBeat_SlowedWithReduceFlashes()
        {
            Assert.AreEqual(3f, AccessibilityOptions.WeakPointBeatHz(false));
            Assert.Less(AccessibilityOptions.WeakPointBeatHz(true), 3f);
        }

        [TestCase(1f, 1f, false)]
        [TestCase(0.9f, 1f, true)]
        [TestCase(1f, 1.1f, true)]
        [TestCase(0.5f, 1.5f, true)]
        [TestCase(0f, 0f, false)] // unset reads as default
        public void Assisted_Logic(float dmg, float cd, bool expected)
        {
            var s = GameSettings.Default;
            s.DamageTaken = dmg; s.EnemyCooldownScale = cd;
            Assert.AreEqual(expected, AccessibilityOptions.IsAssisted(s));
        }

        [Test]
        public void Assisted_TagSticksForTheRun_EvenIfSettingsRevert()
        {
            var s = GameSettings.Default;
            GameSettings.Active = s;
            RunTally.SampleAssist();
            Assert.IsFalse(RunTally.Assisted);
            s.DamageTaken = 0.8f; GameSettings.Active = s;
            RunTally.SampleAssist();
            Assert.IsTrue(RunTally.Assisted);
            GameSettings.Active = GameSettings.Default;
            RunTally.SampleAssist();
            Assert.IsTrue(RunTally.Assisted);
            RunTally.Reset();
            Assert.IsFalse(RunTally.Assisted);
        }

        [Test]
        public void RunLog_AssistedFlag_Serialised()
        {
            var e = new RunLogEntry { assisted = true, evt = "end" };
            StringAssert.Contains("\"assisted\":true", RunLog.ToLine(e));
            Assert.IsFalse(JsonUtility.FromJson<RunLogEntry>("{\"evt\":\"end\"}").assisted); // old lines load as not assisted
        }

        [Test]
        public void RunSummary_CapturesAssisted_AndClears()
        {
            RunSummary.Capture(2, false, 3, 1, 10, assisted: true);
            Assert.IsTrue(RunSummary.Assisted);
            Assert.AreEqual("ASSISTED RUN", RunSummary.AssistedLabel);
            RunSummary.Clear();
            Assert.IsFalse(RunSummary.Assisted);
        }

        [Test]
        public void RunSummary_OutcomeAccents_AreNotARedGreenPair()
        {
            // Win/lose must stay distinguishable without red vs green: blue vs amber.
            var win = RunSummary.OutcomeAccent(true); var lose = RunSummary.OutcomeAccent(false);
            Assert.Greater(win.b, win.r); Assert.Greater(win.b, win.g * 0.9f);
            Assert.Greater(lose.r, lose.b); Assert.Greater(lose.g, lose.b);
        }

        [Test]
        public void SubtitleLine_FormatsSpeaker()
        {
            Assert.AreEqual("Kessler: Hold the line.", SubtitleLine.Format("Kessler", "Hold the line."));
            Assert.AreEqual("Hold the line.", SubtitleLine.Format("", "Hold the line."));
            Assert.AreEqual("", SubtitleLine.Format("Kessler", null));
        }
    }
}
