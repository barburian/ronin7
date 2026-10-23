using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>Player's blade intercepted an incoming enemy attack within the parry window.</summary>
    /// <summary>P5-03: the swinging blade's swept cast touched static world geometry (impact decals).</summary>
    public readonly struct SwordWorldHit
    {
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public SwordWorldHit(Vector3 point, Vector3 normal) { Point = point; Normal = normal; }
    }

    public readonly struct SwordDeflected
    {
        public readonly Vector3 Point;
        public readonly GameObject Attacker;
        public SwordDeflected(Vector3 point, GameObject attacker) { Point = point; Attacker = attacker; }
    }

    /// <summary>
    /// Sunder Beat: the player's deflect landed inside the perfect-timing window (see
    /// <see cref="ParryTiming.ParryQuality"/>). <see cref="Quality"/> is in (0, 1] — 1 at a frame-zero
    /// deflect, tapering toward 0 at the edge of the window. Only published when quality is greater
    /// than zero; a deflect outside the window still publishes <see cref="SwordDeflected"/> but not this.
    /// </summary>
    public readonly struct PerfectParry
    {
        public readonly Vector3 Point;
        public readonly GameObject Attacker;
        public readonly float Quality;
        public PerfectParry(Vector3 point, GameObject attacker, float quality) { Point = point; Attacker = attacker; Quality = quality; }
    }

    /// <summary>
    /// An enemy has begun its attack windup — the telegraph phase before the strike goes live (see
    /// <c>Ronin7.Enemies.MeleeAttacker.BeginAttack</c>). A spatialized, directional audio cue here
    /// matters especially now that art-enemy prefabs carry no visible blade (their weapon is sculpted
    /// into the skinned body mesh — see <c>EnemySwordVisual.HasCharacterArt</c>), so a "readable behind
    /// you" parry telegraph can no longer rely on sight alone.
    /// </summary>
    public readonly struct AttackWindupStarted
    {
        public readonly Vector3 Point;
        public readonly GameObject Attacker;
        public AttackWindupStarted(Vector3 point, GameObject attacker) { Point = point; Attacker = attacker; }
    }

    /// <summary>An enemy attack landed on the player.</summary>
    public readonly struct PlayerHit
    {
        public readonly float Amount;
        public readonly Vector3 Point;
        /// <summary>Where the attack came from (attacker position) — drives directional feedback.
        /// Equals <see cref="Point"/> when unknown, which reads as non-directional.</summary>
        public readonly Vector3 Source;
        public PlayerHit(float amount, Vector3 point) : this(amount, point, point) { }
        public PlayerHit(float amount, Vector3 point, Vector3 source) { Amount = amount; Point = point; Source = source; }
    }

    /// <summary>The player's blade struck something damageable (for sparks / SFX / haptics).</summary>
    public readonly struct SwordImpact
    {
        public readonly Vector3 Point;
        public readonly float Speed;
        public readonly GameObject Victim;
        /// <summary>P4-05 (appended): the blade's world velocity at the hit; zero when unknown. Reversal reads it.</summary>
        public readonly Vector3 Velocity;
        public SwordImpact(Vector3 point, float speed, GameObject victim) : this(point, speed, victim, Vector3.zero) { }
        public SwordImpact(Vector3 point, float speed, GameObject victim, Vector3 velocity) { Point = point; Speed = speed; Victim = victim; Velocity = velocity; }
    }

    /// <summary>An entity's posture meter (see <c>Ronin7.Enemies.PostureMeter</c>) broke: bonus damage
    /// was applied and a forced stagger was triggered.</summary>
    public readonly struct PostureBroken
    {
        public readonly GameObject Entity;
        public PostureBroken(GameObject entity) => Entity = entity;
    }

    /// <summary>
    /// An entity's posture meter (see <c>Ronin7.Enemies.PostureMeter</c>) crossed its configurable
    /// near-break fraction (default 0.8) of postureMax — "one more hit and this enemy staggers".
    /// Fires once per approach to the threshold; re-arms only once posture decays back below it.
    /// Never published on the same hit that also crosses through to <see cref="PostureBroken"/>.
    /// </summary>
    public readonly struct PostureNearBreak
    {
        public readonly GameObject Entity;
        public PostureNearBreak(GameObject entity) => Entity = entity;
    }

    /// <summary>
    /// Follow-Through combo chain advanced (see <c>Ronin7.Player.ComboMomentumController</c>).
    /// Published only when the chain's Count increases — never on a same-target reset to 0 — so
    /// subscribers (e.g. campaign stat tracking for "best combo reached") only see forward progress.
    /// </summary>
    public readonly struct ComboChained
    {
        public readonly int Count;
        public ComboChained(int count) => Count = count;
    }

    /// <summary>
    /// Adrenaline Flow kill streak advanced (see <c>Ronin7.Player.AdrenalineFlow</c>). Published only
    /// when the streak count increases — never on a window-expiry reset — so subscribers (e.g.
    /// campaign stat tracking for "best kill streak reached") only see forward progress. Mirrors
    /// <see cref="ComboChained"/>.
    /// </summary>
    public readonly struct KillStreakAdvanced
    {
        public readonly int Count;
        public KillStreakAdvanced(int count) => Count = count;
    }

    /// <summary>
    /// Sunder Beat perfect-parry streak advanced (see <see cref="Ronin7.Player.ParryFlowController"/>).
    /// Published only when the streak count increases — never on a player-hit/idle-expiry reset — so
    /// subscribers (e.g. campaign stat tracking for "best parry streak reached") only see forward
    /// progress. Mirrors <see cref="KillStreakAdvanced"/>.
    /// </summary>
    public readonly struct ParryStreakAdvanced
    {
        public readonly int Count;
        public ParryStreakAdvanced(int count) => Count = count;
    }

    /// <summary>
    /// G4 "Blade Clash": a deflect landed where BOTH the player's and the enemy's blade were
    /// genuinely moving hard (see <see cref="ParryTiming.IsClash"/>) — a true mutual clash, not just
    /// a well-timed parry against a slow swing. Published in addition to <see cref="SwordDeflected"/>
    /// (and <see cref="PerfectParry"/> when timing also qualifies), never instead of them.
    /// </summary>
    public readonly struct BladeClash
    {
        public readonly Vector3 Point;
        public readonly GameObject Attacker;
        public BladeClash(Vector3 point, GameObject attacker) { Point = point; Attacker = attacker; }
    }

    /// <summary>P4-09/P4-10: Unforgiving Guard let chip damage through a non-perfect deflect. Deliberately not a
    /// <see cref="PlayerHit"/> (that would reset the parry streak); CombatFeedbackController gives it a light sword-hand pulse.</summary>
    public readonly struct GuardChipped
    {
        public readonly float Damage;
        public GuardChipped(float damage) { Damage = damage; }
    }

    /// <summary>P3-02: a hit landed on an enemy's resisted side (see <c>Ronin7.Enemies.WeaknessRules</c>):
    /// reduced damage, no flinch, no posture.</summary>
    public readonly struct HitResisted
    {
        public readonly Vector3 Point;
        public readonly GameObject Victim;
        public HitResisted(Vector3 point, GameObject victim) { Point = point; Victim = victim; }
    }

    /// <summary>P3-02: a hit exploited an enemy's weakness. <see cref="Kind"/> is the
    /// <c>(int)Ronin7.Enemies.WeaknessKind</c>; an int because Combat cannot reference Enemies.</summary>
    public readonly struct WeaknessExploited
    {
        public readonly Vector3 Point;
        public readonly GameObject Victim;
        public readonly int Kind;
        public WeaknessExploited(Vector3 point, GameObject victim, int kind) { Point = point; Victim = victim; Kind = kind; }
    }

    /// <summary>
    /// BUG-39: one per feedback listener (haptics, audio, VFX). <see cref="HitResisted"/> and
    /// <see cref="WeaknessExploited"/> publish inside <c>Health.ApplyDamage</c>, before that same hit's
    /// <see cref="EntityDamaged"/> and <see cref="SwordImpact"/>, so a listener marks the victim on the
    /// weakness event and skips its generic impact/damage feedback for that victim on the same frame.
    /// No new events; other victims hit that frame still get the generic feedback.
    /// </summary>
    public struct HitConfirmLatch
    {
        private int frame;
        private GameObject victim;

        public void Mark(GameObject v) => Mark(v, Time.frameCount);
        public bool Suppresses(GameObject v) => Suppresses(v, Time.frameCount);

        public void Mark(GameObject v, int frameCount) { victim = v; frame = frameCount; }
        public bool Suppresses(GameObject v, int frameCount) => victim != null && v == victim && frameCount == frame;
    }

    /// <summary>P3-07: a boss advanced to its next phase (0-based <see cref="Phase"/>).</summary>
    public readonly struct BossPhaseChanged
    {
        public readonly GameObject Boss;
        public readonly int Phase;
        public BossPhaseChanged(GameObject boss, int phase) { Boss = boss; Phase = phase; }
    }

    /// <summary>P3-04: a teleporting enemy chose its blink <see cref="Destination"/>; the positional cue
    /// should play from there (the blink lands ~0.5 s later).</summary>
    public readonly struct EnemyBlinkTelegraph
    {
        public readonly Vector3 Destination;
        public readonly GameObject Enemy;
        public EnemyBlinkTelegraph(Vector3 destination, GameObject enemy) { Destination = destination; Enemy = enemy; }
    }
}
