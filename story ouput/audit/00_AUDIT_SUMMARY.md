================================================================================
RONIN-7 — DIALOGUE SCRIPT AUDIT: CROSS-CHAPTER SUMMARY
Naturalness ("AI-like" tells) + Canon Consistency
================================================================================
Scope: 14 dialogue scripts (Ch01–Ch13, Ch16). One Opus agent per chapter.
Audit only — no scripts were modified. Per-chapter detail in ChNN_audit.md.
Ch14 (The Cage) and Ch15 (The Vault Within) have NO dialogue scripts and were
not audited.
================================================================================

## SCORECARD

| Ch | Title                  | Grade | Em-dash in speech | "not X, it's Y" | Hard script/canon errors |
|----|------------------------|-------|-------------------|-----------------|--------------------------|
| 01 | The Salvager's Debt    | B+    | 0                 | 5               | 0 (1 soft)               |
| 02 | The Auction            | B     | 0                 | 6               | 2 (continuity + corrupt) |
| 03 | The Sword Remembers    | B-    | 0                 | 8               | 0                        |
| 04 | The Overseer's Hunt    | B-    | 0                 | 4               | 0 (1 soft)               |
| 05 | The Debt of Ashes      | C+    | 0                 | 9               | 0                        |
| 06 | The Iron Dojo          | C-    | 0                 | 13              | 0 (1 soft)               |
| 07 | Forgotten Names        | C     | 1 (L301)          | 14              | 0                        |
| 08 | The Silent Garden      | C     | 0                 | 7               | 0                        |
| 09 | The Pit and the Deep   | C     | 2 (L432, L492)    | 11              | 1 (+2 minor)             |
| 10 | The Ledger of Rust     | B-    | 0                 | 8               | 1 (broken line 430)      |
| 11 | Ghosts and Origins     | C+    | 0                 | 18              | 0                        |
| 12 | The Fracture           | C+    | 0                 | 18              | 1 (+1 minor)             |
| 13 | The Sterile Reckoning  | C-    | 0                 | ~22             | 1 (+1 soft)              |
| 16 | The Throne of Ashes    | C-    | 0                 | ~28             | 1 medium (+3 minor)      |

Em-dash-in-speech violations total: **3 lines across 2 chapters** (Ch07 ×1,
Ch09 ×2). The hard ban is otherwise honored saga-wide.

================================================================================
## THE HEADLINE FINDING

The dialogue does read "AI-like," and it is overwhelmingly **one root cause
expressed four ways** — not fourteen different problems. Fixing the pattern
below would lift nearly every grade.

### 1. The "Not X. It's Y." antithesis is the saga's default sentence shape
This single construction is the dominant AI-tell in EVERY chapter, and its
density rises with chapter length: ~5 in Ch01, ~28 in the Ch16 finale, ~180+
total. Examples across the saga:
- "That's not a commander. That's an owner." (Ch01)
- "These aren't records. They're people." (Ch02)
- "It's not taste. It's wiring." (Ch03)
- "not an exception, an edition" (Ch11)
The problem isn't any single instance — a few are good. It's that the SAME
mirror fires in nearly every speaker's mouth, so the cast collapses into one
rhythm. **Fix is subtractive: keep 1–2 per chapter, rewrite the rest as plain
statements, interruptions, or questions.**

### 2. Aphorism-stacking — every line is a quotable maxim
Beats consistently end on a polished, screenshot-ready epigram. Stress speech
isn't this curated. The Ch13 and Ch16 reveals stack 3–4 maxims where one should
land, diluting the line that matters. Voice notes occasionally *brag* about this
("the whole chapter's thesis in five words"), which is the tell naming itself.

### 3. Register uniformity — everyone sounds the same
A child, a brute drillmaster, a smuggler, a million-year-old oracle, and the
laconic protagonist all speak in the same balanced, sententious cadence. With
speaker labels removed, most lines are swappable. The intended contrasts (warm
Echo / terse Ronin; alien Mourners vs. crew) are eroded because the secondary
cast was written *up* to the leads' polish. **Differentiate by roughing up the
non-leads: fragments, false starts, contractions, plain words, dropped
threads.** (Ch10 already does this well with Sever/Cassie — use it as the model.)

