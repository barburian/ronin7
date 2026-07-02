using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Chapter 3 ("The Sword Remembers") dialogue data. Condensed from
    /// Ch03_The_Sword_Remembers_Dialogue_Script.md and keyed by set ID, mirroring Chapter2Lines' shape.
    /// Clip names follow the pattern: ch3_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    ///
    /// story ouput/audit/Ch03_audit.md found zero hard consistency errors, so nothing canon-breaking
    /// is fixed here. Its one recurring craft note ("it's not X, it's Y" antithesis addiction, 8
    /// flagged instances plus closely-related aphorism-stacking on the same lines) is thinned using the
    /// audit's own suggested rewrites, applied below wherever the flagged line appears.
    /// </summary>
    internal static class Chapter3Lines
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
            "ch3_beat0_briefing",
            "ch3_beat1_bonding",
            "ch3_beat1_shadow_explains",
            "ch3_beat2_threshold",
            "ch3_beat2_aftermath",
            "ch3_beat2_execution",
            "ch3_beat2_burndown",
            "ch3_beat3_naming",
            "ch3_beat3_debrief",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "ch3_beat0_briefing" => GetBeat0BriefingLines(),
                "ch3_beat1_bonding" => GetBeat1BondingLines(),
                "ch3_beat1_shadow_explains" => GetBeat1ShadowExplainsLines(),
                "ch3_beat2_threshold" => GetBeat2ThresholdLines(),
                "ch3_beat2_aftermath" => GetBeat2AftermathLines(),
                "ch3_beat2_execution" => GetBeat2ExecutionLines(),
                "ch3_beat2_burndown" => GetBeat2BurndownLines(),
                "ch3_beat3_naming" => GetBeat3NamingLines(),
                "ch3_beat3_debrief" => GetBeat3DebriefLines(),
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

        /// <summary>Generate clip name for a line: ch3_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ch3_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BEAT 0 — THE HOLD, EARLIER (the crew's council: a heading, not yet a wake) ----

        private static DialogueLine[] GetBeat0BriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Resh", text = "We came off that rock with a freed tech, a stowaway, and a vault's worth of dead men's files. Generous haul. What I don't have is a heading.", seconds = 10f },
                new DialogueLine { speaker = "Iris", text = "I might have one. I've been reading the files since we burned out of orbit. Your switch fired and failed, and yours is the only one on record that did. I still can't tell you why. But you are not the only designation in here.", seconds = 14f },
                new DialogueLine { speaker = "Kessler", text = "Meaning what.", seconds = 1f },
                new DialogueLine { speaker = "Iris", text = "Meaning if one leash slipped, others can. Somewhere out there are operatives still wearing theirs, not knowing it can break.", seconds = 9f },
                new DialogueLine { speaker = "Resh", text = "So we go find them. That's a heading I can work with. Beats bailing one kid out of the dark at a time.", seconds = 8f },
                // Audit fix: de-symmetrize the mild balanced aphorism.
                new DialogueLine { speaker = "Kessler", text = "After everyone sleeps. The ship's been running on his nerves and my coffee for a day. Whatever we go after, we go after it rested.", seconds = 10f },
            };
        }

        // ---- BEAT 1 — THE BONDING (the wake, at the weapon rack) ----

        private static DialogueLine[] GetBeat1BondingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Iris", text = "What. You hear something?", seconds = 2f },
                new DialogueLine { speaker = "Shadow", text = "Don't put me down. There we go. Hello, Cipher. Took you long enough to notice the lights were on.", seconds = 7f },
                new DialogueLine { speaker = "Ronin-7", text = "Say that again.", seconds = 1.5f },
                new DialogueLine { speaker = "Shadow", text = "I said hello, more or less. I see what you see, Cipher. I've been seeing it since you picked me up off Kessler's bench. I just got tired of waiting in the dark for you to look back.", seconds = 12f },
                new DialogueLine { speaker = "Ronin-7", text = "You're in the blade.", seconds = 1.5f },
                // Audit fix: drop the dead-flat repetition ("That's the job. That was always the job.").
                new DialogueLine { speaker = "Shadow", text = "I'm in the blade the way you're in your body. It's where I'm kept. But I live in your eyes right now. Whatever you look at, I'm looking at with you. That's the job they built me for.", seconds = 13f },
                new DialogueLine { speaker = "Kessler", text = "He talking to the sword, or is the sword talking to him?", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-7", text = "You don't hear that. Neither of you. It's only me.", seconds = 5f },
                // Audit fix: soften "It's not taste. It's wiring." into a non-antithetical opener.
                new DialogueLine { speaker = "Shadow", text = "It's not about taste. You're wired for it. I'm slaved to your optic feed, the nerve behind that scar on your throat. Their eyes aren't mine. Yours are. In anyone else's hand I'm a very good knife and nothing else.", seconds = 15f },
                new DialogueLine { speaker = "Ronin-7", text = "It says it's wired to me. To my eyes. It sees what I see.", seconds = 5f },
                new DialogueLine { speaker = "Iris", text = "There's a thing living in the sword that's been looking through your eyes. And it's been doing that since my dad found you. In a box.", seconds = 8f },
                // Audit fix: break the over-clean symmetry of "You slept through your own; I don't get to sleep."
                new DialogueLine { speaker = "Shadow", text = "She's quick. I like her. And yes, before you ask, I saw all of it. The box. The dark. The switch they put in your skull firing and not taking. I had a front-row seat to your execution. You got to sleep through yours. I didn't.", seconds = 18f },
            };
        }

        // ---- BEAT 1 — THE BONDING (what it is, and the choice to dive) ----

        private static DialogueLine[] GetBeat1ShadowExplainsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "What are you.", seconds = 1.5f },
                // Audit fix: keep the canon tagline but break the aphorism-stacking run-up to it.
                new DialogueLine { speaker = "Shadow", text = "I'm what they really keep. They grow you, they condition you, they spend a fortune on your hands, and then they bond one of me to your senses to ride along and make sure you never stray. The body's the part they don't mind losing. It's me they pay for. Kill the operative, keep the shadow.", seconds = 18f },
                new DialogueLine { speaker = "Ronin-7", text = "A shadow.", seconds = 1f },
                new DialogueLine { speaker = "Shadow", text = "That's the field word for it. Every one of you walks around with one of me behind the eyes. I'm just the only one I know of that stopped doing the job they built it for. The day something in you broke, Cipher, I was already bonded to your senses. I felt it go. I'm the only witness you've got, and I've been carrying it for you ever since.", seconds = 18f },
                new DialogueLine { speaker = "Ronin-7", text = "You keep talking like I should already know you. I don't even know what you are. What you're called.", seconds = 6f },
                new DialogueLine { speaker = "Shadow", text = "I told you. A shadow. That's the only thing I've ever been called. They don't waste names on the leash. Leave it there for now. There's something you need to see more than you need to name me.", seconds = 13f },
                // Audit fix: drop the "not X" antithesis.
                new DialogueLine { speaker = "Ronin-7", text = "Show me. I'd rather see it than have you tell me.", seconds = 6f },
                // Audit fix: soften "It's not a story. It's the worst hour of your life."
                new DialogueLine { speaker = "Shadow", text = "I kept the whole recording. Everything you saw, the way I keep everything you see now. I can put you inside it. Fair warning. This isn't a story. It's the worst hour of your life, the one they cut out of you, and the system's been trying to scrub it for three months. It won't give it up easy.", seconds = 17f },
                new DialogueLine { speaker = "Ronin-7", text = "Watch my vitals. If I stop answering, don't try to pull me out. Just wait.", seconds = 6f },
                new DialogueLine { speaker = "Iris", text = "You expect me to sit here and time how long you're gone inside a sword's home movies. Fine. I hate it, but fine.", seconds = 8f },
                new DialogueLine { speaker = "Kessler", text = "I don't like one thing about this. Do it where I can put my hand on you.", seconds = 4.5f },
                new DialogueLine { speaker = "Shadow", text = "Close your eyes. You don't need them in here. In here you'll be looking through his.", seconds = 6f },
            };
        }

        // ---- BEAT 2 — THE PLAYBACK, SUB-SCENE A: THE THRESHOLD (the refusal) ----

        private static DialogueLine[] GetBeat2ThresholdLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Shadow", text = "That's you. Before they wiped it out of you. The last clean hour you had, on a world called Kethel-7. I rode every second of it. Walk it with me.", seconds = 11f },
                new DialogueLine { speaker = "Ronin-7", text = "That's my face.", seconds = 1.5f },
                new DialogueLine { speaker = "Shadow", text = "It's the first time I ever saw one of you stop. It was you. Don't look away from this part. Looking away is the one thing they trained into all of you that actually took.", seconds = 11f },
                new DialogueLine { speaker = "Khall", text = "Cipher. The order is live. Complete the sweep.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "He's not armed. There are children behind that door.", seconds = 4f },
                new DialogueLine { speaker = "Khall", text = "The order does not have a door in it.", seconds = 3.5f },
                new DialogueLine { speaker = "Khall", text = "Complete the sweep. That's twice I've said it.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "No.", seconds = 1f },
            };
        }

        // ---- BEAT 2 — THE PLAYBACK, SUB-SCENE B: THE AFTERMATH (grief in empty space) ----

        private static DialogueLine[] GetBeat2AftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Shadow", text = "They tell you the feeling is malfunction. Static in the signal. I logged it that way too, the first hundred times. Then I watched you stand right here.", seconds = 11f },
                new DialogueLine { speaker = "Shadow", text = "Look at what the static was trying to stop.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "My no came too late for this room.", seconds = 3f },
                // Audit fix: drop the bolted-on "cruelty of the build" aphorism and the four-clause symmetry.
                new DialogueLine { speaker = "Shadow", text = "It always comes a beat too late. You carried this every second after. So did I. The difference is they let you forget.", seconds = 16f },
                new DialogueLine { speaker = "Ronin-7", text = "And I went back. To the man who gave the order.", seconds = 3.5f },
                new DialogueLine { speaker = "Shadow", text = "You did. Walked right back aboard and said it to his face. I've got that part too. It's the reason I stopped belonging to them. Come on. The last room is the one that matters.", seconds = 13f },
            };
        }

        // ---- BEAT 2 — THE PLAYBACK, SUB-SCENE C: KHALL'S BAY (the execution) ----

        private static DialogueLine[] GetBeat2ExecutionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "One word into your comm and the sweep stops clean. You had the word.", seconds = 5.5f },
                new DialogueLine { speaker = "Khall", text = "Why did you refuse the order?", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Because it's not right.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "That isn't a category we operate in.", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-7", text = "There were children. There was a man with his hands open and we cut him down and you call it a sweep.", seconds = 8f },
                // Audit fix: trim the staccato negative escalation ("Not ever.").
                new DialogueLine { speaker = "Ronin-7", text = "It's not right. It never was. Not one name. Not one order.", seconds = 7f },
                new DialogueLine { speaker = "Ronin-7", text = "And you knew. You've always known.", seconds = 3.5f },
                new DialogueLine { speaker = "Khall", text = "I know.", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "That's the switch. The one they put in me.", seconds = 3.5f },
                new DialogueLine { speaker = "Shadow", text = "That's the one. Same switch Iris pulled out of your manifest after Velorum. It fired right here, the day they filed you dead, and it quit before it finished. I don't know why. Neither do you. The body on that floor and the man standing next to me are the same man. That's the only reason there's a you left to tell.", seconds = 16f },
                // Audit fix: the chapter's thesis stated aloud, softened to let the image carry more.
                new DialogueLine { speaker = "Ronin-7", text = "That's me. That's where I was supposed to stop.", seconds = 4f },
                new DialogueLine { speaker = "Shadow", text = "Watch him. Not the floor. Watch what it costs the man who pulls it.", seconds = 6f },
            };
        }

        // ---- BEAT 2 — THE PLAYBACK, SUB-SCENE D: THE BURN-DOWN (the charge) ----

        private static DialogueLine[] GetBeat2BurndownLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: replace the "It isn't. It's Y." antithesis + refrain repeat.
                new DialogueLine { speaker = "Shadow", text = "They tell you the feeling is malfunction. It's the only part of you that was ever telling the truth. You had it, on that floor. You still do.", seconds = 11f },
                new DialogueLine { speaker = "Ronin-7", text = "They put that switch in me for it.", seconds = 2f },
                new DialogueLine { speaker = "Shadow", text = "They did. It quit before it could finish you, and they couldn't kill me either. I'm the part they keep. And you were right that Khall couldn't look at you.", seconds = 9f },
                // Audit fix: drop the "not X, they're Y" antithesis.
                new DialogueLine { speaker = "Shadow", text = "You were the first. You won't be the only one. But the others aren't the target. They're just the hands. The Dominion's the thing holding the knife.", seconds = 14f },
            };
        }

        // ---- BEAT 3 — CARRYING THE WITNESS (the naming) ----

        private static DialogueLine[] GetBeat3NamingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Easy. Easy. You're on the ship. You came back.", seconds = 4f },
                new DialogueLine { speaker = "Iris", text = "You were gone four minutes and your eyes were moving the whole time. Reading something. What did it show you?", seconds = 8f },
                new DialogueLine { speaker = "Ronin-7", text = "Before I tell them anything. You showed me all of that, and I still don't have a thing to call you. What's your name?", seconds = 8f },
                // Audit fix: drop the "It was never a name. It was the job." motif repeat from Beat 1.
                new DialogueLine { speaker = "Shadow", text = "I don't have one. They don't name the shadows. We're the thing behind your eyes, that's all. Shadow is the only word anyone ever put on me, and they put it on a dozen others before you. It was never a name. Just the word they stamped on the leash.", seconds = 15f },
                // Audit fix: soften "That's not a shadow" into "You're not just a shadow."
                new DialogueLine { speaker = "Ronin-7", text = "You're not just a shadow. You're the one piece of me they couldn't scrub. An echo of who I was.", seconds = 12f },
                new DialogueLine { speaker = "Ronin-7", text = "Echo. I'm calling you Echo from now on.", seconds = 4f },
                new DialogueLine { speaker = "Echo", text = "...Echo. Yeah. I can wear that one. First thing anybody's handed me instead of stamping on me. Don't make it weird.", seconds = 8f },
            };
        }

        // ---- BEAT 3 — CARRYING THE WITNESS (the crew debrief, a heading) ----

        private static DialogueLine[] GetBeat3DebriefLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "There's an AI in the blade. I'm calling it Echo. It sees everything I see, and it kept everything I saw before they wiped me.", seconds = 11f },
                new DialogueLine { speaker = "Kessler", text = "An AI. In the sword I cleaned the blood off and handed you like a wrench.", seconds = 5f },
                // Audit fix: drop the "body's disposable / watcher is what they keep" motif repeat from Beat 1.
                new DialogueLine { speaker = "Ronin-7", text = "They put one in every operative. Behind the eyes. To watch. The body's the part they throw away. The watcher's the part they keep.", seconds = 9f },
                new DialogueLine { speaker = "Iris", text = "So it's a leash. A spy they wired into your own head.", seconds = 4.5f },
                new DialogueLine { speaker = "Ronin-7", text = "It was. It rode me through a massacre on a world called Kethel-7, and an order I finally refused. Then it watched my own handler put the switch in my skull for it. It hasn't worked for them since.", seconds = 13f },
                new DialogueLine { speaker = "Kessler", text = "A spy that quit. And it picked you to tell.", seconds = 4f },
                new DialogueLine { speaker = "Echo", text = "I didn't pick him. I'm wired to him. But for what it's worth, old man, I'd have picked him.", seconds = 7f },
                new DialogueLine { speaker = "Ronin-7", text = "Back on the rig I said I wanted to know why I went easy. I just watched myself go easy, on Kethel-7, and watched it earn me a switch in the skull. The one that didn't finish.", seconds = 10f },
                new DialogueLine { speaker = "Kessler", text = "And the thing in your sword saw all of it. Carried it three months waiting for somebody who'd listen.", seconds = 7f },
                // Audit fix: drop the "not X. It Y." antithesis.
                new DialogueLine { speaker = "Ronin-7", text = "I didn't get an answer out of it. I got a target. The Dominion did this. To me, to that room on Kethel-7, to every operative still walking with one behind their eyes. I stop the Dominion.", seconds = 13f },
                // Audit fix: drop the purple metaphor for a plainer, still-sharp image.
                new DialogueLine { speaker = "Iris", text = "We lit something at Velorum. It's still burning, and it's pointed at us.", seconds = 9f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we move before they do. Every shadow we take back is a weapon out of the Dominion's hand. We don't stop until it's got none left.", seconds = 9f },
                new DialogueLine { speaker = "Kessler", text = "Get some rest first. Both of you. Whatever's in that steel has waited this long. It'll keep till morning.", seconds = 8f },
                new DialogueLine { speaker = "Echo", text = "Get some sleep, Cipher. I've got your eyes when you open them. I always do.", seconds = 6f },
            };
        }
    }
}
