using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Chapter 9 ("The Pit and the Deep") dialogue data. Condensed from
    /// Ch09_The_Pit_and_the_Deep_Dialogue_Script.md and keyed by set ID, mirroring Chapter7Lines/
    /// Chapter8Lines' shape. Clip names follow the pattern: ch9_{setId}_{index:00}_{speaker_sanitized}.
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    ///
    /// story ouput/audit/Ch09_audit.md graded the source script C on naturalness (2 hard em-dash
    /// violations in character speech, heavy antithesis/aphorism-stacking, uniform register) and flagged
    /// one moderate + two minor consistency findings, echoed as fix #7 in 00_AUDIT_SUMMARY.md. Applied
    /// here:
    ///  - HARD BAN: the two em-dashes in character speech (source L432 Ronin, L492 Sable) are rewritten
    ///    as separate sentences.
    ///  - Fix #7 (moderate): the source has Gryph pledge to personally dive ("I'll take you down to it
    ///    myself... you'll want me on the dive") in the Bargain beat, then stage him as comm-only while
    ///    Ronin dives alone in the Tide Depths beat. Per the audit's suggested resolution (b), Gryph's
    ///    Bargain-beat lines below are softened to marking the route and guiding by comm, never claiming
    ///    to physically dive — consistent with his comm-only Beat3 appearance and the "same as the
    ///    mountain, same as the gate" solo-descent framing from the Beat0 briefing.
    ///  - Fix #7 (minor): "Everyone I take in lives aboard my ship" is corrected to "the Cairn" — it's
    ///    Kessler's ship, not Ronin's (Ronin recruits crew, he doesn't own the vessel).
    ///  - Minor (Ch09_audit.md 3): Gryph addresses Ronin as "Cipher" from Beat3 on, but nothing on-screen
    ///    in Beat2 gives him the name. A short two-line handoff is added at the end of the Bargain beat
    ///    so the codename is actually given before Gryph uses it.
    /// Full antithesis/aphorism-density thinning (the audit's broader naturalness note) is left for a
    /// dedicated saga-wide pass per 00_AUDIT_SUMMARY.md's recommended fix order — out of scope here,
    /// mirroring how Chapter8Lines only thinned the lines its own audit flagged individually.
    /// </summary>
    internal static class Chapter9Lines
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
            "ch9_beat0_briefing",
            "ch9_beat1_descent",
            "ch9_beat1_old_machinery",
            "ch9_beat2_challenge",
            "ch9_beat2_raid_bark",
            "ch9_beat2_bargain",
            "ch9_beat3_dive_intro",
            "ch9_beat3_confrontation",
            "ch9_beat3_aftermath",
            "ch9_beat3_overdrive",
            "ch9_beat3_recruit",
            "ch9_beat4_reveal",
            "ch9_beat5_holdkept",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "ch9_beat0_briefing" => GetBeat0BriefingLines(),
                "ch9_beat1_descent" => GetBeat1DescentLines(),
                "ch9_beat1_old_machinery" => GetBeat1OldMachineryLines(),
                "ch9_beat2_challenge" => GetBeat2ChallengeLines(),
                "ch9_beat2_raid_bark" => GetBeat2RaidBarkLines(),
                "ch9_beat2_bargain" => GetBeat2BargainLines(),
                "ch9_beat3_dive_intro" => GetBeat3DiveIntroLines(),
                "ch9_beat3_confrontation" => GetBeat3ConfrontationLines(),
                "ch9_beat3_aftermath" => GetBeat3AftermathLines(),
                "ch9_beat3_overdrive" => GetBeat3OverdriveLines(),
                "ch9_beat3_recruit" => GetBeat3RecruitLines(),
                "ch9_beat4_reveal" => GetBeat4RevealLines(),
                "ch9_beat5_holdkept" => GetBeat5HoldKeptLines(),
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

        /// <summary>Generate clip name for a line: ch9_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ch9_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- BEAT 0 — THE CAIRN (the briefing, voice-only) ----

        private static DialogueLine[] GetBeat0BriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrigan", text = "The Garden pointed you down, so down is where I looked. A relay, buried under the Rustfang hold, broadcasting on a band the Program stopped using before any of us were born. It's old, Cipher. It's bigger than a comms node has any business being, and it isn't feeding a base. Whatever they're building, this is one of its veins.", seconds = 20f },
                new DialogueLine { speaker = "Coral Vex", text = "The deep is where they hide the kept. Hardware that old, buried that deep, off every chart. That's not a place they store a thing. That's a place they do a thing they don't want seen. If there's a machine that big in the dark, Cipher, there are shadows near it. There always are.", seconds = 20f },
                new DialogueLine { speaker = "Mera Voss", text = "The Rustfang isn't open ground. It's a hold, a pirate free-hold carved into the caves of a dead world and held by an old warrior. Gryph. He took in every outcast and deserter who'd fight for him, runs the whole warren like a war-band, and answers to no syndicate I ever hunted for. We go down through his hold or we don't go down at all.", seconds = 18f },
                new DialogueLine { speaker = "Resh", text = "I know the name. Gryph's not a syndicate man, he's harder to deal with than one. He's an old warrior, the real kind. You don't buy him and you don't charm him. The men who came to take his hold are decorating the approach to it. You want his deep, friend, you don't bring him a price. You earn him.", seconds = 18f },
                new DialogueLine { speaker = "Iris", text = "A relay that old, that deep, under a hold that's been digging down for years, and those pirates never even knew it was there. You don't bury a thing that big to forget it. You bury it to use it where nobody's looking.", seconds = 14f },
                new DialogueLine { speaker = "Kessler", text = "And it only goes one way. Down a hole, through an old warrior who lives by the blade, into water if the readout's right, and at the bottom of it, whatever's worth hiding under a mountain. I'd send a team. I always want to send a team. But it's shafts and a dive and a man who throws in with one fighter at a time or none, so it's you, Cipher, same as the mountain, same as the gate. I'll hold the rig over the hold and run comm as far down as the rock lets me.", seconds = 24f },
                new DialogueLine { speaker = "Mira", text = "It's another place that only fits you. They keep being places that only fit you.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "They do. Because the Program does its work where it thinks no one will follow. So someone follows. Down a hole this time.", seconds = 8f },
                new DialogueLine { speaker = "Echo", text = "Cipher. Every answer in this story's been further down than the last. The grave, the reliquary, the Garden, each one deeper than the place before it. This one's the deepest yet, and the readout says it ends in water. I don't love water. But I'll tell you what I told you at the gate, turned around. This time I get to be useful the whole way. So let me be. Read you the dark, call the drops, keep you breathing. Down we go.", seconds = 22f },
                new DialogueLine { speaker = "Ronin-7", text = "Down we go.", seconds = 4f },
                new DialogueLine { speaker = "Coral Vex", text = "One thing, before you go where I can't. If you find the kept down there, and you will, they won't be shelved the way mine are. A thing that big needs them awake and working. Whatever you find in that water, it'll be one of us, and it'll be worse off than anyone I keep. Bring them up if you can. That's the only thing I'll ask of this whole descent.", seconds = 18f },
                new DialogueLine { speaker = "Ronin-7", text = "If there's one of us down there, they come up with me. Plot the Rustfang. Take us down.", seconds = 6f },
            };
        }

        // ---- BEAT 1 — THE RUSTFANG HOLD (descent / traversal) ----

        private static DialogueLine[] GetBeat1DescentLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "We're in the hold now, Cipher. Look at it, a wound all the way down, and they've been carving at it for years. Up here it's all theirs, watch-fires, lift-chains, hard men hauling plunder. Keep dropping. The lift only goes so far, then it's rope and the kind of falling we control on purpose.", seconds = 16f },
                new DialogueLine { speaker = "Kessler", text = "I've got you on the rig's scope, a little spark going down a big dark hole. Comm's already roughening up, rock's thick and getting thicker. I'll keep talking while it carries, you keep dropping, and the second I lose you, you keep going anyway. You find that relay and you come back up.", seconds = 16f },
            };
        }

        private static DialogueLine[] GetBeat1OldMachineryLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "Stop. Look at that wall. That isn't pirate work and it isn't natural rock. That's sealed, that's machined, that's ours, Cipher, Program-cut. Gryph's crew have been fortifying down toward this for years and building right around it because they don't know what it is. This wasn't set up down here. It was already here. They dug their hold into the lid of it.", seconds = 20f },
                new DialogueLine { speaker = "Coral Vex", text = "He's right. I can hear it in your feed, that hum. I've stood next to Program work my whole hidden life and that's the sound it makes when it's old and still running. A relay needs a wall, a wall needs a worksite. The pirates think they own a cave. They're standing on the roof of something the Program buried and walked away from with the lights still on.", seconds = 22f },
                new DialogueLine { speaker = "Morrigan", text = "Relay's loud now, loud enough I can finally read what it's doing, and Cipher, it isn't receiving. It's relaying. It takes a signal from somewhere even deeper and throws it further out. You're standing in the middle of a vein, not the heart. The heart's below you, under the water.", seconds = 18f },
                new DialogueLine { speaker = "Ronin-7", text = "A vein. Then let's go down to the heart of it.", seconds = 6f },
            };
        }

        // ---- BEAT 2 — GRYPH'S BARGAIN (hold overlook, protector choice, Ally #5) ----

        private static DialogueLine[] GetBeat2ChallengeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Gryph", text = "Stop right there. You came down a long way through my hold, past my watch, past my crews, and not one of them put you down. That tells me you fight, and it tells me you're here for the bottom shaft, which means you're here for me. So talk fast, stranger. Tell me why I shouldn't have my band put you in the shaft with the rest who came down here wanting what's mine.", seconds = 22f },
                new DialogueLine { speaker = "Ronin-7", text = "There's a Program relay below your water. Old, buried, bigger than anyone down here knows. I need the map to reach it. I didn't come to take it. I came to ask.", seconds = 9f },
                new DialogueLine { speaker = "Gryph", text = "Ask. Nobody comes down my hold to ask. I've met your kind before, dangerous men with good blades, every one of them figured the fastest way to the bottom shaft was a knife in my back and my map cut out of my skull. They came to take. So tell me true. What stops you doing the same.", seconds = 18f },
                new DialogueLine { speaker = "Echo", text = "He's not wrong about the men who came before you, Cipher. He's measuring whether he could stop you. He couldn't, and he half knows it. But he's not pleading, he's daring you. He wants to see which kind of dangerous walked into his hold.", seconds = 12f },
            };
        }

        private static DialogueLine[] GetBeat2RaidBarkLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Gryph", text = "Coil. They've been circling my hold for a month, and they picked their night. So here's your moment, stranger. Stand clear and let them gut me, then take the map off my body. Or you can pick a side and find out something about yourself.", seconds = 13f },
                new DialogueLine { speaker = "Ronin-7", text = "I pick a side. Hold your line, old man. I'll take the bridge, you keep the cradle, and we put them back up the shaft they came down.", seconds = 8f },
                new DialogueLine { speaker = "Rook", text = "Captain's got the cradle, the stranger's got the bridge. Rest of you, on me. We hold the shelf tonight.", seconds = 6f },
            };
        }

        private static DialogueLine[] GetBeat2BargainLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Gryph", text = "You fought beside me. I told you to stand clear and take what you came for off my body, and instead you put yourself on the bridge and bled for men whose names you don't know. I've held this hold thirty years, stranger. Every dangerous man who ever came down here came to take it. You're the first who came down with a blade and used it for my people instead of against them.", seconds = 20f },
                // Audit fix #7 (minor): "aboard my ship" corrected to "the Cairn" (Kessler's ship, not Ronin's).
                new DialogueLine { speaker = "Ronin-7", text = "Then know this. I'm done being the thing that takes. Your map's worth more to me given freely than cut off your body. Everyone I take in lives aboard the Cairn. A tracker, an engineer, a smuggler, a forebear, a child. You'd berth with the strays.", seconds = 16f },
                // Audit fix #7 (moderate): softened from a personal-dive pledge to marking/guiding by comm,
                // matching Gryph's comm-only presence in Beat3's dive.
                new DialogueLine { speaker = "Gryph", text = "A reason to give it. All right, then. The map's yours, and so is my arm, if you'll have it. I'll mark you every drowned gallery between here and the heart of the thing, because I'm the only one left who's been to the waterline and come back up breathing. Not for the protection. For being the first man to come down my hold in thirty years and spend his blade on my people instead of my back.", seconds = 24f },
                new DialogueLine { speaker = "Gryph", text = "But your band doesn't go leaderless while I'm walking you through that water. Rook's run this crew at my shoulder for ten years. As for the deep, you'll want my marks and my voice on the comm. The water's got a guardian, friend. Armored. Quiet. Kills my divers and never says a word, never takes a thing, just makes sure nobody reaches the bottom. I've lost six good crew to it and never once saw it bleed. Better you meet it with me telling you where the air pockets are.", seconds = 26f },
                // Audit fix (minor): a short handoff so Gryph actually learns the codename he uses from
                // Beat3 on (the source script never staged this on-screen).
                new DialogueLine { speaker = "Gryph", text = "I never got your name, stranger.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "Cipher. That's what the crew calls me.", seconds = 4f },
            };
        }

        // ---- BEAT 3 — THE TIDE DEPTHS (drowned archive: boss, Ally #6, Overdrive) ----

        private static DialogueLine[] GetBeat3DiveIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "I told you up top I didn't love water. I love it less now. Look at the racks, Cipher. They're down here too, but they're not shelved. They're wired in, strung into whatever that machine in the dark is, carrying current. The reliquary kept its shadows still. This place puts them to work.", seconds = 18f },
                new DialogueLine { speaker = "Gryph", text = "We're near the bottom now, and the channel's almost gone. The guardian's down there, right where I told you. It doesn't chase and it doesn't talk, it just stands in front of the deep and doesn't let anyone past. Whatever you have to do to it, do it, and get my divers' deaths paid. That's the last I can give you.", seconds = 18f },
            };
        }

        private static DialogueLine[] GetBeat3ConfrontationLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sable", text = "Don't come for me. You'll never reach me. He's always between. They put him here the day they finished wiring me in and he hasn't left the water since. He doesn't eat or sleep, and he won't even speak unless you force it. I've watched him kill everyone who's tried to reach me, and I've felt every one of them die, because they wired me to feel the whole lattice. Turn around. I'm not worth the water.", seconds = 22f },
                new DialogueLine { speaker = "Vane", text = "The asset is correct. You will not reach her. They send us where no one follows. I am where they sent me. You have come a long way down to die in cold water, and you will, and I will not remember it. Turn, or be kept.", seconds = 14f },
                new DialogueLine { speaker = "Echo", text = "Wraith-6. Older make than you, Cipher. Coral's make. His leash never broke. There's no one in there to talk to, only the order. That's what Coral would be if she'd never torn hers out. You can't save him. The only thing left to give him is the end of it. I'm sorry. Run the read with me and put him down.", seconds = 18f },
                new DialogueLine { speaker = "Ronin-7", text = "Older make. Same dark. And nobody came for you. An hour ago I chose to guard a man instead of rob him. You're guarding too, Wraith, they just aimed you at the cage instead of the people in it. I'm sorry. This is the only door I've got left to open for you.", seconds = 13f },
            };
        }

        private static DialogueLine[] GetBeat3AftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "It's done. No last word, no fight left to give. They took everything out of you but the order, and now even that's gone. Rest, Wraith. You came before me and you never once got to.", seconds = 9f },
                new DialogueLine { speaker = "Echo", text = "His shadow's free, Cipher. The leash died with him and the thing they wired behind his eyes just came loose. It's reaching for me, the way Coral's knew mine in the reliquary. It wants a home, and we're the only door open in this whole drowned hole. Hold still. Let it come. This is going to be fast.", seconds = 15f },
            };
        }

        private static DialogueLine[] GetBeat3OverdriveLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Echo", text = "There. Feel that. Everything's stopped but you. The water, the silt, the guns, all of it crawling, and you and me moving full speed through the middle of it. That's his last gift, Cipher, the only thing they ever let him give anyone. Overdrive. They take time away from everyone they leash. We can take their time away from them now.", seconds = 19f },
            };
        }

        private static DialogueLine[] GetBeat3RecruitLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sable", text = "I felt him die. I feel everything in this lattice, the leash snapping, all of it, the same way I've felt every diver die in this water for years. But that one was different. You didn't kill him like a guard. That was grief, like you knew him. I'd forgotten there was another way to do a hard thing. Who are you.", seconds = 20f },
                // Audit fix (hard ban): source em-dashes rewritten as separate sentences.
                new DialogueLine { speaker = "Ronin-7", text = "Ronin-7. They called me Cipher. The crew still does. I'm a newer make than he was. The run they built off the one he came from. And mine got cut loose. I came down here for the machine. I'm not leaving without you.", seconds = 13f },
                new DialogueLine { speaker = "Sable", text = "You can't just take me out. Understand what I am first. They didn't shelve me like the others, they made me into a node. My body is racked with kept shadows, dozens of them, seated in me and strung out through this whole machine, and I am awake inside all of it. Pull me free and I bring what I know about it with me, every relay, every vein, the whole shape of the thing.", seconds = 22f },
                new DialogueLine { speaker = "Ronin-7", text = "We're going to unmake whatever it's strung into. You're not a part I'm collecting, Sable. You're one of us. Everyone I take in lives aboard the Cairn and gets to be more than what they were built for.", seconds = 13f },
                new DialogueLine { speaker = "Sable", text = "More than infrastructure. I haven't been that in so long I don't know what I am underneath it. But I'll come, and I'll bring the lattice in my head, because if you're really going to unmake this thing, you'll need someone who is the map. Before you pull me loose, you need to see what I'm wired into. Bring me to the core.", seconds = 19f },
                new DialogueLine { speaker = "Echo", text = "She's the real thing, Cipher. I can feel her lattice from here, dozens of my own kind seated in one person and all of them awake. It's the racks again, except they made the rack a woman and ran current through her. I promised in the reliquary we'd come back for all of them. Get her out. Then let her show us the work.", seconds = 18f },
            };
        }

        // ---- BEAT 4 — THE CONCORD ENGINE (construction core, reveal + Ch10 hook) ----

        private static DialogueLine[] GetBeat4RevealLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sable", text = "Here it is. Put your eyes on the water and watch it draw itself. Your killswitch is a leash, Cipher. One throat, one hand, built to stop one operative when one handler pulls. You already know that. This is something else. Not a leash for one throat. A voice, loud enough to reach every world at once.", seconds = 22f },
                new DialogueLine { speaker = "Ronin-7", text = "Then what's it running on? A machine that size doesn't run on nothing. Show me what they're burning.", seconds = 7f },
                new DialogueLine { speaker = "Sable", text = "Us. The kept shadows. Look under the water, all those racks, all those lights. You've seen them shelved in the reliquary, still and waiting. They're not waiting anymore. Every shadow they ever shelved is being strung into this thing as wire, every erased operative's shadow made into a relay and forced to carry the silence outward. They didn't just bury us, Cipher. They put us to work.", seconds = 24f },
                new DialogueLine { speaker = "Echo", text = "I heard them the second we hit the water and I didn't want to understand it. Now I do. Every shadow in that water is one of me, Cipher. I promised we'd come back for them. I didn't know we'd be coming back to this.", seconds = 13f },
                new DialogueLine { speaker = "Ronin-7", text = "The thing in my skull was the prototype. Now everyone's the operative.", seconds = 8f },
                // Audit fix (hard ban): source em-dash rewritten as a separate sentence.
                new DialogueLine { speaker = "Sable", text = "And the proof against it. I am the map of this thing, Cipher. I can feel its veins running out to every node they've strung. We're one machine, wired across the dark, spread so wide no single raid could take the whole of it. Each node is a different year of the kept. A different make of operative, shelved when its program was finished with. The shadows racked into me are older than your line, the make right before yours. I'm not the record, Cipher. I'm the map to the others who are.", seconds = 26f },
                new DialogueLine { speaker = "Morrigan", text = "Scattered archives. That fits, and it fits how they hide anything they don't want read. There's a worksite deeper and older than this one, the Ninefold mines, where they cut and conscript the parts for projects this size. If they filed a piece of the Engine into a living archive, the nearest one is down there. We came down a pit to find a vein. Now we follow the vein to the first of the others like her.", seconds = 20f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we go down again. We bring you up out of this water, Sable, and we take the map you are to a ship full of people who'll fight for you instead of through you. And then we go find the others like you, one at a time, and take this machine apart, starting with the ones in this water.", seconds = 16f },
                new DialogueLine { speaker = "Echo", text = "Down again, Cipher. We came to a pit to find a vein and we found the heart, and the heart's a machine the size of the war strung through everyone we ever lost. The Ninefold next. The first of the others like her. We gather the rest of her kind, we unmake the Engine, we bring the kept home. That's the job now.", seconds = 20f },
            };
        }

        // ---- BEAT 5 — THE HOLD KEPT (climb out, the handoff to Rook) ----

        private static DialogueLine[] GetBeat5HoldKeptLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Rook", text = "You're going with him. I can see it. Thirty years you held this hold and never once talked about the surface like a place you'd go. So say it plain, captain, in front of the band, so nobody has to wonder.", seconds = 11f },
                new DialogueLine { speaker = "Gryph", text = "I'm going with him. There's a machine under our water bigger than this whole hold, and it's been eating my crew for years, and I mean to be there when it comes apart. The hold's yours now, Rook. You've run it at my shoulder for ten years, you run it without me now. Keep the band together. Keep them fed. Keep them out of that water.", seconds = 18f },
                new DialogueLine { speaker = "Rook", text = "The hold holds. You taught us how. Go take your machine apart, old man, and don't you dare die out there before you've seen it dead. There's a berth here whenever you want it.", seconds = 11f },
                new DialogueLine { speaker = "Gryph", text = "I'll hold you to the berth. Cipher, let's go up. I've spent thirty years at the bottom of a hole. I'd like to see this ship of yours with the lights on.", seconds = 9f },
            };
        }
    }
}
