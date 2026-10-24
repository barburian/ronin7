# Ronin7 — Roguelike Refactor Design & API Contract

Status: **authoritative build contract** for the story→roguelike refactor.
Every implementing agent codes against the signatures in this doc. If a signature here is wrong,
raise it — do not silently diverge, because parallel modules compile against it.

## 1. Pitch & loop

Ronin-7 is a blade-shadow descending into **the Fracture**, a shifting lattice of Program sectors.

```
Phase6_Boot  --StartRun-->  Node 0 ... Node 14  --Boss killed--> Run won  --> Phase6_Boot
     ^                            |
     |                            +-- player dies (permadeath) --> Run lost --> Phase6_Boot
     +-- spend Echoes at the SHRINE (boot menu) on permanent unlocks between runs
```

* A **run** = 3 sectors x 5 nodes = **15 nodes**, ~25 min (VR session length is a design constraint).
* **Permadeath**: death ends the run. Boons are lost; **Echoes** (meta-currency) are banked.
* Each node loads the single reusable `RunArena` scene; the arena is **assembled at runtime from a
  seed** while the screen is still under GameFlowManager's black fade.

### Map layout per sector (branching graph, Amendment 10)

The map is a `RunMap` graph (`RunMapGenerator.GenerateGraph(seed)`), 5 rows per sector. A node's id is
`RunState.NodeIndex`; `Depth` = sector x 5 + row, so a path is always 15 deep. Edges run from lane l to
lanes l-1..l+1 of the next row.

| row | lanes | kinds |
|---|---|---|
| 0 | 1 | `Combat` (guaranteed soft opener) |
| 1 | 2-3 | weighted: Combat 40 / Elite 25 / Event 15 / Treasure 10 / Shop 10 |
| 2 | 2-3 | same weights; at most 1 Treasure and 1 Shop per sector |
| 3 | 2 | `Forge` + `Shop` |
| 4 | 1 | `Boss` |

After a node's reward the player picks the next node: diegetic exit gates on the arena's north wall, or a
"CHOOSE YOUR PATH" panel when seated or after 6 s without moving. `RunSnapshot` is v2 (adds the path).

### Rewards

| cleared node | reward |
|---|---|
| `Combat` | boon offer, 3 choices |
| `Elite` | boon offer, 3 choices, rarity weights shifted up |
| `Treasure` | 1 guaranteed Rare, no fight |
| `Forge` | MEND (heal 60% of missing HP) or TEMPER (+1 reroll token) |
| `Shop` | spend this run's Echoes: 3 boons (30 / 60 / 120 by rarity) + MEND 25% for 40 |
| `Event` | data-driven 2-option event (Blood Pact, Gamble, Meditate, Forgotten Shrine) |
| `Boss` | boon offer, 3 choices, rarity weights shifted up hardest |

## 2. Reuse mandate

This refactor **adds** a mode; it does not delete shipped work.

* **Never rebuild or delete the shipped chapter scenes** (they hold hand-wired post-build polish).
* Bosses reuse existing `EnemyDefinition` assets already in `Assets/Ronin7/Data/`
  (`Ch16MaelgornPhase1/2/3`, `Ch12Edition`, `Ch13Redactor`, `Ch11DreamingArchive`, …) paired with
  existing Named character prefabs under `Art/Generated/Characters3D/Named/`.
* Trash mobs reuse the 11 prefabs in `Art/Generated/Characters3D/Enemies/`.
* **Tier-3 boons are the five abilities that are already fully built**: `AbilityId.WeakpointSight`,
  `Overdrive`, `PhaseStep`, `Unbroken`, `Mirror`. Granting a Tier-3 boon = `CampaignState.UnlockAbility`.
* Stat boons drive the existing `PlayerCombatModifiers` slots and `Health`.
* Geometry reuses the `XRRigBuilder` partial-class helpers (`BuildWall`, `BuildFloorCeiling`,
  `BuildRoomDetails`, `BuildAccentPointLight`, `BuildRig`, `BuildEnemy`, …).

## 3. VR constraints (non-negotiable, inherited from CLAUDE.md)

90 FPS floor / 11.11 ms. **No camera shake.** 1 unit = 1 m. Arena assembly happens under the black
fade, never mid-gameplay. No per-frame `FindObjectsByType` — use `Health.Active`.

---

## 4. API contract

### Module 1 — `Ronin7.Core`, folder `Scripts/Core/Roguelike/`

```csharp
namespace Ronin7.Core
{
    // Deterministic, allocation-free PRNG (xorshift32). Same seed => same sequence, forever.
    public struct RunRng
    {
        public RunRng(uint seed);           // seed 0 is remapped internally (xorshift cannot use 0)
        public uint State { get; }          // exposed so a run can be resumed/serialized
        public uint NextUInt();
        public int NextInt(int maxExclusive);          // maxExclusive <= 0 => 0
        public int NextInt(int minInclusive, int maxExclusive);
        public float NextFloat();                      // [0,1)
        public bool Chance(float probability);
        // Weighted pick. Returns -1 for null/empty/all-non-positive weights.
        public int PickWeighted(System.ReadOnlySpan<int> weights);
        public static RunRng ForNode(uint runSeed, int nodeIndex); // stable per-node substream
    }

    public enum RoomKind { Combat, Elite, Treasure, Forge, Boss }

    public readonly struct RunNode
    {
        public readonly int Index;          // 0..14 global
        public readonly int Sector;         // 0..2
        public readonly int IndexInSector;  // 0..4
        public readonly RoomKind Kind;
        public RunNode(int index, int sector, int indexInSector, RoomKind kind);
    }

    public static class RunMapGenerator
    {
        public const int SectorCount = 3;
        public const int NodesPerSector = 5;
        public const int TotalNodes = SectorCount * NodesPerSector; // 15
        // Deterministic: same seed => identical map. Always TotalNodes long.
        public static RunNode[] Generate(uint seed);
    }

    // Pure difficulty curve. depth is the global node index 0..14.
    public static class RunScaling
    {
        public static float HealthMultiplier(int depth, RoomKind kind);  // 1.0 .. ~2.6
        public static float DamageMultiplier(int depth, RoomKind kind);  // 1.0 .. ~1.9
        public static int EnemyCount(int depth, RoomKind kind, ref RunRng rng); // Forge/Treasure => 0
        public static int EchoReward(int depth, RoomKind kind);
    }

    // Static, mirrors CampaignState's conventions. Session-scoped; cleared on run end.
    public static class RunState
    {
        public static bool InRun { get; }
        public static uint Seed { get; }
        public static int NodeIndex { get; }
        public static RunNode CurrentNode { get; }
        public static System.Collections.Generic.IReadOnlyList<RunNode> Map { get; }
        public static int EchoesEarned { get; }
        public static int RerollTokens { get; }
        public static System.Collections.Generic.IReadOnlyCollection<string> Boons { get; }

        public static void Begin(uint seed);
        public static bool Advance();                 // true if a next node exists; false = run complete
        public static void AddBoon(string boonId);
        public static int BoonCount(string boonId);   // boons may stack
        public static void AddEchoes(int amount);     // clamps negatives to 0
        public static void AddRerollToken(int amount);
        public static bool TrySpendRerollToken();
        public static void End();                     // clears run-scoped state
        public static void Reset();                   // test/menu hard reset
    }

    // Persistent across runs. Serialized into SaveData v3.
    public static class MetaProgression
    {
        public static int Echoes { get; }
        public static int BestDepth { get; }
        public static int RunsCompleted { get; }
        public static int RunsWon { get; }
        public static void AddEchoes(int amount);
        public static bool TrySpend(int amount);      // false if insufficient
        public static int UpgradeLevel(string upgradeId);
        public static void SetUpgradeLevel(string upgradeId, int level);
        public static void NoteRunEnded(int depthReached, bool won);
        public static void ApplyFrom(SaveData data);
        public static void WriteTo(SaveData data);
        public static void Reset();
    }

    public static class MetaUpgradeId
    {
        public const string StartingHealth = "meta_starting_health";   // +10 max HP per level, max 5
        public const string StartingDamage = "meta_starting_damage";   // +5% blade damage per level, max 5
        public const string StartingBoon   = "meta_starting_boon";     // start with N tier-1 boons, max 3
        public const string RerollTokens   = "meta_reroll_tokens";     // +1 starting reroll, max 3
    }

    public static class BoonId
    {
        // Tier 1 (Common)
        public const string KeenEdge = "boon_keen_edge";
        public const string Ironskin = "boon_ironskin";
        public const string SwiftStep = "boon_swift_step";
        public const string Bloodletter = "boon_bloodletter";
        public const string LongGuard = "boon_long_guard";
        // Tier 2 (Rare)
        public const string SunderBeat = "boon_sunder_beat";
        public const string FollowThrough = "boon_follow_through";
        public const string SecondWind = "boon_second_wind";
        // Tier 3 (Epic) — grant the already-built abilities
        public const string GrantWeakpointSight = "boon_grant_weakpoint_sight";
        public const string GrantOverdrive = "boon_grant_overdrive";
        public const string GrantPhaseStep = "boon_grant_phase_step";
        public const string GrantUnbroken = "boon_grant_unbroken";
        public const string GrantMirror = "boon_grant_mirror";
    }
}
```

**Run events** — Module 1 also adds `Scripts/Core/Roguelike/RunEvents.cs` so Flow and World can
publish/subscribe without a new assembly edge:

```csharp
namespace Ronin7.Core
{
    public readonly struct RoomCleared { public readonly int NodeIndex; public RoomCleared(int nodeIndex); }
    public readonly struct RunStarted  { public readonly uint Seed;     public RunStarted(uint seed); }
    public readonly struct RunEnded    { public readonly int DepthReached; public readonly bool Won;
                                         public RunEnded(int depthReached, bool won); }
    public readonly struct BoonChosen  { public readonly string BoonId;  public BoonChosen(string boonId); }
}
```

**`SaveData` v3** (Module 1 owns this edit; no other module touches `SaveData.cs`/`CampaignState.cs`):
bump `CurrentVersion` to 3 and append — never reorder — these fields, each JsonUtility
forward-compatible (absent => initializer value, exactly like the existing `itemIds` contract):

```csharp
public int metaEchoes;
public int metaBestDepth;
public int metaRunsCompleted;
public int metaRunsWon;
public List<MetaUpgradeEntry> metaUpgrades = new List<MetaUpgradeEntry>();
```

with `[Serializable] public class MetaUpgradeEntry { public string id; public int level; }` in its own
file next to `DrillScoreEntry`. `CampaignState.ToSaveData()` calls `MetaProgression.WriteTo(save)`;
`CampaignState.ApplyFrom(data)` calls `MetaProgression.ApplyFrom(data)`. `CampaignState.Reset()` must
**NOT** clear `MetaProgression` — meta survives a new campaign by definition.

### Module 2 — `Ronin7.Combat`, folder `Scripts/Combat/Boons/`

```csharp
namespace Ronin7.Combat
{
    public enum BoonRarity { Common, Rare, Epic }

    public enum BoonEffectKind
    {
        BladeDamageMultiplier,  // magnitude 0.15 => x1.15, stacks multiplicatively
        MaxHealthAdd,           // magnitude 20 => +20 max HP
        MoveSpeedMultiplier,
        HealOnKill,             // magnitude 3 => 3 HP per kill
        ParryWindowMultiplier,
        ParryFlowBonus,         // adds to PlayerCombatModifiers.ParryFlowMultiplier headroom
        ComboBonus,
        ReviveOnce,
        GrantAbility            // abilityId field carries the AbilityId constant
    }

    [CreateAssetMenu(menuName = "Ronin 7/Boon Definition", fileName = "BoonDefinition")]
    public class BoonDefinition : ScriptableObject
    {
        public string id;                 // a BoonId constant
        public string displayName;
        [TextArea] public string description;
        public BoonRarity rarity;
        public BoonEffectKind effect;
        public float magnitude;
        public string abilityId;          // only for GrantAbility
        public int maxStacks = 3;         // GrantAbility/ReviveOnce use 1
    }

    [CreateAssetMenu(menuName = "Ronin 7/Boon Catalog", fileName = "BoonCatalog")]
    public class BoonCatalog : ScriptableObject
    {
        public BoonDefinition[] boons;
        public BoonDefinition Find(string id);
    }

    // Pure: what 3 boons to offer. No Unity API beyond the SO refs.
    public static class BoonOfferPicker
    {
        // Rarity weights by node kind. Never offers a boon already at maxStacks.
        // Returns up to `count` DISTINCT definitions; fewer only if the pool is exhausted.
        public static System.Collections.Generic.List<BoonDefinition> Pick(
            System.Collections.Generic.IReadOnlyList<BoonDefinition> pool,
            System.Func<string, int> stacksHeld,
            Ronin7.Core.RoomKind kind,
            ref Ronin7.Core.RunRng rng,
            int count = 3);

        public static int RarityWeight(BoonRarity rarity, Ronin7.Core.RoomKind kind);
    }

    // Pure aggregation of held boons into the numbers the rest of combat reads.
    public class BoonInventory
    {
        public void Add(BoonDefinition boon);
        public int Stacks(string boonId);
        public float BladeDamageMultiplier { get; }  // product of (1+magnitude)^stacks
        public float MaxHealthAdd { get; }
        public float MoveSpeedMultiplier { get; }
        public float HealOnKill { get; }
        public float ParryWindowMultiplier { get; }
        public bool HasRevive { get; }
        public bool ConsumeRevive();
        public System.Collections.Generic.IReadOnlyList<string> GrantedAbilities { get; }
        public void Clear();
    }
}
```

Also add to `PlayerCombatModifiers` a fourth slot `public float BoonMultiplier { get; set; } = 1f;`
folded into the existing `DamageMultiplier` product. Keep the 3x cap. Do not change anything else
in that file.

### Module 3 — `Ronin7.World`, folder `Scripts/World/Roguelike/`

```csharp
namespace Ronin7.World
{
    [CreateAssetMenu(menuName = "Ronin 7/Arena Room Library", fileName = "ArenaRoomLibrary")]
    public class ArenaRoomLibrary : ScriptableObject
    {
        [System.Serializable] public class Biome
        {
            public string id;                 // "rust", "program", "garden"
            public Color floorColor, ceilColor, accentColor;
            public GameObject[] propPrefabs;  // optional; may be empty (greybox fallback)
        }
        public Biome[] biomes;
        public Biome ForSector(int sector);   // wraps if fewer biomes than sectors
    }

    [CreateAssetMenu(menuName = "Ronin 7/Enemy Spawn Table", fileName = "EnemySpawnTable")]
    public class EnemySpawnTable : ScriptableObject
    {
        [System.Serializable] public class Entry
        {
            public GameObject prefab;            // combat-ready enemy prefab
            public Ronin7.Enemies.EnemyDefinition definition;
            public int weight = 10;
            public int minDepth = 0;
            public bool eliteOnly;
        }
        [System.Serializable] public class BossEntry
        {
            public GameObject prefab;
            public Ronin7.Enemies.EnemyDefinition definition;
            public int sector;                   // which sector this boss caps
        }
        public Entry[] trash;
        public BossEntry[] bosses;
    }

    // Pure. What to spawn for one node.
    public readonly struct SpawnRequest
    {
        public readonly int EntryIndex;    // index into the table's trash[] (or bosses[] when IsBoss)
        public readonly Vector3 Position;
        public readonly bool IsBoss;
        public SpawnRequest(int entryIndex, Vector3 position, bool isBoss);
    }

    public static class WaveComposer
    {
        // Ring/scatter placement inside the arena, min 1.5 m apart, min 4 m from the player spawn.
        public static System.Collections.Generic.List<Vector3> Positions(
            int count, float arenaHalfExtent, Vector3 playerSpawn, ref Ronin7.Core.RunRng rng);

        // Eligible = minDepth <= depth, and eliteOnly only when kind is Elite/Boss.
        public static System.Collections.Generic.List<SpawnRequest> Compose(
            EnemySpawnTable table, Ronin7.Core.RunNode node, float arenaHalfExtent,
            Vector3 playerSpawn, ref Ronin7.Core.RunRng rng);
    }

    // Scene component on the RunArena scene root. Builds the room, spawns, watches for clear.
    public class RunArenaController : MonoBehaviour
    {
        public event System.Action Cleared;
        public bool IsCleared { get; }
        // Assembles geometry + enemies for RunState.CurrentNode. Safe to call once, in Start().
    }
}
```

