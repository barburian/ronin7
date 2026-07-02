using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 19 dialogue data. Transposed from ep19-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep19_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 19 spans the Rust Meridian debt-colony—Cipher's discovery of proof of his existence in the Ninefold Bank's
    /// quantum ledger, the liberation of operatives from erasure, Tara's revelation as a sleeper agent, and the rescue of
    /// a dying operative who becomes Cassie-04. The episode culminates with seventeen surviving operatives identified across
    /// the outer rim, a commitment to dismantle the Dominion's shadow-network infrastructure, and Cipher's choice to reclaim
    /// himself.
    /// </summary>
    public static class Ep19Lines
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
            "ghost_name",
            "pursuit_barks",
            "mine_descent",
            "drone_barks",
            "ghost_memories",
            "vault_door",
            "vault_barks",
            "purged_entry",
            "hammer_clause",
            "guard_barks",
            "tower_crown",
            "signal_remnant",
            "duel_silence",
            "debt_paid",
            "operatives_name",
            "weight_of_names",
            "choose_myself",
            "final_choice",
            "space_ep19_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "ghost_name" => GetGhostNameLines(),
                "pursuit_barks" => GetPursuitBarksLines(),
                "mine_descent" => GetMineDescentLines(),
                "drone_barks" => GetDroneBarksLines(),
                "ghost_memories" => GetGhostMemoriesLines(),
                "vault_door" => GetVaultDoorLines(),
                "vault_barks" => GetVaultBarksLines(),
                "purged_entry" => GetPurgedEntryLines(),
                "hammer_clause" => GetHammerClauseLines(),
                "guard_barks" => GetGuardBarksLines(),
                "tower_crown" => GetTowerCrownLines(),
                "signal_remnant" => GetSignalRemnantLines(),
                "duel_silence" => GetDuelSilenceLines(),
                "debt_paid" => GetDebtPaidLines(),
                "operatives_name" => GetOperativesNameLines(),
                "weight_of_names" => GetWeightOfNamesLines(),
                "choose_myself" => GetChooseMyselfLines(),
                "final_choice" => GetFinalChoiceLines(),
                "space_ep19_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep19_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep19_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- THE WEIGHT OF A GHOST NAME (Beat 1) ----

        private static DialogueLine[] GetGhostNameLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tara", text = "Three days out of the Drowning Deep. That's enough distance to start breathing like this isn't borrowed time.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "You move like someone trained for something other than debt-labor.", seconds = 2f },
                new DialogueLine { speaker = "Tara", text = "Because I was. Eight years in a debt-colony. Before that, seven years as an auditor trying to trace the Bank's ledger.", seconds = 3f },
                new DialogueLine { speaker = "Tara", text = "The Ninefold Bank compounds every obligation across generations. A child is born owing what their parents were never paid.", seconds = 3.5f },
                new DialogueLine { speaker = "Tara", text = "The Bank's quantum ledger. Every transaction. Every name the Bank has certified as \"erased.\"", seconds = 3f },
                new DialogueLine { speaker = "Tara", text = "Including the Dominion's own operatives. The ones they swore didn't exist.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "If the Bank has that ledger, then my erasure is written in their own hand. Documented. Real.", seconds = 3f },
            };
        }

        // ---- PURSUIT THROUGH THE ASTEROIDS (Beat 2) ----

        private static DialogueLine[] GetPursuitBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Patrol Commander", text = "Unidentified freighter, reduce speed and transmit registry. You are entering restricted orbital space.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Cipher to the turret. We're about to have guests we didn't invite.", seconds = 2f },
                new DialogueLine { speaker = "Pursuit Fighter 2", text = "Dorsal gun is hot! Evasive! Evasive!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Clean shot. Third fighter is breaking pursuit. We're clear.", seconds = 2.5f },
            };
        }

        // ---- THE MINE SHAFT DESCENT (Beat 3a) ----

        private static DialogueLine[] GetMineDescentLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tara", text = "The archive serves two functions. It records debts that compound across generations, obligations inherited from parents who never contracted them.", seconds = 4f },
                new DialogueLine { speaker = "Tara", text = "Alongside the debt ledger, it records erasures. Names of people the Dominion certified as non-existent.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "Why would the Bank record something the Dominion swore didn't exist?", seconds = 2f },
                new DialogueLine { speaker = "Tara", text = "Because the Dominion paid them to. The Bank certified the erasures. Made them official. Permanent record.", seconds = 3f },
            };
        }

        // ---- THERMAL DRONE WAVE (Beat 3) ----

        private static DialogueLine[] GetDroneBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Bank Guard 1", text = "Intruders in sector seven! Shutdown protocol engaged!", seconds = 1.5f },
                new DialogueLine { speaker = "Bank Guard 2", text = "Sector seven thermal signature terminated!", seconds = 1.5f },
            };
        }

        // ---- GHOST MEMORIES (Beat 3b) ----

        private static DialogueLine[] GetGhostMemoriesLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Ghost-memories live in my hands. Training. Kill-counts. A handler's voice giving orders. I cannot place the voice.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "It's like the training knows the path the body should take. But the mind that learned it is gone.", seconds = 3f },
                new DialogueLine { speaker = "Tara", text = "Your body remembers what your mind was stripped of.", seconds = 2f },
                new DialogueLine { speaker = "Tara", text = "That makes you twice the weapon. They taught you twice.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Are you asking if I'm willing to die reaching that archive?", seconds = 2f },
                new DialogueLine { speaker = "Tara", text = "Yes.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "More than willing. Desperate.", seconds = 1.5f },
            };
        }

        // ---- THE VAULT BELOW (Beat 4a) ----

        private static DialogueLine[] GetVaultDoorLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tara", text = "Before I went into the debt-rolls, I was thorough. I took insurance.", seconds = 2.5f },
                new DialogueLine { speaker = "Tara", text = "The lock requires both. Neither alone will open the vault. But together...", seconds = 2.5f },
                new DialogueLine { speaker = "Tara", text = "Before the door fully opens I need to ask you something.", seconds = 2f },
                new DialogueLine { speaker = "Tara", text = "If the archive shows you were a real person before the program, before the Dominion swore you didn't exist, what will you do with that knowledge?", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "I'll make sure they can't say I never was.", seconds = 2.5f },
            };
        }

        // ---- VAULT OPERATIVE WAVE (Beat 4) ----

        private static DialogueLine[] GetVaultBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vault Operative Commander", text = "Intruders are detained. Lockdown protocol is active. Reinforcements are nine minutes out.", seconds = 2.5f },
                new DialogueLine { speaker = "Vault Operative", text = "Seven?", seconds = 1f },
                new DialogueLine { speaker = "Vault Operative Commander", text = "Suppress that. Suppress full neural stack.", seconds = 1.5f },
            };
        }

        // ---- PURGED ENTRY (Beat 4b) ----

        private static DialogueLine[] GetPurgedEntryLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "I'm here. Written in the Bank's own hand.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "The Dominion declared me gone and then wrote down the lie for the Bank to witness. To be certain it stuck.", seconds = 3.5f },
            };
        }

        // ---- THE HAMMER CLAUSE (Beat 5) ----

        private static DialogueLine[] GetHammerClauseLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tara", text = "The archive holds the failsafe specifications. Every operative carries a neural purge device networked to central authority.", seconds = 3.5f },
                new DialogueLine { speaker = "Tara", text = "Your chip fired, but the purge stalled. Never confirmed complete. You were never certified dead.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "We burn this. We burn it all.", seconds = 1.5f },
                new DialogueLine { speaker = "Tara", text = "If the Bank loses this archive, they lose leverage over forty thousand debtors in the colony.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "If the Bank loses this ledger, it loses the proof of how many they destroyed for profit. We copy what matters. Then we burn the rest.", seconds = 3.5f },
            };
        }

        // ---- TOWER ASCENT (Beat 5 barks) ----

        private static DialogueLine[] GetGuardBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Cipher, you have ten minutes before the tower's main defenses activate. Colony guards are converging on the vault entrance.", seconds = 3f },
            };
        }

        // ---- ASH AND VOID (Beat 6) ----

        private static DialogueLine[] GetTowerCrownLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tara", text = "I need to tell you the truth about myself, Cipher. I was never a supervisor. I was a Dominion sleeper, not a weapon, a watcher, embedded in the colony for seven years.", seconds = 3.5f },
                new DialogueLine { speaker = "Tara", text = "Six months ago, the Dominion stopped running the program. They forgot everyone in it. So I activated myself.", seconds = 2.5f },
                new DialogueLine { speaker = "Tara", text = "These are surviving operatives. Scattered across the outer rim. Seventeen are still alive enough to matter.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Bay doors are open! Fifty seconds to full collapse!", seconds = 2f },
                new DialogueLine { speaker = "Tara", text = "The Dominion will come for all of us now. We've made ourselves impossible to ignore.", seconds = 2.5f },
                new DialogueLine { speaker = "Tara", text = "Welcome back to the war.", seconds = 1.5f },
            };
        }

        // ---- SIGNAL REMNANT (Beat 7) ----

        private static DialogueLine[] GetSignalRemnantLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Signal", text = "OPERATIVE-07 RECOGNIZED. REQUEST EXTRACTION. AWAITING ORDERS.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "The operative from the vault. The one who said my designation before her commander silenced her.", seconds = 2.5f },
                new DialogueLine { speaker = "Tara", text = "She's still transmitting, but her chip is degrading. The neural purge is activating. When it finishes, she'll stop transmitting permanently.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "She was just following orders.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "I'm changing course.", seconds = 1.5f },
            };
        }

        // ---- THE DANCE IN DARK CORRIDORS (Beat 8) ----

        private static DialogueLine[] GetDuelSilenceLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "I'm not your enemy.", seconds = 1.5f },
            };
        }

        // ---- OPERATIVE'S JUDGMENT (Beat 8b) ----

        private static DialogueLine[] GetDebtPaidLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tara", text = "Your debt is paid.", seconds = 1.5f },
            };
        }

        // ---- THE OPERATIVE'S NAME (Beat 9) ----

        private static DialogueLine[] GetOperativesNameLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Operative", text = "I don't know my real name.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Then choose one.", seconds = 1.5f },
                new DialogueLine { speaker = "Operative", text = "The Vault operatives wore numbers. I was number four in my cohort. The last survivor.", seconds = 2.5f },
                new DialogueLine { speaker = "Operative", text = "Cassie. I choose Cassie.", seconds = 1f },
                new DialogueLine { speaker = "Cassie-04", text = "Eight years in the Bank's vault rotation, running orders off a dead Dominion channel. They handed my cohort to the Bank as collateral and forgot us.", seconds = 3.5f },
                new DialogueLine { speaker = "Cassie-04", text = "I don't know what choices look like when no one's giving orders.", seconds = 2.5f },
                new DialogueLine { speaker = "Cassie-04", text = "Will you teach me?", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Yes.", seconds = 1f },
            };
        }

        // ---- THE WEIGHT OF NAMES (Beat 10) ----

        private static DialogueLine[] GetWeightOfNamesLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tara", text = "The Ninefold Bank was one of ten syndicates. They all serve the Dominion's infrastructure. All of them together form the shadow network that runs the empire.", seconds = 4f },
                new DialogueLine { speaker = "Tara", text = "If the operatives coordinate carefully, moving quietly, they could dismantle the architecture piece by piece.", seconds = 3f },
                new DialogueLine { speaker = "Tara", text = "It will take years. You may not survive the chip still degrading in your skull. But your existence, proved now by the Bank's own irreplaceable archive, witnessed and undeniable, could mean something other than a weapon.", seconds = 4.5f },
            };
        }

        // ---- THE SIGNAL REMNANT (Beat 11) ----

        private static DialogueLine[] GetChooseMyselfLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "I choose myself.", seconds = 1.5f },
            };
        }

        // ---- THE FINAL CHOICE (Beat 12) ----

        private static DialogueLine[] GetFinalChoiceLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tara", text = "The survivors you're freeing will face a choice that cannot be made for them.", seconds = 3f },
                new DialogueLine { speaker = "Tara", text = "Use what the ledger proved to strike back at everything that erased them. Vengeance or alliance, both begin the same way but end in different places.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "They'll have to choose for themselves.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Tell me who else is alive.", seconds = 2f },
                new DialogueLine { speaker = "Tara", text = "We start here. We move carefully. We verify each coordinate. And when we have all seventeen, we call them all home.", seconds = 3.5f },
            };
        }

        // ---- GALAXY 3 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Cassie-04 is with us now. The ledger burned, but the seventeen names survived.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "The Dominion will come looking for all of us. But we're no longer hiding.", seconds = 2.5f },
            };
        }
    }
}
