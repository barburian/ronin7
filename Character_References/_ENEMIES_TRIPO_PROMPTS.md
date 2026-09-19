# RONIN-7 — ENEMY PREFAB PROMPTS (Tripo text-to-3D)

Ready-to-paste prompts for the **repeatable combat enemies** — the rank-and-file / faction / non-named
bodies fought in waves. The 37 **named** story characters (Aldric, Sever, Vane, the Edition, Samurai-4,
the Dojo masters, Kerrax, the Warden, Maelgorn, etc.) are **not** here — they already have sheets +
image prompts in `Characters2D/_manifest.json` and rows in `_TRIPO_QUEUE.md`.

Most of the ~34 `EnemyDefinition` stat assets in `Project/Assets/Ronin7/Data/` reuse the **same body**,
so they collapse into the **12 distinct visual archetypes** below. Generate 12 meshes, not 34
near-duplicates; the "Serves" line lists which stat assets reuse each one.

## How to use
1. Unity → **Window ▸ AI ▸ Generate ▸ 3D Object** (Mesh Generator) → switch to **Text** input (Tripo
   text-to-3D). *(If text output comes out off-model, fall back to the proven path: paste the same
   Subject text into a flux image prompt, generate a PNG, then run Tripo **image**-to-3D — see
   `Characters2D/_manifest.json` for the flux framing wrapper.)*
2. Paste one **Prompt** block below → **Generate**.
3. Save the result prefab to the listed **Output** path (new `Enemies/` category, matching the
   `Named/` and `Diversity/` split). Tick the box when done.

## Style wrappers (already baked into each block)
- **Humanoid:** `3D game-ready character model, full body, single character in a symmetrical T-pose,
  clean topology, stylized-realistic grounded sci-fi, PBR textures, plain neutral background. <subject>.
  Color palette: <palette>. Weight and quiet menace over spectacle — "samurai elegy in a dead-tech galaxy."`
- **Object (drone / ship / construct):** `3D game-ready model, single object, clean topology,
  stylized-realistic grounded sci-fi, PBR textures, plain neutral background. <subject>. Color palette: <palette>.`

Global art anchor (`00_STORY_BIBLE.md` §2): rust, bone, ash, iron, black water, grave-grey fog. Program
operatives carry a **katana with a single lit hilt** (a shadow-AI core in the pommel) — one glow accent.
Species anatomy per `_SPECIES_BIBLE.txt`; note that species/skin are canon-blank for grunts, so palette
latitude per faction is intentional.

---

## 1. Dominion Trooper
**Serves:** Ch1 boarding troopers · `Ch13LabSecurity` (lab variant) · `Ch16Warden` (finale grunt — re-tint obsidian for the throne-core)
**Output:** `- [ ] Assets/Ronin7/Art/Generated/Characters3D/Enemies/Dominion-Trooper.prefab`

> 3D game-ready character model, full body, single character in a symmetrical T-pose, clean topology, stylized-realistic grounded sci-fi, PBR textures, plain neutral background. A faceless human Dominion soldier: matte charcoal-and-oxblood combat plate over a sealed dark bodysuit, a full-face visored helmet with a single cold cyan scanner-slit, a small Program sigil on one pauldron, hard-armored boots and gloves, a compact rifle mag-clamped at the hip. Uniform, anonymous, disciplined military build. Color palette: charcoal, oxblood, gunmetal, matte black, with one cold cyan visor-glow. Weight and quiet menace over spectacle — "samurai elegy in a dead-tech galaxy."

## 2. Dominion Scan-Drone
**Serves:** ambient Ch4+ surveillance · rogue-drone `FactionCombatant` variant *(behavior-driven, no stat asset)*
**Output:** `- [ ] Assets/Ronin7/Art/Generated/Characters3D/Enemies/Dominion-Scan-Drone.prefab`

> 3D game-ready model, single object, clean topology, stylized-realistic grounded sci-fi, PBR textures, plain neutral background. A small autonomous surveillance drone: a smooth dark armored ovoid/disc body the size of a helmet, a single recessed cold-cyan scanner-eye on the front, a ring of tiny grid-projector lenses, small quiet thruster vents underneath, no limbs. Impersonal, clinical, faceless. Color palette: matte black and gunmetal with a cold cyan scanner-glow.

## 3. Program Operative Grunt
**Serves:** `Bandit` (generic template) · `Ch12Skirmisher` · `Ch12SentinelDuelist` (elite duelist variant) · vested cadets
**Output:** `- [ ] Assets/Ronin7/Art/Generated/Characters3D/Enemies/Program-Operative-Grunt.prefab`