`RunArenaController` must track alive enemies via each spawned `Health.Died` (subscribe on spawn,
unsubscribe on death/destroy) — **never** a per-frame `FindObjectsByType`. On clear it publishes
`Ronin7.Core.RoomCleared`.

### Module 4 — `Ronin7.Flow`, folder `Scripts/Flow/Roguelike/`

```csharp
namespace Ronin7.Flow
{
    // Persistent singleton alongside GameFlowManager. Owns run lifecycle + node transitions.
    public class RunDirector : MonoBehaviour
    {
        public static RunDirector Instance { get; }
        public void StartRun();             // random seed (resumes a saved run if one exists)
        public void StartRun(uint seed);    // daily/known seed
        public void AbandonRun();

        // Pure, unit-testable decisions:
        internal enum PostClearAction { OfferBoon, AdvanceImmediately, WinRun }
        internal static PostClearAction ResolvePostClear(RoomKind kind, bool isFinalNode);
    }
}
```

`RunDirector` reuses `GameFlowManager`'s fade/load path — it must not duplicate scene loading.
Add to `GameFlowManager` a single `public IEnumerator LoadSceneFaded(string scene, GameMode mode)`
wrapper exposing the existing `FadeLoadFade`, and nothing else. Player death during a run is already
funnelled through `GameFlowManager.OnEntityDied`; `RunDirector` subscribes to `EntityDied` too and
ends the run, banking echoes, before the existing game-over path returns to the menu — for a run, it
returns to **`Phase6_Boot`** (the boot menu) — there is no hub in the roguelike build (D-02).

### Module 5 — `Ronin7.Editor`, folder `Scripts/Editor/Roguelike/` (built after 1–4)

* `EnemyPrefabBaker` — menu `Tools/Space Samurai/Roguelike/Bake Enemy Prefabs`: for each of the 11
  `Characters3D/Enemies/*.prefab`, bake a combat-ready spawnable prefab into
  `Assets/Ronin7/Prefabs/Roguelike/Enemies/` using the same component recipe as
  `XRRigBuilder.BuildEnemy` (Health + Enemy + ArmR/Sword/Blade/BladeTip rig + NpcWalkAnimator),
  leaving `target` null so `MeleeAttacker.Awake`'s `FindPlayer()` resolves it at spawn.
* `BoonCatalogBuilder` — menu `.../Build Boon Catalog`: authors the 13 `BoonDefinition` assets and
  the `BoonCatalog` into `Assets/Ronin7/Data/Boons/`.
* `RoguelikeArenaBuilder` — menu `.../Build Run Arena Scene`: authors `Assets/Ronin7/Scenes/RunArena.unity`
  with an XR rig (`BuildRig`), a sword, a `RunArenaController`, lighting, and registers the scene in
  the build list via the existing `EnsureScenesInBuild`.

## 5. Test gate

EditMode baseline before this work: **842 green, 0 skips** (PlayMode 70/70). Every module adds
EditMode tests for its pure logic; the suite must stay green and grow. Pure-logic coverage is
mandatory for: `RunRng` determinism, `RunMapGenerator` shape/determinism, `RunScaling` monotonicity,
`RunState`/`MetaProgression` transitions + save round-trip, `BoonOfferPicker` distinctness/cap
respect, `BoonInventory` aggregation, `WaveComposer` spacing/eligibility, `RunDirector.ResolvePostClear`.

---

## Appendix A — Verified asset inventory (for Module 5 baking)

All paths below were confirmed present on disk on 2026-09-19.

### Trash-mob art prefabs — `Assets/Ronin7/Art/Generated/Characters3D/Enemies/`

| prefab | suggested tier | suggested definition | minDepth |
|---|---|---|---|
| `Ash-World_Scavenger` | light | `Ch7Scavenger` | 0 |
| `Coil_Syndicate_Ganger` | light | `Ch9CoilRaider` | 0 |
| `Dominion_Trooper` | medium | `Ch10SyndicateGuard` | 0 |
| `Program_Operative_Grunt` | medium | `Bandit` | 2 |
| `Hunter_Drone` | fast | `Ch11GhostManifestation` | 3 |
| `Dominion_Scan-Drone` | fast | `Ch13LabSecurity` | 3 |
| `Humanoid_Automaton` | heavy | `Ch10MineAutomaton` | 5 |
| `Iron_Dojo_Warden-Cadre` | heavy | `Ch6Caradoc` | 5 |
| `Spectral_Grave-Guardian` | elite | `Ch8Buried` | 6, eliteOnly |
| `Redaction_Construct` | elite | `Ch13Redactor` | 7, eliteOnly |
| `Dream_Ghost_Manifestation` | elite | `Ch11DreamingArchive` | 8, eliteOnly |

### Boss art prefabs — `Assets/Ronin7/Art/Generated/Characters3D/Named/`

| sector | prefab | definition |
|---|---|---|
| 0 | `The-Warden` | `Ch8Warden` |
| 1 | `Vane_Wraith-6` | `Ch9Vane` |
| 2 | `Maelgorn` | `Ch16MaelgornPhase1` (final boss) |

Spare/alternate bosses already tuned and available: `Kerrax` + `Ch4Kerrax`,
`Samurai-4` + `Ch16Samurai4`, `Sever_Ninja-2` + `Ch10Sever`.

These definitions are **existing project assets** — the runtime must `Instantiate()` a copy before
applying `RunScaling`, never mutate the asset on disk.

### Menu integration points

* `Flow/MainMenuController.cs` — add `OnStartRunClicked()` calling `RunDirector.Instance.StartRun()`,
  matching the existing thin-bridge idiom (each method is one line delegating to a singleton).
* `Flow/MissionLauncher.cs` — unchanged (story code stays compiled). `RunLauncher` and the hub patch were
  removed at the Phase 2 exit (D-02); runs start only from the boot menu's ENTER THE FRACTURE.

### Enemy art prefab rig shape (confirmed by inspecting `Dominion_Trooper.prefab`)

Every `Characters3D/Enemies/*.prefab` is a Tripo mesh with the project's procedural 6-bone rig:

```
<PrefabName>            <- root
  Rig_Root
    Rig_Body
      Rig_ArmL
      Rig_ArmR          <- the weapon pivot; MeleeAttacker.weapon binds here
      Rig_Jaw           <- lip-sync bone (NpcTalkAnimator)
    Rig_LegL
    Rig_LegR            <- NpcWalkAnimator swings these
  Visual_Skinned        <- SkinnedMeshRenderer
```

So `EnemyPrefabBaker` must, per prefab: add `Health` + `Enemy` at the root, build
`Rig_ArmR/Sword/Blade/BladeTip` if absent (matching `EnemyArtWirer`'s existing convention), bind
`weapon = Rig_ArmR`, `bladeTip = .../BladeTip`, `bodyRenderer = Visual_Skinned`'s renderer, add a
`NpcWalkAnimator`, add a capsule collider on the root, and leave `target` null.

### Module 5 implementation note — extend the existing partial class

`BuildRig`, `BuildSword`, `BuildWall`, `BuildFloorCeiling`, `BuildRoomDetails`,
`BuildAccentPointLight`, `BuildEnemy`, `EnsureFolder`, `EnsureScenesInBuild`, `TryLoadInputRefs`
and `EnsureWeaponDefinition` are all **`private static` members of
`public static partial class XRRigBuilder`** in namespace `Ronin7.EditorTools`.

So `RoguelikeArenaBuilder` must be declared as another partial of that same class
(`namespace Ronin7.EditorTools { public static partial class XRRigBuilder { ... } }`), exactly as
every `ChapterNBuilder.cs` does. Prefix all roguelike-local helpers `Rogue` to avoid collisions.
Do NOT duplicate these helpers or change their accessibility.

Reference pattern to copy: `Chapter9Builder.cs` (menu item → `TryLoadInputRefs` → `NewScene` →
load definition assets AFTER `NewScene` (scene creation unloads unused assets, so refs held across
it go fake-null and serialize as `{fileID: 0}`) → build lighting → geometry → rig → save + register).

---

## Amendment 1 — adjudicated review findings (supersedes §4 where they conflict)

Reviewers found real defects in the Module 2 contract as originally written. These decisions are
binding and override the earlier text.

### A1.1 Revive state must be durable (fixes the permadeath hole)

`BoonInventory` is rebuilt whenever the arena scene reloads, which is **every node**. A transient
`_pendingRevives` counter therefore resurrects a spent Second Wind on the next node — infinite
revives, which destroys the mode's core pillar.

Fix, both parts required:
1. Module 1 adds to `RunState`: `public static int RevivesSpent { get; }`, `public static void
   NoteReviveSpent()`, and `RevivesSpent` is cleared by `Begin`/`End`/`Reset` like every other
   run-scoped field.
2. Module 2 adds `public void SetRevivesSpent(int spent)` to `BoonInventory`; `HasRevive` becomes
   `GrantedRevives - spent > 0`. `RunDirector` owns **one** `BoonInventory` for the whole run and
   calls `SetRevivesSpent(RunState.RevivesSpent)` after any rebuild; `ConsumeRevive()` calls
   `RunState.NoteReviveSpent()`.

### A1.2 `BoonInventory.Add` enforces `maxStacks` itself

The picker must not be the only guard — starting boons, Treasure grants and debug grants all bypass
it. `Add` returns early when `Stacks(boon.id) >= Mathf.Max(1, boon.maxStacks)`.
`BoonOfferPicker` likewise treats `maxStacks <= 0` as 1, so a mis-authored asset degrades to
"offerable once" instead of "silently never offerable".

### A1.3 Cut the two effect kinds that have no consumer

`MoveSpeedMultiplier` and `ParryWindowMultiplier` are **removed from `BoonEffectKind`**.

* `ContinuousLocomotion` exposes no speed hook, and its comfort vignette is driven by snap-turn /
  slide / wall-run only — never by linear speed. A speed boon would raise the strongest vection
  stimulus in the game with no mitigation, and would also invalidate the absolute `wallRunMinSpeed`
  and slide-ramp thresholds tuned against the current 5.5 m/s run. Not shippable without in-headset
  validation, so it does not ship in this pass.
* `PerfectParryWindow` is a `private const` and cannot be scaled at runtime; at the proposed
  magnitudes it would have doubled the perfect-parry window, trivialising the mechanic the whole
  combat model rests on.

Removing an unused enum member is the Karpathy-compliant move; adding a consumer speculatively is not.
`BoonId.SwiftStep` and `BoonId.LongGuard` are removed with them.

### A1.4 Wire `ParryFlowBonus` and `ComboBonus` through the existing blackboard

These two DO get consumers, because the change is small and safe. `PlayerCombatModifiers` — already
documented as the Player↔Combat coupling point — gains two additive slots:

```csharp
public float BoonParryFlowBonus { get; set; }  // default 0
public float BoonComboBonus { get; set; }      // default 0
```

`BoonInventory` gains the matching aggregation properties `ParryFlowBonus` / `ComboBonus` (additive,
same shape as `HealOnKill`). `RunDirector` writes both slots once per boon acquisition — never per
frame. The two controllers each change by one line:

* `ParryFlowController` → `ParryTiming.FlowMultiplier(streak, PerStackBonus + mods.BoonParryFlowBonus)`
* `ComboMomentumController` → `MultiplierForCombo(count, PerStack + mods.BoonComboBonus)`

This **amends** the earlier instruction "do not change anything else in `PlayerCombatModifiers`",
which was written before this gap was known.

### A1.5 Damage cap restructured — boons must never be dead weight

Max parry-flow is 1.40x and max combo is 1.60x; their product alone is 2.24x, so against the existing
flat 3x cap a damage-boon stack does **literally nothing** exactly when the player is playing well.
That is the worst possible failure mode for a roguelike's most basic boon.

`PlayerCombatModifiers.DamageMultiplier` becomes:

```
min(Weakpoint * ParryFlow * Combo, 3f)  *  min(BoonMultiplier, 2f)
```

Transient in-combat buffs keep their 3x ceiling; the run-long boon investment is capped separately at
2x and always applies. Absolute worst case is 6x, reached only by a fully-boon-stacked player at peak
flow and combo on a weakpoint — a legitimate roguelike power fantasy, not an exploit.

### A1.6 Offers may legitimately be smaller than 3

`BoonOfferPicker.Pick` can return 0 or 1 items from a non-empty pool (e.g. a Treasure node whose
eligible entries are all Common, which has weight 0 there). This is correct behaviour and must be
documented on `Pick`. **Module 4's offer UI must auto-advance on a 0-item offer and may present a
1- or 2-item offer — it must never block waiting for a choice that cannot be made.** A boon panel
that waits forever is a run-ending soft-lock in a headset.

§1's "Treasure = 1 guaranteed Rare" reads as **Rare-or-better**; Module 4 passes `count: 1` there.

### A1.7 Common pool widened, Combat weights retuned

Five Common boons against a Combat weight of 70 meant ~2.3 of every 3 offers were drawn from the same
five — that reads as "no choice". Two Commons were also just cut by A1.3. Module 5's
`BoonCatalogBuilder` authors **seven** Commons, and Combat's weights become {Common 55, Rare 38,
Epic 7}. Final `BoonId` set:

```
Common: KeenEdge, Whetstone (damage) | Ironskin, SecondSkin (health)
        Bloodletter (heal-on-kill)   | FlowInitiate, ComboInitiate (flow/combo bonus)
Rare:   SunderBeat, FollowThrough, SecondWind
Epic:   GrantWeakpointSight, GrantOverdrive, GrantPhaseStep, GrantUnbroken, GrantMirror
```

### A1.8 Catalog invariants are a test, not a convention

Module 5 adds an EditMode test over the shipped `BoonCatalog` asserting: every `GrantAbility` and
`ReviveOnce` has `maxStacks == 1`; every `GrantAbility` has a non-empty `abilityId` matching an
`AbilityId` constant; all ids are distinct and non-empty; every `BoonId` constant appears exactly
once. A silent mis-authored field otherwise costs the player a run-defining pick for nothing.

---

## Amendment 2 — adjudicated Module 1 review findings

### A2.1 Meta-progression moves OUT of `SaveData` into its own file (fixes real data loss)

Storing meta in the per-slot `SaveData` while treating it as global is incoherent, and the concrete
failure is data loss, not a style problem:

> Player banks 500 Echoes in slot 1. Next session: boot → main menu → **Start New Game** (no Load
> first). `StartNewGame` resets the campaign ledgers but leaves `MetaProgression` at its boot value
> of 0, then transitions to the hub — where `FadeLoadFade`'s arrival autosave writes
> `CampaignState.ToSaveData()` to `SaveSystem.MostRecentSlot`, still slot 1. Slot 1's `metaEchoes`
> becomes 0. 500 Echoes gone, silently, under the black fade.

There is also a cross-profile leak: load slot 1 (meta 500) → menu → New Game → save to slot 2, and
slot 2 holds 500 Echoes it never earned.

**Decision: meta lives outside the slots**, which is both the genre convention (Hades, Dead Cells)
and what §1 of this doc actually promises.

1. **Revert `SaveData.CurrentVersion` to 2** and remove `metaEchoes`, `metaBestDepth`,
   `metaRunsCompleted`, `metaRunsWon`, `metaUpgrades` and `MetaUpgradeEntry` from `SaveData`. No
   shipped build ever wrote a v3 file, so this is safe and it removes the schema-migration risk
   entirely.
