using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Chapter7LinesTests
    {
        // Every speaker used in Chapter7Lines must appear here AND have an exact-match entry in
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
            "The Previous Owner",
        };

        // The exact set IDs Chapter7Builder wires into MissionDirector steps or dialogue players, kept
        // in sync by hand — if this list and Chapter7Lines.SetIds diverge, either the builder is
        // missing a beat or a set is orphaned. ch7_beat1_gauntlet_bark IS wired: it plays as wave 0 of
        // the outer-stacks EnemyWaveSpawner, mirroring Chapter6's per-tower bark convention.
        private static readonly string[] BuilderReferencedSetIds =
        {
            "ch7_beat0_briefing",
            "ch7_beat1_breach",
            "ch7_beat1_gauntlet_bark",
            "ch7_beat1_core_ahead",
            "ch7_beat2_archivist",
            "ch7_beat3_forebear",
            "ch7_beat4_kept_shadows",
            "ch7_beat4_quiet_it",
            "ch7_beat4_mindspace_intro",
            "ch7_beat4_gift",
            "ch7_beat5_sabotage",
            "ch7_beat5_hookout",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Chapter7Lines.SetIds)
            {
                var lines = Chapter7Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Chapter7Lines.SetIds)
            {
                var lines = Chapter7Lines.Get(setId);
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
            foreach (string setId in Chapter7Lines.SetIds)
            {
                var lines = Chapter7Lines.Get(setId);
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
            foreach (string setId in Chapter7Lines.SetIds)
            {
                var lines = Chapter7Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Chapter7Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Chapter7Lines.SetIds)
            {
                var lines = Chapter7Lines.Get(setId);
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
            var known = new HashSet<string>(Chapter7Lines.SetIds);
            foreach (string setId in BuilderReferencedSetIds)
                Assert.IsTrue(known.Contains(setId), $"Chapter7Builder references set '{setId}' which is missing from Chapter7Lines.SetIds.");
        }

        [Test]
        public void UnknownSetId_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, Chapter7Lines.Get("not_a_real_set").Length);
        }
    }
}
