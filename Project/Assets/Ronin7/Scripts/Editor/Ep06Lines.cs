using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 6 dialogue data. Transposed from ep06-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep06_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// </summary>
    public static class Ep06Lines
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
            "approach_transmission",
            "medical_barks",
            "khall_hologram",
            "orbital_ultimatum",
            "yard_assault_barks",
            "gantry_monologue",
            "gantry_elite_barks",
            "archive_deal",
            "archive_barks",
            "failsafe_activation",
            "collapse_barks",
            "escape_jammer",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "approach_transmission" => GetApproachTransmissionLines(),
                "medical_barks" => GetMedicalBarksLines(),
                "khall_hologram" => GetKhallHologramLines(),
                "orbital_ultimatum" => GetOrbitalUltimatumLines(),
                "yard_assault_barks" => GetYardAssaultBarksLines(),
                "gantry_monologue" => GetGantryMonologueLines(),
                "gantry_elite_barks" => GetGantryEliteBarksLines(),
                "archive_deal" => GetArchiveDealLines(),
                "archive_barks" => GetArchiveBarksLines(),
                "failsafe_activation" => GetFailsafeActivationLines(),
                "collapse_barks" => GetCollapseBarksLines(),
                "escape_jammer" => GetEscapeJammerLines(),
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

        /// <summary>Generate clip name for a line: ep06_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep06_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- SIGNAL IN THE DARK: APPROACH TRANSMISSION (Beat 1) ----

        private static DialogueLine[] GetApproachTransmissionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "That transmission from the relay station came through encrypted. I cracked it. It's a Dominion retrieval order.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "Content?", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "The same hand that gave the Kethel-7 kill order is still hunting the ones who survived it. And this time it's signed, Overseer Khall. Your designation's at the top of his list.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "I still don't remember it. Only what Ronin-9 told me.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Ronin-9 and the others scattered to the vault sites after the Rust Collective, hunting their sleeping kin. It's just us out here, and Khall knows it.", seconds = 6f },
                new DialogueLine { speaker = "Kessler", text = "There. Frosthold colony. Below. Military distress beacon running on loop. Could be salvage.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "We leave it.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "The coordinates alone could fetch half a credit if we...", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "Cipher. This is Overseer. Come home. I have left you a gift.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Ronin. Ronin-7.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "Cut the channel.", seconds = 1f },
            };
        }

        // ---- FROSTHOLD DESCENT: MEDICAL COMBAT BARKS (Beat 2) ----

        private static DialogueLine[] GetMedicalBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Hollow Kings Operative 1", text = "Contact! Descender pod on approach!", seconds = 1.5f },
                new DialogueLine { speaker = "Hollow Kings Operative 2", text = "Lock down the wing! All access points sealed!", seconds = 1.5f },
                new DialogueLine { speaker = "Hollow Kings Operative 3", text = "Movement in the northeast corridor!", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "Stay mobile. Don't let them box you in.", seconds = 2f },
                new DialogueLine { speaker = "Hollow Kings Operative 4", text = "Two down! Fall back to the medical console!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Medical console is reading active. Data chip detected. Don't touch it.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "I'm playing it.", seconds = 1.5f },
            };
        }

        // ---- KHALL'S HOLOGRAM MESSAGE (Beat 3) ----

        private static DialogueLine[] GetKhallHologramLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "You were my finest creation, Cipher. I molded you myself. Your hands. Your sight. The way you breathe before a strike.", seconds = 5f },
                new DialogueLine { speaker = "Khall", text = "All of it came from me. All of it was mine to give. And I gave it to make you a weapon without question, without doubt, without mercy.", seconds = 5f },
                new DialogueLine { speaker = "Khall", text = "You are coming back to me, whether you remember wanting to or not. The leash I built into you still answers to my hand.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "What did you find?", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "A name. Cipher. He says it's mine. And he says there's a leash still wired into me, and he's still holding it.", seconds = 4f },
            };
        }

        // ---- ORBITAL ULTIMATUM: THE BREACH (Beat 4) ----

        private static DialogueLine[] GetOrbitalUltimatumLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "We've got company. Multiple signatures. Dominion fleet markers.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "Cipher. You have one hour to surrender. If you do not, I will burn the colony with the salvager inside it.", seconds = 5f },
                new DialogueLine { speaker = "Khall", text = "And the closer you come, the more you will feel it, the part of you I built to obey. It never left. Come alone. Let us speak as we used to. As creator and blade.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "We're leaving. Now.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Negative. We're not outrunning a fleet drop. We fight down, get you to ground, lose them in the colony ruins.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "He said he'd kill you.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "He said a lot of things. Men like that always do. But I've read Khall's file. And I know he needs you alive more than he needs you dead. Otherwise that ultimatum would have been a sentence.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "I need to hear what he's going to say.", seconds = 2f },
            };
        }

        // ---- LANDING YARD ASSAULT: COMBAT BARKS (Beat 5) ----

        private static DialogueLine[] GetYardAssaultBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Trooper 1", text = "Contact on the landing pad! Return fire!", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Trooper 2", text = "Spread formation! Establish a perimeter!", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Trooper 3", text = "Moving to engage on the left flank!", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "Fuel depot two hundred meters east. Try it.", seconds = 2f },
                new DialogueLine { speaker = "Dominion Trooper 4", text = "Decompression! Seal the inner bulkhead!", seconds = 1.5f },
                new DialogueLine { speaker = "Khall", text = "I taught you that maneuver, Cipher. The forced breach into close quarters. You learned it in the third year of your training.", seconds = 4f },
                new DialogueLine { speaker = "Khall", text = "Come inside alone. Let us talk about the orphanage. Let us talk about Kethel-7.", seconds = 4f },
            };
        }

        // ---- CENTRAL HUB: GANTRY MONOLOGUE (Beat 6) ----

        private static DialogueLine[] GetGantryMonologueLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "Welcome home, Cipher.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "I don't remember you.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "No. But your hands do. Every angle you cut today, every breath before a strike, I put those there. I did not command you, Cipher. I built you. Bone and reflex and silence. You are the finest thing I ever made.", seconds = 7f },
                new DialogueLine { speaker = "Khall", text = "You remember Kethel-7 now. The hydroponics. The choir we kept singing through your meditations. More than seventy children in that station.", seconds = 5f },
                new DialogueLine { speaker = "Khall", text = "I sent you in to purge a cell hiding among them. And you did it. Every one of them. Flawless, exactly as I made you to be.", seconds = 5f },
                new DialogueLine { speaker = "Khall", text = "And then something I never coded into you woke up. You broke. You turned on us and walked out. In seventeen years, not one of my operatives had ever broken all the way through. You did.", seconds = 6f },
                new DialogueLine { speaker = "Khall", text = "So I sent the order to erase you, wipe you to a blank slate, the way we reset the failures. It should have ended what you were. Instead you woke half-deleted, and gone. I still cannot explain how you lived.", seconds = 6f },
                new DialogueLine { speaker = "Khall", text = "Your codename is Cipher. It has always been Cipher. And you were mine to build.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "Why?", seconds = 1f },
                new DialogueLine { speaker = "Khall", text = "Because obedience that has never been tested is not obedience at all. I had to know there was nothing left in you I had not put there. Cutting away everything else, that was the lesson.", seconds = 5f },
            };
        }

        // ---- GANTRY FIGHT: ELITE BARKS (Beat 7) ----

        private static DialogueLine[] GetGantryEliteBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Elite 1", text = "Cipher recognized. Engaging at full capacity.", seconds = 2f },
                new DialogueLine { speaker = "Dominion Elite 2", text = "Engaging neural-leash override.", seconds = 2f },
                new DialogueLine { speaker = "Dominion Elite 1", text = "Predictable. You favor the left deception.", seconds = 2f },
                new DialogueLine { speaker = "Dominion Elite 2", text = "Countering with the cascade. The one you taught us.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "Improvisation. That's new.", seconds = 2f },
            };
        }

        // ---- SEALED RECORDS CHAMBER: ARCHIVE DEAL (Beat 8) ----

        private static DialogueLine[] GetArchiveDealLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "The Hollow Kings cached everything they could steal from Dominion archives. Training files. Command structures. Personnel records. It's all here.", seconds = 5f },
                new DialogueLine { speaker = "Khall", text = "I want you to retrieve them. Gather the data core and bring it back to me. And I will tell you your name. Not an operator tag. Not Cipher. The name you had before we rewrote you.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "And if I refuse?", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "Then I leave you to the Dominion's hunters and the leash I still hold. But you won't refuse, will you? Somewhere inside that erased brain you remember what it was to have a real name. And you want it back.", seconds = 6f },
                new DialogueLine { speaker = "Khall", text = "I should tell you something else, though. Something you should know before you go down there.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "The Program never stopped. While you have been drifting, learning to feel, the machine that made you has kept running, children taken, broken, rebuilt into weapons. Hundreds of them, right now, across the Dominion's reach.", seconds = 6f },
                new DialogueLine { speaker = "Khall", text = "Come home and you take your place again, at my side, at the head of it. The rest will answer to the blade I made first. That is what I am offering you, Cipher. Not mercy. Purpose.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "I'll get them.", seconds = 2f },
            };
        }

        // ---- EXPOSED ARCHIVE SECTIONS: COMBAT BARKS (Beat 9) ----

        private static DialogueLine[] GetArchiveBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Hollow Kings Operative 5", text = "Intruder in the north archive! Seal the bulkhead!", seconds = 1.5f },
                new DialogueLine { speaker = "Hollow Kings Operative 6", text = "Multiple casualties! Fall back to secondary position!", seconds = 1.5f },
                new DialogueLine { speaker = "Hollow Kings Operative 7", text = "They're coming through the east wing! Concentrate fire on the...", seconds = 1.5f },
                new DialogueLine { speaker = "Hollow Kings Operative 8", text = "The core is...", seconds = 1f },
                new DialogueLine { speaker = "Ronin-7", text = "Got the core. And, a kit, sealed under it. Dominion issue. Throwing blades, balanced for a hand like mine.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Those standard for your kind?", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "My hands knew them before I did. They're mine.", seconds = 2.5f },
            };
        }

        // ---- KHALL'S FAILSAFE ACTIVATION (Beat 10) ----

        private static DialogueLine[] GetFailsafeActivationLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "There. I have just sent the word to your leash.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "I am not killing you, Cipher. I am reminding you who holds the cord. Feel it? The same signal that should have erased you on Kethel-7, still wired to my hand.", seconds = 5f },
                new DialogueLine { speaker = "Khall", text = "Come home, and it stays quiet. Walk back into the Program, and I never pull it again. That is the only mercy left for something like you.", seconds = 5f },
                new DialogueLine { speaker = "Khall", text = "Stay with your salvager, stay free, and I will keep reaching for you. Every quiet moment. Until the day your own hands turn the blade around and carry you home to me. Your choice.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "Khall.", seconds = 1f },
                new DialogueLine { speaker = "Khall", text = "Yes?", seconds = 1f },
                new DialogueLine { speaker = "Ronin-7", text = "Kethel-7. Did you know I would break?", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "No. I truly did not. That's what makes you remarkable, Cipher. And what makes you dangerous. Goodbye.", seconds = 3f },
            };
        }

        // ---- COLLAPSING FACILITY: ESCAPE BARKS (Beat 11) ----

        private static DialogueLine[] GetCollapseBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Trooper 5", text = "Core temperature critical! Evacuate all personnel!", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Trooper 6", text = "Casualty at the western stairwell! We've lost comms with...", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Trooper 7", text = "Prisoner breach! Seal the east corridor!", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Trooper 8", text = "Breach through the zero-pressure corridor! Get to the landing platform!", seconds = 1.5f },
            };
        }

        // ---- ESCAPE: POST-FROSTHOLD (Beat 12) — setId kept as "escape_jammer" for build wiring; jammer removed in trim ----

        private static DialogueLine[] GetEscapeJammerLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Talk to me. What did he do to you?", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "He reached into me. The part that was built to obey, he pulled on it. Tried to walk me back to him without a fight.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "And?", seconds = 1f },
                new DialogueLine { speaker = "Ronin-7", text = "It didn't take. I'm still mine.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "But the switch he fired on Kethel-7, it was meant to erase me. It should have. It didn't, then or now. That is not a flaw, Kessler.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "Someone built it to fail. Someone wanted me to survive my own execution.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Then somebody out there is on your side. We find them, whoever they are.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "How many others?", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Hundreds. The Program never stopped, children taken right now, broken into what I am. He offered me a place at the head of it.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "I brought something back instead.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Throwing blades. Dominion work.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "Program issue. My hands knew them before my mind did. Whatever else they took, they trained these into me, and I'm taking them back.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "My name was Cipher. That's all he would tell me. Everything before that is gone.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Then we get the rest back. We find what he took. And we burn his Program down with it.", seconds = 3f },
            };
        }
    }
}
