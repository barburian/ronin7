# Ch01 — The Salvager's Debt — Dialogue Audit

**Scope:** `Ch01_The_Salvagers_Debt_Dialogue_Script.md` (audit only; no edits made).
**Auditor pass:** (A) naturalness / AI-tells, (B) canon consistency.

## 1. Summary header

- **Overall naturalness grade: B+**
  Genuinely strong for the spine: laconic Ronin-7, weary Kessler, cold Handler, clipped troopers — four distinguishable registers, almost no contractions missing, real interruptions/false-starts. The one thing dragging it off A is a **cluster of "not X, it's Y" antithesis lines** plus a few repeated stock phrases.
- **Em-dashes in character speech (hard ban): 0 violations.** Every `—` in the file lives in `[stage directions]` or `voice:` notes, which is allowed. The dialogue itself is clean on this count.

| Issue type | Count |
|---|---|
| Em-dash inside a `Line:` (hard ban) | **0** |
| "Not X, it's Y" / antithesis construction | 5 |
| Over-balanced fragment-list / tricolon | 3 |
| Phrase/motif repetition across the chapter | 2 |
| Grammar / idiom wobble | 3 |
| On-the-nose or vague self-narration | 2 |
| **Naturalness total** | **15** |
| Consistency findings | 1 (low severity) + 5 positive confirmations |

---

## 2. Naturalness findings

| Beat | Speaker | Original line | Problem | Suggested rewrite |
|---|---|---|---|---|
| 1 | Kessler | "You didn't kill me. Last time we met, you haven't done that either." | Tense breaks ("Last time… you haven't"). Reads ungrammatical, not laconic. | "You didn't kill me. Last time we met, you didn't either." |
| 1 | Ronin-7 | "I don't remember this. But my hands do." | Balanced antithesis; also doubles the earlier "My hands knew what to do. I didn't tell them." — the hands motif is used twice in one beat. | Keep one. e.g. "I don't remember it. My hands do." (drop the contrastive "But"; let the second sentence land flat.) |
| 1 | Kessler | "…you were Dominion property and they have thrown you to garbage." | Non-idiomatic ("thrown you to garbage"). | "…you were Dominion property, and they threw you out with the garbage." |
| 2 | Kessler | "Cheap skin over augments somebody spent a fortune on. A throat cut and closed. A man flatlined so long the table kept telling me to stop." | Three balanced sentence-fragments in a row — over-symmetrical list, an AI cadence. | Break the symmetry: "Cheap skin over augments worth a fortune. A throat cut and sealed back up. And you'd flatlined so long the table kept telling me to quit." |
| 2 | Kessler | "Not the marks, not the witnesses, not the man who hauled the cargo and saw a face he shouldn't have." | Tricolon ("Not… not… not…"). Characterful but it's the same antithesis shape used elsewhere. | Acceptable as-is given Kessler's cadence; if trimming, cut to two beats: "Not the marks. Not the man who hauled the cargo and saw a face he shouldn't have. That last one was me." |
| 3 | Ronin-7 | "Not a sweep. They were looking for me." | Textbook "not X, it's Y" antithesis — the chapter's signature AI-tell. | "That wasn't a sweep. They came for me." |
| 3 | Kessler | "That's not how you treat a corpse. That's how you treat a mistake you thought you'd already cleaned up." | Antithesis again, back-to-back with the line above. | "You don't send armed men for a corpse. You send them for a mistake you thought you'd buried." (keeps the idea, drops the mirrored "That's… That's…") |
| 4 | Ronin-7 | "The others obeyed it without thinking. That's not a commander. That's an owner." | Antithesis. Third instance of the exact "That's not X. That's Y." mold. | "The others obeyed it without thinking. Commanders don't get that. Owners do." |
| 4 | Kessler | "And whatever doubt it had: is he really standing, is the file wrong. You just answered it. In full color." | Mid-line colon reads as prose punctuation, not speech; the embedded questions are hard to perform; "In full color" is a stranded fragment. | "And whatever doubt it had — was the file wrong, was he really standing — you just answered it. In color." (move the dash to stage-legal form, or:) "It had doubts. Was the file wrong, was he really up and walking. You just answered both." |
| 4 | Kessler | "They don't trade. They collect." | Antithesis. Strong villain-read, but it's the fifth "X not Y" of the chapter. | Strong enough to keep — but if reducing the cluster, this is the one to preserve and cut an earlier one instead. |
| 4 | Ronin-7 | "That's the debt you meant. Not the one to me." | Antithesis fragment. | "That's the debt you meant. Not me." |
| 4 | Handler | "Deliver the item, or you will never see your daughter." / next line "Iris. She's grown, since you saw her." | "your daughter" appears twice in two consecutive sentences (line ends on it; previous sentence "We have taken your daughter there"). Flat, repetitive. | "Deliver the item within three days, or she stops being something you'll ever see again." (then the "Iris." reveal lands harder.) |
| 4 | Kessler | "…they sell bodies and pasts and call it commerce." (Beat 4) AND "They sell bodies and pasts in those markets." (Beat 4 close) | Same stock phrase used twice within Beat 4, and it is also pre-stated verbatim in the SETTING block. Three hits total = pet phrase. | Vary the second: "Velorum's a long burn and a worse welcome. Half of what's for sale there used to be someone." |
| 4 | Kessler | "She was in a Dominion school, but they probably took her in Velorum's markets…" | "probably" — Kessler is vague about where his own daughter is, after six years living this. Undercuts the grief. | "She was in a Dominion school. Then they pulled her into Velorum's markets, where they sell bodies and pasts and call it commerce." |
| 4 | Ronin-7 | "Then it's simple. You give them what they came for. Me." | Mild on-the-nose framing ("Then it's simple" announces the beat). Minor. | "Then you give them what they came for. Me." (drop the signposting clause.) |

