using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 13 dialogue data. Transposed from ep13-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep13_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 13 spans the journey to the Carnival of Forgotten Names, the confrontation with
    /// Coral Vex (Ronin-6), the discovery of the conversion archive, and the anomalous flag
    /// on Cipher's conversion record that marks him as the only exception in seventeen operatives.
    /// </summary>
    public static class Ep13Lines
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
            "transit_intro",
            "lyris_gate",
            "carousel_barks",
            "memory_stolen",
            "yask_warning",
            "mirror_maze",
            "mirror_after",
            "ringmaster_reveal",
            "coral_survival",
            "extraction_barks",
            "blade_at_throat",
            "vault_descent",
            "verath_barks",
            "vault_terminal",
            "khalls_gambit",
            "ronin10",
            "burning_escape",
            "drifting_coral",
            "archive_reviewed",
            "final_drone",
            "the_question",
            "space_ep13_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "transit_intro" => GetTransitIntroLines(),
                "lyris_gate" => GetLyrisGateLines(),
                "carousel_barks" => GetCarouselBarksLines(),
                "memory_stolen" => GetMemoryStolenLines(),
                "yask_warning" => GetYaskWarningLines(),
                "mirror_maze" => GetMirrorMazeLines(),
                "mirror_after" => GetMirrorAfterLines(),
                "ringmaster_reveal" => GetRingmasterRevealLines(),
                "coral_survival" => GetCoralSurvivalLines(),
                "extraction_barks" => GetExtractionBarksLines(),
                "blade_at_throat" => GetBladeAtThroatLines(),
                "vault_descent" => GetVaultDescentLines(),
                "verath_barks" => GetVerathBarksLines(),
                "vault_terminal" => GetVaultTerminalLines(),
                "khalls_gambit" => GetKhallsGambitLines(),
                "ronin10" => GetRonin10Lines(),
                "burning_escape" => GetBurningEscapeLines(),
                "drifting_coral" => GetDriftingCoralLines(),
                "archive_reviewed" => GetArchiveReviewedLines(),
                "final_drone" => GetFinalDroneLines(),
                "the_question" => GetTheQuestionLines(),
                "space_ep13_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep13_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep13_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- TRANSIT & APPROACH (Beats 1 & 2 combined) ----

        private static DialogueLine[] GetTransitIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Morrigan's intelligence puts the Carnival's current orbit in the Ashfield Corridor. It cycles position every seventy-two hours. We have an entry window that closes in about twenty-six hours.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "After that, it moves again and we spend a week tracking it through the Fringe Belt until the next window opens. The Dominion will have flagged our jump signatures from the Market by then.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "I heard you the first time, Kessler.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "The memory Lyris's barker was supposed to take from me at the gate, if the carnival's extraction tech works the way Morrigan described, it will be the specific moment of waking. When you pulled me out of the debris field.", seconds = 6f },
                new DialogueLine { speaker = "Cipher", text = "I don't have it to spare.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Then don't let them take it.", seconds = 2f },
                new DialogueLine { speaker = "Morrigan", text = "The Exchange's footprint is consistent with what I've been tracking. Four active surveillance bands, rotating cipher keys on the outer docking ring. The interior commons frequency is jammed against outside monitoring, standard Vellum practice.", seconds = 6f },
                new DialogueLine { speaker = "Morrigan", text = "Coral Vex is the Exchange's Ringmaster here. She has been running this carnival for at least eight cycles. I have encountered her name in three separate operative-archive transactions, she is not a broker. She holds things and she keeps them. That distinction matters.", seconds = 7f },
                new DialogueLine { speaker = "Cipher", text = "If she holds things and keeps them, she has been waiting for this visit.", seconds = 2.5f },
                new DialogueLine { speaker = "Morrigan", text = "She will recognize your designation. Which means she has been in contact with operative records at some point. How she got access to them is the question I cannot answer from here.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "I'll hold orbit. Three hours. After that, I assume the carnival's taken you apart and I start planning the recovery.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Take the secondary comms chip. If you get into something you can't finish alone, the frequency is...", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "The frequency's jammed from the inside. You told me.", seconds = 2f },
            };
        }

        // ---- THE GATE: LYRIS (Beat 3) ----

        private static DialogueLine[] GetLyrisGateLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Lyris", text = "Welcome to the Carnival of Forgotten Names. We don't ask where you're from. We don't ask what you've done. We only ask, what would you give to know what was taken from you?", seconds = 5f },
                new DialogueLine { speaker = "Lyris", text = "I'm Lyris. I'll be your guide. The first booth is already expecting you.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Who sent the invitation.", seconds = 1.5f },
                new DialogueLine { speaker = "Lyris", text = "The Carnival does not send invitations. It simply arranges itself to be findable by those with the right kind of absence. And you, I can tell, are carrying quite a lot of that.", seconds = 5f },
                new DialogueLine { speaker = "Lyris", text = "The carousel is just ahead. A small test of reflex and rhythm. The Exchange insists on it for new clients. Something about establishing a baseline.", seconds = 4.5f },
            };
        }

        // ---- THE CAROUSEL OF ECHOES (Beat 4) ----

        private static DialogueLine[] GetCarouselBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Memory Grinder Lead", text = "Three rotations, visitor. Still standing at the end, you go forward.", seconds = 2.5f },
                new DialogueLine { speaker = "Lyris", text = "Very good. Three minutes faster than the average. The Exchange will note that.", seconds = 2.5f },
            };
        }

        // ---- MEMORY STOLEN (Beat 5) ----

        private static DialogueLine[] GetMemoryStolenLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Lyris", text = "I should tell you, the carnival doesn't only give. It also receives. That's the exchange. First booth always takes something small. You won't notice for a few hours.", seconds = 4.5f },
                new DialogueLine { speaker = "Cipher", text = "What did it take.", seconds = 1.5f },
                new DialogueLine { speaker = "Lyris", text = "A moment. The specific quality of waking from a long unconsciousness, the first second of recognizing that someone chose to pull you from the dark rather than leave you there. The feeling of being chosen.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "The Ringmaster is in the center tent.", seconds = 2f },
                new DialogueLine { speaker = "Lyris", text = "She's been waiting for you. This way.", seconds = 2f },
            };
        }

        // ---- YASK'S WARNING (Beat 6) ----

        private static DialogueLine[] GetYaskWarningLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Yask", text = "You're the one she was watching for. The operator with the number. I've been told to let you through, but, I need a moment. Please.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Talk.", seconds = 0.5f },
                new DialogueLine { speaker = "Yask", text = "I was taken eight years ago. I was running a Dominion intelligence route through the Corridor, low-rank courier work, nothing that should have made me valuable. They took me anyway. Stripped what they wanted and offered me a choice: the Carnival, or the void.", seconds = 6f },
                new DialogueLine { speaker = "Yask", text = "I chose the Carnival. I'm still choosing it. I'm not ashamed of that.", seconds = 3.5f },
                new DialogueLine { speaker = "Yask", text = "The Ringmaster is not what she appears. I mean that as a warning, not an insult. What she has survived would have killed most things that can be killed.", seconds = 5f },
                new DialogueLine { speaker = "Yask", text = "Khall is monitoring this platform. From a distance, passive surveillance. He has been waiting to see who walks in here with your designation profile. Whatever happens inside, he is watching the outcomes.", seconds = 6f },
                new DialogueLine { speaker = "Cipher", text = "There's a cracked memory crystal near the shattered booth behind you.", seconds = 2f },
            };
        }

        // ---- THE MIRROR MAZE (Beat 7) ----

        private static DialogueLine[] GetMirrorMazeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "I know what you were. I know what they made you into.", seconds = 3f },
            };
        }

        // ---- MIRROR MAZE AFTER (Beat 8) ----

        private static DialogueLine[] GetMirrorAfterLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Lyris", text = "She's inside. She'll open the conversation. Whatever she asks you, answer it honestly. She'll know if you don't, and that matters here more than it usually does.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "And you?", seconds = 1f },
                new DialogueLine { speaker = "Lyris", text = "My job is the door. Hers is what's beyond it.", seconds = 2.5f },
            };
        }

        // ---- THE RINGMASTER STEPS FROM SHADOW (Beat 9) ----

        private static DialogueLine[] GetRingmasterRevealLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Coral Vex", text = "Seven.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "You know my designation.", seconds = 2f },
                new DialogueLine { speaker = "Coral Vex", text = "I know your designation because it is adjacent to mine. I am Ronin-6.", seconds = 3.5f },
                new DialogueLine { speaker = "Coral Vex", text = "Not dead. Converted. The Dominion's erasure protocol was never designed to terminate operatives, Seven. It was designed to rewrite them. To take a person who carried too much capability and too many memories and transform them into something the Dominion could use without the complications of personhood. A living extraction vessel. Walking intelligence hardware, networked to a central repository, harvesting data through every contact and combat engagement.", seconds = 10f },
                new DialogueLine { speaker = "Coral Vex", text = "When conversion completes, when the process runs its full course, the operative stops being a person. They become a file. A very sophisticated, self-updating file, walking around in a body that still functions perfectly.", seconds = 7f },
            };
        }

        // ---- CORAL'S SURVIVAL (Beat 10) ----

        private static DialogueLine[] GetCoralSurvivalLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "You survived it.", seconds = 2f },
                new DialogueLine { speaker = "Coral Vex", text = "I broke the process mid-conversion. Before it finished. There is a window, approximately forty percent through the protocol's completion, where the rewrite is far enough to begin and not far enough to be irreversible. I found it by accident. By fighting back at the wrong moment in the wrong way, and the wrong moment turned out to be the only possible right one.", seconds = 9f },
                new DialogueLine { speaker = "Coral Vex", text = "I built the Carnival because I needed somewhere the Dominion would not look, somewhere that traded in exactly what they valued, so that looking too hard at it would reveal too much about their own operation. I used the Vellum Exchange's infrastructure because they are discreet and they prize what I have. The archive here is not for commerce.", seconds = 9f },
                new DialogueLine { speaker = "Coral Vex", text = "I have a complete copy of the operative archive. Every designation ever processed through the program. Every conversion record. Not sold. Preserved.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "How many.", seconds = 1f },
                new DialogueLine { speaker = "Coral Vex", text = "Seventeen conversions on record. Of those, the archive carries full documentation on every one.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "And mine.", seconds = 1f },
                new DialogueLine { speaker = "Coral Vex", text = "There is an anomalous flag on your entry. That is why I built the carousel's extraction parameters for you specifically. I needed to see what you would do with a version of yourself at the end of its certainty, and what you would do with a version of yourself that was eight years old and couldn't stop what was being done to it.", seconds = 9f },
            };
        }

        // ---- EXTRACTION GUARDS (Beat 11) ----

        private static DialogueLine[] GetExtractionBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Extraction Guard Captain", text = "Stand down! All units hold position.", seconds = 1.5f },
            };
        }

        // ---- BLADE AT THROAT (Beat 12) ----

        private static DialogueLine[] GetBladeAtThroatLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Coral Vex", text = "The archive is the only proof of what they did. Kill me, and Khall has no reason to leave it standing, he will burn everything here the moment I'm dead, and the seventeen records become ash, and they were never real.", seconds = 7f },
                new DialogueLine { speaker = "Coral Vex", text = "I know where every converted operative is currently stored. The full operational record, conversion status, location, dormancy coordinates. I have been maintaining that record for eight cycles.", seconds = 6f },
                new DialogueLine { speaker = "Coral Vex", text = "You came here chasing one file. I have them all.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Show me the archive.", seconds = 2f },
            };
        }

        // ---- THE VAULT BELOW: DESCENT (Beat 13) ----

        private static DialogueLine[] GetVaultDescentLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Coral Vex", text = "The full archive is not on the platform. The platform is too mobile, I move it every seventy-two hours. The archive needs stable geology. Verath Station's bedrock has been hollowed into the Exchange's off-site facility. Row upon row of sealed memory vaults cut into the rock.", seconds = 8f },
                new DialogueLine { speaker = "Coral Vex", text = "Your conversion file is down there. I reviewed it when it first came through the Exchange's acquisition channels, six cycles ago, before I knew to be watching for your designation. There is an anomalous flag on the status field that I could not explain at the time and have not been able to explain since.", seconds = 9f },
                new DialogueLine { speaker = "Cipher", text = "What does the flag say.", seconds = 1.5f },
                new DialogueLine { speaker = "Coral Vex", text = "The conversion was initiated. It did not complete. The status reads: ANOMALOUS TERMINATION, CAUSE UNDETERMINED. Every other record in the archive reads either CONVERSION COMPLETE or CONVERSION FAILED, OPERATIVE DECEASED.", seconds = 7f },
                new DialogueLine { speaker = "Coral Vex", text = "You are the only exception in seventeen records.", seconds = 3f },
            };
        }

        // ---- VERATH STATION ASSAULT (Beat 14) ----

        private static DialogueLine[] GetVerathBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Coral Vex", text = "Four sentries on the entrance, three crawlers covering our approach angle. The crawlers will prioritize me, I've tripped their biometric registry before. Take the right flank. I'll draw the crawlers to the east ridgeline.", seconds = 5.5f },
                new DialogueLine { speaker = "Cipher", text = "The ridge terrain.", seconds = 1f },
                new DialogueLine { speaker = "Dominion Sentry 3", text = "Breach at the east approach, contacts entering the vault!", seconds = 2f },
                new DialogueLine { speaker = "Coral Vex", text = "We have approximately twelve minutes before the platform receives that report. Move.", seconds = 2.5f },
            };
        }

        // ---- THE VAULT TERMINAL (Beat 15) ----

        private static DialogueLine[] GetVaultTerminalLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "The conversion was started. It ran forty percent of the course. And then it stopped.", seconds = 4.5f },
                new DialogueLine { speaker = "Coral Vex", text = "Yes.", seconds = 1f },
            };
        }

        // ---- KHALL'S GAMBIT (Beat 16) ----

        private static DialogueLine[] GetKhallsGambitLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Coral Vex", text = "I've been tracking Khall's surveillance signature since you docked. Passive, long-range, a carrier vessel holding at one hundred and forty thousand kilometers, running emission discipline. Patient.", seconds = 5f },
                new DialogueLine { speaker = "Coral Vex", text = "He has not come for the archive. He came for what your visit would cause. The Carnival's stored memory vials, all of them, the full accumulation of eight cycles of preservation, carry enough concentrated neural material to serve as a cascade pulse weapon. A cascade large enough to blank identity across multiple Belt population centers simultaneously.", seconds = 9f },
                new DialogueLine { speaker = "Coral Vex", text = "The Carnival's activation core can be weaponized. Khall is inbound to weaponize it, a demonstration meant to break syndicate resistance to Dominion authority. Pacification through mass identity erasure. If he reaches the core, everything I have preserved becomes a weapon for exactly the thing I built this place to oppose.", seconds = 10f },
                new DialogueLine { speaker = "Cipher", text = "The activation core. Where.", seconds = 1.5f },
                new DialogueLine { speaker = "Coral Vex", text = "Center of the platform. Adjacent to the main vial storage. We have approximately fifteen minutes before his boarding force reaches it.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "We move.", seconds = 1f },
            };
        }

        // ---- RONIN-10 (Beat 17) ----

        private static DialogueLine[] GetRonin10Lines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-10", text = "Operative Cipher. Stand down. Return to program custody. The directive is current.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "I see you. Whatever's left of you, I see it.", seconds = 3f },
                new DialogueLine { speaker = "Coral Vex", text = "Charges set. Fifteen seconds to the window.", seconds = 2f },
            };
        }

        // ---- THE BURNING OF NAMES (Beat 18) ----

        private static DialogueLine[] GetBurningEscapeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Drones falling! Hard left, now!", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Evasion in the blank. Hold fire on the frigate.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Jump window is open, ten seconds!", seconds = 1.5f },
            };
        }

        // ---- DRIFTING: CORAL STEPS FORWARD (Beat 19) ----

        private static DialogueLine[] GetDriftingCoralLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Not the standard outcome. I wasn't sure, after three hours.", seconds = 3f },
                new DialogueLine { speaker = "Coral Vex", text = "The archive is backed up across three Vellum nodes under my cipher authority. The platform is gone. The backup is not.", seconds = 5f },
                new DialogueLine { speaker = "Coral Vex", text = "The Carnival was the box I built to survive. I am out of the box now.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "There's a berth available. Second on the left, past the engine room. It's small.", seconds = 3f },
                new DialogueLine { speaker = "Coral Vex", text = "Small is fine.", seconds = 1.5f },
            };
        }

        // ---- THE ARCHIVE REVIEWED (Beat 20) ----

        private static DialogueLine[] GetArchiveReviewedLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Coral Vex", text = "I have reviewed your record forty-three times in six cycles. I have cross-referenced the conversion protocol documentation against the technical specifications for every stage. I cannot find a mechanism that produces the forty percent termination outcome without either an external intervention or an internal failure mode that the program's engineers were not aware of.", seconds = 9f },
                new DialogueLine { speaker = "Coral Vex", text = "External intervention would require someone with administrative access to the conversion process. An internal failure mode would require something in your architecture that the program did not design and did not anticipate. Both explanations have the same problem: they require a cause, and the cause is not documented anywhere in the record.", seconds = 9f },
                new DialogueLine { speaker = "Cipher", text = "Seventeen records. Sixteen with resolution. One without.", seconds = 3.5f },
                new DialogueLine { speaker = "Coral Vex", text = "Yes.", seconds = 0.5f },
            };
        }

        // ---- FINAL DRONE (Beat 21) ----

        private static DialogueLine[] GetFinalDroneLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Clean sweep. Good.", seconds = 1.5f },
            };
        }

        // ---- THE QUESTION THAT REMAINS (Beat 22) ----

        private static DialogueLine[] GetTheQuestionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "If the wipe always succeeds...", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "why did mine fail?", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Give me coordinates when you have them.", seconds = 2.5f },
            };
        }

        // ---- GALAXY 2 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Coral's archive is backed up across three Vellum nodes. We have the records now, all seventeen.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "Sixteen resolved. One open. Mine.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "She's plotting the next node. Decommissioned Dominion station, grid RV-9.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Then that's where we find the cause. Set the coordinates.", seconds = 2.5f },
            };
        }
    }
}
