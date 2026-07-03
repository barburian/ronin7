using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Chapter 8 ("The Silent Garden") dialogue data. Condensed from
    /// Ch08_The_Silent_Garden_Dialogue_Script.md and keyed by set ID, mirroring Chapter6Lines/
    /// Chapter7Lines' shape. Clip names follow the pattern: ch8_{setId}_{index:00}_{speaker_sanitized}.
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    ///
    /// story ouput/audit/Ch08_audit.md graded the source script C on naturalness (0 em-dashes, 0 hard
    /// script/canon errors, but heavy antithesis/aphorism-stacking and a uniform register across very
    /// different speakers). The rewrites below thin the worst of that per the audit table: Mera/Resh/
    /// Iris's Beat0 lines are roughened toward plainer, less quotable speech; the shared coinage "the
    /// living and the loud" (originally handed verbatim from the Mourners to Ronin) is paraphrased on
    /// Ronin's side instead of echoed; Mira's on-the-nose "you're sad" line keeps its blunt first
    /// sentence (intentional child-clarity) but softens the second per the audit's suggested rewrite;
    /// and a few of the heaviest "not X, it's Y" / tricolon constructions (Coral's "worse than blood",
    /// the Mourners' "we've buried stronger... cleverer" stack, Khall's "seals/chain/hand" tricolon)
    /// are trimmed toward plainer phrasing. The trial's thematic-spine riddle (Puzzle C, "The Fog's
    /// Question", the only one of the source's three seed puzzles with a fully authored answer) is kept
    /// close to verbatim since the audit flagged it as load-bearing, not a tic.
    ///
    /// PUZZLE SCOPE NOTE: the source script frames three seed puzzles (Grave of True Names, The Honest
    /// Order, The Fog's Question) as production-note placeholders "designed in a later pass," not fully
    /// mechanized content. Only Puzzle C (the riddle with a real authored answer, and the trial's
    /// thematic spine per its own voice notes) is built as the interactive RiddleTrial gate this pass;
    /// Puzzles A/B's set-dressing and phantom-mirror mechanic are out of scope, mirroring Chapter7's
    /// blade-rescue side-objective scope cut. See Chapter8Builder's class summary.
    /// </summary>
    internal static class Chapter8Lines
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
            "ch8_beat0_briefing",
            "ch8_beat1_gate",
            "ch8_beat1_alone",
            "ch8_beat2_riddle_pose",
            "ch8_beat2_wrong_bark",
            "ch8_beat2_riddle_answer",
            "ch8_beat3_warden_intro",
            "ch8_beat3_warden_defeat",
            "ch8_beat4_naming",
            "ch8_beat4_vision_a",
            "ch8_beat4_vision_b",
            "ch8_beat4_aftermath",
            "ch8_beat5_leaving",
            "ch8_beat5_reunion",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "ch8_beat0_briefing" => GetBeat0BriefingLines(),
                "ch8_beat1_gate" => GetBeat1GateLines(),
                "ch8_beat1_alone" => GetBeat1AloneLines(),
                "ch8_beat2_riddle_pose" => GetBeat2RiddlePoseLines(),
                "ch8_beat2_wrong_bark" => GetBeat2WrongBarkLines(),
                "ch8_beat2_riddle_answer" => GetBeat2RiddleAnswerLines(),
                "ch8_beat3_warden_intro" => GetBeat3WardenIntroLines(),
                "ch8_beat3_warden_defeat" => GetBeat3WardenDefeatLines(),
                "ch8_beat4_naming" => GetBeat4NamingLines(),
                "ch8_beat4_vision_a" => GetBeat4VisionALines(),
                "ch8_beat4_vision_b" => GetBeat4VisionBLines(),
                "ch8_beat4_aftermath" => GetBeat4AftermathLines(),
                "ch8_beat5_leaving" => GetBeat5LeavingLines(),
                "ch8_beat5_reunion" => GetBeat5ReunionLines(),
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

        /// <summary>Generate clip name for a line: ch8_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ch8_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BEAT 0 — THE CAIRN (the briefing, voice-only) ----

        private static DialogueLine[] GetBeat0BriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Coral Vex", text = "I named the Silent Garden at the end of Chapter 7, and I told you I never let myself believe in it. Here's the believing. A burial world, tended by keepers older than the Program, older than anything that still breathes. They hold the souls of a people who lived and died on that world ages before our wars, and they'll grant one seeker the sight of a single truth they were never meant to see.", seconds = 20f },
                new DialogueLine { speaker = "Ronin-7", text = "Truth has a price here. You said that too.", seconds = 4f },
                // Audit fix: "worse than blood" epigram thinned.
                new DialogueLine { speaker = "Coral Vex", text = "A trial. They don't sell it, you earn it, and the Garden's buried more seekers than it's ever answered. I don't know the shape of the test. Nobody who failed it came back to describe it, and the ones who passed don't say. I just know the price is real, and it isn't paid in blood.", seconds = 18f },
                // Audit fix: roughened toward plainer, less quotable speech per the audit's Mera/Resh/Iris note.
                new DialogueLine { speaker = "Mera Voss", text = "No walls, no guns, no garrison, and it still buries the people who knock. I've hunted a hundred places that wanted me dead and I knew how each one would come at me. This one I can't price at all. I don't like that.", seconds = 14f },
                new DialogueLine { speaker = "Morrigan", text = "I'll back her, and I hate backing her. I threw everything this bench has at the coordinates. No signal, no power bloom, no comm bleed, nothing in any band I read. Every instrument says there's nothing on that world. I trust the instruments. I also trust Vex.", seconds = 16f },
                // Audit fix: symmetry broken per the audit's suggested rewrite.
                new DialogueLine { speaker = "Iris", text = "A whole world of graves bothers me more than the no-signal does. Somebody dug all of them. Somebody's still tending them. You don't need a graveyard that size for the dead. You need it for whoever comes looking.", seconds = 13f },
                // Audit fix: roughened, dropped the "mouth that decided to look like a field" phrasing.
                new DialogueLine { speaker = "Resh", text = "Smugglers don't run the Silent Garden. The ones who tried don't run anything now. I had it filed under tall tales, the kind you tell so some idiot doesn't try the run. Coral saying it's real, that's the part I don't like.", seconds = 14f },
                new DialogueLine { speaker = "Mira", text = "If it only lets one person in, everybody else waits outside. You're going alone again.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "She's got the shape of it. A place that only opens for one. I hate those. I'll set us down, I'll hold whatever it calls a gate, but I want it said out loud, Cipher. I don't like sending you somewhere I can't follow.", seconds = 16f },
                new DialogueLine { speaker = "Ronin-7", text = "It knows the hand that put the switch in my head, and the hand that broke it. That's worth a trial. I've crossed worse for less.", seconds = 8f },
                new DialogueLine { speaker = "Echo", text = "Cipher. Before you go in, hear me. This isn't the reliquary, it isn't the mountain. There's nothing in there for the blade to do, and I don't think there's anything in there for me to do either, which I'm not used to. Whatever it asks you, it's asking you, not us. Answer true and answer yourself, because in a place like this the trained answer and the honest one are going to be two different things.", seconds = 20f },
                new DialogueLine { speaker = "Ronin-7", text = "The hand and the head, then. You ride along and stay quiet when it counts.", seconds = 6f },
                new DialogueLine { speaker = "Coral Vex", text = "If it opens for you, that isn't luck. It means it already knows what you are and wants a closer look. Walk in knowing you were expected.", seconds = 9f },
                new DialogueLine { speaker = "Ronin-7", text = "Good. I'm done being the one nobody expected. Plot it. Take us down.", seconds = 6f },
            };
        }

        // ---- BEAT 1 — THE GATE OF FOG (the crew held; Ronin walks through alone) ----

        private static DialogueLine[] GetBeat1GateLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mera Voss", text = "There's a wall here. I can put my hand right on it and there's nothing in front of my hand. It let you through and stopped the rest of us cold. I don't like a wall I can't see, and I really don't like one that picks.", seconds = 14f },
                new DialogueLine { speaker = "Coral Vex", text = "It picks. I told you it would. Cipher, this is where I stop being any use to you. Everything I know about the Program ends at this gate. The people in there are older than it and answer to none of it, me included. From here you walk it the way the first seeker walked it, with whatever's in your own skull.", seconds = 20f },
                new DialogueLine { speaker = "Kessler", text = "Comm's going to static and you're six steps in, so I'll say it now while it carries. We can't push past whatever this is, I already tried, it's like leaning on the side of the world. But we're not leaving this rock until you walk back out of that fog. We hold the gate. You come back through it.", seconds = 18f },
                new DialogueLine { speaker = "Ronin-7", text = "I heard you. Hold the gate.", seconds = 2f },
            };
        }

        // ---- BEAT 1 — ALONE (Echo/Ronin alone on the grave-paths; the Mourners set the rules) ----

        private static DialogueLine[] GetBeat1AloneLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "Comm's gone. It's just the two of us now, Cipher. First time since the playback it's only been us walking at something. I'm here the whole way. I already know I have to go quiet when it starts asking. So this is me, before the quiet.", seconds = 14f },
                new DialogueLine { speaker = "Ronin-7", text = "Then stay close and stay quiet. I've thought without you before. I just didn't have you to miss.", seconds = 7f },
                new DialogueLine { speaker = "The Mourners", text = "You were made to walk where you were sent. So walk. The dead under this ground remember every name that was ever taken from them, and they don't forgive a lie laid over a grave. Step on a falsehood, and the ones you slander rise to answer in your place.", seconds = 20f },
            };
        }

        // ---- BEAT 2 — THE FOG'S QUESTION (the riddle pose; the RiddleTrial gate opens here) ----

        private static DialogueLine[] GetBeat2RiddlePoseLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "The Mourners", text = "One question. The fog comes down off the oldest grave, or it doesn't. A leash is set in a thing for one reason: so that when the one who holds it pulls, the thing comes. Tell us, then. When the leash is pulled, what is the duty of the thing on the end of it. Answer quickly. The quick answer is the one they built in you. We're listening for it.", seconds = 22f },
            };
        }

        // ---- BEAT 2 — wave-0 bark for the wrong-answer guardian spawn. ----

        private static DialogueLine[] GetBeat2WrongBarkLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "The Mourners", text = "Wrong. The dead remember what you just said. Rise, and answer for him.", seconds = 5f },
            };
        }

        // ---- BEAT 2 — the true answer + the Mourners' acknowledgment (plays once the RiddleTrial's
        // right pad is reached) ----

        private static DialogueLine[] GetBeat2RiddleAnswerLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "The quick answer is obey. Come when pulled. That's the one they built. So that's the wrong one.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "A thing on a leash has no duty. That duty was a lie they put in the collar. The only thing it owes is the leash itself, broken. I came here for the hand that pulled mine and the hand that cut it. That's my answer. None.", seconds = 14f },
                new DialogueLine { speaker = "The Mourners", text = "True. The truest thing this ground has heard in longer than your Program has had a name. You were pulled, and you did not come. Few have stood on this stone and refused the collar to our faces. The trial of the mind is ended. Now the Garden asks the older question, the one it asks with its hands.", seconds = 20f },
            };
        }

        // ---- BEAT 3 — THE WARDEN (boss intro) ----

        private static DialogueLine[] GetBeat3WardenIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "The Mourners", text = "This is the warden of the last grave. It has put down seekers who walked in stronger than you, and left this ground in pieces. It doesn't tire and it can't be reasoned with, because it isn't asked to think. It's only asked to be sure. Strength was given to you the way thought was. Show us the body keeps what the mind earned.", seconds = 18f },
                new DialogueLine { speaker = "Echo", text = "There you are, a fight, finally something I'm good for. I've got the read back the second it moved. That thing's a tomb wearing a body, but a tomb has load-bearing joints same as anything, and the old blade's eyes are showing me every one of them. Stay off the front of it, work the seams.", seconds = 18f },
            };
        }

        // ---- BEAT 3 — WARDEN DEFEATED ----

        private static DialogueLine[] GetBeat3WardenDefeatLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "The Mourners", text = "Down it goes, back into the ground it guards. Strength, given. Mind, given. The body kept what the mind earned, which is rarer on this ground than either alone. Be still now. We've watched you long enough from inside the fog. We're coming to look at you with what's left of our eyes.", seconds = 16f },
            };
        }

        // ---- BEAT 4 — THE ONE WHO DEFIES (the naming, before the fog opens the vision) ----

        private static DialogueLine[] GetBeat4NamingLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: the "stronger... cleverer" stack thinned toward plainer phrasing.
                new DialogueLine { speaker = "The Mourners", text = "Now we name you, the way we've named only a handful in all the time we've kept this ground. We've buried stronger seekers than you, and cleverer ones too. A leash was set in you, in the place where the spine meets the skull, by people who don't make mistakes. And it was pulled. And it failed. You are the one who defies. The Garden owes its one truth to such a one. Look.", seconds = 24f },
                new DialogueLine { speaker = "Ronin-7", text = "I came for one thing. The hand that put the switch in me, and the hand that pulled it. Show me those.", seconds = 7f },
                new DialogueLine { speaker = "The Mourners", text = "Both hands, as you ask. We don't soften them and we don't name them. The Garden gives sight, not answers. Watch, and know that what you see, you've always carried. We're only opening the eye the leash kept shut.", seconds = 16f },
            };
        }

        // ---- BEAT 4A — VISION A: THE FACELESS HAND (played inside the vision dive) ----

        private static DialogueLine[] GetBeat4VisionALines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "The Woman", text = "There. That's the last of it.", seconds = 3f },
                new DialogueLine { speaker = "The Woman", text = "I hope you will save us.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "Turn. Let me see your face.", seconds = 3f },
            };
        }

        // ---- BEAT 4B — VISION B: KHALL, THE MOMENT OF THE SWITCH (played inside the vision dive) ----

        private static DialogueLine[] GetBeat4VisionBLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "You were right. You've always been right, about the order, about all of it. It's that you broke because you saw it clearer than any of us, and I'm still going to do this anyway.", seconds = 10f },
                new DialogueLine { speaker = "Khall", text = "I'm sorry it has come to this.", seconds = 3f },
                // Audit fix: "seals/chain/hand" tricolon thinned.
                new DialogueLine { speaker = "Khall", text = "A hundred times I've read this. Seals check out. Chain of command checks out. The hand is right. And it still reads like something built only to look real.", seconds = 10f },
                new DialogueLine { speaker = "Khall", text = "Who forges a leash. Who fakes an order this clean, and to what end.", seconds = 5f },
            };
        }

        // ---- BEAT 4C — THE AFTERMATH (back in the barrow, after the vision dive exits) ----

        private static DialogueLine[] GetBeat4AftermathLines()
        {
            // Audit fix: replaced the on-the-nose "I couldn't see her... I still couldn't see her face" restatement.
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "She was right there. The one who made me. And I still couldn't see her face.", seconds = 6f },
                new DialogueLine { speaker = "The Mourners", text = "No. You couldn't. The Garden gives the sight you've earned, not the sight you haven't. You've seen the act. You've heard the prayer she said over you. The face is owed to you too, but it isn't ours to hand across. You will collect it yourself, one who defies. We've only shown you that it exists, and that at the end, she hoped you'd live.", seconds = 20f },
                new DialogueLine { speaker = "Echo", text = "Cipher, I don't have her face either. Must be from before you had me, and the Garden just told you it isn't theirs to give back. But I caught that prayer, every word, and I'm keeping it. Next time we're in front of those hands, we'll know them.", seconds = 14f },
                new DialogueLine { speaker = "Ronin-7", text = "Two people at either end of the thing that killed me, and neither one wanted it. I've been chasing the wrong shape.", seconds = 7f },
            };
        }

        // ---- BEAT 5A — THE LEAVING (the Mourners' farewell + descent hook) ----

        private static DialogueLine[] GetBeat5LeavingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "The Mourners", text = "The truth was yours to earn, and you earned it. We don't give you peace, one who defies. Peace is for the buried, and you turned the grave down once already. We give you the name, the sight, and a direction. The hands you saw didn't work in the light. They worked beneath it. If you mean to find the face, go down.", seconds = 20f },
                new DialogueLine { speaker = "Ronin-7", text = "Down where. Be plain with me once, at the end of it.", seconds = 4f },
                new DialogueLine { speaker = "The Mourners", text = "We don't draw maps. We give sight, not answers, we told you that. But here's the shape of it. The hand that made you and the hand that doubts you both reach up from below, not from the graves, those are ours, from the other dark, the one your Program digs and leaves off every chart. Go down to them.", seconds = 20f },
            };
        }

        // ---- BEAT 5B — THE REUNION (back through the gate, the crew, the outro hook) ----

        private static DialogueLine[] GetBeat5ReunionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "There he is. We held the gate, every hour of it, and that thing never let us a step closer. You walk out of there in one piece?", seconds = 10f },
                new DialogueLine { speaker = "Ronin-7", text = "In one piece. It tested the head and the hands, Echo, not the blade. The blade only mattered at the very end.", seconds = 8f },
                new DialogueLine { speaker = "Coral Vex", text = "It opened for you, and it let you walk back out, which is more than the legend ever promised anyone. Did it give you the truth, Cipher? Did you see the hand?", seconds = 11f },
                new DialogueLine { speaker = "Ronin-7", text = "I saw the hand that made the switch. A woman. Careful. She said she hoped I'd save us, and I couldn't see her face. I saw the hand that pulled it too. He grieved it. And he's started to doubt the order that made him.", seconds = 14f },
                new DialogueLine { speaker = "Mera Voss", text = "A maker who hoped you'd live. A handler who doubts the order he carried out. I hunted for the Program for years and I'd have sworn it didn't have a doubting bone in it. Turns out it's riddled with them.", seconds = 12f },
                // Audit fix: second sentence softened per the audit's suggested rewrite.
                new DialogueLine { speaker = "Mira", text = "You're sad. You found out something good, didn't you. So why do you look like that.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "I found out two people on the inside didn't want me dead, little one. One of them hoped I'd live before I was old enough to remember her. That's a good thing. It's just heavy.", seconds = 11f },
                // Audit fix: "the living and the loud" paraphrased instead of echoed verbatim from the Mourners.
                new DialogueLine { speaker = "Ronin-7", text = "The Garden says the answers aren't up here, not with the rest of us still breathing and shouting. So we stop hunting the surface. We go down.", seconds = 10f },
                new DialogueLine { speaker = "Echo", text = "Down the dark next, Cipher. We came to a graveyard to ask the dead who made us, and they handed us two living questions and a road under the floor. The pits. The deeps. Wherever they buried it, we go dig it out.", seconds = 12f },
            };
        }
    }
}
