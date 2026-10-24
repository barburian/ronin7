using NUnit.Framework;
using Ronin7.Audio;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P6-02 haptic intensity, reduce flashes, slow-mo floor and vignette strength.</summary>
    public class ComfortOptionsTests
    {
        private static readonly string[] Keys =
        {
            SettingsService.KeyHaptic, SettingsService.KeySlowMo, SettingsService.KeyVignetteStrength,
        };
        private float[] saved;
        private int savedReduce;

        [SetUp]
        public void Snapshot()
        {
            saved = new float[Keys.Length];
            for (int i = 0; i < Keys.Length; i++) saved[i] = PlayerPrefs.GetFloat(Keys[i], float.NaN);
            savedReduce = PlayerPrefs.GetInt(SettingsService.KeyReduceFlashes, -1);
        }

        [TearDown]
        public void Restore()
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                if (float.IsNaN(saved[i])) PlayerPrefs.DeleteKey(Keys[i]);
                else PlayerPrefs.SetFloat(Keys[i], saved[i]);
            }
            if (savedReduce < 0) PlayerPrefs.DeleteKey(SettingsService.KeyReduceFlashes);
            else PlayerPrefs.SetInt(SettingsService.KeyReduceFlashes, savedReduce);
            PlayerPrefs.Save();
        }

        [Test]
        public void Defaults_MatchPreP6Behaviour()
        {
            var d = GameSettings.Default;
            Assert.AreEqual(1f, d.HapticIntensity);
            Assert.IsFalse(d.ReduceFlashes);
            Assert.AreEqual(ComfortOptions.SlowMoFloorMin, d.SlowMoFloor);
            Assert.AreEqual(1f, d.VignetteStrength);
        }

        [Test]
        public void NoPrefs_LoadsDefaults()
        {
            foreach (var k in Keys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.DeleteKey(SettingsService.KeyReduceFlashes);
            var l = SettingsService.Load();
            Assert.AreEqual(1f, l.HapticIntensity);
            Assert.IsFalse(l.ReduceFlashes);
            Assert.AreEqual(0.25f, l.SlowMoFloor);
            Assert.AreEqual(1f, l.VignetteStrength);
        }

        [Test]
        public void Roundtrip_AllFour()
        {
            var s = GameSettings.Default;
            s.HapticIntensity = 0.4f; s.ReduceFlashes = true; s.SlowMoFloor = 0.4f; s.VignetteStrength = 0.6f;
            SettingsService.Persist(s);
            var l = SettingsService.Load();
            Assert.AreEqual(0.4f, l.HapticIntensity, 1e-4f);
            Assert.IsTrue(l.ReduceFlashes);
            Assert.AreEqual(0.4f, l.SlowMoFloor, 1e-4f);
            Assert.AreEqual(0.6f, l.VignetteStrength, 1e-4f);
        }

        [Test]
        public void Load_ClampsHandEditedValues()
        {
            PlayerPrefs.SetFloat(SettingsService.KeyHaptic, 7f);
            PlayerPrefs.SetFloat(SettingsService.KeySlowMo, 0.01f);
            PlayerPrefs.SetFloat(SettingsService.KeyVignetteStrength, -3f);
            var l = SettingsService.Load();
            Assert.AreEqual(1f, l.HapticIntensity);
            Assert.AreEqual(0.25f, l.SlowMoFloor);
            Assert.AreEqual(0f, l.VignetteStrength);
        }

        [TestCase(0.8f, 1f, 0.8f)]
        [TestCase(0.8f, 0.5f, 0.4f)]
        [TestCase(0.8f, 0f, 0f)]
        [TestCase(1f, 3f, 1f)]
        [TestCase(0.8f, -1f, 0f)]
        public void HapticAmplitude_Scales(float amp, float intensity, float expected)
            => Assert.AreEqual(expected, ComfortOptions.HapticAmplitude(amp, intensity), 1e-5f);

        [Test]
        public void HapticAmplitude_Zero_MeansNoPulse() => Assert.IsFalse(ComfortOptions.HapticAudible(ComfortOptions.HapticAmplitude(1f, 0f)));

        [Test]
        public void HapticAmplitude_Default_IsAudible() => Assert.IsTrue(ComfortOptions.HapticAudible(ComfortOptions.HapticAmplitude(0.12f, 1f)));

        [TestCase(0.25f, 0.25f, 0.25f)] // default floor: deflect unchanged
        [TestCase(0.35f, 0.25f, 0.35f)] // default floor: overdrive unchanged
        [TestCase(0.4f, 0.25f, 0.4f)]   // default floor: zanshin unchanged
        [TestCase(0.25f, 0.5f, 0.5f)]   // gentlest: deflect lifted to the floor
        [TestCase(0.4f, 0.5f, 0.5f)]
        [TestCase(0.25f, 0.9f, 0.5f)]   // floor itself clamps to 0.5
        [TestCase(0.25f, 0.0f, 0.25f)]  // and to 0.25
        public void SlowMoScale_FloorsAndClamps(float baseScale, float floor, float expected)
            => Assert.AreEqual(expected, ComfortOptions.SlowMoScale(baseScale, floor), 1e-5f);

        [Test]
        public void ReduceFlashes_FlashAmountIsZero_OnlyWhenOn()
        {
            Assert.AreEqual(1f, ComfortOptions.FlashAmount(1f, false));
            Assert.AreEqual(0f, ComfortOptions.FlashAmount(1f, true));
        }

        [Test]
        public void ReduceFlashes_CapsRim_KeepsHueAndSteadyLevel()
        {
            var hot = new Color(1f, 0.4f, 0.25f) * 2.5f;
            var off = ComfortOptions.Rim(hot, false);
            Assert.AreEqual(hot, off, "off = untouched");
            var on = ComfortOptions.Rim(hot, true);
            Assert.AreEqual(ComfortOptions.ReducedRimMax, Mathf.Max(on.r, on.g, on.b), 1e-4f);
            Assert.AreEqual(hot.g / hot.r, on.g / on.r, 1e-4f, "hue preserved");
            var elite = new Color(1f, 0.1f, 0.1f) * 1.2f;
            Assert.AreEqual(elite, ComfortOptions.Rim(elite, true), "an elite rim is already under the cap");
            Assert.AreEqual(Color.black, ComfortOptions.Rim(Color.black, true));
        }

        [Test]
        public void VignetteIntensity_ScalesByStrength()
        {
            Assert.AreEqual(0.4f, ComfortOptions.VignetteIntensity(0.4f, 1f), 1e-5f);
            Assert.AreEqual(0.2f, ComfortOptions.VignetteIntensity(0.4f, 0.5f), 1e-5f);
            Assert.AreEqual(0f, ComfortOptions.VignetteIntensity(0.4f, 0f));
        }
    }
}