> 3D game-ready character model, full body, single character in a symmetrical T-pose, clean topology, stylized-realistic grounded sci-fi, PBR textures, plain neutral background. A mass-cloned human samurai assassin of the current make: lean-muscular manufactured build, dead-calm flat-affect face, faint augment-seams, a thin killswitch scar at the throat, short or shaved hair. Muted black-lacquer operative armor over a fitted dark undersuit — segmented plates, a high collar, no faction markings. Holds a single sheathed katana whose hilt carries one small living light. Economical, "aimed" lethal poise. Color palette: black lacquer, gunmetal, ash-grey, dried-blood accents, with one cold hilt-glow.

## 4. Coil / Syndicate Ganger
**Serves:** `Ch9CoilRaider` · `Ch10SyndicateGuard` · `Ch2Bodyguard` (elite variant) · `Ep10Enforcer` (elite)
**Output:** `- [ ] Assets/Ronin7/Art/Generated/Characters3D/Enemies/Coil-Syndicate-Ganger.prefab`

> 3D game-ready character model, full body, single character in a symmetrical T-pose, clean topology, stylized-realistic grounded sci-fi, PBR textures, plain neutral background. A heavyset Voll syndicate enforcer: broad soft-bulked mass, oily-sheened hairless sickly-green skin, jowled multi-folded face, small close-set half-hooded gold eyes, a wide lipless mouth, throat dewlaps, stubby ringed clawed hands. Mismatched scrap-iron armor plates strapped over stolen fabric, a Coil syndicate arm-band, a stun-pole in one hand and a slug-gun holstered. Bored professional muscle, not a zealot. Color palette: sickly-green oily flesh, scrap-iron grey, gaudy gold, with neon Coil-trim accents.

## 5. Ash-World Scavenger
**Serves:** `Ch7Scavenger` (turncoat picker cell)
**Output:** `- [ ] Assets/Ronin7/Art/Generated/Characters3D/Enemies/Ash-World-Scavenger.prefab`

> 3D game-ready character model, full body, single character in a symmetrical T-pose, clean topology, stylized-realistic grounded sci-fi, PBR textures, plain neutral background. A lean hardy Ashkin frontier picker: human-tall with a broad ribcage, ruddy ochre-to-terracotta skin dry and finely cracked like sun-baked clay, raised keratin mud-flake plates on cheekbones and forearms, amber ember-glow eyes squinted against grit, coarse ash-grey matted hair. A hooded dust-cloak and a breath-scarf over patched leather and rust-cloth layers, a bulging salvage-sack at the hip, a scrap tool and a crude holdout weapon. Wary, weathered, non-militant. Color palette: ochre/terracotta skin, grey ash, rust cloth, bone, sun-bleached leather.

## 6. Humanoid Automaton
**Serves:** `Ch7Automaton` (archive security) · `Ch10MineAutomaton` (mining unit) — slow, tanky
**Output:** `- [ ] Assets/Ronin7/Art/Generated/Characters3D/Enemies/Humanoid-Automaton.prefab`

> 3D game-ready character model, full body, single object in a symmetrical T-pose, clean topology, stylized-realistic grounded sci-fi, PBR textures, plain neutral background. A derelict humanoid security-and-mining robot: heavy blocky worn plating over exposed servo joints and cabling, a broad reinforced torso, an armored featureless head with a single dim optic, oversized hydraulic arms ending in a clamp and a cutting tool, hunched slow-moving stance. Rusted, ancient, still patrolling. Color palette: rust-iron, oxide-brown, tarnished steel, with a dim amber optic-glow.

## 7. Hunter Drone
**Serves:** `EchoHunter` (learning combat drone) *(behavior-driven, no stat asset)*
**Output:** `- [ ] Assets/Ronin7/Art/Generated/Characters3D/Enemies/Hunter-Drone.prefab`

> 3D game-ready model, single object, clean topology, stylized-realistic grounded sci-fi, PBR textures, plain neutral background. A sleek aggressive floating combat drone: an angular predatory fuselage roughly a meter long, a single forward learning-optic sensor, folded blade-arms and a small barrel mount, swept stabilizer fins, downward thruster nacelles, no legs. Fast, watchful, purpose-built to hunt. Color palette: gunmetal and matte black with a cold blue learning-optic glow.

## 8. Redaction Construct
**Serves:** `Ch13Redactor` (elite) · Ch3 memory-dive redaction sentries
**Output:** `- [ ] Assets/Ronin7/Art/Generated/Characters3D/Enemies/Redaction-Construct.prefab`

> 3D game-ready model, single humanoid construct in a symmetrical T-pose, clean topology, stylized digital sci-fi, PBR textures, plain neutral background. A faceless half-formed humanoid figure built of scrubbed static and stacked censor-blocks: a rough head-and-torso silhouette where blocks of glitch and scan-line noise stand in for a body, edges dissolving into digital corruption, no face, arms trailing off into censored fragments. Abstract, eerie, not living. Color palette: glitch-grey, censor-black, static-white, with scan-line cyan seams.

