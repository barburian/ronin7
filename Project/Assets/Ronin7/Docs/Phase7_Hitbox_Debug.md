# Phase 7 Space Combat — Hitbox Debug Log

Date: 2026-05-23
Scene: `Assets/Ronin7/Scenes/Phase7_SpaceCombat.unity`
Status: Phase 7 shipped + headset-verified 2026-05-22. Landing-block fix (enemies-present
gate) shipped + headset-verified 2026-05-23. Hitbox "feel" still flagged by the user
(no specific symptom yet characterized — see Forward-looking notes).

---

## TL;DR (this iteration)

`Projectile.cs` had one provably-wrong code path: `Physics.SphereCast` does **not** report
colliders that the sphere already overlaps at the cast origin. A bolt launched while
already intersecting a target (point-blank fire, or after a sudden swerve into a target)
would sweep right through it. Two changes ship in this iteration:

1. Extracted the firer-exclusion / target-alive decision into a pure
   `public static bool ShouldDamage(GameObject owner, Transform hitRoot, bool targetAlive)`
   so it can be locked by EditMode tests (`Projectile.cs:128`).
2. Added a `Physics.OverlapSphereNonAlloc` pre-check at the start of `Update()`,
   **before** the existing `SphereCast` sweep, so initial overlaps get caught
   (`Projectile.cs:98`). Uses a static `Collider[8]` buffer (`Projectile.cs:41`) to stay
   alloc-free per frame.

That is the entire production-code change. No collider sizes, no aim assist, no scene
edits — deliberately, because the user has not reported a specific symptom yet and feel
tuning is in-headset work.

---

## What changed in `Projectile.cs`

### 1. `ShouldDamage` (pure decision)

`Projectile.cs:128`

```csharp
public static bool ShouldDamage(GameObject owner, Transform hitRoot, bool targetAlive)
{
    if (hitRoot == null) return false;
    if (owner != null && hitRoot.IsChildOf(owner.transform)) return false;
    return targetAlive;
}
```

Rules:

