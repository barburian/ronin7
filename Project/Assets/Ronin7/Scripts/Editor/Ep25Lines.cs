using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 25 dialogue data. Transposed from ep25-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep25_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 25 "The Sterile Reckoning" — the Galaxy 4 launch. Cipher infiltrates the Crimson Thread
    /// Gilded Maw station hunting for reversal technology. Dr. Heris — the surgeon who built his leash
    /// thirty years ago during Kethel-7 — is hidden inside the facility. She can interrupt (not undo) the
    /// failsafe cascade. Cipher rescues a frozen child the Dominion grew as his replacement, downloads the
    /// Program's sourcing records, and recruits Heris as an ally. Final reveal: Cipher was designed not to
    /// standard combat specs, but for an unknown purpose that even his builder was never shown the full
    /// blueprint of. The heading becomes: find the blueprints, achieve full reversal, and learn what he
    /// was designed to become.
    /// </summary>
    public static class Ep25Lines
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
            "lead_intro",
            "harrow_greeting",
            "cargo_barks",
            "architect",
            "enforcer_aftermath",
            "real_record",
            "commando_barks",
            "vault",
            "khall_wire",
            "corridor_barks",
            "self_destruct",
            "purpose_untold",
            "intercept_barks",
            "the_heading",
            "space_ep25_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "lead_intro" => GetLeadIntroLines(),
                "harrow_greeting" => GetHarrowGreetingLines(),
                "cargo_barks" => GetCargoBarksLines(),
                "architect" => GetArchitectLines(),
                "enforcer_aftermath" => GetEnforcerAftermathLines(),
                "real_record" => GetRealRecordLines(),
                "commando_barks" => GetCommandoBarksLines(),
                "vault" => GetVaultLines(),
                "khall_wire" => GetKhallWireLines(),
                "corridor_barks" => GetCorridorBarksLines(),
                "self_destruct" => GetSelfDestructLines(),
                "purpose_untold" => GetPurposeUntoldLines(),
                "intercept_barks" => GetInterceptBarksLines(),
                "the_heading" => GetTheHeadingLines(),
                "space_ep25_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep25_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep25_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ==== BEAT 1 — LEAD INTRO ====

        // ---- LEAD INTRO (briefing: Sanctuary's intel on Dr. Heris) ----

        private static DialogueLine[] GetLeadIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "There's a name in the network Sanctuary pulled. A physician operating out of a medical station hidden in the Sable Drift.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Gilded Maw faction. They traffic in augmentation and neural reconstruction. And according to the files, this physician has published research on a patented reversal technique.", seconds = 4.5f },
                new DialogueLine { speaker = "Cipher", text = "What kind of reversal?", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Augmentation reversal. Not full removal, the leash is wired into your neural architecture too deep for that. But interruption. Slowing. Buying time instead of counting down to zeros.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "The Gilded Maw prices miracles in other people's flesh.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "They do. Which means we go in as buyers. Walk in like we're looking for augmentation work, not hunting for salvation. Let them show us what they can do.", seconds = 4f },
            };
        }

        // ---- HARROW GREETING (docking; Harrow's uncanny recognition) ----

        private static DialogueLine[] GetHarrowGreetingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Harrow", text = "Welcome to the Crimson Thread. I am Harrow, primary facilitator.", seconds = 2.5f },
                new DialogueLine { speaker = "Harrow", text = "Such precise augmentation work. Such... familiar architecture.", seconds = 2.5f },
                new DialogueLine { speaker = "Harrow", text = "I would very much like to show you our medical core. A private tour. Our most specialized work happens in those corridors.", seconds = 3f },
            };
        }

        // ---- CARGO BARKS (cover breaks) ----

        private static DialogueLine[] GetCargoBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Harrow", text = "I'm afraid your cover was insufficient.", seconds = 2f },
            };
        }

        // ==== BEAT 2 — THE ARCHITECT ====

        // ---- ARCHITECT (Dr. Heris reveal: "I put them there") ----

        private static DialogueLine[] GetArchitectLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris", text = "I know every weld in you.", seconds = 2f },
                new DialogueLine { speaker = "Dr. Heris", text = "I put them there.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Kethel-7.", seconds = 1f },
                new DialogueLine { speaker = "Dr. Heris", text = "I kept the surgical record of your purge. Every step. I needed to bear witness to something, even while I was too afraid to stop it.", seconds = 4f },
            };
        }

        // ---- ENFORCER AFTERMATH (who is Dr. Heris; why she defected) ----

        private static DialogueLine[] GetEnforcerAftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris", text = "I trained them. Several Gilded Maw soldiers. On the same combat specifications I gave you.", seconds = 3f },
                new DialogueLine { speaker = "Dr. Heris", text = "I defected to Gilded Maw years ago. Not as a loyalist. As a fugitive. The Dominion marked me as a liability the moment I started keeping unauthorized records.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "You've been waiting for me.", seconds = 1.5f },
                new DialogueLine { speaker = "Dr. Heris", text = "I have.", seconds = 1f },
            };
        }

        // ==== BEAT 3 — THE REAL RECORD ====

        // ---- REAL RECORD (Kethel-7 massacre was real; the purge; the refusal) ----

        private static DialogueLine[] GetRealRecordLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "The massacre. It was real.", seconds = 2f },
                new DialogueLine { speaker = "Dr. Heris", text = "It was real. Overseer Khall ordered it as a loyalty test. He commanded you to eliminate every child in the Kethel-7 orphanage. You refused. The first operative in the history of the Program to refuse a direct order from command.", seconds = 5f },
                new DialogueLine { speaker = "Dr. Heris", text = "Khall purged you as punishment. The failsafe activated. I was present at the surgical intervention. I documented what was happening to your neural matrix. I watched the purge begin.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Why keep the record?", seconds = 1.5f },
                new DialogueLine { speaker = "Dr. Heris", text = "Because I was too afraid to stop it. Because witnessing, even silently, was the only resistance I could afford.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "Can you undo it?", seconds = 1.5f },
                new DialogueLine { speaker = "Dr. Heris", text = "The leash? Not fully. Not quickly. Not without risk.", seconds = 2.5f },
                new DialogueLine { speaker = "Dr. Heris", text = "But yes. The architecture I installed is mine. I know every weld. I can interrupt the cascade. I can slow what is trying to kill you from the inside.", seconds = 4f },
                new DialogueLine { speaker = "Dr. Heris", text = "Not cure. Reprieve. But reprieve is a kind of mercy.", seconds = 2.5f },
            };
        }

        // ---- COMMANDO BARKS (Dominion strike team) ----

        private static DialogueLine[] GetCommandoBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Commando 1", text = "Stand down, CIPHER. Return to command authority.", seconds = 2f },
                new DialogueLine { speaker = "Dominion Commando 2", text = "You don't fight like yourself anymore, Seven. You're broken.", seconds = 2.5f },
            };
        }

        // ==== BEAT 4 — THE VAULT ====

        // ---- VAULT (the frozen child; the sourcing records) ----

        private static DialogueLine[] GetVaultLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris", text = "I have been documenting all of it. Every intake. Every modification. Every scheduled awakening for deployment.", seconds = 3.5f },
                new DialogueLine { speaker = "Dr. Heris", text = "I intercepted the intake order before the Dominion could activate the procedure. I froze her before they could touch her. I don't know if she is family or engineering.", seconds = 4.5f },
                new DialogueLine { speaker = "Dr. Heris", text = "The records don't go back far enough to know. But she is alive. And she is not augmented. And she is not staying here.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "I don't remember her.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "I'm not leaving her.", seconds = 1f },
            };
        }

        // ---- KHALL WIRE (neural channel: countdown to shutdown) ----

        private static DialogueLine[] GetKhallWireLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "Cipher. I see you are exploring the Crimson Thread's hospitality.", seconds = 2.5f },
                new DialogueLine { speaker = "Khall", text = "Dr. Heris was useful once. But her usefulness expired when she began keeping records the Dominion did not authorize. You will cross my next threshold before you reach the hangar, and then we will see how far sentiment carries you.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "How long?", seconds = 1f },
                new DialogueLine { speaker = "Dr. Heris", text = "Six minutes. Somewhere to sit. I can slow it right now. Not cure it, slow it. Enough to reach somewhere safe for the full procedure.", seconds = 4f },
            };
        }

        // ---- CORRIDOR BARKS (Harrow's betrayal) ----

        private static DialogueLine[] GetCorridorBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Harrow", text = "I sold them your location. Dominion pays better than Heris does.", seconds = 2f },
            };
        }

        // ---- SELF DESTRUCT (Harrow initiates station purge) ----

        private static DialogueLine[] GetSelfDestructLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Harrow", text = "Self-destruct sequence initiated. Core meltdown in ninety seconds. Evacuate immediately.", seconds = 2f },
            };
        }

        // ==== BEAT 5 — PURPOSE UNTOLD ====

        // ---- PURPOSE UNTOLD (escape; the hidden design; the unanswered question) ----

        private static DialogueLine[] GetPurposeUntoldLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "We're clear. Jump sequence in ninety seconds.", seconds = 2f },
                new DialogueLine { speaker = "Dr. Heris", text = "The interrupt I performed was partial. Crude, by my own standards. But effective.", seconds = 3.5f },
                new DialogueLine { speaker = "Dr. Heris", text = "Full reversal requires the original augmentation blueprints. The specifications I used to build you. I encoded them before I defected. I know where they are.", seconds = 4f },
                new DialogueLine { speaker = "Dr. Heris", text = "But before we move toward that reversal, I have something you need to understand about your own construction.", seconds = 3f },
                new DialogueLine { speaker = "Dr. Heris", text = "Your augmentation was not standard Ronin-class design. The specifications exceeded combat and control. Whoever designed your neural architecture designed you for something beyond what the Dominion's Augmentation Division was prepared to admit to.", seconds = 5f },
                new DialogueLine { speaker = "Dr. Heris", text = "I was shown the blueprint only in fragments. Specified by someone above the Augmentation Division. And I was never told the reason. I built you to a plan I was never given the full text of.", seconds = 4f },
                new DialogueLine { speaker = "Dr. Heris", text = "You were designed for something. I built you to a plan. And I do not know what you were meant to become.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Do you want to know?", seconds = 1.5f },
                new DialogueLine { speaker = "Dr. Heris", text = "I do. Very much.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Not yet. I'm not ready to learn what I was meant to become until I'm done choosing what I have become.", seconds = 4f },
            };
        }

        // ---- INTERCEPT BARKS (space dogfight) ----

        private static DialogueLine[] GetInterceptBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Corvette dropping out of the dark, it's tracking Heris's signal. Break and engage.", seconds = 3f },
            };
        }

        // ---- THE HEADING (Heris recruits as ally; setting up for blueprints and purpose) ----

        private static DialogueLine[] GetTheHeadingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Hundreds.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "And that's only what she could save from one facility.", seconds = 2.5f },
                new DialogueLine { speaker = "Dr. Heris", text = "Is having the surgeon who built the cage now working to remove it enough to call me an ally?", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Yes.", seconds = 1f },
                new DialogueLine { speaker = "Dr. Heris", text = "And do you want to know more about the purpose encoded in your design? The hidden architecture I was never shown the full text of?", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Not yet.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "I'm not ready to learn what I was made for until I'm done choosing what I choose to be.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Where to?", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "To the blueprints. To the full reversal. To whatever truth is written in the architecture of my augmented body that even its own builder was never shown.", seconds = 5f },
            };
        }

        // ---- GALAXY 4 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "The Crimson Thread was a Gilded Maw flesh-market. The surgeon who built me was waiting in it. Dr. Heris installed my leash thirty years ago, and she's the one person alive who can interrupt it. She bought me months. We got out with a frozen child the Dominion grew as my replacement, and a drive full of the Program's sourcing records.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "Heris says a full reversal needs the original blueprints, and she knows where she hid them. But there's something worse in those specs: you weren't built to standard. Someone above the Augmentation Division designed you for a purpose she was never told. That's the heading now. The blueprints, the cure, and the truth of what you were made for.", seconds = 5.5f },
            };
        }
    }
}
