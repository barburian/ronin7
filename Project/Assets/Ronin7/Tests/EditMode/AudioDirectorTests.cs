using NUnit.Framework;
using Ronin7.Audio;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Covers <see cref="AudioDirector"/>'s pure decision seams added for the roguelike run: per-sector
    /// ambience selection (<see cref="AudioDirector.SectorAmbience"/>) and the explore/combat music
    /// decision (<see cref="AudioDirector.CombatOrExploreMusic"/>). Both are plain data-in/data-out
    /// functions with no MonoBehaviour/EventBus dependency, so they're covered directly rather than by
    /// spinning up the full singleton + scene.
    /// </summary>
    public class AudioDirectorTests
    {
        private static AudioClip NewClip(string name) => AudioClip.Create(name, 1, 1, 1000, false);

        // ---- SectorAmbience: sector -> theme mapping (0 = rust, 1 = program, 2 = garden) ----

        [Test]
        public void SectorAmbience_Sector0_ReturnsRust()
        {
            var rust = NewClip("rust");
            var program = NewClip("program");
            var garden = NewClip("garden");
            Assert.AreSame(rust, AudioDirector.SectorAmbience(0, rust, program, garden));
        }

        [Test]
        public void SectorAmbience_Sector1_ReturnsProgram()
        {
            var rust = NewClip("rust");
            var program = NewClip("program");
            var garden = NewClip("garden");
            Assert.AreSame(program, AudioDirector.SectorAmbience(1, rust, program, garden));
        }

        [Test]
        public void SectorAmbience_Sector2_ReturnsGarden()
        {
            var rust = NewClip("rust");
            var program = NewClip("program");
            var garden = NewClip("garden");
            Assert.AreSame(garden, AudioDirector.SectorAmbience(2, rust, program, garden));
        }

        [Test]
        public void SectorAmbience_WrapsLikeArenaRoomLibrary_ForSector()
        {
            // RunMapGenerator.SectorCount is 3; a sector value outside 0..2 should never be reachable
            // in practice, but ArenaRoomLibrary.ForSector wraps rather than throwing — this mirrors that
            // so the two never disagree about which biome a given sector index means.
            var rust = NewClip("rust");
            var program = NewClip("program");
            var garden = NewClip("garden");
            Assert.AreSame(rust, AudioDirector.SectorAmbience(3, rust, program, garden));
            Assert.AreSame(program, AudioDirector.SectorAmbience(4, rust, program, garden));
            Assert.AreSame(rust, AudioDirector.SectorAmbience(-3, rust, program, garden));
        }

        [Test]
        public void SectorAmbience_UnassignedClips_ReturnsNull_StaysSilent()
        {
            // Every clip field on AudioDirector is optional (unassigned = silent) — the mapping must
            // not throw or substitute a different theme just because one slot is empty.
            Assert.IsNull(AudioDirector.SectorAmbience(1, null, null, null));
        }

        // ---- CombatOrExploreMusic: aggro -> music bed decision ----

        [Test]
        public void CombatOrExploreMusic_NoAggro_ReturnsExplore()
        {
            var explore = NewClip("explore");
            var combat = NewClip("combat");
            Assert.AreSame(explore, AudioDirector.CombatOrExploreMusic(false, explore, combat));
        }

        [Test]
        public void CombatOrExploreMusic_Aggro_ReturnsCombat()
        {
            var explore = NewClip("explore");
            var combat = NewClip("combat");
            Assert.AreSame(combat, AudioDirector.CombatOrExploreMusic(true, explore, combat));
        }
    }
}
