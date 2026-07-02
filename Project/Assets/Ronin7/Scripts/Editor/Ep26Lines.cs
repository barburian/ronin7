using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 26 dialogue data. Transposed from ep26-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep26_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 26 "The Requiem Protocol" — the Pale Choir revelation. Cipher infiltrates the Absolution
    /// and discovers his original purpose: a confessor, trained to carry the final truths of the dead via
    /// his katana's neural-link. High Deacon Mortis activates the Requiem Protocol to restore what the
    /// purge erased. Sallow, the Harmonian archivist, defects and gives Cipher access to the confession
    /// archive — thousands of final moments, including those of worlds the Dominion murdered and erased.
    /// Cipher recruits Sallow as the tenth ally. The heading becomes: the archive holds proof of ordered
    /// genocides across the galaxy. The dead worlds demand to be named and mourned.
    /// </summary>
    public static class Ep26Lines
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
            "contract_intro",
            "ashen_barks",
            "mortis_summons",
            "cathedral_shadow",
            "docking_barks",
            "mortis_greeting",
            "nave_reveal",
            "katana_vo",
            "varrik_test",
            "varrik_duel",
            "refuse",
            "archivist",
            "archive_barks",
            "archive_drive",
            "the_tenth_voice",
            "drone_barks",
            "worlds_not_people",
            "hunter_arrival",
            "hunter_duel",
            "reckoning_vow",
            "space_ep26_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "contract_intro" => GetContractIntroLines(),
                "ashen_barks" => GetAshenBarksLines(),
                "mortis_summons" => GetMortisSummonsLines(),
                "cathedral_shadow" => GetCathedralShadowLines(),
                "docking_barks" => GetDockingBarksLines(),
                "mortis_greeting" => GetMortisGreetingLines(),
                "nave_reveal" => GetNaveRevealLines(),
                "katana_vo" => GetKatanaVoLines(),
                "varrik_test" => GetVarrikTestLines(),
                "varrik_duel" => GetVarrikDuelLines(),
                "refuse" => GetRefuseLines(),
                "archivist" => GetArchivistLines(),
                "archive_barks" => GetArchiveBarksLines(),
                "archive_drive" => GetArchiveDriveLines(),
                "the_tenth_voice" => GetTheTenthVoiceLines(),
                "drone_barks" => GetDroneBarksLines(),
                "worlds_not_people" => GetWorldsNotPeopleLines(),
                "hunter_arrival" => GetHunterArrivalLines(),
                "hunter_duel" => GetHunterDuelLines(),
                "reckoning_vow" => GetReckoningVowLines(),
                "space_ep26_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep26_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep26_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ==== BEAT 1 — THE WEIGHT HE WAS MADE TO CARRY ====

        // ---- CONTRACT INTRO (briefing: Pale Choir death contract on Varrik) ----

        private static DialogueLine[] GetContractIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Pale Choir death contract. Rustfang captain named Varrik. Hidden on Ashen Deep.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Sanctuary flagged it two hours ago. It's a direct hit.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "I know them.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "You remember?", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "I feel it. Don't know why.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "The failsafe is holding. Heris bought us time. But if I'm going to spend it, I want answers.", seconds = 3f },
            };
        }

        // ---- ASHEN BARKS (combat engagement) ----

        private static DialogueLine[] GetAshenBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Stand aside.", seconds = 1f },
            };
        }

        // ---- MORTIS SUMMONS (voice recording from Absolution) ----

        private static DialogueLine[] GetMortisSummonsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mortis (Voice Recording)", text = "The confessor does not remember his purpose. The Choir will remind him.", seconds = 2.5f },
                new DialogueLine { speaker = "Mortis (Voice Recording)", text = "Come to the Absolution. The Requiem Protocol will restore what the purge erased.", seconds = 3f },
            };
        }

        // ==== BEAT 2 — THE CATHEDRAL'S SHADOW ====

        // ---- CATHEDRAL SHADOW (approach to Absolution; Cipher's unease) ----

        private static DialogueLine[] GetCathedralShadowLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "This is a trap.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "I've been carrying the shape of this place inside me without knowing what it was.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "I need to know.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "Then I'm coming with you.", seconds = 1f },
            };
        }

        // ---- DOCKING BARKS (combat; cathedral bay) ----

        private static DialogueLine[] GetDockingBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Move!", seconds = 1f },
            };
        }

        // ---- MORTIS GREETING (High Deacon emerges) ----

        private static DialogueLine[] GetMortisGreetingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mortis", text = "Excellent form. The purge didn't erase your body's knowledge.", seconds = 2.5f },
                new DialogueLine { speaker = "Mortis", text = "Only the name for what you are.", seconds = 1.5f },
                new DialogueLine { speaker = "Mortis", text = "Come. I will give you that name.", seconds = 1.5f },
            };
        }

        // ==== BEAT 3 — THE NAVE OF CONFESSIONS ====

        // ---- NAVE REVEAL (Mortis explains the confessor function) ----

        private static DialogueLine[] GetNaveRevealLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mortis", text = "Before the Dominion transferred you to the Ronin Program, you trained here.", seconds = 2.5f },
                new DialogueLine { speaker = "Mortis", text = "As a confessor.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "I don't remember.", seconds = 1f },
                new DialogueLine { speaker = "Mortis", text = "Your katana carries a neural-link. It records the final moments of those you kill. Not for intelligence. For ritual.", seconds = 3.5f },
                new DialogueLine { speaker = "Mortis", text = "You were built to carry the dead. To absorb their final truths and hold them.", seconds = 2.5f },
                new DialogueLine { speaker = "Mortis", text = "The Dominion called this a weapon function. We call it sacred.", seconds = 2f },
            };
        }

        // ---- KATANA VO (the blade speaks) ----

        private static DialogueLine[] GetKatanaVoLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Katana (Interior VO)", text = "He tells it crooked, but the bone of it is true. I said it once, we are a ledger written in carbon and iron. This is what the ledger was forged to hold.", seconds = 4f },
            };
        }

        // ---- VARRIK TEST (Mortis commands: extract his truth) ----

        private static DialogueLine[] GetVarrikTestLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mortis", text = "Extract his final truth. Then absolve him.", seconds = 2f },
            };
        }

        // ---- VARRIK DUEL (Rustfang captain's resistance) ----

        private static DialogueLine[] GetVarrikDuelLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Varrik", text = "You're not a confessor! You're a predator! You eat souls!", seconds = 2.5f },
            };
        }

        // ---- REFUSE (Cipher chooses not to consume) ----

        private static DialogueLine[] GetRefuseLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "I'm not what you think I am.", seconds = 1.5f },
                new DialogueLine { speaker = "Mortis", text = "Refusing again. The Requiem Protocol activates regardless.", seconds = 2f },
                new DialogueLine { speaker = "Mortis", text = "Your nature is returning whether you accept it or not.", seconds = 2.5f },
            };
        }

        // ---- ARCHIVIST (Sallow intervenes; defection begins) ----

        private static DialogueLine[] GetArchivistLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sallow", text = "You heard him and chose not to drink.", seconds = 1.5f },
                new DialogueLine { speaker = "Sallow", text = "In eleven years of this nave, I have never seen that.", seconds = 2f },
                new DialogueLine { speaker = "Sallow", text = "I maintain the archive of confessions. Thousands of final moments.", seconds = 2f },
                new DialogueLine { speaker = "Sallow", text = "I know the difference between a predator that hungers and a vessel that carries.", seconds = 2.5f },
                new DialogueLine { speaker = "Mortis", text = "Return to your duties, Sallow.", seconds = 1.5f },
                new DialogueLine { speaker = "Sallow", text = "No.", seconds = 1f },
                new DialogueLine { speaker = "Sallow", text = "He is what the design was meant to be before the Dominion weaponized it.", seconds = 2.5f },
                new DialogueLine { speaker = "Sallow", text = "He does not consume the dead. He holds them.", seconds = 1.5f },
            };
        }

        // ---- ARCHIVE BARKS (Mortis orders containment) ----

        private static DialogueLine[] GetArchiveBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mortis", text = "Contain them. The archivist will be disciplined.", seconds = 2f },
            };
        }

        // ---- ARCHIVE DRIVE (Sallow gives Cipher the records) ----

        private static DialogueLine[] GetArchiveDriveLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sallow", text = "The full archive. Every confession recorded in this nave.", seconds = 2f },
                new DialogueLine { speaker = "Sallow", text = "Not just syndicate secrets. The orders behind them. The names behind the names.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "Why give this to me?", seconds = 1f },
                new DialogueLine { speaker = "Sallow", text = "Because you are the only person who has ever walked into this nave and carried the dead without being destroyed by the weight.", seconds = 3.5f },
                new DialogueLine { speaker = "Sallow", text = "Someone should leave here with that burden and give it meaning.", seconds = 2.5f },
            };
        }

        // ==== BEAT 5 — THE TENTH VOICE ====

        // ---- THE TENTH VOICE (escape with Varrik; Mortis comms; drone pursuit) ----

        private static DialogueLine[] GetTheTenthVoiceLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Run. Send coordinates to your faction.", seconds = 1.5f },
                new DialogueLine { speaker = "Mortis", text = "You were magnificent, Cipher. But the confessor-link never sleeps.", seconds = 2.5f },
                new DialogueLine { speaker = "Mortis", text = "Sooner or later you will need what we built into you.", seconds = 2f },
                new DialogueLine { speaker = "Mortis", text = "The Absolution will be waiting.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Ten drones closing! Get out!", seconds = 1.5f },
            };
        }

        // ---- DRONE BARKS (short combat bark) ----

        private static DialogueLine[] GetDroneBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Get out!", seconds = 1f },
            };
        }

        // ---- WORLDS NOT PEOPLE (Sallow reveals the archive's true scope) ----

        private static DialogueLine[] GetWorldsNotPeopleLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sallow", text = "Eleven years of confessions.", seconds = 1.5f },
                new DialogueLine { speaker = "Sallow", text = "But some of them confessed to worlds, Cipher. Not people.", seconds = 2.5f },
                new DialogueLine { speaker = "Sallow", text = "The Dominion's cruelty is not limited to people. It murders whole worlds.", seconds = 2.5f },
                new DialogueLine { speaker = "Sallow", text = "Cleanses them. Erases them. Leaves no name and no record.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "The confessions.", seconds = 1f },
                new DialogueLine { speaker = "Sallow", text = "Are the only record those worlds ever had.", seconds = 1.5f },
            };
        }

        // ==== BEAT 6 — REQUIEM FOR UNMOURNED WORLDS ====

        // ---- HUNTER ARRIVAL (in jump-space; Kessler asks Sallow's price) ----

        private static DialogueLine[] GetHunterArrivalLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "You gave up everything. What's the price?", seconds = 2.5f },
                new DialogueLine { speaker = "Sallow", text = "Nothing. Or everything.", seconds = 1.5f },
                new DialogueLine { speaker = "Sallow", text = "I have spent eleven years recording the dead and handing the records to people who used them as leverage.", seconds = 3f },
                new DialogueLine { speaker = "Sallow", text = "I am done with leverage.", seconds = 1f },
                new DialogueLine { speaker = "Sallow", text = "If you are building something, if this man is going to do something with what he carries, I want to help.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "You're the tenth.", seconds = 1f },
                new DialogueLine { speaker = "Sallow", text = "The tenth what?", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Ally.", seconds = 1f },
            };
        }

        // ---- HUNTER DUEL (short duel bark) ----

        private static DialogueLine[] GetHunterDuelLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Clean. One strike.", seconds = 1.5f },
            };
        }

        // ---- RECKONING VOW (Cipher's vow; Sallow's affirmation) ----

        private static DialogueLine[] GetReckoningVowLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "I was built to carry the dead and give their truth weight.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "The Dominion turned it into a weapon. The Choir turned it into a ritual.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "I am going to turn it into a reckoning.", seconds = 2f },
                new DialogueLine { speaker = "Sallow", text = "The Pale Choir sings to absolve violence once committed.", seconds = 2.5f },
                new DialogueLine { speaker = "Sallow", text = "We never sang loud enough.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Then we'll sing louder.", seconds = 1f },
            };
        }

        // ---- GALAXY 4 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "The Absolution is a sanctuary for the Pale Choir. Mortis is a High Deacon who trained me as a confessor, a vessel to carry the final truths of the dead through my katana's neural-link. It was never about consumption. It was about custody. Sallow, the Harmonian archivist, broke ranks and gave me the full confession archive, thousands of final moments, including those of worlds the Dominion murdered and erased.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "Those confessions name planets, Cipher. Entire worlds the Dominion ordered cleansed. Full extinction, no survivors, no record. Except in the archive. Sallow gave you coordinates to every ordered genocide. That's the heading now, proof that those worlds existed, and a chance to speak their names before anyone else remembers to mourn them.", seconds = 5.5f },
            };
        }
    }
}
