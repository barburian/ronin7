using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
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

        // Sword sparks scale with swing speed (m/s) between these.
        private const int SparkMin = 4;
        private const int SparkMax = 16;
        private const float SparkSpeedLo = 2f;
        private const float SparkSpeedHi = 12f;

        private VfxPool pool;
        private ParticleSystem spark, blood, deflect, hitFlash, death, muzzle, bolt, explosion;

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
        }

        private void OnSwordImpact(SwordImpact e)
        {
            HandledCount++;
            float t = Mathf.InverseLerp(SparkSpeedLo, SparkSpeedHi, e.Speed);
            Emit(spark, e.Point, Mathf.RoundToInt(Mathf.Lerp(SparkMin, SparkMax, t)));
            // Organic victims (anything with a MeleeAttacker) bleed when the blood toggle is on;
            // otherwise the neon sparks above are the whole effect.
            if (GraphicsRuntime.CombatBlood && IsOrganic(e.Victim))
                Emit(blood, e.Point, BloodBurst);
        }

        private void OnDeflect(SwordDeflected e) { HandledCount++; Emit(deflect, e.Point, DeflectBurst); }
        private void OnDamaged(EntityDamaged e) { HandledCount++; Emit(hitFlash, e.Info.Point, DamageBurst); }
        private void OnDied(EntityDied e) { HandledCount++; Emit(death, Center(e.Entity), DeathBurst); }
        private void OnShipFired(ShipWeaponFired e) { HandledCount++; Emit(muzzle, e.WorldPoint, MuzzleBurst); }
        private void OnBoltImpact(ProjectileImpact e) { HandledCount++; Emit(bolt, e.WorldPoint, BoltBurst); }

        private void OnEnemyShipDestroyed(EnemyShipDestroyed e)
        { HandledCount++; Emit(explosion, ShipPoint(e.Ship, e.UniversePosition), ExplosionBurst); }

        private void OnPlayerShipDestroyed(PlayerShipDestroyed e)
        { HandledCount++; Emit(explosion, e.WorldPoint, ExplosionBurst); }

        private void Emit(ParticleSystem prefab, Vector3 point, int baseCount)
        {
            if (pool == null) return; // edit-mode/tests: handler still counts, just nothing to spawn
            int count = Mathf.Max(1, Mathf.RoundToInt(baseCount * GraphicsRuntime.ParticleScale));
            pool.Play(prefab, point, count);
        }

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