## 9. Spectral Grave-Guardian
**Serves:** `Ch8Buried` (roused barrow-dead) · `Ep08Reaper` (Garden reaper, elite)
**Output:** `- [ ] Assets/Ronin7/Art/Generated/Characters3D/Enemies/Spectral-Grave-Guardian.prefab`

> 3D game-ready character model, full body, single figure in a symmetrical T-pose, clean topology, stylized-realistic grounded sci-fi, PBR textures, plain neutral background. A risen grave-guardian of the fog-drowned Silent Garden: a tall gaunt humanoid body assembled from grave-iron, cracked headstone slabs, twisting root and packed grey fog, a hollow cowled head with a faint cold light deep inside, heavy slab-like arms, lower body trailing into mist and root. Ancient, solemn, immovable. Color palette: grave-grey, weathered iron, root-brown, fog-white, no warm color.

## 10. Dream / Ghost Manifestation
**Serves:** `Ch11GhostManifestation` (dream swarm) · `DreamPhantom` (solid↔phased cycler)
**Output:** `- [ ] Assets/Ronin7/Art/Generated/Characters3D/Enemies/Ghost-Manifestation.prefab`

> 3D game-ready character model, full body, single figure in a symmetrical T-pose, clean topology, stylized ethereal sci-fi, PBR textures, plain neutral background. A translucent phantom wearing the half-remembered face of a fallen operative: a gaunt semi-transparent humanoid, features fading in and out of focus, wisps of vapor peeling off the edges of the limbs, faintly luminous from within, a suggestion of an operative's armor dissolving toward the legs. A dream-thing, mournful and unreal. Color palette: desaturated blue-grey and ghost-white with a faint cold inner glow.

## 11. Iron Dojo Warden-Cadre
**Serves:** Ch6 warden-nurses / conditioning-enforcers (builder-spawned dojo cadre)
**Output:** `- [ ] Assets/Ronin7/Art/Generated/Characters3D/Enemies/Iron-Dojo-Cadre.prefab`

> 3D game-ready character model, full body, single character in a symmetrical T-pose, clean topology, stylized-realistic grounded sci-fi, PBR textures, plain neutral background. A human Iron Dojo conditioning-warden: an upright disciplined adult in a clean high-collared bone-white-and-slate institutional uniform over a dark undersuit, a Program-grey apron-sash, a restraint-cuff and a conditioning prod at the belt, calm cold institutional expression, hair pinned severe. Reads as caretaker-and-jailer — a monster that looks kind. Color palette: bone-white, slate-grey, Program grey, with muted steel and one cold indicator-light.

## 12. Interceptor (enemy ship)
**Serves:** `Interceptor` (`EnemyShipDefinition`) — the core space-dogfight enemy
**Output:** `- [ ] Assets/Ronin7/Art/Generated/Characters3D/Enemies/Interceptor.prefab`

> 3D game-ready model, single spacecraft, clean topology, stylized-realistic grounded sci-fi, PBR textures, plain neutral background. A lean single-seat Dominion interceptor: an aggressive dart-shaped fuselage with a narrow armored cockpit canopy, swept forward-raked wings, twin forward cannon barrels, quad rear thruster nozzles, hard-edged riveted panel plating, worn and functional. Fast attack fighter, no landing gear deployed. Color palette: gunmetal and charcoal hull with oxblood squadron stripes and an orange thruster/tracer glow.

---

## Reuse, don't generate (no new mesh needed)
These enemies are phantoms or reskins of a body that already exists — **do not** spend a Tripo run:
- **`MirrorPhantom`** (player-copies), **`HiveCascadeController`** (clone-swarm), **`ZeroGFloatController`**
  (zero-G reskin) → reuse the hero mesh **`Ronin-7_Cipher_Soren`** + a ghost/tint material.
- **`Ch16KhallImage`** (duel-echo of Khall) → reuse the named **Khall** prefab + a ghost material.
- **`TrainingDummy`** (tutorial target) and **`Ch11DreamingArchive`** (1-HP objective prop) → not
  characters; reuse an operative body or a simple stand. Optional, low priority.

## Coverage note
Every non-named `EnemyDefinition` in `Project/Assets/Ronin7/Data/` maps to exactly one archetype above
or to the reuse list. Named-boss assets (`Ch4Kerrax`, `Ch6Hespa/Caradoc/Kaelen`, `Ch7PreviousOwner`,
`Ch8Warden`, `Ch9Vane`, `Ch10Sever/Vess`, `Ch11Aldric`, `Ch12Edition`, `Ch16Samurai4`,
`Ch16Maelgorn*`, `Ep09Vera`) and non-enemy assets (`Katana`, `ShipCannon`, `Zone_Alpha`,
`ChapterCampaign`) are intentionally excluded.
