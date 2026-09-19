using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Player;
using Ronin7.Ship;
using UnityEngine;

namespace Ronin7.Audio
{
    /// <summary>
    /// Persistent, event-driven audio. Subscribes to gameplay events on the <see cref="EventBus"/>
    /// and plays assigned clips — so it needs no hard references to the systems that emit them
    /// (Core + Combat + Ship only for the event types). One-shots fire at the world point of the
    /// event via <see cref="AudioSource.PlayClipAtPoint"/>; per-mode ambience and combat/explore
    /// music crossfade on looping sources, and an engine bed tracks ship speed while flying.
    ///
    /// Assets are user-supplied: drop AudioClips into Assets/Ronin7/Audio and assign them in
    /// the Inspector. Every clip field is optional — unassigned clips simply stay silent, so the
    /// loop is fully playable before audio art lands.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        /// <summary>
        /// Live mix setters used by the settings menu. These only retarget the bed/one-shot
        /// volumes; <see cref="Update"/> eases the looping beds toward the new level on its own,
        /// so changes are click-free and allocation-free.
        /// </summary>
        public void SetSfxVolume(float v) => sfxVolume = Mathf.Clamp01(v);
        public void SetMusicVolume(float v) => musicVolume = Mathf.Clamp01(v);
        public void SetAmbienceVolume(float v) => ambienceVolume = Mathf.Clamp01(v);

        [Header("Combat one-shots")]
        [SerializeField] private AudioClip swordDeflect;
        [SerializeField] private AudioClip swordImpact;
        [SerializeField] private AudioClip playerHit;

        [Header("Space-combat one-shots")]
        [SerializeField] private AudioClip shipGunFire;
        [SerializeField] private AudioClip boltImpact;
        [SerializeField] private AudioClip enemyExplosion;
        [SerializeField] private AudioClip shipHullHit;

        [Header("Flow one-shots")]
        [SerializeField] private AudioClip landing;
        [SerializeField] private AudioClip extraction;

        [Header("Combat feedback one-shots")]
        [Tooltip("Spatialized telegraph cue for an enemy's attack windup (see AttackWindupStarted) — " +
                 "the readable-behind-you parry cue now that art enemies have no visible blade.")]
        [SerializeField] private AudioClip attackWindup;
        [SerializeField] private AudioClip enemyDefeated;
        [SerializeField] private AudioClip abilityActivate;

        [Header("Roguelike run one-shots")]
        [SerializeField] private AudioClip boonChosen;

        [Header("Ambience (looping, per mode)")]
        [SerializeField] private AudioClip flightAmbience;
        [SerializeField] private AudioClip onFootAmbience;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.5f;
        [SerializeField] private float ambienceFade = 1.5f;

        [Header("Roguelike arena ambience (looping, per sector — see RunMapGenerator.SectorCount)")]
        [Tooltip("Sector 0 (\"rust\") — see ArenaRoomLibrary's biome ids.")]
        [SerializeField] private AudioClip sectorAmbienceRust;
        [Tooltip("Sector 1 (\"program\").")]
        [SerializeField] private AudioClip sectorAmbienceProgram;
        [Tooltip("Sector 2 (\"garden\").")]
        [SerializeField] private AudioClip sectorAmbienceGarden;

        [Header("Music (looping, by combat state)")]
        [SerializeField] private AudioClip exploreMusic;
        [SerializeField] private AudioClip combatMusic;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.45f;
        [SerializeField] private float musicFade = 1.5f;

        [Header("Engine loop (2D, throttle-driven)")]
        [SerializeField] private AudioClip engineLoop;
        [SerializeField, Range(0f, 1f)] private float engineVolume = 0.5f;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        // Engine response constants (kept out of the Inspector to limit knobs).
        private const float EngineIdleFraction = 0.4f; // volume while flying at zero speed
        private const float EnginePitchIdle = 0.85f;
        private const float EnginePitchFull = 1.5f;

