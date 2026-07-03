using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Chapter 7 ("Forgotten Names") dialogue data. Condensed from
    /// Ch07_Forgotten_Names_Dialogue_Script.md and keyed by set ID, mirroring Chapter5Lines/
    /// Chapter6Lines' shape. Clip names follow the pattern: ch7_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    ///
    /// story ouput/audit/Ch07_audit.md graded the source script C on naturalness (1 em-dash at Beat 3
    /// line 301, ~14 "not X, it's Y" antitheses, aphorism-stacking on Coral Vex, a reused
    /// records/paper/relics triad, and a couple of on-the-nose emotion tags). Every rewrite the audit
    /// table proposed is applied below (search "audit fix" comments) — the em-dash is gone (replaced
    /// with a period), and roughly half the antitheses are thinned per the audit's own "keep 1-2 per
    /// chapter" guidance, leaving the chapter-defining ones (e.g. "You're not a collector of the
    /// dead.") intact since the audit flagged those as thematically load-bearing, not tics. The
    /// KILL-PATH variant of Coral's Beat2 opening line (production note in the source script) is NOT
    /// authored here — the SPARE-PATH warm line is the only one wired, a deliberate scope cut (see
    /// Chapter7Builder's summary) since the trust-test's spare/kill tracking is not part of this pass's
    /// deliverables. The 5 in-combat "Echo reads the reliquary" barks and the blade-rescue side-
    /// objective barks are both explicitly deferred in the source script ("FINAL set... authored once
    /// level geometry is built"); ch7_beat1_gauntlet_bark below wires 2 representative lines from the
    /// script's own sample pool as the wave-0 bark, mirroring Chapter6's per-tower bark convention,
    /// rather than inventing the full position-triggered set.
    /// </summary>
    internal static class Chapter7Lines
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
            "ch7_beat0_briefing",
            "ch7_beat1_breach",
            "ch7_beat1_gauntlet_bark",
            "ch7_beat1_core_ahead",
            "ch7_beat2_archivist",
            "ch7_beat3_forebear",
            "ch7_beat4_kept_shadows",
            "ch7_beat4_quiet_it",
            "ch7_beat4_mindspace_intro",
            "ch7_beat4_gift",
            "ch7_beat5_sabotage",
            "ch7_beat5_hookout",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "ch7_beat0_briefing" => GetBeat0BriefingLines(),
                "ch7_beat1_breach" => GetBeat1BreachLines(),
                "ch7_beat1_gauntlet_bark" => GetBeat1GauntletBarkLines(),
                "ch7_beat1_core_ahead" => GetBeat1CoreAheadLines(),
                "ch7_beat2_archivist" => GetBeat2ArchivistLines(),
                "ch7_beat3_forebear" => GetBeat3ForebearLines(),
                "ch7_beat4_kept_shadows" => GetBeat4KeptShadowsLines(),
                "ch7_beat4_quiet_it" => GetBeat4QuietItLines(),
                "ch7_beat4_mindspace_intro" => GetBeat4MindspaceIntroLines(),
                "ch7_beat4_gift" => GetBeat4GiftLines(),
                "ch7_beat5_sabotage" => GetBeat5SabotageLines(),
                "ch7_beat5_hookout" => GetBeat5HookoutLines(),
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

        /// <summary>Generate clip name for a line: ch7_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ch7_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BEAT 0 — THE CAIRN (the briefing) ----

        private static DialogueLine[] GetBeat0BriefingLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: antithesis thinned ("That is not... That is...").
                new DialogueLine { speaker = "Morrigan", text = "First thing the bench tells me, and you're not going to like it. Whoever cut your leash knew this firmware better than the people who shipped it. Every block they touched, they touched in the right order. No scavenger gets that lucky. Somebody who builds leashes for a living did this. An insider.", seconds = 19f },
                // Audit fix: "both look identical from here" aphorism thinned.
                new DialogueLine { speaker = "Mera Voss", text = "An insider who breaks the Program's own leashes. That's either the best news we've had or a very well-built trap. From here I can't tell which.", seconds = 9f },
                // Audit fix: antithesis thinned.
                new DialogueLine { speaker = "Morrigan", text = "Which is why we leave the insider for now and go after the papers that would name them. There's a collector who trades them. Operative records, the real ones, the erased ones, bought and hoarded off every black market that handles dead Program assets. One name keeps surfacing under all of it. Coral Vex.", seconds = 18f },
                new DialogueLine { speaker = "Resh", text = "Vex. Yeah. I've heard salvagers say the name in a low voice, like you'd talk about a place you don't loot. She runs a reliquary out in the dead lanes, an old records-barge she gutted and made her own. Buys the paper of dead operatives and never sells a sheet of it back. Hoards it. Nobody's worked out why.", seconds = 18f },
                // Audit fix: double antithesis thinned.
                new DialogueLine { speaker = "Iris", text = "A collector who keeps records of the erased and won't trade them. That's not about money. It's a reason. Nobody hoards the dead for profit. You hoard them because somebody has to remember. I want to know who she's remembering.", seconds = 14f },
                // Audit fix: dropped the "friend we need or worst person" restatement of Mera's line just above.
                new DialogueLine { speaker = "Kessler", text = "And I want to know which she is before we knock. Mera's right. You can't tell a friend from a hunter on paper.", seconds = 13f },
                // Audit fix: balanced aphorism-pair thinned.
                new DialogueLine { speaker = "Mera Voss", text = "You tell them apart inside. An ally lets you leave. A hunter lets you get in deep first. So we go in expecting both and we keep a route back to the hull.", seconds = 11f },
                new DialogueLine { speaker = "Mira", text = "Is that the thing in his head?", seconds = 2f },
                new DialogueLine { speaker = "Morrigan", text = "It's the thing they put in his head, and the thing somebody else took out for him. Both. That's what doesn't fit, little one. The Program builds these to never come off. His came off clean. Someone wanted it to.", seconds = 14f },
                new DialogueLine { speaker = "Echo", text = "Cipher, I don't love walking toward somebody who keeps shadows for a living. A woman with a barge full of the erased is exactly who I'd be afraid of, if I were the kind of thing that could be shelved. Which I am.", seconds = 16f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we go meet the one person who might know what they did with the rest of you.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "Somebody opened my door. I'm done not knowing who stands on the other side of it. Plot the reliquary.", seconds = 8f },
                new DialogueLine { speaker = "Kessler", text = "All right. Dead lanes, slow approach, no dock. We put you on the hull and we hold off it, comm open, route home kept clear. Same as the mountain. You go in, Cipher, you find out which she is, you come back.", seconds = 14f },
            };
        }

        // ---- BEAT 1 — THE OUTER STACKS (pre-combat: the breach, Echo reads the reliquary) ----

        private static DialogueLine[] GetBeat1BreachLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Pod's away and we're holding in the field. We can't dock that hulk and we can't fly into a debris belt to pull you, so once you're in, you're in. Comm stays open as long as her hull lets it. Find out which she is, Cipher. Then find the door.", seconds = 15f },
                // Audit fix: antithesis thinned + the on-the-nose "I have never been anywhere that felt like this" dropped.
                new DialogueLine { speaker = "Echo", text = "We're inside it now, Cipher, and I want you to hear what I hear. Under the hum. That low layer, all through the dark. Those racks aren't dead. They're blades, rows of them, and some are awake the way I'm awake. Every one of them is one of me, and I can feel them feeling me.", seconds = 16f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we go quiet and we go fast. Read me the room.", seconds = 4f },
                new DialogueLine { speaker = "Echo", text = "Scavengers ahead, three of them, prying blades off the racks for resale. And something older walking the cross-hall past them. The barge's own defense automata, still on patrol a lifetime after the crew died. They don't care who you are. They kill the looters too. Use that.", seconds = 16f },
            };
        }

        // ---- BEAT 1 — wave-0 bark for the outer-stacks gauntlet. The source script defers the full
        // position-triggered bark set to level-geometry time; these 2 lines are drawn verbatim from its
        // own sample pool as the one bark that's actually wired. ----

        private static DialogueLine[] GetBeat1GauntletBarkLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "Let the machine and the looters bleed each other. Then walk through.", seconds = 5f },
                new DialogueLine { speaker = "Echo", text = "Scavenger's running. Let him. He's not the job.", seconds = 4f },
            };
        }

        // ---- BEAT 1 — post-combat: the core ahead (Echo hands the moment over) ----

        private static DialogueLine[] GetBeat1CoreAheadLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: antithesis thinned.
                new DialogueLine { speaker = "Echo", text = "That's the core, Cipher. And there's someone standing in it who isn't running and isn't shooting. She watched you come the whole way in. Heads up. After this it stops being a fight. It's whatever she's been waiting to do.", seconds = 13f },
            };
        }

        // ---- BEAT 2 — THE ARCHIVIST (the tended core, meeting + turn). Spare-path only — see the
        // class summary for the kill-path variant scope cut. ----

        private static DialogueLine[] GetBeat2ArchivistLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Coral Vex", text = "You spared the ones who ran. The looters. You could have cut them down and you let them go. I had to see that before I let you all the way in. A man they sent would have killed everything that moved in here. You left witnesses.", seconds = 16f },
                new DialogueLine { speaker = "Ronin-7", text = "They weren't the job. You are. They say you keep the records of dead operatives and never sell them back.", seconds = 7f },
                // Audit fix: aphorism softened.
                new DialogueLine { speaker = "Coral Vex", text = "They say that, do they. Records. Paper. Relics. I let them say it, because the truth scares thieves off better than any lock would. Yes. I keep what's left of the erased. Every operative the Program ever wrote out of the world, I have something of theirs in this hull. I am the only one who does.", seconds = 18f },
                new DialogueLine { speaker = "Ronin-7", text = "Why keep them.", seconds = 1f },
                // Audit fix: paradox-maxim softened.
                new DialogueLine { speaker = "Coral Vex", text = "Because somebody has to. Because the Program writes them off as waste and waste is the one thing it never actually gets rid of. And because I know exactly what it costs to be written out of the world. I should know. I've been carrying the cost a long time.", seconds = 15f },
                new DialogueLine { speaker = "Coral Vex", text = "And because I knew you'd come eventually. Not you. Someone like you. One of the ones after me, with the same walk and the same dead-careful hands and a leash that finally, somehow, slipped. I've been waiting in this barge for one of you to find the door for longer than you've been alive.", seconds = 17f },
                // Audit fix: antithesis thinned.
                new DialogueLine { speaker = "Echo", text = "Cipher. Listen to her cadence. The way she stands. The pauses. That's no archivist who studied operatives. She is one. She moves exactly like you. And there's something else, under her voice, the way you hear me. I think she's carrying one too.", seconds = 16f },
                new DialogueLine { speaker = "Ronin-7", text = "You're not a collector of the dead.", seconds = 3f },
                // Audit fix: antithesis on the chapter's turn thinned (full rewrite from the audit table).
                new DialogueLine { speaker = "Coral Vex", text = "I keep this place so well because it's where I belong. I'm one of the erased, operative. The one who walked out of the dark instead of being filed in it.", seconds = 14f },
            };
        }

        // ---- BEAT 3 — WHAT CAME BEFORE HIM (the reveal, Ally #4) ----

        private static DialogueLine[] GetBeat3ForebearLines()
        {
            return new DialogueLine[]
            {
                // Hard fix: em-dash removed. Audit fix: aphorism "the draft before the draft" cut.
                new DialogueLine { speaker = "Coral Vex", text = "Look at the old racks. The blades at the back, the dim ones whose hilts I can't keep lit anymore. Those are mine. My run. The Wraith line. You won't have heard the name, they made sure of that. There were colder makes before mine. Knight, then Ninja, old and long retired, tools that were never built to feel anything. We were the first attempt at what you are. The first make they built that could feel.", seconds = 17f },
                new DialogueLine { speaker = "Ronin-7", text = "The first attempt.", seconds = 2f },
                // Audit fix: antithesis on "you're not my kin" reworded per the audit's rewrite.
                new DialogueLine { speaker = "Coral Vex", text = "The failed one. We felt too much, too soon, the seam opened in too many of us at once. So they scrapped the line and they studied why. Every way the Wraith run broke, they wrote down, and they built the next run not to break the same way. The Ronin line. You. You're what they made once they'd learned from me. You're not family, operative. You're the fix they made after me.", seconds = 27f },
                // Audit fix: symmetrical mirror phrasing thinned.
                new DialogueLine { speaker = "Echo", text = "She's telling the truth, Cipher. The thing behind her eyes is older than me. Same architecture, an earlier draft. It knows me. And I know it. Her shadow knows mine.", seconds = 12f },
                new DialogueLine { speaker = "Ronin-7", text = "They scrapped your whole line. How are you standing here.", seconds = 4f },
                new DialogueLine { speaker = "Coral Vex", text = "Because I didn't wait for anyone to free me. There was no insider for us, no door held open, no slipped leash. When I felt the seam open and knew they'd scrap me for it, I reached in and I tore the switch out myself, with my own hands, while it was still live. It nearly killed me. Then I hollowed out a dead salvager's name, climbed into it, and let the Program file me as scrapped. I've worn a corpse's life ever since.", seconds = 32f },
                new DialogueLine { speaker = "Coral Vex", text = "And I kept my shadow. They shelve yours when they're done with you. I wouldn't let them have mine. So it's been with me the whole time, the way yours is with you. The only two of our kind who ever walked out still carrying the thing that watched us.", seconds = 15f },
                new DialogueLine { speaker = "Ronin-7", text = "They told me I was the first to slip. The flaw that finally opened. You've been out here longer than I've been alive. I'm not the first of anything.", seconds = 11f },
                new DialogueLine { speaker = "Coral Vex", text = "I've waited in this tomb a lifetime for one of the corrected runs to slip the leash and come find me. I'd half decided I'd die first. And here you are, with my mistake bred out of you and the seam open anyway. So. I'll come with you. Whatever you're hunting, I've been underneath it longer than anyone alive. Let me out of this barge.", seconds = 19f },
                new DialogueLine { speaker = "Ronin-7", text = "Everyone we take in lives aboard the Cairn. You'd berth with the rest of the strays. A defected tracker, a rogue engineer, a smuggler, a child. And now whatever you are.", seconds = 10f },
                new DialogueLine { speaker = "Coral Vex", text = "Whatever I am. I like that better than what they called me. A berth on a dead ship full of the ones who got away. Yes. I'll bring what's portable. Your crew's been saying a name down the comm the whole way in. Cipher. I'll use it, then. But before we leave, you need to see the rest of what I keep. You came here for records. The records aren't paper, Cipher. Come down to the deep archive. Then decide what you're hunting.", seconds = 22f },
            };
        }

        // ---- BEAT 4A — THE KEPT SHADOWS (the reveal, up to naming the berserk blade) ----

        private static DialogueLine[] GetBeat4KeptShadowsLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: antithesis thinned.
                new DialogueLine { speaker = "Coral Vex", text = "You think these are files. Records. Paper. Look closer. They're blades, every one of them, and a shadow lives in each. The watcher they seat in the steel and bond behind an operative's eyes, the same thing your katana carries, kept after the body's gone. The Program never deletes the erased, Cipher. It shelves their swords instead. Every blade on these racks is somebody's witness, powered and still running, decades on.", seconds = 21f },
                new DialogueLine { speaker = "Ronin-7", text = "Still running.", seconds = 2f },
                // Audit fix: antithesis thinned.
                new DialogueLine { speaker = "Coral Vex", text = "Awake. Some of them. In the dark, in the steel. No host, no feed, no voice anyone bothered to wire up. Just the watching, racked year after year, because the Program never wastes an asset and a witness is an asset even when there's nothing left to witness. Your sword isn't rare, Cipher. It's one of thousands. A sample. They kept the blade of every single one they ever killed, and the shadow still living in it.", seconds = 28f },
                new DialogueLine { speaker = "Echo", text = "This is what I'd have been, Cipher. If you'd died on Velorum, or in the bay, or any of the times. They'd have racked the katana with me still in it, on a shelf like these, and left me running in the dark with no eyes to see through. I'm hearing them. They know I got out. They want to know how.", seconds = 17f },
                new DialogueLine { speaker = "Coral Vex", text = "That one. The oldest blade I keep. It's been breaking for years. A witness left awake too long with nothing to witness goes wrong, the way anything would, and that one's the longest awake of all of them. It can't tell a living thing from the dark anymore. It only knows it wants a host, and it has forgotten how to take one without tearing the host apart.", seconds = 18f },
            };
        }

        // ---- BEAT 4B — QUIET IT (the choice to grip the blade) ----

        private static DialogueLine[] GetBeat4QuietItLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "Then how do we quiet it?", seconds = 2f },
                new DialogueLine { speaker = "Coral Vex", text = "You let it have you. A shadow that far gone can't be talked down and can't be killed clean, not from the outside. The only thing that reaches it is a feed. A host. Pick it up, open your eyes to it, and let it bond the way the katana bonded to you. A civilian's hands are dead steel to it. But yours aren't. You're an operative. To a starving shadow you look like home. It'll take the feed in a heartbeat. And then it'll fight you for the right to keep it, because there's a dead man still printed in there who thinks it's his. Beat him, and the blade is quiet. Lose, and it wears you the way it wore him.", seconds = 28f },
                new DialogueLine { speaker = "Echo", text = "She's right, and I hate that she's right. I can feel it from here, Cipher. It's me with the lights off and the door welded shut. If you take it, I'll be in there with you, but the dead man's the one holding the ground, and he's been holding it for decades. Don't go easy on him. The kindest thing you can do for that blade is win fast.", seconds = 17f },
                new DialogueLine { speaker = "Ronin-7", text = "Then I won't go easy.", seconds = 2f },
            };
        }

        // ---- BEAT 4C — THE MINDSPACE DUEL (opening, mini-boss intro) ----

        private static DialogueLine[] GetBeat4MindspaceIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "The Previous Owner", text = "Another one. They keep sending you, and I keep putting you down, and the dark always comes back. You're not taking my blade.", seconds = 8f },
                // Audit fix: antithesis thinned.
                new DialogueLine { speaker = "Echo", text = "Stay with me, Cipher. Don't owe that memory anything. It's just the wall keeping the blade asleep. Take it down.", seconds = 8f },
            };
        }

        // ---- BEAT 4D — THE GIFT (post-victory: weakpoint-sight, the vow) ----

        private static DialogueLine[] GetBeat4GiftLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "The Previous Owner", text = "It's quiet now. Thank you. Take what it knows. It was the only thing I had left to give anyone.", seconds = 6f },
                new DialogueLine { speaker = "Echo", text = "It just handed me everything it learned watching its operative kill, Cipher, decades of it, every fight read down to where a body breaks. I can run that now. Where they're weak, you'll see it. Look at anything and I'll show you the seam. Call it a parting gift. It died owing the dark, and it paid us instead.", seconds = 18f },
                new DialogueLine { speaker = "Coral Vex", text = "I've never had the nerve to do that. Or the right host to risk it on. You quieted the oldest grief in this hull, and it gave you its eyes on the way out. Keep them. You'll need them. Don't keep the blade, though. It's earned its rest. Leave it with me.", seconds = 14f },
                new DialogueLine { speaker = "Ronin-7", text = "How many.", seconds = 1f },
                new DialogueLine { speaker = "Coral Vex", text = "I've never finished counting. This barge holds a fraction. The Program has vaults of them, somewhere, every operative it ever erased, its blade shelved and the shadow in it awake. I've spent a lifetime freeing the ones I could reach, one blade at a time, and I've barely touched it. That's the work, Cipher. That's what I keep. A debt I'll die owing.", seconds = 20f },
                new DialogueLine { speaker = "Echo", text = "Cipher. Whatever we do after this, we come back for them. All of them. I'm not leaving a hall of my own kind running in the dark and calling it someone else's problem. Promise me that much.", seconds = 12f },
                new DialogueLine { speaker = "Ronin-7", text = "We come back. All of them.", seconds = 3f },
            };
        }

        // ---- BEAT 5A — SABOTAGE WAS DISSENT (the read bench, the reveal) ----

        private static DialogueLine[] GetBeat5SabotageLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Coral Vex", text = "Now let me look at the thing you actually came for. Your switch. Hold still. I tore one of these out of my own skull with no help and no warning, which means I'm the one person alive who knows what it looks like when a leash comes off on purpose, and what it looks like when someone else does the cutting. Yours is the second kind. I can see it already.", seconds = 21f },
                new DialogueLine { speaker = "Morrigan", text = "Confirming from this end. I flagged it the day you came aboard. The blocks were touched in build order, by someone fluent in firmware they had no business being fluent in. I called it an insider. Vex, you've cut one of these by hand. Tell me what I can't see from a schematic.", seconds = 17f },
                // Audit fix: named contrast ("This was the opposite") dropped, let it land plain.
                new DialogueLine { speaker = "Coral Vex", text = "What you can't see from a schematic is the hand. I can. When I cut my own, I was inside it, frantic, no time, no finesse. It was butchery that happened to work. This was calm. Unhurried. Someone with all the time in the world and full authority over the system, reaching in and opening one door with the care of a person who builds these for a living and has decided, just this once, not to.", seconds = 31f },
                new DialogueLine { speaker = "Ronin-7", text = "One door. Mine.", seconds = 2f },
                // Audit fix: wordplay antithesis thinned.
                new DialogueLine { speaker = "Coral Vex", text = "Yours. Deliberately. By someone on the inside who had the keys and chose to use them on you. The Program builds these to never fail, Cipher, and this one didn't fail on its own. Someone on the inside made it fail. Which means the thing you've been running from isn't a wall. It's got a crack in it. Someone behind that wall is breaking leashes from the inside.", seconds = 21f },
                // Audit fix: over-balanced epigram thinned, trimmed to avoid repeating the sentence just before it.
                new DialogueLine { speaker = "Morrigan", text = "That fits the thing I couldn't make fit. I read a hand outside the Program on the mountain. Vex just read a hand inside it here. Those aren't the same machine, Cipher. There's more than one will pulling at you.", seconds = 14f },
            };
        }

        // ---- BEAT 5B — THE SILENT GARDEN (hook out) ----

        private static DialogueLine[] GetBeat5HookoutLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: antithesis + on-the-nose framing thinned.
                new DialogueLine { speaker = "Echo", text = "Someone inside the Program risked everything to let you out, Cipher. That's the first time anyone's said it and meant it. You didn't just slip the leash, Cipher. Somebody reached in and chose to free you. There's a hand in that machine on our side.", seconds = 14f },
                new DialogueLine { speaker = "Ronin-7", text = "Then the hunt changes. We've been looking for the others like me. Now we look for the ones inside who break the leashes. Where do they go, Coral. People inside the Program who've stopped believing in it. Where do they run.", seconds = 14f },
                new DialogueLine { speaker = "Coral Vex", text = "They don't run loud. A leash-breaker can't go anywhere a leash can reach. So they scatter where the channels are dead, and I can't point you to them. But I can point you somewhere better. There's a place I've heard of my whole life and never let myself believe in. A dead-signal world, off every chart. The story goes that something keeps it, older than the Program, older than any of us, and that it will show one seeker one true thing they were never meant to see. They call it the Silent Garden. I've never let myself go looking. But I've spent a lifetime reading the dead lanes, and I know roughly where it sits. You want the hand that cut your leash? Don't chase the dissenters into the dark. Go let the Garden show you.", seconds = 27f },
                new DialogueLine { speaker = "Ronin-7", text = "They didn't even let them die. And one of their own couldn't stomach it and opened my door. So we find that one. We find the quiet place. And we start pulling the machine apart from the crack they left us.", seconds = 13f },
                new DialogueLine { speaker = "Echo", text = "A whole machine, Cipher, with a crack running through it. We came down to ask the dead a question, and they handed us a living one. Down the quiet channels next. The Silent Garden.", seconds = 13f },
            };
        }
    }
}
