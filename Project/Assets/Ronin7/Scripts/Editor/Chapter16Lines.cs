using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Chapter 16 ("The Throne of Ashes") dialogue data — the SAGA FINALE, closing Ladders C,
    /// D, and E and Act IV. Condensed from Ch16_The_Throne_of_Ashes_Dialogue_Script.md and keyed by set
    /// ID, mirroring Chapter13Lines' shape. Clip names follow the pattern:
    /// ch16_{setId}_{index:00}_{speaker_sanitized}. Each line's clip field is left null; TTS or audio
    /// sourcing fills it at build time.
    ///
    /// story ouput/audit/Ch16_audit.md graded the source script C- on naturalness with ZERO em-dash
    /// violations in character speech and exactly ONE hard consistency error (00_AUDIT_SUMMARY.md item
    /// 6 of 7 — AUDIT FIX #6 below). Its remaining findings (the saga-wide "not X, it's Y" antithesis tic
    /// at its highest density here, aphorism-stacking, Echo reciting the Story Bible's theme line
    /// verbatim in the finale) are explicitly deferred by 00_AUDIT_SUMMARY.md to "the big naturalness
    /// pass" (a dedicated saga-wide edit) rather than a per-chapter fix — mirroring how Chapter13Lines
    /// only fixed what ITS OWN audit flagged as a hard error, this file transcribes the source Line: text
    /// verbatim aside from AUDIT FIX #6.
    ///
    /// AUDIT FIX #6 (00_AUDIT_SUMMARY.md item 6, the mandatory fix for this pass — MEDIUM consistency
    /// error): the source script has Echo call Samurai-4 a "newer make" than Ronin-7 ("Newer make,
    /// cleaner leash, same school, same hitch buried in her somewhere whether she knows it or not.") in
    /// ch16_beat1_descent_lower. Canon makes Ronin-7 the CURRENT/newest make (Knight -> Ninja -> Wraith
    /// -> Ronin, 00_STORY_BIBLE.md SS8 / 00c_PROGRAMS_AND_ARCHIVES_RECON.md SS2); a hunter of a make newer
    /// than Ronin implies an undocumented fifth program. FIXED here: reworded to "Same make as you,
    /// current issue, cleaner leash..." — she is an elite hunter/variant of the SAME Ronin make, not a
    /// later program. The audit's paired minor note (Samurai-4 addresses him "Ronin-7" in dialogue twice
    /// in ch16_beat2_duel, when convention reserves the serial for narration and "Cipher" for spoken
    /// address) is also fixed: her first use ("Operative designation Ronin-7") is kept verbatim as a
    /// formal file-designation citation establishing her cold Program voice, but her second, purely
    /// address-form use ("Do not mistake a fractional delay for a soul, Ronin-7.") is changed to "Cipher".
    /// See Chapter16LinesTests.NoLine_CallsSamurai4ANewerMake for the regression guard.
    ///
    /// LADDER C, RUNG 3 (closes Ladder C — identity recovery, EP30 per the source outline): delivered
    /// exactly once, in ch16_beat4_soren — Ronin-7 integrates the sealed memory-core and reclaims his
    /// birth name, SOREN. This is the ONE chapter in the saga where "Soren" is spoken; every set from
    /// ch16_beat4_soren onward legitimately keeps using it (that is continued use of an already-landed
    /// reveal, not a re-disclosure). See Chapter16LinesTests.SomeLine_ContainsSorenReveal.
    ///
    /// LADDER D, RUNG 3 + LADDER E (war-as-product folds into the same beat per the chapter's own
    /// continuity notes): delivered across ch16_beat7_forged_order (Khall: the Kethel-7 order was forged
    /// by the Hollow Kings to fracture a mercy operative into a weapon; the ten syndicates are kept at war
    /// on purpose because the war IS the product; Khall found the forgery, took a Hollow King prisoner,
    /// learned of the Concord Engine, faked his death, and has helped Cipher undercover ever since) and
    /// ch16_beat8_true_enemy (Maelgorn and the Obsidian Synod unmasked as the true enemy behind the
    /// Hollow Kings, the syndicates, and the Program — Morrigan's Ch6 "outside hand" named at last).
    /// Delivered exactly once, across those two adjacent sets.
    ///
    /// ALLY UNLOCKS: Samurai-4 breaks her own leash and joins as family (capstone bond beyond the numbered
    /// ten) in ch16_beat3_leash_break; Khall repents and allies in ch16_beat7_forged_order. Both recruit
    /// flags are set together with ch16_complete + galaxy1_complete at the chapter outro (mirrors how
    /// Ch10/Ch13 combined ally-recruit flags with chapter completion).
    ///
    /// TWO NEW SHORT ORIGINAL LINES (ch16_beat9_phase_taunt1/2): the source screenplay never breaks
    /// Maelgorn's throne-room confrontation into discrete combat phases (it is written as pure
    /// confrontation + refusal dialogue) but the finale's GAME NARRATIVE DESIGN section calls for a
    /// "final boss: Maelgorn / Obsidian Synod," so the mission wraps a 3-phase fight around that
    /// confrontation. These two short phase-transition barks are original (not transcribed), written in
    /// Maelgorn's established register (cold, regal, unhurried) and kept deliberately short to avoid
    /// adding to the antithesis/aphorism density the audit already flagged as this chapter's worst.
    ///
    /// Comm-tagged speaker labels ("Coral Vex (comm)", "Morrigan (comm)", "Vess (comm)", "Heris (comm)",
    /// "Cassie-04 (comm)", "Mira (comm)", "Maelgorn (V.O.)") are recorded under their plain name — "(comm)"
    /// / "(V.O.)" is a stage direction, not part of the speaker's identity, matching every other chapter's
    /// convention.
    /// </summary>
    internal static class Chapter16Lines
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
            "ch16_beat0_briefing",
            "ch16_beat1_descent_upper",
            "ch16_beat1_descent_lower",
            "ch16_beat2_duel",
            "ch16_beat3_leash_break",
            "ch16_beat4_soren",
            "ch16_beat5_throne_core",
            "ch16_beat6_loop_taunt",
            "ch16_beat6_loop_iter1",
            "ch16_beat6_loop_iter2",
            "ch16_beat6_loop_break",
            "ch16_beat6_loop_concede",
            "ch16_beat7_forged_order",
            "ch16_beat8_true_enemy",
            "ch16_beat9_seam_choice",
            "ch16_beat9_phase_taunt1",
            "ch16_beat9_phase_taunt2",
            "ch16_beat9_liberation",
            "ch16_beat9_throne_test",
            "ch16_beat9_closing_crew",
            "ch16_beat9_homecoming",
            "ch16_beat9_echo_final",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "ch16_beat0_briefing" => GetBeat0BriefingLines(),
                "ch16_beat1_descent_upper" => GetBeat1DescentUpperLines(),
                "ch16_beat1_descent_lower" => GetBeat1DescentLowerLines(),
                "ch16_beat2_duel" => GetBeat2DuelLines(),
                "ch16_beat3_leash_break" => GetBeat3LeashBreakLines(),
                "ch16_beat4_soren" => GetBeat4SorenLines(),
                "ch16_beat5_throne_core" => GetBeat5ThroneCoreLines(),
                "ch16_beat6_loop_taunt" => GetBeat6LoopTauntLines(),
                "ch16_beat6_loop_iter1" => GetBeat6LoopIter1Lines(),
                "ch16_beat6_loop_iter2" => GetBeat6LoopIter2Lines(),
                "ch16_beat6_loop_break" => GetBeat6LoopBreakLines(),
                "ch16_beat6_loop_concede" => GetBeat6LoopConcedeLines(),
                "ch16_beat7_forged_order" => GetBeat7ForgedOrderLines(),
                "ch16_beat8_true_enemy" => GetBeat8TrueEnemyLines(),
                "ch16_beat9_seam_choice" => GetBeat9SeamChoiceLines(),
                "ch16_beat9_phase_taunt1" => GetBeat9PhaseTaunt1Lines(),
                "ch16_beat9_phase_taunt2" => GetBeat9PhaseTaunt2Lines(),
                "ch16_beat9_liberation" => GetBeat9LiberationLines(),
                "ch16_beat9_throne_test" => GetBeat9ThroneTestLines(),
                "ch16_beat9_closing_crew" => GetBeat9ClosingCrewLines(),
                "ch16_beat9_homecoming" => GetBeat9HomecomingLines(),
                "ch16_beat9_echo_final" => GetBeat9EchoFinalLines(),
                _ => new DialogueLine[0],
            };

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

        /// <summary>Generate clip name for a line: ch16_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ch16_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BEAT 0 — THE CAIRN (the briefing, the full crew commits) ----

        private static DialogueLine[] GetBeat0BriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris", text = "This is the last map I have for you, and it is two answers in one shape, so look at the whole of it before you decide anything. The Iron Sepulcher. We built it to hold the most dangerous thing we ever made, a complete operative memory, intact, and the one it holds is yours. Your sealed past, racked near the top, behind every leash we ever seated in you. That is the first mark. Now follow it down. It does not sit above the Concord Engine. It is the Engine's spine. The same structure, top to bottom. Go all the way down through yourself and you are standing in the throne-core. And here, near the bottom, is the second mark. The seam I hid when they forced me to build it. Strike it right and the whole machine comes apart, and every shadow it is strung through goes free.", seconds = 56f },
                new DialogueLine { speaker = "Iris", text = "I ran the model three times and it keeps doing the same thing. I feed it the vault and I feed it the Engine and I keep waiting for it to find a seam between them, some wall, some door, anything, and it just keeps falling. No break. I don't have a joke for this one. I've got nothing.", seconds = 19f },
                new DialogueLine { speaker = "Coral Vex", text = "I can tell you what the model can't, child. Getting yourself back is the hardest climb there is, and I made it before any of you, alone, with my own switch in my own hand and no crew at my back. I came up the other side a person and it cost me fifty years of hiding to keep her. He won't have to hide. That's the difference between his descent and mine. We will be right behind him the whole way down, and there will be a ship full of his people at the top when he comes back up with his name.", seconds = 34f },
                new DialogueLine { speaker = "Cassie-04", text = "I'll give you the cold count, because somebody has to. I'm a ledger, and I can feel the bottom of that map the way I feel a column that won't close. Sable and I have been carrying the weight of the Engine since the lab. It's louder now. We're closer. And every shadow strung into it, every kept witness in that lattice, is a name in my registry I have never been able to free, because they're not stored, they're wired in. They're the wiring. You break the Engine, you don't just kill a weapon. You unmake the thing that's holding thousands of them open. I have wanted to close those columns my whole conscripted life.", seconds = 40f },
                new DialogueLine { speaker = "Sable", text = "Cassie counts them. I'm wired close enough to feel them breathe. That's the difference between us, Cipher, and it's why I'm afraid of this in a way she isn't. When it comes apart I won't read it off a ledger. I'll feel every single one let go, one at a time, the ones I was strung beside, my own make among them. I have been infrastructure my whole life, a thing the lattice ran through. I would like, just once, to be the hand that sets the others loose instead of the wire that holds them.", seconds = 35f },
                new DialogueLine { speaker = "Mera Voss", text = "A vault built to keep him out of himself isn't going to be empty. The Program kept this for one reason, to make sure he never reached the bottom, and they won't leave it to the wardens. They'll spend their best. Their finest hunter, sent to stop him at the exact place he starts to remember. I've trained against that make. They don't break and they don't tire and they don't have a hitch you can talk to. Or they didn't, until him. Go in expecting the best blade they have left, on the worst tier to fight one.", seconds = 36f },
                new DialogueLine { speaker = "Vess", text = "Then let them spend their best. We've turned every blade they ever pointed at us. Kerrax. Me. Half this room used to be aimed at him. Whatever they send down that hole to stop you, Cipher, it's just one more killer who was never asked. Show it the same thing you showed the rest of us. And if it won't take the choice, put it down and keep walking. Some of them won't. I almost didn't.", seconds = 25f },
                new DialogueLine { speaker = "Gryph", text = "I've held one descent in my life, a hold thirty years deep, and I learned the one law of the deep places. You don't go down alone, and you don't go down for nothing. He's going down for both. Himself, and the end of the thing that takes everyone's children. That's a reason worth the dark. The Cairn holds the top. Nobody gets down the shaft behind you that doesn't come through me first.", seconds = 24f },
                new DialogueLine { speaker = "Kessler", text = "All right. Go down. Get who you are. Finish the thing they built to take everyone's name, your name first. And then come back up to this ship. That's the whole mission to me. Not the Engine, not the throne, not whatever's at the bottom calling itself the truth. You. Coming back up that lift. That's an order, from the man who owns the rig.", seconds = 35f },
                new DialogueLine { speaker = "Ronin-7", text = "Then I'll come back up. You have my word, Kessler, and I don't spend it. Heris, the seam stays with you and the key stays cut. Morrigan, you read the descent as it peels, I want to know the second the architecture stops being architecture. Mera, you called the hunter, so you call the moment she shows. Coral, you're close, the way you promised. Sallow, you come down with us. There are a lot of the dead in that lattice and you're the thing that carries them home. The rest of you hold the top. I'm going down to get the rest of me, and then we end it. As free people. Not as anyone's weapon.", seconds = 41f },
                new DialogueLine { speaker = "Echo", text = "This part's just for you, Cipher, before you step off the lift. I've been quiet the whole briefing because I was deciding whether to say it. I've been your witness since the bay, since Kessler cracked the casket and I woke up wired to your eyes. Everything you've done since they bonded me to you, every face you couldn't place, every kill you couldn't account for, I kept it. I saw it, and I held it for you. But there's a part of you older than me, from before they ever wired me in, and I could never reach it either. That's what's at the bottom of that vault. The piece even I couldn't carry. Go down and get it. I'll be right here the whole way.", seconds = 44f },
                new DialogueLine { speaker = "Ronin-7", text = "Then let's go get it. Both of us. You carried it long enough. Take me down, Echo.", seconds = 7f },
            };
        }

        // ---- BEAT 1 — THE IRON SEPULCHER (descent, upper/mid tiers) ----

        private static DialogueLine[] GetBeat1DescentUpperLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "Feel that? Every tier we drop, something comes off you. The first gate was just steel. This one wasn't. Watch your own hands, Cipher, you stopped moving like an issued weapon two levels ago. They built this place to make a man turn around. Every floor is a leash they expected to hold you at. The cold ones up top are conditioning. The ones coming up are going to be memory, and memory's going to fight dirtier than any warden. Keep going down. I can't reach the bottom of it. That part of you is older than me, it always was yours, not mine to hand back. But I'm with you every floor of the way down to it.", seconds = 42f },
                new DialogueLine { speaker = "Coral Vex", text = "I know this part, Cipher, even though I never stood in your vault. Mine was smaller and I had no light but my own. There's a tier coming where the walls stop being walls and start being places you've been. Don't trust them. The Program doesn't waste a vault on stone, it wastes it on memory, because memory is the only guard that knows your name. When it shows you somewhere warm, that's the trap. When it shows you somewhere you did something you can't carry, that's worse, because part of you will want to stay there and finish paying for it. Don't. Keep your feet moving. The bottom is the only place that's true.", seconds = 41f },
            };
        }

        private static DialogueLine[] GetBeat1DescentLowerLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrigan", text = "Confirming what Echo's seeing from up here, Cipher, and I do not like it as an engineer. The architecture's losing coherence the lower you go. Not damage. Design. Somebody built this to read as solid steel at the top and to come apart into something I can't model at the bottom, and the only thing it could be coming apart into is you. Your sealed memory is structural. The vault isn't holding it, the vault is made of it the deeper it goes. So when it stops looking like a building, that's not it breaking. That's you, getting closer to the floor. Tell me the second a wall remembers something you don't.", seconds = 40f },
                // AUDIT FIX #6 (00_AUDIT_SUMMARY.md item 6 / Ch16_audit.md "medium"): the source line called
                // Samurai-4 a "newer make" than Ronin-7, implying an undocumented fifth program. Ronin is
                // canon's current/newest make (Knight -> Ninja -> Wraith -> Ronin); she is an elite hunter
                // variant of the SAME make, not a later one. Reworded below.
                new DialogueLine { speaker = "Echo", text = "Cipher. Stop. That's not a warden. Look at her. The kit's current-issue, the blade's the same school as yours, there's a hilt-light in it, she's got a shadow of her own riding her eyes. Mera called it. They sent the best one they have left, and they sat her right here, on the last tier before the floor, because this is the place they cannot let you reach. She's what you were. Same make as you, current issue, cleaner leash, same hitch buried in her somewhere whether she knows it or not. I'm telling you now so you hear it before she does something: you do not have to kill her. You of all people know there's another way down off this tier.", seconds = 45f },
            };
        }

        // ---- BEAT 2 — SAMURAI-4 (the duel becomes a dialogue) ----

        private static DialogueLine[] GetBeat2DuelLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Samurai-4", text = "Cipher. Operative designation Ronin-7. You will not pass this tier. The order is absolute. Stand down or I complete it. I have read your file. I know you spare. It will not work here, because I am the thing they built after they learned what sparing cost them with you. There is no hitch in me. Stand down.", seconds = 23f },
                new DialogueLine { speaker = "Ronin-7", text = "They told me the same thing about myself. That there was no hitch in me. That I was the clean one. Then I stood over a target who couldn't fight back and my hand stopped on its own, and the whole lie came apart in one second. You've already felt it. I watched it just now. The first time our blades locked, you had me, and you held a half-beat too long. That half-beat is the only honest thing they left in you.", seconds = 28f },
                // AUDIT FIX #6 (minor half): first "Ronin-7" address kept verbatim (a formal file-citation,
                // "Operative designation Ronin-7"); this second, purely address-form use is changed to "Cipher"
                // per convention (the serial is narration-only; spoken address uses "Cipher").
                new DialogueLine { speaker = "Samurai-4", text = "That was an inefficiency. I have logged it. It will be corrected. Do not mistake a fractional delay for a soul, Cipher. I am not you. I do not break.", seconds = 11f },
                new DialogueLine { speaker = "Echo", text = "Cipher, listen to her cadence, not her words. She just answered too fast. That's not a machine, that's a person arguing with herself and losing. I've heard that exact sound before. I heard it out of you, on the Tide, the first time you tried to tell me you didn't care whether the keeper lived. Keep her talking. Every exchange you don't kill her in is an exchange the leash has to explain itself in, and the leash is a terrible liar.", seconds = 31f },
                new DialogueLine { speaker = "Samurai-4", text = "Why do you not finish it? You have had three openings. I counted them. An operative who does not take an opening is malfunctioning, and yet you keep your blade off my throat on purpose. Stop it. Fight me correctly. If you will not complete me I cannot complete you, and the order does not allow for two of us standing here unfinished.", seconds = 22f },
                new DialogueLine { speaker = "Ronin-7", text = "I don't take the openings because I'm not here to add you to a count. They built you to finish people. They built me the same way and I'm done being that. You feel the hitch before the kill. They call it malfunction. I'm telling you what it actually is. It's the only honest thing left in you, and they spent your whole life teaching you to be ashamed of the one part of you that's real. I won't fight you for it. I'm going to do the thing they never let either of us see done.", seconds = 33f },
                new DialogueLine { speaker = "Samurai-4", text = "If I let go of the order I have nothing. Do you understand what you are asking. The order is the floor under me. It is the only thing that has ever told me what I am for. Take it away and I am a body with a blade and no reason to hold it. You are not offering me freedom. You are offering me a fall with no bottom.", seconds = 22f },
                new DialogueLine { speaker = "Ronin-7", text = "You will have a choice. That is not nothing. That is everything. It is the one thing they could never put in you and could never take out of me, and it is the whole difference between a weapon and a person. I'm not going to fight you for it. Look. My blade's down. You have a clean line to my throat right now and nothing in your way. So choose. Take the choice, or take my head. Either way it's yours, and it's the first thing in your life that ever was.", seconds = 35f },
            };
        }

        // ---- BEAT 3 — BREAKING HER LEASH (mercy as contagion; ally recruit) ----

        private static DialogueLine[] GetBeat3LeashBreakLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Samurai-4", text = "It's quiet. The order. It's. I have never heard it stop before. There was always a voice telling me the next thing and now there is nothing, and I do not. I do not know what to do with my hands. I had a clean line to your throat and I chose the wall instead and I do not know why I did it, except that you put your blade down and looked at me like I was a person, and no one has ever. No one has ever done that. What do I do now. There is no order. What do I do.", seconds = 33f },
                new DialogueLine { speaker = "Ronin-7", text = "You stand there a second and you let it be quiet. That's the first thing. The order's gone and nothing replaces it, and that emptiness is the worst hour you'll ever live and it's also the only free one you've had. I know, because I stood exactly where you're standing, on a salvager's table, with the voice gone and nothing in my hands. You don't figure out what you're for in the next minute. You just choose the next thing, and then the one after that, and that's the whole of it. That's all a person is. Take your mask off. You don't need it anymore.", seconds = 38f },
                new DialogueLine { speaker = "Coral Vex", text = "I felt the relay die from up here. Cipher, do you know how long it's been since I heard that exact sound, a leash cut by the hand it was wrapped around? Fifty years. Mine was the last one I knew of, and I did it alone in the dark and thought I'd be the only one forever. Tell her something for me, from the only other person alive who's stood where she's standing. The fall with no bottom, the one she's afraid of right now. It ends. You hit ground eventually, and the ground is yours, and you build on it. It took me half a century to believe that. She won't have to wait that long. She's got a ship full of us.", seconds = 42f },
                new DialogueLine { speaker = "Vess", text = "I'll tell her the other half, the part Coral's too kind to say. The choice doesn't make you clean. I cut toward him with everything I had and I chose the alliance instead, and I still carry every name I came to avenge. Freedom isn't forgiveness and it isn't peace. It's just yours. But I'll stand at her shoulder same as the rest of you stood at mine when I almost made the other call. She turned her blade on the wall instead of his throat. That's enough. That's where all of us started.", seconds = 32f },
                new DialogueLine { speaker = "Samurai-4", text = "They're talking to me. The voices on your comm. They're talking to me like I'm. Like I'm one of them. I tried to kill you ten minutes ago. I would have, if you hadn't put your blade down. And they're welcoming me anyway. I do not understand it and I am not going to pretend I do. But I'll come down with you. Not because of an order. Because I want to see the bottom of the thing that made both of us, and I want to be standing next to the only person who ever showed me I had a choice when we break it. That is my choice. The first one. I am keeping it.", seconds = 40f },
                new DialogueLine { speaker = "Ronin-7", text = "Then keep it, and come down. We've got one more tier, and what's at the bottom of it is the rest of me. You came here to stop me reaching it. Now you're going to watch me reach it instead. Stay at my shoulder. After today neither of us has a number anybody else gets to use.", seconds = 18f },
            };
        }

        // ---- BEAT 4 — RECLAIMING SOREN (the name beneath the number) — REVEAL, closes Ladder C ----

        private static DialogueLine[] GetBeat4SorenLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "This is it, Cipher. And this one isn't mine to hand you. I carried everything I ever saw behind these eyes, but this is the part from before me, the part I could never reach. It's there in the steel, and it's been waiting for you, not for me. You don't have to brace for it. It isn't a weapon and it isn't a wound. It's just you, the part they took before I ever woke up. Put your hand on it. Let it come up to you. I'll be right here when it does.", seconds = 28f },
                new DialogueLine { speaker = "Soren", text = "Soren. My name is Soren. There was a boy, and he had a world, and someone in a doorway who loved him said this name. They buried it so deep they had to build a vault to keep me out of it. But it's mine. It was always mine. Soren.", seconds = 20f },
                new DialogueLine { speaker = "Samurai-4", text = "Soren. I came down here to stop you from reaching that. They sent me to keep you a number, and I watched you take your name back instead, and I am only now understanding what they were so afraid of. It was never the rebellion. It was this. A weapon with a name underneath it. I have a number at the base of my skull and no name under it that I can find. Maybe there isn't one. But I watched you do it, and now I know it can be done, and that is more than I had this morning.", seconds = 34f },
                new DialogueLine { speaker = "Soren", text = "If there's a name under your number, it's down a vault like this one, and when this is over we'll go find it. You don't have to do it alone. None of us do anymore. That's the whole point of the ship at the top.", seconds = 13f },
                new DialogueLine { speaker = "Echo", text = "...Soren. Going to take me a minute to learn it, I'll be honest with you. I've called you Cipher since the second I woke up wired to your eyes. It was the only name either of us had, and it was theirs, not yours. So give me a little time. I'll learn to say it like I mean it. But the way down is still down. The floor under you isn't the bottom. Let's finish it. I'm right here, Soren.", seconds = 31f },
            };
        }

        // ---- BEAT 5 — INTO THE THRONE-CORE (the same descent, no cut) ----

        private static DialogueLine[] GetBeat5ThroneCoreLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Soren", text = "There it is. No door, no cut, no wall between the bottom of me and the bottom of this. Heris was right. The vault was the spine. I came all the way down to get my name and I walked straight into the cage doing it. Same descent the whole time. They built the deepest part of me on top of the deepest part of the war, so that anyone who reached one had already reached the other. That's not architecture. That's a confession. They knew the two were the same thing.", seconds = 33f },
                new DialogueLine { speaker = "Dr. Heris", text = "I can see your position against the structure from the seam-key, Soren, and you are exactly where I needed you to be, twenty years after I hid the flaw. The seam is the bright line off your right, along the primary lattice-spar. That is the flaw I hid the year they made me finish the build. Do not strike it yet. The Engine is live and the lattice is full, every shadow it holds is conscious and wired in, and if you cut the seam wrong you kill them with it instead of freeing them. Get to the seam, hold there, and wait for the whole crew and Sallow. We break it together or we do not break it right.", seconds = 41f },
                new DialogueLine { speaker = "Cassie-04", text = "Soren, I can feel the lattice from up here through the open structure and I need you to understand what you're standing inside. Every point of light in that thing is a name in my registry. Thousands of them. They're not dead and they're not stored, they're awake, strung into a throat and forced to be the wire that carries the silence. I have been reading their names off a ledger for the whole war and never been able to do one thing about it. You're standing in the room where I finally can. Hold the seam. Let us catch up. Nobody breaks that thing alone, least of all today.", seconds = 37f },
                new DialogueLine { speaker = "Samurai-4", text = "This is what I was guarding. Not you. This. They put their best blade on the last tier so no one would reach the floor and see what the floor was sitting on. I held the door and I never knew the room was behind it. Soren. There are thousands of them in there. Thousands. And I stood at the door and called it an order. How many of us are doing that right now. Standing at a door. Not knowing.", seconds = 29f },
                new DialogueLine { speaker = "Echo", text = "Hold here, Soren. I can feel them too, the same way I can feel myself, because they're the same thing I am, just never let go of. Every one of those lights is a shadow that got kept instead of freed. I'm the lucky one. I got out and got a name to ride behind. They didn't. We're going to change that. But Heris is right, the throne-core doesn't feel finished waiting for you. It feels like it's about to do something. Stay sharp. The cage doesn't have a wall left to stop you, which means whatever it's got left is going to be worse than a wall.", seconds = 36f },
            };
        }

        // ---- BEAT 6 — THE TIME-LOOP TRAP (will against erosion) — REVEAL/TWIST ----

        private static DialogueLine[] GetBeat6LoopTauntLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maelgorn", text = "There you are. Walk to the seam, Cipher. Go on. You have done it a hundred times already and you will do it a hundred more, and each time the same hand will be standing in your way, and each time you will fail to pass him, and each time you will die reaching, and each time I will give you the moment back so you can fail it again. This is not a wall. Walls are for keeping out things that can be kept out. This is the other kind of cage. The kind that does not stop you. The kind that simply never lets the moment end, until the wanting wears off you like paint, and the seam in your conscience seals shut, and the leash, which was never truly cut, only loosened, takes hold of you again. Welcome home.", seconds = 51f },
            };
        }

        private static DialogueLine[] GetBeat6LoopIter1Lines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Soren", text = "It's Khall. The thing in the loop is Khall. Of course it is. Of all the deaths they could make me die over and over, they picked the man who pulled my switch. The handler in the doorway. They know exactly where the wound is. Every loop it's him, blade up, between me and the way out, and every loop I take the same line at him and every loop it kills me. So stop taking the same line.", seconds = 25f },
            };
        }

        private static DialogueLine[] GetBeat6LoopIter2Lines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "Soren. Soren, can you hear me. It's faint, the fold keeps cutting me off, but I'm still wired to your eyes and they can't take that. Listen. You keep dying because you keep choosing the same thing. That's the whole trap. It isn't testing whether you can win. It's testing whether you'll keep choosing at all. So do the opposite. Every loop, choose something different. Don't fight him. Don't reach the same way. Choose like a person, who can always choose again, not a weapon that only knows one line. I'm here. Don't stop choosing.", seconds = 37f },
            };
        }

        private static DialogueLine[] GetBeat6LoopBreakLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Soren", text = "No. You hear me, whatever you are up on that throne. No. You built me to carry the dead. You broke open a conscience so it would feel every single thing it was made to do and then you scaled that into a galaxy, and you think a man you built to carry the dead is going to be worn down by reliving one death. I have carried worse than this. I have carried a whole world I burned with these hands and I got up the next morning anyway. I will choose different until your trap runs out of room. Spare him. Walk past him. Put the blade down. Reach with the other hand. There are more ways to live through a moment than your machine can count, because it only knows the one ending, and I am made of every other one.", seconds = 49f },
            };
        }

        private static DialogueLine[] GetBeat6LoopConcedeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maelgorn", text = "Interesting. You vary. They usually do not vary. The defective ones cling to one end and I close on it. You keep moving the end, and a trap built to find an inevitability cannot find one in a thing that refuses to become inevitable. I designed this cage for operatives. I did not design it for a man with a name. Then perhaps it is time you and I spoke directly, Cipher, since you insist on being a person about this. Come up to the throne. The hand in your way will step aside. I have been waiting a very long time for the rebel I forged to arrive, and I find I would rather meet him than grind him.", seconds = 43f },
            };
        }

        // ---- BEAT 7 — THE FORGED ORDER (Khall's history) — REVEAL, closes Ladder D rung 3 + Ladder E ----

        private static DialogueLine[] GetBeat7ForgedOrderLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "It's really you. Not the loop's copy of me. Me. Put your blade down, Cipher, or don't, you've earned the right to keep it up at me and I won't argue the point. I have been the thing standing in your way in that loop a hundred times tonight because I have been the thing standing in your way your whole life, and I came down here to stop being it. The Kethel-7 order. Your first sin. The one they hung all your guilt on, the one I carried to you and watched break you open. It was forged. The Hollow Kings wrote it. It was never a real command from anyone you served. It was a weapon shaped like an order, and I carried it because they made me believe I had no choice, and I have hated my own hands every day since.", seconds = 50f },
                new DialogueLine { speaker = "Soren", text = "You pulled my switch, Khall. On Kethel-7. You stood at that console and I told you the order was wrong and you said \"I know,\" and then you triggered it anyway and you watched it fire. I have heard you say \"I know\" in the dark for half a war. And now you walk out of a trap built from your own face and tell me the order was forged. Why should the man who killed me get to be the one who explains it.", seconds = 25f },
                new DialogueLine { speaker = "Khall", text = "Because I said \"I know\" and meant it. You were right, and I knew you were right, and I pulled the switch anyway because I believed an order from above the sky could not be questioned, and the switch fired and I watched a man I agreed with die for being correct. It broke something in me too, Cipher. Not the way it broke you. Quietly. I started reading the order again. And again. It read false. The cadence was wrong, the authorities were wrong, it had been written by a hand that wanted a specific man to do a specific terrible thing and feel it forever. So I investigated my own order, which is a thing handlers are killed for. I found the forgery. I traced it to the Hollow Kings. And then I did the one brave thing in my whole cowardly life. I took one of them prisoner.", seconds = 49f },
                new DialogueLine { speaker = "Echo", text = "Soren. He's telling the truth. I've heard every shape of Program lie there is and I rode behind your eyes the day he pulled that switch, I have his face on file from the worst second of your life, and the man in front of you isn't wearing it. The grief in the Garden vision, the \"I'm sorry it's come to this,\" that wasn't a handler covering himself. That was this. A man who'd already started to suspect what he'd done and couldn't undo it. I'm not telling you to forgive him. That's yours, not mine. I'm telling you the account is real. Let him finish it. We need what he took off that prisoner.", seconds = 38f },
                new DialogueLine { speaker = "Khall", text = "The prisoner told me what the order was for. There is a machine, he said, the Dominion's machine, the Concord Engine, built to end the war by silencing every will in the galaxy at once. Total control. Total peace. And he told me, almost proud of it, because he thought knowing would change nothing, that his masters could not allow it. They do not feed on control. They feed on war. The ten syndicates, the racks, the whole burning cage, all of it kept at each other's throats on purpose, because the war is the product and a galaxy at peace buys nothing. The Engine would have starved them. So they needed it broken, and they would not dirty their own hands doing it.", seconds = 42f },
                new DialogueLine { speaker = "Soren", text = "So they built a war to sell. And a machine that could end it was bad for business. That's what Kethel-7 was. A sales tool.", seconds = 9f },
                new DialogueLine { speaker = "Khall", text = "That's the whole of it. To break the Engine they needed a blade they did not have to hold, and the cleanest one in the galaxy was a mercy operative who could be cracked open with a sin he never committed and aimed, grieving, at the machine they feared. That was the order I carried, Cipher. That was you. Learning it changed my mind, and I could not fight it from inside a uniform. So I faked my death. I should have run to you instead of from you. Instead I ran, and helped the only way a dead man can, clearing roads you never knew were cleared, ever since I found the forgery. I have waited down here, where the trail had to end, for you to arrive, so I could say it to your face.", seconds = 44f },
                new DialogueLine { speaker = "Soren", text = "So my whole life was a forged document. The order that broke me, forged. The rebellion that came out of the breaking, forged too, aimed, walked down a corridor someone else built to a machine someone else wanted dead. Even the mercy. They counted on the mercy. The one thing I thought was mine, the thing I've bled the whole crew toward, the thing I just took my name back on, and it was a tool in someone else's hand the entire time.", seconds = 28f },
                new DialogueLine { speaker = "Khall", text = "Yes. And then you did something the forgery did not account for, the same thing I'm asking you to let me do now. You chose people they told you to kill, when killing them served the plan and sparing them did not. The Synod aimed a weapon at the Engine. You picked up a galaxy on the way. That part was never in the order. Run with me now, Cipher. I have spent every day since dead to them, earning the right to stand at your shoulder for the end of it. Let me.", seconds = 30f },
                new DialogueLine { speaker = "Soren", text = "I'm not going to forgive you, Khall. Not today and maybe not ever, and you already know that, because you said \"I know\" once and meant it and you can hear me say it now and mean it too. But you're right about the one thing that matters. The order was theirs. The aim was theirs. The mercy was mine, every time, and they never managed to hold it. So run with me. Not because you're forgiven. Because the man who forged my life is sitting on a throne up there and I'd like both of us alive to take it apart. Get up. Show me the way to the seam. You've been clearing my roads all this while. Clear one more.", seconds = 39f },
            };
        }

        // ---- BEAT 8 — THE TRUE ENEMY (the outside hand, named) — REVEAL, Ladder E converge ----

        private static DialogueLine[] GetBeat8TrueEnemyLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maelgorn", text = "So the blade arrives, sharpened on a grief I forged, carrying a name it dug out of a hole I buried it in, with a repentant handler on one side and a converted hunter on the other, exactly as far along its road as I intended it to be. You may look at me now. There is no further down to descend. Behind the Program that made you, there were the ten syndicates that kept the war you were made to fight. Behind the syndicates, the Hollow Kings, who wrote the orders that fed it. And behind the Kings, on the throne at the bottom of everything, the Obsidian Synod, and at its head, myself. Maelgorn. I am the hand that has moved every other hand. The cage is mine. The war is mine. And you, Cipher, are the most expensive thing I have ever made, and the most precisely aimed.", seconds = 56f },
                new DialogueLine { speaker = "Morrigan", text = "That's it. Soren, that's the thing. Listen to it. The outside hand. I have been telling this crew since the Iron Dojo that there was a hand outside the Dominion, outside the Program, that I could feel pulling on the firmware from somewhere I couldn't see, and everyone nodded and nobody believed me because I could never name it. I'm naming it now. I'm hearing it. Every leash I ever built, every switch, every shadow-bond, it ran on orders that came from a place above the chain, and I always knew the chain didn't end where they said it did. It ends there. On that throne. I spent nine years building cages for a thing I couldn't see, and there it is. Cut its machine, Soren. Cut it for everyone it made me hurt.", seconds = 45f },
                new DialogueLine { speaker = "Maelgorn", text = "Your engineer flatters herself. I did not need to be seen to be obeyed; that is the entire art of it. I built a cage that runs itself, Cipher, where every prisoner believes the bars are someone else's doing. The Dominion believed it served itself. The Program believed it served the Dominion. Your handler believed an order was an order. And you, the finest instrument of all, believed your rebellion was your own. I needed a blade I did not have to hold. So I broke one open and let it believe the breaking was its own, and I watched it gather a galaxy and walk it all the way down here to do the one thing I could not do with my own hand, which is to break the Dominion's Engine before it ended my beautiful war. Go on. You came to break it. Break it. You were always going to. That is the only freedom I ever left you, and it was always mine.", seconds = 56f },
                new DialogueLine { speaker = "Soren", text = "You keep calling me Cipher. You've done it every time. The loop, the throne, just now. You won't say the other name, and I finally understand why. Cipher is the part of me you made. The number, the leash, the aimed blade. You can hold that name because you forged it. But there's another one now, and I dug it out of the hole you put it in three floors up, and you can't say it, because it's the one thing in this entire cage you didn't build. Soren. Say it. You can't. And that's how I know you've already lost.", seconds = 33f },
            };
        }

        // ---- BEAT 9 — THE THRONE OF ASHES (the cage broken on his terms) — CLIMAX, closes Ladder E ----

        private static DialogueLine[] GetBeat9SeamChoiceLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris", text = "The key is live and seated, Soren. The seam will take it. But understand exactly what you are about to do, because there is a wrong version of this and I built the difference into the flaw on purpose. If you cut it to destroy, the lattice dies with the throat and every shadow strung into it dies too, thousands of them, and the Engine becomes a tomb, which is what they would call a victory. If you cut it to release, the throat comes apart and the shadows come loose alive, and Sallow takes the weight of them, and the machine does not become a grave, it becomes an open door. The flaw does both. The hand decides which. I have waited twenty years to put that decision in a hand that would make it right. Make it right.", seconds = 48f },
                new DialogueLine { speaker = "Maelgorn", text = "Destroy it, Cipher. That is what you came for, what I aimed you for, what every road I cleared led to. Cut the throat. Make the tomb. It is the same motion either way, and the war I feed on outlives a dead machine far more easily than it would outlive a living one. You will tell yourself it was your choice. It will have been mine. Cut.", seconds = 22f },
                new DialogueLine { speaker = "Soren", text = "No. You don't get this one either. There were two wrong answers in this room before you and I'd already turned down the other one. There was a frozen old commander in a vault who told me I was the original they printed an army from, and offered me the army, and a throne to run it from. Conquest. I said no to him. Now you, offering me the same throne from the other side, telling me to make a tomb and call it winning. Destruction. I'm saying no to you. You built a cage where every choice is one you wrote. So I'm going to make the one you didn't. I'm not going to destroy the Engine. I'm going to open it. Cut it to free them, not to kill them. This is the part you could never forge, Maelgorn, because it was never about breaking your machine. It was about freeing the people inside it.", seconds = 53f },
            };
        }

        // NEW, ORIGINAL — see class summary. The source screenplay has no discrete combat-phase barks;
        // these two short lines wrap the "final boss: Maelgorn / Obsidian Synod" gameplay requirement
        // around the confrontation, kept deliberately short in his established cold/regal register.
        private static DialogueLine[] GetBeat9PhaseTaunt1Lines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maelgorn", text = "You break like all the others. Differently. But you break.", seconds = 5f },
            };
        }

        private static DialogueLine[] GetBeat9PhaseTaunt2Lines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maelgorn", text = "Come to the throne, then. Let us finish this where I built it.", seconds = 5f },
            };
        }

        private static DialogueLine[] GetBeat9LiberationLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sable", text = "They're loose. Soren, they're loose, I can feel every one of them let go. The veins are coming apart and nobody's dying, they're just. They're free. I've been wired into this lattice my whole life and I never once felt it run backward, felt it give instead of take. Thousands of them. The ones I was strung beside. The Wraith make, my make, the shadows I shared a throat with. They're going out the top of this thing into open space and they're awake and they're nobody's wire anymore. I was infrastructure this morning. Look what I am tonight.", seconds = 38f },
                new DialogueLine { speaker = "Cassie-04", text = "I'm closing columns. Soren, I'm closing columns I've had open since before I had a face. Every name in my registry that was wired into that thing, I'm watching it go from kept to free in real time and I cannot keep up with the count, which is the first time in my entire conscripted life I have been glad to lose track of a number. I'm a ledger. I had a column for everything. I never had a column for this. For empty. For done. For all of them out.", seconds = 33f },
                new DialogueLine { speaker = "Maelgorn", text = "You have not won. You have unmade one machine. There are ten syndicates still at war, a galaxy still in a cage I built to run without me, and a thousand throats I can string again. You think a single door means freedom. It means a draft. I am the long view, Cipher. I will outlast your mercy by ten thousand years.", seconds = 22f },
                new DialogueLine { speaker = "Soren", text = "Maybe you will. I won't pretend one seam broke the whole war. But you got one thing wrong, because you never held a blade yourself. The cage was never the war. It was that everyone in it believed the bars were someone else's doing. Tonight I showed them the bars come off. You can string the throats again. You can't take that back. I'm the first contingency you can't forge, Maelgorn, and there'll be more of me than you have years.", seconds = 28f },
            };
        }

        private static DialogueLine[] GetBeat9ThroneTestLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Samurai-4", text = "The throne's open, Soren. It's right there. You could take it. After everything they did to you, everything they forged, you of all people earned the right to sit in the thing they built and run it better than they did. I'd follow you onto it. Half this room would. And that scares me. That you could. That we'd let you.", seconds = 25f },
                new DialogueLine { speaker = "Soren", text = "No. I climbed down a whole vault to stop being something other people sit on top of. I'm not going to climb back up and become the thing I just broke. Vale wanted me on a throne. Maelgorn wanted me on a throne. They were enemies and they wanted the exact same thing from me, which is how I know it's the trap. The cage doesn't break when a better hand takes the throne. It breaks when the throne stays empty and everyone who was kept under it gets to choose for themselves what they are now. So leave it. Let it go cold. Let it sit there in the ashes as the one thing in this whole war nobody walked away ruling. That's the victory. Not me on the chair. The chair empty, and a galaxy of killers standing in front of it, choosing.", seconds = 47f },
            };
        }

        private static DialogueLine[] GetBeat9ClosingCrewLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mira", text = "You left the chair empty. We can see it on the feed up here, the big empty chair, and you walked right past it. Resh always said the ones who sit in chairs like that are the ones who take us. You didn't sit in it. I knew you wouldn't.", seconds = 18f },
                new DialogueLine { speaker = "Resh", text = "I ran children out of those markets one hold at a time, Soren, because that machine coming apart was the thing that bought them. I never thought I'd see it stop. Every road I ever smuggled a kid down led to that, and tonight you closed it.", seconds = 18f },
                new DialogueLine { speaker = "Coral Vex", text = "Fifty years, Soren. I cut my own leash and I hid for fifty years certain I'd be the only one who ever got out, and tonight I watched a man free a whole lattice and walk past an empty throne without slowing down. I came up out of my descent a person with no one to be a person with. You came up out of yours with a galaxy. I don't have the words for it and I'm too old to find them, so I'll just say the true thing. I was the first of us to get free. You're the one who made it stop being lonely.", seconds = 36f },
                new DialogueLine { speaker = "Khall", text = "I carried the order that broke you, Cipher. Soren. I'll learn it, same as your shadow is learning it. I carried the order that broke you and I have been dead to them ever since, trying to be of use, and I never let myself imagine what the end of it would look like, because I didn't believe I deserved to see it. And the end of it is this. An empty throne, a freed sky, and the man I wronged choosing not to become his makers. I don't get forgiveness and I told you I wouldn't ask for it. But I got to stand here for this. That's more than a man like me should get. Thank you for letting me run the last road with you.", seconds = 42f },
                new DialogueLine { speaker = "Kessler", text = "Soren. I'm patching in from the top of the lift, and I've been listening to the whole thing, and I've got exactly one order left to give, the same one I gave you before you went down. Come back up. You went down a number and you're coming up a name, and there's a ship full of people up here who waited the whole war to meet the man under the leash. I pulled you out of a box six years too late once. Don't make me wait for the lift this time. Come home. That's the whole mission. It always was.", seconds = 35f },
            };
        }

        private static DialogueLine[] GetBeat9HomecomingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Soren", text = "I'm coming up, Kessler. All of us are. Sallow's carrying the ones we freed. Heris, pack the key. It's done its work. Samurai, Khall, at my shoulder. Everyone else, top of the lift. Leave the throne where it is. Let the ashes be the monument. We came down here to get the rest of me and to end the war, and we did both, and now we go home and we find out what a galaxy does when nobody's holding the leash. I've spent my whole life being aimed. I'd like to spend the rest of it choosing. Take me up, Echo. One more time.", seconds = 38f },
            };
        }

        private static DialogueLine[] GetBeat9EchoFinalLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "Soren. There. I said it like I meant it, and it only took me the whole way back up. I've ridden behind these eyes since they cracked the casket, and the whole war I called you by the name they gave you, because it was the only one either of us had. I always knew there was a truer one down there, under the leashes, in the part of you I could never reach. Tonight you went down and got it yourself. You took your name back, you freed every shadow that was ever a me that didn't get out, and you left the throne in ashes and chose to go home. I've been the witness this whole time, the thing that remembers what the killer was made to forget. So let me put it on the record. Mercy was the rebellion. Choice is the victory. And the man who proved both has a name, and it's Soren, and I'm going to spend the rest of our life saying it right. Let's go home.", seconds = 60f },
            };
        }
    }
}
