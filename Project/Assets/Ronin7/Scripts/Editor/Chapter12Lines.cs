using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Chapter 12 ("The Fracture") dialogue data. Condensed from
    /// Ch12_The_Fracture_Dialogue_Script.md and keyed by set ID, mirroring Chapter10Lines'/
    /// Chapter11Lines' shape. Clip names follow the pattern: ch12_{setId}_{index:00}_{speaker_sanitized}.
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    ///
    /// story ouput/audit/Ch12_audit.md graded the source script C+ on naturalness with ZERO em-dash
    /// violations in character speech (all em-dashes it found live in stage directions/production notes,
    /// not Line: text) and ONE hard script error (fix #5 below). Its remaining findings are naturalness
    /// suggestions (the saga-wide "not X, it's Y" antithesis tic, aphorism-stacking) that
    /// 00_AUDIT_SUMMARY.md explicitly defers to "a dedicated pass" — mirroring how Chapter10Lines/
    /// Chapter11Lines only fixed what their OWN audit flagged as a HARD error, this file transcribes the
    /// source Line: text verbatim rather than pre-empting that saga-wide pass.
    ///
    /// AUDIT FIX #5 (Beat 0): the source script has Ronin-7 answer "And Kessler. I heard you" and two
    /// voice: notes claim the beat "answers Kessler," but no Speaker: Kessler block exists anywhere in
    /// Beat 0 — Ronin answers a question that was never spoken on the page. A short Kessler line is
    /// added here (between Mera Voss and Morrigan, voicing the SEGMENT 0 outline's promised "what does a
    /// tool like that do to the people who pick it up") so Ronin's closing line has an antecedent. This
    /// is new-authored text (not a verbatim transcription) written to the same "no em-dash, plain
    /// declarative" standard as the rest of the chapter. The audit's other (minor) Beat 1 finding — Gryph
    /// speaks on comm but isn't listed in that beat's stage-direction comm-holder roster — is a
    /// stage-direction/production-note discrepancy only and has no Line: text to fix.
    ///
    /// LADDER C RUNG 2: delivered exactly once, in ch12_beat3_reveal (Echo's "One through six... You're
    /// the bottom of it" + Vale's "You are the original") — Ronin-7 is the TEMPLATE, the seventh Ronin
    /// and first-viable iteration, 1-6 culled, every edition cloned FROM him. This inverts Ch11's
    /// "you're a copy" supposition. The birth name "Soren" is never used anywhere in this file (reserved
    /// for Ch16 — see Chapter12LinesTests.NoLine_MentionsSoren) and no other numbered ladder rung is
    /// advanced.
    ///
    /// Comm-tagged speaker labels ("Mera Voss (comm)", "Gryph (comm)", "Vess (comm)", "Sable (comm)")
    /// are recorded under their plain name — "(comm)" is a stage direction, not part of the speaker's
    /// identity, matching every other chapter's convention.
    /// </summary>
    internal static class Chapter12Lines
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
            "ch12_beat0_briefing",
            "ch12_beat1_descent",
            "ch12_beat1_repetition",
            "ch12_beat2_greeting",
            "ch12_beat2_offer",
            "ch12_beat2_threat",
            "ch12_beat3_intro",
            "ch12_beat3_reveal",
            "ch12_beat3_aftermath",
            "ch12_beat4_offer",
            "ch12_beat4_refusal",
            "ch12_beat4_cut",
            "ch12_beat4_homecoming",
            "ch12_beat4_parting",
            "ch12_beat4_hook",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "ch12_beat0_briefing" => GetBeat0BriefingLines(),
                "ch12_beat1_descent" => GetBeat1DescentLines(),
                "ch12_beat1_repetition" => GetBeat1RepetitionLines(),
                "ch12_beat2_greeting" => GetBeat2GreetingLines(),
                "ch12_beat2_offer" => GetBeat2OfferLines(),
                "ch12_beat2_threat" => GetBeat2ThreatLines(),
                "ch12_beat3_intro" => GetBeat3IntroLines(),
                "ch12_beat3_reveal" => GetBeat3RevealLines(),
                "ch12_beat3_aftermath" => GetBeat3AftermathLines(),
                "ch12_beat4_offer" => GetBeat4OfferLines(),
                "ch12_beat4_refusal" => GetBeat4RefusalLines(),
                "ch12_beat4_cut" => GetBeat4CutLines(),
                "ch12_beat4_homecoming" => GetBeat4HomecomingLines(),
                "ch12_beat4_parting" => GetBeat4PartingLines(),
                "ch12_beat4_hook" => GetBeat4HookLines(),
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

        /// <summary>Generate clip name for a line: ch12_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ch12_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BEAT 0 — THE CAIRN (the briefing, voice-only) ----

        private static DialogueLine[] GetBeat0BriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cassie-04", text = "Last pin, Cipher. I'd tell you it gets easier at the end. It doesn't. The first three nodes held the Wraith make, the Ninja, the Knight. Three generations before yours, shelved and scattered. This one's different. I cross-checked it until my own ledger got tired of me asking. It doesn't hold an older make. It holds the Ronin make. Yours. The shadows racked in that vault are your own generation, Cipher. The last door has your family behind it.", seconds = 20f },
                new DialogueLine { speaker = "Cassie-04", text = "And here's the part that makes it a job and not a funeral. That node isn't just shelved like the others. They wired it live into the operative command-network, as the interface. It's the mouth that can give orders to every operative still on a leash, anywhere. There's a command-key down there too, an old-line authority they put in cold to hold it, name reads Vale. Whoever wakes him and holds that node can point the whole leashed army wherever they like. So can the people already inside that vault ahead of us. That's the second icon. We are not the only ones who read this map.", seconds = 26f },
                new DialogueLine { speaker = "Sable", text = "She's the last of mine, Cipher. The last node I can feel out there. And I can feel what they did to her, the same as I felt Cassie before the Ninefold, except this one they didn't just store. They made her a trigger. Every shadow racked in her, your make, your generation, all of them wired so a man with the key can pull them at once. I want her off it. Whatever else happens down there, whoever else is in that vault, that's the thing I'm going down for. Get her off the network. Let her be a person before she's a weapon.", seconds = 24f },
                new DialogueLine { speaker = "Mera Voss", text = "All of that is true and none of it matters if we get there second. There's a force already inside, moving down, reading the same key and switchboard we are. So we move now, we move fast. And I'll say the thing none of you will. If someone has to hold this, I'd rather it was a hand I trust than the one already racing us for it. I'm not saying we keep it. I'm saying don't pretend breaking it is free.", seconds = 18f },
                new DialogueLine { speaker = "Kessler", text = "I've given orders that didn't sit right later, and I've seen what a big lever does to a good man over time. So ask the harder question before you ask the easy one. Not what that key does for us. What it does to whoever's hand ends up on it, once every reason still sounds good. I want that answer before I want the vault.", seconds = 20f },
                new DialogueLine { speaker = "Morrigan", text = "I built leashes for nine years, Cipher, so let me tell you what you're looking at. That's not a vault. It's a switchboard. One node, one key, and every conditioned operative still breathing answers the same hand. I have spent this whole war helping you cut leashes one at a time. That down there is the master one. And I'll say the thing the engineer in me has to say out loud so nobody in this room gets a clever idea. A weapon that can command an army is not better in good hands. There are no good hands for it. The only safe version of that thing is a broken one.", seconds = 25f },
                new DialogueLine { speaker = "Coral Vex", text = "I cut one leash in my life, Cipher. My own. Tore the switch out with my own hands and spent fifty years hiding from the people who'd have used it on me again. So believe me when I tell you what a command-network is. It's the dream of every man who ever held my switch. One hand on all of us at once. I have run from that my whole life. I am not walking the rest of the way to it to pick it up. And Mera, you said a hand you trust. I trusted mine, and I spent a lifetime proving there's no hand safe enough to hold this one. We don't hold it. We don't carry it home.", seconds = 22f },
                new DialogueLine { speaker = "Vess", text = "Down there is where they filed my people's killers. The make that did it, racked and counted like stock. Good. I'd want to see the warehouse. Mera's right, we go fast, but I'll say my reason out loud the way you made me say my dead, Cipher. I'm not racing those others for the prize. I'm racing them to be the one who breaks it. Let me be in the room when the thing that built the men who took my people comes apart. I've earned that much honesty.", seconds = 19f },
                new DialogueLine { speaker = "Echo", text = "This part's only for you, Cipher. When the offer comes, and it will, I'll be in your head for it. Somebody waited in the cold a long time to make it. You came down this whole Act looking for who you're a copy of. You may not like the answer you find. When you do, remember. You came to free an army, not lead one. Those are different men, and only one walks back up these stairs. Hold on to which one you are. I'll hold it with you.", seconds = 18f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we go down fast, we get there first, and we get her off the network. Cassie, lock the vault to the map and keep the rival icon live. Sable, reach for her all the way down, even when the depth fights you. Morrigan's right, we don't hold the thing, we break it. And Kessler. I heard you. Whoever's down there waiting with an offer, the man I walk back up as is the one who came to cut the last leash, not pick it up. Everyone holds comm as long as the vault lets you. Echo past that. Take us down.", seconds = 20f },
            };
        }

        // ---- BEAT 1 — THE CRYO-VAULT DESCENT (race / traversal) ----

        private static DialogueLine[] GetBeat1DescentLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "We're in. And it's the opposite of the last one, Cipher. The canyon was a thing that used to be alive. This was never alive. This is the cleanest, coldest, most on-purpose place we've walked into. Look at it. Everything squared, everything doubled, everything in rows. Men built this who wanted you to know they were in control. Keep dropping along the rails, the gantries will hold, they were poured to hold forever. There's light at the bottom, amber, that's her. And there's other light moving between us and it that isn't ours. We are not alone in here, and we are not first. Move.", seconds = 22f },
                new DialogueLine { speaker = "Mera Voss", text = "I've got their pace off your drop-telemetry, Cipher, and they're good. Disciplined. Not scavengers. Whoever sent that force sent professionals, and they're maybe two tiers up on you and moving clean. You can't out-fight your way down, you'll lose the clock in every room you stop to clear. So don't clear them. Slip them. Take the gaps, take the bad footing they won't risk, and let the building do the work the way I'd do it. Reach the bottom first and the fight up here stops mattering.", seconds = 20f },
                new DialogueLine { speaker = "Gryph", text = "Gryph here. Cold deck's a liar, Cipher. Looks solid, frost makes every rail a promise it won't keep. I've crossed ice that wanted me dead politer than this. Don't trust a grip just because it's straight and clean. Straight and clean is how a thing built by men kills you, it lets you get confident. Test your weight, then commit, then don't second-guess it halfway. Halfway is where the frost takes you.", seconds = 16f },
                new DialogueLine { speaker = "Vess", text = "You're walking through their warehouse, Cipher. The make that took my people, stacked and frozen and waiting for a buyer. Part of me wants to crack every box on the way down. I won't. But say what you see for me. Tell me there's a lot of them. Tell me the thing I've been hunting was big enough to need a building this size. I want to know exactly how much of the dark I crossed to get here, and I want to hear it in your voice and not the count of a ledger.", seconds = 19f },
            };
        }

        private static DialogueLine[] GetBeat1RepetitionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "Cipher. Hold up. I want to read something and I want to be wrong about it. That cradle. And the one behind it. And. Give me a second. I'm matching the frames and I keep getting the same answer and I don't like the answer. The makes up top were mixed. Down here they're not mixed. Down here they're all one make. One frame. One face. I've been reading operatives my whole existence and I know exactly one face this well, because I live behind it. These are you. Not your make, Cipher. You. The same man, racked, over and over, all the way down.", seconds = 25f },
                new DialogueLine { speaker = "Ronin-7", text = "I see them. Keep reading. I want a number.", seconds = 7f },
                new DialogueLine { speaker = "Echo", text = "I can't give you one. The tiers go past where my read holds. Hundreds I can see. The shape of it says more than that, a lot more, down past the amber. Cipher, I'll keep my mouth shut and let you walk if you want. But I've been with you since before they wiped you and I'm not going to let you reach the bottom of this thinking you imagined it. Whatever's waiting on that throne is going to tell you what this means, and it's going to tell it like a gift. So I'm telling you the cold version first, from someone who loves you. This is a lot of your face. Keep moving and let's go find out why.", seconds = 24f },
                new DialogueLine { speaker = "Sable", text = "Cipher, she felt you stop. The node. She's at the bottom on the throne-tier and she just, she reached toward you and pulled back. I think she knows what you're standing in. I think she's been awake down here cataloging it the whole time they had her, all those cradles, all that, all of you. And there's something else on the throne-tier with her. Not racked. Not asleep. Warm. Whoever's been waiting for the key, Cipher, he's awake now. You're almost on him.", seconds = 21f },
                new DialogueLine { speaker = "Ronin-7", text = "Then I stop counting and go meet him. Echo, you were right to say it cold. Whatever he calls this, you already gave me the true name for it on the way down. Stay close. If his voice and yours ever tell me two different things about what I'm looking at, I trust the one that's been here since before the wipe. Take me to the bottom.", seconds = 15f },
            };
        }

        // ---- BEAT 2 — WAKING VALE (confrontation, the command-key and the crown) ----

        private static DialogueLine[] GetBeat2GreetingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vale", text = "Don't stop on my account. You've walked a long way down to me. Further than you know. Most men who reach this room reach it cold, on a slab, asleep. You reached it on your feet. That alone tells me the reports were right about you. I am Vale. They put me in the cold a long time ago and told me I'd be woken when the line finally produced something worth commanding. I confess I'd stopped believing it. And then the network woke me, and I opened my eyes, and here you are, standing in the middle of the answer with a sword in your hand and no idea what you're standing in.", seconds = 25f },
                new DialogueLine { speaker = "Ronin-7", text = "I know exactly what I'm standing in. I counted it on the way down. Say what you want and don't dress it.", seconds = 7f },
            };
        }

        private static DialogueLine[] GetBeat2OfferLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vale", text = "Good. Plain, then, since you've earned plain. The network told me what you are the moment it woke me, and I'll tell you, because the people you came down with never will. They love you too much to say it. You are not a soldier in this army. You are not even the best of them. You are the one they were all made out of. Every cradle you walked past with your own face in it, that is not a coincidence and it is not an insult. It is the most valuable fact in the galaxy, and you are the only man alive who gets to own it. The Program spent itself building one operative that worked. They got you. And then they did the only sane thing. They made more of you.", seconds = 27f },
                new DialogueLine { speaker = "Echo", text = "There it is, Cipher. The warm version. He's going to make it sound like the best day of your life. Don't take the frame off him. You already know the cold version, you've known it since the third cradle. He's not telling you something you didn't know. He's telling you to feel good about it. That's the trap. Not the fact. The feeling he wants you to hang on the fact.", seconds = 15f },
                new DialogueLine { speaker = "Vale", text = "I can hear you weighing it. The Program that made you has gone soft. They started worrying about the very thing that makes you valuable, the conscience, the fracture. They call your generation defective because some of you woke up. I call it the opposite. A blade that knows it's a blade is worth a thousand that don't. Every operative on this network whose conscience is cracking, the ones the Dominion would scrap, I would wake. All of them. Self-aware, free of the old handlers, pointed by a single will that knows what it's for. An army that chose to be an army, wearing your face because it is your face, answering the one man with the standing to lead it. You.", seconds = 23f },
                new DialogueLine { speaker = "Ronin-7", text = "Keep talking. I'm still listening for the part where they get a choice. I haven't heard it yet.", seconds = 6f },
                new DialogueLine { speaker = "Vale", text = "Spoken like a man who hasn't held the door yet. You will. You all do, once you see how heavy it is to leave it open. You imagine you'll free them and they'll thank you and scatter into good little lives. They won't. They'll be ten thousand woken weapons with no handler and your face, loose in a galaxy that built them to kill, and the first warlord who offers them an order will have what I'm offering you for free. Someone commands this army. The only question on the table is whether it's a man who is one of them, or a stranger who is not. I am being generous, telling you it should be you. I could simply take the key and never ask.", seconds = 26f },
            };
        }

        private static DialogueLine[] GetBeat2ThreatLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "He's not wrong about the danger, Cipher. That's what makes him dangerous. A freed army with no plan is a real problem, and he's the only one in this room pretending he has the answer to it. But look at the answer. His answer to ten thousand leashes is one bigger leash with better manners. We don't have the clean reply yet. We don't need it yet. We need the node off the network and we need to not become him on the way out. That's the whole job. Don't let him make you solve his problem his way just because it's the only way he's offering.", seconds = 19f },
                new DialogueLine { speaker = "Ronin-7", text = "You keep saying someone will hold it. You're the one who waited in the cold for it to be you.", seconds = 7f },
                new DialogueLine { speaker = "Vale", text = "No. I don't need you out of the way and I don't need you on the throne. I need only one thing. I need you to understand, before you decide, exactly what you're refusing. You keep saying you'll take the node off my network as though the network will let you. As though it doesn't have its own way of keeping an asset in place. You've cut down older makes all the way down here, I've read it, the Wraith, the Ninja, the Knight. Ancestors. Strangers in the end. You've never yet had to put down the one thing this army has that no other army ever did. Let me show you the part of the offer you haven't priced. Then refuse me, if you still can.", seconds = 24f },
            };
        }

        // ---- BEAT 3 — THE GUT-PUNCH AND THE MIRROR (reveal / boss / Mirror unlock) ----

        private static DialogueLine[] GetBeat3IntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7 Edition", text = "The asset stays on the network. That is the order. The original is to be detained or terminated. Compliance is not required of the original. Removal is.", seconds = 11f },
                new DialogueLine { speaker = "Echo", text = "Cipher. Don't go looking for yourself in those eyes. Nobody's home. I've lived behind those eyes my whole life and I know what's missing. But the network woke up when he did, and it's writing your answer on the wall behind him. Rows of one make. Yours. All printed from one source.", seconds = 14f },
                new DialogueLine { speaker = "Ronin-7 Edition", text = "Source-template confirmed. Original present. Designation holds.", seconds = 4f },
                new DialogueLine { speaker = "Echo", text = "And the source has a tag on it. Seventh iteration. First that held. One through six, culled. The source is you, Cipher. You're not a copy of anyone. They built six that failed, then they built you, then they printed all of this off you.", seconds = 12f },
                new DialogueLine { speaker = "Ronin-7", text = "Say it again. The number. One through six.", seconds = 6f },
            };
        }

        private static DialogueLine[] GetBeat3RevealLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "One through six. Flawed. Culled. You're seven, the first that worked. You came down here looking for whoever you're a copy of. There's no one. You're the bottom of it. Every cradle, every leash on every brother you've cut down, it all comes from you, because you're the one they could make it from. I'm so sorry. I wanted it to be the other answer.", seconds = 14f },
                new DialogueLine { speaker = "Vale", text = "Now you understand what you're worth. You came down here hunting the oldest thing in the dark. Look at the racks, Cipher. You're the newest face in this room. Every cradle here starts with you. Every man fears he's a copy of something greater. You are the thing they copied. There is no shame in that, only scale. He is your proof, not your better. Put him down or don't, the readout says what it says. You are the original. Your only choice is whether the original dies in this room or rules from it.", seconds = 22f },
                new DialogueLine { speaker = "Ronin-7", text = "Six. They threw six people into the dark to get one that would hold still.", seconds = 6f },
                new DialogueLine { speaker = "Ronin-7 Edition", text = "Engaging. The original resists removal. Escalating to terminate.", seconds = 5f },
            };
        }

        private static DialogueLine[] GetBeat3AftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "That's what I'd have been. My hands, my blade, and nobody awake behind the eyes. If you hadn't woken up in the dark and refused to let go, that's the face I'd have kept. I don't fear him, Echo. I grieve him. Now help me get past him to her.", seconds = 13f },
                new DialogueLine { speaker = "Echo", text = "It's done. He's down, and his shadow came loose, and. Cipher, it came to me. It's yours now. The same way the others did, except this one isn't a stranger's gift. This one's a copy of you, and now you can call it up at will, a second you, standing where I tell it to stand in a fight. They made you to be copied. So the last thing this one leaves you is a copy of your own, on your terms instead of theirs. I'd call that a small ugly justice if either of us were in the mood. We're not. There she is, off your left, still on the network. Let's get her loose and get out of your own face.", seconds = 23f },
            };
        }

        // ---- BEAT 4 — REFUSING THE THRONE (the refusal, the node cut loose, hook out) ----

        private static DialogueLine[] GetBeat4OfferLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vale", text = "You're standing exactly where I hoped you would. Over your own copy, with the node at your hand and the network at your word. You've proven my whole case for me, you understand. You grieved him. A weapon that grieves is the only weapon worth having, and you're the source of every one of them. So I'll ask it once, plainly, no velvet. Take the key. Wake them. Lead the only army in history that would choose its own commander, because its commander is the man it's made of. Refuse, and you don't save them from command. You just leave the command to someone with worse hands than yours. Be the warden who's one of them. It is the most good you will ever do.", seconds = 24f },
            };
        }

        private static DialogueLine[] GetBeat4RefusalLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "You keep calling it the most good I'll ever do. You'd know. You've spent a long time in the cold rehearsing the most good a cage can do. Here's the answer, Vale, and it's the only one I've got. An army of the freed, commanded, is just the cage with a kinder face on the door. I've cut that door open too many times to walk back through it holding the key. You're right that someone worse might pick this up. So I'm not going to leave it for them. I'm not going to hand it to you. And I'm not going to keep it. I'm going to break it.", seconds = 20f },
            };
        }

        private static DialogueLine[] GetBeat4CutLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "That's the man who walks back up the stairs, Cipher. Cut her loose. Same as the others, except this one's your own make, so make it gentle. She's the last node. When her shadow comes off the network, the record's whole. Every node, every name, every conscript, the whole order of the build, in one place at last. Do it.", seconds = 14f },
            };
        }

        private static DialogueLine[] GetBeat4HomecomingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sable", text = "She's off. Cipher, she's off the network, I can feel her be just a person again, just for a second before. I've got her. I've got the last of mine. Thank you. Thank you for not turning her into the thing he wanted. You went down into a room that offered you everything and you came back up with one freed sister and an empty hand, and I will never be able to tell you what that's worth to the rest of us still wired into things we didn't choose.", seconds = 20f },
            };
        }

        private static DialogueLine[] GetBeat4PartingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Vale", text = "You think you've broken it. You've broken one node and made an orphan of an army that still exists, still sleeps, still wears your face by the thousand, and still needs a hand. You haven't ended the question. You've only left the room before it's answered. I'll still be here when it is. There will always be a vault, and a key, and a man certain he knows the kindest way to hold the leash. You didn't beat the idea today. You just declined it. We'll see how long a galaxy of orphans lets you keep declining.", seconds = 20f },
                new DialogueLine { speaker = "Ronin-7", text = "Maybe. But I declined it. Today I declined it, and the man who came down here ready to say yes walked back up saying no. That's not nothing, Vale. That's the only thing that's ever been worth anything. Stay in your cold. I have somewhere to be.", seconds = 12f },
            };
        }

        private static DialogueLine[] GetBeat4HookLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "The record's whole now, Cipher. And it doesn't just say how they built the leash. It says who. One name under all of it, one hand that drew you and the killswitch and every cradle in this room. We don't go down anymore. We go to him. The man who made you. That's the last door, and it's the only one left.", seconds = 14f },
            };
        }
    }
}
