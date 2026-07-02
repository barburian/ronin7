using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 8 dialogue data. Transposed from ep08-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep08_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// </summary>
    public static class Ep08Lines
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
            "introspection_hold",
            "drone_breach",
            "mera_parley",
            "enforcer_ambush",
            "descent_terms",
            "canyon_scouts",
            "vault_core",
            "reaper_duel",
            "collapse_escape",
            "frigate_bracket",
            "covenant_epilogue",
            "final_pursuit",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "introspection_hold" => GetIntrospectionHoldLines(),
                "drone_breach" => GetDroneBreachLines(),
                "mera_parley" => GetMeraParleyLines(),
                "enforcer_ambush" => GetEnforcerAmbushLines(),
                "descent_terms" => GetDescentTermsLines(),
                "canyon_scouts" => GetCanyonScoutsLines(),
                "vault_core" => GetVaultCoreLines(),
                "reaper_duel" => GetReaperDuelLines(),
                "collapse_escape" => GetCollapseEscapeLines(),
                "frigate_bracket" => GetFrigateBracketLines(),
                "covenant_epilogue" => GetCovenantEpilogueLines(),
                "final_pursuit" => GetFinalPursuitLines(),
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

        /// <summary>Generate clip name for a line: ep08_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep08_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- CORSAIR CARGO HOLD: INTROSPECTION (Beat 1) ----

        private static DialogueLine[] GetIntrospectionHoldLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "Something in me kept breaking the order. I don't remember doing it.", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Kethel-7 wasn't the first time something in me cracked. Tessa said there were others. I can feel the shape of them, but they don't have faces.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "Weapons don't hesitate.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "You do.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "The Fringe relay holds. Tessa has the time she needs.", seconds = 1.5f },
            };
        }

        // ---- CORSAIR CARGO HOLD: LOTUS DRONE BREACH (Beat 2) ----

        private static DialogueLine[] GetDroneBreachLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Lotus Drone 1", text = "Intruder compromised. Engaging.", seconds = 1f },
                new DialogueLine { speaker = "Ronin-7", text = "Stand clear.", seconds = 1f },
                new DialogueLine { speaker = "Lotus Drone 2", text = "Shutdown sequence failed. Detonation on count...", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Holding the interceptor at range. Warning shots sent. They're going weapons-cold.", seconds = 2f },
            };
        }

        // ---- CORSAIR AIRLOCK: MERA'S PARLEY (Beat 3) ----

        private static DialogueLine[] GetMeraParleyLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mera Voss", text = "I'm boarding alone. Hands open. I came to pay a debt.", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Who are you?", seconds = 1.5f },
                new DialogueLine { speaker = "Mera Voss", text = "Mera Voss. Lieutenant of the Crimson Lotus. Eight years ago on Kerrax Station, you had orders to destroy a facility. You killed the armed guards. But the depot held more than soldiers.", seconds = 4f },
                new DialogueLine { speaker = "Mera Voss", text = "Thirty-two people sheltered there. Children. Runaways from the fringe. You could have killed them. You walked away instead.", seconds = 3f },
                new DialogueLine { speaker = "Mera Voss", text = "I was one of them. Junior lieutenant, sheltering children while running black narcotics to survive. You could have called me a collaborator and killed me with the rest.", seconds = 3.5f },
                new DialogueLine { speaker = "Mera Voss", text = "Inside the Lotus, they still call you the Architect of Mercy. The operative who chose.", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-7", text = "I don't remember.", seconds = 1.5f },
                new DialogueLine { speaker = "Mera Voss", text = "The Dominion erased you because they feared what you remembered. But you weren't built to break that order. Something in you did anyway, and they couldn't engineer it back out. That fracture was yours. Not installed, not erased. I can show you proof it was real.", seconds = 5f },
            };
        }

        // ---- CORSAIR CARGO HOLD: ENFORCER FIGHT (Beat 4) ----

        private static DialogueLine[] GetEnforcerAmbushLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Lotus Enforcer 1", text = "Stand down! Step away from the lieutenant!", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "She's not being held.", seconds = 1f },
                new DialogueLine { speaker = "Lotus Enforcer 3", text = "Covering detonation! Fall back!", seconds = 1f },
                new DialogueLine { speaker = "Mera Voss", text = "Still half-man, half-ghost.", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "The choice is mutual now.", seconds = 1.5f },
            };
        }

        // ---- CORSAIR MED-BAY: DESCENT TERMS (Beat 5) ----

        private static DialogueLine[] GetDescentTermsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mera Voss", text = "I cannot stay inside the Lotus. I have been making the right call too many times.", seconds = 2.5f },
                new DialogueLine { speaker = "Mera Voss", text = "Deep in Apex Station, I hold a fragment of your pre-wipe neural profile. The fractures the conditioning couldn't seal. The shape of what broke through, before they erased it.", seconds = 3.5f },
                new DialogueLine { speaker = "Mera Voss", text = "I will hand it over if you grant me passage out of the Lotus and a place aboard this ship.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Done. You have a berth. Welcome to the Corsair.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we go down.", seconds = 1f },
            };
        }

        // ---- CANYON APPROACH: DOMINION SCOUTS FIGHT (Beat 6) ----

        private static DialogueLine[] GetCanyonScoutsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Scout 1", text = "Contact in the ravine! Blocking position!", seconds = 1.5f },
                new DialogueLine { speaker = "Mera Voss", text = "Left!", seconds = 0.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Station entrance is two hundred meters northeast.", seconds = 1.5f },
                new DialogueLine { speaker = "Mera Voss", text = "Before their backup regroups.", seconds = 1f },
            };
        }

        // ---- APEX STATION VAULT CORE (Beat 7) ----

        private static DialogueLine[] GetVaultCoreLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mera Voss", text = "The vault doesn't hold narrative. No scenes. No faces from your past. What surfaces is the shape of where you broke.", seconds = 3f },
                new DialogueLine { speaker = "Mera Voss", text = "The Dominion didn't erase your skills. They erased your reasons. This is proof the reasons were there, and they were yours.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Dominion assault shuttles inbound. Ten minutes.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Station's flagged. You have to move.", seconds = 1.5f },
                new DialogueLine { speaker = "Mera Voss", text = "I have it. We run.", seconds = 1f },
            };
        }

        // ---- APEX VAULT: REAPER UNIT CONFRONTATION (Beat 8) ----

        private static DialogueLine[] GetReaperDuelLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Reaper Unit", text = "Rogue asset identified. Lethal engagement authorized.", seconds = 2f },
                new DialogueLine { speaker = "Reaper Unit", text = "All systems operational. Combat protocol engage.", seconds = 1.5f },
            };
        }

        // ---- APEX COLLAPSE: ESCAPE (Beat 9) ----

        private static DialogueLine[] GetCollapseEscapeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mera Voss", text = "It doesn't give you your memories back.", seconds = 1.5f },
                new DialogueLine { speaker = "Mera Voss", text = "It gives you proof they were real.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Corsair clear! Vault's collapsing behind us!", seconds = 2f },
            };
        }

        // ---- ORBITAL COMBAT: FRIGATE BRACKET (Beat 10) ----

        private static DialogueLine[] GetFrigateBracketLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Portside overwhelmed! Incoming from the bracket!", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Engine array compromised. They're hemorrhaging.", seconds = 1.5f },
                new DialogueLine { speaker = "Mera Voss", text = "Second frigate correcting bearing!", seconds = 1f },
            };
        }

        // ---- OBSERVATION PORT: COVENANT EPILOGUE (Beat 11) ----

        private static DialogueLine[] GetCovenantEpilogueLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "The profile confirms the shape of who I was. But it's a fragment. The full record is scattered.", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Every syndicate vault in the fringe holds a piece of someone like me.", seconds = 2.5f },
                new DialogueLine { speaker = "Mera Voss", text = "That's by design. The Dominion keeps the distributed archive as leverage. They've been trading those memories for years.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Then we find the vaults. We recover what was taken.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "The killswitch is still live. The day they decide I'm worth the trouble, they pull it.", seconds = 2.5f },
                new DialogueLine { speaker = "Mera Voss", text = "Then we stay ahead of them. And we find the rest of you before it's sold off.", seconds = 2.5f },
            };
        }

        // ---- NEBULA EDGE: FINAL PURSUIT (Beat 12) ----

        private static DialogueLine[] GetFinalPursuitLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Bounty-marker on the six! Spinning up FTL!", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Engine's cut.", seconds = 1f },
            };
        }
    }
}
