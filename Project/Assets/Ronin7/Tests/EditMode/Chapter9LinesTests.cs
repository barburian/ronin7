using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Chapter9LinesTests
    {
        // Every speaker used in Chapter9Lines must appear here AND have an exact-match entry in
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
            "Gryph",
            "Rook",
            "Sable",
            "Vane",
        };

        // The exact set IDs Chapter9Builder wires into MissionDirector steps or dialogue players, kept
        // in sync by hand — if this list and Chapter9Lines.SetIds diverge, either the builder is missing
        // a beat or a set is orphaned. ch9_beat2_raid_bark IS wired: it plays as wave 0 of the Coil-raid
        // EnemyWaveSpawner, mirroring Chapter6/7/8's per-encounter bark convention.
        private static readonly string[] BuilderReferencedSetIds =
        {
            "ch9_beat0_briefing",
            "ch9_beat1_descent",
            "ch9_beat1_old_machinery",
            "ch9_beat2_challenge",
            "ch9_beat2_raid_bark",
            "ch9_beat2_bargain",
            "ch9_beat3_dive_intro",
            "ch9_beat3_confrontation",
            "ch9_beat3_aftermath",
            "ch9_beat3_overdrive",
            "ch9_beat3_recruit",
            "ch9_beat4_reveal",
            "ch9_beat5_holdkept",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Chapter9Lines.SetIds)
            {
                var lines = Chapter9Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Chapter9Lines.SetIds)
            {
                var lines = Chapter9Lines.Get(setId);
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
            foreach (string setId in Chapter9Lines.SetIds)
            {
                var lines = Chapter9Lines.Get(setId);
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
            foreach (string setId in Chapter9Lines.SetIds)
            {
                var lines = Chapter9Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Chapter9Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Chapter9Lines.SetIds)
            {
                var lines = Chapter9Lines.Get(setId);
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
            var known = new HashSet<string>(Chapter9Lines.SetIds);
            foreach (string setId in BuilderReferencedSetIds)
                Assert.IsTrue(known.Contains(setId), $"Chapter9Builder references set '{setId}' which is missing from Chapter9Lines.SetIds.");
        }

        [Test]
        public void UnknownSetId_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, Chapter9Lines.Get("not_a_real_set").Length);
        }
    }
}