- Null hit → false (nothing to damage).
- Firer hitting its own hierarchy → false (a bolt can't damage its own shooter, including
  child colliders on the firer's hull).
- Otherwise: the target's `IsAlive` decides. Dead targets are skipped.

Called from `TryHit` (`Projectile.cs:145`). Locked by `ProjectileLogicTests.cs` (6 cases
covering owner-self / owner-child / different-owner / dead / null-hit / null-owner).

### 2. Initial-overlap pre-sweep

`Projectile.cs:98`

```csharp
int overlapCount = Physics.OverlapSphereNonAlloc(transform.position, worldRadius,
    _overlapBuf, ~0, QueryTriggerInteraction.Ignore);
for (int i = 0; i < overlapCount; i++)
{
    if (TryHit(_overlapBuf[i], transform.position, transform.forward)) return;
}
```

Runs every `Update()` immediately before the existing `Physics.SphereCast` (`:108`).
Rationale: Unity's `SphereCast` is documented to ignore colliders whose volume already
overlaps the sphere at the cast origin, which silently dropped point-blank hits. The
OverlapSphere recovers that case. `_overlapBuf` is `static readonly Collider[8]`
(`Projectile.cs:41`) — shared across all bolts, zero per-frame allocation.

Worst case is 8 colliders in the buffer per frame; the loop bails the moment `TryHit`
recycles the bolt.

---

## Harness

### EditMode — `Tests/EditMode/ProjectileLogicTests.cs`

Six `[Test]` cases on `Projectile.ShouldDamage`:

- null hitRoot → false
- owner null, live target → true
- owner hitting itself → false
- owner hitting a child of itself → false
- owner hitting an unrelated live target → true
- owner hitting an unrelated dead target → false

Pure logic, no Unity loop. Runs in milliseconds.

### PlayMode — `Tests/PlayMode/ProjectileHitTests.cs`

Two `[UnityTest]` cases:

- `Projectile_DirectFlight_DamagesTarget` — sanity: a bolt fired from a distance flies
  into a `Health`-bearing target and reduces its HP. Confirms the basic sweep path still
  works post-change.
- `Projectile_InitialOverlap_DamagesTarget` — the harness that proves the fix. A bolt is
  spawned at the exact position of the target (initial overlap). Pre-fix, this test
  fails (SphereCast ignores the overlap, the bolt sails through, HP unchanged).
  Post-fix, the leading OverlapSphere catches it on the next `Update()` tick and HP
  drops.

---

## Two issues observed but deferred (in code, not yet fixed)

### A) `OnTriggerEnter` hit point is approximate, and the path is redundant

`Projectile.cs:118` calls `TryHit(other, transform.position, transform.forward)` — i.e.
it reports the bolt's *current centre* as the contact point, not the actual contact on
the other collider. With the OverlapSphere pre-sweep in `Update()`, the trigger callback
now mostly fires *after* the sweep already resolved the hit; `TryHit` is a no-op the
second time (the bolt has already recycled and `live = false`), so it's harmless but
redundant.

Future fix: either use `Physics.ClosestPoint(other, transform.position)` for a more
honest contact point, or remove the `OnTriggerEnter` path entirely once the
sweep-and-overlap detection is trusted in-headset. Cheap, but defer until the trigger
path actually causes a visible artefact.

### B) `ApplyTint` allocates one material per pooled bolt on first reuse

`Projectile.cs:169`:

```csharp
private void ApplyTint(Color color)
{
    var r = GetComponentInChildren<Renderer>();
    if (r == null) return;
    r.material.color = color;          // <-- this clones the shared material the first time
}
```

The inline comment claims this is amortised because bolts are pooled — that's correct
*per pooled instance*, but each pooled instance still pays one material clone on its
first `Launch`. With ~16 bolts in a pool that's 16 one-time material allocations spread
across the first wave of fire. Not a frame-rate problem on Quest, but it is also not
zero, despite the comment.

Future fix: use a `MaterialPropertyBlock` (no clone, GPU-side override) or pre-build a
single tinted material per bolt-colour and assign `sharedMaterial`. Defer until profiling
flags it.

---

## Forward-looking tuning notes (in-headset session, NOT this plan)

These all need the headset on; static review cannot decide them.

- **Enemy hull colliders.** Open `Phase7_SpaceCombat.unity`, select an `EnemyShip`, look at
  the SphereCollider (or whichever primitive is on the hull) in Scene view at 1:1 zoom and
  check it matches the visual silhouette. Watch for a gap near the canopy / tail; that's a
  classic source of "I'm clearly hitting them but no damage" reports.
- **Player bolt radius.** Bolts default to `radius = 0.2` (passed into `Projectile.Launch`).
  Bumping the SphereCast radius slightly (e.g. 0.3–0.4) reads as "tighter aim" without
  feeling auto-aimed. Do NOT change this until the user actually reports the feel is off —
  a wider sphere also means bolts grazing past geometry will start to hit it.
- **Layer / Physics Matrix.** In `Phase7_SpaceCombat`, confirm that player-bolt-layer ↔
  enemy-hull-layer interact, and enemy-bolt-layer ↔ player-hull-layer interact, in Edit ▸
  Project Settings ▸ Physics ▸ Layer Collision Matrix. The current `~0` mask in
  `OverlapSphereNonAlloc` / `SphereCast` (`Projectile.cs:98`,`:109`) means physics-layer
  filtering is delegated to the matrix — if a layer pairing is off, hits silently drop.
- **Enemy bolts vs player.** The enemy-fire path lives under the `universe` transform
  (moving-world frame). Sanity-check that enemy bolts actually carry a collider that
  matches the player's hull collider's layer, since the player is a stationary rig at the
  origin.

If the user reports a specific symptom ("they're flying through me at close range",
"I'm shooting and nothing reacts", etc.) extend the PlayMode harness with a case that
reproduces it before touching production code — the harness already proves point-blank
hits work, so anything that still feels wrong narrows down to collider geometry, layers,
or feel.

---

## How to use the harness

1. Open Unity → `Window ▸ General ▸ Test Runner`.
2. PlayMode tab → Run All.
3. `Projectile_InitialOverlap_DamagesTarget` is the gate: it would fail on the pre-Agent-4
   `Projectile.cs` (no `OverlapSphereNonAlloc` block, only `SphereCast`). It passes today.
4. If it ever goes red again, the regression is almost certainly in `Projectile.Update()`
   around `:98–:113`.

EditMode tests (`ProjectileLogicTests.cs`) gate the `ShouldDamage` decision against
accidental rewrites; they take milliseconds and should stay green forever unless the
exclusion rules change deliberately.
