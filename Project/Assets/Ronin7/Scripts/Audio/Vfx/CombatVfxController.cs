using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using Ronin7.Player;
using Ronin7.Ship;
using UnityEngine;

namespace Ronin7.Audio.Vfx
{
    /// <summary>
    /// Persistent, event-driven combat VFX — the visual sibling of <see cref="AudioDirector"/> and
    /// <see cref="Ronin7.Player.CombatFeedbackController"/>. Subscribes to the same combat and
    /// space-combat events and turns each into a pooled neon particle burst (sparks, deflect flash,
    /// hit flash, death burst, muzzle/bolt/explosion). Purely additive: it never publishes or mutates
    /// gameplay state.
    ///
    /// Lives in the Audio assembly because — exactly like AudioDirector — it must see events from BOTH
    /// <c>Ronin7.Combat</c> AND <c>Ronin7.Ship</c>; the Combat assembly references only Core
    /// and can't see Ship's events. Reads the resolved tier from the Core <see cref="GraphicsRuntime"/>
    /// snapshot (burst counts × ParticleScale; red splatter gated on CombatBlood) so it takes no Flow
    /// dependency. Effect prefabs are built head­lessly by VfxPrefabBuilder into <c>Resources/Vfx</c> and
    /// loaded by name — no per-scene inspector wiring, and a rebuild can't null the references.
    /// </summary>
    public class CombatVfxController : MonoBehaviour
    {
        public static CombatVfxController Instance { get; private set; }

        // Base burst counts at the High tier (scaled by GraphicsRuntime.ParticleScale at spawn).
        private const int DeflectBurst = 16;
        private const int DamageBurst = 4;
        private const int DeathBurst = 22;
        private const int BloodBurst = 10;
        private const int MuzzleBurst = 6;
        private const int BoltBurst = 7;
        private const int ExplosionBurst = 26;
        private const int TreasureBurst = 30;
        private const int ResistedBurst = DeflectBurst / 4;
        private static readonly Color32 ResistedTint = new Color32(120, 140, 170, 255); // blue-grey
        private static readonly Color32 WhiteTint = new Color32(255, 255, 255, 255);
        private static readonly Color32 VioletTint = new Color32(160, 90, 255, 255);
        private static readonly Color32 GoldWhiteTint = new Color32(255, 235, 170, 255);
        private static readonly Color32 RedTint = new Color32(255, 40, 30, 255);
        private static readonly Color32 PostureBlue = new Color32(80, 140, 255, 255);
        private static readonly Color32 OverdriveMagenta = new Color32(255, 50, 180, 255);
        private static readonly Color32 SpawnViolet = new Color32(170, 90, 255, 255);
        private const float RingSize = 0.5f;      // parry ring <= 0.5 m
        private const float KiaiRingSize = 2.2f;
        private static readonly Color32 WeaknessTint = new Color32(255, 200, 60, 255);  // gold

        // Sword sparks scale with swing speed (m/s) between these.
        private const int SparkMin = 4;
        private const int SparkMax = 16;
        private const float SparkSpeedLo = 2f;
        private const float SparkSpeedHi = 12f;

        private VfxPool pool;
        private Material dissolveMat;
        private ParticleSystem spark, blood, deflect, hitFlash, death, muzzle, bolt, explosion;
        private ParticleSystem streak, wisp, ring, shard, embers, glyph, crescent; // P5-03
        private Afterimage afterimage;
        private UnbrokenShell shell;
        private ImpactDecals decals;
        private static readonly Color32 BoonCommon = new Color32(184, 192, 204, 255);
        private static readonly Color32 BoonRare = new Color32(63, 169, 255, 255);
        private static readonly Color32 BoonEpic = new Color32(255, 50, 180, 255);   // magenta, per the roadmap
        private static readonly Color32 BoonDuo = new Color32(255, 194, 74, 255);
        private const int BoonBurst = 24;

