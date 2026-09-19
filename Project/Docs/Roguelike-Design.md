# Ronin7 — Roguelike Refactor Design & API Contract

Status: **authoritative build contract** for the story→roguelike refactor.
Every implementing agent codes against the signatures in this doc. If a signature here is wrong,
raise it — do not silently diverge, because parallel modules compile against it.

## 1. Pitch & loop

Ronin-7 is a blade-shadow descending into **the Fracture**, a shifting lattice of Program sectors.

```
Hub (Galaxy1_Ch1_Hub)  --StartRun-->  Node 0 ... Node 14  --Boss killed--> Run won  --> Hub
        ^                                   |
        |                                   +-- player dies (permadeath) --> Run lost --> Hub
        +-- spend Echoes on permanent unlocks between runs
```

* A **run** = 3 sectors x 5 nodes = **15 nodes**, ~25 min (VR session length is a design constraint).
* **Permadeath**: death ends the run. Boons are lost; **Echoes** (meta-currency) are banked.
* Each node loads the single reusable `RunArena` scene; the arena is **assembled at runtime from a
  seed** while the screen is still under GameFlowManager's black fade.

### Node layout per sector (index within sector)

| idx | kind |
|---|---|
| 0 | `Combat` (always — a guaranteed soft opener) |
| 1 | weighted `Combat` / `Elite` / `Treasure` |
| 2 | weighted `Combat` / `Elite` / `Treasure` |
| 3 | `Forge` (heal + reroll, no enemies) |
| 4 | `Boss` |

Weights for idx 1–2: Combat 55, Elite 30, Treasure 15. A sector never rolls two Treasures.

### Rewards

| cleared node | reward |
|---|---|
| `Combat` | boon offer, 3 choices |
| `Elite` | boon offer, 3 choices, rarity weights shifted up |
| `Treasure` | 1 guaranteed Rare, no fight |
| `Forge` | heal 60% of missing HP + 1 reroll token |
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
        public void StartRun();             // random seed
        public void StartRun(uint seed);    // daily/known seed
        public void AbandonRun();

        // Pure, unit-testable decisions:
        internal enum PostClearAction { OfferBoon, AdvanceImmediately, WinRun }
        internal static PostClearAction ResolvePostClear(RoomKind kind, bool isFinalNode);
    }

    // Hub-side. Sits next to MissionLauncher on the hub console for the roguelike mode.
    public class RunLauncher : MonoBehaviour { public void LaunchRun(); }
}
```

`RunDirector` reuses `GameFlowManager`'s fade/load path — it must not duplicate scene loading.
Add to `GameFlowManager` a single `public IEnumerator LoadSceneFaded(string scene, GameMode mode)`
wrapper exposing the existing `FadeLoadFade`, and nothing else. Player death during a run is already
funnelled through `GameFlowManager.OnEntityDied`; `RunDirector` subscribes to `EntityDied` too and
ends the run, banking echoes, before the existing game-over path returns to the menu — for a run, it
returns to the **hub** instead of the main menu.

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

### Menu / hub integration points

* `Flow/MainMenuController.cs` — add `OnStartRunClicked()` calling `RunDirector.Instance.StartRun()`,
  matching the existing thin-bridge idiom (each method is one line delegating to a singleton).
* `Flow/MissionLauncher.cs` — unchanged. `RunLauncher` sits beside it on the hub console so the
  story campaign remains launchable; the roguelike is the new default path.

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
