using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Chapter3LinesTests
    {
        // Every speaker used in Chapter3Lines must appear here AND have an exact-match entry in
        // Audio/Tools/generate_voice.py SPEAKER_VOICES, or TTS silently falls back to a default voice.
        // "Shadow" and "Echo" are the same AI pre/post the naming beat — both labels need voices.
        private static readonly HashSet<string> KnownCast = new HashSet<string>
        {
            "Kessler",
            "Ronin-7",
            "Iris",
            "Resh",
            "Shadow",
            "Echo",
            "Khall",
        };

        // The exact set IDs Chapter3Builder wires into its mission steps, kept in sync by hand — if this
        // list and Chapter3Lines.SetIds diverge, either the builder is missing a beat or a set is orphaned.
        private static readonly string[] BuilderReferencedSetIds =
        {
            "ch3_beat0_briefing",
            "ch3_beat1_bonding",
            "ch3_beat1_shadow_explains",
            "ch3_beat2_threshold",
            "ch3_beat2_aftermath",
            "ch3_beat2_execution",
            "ch3_beat2_burndown",
            "ch3_beat3_naming",
            "ch3_beat3_debrief",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Chapter3Lines.SetIds)
            {
                var lines = Chapter3Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Chapter3Lines.SetIds)
            {
                var lines = Chapter3Lines.Get(setId);
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
            foreach (string setId in Chapter3Lines.SetIds)
            {
                var lines = Chapter3Lines.Get(setId);
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
            foreach (string setId in Chapter3Lines.SetIds)
            {
                var lines = Chapter3Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Chapter3Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Chapter3Lines.SetIds)
            {
                var lines = Chapter3Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    Assert.IsTrue(KnownCast.Contains(lines[i].speaker),
                        $"Set '{setId}' line {i} uses unknown speaker '{lines[i].speaker}' — add it to SPEAKER_VOICES in generate_voice.py and to KnownCast here.");
                }
            }
        }

        [Test]
        public void SpeakerLabel_IsShadowBeforeTheNamingAndEchoAfter()
        {
            // Canon (00b §2 / the audit): the AI is labeled "Shadow" through the bonding + playback and
            // "Echo" only from the naming beat on. "Echo" before ch3_beat3_naming is a canon break.
            var preNamingSets = new[]
            {
                "ch3_beat0_briefing", "ch3_beat1_bonding", "ch3_beat1_shadow_explains",
                "ch3_beat2_threshold", "ch3_beat2_aftermath", "ch3_beat2_execution", "ch3_beat2_burndown",
            };
            foreach (string setId in preNamingSets)
            {
                var lines = Chapter3Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    Assert.AreNotEqual("Echo", lines[i].speaker,
                        $"Set '{setId}' line {i} uses speaker 'Echo' before the naming beat.");
                }
            }
        }

        [Test]
        public void EveryBuilderReferencedSetId_ExistsInSetIds()
        {
            var known = new HashSet<string>(Chapter3Lines.SetIds);
            foreach (string setId in BuilderReferencedSetIds)
                Assert.IsTrue(known.Contains(setId), $"Chapter3Builder references set '{setId}' which is missing from Chapter3Lines.SetIds.");
        }

        [Test]
        public void UnknownSetId_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, Chapter3Lines.Get("not_a_real_set").Length);
        }
    }
}
