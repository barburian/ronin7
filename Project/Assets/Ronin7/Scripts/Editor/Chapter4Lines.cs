using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Chapter 4 ("The Overseer's Hunt") dialogue data. Condensed from
    /// Ch04_The_Overseers_Hunt_Dialogue_Script.md and keyed by set ID, mirroring Chapter3Lines' shape.
    /// Clip names follow the pattern: ch4_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    ///
    /// story ouput/audit/Ch04_audit.md found zero hard consistency errors and one soft watch-item
    /// (not fixed here — it only concerns a later chapter's disclosure, see the audit). Its four
    /// flagged "not X, that's Y" antithesis lines are thinned using the audit's own suggested rewrites,
    /// applied below wherever the flagged line appears (Beat1/Resh, Beat2/Ronin-7, Beat3/Kessler,
    /// Beat5/Mera Voss).
    /// </summary>
    internal static class Chapter4Lines
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
            "ch4_beat0_briefing",
            "ch4_beat1_throat",
            "ch4_beat2_tessa_meet",
            "ch4_beat2_sabotage",
            "ch4_beat3_khall",
            "ch4_beat3_aftermath",
            "ch4_beat4_descent_intro",
            "ch4_beat4_descent_calls",
            "ch4_beat4_descent_end",
            "ch4_beat5_kerrax_confront",
            "ch4_beat5_mercy",
            "ch4_beat5_mera_recruit",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "ch4_beat0_briefing" => GetBeat0BriefingLines(),
                "ch4_beat1_throat" => GetBeat1ThroatLines(),
                "ch4_beat2_tessa_meet" => GetBeat2TessaMeetLines(),
                "ch4_beat2_sabotage" => GetBeat2SabotageLines(),
                "ch4_beat3_khall" => GetBeat3KhallLines(),
                "ch4_beat3_aftermath" => GetBeat3AftermathLines(),
                "ch4_beat4_descent_intro" => GetBeat4DescentIntroLines(),
                "ch4_beat4_descent_calls" => GetBeat4DescentCallsLines(),
                "ch4_beat4_descent_end" => GetBeat4DescentEndLines(),
                "ch4_beat5_kerrax_confront" => GetBeat5KerraxConfrontLines(),
                "ch4_beat5_mercy" => GetBeat5MercyLines(),
                "ch4_beat5_mera_recruit" => GetBeat5MeraRecruitLines(),
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

        /// <summary>Generate clip name for a line: ch4_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ch4_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BEAT 0 — THE CAIRN (the briefing, before the drop) ----

        private static DialogueLine[] GetBeat0BriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Resh", text = "Drovis. Coil free-port, built straight down a canyon of dead capital hulls. No Dominion garrison anywhere in it. That's why our courier ran there.", seconds = 10f },
                new DialogueLine { speaker = "Iris", text = "Tessa Rin. Program logistics, gone to ground with a slate of internals she's trying to sell before someone closes the account on her. If anything names who cut your leash, it's in what she's carrying.", seconds = 14f },
                new DialogueLine { speaker = "Kessler", text = "Neutral ground. They can't march troopers in after you. They'll sell you instead, and right now you're a Program weapon walking around breathing.", seconds = 10f },
                new DialogueLine { speaker = "Echo", text = "You're about to walk the most wanted face you own into a city that buys and sells faces for a living. I'll watch the exits, Cipher. Somebody has to.", seconds = 10f },
                new DialogueLine { speaker = "Mira", text = "Is it like Velorum. The loud part.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Louder. Which is why you're staying sealed on this ship with the comm open. You're done with markets.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we reach her first. Take us down.", seconds = 3f },
            };
        }

        // ---- BEAT 1 — THE THROAT (grounding + ambient manhunt + the watcher) ----

        private static DialogueLine[] GetBeat1ThroatLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "That's your face on his slate. And on the drone. Whole city's been handed your picture. Welcome to Drovis.", seconds = 8f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we keep moving and we don't look up.", seconds = 3f },
                new DialogueLine { speaker = "Resh", text = "Drone overhead, two scanners on the rail. Coil's got the chokes plugged and they're checking faces, not papers.", seconds = 9f },
                // Audit fix: drop the "not X. That's Y." antithesis.
                new DialogueLine { speaker = "Resh", text = "Coil doesn't pay for a customs sweep. Somebody's funding this.", seconds = 4f },
                new DialogueLine { speaker = "Echo", text = "Rail. High left. The one not buying anything.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "Saw it.", seconds = 1f },
                new DialogueLine { speaker = "Echo", text = "That one wasn't reading a slate. That one already knew your face before the bounty. Different kind of trouble.", seconds = 8f },
                new DialogueLine { speaker = "Kessler", text = "Enforcer?", seconds = 1f },
                new DialogueLine { speaker = "Ronin-7", text = "No. Enforcers stand where the bounty tells them. That one chose the rail. Leave it. We have a courier to reach.", seconds = 9f },
            };
        }

        // ---- BEAT 2 — THE SINK, SUB-SCENE A: TESSA RIN (the meet + the snatch-team) ----

        private static DialogueLine[] GetBeat2TessaMeetLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tessa Rin", text = "Whatever you're buying, I'm sold out and I'm leaving. Try the next stall.", seconds = 5f },
                new DialogueLine { speaker = "Resh", text = "We're not Coil. You're Tessa Rin, you're carrying Program internals, and there's a snatch-team two aisles back moving like they already bought you. We can get you out.", seconds = 11f },
                new DialogueLine { speaker = "Tessa Rin", text = "Everybody says they can get me out. Then I'm in a Coil cell counting my own teeth.", seconds = 6f },
                new DialogueLine { speaker = "Tessa Rin", text = "That's the face on the bounty. You're the recovery. You're the thing the whole port's hunting tonight.", seconds = 7f },
                new DialogueLine { speaker = "Ronin-7", text = "Then for the next few minutes the Coil wants me more than they want you. Walk behind me. Decide now.", seconds = 8f },
                new DialogueLine { speaker = "Echo", text = "That's the team. Four. The second they read your face that bounty triples and you're the prize.", seconds = 8f },
                new DialogueLine { speaker = "Kessler", text = "Iris, get her and the slate behind the plate-stacks. Resh, with me.", seconds = 5f },
            };
        }

        // ---- BEAT 2 — THE SINK, SUB-SCENE B: THE SABOTAGE (after the fight) ----

        private static DialogueLine[] GetBeat2SabotageLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Tessa Rin", text = "You really aren't them. Fine. The data's real, it's Program disposal internals, and it's the last thing I'm ever selling. Take it.", seconds = 10f },
                new DialogueLine { speaker = "Iris", text = "This is your termination record. The real one, not the manifest.", seconds = 4f },
                new DialogueLine { speaker = "Iris", text = "Hold on. This is wrong.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "Wrong how.", seconds = 1f },
                new DialogueLine { speaker = "Iris", text = "Your killswitch didn't fail. I called it a fault after Velorum, a switch that misfired and quit. It didn't misfire. Somebody got inside the firmware and broke it on purpose.", seconds = 14f },
                new DialogueLine { speaker = "Echo", text = "I told you it quit before it finished and I didn't know why. There's your why. I had a front-row seat to your execution, Cipher, and I missed that.", seconds = 11f },
                // Audit fix: drop the "I wasn't lucky. I was chosen." antithesis.
                new DialogueLine { speaker = "Ronin-7", text = "Then it wasn't luck. Someone wanted me walking.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Who?", seconds = 1f },
                new DialogueLine { speaker = "Iris", text = "The record doesn't say. Whoever did it scrubbed themselves out of it cleaner than they scrubbed your switch. That takes someone high.", seconds = 10f },
                new DialogueLine { speaker = "Tessa Rin", text = "And that's exactly why I'm not standing here while you talk about who in the Program has long arms. I was never in this aisle.", seconds = 9f },
                new DialogueLine { speaker = "Resh", text = "Go to ground deep. If we found you in an afternoon, the Coil will too.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "Pack it. The relay on the Mast is feeding the whole city my face. We trace it back to the man holding the leash.", seconds = 8f },
            };
        }

        // ---- BEAT 3 — THE MAST (Khall / the naming) — REVEAL ----

        private static DialogueLine[] GetBeat3KhallLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "Cipher. You are listed terminated. You are making that a difficult fiction to maintain.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "That's not my name.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "It is the one I gave you.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "I am your Overseer. My name is Khall. I am not trying to kill you, Cipher. I am trying to bring you home before someone with less patience than I have finds you first.", seconds = 13f },
                new DialogueLine { speaker = "Ronin-7", text = "Home doesn't put a switch in your skull.", seconds = 4f },
                new DialogueLine { speaker = "Khall", text = "No. It doesn't.", seconds = 3f },
                new DialogueLine { speaker = "Echo", text = "That's him. That's the voice from the bay, the one that pulled your switch and couldn't watch you fall. He's older now.", seconds = 8f },
                new DialogueLine { speaker = "Ronin-7", text = "You couldn't look at me when you did it. I watched you. So tell me what home is, that you'd switch me off for refusing an order, and then chase me across a free-port to switch me back on.", seconds = 14f },
                new DialogueLine { speaker = "Khall", text = "You were not meant to remember the bay. We will discuss what you're owed when you're home, and not before. The bounty stands. For your sake, end this and let me bring you in.", seconds = 14f },
                new DialogueLine { speaker = "Ronin-7", text = "The man at the bottom of these caves is selling me to you. I'm going to go and ask him why he thinks I'm his to sell.", seconds = 9f },
            };
        }

        // ---- BEAT 3 — THE MAST (the crew adopts the name) ----

        private static DialogueLine[] GetBeat3AftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Iris", text = "Cipher. That's what it kept calling you. And the handler, Khall.", seconds = 5f },
                new DialogueLine { speaker = "Resh", text = "Cipher. Suits you better than nothing did. Easier to shout across a fight, anyway.", seconds = 5f },
                // Audit fix: drop the "not a manhunt, that's a man" antithesis.
                new DialogueLine { speaker = "Kessler", text = "That's no manhunt. He's trying to take something back, and grief like that gets people killed. I don't trust it.", seconds = 11f },
                new DialogueLine { speaker = "Ronin-7", text = "Cipher.", seconds = 1f },
                new DialogueLine { speaker = "Echo", text = "They named you to own you. Doesn't mean it's theirs. Come on. Khall's bought a man, and that man's at the bottom of this rock.", seconds = 10f },
            };
        }

        // ---- BEAT 4 — THE DEEPWORKS, SUB-SCENE A: THE DROP (Echo takes over) ----

        private static DialogueLine[] GetBeat4DescentIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "We've got the line anchored up top. Past the first drop we lose your light. After that it's you and the sword.", seconds = 7f },
                new DialogueLine { speaker = "Echo", text = "Then it's you and me, which is the same thing they've never understood. I can see the whole shape of this place through your eyes. Let me drive the feet, you keep the blade.", seconds = 13f },
            };
        }

        // ---- BEAT 4 — THE DEEPWORKS, SUB-SCENE B: ROUTE-CALLS (ambient, position-triggered) ----

        private static DialogueLine[] GetBeat4DescentCallsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "Footing's loose on the left. Stay to the rock.", seconds = 3f },
                new DialogueLine { speaker = "Echo", text = "Drone, your three. Hold there till it passes.", seconds = 3f },
                new DialogueLine { speaker = "Echo", text = "Water's rising ahead. Shallow yet. Keep moving.", seconds = 3f },
                new DialogueLine { speaker = "Echo", text = "Crowd's long gone. Just us and the dark now.", seconds = 3f },
            };
        }

        // ---- BEAT 4 — THE DEEPWORKS, SUB-SCENE C: THE LAST DROP ----

        private static DialogueLine[] GetBeat4DescentEndLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "That's his light down there. Dominion blue, Coil money. He's home. Last drop, Cipher. No more route to call after this.", seconds = 11f },
            };
        }

        // ---- BEAT 5 — KERRAX'S HOLD (the confrontation) ----

        private static DialogueLine[] GetBeat5KerraxConfrontLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kerrax", text = "The recovery walks in on its own legs. Saves me the finder's fee. You've cost me four good enforcers and a courier I had three-quarters sold, operative.", seconds = 11f },
                new DialogueLine { speaker = "Ronin-7", text = "You're selling a man you've never met to a man you've never met. Both of them want me for reasons you don't know. That should worry you more than it does.", seconds = 11f },
                new DialogueLine { speaker = "Kerrax", text = "Reasons are for buyers. I just collect.", seconds = 4f },
            };
        }

        // ---- BEAT 5 — THE MERCY / MERA REVEALED ----

        private static DialogueLine[] GetBeat5MercyLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "This is the part they built into your hands. The order says finish it. You don't have to listen to your hands, Cipher. Not anymore.", seconds = 9f },
                new DialogueLine { speaker = "Ronin-7", text = "Tell them what you saw.", seconds = 2f },
                new DialogueLine { speaker = "Kerrax", text = "They'll say you malfunctioned. A killer that stops is a broken killer.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "Then I'll keep malfunctioning.", seconds = 3f },
                new DialogueLine { speaker = "Mera Voss", text = "A machine doesn't choose twice.", seconds = 3f },
                new DialogueLine { speaker = "Echo", text = "The rail. The one who already knew your face.", seconds = 4f },
                new DialogueLine { speaker = "Mera Voss", text = "I'm Mera Voss. I tracked operatives for the Dominion. They sent me to confirm a kill, yours or his, didn't matter which. Not one ever stopped.", seconds = 10f },
                new DialogueLine { speaker = "Ronin-7", text = "And now one stopped.", seconds = 2f },
                // Audit fix: drop the "not a fault in the metal, that's a man" antithesis.
                new DialogueLine { speaker = "Mera Voss", text = "Metal doesn't hesitate like that. You did.", seconds = 3f },
            };
        }

        // ---- BEAT 5 — MERA RECRUITED (ally #2, closes Act I) ----

        private static DialogueLine[] GetBeat5MeraRecruitLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "We've got movement up top, the Coil's regrouping. Whatever's happening down there, happen faster.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "You came to confirm a kill. Go back and report a man instead, or come down the rest of the way with us. I'm not putting a gun to it. They do that. I don't.", seconds = 13f },
                new DialogueLine { speaker = "Mera Voss", text = "If I report this they'll send someone who won't hesitate the way I just did. I'd rather be on the side that hesitates. I'm in. I can read a trail no one else on your crew can.", seconds = 13f },
                new DialogueLine { speaker = "Echo", text = "Ally number two. And this one came to kill you. I'd say the day's improving.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "Then you berth aboard the Cairn with the rest of us. Everyone we take in lives on the ship.", seconds = 7f },
                new DialogueLine { speaker = "Mera Voss", text = "A turned weapon and a smuggler and a salvage man's kid. All of you, holed up in a dead leviathan.", seconds = 7f },
                new DialogueLine { speaker = "Kessler", text = "She's coming up with you?", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "She's crew. She'll explain on the way. Leave Kerrax for the Coil to find.", seconds = 6f },
                new DialogueLine { speaker = "Echo", text = "He'll tell it wrong. Let him. The woman behind you saw what really happened, and so did I. It was never a glitch, Cipher. That part's just you.", seconds = 11f },
            };
        }
    }
}
