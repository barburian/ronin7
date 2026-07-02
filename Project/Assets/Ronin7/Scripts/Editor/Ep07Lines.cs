using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 7 dialogue data. Transposed from ep07-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep07_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// </summary>
    public static class Ep07Lines
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
            "approach_viewport",
            "market_tessa",
            "enforcer_barks",
            "corridor_choice",
            "hauler_tessa",
            "servitor_fight",
            "kethel_file",
            "guard_assault",
            "architecture_dying",
            "shaft_escape",
            "manifest_transfer",
            "interceptor_pursuit",
            "compassion_anomalies",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "approach_viewport" => GetApproachViewportLines(),
                "market_tessa" => GetMarketTessaLines(),
                "enforcer_barks" => GetEnforcerBarksLines(),
                "corridor_choice" => GetCorridorChoiceLines(),
                "hauler_tessa" => GetHaulerTessaLines(),
                "servitor_fight" => GetServitorFightLines(),
                "kethel_file" => GetKethelFileLines(),
                "guard_assault" => GetGuardAssaultLines(),
                "architecture_dying" => GetArchitectureDyingLines(),
                "shaft_escape" => GetShaftEscapeLines(),
                "manifest_transfer" => GetManifestTransferLines(),
                "interceptor_pursuit" => GetInterceptorPursuitLines(),
                "compassion_anomalies" => GetCompassionAnomaliesLines(),
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

        /// <summary>Generate clip name for a line: ep07_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep07_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BLACKVEIL ORBIT: APPROACH VIEWPORT (Beat 1) ----

        private static DialogueLine[] GetApproachViewportLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "They didn't make a mistake.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "The erasure. It was deliberate. Punishment.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "We're docked. Salvage crew's expecting us in the hub. We move cargo, we move quiet.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "And others. There were others like me.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "We know. Deep-Cache. The Collective. The question is how many more, and where they're sleeping.", seconds = 2.5f },
            };
        }

        // ---- MARKET HUB ENCOUNTER (Beat 2) ----

        private static DialogueLine[] GetMarketTessaLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tessa", text = "You are supposed to be dead.", seconds = 2f },
                new DialogueLine { speaker = "Tessa", text = "Follow the red beacon. Come alone. If Dominion shows up, I burn everything, and you never learn who built you.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Well. That wasn't expected.", seconds = 2f },
            };
        }

        // ---- YARD ENFORCERS FIGHT (Beat 3) ----

        private static DialogueLine[] GetEnforcerBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Yard Enforcer 1", text = "Contact! Contact in the market! Dominion fugitive!", seconds = 2f },
                new DialogueLine { speaker = "Yard Enforcer 2", text = "Cut off the lower gantry! Don't let him reach the corridor!", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Comms jammed! Moving to the dock authority panel!", seconds = 2f },
                new DialogueLine { speaker = "Yard Enforcer 1", text = "He's tearing through our line!", seconds = 1.5f },
                new DialogueLine { speaker = "Yard Enforcer 3", text = "Fall back! Fall back to the upper rig!", seconds = 1.5f },
            };
        }

        // ---- CORRIDOR MAZE / HEADING IN (Beat 4) ----

        private static DialogueLine[] GetCorridorChoiceLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "This is a Dominion hold. You know that.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "Yes.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "Then we do this your way. But the moment this goes wrong, we run.", seconds = 2.5f },
            };
        }

        // ---- DEAD HAULER INTERIOR / TESSA WAITING (Beat 5) ----

        private static DialogueLine[] GetHaulerTessaLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tessa", text = "You came alone. I'm grateful.", seconds = 2.5f },
                new DialogueLine { speaker = "Tessa", text = "My name is Tessa Rin. I was Neural Integration Specialist, Ronin Program, Dominion black-science division.", seconds = 3.5f },
                new DialogueLine { speaker = "Tessa", text = "Clearance level I will not name.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "Then you know what it is doing to me.", seconds = 3f },
                new DialogueLine { speaker = "Tessa", text = "Yes. I fitted the chip myself. I did it to seventeen operatives of your generation. I was proud of the work until I understood what the work was for.", seconds = 4f },
            };
        }

        // ---- VAULT CORRIDOR / SERVITOR DRONE FIGHT (Beat 6) ----

        private static DialogueLine[] GetServitorFightLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Servitor", text = "Unidentified biometric signature. Drone network engaged. Neutralize intruder.", seconds = 2f },
                new DialogueLine { speaker = "Tessa", text = "I built them to recognize that signal. I did not think you would ever come here.", seconds = 3.5f },
            };
        }

        // ---- ARCHIVE INNER CHAMBER / THE KETHEL-7 FILE (Beat 7) ----

        private static DialogueLine[] GetKethelFileLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7 (recorded)", text = "Overseer. Kethel-7 is clear. The mission is complete.", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-7", text = "That's me. I reported it like a cleared floor.", seconds = 2.5f },
                new DialogueLine { speaker = "Tessa", text = "You did. And what they did to you after, that is not what you think.", seconds = 3f },
            };
        }

        // ---- VAULT PERIMETER / DOMINION GUARD FIGHT (Beat 8) ----

        private static DialogueLine[] GetGuardAssaultLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Guard 1", text = "Signature locked! Archive intruders confirmed!", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Guard 2", text = "Rappel insertion complete! Forming perimeter!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "The exit's cut off! Moving deeper!", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Guard 1", text = "Contact is moving inward! Maintain pressure!", seconds = 1.5f },
            };
        }

        // ---- ARCHIVE ROOM / THE ARCHITECTURE OF THE LEASH (Beat 9) ----

        private static DialogueLine[] GetArchitectureDyingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tessa", text = "The chip in your skull is not a failsafe. The Dominion calls it that. It is a killswitch, a leash with a lethal end. One pulse, fired from the other side of the galaxy, and the operative drops. Clean. Final.", seconds = 5.5f },
                new DialogueLine { speaker = "Tessa", text = "Yours was pulled. Days after Kethel-7, after you carried out the order, and then stopped being theirs. You broke. You ran. So they fired your switch to put you down.", seconds = 5.5f },
                new DialogueLine { speaker = "Tessa", text = "It should have killed you where you stood. It did not. It misfired, and took your memory instead of your life.", seconds = 4.5f },
                new DialogueLine { speaker = "Tessa", text = "A killswitch does not fail like that. Not on its own. Someone altered yours. That is not a malfunction.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "Who?", seconds = 1f },
                new DialogueLine { speaker = "Tessa", text = "I cannot tell you. There is a control layer above the killswitch, above my clearance. I fitted the hardware. I never saw the top of it. Whoever reached your chip reached past me.", seconds = 5.5f },
                new DialogueLine { speaker = "Tessa", text = "Understand this: it is still in you. Still live. Still listening. They can try again.", seconds = 3.5f },
                new DialogueLine { speaker = "Tessa", text = "I stole the killswitch schematics when I defected, I owed the operatives that much. Then I learned what the Dominion does with the ones it kills.", seconds = 5f },
                new DialogueLine { speaker = "Tessa", text = "Erased operatives do not die. The Dominion does not waste a weapon. The body is recovered, loaded dormant into a vault, and filed away. Living weapons in cold storage. Insurance.", seconds = 5.5f },
                new DialogueLine { speaker = "Ronin-7", text = "How many?", seconds = 1f },
                new DialogueLine { speaker = "Tessa", text = "Seventeen that I know of, your generation. One to a vault, scattered across the galaxy. I have the manifest. I have been waiting for someone who could carry it.", seconds = 5.5f },
            };
        }

        // ---- CARGO SHAFT ESCAPE / FINAL VAULT FIGHT (Beat 10) ----

        private static DialogueLine[] GetShaftEscapeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Guard 1", text = "They're ascending! Seal lower levels!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Go! Go! I'll hold them!", seconds = 1f },
                new DialogueLine { speaker = "Dominion Guard 2", text = "Lower section compromised! Shift to upper perimeter!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Magnetic locks engaged! Crossing to the ship!", seconds = 2f },
            };
        }

        // ---- ABOARD CORSAIR / MANIFEST TRANSFER (Beat 11) ----

        private static DialogueLine[] GetManifestTransferLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Yard's not pursuing. We're clear for now.", seconds = 2.5f },
                new DialogueLine { speaker = "Tessa", text = "The encryption is old Dominion-military grade. You will need to work with someone who understands their cipher architecture.", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Why did you stay in the yards? You could have run farther.", seconds = 3f },
                new DialogueLine { speaker = "Tessa", text = "I needed to know if what broke in you at Kethel-7 was real, a conscience, or just the damage. I could not hand this to a malfunction.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "Was it real?", seconds = 1.5f },
                new DialogueLine { speaker = "Tessa", text = "That is the only thing I am certain of.", seconds = 3f },
            };
        }

        // ---- SPACE PURSUIT / DOMINION INTERCEPTOR FIGHT (Beat 12) ----

        private static DialogueLine[] GetInterceptorPursuitLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Interceptor Pilot", text = "Target confirmed! Engaging pursuit!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Evasive! Now! Tessa, those guns are hot!", seconds = 2f },
                new DialogueLine { speaker = "Tessa", text = "I'm, adjusting firing solution...", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Interceptor Pilot", text = "Contact lost behind wreckage! Shifting targeting grid!", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Holding position! They've lost us in the shadow!", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Jump coordinates locked! Now!", seconds = 1.5f },
            };
        }

        // ---- POST-JUMP SILENCE / COMPASSION ANOMALIES (Beat 13) ----

        private static DialogueLine[] GetCompassionAnomaliesLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tessa", text = "Kethel-7 was not the first time something in you cracked.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "What do you mean?", seconds = 1.5f },
                new DialogueLine { speaker = "Tessa", text = "Your training logs show three prior incidents.", seconds = 2f },
                new DialogueLine { speaker = "Tessa", text = "Flagged for compassion anomalies. Buried by Khall's adjutant before they could escalate.", seconds = 3f },
                new DialogueLine { speaker = "Tessa", text = "Khall buried the flags, you were his finest. Kethel-7 was the one he could not bury. It did not make you refuse. It broke you.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "Then everything I've done since I woke, was it mercy, or just the damage talking?", seconds = 4f },
            };
        }
    }
}
