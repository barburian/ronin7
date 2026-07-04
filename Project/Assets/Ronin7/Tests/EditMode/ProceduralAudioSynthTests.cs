using NUnit.Framework;
using Ronin7.Audio;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Covers the pure DSP core in <see cref="ProceduralAudioSynth"/>: output length, sample range,
    /// per-seed determinism, envelope decay (footstep) and loop-continuity (ambience).
    /// </summary>
    public class ProceduralAudioSynthTests
    {
        private const int SampleRate = 44100;

        [Test]
        public void GenerateFootstepThud_LengthMatchesDurationAndSampleRate()
        {
            var samples = ProceduralAudioSynth.GenerateFootstepThud(SampleRate, 0.25f, 1, FootstepSurface.Hard);
            Assert.AreEqual(SampleRate / 4, samples.Length);
        }

        [Test]
        public void GenerateFootstepThud_AllSamplesWithinUnitRange()
        {
            var samples = ProceduralAudioSynth.GenerateFootstepThud(SampleRate, 0.25f, 1, FootstepSurface.Soft);
            foreach (float s in samples)
                Assert.LessOrEqual(System.Math.Abs(s), 1f);
        }

        [Test]
        public void GenerateFootstepThud_SameSeed_IsDeterministic()
        {
            var a = ProceduralAudioSynth.GenerateFootstepThud(SampleRate, 0.25f, 42, FootstepSurface.Hard);
            var b = ProceduralAudioSynth.GenerateFootstepThud(SampleRate, 0.25f, 42, FootstepSurface.Hard);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void GenerateFootstepThud_DifferentSeeds_ProduceDifferentArrays()
        {
            var a = ProceduralAudioSynth.GenerateFootstepThud(SampleRate, 0.25f, 1, FootstepSurface.Hard);
            var b = ProceduralAudioSynth.GenerateFootstepThud(SampleRate, 0.25f, 2, FootstepSurface.Hard);
            CollectionAssert.AreNotEqual(a, b);
        }

        [Test]
        public void GenerateFootstepThud_EnvelopeDecays_LateWindowRmsLessThanEarlyWindow()
        {
            var samples = ProceduralAudioSynth.GenerateFootstepThud(SampleRate, 0.25f, 7, FootstepSurface.Hard);
            int window = samples.Length / 8;

            float Rms(int start, int len)
            {
                double sumSq = 0;
                for (int i = start; i < start + len; i++) sumSq += samples[i] * (double)samples[i];
                return (float)System.Math.Sqrt(sumSq / len);
            }

            float earlyRms = Rms(window, window);       // just after attack
            float lateRms = Rms(samples.Length - window, window); // tail of the decay
            Assert.Less(lateRms, earlyRms);
        }

        [TestCase(FootstepSurface.Soft)]
        [TestCase(FootstepSurface.Hard)]
        public void GenerateFootstepThud_BothSurfaces_ProduceNonSilentOutput(FootstepSurface surface)
        {
            var samples = ProceduralAudioSynth.GenerateFootstepThud(SampleRate, 0.25f, 3, surface);
            float peak = 0f;
            foreach (float s in samples) peak = System.Math.Max(peak, System.Math.Abs(s));
            Assert.Greater(peak, 0f);
        }

        [Test]
        public void GenerateAmbienceBed_LengthMatchesDurationAndSampleRate()
        {
            var samples = ProceduralAudioSynth.GenerateAmbienceBed(SampleRate, 2f, 1, AmbienceTheme.HangarHum);
            Assert.AreEqual(SampleRate * 2, samples.Length);
        }

        [Test]
        public void GenerateAmbienceBed_AllSamplesWithinUnitRange()
        {
            var samples = ProceduralAudioSynth.GenerateAmbienceBed(SampleRate, 2f, 1, AmbienceTheme.GardenWind);
            foreach (float s in samples)
                Assert.LessOrEqual(System.Math.Abs(s), 1f);
        }

        [Test]
        public void GenerateAmbienceBed_SameSeed_IsDeterministic()
        {
            var a = ProceduralAudioSynth.GenerateAmbienceBed(SampleRate, 2f, 42, AmbienceTheme.DreadDrone);
            var b = ProceduralAudioSynth.GenerateAmbienceBed(SampleRate, 2f, 42, AmbienceTheme.DreadDrone);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void GenerateAmbienceBed_DifferentSeeds_ProduceDifferentArrays()
        {
            var a = ProceduralAudioSynth.GenerateAmbienceBed(SampleRate, 2f, 1, AmbienceTheme.HangarHum);
            var b = ProceduralAudioSynth.GenerateAmbienceBed(SampleRate, 2f, 2, AmbienceTheme.HangarHum);
            CollectionAssert.AreNotEqual(a, b);
        }

        [TestCase(AmbienceTheme.HangarHum)]
        [TestCase(AmbienceTheme.GardenWind)]
        [TestCase(AmbienceTheme.DreadDrone)]
        public void GenerateAmbienceBed_IsLoopable_FirstAndLastSampleWithinEpsilon(AmbienceTheme theme)
        {
            var samples = ProceduralAudioSynth.GenerateAmbienceBed(SampleRate, 2f, 5, theme);
            Assert.AreEqual(samples[0], samples[samples.Length - 1], 1e-5f);
        }
    }
}
