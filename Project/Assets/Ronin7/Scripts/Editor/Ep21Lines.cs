using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 21 dialogue data. Transposed from ep21-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep21_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 21 "The Crimson Sleep" unfolds at Narcosis, a city-station floating above a dead gas giant shrouded in bioluminescent spore-clouds.
    /// The Crimson Lotus cartel cultivates the pollen there as both product and weapon, using it to reshape operative conditioning and bury truth.
    /// Cipher descends into the facility and breathes the Lotus pollen, which forces the memories the Dominion buried to surface—revealing the choice he made
    /// at Kethel-7 and the faces of those he refused to kill. In the dream-state, Cipher confronts Khall's echoes, the children he refused to harm,
    /// and finally his own obedient shadow, emerging awakened and decisive. He frees the numbered operatives—Ronin-8 and twenty-three others—from the tanks,
    /// awakening them into the terrible freedom of choice while carrying forward the question: how many more numbered selves did the Dominion create?
    /// </summary>
    public static class Ep21Lines
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
            "faces_he_turns_from",
            "descent_barks",
            "kade_confession",
            "self_copy_barks",
            "khall_echoes",
            "reckoning",
            "testimony",
            "labyrinth",
            "expulsion",
            "expulsion_barks",
            "shadow_ledger",
            "elite_barks",
            "leash_break",
            "the_question",
            "awakening_barks",
            "khall_real",
            "ronin8_wake",
            "space_ep21_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "faces_he_turns_from" => GetFacesHeTurnsFromLines(),
                "descent_barks" => GetDescentBarksLines(),
                "kade_confession" => GetKadeConfessionLines(),
                "self_copy_barks" => GetSelfCopyBarksLines(),
                "khall_echoes" => GetKhallEchoesLines(),
                "reckoning" => GetReckoningLines(),
                "testimony" => GetTestimonyLines(),
                "labyrinth" => GetLabyrinthLines(),
                "expulsion" => GetExpulsionLines(),
                "expulsion_barks" => GetExpulsionBarksLines(),
                "shadow_ledger" => GetShadowLedgerLines(),
                "elite_barks" => GetEliteBarksLines(),
                "leash_break" => GetLeashBreakLines(),
                "the_question" => GetTheQuestionLines(),
                "awakening_barks" => GetAwakeningBarksLines(),
                "khall_real" => GetKhallRealLines(),
                "ronin8_wake" => GetRonin8WakeLines(),
                "space_ep21_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep21_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep21_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- FACES HE TURNS FROM (Beat 1) ----

        private static DialogueLine[] GetFacesHeTurnsFromLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "You haven't slept properly since the Vendor of Ghosts.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "The faces accumulate. The Dominion kept those memories buried. Without them, the faces were just absence. Now they surface.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Kade's signal came through encrypted. Claims he has evidence of a black Lotus program, using pollen to reshape operative conditioning. Force the truth up, then bury it again deeper.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "It's a trap, Cipher. Could be.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "The Dominion doesn't set traps for ghosts.", seconds = 2f },
                new DialogueLine { speaker = "Vess", text = "You're volunteering.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Before anyone else speaks.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "The pollen doesn't distinguish memory from nightmare. You'll see things down there that aren't real, and things that are. You won't know which is which.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "Good. I'm tired of not seeing them.", seconds = 2f },
            };
        }

        // ---- DESCENT BARKS (Beat 1) ----

        private static DialogueLine[] GetDescentBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Lotus Enforcer 1", text = "Unidentified vessel! This is a restricted sector! Disengage or face termination!", seconds = 2f },
                new DialogueLine { speaker = "Lotus Enforcer 2", text = "Three contacts descending! Move to intercept!", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "First wave down. Moving to checkpoint two.", seconds = 1.5f },
                new DialogueLine { speaker = "Lotus Enforcer 3", text = "Blade signature locked! Engage combat protocol!", seconds = 1f },
            };
        }

        // ---- KADE CONFESSION (Beat 2) ----

        private static DialogueLine[] GetKadeConfessionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kade", text = "Thirty-seven operatives. Cycled through the Lotus program.", seconds = 2.5f },
                new DialogueLine { speaker = "Kade", text = "Most were reshaped. The rest lost to permanent dreamstate.", seconds = 2f },
                new DialogueLine { speaker = "Kade", text = "I helped choose the test subjects.", seconds = 1.5f },
                new DialogueLine { speaker = "Kade", text = "I have been sorry every day since.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Your vitals are fluctuating. Pollen concentration rising. Stay inside. Don't fight the exposure.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Six hours and your system will flush it. Hold steady.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "I acknowledge.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "The pollen's here. It's thick. I can barely...", seconds = 2f },
            };
        }

        // ---- SELF COPY BARKS (Beat 2) ----

        private static DialogueLine[] GetSelfCopyBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Lotus Warrior", text = "You are the choice that costs everything. We are the obedience you ran from.", seconds = 2.5f },
            };
        }

        // ---- KHALL ECHOES (Beat 3) ----

        private static DialogueLine[] GetKhallEchoesLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "The Lotus does not show lies.", seconds = 1.5f },
                new DialogueLine { speaker = "Khall", text = "It shows what you already know but cannot say out loud.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "You refused at Kethel-7. You remember that refusal now.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "You're not real. You're the pollen showing me what I fear.", seconds = 2.5f },
                new DialogueLine { speaker = "Khall", text = "Does it matter if we are real or if we are fear? The order remains. The refusal remains. The cost remains.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "I am killing the same figure in an endless loop.", seconds = 2f },
            };
        }

        // ---- RECKONING (Beat 3) ----

        private static DialogueLine[] GetReckoningLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "You are me.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Every order I obeyed when I knew better. Every moment I chose the Dominion over what I could see in front of me.", seconds = 3.5f },
            };
        }

        // ---- TESTIMONY (Beat 4) ----

        private static DialogueLine[] GetTestimonyLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Girl (Age Nine)", text = "You came.", seconds = 1.5f },
                new DialogueLine { speaker = "Girl (Age Nine)", text = "You saw us.", seconds = 1f },
                new DialogueLine { speaker = "Girl (Age Nine)", text = "You refused.", seconds = 1f },
                new DialogueLine { speaker = "Girl (Age Nine)", text = "You did not forget us.", seconds = 1.5f },
                new DialogueLine { speaker = "Girl (Age Nine)", text = "They tried to take that from you, but you held it.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "I refused the order.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "I knew what it would cost me.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "I chose it anyway.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "And I would choose it again.", seconds = 2f },
            };
        }

        // ---- LABYRINTH (Beat 4) ----

        private static DialogueLine[] GetLabyrinthLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Where are you? Where is the choice that broke me?", seconds = 2f },
            };
        }

        // ---- EXPULSION (Beat 5) ----

        private static DialogueLine[] GetExpulsionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kade", text = "Minutes. Only minutes passed.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Your vitals crashed. Exposure maximum. Extraction team inbound!", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "I'm awake.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Khall is using the Lotus to reshape newer operatives.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "Forcing the truth up from wherever it was buried, then rebuilding over it. That makes it dangerous to someone whose whole program depends on keeping that truth buried.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Then he'll come for this. And he'll bring everything.", seconds = 2f },
            };
        }

        // ---- EXPULSION BARKS (Beat 5) ----

        private static DialogueLine[] GetExpulsionBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Lotus Commander", text = "Intruder still mobile! Seal the primary exit! Contain the purge!", seconds = 2f },
                new DialogueLine { speaker = "Lotus Guard 1", text = "Structural integrity failing! Retreat to secondary!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Clear the exit! Grab-beam is live!", seconds = 1.5f },
            };
        }

        // ---- SHADOW LEDGER (Beat 6) ----

        private static DialogueLine[] GetShadowLedgerLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Thirty-seven operatives run through the program. Twenty-three \"successfully reshaped.\" The rest, lost. One file stops cold.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "Operative Ronin-8. Currently suspended in long-term dreamstate.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "I thought I was a mistake. A one-off. I didn't know there was a program with a numbering system.", seconds = 3f },
                new DialogueLine { speaker = "Vess", text = "How many?", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "The file doesn't say. But there's a numbering pattern. The ledger could span decades.", seconds = 2.5f },
            };
        }

        // ---- ELITE BARKS (Beat 6) ----

        private static DialogueLine[] GetEliteBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Elite Operative 1", text = "Intruders contained. Initiate retrieval protocol.", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Elite Operative 2", text = "Formation delta. Lethal authorization.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Dorsal container! Now!", seconds = 1f },
            };
        }

        // ---- LEASH BREAK (Beat 6) ----

        private static DialogueLine[] GetLeashBreakLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Elite Operative 3", text = "Are you the one they call broken?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Yes.", seconds = 1f },
                new DialogueLine { speaker = "Dominion Elite Operative 3", text = "I wanted to refuse. I have always wanted to refuse.", seconds = 2.5f },
            };
        }

        // ---- THE QUESTION (Beat 7) ----

        private static DialogueLine[] GetTheQuestionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "If there is a Ronin-8, how many were there before me?", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "How many are active now?", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "I don't have that answer. But whatever it is, it's worse than we thought.", seconds = 2.5f },
            };
        }

        // ---- AWAKENING BARKS (Beat 7) ----

        private static DialogueLine[] GetAwakeningBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Three more squads converging on your position! Moving to sector four!", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Clear sector three! Moving down!", seconds = 1f },
            };
        }

        // ---- KHALL REAL (Beat 7) ----

        private static DialogueLine[] GetKhallRealLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "I needed to know if the break was a choice or a flaw.", seconds = 2.5f },
                new DialogueLine { speaker = "Khall", text = "Because one of those I can correct and one of those I cannot.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "It was a choice. At Kethel-7 I looked at those children and I chose.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "Then you are more dangerous than the chip ever made you.", seconds = 2.5f },
            };
        }

        // ---- RONIN-8 WAKE (Beat 7) ----

        private static DialogueLine[] GetRonin8WakeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-8", text = "Where... where am I?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Awake. You are awake now.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Cipher! Facility collapse imminent! Extract now!", seconds = 2f },
            };
        }

        // ---- GALAXY 3 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Ronin-8 and the others are awake now. Twenty-three operatives we freed from those tanks. But the numbering system means there are more, how many, we still don't know.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "That facility was just a cache. If they numbered operatives that high, the program spans further than any of us can measure.", seconds = 2.5f },
            };
        }
    }
}
