using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Ep20LinesTests
    {
        // Every speaker used in Ep20Lines must appear here AND have an exact-match entry in
        // Audio/Tools/generate_voice.py SPEAKER_VOICES, or TTS silently falls back to a default voice.
        private static readonly HashSet<string> KnownCast = new HashSet<string>
        {
            "Cipher",
            "Kessler",
            "Khall",
            "Vess",
            "Kess",
            "Tide Baron Squad Lead",
            "Rustfang Enforcer",
            "Ninefold Bank Commander",
            "Crimson Lotus Commander",
            "Crimson Lotus Enforcer 1",
            "Crimson Lotus Enforcer 2",
            "Crimson Lotus Enforcer 3",
            "Dominion Drone Network",
            "Dominion Drone 1",
            "Dominion Drone 2",
            "Tide Baron Squadron Leader",
            "Rustfang Pit Commander",
            "Hollow Kings Infiltrator",
            "Dominion Elite 1",
            "Dominion Elite 2",
            "Dominion Elite 3",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Ep20Lines.SetIds)
            {
                var lines = Ep20Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Ep20Lines.SetIds)
            {
                var lines = Ep20Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(lines[i].speaker), $"Set '{setId}' line {i} has no speaker.");
                    Assert.IsFalse(string.IsNullOrEmpty(lines[i].text), $"Set '{setId}' line {i} has no text.");
                    Assert.Greater(lines[i].seconds, 0f, $"Set '{setId}' line {i} has non-positive seconds.");
                }
            }
        }

        [Test]
        public void ClipNames_AreUniqueAcrossAllSets()
        {
            var seen = new HashSet<string>();
            foreach (string setId in Ep20Lines.SetIds)
            {
                var lines = Ep20Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Ep20Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Ep20Lines.SetIds)
            {
                var lines = Ep20Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    Assert.IsTrue(KnownCast.Contains(lines[i].speaker),
                        $"Set '{setId}' line {i} uses unknown speaker '{lines[i].speaker}' — add it to SPEAKER_VOICES in generate_voice.py and to KnownCast here.");
                }
            }
        }

        [Test]
        public void UnknownSetId_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, Ep20Lines.Get("not_a_real_set").Length);
        }
    }
}
