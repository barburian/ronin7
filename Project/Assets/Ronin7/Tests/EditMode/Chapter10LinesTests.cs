using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Chapter10LinesTests
    {
        // Every speaker used in Chapter10Lines must appear here AND have an exact-match entry in
        // Audio/Tools/generate_voice.py SPEAKER_VOICES, or TTS silently falls back to a default voice.
        private static readonly HashSet<string> KnownCast = new HashSet<string>
        {
            "Morrigan",
            "Sable",
            "Echo",
            "Ronin-7",
            "Gryph",
            "Coral Vex",
            "Cassie-04",
            "Sever",
            "Vess",
        };

        // The exact set IDs Chapter10Builder wires into MissionDirector steps or dialogue players, kept
        // in sync by hand — if this list and Chapter10Lines.SetIds diverge, either the builder is missing
        // a beat or a set is orphaned.
        private static readonly string[] BuilderReferencedSetIds =
        {
            "ch10_beat0_briefing",
            "ch10_beat1_descent",
            "ch10_beat1_strongroom",
            "ch10_beat2_keeper",
            "ch10_beat2_kill",
            "ch10_beat2_phasestep",
            "ch10_beat2_recruit",
            "ch10_beat3_ninja_history",
            "ch10_beat3_reading",
            "ch10_beat4_ambush",
            "ch10_beat4_choice",
            "ch10_beat5_targetlist",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Chapter10Lines.SetIds)
            {
                var lines = Chapter10Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Chapter10Lines.SetIds)
            {
                var lines = Chapter10Lines.Get(setId);
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
            foreach (string setId in Chapter10Lines.SetIds)
            {
                var lines = Chapter10Lines.Get(setId);
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
            foreach (string setId in Chapter10Lines.SetIds)
            {
                var lines = Chapter10Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Chapter10Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Chapter10Lines.SetIds)
            {
                var lines = Chapter10Lines.Get(setId);
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
            var known = new HashSet<string>(Chapter10Lines.SetIds);
            foreach (string setId in BuilderReferencedSetIds)
                Assert.IsTrue(known.Contains(setId), $"Chapter10Builder references set '{setId}' which is missing from Chapter10Lines.SetIds.");
        }

        [Test]
        public void UnknownSetId_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, Chapter10Lines.Get("not_a_real_set").Length);
        }
    }
}
