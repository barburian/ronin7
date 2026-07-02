using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 15 dialogue data. Transposed from ep15-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep15_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 15 spans Khall's hidden confession, the spreading of the truth through dead relays,
    /// and the final confrontation in the desert where the operative learns that the Overseer
    /// engineered his own downfall to crack the Program from within.
    /// </summary>
    public static class Ep15Lines
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
            "relay_approach",
            "relay_barks",
            "signal_confession",
            "ledger",
            "maw_barks",
            "mirs_intro",
            "emberhand_barks",
            "sulfur_confession",
            "water_approach",
            "khall_confession",
            "pale_choir_barks",
            "sunken_understanding",
            "desert_khall",
            "desert_barks",
            "desert_aftermath",
            "relay_reprogram",
            "dreadnought_barks",
            "cockpit_final",
            "space_ep15_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "transit_intro" => GetTransitIntroLines(),
                "relay_approach" => GetRelayApproachLines(),
                "relay_barks" => GetRelayBarksLines(),
                "signal_confession" => GetSignalConfessionLines(),
                "ledger" => GetLedgerLines(),
                "maw_barks" => GetMawBarksLines(),
                "mirs_intro" => GetMirsIntroLines(),
                "emberhand_barks" => GetEmberhandBarksLines(),
                "sulfur_confession" => GetSulfurConfessionLines(),
                "water_approach" => GetWaterApproachLines(),
                "khall_confession" => GetKhallConfessionLines(),
                "pale_choir_barks" => GetPaleChoirBarksLines(),
                "sunken_understanding" => GetSunkenUnderstandingLines(),
                "desert_khall" => GetDesertKhallLines(),
                "desert_barks" => GetDesertBarksLines(),
                "desert_aftermath" => GetDesertAftermathLines(),
                "relay_reprogram" => GetRelayReprogramLines(),
                "dreadnought_barks" => GetDreadnoughtBarksLines(),
                "cockpit_final" => GetCockpitFinalLines(),
                "space_ep15_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep15_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep15_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- THE DEAD FREQUENCY: SIGNAL RECOGNITION (Beat 1) ----

        private static DialogueLine[] GetTransitIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "There's a repeating pattern in the channel noise. Automated pulse. Someone's still broadcasting on the old Dominion bands.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Dominion bands are mostly silent out here. Too much space between listening posts. Too many years of neglect.", seconds = 3.5f },
                new DialogueLine { speaker = "Automated Beacon", text = "Ronin-7. Ronin-7. Report. Ronin-7. Report.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "That's a direct designation call. That's a summons.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "It's not a summons. It's an echo. Something dead calling to something that refused to die.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Set course for the signal source. We're going to answer.", seconds = 2f },
            };
        }

        // ---- RELAY-9 APPROACH: THE TOMB STATION (Beat 2) ----

        private static DialogueLine[] GetRelayApproachLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Station's been dark for years. Emergency lighting on maintenance cycles. Someone's still keeping the power running.", seconds = 3.5f },
                new DialogueLine { speaker = "Coral Vex", text = "The relay designation predates current Dominion protocols by twelve years. It's older than the unified command structure.", seconds = 3.5f },
                new DialogueLine { speaker = "Morrigan", text = "A personal listening post. Maintained in secret. Someone did not want this station catalogued in any official system.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Morrigan, Coral, hold the hauler. I'm going in.", seconds = 2f },
            };
        }

        // ---- RELAY SHAFTS: AUTOMATED DEFENSE (Beat 3) ----

        private static DialogueLine[] GetRelayBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Automated Defense System", text = "ALERT. Unauthorized access to restricted maintenance array. Intruder suppression protocol activated. Engage defensive measures.", seconds = 4f },
            };
        }

        // ---- THE SIGNAL ROOM: THE CONFESSION BEGINS (Beat 4) ----

        private static DialogueLine[] GetSignalConfessionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "If Ronin-7 ever wakes, if you're hearing this transmission, tell him the children he freed are still alive.", seconds = 4f },
                new DialogueLine { speaker = "Khall", text = "I couldn't pull the trigger. I tried to order it, a kill-order, standard protocol, the erasure of witnesses. I gave the command, and then I...", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "Cipher? What is that?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "The confession I was always going to find.", seconds = 2f },
            };
        }

        // ---- THE LEDGER: OPERATIONAL RECORDS SURFACE (Beat 5) ----

        private static DialogueLine[] GetLedgerLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "I've got the core unpacked. Operational manifests, all indexed by date. Sector Desolat-Omega, five years back. One entry surfaces here, \"Operative-7 deployment.\"", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "Target designation: cybernetics breeding site. The fragment notes read, \"subject reported contradiction between target identity and mission brief.\" Escalation to Khall. Khall authorized. Operative executed contingency.", seconds = 6f },
                new DialogueLine { speaker = "Cipher", text = "They cleared me to do something I didn't want to do.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Khall overrode your hesitation himself. Put it in writing. Made it command authority.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "I'm not looking for my past anymore, Kessler. I'm looking for proof of a lie I already know is true.", seconds = 3f },
            };
        }

        // ---- RELAY DEFENSE: GILDED MAW CORVETTE (Beat 6) ----

        private static DialogueLine[] GetMawBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Gilded Maw Operative", text = "Ronin-7. You're still alive. The Dominion said you were purged. They said you were gone.", seconds = 3.5f },
                new DialogueLine { speaker = "Gilded Maw Operative", text = "You're chasing a ghost relay in a dead sector. The Dominion doesn't remember your name, you're a loose wire they flicked.", seconds = 4f },
                new DialogueLine { speaker = "Gilded Maw Operative", text = "But the Pale Choir heard it. The Vellum Exchange heard it. The Hollow Kings heard it. You broke, and every faction wants to know what's rattling around in your skull.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "Cipher, I'm reading a second contact in the system. Deeper in. Moving toward the third world.", seconds = 3f },
            };
        }

        // ---- THE SULFUR THRONE: MIRS THE SMUGGLER (Beat 7) ----

        private static DialogueLine[] GetMirsIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mirs", text = "Relay-9 was a personal listening post. Not in any official registry. Someone's private insurance policy against the rest of the Program.", seconds = 4f },
                new DialogueLine { speaker = "Mirs", text = "My sources say the Overseer who maintained it was Khall. He kept records of the children who survived what you were ordered to do. Whether that makes him good or just guilty, I leave to you.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "He triggered my wipe after Kethel-7. And then he tried to take it back.", seconds = 2.5f },
                new DialogueLine { speaker = "Mirs", text = "He petitioned the Program's central authority for a reversal. They refused him. And then he went silent, leaving breadcrumbs through dead relays, hoping someone strong enough to follow them eventually would wake up and hear the truth.", seconds = 5f },
            };
        }

        // ---- SULFUR DEFENSE: EMBERHAND ENFORCERS (Beat 8) ----

        private static DialogueLine[] GetEmberhandBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Emberhand Enforcer Lead", text = "Intruders in the warbound sector! Engage containment protocols!", seconds = 2f },
            };
        }

        // ---- THE SULFUR CONFESSION: KHALL'S GAMBIT REVEALED (Beat 9) ----

        private static DialogueLine[] GetSulfurConfessionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mirs", text = "Khall did something that should have destroyed him. After he triggered your purge, he went to the Program's central authority and asked for an exception, a reversal.", seconds = 4f },
                new DialogueLine { speaker = "Mirs", text = "They said no. They said your failure had to be complete. They said the program cannot tolerate mercy.", seconds = 4f },
                new DialogueLine { speaker = "Mirs", text = "He couldn't stop them. So he started keeping records. Dead channels. Personal relays. A path for you to follow when you woke up.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "He engineered his own confession.", seconds = 2f },
            };
        }

        // ---- THE WATER MOON APPROACH: ANCIENT RUINS (Beat 10) ----

        private static DialogueLine[] GetWaterApproachLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "There's a full archive buffer. All of it recorded. All of it waiting.", seconds = 3f },
            };
        }

        // ---- THE SUNKEN ARCHIVES: KHALL'S FULL CONFESSION (Beat 11) ----

        private static DialogueLine[] GetKhallConfessionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "The Ronin program is the Dominion's greatest lie. I have spent my career administering it. I have sent operative candidates to their deaths because they knew too much. I have reprogrammed operatives to forget their own names. And I have justified every single choice as necessity.", seconds = 8f },
                new DialogueLine { speaker = "Khall", text = "Ronin-7 was different. He refused the kill-order at Kethel-7. He broke the chain I had spent years hoping someone would break.", seconds = 5f },
                new DialogueLine { speaker = "Khall", text = "I chose him because he showed signs of conscience. I am sorry it took a massacre of innocents to prove me right.", seconds = 4f },
                new DialogueLine { speaker = "Khall", text = "If you are hearing this, I have already tried and failed to undo what I set in motion. The truth is louder than I can silence. I am letting it bleed.", seconds = 4f },
                new DialogueLine { speaker = "Khall", text = "I am recording this knowing it will be destroyed. I am recording this knowing it will be found. Both of those things are true. And I choose to record it anyway.", seconds = 4f },
            };
        }

        // ---- SUNKEN DEFENSE: PALE CHOIR ASSASSINS (Beat 12) ----

        private static DialogueLine[] GetPaleChoirBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Pale Choir Assassin Lead", text = "The archive must remain silent. Your presence here is an anomaly we are authorized to correct.", seconds = 3f },
            };
        }

        // ---- SUNKEN UNDERSTANDING: THE PROGRAM IS CRACKING (Beat 13) ----

        private static DialogueLine[] GetSunkenUnderstandingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "The Choir is burning serious resources to stop you from hearing that confession. That means it matters. That means it's real. That means Khall was right, the truth is louder than they can silence.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "Khall was not just an Overseer covering his tracks. He deliberately let his guilt broadcast outward into the void, betting someone would find it.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "The Program is cracking from within. The truth is becoming un-buryable.", seconds = 3f },
            };
        }

        // ---- THE DESERT RECKONING: KHALL WAITING (Beat 14) ----

        private static DialogueLine[] GetDesertKhallLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "I engineered your resistance, Operative-7. Not consciously, I chose you because you showed signs of breaking.", seconds = 5f },
                new DialogueLine { speaker = "Khall", text = "When the massacre happened and you refused the kill-order, I knew the contradiction had held. The Program's logic requires perfect compliance. You proved it is imperfect.", seconds = 6f },
                new DialogueLine { speaker = "Cipher", text = "Why did you let the confessions bleed through dead channels instead of coming forward?", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "Because what I know would undo them. And they know I know it.", seconds = 3f },
            };
        }

        // ---- DESERT CONVERGENCE: THREE-WAY BATTLE (Beat 15) ----

        private static DialogueLine[] GetDesertBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Squad Lead", text = "All teams converge! Overseer is mobile! Containing spread!", seconds = 3f },
                new DialogueLine { speaker = "Syndicate Hunter Lead", text = "Ronin-7 is in-theater! Full suppression! Don't let him reach the Overseer!", seconds = 2.5f },
            };
        }

        // ---- DESERT RECKONING AFTERMATH: THE TRUTH IS LOOSE (Beat 16) ----

        private static DialogueLine[] GetDesertAftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "He's gone. We lost him.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "He's not gone. He's loose. And the confession is loose with him.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Khall is alive. The confession is bleeding across the void. The Program has a crack running through it that no one can plug.", seconds = 4f },
            };
        }

        // ---- THE UNBROADCAST: RELAY REPROGRAMMING (Beat 17) ----

        private static DialogueLine[] GetRelayReprogramLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "The Dominion will destroy this station the moment they detect the signal. It will be their first priority.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Then it needs to be loud before it dies.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "The Dominion will come. The Pale Choir will come. Every faction that hears this frequency will converge on this point.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "Then we buy it time.", seconds = 2f },
            };
        }

        // ---- THE DREADNOUGHT: DESPERATE DOGFIGHT (Beat 18) ----

        private static DialogueLine[] GetDreadnoughtBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dreadnought Commander", text = "All point-defense, converge on the fighter! It's broadcasting the whole network! Destroy the relay!", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Relay-9 is dark. The signal is scattered. The Dominion can't stop what's already moving.", seconds = 3f },
            };
        }

        // ---- THE COCKPIT: THE SILENCE AFTER (Beat 19 — FINAL) ----

        private static DialogueLine[] GetCockpitFinalLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "I've been moving through space for months, following dead channels and broken beacons.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "I've heard Khall's confession. I've watched the Dominion burn a relay to erase the truth. I've seen the machine finally crack.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "And now I'm sitting in a stolen ship, alone in the dark, looking at my hands and asking a question.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Is there any life after being a weapon?", seconds = 3f },
            };
        }

        // ---- GALAXY 2 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "The confession keeps spreading. They can't stop what Khall set in motion.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "He opened a door. The only question now is who walks through it.", seconds = 3.5f },
            };
        }
    }
}
