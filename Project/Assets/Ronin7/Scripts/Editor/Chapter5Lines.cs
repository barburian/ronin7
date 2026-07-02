using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Chapter 5 ("The Debt of Ashes") dialogue data. Condensed from
    /// Ch05_The_Debt_of_Ashes_Dialogue_Script.md and keyed by set ID, mirroring Chapter4Lines' shape.
    /// Clip names follow the pattern: ch5_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    ///
    /// story ouput/audit/Ch05_audit.md found zero consistency errors and flagged 9 "not X, it's Y"
    /// antithesis lines plus several aphorism-stacking / on-the-nose lines. All 9 antitheses are thinned
    /// using the audit's own suggested rewrites (search "antithesis rewrite" below); several of the
    /// audit's additional subtractive suggestions (aphorism-stacking, tricolon, on-the-nose fixes) are
    /// also applied, per its recommendation to pull the secondary cast toward plainer, rougher speech.
    /// </summary>
    internal static class Chapter5Lines
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
            "ch5_beat0_briefing",
            "ch5_beat1_approach",
            "ch5_beat1_accusation",
            "ch5_beat1_kira_named",
            "ch5_beat2_dive_intro",
            "ch5_beat2_counting_stock",
            "ch5_beat2_hesitation",
            "ch5_beat3_ledger",
            "ch5_beat4_demand",
            "ch5_beat4_verdict",
            "ch5_beat4_release",
            "ch5_beat4_hook",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "ch5_beat0_briefing" => GetBeat0BriefingLines(),
                "ch5_beat1_approach" => GetBeat1ApproachLines(),
                "ch5_beat1_accusation" => GetBeat1AccusationLines(),
                "ch5_beat1_kira_named" => GetBeat1KiraNamedLines(),
                "ch5_beat2_dive_intro" => GetBeat2DiveIntroLines(),
                "ch5_beat2_counting_stock" => GetBeat2CountingStockLines(),
                "ch5_beat2_hesitation" => GetBeat2HesitationLines(),
                "ch5_beat3_ledger" => GetBeat3LedgerLines(),
                "ch5_beat4_demand" => GetBeat4DemandLines(),
                "ch5_beat4_verdict" => GetBeat4VerdictLines(),
                "ch5_beat4_release" => GetBeat4ReleaseLines(),
                "ch5_beat4_hook" => GetBeat4HookLines(),
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

        /// <summary>Generate clip name for a line: ch5_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ch5_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BEAT 0 — THE CAIRN (the briefing, before the drop) ----

        private static DialogueLine[] GetBeat0BriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mera Voss", text = "I've read a hundred of these. Pacification logs. This one carries your old line's signature. In and out clean. A settlement scoured, and somebody rounded the count off after.", seconds = 14f },
                new DialogueLine { speaker = "Iris", text = "The operative-of-record is named Kael Vor. I've run it against everything we have and the name won't resolve. No file, no line, nothing under it. It's a wall.", seconds = 11f },
                new DialogueLine { speaker = "Resh", text = "So let me say the part everyone's circling. We'd be flying to a graveyard to read his name off the kill-record that put people in it. That's not a rescue. You sure this is the one we pick?", seconds = 11f },
                new DialogueLine { speaker = "Kessler", text = "We've buried a lot to get this crew breathing. Now you want me to fly us to a field of graves. Tell me what's worth the climb back out.", seconds = 9f },
                // Audit fix: drop the third stacked maxim.
                new DialogueLine { speaker = "Mera Voss", text = "The truth's at the bottom. You don't have to like digging for it. It's the work.", seconds = 7f },
                new DialogueLine { speaker = "Mira", text = "Is it the bad place?", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "It's a sad place, little one. Not a loud one. You stay close to me the whole time. This one isn't for small eyes.", seconds = 8f },
                // Audit fix: antithesis ("not X, it's Y") thinned.
                new DialogueLine { speaker = "Echo", text = "I'll say the quiet part, since nobody at that table will. You don't save this place, Cipher. You answer for it. The records have your hands in them. I was there for it, and I'll be with you in it again.", seconds = 14f },
                new DialogueLine { speaker = "Ronin-7", text = "No. I don't want this one.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "Set the course anyway. If my name was buried somewhere, it was buried with them.", seconds = 6f },
                // Audit fix: tricolon flattened for Kessler's rougher, plainer register.
                new DialogueLine { speaker = "Kessler", text = "We go careful. Quiet. And nothing of ours stays down there. Iris, plot it, then you and Mera stay on the Cairn and keep tearing into those records.", seconds = 10f },
            };
        }

        // ---- BEAT 1 — THE SETTLEMENT (ash-world approach, ambient) ----

        private static DialogueLine[] GetBeat1ApproachLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: antithesis + over-balance thinned.
                new DialogueLine { speaker = "Echo", text = "She's not a scavenger, not a picker. She's been standing at that grave since before we landed. Careful, Cipher. This isn't a fight. It's worse than one.", seconds = 10f },
                new DialogueLine { speaker = "Echo", text = "Doors all open. Nobody left to close them.", seconds = 3f },
            };
        }

        // ---- BEAT 1 — THE ACCUSATION (Vera confronts him) ----

        private static DialogueLine[] GetBeat1AccusationLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vera Dusk", text = "Don't. Don't say you're sorry, and don't say you're here to help. The last people who walked out of the sky carried that wrapped thing on your hip, and they didn't help. They counted us and they left.", seconds = 13f },
                new DialogueLine { speaker = "Ronin-7", text = "I'm not going to say either of those.", seconds = 3f },
                new DialogueLine { speaker = "Vera Dusk", text = "You logged this as pacification. A clean word so the men who did it could sleep. There were children in that count.", seconds = 10f },
                new DialogueLine { speaker = "Vera Dusk", text = "This one was my sister. Kira. She was fourteen. She kept lists of the ships that came through, because she liked the names. And then a ship came through that didn't have a name, and it had you on it.", seconds = 14f },
                new DialogueLine { speaker = "Ronin-7", text = "I won't lie to you. I don't remember her. I don't remember any of them. They took it out of me when it was finished. I won't dress that up as anything but what it is.", seconds = 13f },
            };
        }

        // ---- BEAT 1 — KIRA NAMED (the sword remembers, she asks for the truth) ----

        private static DialogueLine[] GetBeat1KiraNamedLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: aphorism + over-balanced coda thinned.
                new DialogueLine { speaker = "Ronin-7", text = "What I have is the sword. It remembers what I can't. It says I was there. It says I was late. And late doesn't help her. Or you.", seconds = 9f },
                new DialogueLine { speaker = "Vera Dusk", text = "Late. You stood in my square with that sword and you were late. What does a thing like you even mean by late.", seconds = 8f },
                // Audit fix: aphorism thinned.
                new DialogueLine { speaker = "Echo", text = "She wants to and she can't. Give her the truth. It's all you've got that's worth anything to her.", seconds = 7f },
                new DialogueLine { speaker = "Ronin-7", text = "I can tell you what late meant. No one sees inside this but me and the blade. I'll walk it again and say every second out loud as I see it. If you want that, I won't make you.", seconds = 14f },
                new DialogueLine { speaker = "Vera Dusk", text = "Tell me, then. I've spent ten years not knowing how she went. Whatever it is, it can't be worse than the version I built in the dark.", seconds = 9f },
            };
        }

        // ---- BEAT 2 — WALKING THE DEAD (the memory-dive opens) ----

        private static DialogueLine[] GetBeat2DiveIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "This is the part I never wanted to play back for you. I rode behind those eyes when they did this. I logged every second and couldn't stop a single one of them. Stay with me, Cipher. We only have to watch.", seconds = 14f },
                new DialogueLine { speaker = "Ronin-7", text = "The square's full. Stalls up, people trading. There's a girl by the well with a slate, counting ships. I come up the lane with the blade, and I'm not hurrying. I'm telling you what I see, Vera.", seconds = 14f },
            };
        }

        // ---- BEAT 2 — COUNTING STOCK (Vera hears the flatness of it) ----

        private static DialogueLine[] GetBeat2CountingStockLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: tricolon anaphora trimmed to one beat.
                new DialogueLine { speaker = "Vera Dusk", text = "You say it so flat. Like stock. That was a market. Those were people. That was my sister. And you walked through it counting.", seconds = 9f },
                // Audit fix: on-the-nose narration of his own horror dropped.
                new DialogueLine { speaker = "Ronin-7", text = "I felt nothing. I know how that sounds. They built me so this would feel like nothing.", seconds = 7f },
                // Audit fix: antithesis ("not a monster, just a hand") thinned.
                new DialogueLine { speaker = "Ronin-7", text = "There's no monster here to find, Vera. Just a hand that did what it was told and never asked.", seconds = 7f },
            };
        }

        // ---- BEAT 2 — THE HESITATION (the load-bearing beat) ----

        private static DialogueLine[] GetBeat2HesitationLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: antithesis + fragment-for-effect thinned.
                new DialogueLine { speaker = "Echo", text = "Here. This is the second you've been carrying without knowing the shape of it. Watch his hand. Forget the blade. The hand.", seconds = 9f },
                new DialogueLine { speaker = "Ronin-7", text = "There. That's late. Something in me moved for the first time, and it moved a half second after it could have mattered. One breath too slow to save her.", seconds = 11f },
                new DialogueLine { speaker = "Vera Dusk", text = "You hesitated.", seconds = 1f },
                new DialogueLine { speaker = "Ronin-7", text = "For the first time in my life. And it didn't save her. I've spared people since because of what started in this square.", seconds = 9f },
                // Audit fix: aphorism-stacking + on-the-nose "both are real" thinned.
                new DialogueLine { speaker = "Echo", text = "I logged this as a fault for years, Cipher. It was the first true thing you ever did. And it was too late to matter. I can't make that into one thing.", seconds = 10f },
            };
        }

        // ---- BEAT 3 — THE LEDGER ("Kael Vor" exposed) — REVEAL ----

        private static DialogueLine[] GetBeat3LedgerLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Iris", text = "Cipher. I need you to hear this now, it changes the shape of what you just told her. We finished cross-checking the command records against your own Program file. They don't match.", seconds = 12f },
                new DialogueLine { speaker = "Ronin-7", text = "Match how.", seconds = 1f },
                // Audit fix: antithesis ("not X, it's Y") thinned.
                new DialogueLine { speaker = "Iris", text = "The operative-of-record for this massacre is logged as Kael Vor. The name I flagged at the briefing. It still won't resolve, because there's nothing under it to resolve to. Nobody misfiled this. They planted it.", seconds = 15f },
                new DialogueLine { speaker = "Mera Voss", text = "She's right, and I've seen the trick a hundred times without turning it over. We logged kills under cover-names so the count would belong to a man who didn't exist. Kael Vor was a coat the Program hung the bodies on.", seconds = 15f },
                new DialogueLine { speaker = "Ronin-7", text = "So the name they hung this on isn't even mine.", seconds = 3f },
                new DialogueLine { speaker = "Iris", text = "It was never yours, Cipher. Someone printed Kael Vor to carry the blame so the hand that did this could be wiped clean and kept.", seconds = 9f },
                new DialogueLine { speaker = "Echo", text = "I carried that name behind your eyes for years and logged it as fact. They leave you a fake to hold so you never go looking for the real one. We go looking now.", seconds = 11f },
                // Audit fix: antithesis (false/real pairing) thinned.
                new DialogueLine { speaker = "Ronin-7", text = "Even the name on it was a lie. They couldn't tell the truth about who killed her.", seconds = 5f },
            };
        }

        // ---- BEAT 4 — THE RECKONING, PART A (the demand, the offer) ----

        private static DialogueLine[] GetBeat4DemandLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vera Dusk", text = "I've imagined this with a thousand faces on you. Now I have the real one, and I have what you did, and I have a gun. Give me one reason this isn't the simplest thing I've ever done.", seconds = 12f },
                new DialogueLine { speaker = "Ronin-7", text = "I won't give you one. You don't owe me a reason. If my life is what clears it, take it. I won't lift the blade and I won't ask you not to.", seconds = 11f },
                new DialogueLine { speaker = "Vera Dusk", text = "I came here to kill you. I've planned it for ten years. And I just listened to you walk back into the worst second of her life and tell me every piece of it without flinching.", seconds = 13f },
            };
        }

        // ---- BEAT 4 — THE RECKONING, PART B (the verdict) ----

        private static DialogueLine[] GetBeat4VerdictLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: on-the-nose thesis-naming dropped, image kept.
                new DialogueLine { speaker = "Vera Dusk", text = "They built a hand that doesn't feel. And it felt something in my square. No wonder they were afraid of you. And killing it won't raise her.", seconds = 12f },
                // Audit fix: aphorism-stacking (three maxims) thinned to one line of reasoning.
                new DialogueLine { speaker = "Vera Dusk", text = "Dying's easy. You'd be gone in a second and I'd still be standing here. So you don't get easy. You stay alive, and every day you do, you owe her. You won't ever be done.", seconds = 15f },
                // Audit fix: antithesis + aphorism coda thinned.
                new DialogueLine { speaker = "Ronin-7", text = "I'll carry it. I won't pretend I can earn it down to nothing, because I can't. But I'll carry her name as long as I've got a hand to carry it. That much I can actually promise.", seconds = 12f },
                // Audit fix: on-the-nose statement of her own arc dropped.
                new DialogueLine { speaker = "Mera Voss", text = "Twice now. Kerrax in the caves, this one over a grave. I spent nine years certain the metal couldn't choose twice. I don't know what I'm sure of anymore.", seconds = 11f },
            };
        }

        // ---- BEAT 4 — THE RELEASE (Vera stays, he walks) ----

        private static DialogueLine[] GetBeat4ReleaseLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Resh", text = "Do we take her with us. We don't leave people standing alone in a place like this.", seconds = 6f },
                // Audit fix: antithesis ("not us, her dead") thinned.
                new DialogueLine { speaker = "Ronin-7", text = "No. She's not crew. She's got her dead to stay with, and that's hers.", seconds = 6f },
                new DialogueLine { speaker = "Vera Dusk", text = "He's right. I'm not joining your war. I'm staying with her, and the others under this ash, until I know they're remembered by someone who isn't a record. But I'll know your name when it's a true one. Come back and tell me, when you've dug it up.", seconds = 16f },
            };
        }

        // ---- BEAT 4 — THE HOOK (the identity hunt opens) ----

        private static DialogueLine[] GetBeat4HookLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: tricolon list flattened.
                new DialogueLine { speaker = "Ronin-7", text = "If the name they hung on me was a lie, then everything behind it is too. The grave, the file, the man I thought I half-remembered. None of it real.", seconds = 9f },
                // Audit fix: antithesis ("not X, but Y") thinned.
                new DialogueLine { speaker = "Echo", text = "Forget who they told you you were. We find who's under it. I've got nothing but time, Cipher, and I was there for the start of it. We'll find the rest.", seconds = 12f },
            };
        }
    }
}
