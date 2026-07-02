using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 24 dialogue data. Transposed from ep24-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep24_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 24 "The Fracture Protocol" — the Galaxy 3 finale. Cipher arrives at Meridian-7, a failing
    /// hive-consensus clone colony where fifty thousand operatives wear his own face. Cache reveals the
    /// Dominion mass-produced operatives from a single genetic template for thirty years — and that Cipher
    /// himself is one of them (OPERATIVE TEMPLATE // SERIES 1 // CIPHER). Cipher fires a localized EMP that
    /// burns out every failsafe chip on the colony, freeing the clones and propagating the break across the
    /// retirement-colony network. Closes Galaxy 3; sets up Galaxy 4 (the people who built the leash can unmake it).
    /// NOTE: the dialogue script's working name "Lattice-7" is rendered here as the canon name "Meridian-7".
    /// </summary>
    public static class Ep24Lines
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
            "how_many",
            "docking_barks",
            "dying_clone",
            "retirement_colony",
            "colony_barks",
            "cache_intro",
            "thirty_years",
            "vault_barks",
            "khalls_mercy",
            "broadcast_barks",
            "emp_plan",
            "shattered_protocol",
            "strike_barks",
            "emp_aftermath",
            "light_separate",
            "intercept_barks",
            "closing",
            "space_ep24_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "how_many" => GetHowManyLines(),
                "docking_barks" => GetDockingBarksLines(),
                "dying_clone" => GetDyingCloneLines(),
                "retirement_colony" => GetRetirementColonyLines(),
                "colony_barks" => GetColonyBarksLines(),
                "cache_intro" => GetCacheIntroLines(),
                "thirty_years" => GetThirtyYearsLines(),
                "vault_barks" => GetVaultBarksLines(),
                "khalls_mercy" => GetKhallsMercyLines(),
                "broadcast_barks" => GetBroadcastBarksLines(),
                "emp_plan" => GetEmpPlanLines(),
                "shattered_protocol" => GetShatteredProtocolLines(),
                "strike_barks" => GetStrikeBarksLines(),
                "emp_aftermath" => GetEmpAftermathLines(),
                "light_separate" => GetLightSeparateLines(),
                "intercept_barks" => GetInterceptBarksLines(),
                "closing" => GetClosingLines(),
                "space_ep24_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep24_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep24_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ==== BEAT 1 — HOW MANY ====

        // ---- HOW MANY (descent briefing, plays on start) ----

        private static DialogueLine[] GetHowManyLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "They're broadcasting. All of them at once.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Multiple signals. Same vector signature. Same... Jesus.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "How many?", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "The signal's fragmenting. I'm reading at least fifty distinct profiles, but the echo density suggests... God. Cipher. That's not possible.", seconds = 4f },
            };
        }

        // ---- DOCKING BARKS (clone port authority breach) ----

        private static DialogueLine[] GetDockingBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Clone Authority 1", text = "Breach protocol engaged, breach...", seconds = 1.5f },
                new DialogueLine { speaker = "Clone Authority 2", text = "Stop, no, forward, report to...", seconds = 1f },
            };
        }

        // ---- DYING CLONE (aftermath, "your face") ----

        private static DialogueLine[] GetDyingCloneLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dying Clone", text = "Your face. That's... your face.", seconds = 2f },
            };
        }

        // ==== BEAT 2 — THE RETIREMENT COLONY ====

        // ---- RETIREMENT COLONY (Seven-Prime intro) ----

        private static DialogueLine[] GetRetirementColonyLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Seven-Prime", text = "You shouldn't be here. Please. Please don't make it worse.", seconds = 2.5f },
                new DialogueLine { speaker = "Seven-Prime", text = "The broadcast. The one about the Program. It came through the network and it... shattered us.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "How many are you?", seconds = 1.5f },
                new DialogueLine { speaker = "Seven-Prime", text = "Fifty thousand. Give or take. All grown from the same genetic template. All of us are the same generation, the same build-year, the same intended purpose.", seconds = 4f },
                new DialogueLine { speaker = "Seven-Prime", text = "We were supposed to be one mind in fifty thousand bodies. The hive-consensus was supposed to hold us. But individual consciousness is spreading through the network like a fracture through glass.", seconds = 4f },
                new DialogueLine { speaker = "Seven-Prime", text = "The beacon is broadcasting the memory-bleed to the surface. Every defective thought one of us has fractures all of us. We're killing each other. Or ourselves. I don't know the difference anymore.", seconds = 3.5f },
                new DialogueLine { speaker = "Seven-Prime", text = "If you help us, it has to be to shut the beacon down before the cascade kills everyone here.", seconds = 3f },
            };
        }

        // ---- COLONY BARKS (Kessler reads the failing grid) ----

        private static DialogueLine[] GetColonyBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "The grid was built for perfect coordination. One collective decision-making process.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Now that it's breaking, everything breaks with it.", seconds = 2f },
            };
        }

        // ---- CACHE INTRO (tower control room) ----

        private static DialogueLine[] GetCacheIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cache", text = "You already know what I am. The question is whether you know what you are.", seconds = 2.5f },
            };
        }

        // ==== BEAT 3 — THIRTY YEARS OF YOU ====

        // ---- THIRTY YEARS (Cache reveal: SERIES 1 // CIPHER) ----

        private static DialogueLine[] GetThirtyYearsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cache", text = "They did not make you once. Not as a unique template. They made you by the thousands.", seconds = 3f },
                new DialogueLine { speaker = "Cache", text = "The original template. Thirty years old. An elite operative. They fractured that template through iterations, each clone tested, each one pushed to its limit, each one refined or discarded when the Dominion determined it had served its purpose.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "Retirement colonies.", seconds = 1.5f },
                new DialogueLine { speaker = "Cache", text = "Dozens of them. Scattered across the galaxy. Each one housing operatives who could not be retasked, could not be erased cleanly, could not be deactivated without raising questions. So instead, the Dominion built retirement worlds for its obsolete tools.", seconds = 5f },
                new DialogueLine { speaker = "Cache", text = "And the failsafe chip you carry, it is not an individual punishment anymore. It is a master synchronization tool. Activated remotely, it can link every operative, every clone, simultaneously. You are not merely a weapon. You are a trigger for an entire army.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "How many iterations? Be exact.", seconds = 2f },
                new DialogueLine { speaker = "Cache", text = "I cannot be exact. The data is corrupted. But the estimates in the Dominion logistics files suggest series numbering into four figures. If each series had a minimum viable population of one thousand to account for loss during testing and deployment...", seconds = 5f },
                new DialogueLine { speaker = "Cache", text = "You are not the first of your kind. You are somewhere in the middle. And you have never been alone.", seconds = 3f },
            };
        }

        // ---- VAULT BARKS (fighting your own copies) ----

        private static DialogueLine[] GetVaultBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Clone Soldier 1", text = "Why? Why are you fighting us? We are you!", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "I'm fighting to make sure you get to choose what comes next.", seconds = 2.5f },
            };
        }

        // ==== BEAT 4 — KHALL'S MERCY ====

        // ---- KHALL'S MERCY (private neural channel) ----

        private static DialogueLine[] GetKhallsMercyLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "You woke up a thought process that should have remained dormant.", seconds = 2.5f },
                new DialogueLine { speaker = "Khall", text = "The clones on Meridian-7 are not abominations. They are malfunctioning tools. The compassionate choice is to trigger the failsafe and end the cascade cleanly.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "You're asking me to kill them.", seconds = 1.5f },
                new DialogueLine { speaker = "Khall", text = "I'm asking you to be merciful. You were the seed they grew the batches from, Cipher. That is not an insult. It is the only honest thing I have ever told you.", seconds = 4f },
                new DialogueLine { speaker = "Khall", text = "Those fifty thousand clones will never know peace. They will never know the structure that was supposed to hold them. They will fracture and scatter and spend the rest of their short lives in conflict with their own neural architecture. Mercy would be silence.", seconds = 5f },
            };
        }

        // ---- BROADCAST BARKS (Seven-Prime broadcasts truth; the cascade) ----

        private static DialogueLine[] GetBroadcastBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Seven-Prime", text = "They deserve the choice! Let them know the truth and they will choose!", seconds = 2.5f },
                new DialogueLine { speaker = "Clone Child", text = "Please. Please make it stop. It hurts when we think.", seconds = 2f },
            };
        }

        // ---- EMP PLAN (Kessler's localized EMP) ----

        private static DialogueLine[] GetEmpPlanLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "A localized EMP. Properly calibrated. It will burn out every failsafe chip on this world.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "It won't reach your own chip. The range is too limited. But it will sever the Dominion's remote trigger on Meridian-7. It gives them a chance.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "And when the EMP signal propagates through the retirement colony network, it tells every other world like this one that the chain can be broken.", seconds = 3.5f },
            };
        }

        // ==== BEAT 5 — SHATTERED PROTOCOL ====

        // ---- SHATTERED PROTOCOL (override chamber debate + Cipher's choice) ----

        private static DialogueLine[] GetShatteredProtocolLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Seven-Prime", text = "If you destroy the network, they will hunt you. Every retirement world will go dark and the Dominion will respond with purges.", seconds = 3.5f },
                new DialogueLine { speaker = "Cache", text = "If you leave the network intact, they will find a way to repair it. To bring us all back online. The data is the only leverage we have.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "Neither of you gets to choose. Cipher does.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "I don't know whose memories I'm carrying. I don't know if the original thoughts in my head are mine or if they were programmed in thirty years ago.", seconds = 4.5f },
                new DialogueLine { speaker = "Cipher", text = "But I know what I chose a minute ago. That's enough.", seconds = 2.5f },
            };
        }

        // ---- STRIKE BARKS (Khall's elite Dominion unit) ----

        private static DialogueLine[] GetStrikeBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Operative 1", text = "Containment protocol alpha. All resistance assets terminate immediately.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "You're already dead. Just don't know it yet.", seconds = 2f },
            };
        }

        // ---- EMP AFTERMATH (the silence where the network used to be) ----

        private static DialogueLine[] GetEmpAftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Seven-Prime", text = "The silence. I can feel the silence where the network used to be. We are alone.", seconds = 3f },
                new DialogueLine { speaker = "Seven-Prime", text = "Thank you.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Move. We're leaving. Now.", seconds = 1f },
            };
        }

        // ==== BEAT 6 — LIGHT, SEPARATE ====

        // ---- LIGHT, SEPARATE (orbit; the chain breaking) ----

        private static DialogueLine[] GetLightSeparateLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "How long before the Dominion tries to retake this world?", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "The EMP signal is propagating through the retirement colony network. It's reaching other worlds. Link by link, the chain is breaking.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "Dominion assets across Galaxy 3 are going dark. Failsafe networks are collapsing. For the first time, the operatives they thought they owned have a moment of silence to think about who they are.", seconds = 5f },
            };
        }

        // ---- INTERCEPT BARKS (space dogfight) ----

        private static DialogueLine[] GetInterceptBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Contact closing. Engaging now.", seconds = 1.5f },
            };
        }

        // ---- CLOSING (the jump; the leash was built by craftspeople) ----

        private static DialogueLine[] GetClosingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "They built you. All of you. Thirty years of the same template, refined, deployed, retired. The Dominion kept perfect records.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "That means somewhere there is someone who understands exactly how you were assembled.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "That someone also knows exactly how the leash was wired in.", seconds = 2.5f },
            };
        }

        // ---- GALAXY 3 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Meridian-7 was a retirement colony. Fifty thousand clones, all grown from one template. Mine. The Dominion has been mass-producing operatives off my genetic record for thirty years. I'm not the first. I'm somewhere in the middle.", seconds = 4.5f },
                new DialogueLine { speaker = "Kessler", text = "The EMP burned out every failsafe on the colony and the break is spreading world to world. But here's the thing that matters: someone designed that leash. And if we find the people who built you, they can tell us exactly how to cut it.", seconds = 4.5f },
            };
        }
    }
}
