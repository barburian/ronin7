using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    public class Chapter13LinesTests
    {
        // Every speaker used in Chapter13Lines must appear here AND have an exact-match entry in
        // Audio/Tools/generate_voice.py SPEAKER_VOICES, or TTS silently falls back to a default voice.
        private static readonly HashSet<string> KnownCast = new HashSet<string>
        {
            "Cassie-04",
            "Sable",
            "Kessler",
            "Morrigan",
            "Coral Vex",
            "Mera Voss",
            "Echo",
            "Ronin-7",
            "Vess",
            "Enforcer",
            "Dr. Heris",
            "Sallow",
        };

        // The exact set IDs Chapter13Builder wires into MissionDirector steps or dialogue players, kept
        // in sync by hand — if this list and Chapter13Lines.SetIds diverge, either the builder is missing
        // a beat or a set is orphaned.
        private static readonly string[] BuilderReferencedSetIds =
        {
            "ch13_beat0_briefing",
            "ch13_beat1_breach",
            "ch13_beat1_recognition",
            "ch13_beat1b_enforcer_intro",
            "ch13_beat1b_enforcer_defeated",
            "ch13_beat2_opening",
            "ch13_beat2_the_flaw",
            "ch13_beat2_the_vision",
            "ch13_beat2_the_engine",
            "ch13_beat2_the_ground",
            "ch13_beat3_defection",
            "ch13_beat4_introduce_sallow",
            "ch13_beat4_mechanic",
            "ch13_beat4_complete",
        };

        [Test]
        public void EverySetId_ReturnsNonEmptyArray()
        {
            foreach (string setId in Chapter13Lines.SetIds)
            {
                var lines = Chapter13Lines.Get(setId);
                Assert.IsNotNull(lines, $"Set '{setId}' returned null.");
                Assert.Greater(lines.Length, 0, $"Set '{setId}' returned an empty array.");
            }
        }

        [Test]
        public void EveryLine_HasSpeakerTextAndPositiveSeconds()
        {
            foreach (string setId in Chapter13Lines.SetIds)
            {
                var lines = Chapter13Lines.Get(setId);
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
            foreach (string setId in Chapter13Lines.SetIds)
            {
                var lines = Chapter13Lines.Get(setId);
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
            foreach (string setId in Chapter13Lines.SetIds)
            {
                var lines = Chapter13Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    string clip = Chapter13Lines.ClipName(setId, i, lines[i].speaker);
                    Assert.IsTrue(seen.Add(clip), $"Duplicate clip name '{clip}'.");
                }
            }
        }

        [Test]
        public void EverySpeaker_IsInKnownCast()
        {
            foreach (string setId in Chapter13Lines.SetIds)
            {
                var lines = Chapter13Lines.Get(setId);
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
            var known = new HashSet<string>(Chapter13Lines.SetIds);
            foreach (string setId in BuilderReferencedSetIds)
                Assert.IsTrue(known.Contains(setId), $"Chapter13Builder references set '{setId}' which is missing from Chapter13Lines.SetIds.");
        }

        [Test]
        public void UnknownSetId_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, Chapter13Lines.Get("not_a_real_set").Length);
        }

        [Test]
        public void NoLine_MentionsSoren()
        {
            // Ch13 delivers Ladder A rung 5 (Heris built the killswitch AND planted the saving flaw AND
            // is the Concord Engine's conscripted architect) but must NEVER name "Soren" — the birth-name
            // reveal is reserved for Ch16 (see the dialogue script's "NEVER name that identity here").
            foreach (string setId in Chapter13Lines.SetIds)
            {
                var lines = Chapter13Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    Assert.IsFalse(lines[i].text.ToLowerInvariant().Contains("soren"),
                        $"Set '{setId}' line {i} names Soren — that reveal is reserved for Ch16: \"{lines[i].text}\"");
                }
            }
        }

        [Test]
        public void HerisLine_ContainsIHopeYouWillSaveUs()
        {
            // AUDIT FIX #4 regression guard: the source script's Beat 2 Heris line described the iconic
            // "I hope you will save us" whisper (the entire Ch08 vision payoff) in its voice: note but
            // omitted it from the actual Line: text. This asserts a Heris line in Ch13 carries the exact
            // whisper so it can never silently vanish again.
            bool found = false;
            foreach (string setId in Chapter13Lines.SetIds)
            {
                var lines = Chapter13Lines.Get(setId);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].speaker == "Dr. Heris" && lines[i].text.Contains("I hope you will save us"))
                    {
                        found = true;
                        break;
                    }
                }
            }
            Assert.IsTrue(found, "No Heris line contains the restored 'I hope you will save us' whisper (AUDIT FIX #4).");
        }
    }
}
