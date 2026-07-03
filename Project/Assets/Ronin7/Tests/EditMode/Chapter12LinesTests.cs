using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Chapter12LinesTests
    {
        // Every speaker used in Chapter12Lines must appear here AND have an exact-match entry in
        // Audio/Tools/generate_voice.py SPEAKER_VOICES, or TTS silently falls back to a default voice.
        private static readonly HashSet<string> KnownCast = new HashSet<string>
        {
            "Cassie-04",
            "Sable",
            "Mera Voss",
            "Kessler",
            "Morrigan",
            "Coral Vex",
            "Vess",
            "Echo",
            "Ronin-7",
            "Gryph",
            "Vale",
            "Ronin-7 Edition",
        };

        // The exact set IDs Chapter12Builder wires into MissionDirector steps or dialogue players, kept
        // in sync by hand — if this list and Chapter12Lines.SetIds diverge, either the builder is missing
        // a beat or a set is orphaned.
        private static readonly string[] BuilderReferencedSetIds =
        {
            "ch12_beat0_briefing",
            "ch12_beat1_descent",
            "ch12_beat1_repetition",
            "ch12_beat2_greeting",
            "ch12_beat2_offer",
            "ch12_beat2_threat",
            "ch12_beat3_intro",
            "ch12_beat3_reveal",
            "ch12_beat3_aftermath",
            "ch12_beat4_offer",
            "ch12_beat4_refusal",
            "ch12_beat4_cut",
            "ch12_beat4_homecoming",
            "ch12_beat4_parting",
            "ch12_beat4_hook",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Chapter12Lines.SetIds)
            {
                var lines = Chapter12Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Chapter12Lines.SetIds)
            {
                var lines = Chapter12Lines.Get(setId);
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
            foreach (string setId in Chapter12Lines.SetIds)
            {
                var lines = Chapter12Lines.Get(setId);
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
            foreach (string setId in Chapter12Lines.SetIds)
            {
                var lines = Chapter12Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Chapter12Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Chapter12Lines.SetIds)
            {
                var lines = Chapter12Lines.Get(setId);
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
            var known = new HashSet<string>(Chapter12Lines.SetIds);
            foreach (string setId in BuilderReferencedSetIds)
                Assert.IsTrue(known.Contains(setId), $"Chapter12Builder references set '{setId}' which is missing from Chapter12Lines.SetIds.");
        }

        [Test]
        public void UnknownSetId_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, Chapter12Lines.Get("not_a_real_set").Length);
        }

        [Test]
        public void NoLine_MentionsSoren()
        {
            // Ch12 delivers Ladder C rung 2 (the template reveal) but must NEVER name "Soren" — that
            // reveal is reserved for Ch16 (see the dialogue script's "NEVER name Soren here").
            foreach (string setId in Chapter12Lines.SetIds)
            {
                var lines = Chapter12Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    Assert.IsFalse(lines[i].text.ToLowerInvariant().Contains("soren"),
                        $"Set '{setId}' line {i} names Soren — that reveal is reserved for Ch16: \"{lines[i].text}\"");
                }
            }
        }
    }
}
