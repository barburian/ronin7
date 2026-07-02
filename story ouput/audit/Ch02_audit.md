# Ch02 "The Auction" — Dialogue Audit

**File audited:** `D:\ronin7\story ouput\Ch02_The_Auction_Dialogue_Script.md`
**Audit type:** AUDIT ONLY (no edits made to the script).

## 1. Header

- **Chapter:** RONIN-7 — Chapter 2: The Auction
- **Overall naturalness grade:** **B**
- **Why not higher:** strong, distinct character voices and a hard pass on the em-dash ban, but a steady drumbeat of "not X, it's Y" antithesis and quotable-maxim buttons (heaviest on Resh) keeps the dialogue from feeling fully spoken rather than written.
- **Why not lower:** voices are genuinely differentiated, contractions are mostly present, interruptions/false starts are used well, and the chapter avoids the worst AI tells.

### Issue counts by type
| Type | Count |
|---|---|
| Em-dash in `Line:` (hard ban) | **0** |
| "Not X, but Y" / "It's not X. It's Y." antithesis | 6 |
| Tricolon / over-balanced symmetry | 3 |
| Aphorism / quotable-maxim button | 4 |
| On-the-nose emotion / significance-flagging | 2 |
| Missing contractions (formal register) | 4 |
| Redundant exposition (repeats earlier beat) | 2 |
| **Naturalness total (incl. mild)** | **~13 lines flagged** |
| Consistency — continuity errors | 1 |
| Consistency — production/text integrity | 1 (a block of garbled text) |

**Note on the em-dash ban:** every `Line:` was scanned. No em-dash (—) appears inside character speech. The em-dashes present are confined to `[stage directions]` and `voice:` notes, which the brief permits. Full compliance on this rule.

---

## 2. NATURALNESS FINDINGS

| Beat | Speaker | Original line (quoted) | Problem type | Suggested rewrite |
|---|---|---|---|---|
| 0 | Kessler | "Because you've got a switch in your skull and a clock on your neck and nowhere else to start." | Tricolon / over-balanced symmetry | "Because you've got a switch in your skull and a clock on your neck. And nowhere else to start." (break the rule-of-three rhythm, or drop the third item) |
| 1 | Kessler | "Top tier sells what shines. Weapons, ships, wonders." | Tricolon + redundant exposition (he already gave the up-top/down-bottom split in Beat 0) | Cut or compress: "Top tier sells what shines. The deeper you go, the worse what's for sale." Drop the "weapons, ships, wonders" list since it repeats Beat 0. |
| 1 | Broker | "That's not the problem. The problem is what you're asking to buy. ... the vault doesn't open for money. It opens for trust, and I don't have any to spend on you." | "Not X, it's Y" antithesis (twice in one line) | "Money's not what gets you in. That vault wants trust, and I've got none to spend on you." |
| 1 | Kessler | "He's a wall. We don't go through him. We go around." | "Not X, but Y" antithesis | "He's a wall. So we go around him." |
| 1 | Kessler | "...and the file's still locked behind a dead man." | Clarity (the broker is alive; reads as a contradiction) | "...and the file's still locked behind glass we can't cut." |
| 2 | Resh | "Men like you don't spare. You spend." | Aphorism / antithesis button | "Men like you don't spare anybody. You use them up." |
| 2 | Ronin-7 | "I came here for a route. Not a recruit." | "Not X" antithesis | "I came here for a route. That's all." |
| 2 | Resh | "I am the way past that broker. But I'm not selling you a route and waving you off. ... Not because I owe you. Because you're the first crack I've seen in a wall I've spent twenty years smuggling around." | Aphorism-stacking + antithesis + self-repetition ("the way past that broker" twice); also "I am" → "I'm" | Thin it out: "I'm your way past that broker. But I don't want paying off and waved away. I want in. You're the first crack I've seen in a wall I've spent twenty years smuggling around." |
| 2 | Kessler | "This isn't a stall to rob. It's the Dominion." | "It's not X. It's Y." antithesis | "This is bigger than robbing a stall. This is the Dominion." |
| 4 | Iris | "These aren't records. They're people." | "Not X, they're Y" antithesis | "These aren't records. God. These are people." (a beat/interjection breaks the clean flip) — or "Every one of these is a person." |
| 4 | Resh | "I've spent my whole life smuggling the living out through a building that runs on the dead." | Aphorism button (mild) | Acceptable for Resh's idealist register; if trimming, drop the matched "living/dead" balance: "I've spent my whole life pulling people out of a place that's built on bodies." |
| 4 | Iris | "Do you understand what that means? Every one of you has this." | On-the-nose significance-flagging | Let the fact carry it: "Every one of you has this. A leash in the skull. Yours is the only one on record that failed." (cut the "do you understand" prompt) |
| 5 | Resh | "A salvage rat, a freed tech, a stowaway, and me. The market's whole lost-and-found, riding home on one ship." | On-the-nose "ragtag crew" button + tricolon list | Keep the wry list but soften the tidy button: "A salvage rat, a freed tech, a stowaway, and me. Hell of a manifest." |

**Missing contractions (register slips — collected):**
- Beat 2, Resh: "men like me **do not** get found" → "don't"
- Beat 2, Resh: "**I am** worth more to this market alive" → "I'm"
- Beat 5, Kessler: "**I am not** in the business of turning a child back toward one" → "I'm not"
- Beat 5, Resh: "you **do not** get to be down here" → "you don't"
*(Several of these are arguably intentional emphasis; flagged for consistency since the rest of the chapter contracts freely.)*

