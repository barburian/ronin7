using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Chapter6LinesTests
    {
        // Every speaker used in Chapter6Lines must appear here AND have an exact-match entry in
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
            "Matron Hespa",
            "Drillmaster Caradoc",
            "Master Kaelen",
        };

        // The exact set IDs Chapter6Builder wires into MissionDirector steps or dialogue players, kept in
        // sync by hand — if this list and Chapter6Lines.SetIds diverge, either the builder is missing a
        // beat or a set is orphaned. The 3 per-tower master-intro-bark sets (ch6_beat3_*) ARE wired: each
        // tower's own proximity-armed EnemyWaveSpawner (triggerRadius 9 on that tower's entrance corridor)
        // plays its bark as wave 0, so a bark can only fire once, for the tower the player actually
        // approached — no overlapping-subtitle risk.
        private static readonly string[] BuilderReferencedSetIds =
        {
            "ch6_beat0_briefing",
            "ch6_beat1_climb",
            "ch6_beat1_window",
            "ch6_beat2_morrigan_meet",
            "ch6_beat2_killlist",
            "ch6_beat3_hespa_intro",
            "ch6_beat3_caradoc_intro",
            "ch6_beat3_kaelen_intro",
            "ch6_beat4_confession",
            "ch6_beat5_evacuation",
            "ch6_beat5_descent",
            "ch6_beat5_morrigan_joins",
            "ch6_beat5_outro",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Chapter6Lines.SetIds)
            {
                var lines = Chapter6Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Chapter6Lines.SetIds)
            {
                var lines = Chapter6Lines.Get(setId);
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
            foreach (string setId in Chapter6Lines.SetIds)
            {
                var lines = Chapter6Lines.Get(setId);
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
            foreach (string setId in Chapter6Lines.SetIds)
            {
                var lines = Chapter6Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Chapter6Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Chapter6Lines.SetIds)
            {
                var lines = Chapter6Lines.Get(setId);
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
            var known = new HashSet<string>(Chapter6Lines.SetIds);
            foreach (string setId in BuilderReferencedSetIds)
                Assert.IsTrue(known.Contains(setId), $"Chapter6Builder references set '{setId}' which is missing from Chapter6Lines.SetIds.");
        }

        [Test]
        public void UnknownSetId_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, Chapter6Lines.Get("not_a_real_set").Length);
        }
    }
}
