using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 22 dialogue data. Transposed from ep22-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep22_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 22 "The Void Inheritors" unfolds aboard the derelict Arkship Meridian in the Scythe Nebula.
    /// Cipher breaches the abandoned vessel and discovers the truth at its core: the Dominion seeded frozen human "templates"—
    /// pre-conditioned subjects destined to become operatives—across dozens of colony ships scattered across the galaxy.
    /// Ronin-1 sabotaged the program itself, freeing the templates from programming and disappearing. The Meridian's 143 descendants
    /// have lived free for 200 years in ignorance of what they were meant to become. When Cipher finds Ronin-1's archive and broadcasts
    /// the template coordinates galaxy-wide, she becomes a permanent target—and scatters seeds that will reshape everything.
    /// </summary>
    public static class Ep22Lines
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
            "approach",
            "cargo_barks",
            "recognition",
            "vault_barks",
            "vault_aftermath",
            "children_below",
            "flood_barks",
            "children_aftermath",
            "ronin1_archive",
            "failsafe_barks",
            "younger_chain",
            "ronin12_barks",
            "broadcast",
            "boarding_barks",
            "void_inheritors",
            "stellar_dive_barks",
            "closing",
            "space_ep22_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "approach" => GetApproachLines(),
                "cargo_barks" => GetCargoBarksLines(),
                "recognition" => GetRecognitionLines(),
                "vault_barks" => GetVaultBarksLines(),
                "vault_aftermath" => GetVaultAftermathLines(),
                "children_below" => GetChildrenBelowLines(),
                "flood_barks" => GetFloodBarksLines(),
                "children_aftermath" => GetChildrenAftermathLines(),
                "ronin1_archive" => GetRonin1ArchiveLines(),
                "failsafe_barks" => GetFailsafeBarksLines(),
                "younger_chain" => GetYoungerChainLines(),
                "ronin12_barks" => GetRonin12BarksLines(),
                "broadcast" => GetBroadcastLines(),
                "boarding_barks" => GetBoardingBarksLines(),
                "void_inheritors" => GetVoidInheritorsLines(),
                "stellar_dive_barks" => GetStellarDiveBarksLines(),
                "closing" => GetClosingLines(),
                "space_ep22_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep22_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep22_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- APPROACH ----

        private static DialogueLine[] GetApproachLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "That beacon's been pulsing for days. Too old to be recent. Too structured to be debris.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Dominion signature?", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Maybe. Before your time or after it, I can't say. But yes. Dominion built. Dominion abandoned. The galaxy's full of things they made and then forgot.", seconds = 4f },
                new DialogueLine { speaker = "Irene Sols", text = "If you board, you seal our isolation contract.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "I acknowledge the contract.", seconds = 1.5f },
                new DialogueLine { speaker = "Irene Sols", text = "They built us. Then they forgot us. Whether that was mercy or malice, I have not decided.", seconds = 3f },
            };
        }

        // ---- CARGO BARKS ----

        private static DialogueLine[] GetCargoBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Security Mech 1", text = "Unauthorized contact! Deploying countermeasures!", seconds = 1.5f },
                new DialogueLine { speaker = "Security Mech 2", text = "Thermal signature locked! Engaging!", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Power core is secure. Moving to bridge access.", seconds = 2f },
            };
        }

        // ---- RECOGNITION ----

        private static DialogueLine[] GetRecognitionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Irene Sols", text = "You are one of the Program's operatives.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "Yes.", seconds = 1f },
                new DialogueLine { speaker = "Irene Sols", text = "I thought we might see you one day. We prepared for it in the records.", seconds = 3f },
                new DialogueLine { speaker = "Irene Sols", text = "The Meridian was dispatched 200 years ago. Equipped with pre-conditioned subjects in cryo-stasis. The Dominion's own documentation called them \"templates.\" Biological source material.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Source material for what?", seconds = 1.5f },
                new DialogueLine { speaker = "Irene Sols", text = "For operatives. For weapons. An operative designated Ronin-1 was dispatched to oversee their activation. To review the protocols. To prepare them for programming.", seconds = 4f },
                new DialogueLine { speaker = "Irene Sols", text = "Instead, he chose differently. He wiped the activation data. Freed the subjects from their scheduled programming. Then vanished.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "And the subjects?", seconds = 1f },
                new DialogueLine { speaker = "Irene Sols", text = "They thawed. They woke. They found themselves free in the dark of a ship with no destination. My ancestors grew up around them. We have lived free of the Program ever since. Diminished. Sealed. But free.", seconds = 4.5f },
                new DialogueLine { speaker = "Irene Sols", text = "We were meant to be a thousand. We are 143.", seconds = 2.5f },
            };
        }

        // ---- VAULT BARKS ----

        private static DialogueLine[] GetVaultBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vault Security", text = "Unauthorized biometric intrusion! Lethal authorization active!", seconds = 1.5f },
            };
        }

        // ---- VAULT AFTERMATH ----

        private static DialogueLine[] GetVaultAftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "The ship itself is Dominion architecture.", seconds = 2f },
                new DialogueLine { speaker = "Irene Sols", text = "Yes. Designed by them. Forgotten by them. We are the forgotten design, and so are you.", seconds = 3f },
            };
        }

        // ---- CHILDREN BELOW ----

        private static DialogueLine[] GetChildrenBelowLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Lira", text = "Are you the one Ronin-1's logs promised would come back?", seconds = 2.5f },
                new DialogueLine { speaker = "Irene Sols", text = "No, Lira. He is something else.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "How many of these children are biologically descended from the original subjects?", seconds = 3f },
                new DialogueLine { speaker = "Irene Sols", text = "All of them. The colony has been carefully managed. Eight generations of bloodlines, all descended from the templates. We are the proof that you can break the conditioning. We are the living archive that Ronin-1's choice preserved.", seconds = 5f },
            };
        }

        // ---- FLOOD BARKS ----

        private static DialogueLine[] GetFloodBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Irene Sols", text = "The lower stacks! Pressure cascade! Get the children to the secondary habitats!", seconds = 2.5f },
                new DialogueLine { speaker = "Farm Overseer", text = "Primary seals failing! Water containment compromised!", seconds = 2f },
                new DialogueLine { speaker = "Repair Drone 1", text = "Unauthorized presence! Deploying security protocol!", seconds = 1.5f },
                new DialogueLine { speaker = "Repair Drone 2", text = "Containment failure imminent! Escalating countermeasures!", seconds = 1.5f },
            };
        }

        // ---- CHILDREN AFTERMATH ----

        private static DialogueLine[] GetChildrenAftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Lira", text = "Ronin-1 saved them. Are you saving us?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "No. I'm trying to understand what I am.", seconds = 2.5f },
            };
        }

        // ---- RONIN-1 ARCHIVE ----

        private static DialogueLine[] GetRonin1ArchiveLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-1", text = "If you are hearing this, the Program sent you. I cannot stop what they will do with you.", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-1", text = "But I can tell you what they began with. The templates are out there. Dozens of colony ships dispatched across the galaxy before the first operative was trained.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-1", text = "The subjects are still in cryo. They do not know what was planned for them. I left their coordinates in this archive, sealed against the Dominion. Use it however you decide is right.", seconds = 4f },
                new DialogueLine { speaker = "Irene Sols", text = "What do you believe now?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "I believe Ronin-1 answered the question I didn't know to ask. And I believe I have to answer the one he left open.", seconds = 3.5f },
            };
        }

        // ---- FAILSAFE BARKS ----

        private static DialogueLine[] GetFailsafeBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "No. No. Not happening. Not again.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Hold it. Hold it together. You've beaten this before.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "It's clear. The broadcast can't go out from here.", seconds = 2f },
            };
        }

        // ---- YOUNGER CHAIN ----

        private static DialogueLine[] GetYoungerChainLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "That data is worth leverage. Real leverage. Information this old about the Program's origin could purchase allies. Could buy us time.", seconds = 4f },
                new DialogueLine { speaker = "Irene Sols", text = "Or it should be broadcast openly. Made irretrievable. Let the whole galaxy know what the Dominion did in the dark.", seconds = 3.5f },
                new DialogueLine { speaker = "Lira", text = "What would Ronin-1 have done?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "He already answered that. He left the archive intact and waited for a successor to choose. The question is what this successor decides.", seconds = 3.5f },
            };
        }

        // ---- RONIN-12 BARKS ----

        private static DialogueLine[] GetRonin12BarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-12", text = "Come back with me. It is mercy.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "That is what you have been told mercy looks like.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "Tell Khall what you saw. That the one he is hunting is choosing something other than obedience.", seconds = 3f },
            };
        }

        // ---- BROADCAST ----

        private static DialogueLine[] GetBroadcastLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-12", text = "Overseer Khall is en route. He will reach you before your failsafe finishes.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "Then we broadcast first.", seconds = 1.5f },
                new DialogueLine { speaker = "Irene Sols", text = "The transmission is live. The beacon is holding. They'll pick it up at every relay station within three sectors.", seconds = 3f },
            };
        }

        // ---- BOARDING BARKS ----

        private static DialogueLine[] GetBoardingBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Carrier Commander", text = "All boarding teams, prepare for breach! Suppress the communication array!", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "The broadcast is noted. You have made yourself a permanent target, Cipher. The failsafe is still running. I will reach you before it finishes.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "Reactor corridor clear! Keep transmitting!", seconds = 1.5f },
                new DialogueLine { speaker = "Boarding Team Leader", text = "Target is mobile! Maintaining suppressive fire!", seconds = 1.5f },
                new DialogueLine { speaker = "Khall", text = "The hunters are already moving. There is no amnesty for what you have done here.", seconds = 2f },
            };
        }

        // ---- VOID INHERITORS ----

        private static DialogueLine[] GetVoidInheritorsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Irene Sols", text = "Ronin-1 scattered seeds. You just told the galaxy where they are.", seconds = 2.5f },
                new DialogueLine { speaker = "Lira", text = "Are you leaving?", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Yes. The Meridian was never going to hold me.", seconds = 2f },
            };
        }

        // ---- STELLAR DIVE BARKS ----

        private static DialogueLine[] GetStellarDiveBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Stellar core approaching! You have two minutes to the point of no-return!", seconds = 2.5f },
            };
        }

        // ---- CLOSING ----

        private static DialogueLine[] GetClosingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "What are you thinking?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "The Program is older than either of us knew. Most of it has not been found yet.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Somewhere in the broadcast now spreading across relay stations, dozens of coordinates wait. Colony ships still adrift and silent. Carrying template operatives in cryo-stasis. Pre-conditioned human beings who never knew what was planned for them.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "They are still out there. Scattered. Frozen. Waiting.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "That is where the next step begins.", seconds = 2f },
            };
        }

        // ---- GALAXY 4 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "The broadcast is out. Ronin-1's archive, the template coordinates, the Program's origin, spreading across every relay in three sectors. The Meridian is ash now. The 143 are free.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "And we're a permanent target. Khall's hunters are moving. But the galaxy knows where the frozen ones are now. That changes the whole board.", seconds = 3f },
            };
        }
    }
}
