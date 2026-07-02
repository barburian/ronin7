using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Chapter4LinesTests
    {
        // Every speaker used in Chapter4Lines must appear here AND have an exact-match entry in
        // Audio/Tools/generate_voice.py SPEAKER_VOICES, or TTS silently falls back to a default voice.
        private static readonly HashSet<string> KnownCast = new HashSet<string>
        {
            "Kessler",
            "Ronin-7",
            "Iris",
            "Resh",
            "Mira",
            "Echo",
            "Khall",
            "Tessa Rin",
            "Kerrax",
            "Mera Voss",
        };

        // The exact set IDs Chapter4Builder wires into its mission steps, kept in sync by hand — if this
        // list and Chapter4Lines.SetIds diverge, either the builder is missing a beat or a set is orphaned.
        private static readonly string[] BuilderReferencedSetIds =
        {
            "ch4_beat0_briefing",
            "ch4_beat1_throat",
            "ch4_beat2_tessa_meet",
            "ch4_beat2_sabotage",
            "ch4_beat3_khall",
            "ch4_beat3_aftermath",
            "ch4_beat4_descent_intro",
            "ch4_beat4_descent_calls",
            "ch4_beat4_descent_end",
            "ch4_beat5_kerrax_confront",
            "ch4_beat5_mercy",
            "ch4_beat5_mera_recruit",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Chapter4Lines.SetIds)
            {
                var lines = Chapter4Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Chapter4Lines.SetIds)
            {
                var lines = Chapter4Lines.Get(setId);
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
            foreach (string setId in Chapter4Lines.SetIds)
            {
                var lines = Chapter4Lines.Get(setId);
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
            foreach (string setId in Chapter4Lines.SetIds)
            {
                var lines = Chapter4Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Chapter4Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Chapter4Lines.SetIds)
            {
                var lines = Chapter4Lines.Get(setId);
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
            var known = new HashSet<string>(Chapter4Lines.SetIds);
            foreach (string setId in BuilderReferencedSetIds)
                Assert.IsTrue(known.Contains(setId), $"Chapter4Builder references set '{setId}' which is missing from Chapter4Lines.SetIds.");
        }

        [Test]
        public void UnknownSetId_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, Chapter4Lines.Get("not_a_real_set").Length);
        }
    }
}
