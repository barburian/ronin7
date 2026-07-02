using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 20 dialogue data. Transposed from ep20-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep20_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 20 "Vendor of Ghosts" unfolds at Varek Bazaar, where Vess recognizes Cipher as the operative who killed her brother Kess
    /// six years prior. Through a confrontation with archived letters and the choice to stand against the Dominion rather than pursue vengeance,
    /// Vess becomes Cipher's eighth ally. The episode features a desperate vault heist, Dominion interdiction, and Vess's commitment to dismantle
    /// the shadow network that destroyed her family—ultimately proving that the choice to act together is heavier than the weight of hate.
    /// </summary>
    public static class Ep20Lines
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
            "docking_chaos",
            "docking_barks",
            "recognition",
            "lotus_barks",
            "graveyard",
            "vault_descent",
            "drone_barks",
            "numbered_weapons",
            "syndicate_barks",
            "interdiction",
            "merchants_choice",
            "elite_barks",
            "names_are_archive",
            "kess_message",
            "space_ep20_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "docking_chaos" => GetDockingChaosLines(),
                "docking_barks" => GetDockingBarksLines(),
                "recognition" => GetRecognitionLines(),
                "lotus_barks" => GetLotusBarksLines(),
                "graveyard" => GetGraveyardLines(),
                "vault_descent" => GetVaultDescentLines(),
                "drone_barks" => GetDroneBarksLines(),
                "numbered_weapons" => GetNumberedWeaponsLines(),
                "syndicate_barks" => GetSyndicateBarksLines(),
                "interdiction" => GetInterdictionLines(),
                "merchants_choice" => GetMerchantsChoiceLines(),
                "elite_barks" => GetEliteBarksLines(),
                "names_are_archive" => GetNamesAreArchiveLines(),
                "kess_message" => GetKessMessageLines(),
                "space_ep20_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep20_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep20_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- DOCKING CHAOS (Beat 1) ----

        private static DialogueLine[] GetDockingChaosLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vess", text = "Emberhand lost the station two days ago. Syndicates stopped pretending. Everyone here dies by nightfall.", seconds = 3f },
                new DialogueLine { speaker = "Vess", text = "I need someone who moves through violence like it's a language.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "That's him. He's what you need.", seconds = 1.5f },
            };
        }

        // ---- DOCKING BARKS (Beat 2) ----

        private static DialogueLine[] GetDockingBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tide Baron Squad Lead", text = "Unidentified freighter! Transmit registry or we fire!", seconds = 2f },
                new DialogueLine { speaker = "Rustfang Enforcer", text = "Corvette's open! Grab the reactor coolant and move!", seconds = 1.5f },
                new DialogueLine { speaker = "Ninefold Bank Commander", text = "First squad down! Seal the airlock! Contain the...", seconds = 1.5f },
            };
        }

        // ---- RECOGNITION (Beat 3) ----

        private static DialogueLine[] GetRecognitionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vess", text = "You.", seconds = 1f },
                new DialogueLine { speaker = "Vess", text = "Six years ago you came with Dominion credentials. Said you were hunting Saffron Veil operatives.", seconds = 3f },
                new DialogueLine { speaker = "Vess", text = "You killed seventeen people. You killed my brother.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "I don't remember them.", seconds = 2f },
                new DialogueLine { speaker = "Vess", text = "His name was Kess. He wanted to train as a broker. I was saving to buy his freedom from the syndicate before you arrived.", seconds = 3f },
                new DialogueLine { speaker = "Vess", text = "You took that. You took him.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "The Dominion took your brother. I was the tool it used.", seconds = 2.5f },
                new DialogueLine { speaker = "Vess", text = "The weapon. The order. The intention. All the same.", seconds = 2f },
            };
        }

        // ---- LOTUS BARKS (Beat 4) ----

        private static DialogueLine[] GetLotusBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Crimson Lotus Commander", text = "Targets acquired! Sweeping formation! Move!", seconds = 1.5f },
                new DialogueLine { speaker = "Crimson Lotus Enforcer 1", text = "Neural-disruptor ready! Firing!", seconds = 1f },
                new DialogueLine { speaker = "Crimson Lotus Enforcer 2", text = "Third operative down! Fallback formation!", seconds = 1.5f },
                new DialogueLine { speaker = "Vess", text = "Sector seven seal, now!", seconds = 1f },
                new DialogueLine { speaker = "Crimson Lotus Enforcer 3", text = "Losing integrity! Fall back! Move out!", seconds = 1.5f },
            };
        }

        // ---- GRAVEYARD (Beat 5) ----

        private static DialogueLine[] GetGraveyardLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vess", text = "The stockpile here isn't just black-market goods. Dominion overflow. Ronin Program supply chains. Never catalogued. Deliberately buried.", seconds = 4f },
                new DialogueLine { speaker = "Vess", text = "Dominion is coming back for it. When they do, witnesses get eliminated. That includes her. That includes you.", seconds = 3f },
            };
        }

        // ---- VAULT DESCENT (Beat 6) ----

        private static DialogueLine[] GetVaultDescentLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vess", text = "His name was Kess. He wrote letters before he died. Personal reflections. Hopes about what broker training would teach him. All of it stored here.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "I don't remember him. I don't remember the choice that led me there.", seconds = 2.5f },
                new DialogueLine { speaker = "Vess", text = "But the choice was made.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Yes.", seconds = 1f },
                new DialogueLine { speaker = "Dominion Drone Network", text = "OPERATIVE DESIGNATION CIPHER DETECTED. NEUTRALIZATION PROTOCOL ENGAGED.", seconds = 2f },
            };
        }

        // ---- DRONE BARKS (Beat 7) ----

        private static DialogueLine[] GetDroneBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Drone 1", text = "Target lock. Engaging kinetic fire.", seconds = 1f },
                new DialogueLine { speaker = "Dominion Drone 2", text = "Secondary target acquired. Neutralizing.", seconds = 1f },
                new DialogueLine { speaker = "Vess", text = "East passage is clear! Move through!", seconds = 1f },
            };
        }

        // ---- NUMBERED WEAPONS (Beat 8) ----

        private static DialogueLine[] GetNumberedWeaponsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "You want to tell me why Vess didn't kill you in that vault.", seconds = 2.5f },
                new DialogueLine { speaker = "Vess", text = "Because the man who killed Kess was a weapon following orders. The man standing here is something different.", seconds = 3f },
                new DialogueLine { speaker = "Vess", text = "Something that can choose. That terrifies me more than any operative ever did.", seconds = 2.5f },
                new DialogueLine { speaker = "Vess", text = "But I will take the man who chooses over the machine that obeyed.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "If I had died when the Dominion meant me to, your brother would still be dead. But you wouldn't be standing next to his ghost.", seconds = 3.5f },
            };
        }

        // ---- SYNDICATE BARKS (Beat 9) ----

        private static DialogueLine[] GetSyndicateBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tide Baron Squadron Leader", text = "All teams converging on the vault! Eliminate anything that moves!", seconds = 1.5f },
                new DialogueLine { speaker = "Rustfang Pit Commander", text = "Frontline assault! Push through their formation! Move!", seconds = 1.5f },
                new DialogueLine { speaker = "Hollow Kings Infiltrator", text = "Neural-static deployed! Targeting systems going dark!", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "East corridor clear! Cache is moving! Stay with me!", seconds = 1.5f },
                new DialogueLine { speaker = "Vess", text = "Vault breach in sector three! Moving the core!", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "Five seconds to corridor seal! Move faster!", seconds = 1f },
            };
        }

        // ---- INTERDICTION (Beat 10) ----

        private static DialogueLine[] GetInterdictionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "Emberhand is under interdiction. Non-Dominion forces will disperse immediately.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "Resistance will be met with orbital bombardment. This is not a negotiation.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "Ronin-7. Your presence has been detected. Command offers full memory restoration. Surrender immediately.", seconds = 2.5f },
                new DialogueLine { speaker = "Vess", text = "They don't want you dead. They want you back. If you return with the records, everyone on this bazaar dies to keep them classified.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "All satellite uplinks live! Records are scattered! Can't consolidate them without a full facility!", seconds = 2.5f },
                new DialogueLine { speaker = "Vess", text = "Server three is live! Moving to server five! Cipher, buy us two more minutes!", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Two minutes. I've got them.", seconds = 1f },
                new DialogueLine { speaker = "Khall", text = "The freighter is mine. Cipher, your capture is assured. Resistance will not extend your timeline.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "Dorsal turret active! Khall's signature locked!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Evasive maneuvers! Jump coordinates locking! Sixty seconds to jump distance!", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "You postpone your extinction, Cipher. Nothing more.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Jump in ten! Nine! Eight!", seconds = 2f },
            };
        }

        // ---- MERCHANT'S CHOICE (Beat 11) ----

        private static DialogueLine[] GetMerchantsChoiceLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vess", text = "I spent six years hating you. Waiting for you to die so I could stop carrying it.", seconds = 3f },
                new DialogueLine { speaker = "Vess", text = "Having you here, alive, broken, choosing, I finally understand what Kess wanted out of this life for.", seconds = 3f },
                new DialogueLine { speaker = "Vess", text = "Carrying hate is heavier than any weapon I've ever sold. I'm done carrying it.", seconds = 2f },
                new DialogueLine { speaker = "Vess", text = "Kess's last recorded message. Made the night before your mission. Before you arrived.", seconds = 2.5f },
                new DialogueLine { speaker = "Vess", text = "I'm coming with you. As long as you move against the Dominion. Not for vengeance. For what Kess deserved to live long enough to see.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "You understand the Dominion will hunt you now.", seconds = 2f },
                new DialogueLine { speaker = "Vess", text = "I understand.", seconds = 1f },
            };
        }

        // ---- ELITE BARKS (Beat 12) ----

        private static DialogueLine[] GetEliteBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Elite 1", text = "Targets acquired. Eliminate on contact. Secure the records.", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Elite 2", text = "Formation delta. Moving in.", seconds = 1f },
                new DialogueLine { speaker = "Vess", text = "First operative down! Moving!", seconds = 1f },
                new DialogueLine { speaker = "Dominion Elite 3", text = "Operative is adapting! Formation is compromised!", seconds = 1.5f },
                new DialogueLine { speaker = "Vess", text = "Go. The records are in the satellite network. Safe.", seconds = 1.5f },
            };
        }

        // ---- NAMES ARE ARCHIVE (Beat 13) ----

        private static DialogueLine[] GetNamesAreArchiveLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "I killed seventeen people. I do not remember their faces.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "But now their names are in every server I touch. Their brothers. Their children. The Dominion wanted me to forget them.", seconds = 3f },
                new DialogueLine { speaker = "Vess", text = "That is not a curse.", seconds = 1f },
                new DialogueLine { speaker = "Vess", text = "That is a choice.", seconds = 1f },
            };
        }

        // ---- KESS'S MESSAGE (Beat 14) ----

        private static DialogueLine[] GetKessMessageLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kess", text = "If you're hearing this, Vess, I'm probably dead.", seconds = 2f },
                new DialogueLine { speaker = "Kess", text = "I was never a criminal. I was a hostage. And there are thousands like me.", seconds = 2.5f },
                new DialogueLine { speaker = "Kess", text = "Maybe someday someone will care enough to count us all.", seconds = 2f },
            };
        }

        // ---- GALAXY 4 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Vess is with us now. The bazaar burned, but the records are scattered across a hundred servers, beyond the Dominion's reach.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "One more name on the manifest. One more reason they can't make us disappear quietly.", seconds = 2.5f },
            };
        }
    }
}
