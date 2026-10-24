using NUnit.Framework;
using Ronin7.Audio;
using Ronin7.Core;
using Ronin7.Player;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P2-09 seated / left-handed / teleport settings. Settings persist in PlayerPrefs, so the
    /// persistence tests snapshot and restore every settings key around each test.</summary>
    public class ComfortSettingsTests
    {
        private static readonly string[] IntKeys =
        {
            SettingsService.KeySnapTurn, SettingsService.KeyVignette, SettingsService.KeyQuality,
            SettingsService.KeyBlood, SettingsService.KeySeated, SettingsService.KeyLeftHanded,
            SettingsService.KeyLocomotion,
        };
        private static readonly string[] FloatKeys =
        {
            SettingsService.KeySnapDeg, SettingsService.KeySfx, SettingsService.KeyMusic,
            SettingsService.KeyAmbience, SettingsService.KeyEyeHeight,
        };

        private int?[] savedInts;
        private float?[] savedFloats;

        [SetUp]
        public void SnapshotPrefs()
        {
            savedInts = new int?[IntKeys.Length];
            for (int i = 0; i < IntKeys.Length; i++)
                savedInts[i] = PlayerPrefs.HasKey(IntKeys[i]) ? PlayerPrefs.GetInt(IntKeys[i]) : (int?)null;
            savedFloats = new float?[FloatKeys.Length];
            for (int i = 0; i < FloatKeys.Length; i++)
                savedFloats[i] = PlayerPrefs.HasKey(FloatKeys[i]) ? PlayerPrefs.GetFloat(FloatKeys[i]) : (float?)null;
        }

        [TearDown]
        public void RestorePrefs()
        {
            for (int i = 0; i < IntKeys.Length; i++)
            {
                if (savedInts[i].HasValue) PlayerPrefs.SetInt(IntKeys[i], savedInts[i].Value);
                else PlayerPrefs.DeleteKey(IntKeys[i]);
            }
            for (int i = 0; i < FloatKeys.Length; i++)
            {
                if (savedFloats[i].HasValue) PlayerPrefs.SetFloat(FloatKeys[i], savedFloats[i].Value);
                else PlayerPrefs.DeleteKey(FloatKeys[i]);
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void GameSettings_Default_IsSmoothLocomotionWithSnapTurn()
        {
            // D-10 / P6-05: the Moderate comfort rating assumes snap turn is on out of the box.
            Assert.IsTrue(GameSettings.Default.SnapTurn);
            Assert.AreEqual(LocomotionMode.Smooth, GameSettings.Default.Locomotion);
        }

        [Test]
        public void GameSettings_Roundtrip_NewFields()
        {
            var s = GameSettings.Default;
            s.Seated = true;
            s.LeftHanded = true;
            s.Locomotion = LocomotionMode.Teleport;

            SettingsService.Persist(s);
            var loaded = SettingsService.Load();

            Assert.IsTrue(loaded.Seated);
            Assert.IsTrue(loaded.LeftHanded);
            Assert.AreEqual(LocomotionMode.Teleport, loaded.Locomotion);
        }

        [Test]
        public void GameSettings_OldPrefsWithoutNewKeys_LoadWithDefaults()
        {
            // A pre-P2-09 install: old keys present (non-default values), new keys never written.
            var old = GameSettings.Default;
            old.SnapTurn = true;
            old.MusicVolume = 0.2f;
            SettingsService.Persist(old);
            PlayerPrefs.DeleteKey(SettingsService.KeySeated);
            PlayerPrefs.DeleteKey(SettingsService.KeyLeftHanded);
            PlayerPrefs.DeleteKey(SettingsService.KeyLocomotion);

            var loaded = SettingsService.Load();

            Assert.IsFalse(loaded.Seated);
            Assert.IsFalse(loaded.LeftHanded);
            Assert.AreEqual(LocomotionMode.Smooth, loaded.Locomotion, "D-10: Smooth is the default");
            Assert.IsTrue(loaded.SnapTurn, "old fields still load");
            Assert.AreEqual(0.2f, loaded.MusicVolume, 1e-4f);
        }

        [Test]
        public void HudMountFor_LeftHanded_IsRightController()
        {
            Assert.AreEqual(XRNode.RightHand, HandednessRig.OffHand(leftHanded: true));
        }

        [Test]
        public void HudMountFor_RightHanded_IsLeftController()
        {
            Assert.AreEqual(XRNode.LeftHand, HandednessRig.OffHand(leftHanded: false));
        }

        [Test]
        public void EffectiveEyeOffset_Seated_AddsLiftOnTopOfCalibration()
        {
            var s = GameSettings.Default;
            s.EyeHeightOffset = 0.1f;
            Assert.AreEqual(0.1f, s.EffectiveEyeOffset, 1e-4f);
            s.Seated = true;
            Assert.AreEqual(0.55f, s.EffectiveEyeOffset, 1e-4f);
        }
    }
}
