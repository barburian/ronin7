using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Chapter 1 dialogue data. Transposed from
    /// Ch01_The_Salvagers_Debt_Dialogue_Script.md and keyed by set ID.
    /// Clip names follow the pattern: ch1_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// </summary>
    internal static class Chapter1Lines
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
            "ch1_beat1_wake",
            "ch1_beat1_settle",
            "ch1_beat2_adrift",
            "ch1_beat3_board_pre",
            "ch1_beat3_board_post",
            "ch1_beat4_walk",
            "ch1_beat4_ultimatum",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "ch1_beat1_wake" => GetBeat1WakeLines(),
                "ch1_beat1_settle" => GetBeat1SettleLines(),
                "ch1_beat2_adrift" => GetBeat2AdriftLines(),
                "ch1_beat3_board_pre" => GetBeat3BoardPreLines(),
                "ch1_beat3_board_post" => GetBeat3BoardPostLines(),
                "ch1_beat4_walk" => GetBeat4WalkLines(),
                "ch1_beat4_ultimatum" => GetBeat4UltimatumLines(),
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

        /// <summary>Generate clip name for a line: ch1_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ch1_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BEAT 1 — THE WAKE ----

        private static DialogueLine[] GetBeat1WakeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "There it is.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "Come on. Stay this time.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "Where am I.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "You're on my ship.", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "That's not an answer.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "It's the only one I've got that's true.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "You can crush my windpipe. You're built for it, I can feel that much. But you already did the hard part.", seconds = 8.5f },
                new DialogueLine { speaker = "Ronin-7", text = "What part.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "You didn't kill me. Last time we met, you haven't done that either.", seconds = 5.5f },
                new DialogueLine { speaker = "Ronin-7", text = "I don't know you.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "I know. They took that part out. They're good at it.", seconds = 4.5f },
            };
        }

        private static DialogueLine[] GetBeat1SettleLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "My hands knew what to do. I didn't tell them.", seconds = 4.5f },
                new DialogueLine { speaker = "Kessler", text = "Yeah. I've watched them work for three weeks. They don't ask you much.", seconds = 5.5f },
                new DialogueLine { speaker = "Ronin-7", text = "This.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "That was there when I found you. Not my work. Somebody closed that up clean and then opened it again. I don't know which order.", seconds = 10f },
                new DialogueLine { speaker = "Kessler", text = "That came out of the box with you. I cleaned the blood off it. Figured it was yours.", seconds = 7f },
                new DialogueLine { speaker = "Ronin-7", text = "I don't remember this. But my hands do.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "That's more than you've given me in three weeks. You want to put it down, or you want to keep holding the one thing in here that feels like yours?", seconds = 11f },
                new DialogueLine { speaker = "Ronin-7", text = "What do I call you.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Kessler. And before you ask, no. From what I can tell you were Dominion property and they have thrown you to garbage.", seconds = 9f },
                new DialogueLine { speaker = "Ronin-7", text = "Then start with where I am. Slowly.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Now that I can do.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Come on. Walk it off before you fall down and I have to start over.", seconds = 5.5f },
            };
        }

        // ---- BEAT 2 — THREE WEEKS ADRIFT ----

        private static DialogueLine[] GetBeat2AdriftLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "You hungry? Thirsty? I don't actually know what you run on yet.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "Tell me how I got here.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Three weeks ago I was working this field. Pulling copper out of a freighter that's been dead longer than my daughter's been alive. And my hook snags something it shouldn't. Off on its own. Nothing for a hundred klicks but it.", seconds = 16f },
                new DialogueLine { speaker = "Ronin-7", text = "A casket.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "A casket. Sealed. Dominion locks, fresh ones, not salvage-old. A sealed pod drifting clean out here means somebody paid to be sure it never opened.", seconds = 10f },
                new DialogueLine { speaker = "Ronin-7", text = "And there was no wreck.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "No wreck. Nobody jettisons a sealed military pod into open black by accident. Somebody put you in a box and threw the box away. Took the trouble to lock it first.", seconds = 12f },
                new DialogueLine { speaker = "Ronin-7", text = "You should have left it shut.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "That's what every smart year of my life told me. Cheap skin over augments somebody spent a fortune on. A throat cut and closed. A man flatlined so long the table kept telling me to stop. My instinct said burn it and don't ask.", seconds = 16.5f },
                new DialogueLine { speaker = "Ronin-7", text = "But you opened it.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "I owed it open.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "Owed who.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "You. Though you'll tell me you don't remember, and I'll believe you, because you've been telling me nothing for three weeks straight.", seconds = 8.5f },
                new DialogueLine { speaker = "Kessler", text = "You've woken before. Did you know that? Four, five times. You'd come up off the table thrashing, eyes wide open and nobody behind them. Once you put me into that wall hard enough I saw the next morning sideways. And then you'd just go back under. Like a tide. No name. Nothing a man could use.", seconds = 22f },
                new DialogueLine { speaker = "Ronin-7", text = "And this morning?", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "This morning somebody's home behind the eyes. First time. Three weeks I've been talking to a body, hoping there was still a man in it.", seconds = 10f },
                new DialogueLine { speaker = "Ronin-7", text = "You said you owed me. These hands cut throats. I felt it tonight, on yours. Why would anyone owe that a debt?", seconds = 8.5f },
                new DialogueLine { speaker = "Kessler", text = "Six years ago you did.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Six years ago I was on the wrong end of a clean sweep. Nobody walks out. Not the marks, not the witnesses, not the man who hauled the cargo and saw a face he shouldn't have. That last one was me. So they sent a man to close the door.", seconds = 19f },
                new DialogueLine { speaker = "Ronin-7", text = "Me.", seconds = 0.5f },
                new DialogueLine { speaker = "Kessler", text = "You. You had me on my knees. Hand on the back of my head. I felt the muzzle. And then, nothing. No shot. I knelt there a full minute waiting to die and it didn't come. When I looked up, you were just standing there. Looking at your own hand.", seconds = 20f },
                new DialogueLine { speaker = "Kessler", text = "Then you left. Reported the sweep clean. Far as the order ever knew, I died that day. I've lived six years on a death they wrote down and never checked. I don't know why you did it. I don't think you knew either. The men who built you, they've got a word for it when it happens. They say one of you went mercy. Like it's a fault in the metal.", seconds = 28f },
                new DialogueLine { speaker = "Ronin-7", text = "I don't remember sparing you.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "I know.", seconds = 1f },
                new DialogueLine { speaker = "Ronin-7", text = "I don't remember being a man who would.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Neither do they. That's the part that matters. Whatever they built you to be, you broke it that day. One time, in front of me. I'm betting a thing that broke once can break again.", seconds = 14f },
                new DialogueLine { speaker = "Ronin-7", text = "If they sealed me and threw me away, then they think the job's done.", seconds = 5.5f },
                new DialogueLine { speaker = "Kessler", text = "That'd be my read.", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Then when they find out it isn't, they'll come to finish it.", seconds = 4.5f },
                new DialogueLine { speaker = "Kessler", text = "...Sooner than I'd like.", seconds = 2f },
            };
        }

        // ---- BEAT 3 — THE BOARDING ----

        private static DialogueLine[] GetBeat3BoardPreLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Trooper 1", text = "Salvage registry. We're conducting a sweep. Stand clear of the hatch.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Sweep for what? I'm licensed in this field. Manifest's logged, tariffs are clean. You can pull it from here.", seconds = 7f },
                new DialogueLine { speaker = "Squad Leader (V.O.)", text = "Manifest doesn't cover what we're looking for. Step back.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "I've got a dead reactor and a hold full of other men's garbage. There's nothing on this rig worth two troopers and a clamp.", seconds = 9f },
                new DialogueLine { speaker = "Trooper 1", text = "Then it won't take long.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Hey, listen, there's medical gear back there, half of it live, you go poking it...", seconds = 5.5f },
                new DialogueLine { speaker = "Trooper 2", text = "Stay. There.", seconds = 1.5f },
                new DialogueLine { speaker = "Trooper 1", text = "Command. I've got a face-match flagged here.", seconds = 3f },
                new DialogueLine { speaker = "Squad Leader (V.O.)", text = "Match to what.", seconds = 1.5f },
                new DialogueLine { speaker = "Trooper 1", text = "Flag says decedent. Repeat. The file says this man is deceased. Closed. He's standing in front of me.", seconds = 7.5f },
                new DialogueLine { speaker = "Comm (V.O.)", text = "Hold position. Confirm the face. Stream it. Now, to me. Direct.", seconds = 5f },
                new DialogueLine { speaker = "Trooper 1", text = "...Confirming. Stream is live, sir.", seconds = 2.5f },
                new DialogueLine { speaker = "Comm (V.O.)", text = "Confirmed. Terminate. Recover the remains intact.", seconds = 3f },
            };
        }

        private static DialogueLine[] GetBeat3BoardPostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "...They mobilized a sweep for a dead man.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "Not a sweep. They were looking for me.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "The file said deceased. I heard it. They've got you written down closed and buried, and they still sent armed men aboard the second your face turned up. That's not how you treat a corpse. That's how you treat a mistake you thought you'd already cleaned up.", seconds = 18f },
                new DialogueLine { speaker = "Ronin-7", text = "The box didn't hold.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "They know now.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Yeah. They surely do.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Come on. This conversation's about to get worse, and I'd rather have it sitting down.", seconds = 5.5f },
            };
        }

        // ---- BEAT 4 — THE ULTIMATUM ----

        private static DialogueLine[] GetBeat4WalkLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "You understand what just left this ship. Those visors don't record to a chip. They stream. The whole fight, your face, all of it, went up the chain before the last of them hit the deck.", seconds = 14f },
                new DialogueLine { speaker = "Ronin-7", text = "To the voice. The one that wasn't the squad's.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "You caught that.", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "The others obeyed it without thinking. That's not a commander. That's an owner.", seconds = 5.5f },
                new DialogueLine { speaker = "Kessler", text = "Yeah. Whoever built you sold the use of you a long time ago. That voice holds the lease. And whatever doubt it had: is he really standing, is the file wrong. You just answered it. In full color.", seconds = 15f },
                new DialogueLine { speaker = "Ronin-7", text = "You said the debt made you open the casket. A man doesn't crack a Dominion seal to repay a kindness. Not unless he needs the kindness to be worth something.", seconds = 12f },
                new DialogueLine { speaker = "Kessler", text = "I needed to believe a thing like you could exist. A killer that stops. Because if one could, then maybe the galaxy still owes mercy to a man who can't buy it any other way.", seconds = 14f },
                new DialogueLine { speaker = "Ronin-7", text = "What man.", seconds = 1f },
            };
        }

        private static DialogueLine[] GetBeat4UltimatumLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Drone", text = "Message incoming. Priority override. I couldn't refuse it.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "...Of course you couldn't.", seconds = 2f },
                new DialogueLine { speaker = "Handler (Hologram)", text = "Kessler.", seconds = 1f },
                new DialogueLine { speaker = "Handler (Hologram)", text = "The Dominion has a long memory for the men who once served it. Longer than yours, it seems. You've let yourself forget the arrangement. I haven't.", seconds = 11f },
                new DialogueLine { speaker = "Kessler", text = "I paid out of that. Years ago.", seconds = 3f },
                new DialogueLine { speaker = "Handler (Hologram)", text = "No one pays out of it. You hold something of ours. We watched you use it tonight, messily, but effectively. You will return it, intact, within three days.", seconds = 11f },
                new DialogueLine { speaker = "Handler (Hologram)", text = "Three days, Kessler. You know the markets at Velorum. We have taken your daughter there. Deliver the item, or you will never see your daughter.", seconds = 10f },
                new DialogueLine { speaker = "Handler (Hologram)", text = "Iris. She's grown, since you saw her. We've kept the asset in good condition. Whether we continue to is a matter of your arithmetic.", seconds = 10f },
                new DialogueLine { speaker = "Ronin-7", text = "Iris?", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "My daughter. They took her to make sure I stayed useful. She was in a Dominion school, but they probably took her in Velorum's markets, where they sell bodies and pasts and call it commerce. Six years I've scraped this dead field for enough to buy her back, before they remembered they could just use her on me again. And tonight they remembered.", seconds = 25f },
                new DialogueLine { speaker = "Ronin-7", text = "That's the debt you meant. Not the one to me.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "At the start. Maybe. Before there was a man behind the eyes.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "Then it's simple. You give them what they came for. Me.", seconds = 4.5f },
                new DialogueLine { speaker = "Kessler", text = "No.", seconds = 0.5f },
                new DialogueLine { speaker = "Ronin-7", text = "My life for hers. A clean trade. I don't remember being a man worth keeping, Kessler. I don't remember the mercy you owe me for. But this I can do.", seconds = 12f },
                new DialogueLine { speaker = "Kessler", text = "It's a bluff.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "You think men like that trade? They don't trade. They collect. You walk into Velorum and hand yourself over, and they don't open her cage and wave goodbye. They put you in a box, again, and they put her in the ground next to it, and they file it tidy.", seconds = 19f },
                new DialogueLine { speaker = "Ronin-7", text = "You can't know that.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "I served them. I know exactly what their mercy is worth. I've run that math every night for six years.", seconds = 8f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we don't trade.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "No. I didn't pull you out of a coffin to ship you back to one. We go to Velorum, and we take her back, and we make them eat the difference.", seconds = 12f },
                new DialogueLine { speaker = "Ronin-7", text = "Three days.", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "Three days. Velorum's a long burn and a worse welcome. They sell bodies and pasts in those markets. Maybe somebody there sold yours.", seconds = 9f },
                new DialogueLine { speaker = "Ronin-7", text = "I spared a man I don't remember. I want to know why I went easy.", seconds = 6f },
                new DialogueLine { speaker = "Kessler", text = "Then we ask Velorum.", seconds = 2f },
                new DialogueLine { speaker = "Drone", text = "Heading laid. Velorum.", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Take us out.", seconds = 1.5f },
            };
        }
    }
}
