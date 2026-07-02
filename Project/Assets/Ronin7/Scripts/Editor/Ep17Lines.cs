using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 17 dialogue data. Transposed from ep17-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep17_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 17 spans the Pit, the revelation of Vesper, the discovery that the failsafe
    /// is a networked control system binding all operatives, and the choice to break it.
    /// </summary>
    public static class Ep17Lines
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
            "blood_debt",
            "intake_barks",
            "pitlord_study",
            "holding_pens",
            "arena_barks",
            "the_offer",
            "descent",
            "vesper_reveal",
            "vault_location",
            "record_found",
            "the_rising",
            "khall_confront",
            "khall_parting",
            "bridge_reckoning",
            "network_revelation",
            "hyperspace_drift",
            "cabin_brief",
            "space_ep17_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "blood_debt" => GetBloodDebtLines(),
                "intake_barks" => GetIntakeBarksLines(),
                "pitlord_study" => GetPitlordStudyLines(),
                "holding_pens" => GetHoldingPensLines(),
                "arena_barks" => GetArenaBarksLines(),
                "the_offer" => GetTheOfferLines(),
                "descent" => GetDescentLines(),
                "vesper_reveal" => GetVesperRevealLines(),
                "vault_location" => GetVaultLocationLines(),
                "record_found" => GetRecordFoundLines(),
                "the_rising" => GetTheRisingLines(),
                "khall_confront" => GetKhallConfrontLines(),
                "khall_parting" => GetKhallPartingLines(),
                "bridge_reckoning" => GetBridgeReckoningLines(),
                "network_revelation" => GetNetworkRevelationLines(),
                "hyperspace_drift" => GetHyperspaceRiftLines(),
                "cabin_brief" => GetCabinBriefLines(),
                "space_ep17_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep17_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep17_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BLOOD DEBT (Beat 1) ----

        private static DialogueLine[] GetBloodDebtLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Three years. The Saffron Veil and Hollow Kings have been tearing that same corridor apart for three years.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "Nothing resolved. No movement. Same ships. Same losses. Same profits for whoever's counting.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "A schedule.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "That's not a war. That's a schedule.", seconds = 2.5f },
                new DialogueLine { speaker = "Gryph", text = "Kessler. You're bringing me business or you're bringing me problems?", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Looking to fence salvage. Clean inventory. Nothing hot.", seconds = 2f },
                new DialogueLine { speaker = "Gryph", text = "You're deep in scrap debt, old man. We both know it.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "I can clear it.", seconds = 1f },
                new DialogueLine { speaker = "Gryph", text = "You can't. But I've been patient. That patience costs. Thirty matches. Your operative as pit stock. Every match he wins, your debt shrinks. Lose three consecutive and he goes to the lower pit instead.", seconds = 4.5f },
                new DialogueLine { speaker = "Gryph", text = "Or I impound the ship right now and you walk out of here with nothing.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "I'll do it.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "Cipher, you don't have to...", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "The ship keeps flying. That's the math.", seconds = 1.5f },
            };
        }

        // ---- INTAKE BARKS (Beat 2) ----

        private static DialogueLine[] GetIntakeBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Rustfang Breaker 1", text = "You get three minutes. Disarm or be disarmed.", seconds = 2f },
                new DialogueLine { speaker = "Rustfang Breaker 2", text = "Go.", seconds = 1f },
            };
        }

        // ---- THE PIT-LORD'S STUDY (Beat 3) ----

        private static DialogueLine[] GetPitlordStudyLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Gryph", text = "You fight like someone forgot to be afraid.", seconds = 2.5f },
                new DialogueLine { speaker = "Gryph", text = "That's rare. Most prisoners come in carrying their mortality like a weight. You move like it's not even there.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "What happens if I lose?", seconds = 1.5f },
                new DialogueLine { speaker = "Gryph", text = "Three consecutive losses and you clear the upper pit. You go down to the lower level. Specialty markets. Neural components. Augmentation parts.", seconds = 3.5f },
                new DialogueLine { speaker = "Gryph", text = "The lower pit is where prisoners who can't be killed profitably go to be harvested instead.", seconds = 3f },
                new DialogueLine { speaker = "Gryph", text = "The gravity rigs rotate every six hours. Gives you time to learn the rhythm before it changes.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "Why are you helping me?", seconds = 1.5f },
                new DialogueLine { speaker = "Gryph", text = "I'll tell you when the time comes. Or I won't. Either way, the debt clears by your sword or your corpse.", seconds = 3f },
            };
        }

        // ---- THE HOLDING PENS (Beat 4) ----

        private static DialogueLine[] GetHoldingPensLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tisane", text = "New blood from the surface?", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "First match in four hours.", seconds = 1.5f },
                new DialogueLine { speaker = "Tisane", text = "You'll get three. Maybe four if you look useful. The gravity rig rotates every six hours, zero-g, heavy, zero-g. You learn the rhythm or you break in the wrong gravity and become a stain.", seconds = 4f },
                new DialogueLine { speaker = "Tisane", text = "Lose three in a row and they don't bring you back up here. The lower pit takes what's too valuable to kill but too broken to sell.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "What's down there?", seconds = 1f },
                new DialogueLine { speaker = "Tisane", text = "Nobody talks about it. But the ones who go down, they come back different. If they come back at all.", seconds = 3f },
            };
        }

        // ---- ARENA BARKS (Beat 5) ----

        private static DialogueLine[] GetArenaBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Pit Master", text = "Zero-G bout! Twenty-second grace period! Begin!", seconds = 2f },
                new DialogueLine { speaker = "Pit Master", text = "First victor! Clear the arena!", seconds = 2f },
            };
        }

        // ---- THE OFFER (Beat 6) ----

        private static DialogueLine[] GetTheOfferLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Gryph", text = "You've won four. That's more than the pool expected.", seconds = 2f },
                new DialogueLine { speaker = "Gryph", text = "There's a prisoner in the lower pit who refuses to fight. The Rustfangs want her removed. If you bring her up alive, your debt clears today.", seconds = 3.5f },
                new DialogueLine { speaker = "Rustfang Corporal", text = "One condition. If she's valuable enough to refuse, she's valuable enough to stay functional.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "I'll bring her up alive.", seconds = 1f },
            };
        }

        // ---- THE DESCENT (Beat 7) ----

        private static DialogueLine[] GetDescentLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Rustfang Corporal", text = "Lower pit serves specialty markets. Neural components. Augmentation parts. Prisoners with value can be harvested instead of killed.", seconds = 3.5f },
                new DialogueLine { speaker = "Vesper", text = "Seven?", seconds = 1f },
            };
        }

        // ---- VESPER'S REVELATION (Beat 9) ----

        private static DialogueLine[] GetVesperRevealLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vesper", text = "You move like seven. Like Cipher.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "I have no memory. My designation is all I have.", seconds = 2.5f },
                new DialogueLine { speaker = "Vesper", text = "I was in your cohort. Third generation. Watched them pull you out of the staging compound when you were twelve. I thought you were dead. Then they erased me and dumped me here instead of finishing it.", seconds = 4f },
                new DialogueLine { speaker = "Vesper", text = "The Dominion didn't purge you because the conditioning failed.", seconds = 2.5f },
                new DialogueLine { speaker = "Vesper", text = "They purged you because you proved it could be broken from the inside. Your refusal at Kethel-7, that wasn't a malfunction. That was proof.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Proof of what?", seconds = 1f },
                new DialogueLine { speaker = "Vesper", text = "That we're not machines. That the conditioning can fail. That a Ronin operative can choose mercy. The Overseer could not allow that possibility to circulate. So he purged everyone who knew. He rewrote the record. He made you into a ghost.", seconds = 5.5f },
                new DialogueLine { speaker = "Vesper", text = "My own erasure stalled partway. They dumped me here instead of trying again. They thought I was too broken to matter.", seconds = 3f },
            };
        }

        // ---- VAULT LOCATION (Beat 10) ----

        private static DialogueLine[] GetVaultLocationLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vesper", text = "The vault core is three levels up, past the processing bays. This code will open the security door. The personnel files are still stored there, purge orders, mission logs, termination authorizations.", seconds = 4f },
                new DialogueLine { speaker = "Vesper", text = "Your operational record might still be there. If you find it, you find out why they were afraid of you.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "And you'll stay here?", seconds = 1f },
                new DialogueLine { speaker = "Vesper", text = "I'll hold the lower level. They'll hear us leaving and they'll come down hard. But I can buy you the corridor.", seconds = 3f },
            };
        }

        // ---- THE RECORD FOUND (Beat 12) ----

        private static DialogueLine[] GetRecordFoundLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Classification: Purge. Stamp: Protocol Silencia.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "If you wake, you were right. Kethel-7 was real. You were right to break. Find Kessler.", seconds = 3f },
            };
        }

        // ---- THE RISING (Beat 13) ----

        private static DialogueLine[] GetTheRisingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vesper", text = "Find anything?", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Enough to prove you saved me. The rest is erased.", seconds = 2f },
                new DialogueLine { speaker = "Vesper", text = "Deeper than a standard purge?", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Someone came before me.", seconds = 1.5f },
                new DialogueLine { speaker = "Vesper", text = "The conditioning failures aren't isolated. I've seen fragments, other prisoners who passed through here, operatives waking up across multiple syndicates.", seconds = 4f },
                new DialogueLine { speaker = "Vesper", text = "Whoever is waking will burn the old order or rebuild it. Either way, the war is changing.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Vesper...", seconds = 1f },
                new DialogueLine { speaker = "Vesper", text = "Tell Kessler I said hi.", seconds = 1.5f },
            };
        }

        // ---- KHALL CONFRONTATION (Beat 14) ----

        private static DialogueLine[] GetKhallConfrontLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "The vault file contained a trace beacon. I was waiting for this moment.", seconds = 2.5f },
                new DialogueLine { speaker = "Khall", text = "I want answers about Kethel-7 before the Dominion gets its closure. And I'm calling you Mirror because that's what the vault called you.", seconds = 3.5f },
            };
        }

        // ---- KHALL'S PARTING (Beat 15) ----

        private static DialogueLine[] GetKhallPartingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "You weren't the only one who broke. You were just the one they could never find a clean reason for.", seconds = 3f },
            };
        }

        // ---- THE BRIDGE RECKONING (Beat 16) ----

        private static DialogueLine[] GetBridgeReckoningLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "What happened down there?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "There was an operative in the lower pit. Ronin-3. She arranged my disposal as mercy, just drift instead of burn. And she left a message.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "The Dominion didn't purge me because the conditioning failed. They purged me because I proved it could be broken. From the inside.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "She carved an access code into her own spine so you'd have it if you ever showed up. That's a long bet on a dead man.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "Vesper. That's her?", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Ronin-3. She's still alive.", seconds = 1.5f },
            };
        }

        // ---- THE NETWORK REVELATION (Beat 17) ----

        private static DialogueLine[] GetNetworkRevelationLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "The failsafe chip. It's not just me.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Vesper still carries one. Incomplete purge. Every operative who ever walked out of the program carries one.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "If the Dominion built it into him, they built it into all of them. The failsafe is not personal. It's a network.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Every operative. All of them networked.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Whoever controls the network controls all of them.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Debt is clear. What I'm holding is information rather than scrap. Gryph is done working for the Rustfangs.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Send reply: we'll talk.", seconds = 1f },
            };
        }

        // ---- HYPERSPACE DRIFT (Beat 18) ----

        private static DialogueLine[] GetHyperspaceRiftLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "I'm not the operative they erased. I'm the operative they were afraid of.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "You chose mercy. They couldn't forgive that.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "But others made the same choice. And if those operatives can wake up, if they can coordinate, then the cage is cracking.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "The failsafe network.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "We find out who controls it. And we break it.", seconds = 2f },
            };
        }

        // ---- GALAXY 3 HUB CABIN: PRE-MISSION BRIEFING ----

        private static DialogueLine[] GetCabinBriefLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "That's Rustfang's Cradle below us. Gutted ore world, run by the syndicate that holds my scrap debt. Watch yourself down there.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Set us down. The sooner the debt clears, the sooner we move on.", seconds = 3f },
            };
        }

        // ---- GALAXY 3 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Vesper made it out. She's carrying proof of what they erased.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "The failsafe is a network. Every operative they made is connected. All of them.", seconds = 3f },
            };
        }
    }
}
