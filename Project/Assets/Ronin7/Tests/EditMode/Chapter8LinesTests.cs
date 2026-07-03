using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Chapter8LinesTests
    {
        // Every speaker used in Chapter8Lines must appear here AND have an exact-match entry in
        // Audio/Tools/generate_voice.py SPEAKER_VOICES, or TTS silently falls back to a default voice.
        private static readonly HashSet<string> KnownCast = new HashSet<string>
        {
            "Kessler",
            "Ronin-7",
            "Iris",
            "Resh",
            "Mira",
            "Echo",
            "Mera Voss",
            "Morrigan",
            "Coral Vex",
            "Khall",
            "The Mourners",
            "The Woman",
        };

        // The exact set IDs Chapter8Builder wires into MissionDirector steps or dialogue players, kept
        // in sync by hand — if this list and Chapter8Lines.SetIds diverge, either the builder is
        // missing a beat or a set is orphaned. ch8_beat2_wrong_bark IS wired: it plays as wave 0 of the
        // wrong-answer guardian EnemyWaveSpawner, mirroring Chapter6/7's per-encounter bark convention.
        private static readonly string[] BuilderReferencedSetIds =
        {
            "ch8_beat0_briefing",
            "ch8_beat1_gate",
            "ch8_beat1_alone",
            "ch8_beat2_riddle_pose",
            "ch8_beat2_wrong_bark",
            "ch8_beat2_riddle_answer",
            "ch8_beat3_warden_intro",
            "ch8_beat3_warden_defeat",
            "ch8_beat4_naming",
            "ch8_beat4_vision_a",
            "ch8_beat4_vision_b",
            "ch8_beat4_aftermath",
            "ch8_beat5_leaving",
            "ch8_beat5_reunion",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Chapter8Lines.SetIds)
            {
                var lines = Chapter8Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Chapter8Lines.SetIds)
            {
                var lines = Chapter8Lines.Get(setId);
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
            foreach (string setId in Chapter8Lines.SetIds)
            {
                var lines = Chapter8Lines.Get(setId);
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
            foreach (string setId in Chapter8Lines.SetIds)
            {
                var lines = Chapter8Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Chapter8Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Chapter8Lines.SetIds)
            {
                var lines = Chapter8Lines.Get(setId);
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
            var known = new HashSet<string>(Chapter8Lines.SetIds);
            foreach (string setId in BuilderReferencedSetIds)
                Assert.IsTrue(known.Contains(setId), $"Chapter8Builder references set '{setId}' which is missing from Chapter8Lines.SetIds.");
        }

        [Test]
        public void UnknownSetId_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, Chapter8Lines.Get("not_a_real_set").Length);
        }
    }
}
