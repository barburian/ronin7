using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 10 dialogue data. Transposed from ep10-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep10_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 10 spans the rescue of Cipher from Velloch's Reach sanctuary and the discovery of
    /// the Dominion's "Kael Vor" fabrication—a false identity designed to be erased instead of the person.
    /// </summary>
    public static class Ep10Lines
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
            "descent_contact",
            "gauntlet_barks",
            "sanctuary_threshold",
            "kael_vor",
            "first_breach",
            "the_file",
            "chokepoint_barks",
            "directive_seven",
            "flank_warning",
            "pattern_broken",
            "the_photograph",
            "rescue_run",
            "teen_door",
            "the_architecture",
            "lz_barks",
            "landing_zone_go",
            "extraction_picket",
            "ascent_epilogue",
            "space_ep10_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "descent_contact" => GetDescentContactLines(),
                "gauntlet_barks" => GetGauntletBarksLines(),
                "sanctuary_threshold" => GetSanctuaryThresholdLines(),
                "kael_vor" => GetKaelVorLines(),
                "first_breach" => GetFirstBreachLines(),
                "the_file" => GetTheFileLines(),
                "chokepoint_barks" => GetChokepointBarksLines(),
                "directive_seven" => GetDirectiveSevenLines(),
                "flank_warning" => GetFlankWarningLines(),
                "pattern_broken" => GetPatternBrokenLines(),
                "the_photograph" => GetThePhotographLines(),
                "rescue_run" => GetRescueRunLines(),
                "teen_door" => GetTeenDoorLines(),
                "the_architecture" => GetTheArchitectureLines(),
                "lz_barks" => GetLzBarksLines(),
                "landing_zone_go" => GetLandingZoneGoLines(),
                "extraction_picket" => GetExtractionPicketLines(),
                "ascent_epilogue" => GetAscentEpilogueLines(),
                "space_ep10_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep10_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep10_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- IN TRANSIT: TRANSIT TALK + SIGNAL DISCOVERY (Beats 1-3) ----

        private static DialogueLine[] GetDescentContactLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "You've been standing there since Cinders.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "I know I refused the order. I remember the shape of it now. I know what I chose and what I failed to stop.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "But I still don't know what they called me before any of this was built.", seconds = 2.5f },
                new DialogueLine { speaker = "Mera Voss", text = "The syndicates in Galaxy 2 have been trading memory fragments for years. Records, acquisition logs, erased files. Whatever the program stripped, there's a market for what was taken.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "There's something I've been holding back. A signal I intercepted three days ago. I wanted to wait until we were clear of Cinders' orbit before I said anything.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Tell me.", seconds = 1f },
                new DialogueLine { speaker = "Iris", text = "The colony on record was a mining operation. Closed fifteen years ago. But the registry signal predates the mine by twelve years.", seconds = 3.5f },
                new DialogueLine { speaker = "Iris", text = "There's no administrative record of any orphanage or welfare operation. But the frequency is registered to something called Haven Home.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "Haven Home was a real orphanage. I ran into the name in my Dominion logistics days, buried in acquisition records, flagged for selective erasure. It was active for at least thirty years.", seconds = 4.5f },
                new DialogueLine { speaker = "Kessler", text = "The Dominion erased its records. All of them. That colony doesn't exist on any official chart I've ever seen.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "How many children did Haven Home take in?", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Hundreds over three decades. The Dominion's acquisition logs list it as a primary intake source for the program.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Set course.", seconds = 1f },
                new DialogueLine { speaker = "Sister Meredith", text = "The Dominion told me Operative 7 was dead. If you are only salvagers, you will have no trouble with my welcome.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "One mine. Hull integrity holding. She's been building this approach grid for a long time.", seconds = 2.5f },
            };
        }

        // ---- SURFACE GAUNTLET: COMBAT BARKS (Beat 4) ----

        private static DialogueLine[] GetGauntletBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Nine proximity drones across the field. You're reading them all?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Reading them.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "She's been building this gauntlet for twenty-five years.", seconds = 2f },
            };
        }

        // ---- SANCTUARY ENTRANCE: RECOGNITION (Beat 5, first half) ----

        private static DialogueLine[] GetSanctuaryThresholdLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sister Meredith", text = "You look exactly like him.", seconds = 2f },
                new DialogueLine { speaker = "Sister Meredith", text = "Forgive me. There was a child I raised. He has been dead for twenty-five years.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Tell me about him.", seconds = 1.5f },
            };
        }

        // ---- HAVEN HOME ACCOUNT: THE NAME REVEAL (Beat 5, second half) ----

        private static DialogueLine[] GetKaelVorLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sister Meredith", text = "I ran Haven Home for thirty-one years. Real children. Real work. Difficult, underfunded, thankless, and the only thing I ever did that mattered.", seconds = 4f },
                new DialogueLine { speaker = "Sister Meredith", text = "Men in gray came twice, two years apart. They spoke of selection programs. Better facilities. Full technical apprenticeships and a future I couldn't give them.", seconds = 4f },
                new DialogueLine { speaker = "Sister Meredith", text = "I believed them. I had thirty children and twelve cots. I believed the ones I sent got the lives I couldn't provide.", seconds = 3.5f },
                new DialogueLine { speaker = "Sister Meredith", text = "The first selection, I gave them six children. There was one, the brightest of all of them, and the most difficult, and the most impossible not to love.", seconds = 4f },
                new DialogueLine { speaker = "Sister Meredith", text = "I sent him with his file. The name on his intake record. The name I knew.", seconds = 2.5f },
                new DialogueLine { speaker = "Sister Meredith", text = "Kael Vor.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Kael Vor.", seconds = 1.5f },
                new DialogueLine { speaker = "Sister Meredith", text = "You don't remember it.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "No.", seconds = 1f },
            };
        }

        // ---- FIRST DOMINION BREACH (Beat 6) ----

        private static DialogueLine[] GetFirstBreachLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Intercept craft in the upper atmosphere, they tracked the pulse. At least six troopers deploying through the vent shafts.", seconds = 3f },
                new DialogueLine { speaker = "Sister Meredith", text = "I know these corridors.", seconds = 1f },
                new DialogueLine { speaker = "Dominion Trooper 1", text = "It's a Ronin, it's a...", seconds = 1.5f },
            };
        }

        // ---- THE FILE: KAL-7 / KAEL VOR FABRICATION (Beat 7) ----

        private static DialogueLine[] GetTheFileLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sister Meredith", text = "I decoded the acquisition log three years ago. I had to learn two levels of Dominion cipher. Eleven months.", seconds = 3f },
                new DialogueLine { speaker = "Sister Meredith", text = "Kael Vor was not his name. It was a fabricated history, a name and a file built so the child would believe in something.", seconds = 4.5f },
                new DialogueLine { speaker = "Sister Meredith", text = "And so that I would grieve it, when the time came, instead of the person they actually took.", seconds = 3.5f },
                new DialogueLine { speaker = "Sister Meredith", text = "I never knew his real name.", seconds = 2f },
                new DialogueLine { speaker = "Sister Meredith", text = "Neither did he.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "They gave me a name so there was something to erase. And so that you would grieve the name instead of tracking the person.", seconds = 4f },
                new DialogueLine { speaker = "Sister Meredith", text = "Yes.", seconds = 1f },
            };
        }

        // ---- THE CHOKEPOINT: HOLDING THE LINE (Beat 8) ----

        private static DialogueLine[] GetChokepointBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Collapse is holding but they'll route around it in ten minutes. If there's a third wave we're out of time.", seconds = 3f },
                new DialogueLine { speaker = "Sister Meredith", text = "The service tunnel divides at junction four. If they take the left fork they come out behind us.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "How many more can the intercept craft carry?", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Standard carry is twelve. We've seen eight. Either that's the last of them or there's a second craft we haven't clocked yet.", seconds = 3.5f },
            };
        }

        // ---- DIRECTIVE SEVEN: KHALL'S RECORDING (Beat 9) ----

        private static DialogueLine[] GetDirectiveSevenLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall (recorded)", text = "Operative 7 keeps fracturing. We traced it to pre-acquisition bonding. The matron made him too human first.", seconds = 3.5f },
                new DialogueLine { speaker = "Khall (recorded)", text = "The failsafe was designed for memory removal. But Directive Seven was never about memory.", seconds = 3f },
                new DialogueLine { speaker = "Khall (recorded)", text = "It strips the humanity, the attachment, the refusal, the will to disobey. We do not want dead operatives. We want hollow ones.", seconds = 4.5f },
                new DialogueLine { speaker = "Khall (recorded)", text = "Purge the self. Leave the reflexes.", seconds = 2f },
                new DialogueLine { speaker = "Khall (recorded)", text = "Kael Vor never existed. We gave him the name so there was something to erase. Something for the record, and something for the woman.", seconds = 4f },
                new DialogueLine { speaker = "Sister Meredith", text = "I have listened to that recording forty-seven times. The first time I understood it, I was sick for three days.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "The chip wasn't built to kill me. It was built to hollow me out. To remove the refusal before the next assignment.", seconds = 4f },
                new DialogueLine { speaker = "Sister Meredith", text = "It did not take.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "The evidence of that is Kethel-7.", seconds = 1.5f },
            };
        }

        // ---- THE ENFORCER: FLANK WARNING (Beat 10 intro) ----

        private static DialogueLine[] GetFlankWarningLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Flanking squad on the surface, east side. They'll reach the sanctuary exits in eight minutes if they come through uncontested.", seconds = 3f },
            };
        }

        // ---- AFTER THE FIGHT: PATTERN BROKEN (Beat 11) ----

        private static DialogueLine[] GetPatternBrokenLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "The enforcer had my behavioral file. Every movement I'd make if the program was running.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "I beat it by not running the program.", seconds = 2f },
                new DialogueLine { speaker = "Sister Meredith", text = "That will always be true.", seconds = 1.5f },
            };
        }

        // ---- THE PHOTOGRAPH: LYSSE AND THE ORIGINAL NAME (Beat 12) ----

        private static DialogueLine[] GetThePhotographLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sister Meredith", text = "This is Lysse. She was in your dormitory. Two years younger than you, she called you her big brother and you pretended to object to it.", seconds = 3.5f },
                new DialogueLine { speaker = "Sister Meredith", text = "The men in gray came back two years after they took you. They selected Lysse. I don't know where she went. The acquisition log lists her as Operative 3, of the cohort after yours.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "You believed Kael Vor was real.", seconds = 2f },
                new DialogueLine { speaker = "Sister Meredith", text = "The Dominion needed me to. So that if you ever surfaced, I would anchor you to a false root. A fabricated grief instead of a real person to recognize.", seconds = 4f },
                new DialogueLine { speaker = "Sister Meredith", text = "But you came back as Cipher. As Operative 7. As whatever the program built from what they took.", seconds = 3.5f },
                new DialogueLine { speaker = "Sister Meredith", text = "The person I recognize, the one who will not leave this place until every child in the deep chamber is out, that person existed before any name they gave you.", seconds = 4.5f },
            };
        }

        // ---- RESCUE RUN: BIOSIGNS AND ASCENT (Beat 13 barks) ----

        private static DialogueLine[] GetRescueRunLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Three biosigns moving toward the main chamber. Surface in four minutes, the intercept craft's orbit is bringing it back into firing range.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Moving.", seconds = 0.5f },
            };
        }

        // ---- TEEN DOOR: SHELTER FOUND (Beat 13 scene) ----

        private static DialogueLine[] GetTeenDoorLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Teenager 1", text = "Is it over? Are we... are we getting out?", seconds = 2f },
                new DialogueLine { speaker = "Teenager 2", text = "Mother Meredith said someone was coming. I thought she meant...", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "Stay behind me. Time to move.", seconds = 1.5f },
            };
        }

        // ---- LANDING ZONE: THE ARCHITECTURE FAILED (Beat 14) ----

        private static DialogueLine[] GetTheArchitectureLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "They built the Program to erase humanity. Not memory. The attachment, the refusal, the piece that makes disobedience possible. Directive Seven was designed to remove it before first deployment.", seconds = 4f },
                new DialogueLine { speaker = "Sister Meredith", text = "Applied before first deployment. Designed to be irreversible.", seconds = 2.5f },
                new DialogueLine { speaker = "Sister Meredith", text = "By every operational parameter they developed, an operative who still possessed the original trait after deployment was classified as a hardware failure. The system had a procedure for that.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "The chip fired at Kethel-7. That was the system's response to detecting that the trait was still present.", seconds = 3f },
                new DialogueLine { speaker = "Sister Meredith", text = "By their design, you should be hollow or dead. Both systems were applied and both failed in the same operative.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Their entire architecture of control failed in one operative.", seconds = 2f },
                new DialogueLine { speaker = "Sister Meredith", text = "I have spent twenty-five years asking why a well-engineered system failed at a specific point. I do not have the answer.", seconds = 3.5f },
                new DialogueLine { speaker = "Sister Meredith", text = "But I think it is the most important question you will ever ask. And I think they knew that, which is why they gave you a false name to grieve instead.", seconds = 4.5f },
            };
        }

        // ---- LANDING ZONE: GUNSHIP APPROACH (Beat 15 barks) ----

        private static DialogueLine[] GetLzBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Gunship on strafing approach, all guns hot. Hold the zone!", seconds = 2f },
                new DialogueLine { speaker = "Mera Voss", text = "I've got the guns. Laying down suppressive fire.", seconds = 2f },
            };
        }

        // ---- LANDING ZONE: EVAC POD LAUNCH (Beat 15 finale) ----

        private static DialogueLine[] GetLandingZoneGoLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "We're clear, pod launch point is fifty meters east of your position! GO!", seconds = 2f },
            };
        }

        // ---- EXTRACTION: SPACE-FIGHT BARKS (Beat 16 ascent) ----

        private static DialogueLine[] GetExtractionPicketLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Strike detachment breaking through, they're on an intercept vector.", seconds = 2f },
                new DialogueLine { speaker = "Mera Voss", text = "I see them. Coming in hard and fast.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Pod's away. We're clear.", seconds = 1f },
            };
        }

        // ---- VIEWPORT: THE UNANSWERED QUESTION (Beat 17 epilogue) ----

        private static DialogueLine[] GetAscentEpilogueLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "They gave me a false name so there was something to erase. They ran Directive Seven to strip what conditioning could not reach. They purged me when the chip failed anyway.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "The Program was designed specifically to destroy the thing you're describing. Not a side effect. Not a flaw in the methodology. The entire purpose.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Why did mine survive?", seconds = 1.5f },
                new DialogueLine { speaker = "Sister Meredith", text = "I do not know.", seconds = 1.5f },
                new DialogueLine { speaker = "Sister Meredith", text = "But whatever held, it held through Directive Seven, it held through Kethel-7, and it is holding now. Whatever it is, it has been the durable thing.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Kael Vor was built to be dismantled. Cipher is a weapon's designation. Beneath both of them, something survived a process specifically engineered to destroy it.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "I don't know its name yet. But I know it's there. And I know the question.", seconds = 3f },
                new DialogueLine { speaker = "Sister Meredith", text = "Figure out what it is. That is the question they spent everything they had trying to make sure you never asked.", seconds = 3.5f },
                new DialogueLine { speaker = "Katana", text = "And I know the name. Kael Vor was never in me, but the true one is. I have held it since before the wipe: alive, unburned, waiting. When the chip is dead and you are whole enough to carry it, I will give it back.", seconds = 5f },
            };
        }

        // ---- GALAXY 2 HUB CABIN: POST-MISSION BRIEFING (EP11 hook) ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Meredith disembarked at the relay to resettle the teenagers. She said to tell you they found what they needed. The ground beneath them. The names carved in stone.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "What about the file? The acquisition logs. The real name.", seconds = 2.5f },
                new DialogueLine { speaker = "Mera Voss", text = "Meredith left you all of it. Decades of records. The Dominion's own documents. But the answer you're looking for, the name beneath the names, that's not written down anywhere.", seconds = 4f },
                new DialogueLine { speaker = "Mera Voss", text = "Galaxy 2 trades in memory and stolen histories. The Fringe runs deeper. If there's a way to find what was taken, it will be out there, where the syndicates keep what the Dominion couldn't erase.", seconds = 4f },
            };
        }
    }
}
