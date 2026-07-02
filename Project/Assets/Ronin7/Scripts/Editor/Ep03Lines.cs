using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 3 dialogue data. Transposed from ep03-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep03_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// </summary>
    public static class Ep03Lines
    {
        private struct DialogueLine
        {
            public string speaker;
            public string text;
            public float seconds;
        }

        /// <summary>All set IDs in canonical order.</summary>
        public static readonly string[] SetIds = new[]
        {
            "hauler_hum",
            "hauler_found",
            "hauler_boarding",
            "hauler_fight_barks",
            "hauler_clear",
            "hauler_pod",
            "hauler_fury",
            "hauler_corvette",
            "engine_return",
            "engine_scar",
            "engine_clock",
            "engine_hunter_challenge",
            "engine_hunter_after",
            "engine_brief",
            "engine_lotus_decision",
            "lotus_guidance",
            "lotus_fight_barks",
            "lotus_podbay",
            "lotus_senna",
            "lotus_khall",
            "lotus_reunion",
            "sanctuary_doctor",
            "sanctuary_assault_barks",
            "sanctuary_quiet",
            "sanctuary_file",
            "sanctuary_observatory",
            "space_ep03_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "hauler_hum" => GetHaulerHumLines(),
                "hauler_found" => GetHaulerFoundLines(),
                "hauler_boarding" => GetHaulerBoardingLines(),
                "hauler_fight_barks" => GetHaulerFightBarksLines(),
                "hauler_clear" => GetHaulerClearLines(),
                "hauler_pod" => GetHaulerPodLines(),
                "hauler_fury" => GetHaulerFuryLines(),
                "hauler_corvette" => GetHaulerCorvetteLines(),
                "engine_return" => GetEngineReturnLines(),
                "engine_scar" => GetEngineScarLines(),
                "engine_clock" => GetEngineClockLines(),
                "engine_hunter_challenge" => GetEngineHunterChallengeLines(),
                "engine_hunter_after" => GetEngineHunterAfterLines(),
                "engine_brief" => GetEngineBriefLines(),
                "engine_lotus_decision" => GetEngineLotusDecisionLines(),
                "lotus_guidance" => GetLotusGuidanceLines(),
                "lotus_fight_barks" => GetLotusFightBarksLines(),
                "lotus_podbay" => GetLotusPodbayLines(),
                "lotus_senna" => GetLotusSennaLines(),
                "lotus_khall" => GetLotusKhallLines(),
                "lotus_reunion" => GetLotusReunionLines(),
                "sanctuary_doctor" => GetSanctuaryDoctorLines(),
                "sanctuary_assault_barks" => GetSanctuaryAssaultBarksLines(),
                "sanctuary_quiet" => GetSanctuaryQuietLines(),
                "sanctuary_file" => GetSanctuaryFileLines(),
                "sanctuary_observatory" => GetSanctuaryObservatoryLines(),
                "space_ep03_post" => GetSpaceEp03PostLines(),
                _ => new DialogueLine[0],
            };

            // Convert internal DialogueLine to Ronin7.World.Story.DialogueLine
            var result = new World.Story.DialogueLine[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                result[i] = new World.Story.DialogueLine
                {
                    speaker = lines[i].speaker,
                    text = lines[i].text,
                    seconds = lines[i].seconds,
                    clip = null
                };
            }
            return result;
        }

        /// <summary>Sanitize a speaker name for clip naming: lowercase, strip non-alphanumeric.</summary>
        public static string Sanitize(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return "unknown";
            var sb = new System.Text.StringBuilder();
            foreach (char c in speaker)
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        /// <summary>Generate clip name for a line: ep03_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep03_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- HAULER DIALOGUE (Beats 2-9) ----

        private static DialogueLine[] GetHaulerHumLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "It's not stopping.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "What's not?", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "The sword.It was quiet at Velorum. Since the boarding party it won't settle, like something keeps reaching for me.", seconds = 4.5f },
                new DialogueLine { speaker = "Kessler", text = "That came from the hold. Stay here.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetHaulerFoundLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Iris", text = "Oh gods. Oh gods, Kessler, she's...", seconds = 2f },
                new DialogueLine { speaker = "Mira", text = "Don't touch me.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "No one's hurting you. We found you. You're safe now.", seconds = 3f },
                new DialogueLine { speaker = "Mira", text = "They said people would come. They said that's how you know it's the extraction.", seconds = 3.5f },
                new DialogueLine { speaker = "Iris", text = "We're not extraction. We're salvage. And we're getting you out.", seconds = 3f },
            };
        }

        private static DialogueLine[] GetHaulerBoardingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Enforcer 1", text = "Boarding initiated. All station personnel, standby for manifest verification.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Iris. The lock. Now.", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetHaulerFightBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Enforcer 2", text = "Man down! Contact in Bay Seven! Non-compliance!", seconds = 2f },
                new DialogueLine { speaker = "Dominion Enforcer 3", text = "Requesting backup! Requesting immediate backup!", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetHaulerClearLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Clear. You're clear. Get back to the ship.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetHaulerPodLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mira", text = "You have the same eyes. Did they take yours too?", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "I don't remember.", seconds = 2f },
                new DialogueLine { speaker = "Mira", text = "My name is Mira. They were going to take me for augmentation extraction. That's what the Overseer said. That's what I ran from.", seconds = 4.5f },
                new DialogueLine { speaker = "Ronin-7", text = "I'm taking you somewhere safe.", seconds = 2.5f },
                new DialogueLine { speaker = "Mira", text = "What's your name?", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "I don't have one.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetHaulerFuryLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "You can't keep her. The moment we move off-station with a child in the hold, we become everything the Dominion hunts.", seconds = 4f },
                new DialogueLine { speaker = "Iris", text = "I'm not leaving her.", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we don't move off-station.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Undock in ten seconds or the soldiers board the bridge. We have exactly one move.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Gods. They already know.", seconds = 1.5f },
                new DialogueLine { speaker = "Iris", text = "Know what?", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "A child with partial Dominion augmentation is a library to them. Memories to strip. Neural patterns to replicate. She's not cargo to them, she's a research specimen.", seconds = 4.5f },
                new DialogueLine { speaker = "Kessler", text = "And they know she's here.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetHaulerCorvetteLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "They're launching from the fringe. Corvette class. They want her alive.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we don't stop.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "They've got lock! Evasive!", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "I've got the cannon. Banking left.", seconds = 1.5f },
                new DialogueLine { speaker = "Hollow Kings Pilot", text = "Target is evading. Sustained pursuit authorized.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Corvette is repositioning. You've got maybe sixty seconds.", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Twenty-eight seconds remaining.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Nice work. Come home.", seconds = 1.5f },
            };
        }

        // ---- ENGINE ROOM DIALOGUE (Beats 10-15) ----

        private static DialogueLine[] GetEngineReturnLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mira", text = "They told me I'd never see outside. That the only view that mattered was the target reticle.", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-7", text = "I know that language.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "We're going to the Fringe colonies. You'll be safe there.", seconds = 3f },
            };
        }

        private static DialogueLine[] GetEngineScarLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Sit. Let me look at it.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "I've seen this mark before. On other operatives. Never on one still walking around to ask about it.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "What is it?", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetEngineClockLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "It's not a kill-switch waiting to be thrown. It's one that was already thrown.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "On Kethel-7, whatever you did there, they tried to put you down. This fired. It should have stopped your heart. Instead it burned out your memory and left you breathing.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "Then why am I alive?", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "I don't know. Somebody built this wrong, or built it to fail. You didn't survive the Dominion. You survived your own execution.", seconds = 5.5f },
                new DialogueLine { speaker = "Kessler", text = "They built you to end crime syndicates, not to collect children.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "Then whatever they made me for, it's done. The rest is mine.", seconds = 3f },
            };
        }

        private static DialogueLine[] GetEngineHunterChallengeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Hunter", text = "Stand down. This is a retrieval authorization.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetEngineHunterAfterLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "I'm sorry.", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetEngineBriefLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "She was never going to the Ronin Program.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "The Dominion doesn't care which syndicate holds the children. They feed them all and look the other way.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "She was collateral to that arrangement.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetEngineLotusDecisionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "It's a lure. False distress. But the records it's broadcasting are real.", seconds = 3f },
                new DialogueLine { speaker = "Mira", text = "They're from Block Seven. Senna. Lyric. Keph. The others. They're all on that list.", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-7", text = "We turn away from this. We sail dark and hope no one's tracking us.", seconds = 3f },
                new DialogueLine { speaker = "Mira", text = "I won't leave them.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "If you care about the girl, you care about the rest of them.", seconds = 2.5f },
            };
        }

        // ---- LOTUS STATION DIALOGUE (Beats 16-20) ----

        private static DialogueLine[] GetLotusGuidanceLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mira", text = "Left corridor. The second door on your right is clear. The chamber after has three of them. Can you see the air vents?", seconds = 4f },
            };
        }

        private static DialogueLine[] GetLotusFightBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Crimson Lotus Enforcer 1", text = "Intruder in medical! Blade response!", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetLotusPodbayLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mira", text = "Rescue pod bay ahead. Third corridor, door marked 7-C. That's where they are.", seconds = 3f },
            };
        }

        private static DialogueLine[] GetLotusSennaLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Senna", text = "Are you Dominion?", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "No.", seconds = 1f },
                new DialogueLine { speaker = "Senna", text = "Then you're one of us. Some things come back, when you're ready, they come back.", seconds = 3.5f },
            };
        }

        private static DialogueLine[] GetLotusKhallLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "You are my finest instrument. Return the children and submit to retrieval. I will make you useful again, and you will not remember refusing.", seconds = 5f },
                new DialogueLine { speaker = "Khall", text = "Refuse, and I stop hunting the weapon and start hunting the man who hides it. How long does your salvager last?", seconds = 4.5f },
                new DialogueLine { speaker = "Kessler", text = "He's in your head. Don't listen.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "Five minutes, Seven. Then I choose for you.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "Let me go back.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "No.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "I said no. We've come too far for you to give yourself back to them.", seconds = 3.5f },
            };
        }

        private static DialogueLine[] GetLotusReunionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Come home. Now.", seconds = 1.5f },
                new DialogueLine { speaker = "Mira", text = "You came back for us.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "Yes.", seconds = 1f },
                new DialogueLine { speaker = "Mira", text = "If they told you we don't matter, they lied.", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-7", text = "I know.", seconds = 1.5f },
            };
        }

        // ---- SANCTUARY DIALOGUE (Beats 21-26) ----

        private static DialogueLine[] GetSanctuaryDoctorLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Resistance Doctor", text = "The switch in you is dead as a trigger, it already fired. But it's still wired to their network; they can try to throw it again from anywhere.", seconds = 5f },
                new DialogueLine { speaker = "Resistance Doctor", text = "I can't cut it out, I don't have the tools. But I can jam its carrier signal. Stay inside the field and they can't reach it. Step outside, and you're exposed.", seconds = 5.5f },
                new DialogueLine { speaker = "Resistance Doctor", text = "There's a small school here. A garden. People who won't ask where you came from. The children can stay.", seconds = 3.5f },
            };
        }

        private static DialogueLine[] GetSanctuaryAssaultBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Rustfang Raider", text = "Dominion contract says this sanctuary burns! Move in!", seconds = 2f },
                new DialogueLine { speaker = "Rustfang Raider", text = "Hold the platform! The bounty's worth the blood!", seconds = 2f },
            };
        }

        private static DialogueLine[] GetSanctuaryQuietLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mira", text = "What will you do now?", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "I don't know.", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "That used to sound like failure.", seconds = 2f },
                new DialogueLine { speaker = "Mira", text = "Doesn't it now?", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "No. Not anymore.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetSanctuaryFileLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mira", text = "I found it in the station's secondary vault. I think it's about you.", seconds = 3f },
                new DialogueLine { speaker = "Mira", text = "I can try to decode it. I learned enough about neural security before I ran. If you want me to.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "Try.", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetSanctuaryObservatoryLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Iris", text = "You're thinking about the file.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "I am.", seconds = 1f },
                new DialogueLine { speaker = "Iris", text = "If they wiped you to make you useful, is who you were before still in there to find? Some part they couldn't burn out?", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "I don't know.", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetSpaceEp03PostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Doctor's jammer is holding. As long as we stay in the field, they can't reach the switch. We're safe, for now.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Mira's still working that encrypted file. Kethel-7. If anything's left of who you were, it's in there.", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we find out who I was, and why they couldn't kill me.", seconds = 3f },
            };
        }
    }
}
