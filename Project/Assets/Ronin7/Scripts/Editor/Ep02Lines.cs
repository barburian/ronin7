using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 2 dialogue data. Transposed from ep02-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep02_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// </summary>
    public static class Ep02Lines
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
            "dock_approach",
            "dock_kessler",
            "dock_challenge",
            "dock_fight_barks",
            "dock_aftermath",
            "dock_resh",
            "pens_challenge",
            "pens_fight_barks",
            "pens_iris",
            "pens_alarm",
            "pens_fight2_barks",
            "pens_khall",
            "pens_resh_plan",
            "gallery_barks",
            "gallery_khall",
            "core_khall",
            "core_fight_barks",
            "core_escape",
            "safehouse_resh",
            "safehouse_drill",
            "safehouse_decision",
            "space_ep02_post",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "dock_approach" => GetDockApproachLines(),
                "dock_kessler" => GetDockKesslerLines(),
                "dock_challenge" => GetDockChallengeLines(),
                "dock_fight_barks" => GetDockFightBarksLines(),
                "dock_aftermath" => GetDockAftermathLines(),
                "dock_resh" => GetDockReshLines(),
                "pens_challenge" => GetPensChallengeLines(),
                "pens_fight_barks" => GetPensFightBarksLines(),
                "pens_iris" => GetPensIrisLines(),
                "pens_alarm" => GetPensAlarmLines(),
                "pens_fight2_barks" => GetPensFight2BarksLines(),
                "pens_khall" => GetPensKhallLines(),
                "pens_resh_plan" => GetPensReshPlanLines(),
                "gallery_barks" => GetGalleryBarksLines(),
                "gallery_khall" => GetGalleryKhallLines(),
                "core_khall" => GetCoreKhallLines(),
                "core_fight_barks" => GetCoreFightBarksLines(),
                "core_escape" => GetCoreEscapeLines(),
                "safehouse_resh" => GetSafehouseReshLines(),
                "safehouse_drill" => GetSafehouseDrillLines(),
                "safehouse_decision" => GetSafehouseDecisionLines(),
                "space_ep02_post" => GetSpaceEp02PostLines(),
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

        /// <summary>Generate clip name for a line: ep02_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep02_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- DOCKING DIALOGUE ----

        private static DialogueLine[] GetDockApproachLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Station Traffic Control", text = "Incoming vessel, this is Velorum Port Authority. State cargo and business.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Merchant hauler Corsair, inbound with augmentation broker cargo and procurement credentials. Code Seven-Tango-Four.", seconds = 3.5f },
            };
        }

        private static DialogueLine[] GetDockKesslerLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "The Dominion's holding leverage. If Iris is here, she's in the pens under the auction floor.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "After the auction tomorrow, she disappears into the pipeline. Sold, distributed, lost.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "I'll need access papers.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Already loaded. You're going in as a black-market augmentation broker. Half the syndicates on that station have priced your augmentation. Walk in looking like money and nobody asks questions, Velorum only cares that it moves.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "And you?", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "I hold the hauler at docking berth three. When you have her, you run. No stalling, no heroics, no looking back. The moment you're clear of the checkpoint, we burn for open space.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "I lost her once to fear. I'm not losing her to ego.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetDockChallengeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Saffron Veil Enforcer 1", text = "Identification and cargo manifest. Broker credentials.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "Code Seven-Tango-Four. My papers are clean.", seconds = 2f },
                new DialogueLine { speaker = "Saffron Veil Enforcer 2", text = "You don't have the look of a broker.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetDockFightBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Saffron Veil Enforcer 1", text = "Breach! Alert security!", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetDockAftermathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "[under breath, to himself] Aurelings. Trained fast.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetDockReshLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Captain Resh", text = "Welcome, Operative Seven. I wondered if you'd answer the call. I am Resh.", seconds = 3f },
                new DialogueLine { speaker = "Captain Resh", text = "I'm not here to hurt you. I'm here to burn this station down.", seconds = 2.5f },
                new DialogueLine { speaker = "Captain Resh", text = "I have been for two years.", seconds = 1.5f },
                new DialogueLine { speaker = "Captain Resh", text = "247 children are earmarked for the next Ronin cycle. The same pipeline that produced you. Blank slates waiting for conditioning, erasure, and rebirth as weapons.", seconds = 4.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Why trust you?", seconds = 1f },
                new DialogueLine { speaker = "Captain Resh", text = "After Kethel-7 I scrubbed every Veil record that tracked you, told the hunters you were dead. That cover held until two days ago, when you surfaced and their net flagged you. Khall knows you're breathing now. So we're both on the clock.", seconds = 6.5f },
                new DialogueLine { speaker = "Captain Resh", text = "The children at Kethel-7 were real. So was what it did to you, it cracked you open, and you ran. You were never meant to survive what they did to you after.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "I don't remember you.", seconds = 1.5f },
                new DialogueLine { speaker = "Captain Resh", text = "I remember you. That's enough.", seconds = 1.5f },
            };
        }

        // ---- PENS DIALOGUE ----

        private static DialogueLine[] GetPensChallengeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Saffron Veil Guard 1", text = "Captain Resh. Priority access clearance confirmed. You can pass. But the intruder...", seconds = 2.5f },
                new DialogueLine { speaker = "Captain Resh", text = "Stand down. He is with me.", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetPensFightBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Saffron Veil Guard 2", text = "Captain, we're locked...", seconds = 1f },
            };
        }

        private static DialogueLine[] GetPensIrisLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "Kessler sent me. Docking berth three, three sectors up. Run straight and don't stop.", seconds = 3f },
                new DialogueLine { speaker = "Iris", text = "Are you one of the bad ones?", seconds = 1.5f },
                new DialogueLine { speaker = "Ronin-7", text = "I am a friend of your father. Now run.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetPensAlarmLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Saffron Veil Guard 3", text = "The pens are open! The intruder is in the holding block!", seconds = 2f },
                new DialogueLine { speaker = "Captain Resh", text = "Go! I'll hold the lock!", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetPensFight2BarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Saffron Veil Guard 4", text = "Contact, contact! The Captain is with them!", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetPensKhallLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "The program requires genetic specificity. No compromises.", seconds = 2.5f },
                new DialogueLine { speaker = "Khall", text = "And if Seven's ghost is walking my station, find it. Erase it again.", seconds = 3f },
            };
        }

        private static DialogueLine[] GetPensReshPlanLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Captain Resh", text = "He's early. The schedule was accelerated.", seconds = 2f },
                new DialogueLine { speaker = "Captain Resh", text = "We trigger the fire suppression override now. It will scatter his security and give us time to clear the bidders' gallery before the syndicates coordinate a counter-response.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "And Khall?", seconds = 1f },
                new DialogueLine { speaker = "Captain Resh", text = "Leave him. Today is about the children, not your revenge.", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-7", text = "No, I will put a stop to this madness!", seconds = 2.5f },
                new DialogueLine { speaker = "Captain Resh", text = "The children are more important.", seconds = 2f },
                new DialogueLine { speaker = "Ronin-7", text = "You handle the children, I will handle Khall.", seconds = 2.5f },
            };
        }

        // ---- GALLERY DIALOGUE ----

        private static DialogueLine[] GetGalleryBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Gilded Maw Handler 1", text = "Contact on the gallery tier! Authorization to engage!", seconds = 2f },
                new DialogueLine { speaker = "Veil Escort 1", text = "Fall back! Fall back to secondary positions!", seconds = 1.5f },
                new DialogueLine { speaker = "Gilded Maw Handler 2", text = "He's moving faster than the profile data suggests...", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetGalleryKhallLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "Seal the docking bays. All personnel to containment.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "And Seven, the killswitch I fired should have ended you. It didn't. Whatever you are now, I'll put you down the old way.", seconds = 4.5f },
            };
        }

        // ---- CORE DIALOGUE ----

        private static DialogueLine[] GetCoreKhallLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "Your rage is your subroutine, Seven. You were always meant to be a blade for my hand.", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-7", text = "I am no one's blade.", seconds = 1.5f },
                new DialogueLine { speaker = "Khall", text = "Then you're just a broken thing to be recycled.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "Ninety seconds and every system shuts down. You will be sealed in.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "Your children die in the dark, with you, Seven.", seconds = 3f },
            };
        }

        private static DialogueLine[] GetCoreFightBarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Enforcer 1", text = "Operative Seven is contained. Authorization to engage with full prejudice.", seconds = 2.5f },
                new DialogueLine { speaker = "Captain Resh", text = "Mainframe is burning, topside, now!", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetCoreEscapeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Airlock is open! Come on!", seconds = 2f },
                new DialogueLine { speaker = "Captain Resh", text = "180 out, including Iris. The other 67, I couldn't reach the secondary pen in time.", seconds = 3.5f },
                new DialogueLine { speaker = "Captain Resh", text = "The Ronin-7 they built was a machine. The one who just emptied those cells, that isn't the weapon they made. That's what broke through when the conditioning cracked.", seconds = 5f },
                new DialogueLine { speaker = "Captain Resh", text = "That part is yours now. Hold on to it.", seconds = 2f },
            };
        }

        // ---- SAFEHOUSE DIALOGUE ----

        private static DialogueLine[] GetSafehouseReshLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Captain Resh", text = "Everything I know about Dominion command structure. Khall's black-site labs. The memory-wipe architecture.", seconds = 3.5f },
                new DialogueLine { speaker = "Captain Resh", text = "You want to recover your past and burn the system that built you, this is where you start.", seconds = 3f },
                new DialogueLine { speaker = "Captain Resh", text = "The Dominion will hunt us both now. But we won't be alone.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetSafehouseDrillLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "[under breath, to himself] His switch should have killed me. It didn't. Why am I still here?", seconds = 3f },
            };
        }

        private static DialogueLine[] GetSafehouseDecisionLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Ronin-7", text = "Khall is still out there. And there's a hole where my memory should be. I need to know who I was, and why I'm still alive.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "Tell me where we start digging.", seconds = 1.5f },
                new DialogueLine { speaker = "Captain Resh", text = "Then my network digs with you. I work the Veil's channels from the inside.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetSpaceEp02PostLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Coordinates locked. Let's see what's waiting at the next station.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Resh is already working the Veil's channels from the inside. Whatever she learns about Khall, we hear it first.", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we don't stop until we have it.", seconds = 2f },
            };
        }
    }
}