        /// <summary>Test hook: combat events handled since the last <see cref="Subscribe"/> — proves
        /// the controller is subscribed (and stays unsubscribed after <see cref="Unsubscribe"/>).</summary>
        internal int HandledCount { get; private set; }

        private void Awake()
        {
            // enabled = false first: Destroy defers to end of frame, and a combat event firing
            // this frame would hit the duplicate's never-initialized pool (same shape as the
            // AudioDirector boot-scene-reload NRE).
            if (Instance != null && Instance != this) { enabled = false; Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            var poolGo = new GameObject("VfxPool");
            poolGo.transform.SetParent(transform, false);
            pool = new VfxPool(poolGo.transform);

            spark = Load("SwordSpark");
            blood = Load("BloodSplatter");
            deflect = Load("DeflectFlash");
            hitFlash = Load("HitFlash");
            death = Load("DeathBurst");
            muzzle = Load("MuzzleFlash");
            bolt = Load("BoltImpact");
            explosion = Load("ShipExplosion");
            dissolveMat = Resources.Load<Material>("Vfx/EnemyPBR_Dissolve");
            streak = Load("SparkStreakFx");
            wisp = Load("SpectralWisp");
            ring = Load("ParryRing");
            shard = Load("PostureShard");
            embers = Load("SpawnEmbers");
            glyph = Load("GlyphPuff");
            crescent = Load("SlashCrescent");
            afterimage = new Afterimage(transform, Resources.Load<Material>("Vfx/MirrorGhost"));
            var shellPrefab = Resources.Load<GameObject>("VfxShell/UnbrokenShell");
            if (shellPrefab != null) shell = new UnbrokenShell(transform, shellPrefab);
            if (streak != null) decals = new ImpactDecals(transform, streak.GetComponent<ParticleSystemRenderer>().sharedMaterial);
        }

        private static ParticleSystem Load(string name)
        {
            var go = Resources.Load<GameObject>($"Vfx/{name}");
            return go != null ? go.GetComponent<ParticleSystem>() : null;
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        // Subscribe/Unsubscribe are internal so EditMode tests can drive the lifecycle directly
        // (Awake/OnEnable don't fire on AddComponent in edit mode).
        internal void Subscribe()
        {
            EventBus.Subscribe<SwordImpact>(OnSwordImpact);
            EventBus.Subscribe<SwordDeflected>(OnDeflect);
            EventBus.Subscribe<EntityDamaged>(OnDamaged);
            EventBus.Subscribe<EntityDied>(OnDied);
            EventBus.Subscribe<ShipWeaponFired>(OnShipFired);
            EventBus.Subscribe<ProjectileImpact>(OnBoltImpact);
            EventBus.Subscribe<EnemyShipDestroyed>(OnEnemyShipDestroyed);
            EventBus.Subscribe<PlayerShipDestroyed>(OnPlayerShipDestroyed);
            EventBus.Subscribe<TreasureOpened>(OnTreasureOpened);
            EventBus.Subscribe<HitResisted>(OnHitResisted);
            EventBus.Subscribe<WeaknessExploited>(OnWeaknessExploited);
            EventBus.Subscribe<PostureBroken>(OnPostureBroken);
            EventBus.Subscribe<EnemySpawning>(OnSpawning);
            EventBus.Subscribe<AbilityActivated>(OnAbility);
            EventBus.Subscribe<PhaseStepStarted>(OnPhaseStart);
            EventBus.Subscribe<SkillVfx>(OnSkill);
            EventBus.Subscribe<EnemyActivated>(OnEnemyActivated);
            EventBus.Subscribe<SwordWorldHit>(OnWorldHit);
            EventBus.Subscribe<BoonChosen>(OnBoonChosen);
        }

        internal void Unsubscribe()
        {
            EventBus.Unsubscribe<SwordImpact>(OnSwordImpact);
            EventBus.Unsubscribe<SwordDeflected>(OnDeflect);
            EventBus.Unsubscribe<EntityDamaged>(OnDamaged);
            EventBus.Unsubscribe<EntityDied>(OnDied);
            EventBus.Unsubscribe<ShipWeaponFired>(OnShipFired);
            EventBus.Unsubscribe<ProjectileImpact>(OnBoltImpact);
            EventBus.Unsubscribe<EnemyShipDestroyed>(OnEnemyShipDestroyed);
            EventBus.Unsubscribe<PlayerShipDestroyed>(OnPlayerShipDestroyed);
            EventBus.Unsubscribe<TreasureOpened>(OnTreasureOpened);
            EventBus.Unsubscribe<HitResisted>(OnHitResisted);
            EventBus.Unsubscribe<WeaknessExploited>(OnWeaknessExploited);
            EventBus.Unsubscribe<PostureBroken>(OnPostureBroken);
            EventBus.Unsubscribe<EnemySpawning>(OnSpawning);
            EventBus.Unsubscribe<AbilityActivated>(OnAbility);
            EventBus.Unsubscribe<PhaseStepStarted>(OnPhaseStart);
            EventBus.Unsubscribe<SkillVfx>(OnSkill);
            EventBus.Unsubscribe<EnemyActivated>(OnEnemyActivated);
            EventBus.Unsubscribe<SwordWorldHit>(OnWorldHit);
            EventBus.Unsubscribe<BoonChosen>(OnBoonChosen);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (afterimage != null && afterimage.Visible) afterimage.Tick(now);
            if (shell != null && shell.Visible) shell.Tick(now);
            if (decals != null && decals.Live > 0) decals.Tick(now);
        }

        // BUG-39: resisted/exploited hits own their VFX; the generic sparks + damage flash skip that victim this frame.
        private HitConfirmLatch latch;

        private void OnSwordImpact(SwordImpact e)
        {
            if (latch.Suppresses(e.Victim)) return;
            HandledCount++;
            float t = Mathf.InverseLerp(SparkSpeedLo, SparkSpeedHi, e.Speed);
            int n = Mathf.RoundToInt(Mathf.Lerp(SparkMin, SparkMax, t));
            var enemy = e.Victim != null ? e.Victim.GetComponentInParent<Enemy>() : null;
            switch (enemy != null ? enemy.VfxClass : MaterialClass.Organic)
            {
                case MaterialClass.Mech:     // amber streaks + a white flash
                    Emit(streak, e.Point, n); Emit(hitFlash, e.Point, 2, WhiteTint); break;
                case MaterialClass.Spectral: // cyan/violet wisps drifting up
                    Emit(wisp, e.Point, n); Emit(wisp, e.Point, n / 2, VioletTint); break;
                case MaterialClass.Armored:  // short white-gold
                    Emit(spark, e.Point, Mathf.Max(2, n / 2), GoldWhiteTint); break;
                default:                     // organic: red + amber
                    Emit(spark, e.Point, n); Emit(spark, e.Point, n / 2, RedTint); break;
            }
            // Organic victims (anything with a MeleeAttacker) bleed when the blood toggle is on;
            // otherwise the neon sparks above are the whole effect.
            if (GraphicsRuntime.CombatBlood && IsOrganic(e.Victim))
                Emit(blood, e.Point, BloodBurst);
        }

        private void OnDeflect(SwordDeflected e) { HandledCount++; Emit(deflect, e.Point, DeflectBurst); Emit(ring, e.Point, 1, null, RingSize); }
        private void OnDamaged(EntityDamaged e)
        {
            if (latch.Suppresses(e.Entity)) return;
            HandledCount++;
            Emit(hitFlash, e.Info.Point, DamageBurst);
        }
        private void OnDied(EntityDied e)
        {
            HandledCount++;
            Emit(death, Center(e.Entity), DeathBurst);
            // P5-03: only dying enemies swap to the dissolve material.
            if (pool != null && e.Entity != null && e.Entity.GetComponentInParent<Enemy>() != null)
                DeathDissolve.Begin(e.Entity, dissolveMat);
        }
        // P2-02: Treasure TAKE — a rarity-coloured burst from the same pooled death-burst system.
        private void OnTreasureOpened(TreasureOpened e) { HandledCount++; Emit(death, e.Point, TreasureBurst, e.Color); }

        // P3-03: resisted = quarter-burst blue-grey deflect flash; exploited = max gold sparks plus a full
        // hit flash on the victim's body (MeleeAttacker's own flash timer fades it back, no new state).
        private void OnHitResisted(HitResisted e) { latch.Mark(e.Victim); HandledCount++; Emit(deflect, e.Point, ResistedBurst, ResistedTint); }

        private void OnWeaknessExploited(WeaknessExploited e)
        {
            latch.Mark(e.Victim);
            HandledCount++;
            Emit(spark, e.Point, SparkMax, WeaknessTint);
            var body = e.Victim != null ? e.Victim.GetComponentInParent<MeleeAttacker>() : null;
            if (body != null) RendererTint.Flash(body.BodyRenderer, 1f);
        }

        // P5-03: white shards + a blue rim ring. Particle billboards only: no screen flash, no shake.
        private void OnPostureBroken(PostureBroken e)
        {
            HandledCount++;
            Vector3 p = Center(e.Entity) + Vector3.up;
            Emit(shard, p, 24);
            Emit(ring, p, 1, PostureBlue, RingSize);
        }

        private void OnSpawning(EnemySpawning e) { HandledCount++; Emit(embers, e.Point, 14); }

        // Ability events carry only an id, so the effect is anchored on the player's feet / head.
        private void OnAbility(AbilityActivated e)
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 head = cam.transform.position;
            Vector3 feet = new Vector3(head.x, head.y - 1.5f, head.z);
            switch (e.Id)
            {
                case AbilityId.PhaseStep: // end of the blink: puff at the landing, ghost left at the origin
                    HandledCount++; Emit(glyph, feet + Vector3.up * 0.9f, 12); afterimage?.Show(Time.unscaledTime); break;
                case AbilityId.Unbroken: HandledCount++; shell?.Play(cam.transform, Time.unscaledTime); break;
                case AbilityId.Overdrive: HandledCount++; Emit(ring, feet + Vector3.up * 0.05f, 1, OverdriveMagenta, 1.2f); break;
                case Ronin7.Player.ArtId.KiaiPalm: HandledCount++; Emit(ring, feet + Vector3.up * 0.05f, 1, null, KiaiRingSize); break;
            }
        }

        // P5-03: start of a Phase Step. Freeze the hands + sword pose, puff at the origin (8 + 12 at the end <= 20).
        private void OnPhaseStart(PhaseStepStarted e)
        {
            HandledCount++;
            Emit(glyph, e.Origin + Vector3.up * 0.9f, 8);
            var rig = VRRig.Instance;
            if (afterimage == null || rig == null) return;
            phaseRoots.Clear();
            if (rig.LeftHand != null) phaseRoots.Add(rig.LeftHand);
            if (rig.RightHand != null) phaseRoots.Add(rig.RightHand);
            var holster = rig.GetComponentInChildren<KatanaHolster>(true); // once per blink, not per frame
            var sword = holster != null && holster.Sword != null ? holster.Sword.transform : null;
            if (sword != null && !sword.IsChildOf(rig.LeftHand) && !sword.IsChildOf(rig.RightHand)) phaseRoots.Add(sword);
            afterimage.Capture(phaseRoots);
        }
        private readonly System.Collections.Generic.List<Transform> phaseRoots = new();

        private void OnSkill(SkillVfx e)
        {
            HandledCount++;
            switch (e.Id)
            {
                case SkillVfx.Riposte: Emit(streak, e.Point, 8, RedTint, 0.045f); break;            // red edge glint
                case SkillVfx.Iaido: Emit(streak, e.Point, 6, WhiteTint, 0.07f); break;             // white flash line
                case SkillVfx.ChargedSlash:                                                         // crescent travelling the slash range
                    if (pool != null && e.Direction.sqrMagnitude > 1e-4f)
                        pool.Play(crescent, e.Point, 1, null, null, e.Direction.normalized * (ChargeLogic.Range / 0.3f));
                    break;
            }
        }

        // P5-03 spawn portal: the floor disc is RunArenaController's telegraph; here the beam + reverse dissolve.
        private void OnEnemyActivated(EnemyActivated e)
        {
            HandledCount++;
            if (e.Enemy == null) return;
            Emit(streak, e.Enemy.transform.position, 3, SpawnViolet, 0.05f, Vector3.up * 9f, shared: true); // G6: adds spawn together -> 1 draw
            if (pool != null) DeathDissolve.BeginReverse(e.Enemy, dissolveMat);
        }

        private void OnWorldHit(SwordWorldHit e)
        {
            HandledCount++;
            Emit(streak, e.Point, 4, null, null, e.Normal * 3f);
            decals?.Place(e.Point, e.Normal, Time.unscaledTime);
        }

        // Rarity comes from the catalog (BoonChosen carries only the id).
        private void OnBoonChosen(BoonChosen e)
        {
            HandledCount++;
            var cam = Camera.main;
            if (cam == null) return;
            var def = RunDirector.Instance != null && RunDirector.Instance.Catalog != null ? RunDirector.Instance.Catalog.Find(e.BoonId) : null;
            Emit(death, cam.transform.position + cam.transform.forward * 0.9f - Vector3.up * 0.25f, BoonBurst, BoonTint(def != null ? def.rarity : BoonRarity.Common));
        }

        internal static Color32 BoonTint(BoonRarity r)
            => r == BoonRarity.Epic ? BoonEpic : r == BoonRarity.Duo ? BoonDuo : r == BoonRarity.Rare ? BoonRare : BoonCommon;

        private void OnShipFired(ShipWeaponFired e) { HandledCount++; Emit(muzzle, e.WorldPoint, MuzzleBurst); }
        private void OnBoltImpact(ProjectileImpact e) { HandledCount++; Emit(bolt, e.WorldPoint, BoltBurst); }

        private void OnEnemyShipDestroyed(EnemyShipDestroyed e)
        { HandledCount++; Emit(explosion, ShipPoint(e.Ship, e.UniversePosition), ExplosionBurst); }

        private void OnPlayerShipDestroyed(PlayerShipDestroyed e)
        { HandledCount++; Emit(explosion, e.WorldPoint, ExplosionBurst); }

        private void Emit(ParticleSystem prefab, Vector3 point, int baseCount, Color32? tint = null, float? size = null, Vector3? velocity = null, bool shared = false)
        {
            if (pool == null) return; // edit-mode/tests: handler still counts, just nothing to spawn
            pool.Play(prefab, point, ScaledCount(baseCount), tint, size, velocity, shared);
        }

        /// <summary>Burst size after the tier's <see cref="GraphicsRuntime.ParticleScale"/>; never below 1.</summary>
        internal static int ScaledCount(int baseCount)
            => Mathf.Max(1, Mathf.RoundToInt(baseCount * GraphicsRuntime.ParticleScale));

        // All on-foot enemies derive from MeleeAttacker (which RequireComponent(Health)); the sword
        // victim is the Health GameObject, so this is the dependency-light "is this a creature?" test.
        private static bool IsOrganic(GameObject victim)
            => victim != null && victim.GetComponentInParent<MeleeAttacker>() != null;

        private static Vector3 Center(GameObject go) => go != null ? go.transform.position : Vector3.zero;

        // EnemyShipDestroyed carries a universe-local position; prefer the live ship transform when present.
        private static Vector3 ShipPoint(GameObject ship, Vector3 fallback)
            => ship != null ? ship.transform.position : fallback;
    }
}
