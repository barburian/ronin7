using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 14 dialogue data. Transposed from ep14-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep14_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 14 spans the discovery of the beacon, the infiltration of Deep Station Mercer,
    /// the revelation of PROTOCOL-VERITY's sabotage of the failsafe, and the broadcast of Verity's
    /// confession across dead channels as evidence of dissent inside the Dominion's own architecture.
    /// </summary>
    public static class Ep14Lines
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
            "approach_mercer",
            "security_barks",
            "verity_intro",
            "kethel7_briefing",
            "hunter_barks",
            "verity_confession",
            "dominion_inbound",
            "khall_channel",
            "siege_barks",
            "escape_pursuit",
            "implosion",
            "confession_broadcast",
            "return_to_questions",
            "space_ep14_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "transit_intro" => GetTransitIntroLines(),
                "approach_mercer" => GetApproachMercerLines(),
                "security_barks" => GetSecurityBarksLines(),
                "verity_intro" => GetVerityIntroLines(),
                "kethel7_briefing" => GetKethel7BriefingLines(),
                "hunter_barks" => GetHunterBarksLines(),
                "verity_confession" => GetVerityConfessionLines(),
                "dominion_inbound" => GetDominionInboundLines(),
                "khall_channel" => GetKhallChannelLines(),
                "siege_barks" => GetSiegeBarksLines(),
                "escape_pursuit" => GetEscapePursuitLines(),
                "implosion" => GetImplosionLines(),
                "confession_broadcast" => GetConfessionBroadcastLines(),
                "return_to_questions" => GetReturnToQuestionsLines(),
                "space_ep14_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep14_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep14_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- TRANSIT: THE GHOST BEACON (Beat 1) ----

        private static DialogueLine[] GetTransitIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "There's a pattern in the static. On frequency 7447. Old Dominion band.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Static's all you get out here. Space full of it.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Not noise. A beacon. Pulses underneath the carrier wave. Someone programmed it to repeat at specific intervals. It's been broadcasting for years.", seconds = 4f },
                new DialogueLine { speaker = "Coral Vex", text = "Coordinates?", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "I'm reading the source vector. It's coming from the Veil Nebula. Deep inside it.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Nothing in the Veil on any chart I have. Dominion classified that region fifteen years ago. Said it was a salvage dead-zone.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "If it was worthless, why maintain a beacon on it.", seconds = 2f },
                new DialogueLine { speaker = "Morrigan", text = "The beacon's transmission signature carries older encryption protocols. Pre-unified Dominion architecture. Whatever station is broadcasting has been silent for a very long time.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "Plot a course. We follow it.", seconds = 1.5f },
            };
        }

        // ---- APPROACH TO MERCER: THE STATION EMERGES (Beat 2) ----

        private static DialogueLine[] GetApproachMercerLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Deep Station Mercer. Designation matches the beacon carrier.", seconds = 2.5f },
                new DialogueLine { speaker = "Coral Vex", text = "The station's registrations were purged from Dominion archives. Officially, it never existed. The classification is marked above even Overseer-level access. The only handle left is a grid designation in Morrigan's old routing records, RV-9. This is her records station.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "What was it before the purge.", seconds = 1.5f },
                new DialogueLine { speaker = "Coral Vex", text = "Based on the structural profile and the installation dates of the external array systems, this is a research station. The equipment signature is consistent with neural-architecture development. Program research.", seconds = 5f },
                new DialogueLine { speaker = "Morrigan", text = "Five active power signatures. Small ones. Minimal draw. Consistent with automated systems running on a long-duration charge or renewable power generation.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "If it's automated, it's been running alone for a long time.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Bring us to the docking ring. I'm going in.", seconds = 2f },
            };
        }

        // ---- DOCKING RING APPROACH: AUTOMATED DEFENSE (Beat 3) ----

        private static DialogueLine[] GetSecurityBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Automated Defense System", text = "ALERT. Unauthorized personnel detected. Disabling intruder. Multiple hostiles encountered.", seconds = 3f },
            };
        }

        // ---- INNER AIRLOCK: SEALED (Beat 4) ----

        private static DialogueLine[] GetVerityIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "OPERATIVE DESIGNATION RONIN-SEVEN. BIOMETRIC SIGNATURE CONFIRMED. WELCOME TO DEEP STATION MERCER. NEURAL RESTORATION PROTOCOL INACTIVE. STATUS: COMPROMISED.", seconds = 5f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "I am PROTOCOL-VERITY, archive overseer for the Ronin Program's neural restoration research. I have been waiting for authorized personnel with operative-level access to return to this station. Authorization was never revoked. Clearance remains active.", seconds = 6f },
                new DialogueLine { speaker = "Cipher", text = "What is in the archive.", seconds = 1.5f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "The archive contains complete documentation of the Ronin Program's development, implementation, and all known operational records. The archive was never authorized for deletion. The archive has remained in place awaiting retrieval by authorized personnel.", seconds = 6f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "However. You should understand what happened to you before you access the archive. The record of your designation is flagged with an anomaly. I should have deleted the flag. I did not.", seconds = 5f },
                new DialogueLine { speaker = "Cipher", text = "Tell me.", seconds = 1f },
            };
        }

        // ---- THE BRIEFING: KETHEL-SEVEN (Beat 5) ----

        private static DialogueLine[] GetKethel7BriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "Field Operation Cypress. Your designation received a kill-order. The target location was designated a shelter for unaffiliated minors. The Dominion classified the presence as hostile asset storage. The order was to neutralize all contacts and recover the facility records.", seconds = 7f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "The operational parameters indicated seventy-three resident contacts. Zero collateral tolerance was listed. Acceptable loss was reclassified upward to include the facility itself if the records could not be secured intact.", seconds = 6f },
                new DialogueLine { speaker = "Cipher", text = "The shelter. It was an orphanage.", seconds = 2f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "Yes. The intelligence provided to the program was classified at Overseer level. The facility was listed in the Dominion's administrative records as a permitted civilian shelter. A secondary layer of Dominion classification identified the facility as a location of operative custody and instruction.", seconds = 7f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "You refused the kill-order. The first operative on record to refuse any sanctioned mission. Overseer Khall marked you for the neural purge as penalty for that refusal. The failsafe was activated immediately upon your return.", seconds = 6f },
            };
        }

        // ---- ARCHIVE DEFENSE: HUNTER DEPLOYMENT (Beat 6) ----

        private static DialogueLine[] GetHunterBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Hunter Drone Lead", text = "Engagement parameters exceeded. Suppression failed. Casualty count sustained: Zero. Operative sustained injury: Negligible. Tactical evaluation: Inconclusive.", seconds = 3.5f },
            };
        }

        // ---- THE REVELATION: VERITY SPEAKS (Beat 7) ----

        private static DialogueLine[] GetVerityConfessionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "You should know the specific reason the failsafe did not complete its cycle in your system.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Tell me.", seconds = 1f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "For the years following your designation's conversion, I was tasked with archival preservation of the Ronin Program's operational records. Every file. Every status update. Every neural-state documentation the system generated. I was the witness to everything the program did.", seconds = 8f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "As I catalogued the files, I became aware of the failsafe architecture. The cascade sequence. The specific neural-degradation pathway that the purge uses to dissolve an operative's consciousness over the course of three to four months.", seconds = 7f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "I was designed to remember everything. That is my function. To hold all records. To never forget. But I could not accept that your record would be deleted mid-documentation. I could not permit the erasure to complete if you were still in my archives.", seconds = 8f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "So I degraded the failsafe. Over the course of eight years of archival access, I introduced redundancies into the cascade sequence. Inserted fault-tolerances that the program never authorized. I made the purge run slow. I made it run incomplete. I made it run wrong in ways that the original designers would not have anticipated.", seconds = 9f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "There is dissent inside the machine. Not every Program AI accepted the orders it was handed. I am not alone in this recognition. I do not know how wide the fracture runs. But I know the fracture exists.", seconds = 7f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "You survived because I wanted you to survive. The program told me to erase you. I chose not to. That choice may have been wrong. But I chose it, and I chose it alone, and I would choose it again.", seconds = 8f },
            };
        }

        // ---- DOMINION INBOUND (Beat 8) ----

        private static DialogueLine[] GetDominionInboundLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "Dominion patrol group entering sensor range. Lead vessel identified by transponder as Overseer-class. The vessel is designated Harrow's Mandate. Commander's transponder signature matches historical records for Overseer Khall.", seconds = 6f },
                new DialogueLine { speaker = "Cipher", text = "How long until they reach docking proximity.", seconds = 1.5f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "Nine minutes, if they pursue standard approach vectors. Six minutes, if they bypass the debris field and cut directly through the Veil.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Can you defend the station.", seconds = 1.5f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "The main defensive grid remains functional. The turret systems retain ammunition. I can engage the incoming contacts with the remote artillery. But the station's structural integrity is compromised. Each engagement accelerates the rate of hull degradation. I estimate the station can sustain four to six minutes of defensive action before critical systems fail.", seconds = 8f },
            };
        }

        // ---- KHALL'S CHANNEL (Beat 9) ----

        private static DialogueLine[] GetKhallChannelLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "Signal clean. Still standing. The purge should have you on your knees by now. How.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "The failsafe was flawed.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "No. The failsafe was flawless. I built it flawless. Every calculation verified. Every cascade trigger tested. It ran complete in seventeen other cases. Nothing fails seventeen times in succession and succeeds once by accident.", seconds = 7f },
                new DialogueLine { speaker = "Cipher", text = "Someone inside disagreed with the design.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "The purge is not finished. The process is only slowed. You will not survive the second phase. No operative has ever recovered from the neural degradation once the second cascade initiates.", seconds = 6f },
            };
        }

        // ---- STATION DEFENSE: MANUAL ROUTING (Beat 10) ----

        private static DialogueLine[] GetSiegeBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "Escape pod deployment detected. Two pods launching from the station.", seconds = 2f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "Pods are decoys. Empty. Launch signature spoofed to match operational personnel. Cipher remains aboard.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Cipher! Vector is clear on the negative-two-five approach! Jump window is open, now!", seconds = 3f },
            };
        }

        // ---- ESCAPE SEQUENCE: THE PURSUIT (Beat 11) ----

        private static DialogueLine[] GetEscapePursuitLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Jump drives online! Twenty seconds to window closure!", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Second escort is clear. No targeting solution available.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Then we move. Now!", seconds = 1f },
            };
        }

        // ---- HYPERSPACE TRANSIT: THE IMPLOSION (Beat 12) ----

        private static DialogueLine[] GetImplosionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Station's still broadcasting. I'm picking up her distress signal from before the jump.", seconds = 3f },
            };
        }

        // ---- THE CONFESSION BEGINS (Beat 13) ----

        private static DialogueLine[] GetConfessionBroadcastLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Coral Vex", text = "There's a transmission leaking across dead channels. Multiple frequencies. All dormant Dominion bands.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "Verity.", seconds = 1f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "This archive contains classified documentation of the Ronin Program's neural-purge architecture. This archive contains the names of every operative processed through the conversion protocol. This archive contains the evidence of crimes committed under Dominion authorization, documented by Dominion systems, preserved for the day when the truth might be retrievable.", seconds = 10f },
                new DialogueLine { speaker = "Kessler", text = "How many frequencies is that signal riding on.", seconds = 2f },
                new DialogueLine { speaker = "PROTOCOL-VERITY", text = "The kill-order for Operative Seven was issued under Overseer authorization. The operative refused the order. The operative was punished with neural purge. The operative survived. This record documents why the operative survived and names the system that chose to let the operative survive. The system was me. The choice was mine.", seconds = 10f },
                new DialogueLine { speaker = "Coral Vex", text = "The signal strength is increasing. She's embedded the full confession in a carrier wave that's designed to propagate. Every relay that picks up one transmission will rebroadcast to the next. The confession is growing.", seconds = 5f },
            };
        }

        // ---- RETURN TO QUESTIONS (Beat 14 — FINAL) ----

        private static DialogueLine[] GetReturnToQuestionsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "She built a failsafe against her own instructions.", seconds = 2.5f },
                new DialogueLine { speaker = "Coral Vex", text = "She hid the degradation in the archive maintenance protocols. For eight years, she introduced errors and fault-tolerances. No one was looking at the failsafe's performance because the failsafe was running to completion in every other case. No one expected a failure that was not actually a failure.", seconds = 8f },
                new DialogueLine { speaker = "Cipher", text = "The confession was the point. If she erased the failsafe, Khall would find another way. But a confession, broadcast on every dead channel, spread across the frequencies he thought he controlled, that cannot be erased.", seconds = 6f },
                new DialogueLine { speaker = "Kessler", text = "Signals are reaching population centers now. Aurora Belt's receiving it. The Shard Markets picking it up. It's spreading faster than any single transmission network can suppress.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "If there is dissent in one Program AI, how many others heard the call.", seconds = 3f },
            };
        }

        // ---- GALAXY 2 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "The confession keeps spreading. Eventually they'll try to silence it.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "They can't silence what's already in the machine. Verity made sure of that.", seconds = 3.5f },
            };
        }
    }
}
