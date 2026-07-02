using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 9 dialogue data. Transposed from ep09-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep09_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Set count runs higher than earlier episodes because EP09 spans the Galaxy 2 hub plus five
    /// scenes (CharSpire / SpireDuel / Kethel7Memory / CindersRefinery / Extraction).
    /// </summary>
    public static class Ep09Lines
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
            "space_ep09_briefing",
            "spire_docking",
            "corridor_runners",
            "bounty_read",
            "tavern_murmur",
            "hunters_gambit",
            "catwalk_duel",
            "mask_removed",
            "the_fracture",
            "dominion_breach",
            "the_evidence",
            "memory_descent",
            "memory_echoes",
            "memory_refusal",
            "kira_name",
            "cinders_breach",
            "khall_reckoning",
            "cinders_scouts",
            "cinders_escape",
            "extraction_picket",
            "ascent_epilogue",
            "space_ep09_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "space_ep09_briefing" => GetSpaceBriefingLines(),
                "spire_docking" => GetSpireDockingLines(),
                "corridor_runners" => GetCorridorRunnersLines(),
                "bounty_read" => GetBountyReadLines(),
                "tavern_murmur" => GetTavernMurmurLines(),
                "hunters_gambit" => GetHuntersGambitLines(),
                "catwalk_duel" => GetCatwalkDuelLines(),
                "mask_removed" => GetMaskRemovedLines(),
                "the_fracture" => GetTheFractureLines(),
                "dominion_breach" => GetDominionBreachLines(),
                "the_evidence" => GetTheEvidenceLines(),
                "memory_descent" => GetMemoryDescentLines(),
                "memory_echoes" => GetMemoryEchoesLines(),
                "memory_refusal" => GetMemoryRefusalLines(),
                "kira_name" => GetKiraNameLines(),
                "cinders_breach" => GetCindersBreachLines(),
                "khall_reckoning" => GetKhallReckoningLines(),
                "cinders_scouts" => GetCindersScoutsLines(),
                "cinders_escape" => GetCindersEscapeLines(),
                "extraction_picket" => GetExtractionPicketLines(),
                "ascent_epilogue" => GetAscentEpilogueLines(),
                "space_ep09_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep09_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep09_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- GALAXY 2 HUB CABIN: PRE-MISSION BRIEFING (Beat 1) ----

        private static DialogueLine[] GetSpaceBriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "You've been standing there for two hours.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "The core confirms the shape of the choices. That I refused orders. But it doesn't tell me what I refused to kill, or who begged me not to.", seconds = 4.5f },
                new DialogueLine { speaker = "Mera Voss", text = "The syndicates have been trading fragments across Galaxy 2 for years. Somewhere on the Char Spire there's a market for exactly what you're missing.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "The failsafe is still ticking. Every hour we spend circling is an hour we don't get back.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "Then we move.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "The past has a way of finding you before you find it.", seconds = 2f },
            };
        }

        // ---- CHAR SPIRE: DOCKING BAY, THE BOUNTY HOLOGRAM (Beat 2) ----

        private static DialogueLine[] GetSpireDockingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Someone spent serious credits on that.", seconds = 2f },
                new DialogueLine { speaker = "Mera Voss", text = "Dominion-grade posting. Not a fringe operator.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "They're afraid of something.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "I'd say flattered, but that amount of fear usually ends badly for someone.", seconds = 2f },
            };
        }

        // ---- CHAR SPIRE: SYNDICATE RUNNERS IN THE MAG-LOCKED CORRIDOR (Beat 3) ----

        private static DialogueLine[] GetCorridorRunnersLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Syndicate Runner 1", text = "Station buy-back! Stand down!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Back!", seconds = 0.5f },
                new DialogueLine { speaker = "Syndicate Runner 2", text = "He's Dominion...", seconds = 0.5f },
                new DialogueLine { speaker = "Kessler", text = "Fast.", seconds = 1f },
            };
        }

        // ---- CHAR SPIRE: READING THE BOUNTY (Beat 4) ----

        private static DialogueLine[] GetBountyReadLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "This is a retirement payout. Enough to buy a ship. Enough to buy two.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Someone very afraid of you posted this.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "I think I already know them.", seconds = 1.5f },
            };
        }

        // ---- CHAR SPIRE: TAVERN AMBIENCE (cast colour — Barkeep + patrons) ----

        private static DialogueLine[] GetTavernMurmurLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Barkeep", text = "You want quiet, you picked the wrong deck. Half the Spire heard about that bounty.", seconds = 3f },
                new DialogueLine { speaker = "Salvager Patron 1", text = "That's him. Right off the hologram.", seconds = 1.5f },
                new DialogueLine { speaker = "Salvager Patron 2", text = "Don't stare. Bounty that size, staring gets you spaced.", seconds = 2f },
            };
        }

        // ---- CHAR SPIRE: THE HUNTER'S GAMBIT (Beat 5) ----

        private static DialogueLine[] GetHuntersGambitLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vera Dusk", text = "Cipher. Number seven.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "You have me at a disadvantage.", seconds = 1.5f },
                new DialogueLine { speaker = "Vera Dusk", text = "I know.", seconds = 1f },
                new DialogueLine { speaker = "Vera Dusk", text = "You have a choice. Come quietly and I will be fast about it. Or I take the old man instead.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "She's Dominion-trained. Look at the stance.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "We're not going quietly.", seconds = 1f },
            };
        }

        // ---- SPIRE DUEL: CATWALK DUEL BARKS (Beat 6) ----

        private static DialogueLine[] GetCatwalkDuelLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vera Dusk", text = "I've studied every recorded movement you've ever made. Every mission the Dominion filed before the wipe.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "Your anger is widening your reach.", seconds = 1.5f },
            };
        }

        // ---- SPIRE DUEL: THE MASK REMOVED — VERA'S TESTIMONY (Beat 7, yield dialogue) ----

        private static DialogueLine[] GetMaskRemovedLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vera Dusk", text = "Vera Dusk.", seconds = 1f },
                new DialogueLine { speaker = "Vera Dusk", text = "I was at the Kethel-7 orphanage five years ago.", seconds = 2f },
                new DialogueLine { speaker = "Vera Dusk", text = "I was twelve. I hid inside the walls when the Dominion came. Twenty-four children died.", seconds = 3.5f },
                new DialogueLine { speaker = "Vera Dusk", text = "I survived because I was too small and too frightened to be found.", seconds = 2.5f },
                new DialogueLine { speaker = "Vera Dusk", text = "You were there. And then you went down. And then the soldiers came in after you.", seconds = 3f },
                new DialogueLine { speaker = "Vera Dusk", text = "I've been hunting you for three years trying to understand exactly what I saw.", seconds = 2.5f },
            };
        }

        // ---- SPIRE DUEL: THE FRACTURE (Beat 8) ----

        private static DialogueLine[] GetTheFractureLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Tell me what you saw.", seconds = 1.5f },
                new DialogueLine { speaker = "Vera Dusk", text = "I watched you refuse. The kill-order came through on comms, and you stopped.", seconds = 3f },
                new DialogueLine { speaker = "Vera Dusk", text = "Then the failsafe triggered. You went down hard. And the soldiers came in without you.", seconds = 2.5f },
                new DialogueLine { speaker = "Vera Dusk", text = "Either you grew a conscience in the field, or you were always something they could not fully build.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Why are you telling him this?", seconds = 1.5f },
                new DialogueLine { speaker = "Vera Dusk", text = "Because I came here to kill him and I need to understand why I can't.", seconds = 2.5f },
            };
        }

        // ---- SPIRE DUEL: DOMINION STRIKE TEAM BREACHES THE SPIRE (Beat 9) ----

        private static DialogueLine[] GetDominionBreachLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Strike team. East corridor. At least six.", seconds = 1.5f },
                new DialogueLine { speaker = "Vera Dusk", text = "They're not here for you. I've been running unsanctioned bounties for two years. They've been patient enough.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "Same direction we're walking.", seconds = 1f },
                new DialogueLine { speaker = "Dominion Scout 1", text = "Corridor sealed! Box them at the hub!", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Scout 2", text = "Rogue asset confirmed! Lethal engagement cleared!", seconds = 1.5f },
                new DialogueLine { speaker = "Vera Dusk", text = "Left junction! Maintenance hub!", seconds = 1f },
            };
        }

        // ---- SPIRE DUEL: THE EVIDENCE (Beat 10, maintenance hub) ----

        private static DialogueLine[] GetTheEvidenceLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vera Dusk", text = "Your existence is evidence.", seconds = 1.5f },
                new DialogueLine { speaker = "Vera Dusk", text = "Of Kethel-7. Of every site like it. Khall is still running programs, same architecture, same raw material.", seconds = 4f },
                new DialogueLine { speaker = "Vera Dusk", text = "Your failsafe was designed to erase you cleanly. Someone let you survive. That means someone inside the program knows what you are.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "I don't remember refusing the order.", seconds = 1.5f },
                new DialogueLine { speaker = "Vera Dusk", text = "Your body does. I watched it happen.", seconds = 2f },
            };
        }

        // ---- KETHEL-7 MEMORY: DESCENT INTO THE FRAGMENTS (Beat 11 intro) ----

        private static DialogueLine[] GetMemoryDescentLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vera Dusk", text = "You were the ideal. The Ronin Program's design, the blank slate they poured the conditioning into. You were never supposed to break.", seconds = 3.5f },
                new DialogueLine { speaker = "Vera Dusk", text = "You were CIPHER. Number seven. And they built you to be permanent.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Easy.", seconds = 1f },
            };
        }

        // ---- KETHEL-7 MEMORY: CHILD ECHOES (memory-space vignettes) ----

        private static DialogueLine[] GetMemoryEchoesLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Child Echo 1", text = "Hide. Quick, inside the wall.", seconds = 2f },
                new DialogueLine { speaker = "Child Echo 2", text = "Where's Kira? She was right here.", seconds = 2f },
                new DialogueLine { speaker = "Child Echo 1", text = "Don't make a sound. Don't breathe.", seconds = 2f },
            };
        }

        // ---- KETHEL-7 MEMORY: THE REFUSAL SURFACES (Beat 11 fragments) ----

        private static DialogueLine[] GetMemoryRefusalLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "I see the doorframe. I see the fire. I hear the order.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "And then my hand opens.", seconds = 1.5f },
                new DialogueLine { speaker = "Vera Dusk", text = "That's the refusal.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Then there's nothing. Just white. Just the ground.", seconds = 2f },
                new DialogueLine { speaker = "Vera Dusk", text = "The failsafe dropped you before the soldiers came in. You never saw what happened after you refused.", seconds = 3f },
            };
        }

        // ---- KETHEL-7 MEMORY: KIRA (Beat 14 — the name) ----

        private static DialogueLine[] GetKiraNameLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vera Dusk", text = "The bounty on your head is Khall's work. He placed it personally. He's afraid of you, and fear is the only weapon that costs him something.", seconds = 4f },
                new DialogueLine { speaker = "Vera Dusk", text = "My sister's name was Kira. She was nine years old.", seconds = 2.5f },
                new DialogueLine { speaker = "Vera Dusk", text = "When you find Khall, say it to him. Make him know what he destroyed.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "Kira.", seconds = 1f },
            };
        }

        // ---- CINDERS REFINERY: BREACHING THE FACILITY (Beat 20) ----

        private static DialogueLine[] GetCindersBreachLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vera Dusk", text = "Command chamber. Straight through, third junction, up one level.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "How bad is the shoulder?", seconds = 1f },
                new DialogueLine { speaker = "Vera Dusk", text = "Fine.", seconds = 0.5f },
                new DialogueLine { speaker = "Kessler", text = "I'm holding the landing pad. Make it count.", seconds = 1.5f },
            };
        }

        // ---- CINDERS REFINERY: KHALL OVER FACILITY COMMS (Beat 21 — escapes by pod) ----

        private static DialogueLine[] GetKhallReckoningLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vera Dusk", text = "This is the man who ordered Kethel-7. Who built instruments out of children and called it necessity.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "My masterpiece.", seconds = 1.5f },
                new DialogueLine { speaker = "Khall", text = "Still broken in all the right places.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "You want your name back? Ask the Fringe what they called you, if they even know.", seconds = 3f },
                new DialogueLine { speaker = "Vera Dusk", text = "The pod's away. He's gone.", seconds = 1.5f },
            };
        }

        // ---- CINDERS REFINERY: DOMINION SCOUT WAVE BARKS ----

        private static DialogueLine[] GetCindersScoutsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Scout 2", text = "Rogue asset on site! All squads converge!", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Scout 3", text = "The bounty hunter is with him! Lethal engagement cleared!", seconds = 2f },
                new DialogueLine { speaker = "Vera Dusk", text = "On your left. I'll take the flank.", seconds = 1.5f },
            };
        }

        // ---- CINDERS REFINERY: THROUGH FIRE (Beat 22) ----

        private static DialogueLine[] GetCindersEscapeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "I'm on the pad. East wing is going, come out now.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Kira.", seconds = 1.5f },
            };
        }

        // ---- EXTRACTION: THE DOMINION PICKET (Beats 12/15 condensed) ----

        private static DialogueLine[] GetExtractionPicketLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Three contacts! They had our bearing before we lifted, someone sold them our approach vector.", seconds = 2.5f },
                new DialogueLine { speaker = "Vera Dusk", text = "Target one is in my reticle. Say when.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "When.", seconds = 0.5f },
                new DialogueLine { speaker = "Kessler", text = "That was my port array.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Lane's clear.", seconds = 1f },
            };
        }

        // ---- EXTRACTION: ASCENT EPILOGUE (Beat 23) ----

        private static DialogueLine[] GetAscentEpilogueLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "I refused the order. I remember it now. I know what I did and what I failed to stop.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "But I still don't know the name I had before they built this. Before any of it.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "She's stable. Again.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "She came here to kill me and she fought beside me instead.", seconds = 2f },
                new DialogueLine { speaker = "Mera Voss", text = "That's what the Architect of Mercy does. He makes it possible for people to choose something other than what they came for.", seconds = 3f },
            };
        }

        // ---- GALAXY 2 HUB CABIN: POST-MISSION BRIEFING (EP10 hook) ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vera Dusk", text = "Before I went under, I pulled one more thread from the stolen records. A sanctuary on Velloch's Reach, survivors who hid from the program's first sites.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "If anyone kept names the Dominion erased, it's the people who hid from it.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "Set course for the Reach.", seconds = 1.5f },
            };
        }
    }
}
