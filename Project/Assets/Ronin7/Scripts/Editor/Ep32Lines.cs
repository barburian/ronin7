using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 32 dialogue data. Transposed from ep32-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep32_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 32 "The Throne of Ashes" — Galaxy 4 closer. Soren storms the Dominion fortress,
    /// confronts Khall, learns the Kethel-7 kill-order was forged by the Hollow Kings—and that
    /// the true enemy is First Overseer Maelgorn and the Obsidian Synod. Khall repents and stays
    /// behind to dismantle the fortress. Soren calls in the ten allies and assembles a war council
    /// to face the Synod. The fate of four galaxies turns on one name: Soren.
    /// </summary>
    public static class Ep32Lines
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
            "breach_approach",
            "breach_dogfight",
            "breach_cleared",
            "vale_pa",
            "hangar_engage",
            "vale_aftermath",
            "vault_history",
            "vault_combat",
            "khall_recording",
            "threne_offer",
            "choir_engage",
            "choir_aftermath",
            "throne_confront",
            "throne_engage",
            "maelgorn_reveal",
            "exodus_dock",
            "blockade_run",
            "threshold_call",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "breach_approach" => GetBreachApproachLines(),
                "breach_dogfight" => GetBreachDogfightLines(),
                "breach_cleared" => GetBreachClearedLines(),
                "vale_pa" => GetValePaLines(),
                "hangar_engage" => GetHangarEngageLines(),
                "vale_aftermath" => GetValeAftermathLines(),
                "vault_history" => GetVaultHistoryLines(),
                "vault_combat" => GetVaultCombatLines(),
                "khall_recording" => GetKhallRecordingLines(),
                "threne_offer" => GetThreneOfferLines(),
                "choir_engage" => GetChoirEngageLines(),
                "choir_aftermath" => GetChoirAftermathLines(),
                "throne_confront" => GetThroneConfrontLines(),
                "throne_engage" => GetThroneEngageLines(),
                "maelgorn_reveal" => GetMaelgornRevealLines(),
                "exodus_dock" => GetExodusDockLines(),
                "blockade_run" => GetBlockadeRunLines(),
                "threshold_call" => GetThresholdCallLines(),
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

        /// <summary>Generate clip name for a line: ep32_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep32_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ==== BEAT 1 — THE BREACH ====

        private static DialogueLine[] GetBreachApproachLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Last chance to turn back. You've got a name now, Soren. People with names have things to lose.", seconds = 3f },
                new DialogueLine { speaker = "Soren", text = "Then I'll lose them here, or I'll know why I almost did. Khall is in that tower. He knows what Kethel-7 really was.", seconds = 3.5f },
            };
        }

        private static DialogueLine[] GetBreachDogfightLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Pilot 1", text = "Interceptors locked! Firing solution active!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Evasive! Soren, gun rig, now!", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Pilot 2", text = "Lead element destroyed! Redistributing fire!", seconds = 1.5f },
                new DialogueLine { speaker = "Iris", text = "Secondary turret is online! Flanker at nine o'clock!", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Hold approach! Fortress outer dock is dilating!", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetBreachClearedLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Automated Fortress Voice", text = "Clearance confirmed. Operative Ronin-Seven. Overseer Khall awaits your arrival.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "They opened the door. That is not a welcome. That is a spring trap.", seconds = 2.5f },
                new DialogueLine { speaker = "Soren", text = "Then let's see what it's built to catch.", seconds = 1.5f },
            };
        }

        // ==== BEAT 2 — THE GATE-KEEPERS ====

        private static DialogueLine[] GetValePaLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Commander Vale", text = "Khall sent me to retrieve you. Or confirm you. Dominion doesn't say which.", seconds = 3f },
                new DialogueLine { speaker = "Soren", text = "Vale. You remember the orphanage.", seconds = 2f },
                new DialogueLine { speaker = "Commander Vale", text = "I remember what we were told happened. After you woke me out of the ice at Thermopause, I walked back in through the front door, old rank buys a great deal of blindness. And I stopped trusting the source.", seconds = 4f },
            };
        }

        private static DialogueLine[] GetHangarEngageLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Soldier", text = "Target engaged! Secure the hangar!", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Soldier 2", text = "Units three through seven are compromised! Falling back to secondary...", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetValeAftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Commander Vale", text = "You haven't forgotten how to bury family. Khall knew you wouldn't. Come. He wants you to see the Vault before you see him.", seconds = 3.5f },
            };
        }

        // ==== BEAT 3 — THE NURTURE VAULT ====

        private static DialogueLine[] GetVaultHistoryLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Commander Vale", text = "They took us as children. Stripped the names, installed the numbers. I was Operative Two. You were the anomaly, best instinct ever recorded, but you felt things. Khall ran interference when the handlers wanted to cut that out of you.", seconds = 5f },
                new DialogueLine { speaker = "Soren", text = "Then Kethel-7...", seconds = 1.5f },
                new DialogueLine { speaker = "Commander Vale", text = "Something never fit about that order. The cipher was Dominion-standard but the routing tag ran one protocol cycle ahead of what we were using at the time. A forgery with Hollow Kings access could clear that gap.", seconds = 4.5f },
            };
        }

        private static DialogueLine[] GetVaultCombatLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Commander Vale", text = "Coverage above! Moving to the east catwalk!", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetKhallRecordingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Commander Vale", text = "Khall left it. He has been leaving it there for years, I think. Like a confession waiting to be heard.", seconds = 3.5f },
                new DialogueLine { speaker = "Khall (Recording)", text = "If you found this, you survived what I did to you, and I owe you the accounting. There was no massacre order from me, only the one they made you believe came from me.", seconds = 4.5f },
                new DialogueLine { speaker = "Khall (Recording)", text = "The Hollow Kings forged it in Dominion cipher. I discovered the forgery six days too late. By then you had already been purged.", seconds = 3.5f },
                new DialogueLine { speaker = "Khall (Recording)", text = "The only move I had left was to make the Dominion believe the purge held. Faking your death was the only gift I could still give you.", seconds = 4f },
            };
        }

        // ==== BEAT 4 — THE PALE CHOIR'S PLAY ====

        private static DialogueLine[] GetThreneOfferLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Revenant Threne", text = "Dominion, your lower levels are ours. We have sealed the manufacturing decks and the creche sublevel chambers.", seconds = 3.5f },
                new DialogueLine { speaker = "Revenant Threne", text = "Operative Seven, your augmentations carry the resonance key to the failsafe network. Surrender yourself, and the children in sublevel creches leave breathing.", seconds = 4f },
                new DialogueLine { speaker = "Commander Vale", text = "The network codes. They want Khall's override architecture.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "No. They want me to choose between myself and innocence. Again. I already know what that choice costs.", seconds = 3.5f },
            };
        }

        private static DialogueLine[] GetChoirEngageLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Choir Operative 1", text = "Surrender brings peace.", seconds = 1.5f },
                new DialogueLine { speaker = "Choir Operative 2", text = "We sing for all you've taken. Join us in the absolution.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetChoirAftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Revenant Threne", text = "Choose, Operative.", seconds = 1.5f },
                new DialogueLine { speaker = "Soren", text = "Vale. The children. Are they safe?", seconds = 1.5f },
                new DialogueLine { speaker = "Commander Vale", text = "Khall moved them twenty minutes ago. They're in orbital sanctuary. Threne is bluffing.", seconds = 2.5f },
                new DialogueLine { speaker = "Soren", text = "Then Threne has nothing.", seconds = 1f },
            };
        }

        // ==== BEAT 5 — THE DESCENDING KING ====

        private static DialogueLine[] GetThroneConfrontLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Soren", text = "I heard the recording.", seconds = 1.5f },
                new DialogueLine { speaker = "Khall", text = "Then you heard what I owed you since Kethel-7. The Hollow Kings forged the order. I was too certain of my own intelligence to question a transmission that fit too perfectly. Until the bodies were already supposed to be cooling.", seconds = 5f },
                new DialogueLine { speaker = "Khall", text = "I faked your death to pull you out of Dominion reach. I told myself it was mercy. It was also cowardice. I could not face what my credulity had nearly cost you.", seconds = 4f },
                new DialogueLine { speaker = "Soren", text = "Who else knew?", seconds = 1f },
                new DialogueLine { speaker = "Khall", text = "That is the question that should have led you here faster. Because it leads somewhere I cannot follow you.", seconds = 3f },
            };
        }

        private static DialogueLine[] GetThroneEngageLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Rogue Officer 1", text = "Stand down, Overseer. Protocol demands containment.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetMaelgornRevealLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "There is an authority above the Dominion that the Dominion itself does not name aloud. First Overseer Maelgorn. The Obsidian Synod.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "They built the failsafe network before you were born. Every operative across four galaxies is still leashed to it, all except you. You burned your own leash out of the Sepulcher and the loop.", seconds = 4f },
                new DialogueLine { speaker = "Khall", text = "Maelgorn ordered the Hollow Kings to fracture the Ronin Program the moment it began producing operatives who felt loyalty above obedience. Your refusal at Kethel-7 was the proof of concept they needed eliminated.", seconds = 4f },
                new DialogueLine { speaker = "Soren", text = "And now?", seconds = 1f },
                new DialogueLine { speaker = "Khall", text = "Now you are the only loose piece on the board. Maelgorn will come for you, and for everyone standing beside you.", seconds = 3.5f },
            };
        }

        // ==== BEAT 6 — THE EXODUS ====

        private static DialogueLine[] GetExodusDockLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Commander Vale", text = "I am not staying. The Choir, the Hollow Kings, whatever the Synod sends next, they will strip this place clean inside a week.", seconds = 3.5f },
                new DialogueLine { speaker = "Iris", text = "We need her.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Another sword. Another reason to run.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "Come with us.", seconds = 1.5f },
                new DialogueLine { speaker = "Khall", text = "My place is here, dismantling what I built, piece by piece, giving every soldier the choice we never had.", seconds = 3.5f },
                new DialogueLine { speaker = "Khall", text = "Go. Finish what I could not start.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetBlockadeRunLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Privateer Captain", text = "Corsair, you are targeted! Lower shields or burn!", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Through them! Maximum burn!", seconds = 1.5f },
            };
        }

        // ==== BEAT 7 — THE THRESHOLD ====

        private static DialogueLine[] GetThresholdCallLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Soren", text = "The Obsidian Synod controls the failsafe network. Every operative across four galaxies is still leashed except me. And except those who can choose.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "That's a war council, not a crew.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "It's both.", seconds = 1f },
                new DialogueLine { speaker = "Iris", text = "They're coming. All of them. Confirmations still arriving.", seconds = 2f },
                new DialogueLine { speaker = "Iris", text = "And an eleventh signal, riding under Cassie's band. Tara. She says the survivor network is seventeen strong and standing by, and that you still owe her a war.", seconds = 4f },
                new DialogueLine { speaker = "Soren", text = "Tell her the debt stands. Her roster fights with us.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "One sword cannot take the Synod. Ten might. Let's go find out.", seconds = 2.5f },
            };
        }
    }
}
