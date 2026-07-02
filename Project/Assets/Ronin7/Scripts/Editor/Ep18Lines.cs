using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 18 dialogue data. Transposed from ep18-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep18_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 18 spans the Drowning Deep—Takeshi's rescue from the Abyssal Vault, the revelation
    /// of the networked failsafe architecture, and the discovery of a master ledger naming every
    /// operative the Dominion has erased.
    /// </summary>
    public static class Ep18Lines
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
            "cabin_brief2",
            "bartender_info",
            "station_enforcer_barks",
            "corridor_confession",
            "prisoner_worker",
            "quarantine_recognition",
            "failsafe_reveal",
            "extraction_barks",
            "vault_inventory",
            "sable_intercept",
            "construct_barks",
            "commodore_logic",
            "dock_return",
            "hyperspace_revelation",
            "ledger_promise",
            "observation_truth",
            "space_ep18_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "cabin_brief2" => GetCabinBrief2Lines(),
                "bartender_info" => GetBartenderInfoLines(),
                "station_enforcer_barks" => GetStationEnforcerBarksLines(),
                "corridor_confession" => GetCorridorConfessionLines(),
                "prisoner_worker" => GetPrisonerWorkerLines(),
                "quarantine_recognition" => GetQuarantineRecognitionLines(),
                "failsafe_reveal" => GetFailsafeRevealLines(),
                "extraction_barks" => GetExtractionBarksLines(),
                "vault_inventory" => GetVaultInventoryLines(),
                "sable_intercept" => GetSableInterceptLines(),
                "construct_barks" => GetConstructBarksLines(),
                "commodore_logic" => GetCommodoreLogicLines(),
                "dock_return" => GetDockReturnLines(),
                "hyperspace_revelation" => GetHyperspaceRevelationLines(),
                "ledger_promise" => GetLedgerPromiseLines(),
                "observation_truth" => GetObservationTruthLines(),
                "space_ep18_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep18_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep18_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- CARGO BAY WEIGHT (Beat 1) ----

        private static DialogueLine[] GetCabinBrief2Lines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "You're going to want to see this.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "What is it?", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "A message. Encrypted through a dead-drop network I thought I'd burned years ago.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Coordinates to Thalassa-5. And one word: \"Takeshi.\"", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Your son.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "Recruited into the Dominion at twelve. Silent for eight years. But someone got a message out.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "These coordinates are the first sign he's still breathing.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "We set course now.", seconds = 1f },
            };
        }

        // ---- THALASSA STATION DESCENT (Beat 3) ----

        private static DialogueLine[] GetBartenderInfoLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Bartender", text = "You're looking for something specific, and you're carrying the weight of it like a wound.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "The Abyssal Vault. What do you know about operations down there?", seconds = 2.5f },
                new DialogueLine { speaker = "Bartender", text = "The Vault is a prison and a market both. Prisoners are value, and value is extracted.", seconds = 2.5f },
                new DialogueLine { speaker = "Bartender", text = "They flood the cells twice daily. Drowning near to death and revival, over and over, until the prisoners' memory barriers collapse. What's inside gets sold. Everything extracted flows through the Hollow Kings' network.", seconds = 4f },
                new DialogueLine { speaker = "Bartender", text = "What remains of the prisoners gets recycled as programmable labor. Work until expiration. Efficient.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "How deep?", seconds = 1f },
                new DialogueLine { speaker = "Bartender", text = "Two kilometers beneath the ice, tethered to thermal vents. Corridors are built to flood and drain on a tidal cycle. Prisoners don't climb out. They're already beneath the drowning.", seconds = 4f },
            };
        }

        // ---- THE STATION INTERCEPTOR (Beat 4) ----

        private static DialogueLine[] GetStationEnforcerBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tide Baron Enforcer", text = "Kessler. I remember you.", seconds = 2f },
                new DialogueLine { speaker = "Tide Baron Enforcer", text = "The old debt. From the runoff years. Still outstanding.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "That debt's been paid through channels...", seconds = 1.5f },
                new DialogueLine { speaker = "Enforcer 1", text = "Multiple contacts! Detainment protocol!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "We're out of time. We move now or we're trapped here.", seconds = 2.5f },
            };
        }

        // ---- THE CORRIDOR CONFESSION (Beat 5) ----

        private static DialogueLine[] GetCorridorConfessionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Takeshi was taken when he was twelve.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "The Dominion's recruiters came to his school. They said he had augmentation markers. Potential. I tried to stop them.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "They took him anyway.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "The Barons declared his departure a \"compensable debt.\" A boy with operative markers is valuable merchandise. I would have had to pay or lose him.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "I chose to disappear instead. I've been paying in other ways ever since.", seconds = 3f },
            };
        }

        // ---- VAULT OUTER LEVELS (Beat 6) ----

        private static DialogueLine[] GetPrisonerWorkerLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Prisoner-Worker", text = "You move like an operative.", seconds = 2f },
            };
        }

        // ---- THE QUARANTINE RECOGNITION (Beat 8) ----

        private static DialogueLine[] GetQuarantineRecognitionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Takeshi", text = "Seven.", seconds = 1f },
                new DialogueLine { speaker = "Takeshi", text = "You came.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "How do you know that designation?", seconds = 2f },
                new DialogueLine { speaker = "Takeshi", text = "Because my father told me about you before they took me.", seconds = 2.5f },
                new DialogueLine { speaker = "Takeshi", text = "He said when I was scared I should remember a soldier who chose mercy over mission. He said you were the only operative they ever couldn't make kill children.", seconds = 4.5f },
                new DialogueLine { speaker = "Takeshi", text = "The Barons said you were dead. But I never believed it.", seconds = 2f },
                new DialogueLine { speaker = "Takeshi", text = "How did you know to come?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Your father sent a message. One word. That was enough.", seconds = 2.5f },
                new DialogueLine { speaker = "Takeshi", text = "Because I'm the only operative who ever chose someone over the mission.", seconds = 2.5f },
                new DialogueLine { speaker = "Takeshi", text = "No. You're the only operative who chose my father. And that was the whole choice.", seconds = 3f },
            };
        }

        // ---- THE EXTRACTION CHAMBER (Beat 9) ----

        private static DialogueLine[] GetFailsafeRevealLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Takeshi", text = "The Dominion's failsafe leaves a neural signature in every operative who ever walked out of the program.", seconds = 3f },
                new DialogueLine { speaker = "Takeshi", text = "A latticework connecting chip to handler to command hub. Every operative networked to the next.", seconds = 2.5f },
                new DialogueLine { speaker = "Takeshi", text = "By extracting from enough operatives over enough years, the Vault built a complete map. The full reach of the network. How many operatives are connected. How the routing flows through Dominion command infrastructure.", seconds = 4.5f },
                new DialogueLine { speaker = "Takeshi", text = "They have that map now. The Barons sold it to two major buyers. The network is no longer a secret the Dominion controls alone.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "And you.", seconds = 1f },
                new DialogueLine { speaker = "Takeshi", text = "Tomorrow they plan to extract my memories of you. Of your defection. To sell as proof that the conditioning can break. To sell that crack in the architecture to anyone desperate enough to pay.", seconds = 4f },
            };
        }

        // ---- THE EXTRACTION FIGHT (Beat 10) ----

        private static DialogueLine[] GetExtractionBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Researcher 1", text = "Specimen is mobile! Override containment!", seconds = 1.5f },
            };
        }

        // ---- THE VAULT'S INVENTORY (Beat 11) ----

        private static DialogueLine[] GetVaultInventoryLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Takeshi", text = "Seventeen prisoners remain in the Vault. All operatives. All still alive.", seconds = 2.5f },
                new DialogueLine { speaker = "Takeshi", text = "If containment fails, the standard protocol is a full-structure flood. Kill the evidence. No survivors. No proof.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "Where are the emergency bulkhead controls?", seconds = 1f },
                new DialogueLine { speaker = "Takeshi", text = "Three sectors down. The command core. Below the research floor.", seconds = 2f },
            };
        }

        // ---- RESEARCH FLOOR DESCENT (Beat 12) ----

        private static DialogueLine[] GetSableInterceptLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sable Dross", text = "I've watched you since you entered the Vault.", seconds = 2.5f },
                new DialogueLine { speaker = "Sable Dross", text = "I joined the Tide Barons for deep-sea research. For the study of bioluminescence and neural adaptation in high-pressure environments.", seconds = 3.5f },
                new DialogueLine { speaker = "Sable Dross", text = "I did not join to watch the Vault become what it became.", seconds = 2.5f },
                new DialogueLine { speaker = "Sable Dross", text = "The command core access code. Use it.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Why would you give me this?", seconds = 1f },
                new DialogueLine { speaker = "Sable Dross", text = "Because the Dominion doesn't care about justice. They care about efficiency. The Barons don't care about the prisoners. They care about profit. And I have decided I will care about neither.", seconds = 4.5f },
                new DialogueLine { speaker = "Sable Dross", text = "After this, I am done with the Tide Barons. I want passage from the Dominion's reach.", seconds = 2.5f },
            };
        }

        // ---- COMMAND CORE APPROACH (Beat 13) ----

        private static DialogueLine[] GetConstructBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Defense System (Overhead)", text = "Intruder neutralized by automated defense. Initiate containment protocol.", seconds = 2.5f },
            };
        }

        // ---- THE COMMODORE'S LOGIC (Beat 14) ----

        private static DialogueLine[] GetCommodoreLogicLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Commodore", text = "The Tide Barons have never exploited weakness in operatives.", seconds = 2.5f },
                new DialogueLine { speaker = "Commodore", text = "Weakness is unreliable. It breaks in unpredictable ways.", seconds = 2.5f },
                new DialogueLine { speaker = "Commodore", text = "Strength is what we exploit. The capacity for connection. For compassion. The flaw that makes operatives useful to anyone who understands it.", seconds = 4.5f },
            };
        }

        // ---- THE DOCK RETURN (Beat 15) ----

        private static DialogueLine[] GetDockReturnLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sable Dross", text = "The Vault's full extraction records. Every operative transferred. Every memory extracted. Every price paid.", seconds = 3.5f },
                new DialogueLine { speaker = "Sable Dross", text = "I copied everything before the collapse. The originals are wiped. No second copy exists.", seconds = 2.5f },
                new DialogueLine { speaker = "Sable Dross", text = "I'm done with the Barons.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "You have passage. Welcome aboard.", seconds = 1.5f },
            };
        }

        // ---- HYPERSPACE REVELATION (Beat 17) ----

        private static DialogueLine[] GetHyperspaceRevelationLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sable Dross", text = "The network map was sold to at least two external buyers before I wiped the Vault's original records.", seconds = 3.5f },
                new DialogueLine { speaker = "Sable Dross", text = "One buyer paid in currency used only by organizations working to dismantle the Dominion from outside.", seconds = 3f },
                new DialogueLine { speaker = "Sable Dross", text = "That might be you, eventually.", seconds = 2f },
            };
        }

        // ---- THE LEDGER'S PROMISE (Beat 18) ----

        private static DialogueLine[] GetLedgerPromiseLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sable Dross", text = "The archive contains everything the Vault extracted from operatives over the past seven years.", seconds = 2.5f },
                new DialogueLine { speaker = "Sable Dross", text = "Alongside the network map, there are the Vault's procurement records. Every operative transferred in. Dates of capture. Last Dominion contact recorded. Status at transfer.", seconds = 4.5f },
                new DialogueLine { speaker = "Sable Dross", text = "These entries are different. Not dead. Not broken. But struck from the Dominion's own records as though they never existed.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "This is only the Vault's copy.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Somewhere in the Dominion, there is a master ledger. Every operative they ever built. Every one they erased. A complete accounting of what they made and then unmade.", seconds = 4.5f },
            };
        }

        // ---- OBSERVATION WINDOW TRUTH (Beat 20) ----

        private static DialogueLine[] GetObservationTruthLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "The Dominion built us and erased us when we stopped being useful.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "If there is a master ledger, every name they struck from the record, then there are people who don't know they're still on someone's list. Or still alive enough to matter.", seconds = 4.5f },
                new DialogueLine { speaker = "Cipher", text = "We find that ledger. And we make sure no one else forgets what the Dominion tried to erase.", seconds = 3.5f },
            };
        }

        // ---- GALAXY 3 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Takeshi is alive. Sable Dross is aboard with the Vault's full records.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "And somewhere there's a master ledger naming every operative they erased. We find it next.", seconds = 3.5f },
            };
        }
    }
}