        private AudioSource ambience;
        private AudioClip ambienceTarget;
        private float ambienceLevel; // smoothed current volume

        private AudioSource music;
        private AudioClip musicTarget;
        private float musicLevel;

        private AudioSource engine;
        private float engineLevel;

        private GameMode currentMode;
        private ShipController ship; // cached; re-found per scene for the engine loop
        private OneShotPool oneShots;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Destroy is deferred to end of frame, but Update would still run on this
                // duplicate THIS frame with ambience/music/engine never created (we return
                // before building them) — a guaranteed NRE on every boot-scene reload
                // (Return to Menu / Game Over). Disabling first suppresses that.
                enabled = false;
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            ambience = NewLoopSource();
            music = NewLoopSource();
            engine = NewLoopSource();

            // 3D one-shot pool replaces AudioSource.PlayClipAtPoint, which allocates a
            // temporary GameObject + AudioSource per call. Lives as a child so it shares
            // our DontDestroyOnLoad lifetime.
            var poolGo = new GameObject("OneShotPool");
            poolGo.transform.SetParent(transform, false);
            oneShots = poolGo.AddComponent<OneShotPool>();
        }

        /// <summary>A 2D, silent, looping source used for the ambience/music/engine beds.</summary>
        private AudioSource NewLoopSource()
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D bed
            src.volume = 0f;
            return src;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<SwordDeflected>(OnDeflect);
            EventBus.Subscribe<SwordImpact>(OnImpact);
            EventBus.Subscribe<PlayerHit>(OnPlayerHit);
            EventBus.Subscribe<LandingRequested>(OnLanding);
            EventBus.Subscribe<ZoneCompleted>(OnExtracted);
            EventBus.Subscribe<GameModeChanged>(OnModeChanged);
            EventBus.Subscribe<ShipWeaponFired>(OnShipFired);
            EventBus.Subscribe<ProjectileImpact>(OnBoltImpact);
            EventBus.Subscribe<EnemyShipDestroyed>(OnEnemyDestroyed);
            EventBus.Subscribe<PlayerShipDamaged>(OnPlayerShipDamaged);
            EventBus.Subscribe<SpaceEncounterStarted>(OnEncounterStarted);
            EventBus.Subscribe<SpaceEncounterCleared>(OnEncounterCleared);
            EventBus.Subscribe<AttackWindupStarted>(OnAttackWindup);
            EventBus.Subscribe<EntityDied>(OnEntityDied);
            EventBus.Subscribe<AbilityActivated>(OnAbilityActivated);
            EventBus.Subscribe<BoonChosen>(OnBoonChosen);
            EventBus.Subscribe<RunNodeEntered>(OnRunNodeEntered);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SwordDeflected>(OnDeflect);
            EventBus.Unsubscribe<SwordImpact>(OnImpact);
            EventBus.Unsubscribe<PlayerHit>(OnPlayerHit);
            EventBus.Unsubscribe<LandingRequested>(OnLanding);
            EventBus.Unsubscribe<ZoneCompleted>(OnExtracted);
            EventBus.Unsubscribe<GameModeChanged>(OnModeChanged);
            EventBus.Unsubscribe<ShipWeaponFired>(OnShipFired);
            EventBus.Unsubscribe<ProjectileImpact>(OnBoltImpact);
            EventBus.Unsubscribe<EnemyShipDestroyed>(OnEnemyDestroyed);
            EventBus.Unsubscribe<PlayerShipDamaged>(OnPlayerShipDamaged);
            EventBus.Unsubscribe<SpaceEncounterStarted>(OnEncounterStarted);
            EventBus.Unsubscribe<SpaceEncounterCleared>(OnEncounterCleared);
            EventBus.Unsubscribe<AttackWindupStarted>(OnAttackWindup);
            EventBus.Unsubscribe<EntityDied>(OnEntityDied);
            EventBus.Unsubscribe<AbilityActivated>(OnAbilityActivated);
            EventBus.Unsubscribe<BoonChosen>(OnBoonChosen);
            EventBus.Unsubscribe<RunNodeEntered>(OnRunNodeEntered);
        }

        private void Update()
        {
            UpdateRunMusic();
            ambienceLevel = StepCrossfade(ambience, ambienceTarget, ambienceVolume, ambienceFade, ambienceLevel);
            musicLevel = StepCrossfade(music, musicTarget, musicVolume, musicFade, musicLevel);
            UpdateEngine();
        }

        /// <summary>
        /// Roguelike run explore/combat crossfade: while a run is active, the music bed simply tracks
        /// <see cref="CombatActivity.OnFootAggro"/> (already maintained by every MeleeAttacker's FSM,
        /// zero extra bookkeeping) — no aggro'd enemy means explore, any aggro'd enemy means combat.
        /// Gated on <see cref="RunState.InRun"/> so campaign chapters (which drive music via
        /// <see cref="OnModeChanged"/>/space-encounter events instead) are untouched. A static bool
        /// read; no allocation.
        /// </summary>
        private void UpdateRunMusic()
        {
            if (!RunState.InRun) return;
            musicTarget = CombatOrExploreMusic(CombatActivity.OnFootAggro, exploreMusic, combatMusic);
        }

        /// <summary>Pure decision: which music bed the run should be on right now. A7.10-style test seam.</summary>
        internal static AudioClip CombatOrExploreMusic(bool aggro, AudioClip exploreMusic, AudioClip combatMusic) =>
            aggro ? combatMusic : exploreMusic;

        /// <summary>
        /// Drive a looping bed toward <paramref name="target"/>: if the playing clip isn't the
        /// desired one, fade fully out, swap, then fade in; otherwise hold at <paramref name="volume"/>
        /// (or 0 when there's no clip). Returns the updated smoothed level.
        /// </summary>
        private static float StepCrossfade(AudioSource src, AudioClip target, float volume, float fade, float level)
        {
            bool needSwap = src.clip != target;
            float dest = needSwap ? 0f : (target != null ? volume : 0f);
            float rate = fade > 0f ? (volume / fade) : volume;
            level = Mathf.MoveTowards(level, dest, rate * Time.unscaledDeltaTime);
            src.volume = level;

            if (needSwap && level <= 0.001f)
            {
                src.clip = target;
                if (target != null) src.Play();
                else src.Stop();
            }
            return level;
        }

        /// <summary>Engine hum whose volume + pitch track ship speed; only while flying.</summary>
        private void UpdateEngine()
        {
            if (currentMode == GameMode.SpaceFlight)
            {
                if (ship == null) ship = ShipController.Instance;
            }
            else ship = null;

            if (engine.clip != engineLoop)
            {
                engine.clip = engineLoop;
                if (engineLoop != null) engine.Play();
                else engine.Stop();
            }

            float targetVol = 0f, targetPitch = 1f;
            if (ship != null && engineLoop != null && engineVolume > 0f)
            {
                float t = ship.MaxSpeed > 0f ? Mathf.Clamp01(Mathf.Abs(ship.CurrentSpeed) / ship.MaxSpeed) : 0f;
                targetVol = engineVolume * (EngineIdleFraction + (1f - EngineIdleFraction) * t);
                targetPitch = Mathf.Lerp(EnginePitchIdle, EnginePitchFull, t);
            }

            float rate = engineVolume > 0f ? engineVolume / 0.3f : 1f; // ~0.3s to full
            engineLevel = Mathf.MoveTowards(engineLevel, targetVol, rate * Time.unscaledDeltaTime);
            engine.volume = engineLevel;
            engine.pitch = Mathf.Lerp(engine.pitch, targetPitch, 8f * Time.unscaledDeltaTime);
        }

        private void OnDeflect(SwordDeflected e) => PlayAt(swordDeflect, e.Point);
        private void OnImpact(SwordImpact e) => PlayAt(swordImpact, e.Point);
        private void OnPlayerHit(PlayerHit e) => PlayAt(playerHit, e.Point);
        private void OnLanding(LandingRequested _) => PlayAt(landing, ListenerPoint());
        private void OnExtracted(ZoneCompleted _) => PlayAt(extraction, ListenerPoint());

        private void OnAttackWindup(AttackWindupStarted e) => PlayAt(attackWindup, e.Point);

        // A7.4-style: don't fire a "kill" thud on the player's own death — that has its own game-over
        // flow (RunDirector/GameFlowManager); this is enemy-defeated combat feedback only.
        private void OnEntityDied(EntityDied e)
        {
            var playerObj = VRRig.Instance != null ? VRRig.Instance.gameObject : null;
            if (playerObj != null && e.Entity == playerObj) return;
            PlayAt(enemyDefeated, e.Entity != null ? e.Entity.transform.position : ListenerPoint());
        }

        private void OnAbilityActivated(AbilityActivated _) => PlayAt(abilityActivate, ListenerPoint());
        private void OnBoonChosen(BoonChosen _) => PlayAt(boonChosen, ListenerPoint());

        /// <summary>Sets the arena's looping ambience bed to the entered node's sector theme (rust /
        /// program / garden — see <see cref="SectorAmbience"/>); Update's StepCrossfade handles the
        /// actual fade-out/swap/fade-in.</summary>
        private void OnRunNodeEntered(RunNodeEntered e) =>
            ambienceTarget = SectorAmbience(e.Sector, sectorAmbienceRust, sectorAmbienceProgram, sectorAmbienceGarden);

        /// <summary>Pure sector -> ambience clip mapping (0 = rust, 1 = program, 2 = garden, matching
        /// ArenaRoomLibrary's biome order); wraps like ArenaRoomLibrary.ForSector so an out-of-range
        /// sector never throws. A7.10-style test seam.</summary>
        internal static AudioClip SectorAmbience(int sector, AudioClip rust, AudioClip program, AudioClip garden)
        {
            const int SectorCount = 3;
            int index = ((sector % SectorCount) + SectorCount) % SectorCount;
            return index switch
            {
                0 => rust,
                1 => program,
                _ => garden,
            };
        }

        private void OnShipFired(ShipWeaponFired e) => PlayAt(shipGunFire, e.WorldPoint);
        private void OnBoltImpact(ProjectileImpact e) => PlayAt(boltImpact, e.WorldPoint);
        private void OnPlayerShipDamaged(PlayerShipDamaged e) => PlayAt(shipHullHit, e.WorldPoint);
        private void OnEnemyDestroyed(EnemyShipDestroyed e) =>
            PlayAt(enemyExplosion, e.Ship != null ? e.Ship.transform.position : ListenerPoint());

        // Combat music swaps in for the duration of an encounter, then back to the explore bed.
        private void OnEncounterStarted(SpaceEncounterStarted _) => musicTarget = combatMusic;
        private void OnEncounterCleared(SpaceEncounterCleared _) => musicTarget = exploreMusic;

        private void OnModeChanged(GameModeChanged e)
        {
            currentMode = e.Current;
            // Set the desired beds; Update() handles the fade-out/swap/fade-in. The explore bed is
            // the default music outside an active encounter (encounter events override it).
            ambienceTarget = e.Current switch
            {
                GameMode.SpaceFlight => flightAmbience,
                GameMode.OnFoot => onFootAmbience,
                _ => ambienceTarget
            };
            if (e.Current == GameMode.SpaceFlight || e.Current == GameMode.OnFoot)
                musicTarget = exploreMusic;
        }

        private void PlayAt(AudioClip clip, Vector3 point)
        {
            if (clip == null || sfxVolume <= 0f) return;
            oneShots.Play(clip, point, sfxVolume);
        }

        /// <summary>World point near the listener, so non-positional cues (landing/extract) read as present.</summary>
        private static Vector3 ListenerPoint()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position : Vector3.zero;
        }
    }
}
