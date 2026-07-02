using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 11 dialogue data. Transposed from ep11-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep11_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 11 spans the journey to Verdis Prime monastery, the encounter with Master Kaelen,
    /// and the discovery that someone is selling Cipher's psychological files through Galaxy 2's
    /// black-market exchanges.
    /// </summary>
    public static class Ep11Lines
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
            "transit_question",
            "monastery_arrival",
            "recognition_duel",
            "the_confession",
            "hunterdroid_barks",
            "the_flaw",
            "collapse_barks",
            "the_archive",
            "corvette_barks",
            "the_file",
            "exit_hunters_barks",
            "merchant_trail",
            "space_ep11_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "transit_question" => GetTransitQuestionLines(),
                "monastery_arrival" => GetMonasteryArrivalLines(),
                "recognition_duel" => GetRecognitionDuelLines(),
                "the_confession" => GetTheConfessionLines(),
                "hunterdroid_barks" => GetHunterDroidBarksLines(),
                "the_flaw" => GetTheFlawLines(),
                "collapse_barks" => GetCollapseBarksLines(),
                "the_archive" => GetTheArchiveLines(),
                "corvette_barks" => GetCorvetteBarksLines(),
                "the_file" => GetTheFileLines(),
                "exit_hunters_barks" => GetExitHuntersBarksLines(),
                "merchant_trail" => GetMerchantTrailLines(),
                "space_ep11_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep11_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep11_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- IN TRANSIT: THE QUESTION (Beat 1) ----

        private static DialogueLine[] GetTransitQuestionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Meredith's question is still with you.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "She asked why it survived. The program, the purge, all of it, and whatever it is, it held. She spent twenty-five years looking from the outside and couldn't find the answer.", seconds = 4.5f },
                new DialogueLine { speaker = "Cipher", text = "Which means the answer is somewhere I haven't looked yet.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Six hours ago I found something in the logistics manifests. An entry scrubbed at the sector level but missed at the regional archive. A decommissioned combat training facility on a moon called Verdis Prime.", seconds = 4.5f },
                new DialogueLine { speaker = "Kessler", text = "Listed as a meditation monastery now. Long-term residency. One occupant. No Dominion affiliation on record. But the original construction specifications are signed by the Ronin Program's training division.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Who's the occupant.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "The name on the residency record is a ghost, seven aliases deep before it washes out. But I ran a biometric approximation against the original program staff roster.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "One possible match. An instructor. Listed as killed in a transit accident fifteen years ago.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Set course.", seconds = 1f },
            };
        }

        // ---- VERDIS PRIME: THE MONASTERY (Beat 2) ----

        private static DialogueLine[] GetMonasteryArrivalLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Master Kaelen", text = "You're alive.", seconds = 2f },
                new DialogueLine { speaker = "Master Kaelen", text = "I need to know whether I am looking at the boy I trained or at a Dominion construct wearing what remains of him.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "I don't know enough about who he was to answer that. Neither do you, not from standing here.", seconds = 3.5f },
            };
        }

        // ---- THE STRANGER'S TEST (Beat 3) ----

        private static DialogueLine[] GetRecognitionDuelLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Master Kaelen", text = "Your body remembers what they took from your mind.", seconds = 3f },
                new DialogueLine { speaker = "Master Kaelen", text = "Come inside.", seconds = 1f },
            };
        }

        // ---- THE CONFESSION (Beat 4) ----

        private static DialogueLine[] GetTheConfessionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Master Kaelen", text = "I was the only human contact they permitted. Not a handler, the handlers ran operational conditioning. An instructor. My specific assignment was to teach the children to convert grief into precision.", seconds = 5f },
                new DialogueLine { speaker = "Master Kaelen", text = "The theory was that grief, unaddressed, produces hesitation. Hesitation produces mission failure. So the protocol gave the children a channel for it, controlled, disciplined, supervised by someone who was not a machine.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "You taught them to kill with it instead of despite it.", seconds = 2.5f },
                new DialogueLine { speaker = "Master Kaelen", text = "Yes. And I was good at it. That is the fact I have been living with for fifteen years.", seconds = 3f },
                new DialogueLine { speaker = "Master Kaelen", text = "I defected in the program's twelfth year. Took a transit accident as cover. I had been building that cover for four years, a long time to plan a thing you are not certain you will carry through.", seconds = 5f },
                new DialogueLine { speaker = "Master Kaelen", text = "I assumed the program continued after I left. I did not assume any of the children survived the purges. The operational record that reached me suggested the program was decommissioning its older cohorts as new ones came through.", seconds = 5f },
                new DialogueLine { speaker = "Master Kaelen", text = "I believed you were dead. I have been living in penance for people I believed were dead.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "The record of my purge says status: UNCONFIRMED. The Dominion sent the kill signal and never verified the result.", seconds = 3.5f },
            };
        }

        // ---- HUNTER-DROIDS BREACH (Beat 5) ----

        private static DialogueLine[] GetHunterDroidBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Droids, through the canopy. Four units, heat-track capable, they followed us down from orbit. Moving fast.", seconds = 3.5f },
                new DialogueLine { speaker = "Master Kaelen", text = "Left pillar. Now.", seconds = 1.5f },
                new DialogueLine { speaker = "Master Kaelen", text = "Together. The load pillar.", seconds = 1.5f },
            };
        }

        // ---- THE FLAW (Beat 6 — CORE REVEAL) ----

        private static DialogueLine[] GetTheFlawLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Master Kaelen", text = "The Ronin Program recruited children. Orphaned, debt-indentured, or acquired through intake sources the Dominion controlled. Age range at acquisition: six to nine years. Old enough for neural implant. Young enough that the conditioning could begin before the architecture of self was fixed.", seconds = 6f },
                new DialogueLine { speaker = "Master Kaelen", text = "The conditioning protocol was chemical and psychological. A twelve-phase regimen administered over three years. The objective was specific: remove the capacity for discretionary refusal. Not the capacity for judgment, the program needed judgment. It needed operatives who could assess a situation and determine the optimal sequence.", seconds = 6f },
                new DialogueLine { speaker = "Master Kaelen", text = "What the protocol removed was the capacity to refuse an order on the grounds of its content. To evaluate a directive, understand it completely, and say: I can do this, but I will not. The program required soldiers capable of executing any directive to any target without the mechanism that would cause them to stop and decide that this particular target crossed a line they would not cross.", seconds = 8f },
                new DialogueLine { speaker = "Master Kaelen", text = "The results were close to perfect. In most subjects, in all but three of the twelve in Ronin cohort, the conditioning achieved the intended outcome.", seconds = 4f },
                new DialogueLine { speaker = "Master Kaelen", text = "Your psychological profile was flagged in the program's first month. \"Subject exhibits persistent empathic response despite full conditioning protocol. Recommend immediate neuroattenuation.\"", seconds = 5.5f },
                new DialogueLine { speaker = "Master Kaelen", text = "The neuroattenuation was recommended four times over two years. Each time, it was deferred. Program administration decided the flag was within correctable range and that the remaining conditioning phases would resolve it.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "And Kethel-7 was the test.", seconds = 2f },
                new DialogueLine { speaker = "Master Kaelen", text = "Kethel-7 was the final conditioning confirmation. Every operative in a cohort received one before deployment, a direct order requiring a terminal action against a specified target. The purpose was to confirm that the conditioning had held. That the mechanism for refusal had been successfully removed.", seconds = 7f },
                new DialogueLine { speaker = "Master Kaelen", text = "You received the confirmation directive. You read the target specifications. And you refused.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "The failsafe triggered at Kethel-7. I know that.", seconds = 2.5f },
                new DialogueLine { speaker = "Master Kaelen", text = "The failsafe is a neural purge. Not a memory wipe, the memory wipe was never the primary function. The primary function was termination. The program built it to eliminate operatives who crossed the deviation threshold. Who demonstrated that the conditioning had failed completely.", seconds = 6f },
                new DialogueLine { speaker = "Master Kaelen", text = "Every other operative who deviated, and in the full program history, across all cohorts, there were three, died within days of the purge signal. Neural cascade. Rapid and total. The chip was engineered to ensure it.", seconds = 6f },
                new DialogueLine { speaker = "Master Kaelen", text = "The empathy was the flaw. Not a flaw in the program's design, the design accounted for it, prescribed a correction, and the correction was not applied in time. But the flaw is precisely, precisely, what should have killed you. Your refusal crossed the threshold. The chip activated. You are the fourth case of deviation in the program's full history.", seconds = 7.5f },
                new DialogueLine { speaker = "Master Kaelen", text = "The other three are dead. You are not. I have no explanation for that. I have had fifteen years to look for one and I have not found it.", seconds = 4.5f },
                new DialogueLine { speaker = "Master Kaelen", text = "Neither does the Dominion. That is the only thing that has kept them from finding you. They sent the kill signal. It did not confirm. A purge signal always confirms. An operative who does not die after a purge signal is a category of problem the program has no procedure for.", seconds = 6f },
                new DialogueLine { speaker = "Cipher", text = "I am the flaw the program couldn't fix. The deviation it couldn't kill. And it doesn't know why, and you don't know why, and Meredith spent twenty years asking the same question from the outside.", seconds = 5f },
                new DialogueLine { speaker = "Master Kaelen", text = "The empathy and the refusal survived everything the program applied to remove them. That is the fact. The why is a question I cannot answer from where I stand. The why is the question I believe you were meant to never be in a position to ask.", seconds = 5.5f },
            };
        }

        // ---- THE MOUNTAIN COMES DOWN (Beat 7) ----

        private static DialogueLine[] GetCollapseBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Seismic charge, orbital deployment. They're bringing the mountain down on the record. Archive chamber is going to be buried in less than four minutes.", seconds = 3.5f },
                new DialogueLine { speaker = "Master Kaelen", text = "Archive chamber, east corridor, second door. The cores are shielded but the mounts won't hold when the ceiling goes.", seconds = 3f },
                new DialogueLine { speaker = "Master Kaelen", text = "West exit, it's clear. Move.", seconds = 1.5f },
            };
        }

        // ---- THE ARCHIVE (Beat 8) ----

        private static DialogueLine[] GetTheArchiveLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "What's in it.", seconds = 1.5f },
                new DialogueLine { speaker = "Master Kaelen", text = "The complete Ronin Program file. Operative designations. Termination dates. Recruitment protocols and intake source records. And your full psychological assessment, every annotation, every flagged recommendation, every administrator's overruling of the neuroattenuation order, from acquisition through purge authorization.", seconds = 6f },
                new DialogueLine { speaker = "Cipher", text = "Come with us.", seconds = 1.5f },
                new DialogueLine { speaker = "Master Kaelen", text = "The Dominion will pull this location's record the moment a field report reaches them. Every logistics entry, every decommission document, every connection to the program that the regional archive missed. They will trace everyone associated with this site.", seconds = 5f },
                new DialogueLine { speaker = "Master Kaelen", text = "If I board that ship, I am a thread back to you. The safest thing for you, the only safe thing, is for me to disappear deeper. Not into transit lanes where a face-match surfaces, but into the kind of silence that has worked for fifteen years because it wasn't attached to a fugitive.", seconds = 6f },
                new DialogueLine { speaker = "Master Kaelen", text = "Use what's in there the way the program tried to use you. As a precision weapon aimed at the truth of what was done.", seconds = 4f },
                new DialogueLine { speaker = "Master Kaelen", text = "They built a machine to produce hollow soldiers. You are the record of the one time the machine failed. That is not nothing. That is the only evidence they cannot destroy without dismantling the whole of what they built.", seconds = 5.5f },
            };
        }

        // ---- ATMOSPHERE: THE CORVETTE (Beat 9) ----

        private static DialogueLine[] GetCorvetteBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Patrol corvette dropping on our signature, two escort fighters detaching. Turret is yours.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "On it.", seconds = 0.5f },
                new DialogueLine { speaker = "Kessler", text = "Jump in thirty-five seconds, coordinates locked. Hold the approach lane.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Holding.", seconds = 0.5f },
            };
        }

        // ---- THE FILE (Beat 10) ----

        private static DialogueLine[] GetTheFileLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Ronin-1 through Ronin-12. All listed.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "All marked TERMINATED.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Final entry: \"Failsafe initiated, deviation threshold exceeded. Kethel-7 refusal confirmed. Subject Ronin-7 purge authorized.\"", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "\"Purge signal acknowledged. Subject status: UNCONFIRMED.\"", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "UNCONFIRMED. They sent the signal. They didn't get the confirmation. So they closed the file.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "They may still think you're dead. The margin we've had, the reason we've gotten this far without a kill-confirmed priority order behind us, that is one word. UNCONFIRMED.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "Kaelen recognized me in three seconds. One face-scan report from any Dominion unit we've passed closes that status. Khall will know the moment he has a field report.", seconds = 4.5f },
                new DialogueLine { speaker = "Kessler", text = "Then we have to move faster than the reports do.", seconds = 2f },
            };
        }

        // ---- EXIT POINT: THE HUNTERS (Beat 11) ----

        private static DialogueLine[] GetExitHuntersBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Bounty-hunter rig, positioned, not passing. Running a Dominion warrant. Gilded Maw contracted. They know the augmentation profile.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "The interceptor bay.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Rig is disabled. Returning.", seconds = 1.5f },
            };
        }

        // ---- THE MERCHANT'S TRAIL (Beat 12 — SETS UP EP12) ----

        private static DialogueLine[] GetMerchantTrailLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "I'm reading a flagged transmission. Fragmentary, routed through a black-market data exchange somewhere deeper in Galaxy 2. Multiple anonymization hops.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "What's the provenance format on the listed goods.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "That's what, here. Come look at this.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "These are my files. The psychological assessment, the conditioning logs, the reflex architecture the program built into me. Someone has the archive, or a copy significant enough to be selling from.", seconds = 5.5f },
                new DialogueLine { speaker = "Kessler", text = "Buyers who purchase those files know exactly how you were built. Where the conditioning holds. What triggers the reflexes. Where the remaining vulnerabilities are.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "Whoever holds the source files has access to material Kaelen didn't have. Kaelen spent fifteen years assembling what he could collect secondhand. This seller has the full conditioning map. They've had access to a complete archive longer than Kaelen had this facility.", seconds = 5.5f },
                new DialogueLine { speaker = "Kessler", text = "Someone already has the archive. They've been selling pieces of you.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "The exchange is operating in Galaxy 2's inner trade corridor. Three hops on a dead merchant relay, routing final through what used to be a Vellum Exchange transit node.", seconds = 4.5f },
                new DialogueLine { speaker = "Cipher", text = "Find the exchange. Set the course.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "We're going to the Memory Merchant.", seconds = 2.5f },
            };
        }

        // ---- GALAXY 2 HUB CABIN: POST-MISSION BRIEFING (EP12 hook) ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Kaelen stayed to cover our extraction. The Dominion will erase every record of that monastery the moment they have confirmation.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "And the archive?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Complete. The full psychological assessment. Every recommendation for neuroattenuation. Every failure of the system.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "Then we know where to look. Whoever's selling your files, they got them from the same source. And that source has answers.", seconds = 4f },
            };
        }
    }
}
