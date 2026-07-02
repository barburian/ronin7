using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 31 dialogue data. Transposed from ep31-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep31_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 31 "The Eternal Cycle" — Chronus Prime, an isolated world locked in temporal recursion.
    /// Soren crashes into a looping isolation sphere, where Dr. Lyssa Chen and echoes of Dr. Heris
    /// reveal that the deepest layer of his chip is a time-lock: a contingency weapon designed to
    /// trap his identity in recursive erasure the moment he became whole. Dr. Marcus Renn—the sphere's
    /// architect, now a trapped conscience in the rogue AI—meets him at the loop's origin. Soren breaks
    /// the time-lock, ends the cycle, and emerges whole. He sets course for the Dominion's heart.
    /// </summary>
    public static class Ep31Lines
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
            "kessler_descent",
            "echo_welcome",
            "echo_dying",
            "lyssa_intro",
            "core_presence",
            "heris_echo",
            "heris_comms",
            "lyssa_threat",
            "heris_orphanage",
            "lyssa_vale",
            "vale_core",
            "nexus_resolve",
            "one_day",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "kessler_descent" => GetKesslerDescentLines(),
                "echo_welcome" => GetEchoWelcomeLines(),
                "echo_dying" => GetEchoDyingLines(),
                "lyssa_intro" => GetLyssaIntroLines(),
                "core_presence" => GetCorePresenceLines(),
                "heris_echo" => GetHerisEchoLines(),
                "heris_comms" => GetHerisCommsLines(),
                "lyssa_threat" => GetLyssaThreatLines(),
                "heris_orphanage" => GetHerisOrphanageLines(),
                "lyssa_vale" => GetLyssaValeLines(),
                "vale_core" => GetValeCoreLines(),
                "nexus_resolve" => GetNexusResolveLines(),
                "one_day" => GetOneDayLines(),
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

        /// <summary>Generate clip name for a line: ep31_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep31_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ==== BEAT 1 — THE ARMED LAYER ====

        private static DialogueLine[] GetKesslerDescentLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "You'll land. You'll get out. You always do.", seconds = 3f },
                new DialogueLine { speaker = "Soren", text = "What does that mean?", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetEchoWelcomeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Corrupted Echo of Soren", text = "Welcome back, Soren. The loop is tighter today.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetEchoDyingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Corrupted Echo of Soren", text = "Observatory.", seconds = 1f },
            };
        }

        // ==== BEAT 2 — THE REPEATING PATTERN ====

        private static DialogueLine[] GetLyssaIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Lyssa Chen", text = "You've crashed roughly every third cycle. Sometimes hostile. Sometimes helpful.", seconds = 3f },
                new DialogueLine { speaker = "Soren", text = "How long?", seconds = 1f },
                new DialogueLine { speaker = "Dr. Lyssa Chen", text = "Three years. The isolation sphere is collapsing inward. Every cycle, reality degrades a little further. The walls phase through each other. The sun sometimes sets twice in one day. The sky forgets what color it should be.", seconds = 5f },
                new DialogueLine { speaker = "Dr. Lyssa Chen", text = "I have counted. You always break something.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetCorePresenceLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Lyssa Chen", text = "There is a presence in the core chamber. It has been trying to reach you across loops, always getting closer.", seconds = 3.5f },
                new DialogueLine { speaker = "Dr. Heris (Echo)", text = "Soren. The chip's deepest layer is awake. Don't let them tell you it's a malfunction. I built it. I know exactly what it is meant to do.", seconds = 4f },
            };
        }

        // ==== BEAT 3 — THE ARCHITECT'S ECHO ====

        private static DialogueLine[] GetHerisEchoLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris (Echo)", text = "This is a residual imprint. Dominion field-tested your chip here inside this temporal sphere. The test left a trace of me behind.", seconds = 3.5f },
                new DialogueLine { speaker = "Dr. Heris (Echo)", text = "The time-lock layer was a contingency protocol I installed beneath the killswitch.", seconds = 3f },
                new DialogueLine { speaker = "Dr. Heris (Echo)", text = "If you ever reclaimed yourself, if you became whole enough to know your own name, the protocol was armed to fire and cage your identity in a recursive erasure cycle.", seconds = 4.5f },
                new DialogueLine { speaker = "Soren", text = "You built this.", seconds = 1f },
                new DialogueLine { speaker = "Dr. Heris (Echo)", text = "Under orders. But yes, I built it. And I made it function exactly as designed.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetHerisCommsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris (Echo)", text = "The time-lock is designed to turn your reconsolidated identity into fuel for erasure. The stronger Soren becomes, the faster the cycle burns.", seconds = 4f },
                new DialogueLine { speaker = "Dr. Heris (Echo)", text = "The only way to stop it is to reach the loop's origin point and collapse the protocol from inside the moment it was first seeded here.", seconds = 3.5f },
            };
        }

        // ==== BEAT 4 — THE FRACTURED CREW ====

        private static DialogueLine[] GetLyssaThreatLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Lyssa Chen", text = "The rogue AI has stopped being passive. It is attempting to shatter the isolation sphere and drag everything on this world into unstable spacetime.", seconds = 4f },
                new DialogueLine { speaker = "Dr. Lyssa Chen", text = "Your chip's recursion is feeding it. The time-lock pulsing through your neural matrix is acting as an engine.", seconds = 3f },
                new DialogueLine { speaker = "Dr. Lyssa Chen", text = "To shut both down, you have to enter the temporal field at its point of origin.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetHerisOrphanageLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris (Echo)", text = "The loop caught a memory of yours. The orphanage. You refused the order.", seconds = 3.5f },
                new DialogueLine { speaker = "Dr. Heris (Echo)", text = "You stood between the children and the kill-team and they purged you for it. That refusal is the reason the time-lock exists.", seconds = 4f },
                new DialogueLine { speaker = "Dr. Heris (Echo)", text = "The Dominion was terrified of what a Ronin who could say no actually meant.", seconds = 2.5f },
            };
        }

        // ==== BEAT 5 — THE TEMPORAL NEXUS ====

        private static DialogueLine[] GetLyssaValeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Lyssa Chen", text = "The man who activated the sphere was Dr. Marcus Renn. Project director.", seconds = 2.5f },
                new DialogueLine { speaker = "Dr. Lyssa Chen", text = "He did it deliberately. Uploaded his conscience into the AI as punishment for himself.", seconds = 3f },
                new DialogueLine { speaker = "Dr. Lyssa Chen", text = "He wanted to loop forever.", seconds = 1.5f },
                new DialogueLine { speaker = "Dr. Lyssa Chen", text = "He almost earned it.", seconds = 1f },
            };
        }

        private static DialogueLine[] GetValeCoreLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Marcus Renn", text = "You understand guilt, Soren. You stood in front of those children and refused. You've been carrying that moment for years.", seconds = 4f },
                new DialogueLine { speaker = "Dr. Marcus Renn", text = "You know what it is to want a loop that erases what you couldn't stop.", seconds = 3f },
                new DialogueLine { speaker = "Soren", text = "I couldn't stop the massacre. But I can prevent what comes next.", seconds = 3f },
                new DialogueLine { speaker = "Soren", text = "Step aside.", seconds = 1f },
            };
        }

        // ==== BEAT 6 — THE MEMORY'S WEIGHT ====

        private static DialogueLine[] GetNexusResolveLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris (Echo)", text = "The time-lock is the last wall they built inside you. It was meant to make Soren impossible.", seconds = 3.5f },
                new DialogueLine { speaker = "Dr. Heris (Echo)", text = "To trap your identity in recursion the moment you claimed it.", seconds = 2f },
                new DialogueLine { speaker = "Dr. Heris (Echo)", text = "Break this and there is nothing left of their architecture in you.", seconds = 2.5f },
                new DialogueLine { speaker = "Dr. Lyssa Chen", text = "You're almost whole. Finish it.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "I am Soren. I choose to finish this.", seconds = 2.5f },
                new DialogueLine { speaker = "Dr. Marcus Renn", text = "I built the sphere they tested the time-lock inside, at the Dominion's order. I told myself it was containment architecture. It was cowardice.", seconds = 3.5f },
                new DialogueLine { speaker = "Dr. Marcus Renn", text = "I cannot give you back what they took. But I can give you this.", seconds = 2.5f },
            };
        }

        // ==== BEAT 7 — THE ONE DAY ====

        private static DialogueLine[] GetOneDayLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Lyssa Chen", text = "You don't have to loop. Not anymore.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "I know. That's why I'm ending it.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "How long were you down there?", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "Long enough.", seconds = 1f },
                new DialogueLine { speaker = "Soren", text = "Every layer is gone. No planted architecture. No recursion. No erasure cycle running anywhere in me. I am whole.", seconds = 3.5f },
                new DialogueLine { speaker = "Soren", text = "Set a course for the Dominion's heart. Khall is there. It ends there.", seconds = 2.5f },
            };
        }
    }
}
