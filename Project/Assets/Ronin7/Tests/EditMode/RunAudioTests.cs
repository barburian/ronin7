using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Audio;
using Ronin7.Core;
using Ronin7.Editor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P6-01: music selector, volume-to-dB map, duck timing, missing-clip log, and the synthesized audio.</summary>
    public class RunAudioTests
    {
        // ---- MusicFor ----
        [TestCase(RoomKind.Combat, 0, false, MusicSlot.RustExplore)]
        [TestCase(RoomKind.Combat, 0, true, MusicSlot.RustCombat)]
        [TestCase(RoomKind.Boss, 0, false, MusicSlot.RustBoss)]
        [TestCase(RoomKind.Boss, 0, true, MusicSlot.RustBoss)]
        [TestCase(RoomKind.Elite, 1, false, MusicSlot.ProgramExplore)]
        [TestCase(RoomKind.Elite, 1, true, MusicSlot.ProgramCombat)]
        [TestCase(RoomKind.Boss, 1, false, MusicSlot.ProgramBoss)]
        [TestCase(RoomKind.Combat, 2, true, MusicSlot.GardenCombat)]
        [TestCase(RoomKind.Boss, 2, true, MusicSlot.GardenBoss)]
        [TestCase(RoomKind.Boss, 3, false, MusicSlot.RustBoss)]   // sectors wrap
        [TestCase(RoomKind.Combat, -1, false, MusicSlot.GardenExplore)]
        public void MusicFor_PicksSlot(RoomKind kind, int sector, bool aggro, MusicSlot expected) =>
            Assert.AreEqual(expected, AudioDirector.MusicFor(kind, sector, aggro));

        [Test]
        public void MusicFor_TwoArgIsNeverCombat()
        {
            Assert.AreEqual(MusicSlot.ProgramExplore, AudioDirector.MusicFor(RoomKind.Combat, 1));
            Assert.AreEqual(MusicSlot.GardenBoss, AudioDirector.MusicFor(RoomKind.Boss, 2));
        }

        // ---- VolumeToDb ----
        [Test] public void VolumeToDb_Zero_IsMinus80() => Assert.AreEqual(-80f, SettingsService.VolumeToDb(0f), 0.01f);
        [Test] public void VolumeToDb_One_IsZero() => Assert.AreEqual(0f, SettingsService.VolumeToDb(1f), 0.001f);
        [Test] public void VolumeToDb_Half_IsAboutMinus6() => Assert.AreEqual(-6.02f, SettingsService.VolumeToDb(0.5f), 0.01f);
        [Test] public void VolumeToDb_Negative_ClampsLikeZero() => Assert.AreEqual(-80f, SettingsService.VolumeToDb(-1f), 0.01f);

        // ---- duck timing ----
        [Test]
        public void Duck_IdleIsZeroDb() => Assert.AreEqual(0f, new MusicDuck().DbAt(5f));

        [Test]
        public void Duck_HoldsMinus6ForPointThreeSeconds()
        {
            var d = new MusicDuck();
            d.Trigger(10f);
            Assert.AreEqual(-6f, d.DbAt(10f));
            Assert.AreEqual(-6f, d.DbAt(10.29f));
        }

        [Test]
        public void Duck_ReleasesBackToZero()
        {
            var d = new MusicDuck();
            d.Trigger(10f);
            float mid = d.DbAt(10.3f + MusicDuck.ReleaseSeconds * 0.5f);
            Assert.That(mid, Is.GreaterThan(-6f).And.LessThan(0f));
            Assert.AreEqual(0f, d.DbAt(10.3f + MusicDuck.ReleaseSeconds + 0.01f));
        }

        [Test]
        public void Duck_RetriggerExtendsTheHold()
        {
            var d = new MusicDuck();
            d.Trigger(10f);
            d.Trigger(10.2f);
            Assert.AreEqual(-6f, d.DbAt(10.45f));
        }

        // ---- missing clip dev log ----
        [Test]
        public void NoteMissing_ReportsEachCueOnce()
        {
            string cue = "TestCue_" + System.Guid.NewGuid();
            Assert.IsTrue(AudioDirector.NoteMissing(cue));
            Assert.IsFalse(AudioDirector.NoteMissing(cue));
        }

        // ---- low health ----
        [TestCase(0.5f, 0f)]
        [TestCase(0.25f, 0f)]
        [TestCase(0.24f, 1f)]
        [TestCase(0.01f, 1f)]
        [TestCase(0f, 0f)]
        public void LowHealthTarget_BelowQuarterOnly(float fraction, float expected) =>
            Assert.AreEqual(expected, AudioDirector.LowHealthTarget(fraction));

        // ---- import policy ----
        [Test]
        public void ImportPolicy_SmallOneShotDecompressesAndPreloads()
        {
            var (load, preload) = RunAudioBuilder.ImportPolicy(50 * 1024, false, false);
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, load);
            Assert.IsTrue(preload);
        }

        [Test]
        public void ImportPolicy_LargeOrMusicStaysCompressed_LoopsDoNotPreload()
        {
            Assert.AreEqual(AudioClipLoadType.CompressedInMemory, RunAudioBuilder.ImportPolicy(300 * 1024, false, false).load);
            var (load, preload) = RunAudioBuilder.ImportPolicy(10 * 1024, true, true);
            Assert.AreEqual(AudioClipLoadType.CompressedInMemory, load);
            Assert.IsFalse(preload);
        }

        // ---- the synthesized music is a real, repeating, in-range loop ----
        [Test]
        public void Tracks_AreLoopLengthTonalAndBounded([Values(0, 1, 2)] int sector,
            [Values(MusicIntensity.Explore, MusicIntensity.Combat, MusicIntensity.Boss)] MusicIntensity inten)
        {
            var b = ProceduralMusicSynth.GenerateTrack(sector, inten);
            float secs = b.Length / (float)ProceduralMusicSynth.MusicRate;
            Assert.That(secs, Is.InRange(29.9f, 60f));
            Assert.AreEqual(ProceduralMusicSynth.LoopSeconds(sector, inten), secs, 0.01f);

            double sum = 0; float peak = 0;
            foreach (float v in b) { sum += v * v; peak = Mathf.Max(peak, Mathf.Abs(v)); }
            Assert.LessOrEqual(peak, 1f);
            Assert.Greater(Mathf.Sqrt((float)(sum / b.Length)), 0.04f, "silent");

            // The chord cycle repeats every 8 bars, so the signal correlates with itself at that lag. White noise sits near 0.
            float bar = secs / 16f;
            int lag = Mathf.RoundToInt(bar * 8f * ProceduralMusicSynth.MusicRate);
            double ab = 0, aa = 0, bb = 0;
            for (int i = 0; i < b.Length - lag; i += 3) { float x = b[i], y = b[i + lag]; ab += x * y; aa += x * x; bb += y * y; }
            Assert.Greater(ab / System.Math.Sqrt(aa * bb), 0.5, "not repeating, so not music-like");
        }

        [Test]
        public void ExploreAndCombatShareALoopLength_SoTheyCrossfadeInSync()
        {
            for (int s = 0; s < 3; s++)
                Assert.AreEqual(ProceduralMusicSynth.LoopSeconds(s, MusicIntensity.Explore), ProceduralMusicSynth.LoopSeconds(s, MusicIntensity.Combat));
        }

        [Test]
        public void Tracks_AreDeterministic() =>
            CollectionAssert.AreEqual(ProceduralMusicSynth.GenerateTrack(1, MusicIntensity.Combat), ProceduralMusicSynth.GenerateTrack(1, MusicIntensity.Combat));

        [Test]
        public void Cues_AreAudibleBoundedAndWindupsDiffer()
        {
            var windups = new List<float[]>();
            foreach (ProceduralMusicSynth.Cue c in System.Enum.GetValues(typeof(ProceduralMusicSynth.Cue)))
            {
                var b = ProceduralMusicSynth.GenerateCue(c);
                float peak = 0;
                foreach (float v in b) peak = Mathf.Max(peak, Mathf.Abs(v));
                Assert.That(peak, Is.InRange(0.3f, 1f), c.ToString());
                if (c.ToString().StartsWith("Windup")) windups.Add(b);
            }
            Assert.AreEqual(5, windups.Count);
            for (int i = 0; i < windups.Count; i++)
                for (int j = i + 1; j < windups.Count; j++)
                {
                    // Distinct pitch: zero-crossing rates (a pitch proxy) differ by at least 15%, or the lengths differ clearly.
                    float zi = ZeroCrossRate(windups[i]), zj = ZeroCrossRate(windups[j]);
                    bool pitchDiffers = Mathf.Abs(zi - zj) / Mathf.Max(zi, zj) > 0.15f;
                    bool lengthDiffers = Mathf.Abs(windups[i].Length - windups[j].Length) > 0.05f * ProceduralMusicSynth.CueRate;
                    Assert.IsTrue(pitchDiffers || lengthDiffers, "windups " + i + " and " + j + " are not distinct");
                }
        }

        [Test]
        public void LowHealthLoop_IsExactlyOneSecond() =>
            Assert.AreEqual(ProceduralMusicSynth.CueRate, ProceduralMusicSynth.GenerateCue(ProceduralMusicSynth.Cue.LowHealthLoop).Length);

        private static float ZeroCrossRate(float[] b)
        {
            int z = 0;
            for (int i = 1; i < b.Length; i++) if ((b[i - 1] < 0) != (b[i] < 0)) z++;
            return z / (float)b.Length;
        }
    }
}
