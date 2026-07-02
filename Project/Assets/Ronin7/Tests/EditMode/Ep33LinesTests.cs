using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Ep33LinesTests
    {
        // Every speaker used in Ep33Lines must appear here AND have an exact-match entry in
        // Audio/Tools/generate_voice.py SPEAKER_VOICES, or TTS silently falls back to a default voice.
        private static readonly HashSet<string> KnownCast = new HashSet<string>
        {
            "Kessler",
            "Soren",
            "Mera Voss",
            "Captain Resh",
            "Gryph",
            "Vess",
            "Coral Vex",
            "Morrigan",
            "Dr. Heris",
            "Sallow",
            "Sable Dross",
            "Cassie-04",
            "Samurai-4",
            "Khall",
            "Maelgorn",
            "Dominion Strike Leader",
            "Dominion Trooper 1",
            "Dominion Trooper 2",
            "Obsidian Trooper 1",
            "Dominion Guard 1",
            "Privateers Commander",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Ep33Lines.SetIds)
            {
                var lines = Ep33Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Ep33Lines.SetIds)
            {
                var lines = Ep33Lines.Get(setId);
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
            foreach (string setId in Ep33Lines.SetIds)
            {
                var lines = Ep33Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Ep33Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Ep33Lines.SetIds)
            {
                var lines = Ep33Lines.Get(setId);
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
            Assert.AreEqual(0, Ep33Lines.Get("not_a_real_set").Length);
        }
    }
}
