using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 12 dialogue data. Transposed from ep12-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep12_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 12 spans the journey to the Shard Market, the encounter with Morrigan the defector,
    /// and the revelation of an outside hand orchestrating the Ronin Program from beyond
    /// the Dominion's official hierarchy.
    /// </summary>
    public static class Ep12Lines
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
            "transit_intro",
            "morrigan_intercept",
            "recognition_duel",
            "the_confession",
            "purifier_barks",
            "the_outside_hand",
            "gunboat_barks",
            "hollow_kings_offer",
            "shardborn_barks",
            "the_shard",
            "the_defector",
            "cruiser_barks",
            "the_viewport",
            "space_ep12_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "transit_intro" => GetTransitIntroLines(),
                "morrigan_intercept" => GetMorriganInterceptLines(),
                "recognition_duel" => GetRecognitionDuelLines(),
                "the_confession" => GetTheConfessionLines(),
                "purifier_barks" => GetPurifierBarksLines(),
                "the_outside_hand" => GetTheOutsideHandLines(),
                "gunboat_barks" => GetGunboatBarksLines(),
                "hollow_kings_offer" => GetHollowKingsOfferLines(),
                "shardborn_barks" => GetShardBornBarksLines(),
                "the_shard" => GetTheShardLines(),
                "the_defector" => GetTheDefectorLines(),
                "cruiser_barks" => GetCruiserBarksLines(),
                "the_viewport" => GetTheViewportLines(),
                "space_ep12_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep12_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep12_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- TRANSIT: THE CARRYING WEIGHT (Beat 1) ----

        private static DialogueLine[] GetTransitIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "The origin choke-point is inside the asteroid cluster. Shard Market. It's Hollow Kings territory, they've been running memory-commerce out of derelict mine-fields in this sector for the last decade. Stripped neural imprints, combat-conditioning parcels, identity fragments from disappeared operatives.", seconds = 6f },
                new DialogueLine { speaker = "Kessler", text = "The market sits inside a hollowed asteroid, rotating. Docking cradles on the outer hull. Zero-gravity commons inside. A thousand vendors, a thousand different ways to make money off what someone else lost.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "How many entries through the docking ring before the interior checks identification.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "The outer ring doesn't check. It's the inner commons that runs the vendor compliance protocols, and those are Hollow Kings-administered, which means they're for show. The real currency in there is anonymity. Nobody asks, because everybody would rather not be asked in return.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "I go in alone. You monitor the entry ring.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "If you run into trouble in there, the entry ring is the only channel I can work from. The commons' internal frequency is jammed, Hollow Kings run interference against outside monitoring as standard practice.", seconds = 4.5f },
                new DialogueLine { speaker = "Kessler", text = "There's a vendor offering a lot labeled \"Ronin Protocol, Incomplete.\" That's either the source or a front for the source. Either way, it's the thread.", seconds = 4f },
            };
        }

        // ---- THE SHARD MARKET: MORRIGAN'S INTERCEPT (Beat 2) ----

        private static DialogueLine[] GetMorriganInterceptLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrigan", text = "You won't find what you're looking for at any of these stalls.", seconds = 2.5f },
                new DialogueLine { speaker = "Morrigan", text = "I've been wearing pieces of you for three cycles. Your training sequences. The specific architecture of your rage, how it moves before you decide to move. The night you argued with someone called Ronin-3 about limits. The way the conditioning stacked in the sixth phase, where they tried four times to remove something and four times it came back.", seconds = 7f },
                new DialogueLine { speaker = "Morrigan", text = "I know you came for the lot labeled 'Ronin Protocol.' It's mine. I put it on the exchange to create a reason for you to walk through this docking ring, and it worked.", seconds = 4.5f },
                new DialogueLine { speaker = "Morrigan", text = "I'll tell you everything I absorbed from your memories. What the conditioning built, what it tried to remove, and something you cannot have found anywhere else because it lives in a layer of the program's architecture that no one outside its infrastructure team has ever had access to. But I need to confirm first that the man I've been dreaming inside is still real.", seconds = 7f },
                new DialogueLine { speaker = "Morrigan", text = "Fight me. If you're still sharp enough to have earned the truth, you'll win.", seconds = 3f },
            };
        }

        // ---- THE ZERO-GRAVITY DUEL (Beat 3) ----

        private static DialogueLine[] GetRecognitionDuelLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrigan", text = "Sharp enough.", seconds = 1.5f },
                new DialogueLine { speaker = "Morrigan", text = "The lower vaults. What I have to tell you needs walls.", seconds = 2f },
            };
        }

        // ---- THE MERCHANT'S CONFESSION (Beat 4) ----

        private static DialogueLine[] GetTheConfessionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrigan", text = "I was a Dominion intelligence officer for seventeen years. Specifically, I was a program architect, I built dead drops, encrypted failsafe protocols, designed the communications infrastructure that the Ronin Program ran on. I was not a handler. Handlers broke people. I built the systems that the breaking required to function.", seconds = 7f },
                new DialogueLine { speaker = "Morrigan", text = "I defected in the program's fifteenth year, when I understood completely what I had made. I want to be precise about that: I did not defect when I first suspected. I defected when I had removed my last reason to doubt. That took longer than it should have.", seconds = 6f },
                new DialogueLine { speaker = "Cipher", text = "You built the infrastructure the program ran on. You also built its communications layer. Which means you could access incoming traffic at the architecture level, not just the content, the routing.", seconds = 4f },
                new DialogueLine { speaker = "Morrigan", text = "Yes. I'll get to what that means. First: when I ran, I went to ground in the Hollow Kings network. They value operational intelligence and they pay well and they ask fewer questions than most employers. I worked freelance, analysis contracts, encryption consulting. Three cycles ago, a Crimson Lotus fence came through the Market with a parcel of stripped neural imprints. The provenance was obvious to anyone who knew the conditioning format. They were yours.", seconds = 7.5f },
                new DialogueLine { speaker = "Morrigan", text = "I purchased them. Not as merchandise, I purchased them to take them off the market. And then I wore them, because wearing a stripped imprint is how you verify what's in it. That is the standard practice here. The verification process became three cycles of living inside your childhood. Your training. The chamber where they first put the chip in you, at the age of eight.", seconds = 8f },
                new DialogueLine { speaker = "Cipher", text = "The lot on the exchange was yours. You placed it there.", seconds = 2f },
                new DialogueLine { speaker = "Morrigan", text = "I needed you to come to me specifically. Posting your own provenance format as the lot listing was the one message I could send that I was certain would reach you, given what I knew you were looking for. I made you the bait for a trap designed to help you. I recognize the irony.", seconds = 6f },
                new DialogueLine { speaker = "Morrigan", text = "There is one shard I cannot wear. I have tried eight times. A mission number. Three digits and a sector code. Every time I seat it in the interface I come back screaming from whatever it contains, and I cannot tell you what is in it because I have never successfully completed the wear.", seconds = 6f },
                new DialogueLine { speaker = "Morrigan", text = "There is something you need to hear before I give you this. About what I found in the routing architecture. About something in the program that I could not explain and could not report and have been carrying ever since I found it.", seconds = 5.5f },
            };
        }

        // ---- PURIFIERS BREACH (Beat 5) ----

        private static DialogueLine[] GetPurifierBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Purifier Lead", text = "Contact in passage! Neural lance ready, pattern suppression!", seconds = 2f },
                new DialogueLine { speaker = "Morrigan", text = "Lance discharge in a corridor this narrow will take us all down, they know that. They're boxing us into the emergency lock.", seconds = 3.5f },
                new DialogueLine { speaker = "Morrigan", text = "Emergency lock, end of the passage. Two minutes of air. Move.", seconds = 2.5f },
                new DialogueLine { speaker = "Dominion Purifier 2", text = "Containment failed, EMP in sector, air loss confirmed! Transition to...", seconds = 2f },
            };
        }

        // ---- THE OUTSIDE HAND (Beat 6 — CORE REVEAL) ----

        private static DialogueLine[] GetTheOutsideHandLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrigan", text = "The Ronin Program's communications architecture runs through three authorization layers. Field operations go through Overseer-level authority. Program policy goes through the High Council. And there is a third layer, the deepest, the oldest, that handles the foundational directives. The parameters that define what the program is, what it does, and what the failsafe is calibrated to enforce.", seconds = 7f },
                new DialogueLine { speaker = "Morrigan", text = "I spent four years inside the program's deep communications layer. Building the encryption. Verifying the routing integrity. Which means I read every directive that passed through the third layer, and I cross-referenced the authorization ciphers against the full Dominion authority registry to confirm provenance.", seconds = 7f },
                new DialogueLine { speaker = "Morrigan", text = "The oldest directives, the ones that predate Overseer Khall, that predate the program's official Dominion charter, carry an authorization cipher I could not place. Not in the High Council registry. Not in the historical Overseer record. Not in any Dominion authority structure I had access to, and I had access to all of them.", seconds = 8f },
                new DialogueLine { speaker = "Morrigan", text = "Someone was feeding directives into the program from outside the Dominion entirely. Before it was called the Ronin Program. Before the Dominion's own charter for it was signed. The architecture that made you what you were, the foundational conditioning parameters, the failsafe design specifications, the core identity-erasure protocols, part of it was laid by a hand that no one inside the program was ever permitted to see. Not Khall. Not the Council. No one I could name.", seconds = 10f },
                new DialogueLine { speaker = "Cipher", text = "An authorization cipher that predates the program's official charter. Outside Dominion authority structure.", seconds = 3f },
                new DialogueLine { speaker = "Morrigan", text = "The cipher exists. I recorded it. I cannot trace it to a name because the name was never in the system, the instruction source was never inside the Dominion's administrative framework, which means tracing it inward produces nothing. You trace it outward or not at all.", seconds = 6f },
                new DialogueLine { speaker = "Morrigan", text = "I have been afraid of that cipher since I found it. I have not stopped being afraid of it. That is the most accurate thing I can tell you about what it means.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "This has to wait until I've heard the rest of what you know. Tell me what to expect from the shard.", seconds = 3f },
                new DialogueLine { speaker = "Morrigan", text = "A night. The orphanage compound on Kethel-7. The transports. And the order given.", seconds = 3.5f },
            };
        }

        // ---- GUNBOATS THROUGH THE GRAVEYARD (Beat 7) ----

        private static DialogueLine[] GetGunboatBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrigan", text = "Dorsal mount is yours. The locks are live but the geometry here favors us, their targeting systems are calibrated for open transit. The debris scatter will break lock solutions on anything smaller than a frigate.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "Mount.", seconds = 0.5f },
                new DialogueLine { speaker = "Morrigan", text = "The second one is out. The first is breaking to report, we have a heading window before the report reaches anything with intercept capacity. Minutes, not hours. I need a new course fed to the drive system as soon as we clear the field.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "Clear the field. Then we need to talk about who reported our departure from the Market.", seconds = 2.5f },
            };
        }

        // ---- THE HOLLOW KINGS' OFFER (Beat 8) ----

        private static DialogueLine[] GetHollowKingsOfferLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrigan", text = "There is a second reason I arranged this meeting. One I was dispatched to complete.", seconds = 3.5f },
                new DialogueLine { speaker = "Morrigan", text = "The Hollow Kings' operational council believes you are the only surviving operative whose architecture allows breaking the failsafe network from within. They have an infrastructure analysis group that has been studying the network topology for two years. Their conclusion is that a modified operative, one whose purge protocol was disrupted at the chip level rather than the signal level, could be used to introduce a systemic fault into the network that would cascade across all active operational leashes simultaneously.", seconds = 9f },
                new DialogueLine { speaker = "Morrigan", text = "The Council will restore your memories. Shard by shard, fully, from every parcel they have acquired over the past three years. They have significant holdings of your psychological archive. In exchange, you submit to a six-cycle modification process and then deploy against the network. Those are the offered terms.", seconds = 7f },
                new DialogueLine { speaker = "Cipher", text = "Different master. Same chains.", seconds = 2f },
                new DialogueLine { speaker = "Morrigan", text = "I told them you would refuse. They told me to deliver the offer regardless.", seconds = 3f },
                new DialogueLine { speaker = "Morrigan", text = "Shardborn. The council sends them when a message is refused. Hold the corridor.", seconds = 2f },
            };
        }

        // ---- THE SHARDBORN (Beat 9) ----

        private static DialogueLine[] GetShardBornBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrigan", text = "The council will send more when these don't report.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "How long is the reporting window.", seconds = 1.5f },
                new DialogueLine { speaker = "Morrigan", text = "Forty minutes from boarding confirmation. They committed to the entry at the transit ring, that's the last check-in. Forty minutes from there.", seconds = 3.5f },
            };
        }

        // ---- THE SHARD (Beat 10) ----

        private static DialogueLine[] GetTheShardLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrigan", text = "It was never mine to carry.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "I remember the signal that carried the kill order. Khall's voice was on top. Clear, direct, the standard command-channel architecture. The order was his.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "But the authorization cipher you described, the one that predates the program's charter, it was in the same transmission. Not on top. Layered underneath Khall's authority in the command-channel architecture. Below the command layer. In the infrastructure layer.", seconds = 7f },
                new DialogueLine { speaker = "Morrigan", text = "You can identify it? The cipher, the shape of it, even without the source attribution.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "The shape of it. Not the source. I heard it once, underneath everything else, in a moment I will not forget again.", seconds = 4f },
                new DialogueLine { speaker = "Morrigan", text = "Then whoever they are, whatever hand fed those instructions into the program from outside, they were present in the architecture of the order that broke you. Not through Khall alone. Through something above him that we still cannot name.", seconds = 7f },
            };
        }

        // ---- THE DEFECTOR (Beat 11) ----

        private static DialogueLine[] GetTheDefectorLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Not the standard pickup timeline. I'm reading Hollow Kings pursuit signatures on the nav display, someone's annoyed.", seconds = 3.5f },
                new DialogueLine { speaker = "Morrigan", text = "I am done being anyone's property.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "She knows the program's architecture from the inside. The communications layer, the failsafe infrastructure, the routing. She knows things Kaelen's archive doesn't contain.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "The collar's open. Come through.", seconds = 2f },
                new DialogueLine { speaker = "Morrigan", text = "I kept records. Not only yours. For eleven years I logged every operational directive that passed through the program's third authorization layer, every conditioning run, every mission order, every erasure. The routing metadata, the authorization ciphers, the full provenance stack. All of it.", seconds = 7.5f },
                new DialogueLine { speaker = "Morrigan", text = "I moved the records out of Dominion systems before I defected. They are in a decommissioned Dominion research station, grid designation RV-9. A station I helped encrypt before the program's operational scope expanded and the site was decommissioned as a redundant node. The records are complete. Not only your file. Every operative ever processed through the program. Every designation. Every conditioning run. Every mission order. Every erasure.", seconds = 10f },
                new DialogueLine { speaker = "Kessler", text = "Hollow Kings pursuit cruiser just dropped out of transit. Reading its mass at eleven hundred tons. Too heavy for us to outrun in open lane. They're already vectoring.", seconds = 4.5f },
                new DialogueLine { speaker = "Cipher", text = "The debris field, the construction frames off the transit junction. Feed them the cutter as a decoy and funnel the cruiser into the frames.", seconds = 3.5f },
            };
        }

        // ---- PURSUIT: THE CRUISER (Beat 12) ----

        private static DialogueLine[] GetCruiserBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Cruiser committed to the cutter's course, they're reading the transponder and following it into the central lane. We have ninety seconds in the boundary route before their sensors reorient.", seconds = 5f },
                new DialogueLine { speaker = "Morrigan", text = "Frames on port, adjust heading four degrees.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "I see it. Two degrees. Hold the compensation.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Through. Jump coordinates locked. Jumping.", seconds = 2f },
            };
        }

        // ---- THE VIEWPORT (Beat 13) ----

        private static DialogueLine[] GetTheViewportLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Morrigan", text = "The archive at RV-9. It will take Hollow Kings intelligence between four and seven cycles to identify the station once they know we have information about its existence. They may already be beginning that search, I cannot rule out that my records were partially compromised before I moved them.", seconds = 7f },
                new DialogueLine { speaker = "Cipher", text = "We reach that archive before they do.", seconds = 2f },
                new DialogueLine { speaker = "Morrigan", text = "There is a step between here and there. The Carnival of Forgotten Names, the Vellum Exchange market running in the Ashfield Corridor, six transit points from RV-9. The Exchange has been accumulating operative-archive materials for as long as I've known about their operation. If anyone in Galaxy 2 has a copy of the routing that leads back to the authorization cipher, they have it.", seconds = 8.5f },
                new DialogueLine { speaker = "Cipher", text = "The Carnival first. Then the archive.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "The Carnival of Forgotten Names. Setting the transit coordinates.", seconds = 3f },
            };
        }

        // ---- GALAXY 2 HUB CABIN: POST-MISSION BRIEFING (EP13 hook) ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "We've got Morrigan, we've got the archive shards, and we've got a cipher that came from somewhere outside the Dominion entirely. We still don't have the source.", seconds = 4.5f },
                new DialogueLine { speaker = "Cipher", text = "The Carnival of Forgotten Names. The Vellum Exchange will have accumulated pieces of this. If anyone in Galaxy 2 has the routing back to the source, they do.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "And after the Carnival, this decommissioned research station. RV-9. The complete archive.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "The connection will be there. We'll find the hand behind it.", seconds = 3f },
            };
        }
    }
}
