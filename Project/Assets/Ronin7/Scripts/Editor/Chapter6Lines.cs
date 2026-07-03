using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Chapter 6 ("The Iron Dojo") dialogue data. Condensed from
    /// Ch06_The_Iron_Dojo_Dialogue_Script.md and keyed by set ID, mirroring Chapter5Lines' shape.
    /// Clip names follow the pattern: ch6_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    ///
    /// story ouput/audit/Ch06_audit.md graded the source script C- on naturalness (0 em-dashes, but
    /// ~13 "not X, it's Y" antitheses, tricolon/aphorism-stacking, and a uniform "everyone is a poet"
    /// register) and flagged one soft consistency item. Every antithesis the audit listed a rewrite for
    /// is applied verbatim below (search "audit fix" comments); the two lines marked "keep" in the audit
    /// (Kaelen's "the prayer that didn't take" and Ronin-7's "somebody soft as you gave it to me") are
    /// kept as written, per its own recommendation. The "hand on the strings" pet-phrase cluster (3
    /// speakers, same image, ~40 lines apart) is broken by using the audit's 3 rewrites, none of which
    /// share the image anymore. The soft consistency fix scopes Morrigan's "one insider" claim in
    /// ch6_beat5_morrigan_joins to "everything built in this rock" (the Ronin-line work this citadel
    /// does) rather than every Program make, per the audit's suggested fix.
    /// </summary>
    internal static class Chapter6Lines
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
            "ch6_beat0_briefing",
            "ch6_beat1_climb",
            "ch6_beat1_window",
            "ch6_beat2_morrigan_meet",
            "ch6_beat2_killlist",
            "ch6_beat3_hespa_intro",
            "ch6_beat3_caradoc_intro",
            "ch6_beat3_kaelen_intro",
            "ch6_beat4_confession",
            "ch6_beat5_evacuation",
            "ch6_beat5_descent",
            "ch6_beat5_morrigan_joins",
            "ch6_beat5_outro",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "ch6_beat0_briefing" => GetBeat0BriefingLines(),
                "ch6_beat1_climb" => GetBeat1ClimbLines(),
                "ch6_beat1_window" => GetBeat1WindowLines(),
                "ch6_beat2_morrigan_meet" => GetBeat2MorriganMeetLines(),
                "ch6_beat2_killlist" => GetBeat2KillListLines(),
                "ch6_beat3_hespa_intro" => GetBeat3HespaIntroLines(),
                "ch6_beat3_caradoc_intro" => GetBeat3CaradocIntroLines(),
                "ch6_beat3_kaelen_intro" => GetBeat3KaelenIntroLines(),
                "ch6_beat4_confession" => GetBeat4ConfessionLines(),
                "ch6_beat5_evacuation" => GetBeat5EvacuationLines(),
                "ch6_beat5_descent" => GetBeat5DescentLines(),
                "ch6_beat5_morrigan_joins" => GetBeat5MorriganJoinsLines(),
                "ch6_beat5_outro" => GetBeat5OutroLines(),
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

        /// <summary>Generate clip name for a line: ch6_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ch6_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BEAT 0 — THE CAIRN (the briefing) ----

        private static DialogueLine[] GetBeat0BriefingLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: antithesis thinned.
                new DialogueLine { speaker = "Resh", text = "Every kid I ever pulled out of those markets was tagged for one address. This one. They don't sell children here. They make them. Whole.", seconds = 14f },
                new DialogueLine { speaker = "Resh", text = "One card left to play. A Program engineer inside the walls, been slipping me children for years and never asked for anything back. A name and nothing else. Morrigan.", seconds = 13f },
                // Audit fix: tricolon flattened.
                new DialogueLine { speaker = "Mera Voss", text = "Then understand what the name buys you. This isn't a free-port, Resh. Real soldiers. Real air cover. Nowhere to disappear if it goes wrong.", seconds = 12f },
                // Audit fix: list/tricolon exposition dump thinned.
                new DialogueLine { speaker = "Iris", text = "That spine is where they do everything. The switches, the bonding, the conditioning. All of it, one mountain. Whatever they did to Cipher, they do it here, to a new batch every season. I want it read.", seconds = 15f },
                new DialogueLine { speaker = "Kessler", text = "I hear all of that, and I'm counting the bodies it costs. Tell me why we walk into the Dominion's own house instead of around it.", seconds = 11f },
                new DialogueLine { speaker = "Resh", text = "Because around it just means more of them grow up into the thing we keep fighting. You want to stop bleeding downstream. This is the top of it. There's nothing above this.", seconds = 12f },
                new DialogueLine { speaker = "Mira", text = "It looks like a school.", seconds = 2f },
                // Audit fix: on-the-nose tag dropped.
                new DialogueLine { speaker = "Kessler", text = "It's made to look like one. That's the trick of it. And it's why you're staying sealed aboard with the hatch shut and the comm open.", seconds = 11f },
                new DialogueLine { speaker = "Mira", text = "Okay. But the other children. Somebody's getting them out.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "Somebody is.", seconds = 1f },
                new DialogueLine { speaker = "Ronin-7", text = "We go shut down the place that's still turning out more like me.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "All right. We do it Resh's way. Plot the slope, drop him low and far, let the mountain be the door.", seconds = 10f },
            };
        }

        // ---- BEAT 1 — THE DROP & THE CLIMB (parkour traversal, Echo calls routes) ----

        private static DialogueLine[] GetBeat1ClimbLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Shuttle's clear and we're holding their air-net off you. Past the treeline you're on the rock and on your own feet. Climb careful.", seconds = 10f },
                // Audit fix: aphorism thinned.
                new DialogueLine { speaker = "Echo", text = "On your own feet, sure. Not alone, though. Same as the caves, only this time we go up, into the open, with a real sky to fall out of. Let me drive the route. You keep the blade.", seconds = 14f },
                // Audit fix: over-balanced symmetry thinned.
                new DialogueLine { speaker = "Ronin-7", text = "Drovis was a climb down. This one's up.", seconds = 3f },
                // Audit fix: aphorism-stacking thinned.
                new DialogueLine { speaker = "Echo", text = "She's at the top, and so is everything they buried in you. Left of the spillway, then the wall. They're all watching the gate. Nobody watches the cliff. Who climbs a mountain to break IN?", seconds = 13f },
                // Audit fix: quip-aphorism thinned.
                new DialogueLine { speaker = "Ronin-7", text = "Then I'm nobody. Get me up there.", seconds = 3f },
            };
        }

        // ---- BEAT 1 — THE WINDOW (last reach before Morrigan) ----

        private static DialogueLine[] GetBeat1WindowLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "That's her window. No light, no guard, cracked open from the inside a while now. Last reach, Cipher. After this it's just you, the cold, and the woman in the dark.", seconds = 12f },
            };
        }

        // ---- BEAT 2 — MORRIGAN'S ROOM (the meet) ----

        private static DialogueLine[] GetBeat2MorriganMeetLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrigan", text = "Cipher. You came up the cliff. Good. Sit, don't sit, I don't care. We have a small window and a large mountain.", seconds = 10f },
                new DialogueLine { speaker = "Ronin-7", text = "Resh sent me. He says you've been pulling children out of here for years.", seconds = 5f },
                // Audit fix: antithesis + on-the-nose self-narration thinned.
                new DialogueLine { speaker = "Morrigan", text = "Nine years, one child out the bottom whenever I could stomach the math. Don't call that a clean conscience. It's just what I pay myself to get out of bed. I can't empty this place alone. You can.", seconds = 16f },
                new DialogueLine { speaker = "Ronin-7", text = "Tell me where it breaks.", seconds = 2f },
            };
        }

        // ---- BEAT 2 — THE KILL-LIST (three towers unlock, any order) ----

        private static DialogueLine[] GetBeat2KillListLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrigan", text = "Three towers, three masters. The Cradle takes the youngest and unmakes them, Matron Hespa runs it, and she'll smile while she tells you it's kindness. The Proving breaks the rest into shape, Drillmaster Caradoc, no smile at all. The Vesting finishes them, the bonding, the switch, the last hollowing. That one's Master Kaelen, the architect.", seconds = 24f },
                // Audit fix: aphorism thinned.
                new DialogueLine { speaker = "Morrigan", text = "Cut one tower and the school staggers. Cut all three and it falls, and there's no one left to stop me opening every door. Kill Hespa, kill Caradoc, kill Kaelen. Take them in whatever order you want. All three are open.", seconds = 15f },
                new DialogueLine { speaker = "Ronin-7", text = "The children.", seconds = 1f },
                // Audit fix: arch register/missing contraction thinned.
                new DialogueLine { speaker = "Morrigan", text = "They're not yours to touch, and you knew that before you said it. The cadre, the garrison, the machines, cut all of it down. Not one trainee. The moment the third master falls, you come back here and I empty the mountain.", seconds = 14f },
                new DialogueLine { speaker = "Echo", text = "She's clean, Cipher, or as clean as anyone gets who lived this long inside it. I know what's in all three towers. Wherever you want to start, I'll walk it with you.", seconds = 12f },
            };
        }

        // ---- BEAT 3A — THE CRADLE (Matron Hespa intro) ----

        private static DialogueLine[] GetBeat3HespaIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Matron Hespa", text = "There now. Don't mind the noise, my loves. Eyes on me. We were on our numbers, weren't we.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7", text = "Step away from the child.", seconds = 2f },
                // Audit fix: over-balanced antithesis thinned to a question.
                new DialogueLine { speaker = "Matron Hespa", text = "I don't hurt them. I take the hurting things away from them. I'm the kindest thing that ever happens to them. You walked in here with a sword. Which of us is the cruel one?", seconds = 16f },
                // Audit fix: polished aphorism thinned.
                new DialogueLine { speaker = "Echo", text = "She means it, Cipher, that's the worst of it. She did it to a thousand kids and tucked every one in after. Don't let the soft voice slow your hand.", seconds = 11f },
                new DialogueLine { speaker = "Ronin-7", text = "I had a number once. Somebody soft as you gave it to me. You don't get to keep doing it.", seconds = 7f },
            };
        }

        // ---- BEAT 3B — THE PROVING (Drillmaster Caradoc intro) ----

        private static DialogueLine[] GetBeat3CaradocIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Drillmaster Caradoc", text = "Stand down, all of you. I've watched you cut through my floor, and I want a turn. You kill with my spacing, my count. That's my work standing in front of me with a sword.", seconds = 14f },
                // Audit fix: on-the-nose tag dropped.
                new DialogueLine { speaker = "Ronin-7", text = "I move like yours because you made me move like yours. Don't put your name on it.", seconds = 6f },
                // Audit fix: creed-aphorism trimmed, blunter register for Caradoc per the audit's register note.
                new DialogueLine { speaker = "Drillmaster Caradoc", text = "Proud? There's no proud in it, boy. Some break and some get broke. Somebody's got to do the breaking or the galaxy eats them whole. I made you hard enough to stand here. You're welcome.", seconds = 14f },
                new DialogueLine { speaker = "Echo", text = "He ran the floor you were beaten on, Cipher. I logged every hour of it. Don't trade blows with him to prove a point. Just end it.", seconds = 9f },
                new DialogueLine { speaker = "Drillmaster Caradoc", text = "Should've known. Soft hands at the top, soft hands at the bottom. Go on. Do the honest thing, at least.", seconds = 9f },
                new DialogueLine { speaker = "Ronin-7", text = "The honest thing is the children walking out of here. You don't get to be part of that.", seconds = 7f },
            };
        }

        // ---- BEAT 3C — THE VESTING (Master Kaelen intro, boss break) ----

        private static DialogueLine[] GetBeat3KaelenIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "Look at them, Cipher. That's you, a year before the bay. You don't have to kill the ones still mostly children under it. The metal cadre, yes. Them, you can choose.", seconds = 12f },
                new DialogueLine { speaker = "Master Kaelen", text = "You can stop swinging. They'll keep coming until I tell them not to, and I'm not going to. Unless you're the rare thing the floor reports say you are. The hand that hesitates. Go on. Spare one. I'd genuinely like to see it.", seconds = 17f },
                new DialogueLine { speaker = "Ronin-7", text = "They're children with your knives put in them. The knives are yours. So you're the one I came up here for.", seconds = 7f },
                // Audit fix: tricolon kept close to source (acceptable for this precise character) but trimmed.
                new DialogueLine { speaker = "Master Kaelen", text = "My knives, yes, I'll own them. Hespa thinks she's a mother. Caradoc thinks he's a forge. Me, I've read the plans, and I build it anyway. Come and read it back to me.", seconds = 16f },
            };
        }

        // ---- BEAT 4 — KAELEN'S CONFESSION (Ladder B, rung 4 — the saga's one disclosure) ----

        private static DialogueLine[] GetBeat4ConfessionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Master Kaelen", text = "There. Now I can say the part I built my whole life around not saying. You think the feeling is a wound. A thing that happened to you.", seconds = 9f },
                new DialogueLine { speaker = "Ronin-7", text = "Isn't it.", seconds = 1f },
                // Audit fix: classic "not X, it's Y" thinned.
                new DialogueLine { speaker = "Master Kaelen", text = "No. We built you hollow on purpose. A soldier who feels, hesitates. A soldier who hesitates, fails. So we reach in and cut it out, every time, by hand. The hollow isn't a side effect. We were aiming for it.", seconds = 18f },
                // Audit fix: "not X, it's Y" antithesis thinned.
                new DialogueLine { speaker = "Master Kaelen", text = "But you can't pour nothing into a vessel and seal it shut forever. Somewhere along the seam the weld stays weak, and in the rare ones, it opens. And what comes through the seam isn't damage. It's the thing we worked hardest to keep out. Mercy.", seconds = 21f },
                // Audit fix: "not X, it's Y" thinned.
                new DialogueLine { speaker = "Master Kaelen", text = "Your hesitation, the thing the floor reports flagged as a fault. It was never a fault. The seam was just giving way. That's all you are. The prayer that didn't take.", seconds = 15f },
                new DialogueLine { speaker = "Ronin-7", text = "Then I'm exactly what you were afraid of.", seconds = 4f },
                // Audit fix: balanced antithesis + missing contraction thinned.
                new DialogueLine { speaker = "Master Kaelen", text = "You're exactly what we made. And exactly what we couldn't stand to admit we made.", seconds = 5f },
                new DialogueLine { speaker = "Echo", text = "I logged that second as a fault, Cipher, same as they did. Neither of us knew what we were looking at.", seconds = 8f },
            };
        }

        // ---- BEAT 5 — THE EVACUATION (Morrigan opens the doors) ----

        private static DialogueLine[] GetBeat5EvacuationLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrigan", text = "All three. I felt the floor go still. Nine years, and you did it in an afternoon. Don't say anything yet. Help me open the doors before the garrison works out there's no one left giving them orders.", seconds = 14f },
                new DialogueLine { speaker = "Ronin-7", text = "Hundreds of them, and one shuttle. You don't carry this off a mountain. So how does it walk?", seconds = 6f },
                // Audit fix: two antitheses thinned.
                new DialogueLine { speaker = "Morrigan", text = "We're not carrying anyone. We let them walk, and we trust them to, which this place never once did. Eldest at the front and the back, counting heads the whole way down. Resh's name opens the doors at the bottom. Any other way and they're just freight again.", seconds = 22f },
            };
        }

        // ---- BEAT 5 — THE IRON YARD (bells wrong, the trainees descend) ----

        private static DialogueLine[] GetBeat5DescentLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "You're not going with them.", seconds = 2f },
                // Audit fix: heavily over-balanced antithesis thinned.
                new DialogueLine { speaker = "Morrigan", text = "They get to walk away from this. I don't, not yet. They go down the mountain. I've got to go further up. Because your leash didn't slip on its own, and I've spent nine years close enough to the work to know it.", seconds = 16f },
            };
        }

        // ---- BEAT 5 — MORRIGAN JOINS (Ally #3, the two seeds) ----

        private static DialogueLine[] GetBeat5MorriganJoinsLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: pet-phrase "hand on the strings" dropped for "steering".
                new DialogueLine { speaker = "Morrigan", text = "I've read everything that's come through this rock for years, and two things never fit. One, the Program's own actions don't add up to what happened to you. Someone outside the Program is steering this. I don't know who. But the pattern's not theirs.", seconds = 18f },
                // Soft consistency fix: scoped to "everything built in this rock" (the Ronin-line work this citadel does), not every Program make — avoids implying one architect built every Dominion operative line.
                new DialogueLine { speaker = "Morrigan", text = "Two. Everything built in this rock, the switches, the bonding, all of it, traces back through every revision to one mind. One insider on the Program's own side. I've chased him nine years and never got a name. I mean to.", seconds = 17f },
                new DialogueLine { speaker = "Echo", text = "That's the first time anyone's said out loud this is bigger than your handler and your switch, Cipher. She's not wrong, and she's the first one with the eyes to see it. Keep her.", seconds = 12f },
                new DialogueLine { speaker = "Ronin-7", text = "Then you berth aboard the Cairn with the rest of us. There's a training bay in her belly we never named. We'll call it after the thing we just brought down, so we remember what it was for.", seconds = 14f },
                new DialogueLine { speaker = "Morrigan", text = "A dead leviathan full of strays, and now a rogue engineer too. Fine. Give me a bench and the time, and I'll take your leash apart. Then I'll take apart the man who designed it.", seconds = 13f },
            };
        }

        // ---- BEAT 5 — THE OUTRO HOOK (the citadel burns, the hunt widens) ----

        private static DialogueLine[] GetBeat5OutroLines()
        {
            return new DialogueLine[]
            {
                // Audit fix: pet-phrase "a hand of its own" dropped.
                new DialogueLine { speaker = "Ronin-7", text = "We came to break the place that keeps making me. It's broken. And whoever made it has someone over THEM. So we don't stop here.", seconds = 10f },
                // Audit fix: pet-phrase "hand on the strings" dropped; trimmed to avoid a third maxim-closer in a row.
                new DialogueLine { speaker = "Echo", text = "Up the whole way, then, same as the climb. Mountain was never the top of anything. Just the first thing tall enough to show you the next one. Let's go find whoever's steering.", seconds = 13f },
            };
        }
    }
}
