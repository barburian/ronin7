using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 30 dialogue data. Transposed from ep30-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep30_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 30 "The Vault Within" — the Iron Sepulcher, a Dominion black-archive station.
    /// Cipher and Mera Voss (returning from EP08) breach the Sepulcher to steal his sealed
    /// file (SUBJECT 7). He deletes the killswitch protocol — severing the Dominion's remote
    /// leash — and reclaims his birth name SOREN, spoken aloud for the first time. The episode
    /// closes on the discovery that a deeper, older time-lock remains armed inside his chip.
    /// Speaker label is "Cipher" through the vault (beats 1-4) and "Soren" after the reclaim
    /// (beats 5-6); both map to the same voice for continuity.
    /// </summary>
    public static class Ep30Lines
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
            "mera_intro",
            "corvette_dogfight",
            "sepulcher_briefing",
            "khall_welcome",
            "khall_hologram",
            "warden_clear",
            "file_speaks",
            "docking_escape",
            "hollow_kings_offer",
            "debris_evade",
            "name_remains",
            "space_ep30_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "mera_intro" => GetMeraIntroLines(),
                "corvette_dogfight" => GetCorvetteDogfightLines(),
                "sepulcher_briefing" => GetSepulcherBriefingLines(),
                "khall_welcome" => GetKhallWelcomeLines(),
                "khall_hologram" => GetKhallHologramLines(),
                "warden_clear" => GetWardenClearLines(),
                "file_speaks" => GetFileSpeaksLines(),
                "docking_escape" => GetDockingEscapeLines(),
                "hollow_kings_offer" => GetHollowKingsOfferLines(),
                "debris_evade" => GetDebrisEvadeLines(),
                "name_remains" => GetNameRemainsLines(),
                "space_ep30_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep30_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep30_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ==== BEAT 1 — THE PRICE OF MEMORY (INTRO) ====

        private static DialogueLine[] GetMeraIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mera Voss", text = "Subject 7. Still breathing.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Mera.", seconds = 1f },
                new DialogueLine { speaker = "Mera Voss", text = "You should be dead three times over. Dominion burns through purged operatives like fuel. But here you stand.", seconds = 3f },
                new DialogueLine { speaker = "Mera Voss", text = "That makes you either the most durable artifact they ever built, or the one they all need dead the most. Both possibilities are lucrative.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "Mera. Took your time finding your way back to us.", seconds = 2f },
                new DialogueLine { speaker = "Mera Voss", text = "Kessler. Still moving cargo for the lost?", seconds = 2f },
                new DialogueLine { speaker = "Mera Voss", text = "I have the access codes for Tier 3 of the Iron Sepulcher. Your sealed file is logged there as SUBJECT 7. Has been since your purge.", seconds = 3.5f },
                new DialogueLine { speaker = "Mera Voss", text = "But the price is not currency. I enter with you. I read the file at the same moment you do.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "That's not protocol for a retrieval. That's leverage.", seconds = 2.5f },
                new DialogueLine { speaker = "Mera Voss", text = "Old habits. The Lotus fenced pieces of his mind once, before I knew whose they were. I owe him my own eyes on the whole of it.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "I accept.", seconds = 1f },
            };
        }

        // ==== BEAT 1 — THE PRICE OF MEMORY (DOGFIGHT) ====

        private static DialogueLine[] GetCorvetteDogfightLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Station Control", text = "Unauthorized vessels! Power down immediately or we burn you!", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Engines to full! Now!", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Two minutes of fire to clear them.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Third is breaking pursuit!", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "They know our heading now. The Sepulcher's location is already logged.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "Then we move fast.", seconds = 1f },
            };
        }

        // ==== BEAT 2 — THE SEPULCHER APPROACH (BRIEFING + RUN) ====

        private static DialogueLine[] GetSepulcherBriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mera Voss", text = "The Sepulcher is a tomb for classified files. Operative memories. Purge records. Things the Dominion does not want circulating.", seconds = 3f },
                new DialogueLine { speaker = "Mera Voss", text = "No conventional docking. We breach through the thermal exhaust maintenance lock. Ninety-second spoofed signature window before every alarm in the station goes active.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "And if we miss the window?", seconds = 1.5f },
                new DialogueLine { speaker = "Mera Voss", text = "Then we burn to cinder and the file stays buried.", seconds = 1.5f },
                new DialogueLine { speaker = "Mera Voss", text = "Your file is locked in Tier 3. The deepest vault level. The only way to stop your neural purge is to reach the edit console and delete the killswitch protocol from the archive record directly. If the Dominion's remote trigger has no reference to activate, it severs the link to your chip.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "And if the deletion does not work?", seconds = 1.5f },
                new DialogueLine { speaker = "Mera Voss", text = "Then you are still dying. Just slower now. Without anyone on the other end of the trigger ordering it.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "That is enough.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "Signature spoof is active. Window is now.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Weapons lock! Evasive!", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Four seconds to the lock.", seconds = 1f },
            };
        }

        // ==== BEAT 2 — KHALL'S WELCOME (ON ENCOUNTER CLEAR) ====

        private static DialogueLine[] GetKhallWelcomeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "Cipher. Welcome to the Sepulcher.", seconds = 2.5f },
                new DialogueLine { speaker = "Khall", text = "I have been expecting this approach for six cycles. The vault is sealed ahead of you.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "Read what is there. Then decide if you still want to keep surviving.", seconds = 2.5f },
                new DialogueLine { speaker = "Mera Voss", text = "Move fast.", seconds = 1f },
            };
        }

        // ==== BEAT 3 — INTO THE IRON (HOLOGRAM) ====

        private static DialogueLine[] GetKhallHologramLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mera Voss", text = "You're three corridors from Tier 3. Left at the main junction. The cryogenic chamber is beyond the secondary bulkhead.", seconds = 3f },
                new DialogueLine { speaker = "Khall (Hologram)", text = "You were the only operative I believed could understand what the Dominion actually was.", seconds = 3f },
                new DialogueLine { speaker = "Khall (Hologram)", text = "The orphanage test was designed to break your conditioning. To see if the Program's mercy could fracture under pressure.", seconds = 3.5f },
                new DialogueLine { speaker = "Khall (Hologram)", text = "When it did not break, when you chose to refuse, I knew what you were.", seconds = 2.5f },
                new DialogueLine { speaker = "Khall (Hologram)", text = "I armed the killswitch as ordered. But I made certain you survived anyway. Because I was not ready to accept what your refusal meant.", seconds = 3.5f },
                new DialogueLine { speaker = "Khall (Hologram)", text = "I do not expect forgiveness. I expect you to understand that I was trapped in the same cage you are escaping.", seconds = 3f },
            };
        }

        // ==== BEAT 3 — THE WARDEN CLEARED ====

        private static DialogueLine[] GetWardenClearLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mera Voss", text = "You're clear. The vault is ahead.", seconds = 1.5f },
            };
        }

        // ==== BEAT 4 — THE FILE SPEAKS ====

        private static DialogueLine[] GetFileSpeaksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher (Recording - Younger)", text = "My name is Soren. I chose to stop.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "My name is Soren.", seconds = 1.5f },
                new DialogueLine { speaker = "Katana (Interior VO)", text = "Soren. Yes. I have kept it for you since before the wipe. Take it back. Take everything back.", seconds = 3.5f },
            };
        }

        // ==== BEAT 4 — THE DOCKING-RING ESCAPE ====

        private static DialogueLine[] GetDockingEscapeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Now! Go!", seconds = 1f },
            };
        }

        // ==== BEAT 5 — THE HOLLOW KINGS ====

        private static DialogueLine[] GetHollowKingsOfferLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Broker (Hollow Kings)", text = "Operative Cipher. A name has cleared from the Dominion's purged list. Remarkable.", seconds = 2.5f },
                new DialogueLine { speaker = "Broker (Hollow Kings)", text = "We offer one transaction. Scrub all Dominion server records of the Sepulcher vault heist. Delete every trace of what occurred in Tier 3.", seconds = 4f },
                new DialogueLine { speaker = "Broker (Hollow Kings)", text = "In exchange, you take one contract. Small retrieval. Classified location. One target. Alive.", seconds = 2.5f },
                new DialogueLine { speaker = "Soren", text = "I did not come this far to trade one leash for another.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "The Carnival's ledgers said the stolen shards of my memory moved through Lotus hands before Morrigan bought them back. Your hands, Mera?", seconds = 3.5f },
                new DialogueLine { speaker = "Mera Voss", text = "Then we are even on the subject of things neither of us will explain.", seconds = 2.5f },
                new DialogueLine { speaker = "Soren", text = "That is acceptable.", seconds = 1f },
            };
        }

        // ==== BEAT 5 — THE DEBRIS EVASION ====

        private static DialogueLine[] GetDebrisEvadeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Contact closing! Weapons hot!", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "We are clear.", seconds = 1f },
            };
        }

        // ==== BEAT 6 — THE NAME THAT REMAINS ====

        private static DialogueLine[] GetNameRemainsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "The killswitch is confirmed gone. The remote trigger is severed.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "The neural purge has stopped.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "But the diagnostic is showing something else.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Deeper in the architecture. Below the purge systems. A second layer. Older. It has different construction patterns.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "A time-lock. Dormant. Structured completely differently from the killswitch. It did not activate when the remote trigger died.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "It is still waiting. Timing toward something none of us can identify.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Tessa Rin would need to see this. Even then I do not know how fast she could answer.", seconds = 2.5f },
                new DialogueLine { speaker = "Mera Voss", text = "That architecture is not Khall's. It is older than him.", seconds = 2f },
                new DialogueLine { speaker = "Mera Voss", text = "That is foundational Dominion work. Oldest generation.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "The killswitch is gone. The remote trigger is severed. The neural purge has stopped.", seconds = 3f },
                new DialogueLine { speaker = "Soren", text = "But there is another layer. Older. Deeper. Still armed.", seconds = 2.5f },
                new DialogueLine { speaker = "Soren", text = "Whatever the time-lock holds, I carry it forward now knowing who I am.", seconds = 2.5f },
                new DialogueLine { speaker = "Soren", text = "I am Soren. Not a designation handed down. Not a codename. Mine, from before all the conditioning and the purge and the years of not knowing.", seconds = 3.5f },
            };
        }

        // ==== GALAXY 4 HUB CABIN: POST-MISSION BRIEFING ====

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "We breached the Iron Sepulcher, the Dominion's black-archive, anchored in a dead nebula at the galaxy's cold rim. Mera Voss came back for this, trading her access codes for her own eyes on my file. Inside Tier 3, the vault opened my sealed record: SUBJECT 7. And underneath the designation was a name I was never supposed to hear again. I deleted the killswitch protocol from the archive. The remote trigger is severed. The neural purge has gone quiet. I have my name back. I am Soren.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "But the diagnostic found something underneath all of it. A second layer in your chip, older than the killswitch, built differently, dormant. A time-lock. It did not die when the trigger did. It is still counting toward something none of us can read, and Mera says that architecture is older than Khall, foundational Dominion work, oldest generation. So we are not finished. Whatever they buried deepest in you is still armed, and we need to find what it is waiting for before it finds us.", seconds = 5f },
            };
        }
    }
}
