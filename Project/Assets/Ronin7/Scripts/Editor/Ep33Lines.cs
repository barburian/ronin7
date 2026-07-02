using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Canonical Episode 33 dialogue data. Transposed from ep33-dialogue-script.txt and keyed by set ID.
    /// Clip names follow the pattern: ep33_{setId}_{index:00}_{speaker_sanitized}
    /// Each line's clip field is left null; TTS or audio sourcing fills it at build time.
    /// Episode 33 "The Tenfold Pact" — the series finale. Ten syndicate heads gather at neutral ground to
    /// hear Soren's revelation: the Dominion's architect is First Overseer Maelgorn and the Obsidian Synod.
    /// Together they storm the Synod fortress, shatter the failsafe network, and free thousands of operatives
    /// from neural enslavement. Khall dies destroying the Beacon from within. Maelgorn falls to the ten.
    /// At a table — the same table where war councils begin — the ten swear the Tenfold Pact: to spend
    /// their remaining lives keeping instead of taking, protecting the freedoms they reclaimed.
    /// </summary>
    public static class Ep33Lines
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
            "gathering_table",
            "gathering_fight",
            "gathering_pact",
            "map_briefing",
            "assault_approach",
            "assault_dogfight",
            "assault_cleared",
            "creche_divide",
            "creche_combat",
            "creche_rescue",
            "beacon_lit",
            "beacon_duel",
            "beacon_freed",
            "khall_death",
            "throne_confront",
            "throne_fight",
            "throne_fall",
            "exodus_escape",
            "convoy_defense",
            "convoy_after",
            "pact_vows",
            "epilogue",
        };

        /// <summary>Get a fresh dialogue-line array for the given set ID.</summary>
        public static World.Story.DialogueLine[] Get(string setId)
        {
            var lines = setId switch
            {
                "gathering_table" => GetGatheringTableLines(),
                "gathering_fight" => GetGatheringFightLines(),
                "gathering_pact" => GetGatheringPactLines(),
                "map_briefing" => GetMapBriefingLines(),
                "assault_approach" => GetAssaultApproachLines(),
                "assault_dogfight" => GetAssaultDogfightLines(),
                "assault_cleared" => GetAssaultClearedLines(),
                "creche_divide" => GetCrecheDivideLines(),
                "creche_combat" => GetCrecheCombatLines(),
                "creche_rescue" => GetCrecheRescueLines(),
                "beacon_lit" => GetBeaconLitLines(),
                "beacon_duel" => GetBeaconDuelLines(),
                "beacon_freed" => GetBeaconFreedLines(),
                "khall_death" => GetKhallDeathLines(),
                "throne_confront" => GetThroneConfrontLines(),
                "throne_fight" => GetThroneFightLines(),
                "throne_fall" => GetThroneFallLines(),
                "exodus_escape" => GetExodusEscapeLines(),
                "convoy_defense" => GetConvoyDefenseLines(),
                "convoy_after" => GetConvoyAfterLines(),
                "pact_vows" => GetPactVowsLines(),
                "epilogue" => GetEpilogueLines(),
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

        /// <summary>Generate clip name for a line: ep33_{setId}_{index:00}_{speaker_sanitized}</summary>
        public static string ClipName(string setId, int index, string speaker)
        {
            return $"ep33_{setId}_{index:00}_{Sanitize(speaker)}";
        }

        // ==== BEAT 1 — THE GATHERING ====

        private static DialogueLine[] GetGatheringTableLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Ten syndicate heads, one room, no shooting. If this works I'm framing it.", seconds = 2.5f },
                new DialogueLine { speaker = "Mera Voss", text = "A table. Neutral ground. That's a choice.", seconds = 2f },
                new DialogueLine { speaker = "Captain Resh", text = "You've summoned the ten.", seconds = 1.5f },
                new DialogueLine { speaker = "Gryph", text = "Ten worst people in the galaxy in one room. This story writes itself.", seconds = 2f },
                new DialogueLine { speaker = "Vess", text = "I came to kill you, once. Strange, what a table can do instead.", seconds = 2f },
                new DialogueLine { speaker = "Coral Vex", text = "That makes eleven, if we're counting the man standing at the head.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "You've heard the rumors. The Dominion sent a retrieval order that never came home.", seconds = 2.5f },
                new DialogueLine { speaker = "Soren", text = "That's because the order was forged by the Hollow Kings to fracture the Ronin Program from inside. Overseer Khall was manipulated into nearly destroying me. But the Dominion's real architect, First Overseer Maelgorn and the Obsidian Synod, still controls the failsafe network that leashes every operative alive.", seconds = 5.5f },
                new DialogueLine { speaker = "Mera Voss", text = "You hunted us for a decade, Ronin. Now you set a table. Why should ten old enemies share a knife?", seconds = 3f },
                new DialogueLine { speaker = "Soren", text = "Because the Dominion taught all of us the same lesson. They took me as a child and made me a sword to point at you. They took your people too, harvested, augmented, sold. We've been fighting each other inside a cage the Dominion built. I mean to break it.", seconds = 4.5f },
                new DialogueLine { speaker = "Coral Vex", text = "He's not lying. I'd know.", seconds = 1f },
            };
        }

        private static DialogueLine[] GetGatheringFightLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dominion Strike Leader", text = "All units converge! Target is assembled! Breach pattern seven!", seconds = 2f },
                new DialogueLine { speaker = "Dominion Trooper 1", text = "Contact! All targets in assembly! Weapons hot!", seconds = 1.5f },
                new DialogueLine { speaker = "Dominion Trooper 2", text = "Formation compromised! Retreat, retreat!", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetGatheringPactLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Soren", text = "You're safe. The collar is dead. You're people again.", seconds = 2.5f },
                new DialogueLine { speaker = "Mera Voss", text = "You could have killed those two.", seconds = 1.5f },
                new DialogueLine { speaker = "Soren", text = "They're children with chips in their heads. So was I.", seconds = 2f },
                new DialogueLine { speaker = "Captain Resh", text = "You're telling me there's a way to end the auction.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "The failsafe network that controls every operative runs from one place. The Obsidian Synod. We burn the Synod, the Dominion's nervous system dies, and every weapon they ever made stops being a slave.", seconds = 4f },
                new DialogueLine { speaker = "Gryph", text = "Ten syndicates storming the throne of the empire. I'm in for the story alone.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "The Pact is struck.", seconds = 1f },
            };
        }

        // ==== BEAT 3 — THE MAP ====

        private static DialogueLine[] GetMapBriefingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Coral Vex", text = "Three shells. Outer shield-lattice, a void-dock gauntlet, then the Synod sanctum itself.", seconds = 2.5f },
                new DialogueLine { speaker = "Coral Vex", text = "And this is the Beacon. The failsafe network's heart. If Maelgorn triggers it during the assault, every chipped operative still alive turns into a weapon aimed at us.", seconds = 4f },
                new DialogueLine { speaker = "Soren", text = "Then someone has to reach the Beacon and kill it before the rest of us reach Maelgorn.", seconds = 2.5f },
                new DialogueLine { speaker = "Khall", text = "I can reach it. I built half of it.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "He stays. He gave me back to myself. Let him pay the rest of what he owes.", seconds = 3f },
                new DialogueLine { speaker = "Khall", text = "I dismantled what I could from the inside. It was not enough. Let me end the part that still wears my fingerprints.", seconds = 3.5f },
                new DialogueLine { speaker = "Morrigan", text = "How long to reach the Beacon from the void-dock?", seconds = 1.5f },
                new DialogueLine { speaker = "Khall", text = "If the shields fall, twenty minutes. But the Beacon itself will fight us.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "Then we have twenty minutes to kill a god.", seconds = 1.5f },
            };
        }

        // ==== BEAT 4 — THE ASSAULT ====

        private static DialogueLine[] GetAssaultApproachLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "All units, maintain formation. Lattice emitters coming online, gryph, those ramming runs you wanted.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetAssaultDogfightLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Gryph", text = "That's one! Bring 'em!", seconds = 1f },
                new DialogueLine { speaker = "Maelgorn", text = "Soren. Yes, I know the name you clawed back. It changes nothing. You are property that learned to talk.", seconds = 3.5f },
                new DialogueLine { speaker = "Soren", text = "Property doesn't bring nine friends and a fleet.", seconds = 1.5f },
                new DialogueLine { speaker = "Maelgorn", text = "When I light the Beacon, every operative you spared will finish what I started.", seconds = 2.5f },
            };
        }

        private static DialogueLine[] GetAssaultClearedLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Khall", text = "He means it. Get me to the core.", seconds = 1.5f },
            };
        }

        // ==== BEAT 5 — TEN HANDS DIVIDED ====

        private static DialogueLine[] GetCrecheDivideLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Soren", text = "Coral, Morrigan, the data-spine. Seize the Synod's records before they can be purged.", seconds = 2.5f },
                new DialogueLine { speaker = "Soren", text = "Dr. Heris, Resh, the Crucible Decks. The Dominion's next harvest sleeps there.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "Mera, Vess, Gryph, Cassie-04, Sable Dross, hold the dock and carve the road to the sanctum. No one gets past you.", seconds = 3f },
                new DialogueLine { speaker = "Soren", text = "Khall and I are going to the Beacon.", seconds = 1.5f },
                new DialogueLine { speaker = "Captain Resh", text = "How many children?", seconds = 1f },
                new DialogueLine { speaker = "Soren", text = "Thousands. Get them out. That's the whole reason we came.", seconds = 2.5f },
                new DialogueLine { speaker = "Dr. Heris", text = "I put augments into bodies that small for fifteen years. Let me be the one who takes them back out.", seconds = 3.5f },
            };
        }

        // ==== BEAT 6 — THE CRÈCHE HALLS ====

        private static DialogueLine[] GetCrecheCombatLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Obsidian Trooper 1", text = "Sector three compromised! Operative-class contact!", seconds = 2f },
                new DialogueLine { speaker = "Dominion Guard 1", text = "Contact on the medical level! Defensive formation!", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetCrecheRescueLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Dr. Heris", text = "One hundred seventeen in this chamber. Moving to next.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "Beyond this is the Beacon. And likely her.", seconds = 2.5f },
                new DialogueLine { speaker = "Soren", text = "Samurai-4.", seconds = 1f },
                new DialogueLine { speaker = "Khall", text = "She was the finest after you. Maelgorn keeps her closest.", seconds = 2f },
            };
        }

        // ==== BEAT 7 — THE LIT BEACON ====

        private static DialogueLine[] GetBeaconLitLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maelgorn", text = "Watch, Soren. Watch what loyalty costs.", seconds = 2.5f },
                new DialogueLine { speaker = "Samurai-4", text = "Run.", seconds = 1f },
            };
        }

        // ==== BEAT 8 — THE DUEL ====

        private static DialogueLine[] GetBeaconDuelLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Samurai-4", text = "End this. Please end this.", seconds = 2f },
            };
        }

        private static DialogueLine[] GetBeaconFreedLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Soren", text = "The Beacon is dark. We're coming.", seconds = 2f },
                new DialogueLine { speaker = "Khall", text = "It's dark, Seven. The network's dark. No more leashes.", seconds = 2.5f },
                new DialogueLine { speaker = "Samurai-4", text = "You came back. You said you would.", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "Always.", seconds = 1f },
            };
        }

        // ==== BEAT 9 — THE DARK NETWORK ====

        private static DialogueLine[] GetKhallDeathLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Soren", text = "The children are walking free. Every operative in the Synod is unplugged now. That's the whole reason we came.", seconds = 3.5f },
                new DialogueLine { speaker = "Khall", text = "I'm sorry. For all of it.", seconds = 1.5f },
                new DialogueLine { speaker = "Soren", text = "You gave me the choice in the end. Spend the last of it knowing the children walk free.", seconds = 3f },
            };
        }

        // ==== BEAT 10 — THE THRONE ====

        private static DialogueLine[] GetThroneConfrontLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maelgorn", text = "You unplugged my network and killed my handler. Sentiment. The Dominion is not a machine you switch off, Soren. It is an idea, that some are born to be used. Kill me and ten others take my chair.", seconds = 5f },
                new DialogueLine { speaker = "Soren", text = "That's why I didn't come with only a sword. I came with the ten chairs you used to terrify the galaxy into obedience. Look who's sitting in them now.", seconds = 3.5f },
            };
        }

        // ==== BEAT 11 — THE FALL OF MAELGORN ====

        private static DialogueLine[] GetThroneFightLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mera Voss", text = "Cover left! Moving through!", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetThroneFallLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Maelgorn", text = "You think criminals will rule better than emperors?", seconds = 2f },
                new DialogueLine { speaker = "Soren", text = "No. I think no one should rule the way you did. That's the difference.", seconds = 2.5f },
                new DialogueLine { speaker = "Kessler", text = "Whatever you're doing, do it leaving. This whole tomb is coming down.", seconds = 2f },
            };
        }

        // ==== BEAT 13 — ASHES ====

        private static DialogueLine[] GetExodusEscapeLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sable Dross", text = "Power vacuum. By tomorrow every warlord and every splinter syndicate will be carving up the worlds we just freed. We didn't end the war. We started ten of them.", seconds = 3.5f },
                new DialogueLine { speaker = "Soren", text = "Then we don't let go. The reach you each built to prey on people, turn it to protect them instead.", seconds = 3f },
            };
        }

        // ==== BEAT 14 — THE FIRST DEFENSE ====

        private static DialogueLine[] GetConvoyDefenseLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Privateers Commander", text = "Convoy is undefended! Moving to intercept!", seconds = 2f },
                new DialogueLine { speaker = "Sallow", text = "Hold formation! No transport is lost!", seconds = 1.5f },
            };
        }

        private static DialogueLine[] GetConvoyAfterLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Sallow", text = "My own people did that. The ones who wanted the dark to stay dark.", seconds = 2.5f },
                new DialogueLine { speaker = "Soren", text = "Then they're your work now. Every one of you knows your own kind better than any law could. That's the point.", seconds = 3f },
                new DialogueLine { speaker = "Coral Vex", text = "A protector who knows where every body is buried. We'd be... unprecedented.", seconds = 2f },
            };
        }

        // ==== BEAT 15 — THE TENFOLD PACT ====

        private static DialogueLine[] GetPactVowsLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Mera Voss", text = "I take the trade-lanes. The routes of every synced, the hidden channels where children used to move. They move to freedom now, or they move to me.", seconds = 3.5f },
                new DialogueLine { speaker = "Morrigan", text = "I take the truth. That no lie like the one done to Soren is ever planted in an operative's mind again. The Hollow Kings' greatest strength was secrets. Now secrets are a weapon they never see coming.", seconds = 4f },
                new DialogueLine { speaker = "Captain Resh", text = "I take the children. Every harvest line hunted to its root. Every auction block burned. The Saffron Veil ran black-market operations. Now it runs orphanages.", seconds = 3.5f },
                new DialogueLine { speaker = "Dr. Heris", text = "I take the augmented. To unmake the leashes I once built. The Gilded Maw was the Dominion's surgeon. Now it's the galaxy's healer. Every operative who was chained gets the choice of free will or peace.", seconds = 4.5f },
                new DialogueLine { speaker = "Sallow", text = "I take the dead and the grieving. The monastery's doors open to all who want to learn that even weapons can be unmade. The Pale Choir sang for the powerful. Now we sing for everyone they broke.", seconds = 4f },
                new DialogueLine { speaker = "Gryph", text = "I take the lawless rim. The pits, the abandoned stations, the places where hope goes to die. The Rustfangs broke people for sport. Now we defend what's defenseless there.", seconds = 3.5f },
                new DialogueLine { speaker = "Sable Dross", text = "I take the water and the worlds that thirst. The Tide Barons drowned operatives in the deep. Now the deep protects instead of traps.", seconds = 3f },
                new DialogueLine { speaker = "Cassie-04", text = "I take the freed operatives, my own kind, scattered and waking. The Ninefold Bank measured every life in debt. Now we measure success in how many walk free.", seconds = 3.5f },
                new DialogueLine { speaker = "Vess", text = "I take the guns. Every weapon the Dominion ever made. To point them only outward now. The Emberhand remembers fire and loss. Now we burn only what would burn others.", seconds = 3.5f },
                new DialogueLine { speaker = "Coral Vex", text = "I take the watch. The eyes that never close. The Vellum Exchange trades in secrets, now we trade in truth. Every operation, every command, every scrap of intelligence gets put where it can protect, never prey.", seconds = 4f },
                new DialogueLine { speaker = "Soren", text = "You spent your lives taking. Spend the rest of them keeping.", seconds = 2f },
                new DialogueLine { speaker = "Kessler", text = "The ten worst people in the galaxy and the man they built to kill them, swearing to babysit creation. Nobody is going to believe me.", seconds = 3f },
            };
        }

        // ==== BEAT 16 — EPILOGUE: THE OPEN STARS ====

        private static DialogueLine[] GetEpilogueLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Samurai-4", text = "What now, Seven?", seconds = 1.5f },
                new DialogueLine { speaker = "Soren", text = "Now they're safe. And I get to find out who I am when no one's pointing me at anything.", seconds = 3f },
                new DialogueLine { speaker = "Samurai-4", text = "Then let's go find out.", seconds = 1.5f },
            };
        }
    }
}
