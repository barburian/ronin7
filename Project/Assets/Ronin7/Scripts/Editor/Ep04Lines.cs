using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 4 dialogue data. Transposed from ep04-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep04_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// </summary>
    public static class Ep04Lines
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
            "jungle_vigil",
            "jungle_fight_barks",
            "jungle_confession",
            "space_ep04_approach",
            "space_ep04_drones",
            "archive_ghost",
            "archive_sentry_barks",
            "archive_welcome",
            "heart_khall_log",
            "heart_blade_truth",
            "heart_enforcer_challenge",
            "heart_enforcer_after",
            "heart_files",
            "heart_alarm",
            "heart_escape",
            "ledger_count",
            "ledger_revelation",
            "space_ep04_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "jungle_vigil" => GetJungleVigilLines(),
                "jungle_fight_barks" => GetJungleFightBarksLines(),
                "jungle_confession" => GetJungleConfessionLines(),
                "space_ep04_approach" => GetSpaceEp04ApproachLines(),
                "space_ep04_drones" => GetSpaceEp04DronesLines(),
                "archive_ghost" => GetArchiveGhostLines(),
                "archive_sentry_barks" => GetArchiveSentryBarksLines(),
                "archive_welcome" => GetArchiveWelcomeLines(),
                "heart_khall_log" => GetHeartKhallLogLines(),
                "heart_blade_truth" => GetHeartBladeTruthLines(),
                "heart_enforcer_challenge" => GetHeartEnforcerChallengeLines(),
                "heart_enforcer_after" => GetHeartEnforcerAfterLines(),
                "heart_files" => GetHeartFilesLines(),
                "heart_alarm" => GetHeartAlarmLines(),
                "heart_escape" => GetHeartEscapeLines(),
                "ledger_count" => GetLedgerCountLines(),
                "ledger_revelation" => GetLedgerRevelationLines(),
                "space_ep04_post" => GetSpaceEp04PostLines(),
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

        /// <summary>Generate clip name for a line: ep04_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep04_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- JUNGLE MOON DIALOGUE (Beats 1-4) ----

        private static DialogueLine[] GetJungleVigilLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "She screamed. You couldn't stop it.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "What are you talking about?", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "The blade is speaking.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "That weapon has been resonating ever since I pulled you from the void.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Some blades carry combat records. Yours carries something else. I think... I think it's carrying you.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "How many?", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "I don't know yet. But it's giving them back faster than I expected. Whatever the killswitch did, it didn't finish you, it locked you. We need to move.", seconds = 4.5f },
            };
        }

        private static DialogueLine[] GetJungleFightBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Rustfang Scout 1", text = "Contact. Derelict Corsair-class. No transponder. Claim it.", seconds = 2f },
                new DialogueLine { speaker = "Rustfang Scout 2", text = "Breach the ramp! Move!", seconds = 1.5f },
                new DialogueLine { speaker = "Katana", text = "High guard. Third taught you this.", seconds = 1.5f },
                new DialogueLine { speaker = "Rustfang Scout 3", text = "Operative! Operative!", seconds = 1f },
                new DialogueLine { speaker = "Rustfang Scout 4", text = "Fall back! Signal the...", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetJungleConfessionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "I wasn't always a salvager.", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-7", text = "I know.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "I was Dominion. An operative, same as you, different generation, no blade.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Six years ago a raid went bad. I was pinned, written off, left to bleed. One of you was on that line, you could have stepped over me. You pulled me out instead.", seconds = 5.5f },
                new DialogueLine { speaker = "Kessler", text = "I deserted within the month. Couldn't wear the uniform after a man I was sent to kill saved my life. Been drifting the fringe as a salvager ever since.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "The blade. How is this possible?", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Your blade didn't come with the ship. It came bonded to you, neural-locked to your augmented nervous system before they wiped you.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "I think when the killswitch misfired, instead of erasing your memories it locked them into the only thing that could outlast the chip. The steel itself.", seconds = 4.5f },
                new DialogueLine { speaker = "Katana", text = "He is correct. We are a ledger written in carbon and iron.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Then we use it. We go to the Archive. We get the rest before Khall's people find you.", seconds = 3f },
            };
        }

        // ---- SPACE APPROACH & COMBAT (Beats 5-6) ----

        private static DialogueLine[] GetSpaceEp04ApproachLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Corona Archive shows on old salvage maps. Believed decommissioned twelve years ago. If there are files left, they'll still be registered to your biometrics.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Whatever they made you, the record of it is in there. We get it before Khall's people do.", seconds = 3.5f },
            };
        }

        private static DialogueLine[] GetSpaceEp04DronesLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Drone 1", text = "Target locked. Evasion countermeasures active.", seconds = 2f },
                new DialogueLine { speaker = "Katana", text = "Precision, not aggression. You learned this form in silence.", seconds = 2.5f },
                new DialogueLine { speaker = "Drone 2", text = "First unit down. Engaging alternate vector. Firing...", seconds = 2f },
                new DialogueLine { speaker = "Drone 3", text = "Disengaging to beacon deploy...", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Beacon fired before it died. We have company coming. How long to Archive?", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "Seventeen minutes at current vector.", seconds = 2f },
            };
        }

        // ---- ARCHIVE DIALOGUE (Beats 7-14) ----

        private static DialogueLine[] GetArchiveGhostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Docking sequence initiated. Archive is cold. No active defenses detected, but the station knows you're here.", seconds = 3.5f },
                new DialogueLine { speaker = "Archive Ghost Signal", text = "Welcome, Operative Ronin-7. Your reactivation has been logged.", seconds = 3f },
                new DialogueLine { speaker = "Archive Ghost Signal", text = "Your biometrics match file RONIN-SEVENTH-GENERATION. Clearance granted to archives one through nine.", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-7", text = "It still thinks I'm coming home.", seconds = 2.5f },
                new DialogueLine { speaker = "Katana", text = "They always intended to call you back. The erasure was meant to be temporary, a reset. Had the killswitch done its job, you would have activated, returned here, and been remade. It didn't. You walked away with the gaps instead.", seconds = 6f },
                new DialogueLine { speaker = "Kessler", text = "They expect you to rebuild yourself for whatever comes next.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "We can recover everything, every file, every record. Or we can burn the Archive down and let the ghosts sleep.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "That answers that.", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetArchiveSentryBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sentry 1", text = "Intruder detected. Detainment protocol engaged.", seconds = 2f },
                new DialogueLine { speaker = "Katana", text = "Pivot on the wave-pressure. The cadence was always three-beat. You were always graceful in the void.", seconds = 3.5f },
            };
        }

        private static DialogueLine[] GetArchiveWelcomeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sentry 1", text = "Welcome home, brother.", seconds = 2f },
                new DialogueLine { speaker = "Sentry 2", text = "Welcome home, brother.", seconds = 2f },
            };
        }

        // ---- HEART CHAMBER DIALOGUE (Beats 10-12) ----

        private static DialogueLine[] GetHeartKhallLogLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "If you're watching this, the reset failed.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "Strength that questions is weakness. The orphanage was a lesson. Mercy and duty are incompatible.", seconds = 4f },
                new DialogueLine { speaker = "Khall", text = "You were tested. You were broken. Now we remake you.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetHeartBladeTruthLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Katana", text = "He lied. The orphanage was not a lesson.", seconds = 2.5f },
                new DialogueLine { speaker = "Katana", text = "What you did that night is why he triggered the killswitch. The truth of it is buried deeper than he wants you to dig.", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-7", text = "What did I do?", seconds = 1.5f },
                new DialogueLine { speaker = "Katana", text = "Not yet. The memory will tear you open if it arrives before you can hold it. Find the ones who survived that night. Let them tell you what they saw.", seconds = 4f },
            };
        }

        private static DialogueLine[] GetHeartEnforcerChallengeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Enforcer", text = "Operative Ronin-7. You are classified as escaped asset. Return to Dominion custody.", seconds = 3f },
                new DialogueLine { speaker = "Katana", text = "He leads with the high parry. You taught him that. Use it.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetHeartEnforcerAfterLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Enforcer", text = "I'm sorry, brother. I didn't have a Kessler.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetHeartFilesLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Archive Terminal", text = "File load successful. Displaying intake record: OPERATIONAL CHILD SPECIMEN, COHORT RONIN-SEVENTH.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "What is my name?", seconds = 1.5f },
                new DialogueLine { speaker = "Katana", text = "Not yet. You are not ready. The gaps are still raw, pull too hard and they tear.", seconds = 3f },
                new DialogueLine { speaker = "Katana", text = "When the chip is silenced, I will show you everything. Every memory. Every name the Dominion tried to burn away.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "The alarms are triggering now. He knows you're here.", seconds = 2.5f },
            };
        }

        // ---- HEART ESCAPE DIALOGUE (Beats 13-14) ----

        private static DialogueLine[] GetHeartAlarmLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "All operatives are compelled to return to Dominion custody.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "Non-compliance triggers involuntary termination. Ronin-7, your time ends now.", seconds = 3.5f },
                new DialogueLine { speaker = "Katana", text = "He commands a battleship at the Reaches' edge. He will burn this station and chase you across every corridor between stars.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "We go. Now.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "We find the others who survived. We make this harder for him.", seconds = 3f },
            };
        }

        private static DialogueLine[] GetHeartEscapeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Interceptor 1", text = "Corsair departing. Weapons free. Converge.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Starboard thruster degrading! Compensate!", seconds = 2.5f },
                new DialogueLine { speaker = "Katana", text = "The star remembers you. It always has.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "We're clear. Jumping now.", seconds = 2f },
            };
        }

        // ---- LEDGER DIALOGUE (Beats 15-16) ----

        private static DialogueLine[] GetLedgerCountLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "I pulled everything before we fled. Look.", seconds = 2f },
                new DialogueLine { speaker = "Katana", text = "Count them.", seconds = 1f },
                new DialogueLine { speaker = "Ronin-7", text = "How many of us?", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "You were never a program. You were a harvest.", seconds = 3f },
                new DialogueLine { speaker = "Katana", text = "Every numbered child is still in here. Every one who broke. Every moment they tried to make you forget. We carry them all.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "How long has this been happening?", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Longer than either of us has been alive.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetLedgerRevelationLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Katana", text = "There. The roster entry.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "We're not unique.", seconds = 2f },
                new DialogueLine { speaker = "Katana", text = "No. But you broke. They ordered the unthinkable, and you carried it out, and then something in you refused to keep being their weapon. That break is the proof the system can fail.", seconds = 6f },
                new DialogueLine { speaker = "Katana", text = "Find the others. The flagged ones. The ones still in the vaults. The ones still being processed. That is why I remember.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "Then we go. We find them.", seconds = 2f },
            };
        }

        // ---- POST-EPISODE COCKPIT BRIEFING (bridge to EP05) ----

        private static DialogueLine[] GetSpaceEp04PostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "The ledger's flagged entries are real. Anomalous, unresolved, operatives who broke conditioning and lived. Like you.", seconds = 4f },
                new DialogueLine { speaker = "Katana", text = "One signal repeats in the deep registers. A woman's cadence. Ninth generation.", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we follow it. We find her before Khall does.", seconds = 2.5f },
            };
        }
    }
}
