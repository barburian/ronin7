using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Chapter11LinesTests
    {
        // Every speaker used in Chapter11Lines must appear here AND have an exact-match entry in
        // Audio/Tools/generate_voice.py SPEAKER_VOICES, or TTS silently falls back to a default voice.
        private static readonly HashSet<string> KnownCast = new HashSet<string>
        {
            "Cassie-04",
            "Sable",
            "Coral Vex",
            "Vess",
            "Echo",
            "Ronin-7",
            "Gryph",
            "Kira",
            "Younger Self",
            "Aldric",
        };

        // The exact set IDs Chapter11Builder wires into MissionDirector steps or dialogue players, kept
        // in sync by hand — if this list and Chapter11Lines.SetIds diverge, either the builder is missing
        // a beat or a set is orphaned.
        private static readonly string[] BuilderReferencedSetIds =
        {
            "ch11_beat0_briefing",
            "ch11_beat1_descent",
            "ch11_beat1_arkship",
            "ch11_beat2_ghosts",
            "ch11_beat2_youngerself",
            "ch11_beat2_keeper",
            "ch11_beat2_kill",
            "ch11_beat2_unbroken",
            "ch11_beat3_intro",
            "ch11_beat3_reveal",
            "ch11_beat3_echokin",
            "ch11_beat4_mercy",
            "ch11_beat4_homecoming",
            "ch11_beat4_targetlist",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Chapter11Lines.SetIds)
            {
                var lines = Chapter11Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Chapter11Lines.SetIds)
            {
                var lines = Chapter11Lines.Get(setId);
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
            foreach (string setId in Chapter11Lines.SetIds)
            {
                var lines = Chapter11Lines.Get(setId);
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
            foreach (string setId in Chapter11Lines.SetIds)
            {
                var lines = Chapter11Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Chapter11Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Chapter11Lines.SetIds)
            {
                var lines = Chapter11Lines.Get(setId);
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
            var known = new HashSet<string>(Chapter11Lines.SetIds);
            foreach (string setId in BuilderReferencedSetIds)
                Assert.IsTrue(known.Contains(setId), $"Chapter11Builder references set '{setId}' which is missing from Chapter11Lines.SetIds.");
        }

        [Test]
        public void UnknownSetId_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, Chapter11Lines.Get("not_a_real_set").Length);
        }

        [Test]
        public void NoLine_MentionsSoren()
        {
            // Ch11 plants the buried-self/Younger-Self thread but must NEVER name "Soren" — that reveal
            // is reserved for Ch16 (see the dialogue script's "CRITICAL: never name Soren here").
            foreach (string setId in Chapter11Lines.SetIds)
            {
                var lines = Chapter11Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    Assert.IsFalse(lines[i].text.ToLowerInvariant().Contains("soren"),
                        $"Set '{setId}' line {i} names Soren — that reveal is reserved for Ch16: \"{lines[i].text}\"");
                }
            }
        }
    }
}
