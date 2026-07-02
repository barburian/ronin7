using System;
using System.Collections.Generic;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Echo's line pools per event kind, widened as chapter-completion flags land. Pre-Ch3 pools are
    /// empty: Echo hasn't woken or been named yet (she's a dormant shadow-AI in the katana until
    /// Ronin names her in Ch3 — see "story ouput/00b_SWORD_AI_RECON.md"), so she's silent. Echo never
    /// calls him "Ronin-7"; she starts naming him "Cipher" once the Ch4 pools unlock. Voice: terse,
    /// protective, a wry witness — dry warmth, not a cold machine.
    /// </summary>
    public static class EchoLines
    {
        public const string EnemyKilled = "enemy_killed";
        public const string PlayerHurt = "player_hurt";
        public const string AbilityActivated = "ability_activated";
        public const string IdleHint = "idle_hint";

        public static Dictionary<string, string[]> PoolsFor(Func<string, bool> hasFlag)
        {
            var pools = new Dictionary<string, string[]>
            {
                { EnemyKilled, Array.Empty<string>() },
                { PlayerHurt, Array.Empty<string>() },
                { AbilityActivated, Array.Empty<string>() },
                { IdleHint, Array.Empty<string>() },
            };

            if (hasFlag == null || !hasFlag("ch3_complete"))
            {
                return pools; // silent: not yet named/woken
            }

            pools[EnemyKilled] = new[]
            {
                "Down. Keep moving.",
                "One less. Eyes up.",
                "Clean. Don't linger.",
                "That's the last of them.",
                "Good. Next.",
                "Dropped. You're still exposed.",
            };
            pools[PlayerHurt] = new[]
            {
                "You're bleeding. Fall back.",
                "That one's going to slow you down.",
                "Guard up. You're hurt.",
                "Watch your footing, you're hit.",
                "Breathe. You've taken worse.",
            };
            pools[AbilityActivated] = new[]
            {
                "There it is.",
                "Good. Use it.",
                "Felt that.",
                "That's the edge you needed.",
            };
            pools[IdleHint] = new[]
            {
                "Something's off here.",
                "Keep your guard up.",
                "Quiet. Too quiet.",
                "Stay sharp.",
            };

            if (hasFlag("ch4_complete"))
            {
                // Ch4 names him Cipher; Echo never says "Ronin-7", and starts using his real name here.
                pools[EnemyKilled] = Concat(pools[EnemyKilled], new[]
                {
                    "That's for Cipher. Move.",
                    "Cipher, eyes up. Next wave.",
                });
                pools[IdleHint] = Concat(pools[IdleHint], new[]
                {
                    "Cipher. Focus.",
                });
            }

            if (hasFlag("ch7_complete"))
            {
                pools[PlayerHurt] = Concat(pools[PlayerHurt], new[]
                {
                    "I've seen you take worse than this and still stand.",
                });
                pools[AbilityActivated] = Concat(pools[AbilityActivated], new[]
                {
                    "Others carry this too. Use it well.",
                });
            }

            if (hasFlag("ch9_complete"))
            {
                pools[EnemyKilled] = Concat(pools[EnemyKilled], new[]
                {
                    "Another one that won't report back.",
                });
                pools[IdleHint] = Concat(pools[IdleHint], new[]
                {
                    "The lattice is close. I can feel it.",
                });
            }

            return pools;
        }

        private static string[] Concat(string[] a, string[] b)
        {
            var result = new string[a.Length + b.Length];
            a.CopyTo(result, 0);
            b.CopyTo(result, a.Length);
            return result;
        }
    }
}