### 4. On-the-nose theme-stating
Characters narrate the meaning of their own scene aloud ("A weapon that grieves.
That's the thing they can't allow"; "I'm done being the thing that takes";
Echo reciting the Story Bible logline verbatim in Ch16). The imagery already
delivers it; the line should be implied, not spoken.

Secondary shared tics: tricolons ("No walls. No guns. No garrison."), and
pet-phrases bleeding across characters ("hand on the strings" in Ch06 across 3
speakers; "the living and the loud" handed verbatim between Mourners and Ronin
in Ch08).

### Grade trend
Early chapters (Ch01–Ch04) hold the B range; the middle and late chapters drift
to C/C- as tic-density climbs with length. The writing is *skilled* — the fault
is over-polish, not weakness.

================================================================================
## CANON CONSISTENCY — STRONG, with a short fix list

Consistency is a genuine strength. Across all 14 chapters the big retcons hold:
codename "Cipher" usage (Echo never says "7"; "Ronin-7" stays narration-only),
sword = Cipher-only shadow-AI, Kethel-7 = planet/mission, Coral = Wraith-line
forebear, the Ch11→Ch12 prime-template inversion, Heris scoped to the Ronin line
+ Engine, the generation→ability node chain, and the Hub briefings.

### Hard errors to fix (real script defects)
1. **Ch02, Beat 4** — Resh says "get the kid and Kessler's girl home," but Mira
   isn't discovered until Beat 5 and his own later shock proves he doesn't know
   she exists. Fix: drop "the kid and."
2. **Ch02, Beat 2** — corrupted/garbled text block (dropped characters: "how i
   ays ends," "Resh, cor ," "hal econd"). Needs restoration.
3. **Ch10, line 430** — Ronin's surrender line has typos ("i am",
   "abonimation", "dominion"), is the only block missing its `voice:` note, and
   clashes in register with his voice elsewhere.
4. **Ch13, Beat 2 (line 422)** — the iconic "I hope you will save us" whisper
   (the entire Ch08 payoff) is described in Heris's voice note but MISSING from
   her actual `Line:` text; the 57s duration is mismatched to the ~12 words
   that remain.
5. **Ch12, Beat 0** — Ronin answers "And Kessler. I heard you" and two voice
   notes claim the beat answers Kessler, but Kessler has no line in the beat.
   (Minor: Gryph speaks on comm in Beat 1 but isn't in that beat's comm-holder
   list.)
6. **Ch16 (medium)** — Samurai-4 is called a "newer make" than Ronin, but canon
   makes Ronin the current/newest make (Knight→Ninja→Wraith→Ronin); this
   implies an undocumented fifth program. (Minor: Samurai-4 addresses him
   "Ronin-7" in dialogue, which convention reserves for narration.)
7. **Ch09 (moderate)** — Gryph pledges to dive personally in Beat 2 but is
   comm-only while Ronin dives alone in Beat 3. (Minor: Ronin calls the Cairn
   "my ship"; it's Kessler's.)

### Soft watch-items (hold for later drafts)
- **Ch06** — Morrigan's "one architect / all of it" Heris-seed should be scoped
  to the Ronin line so it doesn't imply a single architect of all makes
  (contradicts the Heris retcon).
- **Ch04** — Iris's "someone high… inside the firmware" edges toward Ch7's
  internal-dissent thread; she leaves WHO open, so it holds.
- **Ch01** — Iris's whereabouts ("Dominion school" vs. standing Velorum
  collateral); the dialogue nearly self-resolves it.

================================================================================
## RECOMMENDED FIX ORDER

1. **Quick wins (correctness):** the 7 hard script errors above — small, local,
   unambiguous edits.
2. **The big naturalness pass (highest impact):** a saga-wide thinning of the
   "not X, it's Y" antithesis and aphorism buttons. Target the late chapters
   (Ch11–Ch13, Ch16) first — that's where density is worst.
3. **Voice differentiation:** rough up the secondary cast chapter by chapter;
   use Ch10's Sever/Cassie lines as the in-canon style reference.
4. **Soft canon watch-items:** fold in during the same edit pass.

Per-chapter line-level rewrites (with proposed replacements) are in each
ChNN_audit.md.
================================================================================