**Note on what's working (so it isn't lost in edits):** the short interrogatives ("What part." / "Owed who." / "What man.") and the broken-rhythm reveals ("Six years ago you did.") are excellent and very un-AI. The fix for the antithesis cluster is *thinning* it (keep one or two, vary the rest), not flattening every line.

---

## 3. Consistency findings

**Positive confirmations (canon respected):**
1. **Codename "Cipher" correctly absent.** Ch01 predates the crew hearing "Cipher" (Ch4). The Handler refers to Ronin-7 only as "the item," "something of ours," "the asset" — never a codename. Speaker labels use the serial "Ronin-7" (narration-only), per Bible §0/§3. ✔
2. **Khall correctly unnamed.** The handler is labeled "Handler (Hologram)" / "Comm (V.O.)" and is never named; naming is reserved for Ch4 (Ladder D rung 1). ✔ (`GAME_STORY_SO_FAR` Khall entry: "Appears unnamed as the command-room hologram in Ch1.")
3. **Katana correctly dormant / unrevealed.** Treated as "a plain katana," "feels like yours," "my hands do" — no shadow-AI dialogue, no memory-vessel claim. Matches `00b_SWORD_AI_RECON.md` §6 ("AI nature not yet revealed") and `GAME_STORY` Ch01 ("its AI nature not yet revealed"). ✔
4. **Ship naming correct.** Kessler calls it "my ship" / "the rig," never "The Cairn." Bible §5: "in dialogue he calls it 'the rig'." ✔
5. **Timeline anchors correct.** Six-years-ago mercy, ~three-weeks revival, three-day ultimatum, Velorum as destination — all match Bible §9 master timeline. ✔

**Issue (low severity):**

| Location (beat) | Issue | Canon ref | Fix |
|---|---|---|---|
| Beat 4 — Handler "We have taken your daughter there" vs. Kessler "Six years I've scraped this dead field for enough to buy her back" / "She was in a Dominion school, but they probably took her in Velorum's markets" | Soft tension on **when/where Iris is held.** Bible §5 / Codex: Iris was "sold into Velorum's markets and held as collateral" (a standing hold). The chapter instead frames her as in "a Dominion school" until "tonight they remembered" and moved her to Velorum — so the leverage is *reactivated* now, not standing. The chapter resolves this internally (Kessler: "before they remembered they could just use her on me again. And tonight they remembered"), but the "Dominion school" detail is new-to-canon and the "buy her back" line implies a longer-running captivity that rubs against "took her there" tonight. | `00_STORY_BIBLE.md` §5 (Iris); `GAME_STORY_SO_FAR.md` Iris/Ch01 entries | Not a hard contradiction — leave the beat, but tighten Kessler's line (see naturalness row, drop "probably") so the school→market move reads as deliberate Dominion leverage rather than Kessler guessing. If the writers' room wants a clean match to "held as collateral," cut "Dominion school" and have her already in the markets, with tonight being the Dominion *reactivating* the threat. |

No violations found on: katana-as-shadow-AI (correctly hidden), Kethel-7 (not referenced), Coral Vex / Ronin-template / Heris (not referenced), or voice/accent consistency (all four registers hold across the chapter).

---

## 4. Prose summary — biggest recurring problems

This is a well-built chapter and an easy fix. The em-dash hard ban is fully respected (zero in dialogue), the four voices are distinct, and the laconic exchanges (Ronin-7's one- and two-word questions, Kessler's beat-then-land confessions) are the opposite of AI mush.

The one systemic tell is the **"not X, it's Y" antithesis pattern**, which fires at least five times — "Not a sweep. They were looking for me," "That's not how you treat a corpse. That's how you treat a mistake," "That's not a commander. That's an owner," "They don't trade. They collect," "Not the one to me." Individually most of them are good lines; collectively they become a recognizable rhythm, especially in Beats 3–4 where three land within a few exchanges. The fix is to keep the one or two strongest (the "owner" and "they collect" reads) and rephrase the rest so the chapter stops resolving its dramatic turns with the same grammatical mirror.

Two smaller recurring issues: **stock-phrase repetition** ("they sell bodies and pasts and call it commerce" appears twice in Beat 4 plus once in the SETTING block) and a **trio of grammar/punctuation wobbles** ("Last time we met, you haven't done that either," "thrown you to garbage," and the prose-colon in "And whatever doubt it had: is he really standing"). On canon, the chapter is clean: it correctly withholds the Cipher codename, keeps Khall unnamed, and keeps the katana's AI dormant — the only soft spot is the slightly fuzzy account of where Iris has been held, which the dialogue itself nearly resolves and a one-word trim ("probably") finishes off.
