using System.Collections.Generic;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Flow
{
    public enum KesslerOutcome { FirstRun, DiedRust, DiedProgram, DiedGarden, Won, WonHeat }

    /// <summary>One spoken line. <see cref="Clip"/> is the Resources/RunVoice clip name (see RunVoiceManifest).</summary>
    public readonly struct RunVoiceLine
    {
        public readonly string Speaker, Text, Clip;
        public RunVoiceLine(string speaker, string text, string clip) { Speaker = speaker; Text = text; Clip = clip; }
    }

    /// <summary>
    /// P6-04: Kessler's run-end lines (menu, after the summary) and the boss intro / phase-change lines.
    /// Mirrors the ChNNLines shape (static bank, clip names derived from keys) but lives in Flow because
    /// DialoguePlayer sits in World, which Flow cannot see; <see cref="RunVoice"/> publishes the same
    /// <see cref="SubtitleLine"/> event, so SubtitleDisplay shows these identically. Content is drawn from
    /// the story bible: Kessler the salvager (The Cairn, "the rig", Iris, the six-year mercy debt), the Rustfang
    /// free-hold, the Program, the Silent Garden, the Coil, the Obsidian Synod and Maelgorn.
    /// </summary>
    public static class KesslerRunLines
    {
        private const string K = "Kessler";

        private static readonly string[][] Kessler =
        {
            // FirstRun
            new[]
            {
                "First dive's always the ugliest, Cipher. You're back on the table, and that's the part that counts.",
                "Easy. I pulled you out of the Fracture once, I'll do it again. The rig's still warm, and so am I.",
                "Six years ago you spared me when the book said don't. Least I can do is stitch you up twice.",
                "Breathe. Everybody dies the first time down there. Most just don't wake up on a salvager's table after.",
            },
            // DiedRust
            new[]
            {
                "The Rustfang chews up better fighters than you. Gryph's old hold is carved into those caves. Learn the pits.",
                "Coil blades in the dark, Cipher. Watch your flanks first, the front will take care of itself.",
                "Rust took you. Fine. It takes everybody on the first pass. Sit down, drink something, go again.",
                "You were still counting exits when they closed. Don't count. Listen for the edge.",
            },
            // DiedProgram
            new[]
            {
                "The Program built that place to erase you, Cipher. Don't walk its halls like you belong there.",
                "Cold rooms, clean lines, no mercy. That's the Program talking. Answer it with your own pace.",
                "Their guards swing like they were taught. Because they were. Find the rhythm, then break it.",
                "I've seen what the Program does to operatives. Don't hand it a second sample of you. Go again.",
            },
            // DiedGarden
            new[]
            {
                "The Silent Garden gets in your head. Khall's grief took root in there too. Don't let it take yours.",
                "You made it to the Garden. That's further than most. The last stretch is the one that bites.",
                "Quiet down there, isn't it. Too quiet. Hear the bloom before it hears you.",
                "One more breath, Cipher. The Garden doesn't hate you. It just doesn't care. Make it care.",
            },
            // Won
            new[]
            {
                "You closed it, Cipher. The Fracture's quiet. I'm not ashamed to say I held my breath the whole way.",
                "Maelgorn's machine stalled for a day. The war is the product, and you just broke the line. Good work.",
                "Iris would have loved to see that. Hell, so would I. Come aboard, the rig is lit up like a festival.",
                "Mercy as rebellion. That's what you do, Cipher. Rest now. The Cairn has your back.",
            },
            // WonHeat
            new[]
            {
                "With the heat turned all the way up? The Synod will feel that one in its ledgers, Cipher.",
                "Hardest run I've watched, and you walked off it. The Ten Syndicates can keep their wars. We keep you.",
                "You paid the full price and came out standing. Save some luck for the next dive.",
                "That's not a run, that's a statement. The Cairn is still ringing. Somebody tell Velorum.",
            },
        };

        private static readonly string[] OutcomeKeys = { "first", "rust", "program", "garden", "won", "wonheat" };

        // Boss lines by sector 0..2 (Warden, Vane / Wraith-6, Maelgorn). Phase 1 = second phase, 2 = third.
        private static readonly string[] BossSpeaker = { "Warden", "Vane", "Maelgorn" };
        private static readonly string[] BossIntroText =
        {
            "I keep the gate, operative. Nobody crosses the Rustfang without my leave.",
            "Operative detected. Designation Ronin. Return to the Program, or be erased.",
            "You are a weapon I allowed to wander, Ronin-7. War is a harvest, and you are late to it.",
        };
        private static readonly string[][] BossPhaseText =
        {
            new[] { "The gate does not break, and neither do I. Come, feel the weight of it." },
            new[] { "Erasure incomplete. Switching to pursuit." },
            new[]
            {
                "Mercy is a tax I have never paid. Let me collect it from you.",
                "Ten syndicates, a thousand wars, and you think one blade ends it? Then end it, Soren.",
            },
        };

        public static RunVoiceLine[] Variants(KesslerOutcome o)
        {
            string[] t = Kessler[(int)o];
            var r = new RunVoiceLine[t.Length];
            for (int i = 0; i < t.Length; i++)
                r[i] = new RunVoiceLine(K, t[i], $"runvoice_kessler_{OutcomeKeys[(int)o]}_{i:00}");
            return r;
        }

        public static RunVoiceLine BossIntro(int sector)
        {
            int s = Mathf.Clamp(sector, 0, 2);
            return new RunVoiceLine(BossSpeaker[s], BossIntroText[s], $"runvoice_boss{s}_intro");
        }

        /// <summary>Line for the boss entering its <paramref name="phase"/>-th phase (BossPhaseChanged.Phase: 1 = second phase). Null if it has none.</summary>
        public static RunVoiceLine? BossPhase(int sector, int phase)
        {
            if ((uint)sector > 2 || phase < 1 || phase > BossPhaseText[sector].Length) return null;
            return new RunVoiceLine(BossSpeaker[sector], BossPhaseText[sector][phase - 1], $"runvoice_boss{sector}_phase{phase}");
        }

        /// <summary>Every line in the bank, for the VO manifest export and tests.</summary>
        public static IEnumerable<RunVoiceLine> AllLines()
        {
            foreach (KesslerOutcome o in System.Enum.GetValues(typeof(KesslerOutcome)))
                foreach (var l in Variants(o)) yield return l;
            for (int s = 0; s < 3; s++)
            {
                yield return BossIntro(s);
                for (int p = 1; BossPhase(s, p) is RunVoiceLine l; p++) yield return l;
            }
        }

        /// <summary>Win with Heat beats win; first-ever run beats the per-sector loss lines; else the sector of the death room (5 rooms each).</summary>
        public static KesslerOutcome Outcome(bool won, int depth, bool firstRun, int heat)
        {
            if (won) return heat > 0 ? KesslerOutcome.WonHeat : KesslerOutcome.Won;
            if (firstRun) return KesslerOutcome.FirstRun;
            int sector = Mathf.Clamp(depth / RunMapGenerator.NodesPerSector, 0, RunMapGenerator.SectorCount - 1);
            return KesslerOutcome.DiedRust + sector;
        }

        /// <summary>Random variant that never equals <paramref name="last"/> (-1 = none yet). <paramref name="rand"/> is any non-negative int.</summary>
        public static int NextVariant(int count, int last, int rand)
        {
            if (count <= 1) return 0;
            if (last < 0 || last >= count) return rand % count;
            int v = rand % (count - 1);
            return v >= last ? v + 1 : v;
        }
    }
}
