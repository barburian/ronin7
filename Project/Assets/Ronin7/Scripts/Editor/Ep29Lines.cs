using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 29 dialogue data. Transposed from ep29-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep29_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 29 "The Unbroken Bond" — Ketos Prime orbital station and the encounter with Sister-Iron.
    /// Cipher receives a message from Samurai-4, an operative from his Program days, now designated
    /// Sister-Iron and sent to complete his purification. The confrontation at Ketos Prime, their
    /// childhood training hub, forces both of them to confront the nature of the Dominion's conditioning
    /// and what freedom truly costs. Samurai-4 breaks her neural leash, sacrificing her escape to let
    /// Cipher survive. The failsafe runs slower than expected — someone in the Dominion's chain is
    /// protecting him, and the truth lies in sealed operative files waiting to be stolen.
    /// </summary>
    public static class Ep29Lines
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
            "sister_signal",
            "duel_intro",
            "duel_yield",
            "archive_descent",
            "neural_scar",
            "implant_evidence",
            "leash_break",
            "wrist_sacrifice",
            "escape_reflection",
            "intermission_packet",
            "space_ep29_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "sister_signal" => GetSisterSignalLines(),
                "duel_intro" => GetDuelIntroLines(),
                "duel_yield" => GetDuelYieldLines(),
                "archive_descent" => GetArchiveDescentLines(),
                "neural_scar" => GetNeuralScarLines(),
                "implant_evidence" => GetImplantEvidenceLines(),
                "leash_break" => GetLeashBreakLines(),
                "wrist_sacrifice" => GetWristSacrificeLines(),
                "escape_reflection" => GetEscapeReflectionLines(),
                "intermission_packet" => GetIntermissionPacketLines(),
                "space_ep29_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep29_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep29_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ==== BEAT 1 — THE SISTER IN THE SIGNAL ====

        private static DialogueLine[] GetSisterSignalLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Someone in the Program just pinged your augmentation signature.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Tight-beam frequency. Handler-tier encryption. This is live traffic, not intercepted archive.", seconds = 3f },
                new DialogueLine { speaker = "Samurai-4", text = "Operative Ronin-7. If you receive this, understand that I am not here to retrieve you.", seconds = 3.5f },
                new DialogueLine { speaker = "Samurai-4", text = "I am here to finish what the Failsafe began. You are contaminated. I will purify you.", seconds = 3f },
                new DialogueLine { speaker = "Samurai-4", text = "Ketos Prime, six cycles. Your old home awaits. They call me Sister-Iron now.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "How did she find us?", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Your neural signature carries a Dominion beacon. Always has. I thought it was passive, surveillance only. But it can broadcast. And if someone knows how to query it...", seconds = 4f },
                new DialogueLine { speaker = "Iris", text = "That's a kill contract. From the inside.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Ketos Prime was our training hub. Twelve of us. We grew up in those corridors.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "One of them just announced she's coming to kill me.", seconds = 2f },
            };
        }

        // ==== BEAT 2 — THE UNBROKEN (INTRO) ====

        private static DialogueLine[] GetDuelIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Samurai-4", text = "We grew up together.", seconds = 1.5f },
                new DialogueLine { speaker = "Samurai-4", text = "Same facility. Same trainer. Same conditioning. But you broke, and I remained whole.", seconds = 3f },
                new DialogueLine { speaker = "Samurai-4", text = "They told us you died the moment you disobeyed. That the Failsafe was the Program's kindness, burning the corruption before it spread.", seconds = 3.5f },
                new DialogueLine { speaker = "Samurai-4", text = "I believed them. I was proud of you, even dead.", seconds = 2f },
                new DialogueLine { speaker = "Samurai-4", text = "Then you surfaced in Tide Baron space three cycles ago. You fought with a Dominion blade. That was you, wasn't it?", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Yes.", seconds = 1f },
                new DialogueLine { speaker = "Samurai-4", text = "I spent years hunting Hollow Kings archives to find you. Shards of your memory in traders' vaults. Fragments. Like you'd been scattered and I was supposed to collect the pieces and put them back wrong.", seconds = 4f },
                new DialogueLine { speaker = "Samurai-4", text = "You were never meant to escape. You are defective. Unfixed. And somehow, absurdly, alive.", seconds = 3f },
                new DialogueLine { speaker = "Samurai-4", text = "I am going to fix that. And then I am going home.", seconds = 2f },
            };
        }

        // ==== BEAT 2 — THE UNBROKEN (POST-DUEL) ====

        private static DialogueLine[] GetDuelYieldLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Samurai-4", text = "There. That is the fighter I remember.", seconds = 2f },
                new DialogueLine { speaker = "Samurai-4", text = "It does not matter.", seconds = 1.5f },
                new DialogueLine { speaker = "Samurai-4", text = "I just transmitted this station's location to the Dominion. In twelve hours, Ketos Prime burns.", seconds = 3f },
                new DialogueLine { speaker = "Samurai-4", text = "I will go home as the operative who closed the Ronin-7 file. You will be ash, and the contamination will be contained.", seconds = 3f },
                new DialogueLine { speaker = "Samurai-4", text = "This is what I was built for.", seconds = 1.5f },
            };
        }

        // ==== BEAT 3 — THE ARCHIVE DESCENT (FIRST HALF) ====

        private static DialogueLine[] GetArchiveDescentLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Samurai-4", text = "You want to remember?", seconds = 1.5f },
                new DialogueLine { speaker = "Samurai-4", text = "Be careful what you ask for.", seconds = 2f },
                new DialogueLine { speaker = "Samurai-4", text = "Do you know what your first kill was?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "No.", seconds = 1f },
                new DialogueLine { speaker = "Samurai-4", text = "Me. Not fatally. Not permanently. They made us fight to unconsciousness to build the bond.", seconds = 3.5f },
                new DialogueLine { speaker = "Samurai-4", text = "You won. I woke up with neural scarring I still carry. That is love in the Program.", seconds = 2.5f },
                new DialogueLine { speaker = "Samurai-4", text = "That is what family means in here. And I am standing in your way right now because I still love you enough to end you before they do it worse.", seconds = 3.5f },
            };
        }

        // ==== BEAT 3 — THE SCAR IN MEMORY (SECOND HALF) ====

        private static DialogueLine[] GetNeuralScarLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Samurai-4", text = "You are still fast.", seconds = 1.5f },
                new DialogueLine { speaker = "Samurai-4", text = "You are still mine.", seconds = 1f },
                new DialogueLine { speaker = "Samurai-4", text = "This is mercy, do you understand that?", seconds = 2f },
                new DialogueLine { speaker = "Samurai-4", text = "If they take you alive, they will unmake you cell by cell.", seconds = 2.5f },
                new DialogueLine { speaker = "Samurai-4", text = "At least this way, it is me. At least it is quick.", seconds = 2f },
            };
        }

        // ==== BEAT 4 — THE IMPLANT ====

        private static DialogueLine[] GetImplantEvidenceLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Look at the execution rate.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "My Failsafe is still active. Still running.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "But it has been crawling for months, something is slowing it.", seconds = 2f },
                new DialogueLine { speaker = "Samurai-4", text = "That's impossible.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "It is not mercy. It is not malfunction.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Someone in the Dominion's own chain gave me a longer clock, then wiped my memory and threw me out anyway.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "You are hunting a ghost because you were told I was already dead. But the file they fed you is not the file I am living inside.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "We are not a program. We are evidence. And they are destroying that evidence one operative at a time.", seconds = 3f },
                new DialogueLine { speaker = "Samurai-4", text = "The data could be corrupted.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "You know it isn't.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "The Dominion took my memories for a reason. They wanted something buried.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "But you still have yours. You were never flagged for erasure.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Find the orphanage mission in your own conditioning log. Find it, and then tell me what you see.", seconds = 3f },
            };
        }

        // ==== BEAT 5 — THE SCAR IN MEMORY (LEASH BREAK) ====

        private static DialogueLine[] GetLeashBreakLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Samurai-4", text = "I cannot question a direct command.", seconds = 2f },
                new DialogueLine { speaker = "Samurai-4", text = "The conditioning...", seconds = 1f },
                new DialogueLine { speaker = "Samurai-4", text = "Do you feel that?", seconds = 1.5f },
                new DialogueLine { speaker = "Samurai-4", text = "The thing behind my neck that tightens every time I drift toward doubt?", seconds = 2.5f },
                new DialogueLine { speaker = "Samurai-4", text = "That is not loyalty. That is a leash.", seconds = 1.5f },
                new DialogueLine { speaker = "Samurai-4", text = "You are the only operative in the Program who ever broke it.", seconds = 2f },
                new DialogueLine { speaker = "Samurai-4", text = "And you are standing here telling me it broke you, not freed you.", seconds = 2.5f },
                new DialogueLine { speaker = "Samurai-4", text = "And you are still here. Still fighting. Still...", seconds = 2f },
                new DialogueLine { speaker = "Samurai-4", text = "My implant is not like yours.", seconds = 2f },
                new DialogueLine { speaker = "Samurai-4", text = "Mine still has a full collar. If I try to betray them, it activates.", seconds = 2.5f },
                new DialogueLine { speaker = "Samurai-4", text = "But there is one thing it cannot stop me from doing.", seconds = 1.5f },
                new DialogueLine { speaker = "Samurai-4", text = "The activation signal I sent, I just burned it.", seconds = 2f },
                new DialogueLine { speaker = "Samurai-4", text = "Get out. Live. Find out what we actually are.", seconds = 2f },
                new DialogueLine { speaker = "Samurai-4", text = "And Ronin-7?", seconds = 1f },
                new DialogueLine { speaker = "Samurai-4", text = "Come back for me when you have won.", seconds = 1.5f },
            };
        }

        // ==== BEAT 6 — THE ESCAPE (PART 1) ====

        private static DialogueLine[] GetWristSacrificeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Now, now, now!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Dominion corvettes, three minutes!", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "She let me go.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "My sister. She broke her conditioning and let me go.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Clear.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "That was the bloodiest extract we have run.", seconds = 1.5f },
            };
        }

        // ==== BEAT 6 — THE ESCAPE (PART 2) ====

        private static DialogueLine[] GetEscapeReflectionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "I made an ally. She reminded me of something the Program tried to cut out.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "I was trained to be alone.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "But we were never built to be alone. We were children.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Twelve of us. And at least one of them just chose to remember that.", seconds = 2.5f },
            };
        }

        // ==== BEAT 7 — THE UNFINISHED DUEL ====

        private static DialogueLine[] GetIntermissionPacketLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Samurai-4", text = "I survived the trigger. Barely.", seconds = 1.5f },
                new DialogueLine { speaker = "Samurai-4", text = "It will fire again if I move too fast, but slower now.", seconds = 2f },
                new DialogueLine { speaker = "Samurai-4", text = "I am going undercover inside the Dominion's inner tier, using my clearance to look for the orphanage files.", seconds = 3f },
                new DialogueLine { speaker = "Samurai-4", text = "You were right. Something is wrong with what they showed us.", seconds = 2f },
                new DialogueLine { speaker = "Samurai-4", text = "Find Handler Khall. Make him answer for what he knows.", seconds = 2f },
                new DialogueLine { speaker = "Samurai-4", text = "And Ronin-7...", seconds = 1f },
                new DialogueLine { speaker = "Samurai-4", text = "This is not goodbye. This is intermission.", seconds = 1.5f },
                new DialogueLine { speaker = "Samurai-4", text = "When we meet again, I want to fight you as myself, not as their instrument.", seconds = 2.5f },
                new DialogueLine { speaker = "Samurai-4", text = "Until then: stay alive. That is an order from your oldest sister.", seconds = 2f },
            };
        }

        // ==== GALAXY 4 HUB CABIN: POST-MISSION BRIEFING ====

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "We arrived at Ketos Prime, our old training hub, the place where twelve operatives grew up together before the Dominion scattered us across the galaxy. Samurai-4, designated Sister-Iron, was sent to complete my purification. In the neural archive beneath the station, we discovered that my Failsafe runs slower than it should. Someone in the Dominion's command chain slowed my purge, then erased my memory of why. That deception broke her conditioning. She burned her own activation signal and let me escape so I could find the truth. She is alive somewhere, undercover in the Dominion's inner tier, hunting the orphanage mission that both of us were ordered to commit.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "Which means the Dominion doesn't want us looking at sealed operative files. They're trying to bury something in your record, the real reason you refused that massacre at Kethel-7, the real reason someone protected your Failsafe, the real reason twelve children became weapons instead of staying children. There's a black-site archive that holds the answer. Handler-tier access only. We're going to steal your own file and find out what they need erased so badly they'd destroy one of their own operatives to keep it hidden.", seconds = 5f },
            };
        }
    }
}
