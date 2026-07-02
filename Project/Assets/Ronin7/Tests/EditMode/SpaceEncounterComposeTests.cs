using NUnit.Framework;
using Ronin7.Ship;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Pure-math coverage for <see cref="SpaceEncounterManager.ComposeWave"/>: the ramp, the
    /// pool-safety clamp that keeps a wave from exceeding the shared bolt pool, and the optional
    /// elite cadence. No scene/MonoBehaviour needed — the method is side-effect free by design.
    /// </summary>
    public class SpaceEncounterComposeTests
    {
        // Convenience wrapper: elites off (elitesPerWave = 0) so size tests read cleanly.
        private static int Size(int waveIndex, int galaxies, int basePerWave, int addedPerWave,
            int addedPerGalaxy, int maxPerWave)
            => SpaceEncounterManager.ComposeWave(waveIndex, galaxies, basePerWave, addedPerWave,
                addedPerGalaxy, maxPerWave, elitesFromWave: 0, elitesPerWave: 0, out _);

        [Test]
        public void ComposeWave_RampsByWaveIndex()
        {
            // base 2, +1 per wave, no galaxy bonus, generous cap.
            Assert.AreEqual(2, Size(0, 0, 2, 1, 0, 12));
            Assert.AreEqual(3, Size(1, 0, 2, 1, 0, 12));
            Assert.AreEqual(4, Size(2, 0, 2, 1, 0, 12));
            Assert.AreEqual(5, Size(3, 0, 2, 1, 0, 12));
        }

        [Test]
        public void ComposeWave_ClampsAtMaxPerWave()
        {
            // base 8, +4 per wave → wave 3 wants 20, must clamp to the cap of 8.
            Assert.AreEqual(8, Size(3, 0, 8, 4, 0, 8));
            // A lower cap also bites a mid-size ramp.
            Assert.AreEqual(5, Size(10, 0, 2, 1, 0, 5));
        }

        [Test]
        public void ComposeWave_NeverEmpty_EvenWhenCapBelowBase()
        {
            // Pathological tuning (cap below base) still yields at least one ship.
            Assert.AreEqual(1, Size(0, 0, 8, 0, 0, 1));
        }

        [Test]
        public void ComposeWave_ScalesWithGalaxiesCompleted()
        {
            // +2 per completed galaxy; 3 galaxies adds 6 on top of the base 2.
            Assert.AreEqual(2, Size(0, 0, 2, 0, 2, 12));
            Assert.AreEqual(8, Size(0, 3, 2, 0, 2, 12));
            // ...but the galaxy bonus is still subject to the cap.
            Assert.AreEqual(6, Size(0, 9, 2, 0, 2, 6));
        }

        [Test]
        public void ComposeWave_NoElites_BeforeElitesFromWave()
        {
            SpaceEncounterManager.ComposeWave(0, 0, 4, 0, 0, 12,
                elitesFromWave: 2, elitesPerWave: 1, out int elites0);
            SpaceEncounterManager.ComposeWave(1, 0, 4, 0, 0, 12,
                elitesFromWave: 2, elitesPerWave: 1, out int elites1);
            Assert.AreEqual(0, elites0);
            Assert.AreEqual(0, elites1);
        }

        [Test]
        public void ComposeWave_ElitesAppear_AtAndAfterElitesFromWave()
        {
            SpaceEncounterManager.ComposeWave(2, 0, 4, 0, 0, 12,
                elitesFromWave: 2, elitesPerWave: 1, out int elites2);
            SpaceEncounterManager.ComposeWave(3, 0, 4, 0, 0, 12,
                elitesFromWave: 2, elitesPerWave: 1, out int elites3);
            Assert.AreEqual(1, elites2);
            Assert.AreEqual(1, elites3);
        }

        [Test]
        public void ComposeWave_ElitesNeverExceedTotal()
        {
            // Ask for 10 elites in a 2-ship wave: capped at the total.
            int total = SpaceEncounterManager.ComposeWave(0, 0, 2, 0, 0, 8,
                elitesFromWave: 0, elitesPerWave: 10, out int elites);
            Assert.AreEqual(2, total);
            Assert.AreEqual(2, elites);
            Assert.LessOrEqual(elites, total);
        }

        [Test]
        public void ComposeWave_ZeroElitesPerWave_DisablesElites()
        {
            // elitesPerWave 0 (the "no elite asset assigned" path) → never any elites, even past cadence.
            SpaceEncounterManager.ComposeWave(5, 0, 4, 0, 0, 12,
                elitesFromWave: 0, elitesPerWave: 0, out int elites);
            Assert.AreEqual(0, elites);
        }
    }
}
