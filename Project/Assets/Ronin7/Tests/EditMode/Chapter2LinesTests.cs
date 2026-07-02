using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Chapter2LinesTests
    {
        // Every speaker used in Chapter2Lines must appear here AND have an exact-match entry in
        // Audio/Tools/generate_voice.py SPEAKER_VOICES, or TTS silently falls back to a default voice.
        private static readonly HashSet<string> KnownCast = new HashSet<string>
        {
            "Kessler",
            "Ronin-7",
            "Resh",
            "Broker",
            "Iris",
            "Mira",
            "Enforcer",
        };

        // The exact set IDs Chapter2Builder wires into its mission steps, kept in sync by hand — if this
        // list and Chapter2Lines.SetIds diverge, either the builder is missing a beat or a set is orphaned.
        private static readonly string[] BuilderReferencedSetIds =
        {
            "ch2_beat0_briefing",
            "ch2_beat1_undermarket",
            "ch2_beat2_resh_confront",
            "ch2_beat2_resh_recruit",
            "ch2_beat1_broker",
            "ch2_beat3_iris",
            "ch2_beat4_reveal",
            "ch2_beat5_reunion",
            "ch2_beat5_kept",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Chapter2Lines.SetIds)
            {
                var lines = Chapter2Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Chapter2Lines.SetIds)
            {
                var lines = Chapter2Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(lines[i].speaker), $"Set '{setId}' line {i} has no speaker.");
                    Assert.IsFalse(string.IsNullOrEmpty(lines[i].text), $"Set '{setId}' line {i} has no text.");
                    Assert.Greater(lines[i].seconds, 0f, $"Set '{setId}' line {i} has non-positive seconds.");
                }
            }
        }

        [Test]
        public void NoLine_ContainsAnEmDash()
        {
            foreach (string setId in Chapter2Lines.SetIds)
            {
                var lines = Chapter2Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    Assert.IsFalse(lines[i].text.Contains("—"),
                        $"Set '{setId}' line {i} contains an em-dash: \"{lines[i].text}\"");
                }
            }
        }

        [Test]
        public void ClipNames_AreUniqueAcrossAllSets()
        {
            var seen = new HashSet<string>();
            foreach (string setId in Chapter2Lines.SetIds)
            {
                var lines = Chapter2Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Chapter2Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Chapter2Lines.SetIds)
            {
                var lines = Chapter2Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    Assert.IsTrue(KnownCast.Contains(lines[i].speaker),
                        $"Set '{setId}' line {i} uses unknown speaker '{lines[i].speaker}' — add it to SPEAKER_VOICES in generate_voice.py and to KnownCast here.");
                }
            }
        }

        [Test]
        public void EveryBuilderReferencedSetId_ExistsInSetIds()
        {
            var known = new HashSet<string>(Chapter2Lines.SetIds);
            foreach (string setId in BuilderReferencedSetIds)
                Assert.IsTrue(known.Contains(setId), $"Chapter2Builder references set '{setId}' which is missing from Chapter2Lines.SetIds.");
        }

        [Test]
        public void UnknownSetId_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, Chapter2Lines.Get("not_a_real_set").Length);
        }
    }
}
