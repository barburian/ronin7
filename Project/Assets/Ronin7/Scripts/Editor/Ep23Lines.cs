using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 23 dialogue data. Transposed from ep23-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep23_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 23 "Thermopause" unfolds at a derelict Dominion cryo-outpost suspended between a dying star and a rogue ice world.
    /// Cipher pursues a signal and discovers operatives preserved in stasis — not executed, but quarantined. The "defective generation":
    /// soldiers who showed mercy, who refused the massacre on Kethel-7. Commander Vale wakes and reveals the truth: they were not erased.
    /// They were buried in ice because showing conscience was classified as a terminal flaw. Cipher wakes twelve of them. They can be woken.
    /// Thousands more wait frozen in five more installations. The Program was far deeper, far more fractured, than anyone knew.
    /// </summary>
    public static class Ep23Lines
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
            "signal",
            "drone_barks",
            "ice_below",
            "sentinel_barks",
            "ice_aftermath",
            "crypt_below",
            "cryo_barks",
            "crypt_aftermath",
            "the_commander",
            "defense_barks",
            "defective_generation",
            "defective_barks",
            "defective_aftermath",
            "the_wake",
            "boarding_barks",
            "wake_aftermath",
            "ghost_fleet",
            "ghost_barks",
            "closing",
            "space_ep23_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "signal" => GetSignalLines(),
                "drone_barks" => GetDroneBarksLines(),
                "ice_below" => GetIceBelowLines(),
                "sentinel_barks" => GetSentinelBarksLines(),
                "ice_aftermath" => GetIceAftermathLines(),
                "crypt_below" => GetCryptBelowLines(),
                "cryo_barks" => GetCryoBarksLines(),
                "crypt_aftermath" => GetCryptAftermathLines(),
                "the_commander" => GetTheCommanderLines(),
                "defense_barks" => GetDefenseBarksLines(),
                "defective_generation" => GetDefectiveGenerationLines(),
                "defective_barks" => GetDefectiveBarksLines(),
                "defective_aftermath" => GetDefectiveAftermathLines(),
                "the_wake" => GetTheWakeLines(),
                "boarding_barks" => GetBoardingBarksLines(),
                "wake_aftermath" => GetWakeAftermathLines(),
                "ghost_fleet" => GetGhostFleetLines(),
                "ghost_barks" => GetGhostBarksLines(),
                "closing" => GetClosingLines(),
                "space_ep23_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep23_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep23_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- SIGNAL ----

        private static DialogueLine[] GetSignalLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "That frequency is dead-channel. Military band. The kind of thing the Dominion only uses for listening posts and derelict installations.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "Is it broadcasting?", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "No. Just repeating. Like something left on a shelf to die slowly. Could be a graveyard. Could be bait.", seconds = 3.5f },
            };
        }

        // ---- DRONE BARKS ----

        private static DialogueLine[] GetDroneBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Empire Drone", text = "Intruder-detection-protocol-alpha-alert-alert-ALERT...", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "It's a Program asset. Or it was.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "See that? That chill you're feeling?", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "That's not cold. That's recognition.", seconds = 1.5f },
            };
        }

        // ---- ICE BELOW ----

        private static DialogueLine[] GetIceBelowLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Power relay's down there. We get it online, the station's docking mechanisms respond. We don't, we're sitting dark in the vacuum with no comms home.", seconds = 4f },
                new DialogueLine { speaker = "Iris", text = "And the Dominion doesn't leave infrastructure unguarded. Not ever.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Quick breach, in-out. You get the relay open, I keep the hauler warm. If anything moves, you move faster.", seconds = 3f },
            };
        }

        // ---- SENTINEL BARKS ----

        private static DialogueLine[] GetSentinelBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sentinel Platform 1", text = "Unauthorized biological signature detected. Initiating defensive protocols.", seconds = 1.5f },
                new DialogueLine { speaker = "Sentinel Platform 2", text = "Magnetic interference detected. Eliminating threat.", seconds = 1.5f },
            };
        }

        // ---- ICE AFTERMATH ----

        private static DialogueLine[] GetIceAftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Relay is live. Station's docking bay is opening.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Understood. Coming down to collect you.", seconds = 1.5f },
            };
        }

        // ---- CRYPT BELOW ----

        private static DialogueLine[] GetCryptBelowLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cryo-Interface", text = "Seven? Seven, is that you? Seven? Seven, is that you?", seconds = 2.5f },
            };
        }

        // ---- CRYO BARKS ----

        private static DialogueLine[] GetCryoBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cryo-Animate 1", text = "Return. Return to stasis. Return to...", seconds = 1.5f },
                new DialogueLine { speaker = "Cryo-Animate 2", text = "Wake not allowed. Sleep required. Sleep. Return to...", seconds = 1.5f },
            };
        }

        // ---- CRYPT AFTERMATH ----

        private static DialogueLine[] GetCryptAftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Station AI", text = "Cohort designations Ronin-1 through Ronin-9, third iteration. All status: Preserved. Cause of stasis: Authorized quarantine. Duration: Indefinite.", seconds = 3.5f },
                new DialogueLine { speaker = "Station AI", text = "Defective generation classification: Confirmed. Containment priority: Terminal.", seconds = 2.5f },
            };
        }

        // ---- THE COMMANDER ----

        private static DialogueLine[] GetTheCommanderLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Let them sleep, Seven. They're ghosts now. Dead in all the ways that matter.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "She needs to wake.", seconds = 1.5f },
            };
        }

        // ---- DEFENSE BARKS ----

        private static DialogueLine[] GetDefenseBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Defense Drone 3", text = "Containment breach! Quarantine protocol activated!", seconds = 1.5f },
                new DialogueLine { speaker = "Defense Drone 4", text = "Containment protocol failure! Terminating breach!", seconds = 1.5f },
                new DialogueLine { speaker = "Vale", text = "Did we complete the mission?", seconds = 2f },
                new DialogueLine { speaker = "Vale", text = "Your mark is gone.", seconds = 2f },
                new DialogueLine { speaker = "Vale", text = "What happened to you, Seven?", seconds = 2f },
            };
        }

        // ---- DEFECTIVE GENERATION ----

        private static DialogueLine[] GetDefectiveGenerationLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "You remember Kethel-7?", seconds = 2f },
                new DialogueLine { speaker = "Vale", text = "I remember the order. To cull the defective generation, the ones who had shown mercy in field operations. The children were a test. A loyalty trial. And you refused.", seconds = 4f },
                new DialogueLine { speaker = "Vale", text = "You reported it. Then you were gone.", seconds = 2f },
                new DialogueLine { speaker = "Vale", text = "They didn't erase us.", seconds = 1.5f },
                new DialogueLine { speaker = "Vale", text = "They quarantined us. Everyone who knew. Everyone who had shown the capacity to refuse. Labeled us the \"defective generation\" and sealed us in ice.", seconds = 4f },
                new DialogueLine { speaker = "Vale", text = "Not to kill us. To keep us quiet. Indefinite. Terminal containment of a terminal flaw.", seconds = 2.5f },
                new DialogueLine { speaker = "Vale", text = "We can be woken, Seven. All of us. And we remember everything they tried to erase.", seconds = 2.5f },
            };
        }

        // ---- DEFECTIVE BARKS ----

        private static DialogueLine[] GetDefectiveBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "The reactor vents?", seconds = 1f },
                new DialogueLine { speaker = "Vale", text = "Cooling system will stabilize when we vent the heat.", seconds = 1.5f },
                new DialogueLine { speaker = "Cryo-Animate 1", text = "Programming fractured. Cannot... cannot comply...", seconds = 1.5f },
            };
        }

        // ---- DEFECTIVE AFTERMATH ----

        private static DialogueLine[] GetDefectiveAftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vale", text = "The Dominion made us defective by giving us a conscience. Then decided that was too dangerous to leave walking around.", seconds = 3.5f },
            };
        }

        // ---- THE WAKE ----

        private static DialogueLine[] GetTheWakeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vale", text = "We are not going back to the Dominion. We are free, or we are nothing.", seconds = 3f },
            };
        }

        // ---- BOARDING BARKS ----

        private static DialogueLine[] GetBoardingBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Destroyer Commander", text = "All boarding teams, breach on contact! Suppress all personnel on lower decks!", seconds = 2f },
                new DialogueLine { speaker = "Iris", text = "I have the hauler docked. You're loading now. Don't argue. Move.", seconds = 2.5f },
            };
        }

        // ---- WAKE AFTERMATH ----

        private static DialogueLine[] GetWakeAftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vale", text = "You didn't choose to be erased, Seven. And you don't have to choose to stay forgotten.", seconds = 3f },
            };
        }

        // ---- GHOST FLEET ----

        private static DialogueLine[] GetGhostFleetLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vale", text = "The manifest you pulled from the relay's backup systems, I've been reading it.", seconds = 2.5f },
                new DialogueLine { speaker = "Vale", text = "Five more installations, Seven. Scattered across the outer rim. All marked with the same designation. All housing cryo-vaults. All containing preserved operatives classified as the defective generation.", seconds = 4.5f },
                new DialogueLine { speaker = "Vale", text = "Thousands of operatives. All of them quarantined. All of them waiting.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "How many iterations of the program were authorized?", seconds = 2.5f },
                new DialogueLine { speaker = "Vale", text = "More than we knew existed. More than the Dominion ever documented in channels we could access.", seconds = 3f },
            };
        }

        // ---- GHOST BARKS ----

        private static DialogueLine[] GetGhostBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Evasion pattern delta! Stay close to the debris!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Jump coordinates locked. We're clear. We made it.", seconds = 2f },
            };
        }

        // ---- CLOSING ----

        private static DialogueLine[] GetClosingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vale", text = "The defective generation. That is what they called us.", seconds = 2f },
                new DialogueLine { speaker = "Vale", text = "But how many of you are there, exactly?", seconds = 2.5f },
            };
        }

        // ---- GALAXY 3 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Twelve of them woke from cryo. They remember me. They remember what they refused. There are five more installations like the Thermopause, thousands more operatives, all classified defective, all waiting in the ice.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "The manifest shows more variants than anyone documented. More iterations of the Program. This wasn't a single cohort buried in ice. This was the whole backup generation. And the Dominion was willing to keep them frozen forever rather than let them wake.", seconds = 3.5f },
            };
        }
    }
}
