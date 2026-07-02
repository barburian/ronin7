using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 5 dialogue data. Transposed from ep05-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep05_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// </summary>
    public static class Ep05Lines
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
            "market_recognition",
            "warrens_enforcer_barks",
            "warrens_after",
            "kethel_explained",
            "lock_alert",
            "lock_hunter_barks",
            "purge_truth",
            "defective_generation",
            "deck_silencer_barks",
            "deck_wounded",
            "medbay_kessler",
            "rotunda_alert",
            "rotunda_strike_barks",
            "rotunda_choice",
            "bridge_boarders_barks",
            "bridge_others",
            "archive_reading",
            "hull_elite_barks",
            "hull_reinforcements",
            "khall_duel_open",
            "khall_duel_barks",
            "khall_duel_after",
            "spare_choice",
            "escape_threat",
            "space_ep05_approach",
            "space_ep05_fighters",
            "space_ep05_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "market_recognition" => GetMarketRecognitionLines(),
                "warrens_enforcer_barks" => GetWarrensEnforcerBarksLines(),
                "warrens_after" => GetWarrensAfterLines(),
                "kethel_explained" => GetKethelExplainedLines(),
                "lock_alert" => GetLockAlertLines(),
                "lock_hunter_barks" => GetLockHunterBarksLines(),
                "purge_truth" => GetPurgeTruthLines(),
                "defective_generation" => GetDefectiveGenerationLines(),
                "deck_silencer_barks" => GetDeckSilencerBarksLines(),
                "deck_wounded" => GetDeckWoundedLines(),
                "medbay_kessler" => GetMedbayKesslerLines(),
                "rotunda_alert" => GetRotundaAlertLines(),
                "rotunda_strike_barks" => GetRotundaStrikeBarksLines(),
                "rotunda_choice" => GetRotundaChoiceLines(),
                "bridge_boarders_barks" => GetBridgeBoardersBarksLines(),
                "bridge_others" => GetBridgeOthersLines(),
                "archive_reading" => GetArchiveReadingLines(),
                "hull_elite_barks" => GetHullEliteBarksLines(),
                "hull_reinforcements" => GetHullReinforcementsLines(),
                "khall_duel_open" => GetKhallDuelOpenLines(),
                "khall_duel_barks" => GetKhallDuelBarksLines(),
                "khall_duel_after" => GetKhallDuelAfterLines(),
                "spare_choice" => GetSpareChoiceLines(),
                "escape_threat" => GetEscapeThreatLines(),
                "space_ep05_approach" => GetSpaceEp05ApproachLines(),
                "space_ep05_fighters" => GetSpaceEp05FightersLines(),
                "space_ep05_post" => GetSpaceEp05PostLines(),
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

        /// <summary>Generate clip name for a line: ep05_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep05_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- RUST COLLECTIVE: MARKET TIER (Beat 2) ----

        private static DialogueLine[] GetMarketRecognitionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-9", text = "I know that scar.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-9", text = "Behind your ear. The neural anchor. I was supposed to get one like it, before they decided I was defective.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "Who are you?", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-9", text = "Ronin-9. Fifth-ranked in our cohort. And you don't remember me. That's the whole design, isn't it.", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-7", text = "The Rust Collective isn't safe for this conversation.", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-9", text = "No place is. But the cargo warrens have corridors Khall's reach hasn't mapped yet. Come on.", seconds = 3f },
            };
        }

        // ---- CARGO WARRENS: ENFORCER FIGHT (Beat 3) ----

        private static DialogueLine[] GetWarrensEnforcerBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Enforcer 1", text = "Hold it! Station restricted zone!", seconds = 1.5f },
                new DialogueLine { speaker = "Enforcer 2", text = "You refugees don't run this deep, understand?", seconds = 2f },
                new DialogueLine { speaker = "Enforcer 3", text = "What the!", seconds = 0.5f },
            };
        }

        // ---- CARGO WARRENS: POST-FIGHT (Beat 4) ----

        private static DialogueLine[] GetWarrensAfterLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-9", text = "You still move like him.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-9", text = "We trained together for seventeen years. Same cohort. Same handlers. Your real name, the conditioning locked it away from me. But I remember your face. I was watching the day they chose you for Kethel-7.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "Kethel-7.", seconds = 1f },
                new DialogueLine { speaker = "Ronin-9", text = "Come on. We need to move. And I need to tell you what they erased.", seconds = 3f },
            };
        }

        // ---- KETHEL-7 EXPLANATION (Beat 5) ----

        private static DialogueLine[] GetKethelExplainedLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-9", text = "Kethel-7 is a planet. A welfare orphanage, off the beaten routes. It's where they sent you to do the unthinkable.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-9", text = "Overseer Khall's order named a Hollow Kings cell hiding inside, sheltering among the residents. Infiltrate. Confirm. Eliminate. Clean work.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-9", text = "There was no cell. Just children. And you were the finest weapon the program ever forged.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-9", text = "You carried it out. Every one of them. That's the part you don't get to forget for free.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "I don't remember.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-9", text = "Then let me. Because what you did after is why you're standing here, and why I'm the only one left who'll say it to your face.", seconds = 4f },
            };
        }

        // ---- PRESSURE-LOCK CHASE: STATION ALERT (Beat 6) ----

        private static DialogueLine[] GetLockAlertLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Station Comms", text = "Alert. Hunter-team deployed to Pressure-Lock District.", seconds = 3f },
            };
        }

        // ---- PRESSURE-LOCK CHASE: HUNTER FIGHT (Beat 6) ----

        private static DialogueLine[] GetLockHunterBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Hunter 1", text = "Target's venting atmo. Sealing secondary locks!", seconds = 2f },
                new DialogueLine { speaker = "Ronin-9", text = "Access code Delta-Nine. Killing reinforcement lock on your left. You've got thirty seconds before the magnetic containment resets.", seconds = 3f },
                new DialogueLine { speaker = "Hunter 2", text = "Command, I've lost...", seconds = 1f },
            };
        }

        // ---- THE PURGE (Beat 7) ----

        private static DialogueLine[] GetPurgeTruthLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "What Khall did to me.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-9", text = "You walked out of that facility and something came apart. You'd killed before, but never children, never the helpless. The conditioning held right up until it didn't.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-9", text = "You broke. First operative in the program's history to break all the way through, to turn on them instead of dying on command. And then you ran.", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-7", text = "And Khall?", seconds = 1f },
                new DialogueLine { speaker = "Ronin-9", text = "Every operative carries a killswitch, a kill order wired into the skull, fired from the Beacon. Khall sent yours. It was meant to stop your heart.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-9", text = "It didn't. It misfired, burned through your memory instead of your life. They dumped you in the dark for dead. You survived your own execution. That's why Kethel-7 is gone from you, and why you're still breathing.", seconds = 5f },
            };
        }

        // ---- THE DEFECTIVE GENERATION (Beat 8) ----

        private static DialogueLine[] GetDefectiveGenerationLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-9", text = "There were seven of us. Seven operatives in our cohort who showed anomalies.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-9", text = "Ranked fifth, I was. Stripped of augmentations one piece at a time for asking the wrong questions about the names we were forbidden to remember.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-9", text = "Left on the Collective to die slowly. Knowing too much to be killed cleanly.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-9", text = "Khall called us the defective generation. Seven who cracked in one cycle, asking questions, hesitating, remembering names we weren't allowed to keep. But cracking just gets you stripped and dumped here, like me. You did what no operative ever had, you broke clean through and turned on them. The rest of us they could afford to throw away. You, they had to put down.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "Where are the others?", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-9", text = "Some in the vaults, sleeping. Some here on the Collective, hiding. Some...", seconds = 2f },
                new DialogueLine { speaker = "Ronin-9", text = "being hunted by Dominion silencers.", seconds = 2f },
            };
        }

        // ---- OBSERVATION DECK: SILENCER FIGHT (Beat 9) ----

        private static DialogueLine[] GetDeckSilencerBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Silencer 1", text = "Ronin-9 identified. Authorizing termination.", seconds = 2f },
                new DialogueLine { speaker = "Silencer 2", text = "Ronin-7. Stand down. You are not the target.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-9", text = "Your two! Collapse the walkway, magnetic lock is failing!", seconds = 2.5f },
            };
        }

        // ---- OBSERVATION DECK: WOUNDED (Beat 10) ----

        private static DialogueLine[] GetDeckWoundedLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-9", text = "There are others still hiding in the Collective. And Khall is moving personally.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-9", text = "He wants you brought in alive, he wants to study why the programming failed.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "We need to reach Kessler.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-9", text = "Yes. We do.", seconds = 1.5f },
            };
        }

        // ---- MED-BAY: KESSLER ARRIVES (Beat 11) ----

        private static DialogueLine[] GetMedbayKesslerLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Either of you want to tell me what just happened out there?", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-7", text = "I have a past I don't remember. She's part of it.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "I thought you were a casualty of war. I didn't know you were a weapon that had learned to break.", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-9", text = "He is not broken. The programming is. He is proof it isn't absolute, that something in a person can survive seventeen years of conditioning and still choose.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-9", text = "That is what Khall cannot accept.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "Or is whatever broke in me just another layer of what they built?", seconds = 3f },
            };
        }

        // ---- CENTRAL ROTUNDA: STRIKE TEAM (Beat 12) ----

        private static DialogueLine[] GetRotundaAlertLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "Ronin-7. Stand down. A defective operative has fed you fabricated memories.", seconds = 3.5f },
            };
        }

        private static DialogueLine[] GetRotundaStrikeBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Officer", text = "All units converge on the reactor core!", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Suppression systems are down! You've got maybe forty seconds before they cycle back online!", seconds = 2.5f },
                new DialogueLine { speaker = "Khall", text = "Surrender, and I will restore what the misfire cost you. Memories. Identity. A place in the Program that was always yours.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-9", text = "He is lying. You were never broken. You were just finally awake.", seconds = 3f },
            };
        }

        // ---- ROTUNDA: THE CHOICE (Beat 13) ----

        private static DialogueLine[] GetRotundaChoiceLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-9", text = "You think they gave you the capacity to choose. They did not.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-9", text = "Choice is not something the Dominion installs. It surfaced on Kethel-7 when the killing was done and you finally saw what your hands had done.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-9", text = "It broke you, and breaking was the most human thing you ever did. Khall thinks he can condition that out. But the part of you that cracked, that is the part the killswitch could not reach.", seconds = 5f },
            };
        }

        // ---- SPACE COMBAT: BOARDERS (Beat 14) ----

        private static DialogueLine[] GetBridgeBoardersBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Boarder 1", text = "Bridge breached! Suppress the reactor access!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Divert auxiliary power to starboard seals! Buy him time!", seconds = 2f },
                new DialogueLine { speaker = "Dominion Boarder 2", text = "Contact lost. Reactor breach imminent. All units evacuate...", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Report!", seconds = 1f },
                new DialogueLine { speaker = "Ronin-7", text = "The bridge is secure. Reactors stable.", seconds = 2f },
            };
        }

        // ---- BRIDGE: OTHERS ARRIVE (Beat 15) ----

        private static DialogueLine[] GetBridgeOthersLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-9", text = "The others heard. They are coming.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-9", text = "The defective generation is gathering.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Khall will know. He'll move faster.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we don't wait. We move first.", seconds = 2f },
            };
        }

        // ---- ARCHIVE: READING THE FILES (Beat 16) ----

        private static DialogueLine[] GetArchiveReadingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "Subject Ronin-7, elimination order executed in full. Post-action: catastrophic conditioning failure. Subject defected.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "First recorded conscience-break in program history. Recommended: execute killswitch, archive behavioral data, reassess conditioning parameters.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "Kethel-7 residents, civilian roster. Seventy-three children. Ages four through sixteen.", seconds = 3f },
            };
        }

        // ---- OUTER HULL: KHALL'S ELITE (Beat 17) ----

        private static DialogueLine[] GetHullEliteBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Elite Operative 1", text = "Ronin-7 acquired. Preparing extraction.", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Ronin-9. Can you collapse the structural supports on my signal?", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-9", text = "Already prepped. You've got three seconds from mark.", seconds = 2f },
                new DialogueLine { speaker = "Elite Operative 2", text = "Mission failure. Operatives lost...", seconds = 1.5f },
            };
        }

        // ---- HULL: REINFORCEMENTS DOCKING (Beat 18) ----

        private static DialogueLine[] GetHullReinforcementsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-9", text = "The others are all aboard. Both vessels secure.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "He's coming for you personally. Khall. I've got a trace on his flagship entering the approach corridor.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-9", text = "Then we need to be ready.", seconds = 1.5f },
            };
        }

        // ---- COMMAND HUB: KHALL DUEL (Beat 19) ----

        private static DialogueLine[] GetKhallDuelOpenLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "Seventeen years. Every generation before yours, the conditioning held without exception. Then your cohort. Seven of you, cracked in a single cycle.", seconds = 4f },
                new DialogueLine { speaker = "Khall", text = "I gave you Kethel-7 because you were my finest. No hesitation, nothing flagged in seventeen years. You walked in and did exactly as ordered.", seconds = 5f },
            };
        }

        private static DialogueLine[] GetKhallDuelBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "And then you broke. First one ever to walk out and turn on us. So I fired your killswitch. I believed it would end you cleanly. I was wrong.", seconds = 4f },
            };
        }

        private static DialogueLine[] GetKhallDuelAfterLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "I can restore what was taken. Complete memories. A full self. You will simply owe the Dominion one more debt.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "Or I stay as I am.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "That is what they all choose. It is why I always have to start again.", seconds = 3f },
            };
        }

        // ---- ROTUNDA: THE CHOICE TO SPARE (Beat 20) ----

        private static DialogueLine[] GetSpareChoiceLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-9", text = "Don't.", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-9", text = "If you kill him now you become what they built you to be. If you let him go, you become what you chose to be.", seconds = 4f },
            };
        }

        // ---- ESCAPE AND THREAT (Beat 21) ----

        private static DialogueLine[] GetEscapeThreatLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "You have bought yourself a grave, Ronin-7. The Dominion does not leave its broken tools to rust in peace.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-9", text = "He was telling the truth, you know. About that last part.", seconds = 3f },
            };
        }

        // ---- SPACE APPROACH & BRIEFING (Authored) ----

        private static DialogueLine[] GetSpaceEp05ApproachLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "The Rust Collective orbits a dying neutron star. Salvagers, refugees, Dominion discards looking for oblivion. It's where broken things go to disappear.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "You think the others are there?", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "The signal Katana found came from somewhere in those corridors. We dock, we trade salvage for information, we find them before Khall does.", seconds = 4f },
            };
        }

        // ---- SPACE COMBAT: FIGHTERS (Authored) ----

        private static DialogueLine[] GetSpaceEp05FightersLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Fighter 1", text = "Picket line to all wings: the salvager does not reach the Collective.", seconds = 2f },
                new DialogueLine { speaker = "Dominion Fighter 2", text = "All units weapons hot. Engage on intercept.", seconds = 2f },
            };
        }

        // ---- POST-EPISODE BRIEFING (bridge to EP06) ----

        private static DialogueLine[] GetSpaceEp05PostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Khall escaped. But he's just the blade. Someone above him sanctioned Kethel-7. Someone still walking free in Dominion Command.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-9", text = "And the others who answered, they're aboard. The rest of the defective generation, scattered across the fringe, are starting to hear what happened here.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we move toward them. We don't wait for Khall to find us.", seconds = 3f },
            };
        }
    }
}