2. Remove the `MetaProgression.WriteTo`/`ApplyFrom` calls from `CampaignState.ToSaveData`/`ApplyFrom`.
3. Add `MetaSaveData` (its own `[Serializable]`, its own `version = 1`) and persist it to
   `meta.json` beside the slot files, **reusing `SaveSystem`'s existing atomic write-temp-then-move
   helper** — do not hand-roll a second write path. `MetaProgression.Save()` / `Load()` wrap it.
4. `MetaProgression.Load()` is called once at boot; `Save()` is called when a run ends and when an
   upgrade is purchased. A missing/corrupt `meta.json` loads as a fresh zeroed meta, never throws.
5. `CampaignState.Reset()` still must not touch meta, and now genuinely doesn't need to.

### A2.2 Concurrent enemies are capped at 4 — reinforcements drip in

Enemy *count* is the wrong difficulty lever for room-scale VR melee, and the current curve reaches 6.
This project's own shipped, hand-tuned content caps at **3-4 concurrent** (Ch2: 3 enforcers + 1
bodyguard; Ch10: 3 guards then 2 automatons). There is no attacker-token or turn-taking gate anywhere
in `Scripts/Enemies` — every `MeleeAttacker` closes and swings independently. A player with a physical
sword can address maybe 120° of arc, so enemies 5 and 6 are by definition behind them; combined with
the no-camera-shake rule there is **no off-screen hit feedback at all**. Being hit from an unseen
angle while turning is a textbook VR frustration and sickness vector.

* `RunScaling` gains `public const int MaxConcurrentEnemies = 4;`
* `RunScaling.EnemyCount` keeps returning the **total** for the node.
* **Module 3** spawns at most `MaxConcurrentEnemies` at once and releases one held-back reinforcement
  per death until the total is exhausted. This keeps deep fights long and attritional without ever
  surrounding the player.
* Add a test pinning the invariant: across every reachable depth and kind, concurrent spawns never
  exceed `MaxConcurrentEnemies`. This is a VR constraint, so it gets a gate, not a comment.

### A2.3 `EchoReward` must pay 0 at Forge nodes

`baseReward` is 0 for Forge but `depth` is added unconditionally, so a depth-13 Forge pays 13 Echoes
for a no-fight heal room. The existing test only checks depth 0, where the bug is invisible.
Fix the formula and re-test at depths 0, 7 and 13.

### A2.4 `MetaProgressionTests` must not clobber global campaign state

Its `[SetUp]`/`[TearDown]` copies `CampaignStateAbilityTests`' idiom but drops the `CampaignState.Reset()`
that is the entire point of it, while its tests mutate global `CampaignState` (and, through
`ApplyFrom`, silently zero `CampaignStats` and `DrillBestScores` for every later test class in the
same domain). Add `CampaignState.Reset()` to both, matching the sibling exactly.

### A2.5 Determinism needs golden vectors, not self-comparison

Every current determinism test compares two calls in the same process. They would keep passing if
someone reordered the xorshift steps, changed the zero-seed constant, or swapped the roll order —
silently changing every existing seed and breaking the daily-seed feature this doc sells.

These vectors were computed by two independent simulations of the exact bit operations and agree:

* `new RunRng(1)`, first five `NextUInt()`: `270369, 67634689, 2647435461, 307599695, 2398689233`
* `new RunRng(0)`, first three (also pins the zero-seed remap): `1359758873, 3761132862, 2075758394`
* `RunMapGenerator.Generate(2026)` kinds: `C C E F B  C T C F B  C E C F B`
* `RunMapGenerator.Generate(1)` kinds: `C E T F B  C E T F B  C C C F B`

Assert them explicitly. This converts the mode's core guarantee from a comment into a gate.

### A2.6 Smaller correctness items

* `MetaProgression.SetUpgradeLevel("typo_id", n)` clamps to 0 but still **inserts** the key, which
  then serializes into every later save. Early-return when `MaxLevel(id) == 0`, and skip `level == 0`
  entries when writing, to keep the file clean.
* `RunState.Map` hands out the live `RunNode[]`; a caller can cast and rewrite the run. Wrap it with
  `System.Array.AsReadOnly` on `Begin` (one allocation per run).
* `RunState.CurrentNode` returns `default` outside a run — a plausible-looking `{0, 0, Combat}` node
  that Module 3 would happily build a real room from. Document it and have callers check `InRun`.
* `RunRng.NextInt(min, max)` overflows for very wide ranges, and `PickWeighted` sums weights into an
  `int`. Neither is reachable from current callers; document the preconditions rather than adding
  unused `long` paths (Karpathy: no speculative code).
* Add a `SaveSystem` test that a hand-written v2 JSON file still loads correctly with the meta fields
  now gone, alongside the existing `Load_MissingItemIdsAbilityIds_LoadsWithEmptyLists` precedent.

---

## Amendment 3 — adjudicated Module 3 findings

### A3.1 No reflection. Add `Enemy.Configure` and bake prefabs inactive.

Module 3 reflects a scaled `EnemyDefinition` into `Enemy`'s private `definition` field because
`Enemy.Awake()` fires synchronously inside `Instantiate()`, before any caller can intervene. That is
fragile (a rename silently breaks difficulty scaling with no compile error) and it already produced a
real bug: `Awake`'s one-time `PostureMeter.Configure(definition.maxHealth * postureMaxFraction)` runs
**before** the swap, so deep-run enemies get posture-break thresholds computed from unscaled base
health — they stagger far too easily exactly where the run should be hardest.

The fix removes the problem rather than working around it:

1. **`Ronin7.Enemies.Enemy` gains `public void Configure(EnemyDefinition def)`** — assigns the
   `definition` field. A three-line, purely additive public method; no existing behaviour changes.
2. **Module 5's `EnemyPrefabBaker` bakes every spawnable enemy prefab with its root GameObject
   inactive.** Unity does not run `Awake` on an instantiated inactive object, which hands the spawner
   a window that does not otherwise exist.
3. **Module 3's spawn path becomes:** `Instantiate(prefab)` → `Configure(scaledRuntimeCopy)` →
   position/parent → `SetActive(true)`. `Awake` then runs once, with the correct definition, and
   configures both `Health` and `PostureMeter` from the scaled values.

Delete the cached `FieldInfo` and all reflection from `RunArenaController`.

### A3.2 `ArenaGeometryBuilder` joins the contract

It was specified in the build brief but not in §4, yet Module 5's `RoguelikeArenaBuilder` needs it as
a stable surface. Contract:

```csharp
namespace Ronin7.World
{
    public static class ArenaGeometryBuilder
    {
        public static void Build(Transform parent, float halfExtent,
                                 ArenaRoomLibrary.Biome biome, ref Ronin7.Core.RunRng rng);
        // Pure, deterministic, separately testable — no GameObjects.
        public static System.Collections.Generic.List<Vector3> PropPositions(
            float halfExtent, int count, ref Ronin7.Core.RunRng rng);
    }
}
```

### A3.3 Reinforcement drip (implements A2.2)

`RunArenaController` spawns at most `RunScaling.MaxConcurrentEnemies` (4) at once and releases one
held-back reinforcement per enemy death until the node's total is exhausted. The room is cleared only
when the total is exhausted **and** no enemy is alive.

Extract the decision as a pure, unit-testable static so it is gated rather than eyeballed — e.g.
`internal static int ReleaseCount(int aliveNow, int queuedRemaining, int maxConcurrent)` — and test:
never exceeds the cap; every queued enemy eventually releases; a 0-enemy node is cleared immediately;
simultaneous deaths do not over-release.

### A3.4 Accepted as-is

* **Static accent lights, no `LightBudget` gating.** Correct call — zero per-frame cost satisfies the
  VR budget by construction, and driving `AmbientLightPulse` would have meant reflecting into a
  second class for cosmetic pulsing. If animated accent lighting is wanted later, `AmbientLightPulse`
  should gain a public `Configure(...)`; it is not needed for this pass.
* **Degenerate-room fallback ring.** Packing N points at both a minimum mutual distance and a minimum
  player distance is genuinely impossible in a small enough room. The "always returns `count`, never
  hangs" guarantee is the one that matters; document that the spacing guarantee is best-effort below
  a viable arena size.
* **Walls reuse `floorColor`.** Fine — `Biome` has no wall colour and adding one is speculative.

---

## Amendment 4 — the missing integration layer (CRITICAL)

Two gaps fell between Module 2 (which built the boon aggregation) and Module 4 (which built the
offer UI and run flow). Each module did its half correctly; nobody owned the join. As it stands the
mode does not work.

### A4.1 Boons are recorded but never applied — the whole system is inert

`RunDirector` calls `RunState.AddBoon(...)`, and that is the end of it. Repo-wide, `BoonInventory` is
referenced **only in comments**; nothing constructs one, and nothing ever writes
`PlayerCombatModifiers.BoonMultiplier`. The player picks a run-defining boon and literally nothing
changes.

`RunDirector` owns **one** `BoonInventory` for the whole run (this also satisfies A1.1's ownership
requirement) and gains:

```csharp
private readonly BoonInventory inventory = new BoonInventory();
private void RebuildInventory();   // from RunState.Boons via BoonCatalog.Find, then
                                   // inventory.SetRevivesSpent(RunState.RevivesSpent)
private void ApplyBoonsToPlayer(); // push aggregates onto the freshly-loaded rig
```

`RebuildInventory()` runs on run start and after every arena scene load (the durable record is
`RunState.Boons`, since the rig and inventory are rebuilt per node). `ApplyBoonsToPlayer()` runs
after every arena load **and** immediately after each boon pick:

1. Resolve `VRRig.Instance`; if absent, log a clear warning and return (never NRE).
2. `PlayerCombatModifiers` on the rig root (`GetComponent`, else `AddComponent`):
   * `BoonMultiplier   = inventory.BladeDamageMultiplier`
   * `BoonParryFlowBonus = inventory.ParryFlowBonus`
   * `BoonComboBonus     = inventory.ComboBonus`
3. Player `Health`: read its prefab-authored `Max` **before** applying (that is the base each load,
   since the rig is rebuilt from prefab), then `Configure(baseMax + inventory.MaxHealthAdd)`.
4. Ability grants: `foreach (id in inventory.GrantedAbilities) CampaignState.UnlockAbility(id);` —
   the five existing ability controllers already gate on `CampaignState.HasAbility`, so this is the
   entire wiring needed for the Epic tier.
5. Heal-on-kill: `RunDirector` subscribes to `EntityDied`; when the dead entity is **not** the player
   rig and `inventory.HealOnKill > 0`, call `Heal` on the player's `Health`. Balanced against the
   `EntityDied` unsubscribe in `OnDisable` like every other subscription in this file.

### A4.2 Player health resets to full every node — attrition and permadeath are gutted

Each node is a fresh scene load, so the rig is rebuilt from prefab at full HP. The player would enter
every one of the 15 rooms fully healed. That removes all attrition, makes the Forge's heal reward
meaningless, and makes dying essentially impossible outside a single catastrophic room — permadeath
is the mode's core pillar and this quietly disables it.

`RunState` gains durable player health:

```csharp
public static float PlayerHealth { get; }      // < 0 means "unset" => spawn at full
public static void NotePlayerHealth(float current);
public static void ClearPlayerHealth();        // back to "unset"
```

Cleared by `Begin`/`End`/`Reset` with the other run-scoped fields.

* `RunDirector` records `RunState.NotePlayerHealth(health.Current)` immediately **before** leaving a
  node (before the fade/load starts, while the rig still exists).
* In `ApplyBoonsToPlayer()`, **after** step 3's `Configure`, restore: if `RunState.PlayerHealth >= 0`,
  set current HP to `Mathf.Min(stored, newMax)`; otherwise leave it at full. Note the ordering —
  `Configure` must come first, because raising max HP mid-run should not silently heal, and
  `Health.Configure` resets current to max.
  `Health` needs a way to set current without a damage event; if none exists, add a minimal
  `public void SetCurrent(float value)` to `Health` clamped to `[0, Max]`, and nothing else.
* Ironskin (`MaxHealthAdd`) therefore raises the ceiling immediately but heals only by the amount the
  new headroom implies — i.e. it does not full-heal. That is the correct roguelike behaviour.
