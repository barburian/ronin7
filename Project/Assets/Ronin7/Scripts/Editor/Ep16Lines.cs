using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 16 dialogue data. Transposed from ep16-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep16_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 16 spans Cipher's arrival at the hidden monastery on Cinder Vale, the training and revelation
    /// of his ongoing servitude, Khall's arrival and the severance of the locator beacon, and Cipher's choice
    /// to lay down the sword and understand the true nature of freedom.
    /// </summary>
    public static class Ep16Lines
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
            "transit_intro",
            "salvage_barks",
            "sela_intro",
            "descent",
            "morrow_intro",
            "sela_practice",
            "pilgrim",
            "corvette_barks",
            "locator_reveal",
            "makers_truth",
            "overseer_arrival",
            "the_offer",
            "the_severance",
            "garden_remade",
            "departure",
            "cage_revealed",
            "sword_laid_down",
            "silent_garden",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "transit_intro" => GetTransitIntroLines(),
                "salvage_barks" => GetSalvageBarksLines(),
                "sela_intro" => GetSelaIntroLines(),
                "descent" => GetDescentLines(),
                "morrow_intro" => GetMorrowIntroLines(),
                "sela_practice" => GetSelaPracticeLines(),
                "pilgrim" => GetPilgrimLines(),
                "corvette_barks" => GetCorvetteBarksLines(),
                "locator_reveal" => GetLocatorRevealLines(),
                "makers_truth" => GetMakersTruthLines(),
                "overseer_arrival" => GetOverseerArrivalLines(),
                "the_offer" => GetTheOfferLines(),
                "the_severance" => GetTheSeveranceLines(),
                "garden_remade" => GetGardenRemadeLines(),
                "departure" => GetDepartureLines(),
                "cage_revealed" => GetCageRevealedLines(),
                "sword_laid_down" => GetSwordLaidDownLines(),
                "silent_garden" => GetSilentGardenLines(),
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

        /// <summary>Generate clip name for a line: ep16_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep16_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BEAT 1: THE WEIGHT OF THE QUESTION (Transit Intro) ----

        private static DialogueLine[] GetTransitIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Is there life after being a weapon?", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "You can ask that every day for the rest of your life, or you can find someone who already answered it.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Frostwind Reach. Unmarked stellar chart. There's a dead world here, Cinder Vale. I've stopped here before.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "Drive coils are overheating. We're losing power fast. Emergency descent initiated.", seconds = 2.5f },
            };
        }

        // ---- BEAT 2: THE SALVAGE PIRATES (Salvage Barks) ----

        private static DialogueLine[] GetSalvageBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Salvage Pirate 1", text = "New wreck! Easy pickings!", seconds = 1.5f },
                new DialogueLine { speaker = "Salvage Pirate 2", text = "Come on! They're grounded!", seconds = 1f },
            };
        }

        // ---- BEAT 3: SELA EMERGES (Sela Intro) ----

        private static DialogueLine[] GetSelaIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sela", text = "This is Ashfall. We don't ask questions.", seconds = 2f },
                new DialogueLine { speaker = "Sela", text = "We ask forgiveness instead, but first you have to remember why you need it.", seconds = 3f },
                new DialogueLine { speaker = "Sela", text = "Your pilot took burn-damage from the descent. He's lucky the coils didn't rupture. He's lucky the blast didn't take him.", seconds = 3.5f },
            };
        }

        // ---- BEAT 3: INTO THE DEPTHS (Descent) ----

        private static DialogueLine[] GetDescentLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "How far underground are we?", seconds = 1.5f },
                new DialogueLine { speaker = "Sela", text = "Far enough that the Dominion stopped looking for us decades ago. We're below their sensors. Below their reach. The world is dead. The monastery is alive.", seconds = 4f },
            };
        }

        // ---- BEAT 4: THE ABBOT MORROW (Morrow Intro) ----

        private static DialogueLine[] GetMorrowIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrow", text = "Sela says you woke up without a past.", seconds = 2f },
                new DialogueLine { speaker = "Morrow", text = "That is mercy pretending to be cruelty. But you are welcome here regardless of your history.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "I have a name. A designation. The rest is blank.", seconds = 2f },
                new DialogueLine { speaker = "Morrow", text = "Then you have your own choice to make. You may rest in this hall and ask forgiveness of ghosts that may not even remember their names. Or you may work in the Blade Garden and understand the discipline beneath violence.", seconds = 5f },
                new DialogueLine { speaker = "Morrow", text = "Either way, you will do it as yourself. Not as a weapon.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "The Blade Garden.", seconds = 1f },
            };
        }

        // ---- BEAT 5: THE PRACTICE (Sela Practice) ----

        private static DialogueLine[] GetSelaPracticeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sela", text = "You fight like someone trying to remember.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "I don't remember the training.", seconds = 1.5f },
                new DialogueLine { speaker = "Sela", text = "Your body does. Your body remembers everything you've forgotten.", seconds = 2.5f },
                new DialogueLine { speaker = "Sela", text = "I have been here five years. Before that I don't talk about. But I know that look, it's the one I had when I arrived. You can see the shape of the cage, but you still think the door is a lie.", seconds = 4.5f },
                new DialogueLine { speaker = "Cipher", text = "What was your name? Before the monastery?", seconds = 2f },
                new DialogueLine { speaker = "Sela", text = "That operative is dead. Don't mourn her. She chose to die here, and something else chose to live instead.", seconds = 3.5f },
            };
        }

        // ---- BEAT 6: THE PILGRIM (Pilgrim) ----

        private static DialogueLine[] GetPilgrimLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrow", text = "Overseer Khall is offering a recovery bounty on a lost asset carrying classified neural implants.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "Ronin-7 was the finest of his generation. The failsafe was meant to be merciful. I am offering this bounty not as a sanction, but as a courtesy. He was something worth making.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "He broadcast that knowing I would hear it.", seconds = 2f },
                new DialogueLine { speaker = "Morrow", text = "Khall is hunting you. He always was.", seconds = 2f },
            };
        }

        // ---- BEAT 7: THE CORVETTE ASSAULT (Corvette Barks) ----

        private static DialogueLine[] GetCorvetteBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Soldier 1", text = "Monastery breach! Hostiles engaging in central corridor!", seconds = 2f },
                new DialogueLine { speaker = "Dominion Soldier 2", text = "Clearing meditation hall! Stand by for thermal scan!", seconds = 1.5f },
            };
        }

        // ---- BEAT 8: THE LOCATOR REVELATION (Locator Reveal) ----

        private static DialogueLine[] GetLocatorRevealLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrow", text = "The chip in your spine is broadcasting. Not the purge sequence, a secondary function. A locator.", seconds = 3.5f },
                new DialogueLine { speaker = "Morrow", text = "They allowed you to believe you were free while the leash stayed invisible.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "The Dominion never lost me.", seconds = 1.5f },
                new DialogueLine { speaker = "Morrow", text = "No. They were always watching.", seconds = 1.5f },
            };
        }

        // ---- BEAT 9: THE MAKER'S TRUTH (Makers Truth) ----

        private static DialogueLine[] GetMakersTruthLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrow", text = "I was a Dominion architect. One of the early designers of operative conditioning, a precursor to everything later built into your generation.", seconds = 4f },
                new DialogueLine { speaker = "Morrow", text = "I designed the first versions of the locator function. I trained your handler Khall when he was still a boy. I built the machine that is tearing you apart, one second at a time.", seconds = 4f },
                new DialogueLine { speaker = "Morrow", text = "Then I saw what the program became and I faked my own death.", seconds = 2.5f },
                new DialogueLine { speaker = "Morrow", text = "I came to this place to stop killing. You didn't come for peace, you came because you have nowhere else to go. But I am asking you the same question I once asked myself: will you turn that weapon on the people who built you, or will you learn that putting the sword down is the only real victory?", seconds = 6f },
            };
        }

        // ---- BEAT 10: THE OVERSEER'S ARRIVAL (Overseer Arrival) ----

        private static DialogueLine[] GetOverseerArrivalLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "I knew you would come to this place. Every operative eventually looks for a way to stop.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "There is no peace in renunciation, Ronin-7. There is only the lie you tell yourself while the purge continues.", seconds = 4f },
                new DialogueLine { speaker = "Morrow", text = "I built the first version of him. And I will not let you break what is left.", seconds = 3f },
            };
        }

        // ---- BEAT 11: THE OFFER (The Offer) ----

        private static DialogueLine[] GetTheOfferLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "Your augments are a living record of everything we built. Every syndicate in this galaxy will hunt you for the architecture alone.", seconds = 3.5f },
                new DialogueLine { speaker = "Khall", text = "Come back to the Dominion and I can restore your full capacity. You could find out what you were truly made for.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "Or you can stay here and die slowly in the dark, wondering if the peace was real or just the slowest possible form of cruelty.", seconds = 4f },
            };
        }

        // ---- BEAT 12: THE SEVERANCE (The Severance) ----

        private static DialogueLine[] GetTheSeveranceLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "You cut yourself open for a signal relay. The purge is still running. You are still dying.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "I know.", seconds = 1f },
                new DialogueLine { speaker = "Morrow", text = "The beacon was never the leash. The leash is what he believes about himself. But now you have no signal to follow, Overseer. You have no business here.", seconds = 4f },
            };
        }

        // ---- BEAT 13: THE GARDEN REMADE (Garden Remade) ----

        private static DialogueLine[] GetGardenRemadeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrow", text = "You are learning. But you carry ghosts, a massacre, an orphanage, choices made and choices made in your name. You will need to know the difference between them before this is over.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "How will I know?", seconds = 1.5f },
                new DialogueLine { speaker = "Morrow", text = "You won't, not yet. Build a life anyway. The truth finds you when you are ready to hold it.", seconds = 3f },
            };
        }

        // ---- BEAT 14: THE DEPARTURE (Departure) ----

        private static DialogueLine[] GetDepartureLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "I pulled a body out of the void and it turned out to be a man. I am glad he woke up.", seconds = 3f },
            };
        }

        // ---- BEAT 15: THE CAGE REVEALED (Cage Revealed) ----

        private static DialogueLine[] GetCageRevealedLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sela", text = "Before I came here, I served a syndicate that believed peace was weakness. We fought constantly, against three other syndicates, rotating enemies, always funded just enough to keep the killing going.", seconds = 5f },
                new DialogueLine { speaker = "Sela", text = "I was years into the work before I understood the pattern. The wars never ended. They just rotated. Enemies changed sides. Debts accumulated. And somewhere above all of it, someone was keeping score.", seconds = 5f },
                new DialogueLine { speaker = "Sela", text = "The Dominion does not rule the galaxies by force alone. It keeps them caged by keeping the syndicates desperate and at war. It seeds the hatred, funds both sides, and collects the debt. Every galaxy is a pit they built to trap the people in it.", seconds = 6f },
            };
        }

        // ---- BEAT 16: THE SWORD LAID DOWN (Sword Laid Down) ----

        private static DialogueLine[] GetSwordLaidDownLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Duty, not destiny.", seconds = 1.5f },
            };
        }

        // ---- BEAT 17: THE SILENT GARDEN (Silent Garden) ----

        private static DialogueLine[] GetSilentGardenLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrow", text = "To forget is to be cursed. To remember without becoming the thing you remembered, that is to be free.", seconds = 4f },
            };
        }
    }
}