**Lines that read as strong/natural (kept for contrast):** Kessler's "And let me do the talking. You have a way of ending conversations."; Resh's "That's exactly what the last one said."; Kessler's "And yet."; Mira's "I saw you on the floor. You weren't running. You were with them. So I followed."; Ronin-7's clipped "Why." / "Go." / "No." beats, which land his casting note (deep, flat, arithmetic) cleanly.

---

## 3. CONSISTENCY FINDINGS

| Location (beat) | Issue | Canon reference violated | Fix |
|---|---|---|---|
| Beat 4, Resh: "Let's get **the kid** and Kessler's girl home before this market notices..." | **Continuity error.** Mira (the only "kid") is not discovered until Beat 5 — she is a silent background plant in Beat 2 the crew never notices, and Resh's own shock in Beat 5 ("How did you get past me") proves he has no idea she exists. He cannot reference "the kid" here. "Kessler's girl" = Iris is correct, but "the kid" is a person who, in-story, the crew does not yet know about. | Internal SETTING continuity (Beat 2 plant: "no line… loses her in the press"; Beat 5: stowaway *reveal*). Also `GAME_STORY_SO_FAR.md` Ch02 ("they find Mira… who followed Resh's legend") places the discovery after the vault. | Change to: "Let's get Kessler's girl home before this market notices what it just sold us for free." (Remove "the kid and".) |
| Beat 2, CONVERGENCE block | **Production/text-integrity error.** Several lines and stage directions are garbled with dropped characters: Resh's line "That's how **i  ays ends** with your kind" (should read "how it always ends"); stage dirs "Resh, **cor  ,**", "eyes shut for **hal  econd**", "Reads **h  e way** he reads everything", "No drama in **i  ly a door** held open", "the certainty he braced **ag   just** didn't arrive". | Not a canon-fact issue, but it breaks readability of a key emotional beat (the spare). | Restore the intended text, e.g. "That's how it always ends with your kind."; "Resh, cornered,"; "eyes shut for half a second"; "Reads him the way he reads everything"; "No drama in it. Only a door held open."; "the certainty he braced against just didn't arrive." |

### Consistency checks PASSED (worth recording)
- **Codename timeline:** No character says "Cipher." Ronin-7 is referred to only as "the operative" / by serial in speaker labels. Correct — "Cipher" is not spoken until Ch4 (`00_STORY_BIBLE.md` §0, Ladder D rung 1). ✔
- **Katana = dormant shadow-AI, unrevealed:** the blade stays wrapped and silent throughout; no AI/Echo/voice, and no "memory-vessel" language. Correct — the AI wakes in Ch3 (`00b_SWORD_AI_RECON.md`; bible §3). ✔
- **Killswitch reveal (Ladder A rung 1):** Iris's "The killswitch discharged. Full sequence… it didn't take… you just kept breathing," and Ronin-7's "Mine's the only one they've found that failed" deliver exactly rung 1 (fired and failed; he survived his own execution) without over-disclosing later rungs (sabotage = Ch4). Correct. ✔
- **The 3-day clock:** "The voice on the comm gave three days" (Beat 0) and Kessler's "Three days, that voice gave me" (Beat 3) carry forward Ch1's Khall ultimatum consistently. ✔
- **Allies stay aboard the ship (the Hub):** Mira is explicitly kept aboard ("You stay with the ship"; "She stays aboard, with me"), and the crew closes the chapter together on Kessler's ship. Correct (`MEMORY.md` allies-aboard; bible §4). ✔
- **Resh's loyalty hook:** "Not because I owe you" — he joins to strike the Dominion at its source, not to repay a debt. Matches bible §4 #1 exactly. ✔
- **Iris = Kessler's daughter, freed here; six-year separation:** consistent with bible §5 / §9. ✔
- **Final image hand-off to Ch3:** the wrapped katana laid on the rack, room now full — matches Ch3's stated open. ✔

---

## 4. Summary — the chapter's biggest recurring problems

The chapter is well above the AI-slop line: voices are distinct (Kessler's dry gravel, the broker's reedy panic, Resh's frictionless trader-patter cracking open into the idealist, Ronin-7's clipped arithmetic), contractions flow, and there is not a single em-dash inside character speech.

The one persistent craft tic is the **antithetical maxim** — "not X, it's Y" / "don't go through, go around" / "don't spare, you spend" / "these aren't records, they're people." It appears at least six times, and because each one is shaped as a clean, quotable button, the cumulative effect is that characters keep landing punchlines instead of talking. Resh carries the most of these; thinning two or three of his would sharpen rather than dull him. A secondary tic is **aphorism-stacking on the crew-forming beats** (Beat 2's recruitment, Beat 5's "lost-and-found"), where the writing reaches for a summarizing epigram the scene doesn't need.

On consistency the chapter is clean on every canon axis — codename timeline, dormant katana, the killswitch reveal, the three-day clock, allies-aboard, Resh's hook — with **one true continuity bug**: Resh references "the kid" in Beat 4, before Mira is discovered in Beat 5, which contradicts his own surprised reaction at the stowaway reveal. There is also a **block of corrupted text** in the Beat 2 convergence (dropped characters in Resh's spare-line and surrounding directions) that should be restored before this script is used.
