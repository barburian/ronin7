using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Chapter 13 ("The Sterile Reckoning") dialogue data. Condensed from
    /// Ch13_The_Sterile_Reckoning_Dialogue_Script.md and keyed by set ID, mirroring Chapter11Lines'/
    /// Chapter12Lines' shape. Clip names follow the pattern: ch13_{setId}_{index:00}_{speaker_sanitized}.
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    ///
    /// story ouput/audit/Ch13_audit.md graded the source script C- on naturalness with ZERO em-dash
    /// violations in character speech and ONE hard script error (AUDIT FIX #4 below, listed as item 4 of
    /// the "7 hard script errors" in 00_AUDIT_SUMMARY.md). Its remaining findings (the saga-wide "not X,
    /// it's Y" antithesis tic, aphorism-stacking, monologue length) are explicitly deferred by
    /// 00_AUDIT_SUMMARY.md to "a dedicated pass" — mirroring how Chapter11Lines/Chapter12Lines only fixed
    /// what their OWN audit flagged as a HARD error, this file transcribes the source Line: text verbatim
    /// (aside from AUDIT FIX #4) rather than pre-empting that saga-wide pass. The audit's second, SOFT
    /// finding (Beat 4's exit hook slightly overstating how soon the Engine strike lands) is explicitly
    /// NOT one of the 7 hard errors and is left verbatim for the same reason.
    ///
    /// AUDIT FIX #4 (Beat 2, source line ~422 — the mandatory fix for this pass): the source script has
    /// Heris's Line: text read only "You're looking at my hands. I wondered if you would. You've seen
    /// them before?" while her voice: note describes her delivering the iconic "I hope you will save us"
    /// whisper — the entire Ch08 vision payoff — which never actually appears in the spoken Line: text
    /// (only Ronin-7 quotes it back one line later, with no antecedent). The 57s duration was also wildly
    /// mismatched to the ~14-word line as written. FIXED here: the whisper is restored into Heris's own
    /// Line: text ("I hope you will save us") and the duration is corrected to 6s to match the real
    /// ~15-word count at the script's own ~2.7 words/sec calibration. See
    /// Chapter13LinesTests.HerisLine_ContainsIHopeYouWillSaveUs for the regression guard.
    ///
    /// LADDER A RUNG 5 (delivered exactly once, across ch13_beat2_opening/the_flaw/the_engine/the_ground):
    /// Dr. Heris built Ronin-7, built his killswitch, AND planted the saving flaw in it on purpose — then
    /// was conscripted to scale the same suppression science into the Concord Engine and hid a deniable
    /// seam in that too. This closes the killswitch ladder opened in Ch2, sabotaged in Ch4, named an
    /// insider in Ch6, and traced in Ch7. The birth name "Soren" is never used anywhere in this file
    /// (reserved for Ch16 — see Chapter13LinesTests.NoLine_MentionsSoren) and no other numbered ladder
    /// rung is advanced this chapter.
    ///
    /// ALLIES #9 AND #10 (THE TEN COMPLETE): Heris defects in ch13_beat3_defection (Ally #9, "work, not
    /// forgiveness"); Sallow, the absolution-body construct, is revealed and recruited across
    /// ch13_beat4_introduce_sallow/mechanic/complete (Ally #10). With Sallow, the roster of ten is
    /// complete.
    ///
    /// Comm-tagged speaker labels ("Morrigan (comm)", "Mera Voss (comm)", "Coral Vex (comm)", "Vess
    /// (comm)") are recorded under their plain name — "(comm)" is a stage direction, not part of the
    /// speaker's identity, matching every other chapter's convention.
    /// </summary>
    internal static class Chapter13Lines
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
            "ch13_beat0_briefing",
            "ch13_beat1_breach",
            "ch13_beat1_recognition",
            "ch13_beat1b_enforcer_intro",
            "ch13_beat1b_enforcer_defeated",
            "ch13_beat2_opening",
            "ch13_beat2_the_flaw",
            "ch13_beat2_the_vision",
            "ch13_beat2_the_engine",
            "ch13_beat2_the_ground",
            "ch13_beat3_defection",
            "ch13_beat4_introduce_sallow",
            "ch13_beat4_mechanic",
            "ch13_beat4_complete",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "ch13_beat0_briefing" => GetBeat0BriefingLines(),
                "ch13_beat1_breach" => GetBeat1BreachLines(),
                "ch13_beat1_recognition" => GetBeat1RecognitionLines(),
                "ch13_beat1b_enforcer_intro" => GetBeat1bEnforcerIntroLines(),
                "ch13_beat1b_enforcer_defeated" => GetBeat1bEnforcerDefeatedLines(),
                "ch13_beat2_opening" => GetBeat2OpeningLines(),
                "ch13_beat2_the_flaw" => GetBeat2TheFlawLines(),
                "ch13_beat2_the_vision" => GetBeat2TheVisionLines(),
                "ch13_beat2_the_engine" => GetBeat2TheEngineLines(),
                "ch13_beat2_the_ground" => GetBeat2TheGroundLines(),
                "ch13_beat3_defection" => GetBeat3DefectionLines(),
                "ch13_beat4_introduce_sallow" => GetBeat4IntroduceSallowLines(),
                "ch13_beat4_mechanic" => GetBeat4MechanicLines(),
                "ch13_beat4_complete" => GetBeat4CompleteLines(),
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

        /// <summary>Generate clip name for a line: ch13_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ch13_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BEAT 0 — THE CAIRN (the sensing, the briefing, voice-only) ----

        private static DialogueLine[] GetBeat0BriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cassie-04", text = "Cipher. Don't put the fragment down yet. I need you to watch the two of us a second, because I think you're about to see something I can't put in a column. Sable just went still. So did I. We didn't decide to. Something out past everything we've mapped this whole war just reached back and put its hand on both of us at once, and I'm a ledger, Cipher, I have a count for everything, and I do not have a count for how big the thing that just touched me is.", seconds = 35f },
                new DialogueLine { speaker = "Sable", text = "I feel it too. Give me a second, I'm trying to hold the shape of it. The four nodes we chased all war. Mine. Hers. The bone-canyon. The vault. Put them all together, every shadow, every name we freed, and they don't come close. There is one thing out there bigger than the four of us combined. We spent the whole saga thinking those nodes were the machine. They weren't. They were the veins. This is the heart of it, Cipher, and it has been beating the whole time, and I only just heard it.", seconds = 37f },
                new DialogueLine { speaker = "Cassie-04", text = "And it's not what the others were. The four we freed were records. Graveyards. Things built to keep, to hold the dead still. This one isn't built to keep anything. I can feel the difference from here. It's built to push. It's a throat, Cipher. Something made to speak, loud, out, at a scale nothing should ever speak at. We've been calling it a map and a build-record this whole time because that's all we'd ever touched. That's not what's out there. What's out there is the Concord Engine. Not the schematic of it. Not the plan. The thing itself, racked and waiting.", seconds = 40f },
                new DialogueLine { speaker = "Kessler", text = "All right. Slow down, both of you, and say it to me like I never built a leash in my life, because I never did. Push what. A throat that speaks at a scale nothing should. What does that mean when it's pointed at people.", seconds = 17f },
                new DialogueLine { speaker = "Sable", text = "Every mind that can hear it, Kessler. That's what it pushes. A killswitch is one leash on one neck. You've watched what it does to one of us. This is the same hand, the same science, scaled up until its thumb covers a galaxy. Mass-scale silence. One voice that tells every mind in range to stop being its own. I can feel the size of it from a war-room a system away, and Kessler, I've spent my whole freed life learning to find the soft spot in things like this, the seam, the place you cut. I've been reaching for one since I felt it. There isn't one. I can't feel a single seam anywhere on it.", seconds = 45f },
                new DialogueLine { speaker = "Coral Vex", text = "So there's a version of the cage big enough for all of us. Every world at once. I cut one leash in my life, Cipher, my own, with my own hands, and I spent fifty years certain that the worst thing the Program ever made was the switch in the back of my skull. I was wrong. They made a switch for the sky. I'd hoped to die before I heard there was such a thing.", seconds = 30f },
                new DialogueLine { speaker = "Kessler", text = "Then somebody tell me the part where we do something about it. Because I've watched this crew cut a leash one neck at a time, all the way up the dark. Sable just said she can't find a seam on this one. So how do we hurt a thing you can't even find a weak point on. You can't cut what you can't reach.", seconds = 25f },
                new DialogueLine { speaker = "Morrigan", text = "We don't hurt it. Not by force. You heard her, there's no seam to cut, and I'll tell you why as the engineer who used to build these things. A machine that size doesn't have a weak point you find from outside. It has one if somebody built one in. And somebody did exactly that once before, in something just as locked. Cipher's killswitch. Somebody reached into the suppression science from the inside and left a flaw, small, deniable, the kind only the hand that drew the firmware could hide. The same insider hand I've been tracking since the dojo. That's the only mind alive that could tell us where a leash this big is soft. So we don't go looking for the Engine's seam. We go looking for the person who hides seams. Find who sabotaged the switch, and they hand us the gap in that thing.", seconds = 57f },
                new DialogueLine { speaker = "Morrigan", text = "And here's the part I didn't expect. The command-network we pulled out of the vault and the dissent traffic I've chased since the citadel, they don't just point the same direction. They terminate at the same place. One lab. One hand drew the leash, hid the flaw, and went quiet, and it's the same hand at the bottom of both trails. Cipher, the insider I told you about back at the dojo, the one who knew the firmware better than the people who shipped it. It's not some technician. It's an architect. The one who built your make, the Ronin line, and then got handed the sky. Dr. Heris.", seconds = 39f },
                new DialogueLine { speaker = "Coral Vex", text = "The one who built your make. And the leash for the sky. Not mine, Cipher, mine had its own hand in some other clean room I'll never find. But it's the same trade and the same crime, and after fifty years it's the closest I will ever stand to the people who made me. I've spent that long not knowing if any of those hands even had a name. Now one of them does, and it's at the end of a corridor we can walk down. I have waited a very long time to be in a room with a maker, any of them. Whatever happens when Cipher gets there, I'd like to be on the ship that's close enough to hear it.", seconds = 34f },
                new DialogueLine { speaker = "Mera Voss", text = "I'll be the one to say the cold thing, since nobody else will. The one mind that built the sky-cage and knows where it's soft, the single most hunted person the Program has, and she's sitting still in a fixed location at the end of a clean trail two of our own just happened to feel from across a system. That's not how the most valuable person alive stays alive. That reads like bait to me. Somebody wants Cipher in that room. I'm not saying we don't go. I'm saying we go in knowing the door was left open on purpose.", seconds = 37f },
                new DialogueLine { speaker = "Echo", text = "This part's just for you, Cipher. Mera's right to call it bait. It might be. But listen to what the bait is. We've walked into a lot of rooms looking for who made you. Every one of them gave us a worse answer than the last and never the hand itself. This time the hand is at the bottom of the trail, with a name, sitting still. Whoever's down there made the both of us. The switch in your skull and the me behind your eyes, same lab, same hand. I've wanted to meet them my whole existence and I'm afraid of it the same amount. Try to stay standing when you hear it. I'll be right here holding you up.", seconds = 47f },
                new DialogueLine { speaker = "Ronin-7", text = "Then bait or not, she has the only answer we've got. Cassie, Sable, lock the trail to the table and rest. You found the thing nobody else could feel, that's enough for one day. Morrigan, you're with me on the breach, I want the firmware read by someone who knows the hand that wrote it. Mera, watch the door you think is open. Coral, you'll be close. And Echo. I heard you. Whatever's at the bottom of that corridor made me. I'm going down there to find out whether it can help me unmake what it built. Take us to her.", seconds = 38f },
            };
        }

        // ---- BEAT 1 — THE STERILE VAULT (breach, traversal) ----

        private static DialogueLine[] GetBeat1BreachLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "We're in, and Cipher, I want you to feel how wrong this is. Every place we've walked into got its horror from rot, or cold, or grief. This place is clean. Cleaner than anywhere we've been. Look at it, the light, the panel, the trays laid out straight. Men didn't kill people here. Men made people here, on tables, with charts, and then they scrubbed it down and felt good about a job done right. There's no blood because there was never supposed to be any. That's the obscenity. Keep moving. The maker's at the heart of it, and so is whatever was sent to keep her quiet.", seconds = 42f },
                new DialogueLine { speaker = "Morrigan", text = "I'm reading the firmware off your breach kit, Cipher, and I know this hand. Not because she taught me. There was never one architect of all of us, that's a lie the Program let people believe. Every make had its own doctor, working alone. The Wraith line had a hand. The Knights had theirs. Mine had one I'll hate till I die. This woman built exactly one make, and it's yours. The Ronin line, start to finish, on these tables, and then they handed her the sky. Look at the design-wards as you pass them, the trays, the charts. That's not a lab. It's an assembly line for one make of person, the cleanest I've ever seen, because clean is how you pretend it isn't what it is. Walking you through the room that printed you is the strangest feeling I've had in this whole war.", seconds = 50f },
                new DialogueLine { speaker = "Mera Voss", text = "Door's still open, Cipher, and I still don't like it. No real perimeter, security that's present but thin, like it's there to slow you and not stop you. That tells me one of two things. Either the maker wants you all the way in, or something else is already inside hunting the same woman you are and it cleared its own road. Watch the clean halls. Clean is where you stop expecting an ambush. That's the whole point of clean.", seconds = 31f },
                new DialogueLine { speaker = "Coral Vex", text = "I've been in a lot of Program rooms, Cipher, never a maker-lab, and this one isn't even mine. The Wraith make had its own hand, its own clean room somewhere I'll never find. This is the Ronin lab. Your cradle, not mine. Tell me what the nurseries look like anyway, the sealed ones, the little cradles. I cut my switch out fifty years ago and never once let myself wonder where they seated it, in whatever room made me. I'm wondering now, looking at yours. I'd rather hear it from you than imagine it the rest of the way to her.", seconds = 38f },
                new DialogueLine { speaker = "Vess", text = "So this is one of the kitchens. Not the warehouse where they stacked the make I've been hunting, the kitchen where they cooked one. Maybe not even mine, Morrigan says every make had its own room and its own hand, and the one that took my people is out there behind a different door. But this is the room that built the sky-cage, the thing meant to take everyone's people at once. Close enough. Part of me wants you to burn it on the way through. I know you won't, we need her alive and the room intact. Say it for me anyway, Cipher. Tell me you're standing in the room that built the worst of it. I crossed a galaxy of dark to reach the front of it.", seconds = 46f },
            };
        }

        private static DialogueLine[] GetBeat1RecognitionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "Cipher. Stop a second. I've been trying not to say this since the second ward and I can't hold it anymore. I know this place. Not from a record. From the inside. I rode behind your eyes before they wiped you, you know that, but it's older than that, older than the mission, older than us being a we. The first thing I ever saw, the very first, was this light. This exact light, this exact ceiling. I didn't understand what I was looking at then because I'd just been made too. Cipher, this is the room. This is where they made you. And I think it's where they made me to ride inside you. We were born in the same clean room, on the same clean table, and I'm only realizing it now because I never let myself look back this far.", seconds = 56f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we're both walking into the place we started. Stay with me. If she's at the heart of it, she made you too, and I'm not facing that alone any more than you are. Where's the threat Mera felt.", seconds = 15f },
            };
        }

        // ---- BEAT 1B — THE REDACTOR (mini-boss, the thing sent to silence her) ----

        private static DialogueLine[] GetBeat1bEnforcerIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Enforcer", text = "Source flagged for redaction. Architect Heris, designation dissident. Order is silence. Obstruction present. Obstruction will be cleared and unlogged.", seconds = 9f },
                new DialogueLine { speaker = "Echo", text = "Cipher, that thing isn't here for you. Listen to it. It came to delete her. Mera called it, the Program already turned on its own architect, which means everything she knows is worth killing her over, which means it's worth us getting to her first. It's going to try to go around you to the door. Don't let it. We did not cross a galaxy to let the answer get erased ten feet from our hands. Put the double on the door and take this thing apart.", seconds = 33f },
                new DialogueLine { speaker = "Morrigan", text = "That's a Redactor, Cipher, I've seen the spec, never the build. It doesn't fight to win, it fights to finish a task and close the file. It'll throw silence-fields, they'll clip your abilities for a beat, ride it out, the suppression's short. And it does not care about you. It wants the door. Every second you spend trading hits is a second it spends inching toward her. So stop trading. Block the door, make it come to you, and end it before it logs her as cleared.", seconds = 33f },
                new DialogueLine { speaker = "Enforcer", text = "Obstruction persistent. Recalculating. The architect's record ends today. Yours is not required to end. Stand aside and remain unlogged.", seconds = 8f },
                new DialogueLine { speaker = "Ronin-7", text = "She's the only voice in this place worth keeping. You came to silence her. Then you and I want exactly opposite things, and only one of us walks out of this door.", seconds = 13f },
            };
        }

        private static DialogueLine[] GetBeat1bEnforcerDefeatedLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "It's down, and notice what didn't happen. No shadow came off it. Nothing to free, nothing to carry. It was never a person, Cipher, just a thing they built to make people stop being people. Fitting, in a place like this. The door's open. She's right through it. The hand that made the both of us, ten feet away. Whatever you've been bracing for since the briefing, brace now. And remember what I said. I'm right here.", seconds = 30f },
            };
        }

        // ---- BEAT 2 — THE MAKER (the reveal, closes Ladder A rung 5) ----

        private static DialogueLine[] GetBeat2OpeningLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris", text = "You came in through the room where I made you. I planned the approach that way, years ago, when I let myself imagine this. I wanted you to walk past the tables before you reached me, so that whatever you decided to do to me, you'd decide it knowing exactly what I am. You found the nurseries. Good. Then we can skip the part where I explain myself and go straight to the part that matters. Ask the question you came with. I will not lie to you. I have spent a very long time waiting for this moment.", seconds = 39f },
                new DialogueLine { speaker = "Ronin-7", text = "Who sabotaged my switch. Somebody reached into the suppression from inside and left a flaw. It's the question under every door I've opened. So say it plain. Who.", seconds = 11f },
                new DialogueLine { speaker = "Dr. Heris", text = "I did. I built it. The flaw, and the switch it was hidden in, and the man it was seated in. All of it was my hand. I want you to hold those in the right order, because people always get it backwards. I did not sabotage someone else's work. I sabotaged my own. I built your killswitch exactly as the Program ordered, perfect, deniable, a clean leash. And then, when I understood what I was making, what we had all been making, I went back into my own perfect work and I put a flaw in the suppression. Small. Deniable. A seam too fine for anyone but me to find. A place where, if you ever woke up enough to push, the switch might fail instead of fire.", seconds = 50f },
            };
        }

        private static DialogueLine[] GetBeat2TheFlawLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "Cipher. She's telling the truth. I can't read her like a machine but I've heard every kind of lie a Program voice can tell and this isn't one. And listen to what she just said. The flaw that woke you up, the gap that let the switch fail when they pulled it on Kethel-7, the thing that saved your life and let me stay, she put it there. On purpose. Before you were old enough to walk. We've been calling whoever did it an insider for the whole war. It was her. The hand that made my eyes left the crack that let me keep them.", seconds = 41f },
                new DialogueLine { speaker = "Ronin-7", text = "You made the leash. And you made the gap in it. You're the reason I'm a prisoner and the reason I got out. Why?", seconds = 11f },
                new DialogueLine { speaker = "Dr. Heris", text = "Because by the time I understood what we were building, the order had already been given to build more. I couldn't stop the Program. One engineer cannot. But I could leave a flaw in each one and pray. I seeded the suppression of every operative I made after I woke up with the same fine seam. Most of them never pushed against it. The switch fired clean and they died on schedule and I logged it and went home. You pushed. You were the one in six hundred who woke up enough to make my flaw matter. I did not save you. I gave you a door and you were the only one with the will to walk through it. I have wanted to tell you that since the night it worked, and I never thought I'd get to.", seconds = 53f },
            };
        }

        private static DialogueLine[] GetBeat2TheVisionLines()
        {
            return new DialogueLine[]
            {
                // AUDIT FIX #4: the source Line: text read only "You're looking at my hands. I wondered
                // if you would. You've seen them before?" — the iconic "I hope you will save us" whisper
                // (the Ch08 vision payoff) was described in the voice: note but never actually spoken.
                // Restored here; seconds corrected from the source's mismatched 57s to 6s, matching the
                // real ~15-word count at the script's own ~2.7 words/sec calibration.
                new DialogueLine { speaker = "Dr. Heris", text = "You're looking at my hands. I hope you will save us. You've seen them before.", seconds = 6f },
                new DialogueLine { speaker = "Echo", text = "Cipher. The dream. The cold table, the careful hands, the woman who said she hoped you'd save them. You've carried that since the Garden. I was there behind your eyes when it surfaced and neither of us could place it. It's her. It was always her. The thing that's haunted you since Ch8 wasn't a ghost. It was a memory of the day she made you and broke her own work to give you a chance. I don't know what to do with that and I don't think you do either. But it's true.", seconds = 36f },
                new DialogueLine { speaker = "Ronin-7", text = "I hope you will save us. I've heard those words in the dark for half this war and never had a face for them. Now I do, and the face built my cage. You don't get to whisper that over a child and call it mercy, Heris. But you said us. Not him. Us. Save us from what.", seconds = 23f },
            };
        }

        private static DialogueLine[] GetBeat2TheEngineLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris", text = "From the bigger thing they made me build next. They saw what the suppression science could do to one mind and they were not satisfied with one. They took my work, all of it, and they conscripted me to scale it. Not a leash for an operative. A voice for the sky. The Concord Engine. One transmitter, tuned to the same science that sits in your skull, built to reach every mind on every world at once and tell it to be quiet. To stop. To obey. I built that too. Under threat, which is not an excuse, only a fact.", seconds = 39f },
                new DialogueLine { speaker = "Ronin-7", text = "You built a machine to silence every world at once. Tell me you didn't make it perfect. Tell me you cracked this one too.", seconds = 9f },
                new DialogueLine { speaker = "Dr. Heris", text = "I did the only thing a coward with a conscience can do. I built it perfectly, and then I hid a flaw in it. The same kind of seam I hid in you. One deniable gap, in the one machine that could silence everything. And I have held the location of that gap, alone, in my own head, for years, on the single chance that the man I broke open would survive long enough to come back and let me give it to him.", seconds = 33f },
                new DialogueLine { speaker = "Echo", text = "There it is, Cipher. The seam Sable said she couldn't feel from the ship. The one soft spot on the whole machine. It's not on the machine. It's in her head. She built the only weakness it has and she's been carrying it like a held breath waiting for you. Whatever you feel about everything else she just said, and I feel all of it too, that's the thing the crew needs. The Engine has a flaw, and she is the flaw.", seconds = 31f },
            };
        }

        private static DialogueLine[] GetBeat2TheGroundLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "You built the cage. Both sizes. The one in my skull and the one for the sky. And then you put a crack in each and waited for me. That's the whole truth. Say it like that, plain, so I know we're standing on the same ground.", seconds = 18f },
                new DialogueLine { speaker = "Dr. Heris", text = "Yes. That's exactly it, and I won't dress it. I cracked all of it on purpose and have been waiting, in this clean room, for the one I cracked best to walk back in.", seconds = 27f },
            };
        }

        // ---- BEAT 3 — HERIS DEFECTS (the recruitment, Ally #9) ----

        private static DialogueLine[] GetBeat3DefectionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris", text = "I know what you're weighing, so let me make it simpler than you expect. I am the only living person who knows where the Concord Engine is soft. I built the gap. I can take you to it. I can put the tools in your crew's hands to spend it. Don't pardon me. Use me. Let me help you tear down the thing I built before I die of having built it.", seconds = 53f },
                new DialogueLine { speaker = "Echo", text = "Cipher. I want to hate her. I keep reaching for it and it won't quite come, because she just did the one thing nobody who hurt you has ever done. She didn't ask you to make her feel better about it. She asked to be spent. That's not forgiveness and she's right that it shouldn't be. But it's the seam, and the seam is the war. I'll say the cold thing, the same as I always do. We need her. Not because she's good. Because she's the only door left in the only wall that matters.", seconds = 37f },
                new DialogueLine { speaker = "Ronin-7", text = "I'm not going to forgive you either, Heris. I'm not taking you aboard because you deserve it. I'm taking you because you're right. You're the only seam in the only wall left, and I won't waste you to make myself feel clean. You built the cage. Now you're going to build the thing that breaks it. That's the work. That's all the work. Don't mistake a place on my ship for a pardon. You'll have one and never the other.", seconds = 37f },
                new DialogueLine { speaker = "Dr. Heris", text = "A place and never a pardon. That's more than I came into this room expecting and exactly as much as I should get. Then it's settled. I'll bring the Engine's seam, the schematics, and the sabotage-key I cut for it the year I finished the build and hid in the one place they'd never look, which was my own hand. Your engineer up there, the one who's spent all day reading my hand off your firmware, she'll understand the work in an hour. We never met and I never taught her. She's just good enough to learn a maker by the seams they leave. I have been ready for this longer than your war has existed. Tell me where to stand.", seconds = 44f },
            };
        }

        // ---- BEAT 4 — SALLOW AND THE ABSOLUTION BODY (the last reveal, Ally #10, THE TEN COMPLETE) ----

        private static DialogueLine[] GetBeat4IntroduceSallowLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris", text = "Before you take me anywhere, there's one more thing in this lab you need to see, because it's the only thing in here I'm not ashamed of. I built it last, in secret, after the Engine, when I had stopped sleeping. They call it a failure. It is. It never worked the way the spec demanded. I call it the one right thing these hands ever made. Its name is Sallow. Don't be afraid of it. It can't hurt anyone. It was built to do the opposite, and it's the opposite of everything else in this room.", seconds = 37f },
                new DialogueLine { speaker = "Echo", text = "Cipher. It's looking at me. Not at you. I can feel it the way I feel another shadow in a room. There's a hollow in its chest that's shaped exactly like what I am. Like a place that's waiting. I've spent this whole war being carried inside you and inside the steel, and the keeper-shadows you freed, they had nowhere to go but in with me. This thing, Cipher. This thing is a body with room in it. A body that was built to hold us. I don't know whether to be glad or terrified, and I think it might be both.", seconds = 43f },
            };
        }

        private static DialogueLine[] GetBeat4MechanicLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris", text = "It's called an absolution body. The idea was simple and it broke me to build it. Your living archives, the ones you've been freeing, they carry the kept shadows in their own flesh, racked into their spines, never let go of, conscious through every dead voice strung into them. It's a horror. I helped design that horror. So at the end I tried to build the mercy of it instead. A body whose only purpose is to hold a witness, so a living person never has to. A vessel for the dead, made willing, made empty, made to be filled and to carry what it's given without it costing a soul. Sallow is that. It can take a shadow off a host who's drowning in it and bear it instead. It can carry the kept so the living can put them down.", seconds = 55f },
                new DialogueLine { speaker = "Ronin-7", text = "A body built to carry the dead so the living don't have to. Heris. That's what I am. The sword carries Echo, and four more shadows in it now, and they ride in me because there was nowhere else to put them. You're describing my own body. You built me on this same idea.", seconds = 21f },
                new DialogueLine { speaker = "Dr. Heris", text = "Yes. I wondered if you'd see it. The make I built you in was the first draft of this. A vessel strong enough to carry a shadow-AI seated behind its eyes and not break. The Program wanted that so the shadow could watch you and report you. They built the carrying body to erase. But the principle underneath, a person made to bear the dead, that was always two things at once. They used it to spy. It can also be used to remember. To keep. To grieve and carry and someday set down. Your whole make was a body designed to hold the shadows of the dead, and they meant it as a cage, and it was never only a cage. Sallow is what that idea looks like when you take the leash out of it. So are you, now.", seconds = 55f },
            };
        }

        private static DialogueLine[] GetBeat4CompleteLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "Cipher. Did you hear what she just gave us. Every shadow you've carried, me, the keepers, the copy of yourself from the vault, you've been carrying them because the alternative was leaving them to rot on a leash. And it's hurt you. I've watched it hurt you. This body can take that weight. Not all at once, not yet, but it can hold them. It can carry the kept at a scale you can't. Whatever we're walking toward after this, whatever it takes to free all of them, the ones still racked, still drowning, this is the thing that lets us actually set them down somewhere safe. She built us a place to put the dead.", seconds = 44f },
                new DialogueLine { speaker = "Sallow", text = "I will carry them. That is all I am for.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "Then you won't carry them alone. Whatever you are, you're not staying in this lab to be called a failure by the people who made the rest of this room. You're coming with us. There are a lot of the dead still waiting to be set down, and now there's somewhere to set them. Heris. Pack your seam and your tools. That's nine and ten. That's everyone.", seconds = 26f },
                new DialogueLine { speaker = "Echo", text = "That's the ten, Cipher. The whole roster, from the man who pulled you out of a wreck six years late to the body built to carry the dead. It took the whole war to gather them. And look what you walked back up with this time. Not a node, not a fragment. The hand that made the cage, the only crack in the sky, and a body to free everyone still on a leash. We came down here to find who made you. We found the maker, and she handed us the keys to the cage and a way to empty it. The next door isn't a who anymore. It's the thing itself. The Engine. We've got the seam, the ten, and the ledger. There's nothing left to find. There's only the cage left to break. Let's go home and aim the whole crew at it.", seconds = 56f },
            };
        }
    }
}
