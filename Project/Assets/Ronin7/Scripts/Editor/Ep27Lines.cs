using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 27 dialogue data. Transposed from ep27-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep27_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 27 "The Hollow Choir" — Leviathan-9 and the thought-fossil revelation. Cipher infiltrates
    /// Leviathan Station to confront the Pale Choir and discovers the truth he has been circling: he fired
    /// the Genesis Cannon that murdered a living thought-oracle forty years ago, on Overseer Khall's order.
    /// The thought-fossil reveals the deeper architecture — the ten syndicates of the fringe were engineered
    /// by the Dominion as a cage, each bar holding the others in place. Meren and her settlement are protected;
    /// Sallow (the Harmonian archivist from EP26) secures the Archive's proof of the genocide and the cage's design.
    /// No new ally recruits. The heading becomes: Kethel-7, the orphanage massacre, the first order Cipher ever
    /// refused, the origin point everything has been circling back toward.
    /// </summary>
    public static class Ep27Lines
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
            "drek_briefing",
            "gate_challenge",
            "dying_cultist",
            "meren_intro",
            "bloodied_elder",
            "sallow_archive",
            "sallow_invite",
            "khall_memory",
            "weight_witness",
            "kessler_descent",
            "handover",
            "handler_barks",
            "khall_confront",
            "bone_fleet_barks",
            "relay_aftermath",
            "space_ep27_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "drek_briefing" => GetDrekBriefingLines(),
                "gate_challenge" => GetGateChallengeLines(),
                "dying_cultist" => GetDyingCultistLines(),
                "meren_intro" => GetMerenIntroLines(),
                "bloodied_elder" => GetBloodiedElderLines(),
                "sallow_archive" => GetSallowArchiveLines(),
                "sallow_invite" => GetSallowInviteLines(),
                "khall_memory" => GetKhallMemoryLines(),
                "weight_witness" => GetWeightWitnessLines(),
                "kessler_descent" => GetKesslerDescentLines(),
                "handover" => GetHandoverLines(),
                "handler_barks" => GetHandlerBarksLines(),
                "khall_confront" => GetKhallConfrontLines(),
                "bone_fleet_barks" => GetBoneFleetBarksLines(),
                "relay_aftermath" => GetRelayAftermathLines(),
                "space_ep27_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep27_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep27_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ==== BEAT 1 — THE BONE GATES ====

        // ---- DREK BRIEFING (opening office scene) ----

        private static DialogueLine[] GetDrekBriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Drek", text = "Pale Choir's been running cargo and prisoners through the marrow-tunnels for months. Dominion wants the nest cleared.", seconds = 3.5f },
                new DialogueLine { speaker = "Drek", text = "Station command doesn't care what's in the tunnels. Dead space. Real estate now.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "What was it before the dead space?", seconds = 2f },
                new DialogueLine { speaker = "Drek", text = "Before?", seconds = 1f },
                new DialogueLine { speaker = "Drek", text = "Don't know. Don't ask. The leviathan was a creature. Now it's a resource.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "The Choir. Are they organized?", seconds = 1.5f },
                new DialogueLine { speaker = "Drek", text = "Religious. Keep to themselves. They're tending something down in the bone-chambers. Not aggressive unless you breach their perimeter.", seconds = 3f },
            };
        }

        // ---- GATE CHALLENGE (bone gate confrontation) ----

        private static DialogueLine[] GetGateChallengeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Choir Cultist 1", text = "The gate is sealed to the unworthy.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "I'm not leaving.", seconds = 1f },
            };
        }

        // ---- DYING CULTIST (combat capture) ----

        private static DialogueLine[] GetDyingCultistLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dying Cultist", text = "Why don't you run?", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "I need to know what you're guarding.", seconds = 2f },
                new DialogueLine { speaker = "Dying Cultist", text = "Because we're already inside the corpse of a god. The Dominion made sure of that. They fed it to death, and we feed it our devotion.", seconds = 4f },
                new DialogueLine { speaker = "Dying Cultist", text = "You're just another ghost, samurai.", seconds = 1.5f },
            };
        }

        // ==== BEAT 2 — THE WET CHAMBERS ====

        // ---- MEREN INTRO (settlement introduction) ----

        private static DialogueLine[] GetMerenIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Meren", text = "You're Dominion. Or you were.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "We're not here for residents.", seconds = 1f },
                new DialogueLine { speaker = "Meren", text = "You're here for the Choir. They've been running this settlement's water and air-recycle systems for two generations. They came with my grandmother.", seconds = 3f },
                new DialogueLine { speaker = "Meren", text = "The leviathan was a living oracle. Worshipped. It knew things.", seconds = 2f },
                new DialogueLine { speaker = "Meren", text = "Forty years ago the Dominion cut off its food supply. Said it was a cultural liability. Let it starve.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "And now?", seconds = 1f },
                new DialogueLine { speaker = "Meren", text = "Now it's a resource. Its bones are real estate. Its death bought us shelter and employment and the Choir bought us hope that maybe something in this creature would be remembered.", seconds = 4f },
            };
        }

        // ---- BLOODIED ELDER (neural flash recognition) ----

        private static DialogueLine[] GetBloodiedElderLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Bloodied Elder", text = "We have been waiting for the one who fired the Genesis Cannon.", seconds = 2.5f },
                new DialogueLine { speaker = "Bloodied Elder", text = "The leviathan has been calling you home.", seconds = 2f },
            };
        }

        // ==== BEAT 3 — THE MARROW ARCHIVE ====

        // ---- SALLOW ARCHIVE (thought-oracle revelation) ----

        private static DialogueLine[] GetSallowArchiveLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sallow", text = "The leviathan was a living records-keeper. An alien species that absorbed and preserved all knowledge it encountered.", seconds = 3.5f },
                new DialogueLine { speaker = "Sallow", text = "We call them thought-oracles. The Dominion called them a problem.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Why would it be a problem?", seconds = 1.5f },
                new DialogueLine { speaker = "Sallow", text = "Because it remembered everything. Every atrocity. Every order. Every secret the Dominion wanted buried.", seconds = 3f },
                new DialogueLine { speaker = "Sallow", text = "So they took a ship with a Genesis Cannon and killed a god. Then they walked away from what they'd done and called the corpse real estate.", seconds = 3.5f },
                new DialogueLine { speaker = "Sallow", text = "The Choir has spent four decades keeping its residual neural tissue intact. We are its voice now.", seconds = 2.5f },
                new DialogueLine { speaker = "Sallow", text = "And it has been singing your designation since the day you pressed the trigger.", seconds = 2.5f },
            };
        }

        // ---- SALLOW INVITE (fossil touch invitation) ----

        private static DialogueLine[] GetSallowInviteLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sallow", text = "It wants to show you. Put your hand to the thought-fossil.", seconds = 2.5f },
                new DialogueLine { speaker = "Sallow", text = "Are you ready?", seconds = 1.5f },
            };
        }

        // ==== BEAT 4 — THE WEIGHT OF A GOD ====

        // ---- KHALL MEMORY (Genesis Cannon order recall) ----

        private static DialogueLine[] GetKhallMemoryLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall (Memory)", text = "The oracle has absorbed too many Dominion records. It is a liability.", seconds = 2.5f },
                new DialogueLine { speaker = "Khall (Memory)", text = "Purge it. Use the Genesis Cannon.", seconds = 2f },
            };
        }

        // ---- WEIGHT WITNESS (Sallow's recognition) ----

        private static DialogueLine[] GetWeightWitnessLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sallow", text = "Now you know what they built you to protect.", seconds = 2.5f },
            };
        }

        // ==== BEAT 5 — THE HANDLER ARRIVES ====

        // ---- KESSLER DESCENT (capital ship descent alert) ----

        private static DialogueLine[] GetKesslerDescentLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Cipher. A Dominion capital ship is descending. Not to hold the station. To crack the creature open and destroy the Archive.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "One word from command: Khall. You need to move now.", seconds = 2f },
            };
        }

        // ---- HANDOVER (archive drive and settlement evacuation) ----

        private static DialogueLine[] GetHandoverLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "How long until impact?", seconds = 1f },
                new DialogueLine { speaker = "Sallow", text = "Minutes. You need to reach the heart-chamber. The settlement is there. The Dominion will level it.", seconds = 3f },
            };
        }

        // ---- HANDLER BARKS (combat engagement audio) ----

        private static DialogueLine[] GetHandlerBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Assault Commander", text = "Structure is hostile! Multiple threats converging!", seconds = 2f },
                new DialogueLine { speaker = "Meren", text = "Hold the east corridor! The settlement stays intact!", seconds = 1.5f },
            };
        }

        // ==== BEAT 6 — BONE FLEET ====

        // ---- KHALL CONFRONT (shuttle bay confrontation) ----

        private static DialogueLine[] GetKhallConfrontLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "You remember now.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "You gave the order.", seconds = 1.5f },
                new DialogueLine { speaker = "Khall", text = "I gave you a blank slate. A mercy. The weight of that trigger was mine to carry, not yours.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "You wiped me because I refused an order. The blank slate kept your program intact. Not me.", seconds = 3.5f },
                new DialogueLine { speaker = "Khall", text = "The cannon fires in fifteen minutes. The only question is whether you spend them here or saving something that matters.", seconds = 3f },
            };
        }

        // ---- BONE FLEET BARKS (space combat timer) ----

        private static DialogueLine[] GetBoneFleetBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "You've got three minutes before the cannon fires!", seconds = 2f },
            };
        }

        // ==== BEAT 7 — THE VOICE PRESERVED ====

        // ---- RELAY AFTERMATH (post-combat resolution and heading) ----

        private static DialogueLine[] GetRelayAftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Weapons Officer", text = "Targeting relay is non-responsive! Firing solution compromised!", seconds = 2.5f },
                new DialogueLine { speaker = "Sallow", text = "The Archive is secure. The confessions, the schematics, the proof of the cage, all preserved.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "What comes next?", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Kethel-7.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "The massacre.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "The original wound. The first order I refused. Everything the Dominion built around that refusal.", seconds = 3f },
                new DialogueLine { speaker = "Sallow", text = "Then you go there. You face it directly.", seconds = 2f },
            };
        }

        // ---- GALAXY 4 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Leviathan-9 was a living thought-oracle, a sentient archive that absorbed and preserved all knowledge it encountered. The Dominion murdered it forty years ago with a Genesis Cannon because it remembered every atrocity, every order, every secret they wanted buried. The Pale Choir preserved its residual neural tissue in the Archive. When I touched the thought-fossil, I remembered. I fired the cannon. Khall gave the order. But the Archive showed me something deeper, the ten syndicates of the fringe are not rivals. They are a cage. Each bar holding the others in place.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "So the heading is Kethel-7, the orphanage, the massacre, the first order you ever refused. The original wound. Everything you have been circling leads back to that origin point. This is where they built the cage. Where they designed you to carry a burden that was never yours to bear. Where you face it directly, and where you choose what comes next.", seconds = 5f },
            };
        }
    }
}
