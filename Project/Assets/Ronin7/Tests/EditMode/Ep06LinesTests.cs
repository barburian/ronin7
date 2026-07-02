using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Ep06LinesTests
    {
        // Every speaker used in Ep06Lines must appear here AND have an exact-match entry in
        // Audio/Tools/generate_voice.py SPEAKER_VOICES, or TTS silently falls back to a default voice.
        private static readonly HashSet<string> KnownCast = new HashSet<string>
        {
            "Kessler",
            "Ronin-7",
            "Khall",
            "Hollow Kings Operative 1",
            "Hollow Kings Operative 2",
            "Hollow Kings Operative 3",
            "Hollow Kings Operative 4",
            "Hollow Kings Operative 5",
            "Hollow Kings Operative 6",
            "Hollow Kings Operative 7",
            "Hollow Kings Operative 8",
            "Dominion Trooper 1",
            "Dominion Trooper 2",
            "Dominion Trooper 3",
            "Dominion Trooper 4",
            "Dominion Trooper 5",
            "Dominion Trooper 6",
            "Dominion Trooper 7",
            "Dominion Trooper 8",
            "Dominion Elite 1",
            "Dominion Elite 2",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Ep06Lines.SetIds)
            {
                var lines = Ep06Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Ep06Lines.SetIds)
            {
                var lines = Ep06Lines.Get(setId);
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
            foreach (string setId in Ep06Lines.SetIds)
            {
                var lines = Ep06Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Ep06Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Ep06Lines.SetIds)
            {
                var lines = Ep06Lines.Get(setId);
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
            Assert.AreEqual(0, Ep06Lines.Get("not_a_real_set").Length);
        }
    }
}
