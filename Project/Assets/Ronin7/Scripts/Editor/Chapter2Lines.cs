using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Chapter 2 ("The Auction") dialogue data. Condensed from
    /// Ch02_The_Auction_Dialogue_Script.md and keyed by set ID, mirroring Chapter1Lines' shape.
    /// Clip names follow the pattern: ch2_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    ///
    /// Two audit fixes from story ouput/audit/Ch02_audit.md are applied here (see call sites below):
    /// (1) Resh's Beat4 line drops "the kid and" — Mira isn't discovered until the escape/dock beat, so
    ///     Resh can't reference her yet. (2) The corrupted Beat2 convergence text ("i  ays ends") is
    ///     restored to "it always ends". The file also thins the audit's flagged "not X, it's Y"
    ///     antithesis pattern down from ~6 instances to effectively none, per the audited voice rules.
    /// </summary>
    internal static class Chapter2Lines
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
            "ch2_beat0_briefing",
            "ch2_beat1_undermarket",
            "ch2_beat2_resh_confront",
            "ch2_beat2_resh_recruit",
            "ch2_beat1_broker",
            "ch2_beat3_iris",
            "ch2_beat4_reveal",
            "ch2_beat5_reunion",
            "ch2_beat5_kept",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "ch2_beat0_briefing" => GetBeat0BriefingLines(),
                "ch2_beat1_undermarket" => GetBeat1UndermarketLines(),
                "ch2_beat2_resh_confront" => GetBeat2ReshConfrontLines(),
                "ch2_beat2_resh_recruit" => GetBeat2ReshRecruitLines(),
                "ch2_beat1_broker" => GetBeat1BrokerLines(),
                "ch2_beat3_iris" => GetBeat3IrisLines(),
                "ch2_beat4_reveal" => GetBeat4RevealLines(),
                "ch2_beat5_reunion" => GetBeat5ReunionLines(),
                "ch2_beat5_kept" => GetBeat5KeptLines(),
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

        /// <summary>Generate clip name for a line: ch2_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ch2_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BEAT 0 — ABOARD THE RIG (the Velorum briefing) ----

        private static DialogueLine[] GetBeat0BriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "You slept four hours and stood watch the other six. Trust the hull eventually.", seconds = 8f },
                new DialogueLine { speaker = "Ronin-7", text = "The voice on the comm gave three days. I don't have six hours to spend not moving.", seconds = 7f },
                new DialogueLine { speaker = "Kessler", text = "Then we move. If a dead operative's file survived anywhere, it's on Velorum.", seconds = 7f },
                new DialogueLine { speaker = "Kessler", text = "Auction-world. They sell weapons up top, people down the bottom, and file the paperwork on both.", seconds = 9f },
                new DialogueLine { speaker = "Ronin-7", text = "You know it.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "I know it the way you know a wound. I swore I'd never set foot on that rock again.", seconds = 8f },
                new DialogueLine { speaker = "Kessler", text = "And here I am, plotting a course back for a man I scraped out of a coffin.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "Why.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "Because somebody has to find out who they buried in my casket. Might as well be the two of us.", seconds = 8f },
                new DialogueLine { speaker = "Ronin-7", text = "Then set the course.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Already set. Strap in. Velorum doesn't get gentler the closer you get.", seconds = 5f },
            };
        }

        // ---- BEAT 1 — THE UNDERMARKET (descent + naming Resh) ----

        private static DialogueLine[] GetBeat1UndermarketLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Keep your hands where the crowd can't read them. Down here a drawn blade is a price tag.", seconds = 8f },
                new DialogueLine { speaker = "Ronin-7", text = "There are no sightlines. No exits I'd trust.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "That's the design. Built to make a newcomer feel small. Don't argue with it, just move.", seconds = 7f },
                new DialogueLine { speaker = "Kessler", text = "Top tier sells what shines. The deeper you go, the worse what's for sale.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "And my records are at the bottom. We have three days.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "If they're anywhere, they're with a broker. Man trades in the paper that follows dead operatives.", seconds = 8f },
                new DialogueLine { speaker = "Ronin-7", text = "Give me the name.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Resh. Smuggler. Runs the routes nobody's mapped, the ones under the cells.", seconds = 6f },
                new DialogueLine { speaker = "Kessler", text = "They say he pulls children out of the markets, one at a time, for years.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "A smuggler with a conscience.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "A useful one, anyway. He'll be working the market row. Let me do the talking.", seconds = 6f },
            };
        }

        // ---- BEAT 2 — SPARING RESH (confrontation) ----

        private static DialogueLine[] GetBeat2ReshConfrontLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "That's him. Works the rail like he owns the floor. Easy. We're buyers. We talk.", seconds = 6f },
                new DialogueLine { speaker = "Resh", text = "Whatever you're selling, I'm not. Whatever you're buying, not from me. I'm working.", seconds = 6f },
                new DialogueLine { speaker = "Kessler", text = "We need a route. Past a broker who won't open his glass. Word is you know the doors off the map.", seconds = 8f },
                new DialogueLine { speaker = "Resh", text = "No. I know what you are. I've moved enough stock to know an operative when one's in my light.", seconds = 8f },
                new DialogueLine { speaker = "Resh", text = "You're Program work. Men like me don't get found by men like you and walk away from it.", seconds = 7f },
                new DialogueLine { speaker = "Ronin-7", text = "I'm not here for you.", seconds = 2f },
                new DialogueLine { speaker = "Resh", text = "That's exactly what the last one said.", seconds = 3f },
                new DialogueLine { speaker = "Resh", text = "I have people on every tier who owe me. I'm worth more to this market alive than you are worth getting me.", seconds = 9f },
                new DialogueLine { speaker = "Resh", text = "So you walk, or this floor decides which of us it keeps.", seconds = 5f },
                // Audit fix: restore the corrupted text ("i  ays ends" -> "it always ends").
                new DialogueLine { speaker = "Resh", text = "Go on, then. You've got me. That's how it always ends with your kind.", seconds = 6f },
                new DialogueLine { speaker = "Resh", text = "You find the one door I didn't have. So finish it and stop making me wait.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "Go.", seconds = 1f },
                new DialogueLine { speaker = "Resh", text = "...What.", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "You're free to go. I won't stop you.", seconds = 4f },
                new DialogueLine { speaker = "Resh", text = "Nobody with their boot on my neck takes it off. Not once, not in my whole life down here.", seconds = 7f },
                new DialogueLine { speaker = "Resh", text = "Men like you don't spare anybody. You use them up.", seconds = 4f },
            };
        }

        // ---- BEAT 2 — SPARING RESH (recruit) ----

        private static DialogueLine[] GetBeat2ReshRecruitLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Resh", text = "You're a turned one. A Program weapon walking around with a salvage rat and an open hand.", seconds = 7f },
                new DialogueLine { speaker = "Kessler", text = "And yet.", seconds = 1f },
                new DialogueLine { speaker = "Resh", text = "I pull children out of this market. One at a time. For every one I get out, the machine takes ten more.", seconds = 8f },
                new DialogueLine { speaker = "Ronin-7", text = "I came here for a route. That's all.", seconds = 3f },
                new DialogueLine { speaker = "Resh", text = "I'm your way past that broker. But I don't want paying off and waved away. I want in.", seconds = 7f },
                new DialogueLine { speaker = "Resh", text = "You're the first crack I've seen in a wall I've spent twenty years smuggling around.", seconds = 6f },
                new DialogueLine { speaker = "Kessler", text = "This is bigger than robbing a stall. This is the Dominion.", seconds = 5f },
                new DialogueLine { speaker = "Resh", text = "I've known exactly who I'm crossing since I was younger than the kids I haul out of here.", seconds = 7f },
                new DialogueLine { speaker = "Resh", text = "The difference today is who I'd be crossing it with. Best odds I've ever had.", seconds = 6f },
                new DialogueLine { speaker = "Resh", text = "I caught a lead this morning. Movement through the cells, collateral being shifted.", seconds = 6f },
                new DialogueLine { speaker = "Resh", text = "My routes run the same corridor as your broker's vault. We pick it up on the way.", seconds = 6f },
            };
        }

        // ---- BEAT 1 — THE BROKER'S STALL (the refusal, in person) ----

        private static DialogueLine[] GetBeat1BrokerLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Broker", text = "Glass stays down. You talk to the grille or you talk to nobody. What do you want.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "Operative records. A termination file. The designation is mine.", seconds = 4.5f },
                new DialogueLine { speaker = "Broker", text = "No. No, no. You're the kind of question that gets a man's stall burned with him inside it.", seconds = 7f },
                new DialogueLine { speaker = "Broker", text = "I don't have it. I never had it. Move on before somebody sees you standing here.", seconds = 6f },
                new DialogueLine { speaker = "Kessler", text = "We can pay.", seconds = 1.5f },
                new DialogueLine { speaker = "Broker", text = "Money's not what gets you in. That vault wants trust, and I've got none to spend on you.", seconds = 7f },
                new DialogueLine { speaker = "Kessler", text = "He's a wall. So we go around him.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "Around how.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Resh already gave us the door. His routes run under this stall. Let's move.", seconds = 5f },
            };
        }

        // ---- BEAT 3 — IRIS FREED ----

        private static DialogueLine[] GetBeat3IrisLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Enforcer", text = "Channel's supposed to be dead down here. Who's moving?", seconds = 4f },
                new DialogueLine { speaker = "Resh", text = "The collateral I tracked this morning. Same cage. Your records are still past it.", seconds = 6f },
                new DialogueLine { speaker = "Iris", text = "If you're buyers, the auction's two tiers up. If you're something else, get it over with.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "The tag says Iris.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Say that again. Say the name again.", seconds = 3f },
                new DialogueLine { speaker = "Iris", text = "...Who is that. Who's on the channel.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Iris. It's your father. I've been scraping a dead field for six years to buy you back.", seconds = 10f },
                new DialogueLine { speaker = "Iris", text = "They told me you stopped looking. They told me you signed me away.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "They lied. I never stopped. Not one night. I'm right above you.", seconds = 7f },
                new DialogueLine { speaker = "Ronin-7", text = "Hold still. I'm cutting you loose.", seconds = 2.5f },
                new DialogueLine { speaker = "Iris", text = "You're the one he came with. The operative. He's really up there.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "He never sat down. Go to him.", seconds = 3f },
                new DialogueLine { speaker = "Iris", text = "I read systems. Locks, networks, the paper they keep on people. Take me to that vault.", seconds = 7f },
                new DialogueLine { speaker = "Resh", text = "She's right. I can get you to the door. Getting through it is a different trade.", seconds = 6f },
                new DialogueLine { speaker = "Kessler", text = "Iris. Stay between them. You come back up to me. You hear?", seconds = 5f },
                new DialogueLine { speaker = "Iris", text = "I hear you. I'm coming back. Let me earn the trip first.", seconds = 5f },
            };
        }

        // ---- BEAT 4 — THE FAILED EXECUTION (the reveal) ----

        private static DialogueLine[] GetBeat4RevealLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Resh", text = "Door's mine. Terminal's clear now that the muscle's down. Iris, it's yours.", seconds = 6f },
                new DialogueLine { speaker = "Iris", text = "Every one of these is a person. Dead operatives, jettisoned, then bought back here as salvage.", seconds = 7f },
                new DialogueLine { speaker = "Ronin-7", text = "The same trade that put me in front of Kessler. A box thrown out as garbage.", seconds = 6f },
                new DialogueLine { speaker = "Resh", text = "I've spent my whole life pulling people out of a place that's built on bodies.", seconds = 6f },
                new DialogueLine { speaker = "Iris", text = "...There's one here under your designation. Filed with the dead. It's yours.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "Read it.", seconds = 1f },
                new DialogueLine { speaker = "Iris", text = "This is your termination order. Executed. Signed off.", seconds = 5f },
                new DialogueLine { speaker = "Iris", text = "The killswitch discharged. Full sequence.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "Then why am I reading it?", seconds = 2.5f },
                new DialogueLine { speaker = "Iris", text = "Because it didn't take. It fired into your skull, full discharge, and you just kept breathing.", seconds = 8f },
                new DialogueLine { speaker = "Iris", text = "Every one of you has this. A leash in the skull. Yours is the only one on record that failed.", seconds = 8f },
                new DialogueLine { speaker = "Ronin-7", text = "No.", seconds = 1f },
                new DialogueLine { speaker = "Ronin-7", text = "Mine's the only one they've found that failed.", seconds = 4f },
                new DialogueLine { speaker = "Iris", text = "...You think there are more of you. Ones that slipped and nobody caught it.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "A leash that broke once on me can break on others. The men who built it don't know that yet.", seconds = 7f },
                new DialogueLine { speaker = "Kessler", text = "I pulled a man out of a coffin who'd already been executed. I didn't know nothing was ever supposed to come out of it.", seconds = 11f },
                new DialogueLine { speaker = "Ronin-7", text = "Copy it. The file, and the others. Every leash in this room.", seconds = 5f },
                new DialogueLine { speaker = "Iris", text = "Copying. It'll take both of us a lifetime to read what's in here.", seconds = 4f },
                // Audit fix: dropped "the kid and" — Mira hasn't been discovered yet at this point.
                new DialogueLine { speaker = "Resh", text = "Then it's a good thing you found a crew. Door's clear. Let's get Kessler's girl home before this market notices what it just sold us for free.", seconds = 12f },
            };
        }

        // ---- BEAT 5 — BACK ABOARD (reunion) ----

        private static DialogueLine[] GetBeat5ReunionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Iris", text = "It's so quiet up here. I forgot quiet.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Six years I had a voice on a wire and a coffin where you should have been. Stand still.", seconds = 9f },
                new DialogueLine { speaker = "Kessler", text = "Let me look at you with my own eyes.", seconds = 3f },
                new DialogueLine { speaker = "Iris", text = "I told you I'd come back. I earned the trip.", seconds = 4f },
                new DialogueLine { speaker = "Resh", text = "Hold the moment. I came up that ramp with one more set of footsteps than I should have.", seconds = 6f },
                new DialogueLine { speaker = "Resh", text = "Stay put.", seconds = 2f },
                new DialogueLine { speaker = "Resh", text = "...No. No, you don't get to be down here. How did you get past me.", seconds = 6f },
                new DialogueLine { speaker = "Mira", text = "You're Resh. You're the one who gets us out.", seconds = 4f },
                new DialogueLine { speaker = "Resh", text = "You know my face.", seconds = 2f },
                new DialogueLine { speaker = "Mira", text = "I saw you on the floor. You weren't running. You were with them. So I followed.", seconds = 6f },
            };
        }

        // ---- BEAT 5 — BACK ABOARD (Mira kept) ----

        private static DialogueLine[] GetBeat5KeptLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mira", text = "...Is the loud part over? Up there. It's loud.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "...That's a child. Resh, that is a child standing in my hold.", seconds = 4f },
                new DialogueLine { speaker = "Resh", text = "She stowed away. Off the auction floor. She's market stock that walked off the shelf on her own legs.", seconds = 7f },
                new DialogueLine { speaker = "Kessler", text = "I'm not in the business of turning a child back toward a cage. Not on this ship.", seconds = 7f },
                new DialogueLine { speaker = "Mira", text = "Are you going to send me back.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "No.", seconds = 1f },
                new DialogueLine { speaker = "Ronin-7", text = "Nobody puts you back. You stay with the ship. You stay where Kessler can see you.", seconds = 7f },
                new DialogueLine { speaker = "Resh", text = "...That's twice today you've done a thing your kind doesn't do.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Then she stays. She stays aboard, with me, same as Iris. I have the room and I have the reason.", seconds = 7f },
                new DialogueLine { speaker = "Resh", text = "A salvage rat, a freed tech, a stowaway, and me. Hell of a manifest.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "Get some rest. Velorum can keep the rest of it.", seconds = 3f },
            };
        }
    }
}
