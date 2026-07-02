using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 28 dialogue data. Transposed from ep28-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep28_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 28 "The Harvest Moon Festival" — Aethon-9 and the child-extraction operation.
    /// Cipher and Kessler discover a Dominion facility on Aethon-9 harvesting children for unknown
    /// purposes, mirroring the Kethel-7 massacre. Kessler confesses his unknowing complicity in
    /// running logistics for the sealed operations years ago. Cipher reads his own CIPHER-7 file,
    /// learning his Dominion designation and the behavioral anomaly that made him refuse orders.
    /// They extract ~30 children and retrieve fragments of archives revealing 17 more harvest
    /// installations galaxy-wide. Maya Selene, a festival administrator, helps the operation.
    /// The heading becomes: SISTER-IRON, an operative dispatched to complete Cipher's purification.
    /// </summary>
    public static class Ep28Lines
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
            "scan_agents",
            "maya_intro",
            "garden_walk",
            "manifest_room",
            "kessler_confession",
            "extraction_combat",
            "kethel_confession",
            "archive_file",
            "failsafe_explain",
            "ceremony_plan",
            "commander_recognition",
            "maya_farewell",
            "khall_confront",
            "breaking_barks",
            "medbay_aftermath",
            "sister_iron",
            "space_ep28_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "scan_agents" => GetScanAgentsLines(),
                "maya_intro" => GetMayaIntroLines(),
                "garden_walk" => GetGardenWalkLines(),
                "manifest_room" => GetManifestRoomLines(),
                "kessler_confession" => GetKesslerConfessionLines(),
                "extraction_combat" => GetExtractionCombatLines(),
                "kethel_confession" => GetKethelConfessionLines(),
                "archive_file" => GetArchiveFileLines(),
                "failsafe_explain" => GetFailsafeExplainLines(),
                "ceremony_plan" => GetCeremonyPlanLines(),
                "commander_recognition" => GetCommanderRecognitionLines(),
                "maya_farewell" => GetMayaFarewellLines(),
                "khall_confront" => GetKhallConfrontLines(),
                "breaking_barks" => GetBreakingBarksLines(),
                "medbay_aftermath" => GetMedBayAftermathLines(),
                "sister_iron" => GetSisterIronLines(),
                "space_ep28_post" => GetSpacePostLines(),
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

        /// <summary>Generate clip name for a line: ep28_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep28_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ==== BEAT 1 — THE LUMINOUS DESCENT ====

        // ---- SCAN AGENTS (docking security sweep) ----

        private static DialogueLine[] GetScanAgentsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Scan Agent 1", text = "Neural signature flagged. Asset CIPHER. Detain for verification.", seconds = 2f },
                new DialogueLine { speaker = "Dominion Scan Agent 2", text = "Suspect is mobile! All units converge on platform...", seconds = 1.5f },
            };
        }

        // ---- MAYA INTRO (festival crowd, initial meeting) ----

        private static DialogueLine[] GetMayaIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maya Selene", text = "Those are local security. My people. Your neural profile triggered our safety protocols.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "I didn't mean to alarm them.", seconds = 1.5f },
                new DialogueLine { speaker = "Maya Selene", text = "You're augmented. Heavily. That katana isn't ceremonial. So I'm guessing you're either running from something or toward something. Either way, walking through that crowd again is going to end in more security requests.", seconds = 4f },
                new DialogueLine { speaker = "Maya Selene", text = "I can walk you through the festival grounds. File a false report. You get a few hours before the next sweep.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Why would you do that?", seconds = 1.5f },
                new DialogueLine { speaker = "Maya Selene", text = "Because some part of me recognizes the look. Someone trying to figure out who they are while running from what they were. Come on.", seconds = 3f },
            };
        }

        // ==== BEAT 2 — THE GARDEN BELOW ====

        // ---- GARDEN WALK (bioluminescent terraces) ----

        private static DialogueLine[] GetGardenWalkLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maya Selene", text = "Aethon-9's spent the last six years building something real. The Dominion actually left us alone to do it.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "That should concern you.", seconds = 1.5f },
                new DialogueLine { speaker = "Maya Selene", text = "No one questions Dominion designations on their own land. Those have been sealed for three years. Automated systems handle whatever's down there.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Do you have children? On this world?", seconds = 2f },
                new DialogueLine { speaker = "Maya Selene", text = "Let's keep walking.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "I need to go down there.", seconds = 1.5f },
                new DialogueLine { speaker = "Maya Selene", text = "That's a containment zone. Security drones. You'll trigger an alarm.", seconds = 2f },
            };
        }

        // ==== BEAT 3 — THE MANIFEST ROOM ====

        // ---- MANIFEST ROOM (discovery of extraction operation) ----

        private static DialogueLine[] GetManifestRoomLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maya Selene", text = "I heard rumors. Kids disappearing from the lower settlements. Explained as scholarships. Dominion generosity.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "That's exactly what they want everyone to believe.", seconds = 2f },
            };
        }

        // ---- KESSLER CONFESSION (comms revelation and guilt) ----

        private static DialogueLine[] GetKesslerConfessionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Cipher. Report.", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Aethon-9 is running a child-extraction operation. Sealed facility beneath the agricultural sector. I'm reading a manifest now.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "Forty-seven children. Ages six to fourteen. Transferred off-world under educational relocation codes.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Cipher... I need to tell you something.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "During my Dominion logistics years, I ran contracts through Aethon-9. Sealed neural crates. Young-sized units. Loading them onto orbital transports.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "I never opened them. I didn't want to know what was inside.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "I'm sorry. I don't know if that means anything now.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "It means you're going to help me end this. That's what it means.", seconds = 2f },
            };
        }

        // ==== BEAT 4 — THE EXTRACTION TEAM ====

        // ---- EXTRACTION COMBAT (hydroponic farm combat) ----

        private static DialogueLine[] GetExtractionCombatLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maya Selene", text = "How are you doing this? How is your body remembering this?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "I don't know. I only remember what I'm doing when I'm doing it.", seconds = 2.5f },
            };
        }

        // ---- KETHEL CONFESSION (aftermath questions about the orphanage) ----

        private static DialogueLine[] GetKethelConfessionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maya Selene", text = "Do you remember anything before the Dominion?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Fragments. Phantom reflexes. The certainty I've done this exact thing before.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "There was an orphanage. A command to massacre the children. I refused.", seconds = 3f },
                new DialogueLine { speaker = "Maya Selene", text = "And the children?", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "Some survived. The ones I couldn't reach in time did not.", seconds = 2.5f },
            };
        }

        // ==== BEAT 5 — THE UNDERGROUND ARCHIVE ====

        // ---- ARCHIVE FILE (CIPHER-7 file discovery and reading) ----

        private static DialogueLine[] GetArchiveFileLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maya Selene", text = "CIPHER. They assigned you a number instead of a name.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "It's not assigned. It's earned. Every operative in the Program gets a number and a codename and training that strips everything else away.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "I was seven. They marked me CIPHER-7. And the file carries the name the syndicates gave me later: the Architect of Mercy. The documentation logs it as a behavioral anomaly they never successfully corrected.", seconds = 4.5f },
                new DialogueLine { speaker = "Maya Selene", text = "You remember?", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "My body remembers. That's almost worse.", seconds = 1.5f },
            };
        }

        // ---- FAILSAFE EXPLAIN (containment drones and slow purge) ----

        private static DialogueLine[] GetFailsafeExplainLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Hold still.", seconds = 1f },
                new DialogueLine { speaker = "Maya Selene", text = "What were those drones?", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "Capture units. The Dominion wants me alive. There's a failsafe in my neural implant, a slow purge that started the day I refused the order at Kethel-7. If they can trigger it remotely, they let the purge finish me.", seconds = 4f },
                new DialogueLine { speaker = "Cipher", text = "I'm already dying. They just want me to die on their schedule instead of mine.", seconds = 2.5f },
                new DialogueLine { speaker = "Maya Selene", text = "Then we're not wasting any more of your time on hesitation.", seconds = 2f },
            };
        }

        // ==== BEAT 6 — THE HARVEST CEREMONY ====

        // ---- CEREMONY PLAN (transport intercept and facility destruction) ----

        private static DialogueLine[] GetCeremonyPlanLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Transport ship arriving at festival peak. Six hours. They're extracting the children currently staged in the lower hold. Maybe thirty of them. Already prepped for transit.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "We burn the facility. Destroy the manifests. Get the children to the Corsair.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "The Dominion wired a colony-wide destruct into the facility's power core. If you're captured and the failsafe activates, the whole valley system goes with it.", seconds = 3.5f },
                new DialogueLine { speaker = "Cipher", text = "Aethon-9 is disposable infrastructure to them. We're not staying long enough to matter.", seconds = 2.5f },
            };
        }

        // ---- COMMANDER RECOGNITION (pre-launch bay confrontation) ----

        private static DialogueLine[] GetCommanderRecognitionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Commander", text = "CIPHER-7. Confirming asset recognition. Engaging.", seconds = 2f },
            };
        }

        // ---- MAYA FAREWELL (face-cupping moment before children evacuation) ----

        private static DialogueLine[] GetMayaFarewellLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maya Selene", text = "How does someone like you remember anything?", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "By being with someone who isn't afraid. Go.", seconds = 2f },
            };
        }

        // ==== BEAT 7 — THE BREAKING POINT ====

        // ---- KHALL CONFRONT (mainframe descent) ----

        private static DialogueLine[] GetKhallConfrontLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "CIPHER. You're still functioning. I'm almost impressed.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "Kethel-7. The orphanage. The order. The massacre. The refusal.", seconds = 2f },
                new DialogueLine { speaker = "Cipher", text = "I'm destroying this facility and every manifest in it.", seconds = 1.5f },
                new DialogueLine { speaker = "Khall", text = "CIPHER is a compromised asset. The Program's correction was always inevitable. Stand down and accept reclamation.", seconds = 3f },
            };
        }

        // ---- BREAKING BARKS (combat engagement audio) ----

        private static DialogueLine[] GetBreakingBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "This ends now.", seconds = 1.5f },
            };
        }

        // ==== BEAT 8 — THE MOON RISES ====

        // ---- MEDBAY AFTERMATH (recovery and archive revelation) ----

        private static DialogueLine[] GetMedBayAftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "The children are being routed to safe colonies through Morrigan's contact network. They'll have a chance.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Maya filed a broadcast with the Outer Worlds Council naming the Aethon-9 operation by facility code. She's calling for an investigation.", seconds = 3f },
                new DialogueLine { speaker = "Cipher", text = "What do I remember of Kethel-7 now?", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Everything?", seconds = 1f },
                new DialogueLine { speaker = "Cipher", text = "The order came in. I refused it. They wiped me for the refusal.", seconds = 2.5f },
                new DialogueLine { speaker = "Cipher", text = "I don't know yet who issued the original command. How far up the chain it reached. Only that it was real. That the children were real. And that I was right to refuse.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "I didn't know what was in those crates.", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "I know. That's what they count on. Everyone looking away. Everyone pretending.", seconds = 2f },
            };
        }

        // ---- SISTER IRON (operative designation discovery) ----

        private static DialogueLine[] GetSisterIronLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Kessler. Run every operative file we hold against that designation. SISTER-IRON.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "What are you afraid of?", seconds = 1.5f },
                new DialogueLine { speaker = "Cipher", text = "That I know her.", seconds = 1.5f },
            };
        }

        // ---- GALAXY 4 HUB CABIN: POST-MISSION BRIEFING ----

        private static DialogueLine[] GetSpacePostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Cipher", text = "Aethon-9 was running a harvest operation beneath the festival grounds, a child-extraction facility that mirrors Kethel-7 exactly. Forty-seven children marked for off-world transfer. Kessler learned he unknowingly ran logistics for these operations years ago when he was still Dominion. The underground archive held my file: CIPHER-7, the operative marked at age seven with a behavioral anomaly they never corrected, the refusal to massacre children. I read my own designation for the first time. We got thirty children to safety and pulled fragments of the archive revealing seventeen more harvest installations across the galaxy.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "So the heading is outward and inward both, hunting SISTER-IRON, an operative dispatched by Dominion command to complete your purification. That name carries weight, Cipher. You need to know what you're facing. And we need to find every one of those seventeen facilities before the Dominion decides extraction is less valuable than erasure.", seconds = 5f },
            };
        }
    }
}