* **Forge nodes** heal 60% of missing HP against this stored value (per §1's reward table), then
  re-store it. This is what makes the Forge a real decision point.

### A4.3 Tests required

These are the guarantees that make the mode function; they get gates, not comments.

* Rebuilding the inventory from `RunState.Boons` reproduces identical aggregates.
* A spent revive stays spent across a rebuild (already required by A1.1 — assert it end-to-end here).
* `MaxHealthAdd` raises max HP without full-healing, and stored HP carries across a simulated node
  hop, clamped to the new max.
* Forge healing is 60% of *missing*, never overshoots max.
* Every `GrantAbility` boon results in `CampaignState.HasAbility(id)`.
* Extract the pure arithmetic (`HealAmountForForge(current, max)`, `CarryOverHealth(stored, newMax)`)
  as `internal static` helpers so they are testable without a rig, matching this codebase's
  established pure-static + `InternalsVisibleTo` pattern.

---

## Amendment 5 — adjudicated Module 3 review (all binding)

### A5.1 Three run-ending soft-locks in `RunArenaController`

1. **All-spawns-fail leaves the room permanently uncleared.** `MarkCleared()` is only reachable
   before the spawn loop or from a death callback, so if every spawn early-returns (a broken prefab
   reference), the player is locked in an empty arena with no exit. **Fix:** move the clear check to
   *after* the spawn loop — `if (aliveCount == 0) MarkCleared();`. That makes the node self-healing
   regardless of why nothing spawned.
2. **An enemy instantiated without a `Health` is left in the scene**, chasing and damaging the player
   forever, unkillable and uncounted. **Fix:** `Destroy(instance)` before returning.
3. **A `Boss` node with no usable boss entry clears instantly and silently** — winning the run, with
   full Echoes and `RunsWon`, without a boss fight. **Fix:** only `Forge`/`Treasure` may legitimately
   produce zero enemies; for `Combat`/`Elite`/`Boss` log a loud `Debug.LogError` and then still clear
   (clearing is the safe failure; a soft-lock is worse than a skipped fight).

`WaveComposer` must also exclude entries with a null `prefab` or null `definition` from eligibility,
exclude null-prefab bosses from both the sector match and the last-entry fallback, and exclude
`weight <= 0` entries (today a zero-weight entry is spawned *exclusively* when it is the only
eligible one — the exact opposite of what setting weight 0 means).

### A5.2 `Object.Destroy` is illegal in edit mode — it breaks the gate AND ships a bad collider

`ArenaGeometryBuilder` calls `Object.Destroy(ceiling.GetComponent<Collider>())`. Outside play mode
that logs an error and does not destroy, so ~6 EditMode tests fail (the test framework fails a test on
an unexpected `LogError`) — the 842-green gate does not survive it. Worse, A3.2 makes this the surface
Module 5's *editor* arena builder calls, so the shipped `RunArena.unity` would carry an invisible
24x24 m ceiling collider that teleport arcs and raycasts hit.

**Fix — do not destroy at all**, since this is shared runtime/editor code:
`var c = ceiling.GetComponent<Collider>(); if (c != null) c.enabled = false;`
Update the test to assert `IsFalse(c.enabled)`.

### A5.3 One coordinate space, stated explicitly

Geometry is built in the controller's **local** space; `playerSpawn.position` and the `Instantiate`
overload are **world**. It only works while the arena root sits at world identity. **Fix:** convert
once with `transform.InverseTransformPoint(...)`, compose in arena-local space, and spawn via
`SetParent(transform, false)` + `localPosition`. Document on `Positions` that it returns arena-local
coordinates.

### A5.4 Fallback ring must stay in the room and on the floor

The ring is centred on `playerSpawn` with `radius = Max(bound, MinPlayerDistance)`, so an off-centre
spawn puts points outside the walls, and at `halfExtent = 4` the radius lands exactly *on* the wall
plane. It also returns `y = playerSpawn.y`: if `playerSpawn` is a head-height transform, enemies spawn
1.2 m in the air and `Enemy.Awake` latches `groundY` there permanently — a floating, unreachable
enemy, i.e. a genuine soft-lock.

**Fix:** `radius = Mathf.Min(Mathf.Max(MinPlayerDistance, bound * 0.6f), bound)`, build the point from
`playerSpawn.x`/`.z` only, clamp `x`/`z` to +/-`bound`, and force `y = 0f` on **both** paths.

At the shipped `halfExtent = 12` the sampler accepts ~83% per attempt so the fallback is near-dead
code — but `arenaHalfExtent` is an unclamped serialized field, so a designer typing `4` reaches it on
nearly every point.

### A5.5 Concurrency cap via pre-instantiation, not mid-combat spawning

`RunScaling` gains `public const int MaxConcurrentEnemies = 4;` (it does not exist anywhere today).

The obvious drip implementation — `Instantiate` inside `OnEnemyDied` — is **wrong for VR**:
`OnEnemyDied` runs synchronously inside `Health.ApplyDamage`, inside a blade-contact frame, and
`MeleeAttacker.Awake` does a full-scene `FindObjectsByType<Health>()`. That is a guaranteed frame
spike at the worst possible moment under an 11.11 ms budget.

**A3.1's inactive-prefab baking makes the right implementation free:** instantiate **all N inactive
under the black fade in `Start`**, hold them in a queue, `SetActive(true)` the first
`min(N, MaxConcurrentEnemies)`, and activate one more per death. Subscribe to `Health.Died` at
**activation** time, not instantiate time, so the alive set and the queue stay disjoint.

Keep `internal static int ReleaseCount(int aliveNow, int queuedRemaining, int maxConcurrent)` as the
pure seam — `Health.Died` fires synchronously, so each death callback completes before the next and
reading `aliveNow` fresh is correct by construction.

### A5.6 Replace the closure dictionary with the shipped idiom

`Dictionary<Health, Action>` keyed on a `UnityEngine.Object` resolves through
`EqualityComparer<Health>.Default`, which does **not** apply Unity's fake-null `==` overload — so an
enemy destroyed without dying can never be removed and the room never clears. The neighbouring shipped
code (`EnemyWaveSpawner`) uses a plain `int aliveCount` + a method-group handler. Since A5.5 needs a
queue anyway, switch to `int alive` + `List<Health> queued` + one `OnEnemyDied(Health)` handler.
Simpler, and it deletes a bug class.

### A5.7 Smaller items

* Drop `RunArenaController`'s redundant `health.Configure(scaled...)` once A3.1 lands — `Enemy.Awake`
  does it from the correct definition, and doing it twice hides the next bug.
* `Destroy` the per-enemy runtime `EnemyDefinition` clone with its enemy (~90 orphans per run
  otherwise; currently reclaimed only by an implicit `UnloadUnusedAssets`).
* **Quest light budget:** `Mobile_RPAsset` sets `additionalLightsPerObjectLimit: 4` per-pixel. Four
  overlapping `range 9` point lights in a 24x24 m room saturate it with **zero headroom**, so any
  fifth light (boon VFX, weapon glow) is silently culled. Drop to **2** accent lights on the Quest
  tier, or profile a depth-14 Elite fight in-headset before shipping.
* **Audit instantiated prop prefabs for `MeshCollider`** and strip + error — CLAUDE.md forbids
  introducing one, and a designer dropping a Tripo art prefab into `propPrefabs` introduces one at
  runtime where no scene audit sees it. The existing "never creates a MeshCollider" test passes a
  null biome and never exercises this path; extend it.
* **Derive the no-spawn exclusion centre from the live rig** (`Camera.main` or the player `Health`
  transform, `y = 0`), treating `playerSpawn` as the fallback — not the reverse. Today an enemy can
  materialise inside the player's body at fade-in, the worst possible VR moment.
* Fix the inverted SRP-batching comment: the SRP Batcher does **not** batch renderers carrying a
  MaterialPropertyBlock. `RendererTint` is still correct here — it avoids per-renderer `.material`
  clone leaks, which is what CLAUDE.md actually cares about — but the stated reason is wrong and would
  mislead a later optimisation. Rename the test accordingly.
* `ArenaGeometryBuilder.Build` returns `GameObject`; A3.2 said `void`. **Amend the doc to match the
  code** — returning the root is more useful.

### A5.8 `RunArenaController` tests are now mandatory

The "pure wiring, no tests" trim was defensible before A3.3; it is not now, and this review found
three run-ending bugs in that one class. Required:

* `ReleaseCount` — cap respected, queue always drains, never negative, 0 when alive >= cap.
* A **gate** asserting that for every depth 0-14 x every `RoomKind`, concurrent spawns never exceed
  `RunScaling.MaxConcurrentEnemies`. This is a VR constraint, so it gets a test, not a comment.
* `WaveComposer` exclusion tests: null-prefab, null-definition and zero-weight trash entries;
  null-prefab bosses.
* `Positions` bounds + `y == 0` for a **non-centred** `playerSpawn` at `halfExtent` 4, 6 and 12.
* One PlayMode lifecycle test: 5 queued, exactly 4 active, kill one, 5th activates, all dead,
  `IsCleared` true and exactly one `RoomCleared` published. Plus a table-of-null-prefabs case
  asserting the node still clears.

Tighten the loose assertions: tests assert `<= halfExtent` where the real guarantee is
`<= halfExtent - margin`, so they would currently pass with enemies standing inside the wall.

---

## Amendment 6 — adjudicated Module 4 findings

Module 4 did implement the boon application layer that A4.1 called for (it landed after the gap was
first identified). A4.1 stands as the specification of that layer; these are the defects found in it.

### A6.1 `DeathInterceptor` is a single slot and two systems fight over it

`Health.DeathInterceptor` is one `Func<bool>`. `UnbrokenWard.OnEnable` assigns it unconditionally.
`RunDirector.ApplyBoonInventoryToRig` then assigns it again on **every** arena load, after the rig's
`OnEnable` has run:

```csharp
health.DeathInterceptor = boonInventory.HasRevive ? (Func<bool>)boonInventory.ConsumeRevive : null;
```

Consequences, both silent:
* With Second Wind held, `UnbrokenWard`'s registration is overwritten — the ward never fires.
* **Without** Second Wind, the hook is set to `null`, which *deletes* the ward's registration outright.

So `AbilityId.Unbroken` — one of the five headline Epic ability boons, and a shipped Chapter 11
mechanic — never works during a run, in either case, with no error.

**Fix:** `RunDirector` must **chain, never clobber, and never null**. Capture the existing interceptor
and compose, so both mechanics fire in a defined order:

```csharp
var previous = health.DeathInterceptor;          // e.g. UnbrokenWard's, registered at rig OnEnable
health.DeathInterceptor = () =>
    boonInventory.ConsumeRevive() || (previous != null && previous());
```

(`ConsumeRevive` already returns false when no revive remains, so the `HasRevive` test is redundant.)
Nothing accumulates across nodes because the rig and its components are rebuilt per scene load.
Add a test asserting that with both Second Wind and Unbroken held, the player survives **twice**.

### A6.2 Second Wind revives at 1 HP, which makes it near-worthless

`Health.ApplyDamage` sets `Current = 1f` after a successful interception — correct for Chapter 11's
"survive the blow" ward, but as a Rare roguelike boon it means the player revives into near-certain
death on the next contact. §1 intends a meaningful second life.

**Fix — minimal and backwards-compatible.** Add to `Health`:

```csharp
/// <summary>Fraction of max health restored when DeathInterceptor survives a lethal blow.
/// Default 0 preserves the existing survive-at-1-HP behaviour exactly.</summary>
public float ReviveFraction { get; set; }
```

and change the interception line to `Current = Mathf.Max(1f, maxHealth * ReviveFraction);`. With the
default of 0 this is byte-identical to today for `UnbrokenWard` and every existing scene/test.
`RunDirector` sets `health.ReviveFraction = 0.3f` while a Second Wind is held.

### A6.3 Confirm the meta starting-bonus assumption

Module 4 grants `MetaUpgradeId.StartingHealth`/`StartingDamage` as extra stacks of
`BoonId.Ironskin`/`KeenEdge` rather than inventing a second stat channel. That reuse is right, but it
only delivers the documented "+10 HP / +5% damage per level" if Module 5's `BoonCatalogBuilder`
authors those two magnitudes to match — a naming convention, not a type contract.

**Fix:** Module 5 authors `Ironskin.magnitude = 10` and `KeenEdge.magnitude = 0.05`, and A1.8's
catalog-invariant test asserts both explicitly, so the coupling is gated rather than assumed.

### A6.4 Accepted as good work

* The `RunState.InRun` guard in `GameFlowManager.OnEntityDied` is the right seam — `EventBus` has no
  ordering contract, so without it a run death would race the main-menu game-over against the
  hub-return path. The second small edit to `GameFlowManager` was justified.
* The `pendingScene` latch is a real find: a zero-enemy Forge/Treasure node calls `MarkCleared()`
  synchronously from `Start()`, i.e. *inside* the still-in-flight `FadeLoadFade` that loaded it. A
  naive `transitioning` boolean would have silently dropped the advance and soft-locked the run.
  Mirroring `GameFlowManager`'s own `pendingGameOver` idiom is exactly right.
* Reusing `BoonOfferPicker.Pick(..., RoomKind.Treasure, count: 1)` for the Treasure reward instead of
  re-deriving the rarity rule is correct — one source of truth.
* Declining to fake HP persistence by routing through `Health.ApplyDamage` was the right call:
  it publishes `EntityDamaged`, which haptics and audio consume, so every node transition would have
  fired a phantom hit in the headset. A4.2's `SetCurrent` is the clean answer.

---

## Amendment 7 — adjudicated Module 4 review (all binding)

### A7.1 CRITICAL — `RunState.End()` must not run inside the `EntityDied` publish

`EventBus.Publish` invokes one multicast delegate in subscription order. `RunDirector.OnEntityDied`
→ `EndRun` → `RunState.End()` flips `InRun` to false **synchronously, still inside the publish**. If
`RunDirector` subscribed first, `GameFlowManager.OnEntityDied`'s `if (RunState.InRun) return;` guard
then sees false and fires the main-menu game-over — giving two concurrent
`LoadSceneAsync(..., Single)` coroutines with two `ScreenFader`s. Subscription order is undecided
today because nothing instantiates `RunDirector` yet, so Module 5's wiring would silently decide it.

**Fix:** latch it. `RunDirector` sets `private bool endingRun = true` **before** publishing, exposed as
`internal bool EndingRun`; `GameFlowManager.OnEntityDied` guards on
`RunState.InRun || (RunDirector.Instance != null && RunDirector.Instance.EndingRun)`. Move
`RunState.End()` out of the handler entirely — call it in the scene-load routine once
`LoadSceneFaded` has returned. Do **not** rely on `[DefaultExecutionOrder]` or component order.

### A7.2 CRITICAL — the boon panel is a headset-level soft-lock

Once the offer panel is built, the only exit from the run is a successful `Button.onClick`, and three
independent conditions make that click impossible:

* **No `EventSystem` in the arena scene.** `TrackedDeviceGraphicRaycaster` needs an `EventSystem` +
  `XRUIInputModule`; the only thing that creates one is the main-menu builder. `GameOverPanel` gets
  away with this because it races an auto-dismiss timer — there, clicks are optional. Here they are
  mandatory.
* **The canvas is world-anchored once** at `camera.position + forward * 1.3` and parented to nothing.
  Snap-turn, teleport or walk away and it is behind the player or inside a wall, permanently.
* Only the `Camera.main == null` case currently falls back to auto-advance.

**Fix, all three:**
1. `RoguelikeArenaBuilder` must create the XR UI `EventSystem` in the arena scene (the builder helper
   already exists — it is simply only called from the main-menu builder today).
2. `BoonOfferPanelFactory.Build` returns null when `EventSystem.current == null`, and `RunDirector`
   treats null as auto-advance **with a loud warning**.
3. A generous unscaled fallback timer in `RunDirector` (60 s) auto-picks choice 0 and advances, giving
   the offer the same safety net the game-over panel has.
4. Re-anchor the panel to the head **by position only**, every frame. Never rotate the world around
   the player; no shake.

### A7.3 Wire A4.2 — the seams exist and nothing calls them

`RunState.NotePlayerHealth`, `PlayerHealth`, `ClearPlayerHealth`, `HealAmountForForge`,
`CarryOverHealth` and `Health.SetCurrent` all now exist on disk with **zero callers**. Consequences
today: the player enters all 15 rooms at full HP; `ApplyForgeReward`'s
`Heal((Max - Current) * 0.6f)` is **provably exactly 0 every time**, so the Forge's whole reward is
one reroll token; and every Ironskin holder gets a free full heal at the start of every node.

Wire exactly as A4.2 specifies: `NotePlayerHealth(health.Current)` in `AdvanceNode` before the scene
load starts; after `Configure`, restore with
`health.SetCurrent(RunState.CarryOverHealth(RunState.PlayerHealth, health.Max))` when
`PlayerHealth >= 0`; `ApplyForgeReward` uses `RunState.HealAmountForForge(current, max)` then
re-stores. Delete the now-stale doc comment claiming no durable HP field exists.

Also document the load-bearing invariant at the `health.Configure(health.Max + MaxHealthAdd)` call
site: it is correct only because `VRRig` is a per-scene component with no `DontDestroyOnLoad`, so
`health.Max` is the prefab base on every load. If a rig ever survives a load this becomes
`base + 15 x add`.

### A7.4 `HealOnKill` has no consumer — Bloodletter is inert

A4.1 step 5 required it and `OnEntityDied` does the opposite, early-returning on anything that is not
the player. Add the enemy-death branch before the player check (~4 lines; the subscription already
exists and is already balanced).

### A7.5 The arena rig is missing every component the boons drive

This is the largest single gap and it is a **Module 5 requirement**:

* `XRRigBuilder.BuildRig` adds `XROrigin`, `CharacterController`, `ContinuousLocomotion`,
  `WallClimbLocomotion`, `VRRig`, `Health` — but **not** `PlayerCombatModifiers`. So today every
  damage/parry-flow/combo boon silently does nothing.
* The five ability components (`WeakpointSight`, `OverdriveController`, `PhaseStepController`,
  `UnbrokenWard`, `MirrorSummonController`) are added only by `AttachPlayerAbilities`, which the
  chapter builders call. All five correctly gate on `CampaignState.HasAbility` in `Awake` — so the
  unlock half works, but **on a rig without the components all five Epic boons do literally nothing**.
* `ParryFlowController` and `ComboMomentumController` are added by **no builder at all** (they were
  hand-wired into eight scenes post-build), so `FlowInitiate`/`ComboInitiate` are dead too.

**Fix:** `RunDirector` uses `GetComponent<PlayerCombatModifiers>() ?? AddComponent<...>()` per A4.1.
`RoguelikeArenaBuilder` must call `AttachPlayerAbilities(rig, refs)` after `BuildRig` **and** add
`ParryFlowController` + `ComboMomentumController`. Without this the entire Epic tier is cosmetic.

### A7.6 Meta stat upgrades above level 3 are discarded

`ApplyStartingBonuses` adds one `Ironskin`/`KeenEdge` stack per upgrade level, but
`BoonDefinition.maxStacks` defaults to **3** while `StartingHealth`/`StartingDamage` go to level **5**
— so levels 4 and 5 are silently thrown away. Worse, `RunState.BoonCount` (uncapped) then disagrees
with `BoonInventory.Stacks` (capped), and since `BoonCount` is what feeds `BoonOfferPicker`'s
`stacksHeld`, **Ironskin and KeenEdge drop out of the offer pool for the whole run** once the upgrade
is bought out.

**Fix:** Module 5 authors `Ironskin` and `KeenEdge` with `maxStacks = 8` (5 meta + 3 in-run headroom),
and A1.8's catalog test asserts the cap as well as A6.3's magnitudes.

### A7.7 Epic boons must not mutate the campaign save

`GrantBoon` calls `CampaignState.UnlockAbility`, which is persistent and never revoked, and every
arena arrival autosaves. So a run pick **permanently unlocks a story ability in the player's campaign
save**; and conversely a player who finished the campaign already has all five, making the rarest
tier a dead pick for them. My original contract mandated this — the contract was wrong.

**Fix:** a run-scoped overlay. `RunState` gains `GrantAbility(string)` / `HasGrantedAbility(string)`,
cleared with the run. Add `Ronin7.Core.AbilityAccess.Has(string id)` returning
`CampaignState.HasAbility(id) || RunState.HasGrantedAbility(id)`, and change the five ability
controllers' `Awake` gate to call it (one line each). `GrantBoon` calls `RunState.GrantAbility`,
never `CampaignState.UnlockAbility`. Nothing touches the campaign save.

### A7.8 Fail loudly, not silently

Four independent paths currently produce "the run had no boons and printed nothing": a null
`boonCatalog` (all four call sites early-return), a missing `PlayerCombatModifiers`, a null
`VRRig.Instance` (A4.1 step 1 required a warning and there is none), and a null `EventSystem`. Each
gets a `Debug.LogError`/`LogWarning`. A silent no-op is the worst failure mode in a headset.

### A7.9 Timing and polish

* **Apply boons under the black fade.** `ApplyBoonInventoryToRig` currently runs only after
  `LoadSceneFaded` fully returns — after the camera wait and the entire fade-in — while
  `RunArenaController.Start` has already spawned and activated the enemies. The player is live,
  visible and attackable for the whole fade-in **at base stats**, then gets a visible instant full
  heal when it completes. §3 requires assembly under the fade. Hook `SceneManager.sceneLoaded`, or
  split `LoadSceneFaded` so there is a hook between load and fade-in.
* **Skip the pointless double fade** on zero-enemy nodes: the in-flight routine still plays its whole
  fade-in tail before `pendingScene` is consumed, so Forge/Treasure rooms fade fully in and
  immediately back out — about six times a run.
* **Idempotence:** `DismissOfferPanel()` as the first statement of `BeginBoonOffer` (today a second
  offer orphans a live canvas in the player's face with its handler still subscribed), plus
  `lastClearedNode` so a repeated `RoomCleared` for the current node cannot double-award echoes.
* **HUD:** `RunEnded` is published before `RunState.End()`, so `RunHudPanel.Refresh()` still sees
  `InRun == true` and displays a dead run's stats for the rest of the hub session. Have
  `OnRunEnded` clear the text directly rather than re-rendering.
* **Button text overflow:** `VerticalWrapMode.Overflow` on a 0.28 x 0.32 m button means a 3-4 line
  `[TextArea]` description spills over the adjacent choice, and the spilled text is not on that
  button's raycast target — so it reads as a label for the wrong boon. Use `Truncate` and give the
  description its own smaller sub-label.
* `boonInventory` is re-`new`-ed every load while its own comment and A1.1 say one per run. Behaviour
  is equivalent (`RunState.Boons` is the durable ledger) but fix the contradiction.
* `GrantStartingBoons` always grants the first N Commons in array order — identical every run. Use
  `RunRng`.

### A7.10 Required test seams

Flow already has `InternalsVisibleTo`, so these are free. Extract and gate:
`ResolveOffer(int choiceCount, bool hasCamera, bool hasEventSystem)` (the A1.6 auto-advance gate —
currently the soft-lock decision is unreachable from a test), `ShouldLatchSceneLoad`/`ConsumePending`
(the latch is where a bug is a run-ending soft-lock), `StartingStacks(int upgradeLevel, int maxStacks)`
(would have caught A7.6 immediately), and `ShouldHealOnKill(dead, playerRig, healOnKill)`.
Plus all five of A4.3's tests, which remain absent.

---

## Amendment 8 — Phase 0 "ship-safe" behaviour changes (Roguelike-Roadmap.md P0-01 … P0-12)

Binding as of tag `roguelike-phase0` (0.1.0). Each item names the task that landed it.

- **A8.1 Campaign autosave never runs during a run (P0-01).** `GameFlowManager.ShouldAutosaveOnArrival`
  takes `inRun`; run nodes no longer write the blank `CampaignState` over the campaign slot. The settings
  panel's slot SAVE buttons are also refused while `RunState.InRun` (P0-05).
- **A8.2 Zero-enemy clears wait for the rig (P0-02).** A Forge/Treasure `RoomCleared` that arrives during
  an arena load, before `onLoaded` restores the carried HP, is parked and replayed by
  `RunDirector.OnArenaLoaded`. Forge result = carried HP + 60% of missing (`ForgeResultHealth`).
- **A8.3 Pause (P0-03, Meta VRC).** `RunPauseController` (self-installed, persistent) pauses a run on focus
  loss (device builds), `OnApplicationPause` and the left Menu button: `TimeScaleChannel.Pause` = 0
  (appended), the boon-offer fallback timer frozen and its panel hidden, `AudioListener.pause`, and the
  pause panel. Play resumes **only** on RESUME. At scale 0 the arbiter keeps the baseline
  `fixedDeltaTime`. A held blade is disarmed while its hand is untracked or the game is paused and
  re-arms after 3 tracked frames (`Grabber.BladeArmed`); `BladeDamager.OnEnable` resets the speed EMA.
  In RunArena the Menu button belongs to the pause; the settings panel opens from the pause panel.
- **A8.4 Leaving a run (P0-04).** SAVE & QUIT / Return-to-Menu during a run call
  `RunDirector.SuspendRun`: the node-entry snapshot is kept and RESUME RUN restarts that node (D-09).
  ABANDON from the pause panel needs a 1 s hold and ends the run as a loss. The menu ABANDON is two-click
  (CONFIRM ABANDON?), only banks the saved run, and **no longer starts a new run**. Enemies outside the
  arena (|x| or |z| > halfExtent + 0.5, or y < −2) are killed by a 1 Hz sweep so the room clears.
- **A8.5 Post-processing (P0-06).** Post renders on PCVR and Quest 3/3S/Pro, not on Quest 2 (which also
  turns HDR off); `GraphicsDirector` sets it per scene load.
- **A8.6 Quest display (P0-07).** 90 Hz and fixed foveation (Quest 3 class 0.75, Quest 2 0.5) through the
  OpenXR display subsystem; no realtime shadows on Quest; PCVR shadows 20 m / 2 cascades.
- **A8.7 Spawn data (P0-08).** Roguelike-owned `RL_` definitions replace story assets that were not
  combat-viable (`RL_DreamGhost`, `RL_GraveGuardian`). Story `Ch*` definitions stay untouched.
- **A8.8 Saves (P0-09).** A discarded `run.json` banks its Echoes; a newer `meta.json` is never
  overwritten; `MetaProgression.Save` before `Load` is refused; the run summary is finalized only when the
  run-end scene has loaded. Phase fixtures in `Project/TestData/saves_phaseN/` must keep loading (G7).
- **A8.9 Build content (P0-10, D-02 default).** Only `Phase6_Boot` and `RunArena` ship; campaign entry
  points are hidden and refuse scenes not in the build.
- **A8.10 Telemetry and dev tools (P0-11).** Every finished run appends one line to
  `persistentDataPath/runlog.jsonl` (local only). Dev shortcuts exist only under `RONIN7_DEV`.

## Amendment 9 — Phase 1 combat/data numbers (Roguelike-Roadmap.md P1-01 … P1-15)

Binding as of Phase 1 (`0.2.0`). The roadmap text called this "Amendment 8"; 8 was taken by Phase 0. Each
item names the task that landed it.

- **A9.1 Damage filter hook (P1-01).** `Health.DamageFilter` (`Func<DamageInfo,float>`, single slot, null =
  unchanged) runs first, then `Health.IncomingDamageMultiplier` (default 1). A result that is not > 0
  (including NaN) emits no events. `Damaged` / `EntityDamaged` / `EntityDied` carry the *applied* amount.
  `DamageInfo` gained `Speed` (blade m/s behind the hit, 0 for non-blade).
- **A9.2 Overdrive (P1-01, P1-06).** Explicit 1.5x damage via `PlayerCombatModifiers.OverdriveMultiplier`
  (inside the 3x transient cap with ParryFlow x Combo; the old accidental bonus from a time-scaled fixed step
  is gone). Swing speed divides the fixed step by `Time.timeScale` (`BladeDamager.RealFixedDt`; 0 when
  paused), so it is time-scale independent. The perfect-parry window runs on `unscaledDeltaTime`.
- **A9.3 Unbroken (P1-07).** Once per run in a run (`RunState.UnbrokenUsed`, persisted as the appended
  `RunSnapshot.unbrokenUsed`; `CurrentVersion` stays 1, legacy files read false). The death interceptor tries
  Unbroken first (revive at 1 HP, fraction 0), then Second Wind (30%).
- **A9.4 Weakpoint Sight (P1-06).** No longer a flat damage slot. A blade hit is *precision* when the hit
  point is in the top 20% of the victim's bounds (head) or the blade velocity is within ~46° of its axis
  (thrust, dot > 0.7). Precision x1.25; x2.0 while Sight is active; body hits unchanged (x1).
- **A9.5 Boon picker (P1-08).** Tier first: pick a tier by node weights (independent of boons per tier), then
  uniformly within it. An empty tier falls to the next tier down (weight > 0 only), then up. Abilities
  already granted this run are never offered. `AbilityAccess.Has` reads `RunState` only in a run and
  `CampaignState` only outside one.
- **A9.6 Elite rooms (P1-09).** One elite lead (eliteOnly pick if eligible, else the highest-`maxHealth`
  eligible trash) plus escorts from non-eliteOnly trash: 2 in sectors 0-1, 3 in sector 2.
  `RL_HumanoidAutomaton` and `RL_IronWarden` are `eliteOnly`, `minDepth` 2 (were 5, not elite-only).
- **A9.7 Boss bands (P1-10).** Boss nodes use total HP by sector {450, 650, 900} and damage multiplier
  {0.9, 1.1, 1.3} on base damage (out-of-range sectors clamp); other kinds keep the depth multipliers.
  Maelgorn base damage 16 -> 24 (1.3x = 31.2 vs sector-1 VaneWraith 24 x 1.1 = 26.4).
- **A9.8 Posture (P1-05).** `postureMaxFraction` x scaled max HP: trash 0.6, heavies 0.5 (HumanoidAutomaton,
  IronWarden, GraveGuardian, RedactionConstruct), bosses 0.35 (Warden, VaneWraith, Maelgorn).
- **A9.9 Super armor (P1-04).** `superArmor` on HumanoidAutomaton, IronWarden, Warden, VaneWraith, Maelgorn:
  a hit during windup does not flinch-cancel it.
- **A9.10 Telegraph (P1-14, P1-15).** An attacker outside +-50° of the player's forward yaw gets +0.25 s
  telegraph (`MeleeAttacker.TelegraphFor`). The parry window is 0.75 x the attack's actual telegraph. The
  windup pose is a smoothstep reaching full draw at 0.7 x that telegraph (ahead of the parry window); the
  strike pose is an ease-out over the first 35% of Active. A 3 cm additive blade-edge line shows in
  Windup/Active.
- **A9.11 Data ownership (P1-02).** Every spawn-table enemy has its own `RL_<Name>` `EnemyDefinition`
  cloned from its story source; the roguelike tunes only these (DreamGhost 90 HP / 12 dmg, GraveGuardian
  120 HP). Story `Ch*` definitions stay untouched. `EnemyDefinition` gained timing, poise, weakness and pose
  fields.
- **A9.12 Enemy textures (P1-12).** Albedo / MR / Normal extracted to PNGs with ASTC on Android, max size
  <= 1024, mipmaps on (`EnemyTextureTests` guards all 14 materials).

## Amendment 10 — Phase 2 numbers (Roguelike-Roadmap.md P2-01 … P2-11)

Binding as of Phase 2 (`0.3.0`). Each item names the task that landed it.

- **A10.1 Echo shop (P2-01).** `MetaProgression.UpgradeCost` / `TryPurchase`. Costs: Starting Health and
  Starting Damage 40 / 60 / 80 / 100 / 120 (5 levels); Starting Boon 80 / 150 / 250; Reroll Tokens
  50 / 100 / 150. SHRINE button and "ECHOES n" label on the boot menu.
- **A10.2 Arena size (P2-05).** `RunScaling.ArenaHalfExtent`: Combat 8 m, Elite 9, Boss 11, Forge / Treasure /
  Shop / Event 5.
- **A10.3 Waves (P2-07).** `WaveComposer.SplitWaves(n, 3)`: waves of <= 3; the next wave is due when
  alive <= 1. `RunScaling.MaxConcurrentEnemies` stays 4.
- **A10.4 Front-arc spawns (P2-06, BUG-24).** Enemies are placed 4-7 m from the head inside a 70 deg
  half-angle front arc (substream `RunRng.ForNode(seed, nodeId*31+k)`; pre-rolled point if none fits),
  telegraphed by a floor disc and audio for 1.2 s before activating.
- **A10.5 Branching map (P2-04).** Rows and weights as in section 1 (rows 1-2 weights 40/25/15/10/10 for
  Combat/Elite/Event/Treasure/Shop; <= 1 Treasure and <= 1 Shop per sector across rows 1-2; row 3 = Forge +
  Shop). Golden kinds, seed 2026 (`|` separates rows, 3 sectors, 27 nodes):
  `CBT | ELT SHP | CBT TRS ELT | FRG SHP | BOS | CBT | ELT ELT | EVT CBT CBT | FRG SHP | BOS | CBT | CBT SHP CBT | ELT TRS | FRG SHP | BOS`.
  `RoomKind` appends `Shop`, `Event`; `RunNode` gains `Lane` and `Depth`. Snapshot v2 adds `path`, `depth`,
  `maxHealthBonus`; v1 runs are discarded and their Echoes banked (A8.8). Fixture `TestData/saves_phase2`.
- **A10.6 Shop and events (P2-04).** Boons cost 30 / 60 / 120 run-Echoes by Common / Rare / Epic; MEND 25%
  costs 40. Events: Blood Pact (-15 max HP, Rare boon); Gamble (50% Epic boon, else 25 damage); Meditate
  (heal 30% of max); Forgotten Shrine (spend 40 Echoes, +1 reroll, heal 15%).
- **A10.7 Forge, sector, beats (P2-02).** Forge is revealed: MEND heals 60% of missing, TEMPER gives +1
  reroll (until P4-07). Treasure shows a single TAKE card. Sector change heals 20% of missing and shows a
  2.5 s title, then CONTINUE / SAVE & QUIT. Death beat 1.2 s, victory beat ("FRACTURE CLOSED") 2 s, both
  unscaled, no shake.
- **A10.8 Onboarding (P2-08).** `MetaSaveData.tutorialDone` (default false). Node 0 of a first run spawns
  exactly 2 Scavengers and shows the north-wall prompt sequence; set on the first `RoomCleared`.
- **A10.9 Comfort (P2-09).** Seated adds +0.45 m eye offset (`GameSettings.SeatedEyeOffset`), relaxes the
  posture guard and forces the gate panel fallback; Left-Handed mirrors HUD mount, holster and parry hand;
  locomotion Smooth / Teleport. Reachable from the boot menu.
- **A10.10 Damage latency (P2-10, BUG-29).** The enemy strike resolves when the strike pose lands, at 0.35 of
  Active, instead of at the end of Active; a landed blow closes the parry window for the follow-through. The
  parry window shrinks from ~1.0 s to ~0.48 s. Damage -> feedback was already same-frame.
- **A10.11 Movement tuning (P2-11).** `EnemyDefinition` turnRateDegrees / acceleration / deceleration:
  humanoid default 240 deg/s, 4, 6; drones 300 / 3 / 3; bosses 150 / 2.5 / 4. `RoguelikeDataBuilder` keeps
  these overrides.
- **A10.12 Phase 2 exit (D-02).** `RunLauncher` and the hub patch (`RoguePatchHubLauncher` plus the hub's
  RunLauncher object) are removed; the build ships `Phase6_Boot` + `RunArena`. EditMode baseline after
  removal: 1518 (no tests referenced them).
- **A10.13 Device numbers (G5/G6).** Quest 3S, release 0.3.0 (code 3), 2026-10-14. G5: the user completed a
  full run to the win screen on Quest and also did the room-1 death run and the PCVR-via-Link runs ("it's
  fine"). Logcat for the Quest session had 0 exceptions, 0 "couldn't be loaded", 0 shader errors. The only
  warning was "Main Camera does not use a Tracked Pose Driver" (BUG-37). The meta save after the win was
  37 -> 321 Echoes and 4 -> 5 runs completed. G6 (VrApi logcat, release build, 98 samples over ~1.5 min of
  gameplay): FPS avg 90.3, min 88, max 91 at 90 Hz. Stale frames: 0 in 91 samples; the rest were
  1, 1, 1, 2, 2, 3, 4, about 14 frames of ~8,700 (~0.16%, under the 1% limit). This is not the full 2-minute
  4-enemy OVR Metrics capture. G8 pause: confirmed by the user. Known issues carried to Phase 3: BUG-30..38
  (see roadmap P3-00).

## Build status — 2026-09-19

### Compile

All six runtime assemblies plus the editor and test assemblies compile clean. Four rounds of real
errors were found and fixed by the first compile, none of which any of the four Opus reviews caught
(they are compiler facts, not logic defects):

| Round | Errors | Cause |
|---|---|---|
| 1 | 2 | `RunState.HealAmountForForge`/`CarryOverHealth` were `internal` but called from `Ronin7.Flow`, a different assembly. `InternalsVisibleTo` only grants the test assembly. **Spec error in A4.2** — the amendment asked for `internal` for testability without noticing Flow needed them. Now `public`. |
| 2 | 2 | `CS0051` — public NUnit test methods cannot take an `internal` nested enum as a parameter even with `InternalsVisibleTo`. Table-driven cases now pass the member name and compare `ToString()`. |
| 3 | 9 | `CS0104` — `Object` is ambiguous when a file has both `using System;` and `using UnityEngine;`. Qualified as `UnityEngine.Object`. |
| 4 | 0 | Clean. All four `Tools/Space Samurai/Roguelike/...` menu items register. |

### EditMode gate — first run

```
passed=1209  failed=2  skipped=11  total=1222
```

Against the documented 842 baseline that is **+380 tests**. The 11 skips are the asset-invariant
tests (A1.8) correctly self-skipping via `Assert.Ignore` because the baking menus have not been run
yet — by design, so a fresh checkout does not fail the gate.

Both failures were genuine:

1. **`ArenaGeometryBuilder` MeshCollider strip was a no-op outside play mode.** It used
   `Object.Destroy`, which does nothing in edit mode — so it logged "stripping it" and shipped the
   collider anyway. This is the *same* trap A5.2 caught on the ceiling collider, reintroduced in the
   new strip code. CLAUDE.md forbids MeshColliders reaching scenes, so this mattered. Fixed by
   branching on `Application.isPlaying`.
2. **Float equality in the revive test.** `100 * 0.3f` is `30.0000019`, asserted with exact
   equality. The product behaviour was correct; the assertion was not. Now uses a tolerance.

### What each verification layer actually caught

The three layers found disjoint defect classes, which is the argument for running all three:

* **Opus reviews** — design and integration defects invisible to tooling: the `DeathInterceptor`
  collision silently disabling `UnbrokenWard`, the meta-currency wipe on New Game, the boon panel
  with no reachable exit, enemy counts wrong for room-scale VR.
* **Compiler** — accessibility and namespace facts that reading does not reliably surface.
* **Tests** — runtime behaviour both had read straight past (the MeshCollider no-op).

### PlayMode gate — corrected baseline

`CLAUDE.md` records "PlayMode: 70/70 green". That figure is **stale**: this project contains
**7** PlayMode tests in total (`KatanaHolsterTests` 2, `ProjectileHitTests` 3, plus the 2 added here
in `RunArenaLifecycleTests`). Verified by running the suite on 2026-09-19.

Result: `total=7 passed=5 failed=2`. Both failures are `ProjectileHitTests` and are **not** game-code
defects — they are collateral from the `ai-game-developer` MCP plugin emitting
`Debug.LogError` ("Authorization failed. Token may be missing, invalid, or revoked" /
"Version handshake failed") while the suite ran. Unity's test framework fails any test on an
unhandled `LogError`, and the stack traces sit entirely inside `com.IvanMurzak.Unity.MCP`. With that
plugin disconnected or its token restored, all 7 pass.

**The two new arena-lifecycle tests passed**, covering the full room cycle: instantiate N inactive,
activate `min(N, 4)`, kill one, confirm a reinforcement releases, kill the rest, assert `IsCleared`
and exactly one `RoomCleared` published — plus the table-of-null-prefabs case still clearing instead
of soft-locking.

**Batch mode cannot run PlayMode here.** `-runTests -batchmode -testPlatform PlayMode` exits
immediately with code 1 regardless of whether the editor holds the project lock — it is the
graphics-device requirement CLAUDE.md already notes, not a lock conflict. Run it **windowed**
instead: `Unity.exe -runTests -projectPath "Project" -testPlatform PlayMode -testResults out.xml`.


## Amendment 11 — Phase 3 weakness contract and soft-lock gate (Roguelike-Roadmap.md P3-01 … P3-08)

Binding as of Phase 3. The Phase 3 brief called this "Amendment 9"; 9 (Phase 1) and 10 (Phase 2) are taken.

- **A11.1 WeaknessKind is append-only.** The enum is serialized in `EnemyDefinition.cs`: `None, Backstab,
  HeavyBreak, Recovery, Alternating, Reflect, Linchpin, ParryGate, CounterHit, PostureOnly, Blink`. Never
  reorder or remove. `WeaknessRules.Evaluate(kind, ctx, heavySwingSpeed)` is pure and returns Neutral /
  Resisted / Exploited:

  | Kind | Exploited | Resisted | Else |
  |---|---|---|---|
  | Backstab | staggered, or hit from >= 110 deg off forward | front hit | - |
  | HeavyBreak | swing speed >= `heavySwingSpeed` | slower swing | Neutral from behind |
  | Recovery | staggered or recovering | attacking | Neutral |
  | Alternating | opposite side to the last hit | same side as the last hit within 4 s | Neutral (first hit or > 4 s) |
  | Reflect | player-sourced Projectile | - | Neutral |
  | ParryGate | staggered | not staggered | - |
  | CounterHit | attacking, staggered, Weakpoint Sight active, or ally source | otherwise | - |
  | PostureOnly | never | always (HP x0.2) | posture gain x5 |
  | Blink | within 1 s of a blink | - | Neutral |
  | None, Linchpin | - | - | Neutral (Linchpin's death effect is wired elsewhere) |

- **A11.2 Soft-lock gate.** `WeaknessRulesTests.EveryWeaknessKind_HasAbilityFreeDamagePath` enumerates every
  `WeaknessKind` and brute-forces both facings, slow/fast swings, both
  hit ages and all 256 flag combinations; each kind must reach a multiplier >= 1 with no ability. A newly
  appended kind fails the suite until it has such a path. The one exception is `PostureOnly`: its HP is always
  Resisted, so its path is the posture break (`PostureOnlyPostureMultiplier` >= 1 is asserted instead).
- **A11.3 FilterDamage contract (`Enemy.FilterDamage`, installed as `Health.DamageFilter` only when
  `weakness != None`).**
  - Posture-break bonus damage (`PostureMeter.ApplyingBonus`) passes through unchanged: it is not a new hit.
  - **Resisted: no flinch and no posture.** `NextHitGainScale = 0` and a `HitResisted` event; HP x
    `resistMultiplier` (PostureOnly: x0.2, with posture scale x5 instead).
  - **Exploited:** `WeaknessExploited` event; HP x `exploitMultiplier`. HeavyBreak and ParryGate force-stagger
    **only if the enemy is not already staggered**. This deviates from the P3-02 spec, which would re-stagger
    on every exploit, because that allows a stunlock.
  - **Melee uses the hit point, not the rig root,** as the source position (roomscale puts the player rig
    root metres from the sword). Other damage types use the source transform.
  - **Reflect** exploit deals `Max HP x exploitMultiplier` (a fraction of max health), so it works on any HP
    pool.
- **A11.4 Reflect uses player-owned bolts.** `Projectile.Reflect` (blade speed >= `ReflectMinSpeed` 2.5 m/s)
  hands ownership to the player root, re-aims at the firer, and doubles speed, once per bolt. The hit then
  reaches `Health` as a player-sourced `Projectile`, which is what `WeaknessKind.Reflect` exploits.
- **A11.5 Elite affixes (`EliteAffixes`).** `Armored, Swift, Unflinching, Vampiric, Volatile` (append-only:
  the values are part of the seeded pick order). Picked from their **own RNG stream,
  `RunRng.ForNode(runSeed, nodeId ^ 0x0E117E)`,** never the map or spawn stream, so existing golden vectors
  cannot move. One affix in sectors 0-1, two from sector 2; **never Swift together with Volatile**. Applied to
  the runtime definition clone only, never a shared asset. Swift leaves `telegraphTime` untouched (fairness).
  Volatile's blast tests horizontal (XZ) distance only, radius 2 m, 1.2 s floor ring.
- **A11.6 Boss phases scale by weight share.** Each phase asset's own `maxHealth` is only a weight: phase i gets
  `RunScaling.BossTargetHealth(sector) x weight_i / sum(weights)` (`RunArenaController.PhaseHealths`; any
  non-positive weight falls back to equal shares). The fight's total is therefore always BossTargetHealth.
- **A11.7 Boss adds (A5.1 / A5.5).** Adds are pre-instantiated **inactive at Start** with every other enemy
  (A5.5: nothing is instantiated mid-fight) and held in `reservedAdds`, out of the queue. The first
  `BossPhaseChanged` makes `RunArenaController` release them as one extra wave through the normal wave path.
  The boss never spawns adds itself (A5.1: spawning is the arena's job). A single-phase boss never reserves
  adds. Every phase clone is destroyed on death (A5.7).
- **A11.8 Health bars, no damage numbers.** Bosses get a runtime-built north-wall bar showing the whole fight
  (all phases) with phase-threshold markers; non-bosses get `EnemyHealthBar` (shown on first hit, hidden 4 s
  after the last, yaw-billboard). Both use the shared unlit material with colour via MPB (no Canvas, no new
  Material). There are no floating damage numbers: feedback is haptics, audio (`HitResisted`,
  `WeaknessExploited`) and the bars.
- **A11.9 Save format.** Phase 3 added no serialized save fields: `RunSnapshot.CurrentVersion` stays 2 and
  `MetaSaveData.CurrentVersion` stays 1. Fixture `TestData/saves_phase3`.

## Amendment 12 — Phase 4 stats, skills, boons, meta and save v3 (Roguelike-Roadmap.md P4-00 … P4-11)

(roadmap called this Amendment 10) Binding as of Phase 4 (`0.5.0`). 10 (Phase 2) and 11 (Phase 3) were already taken.

- **A12.1 BUG-39 weakness tells (P4-00).** Colour rule: **gold = open now, blue-grey = guarded**, applied as HDR
  MPB tints via `RendererTint` (no new material, no light). `WeaknessRules.IsOpen(kind, staggered, attacking,
  recovering, recentlyBlinked)` is the pure state test (Recovery: staggered or recovering; Backstab and ParryGate:
  staggered; CounterHit: attacking or staggered; Blink: recently blinked; the rest are false because they carry their
  own marker). `RestsGuarded` kinds (ParryGate, CounterHit, PostureOnly) rest blue-grey whenever not open. Per-kind
  markers: Alternating shoulders (gold on the side opposite the last hit), Backstab back plate, HeavyBreak slab
  (flickers blue-grey on a too-slow hit, hidden while shattered), Construct chest crack (grows with posture, gold at
  the break).
  - **First-encounter teach is per `WeaknessKind`, not per archetype** (`WeaknessTeach`): on first aggro of a kind the
    marker pulses 1.5 s and the wrist HUD shows one short hint (<= 5 words, 4 s); the first `WeaknessExploited` of
    that kind plays a **0.5x slow-mo for 0.4 s real time** on the appended `TimeScaleChannel.WeaknessTeach`, restored
    on expiry, disable, app pause and mode change. Seen kinds persist in the appended `MetaSaveData.seenWeaknesses`.
  - **Hit-confirm separation:** a resisted or exploited hit suppresses the generic `SwordImpact` haptics and audio on
    the same frame (`HitConfirmLatch`). Exploit = double haptic beat (0.8/0.08 s, then 1.0/0.1 s at +60 ms) and a
    +3 dB clip; resisted = one tiny tick on the sword hand. No camera shake, no damage numbers.
- **A12.2 BUG-40 archetype marks and art height (P4-00).** One unlit accent primitive per trash archetype
  (`MarkSpecs`, e.g. shield trooper cyan shield slab, scan drone green halo disc, Ganger magenta waist ring — removed by A13.8), HDR, no
  collider, no shadows, +1 draw per enemy; the Scavenger stays unmarked and bosses are left as-is. The **art-height
  fix**: skinned-mesh renderer bounds carry ~0.2 units of rig padding per side, which left humanoids 1.28 m tall with
  feet 0.26 m off the floor; art is now sized from the mesh (humanoids 1.8 m grounded, drones 0.8 m hovering) and the
  root stays at y = 0 (`RunArenaController` overwrites its position at spawn). `RunLog` (BUG-41) gained run
  start/abandon, sector, per-archetype spawn/kill, elites and affixes, resisted vs exploited hits and boon offers.
- **A12.3 Stat slots and sheet (P4-01/02).** `BoonEffectKind` is append-only and pinned by
  `BoonEffectKind_ExistingValues_ArePinned`. `PlayerCombatModifiers.Boon(kind)` holds a bonus fraction (0 = neutral)
  per kind, written on every load and pick; consumers read at use time, never cached in `Awake`. Caps, enforced by
  each consumer: EdgeSensitivity thresholds x0.70; PostureDamage x2.0; Precision x2.0; ParryWindow +0.04 s;
  StaggerDuration x1.5; DamageReduction 40%; AbilityHaste 40%; PhaseCharges 3 (base 1); EchoGain x1.5; Luck 3
  (+4 Rare / +2 Epic weight per point). Excluded on purpose: move speed, attack speed, reach, random crit, flat armor.
- **A12.4 Art input slots and the modal gate (P4-03).** Weapon Art = right trigger with the sword held; Off-hand Art
  = left trigger with an empty left hand; one Art per slot, a new one replaces the old; A, B, X get nothing new.
  `ArtSlots.ModalOpen` (set by `RunDirector`) blocks firing while a boon/choice panel, the pause menu or the summary
  is up. Twin Wakizashi sets `OffhandBlocked`. `RunState.WeaponArtId` / `OffhandArtId` are snapshotted (v3).
- **A12.5 Skills S1-S8 (P4-04).** S1 Riposte, S2 Quick Draw, S3 Zanshin: boon-granted passives. S4 Charged Slash and
  S5 Blade Throw: Weapon Arts. S6 Kiai Palm, S7 Chain Hook, S8 Element Imbue: Off-hand Arts. Numbers as in the
  roadmap table except these calls made during implementation:
  - **Quick Draw's x2.5 sits outside the 2x boon damage cap** (it multiplies on top, like a gesture bonus).
  - **Chain Hook has a 5 s cooldown and a 0.2 m radius sphere cast** (8 m range); it pulls the enemy, never the player.
  - **AbilityHaste applies to every skill cooldown** (`SkillRig.CooldownScale`), not only Phase Step / Mirror / Overdrive.
  - Hand speed under slow-mo is converted to real m/s (`SkillRig.RealVelocity`).
  - Phase Step has 0.2 s of invulnerability during the fade; `Health.IsInvulnerable` is the shared signal and
    `MeleeAttacker.LandHit` / `Projectile` publish no `PlayerHit` while it is set (1f792a6).
- **A12.6 Boon catalog, 45 boons (P4-05/06).** 15 existing + 30 new. Tags: Parry, Flow, Bleed, Storm, Executioner,
  Ability, Bulwark, Curse. `BoonRarity.Duo = 3` (append-only). The picker gives x1.5 weight within a tier to boons
  sharing a tag with owned boons; a Duo is eligible only when every `requiresTags` tag is owned (Thunder Riposte =
  Parry + Storm, Crimson Tide = Bleed + Flow, Ghost Step = Ability + Parry). Three Curses (Glass Edge, Blood Price,
  Hollow Oath) have a negative secondary; Reckless Flow is Parry + Curse. `DamageType.Status` (DoT) never flinches,
  never adds posture, and never re-procs `RunBoonProcs`. Combo Initiate is now **Reversal** (BUG-20 fix). Offer rates
  were tuned from the runlog (P4-06).
  - **Packs are derived from tags, not stored per boon:** the Bleed, Storm and Executioner tags map to `BoonPackId`
    (`pack_bleed`, `pack_storm`, `pack_executioner`). **Pack locks:** `BoonOfferPicker.IsPackUnlocked` hides a locked
    pack's boons from offers and starting boons; Quick Draw (Executioner) therefore appears only after that pack is
    bought. Kensei, Dancer, Shadow and Bulwark boons are never locked.
- **A12.7 Forge Tempering (P4-07).** One temper per boon (`MaxTemper = 1`); magnitude x1.5 (`1 + 0.5 x level`) on
  **both primary and secondary**; **curses can be tempered** (they are Rare); Epic boons (ability grants) cannot.
  Stored as `RunState.TemperLevels`, one boon id per level.
- **A12.8 Loadouts (P4-08).** Katana (default), Nodachi (2.0 / 7 m/s, 14-60, 0.35 s) and Tanto (1.0 / 4.5 m/s,
  6-28, 0.18 s) follow the roadmap. **Twin Wakizashi deliberately does not use the roadmap's 6-26 each:** the user's
  rule is **0.6x damage per blade and 1.25x swing speed** (implemented as 4.8-24); the Off-hand Art slot is disabled
  while it is equipped. Switching to 6-26 means editing the asset and `LoadoutAssets_MatchRoadmapAndTwinDecision`.
  `WeaponDefinition.id` is the persisted `WeaponId`.
- **A12.9 Heat and Daily Fracture (P4-09).** Unlocked after the first win. Ranks (max): Sharpened Program 3 (enemy
  damage +15%), Hardened Shells 3 (HP +15%), Quick Telegraph 2 (x0.9, hard floor 0.6 s), Swarm Protocol 2 (+1 total
  enemies, never max concurrent), Rusted Forge 1 (MEND 30%), No Second Chances 1, Unforgiving Guard 1 (30% chip on
  non-perfect deflects), Escorted Bosses 1. `HeatRank` is append-only. Echo multiplier = 1 + 0.1 x ranks. **Daily
  Fracture** = murmur3 UTC date seed with the top bit set (random seeds never set it, so a resumed run knows it is a
  daily) plus the **fixed set Sharpened Program, Hardened Shells, Quick Telegraph, Swarm Protocol at rank 1 each
  (4 ranks, x1.4 Echoes)**. **Heat selection is locked while a run is saved** (`RunEntryOptions.HasSavedRun`); the
  live run's ranks are snapshotted.
- **A12.10 Shrine (P4-10).** Four tabs: UPGRADES, ARMORY, PACKS & KEEPSAKES, HEAT. Prices in Echoes: Nodachi 150,
  Tanto 150, Twin Wakizashi 300, Weapon Art slot 250, Off-hand Art slot 250, each archetype pack 120, each Keepsake
  200 (`KeepsakeCost`). A **Keepsake** starts the run holding an Epic the player picked in a past run
  (`pastEpics`, bought subset `keepsakes`, `selectedKeepsake`). The **Art selector** per slot works only once that
  slot is bought; `weaponArt` / `offhandArt` persist the pick.
- **A12.11 Currency split (P4-11, BUG-33, D-11).** **Shards** are the run currency: per-room reward = the old
  `EchoReward` (now `ShardReward`), Shop and event prices keep the same numbers, shown live on the HUD, lost at run
  end, never banked. **Echoes** are paid once at the result: `EchoResult` = **win 320, loss 60 + 6 x depth** (60 at
  depth 0 to 150 at depth 15); `EchoGain` (cap x1.5) and the heat multiplier apply at the result, not per room.
- **A12.12 D-12: two Shops per sector are allowed** (one mid-row Shop in rows 1-2, plus the row 3 Forge + Shop).
- **A12.13 Save format.** `RunSnapshot.CurrentVersion` = **3**. New v3 fields: `shards`, `weaponArtId`,
  `offhandArtId`, `temperLevels`, `loadoutId`, `heatRanks`; `echoesEarned` is written as 0. **Per-version migration**
  (`RunSnapshot.DiscardEchoes`): v1 and v2 snapshots bank their stored `echoesEarned`; any other version mismatch
  (v3+) banks the **loss result for the depth reached, never Shards**. `MetaSaveData` **stays v1** with appended
  fields only: `seenWeaknesses`, `unlockedPacks`, `unlockedWeapons`, `selectedLoadout`, `heatRanks`, `pastEpics`,
  `keepsakes`, `selectedKeepsake`, `weaponArt`, `offhandArt`; an old `meta.json` loads defaults. Fixture
  `TestData/saves_phase4`.
- **A12.14 `RunRng.ForNode` seed mix (BUG-35).** The node seed now goes through a murmur3 fmix32 avalanche, because
  low seeds (1..64) differed only in low bits and every Gamble rolled the same. It moves the golden vectors, which is
  why it ships in the same v3 bump (a v2 run would resume with different node contents).

## Amendment 13 — Phase 5 graphics decisions (Roguelike-Roadmap.md P5-00 … P5-08)

User answers on 2026-10-23 for the design calls the roadmap left open:

- **A13.1 Biome kit source (P5-05).** Kit meshes (floors, walls, pillars, roofs, hero props, fixtures) are made with
  **Unity AI GenerateMesh**, then decimated or cleaned to the per-piece triangle targets in the P5-05 kit table.
  Procedural editor-built kits and Tripo image→3D were offered and not chosen.
- **A13.2 Shell count (P5-05).** The roadmap aim stands: **3–4 room shells per biome** (Rust, Program, Garden), each
  authored at the 8 m and 11 m `ArenaHalfExtent` footprints. Program is built end to end first.
- **A13.3 UI font (P5-07).** **Rajdhani** (OFL), SDF 1024, padding 9, with LiberationSans as the fallback. Orbitron
  was offered and not chosen.
- **A13.4 Perf probe (P5-00).** The in-editor budget numbers come from `PerfBudgetProbe` (hands posed in view,
  Mobile_RPAsset = the Quest 3 path). Every later P5 task re-measures and must stay inside the Quest 3 column.
- **A13.5 Loadout blades (P5-04).** Distinct meshes at real proportions, built as `ArtPrefabBuilder` variants of the
  Katana (blade ~0.70 m in the user's reference; **0.84 m in game**: 0.62 m prefab x the arena's 1.35 upscale, kept as built so its reach is unchanged), the others scaled by the same 0.837/0.70 ratio: **Nodachi 1.14 m, Twin Wakizashi 0.54 m each, Tanto 0.34 m**. Hit reach follows the blade
  length (the swept damage tracks the blade), so the Nodachi reaches further and the Tanto/Wakizashi shorter; the
  P4-08 damage/speed stats are unchanged.
- **A13.6 Biome kit source, revised (P5-05, 2026-10-23).** Supersedes A13.1. On this account every Unity AI 3D model
  (Tripo P1/P2) requires an image reference, and an image reference cannot be passed through MCP (the 64-bit EntityId,
  e.g. 568105584918882030, exceeds JSON's 2^53 safe range and arrives rounded). User choice, **hybrid**: structural
  pieces (floors, walls, pillars, roof, fixtures, glyph panels) are built in editor code to the kit-table triangle
  targets and skinned with **Unity AI materials** (GenerateMaterial + AddPbrToMaterial); **hero props** come from Unity
  AI image→3D, run by the user in the generator UI from reference images generated with GenerateImage, then
  `RetopologyMesh` (takes an asset path, unaffected by the EntityId bug) and the kit builder.
- **A13.7 Curved boon offer kept, slimmed (user, P5-07c/d, 2026-10-24).** The ±12° card curve stays; the offer
  must cost <= 10 UI draws. Findings: a rotated (or vertex-bent) card is not coplanar with the panel canvas, so the
  canvas batcher gives each of its graphics its own draw (7b: +22). Done: (1) a nested `Canvas` (+ both raycasters)
  per card, so each card batches internally; card clicks still hit through the nested raycasters (scripted
  RaycastAll + click in play mode, PlayMode 16/16); (2) `UiKit.Frame` is ONE baked 9-slice sprite (dark glass fill
  + 3 px white edge, tinted by the accent colour; `BoonIconBuilder.BakeUiFrame`, `Processed/Resources/UiKitFrame.png`)
  instead of glass + edge Images, and it lives in the BoonIcons atlas folder so frame and icon share a texture in
  builds; (3) no Bold on card labels (Rajdhani-Bold is a 2nd TMP material); (4) the card canvases are the last
  siblings so the panel's own frames and labels batch in one run. Result (atlas packed as in a build, PerfBudgetProbe
  Rust node 1): Mobile 49 draws / 28 SetPass without a panel -> 58 / 37 with the offer (**+9 / +9**), PC 72 / 38 ->
  83 / 49; magenta 0.0000. In-editor play mode with the default build-only packer the icons are loose textures, so
  each card costs +1 there (estimated ~+12, not measured). The frame fill is now accent-tinted near-black instead of fixed blue-black.
- **A13.8 Ganger waist ring removed (user, 2026-10-24).** Amends A12.2. The Coil Syndicate Ganger's magenta waist
  ring is gone; the Ganger is unmarked like the Scavenger. Reason: after the Mobile grade its magenta (~206,9,241)
  read as an error shader in renders and tripped the perf probe's magenta check. It must be identifiable without a
  mark (charger silhouette, telegraph, behaviour); `EnemyPrefabBaker.MarkSpecs` no longer lists it, and
  `SpawnTableInvariantTests` asserts it has no `ArchetypeMark`.
- **A13.9 Archetype marks removed (user, 2026-10-24 v0.6.0 playtest).** Amends A12.2 / A13.8. The BUG-40 marks
  (grunt visor bar, hunter barrel, scan-drone halo, automaton core, warden banner, guardian pool, construct yoke,
  ghost orb) read as glitches in the headset. Archetypes are identified by silhouette, telegraph and behaviour;
  those `EnemyPrefabBaker.MarkSpecs` rows have no mesh and `SpawnTableInvariantTests` asserts only the Shield Trooper
  keeps an `ArchetypeMark`. Exception: the trooper's cyan shield slab stays, because it is the HeavyBreak guard tell
  (flickers on resisted hits, hidden on break). The BUG-39 weak-point marks (`WeakMarkSpecs`) stay.

## Amendment 14: Phase 6 accessibility and comfort

- A14.1. **Slow-mo option is a floor.** `SlowMoFloor` 0.25-0.5 (default 0.25) lifts each effect's own scale: `max(effectScale, floor)` via `ComfortOptions.SlowMoScale`, applied to deflect (0.25), Overdrive (0.35) and Zanshin (0.4) only; Pause and WeaknessTeach are untouched. The default floor leaves all three exactly as before; 0.5 is the gentlest.
- A14.2. **Reduce Flashes** (`GameSettings.ReduceFlashes`, default off) gates: the hit vignette flash (`CombatFeedbackController`) and the asteroid hit punch; the enemy hit flash and weakness flash (`RendererTint.Flash` returns 0); the white `hitFlash` particle bursts; the Overdrive PC bloom boost. Enemy rims go through `ComfortOptions.Rim` in `RendererTint.Apply(r, c, rim)`: with the option on no rim channel exceeds 1.2 (the BUG-46 elite level), so telegraph/stagger/sight rims stay readable but never bloom, and elite rims (already 1.2, steady) are unchanged. The death fade-to-dark vignette is a fade, not a flash, and is kept. The weak-point mark scale pulse (3 Hz, scale only, no emission) is left for P6-03.
- A14.3. **Haptic intensity** scales every `Haptics.Pulse` (and the Ship-side mirror in `TrickBoostController`); 0 sends nothing. Devices are cached per hand and re-fetched when invalid. **Vignette strength** scales `ComfortVignette.SetIntensity` (locomotion/ship comfort); the damage/death flash path is separate.
- A14.4. Settings panel: the four rows are appended by `SettingsPanelBuilder.AddComfortOptionRows` (idempotent, same grow-and-shift scheme as `AddComfortRows`), patched into Phase6_Boot and RunArena via *Roguelike/Patch Comfort Option Rows (P6-02)*. P6-03 appends rows the same way; the panel is now about 1.47 m tall, so P6-03 should consider a second column.
- A14.5. **Windup pulse vs Reduce Flashes.** `MeleeAttacker` windups pulse the rim by a smooth sine factor, 2 Hz rising linearly to 6 Hz at the strike (`AccessibilityOptions.PulseHz`/`AdvancePhase`; phase is accumulated so the rate change never jumps), on top of the existing edge line and windup pose. It goes through `RendererTint` (MPB), no new materials, no allocations. With Reduce Flashes on, the rim is capped at 1.2 first (`ComfortOptions.Rim`) and the pulse is applied after the cap as a constant 2 Hz (under the 3-per-second flash threshold), shallow (0.65-1.0 of the capped rim, vs 0.35-1.0) sine, so a readable modulation survives and nothing blooms. Progress is still carried by the rim ramp and the pose.
- A14.6. **Weak-point mark beat** (scale only, no emission, 3 beats/s) is slowed to 1.5 beats/s when Reduce Flashes is on (`AccessibilityOptions.WeakPointBeatHz`); it is unchanged otherwise.
- A14.7. **Colour audit.** The only red/green good/bad pair in the run UI was the run summary headline (green RUN COMPLETE / red RUN LOST); it is now blue vs amber (`RunSummary.OutcomeAccent`), with the wording kept. Boon cards already print the tier word (`BoonOfferPanel.TierWord`). Story-era `HeatMeter` and `ExtractionZone` use green/red but are in neither roguelike scene, so left alone.
- A14.8. **UI scale** (`GameSettings.UiScale`, 1-1.5): `UiScaleFollower` multiplies a panel's authored localScale, event-driven by `SettingsChanged`. On the settings canvas, wrist HUD, main menu, boon/path/shop/event panel, meta shop, pause, game over, run summary and the subtitle band. Panels are scaled around their pivot at their existing distance; the title card is transient and unscaled.
- A14.9. **Assists.** `DamageTaken` (0.5-1) multiplies in `Health.ApplyDamage` only on a Health that has `PlayerCombatModifiers` (the player rig), after boon reduction and Last Stand. `EnemyCooldownScale` (1-1.5) multiplies `EnemyDefinition.attackCooldown` in `Enemy.OnActiveEnd`; telegraph times and damage are untouched, so the fight stays readable. Unset (0/NaN) reads as default, never invulnerable. A run is Assisted if either is off-default at run start, any node entry or run end (`RunTally.SampleAssist`, sticky), logged as `assisted` in `runlog.jsonl` (append-only) and shown as ASSISTED RUN on the summary. Echoes bank normally (D-06).
- A14.10. **Subtitles** (`GameSettings.Subtitles`, default on): `DialoguePlayer` publishes `SubtitleLine` at line start; `SubtitleDisplay` (in Phase6_Boot and RunArena) shows it in a rigidly head-locked band 1.5 m ahead and 0.45 m below the eye, 28 px text at 0.0013 scale, for the line's duration. P6-04 only has to play lines through a `DialoguePlayer`.
- A14.11. **Settings panel is two columns** (1200 x 980 px). `SettingsPanelBuilder.AddAccessibilityColumn` moves the seven comfort rows that P2-09/P6-02 appended at the bottom into column 2 and adds UI Scale / Damage Taken / Enemy Cooldown / Subtitles under them; patched into both scenes by *Roguelike/Patch Accessibility (P6-03)*.
- A14.12. **Replay Tutorial is one-shot.** `GameSettings.ReplayTutorial` (persisted, default off) forces the P2-08 prompts on the next run's first combat room regardless of `runsCompleted`/`tutorialDone` (`MetaProgression.ShouldShowTutorial`). When `RunArenaController` shows them it publishes `TutorialReplayShown`, and `SettingsService` clears the toggle (so it cannot nag every run; re-enable to replay again). `runsCompleted` is never touched.
- A14.13. **Run voice lives in Flow, not DialoguePlayer.** `DialoguePlayer` is in `Ronin7.World`, which Flow cannot reference, so `Flow/Roguelike/RunVoice` (added to the RunDirector object at runtime, no scene patch) plays the `KesslerRunLines` bank itself and publishes the same `SubtitleLine`, so `SubtitleDisplay` shows it unchanged. Clips load from `Resources/RunVoice/<clip>`; a missing clip still shows the subtitle (silent). Manifest: *Tools/Space Samurai/Audio/Export Run Voice Manifest* -> `Voice/runvoice_voice_manifest.json` (31 lines); generate with `generate_voice.py --manifest ... --out Assets/Ronin7/Resources/RunVoice`. Voices: Kessler `en-US-GuyNeural` (existing), Warden `en-US-RogerNeural`, Vane `en-US-AndrewNeural`, Maelgorn `en-GB-ThomasNeural` (existing).
- A14.14. **Kessler outcome keys** (`KesslerRunLines.Outcome`): won with Heat > 0 = WonHeat; won = Won; the player's first-ever run (RunsCompleted == 0 before the run is counted) = FirstRun; else the sector of the death room (`depth / 5`: Rust, Program, Garden). 4 variants each; chosen randomly, never the same variant as the last time that outcome played (session memory). Spoken 1.2 s after the summary panel appears. `RunSummary` gained `Heat` and `FirstRun` (session-only, not saved).
- A14.15. **Boss lines**: intro on `RunNodeEntered` with `RoomKind.Boss`; phase lines on `BossPhaseChanged` (Phase 1 = second phase). Warden: intro + 1, Vane / Wraith-6: intro + 1, Maelgorn: intro + 2.
- A14.16. **Display-name renames (content only; IDs, asset names and save keys unchanged)**: sectors `THE RUST` -> `THE RUSTFANG`, `THE GARDEN` -> `THE SILENT GARDEN` (`THE PROGRAM` kept). Events: Blood Pact -> Synod Contract, Gamble -> Velorum Wager, Meditate -> Cairn Respite, Forgotten Shrine -> Kethel-7 Shrine. Boons: Echo Twin -> Shadow Twin, Iron Wall -> Iron Dojo Wall, Blood Price -> Coil Tithe, Headhunter -> Drovis Bounty, Whetstone -> Salvager's Whetstone. Edited in the `.asset` files and in `BoonCatalogBuilder` / `RunEventBuilder` so a rebuild keeps them.
- A14.17. **Audio routing (P6-01).** `Audio/Ronin7.mixer`: Master with Music / SFX / Ambience / UI children, exposed dB params `MasterVol`, `MusicVol`, `SfxVol`, `AmbienceVol`, `UiVol`. `SettingsService.VolumeToDb(v) = 20*log10(max(v, 0.0001))` (0 gives -80, 1 gives 0, 0.5 gives -6.02). With a mixer assigned, `AudioDirector.SetXVolume` writes the param and the authored bed levels stay put; without one it falls back to the old per-source scaling. Music is the only slider that shares its param: the duck is added to the slider dB (`MusicVol = slider + duck`) instead of a fifth group. Duck: -6 dB held 0.3 s then a 0.25 s release, triggered by `WeaknessExploited`, `PostureBroken`, `BossPhaseChanged` (pure `MusicDuck`). Sources: ambience, both music sources, engine, low-health loop and the 16 pooled one-shots are routed by the director; `RunVoice`'s source (Flow cannot see the mixer) is routed onto SFX by the director on `RunStarted` and `RunNodeEntered`. UI has no sources yet (no UI clicks exist); the group is there for them.
- A14.18. **Run music selection and crossfade.** `AudioDirector.MusicFor(kind, sector[, aggro])` returns a `MusicSlot` (sector * 3 + 0 explore / 1 combat / 2 boss): a Boss node plays its sector's boss loop for the whole node, any other node plays explore, or combat while `CombatActivity.OnFootAggro`. Sectors wrap like `SectorAmbience`. Two music sources crossfade (not fade-out-then-in); when both loops have the same length (every explore/combat pair does by design) the incoming loop starts at the outgoing loop's playhead so the beat carries. A missing run clip falls back to the old `exploreMusic` / `combatMusic`. No `Kind` was needed on `RunNodeEntered` (it already carries it).
- A14.19. **Synthesized placeholder music, nothing licensed (user decision).** `ProceduralMusicSynth` (Audio) builds each loop bar by bar into a ring buffer with modulo-wrapped note tails, so loops are seamless by construction. 16 bars, mono, 22.05 kHz: Rust 100 BPM (38.4 s; boss 110 BPM, 34.9 s) industrial: detuned saw drone, driven saw bass, metallic strikes; Program 120 BPM (32 s; boss 128 BPM, 30 s) cold digital pulse: square arps, sine/square bass pulse, FM bell; Garden 80 BPM (48 s; boss 88 BPM, 43.6 s) organic: sine pad, plucks, flute, hand drums. Boss tracks sit 5 semitones lower with heavier drums. Baked by *Audio/P6-01 Generate Run Music And Cues* into `Audio/Generated/Music` and `Cues`; the four old noise placeholders (`boonChosen`, `runLost`, `runWon`, plain melee windup) were overwritten in place so their GUIDs and scene references survive. Wired additively into Phase6_Boot by *Audio/P6-01 Wire Run Audio*, and by `XRRigBuilder` on a rebuild.
- A14.20. **New cue slots.** `BoonOffered` (new `Core` event, published when the offer panel shows), plus `runStarted`, `roomCleared`, `boonOffered`, `bossPhase`, and `enemySpawning` (now its own rising portal swell, no longer the windup fallback). Low-health loop (`Loop_LowHealth`, exactly 1 s lub-dub): fades in below 25% HP during a run, silent at 0 HP; reads the same player `Health` as `HeartbeatHaptics` (found lazily, at most once a second). Menu ambience (`Loop_MenuAmbience`, 24 s sus pad) is the default ambience bed until a run node sets a sector bed, and returns on `RunEnded`; this closes the old `XRRigBuilder` TODO. A `PlayAt` with no clip logs one warning per cue (`[CallerMemberName]`, no allocation).
- A14.21. **Per-archetype windup voices (colour-free telegraph).** Chosen by enemy class: plain melee (`attackWindup`, two-tone square tick), `ChargerEnemy` (95 to 240 Hz growling saw with 18 Hz tremolo), `BlinkEnemy` (two falling glassy pings), `SupportDrone` (double 1250 Hz beep), `RangedEnemy` (500 to 1500 Hz rising whine). Pitch, length and timbre all differ (asserted by a test). Bosses use the melee voice until they get their own.
- A14.22. **Quest import settings** (*Audio/P6-01 Apply Quest Import Settings*, idempotent, runs after generation): everything under `Audio/Generated` and `Resources/RunVoice` gets Vorbis q0.5 (default and Android override), force-mono, Decompress On Load when the source wav is under 200 KB and not music or ambience, Compressed In Memory otherwise, and Preload off for every loop. The 3,357 legacy chapter VO mp3s are left alone: the build contains only Phase6_Boot, RunArena and the 18 room scenes.
- A14.23. **Slots to license or commission for 1.0** (placeholders stay until then; nothing was bought or downloaded). All loops 45 to 90 s, seamless, stems preferred (drums / bass / harmony) so explore and combat can share a tempo.
  1. Rust explore: slow industrial dread, 85 to 95 BPM, D minor, low drones, distant machinery percussion, sparse metal resonance.
  2. Rust combat: driving industrial, 120 BPM, heavy 4-on-the-floor kick, distorted bass, metallic hits on the off-beat.
  3. Rust boss (Warden): 130 BPM, brutal and slow-grinding, taiko plus detuned brass and anvil hits, a clear 2-phase build.
  4. Program explore: cold ambient electronica, 100 to 110 BPM, A minor, clean sine pulses, glassy bells, sidechained pads.
  5. Program combat: 128 BPM, tight gated arpeggios, sharp digital percussion, relentless 16th pulse.
  6. Program boss (Vane / Wraith-6): 140 BPM, glitchy and aggressive, bit-crushed bass, ominous choir pad, tension riser into each phase.
  7. Garden explore: airy and organic, 70 to 80 BPM, D major pentatonic, soft plucks, bamboo flute, field-recorded wind and water.
  8. Garden combat: 100 BPM, hand drums and shaker, urgent plucked strings, flute ostinato, still warm.
  9. Garden boss (Maelgorn): 90 BPM, vast and mournful, taiko and low strings with a dissonant choir, a 3-phase arc to match his two phase lines.
  Also worth commissioning: a menu theme, and a victory and a defeat sting (currently synthesized cues).
- A14.17. **Audio routing (P6-01).** `Audio/Ronin7.mixer`: Master with Music / SFX / Ambience / UI children, exposed dB params `MasterVol`, `MusicVol`, `SfxVol`, `AmbienceVol`, `UiVol`. `SettingsService.VolumeToDb(v) = 20*log10(max(v, 0.0001))` (0 gives -80, 1 gives 0, 0.5 gives -6.02). With a mixer assigned, `AudioDirector.SetXVolume` writes the param and the authored bed levels stay put; without one it falls back to the old per-source scaling. Music is the only slider that shares its param: the duck is added to the slider dB (`MusicVol = slider + duck`) instead of a fifth group. Duck: -6 dB held 0.3 s then a 0.25 s release, triggered by `WeaknessExploited`, `PostureBroken`, `BossPhaseChanged` (pure `MusicDuck`). Sources: ambience, both music sources, engine, low-health loop and the 16 pooled one-shots are routed by the director; `RunVoice`'s source (Flow cannot see the mixer) is routed onto SFX by the director on `RunStarted` and `RunNodeEntered`. UI has no sources yet (no UI clicks exist); the group is there for them.
- A14.18. **Run music selection and crossfade.** `AudioDirector.MusicFor(kind, sector[, aggro])` returns a `MusicSlot` (sector * 3 + 0 explore / 1 combat / 2 boss): a Boss node plays its sector's boss loop for the whole node, any other node plays explore, or combat while `CombatActivity.OnFootAggro`. Sectors wrap like `SectorAmbience`. Two music sources crossfade (not fade-out-then-in); when both loops have the same length (every explore/combat pair does by design) the incoming loop starts at the outgoing loop's playhead so the beat carries. A missing run clip falls back to the old `exploreMusic` / `combatMusic`. No `Kind` was needed on `RunNodeEntered` (it already carries it).
- A14.19. **Synthesized placeholder music, nothing licensed (user decision).** `ProceduralMusicSynth` (Audio) builds each loop bar by bar into a ring buffer with modulo-wrapped note tails, so loops are seamless by construction. 16 bars, mono, 22.05 kHz: Rust 100 BPM (38.4 s; boss 110 BPM, 34.9 s) industrial: detuned saw drone, driven saw bass, metallic strikes; Program 120 BPM (32 s; boss 128 BPM, 30 s) cold digital pulse: square arps, sine/square bass pulse, FM bell; Garden 80 BPM (48 s; boss 88 BPM, 43.6 s) organic: sine pad, plucks, flute, hand drums. Boss tracks sit 5 semitones lower with heavier drums. Baked by *Audio/P6-01 Generate Run Music And Cues* into `Audio/Generated/Music` and `Cues`; the four old noise placeholders (`boonChosen`, `runLost`, `runWon`, plain melee windup) were overwritten in place so their GUIDs and scene references survive. Wired additively into Phase6_Boot by *Audio/P6-01 Wire Run Audio*, and by `XRRigBuilder` on a rebuild.
- A14.20. **New cue slots.** `BoonOffered` (new `Core` event, published when the offer panel shows), plus `runStarted`, `roomCleared`, `boonOffered`, `bossPhase`, and `enemySpawning` (now its own rising portal swell, no longer the windup fallback). Low-health loop (`Loop_LowHealth`, exactly 1 s lub-dub): fades in below 25% HP during a run, silent at 0 HP; reads the same player `Health` as `HeartbeatHaptics` (found lazily, at most once a second). Menu ambience (`Loop_MenuAmbience`, 24 s sus pad) is the default ambience bed until a run node sets a sector bed, and returns on `RunEnded`; this closes the old `XRRigBuilder` TODO. A `PlayAt` with no clip logs one warning per cue (`[CallerMemberName]`, no allocation).
- A14.21. **Per-archetype windup voices (colour-free telegraph).** Chosen by enemy class: plain melee (`attackWindup`, two-tone square tick), `ChargerEnemy` (95 to 240 Hz growling saw with 18 Hz tremolo), `BlinkEnemy` (two falling glassy pings), `SupportDrone` (double 1250 Hz beep), `RangedEnemy` (500 to 1500 Hz rising whine). Pitch, length and timbre all differ (asserted by a test). Bosses use the melee voice until they get their own.
- A14.22. **Quest import settings** (*Audio/P6-01 Apply Quest Import Settings*, idempotent, runs after generation): everything under `Audio/Generated` and `Resources/RunVoice` gets Vorbis q0.5 (default and Android override), force-mono, Decompress On Load when the source wav is under 200 KB and not music or ambience, Compressed In Memory otherwise, and Preload off for every loop. The 3,357 legacy chapter VO mp3s are left alone: the build contains only Phase6_Boot, RunArena and the 18 room scenes.
- A14.23. **Slots to license or commission for 1.0** (placeholders stay until then; nothing was bought or downloaded). All loops 45 to 90 s, seamless, stems preferred (drums / bass / harmony) so explore and combat can share a tempo.
  1. Rust explore: slow industrial dread, 85 to 95 BPM, D minor, low drones, distant machinery percussion, sparse metal resonance.
  2. Rust combat: driving industrial, 120 BPM, heavy 4-on-the-floor kick, distorted bass, metallic hits on the off-beat.
  3. Rust boss (Warden): 130 BPM, brutal and slow-grinding, taiko plus detuned brass and anvil hits, a clear 2-phase build.
  4. Program explore: cold ambient electronica, 100 to 110 BPM, A minor, clean sine pulses, glassy bells, sidechained pads.
  5. Program combat: 128 BPM, tight gated arpeggios, sharp digital percussion, relentless 16th pulse.
  6. Program boss (Vane / Wraith-6): 140 BPM, glitchy and aggressive, bit-crushed bass, ominous choir pad, tension riser into each phase.
  7. Garden explore: airy and organic, 70 to 80 BPM, D major pentatonic, soft plucks, bamboo flute, field-recorded wind and water.
  8. Garden combat: 100 BPM, hand drums and shaker, urgent plucked strings, flute ostinato, still warm.
  9. Garden boss (Maelgorn): 90 BPM, vast and mournful, taiko and low strings with a dissonant choir, a 3-phase arc to match his two phase lines.
  Also worth commissioning: a menu theme, and a victory and a defeat sting (currently synthesized cues).

