using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 1 dialogue data. Transposed from ep01-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep01_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// </summary>
    internal static class Ep01Lines
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
            "ship_beat2",
            "ship_beat3_barks",
            "ship_beat4",
            "ship_beat7",
            "ship_beat56",
            "market_kessler",
            "market_dockhand",
            "market_trader",
            "hideout_intro",
            "hideout_wave1_bark",
            "hideout_wave2_bark",
            "hideout_wave3_bark",
            "hideout_hack",
            "space_briefing",
            "space_velorum",
            "space_enemy_warning",
            "space_objective_cleared",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "ship_beat2" => GetShipBeat2Lines(),
                "ship_beat3_barks" => GetShipBeat3BarksLines(),
                "ship_beat4" => GetShipBeat4Lines(),
                "ship_beat7" => GetShipBeat7Lines(),
                "ship_beat56" => GetShipBeat56Lines(),
                "market_kessler" => GetMarketKesslerLines(),
                "market_dockhand" => GetMarketDockHandLines(),
                "market_trader" => GetMarketTraderLines(),
                "hideout_intro" => GetHideoutIntroLines(),
                "hideout_wave1_bark" => GetHideoutWave1BarkLines(),
                "hideout_wave2_bark" => GetHideoutWave2BarkLines(),
                "hideout_wave3_bark" => GetHideoutWave3BarkLines(),
                "hideout_hack" => GetHideoutHackLines(),
                "space_briefing" => GetSpaceBriefingLines(),
                "space_velorum" => GetSpaceVelorumLines(),
                "space_enemy_warning" => GetSpaceEnemyWarningLines(),
                "space_objective_cleared" => GetSpaceObjectiveClearedLines(),
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

        /// <summary>Generate clip name for a line: ep01_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep01_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ---- SHIP DIALOGUE ----

        private static DialogueLine[] GetShipBeat2Lines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "You're awake. That's good. Better than I expected.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "You were floating in the debris field. No ID. No memory, from what I can tell. You had a katana holstered to your belt, and a lot of augmentation. So I brought you in.", seconds = 4.5f },
                new DialogueLine { speaker = "Ronin-7", text = "How long?", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Three weeks. You were unconscious for the better part of the first one. Your body's running on something I don't fully understand. But you're stable now. That's what matters.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "My name?", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "You don't have one. Not anymore. Not that you remember.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "But you will. First things first. Can you stand?", seconds = 2.5f },
                new DialogueLine { speaker = "Ronin-7", text = "I can move.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "One thing before you find your feet. The Dominion sweeps this sector for salvage every few weeks, and one's overdue. I've kept you hidden three weeks, I can't hide you through an inspection.", seconds = 5.5f },
                new DialogueLine { speaker = "Kessler", text = "So when they board, and they will, be ready to move.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetShipBeat3BarksLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Trooper 1", text = "Medical bay contact. Breach imminent!", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Trooper 2", text = "Target's armed! Weapons hot!", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetShipBeat4Lines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Routine check. This happens every few weeks. They like to remind us they're in charge.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "I had the chance to hand you over for 3 weeks now. Didn't.", seconds = 3f },
                new DialogueLine { speaker = "Ronin-7", text = "Why?", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "Because six years ago, you saved my life. I was Dominion then, same as you. A raid went bad, I was pinned, written off for dead. You came back for me when the order was to leave me.", seconds = 5.5f },
                new DialogueLine { speaker = "Kessler", text = "I disappeared after that. Walked off the raid, off the grid, off everything I'd been. Been drifting the fringe as a salvager ever since.", seconds = 4.5f },
                new DialogueLine { speaker = "Kessler", text = "I wasn't looking for you. I was stripping a dead hauler for parts and there you were, in the debris. The moment I saw your face, I knew it.", seconds = 4.5f },
                new DialogueLine { speaker = "Ronin-7", text = "What is your name.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "Just Kessler now.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetShipBeat7Lines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "We've got a problem. Those troopers, their helmet feeds were live. Everything they saw went up to a Dominion relay before I could cut the signal.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "Meaning?", seconds = 1f },
                new DialogueLine { speaker = "Kessler", text = "Meaning a server somewhere just watched you fight. And their systems don't forget a face. Especially not yours.", seconds = 4f },
            };
        }

        private static DialogueLine[] GetShipBeat56Lines()
        {
            return new DialogueLine[]
            {
                // Beat 6: Dominion side — the recognition flag triggers Khall
                new DialogueLine { speaker = "Ship computer", text = "Incoming message.", seconds = 1.5f },
                new DialogueLine { speaker = "Khall", text = "Recognition flag. Priority black.", seconds = 2.5f },
                new DialogueLine { speaker = "Khall", text = "Kessler. Don't think you can escape the Dominion's grasp. We already know who you are.", seconds = 5f },
                new DialogueLine { speaker = "Khall", text = "We will keep your daughter Iris safe, for now. You will see her again when you return our lost item.", seconds = 5f },
                new DialogueLine { speaker = "Kessler", text = "That's Khall, high Dominion. If his systems have your face, hiding's finished. For both of us.", seconds = 4f },
                new DialogueLine { speaker = "Ronin-7", text = "They have your daughter. I will not let her be harmed because of me. Turn me in!", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Nonsense. They will not harm her. They are just using her as leverage. We need to take her back, and anyone in the way learns why they should've left you in the dark.", seconds = 5f },
                new DialogueLine { speaker = "Ronin-7", text = "Then we save her. Together.", seconds = 2f },
            };
        }

        // ---- MARKET DIALOGUE ----

        private static DialogueLine[] GetMarketKesslerLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Keep your hood up. This market's crawling with Dominion eyes.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "Somebody here runs cargo for the garrison. Find them, and we find the way in.", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "I'll watch the exits. Go on, ask around.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetMarketDockHandLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dock Hand", text = "You're not from the platforms. I can tell.", seconds = 2.5f },
                new DialogueLine { speaker = "Dock Hand", text = "The Dominion command? That's the old relay compound, past the cargo cranes. Don't go near it.", seconds = 4f },
                new DialogueLine { speaker = "Dock Hand", text = "Whatever they're holding in there, it isn't worth your life.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetMarketTraderLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Trader", text = "Information's not free, stranger. But I hate those Dominion boots, so listen.", seconds = 4f },
                new DialogueLine { speaker = "Trader", text = "The compound guards rotate at dusk. Three on the door. Quiet types.", seconds = 3.5f },
                new DialogueLine { speaker = "Trader", text = "Get inside, find their command terminal. Everything they move through this world is logged there.", seconds = 4f },
            };
        }

        // ---- HIDEOUT DIALOGUE ----

        private static DialogueLine[] GetHideoutIntroLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "I'm patched into your feed. Three hostiles in the entry hall, take them down fast.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "Past them there's a corridor. The command room's at the end. That's where their data lives.", seconds = 4f },
            };
        }

        private static DialogueLine[] GetHideoutWave1BarkLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Trooper", text = "Contact! Engaging!", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetHideoutWave2BarkLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Trooper", text = "Thermal signature converging on platform! Containing spread!", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetHideoutWave3BarkLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Trooper", text = "Unit approaching! All available teams to the command room!", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetHideoutHackLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "You're in. Pulling the manifest now...", seconds = 3f },
                new DialogueLine { speaker = "Kessler", text = "No. No, no, no... They moved her. Iris isn't in the school anymore.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "She's in Dominion custody. Transit-classified. Destination logged as, Velorum Station.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "An auction. They're going to sell her. We move now, get back to the ship.", seconds = 4f },
            };
        }

        // ---- SPACE DIALOGUE ----

        private static DialogueLine[] GetSpaceBriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Check the silver planet, Aquilane. Water-mining world. Nothing but storms and silver seas down there.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "The Dominion runs a relay platform on the surface. That's the place where Iris's school was.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Set us down clean.", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetSpaceVelorumLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Velorum Station. It's a hub where assets and intelligence are brokered under cover of an open market.", seconds = 3.5f },
                new DialogueLine { speaker = "Kessler", text = "We don't hide and we don't wait. We walk into Velorum, we find the right buyer, and we get the location.", seconds = 3.5f },
                new DialogueLine { speaker = "Ronin-7", text = "Into the lion's mouth.", seconds = 1.5f },
                new DialogueLine { speaker = "Kessler", text = "We find Iris. We don't stop until we do.", seconds = 3f },
            };
        }

        private static DialogueLine[] GetSpaceEnemyWarningLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Watch out! Hostiles closing in!", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Heads up! Enemy ships, right on top of us!", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "Contacts inbound! They're coming for us, watch yourself!", seconds = 2f },
            };
        }

        private static DialogueLine[] GetSpaceObjectiveClearedLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Skies are clear. Follow the marker, that's where we're headed next.", seconds = 3f },
            };
        }
    }
}
