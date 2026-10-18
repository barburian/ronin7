using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>
    /// P4-05: on-hit / on-kill / on-parry boon procs (Hemorrhage bleed, Storm chains, Counterweight,
    /// Whirlwind, Overclock lifesteal, Thunder Riposte, Ghost Step, Last Stand). Sits on the rig root next
    /// to <see cref="PlayerCombatModifiers"/> and <see cref="Health"/>. Every proc deals
    /// <see cref="DamageType.Status"/> damage, which never flinches, never builds posture and never re-procs.
    /// Proc magnitudes scale with <see cref="PlayerCombatModifiers.ProcRateScale"/> (S8 Element Imbue).
    /// Allocation-free: fixed bleed slots, a reused Health scratch array, one pooled line.
    /// </summary>
    public class RunBoonProcs : MonoBehaviour
    {
        public const int MaxBleeds = 5;
        public const float TickInterval = 0.25f;
        public const int BleedTicks = 12;
        public const float BaseBleed = 0.05f;
        public const float StaticMeterMax = 4f;
        public const float ClashRadius = 2.5f;
        public const float ClashPosture = 25f;
        public const float WhirlwindWindow = 0.15f;
        public const float LineSeconds = 0.12f;
        private const int MaxLinePoints = 8;

        [SerializeField] private PlayerCombatModifiers mods;
        [SerializeField] private Health health;
        [Tooltip("Pooled additive line for chain procs (built by RoguelikeArenaBuilder.EnsureBoonProcs).")]
        [SerializeField] private LineRenderer procLine;

        private struct Bleed { public Health Target; public float PerTick; public int TicksLeft; }
        private readonly Bleed[] bleeds = new Bleed[MaxBleeds];
        private int bleedCount;
        private float tickTimer;
        private float staticMeter;
        private float lineOffAt = -1f;
        private Health[] scratch = new Health[64];
        private readonly Vector3[] linePoints = new Vector3[MaxLinePoints];
        private readonly Health[] visited = new Health[MaxLinePoints];

        private Health lastVictim;
        private float lastVictimAmount, lastVictimTime = -1f;
        private bool lastVictimBonused;

        // ---- pure helpers (EditMode-tested) ----

        /// <summary>Total bleed dealt over the DoT for one hit. 0 when Serrated Edge isn't held.</summary>
        public static float BleedTotal(float hit, float bleed, float procScale, bool comboBleed, int combo) =>
            bleed <= 0f ? 0f : hit * (BaseBleed + bleed) * procScale * (comboBleed && combo >= 3 ? 2f : 1f);

        public static int ChainJumps(float chainJumps) => 1 + Mathf.FloorToInt(Mathf.Max(0f, chainJumps));
        public static float ChainRange(float chainJumps) => 4f + 1.5f * Mathf.Max(0f, chainJumps);

        /// <summary>Advances the Static Edge meter by <paramref name="procScale"/>; fires (and drains 4) at 4+.</summary>
        public static float StaticMeterStep(float meter, float procScale, out bool fire)
        {
            meter += procScale;
            fire = meter >= StaticMeterMax;
            return fire ? meter - StaticMeterMax : meter;
        }

        private void Awake()
        {
            if (mods == null) mods = GetComponent<PlayerCombatModifiers>();
            if (health == null) health = GetComponent<Health>();
            if (procLine != null) procLine.enabled = false;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EntityDamaged>(OnDamaged);
            EventBus.Subscribe<EntityDied>(OnDied);
            EventBus.Subscribe<PerfectParry>(OnPerfectParry);
            EventBus.Subscribe<BladeClash>(OnClash);
            EventBus.Subscribe<AbilityActivated>(OnAbility);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EntityDamaged>(OnDamaged);
            EventBus.Unsubscribe<EntityDied>(OnDied);
            EventBus.Unsubscribe<PerfectParry>(OnPerfectParry);
            EventBus.Unsubscribe<BladeClash>(OnClash);
            EventBus.Unsubscribe<AbilityActivated>(OnAbility);
            bleedCount = 0;
            if (mods != null) mods.LastStandActive = false;
        }

        private float B(BoonEffectKind k) => mods != null ? mods.Boon(k) : 0f;

        private void Update()
        {
            if (mods != null && health != null)
                mods.LastStandActive = mods.Boon(BoonEffectKind.LastStand) > 0f
                    && PlayerCombatModifiers.LastStandActiveAt(health.Current, health.Max);

            if (procLine != null && lineOffAt >= 0f && Time.time >= lineOffAt) { procLine.enabled = false; lineOffAt = -1f; }

            if (bleedCount == 0) { tickTimer = 0f; return; }
            tickTimer += Time.deltaTime;
            if (tickTimer < TickInterval) return;
            tickTimer -= TickInterval;
            for (int i = bleedCount - 1; i >= 0; i--)
            {
                if (i >= bleedCount) continue;
                var t = bleeds[i].Target;
                if (t == null || !t.IsAlive) { RemoveBleed(i); continue; }
                t.ApplyDamage(new DamageInfo(bleeds[i].PerTick, t.transform.position, Vector3.zero, gameObject, DamageType.Status));
                // The tick may have killed it (OnDied already removed the slot) — only touch our own entry.
                if (i < bleedCount && bleeds[i].Target == t && --bleeds[i].TicksLeft <= 0) RemoveBleed(i);
            }
        }

        private void OnDamaged(EntityDamaged e)
        {
            if (e.Info.Source != gameObject || e.Info.Type == DamageType.Status || mods == null) return;
            if (!e.Entity.TryGetComponent<Health>(out var victim) || !IsEnemy(victim)) return;
            float amount = e.Info.Amount;
            Vector3 p = e.Entity.transform.position;

            // Thunder Riposte (Duo): the riposte hit arcs to every other enemy nearby.
            float riposteChain = B(BoonEffectKind.RiposteChain);
            if (riposteChain > 0f && e.Entity == mods.LastRiposteVictim && Time.frameCount == mods.LastRiposteFrame)
            {
                int pts = 0;
                linePoints[pts++] = p;
                int n = SnapshotActive();
                for (int i = 0; i < n; i++)
                {
                    var h = scratch[i];
                    if (h == null || h == victim || !h.IsAlive || !IsEnemy(h)) continue;
                    Vector3 hp = h.transform.position;
                    if ((hp - p).sqrMagnitude > riposteChain * riposteChain) continue;
                    if (pts < MaxLinePoints) linePoints[pts++] = hp;
                    Status(h, amount * 0.5f);
                }
                if (pts > 1) { ShowLine(pts); Pulse(0.5f); }
            }

            // Open Veins: a bleeding target takes extra.
            float vuln = B(BoonEffectKind.BleedVulnerability);
            if (vuln > 0f && victim.IsAlive && FindBleed(victim) >= 0) Status(victim, amount * vuln);

            // Serrated Edge (+ Crimson Tide doubling at combo 3+).
            float total = BleedTotal(amount, B(BoonEffectKind.Bleed), mods.ProcRateScale,
                B(BoonEffectKind.ComboBleed) > 0f, mods.ComboCount);
            if (total > 0f && victim.IsAlive) AddBleed(victim, total);

            // Whirlwind: a second victim within 0.15 s bonuses both (the earlier one only once).
            float multi = B(BoonEffectKind.MultiHitBonus);
            if (multi > 0f)
            {
                if (lastVictim != null && lastVictim != victim && Time.time - lastVictimTime <= WhirlwindWindow)
                {
                    if (victim.IsAlive) Status(victim, amount * multi);
                    if (!lastVictimBonused && lastVictim.IsAlive) Status(lastVictim, lastVictimAmount * multi);
                    lastVictimBonused = true;
                }
                else lastVictimBonused = false;
                if (lastVictim != victim) { lastVictim = victim; lastVictimAmount = amount; }
                lastVictimTime = Time.time;
            }

            // Overclock lifesteal while Overdrive is live.
            float steal = B(BoonEffectKind.OverdriveLifesteal);
            if (steal > 0f && mods.OverdriveMultiplier > 1f && health != null) health.Heal(steal);

            // Static Edge: every 4 meter (imbue doubles the step) chains lightning from the victim.
            float stat = B(BoonEffectKind.StaticChain);
            if (stat > 0f)
            {
                staticMeter = StaticMeterStep(staticMeter, mods.ProcRateScale, out bool fire);
                if (fire) Chain(victim, stat * Mathf.Min(mods.BoonMultiplier, 2f), B(BoonEffectKind.ChainJumps));
            }
        }

        private void Chain(Health from, float dmg, float chainJumps)
        {
            int jumps = ChainJumps(chainJumps);
            float range = ChainRange(chainJumps);
            int pts = 0, vis = 0;
            visited[vis++] = from;
            linePoints[pts++] = from.transform.position;
            if (from.IsAlive) Status(from, dmg);
            int n = SnapshotActive();
            Vector3 cur = linePoints[0];
            for (int j = 0; j < jumps && vis < visited.Length; j++)
            {
                Health best = null; float bestSq = range * range;
                for (int i = 0; i < n; i++)
                {
                    var h = scratch[i];
                    if (h == null || !h.IsAlive || !IsEnemy(h) || Visited(h, vis)) continue;
                    float sq = (h.transform.position - cur).sqrMagnitude;
                    if (sq <= bestSq) { bestSq = sq; best = h; }
                }
                if (best == null) break;
                visited[vis++] = best;
                cur = best.transform.position;
                linePoints[pts++] = cur;
                Status(best, dmg);
            }
            ShowLine(pts);
            Pulse(0.35f);
        }

        private bool Visited(Health h, int vis)
        {
            for (int i = 0; i < vis; i++) if (visited[i] == h) return true;
            return false;
        }

        private void OnDied(EntityDied e)
        {
            if (e.Entity == null || !e.Entity.TryGetComponent<Health>(out var h)) return;
            int i = FindBleed(h);
            if (i < 0) return;
            RemoveBleed(i);
            float heal = B(BoonEffectKind.HealOnBleedKill);
            if (heal > 0f && health != null && health.IsAlive) { health.Heal(heal); Pulse(0.3f); }
        }

        private void OnPerfectParry(PerfectParry e)
        {
            float heal = B(BoonEffectKind.HealOnPerfectParry);
            if (heal > 0f && health != null && health.IsAlive) health.Heal(heal);
        }

        private void OnClash(BladeClash e)
        {
            float shock = B(BoonEffectKind.ClashShock);
            if (shock <= 0f) return;
            int n = SnapshotActive();
            for (int i = 0; i < n; i++)
            {
                var h = scratch[i];
                if (h == null || !h.IsAlive || !IsEnemy(h)) continue;
                if ((h.transform.position - e.Point).sqrMagnitude > ClashRadius * ClashRadius) continue;
                Status(h, shock);
                if (h.TryGetComponent<PostureMeter>(out var pm)) pm.AddPosture(ClashPosture, gameObject);
            }
            Pulse(0.5f);
        }

        private void OnAbility(AbilityActivated e)
        {
            float window = B(BoonEffectKind.GhostStep);
            if (window <= 0f || e.Id != AbilityId.PhaseStep || health == null) return;
            int n = SnapshotActive();
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                var h = scratch[i];
                if (h == null || !h.IsAlive || !h.TryGetComponent<MeleeAttacker>(out var ma)) continue;
                if (ma.Target != health || ma.TimeToActive > window) continue;
                ma.ForceStagger();
                EventBus.Publish(new PerfectParry(ma.transform.position, ma.gameObject, 1f));
                any = true;
            }
            if (any) Pulse(0.6f);
        }

        // ---- internals ----

        private void Status(Health h, float amount)
        {
            if (amount <= 0f) return;
            Vector3 p = h.transform.position;
            h.ApplyDamage(new DamageInfo(amount, p, Vector3.zero, gameObject, DamageType.Status));
        }

        private bool IsEnemy(Health h) => h != health && h.TryGetComponent<Enemy>(out _);

        /// <summary>Copies <see cref="Health.Active"/> into the scratch array: proc damage can kill and
        /// mutate the live registry mid-loop.</summary>
        private int SnapshotActive()
        {
            var list = Health.Active;
            if (scratch.Length < list.Count) scratch = new Health[list.Count * 2]; // rare growth only
            list.CopyTo(scratch, 0);
            return list.Count;
        }

        private int FindBleed(Health h)
        {
            for (int i = 0; i < bleedCount; i++) if (bleeds[i].Target == h) return i;
            return -1;
        }

        private void AddBleed(Health h, float total)
        {
            int i = FindBleed(h);
            if (i >= 0) total += bleeds[i].PerTick * bleeds[i].TicksLeft; // re-bleed: add the remainder
            else if (bleedCount < MaxBleeds) i = bleedCount++;
            else
            {
                i = 0; // full: replace the slot closest to expiring
                for (int k = 1; k < bleedCount; k++) if (bleeds[k].TicksLeft < bleeds[i].TicksLeft) i = k;
            }
            bleeds[i] = new Bleed { Target = h, PerTick = total / BleedTicks, TicksLeft = BleedTicks };
        }

        private void RemoveBleed(int i)
        {
            bleeds[i] = bleeds[--bleedCount];
            bleeds[bleedCount] = default;
        }

        private void ShowLine(int pts)
        {
            if (procLine == null || pts < 2) return;
            procLine.positionCount = pts;
            procLine.SetPositions(linePoints); // uses the first positionCount entries
            procLine.enabled = true;
            lineOffAt = Time.time + LineSeconds;
        }

        private static void Pulse(float amp)
        {
            Haptics.Pulse(GameSettings.Active.LeftHanded ? XRNode.LeftHand : XRNode.RightHand, amp, 0.04f);
        }

        /// <summary>Test hook: true while <paramref name="h"/> has a live bleed slot.</summary>
        internal bool IsBleeding(Health h) => FindBleed(h) >= 0;
    }
}
