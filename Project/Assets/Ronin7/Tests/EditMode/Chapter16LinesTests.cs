using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Chapter16LinesTests
    {
        // Every speaker used in Chapter16Lines must appear here AND have an exact-match entry in
        // Audio/Tools/generate_voice.py SPEAKER_VOICES, or TTS silently falls back to a default voice.
        // (Verified against generate_voice.py at authoring time: every one of these already has an entry
        // from earlier EP29-EP33/Ch1-13 blocks — no new speakers were introduced by this chapter.)
        private static readonly HashSet<string> KnownCast = new HashSet<string>
        {
            "Dr. Heris",
            "Iris",
            "Coral Vex",
            "Cassie-04",
            "Sable",
            "Mera Voss",
            "Vess",
            "Gryph",
            "Kessler",
            "Ronin-7",
            "Echo",
            "Morrigan",
            "Samurai-4",
            "Soren",
            "Khall",
            "Maelgorn",
            "Mira",
            "Resh",
        };

        // The exact set IDs Chapter16Builder wires into MissionDirector steps or dialogue players, kept
        // in sync by hand — if this list and Chapter16Lines.SetIds diverge, either the builder is missing
        // a beat or a set is orphaned.
        private static readonly string[] BuilderReferencedSetIds =
        {
            "ch16_beat0_briefing",
            "ch16_beat1_descent_upper",
            "ch16_beat1_descent_lower",
            "ch16_beat2_duel",
            "ch16_beat3_leash_break",
            "ch16_beat4_soren",
            "ch16_beat5_throne_core",
            "ch16_beat6_loop_taunt",
            "ch16_beat6_loop_iter1",
            "ch16_beat6_loop_iter2",
            "ch16_beat6_loop_break",
            "ch16_beat6_loop_concede",
            "ch16_beat7_forged_order",
            "ch16_beat8_true_enemy",
            "ch16_beat9_seam_choice",
            "ch16_beat9_phase_taunt1",
            "ch16_beat9_phase_taunt2",
            "ch16_beat9_liberation",
            "ch16_beat9_throne_test",
            "ch16_beat9_closing_crew",
            "ch16_beat9_homecoming",
            "ch16_beat9_echo_final",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Chapter16Lines.SetIds)
            {
                var lines = Chapter16Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Chapter16Lines.SetIds)
            {
                var lines = Chapter16Lines.Get(setId);
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
            foreach (string setId in Chapter16Lines.SetIds)
            {
                var lines = Chapter16Lines.Get(setId);
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
            foreach (string setId in Chapter16Lines.SetIds)
            {
                var lines = Chapter16Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Chapter16Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Chapter16Lines.SetIds)
            {
                var lines = Chapter16Lines.Get(setId);
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
            var known = new HashSet<string>(Chapter16Lines.SetIds);
            foreach (string setId in BuilderReferencedSetIds)
                Assert.IsTrue(known.Contains(setId), $"Chapter16Builder references set '{setId}' which is missing from Chapter16Lines.SetIds.");
        }

        [Test]
        public void EverySetId_IsReferencedByBuilder()
        {
            // The inverse of the guard above: catches a set authored in Chapter16Lines but never wired
            // into a mission step, which would silently orphan dialogue content in the largest chapter.
            var referenced = new HashSet<string>(BuilderReferencedSetIds);
            foreach (string setId in Chapter16Lines.SetIds)
                Assert.IsTrue(referenced.Contains(setId), $"Chapter16Lines set '{setId}' is not referenced by Chapter16Builder's BuilderReferencedSetIds.");
        }

        [Test]
        public void UnknownSetId_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, Chapter16Lines.Get("not_a_real_set").Length);
        }

        [Test]
        public void SomeLine_ContainsSorenReveal()
        {
            // Ch16 is the ONE chapter in the saga where the birth-name reveal (Ladder C, rung 3) is
            // delivered — unlike every earlier chapter (see e.g. Chapter13LinesTests.NoLine_MentionsSoren),
            // this chapter must NOT suppress it. Guards that the reveal line actually exists.
            bool found = false;
            foreach (string setId in Chapter16Lines.SetIds)
            {
                var lines = Chapter16Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].text.Contains("My name is Soren"))
                    {
                        found = true;
                        break;
                    }
                }
            }
            Assert.IsTrue(found, "No line contains the SOREN identity-reveal (\"My name is Soren\") — Ladder C rung 3 must land in this chapter.");
        }

        [Test]
        public void NoLine_CallsSamurai4ANewerMake()
        {
            // AUDIT FIX #6 regression guard (00_AUDIT_SUMMARY.md item 6 / Ch16_audit.md "medium"): the
            // source script called Samurai-4 a "newer make" than Ronin-7, which contradicts canon (Ronin
            // is the current/newest make). Asserts the phrase never reappears.
            foreach (string setId in Chapter16Lines.SetIds)
            {
                var lines = Chapter16Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    Assert.IsFalse(lines[i].text.ToLowerInvariant().Contains("newer make"),
                        $"Set '{setId}' line {i} calls Samurai-4 a \"newer make\" — AUDIT FIX #6 regression: \"{lines[i].text}\"");
                }
            }
        }

        [Test]
        public void Samurai4Duel_NeverAddressesHimAsRonin7AfterIntro()
        {
            // AUDIT FIX #6 (minor half): Samurai-4's formal file-designation citation on intro
            // ("Operative designation Ronin-7") is the ONE allowed use of the serial in her spoken
            // address; every subsequent line of hers must use "Cipher" (convention reserves "Ronin-7"
            // for narration). Guards the second occurrence that the audit specifically flagged is fixed.
            var lines = Chapter16Lines.Get("ch16_beat2_duel");
            int samurai4RoninAddressCount = 0;
            foreach (var line in lines)
            {
                if (line.speaker != "Samurai-4") continue;
                if (line.text.Contains("Ronin-7")) samurai4RoninAddressCount++;
            }
            Assert.AreEqual(1, samurai4RoninAddressCount,
                "Samurai-4 should address Ronin-7 by serial exactly once (the intro file-designation citation) — AUDIT FIX #6 regression.");
        }
    }
}
